using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Main extension host implementation
/// </summary>
public class GeneralExtensionHost : IExtensionHost
{
    private readonly string _hostVersion;
    private readonly string _extensionsDirectory;
    private readonly string _backupDirectory;
    private readonly IExtensionHttpClient _httpClient;
    private readonly IVersionCompatibilityChecker _compatibilityChecker;
    private readonly IDownloadQueueManager _downloadQueue;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IPlatformMatcher _platformMatcher;
    private readonly IExtensionLifecycleHooks? _lifecycleHooks;
    private readonly IExtensionMetadataMapper? _metadataMapper;
    private readonly Dictionary<string, bool> _autoUpdateSettings = new();
    private bool _globalAutoUpdate;

    public IExtensionCatalog ExtensionCatalog { get; }

    public event EventHandler<ExtensionUpdateEventArgs>? ExtensionUpdateStatusChanged;

    /// <summary>
    /// Initialize General Extension Host with injected dependencies
    /// </summary>
    /// <param name="options">Configuration options</param>
    /// <param name="httpClient">HTTP client for extension API</param>
    /// <param name="catalog">Extension catalog</param>
    /// <param name="compatibilityChecker">Version compatibility checker</param>
    /// <param name="downloadQueue">Download queue manager</param>
    /// <param name="dependencyResolver">Dependency resolver</param>
    /// <param name="platformMatcher">Platform matcher</param>
    /// <param name="lifecycleHooks">Optional lifecycle hooks for extension events</param>
    /// <param name="metadataMapper">Optional metadata mapper for DTO-to-domain conversion</param>
    public GeneralExtensionHost(
        ExtensionHostOptions options,
        IExtensionHttpClient httpClient,
        IExtensionCatalog catalog,
        IVersionCompatibilityChecker compatibilityChecker,
        IDownloadQueueManager downloadQueue,
        IDependencyResolver dependencyResolver,
        IPlatformMatcher platformMatcher,
        IExtensionLifecycleHooks? lifecycleHooks = null,
        IExtensionMetadataMapper? metadataMapper = null)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        
        _hostVersion = options.HostVersion;
        _extensionsDirectory = options.ExtensionsDirectory;
        _backupDirectory = Path.Combine(options.ExtensionsDirectory, ".backup");
        
        // Assign injected dependencies
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ExtensionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
        _downloadQueue = downloadQueue ?? throw new ArgumentNullException(nameof(downloadQueue));
        _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _platformMatcher = platformMatcher ?? throw new ArgumentNullException(nameof(platformMatcher));
        _lifecycleHooks = lifecycleHooks;
        _metadataMapper = metadataMapper;

        // Wire up events
        _downloadQueue.DownloadStatusChanged += OnDownloadStatusChanged;

        // Wire download handler so the queue can perform actual downloads
        _downloadQueue.DownloadHandler = (id, path, progress, ct) =>
            _httpClient.DownloadExtensionAsync(id, path, progress, ct);

        // Ensure directories exist
        Directory.CreateDirectory(_extensionsDirectory);
        Directory.CreateDirectory(_backupDirectory);

