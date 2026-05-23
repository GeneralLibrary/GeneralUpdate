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
    /// <para>
    /// Wraps the existing strategy layer and parallelizes per-file diff operations
    /// using a throttled producer-consumer pattern via <see cref="SemaphoreSlim"/>.
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// var pipeline = new DiffPipeline(new DiffPipelineOptions { MaxDegreeOfParallelism = 8 });
    /// var reporter = new Progress&lt;DiffProgress&gt;(p => Console.WriteLine(p));
    /// await pipeline.CleanAsync(src, tgt, patch, reporter, ct);
    /// </code>
    /// </para>
    /// </summary>
    public class DiffPipeline
    {
        private readonly DiffPipelineOptions _options;
        private readonly IBinaryDiffer _binaryDiffer;

        private const string PatchExtension = ".patch";
        private const string DeleteListFileName = "generalupdate_delete_files.json";

        /// <summary>
        /// Initialises a new pipeline with the specified options and a default <see cref="StreamingHdiffDiffer"/>.
        /// </summary>
        public DiffPipeline(DiffPipelineOptions? options = null)
            : this(options ?? new DiffPipelineOptions(), new Differ.StreamingHdiffDiffer())
        {
        }

        /// <summary>
        /// Initialises a new pipeline with the specified options and binary differ.
        /// </summary>
        public DiffPipeline(DiffPipelineOptions options, IBinaryDiffer binaryDiffer)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _binaryDiffer = binaryDiffer ?? throw new ArgumentNullException(nameof(binaryDiffer));
        }

        #region Clean (Patch Generation)

        /// <summary>
        /// Compares source and target directories, generating patch files in parallel.
        /// </summary>
        /// <param name="sourcePath">Old-version directory.</param>
        /// <param name="targetPath">New-version directory.</param>
        /// <param name="patchPath">Output patch directory.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task CleanAsync(
            string sourcePath,
            string targetPath,
            string patchPath,
            IProgress<DiffProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateDirectories(sourcePath, targetPath, patchPath);

            var storageManager = new StorageManager();
            var comparisonResult = storageManager.Compare(sourcePath, targetPath);
            var differentFiles = comparisonResult.DifferentNodes.ToList();
            var leftNodes = comparisonResult.LeftNodes.ToList();

            int total = differentFiles.Count;
            if (total == 0)
            {
                progress?.Report(DiffProgress.Complete(0));
                return;
            }

            int completed = 0;
            // Per-file concurrency limit
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
                        // New file 鈥?copy directly
                        File.Copy(file.FullName, Path.Combine(tempDir, Path.GetFileName(file.FullName)), true);
                    }

                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new DiffProgress(done, total, file.Name));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new DiffProgress(done, total, file.Name, ex.Message));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));

            await Task.WhenAll(tasks);

            // Write delete list
            var exceptFiles = storageManager.Except(sourcePath, targetPath)?.ToList();
            if (exceptFiles is { Count: > 0 })
            {
                var deletePath = Path.Combine(patchPath, DeleteListFileName);
                StorageManager.CreateJson(deletePath, exceptFiles, FileNodesJsonContext.Default.ListFileNode);
            }

            progress?.Report(DiffProgress.Complete(total));
        }

        #endregion Clean

        #region Dirty (Patch Application)

        /// <summary>
        /// Applies patches from patchPath to appPath in parallel where safe.
        /// </summary>
        /// <param name="appPath">Application directory to patch.</param>
        /// <param name="patchPath">Patch directory.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task DirtyAsync(
            string appPath,
            string patchPath,
            IProgress<DiffProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

            var skipDirectory = BlackListManager.Instance.SkipDirectorys.ToList();
            var patchFiles = StorageManager.GetAllFiles(patchPath, skipDirectory).ToList();
            var oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory).ToList();

            // Phase 1: Handle delete list (sequential 鈥?modifies file collection)
            HandleDeleteList(patchFiles, oldFiles);

            // Refresh after deletions
            oldFiles = StorageManager.GetAllFiles(appPath, skipDirectory).ToList();

            // Phase 2: Parallel patch application for independent files
            int total = oldFiles.Count;
            if (total == 0)
            {
                progress?.Report(DiffProgress.Complete(0));
                // Still copy new files
                await CopyUnknownFiles(appPath, patchPath);
                return;
            }

            int completed = 0;
            var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

            // Match old files to patches (fast, sequential)
            var matchedPairs = new List<(FileInfo OldFile, FileInfo PatchFile)>();
            foreach (var oldFile in oldFiles)
            {
                var patchFile = patchFiles.FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f.Name);
                    // Strip .patch extension from patch file name
                    if (name.EndsWith(".patch", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 6);
                    return name.Equals(oldFile.Name, StringComparison.OrdinalIgnoreCase);
                });

                if (patchFile != null)
                    matchedPairs.Add((oldFile, patchFile));
            }

            // Apply patches in parallel
            var tasks = matchedPairs.Select(pair => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ApplyPatch(pair.OldFile.FullName, pair.PatchFile.FullName, cancellationToken);

                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new DiffProgress(done, total, pair.OldFile.Name));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new DiffProgress(done, total, pair.OldFile.Name, ex.Message));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));

            await Task.WhenAll(tasks);
            progress?.Report(DiffProgress.Complete(total));

            // Phase 3: Copy new/unknown files (sequential)
            await CopyUnknownFiles(appPath, patchPath);
        }

        #endregion Dirty

        #region Private Helpers

        private static async Task ApplyPatch(string appFilePath, string patchFilePath, CancellationToken ct)
        {
            if (!File.Exists(appFilePath) || !File.Exists(patchFilePath)) return;

            var tempPath = Path.Combine(
                Path.GetDirectoryName(appFilePath)!,
                $"{Path.GetRandomFileName()}_{Path.GetFileName(appFilePath)}");

            var handler = new Binary.BinaryHandler(); // Use default BZip2 handler for reading patches
            await handler.DirtyAsync(appFilePath, tempPath, patchFilePath, ct);
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

        private static async Task CopyUnknownFiles(string appPath, string patchPath)
        {
            await Task.Run(() =>
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

        #endregion Private Helpers
    }
}
