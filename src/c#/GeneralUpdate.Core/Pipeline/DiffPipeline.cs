using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Models;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Differ;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Parallel differential pipeline supporting configurable parallelism, progress reporting, pluggable matchers,
/// and cancellation tokens. Provides "Clean" (generate patches) and "Dirty" (apply patches) operation modes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipeline"/> is the core execution engine of the GeneralUpdate differential update mechanism.
/// It uses binary differ algorithms (such as HDiffPatch) to generate and apply binary differential patches
/// between application files, significantly reducing the size of update packages.
/// </para>
/// <para>
/// Two primary operation modes:
/// <list type="table">
///   <listheader>
///     <term>Mode</term>
///     <description>Method</description>
///     <description>Description</description>
///   </listheader>
///   <item>
///     <term>Clean Mode</term>
///     <description><see cref="CleanAsync"/></description>
///     <description>
///       Compares the old version (source) and new version (target) directories, generating .patch files for
///       files that have changed. New files are copied directly, and deleted files are recorded in a deletion
///       manifest. This mode is used on the server/publishing side.
///     </description>
///   </item>
///   <item>
///     <term>Dirty Mode</term>
///     <description><see cref="DirtyAsync"/></description>
///     <description>
///       Applies patch files to the client's old version files in parallel, producing updated files.
///       This mode is used during client-side updates.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="DiffPipelineBuilder"/> for fluent configuration, or instantiate directly via constructors.
/// Both operations support concurrency control via <see cref="SemaphoreSlim"/> (configured through
/// <see cref="DiffPipelineOptions.MaxDegreeOfParallelism"/>), file-level progress reporting via
/// <see cref="IProgress{DiffProgress}"/>, and cancellation via <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// File processing strategy:
/// <list type="bullet">
///   <item><description>Changed files: Generate/apply binary patches.</description></item>
///   <item><description>New files: Copy directly.</description></item>
///   <item><description>Deleted files: Recorded in <c>generalupdate_delete_files.json</c>; removed during dirty mode execution.</description></item>
///   <item><description>Unchanged files: Skipped.</description></item>
/// </list>
/// </para>
/// </remarks>
public class DiffPipeline
{
    private readonly DiffPipelineOptions _options;
    private readonly IBinaryDiffer _binaryDiffer;
    private readonly ICleanMatcher _cleanMatcher;
    private readonly IDirtyMatcher _dirtyMatcher;
    private readonly IProgress<DiffProgress>? _progress;

    private const string PatchExtension = ".patch";
    private const string DeleteListFileName = "generalupdate.delete.json";