        // Load installed extensions
        ExtensionCatalog.LoadInstalledExtensions();
        GeneralTracer.Info($"GeneralExtensionHost: initialized with DI. HostVersion={_hostVersion}, ExtensionsDirectory={_extensionsDirectory}");
    }

    /// <summary>
    /// Initialize General Extension Host (legacy constructor for backward compatibility)
    /// </summary>
    /// <param name="options">Configuration options for the extension host</param>
    public GeneralExtensionHost(ExtensionHostOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        
        _hostVersion = options.HostVersion;
        _extensionsDirectory = options.ExtensionsDirectory;
        _backupDirectory = Path.Combine(options.ExtensionsDirectory, ".backup");
        
        // Create dependencies in the correct order
        _httpClient = new ExtensionHttpClient(options.ServerUrl, options.Scheme, options.Token);
        ExtensionCatalog = new ExtensionCatalog(options.CatalogPath ?? options.ExtensionsDirectory);
        _compatibilityChecker = new VersionCompatibilityChecker();
        _downloadQueue = new DownloadQueueManager();
        _dependencyResolver = new DependencyResolver(ExtensionCatalog);
        _platformMatcher = new PlatformMatcher();

        // Wire up events
        _downloadQueue.DownloadStatusChanged += OnDownloadStatusChanged;

        // Ensure directories exist
        Directory.CreateDirectory(_extensionsDirectory);
        Directory.CreateDirectory(_backupDirectory);

        // Load installed extensions
        ExtensionCatalog.LoadInstalledExtensions();
        GeneralTracer.Info($"GeneralExtensionHost: initialized (legacy). HostVersion={_hostVersion}, ExtensionsDirectory={_extensionsDirectory}, ServerUrl={options.ServerUrl}");
    }

    public async Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryExtensionsAsync(ExtensionQueryDTO query)
    {
        GeneralTracer.Info($"GeneralExtensionHost.QueryExtensionsAsync: querying extensions. ExtensionId={query?.Id}");
        try
        {
            var result = await _httpClient.QueryExtensionsAsync(query);
            GeneralTracer.Info($"GeneralExtensionHost.QueryExtensionsAsync: query completed. Code={result?.Code}, ItemCount={result?.Body?.Items?.Count() ?? 0}");
            return result;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("GeneralExtensionHost.QueryExtensionsAsync: exception occurred during extension query.", ex);
            throw;
        }
    }

    public async Task<bool> DownloadExtensionAsync(string extensionId, string savePath)
    {
        GeneralTracer.Info($"GeneralExtensionHost.DownloadExtensionAsync: downloading extension. ExtensionId={extensionId}, SavePath={savePath}");
        var progress = new Progress<int>(p =>
        {
            GeneralTracer.Debug($"GeneralExtensionHost.DownloadExtensionAsync: progress={p}% for ExtensionId={extensionId}");
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                Status = ExtensionUpdateStatus.Updating,
                Progress = p
            });
        });

        try
        {
            var success = await _httpClient.DownloadExtensionAsync(extensionId, savePath, progress);
            if (success)
                GeneralTracer.Info($"GeneralExtensionHost.DownloadExtensionAsync: extension downloaded successfully. ExtensionId={extensionId}");
            else
                GeneralTracer.Warn($"GeneralExtensionHost.DownloadExtensionAsync: extension download returned false. ExtensionId={extensionId}");
            return success;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"GeneralExtensionHost.DownloadExtensionAsync: exception during download of ExtensionId={extensionId}.", ex);
            throw;
        }
    }

    public async Task<bool> UpdateExtensionAsync(string extensionId)
    {
        GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: starting extension update. ExtensionId={extensionId}");
        try
        {
            // Notify queued
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                Status = ExtensionUpdateStatus.Queued
            });

            // Query for the latest version
            var query = new ExtensionQueryDTO
            {
                Id = extensionId
            };

            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: querying server for extension metadata. ExtensionId={extensionId}");
            var response = await QueryExtensionsAsync(query);
            if (response.Body?.Items == null)
            {
                GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: server returned null items for ExtensionId={extensionId}.");
                throw new InvalidOperationException("Failed to query extension from server");
            }

            var serverExtension = response.Body.Items.FirstOrDefault(e => string.Equals(e.Id, extensionId));
            if (serverExtension == null)
            {
                GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: extension not found on server. ExtensionId={extensionId}");
                throw new InvalidOperationException($"Extension {extensionId} not found on server");
            }

            // Convert DTO to metadata (use injected mapper if available, fallback to static)
            var metadata = _metadataMapper != null
                ? _metadataMapper.ToMetadata(serverExtension)
                : ToMetadata(serverExtension);
            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: metadata resolved. Name={metadata.Name}, Version={metadata.Version}");

            // Check compatibility
            if (!IsExtensionCompatible(metadata))
            {
                GeneralTracer.Warn($"GeneralExtensionHost.UpdateExtensionAsync: extension not compatible with host version={_hostVersion}. ExtensionId={extensionId}");
                throw new InvalidOperationException($"Extension {extensionId} is not compatible with host version {_hostVersion}");
            }

            // Check platform support
            if (!_platformMatcher.IsCurrentPlatformSupported(metadata))
            {
                GeneralTracer.Warn($"GeneralExtensionHost.UpdateExtensionAsync: extension does not support current platform. ExtensionId={extensionId}");
                throw new InvalidOperationException($"Extension {extensionId} does not support current platform");
            }

            // Resolve and download dependencies using the dependency resolver
            var dependencyList = metadata.DependencyList;
            if (dependencyList.Count > 0)
            {
                GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: resolving {dependencyList.Count} dependency/ies for ExtensionId={extensionId}");

                var missingDeps = _dependencyResolver.GetMissingDependencies(metadata);
                var sortedDeps = _dependencyResolver.GetTransitiveDependencies(dependencyList.ToList());

                GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: {missingDeps.Count} dependencies need installation (sorted: {sortedDeps.Count})");

                foreach (var dep in sortedDeps)
                {
                    if (ExtensionCatalog.GetInstalledExtensionById(dep) == null)
                    {
                        GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: installing missing dependency dep={dep}");
                        await UpdateExtensionAsync(dep);
                    }
                    else
                    {
                        GeneralTracer.Debug($"GeneralExtensionHost.UpdateExtensionAsync: dependency already installed. dep={dep}");
                    }
                }
            }

            // Download extension
            var fileName = $"{metadata.Name}_{metadata.Version}{metadata.Format}";
            var savePath = Path.Combine(_extensionsDirectory, fileName);

            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: downloading extension package. SavePath={savePath}");
            var downloaded = await DownloadExtensionAsync(extensionId, savePath);
            if (!downloaded)
            {
                GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: download failed for ExtensionId={extensionId}.");
                throw new InvalidOperationException("Failed to download extension");
            }

            // Verify SHA256 hash of downloaded package (if hash is provided)
            if (!string.IsNullOrWhiteSpace(metadata.Hash))
            {
                GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: verifying SHA256 hash for ExtensionId={extensionId}.");
                var actualHash = await ComputeFileSha256Async(savePath);
                if (!string.Equals(actualHash, metadata.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: hash mismatch for ExtensionId={extensionId}. Expected={metadata.Hash}, Actual={actualHash}");
                    SafeDeleteFile(savePath);
                    throw new InvalidOperationException($"SHA256 hash verification failed for extension {extensionId}");
                }
                GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: SHA256 hash verified for ExtensionId={extensionId}.");
            }

            // Install extension
            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: installing extension package. Path={savePath}");
            var installSuccess = await InstallExtensionAsync(savePath, rollbackOnFailure: true);
            if (!installSuccess)
            {
                GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: installation failed for ExtensionId={extensionId}.");
                throw new InvalidOperationException("Failed to install extension");
            }

            // Update catalog with metadata
            metadata.UploadTime = DateTime.UtcNow;
            ExtensionCatalog.AddOrUpdateInstalledExtension(metadata);
            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: extension catalog updated. ExtensionId={extensionId}, Version={metadata.Version}");

            // Notify success
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = metadata.Name,
                Status = ExtensionUpdateStatus.UpdateSuccessful,
                Progress = 100
            });

            GeneralTracer.Info($"GeneralExtensionHost.UpdateExtensionAsync: extension update completed successfully. ExtensionId={extensionId}");
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionAsync: exception during extension update. ExtensionId={extensionId}", ex);
            // Notify failure
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                Status = ExtensionUpdateStatus.UpdateFailed,
                ErrorMessage = ex.Message
            });

            return false;
        }
    }

    public async Task<bool> InstallExtensionAsync(string extensionPath, bool rollbackOnFailure = true)
    {
        string? backupPath = null;
        string? extractedExtensionDir = null;
        GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: starting installation. Path={extensionPath}, RollbackOnFailure={rollbackOnFailure}");

        try
        {
            // Validate extension file exists
            if (!File.Exists(extensionPath))
            {
                GeneralTracer.Error($"GeneralExtensionHost.InstallExtensionAsync: extension file not found. Path={extensionPath}");
                throw new FileNotFoundException("Extension file not found", extensionPath);
            }

            // Validate it's a zip file
            if (!extensionPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                GeneralTracer.Error($"GeneralExtensionHost.InstallExtensionAsync: extension file is not a .zip. Path={extensionPath}");
                throw new InvalidOperationException("Extension file must be a .zip file");
            }

            // Invoke lifecycle hook: before install
            if (_lifecycleHooks != null)
            {
                var tempMeta = new ExtensionMetadata { Id = extensionPath, Name = Path.GetFileNameWithoutExtension(extensionPath) };
                var canInstall = await _lifecycleHooks.OnBeforeInstallAsync(tempMeta, extensionPath);
                if (!canInstall)
                {
                    GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: installation cancelled by lifecycle hook. Path={extensionPath}");
                    return false;
                }
            }

            // Extract extension name from file name (e.g., "demo-extension_1.0.0.zip" -> "demo-extension")
            var fileName = Path.GetFileNameWithoutExtension(extensionPath);
            var extensionName = fileName;
            
            // Try to parse extension name if it follows pattern "name_version"
            var underscoreIndex = fileName.LastIndexOf('_');
            if (underscoreIndex > 0)
            {
                extensionName = fileName.Substring(0, underscoreIndex);
            }

            // Determine target installation directory for this extension
            var targetExtensionDir = Path.Combine(_extensionsDirectory, extensionName);
            extractedExtensionDir = targetExtensionDir;
            GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: resolved extension name={extensionName}, targetDir={targetExtensionDir}");

            // Create backup if extension already exists
            var existingExtension = ExtensionCatalog.GetInstalledExtensions()
                .FirstOrDefault(e => e.Name == extensionName);

            if (existingExtension != null && rollbackOnFailure)
            {
                if (Directory.Exists(targetExtensionDir))
                {
                    backupPath = Path.Combine(_backupDirectory, $"{extensionName}_{DateTime.UtcNow:yyyyMMddHHmmss}");
                    GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: backing up existing installation to {backupPath}");
                    // Create backup directory structure
                    Directory.CreateDirectory(_backupDirectory);
                    
                    // Copy entire extension directory to backup
                    CopyDirectory(targetExtensionDir, backupPath);
                    GeneralTracer.Info("GeneralExtensionHost.InstallExtensionAsync: backup created successfully.");
                }
            }

            // Remove existing extension directory if it exists
            if (Directory.Exists(targetExtensionDir))
            {
                GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: removing existing installation at {targetExtensionDir}");
                Directory.Delete(targetExtensionDir, true);
            }

            // Create target directory
            Directory.CreateDirectory(targetExtensionDir);

            // Extract the zip file to the installation directory (with Zip Slip protection)
            GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: extracting package to {targetExtensionDir}");
            await SafeExtractZipAsync(extensionPath, targetExtensionDir);
            GeneralTracer.Info("GeneralExtensionHost.InstallExtensionAsync: extraction completed successfully.");

            // Delete backup on success
            if (backupPath != null && Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
                GeneralTracer.Debug($"GeneralExtensionHost.InstallExtensionAsync: cleaned up backup at {backupPath}.");
            }

            GeneralTracer.Info($"GeneralExtensionHost.InstallExtensionAsync: extension installed successfully. Name={extensionName}");

            // Invoke lifecycle hook: after install
            if (_lifecycleHooks != null)
            {
                var installedMeta = new ExtensionMetadata { Id = extensionPath, Name = extensionName };
                await _lifecycleHooks.OnAfterInstallAsync(installedMeta);
            }

            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error($"GeneralExtensionHost.InstallExtensionAsync: installation failed for path={extensionPath}.", ex);
            // Rollback on failure
            if (rollbackOnFailure && backupPath != null && Directory.Exists(backupPath))
            {
                GeneralTracer.Warn("GeneralExtensionHost.InstallExtensionAsync: attempting rollback from backup.");
                try
                {
                    // Remove failed installation
                    if (extractedExtensionDir != null && Directory.Exists(extractedExtensionDir))
                    {
                        Directory.Delete(extractedExtensionDir, true);
                    }

                    // Restore from backup
                    if (extractedExtensionDir != null)
                    {
                        CopyDirectory(backupPath, extractedExtensionDir);
                    }

                    // Delete backup
                    Directory.Delete(backupPath, true);
                    GeneralTracer.Info("GeneralExtensionHost.InstallExtensionAsync: rollback completed successfully.");
                }
                catch (Exception rollbackEx)
                {
                    GeneralTracer.Error("GeneralExtensionHost.InstallExtensionAsync: rollback also failed.", rollbackEx);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Recursively copy a directory and all its contents
    /// </summary>
    /// <param name="sourceDir">Source directory path</param>
    /// <param name="targetDir">Target directory path</param>
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        // Recursively copy subdirectories
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, bool>> UpdateExtensionsAsync(IEnumerable<string> extensionIds, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();
        
        foreach (var extensionId in extensionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var success = await UpdateExtensionAsync(extensionId);
                results[extensionId] = success;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GeneralTracer.Error($"GeneralExtensionHost.UpdateExtensionsAsync: bulk update failed for ExtensionId={extensionId}.", ex);
                results[extensionId] = false;
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public bool IsExtensionCompatible(ExtensionMetadata extension)
    {
        return _compatibilityChecker.IsCompatible(extension, _hostVersion);
    }

    public void SetAutoUpdate(string extensionId, bool autoUpdate)
    {
        _autoUpdateSettings[extensionId] = autoUpdate;
    }

    public void SetGlobalAutoUpdate(bool enabled)
    {
        _globalAutoUpdate = enabled;
    }

    /// <inheritdoc/>
    public async Task<bool> UninstallExtensionAsync(
        string extensionId,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Uninstalling extension: {extensionId}");
        var extension = ExtensionCatalog.GetInstalledExtensionById(extensionId);
        if (extension == null)
        {
            GeneralTracer.Warn($"Extension not found for uninstall: {extensionId}");
            return false;
        }

        if (_lifecycleHooks != null)
        {
            var canProceed = await _lifecycleHooks.OnBeforeUninstallAsync(extension, cancellationToken);
            if (!canProceed)
            {
                GeneralTracer.Info($"Uninstall cancelled by lifecycle hook for: {extensionId}");
                return false;
            }
        }

        ExtensionCatalog.RemoveInstalledExtension(extensionId);

        if (_lifecycleHooks != null)
            await _lifecycleHooks.OnAfterUninstallAsync(extensionId, cancellationToken);

        GeneralTracer.Info($"Extension uninstalled: {extensionId}");
        return true;
    }

    /// <inheritdoc/>
    public async Task ActivateExtensionAsync(
        string extensionId,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Activating extension: {extensionId}");

        if (_lifecycleHooks != null)
            await _lifecycleHooks.OnBeforeActivateAsync(extensionId, cancellationToken);

        // Extension activation is host-specific (e.g., Assembly.LoadFrom).
        // The lifecycle hooks provide the extension points for host implementations.
        GeneralTracer.Info($"Activation completed for extension: {extensionId}");

        if (_lifecycleHooks != null)
            await _lifecycleHooks.OnAfterActivateAsync(extensionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeactivateExtensionAsync(
        string extensionId,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Deactivating extension: {extensionId}");

        if (_lifecycleHooks != null)
            await _lifecycleHooks.OnBeforeDeactivateAsync(extensionId, cancellationToken);

        // Extension deactivation is host-specific.
        GeneralTracer.Info($"Deactivation completed for extension: {extensionId}");

        if (_lifecycleHooks != null)
            await _lifecycleHooks.OnAfterDeactivateAsync(extensionId, cancellationToken);
    }

    /// <summary>
    /// Check if auto-update is enabled for an extension
    /// </summary>
    /// <param name="extensionId">Extension ID</param>
    /// <returns>True if auto-update is enabled</returns>
    public bool IsAutoUpdateEnabled(string extensionId)
    {
        if (_autoUpdateSettings.TryGetValue(extensionId, out var setting))
        {
            return setting;
        }

        return _globalAutoUpdate;
    }

    private void OnDownloadStatusChanged(object? sender, DownloadTaskEventArgs e)
    {
        ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
        {
            ExtensionId = e.Task.Extension.Id,
            ExtensionName = e.Task.Extension.Name,
            Status = e.Task.Status,
            Progress = e.Task.Progress,
            ErrorMessage = e.Task.ErrorMessage
        });
    }

    private static ExtensionMetadata ToMetadata(ExtensionDTO dto)
    {
        return new ExtensionMetadata
        {
            Id = dto.Id,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Version = dto.Version,
            FileSize = dto.FileSize,
            UploadTime = dto.UploadTime,
            Status = dto.Status,
            Description = dto.Description,
            Format = dto.Format,
            Hash = dto.Hash,
            Publisher = dto.Publisher,
            License = dto.License,
            Categories = dto.Categories != null ? string.Join(",", dto.Categories) : null,
            SupportedPlatforms = dto.SupportedPlatforms,
            MinHostVersion = dto.MinHostVersion,
            MaxHostVersion = dto.MaxHostVersion,
            ReleaseDate = dto.ReleaseDate,
            Dependencies = dto.Dependencies != null ? string.Join(",", dto.Dependencies) : null,
            IsPreRelease = dto.IsPreRelease,
            DownloadUrl = dto.DownloadUrl,
            CustomProperties = dto.CustomProperties != null ? 
                JsonConvert.SerializeObject(dto.CustomProperties) : null
        };
    }

    /// <summary>
    /// Securely extract a ZIP archive with path traversal (Zip Slip) protection.
    /// Each entry's destination path is validated to ensure it stays within the target directory.
    /// </summary>
    /// <param name="zipPath">Path to the ZIP archive.</param>
    /// <param name="destinationDir">Target extraction directory.</param>
    private static async Task SafeExtractZipAsync(string zipPath, string destinationDir)
    {
        var fullDestDir = Path.GetFullPath(destinationDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                // Decode entry name; ZipArchive entries can use backslashes on some platforms
                var entryName = entry.FullName.Replace('\\', '/');

                // Combine and normalize to detect traversal
                var destinationPath = Path.GetFullPath(Path.Combine(fullDestDir, entryName));

                // Validate that the resolved path stays within the destination directory
                if (!destinationPath.StartsWith(fullDestDir + Path.DirectorySeparatorChar)
                    && destinationPath != fullDestDir)
                {
                    GeneralTracer.Warn($"SafeExtractZipAsync: blocked path traversal for entry '{entry.FullName}' -> '{destinationPath}'");
                    continue; // Skip malicious entries
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    // Directory entry
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    // Ensure parent directory exists
                    var parentDir = Path.GetDirectoryName(destinationPath);
                    if (parentDir != null)
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        });
    }

    /// <summary>
    /// Compute the SHA256 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>Hexadecimal SHA256 hash string.</returns>
    private static Task<string> ComputeFileSha256Async(string filePath)
    {
        return Task.Run(() =>
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        });
    }

    /// <summary>
    /// Safely delete a file, ignoring errors if the file does not exist.
    /// </summary>
    private static void SafeDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"SafeDeleteFile: failed to delete {filePath}. {ex.Message}");
        }
    }
}
