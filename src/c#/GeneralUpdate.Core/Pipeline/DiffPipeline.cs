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
/// 并行差异管道，支持可配置的并行度、进度报告、可插拔匹配器和取消令牌。
/// 提供"清理"（生成补丁）和"脏"（应用补丁）两种操作模式。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DiffPipeline"/> 是 GeneralUpdate 差异更新机制的核心执行引擎。
/// 它使用二进制差异算法（如 HDiffPatch）生成和应用程序文件之间的二进制差异补丁，
/// 从而显著减少更新包的大小。
/// </para>
/// <para>
/// 两种主要操作模式：
/// <list type="table">
///   <listheader>
///     <term>模式</term>
///     <description>方法</description>
///     <description>说明</description>
///   </listheader>
///   <item>
///     <term>清理模式（Clean）</term>
///     <description><see cref="CleanAsync"/></description>
///     <description>
///       比较旧版本（source）和新版本（target）目录，为发生变化的文件生成 .patch 补丁文件。
///       新增文件直接复制，删除的文件记录到删除清单中。此模式在服务端/发布端使用。
///     </description>
///   </item>
///   <item>
///     <term>脏模式（Dirty）</term>
///     <description><see cref="DirtyAsync"/></description>
///     <description>
///       将补丁文件并行应用到客户端的旧版本文件上，生成更新后的文件。
///       此模式在客户端更新时使用。
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// 使用 <see cref="DiffPipelineBuilder"/> 进行流畅配置，或直接调用构造函数创建实例。
/// 两种操作均支持通过 <see cref="SemaphoreSlim"/> 控制并发度（通过 <see cref="DiffPipelineOptions.MaxDegreeOfParallelism"/> 配置），
/// 通过 <see cref="IProgress{DiffProgress}"/> 报告文件级进度，
/// 以及通过 <see cref="CancellationToken"/> 支持取消操作。
/// </para>
/// <para>
/// 文件处理策略：
/// <list type="bullet">
///   <item><description>变化的文件：生成/应用二进制补丁。</description></item>
///   <item><description>新增的文件：直接复制。</description></item>
///   <item><description>删除的文件：在 <c>generalupdate_delete_files.json</c> 中记录，脏模式执行时删除。</description></item>
///   <item><description>未变化的文件：跳过处理。</description></item>
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
    private const string DeleteListFileName = "generalupdate_delete_files.json";

    /// <summary>
    /// 使用默认选项、默认差异比较器（<see cref="StreamingHdiffDiffer"/>）和默认匹配器初始化管道实例。
    /// </summary>
    /// <remarks>
    /// 此构造函数适用于大多数场景，无需额外配置即可使用。
    /// 默认并行度为 2（通过 <see cref="DiffPipelineOptions"/> 的默认值），
    /// 默认使用 <see cref="StreamingHdiffDiffer"/> 作为二进制差异算法。
    /// </remarks>
    public DiffPipeline()
        : this(new DiffPipelineOptions(), new StreamingHdiffDiffer(), null, null, null)
    {
    }

    /// <summary>
    /// 使用指定的选项和默认差异比较器初始化管道实例。
    /// </summary>
    /// <param name="options">管道选项，用于配置并行度等参数。不能为 <c>null</c>。</param>
    /// <remarks>
    /// 适用于需要自定义并行度或错误处理策略但使用默认差异算法的场景。
    /// </remarks>
    public DiffPipeline(DiffPipelineOptions options)
        : this(options, new StreamingHdiffDiffer(), null, null, null)
    {
    }

    /// <summary>
    /// 使用完整配置初始化管道实例。
    /// </summary>
    /// <param name="options">管道选项，包含并行度、错误处理等配置。不能为 <c>null</c>。</param>
    /// <param name="binaryDiffer">二进制差异比较器，负责生成和应用二进制补丁。不能为 <c>null</c>。</param>
    /// <param name="cleanMatcher">
    /// 清理阶段（<see cref="CleanAsync"/>）使用的文件匹配器。用于比较新旧目录中的文件节点。
    /// 如果为 <c>null</c>，则使用 <see cref="DefaultCleanMatcher"/>。
    /// </param>
    /// <param name="dirtyMatcher">
    /// 脏阶段（<see cref="DirtyAsync"/>）使用的文件匹配器。用于将补丁文件匹配到对应的旧版本文件。
    /// 如果为 <c>null</c>，则使用 <see cref="DefaultDirtyMatcher"/>。
    /// </param>
    /// <param name="progress">
    /// 可选的进度报告器，用于接收文件级处理进度更新。
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// 当 <paramref name="options"/> 或 <paramref name="binaryDiffer"/> 为 <c>null</c> 时引发。
    /// </exception>
    /// <remarks>
    /// 此构造函数适用于需要完全控制差异比较器、匹配器和进度报告的进阶场景。
    /// 推荐使用 <see cref="DiffPipelineBuilder"/> 的流畅 API 进行配置。
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
    /// 使用指定的选项、差异比较器和进度报告器初始化管道实例（向后兼容构造函数）。
    /// </summary>
    /// <param name="options">管道选项。不能为 <c>null</c>。</param>
    /// <param name="binaryDiffer">二进制差异比较器。不能为 <c>null</c>。</param>
    /// <param name="progress">可选的进度报告器。</param>
    /// <remarks>
    /// 此构造函数仅用于保持二进制兼容性。新代码应使用接受 <c>ICleanMatcher</c> 和
    /// <c>IDirtyMatcher</c> 参数的重载构造函数。
    /// </remarks>
    public DiffPipeline(DiffPipelineOptions options, IBinaryDiffer binaryDiffer, IProgress<DiffProgress>? progress = null)
        : this(options, binaryDiffer, null, null, progress)
    {
    }

    /// <summary>
    /// 比较源目录（旧版本）和目标目录（新版本），为发生变化的文件并行生成差异补丁。
    /// </summary>
    /// <param name="sourcePath">旧版本应用程序目录路径。该目录必须存在。</param>
    /// <param name="targetPath">新版本应用程序目录路径。该目录必须存在。</param>
    /// <param name="patchPath">补丁文件输出目录路径。如果不存在则自动创建。</param>
    /// <param name="progress">可选的进度报告器，覆盖构造函数中设置的进度报告器。用于接收文件级处理进度更新。</param>
    /// <param name="cancellationToken">取消令牌，用于取消正在进行的补丁生成操作。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="ArgumentNullException">
    /// 当 <paramref name="sourcePath"/>、<paramref name="targetPath"/> 或 <paramref name="patchPath"/> 为 <c>null</c> 或空白时引发。
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// 当 <paramref name="sourcePath"/> 或 <paramref name="targetPath"/> 指定的目录不存在时引发。
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// 当通过 <paramref name="cancellationToken"/> 取消操作时引发。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 详细工作流程：
    /// <list type="number">
    ///   <item><description>验证输入目录是否存在。</description></item>
    ///   <item><description>使用 <see cref="ICleanMatcher.Compare"/> 比较新旧目录，识别出变化的文件（DifferentNodes）和新增的文件（LeftNodes）。</description></item>
    ///   <item><description>对每个变化的文件：计算相对路径、创建临时目录、使用 <see cref="IBinaryDiffer.CleanAsync"/> 生成 .patch 文件。</description></item>
    ///   <item><description>对每个新增的文件：直接复制到补丁输出目录的相应位置。</description></item>
    ///   <item><description>生成 <c>generalupdate_delete_files.json</c> 清单，记录旧版本中已删除（不再存在于新版本中）的文件。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 文件处理通过 <see cref="SemaphoreSlim"/> 控制并发度，最大并发数由 <see cref="DiffPipelineOptions.MaxDegreeOfParallelism"/> 决定。
    /// 如果 <see cref="DiffPipelineOptions.StopOnFirstError"/> 为 <c>false</c>（默认值），单个文件的失败不会影响其他文件的处理，
    /// 错误信息通过进度报告机制传递。如果为 <c>true</c>，任何文件的失败都会立即终止所有处理。
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
    /// 将补丁文件从 <paramref name="patchPath"/> 并行应用到 <paramref name="appPath"/> 中的旧版本文件上。
    /// </summary>
    /// <param name="appPath">应用程序安装目录（包含旧版本文件）。</param>
    /// <param name="patchPath">补丁文件所在目录。</param>
    /// <param name="progress">可选的进度报告器，覆盖构造函数中设置的进度报告器。</param>
    /// <param name="cancellationToken">取消令牌，用于取消正在进行的补丁应用操作。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 详细工作流程：
    /// <list type="number">
    ///   <item><description>如果 <paramref name="appPath"/> 或 <paramref name="patchPath"/> 不存在，直接返回。</description></item>
    ///   <item><description>扫描补丁目录中的所有文件（跳过黑名单目录），查找 <c>generalupdate_delete_files.json</c> 并执行文件删除。</description></item>
    ///   <item><description>使用 <see cref="IDirtyMatcher.Match"/> 将补丁文件与旧版本文件进行匹配配对。</description></item>
    ///   <item><description>对每个匹配的文件对，使用临时文件策略安全地应用补丁：先将补丁结果写入临时文件，
    ///         成功后删除原文件再移动临时文件到原位置，确保应用过程中的故障不会损坏原始文件。</description></item>
    ///   <item><description>复制所有不在补丁清单中的未知/新增文件到应用程序目录。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 删除清单处理细节：
    /// 如果补丁目录中包含 <c>generalupdate_delete_files.json</c> 文件，该文件记录了在新版本中已被删除的文件。
    /// 系统通过比较文件中记录的文件哈希值与当前文件的 SHA256 哈希值来识别并删除这些文件。
    /// </para>
    /// <para>
    /// 临时文件策略：
    /// <see cref="ApplyPatch"/> 方法使用 <c>{随机文件名}_{原文件名}</c> 的临时文件名，
    /// 在确保补丁成功应用后才替换原文件。这种策略最大限度地降低了应用失败时数据丢失的风险。
    /// </para>
    /// <para>
    /// 最后，<see cref="CopyUnknownFiles"/> 会清理补丁目录并将所有新增文件复制到应用程序目录中。
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
    /// 使用临时文件策略安全地将单个补丁文件应用到对应的应用程序文件。
    /// </summary>
    /// <param name="appFilePath">要更新的应用程序文件完整路径。</param>
    /// <param name="patchFilePath">补丁文件完整路径。</param>
    /// <param name="ct">取消令牌。</param>
    /// <remarks>
    /// <para>
    /// 此方法执行以下步骤：
    /// <list type="number">
    ///   <item><description>检查应用程序文件和补丁文件是否存在，如果任一不存在则跳过。</description></item>
    ///   <item><description>在与应用程序文件相同的目录中创建一个临时文件（名称格式：<c>{随机文件名}_{原文件名}</c>）。</description></item>
    ///   <item><description>调用 <see cref="IBinaryDiffer.DirtyAsync"/> 将补丁应用到原文件，输出写入临时文件。</description></item>
    ///   <item><description>如果补丁应用成功，删除原文件并将临时文件移动至原文件位置。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 这种"写入临时文件→替换原文件"的策略确保了如果补丁应用过程中发生故障，
    /// 原始文件不会被损坏或丢失。
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
    /// 处理删除清单（generalupdate_delete_files.json），从应用程序目录中删除已废弃的文件。
    /// </summary>
    /// <param name="patchFiles">补丁目录中的文件列表。</param>
    /// <param name="oldFiles">应用程序目录中的文件列表。</param>
    /// <remarks>
    /// <para>
    /// 此方法查找补丁目录中的 <c>generalupdate_delete_files.json</c> 文件，
    /// 该文件包含在新版本中已被删除的文件的 SHA256 哈希值列表。
    /// 然后扫描应用程序目录中的每个文件，计算其 SHA256 哈希值并与清单中的值比对，
    /// 匹配的文件将被删除。
    /// </para>
    /// <para>
    /// 注意：删除前会将文件属性重置为 <see cref="FileAttributes.Normal"/>，
    /// 以防止因只读属性导致删除失败。
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
    /// 将补丁目录中的新增文件（不在旧版本中的文件）复制到应用程序目录，然后清理补丁目录。
    /// </summary>
    /// <param name="appPath">应用程序目录路径。</param>
    /// <param name="patchPath">补丁目录路径。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <remarks>
    /// <para>
    /// 此方法执行以下操作：
    /// <list type="number">
    ///   <item><description>比较应用程序目录和补丁目录，找出补丁目录中新增的文件。</description></item>
    ///   <item><description>过滤掉黑名单格式（如可执行文件扩展名）的文件。</description></item>
    ///   <item><description>将新增文件复制到应用程序目录的相应位置，自动创建缺失的子目录。</description></item>
    ///   <item><description>最后删除整个补丁目录，完成清理。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 此步骤在脏模式的最后阶段执行，确保所有新增文件都被正确地合并到应用程序目录中。
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
    /// 根据目标文件信息计算其在补丁输出目录中的临时子目录路径。
    /// </summary>
    /// <param name="file">当前正在处理的文件节点。</param>
    /// <param name="targetPath">目标（新版本）目录路径。</param>
    /// <param name="patchPath">补丁输出目录路径。</param>
    /// <returns>文件的临时子目录完整路径。如果文件在目标目录的根目录下，则返回补丁目录路径。</returns>
    /// <remarks>
    /// 此方法通过将文件的完整路径中的目标目录部分替换为补丁目录部分来计算相对路径。
    /// 如果目录不存在，则自动创建。这样可以保持补丁输出目录中的目录结构与目标目录一致。
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
    /// 验证输入目录是否存在且不为空。
    /// </summary>
    /// <param name="sourcePath">源（旧版本）目录路径。</param>
    /// <param name="targetPath">目标（新版本）目录路径。</param>
    /// <param name="patchPath">补丁输出目录路径。</param>
    /// <exception cref="ArgumentNullException">当任意路径为 <c>null</c> 或空白时引发。</exception>
    /// <exception cref="DirectoryNotFoundException">当源目录或目标目录不存在时引发。</exception>
    /// <remarks>
    /// 此验证仅在 <see cref="CleanAsync"/> 开始时调用。它确保所有必需的输入目录都已就绪，
    /// 避免在执行过程中因路径无效而失败。
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
