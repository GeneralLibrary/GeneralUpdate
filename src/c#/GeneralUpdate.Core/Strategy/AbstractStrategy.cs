using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Network;
using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// 抽象基类，定义平台特定的更新策略。提供管道执行循环、上下文构建和错误处理等通用逻辑。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类是模板方法模式的典型应用，子类（<see cref="WindowsStrategy"/>、<see cref="LinuxStrategy"/>、
    /// <see cref="MacStrategy"/>）通过重写 <see cref="BuildPipeline"/> 方法提供各自平台的中间件链。
    /// </para>
    /// <para>
    /// <b>管道执行循环（<see cref="ExecuteAsync"/>）：</b>
    /// <list type="number">
    ///   <item><description>遍历 <c>_configinfo.UpdateVersions</c> 集合，逐一处理每个更新版本。</description></item>
    ///   <item><description>调用 <see cref="CreatePipelineContext"/> 构建管道上下文，包含压缩包路径、
    ///   哈希值、格式编码、源路径和补丁配置等关键参数。</description></item>
    ///   <item><description>调用 <see cref="BuildPipeline"/>（抽象方法，由子类实现）获取中间件构建器。</description></item>
    ///   <item><description>执行 <c>PipelineBuilder.Build()</c>，以先进先出（FIFO）顺序执行注册的中间件：
    ///   <c>Hash</c>（完整性校验）→ <c>Decompress</c>（解压更新包）→ <c>Patch</c>（应用增量补丁）。</description></item>
    ///   <item><description>通过 <see cref="VersionService.Report"/> 向服务器报告当前版本的更新结果。</description></item>
    ///   <item><description>删除已处理的压缩包文件。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>错误处理：</b>每个版本的更新失败时，调用 <see cref="HandleExecuteException"/> 记录异常并分发事件，
    /// 然后调用 <c>TryRollback</c> 尝试从备份目录恢复。错误不会中断后续版本的处理。
    /// 所有版本处理完毕后清理临时目录并调用 <see cref="OnExecuteCompleteAsync"/>。
    /// </para>
    /// </remarks>
    public abstract class AbstractStrategy : IStrategy
    {
        private const string Patchs = "patchs";

        /// <summary>
        /// 全局配置信息，包含更新包路径、临时目录、报告地址、版本列表等参数。
        /// 由 <see cref="Create"/> 方法初始化，供管道执行循环使用。
        /// </summary>
        protected GlobalConfigInfo _configinfo = new();

        /// <summary>
        /// 获取或设置生命周期钩子。由引导程序注入，用于在更新前后执行自定义逻辑。
        /// </summary>
        public IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

        /// <summary>
        /// 获取或设置更新状态报告器。负责向服务器报告每个版本的处理进度和最终结果。
        /// </summary>
        public IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

        /// <summary>
        /// 获取或设置差异补丁管道。支持并行应用增量补丁并报告进度。
        /// </summary>
        public DiffPipeline? DiffPipeline { get; set; }

        /// <summary>
        /// 获取或设置要启动的应用程序名称。由上层策略（如 <see cref="UpgradeUpdateStrategy"/>）
        /// 在调用 <see cref="StartAppAsync"/> 前设置。
        /// </summary>
        public string? LaunchAppName { get; set; }

        /// <summary>
        /// 获取或设置是否同时启动 Bowl 辅助进程。仅 Windows 平台有效，由上层策略在调用
        /// <see cref="StartAppAsync"/> 前设置。
        /// </summary>
        public bool LaunchBowl { get; set; }

        /// <summary>
        /// 获取或设置是否优先使用更新路径。当为 <c>true</c> 时，<see cref="StartAppAsync"/>
        /// 会优先从 <see cref="GlobalConfigInfo.UpdatePath"/> 解析应用程序，
        /// 失败后再回退到 <see cref="GlobalConfigInfo.InstallPath"/>。
        /// 由 <see cref="ClientUpdateStrategy"/> 在启动升级进程时设置。
        /// </summary>
        public bool UseUpdatePath { get; set; }

        /// <summary>
        /// 启动主应用程序。虚方法，由子类提供平台特定的应用启动实现。
        /// </summary>
        /// <remarks>
        /// 默认实现抛出 <see cref="NotImplementedException"/>。子类应重写此方法以执行
        /// 平台相关的进程启动逻辑（如设置工作目录、环境变量等）。
        /// </remarks>
        /// <returns>表示异步操作的任务。</returns>
        public virtual Task StartAppAsync() => throw new NotImplementedException();
        
        /// <summary>
        /// 执行更新管道的核心循环。遍历所有待更新版本，依次构建管道上下文、执行中间件链、报告状态并清理资源。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>管道执行循环详解：</b>
        /// <list type="number">
        ///   <item><description><b>遍历版本：</b>从 <c>_configinfo.UpdateVersions</c> 中逐个取出
        ///   <see cref="VersionInfo"/> 对象。</description></item>
        ///   <item><description><b>构建上下文：</b>调用 <see cref="CreatePipelineContext"/> 创建
        ///   <see cref="PipelineContext"/>，包含压缩包路径（由 <c>TempPath</c> 和版本名称拼接）、
        ///   哈希值、压缩格式、源路径和补丁配置等关键参数。</description></item>
        ///   <item><description><b>构建管道：</b>调用 <see cref="BuildPipeline"/>（抽象方法，由子类实现
        ///   平台特定逻辑），注册 <c>Hash</c>（完整性校验）、<c>Decompress</c>（解压缩）、
        ///   <c>Patch</c>（增量补丁）等中间件。</description></item>
        ///   <item><description><b>执行管道：</b>调用 <c>PipelineBuilder.Build()</c>，按照注册顺序以
        ///   先进先出方式依次执行所有中间件。</description></item>
        ///   <item><description><b>报告状态：</b>通过 <see cref="VersionService.Report"/> 向服务器报告
        ///   当前版本的更新结果（成功或失败）。</description></item>
        ///   <item><description><b>清理资源：</b>调用 <c>DeleteVersionZip</c> 删除当前版本已处理的压缩包文件。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>错误处理策略：</b>单个版本更新失败时，捕获异常后依次调用
        /// <see cref="HandleExecuteException"/> 记录错误、<c>TryRollback</c> 尝试从备份恢复，
        /// 然后继续处理下一个版本。所有版本处理完毕后，无论是否有版本失败，都会执行清理操作
        /// 并调用 <see cref="OnExecuteCompleteAsync"/>。
        /// </para>
        /// <para>
        /// <b>资源清理：</b>循环结束后删除补丁临时目录，并尝试清理 <c>TempPath</c>
        /// （仅当目录为空时删除，避免误删其他 <c>AppType</c> 的包文件）。
        /// </para>
        /// </remarks>
        public virtual async Task ExecuteAsync()
        {
            try
            {
                var status = ReportType.None;
                var patchPath = StorageManager.GetTempDirectory(Patchs);
                foreach (var version in _configinfo.UpdateVersions)
                {
                    try
                    {
                        var context = CreatePipelineContext(version, patchPath);
                        var pipelineBuilder = BuildPipeline(context);
                        await pipelineBuilder.Build();
                        status = ReportType.Success;
                    }
                    catch (Exception e)
                    {
                        status = ReportType.Failure;
                        HandleExecuteException(e);
                        TryRollback();
                    }
                    finally
                    {
                        await VersionService.Report(_configinfo.ReportUrl
                            , version.RecordId
                            , status
                            , version.AppType
                            , _configinfo.Scheme
                            , _configinfo.Token);

                        // Delete only this version's zip file — other AppType packages
                        // in TempPath may still be needed by a downstream process.
                        DeleteVersionZip(version);
                    }
                }

                Clear(patchPath);
                TryCleanTempPath();
                await OnExecuteCompleteAsync();
            }
            catch (Exception e)
            {
                HandleExecuteException(e);
            }
        }

        /// <summary>
        /// 初始化策略实例。接收全局配置信息并存储以供后续使用。
        /// </summary>
        /// <param name="parameter">全局配置信息，包含更新包路径、临时目录、报告地址、版本列表等参数。</param>
        public virtual void Create(GlobalConfigInfo parameter) => _configinfo = parameter;

        /// <summary>
        /// 创建管道上下文，填充公共参数和平台特定参数。子类可重写此方法以添加平台特定的上下文参数。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 管道上下文（<see cref="PipelineContext"/>）包含以下键值对：
        /// <list type="table">
        ///   <listheader><term>键</term><description>说明</description></listheader>
        ///   <item><term><c>ZipFilePath</c></term><description>压缩包完整路径，由 <c>TempPath</c>
        ///   和版本名称拼接而成，用于 <c>Decompress</c> 中间件定位更新包。</description></item>
        ///   <item><term><c>Hash</c></term><description>更新包的哈希值，用于 <c>Hash</c> 中间件
        ///   校验文件完整性，防止数据损坏或篡改。</description></item>
        ///   <item><term><c>Format</c></term><description>压缩格式（如 ZIP、GZip），用于
        ///   <c>Decompress</c> 中间件选择合适的解压算法。</description></item>
        ///   <item><term><c>Encoding</c></term><description>文件编码格式，用于解压时正确处理文件名编码。</description></item>
        ///   <item><term><c>SourcePath</c></term><description>目标安装路径，由 <see cref="ResolveTargetPath"/>
        ///   根据版本的应用类型和配置决定。</description></item>
        ///   <item><term><c>PatchPath</c></term><description>补丁文件的临时存储路径，用于 <c>Patch</c> 中间件。</description></item>
        ///   <item><term><c>PatchEnabled</c></term><description>是否启用增量补丁功能，由 <c>_configinfo.PatchEnabled</c> 控制。</description></item>
        ///   <item><term><c>DiffPipeline</c></term><description>差异补丁管道实例，用于并行应用补丁并报告进度。</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="version">当前待处理的版本信息，包含名称、哈希值、应用类型等。</param>
        /// <param name="patchPath">补丁文件的临时存储目录路径。</param>
        /// <returns>填充完毕的管道上下文实例。</returns>
        protected virtual PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
        {
            var context = new PipelineContext();
            // Common parameters
            context.Add("ZipFilePath", Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format.ToExtension()}"));
            // Hash middleware
            context.Add("Hash", version.Hash);
            // Zip middleware
            context.Add("Format", _configinfo.Format);
            context.Add("Encoding", _configinfo.Encoding);
            // Patch middleware
            // For Upgrade packages, apply to UpdatePath if configured; otherwise fall back to InstallPath
            var sourcePath = ResolveTargetPath(version);
            context.Add("SourcePath", sourcePath);
            context.Add("PatchPath", patchPath);
            context.Add("PatchEnabled", _configinfo.PatchEnabled);
            // DiffPipeline for parallel patch application with progress reporting
            context.Add("DiffPipeline", DiffPipeline);
            
            return context;
        }

        /// <summary>
        /// 构建更新管道中间件链。抽象方法，由各平台子类提供具体的中间件注册逻辑。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 子类应在此方法中创建 <see cref="PipelineBuilder"/> 实例，并依次注册以下中间件
        /// （顺序不可颠倒）：
        /// <list type="number">
        ///   <item><description><b>Hash 中间件：</b>校验更新包的文件完整性，防止数据损坏或篡改。</description></item>
        ///   <item><description><b>Decompress 中间件：</b>解压更新包到目标安装目录。</description></item>
        ///   <item><description><b>Patch 中间件：</b>应用增量补丁，仅更新变更的文件以节省带宽和磁盘空间。</description></item>
        /// </list>
        /// 各平台可根据自身特性添加额外的中间件（如权限设置、符号链接处理等）。
        /// </para>
        /// </remarks>
        /// <param name="context">管道上下文，包含中间件执行所需的所有参数。</param>
        /// <returns>配置完中间件的管道构建器实例。</returns>
        protected abstract PipelineBuilder BuildPipeline(PipelineContext context);

        /// <summary>
        /// 在 <see cref="ExecuteAsync"/> 成功完成后调用。子类可重写此方法以添加平台特定的执行后逻辑。
        /// </summary>
        /// <remarks>
        /// 此方法在所有版本处理完毕、临时目录清理完成后调用。可用于执行平台特定的收尾工作，
        /// 如清理临时文件、记录完成日志等。
        /// </remarks>
        /// <returns>表示异步操作的任务。</returns>
        protected virtual Task OnExecuteCompleteAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 处理管道执行过程中发生的异常。记录错误日志并通过事件系统分发异常信息。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 此方法在 <see cref="ExecuteAsync"/> 捕获到异常时调用，执行以下操作：
        /// <list type="number">
        ///   <item><description>通过 <see cref="GeneralTracer.Error"/> 记录异常堆栈和消息。</description></item>
        ///   <item><description>通过 <see cref="EventManager"/> 分发 <see cref="ExceptionEventArgs"/>，
        ///   供上层监听者处理。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 子类可重写此方法以添加自定义错误处理逻辑，如发送告警通知、记录审计日志等。
        /// </para>
        /// </remarks>
        /// <param name="e">管道执行过程中捕获的异常。</param>
        protected virtual void HandleExecuteException(Exception e)
        {
            GeneralTracer.Error($"Strategy execution exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }

        /// <summary>
        /// 检查指定路径下是否存在目标文件。
        /// </summary>
        protected static string CheckPath(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var tempPath = Path.Combine(path, name);
            return File.Exists(tempPath) ? tempPath : string.Empty;
        }

        /// <summary>
        /// 解析可执行文件的完整路径。可选地优先检查 <see cref="GlobalConfigInfo.UpdatePath"/>，
        /// 失败后再回退到 <c>InstallPath</c>。
        /// </summary>
        /// <param name="name">可执行文件的名称。</param>
        /// <param name="preferUpdatePath">当为 <c>true</c> 时，优先检查 <c>UpdatePath</c>。</param>
        /// <returns>找到则返回完整路径，否则返回空字符串。</returns>
        protected string ResolveAppPath(string name, bool preferUpdatePath = false)
        {
            if (preferUpdatePath && !string.IsNullOrWhiteSpace(_configinfo.UpdatePath))
            {
                var upgradeDir = ResolveUpdateDir();
                var path = CheckPath(upgradeDir, name);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return CheckPath(_configinfo.InstallPath, name);
        }

        /// <summary>
        /// 解析更新包的目标应用路径。根据版本的应用类型决定使用更新路径还是安装路径。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>路径解析逻辑：</b>
        /// <list type="bullet">
        ///   <item><description>如果当前版本的应用类型为升级端（<c>AppType == 2</c>）且
        ///   <c>UpdatePath</c> 已配置，则优先使用 <c>UpdatePath</c> 作为目标路径。</description></item>
        ///   <item><description>否则使用 <c>InstallPath</c> 作为目标路径。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <c>UpdatePath</c> 可以是绝对路径或相对路径。相对路径会与 <c>InstallPath</c> 拼接为完整路径。
        /// 此设计允许将升级端的更新包应用到与客户端不同的目标目录。
        /// </para>
        /// </remarks>
        /// <param name="version">当前待处理的版本信息，用于判断应用类型。</param>
        /// <returns>目标安装目录的完整路径。</returns>
        protected string ResolveTargetPath(VersionInfo version)
        {
            if (version.AppType == 2 && !string.IsNullOrWhiteSpace(_configinfo.UpdatePath))
                return ResolveUpdateDir();

            return _configinfo.InstallPath;
        }

        /// <summary>
        /// Resolves <see cref="GlobalConfigInfo.UpdatePath"/> to an absolute path.
        /// Relative paths are combined with <see cref="GlobalConfigInfo.InstallPath"/>.
        /// </summary>
        private string ResolveUpdateDir()
        {
            return Path.IsPathRooted(_configinfo.UpdatePath)
                ? _configinfo.UpdatePath
                : Path.Combine(_configinfo.InstallPath, _configinfo.UpdatePath);
        }

        // ═══ Safe hooks/reporter wrappers (shared by all strategy subclasses) ═══
        // Note: Each subclass builds its own UpdateContext via BuildUpdateContext().
        // Subclasses should call hooks/reporter through their own context-aware wrappers.
        // The Hooks and Reporter properties are declared here so subclasses inherit them
        // without redeclaring.

        /// <summary>
        /// Attempts to restore from backup when a pipeline execution fails.
        /// Only restores if a backup directory exists for the current version.
        /// </summary>
        private void TryRollback()
        {
            try
            {
                var backupDir = _configinfo.BackupDirectory;
                if (!string.IsNullOrWhiteSpace(backupDir) && Directory.Exists(backupDir))
                {
                    GeneralTracer.Warn($"AbstractStrategy.TryRollback: restoring from backup {backupDir} -> {_configinfo.InstallPath}");
                    StorageManager.Restore(backupDir, _configinfo.InstallPath);
                    GeneralTracer.Info("AbstractStrategy.TryRollback: restore completed.");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("AbstractStrategy.TryRollback: rollback failed.", ex);
            }
        }

        private static void Clear(string path)
        {
            if (Directory.Exists(path))
                StorageManager.DeleteDirectory(path);
        }

        /// <summary>
        /// Deletes the zip file for a processed version from TempPath.
        /// Only removes the specific file — other packages in the same directory
        /// may belong to a different AppType and must be kept for downstream processes.
        /// </summary>
        private void DeleteVersionZip(VersionInfo version)
        {
            if (string.IsNullOrWhiteSpace(_configinfo.TempPath)) return;

            var zipPath = Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format.ToExtension()}");
            try
            {
                if (File.Exists(zipPath))
                {
                    File.SetAttributes(zipPath, FileAttributes.Normal);
                    File.Delete(zipPath);
                    GeneralTracer.Info($"AbstractStrategy: deleted processed zip {zipPath}");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AbstractStrategy: failed to delete zip {zipPath}. {ex.Message}");
            }
        }

        /// <summary>
        /// Removes TempPath if it is empty after all processed zips have been deleted.
        /// The last process in the chain (usually Upgrade) will find an empty directory
        /// and clean it up. Earlier processes skip this because other AppType packages
        /// still remain in the directory.
        /// </summary>
        private void TryCleanTempPath()
        {
            try
            {
                var tempPath = _configinfo.TempPath;
                if (string.IsNullOrWhiteSpace(tempPath) || !Directory.Exists(tempPath)) return;

                if (!Directory.EnumerateFileSystemEntries(tempPath).Any())
                {
                    Directory.Delete(tempPath, false);
                    GeneralTracer.Info($"AbstractStrategy: cleaned empty temp directory {tempPath}");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AbstractStrategy: failed to clean temp directory. {ex.Message}");
            }
        }
    }
}
