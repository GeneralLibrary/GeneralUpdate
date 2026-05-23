using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.HashAlgorithms;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Models;

namespace GeneralUpdate.Differential.Pipeline
{
    /// <summary>
    /// Parallel differential pipeline with configurable parallelism, progress reporting, and cancellation.
    /// </summary>
    /// <remarks>
    /// Wraps the existing strategy layer and parallelizes per-file diff operations
    /// using a throttled producer-consumer pattern via <see cref="SemaphoreSlim"/>.
    /// Use <see cref="DiffPipelineBuilder"/> for fluent configuration.
    /// </remarks>
    public class DiffPipeline
    {
        private readonly DiffPipelineOptions _options;
        private readonly IBinaryDiffer _binaryDiffer;
        private readonly IProgress<DiffProgress>? _progress;
        private const string PatchExtension = ".patch";
        private const string DeleteListFileName = "generalupdate_delete_files.json";

        /// <summary>
        /// Initialises a new pipeline with default options (<see cref="Differ.StreamingHdiffDiffer"/>).
        /// </summary>
        public DiffPipeline()
            : this(new DiffPipelineOptions(), new Differ.StreamingHdiffDiffer(), null)
        {
        }

        /// <summary>
        /// Initialises a new pipeline with the specified options.
        /// </summary>
        public DiffPipeline(DiffPipelineOptions options)
            : this(options, new Differ.StreamingHdiffDiffer(), null)
        {
        }

        /// <summary>
        /// Initialises a new pipeline with the specified options, differ, and optional progress reporter.
        /// </summary>
        public DiffPipeline(DiffPipelineOptions options, IBinaryDiffer binaryDiffer, IProgress<DiffProgress>? progress = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _binaryDiffer = binaryDiffer ?? throw new ArgumentNullException(nameof(binaryDiffer));
            _progress = progress;
        }

        /// <summary>
        /// Compares source and target directories, generating patch files in parallel.
        /// </summary>
        public async Task CleanAsync(
            string sourcePath,
            string targetPath,
            string patchPath,
            IProgress<DiffProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var reporter = progress ?? _progress;
            ValidateDirectories(sourcePath, targetPath, patchPath);

            var storageManager = new StorageManager();
            var comparisonResult = storageManager.Compare(sourcePath, targetPath);
            var differentFiles = comparisonResult.DifferentNodes.ToList();
            var leftNodes = comparisonResult.LeftNodes.ToList();

            int total = differentFiles.Count;
            if (total == 0)
            {
                reporter?.Report(DiffProgress.Complete(0));
                return;
            }

            int completed = 0;
            var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

            var tasks = differentFiles.Select(file => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tempDir = GetTempDirectory(file, targetPath, patchPath);
                    var oldFile = FindMatchingFile(file, leftNodes);

                    if (oldFile != null)
                    {
                        if (!StorageManager.HashEquals(oldFile.FullName, file.FullName))
                        {
                            var tempPatchPath = Path.Combine(tempDir, $"{file.Name}{PatchExtension}");
                            await _binaryDiffer.CleanAsync(oldFile.FullName, file.FullName, tempPatchPath, cancellationToken);
                        }
                    }
                    else
                    {
                        File.Copy(file.FullName, Path.Combine(tempDir, Path.GetFileName(file.FullName)), true);
                    }

                    int done = Interlocked.Increment(ref completed);
                    reporter?.Report(new DiffProgress(done, total, file.Name));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!_options.StopOnFirstError)
                {
                    int done = Interlocked.Increment(ref completed);
                    reporter?.Report(new DiffProgress(done, total, file.Name, ex.Message));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));

            await Task.WhenAll(tasks);

            var exceptFiles = storageManager.Except(sourcePath, targetPath)?.ToList();
            if (exceptFiles is { Count: > 0 })
            {
                var deletePath = Path.Combine(patchPath, DeleteListFileName);
                StorageManager.CreateJson(deletePath, exceptFiles, FileNodesJsonContext.Default.ListFileNode);
            }

            reporter?.Report(DiffProgress.Complete(total));
        }

        /// <summary>
        /// Applies patches from patchPath to appPath in parallel.
        /// </summary>
        public async Task DirtyAsync(
            string appPath,
            string patchPath,
            IProgress<DiffProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var reporter = progress ?? _progress;
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

            var skipDirectory = BlackListManager.Instance.SkipDirectorys.ToList();
            var patchFiles = StorageManager.GetAllFiles(patchPath, skipDirectory).ToList();
            var oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory).ToList();