    /// <summary>
    /// Initializes a new pipeline instance with default options, default binary differ
    /// (<see cref="StreamingHdiffDiffer"/>), and default matchers.
    /// </summary>
    /// <remarks>
    /// This constructor is suitable for most scenarios and requires no additional configuration.
    /// The default parallelism is 2 (via <see cref="DiffPipelineOptions"/> defaults),
    /// and the default binary differ is <see cref="StreamingHdiffDiffer"/>.
    /// </remarks>
    public DiffPipeline()
        : this(new DiffPipelineOptions(), new StreamingHdiffDiffer(), null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new pipeline instance with the specified options and the default binary differ.
    /// </summary>
    /// <param name="options">The pipeline options for configuring parameters such as parallelism. Must not be <c>null</c>.</param>
    /// <remarks>
    /// Suitable for scenarios that require custom parallelism or error handling strategies while using
    /// the default binary differ algorithm.
    /// </remarks>
    public DiffPipeline(DiffPipelineOptions options)
        : this(options, new StreamingHdiffDiffer(), null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new pipeline instance with full configuration.
    /// </summary>
    /// <param name="options">The pipeline options containing parallelism, error handling, and other settings. Must not be <c>null</c>.</param>
    /// <param name="binaryDiffer">The binary differ responsible for generating and applying binary patches. Must not be <c>null</c>.</param>
    /// <param name="cleanMatcher">
    /// The file matcher used during the Clean phase (<see cref="CleanAsync"/>). Compares file nodes between
    /// the old and new directories. If <c>null</c>, <see cref="DefaultCleanMatcher"/> is used.
    /// </param>
    /// <param name="dirtyMatcher">
    /// The file matcher used during the Dirty phase (<see cref="DirtyAsync"/>). Matches patch files to their
    /// corresponding old version files. If <c>null</c>, <see cref="DefaultDirtyMatcher"/> is used.
    /// </param>
    /// <param name="progress">
    /// An optional progress reporter for receiving file-level processing progress updates.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="binaryDiffer"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This constructor is suitable for advanced scenarios requiring full control over the binary differ,
    /// matchers, and progress reporting. The fluent API provided by <see cref="DiffPipelineBuilder"/>
    /// is recommended for configuration.
    /// </remarks>
    public DiffPipeline(
        DiffPipelineOptions options,
        IBinaryDiffer binaryDiffer,
        ICleanMatcher? cleanMatcher = null,
        IDirtyMatcher? dirtyMatcher = null,
        IProgress<DiffProgress>? progress = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _binaryDiffer = binaryDiffer ?? throw new ArgumentNullException(nameof(binaryDiffer));
        _cleanMatcher = cleanMatcher ?? new DefaultCleanMatcher();
        _dirtyMatcher = dirtyMatcher ?? new DefaultDirtyMatcher();
        _progress = progress;
    }

    /// <summary>
    /// Initializes a new pipeline instance with the specified options, binary differ, and progress reporter
    /// (backward-compatible constructor).
    /// </summary>
    /// <param name="options">The pipeline options. Must not be <c>null</c>.</param>
    /// <param name="binaryDiffer">The binary differ. Must not be <c>null</c>.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <remarks>
    /// This constructor is provided only for binary compatibility. New code should use the overload that
    /// accepts <c>ICleanMatcher</c> and <c>IDirtyMatcher</c> parameters.
    /// </remarks>
    public DiffPipeline(DiffPipelineOptions options, IBinaryDiffer binaryDiffer, IProgress<DiffProgress>? progress = null)
        : this(options, binaryDiffer, null, null, progress)
    {
    }

    /// <summary>
    /// Compares the source directory (old version) and target directory (new version), generating differential
    /// patches in parallel for files that have changed.
    /// </summary>
    /// <param name="sourcePath">The old version application directory path. This directory must exist.</param>
    /// <param name="targetPath">The new version application directory path. This directory must exist.</param>
    /// <param name="patchPath">The patch file output directory path. Created automatically if it does not exist.</param>
    /// <param name="progress">An optional progress reporter that overrides the one set in the constructor. Receives file-level processing progress updates.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the ongoing patch generation operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sourcePath"/>, <paramref name="targetPath"/>, or <paramref name="patchPath"/> is <c>null</c> or whitespace.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the directory specified by <paramref name="sourcePath"/> or <paramref name="targetPath"/> does not exist.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Detailed workflow:
    /// <list type="number">
    ///   <item><description>Validates that the input directories exist.</description></item>
    ///   <item><description>Uses <see cref="ICleanMatcher.Compare"/> to compare the old and new directories,
    ///         identifying changed files (DifferentNodes) and new files (LeftNodes).</description></item>
    ///   <item><description>For each changed file: computes the relative path, creates a temporary directory,
    ///         and uses <see cref="IBinaryDiffer.CleanAsync"/> to generate a .patch file.</description></item>
    ///   <item><description>For each new file: copies it directly to the corresponding location in the patch output directory.</description></item>
    ///   <item><description>Generates a <c>generalupdate_delete_files.json</c> manifest recording files that have been
    ///         deleted from the new version (no longer present in the old version).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// File processing is controlled by a <see cref="SemaphoreSlim"/> with the maximum concurrency determined
    /// by <see cref="DiffPipelineOptions.MaxDegreeOfParallelism"/>. If <see cref="DiffPipelineOptions.StopOnFirstError"/>
    /// is <c>false</c> (default), failure of an individual file does not affect processing of other files,
    /// and error details are passed through the progress reporting mechanism. If <c>true</c>, any file failure
    /// immediately terminates all processing.
    /// </para>
    /// </remarks>
    public async Task CleanAsync(
        string sourcePath,
        string targetPath,
        string patchPath,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var reporter = progress ?? _progress;
        ValidateDirectories(sourcePath, targetPath, patchPath);

        var comparisonResult = _cleanMatcher.Compare(sourcePath, targetPath);
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
                var oldFile = _cleanMatcher.Match(file, leftNodes);

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

        var exceptFiles = _cleanMatcher.Except(sourcePath, targetPath)?.ToList();
        if (exceptFiles is { Count: > 0 })
        {
            var deletePath = Path.Combine(patchPath, DeleteListFileName);
            StorageManager.CreateJson(deletePath, exceptFiles, FileNodesJsonContext.Default.ListFileNode);
        }

        reporter?.Report(DiffProgress.Complete(total));
    }

    /// <summary>
    /// Applies patch files from <paramref name="patchPath"/> to the old version files in <paramref name="appPath"/> in parallel.
    /// </summary>
    /// <param name="appPath">The application installation directory (containing old version files).</param>
    /// <param name="patchPath">The directory containing patch files.</param>
    /// <param name="progress">An optional progress reporter that overrides the one set in the constructor.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the ongoing patch application operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// Detailed workflow:
    /// <list type="number">
    ///   <item><description>If <paramref name="appPath"/> or <paramref name="patchPath"/> does not exist, returns immediately.</description></item>
    ///   <item><description>Scans all files in the patch directory (skipping blacklisted directories), finds the
    ///         <c>generalupdate_delete_files.json</c> file, and performs file deletion.</description></item>
    ///   <item><description>Uses <see cref="IDirtyMatcher.Match"/> to pair patch files with their corresponding old version files.</description></item>
    ///   <item><description>For each matched file pair, safely applies the patch using a temporary file strategy:
    ///         first writes the patch result to a temporary file, then on success deletes the original file and
    ///         moves the temporary file to the original location, ensuring failures during application do not
    ///         corrupt the original file.</description></item>
    ///   <item><description>Copies all unknown/new files not present in the patch manifest to the application directory.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Deletion manifest handling details:
    /// If the patch directory contains a <c>generalupdate_delete_files.json</c> file, this file records the
    /// SHA256 hash values of files that have been deleted in the new version. The system identifies and removes
    /// these files by comparing the recorded hash values with the SHA256 hash of each current file.
    /// </para>
    /// <para>
    /// Temporary file strategy:
    /// The <see cref="ApplyPatch"/> method uses a temporary file name of the format <c>{randomFileName}_{originalFileName}</c>.
    /// The original file is only replaced after the patch has been successfully applied. This strategy minimizes
    /// the risk of data loss in the event of an application failure.
    /// </para>
    /// <para>
    /// Finally, <see cref="CopyUnknownFiles"/> cleans up the patch directory and copies all new files to the
    /// application directory.
    /// </para>
    /// </remarks>
    public async Task DirtyAsync(
        string appPath,
        string patchPath,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var reporter = progress ?? _progress;
        if (!Directory.Exists(appPath) || !Directory.Exists(patchPath)) return;

        var skipDirectory = BlackListDefaults.DefaultSkipDirectories;
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
            var patchFile = _dirtyMatcher.Match(oldFile, patchFiles);
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

    /// <summary>
    /// Safely applies a single patch file to the corresponding application file using a temporary file strategy.
    /// </summary>
    /// <param name="appFilePath">The full path to the application file to update.</param>
    /// <param name="patchFilePath">The full path to the patch file.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <remarks>
    /// <para>
    /// This method executes the following steps:
    /// <list type="number">
    ///   <item><description>Checks whether the application file and patch file both exist; skips if either does not exist.</description></item>
    ///   <item><description>Creates a temporary file in the same directory as the application file
    ///         (file name format: <c>{randomFileName}_{originalFileName}</c>).</description></item>
    ///   <item><description>Calls <see cref="IBinaryDiffer.DirtyAsync"/> to apply the patch to the original file,
    ///         writing output to the temporary file.</description></item>
    ///   <item><description>If the patch application succeeds, deletes the original file and moves the temporary
    ///         file to the original file location.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This "write to temp file then replace original" strategy ensures that if a failure occurs during
    /// patch application, the original file is not corrupted or lost.
    /// </para>
    /// </remarks>
    private async Task ApplyPatch(string appFilePath, string patchFilePath, CancellationToken ct)
    {
        if (!File.Exists(appFilePath) || !File.Exists(patchFilePath)) return;

        var tempPath = Path.Combine(
            Path.GetDirectoryName(appFilePath)!,
            $"{Path.GetRandomFileName()}_{Path.GetFileName(appFilePath)}");

        await _binaryDiffer.DirtyAsync(appFilePath, tempPath, patchFilePath, ct);

        if (File.Exists(tempPath))
        {
            if (File.Exists(appFilePath))
            {
                File.SetAttributes(appFilePath, FileAttributes.Normal);
                File.Delete(appFilePath);
            }
            File.Move(tempPath, appFilePath);
        }
    }

    /// <summary>
    /// Processes the deletion manifest (generalupdate_delete_files.json) and removes obsolete files from the application directory.
    /// </summary>
    /// <param name="patchFiles">The list of files in the patch directory.</param>
    /// <param name="oldFiles">The list of files in the application directory.</param>
    /// <remarks>
    /// <para>
    /// This method locates the <c>generalupdate_delete_files.json</c> file in the patch directory,
    /// which contains a list of SHA256 hash values for files that have been deleted in the new version.
    /// It then scans each file in the application directory, computes its SHA256 hash, and compares it
    /// against the values in the manifest. Matching files are deleted.
    /// </para>
    /// <para>
    /// Note: Before deletion, file attributes are reset to <see cref="FileAttributes.Normal"/> to prevent
    /// deletion failures caused by read-only attributes.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Copies new files from the patch directory (files not present in the old version) to the application
    /// directory, then cleans up the patch directory.
    /// </summary>
    /// <param name="appPath">The application directory path.</param>
    /// <param name="patchPath">The patch directory path.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method performs the following operations:
    /// <list type="number">
    ///   <item><description>Compares the application directory and patch directory to identify files that are new
    ///         in the patch directory.</description></item>
    ///   <item><description>Filters out files with blacklisted formats (e.g., executable file extensions).</description></item>
    ///   <item><description>Copies the new files to the corresponding locations in the application directory,
    ///         automatically creating any missing subdirectories.</description></item>
    ///   <item><description>Finally, deletes the entire patch directory to complete cleanup.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This step is executed in the final phase of the dirty mode to ensure all new files are correctly
    /// merged into the application directory.
    /// </para>
    /// </remarks>
    private static Task CopyUnknownFiles(string appPath, string patchPath)
    {
        return Task.Run(() =>
        {
            var fileManager = new StorageManager();
            var comparisonResult = fileManager.Compare(appPath, patchPath);
            foreach (var file in comparisonResult.DifferentNodes)
            {
                var extensionName = Path.GetExtension(file.FullName);
                if (BlackListDefaults.DefaultBlackFormats.Contains(extensionName)) continue;

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

    /// <summary>
    /// Computes the temporary subdirectory path in the patch output directory for a given target file.
    /// </summary>
    /// <param name="file">The file node currently being processed.</param>
    /// <param name="targetPath">The target (new version) directory path.</param>
    /// <param name="patchPath">The patch output directory path.</param>
    /// <returns>The full path to the file's temporary subdirectory. If the file is at the root of the target directory, returns the patch directory path.</returns>
    /// <remarks>
    /// This method computes the relative path by replacing the target directory portion of the file's full path
    /// with the patch directory portion. If the directory does not exist, it is created automatically.
    /// This preserves the directory structure in the patch output directory to match the target directory.
    /// </remarks>
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

    /// <summary>
    /// Validates that the input directories exist and are not null or empty.
    /// </summary>
    /// <param name="sourcePath">The source (old version) directory path.</param>
    /// <param name="targetPath">The target (new version) directory path.</param>
    /// <param name="patchPath">The patch output directory path.</param>
    /// <exception cref="ArgumentNullException">Thrown when any path is <c>null</c> or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory or target directory does not exist.</exception>
    /// <remarks>
    /// This validation is called only at the start of <see cref="CleanAsync"/>. It ensures that all required
    /// input directories are ready, preventing failures due to invalid paths during execution.
    /// </remarks>
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