            HandleDeleteList(patchFiles, oldFiles);
            oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory).ToList();

            int total = oldFiles.Count;
            if (total == 0)
            {
                reporter?.Report(DiffProgress.Complete(0));
                await CopyUnknownFiles(appPath, patchPath);
                return;
            }

            int completed = 0;
            var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

            var matchedPairs = new List<(FileInfo OldFile, FileInfo PatchFile)>();
            foreach (var oldFile in oldFiles)
            {
                var patchFile = patchFiles.FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f.Name);
                    if (name.EndsWith(".patch", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 6);
                    return name.Equals(oldFile.Name, StringComparison.OrdinalIgnoreCase);
                });

                if (patchFile != null)
                    matchedPairs.Add((oldFile, patchFile));
            }

            var tasks = matchedPairs.Select(pair => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ApplyPatch(pair.OldFile.FullName, pair.PatchFile.FullName, cancellationToken);

                    int done = Interlocked.Increment(ref completed);
                    reporter?.Report(new DiffProgress(done, total, pair.OldFile.Name));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!_options.StopOnFirstError)
                {
                    int done = Interlocked.Increment(ref completed);
                    reporter?.Report(new DiffProgress(done, total, pair.OldFile.Name, ex.Message));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));

            await Task.WhenAll(tasks);
            reporter?.Report(DiffProgress.Complete(total));
            await CopyUnknownFiles(appPath, patchPath);
        }

        private async Task ApplyPatch(string appFilePath, string patchFilePath, CancellationToken ct)
        {
            if (!File.Exists(appFilePath) || !File.Exists(patchFilePath)) return;

            var tempPath = Path.Combine(
                Path.GetDirectoryName(appFilePath)!,
                $"{Path.GetRandomFileName()}_{Path.GetFileName(appFilePath)}");

            await _binaryDiffer.DirtyAsync(appFilePath, tempPath, patchFilePath, ct);

            // Atomic replacement
            if (File.Exists(appFilePath))
            {
                File.SetAttributes(appFilePath, FileAttributes.Normal);
                File.Delete(appFilePath);
            }
            File.Move(tempPath, appFilePath);
        }

        private static void HandleDeleteList(IEnumerable<FileInfo> patchFiles, IEnumerable<FileInfo> oldFiles)
        {
            var json = patchFiles.FirstOrDefault(i => i.Name.Equals(DeleteListFileName, StringComparison.OrdinalIgnoreCase));
            if (json == null) return;

            var deleteFiles = StorageManager.GetJson<List<FileNode>>(json.FullName, FileNodesJsonContext.Default.ListFileNode);
            if (deleteFiles == null) return;

            var hashAlgorithm = new Sha256HashAlgorithm();
            var toDelete = oldFiles
                .Where(old => deleteFiles.Any(del => del.Hash.SequenceEqual(hashAlgorithm.ComputeHash(old.FullName))))
                .ToList();

            foreach (var file in toDelete)
            {
                if (!File.Exists(file.FullName)) continue;
                File.SetAttributes(file.FullName, FileAttributes.Normal);
                File.Delete(file.FullName);
            }
        }

        private static Task CopyUnknownFiles(string appPath, string patchPath)
        {
            return Task.Run(() =>
            {
                var fileManager = new StorageManager();
                var comparisonResult = fileManager.Compare(appPath, patchPath);
                foreach (var file in comparisonResult.DifferentNodes)
                {
                    var extensionName = Path.GetExtension(file.FullName);
                    if (BlackListManager.Instance.IsBlacklisted(extensionName)) continue;

                    var targetFileName = file.FullName.Replace(patchPath, "").TrimStart(Path.DirectorySeparatorChar);
                    var targetPath = Path.Combine(appPath, targetFileName);
                    var parentFolder = Directory.GetParent(targetPath);
                    if (parentFolder?.Exists == false)
                        parentFolder.Create();

                    File.Copy(file.FullName, targetPath, true);
                }

                if (Directory.Exists(patchPath))
                    StorageManager.DeleteDirectory(patchPath);
            });
        }

        private static string GetTempDirectory(FileNode file, string targetPath, string patchPath)
        {
            var tempPath = file.FullName
                .Replace(targetPath, "")
                .Replace(Path.GetFileName(file.FullName), "")
                .Trim(Path.DirectorySeparatorChar);
            var tempDir = string.IsNullOrEmpty(tempPath) ? patchPath : Path.Combine(patchPath, tempPath);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static FileNode? FindMatchingFile(FileNode newFile, IEnumerable<FileNode> leftNodes)
        {
            var match = leftNodes.FirstOrDefault(i =>
                string.Equals(i.Name, newFile.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.RelativePath, newFile.RelativePath, StringComparison.OrdinalIgnoreCase));

            if (match == null) return null;
            if (!File.Exists(match.FullName)) return null;
            if (!File.Exists(newFile.FullName)) return null;
            return match;
        }

        private static void ValidateDirectories(string sourcePath, string targetPath, string patchPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(targetPath)) throw new ArgumentNullException(nameof(targetPath));
            if (string.IsNullOrWhiteSpace(patchPath)) throw new ArgumentNullException(nameof(patchPath));

            if (!Directory.Exists(sourcePath))
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            if (!Directory.Exists(targetPath))
                throw new DirectoryNotFoundException($"Target directory not found: {targetPath}");
        }
    }
}
