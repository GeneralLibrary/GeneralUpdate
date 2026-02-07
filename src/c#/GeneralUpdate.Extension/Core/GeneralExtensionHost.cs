using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

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
    public GeneralExtensionHost(
        ExtensionHostOptions options,
        IExtensionHttpClient httpClient,
        IExtensionCatalog catalog,
        IVersionCompatibilityChecker compatibilityChecker,
        IDownloadQueueManager downloadQueue,
        IDependencyResolver dependencyResolver,
        IPlatformMatcher platformMatcher)
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
        _dependencyResolver = dependencyResolver;// ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _platformMatcher = platformMatcher ?? throw new ArgumentNullException(nameof(platformMatcher));

        // Wire up events
        _downloadQueue.DownloadStatusChanged += OnDownloadStatusChanged;

        // Ensure directories exist
        Directory.CreateDirectory(_extensionsDirectory);
        Directory.CreateDirectory(_backupDirectory);

        // Load installed extensions
        ExtensionCatalog.LoadInstalledExtensions();
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
    }

    public async Task<HttpResponseDTO<PagedResultDTO<ExtensionDTO>>> QueryExtensionsAsync(ExtensionQueryDTO query)
    {
        return await _httpClient.QueryExtensionsAsync(query);
    }

    public async Task<bool> DownloadExtensionAsync(string extensionId, string savePath)
    {
        var progress = new Progress<int>(p =>
        {
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                Status = ExtensionUpdateStatus.Updating,
                Progress = p
            });
        });

        return await _httpClient.DownloadExtensionAsync(extensionId, savePath, progress);
    }

    public async Task<bool> UpdateExtensionAsync(string extensionId)
    {
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

            var response = await QueryExtensionsAsync(query);
            if (response.Body?.Items == null)
            {
                throw new InvalidOperationException("Failed to query extension from server");
            }

            var serverExtension = response.Body.Items.FirstOrDefault(e => string.Equals(e.Id, extensionId));
            if (serverExtension == null)
            {
                throw new InvalidOperationException($"Extension {extensionId} not found on server");
            }

            // Convert DTO to metadata
            var metadata = ToMetadata(serverExtension);

            // Check compatibility
            if (!IsExtensionCompatible(metadata))
            {
                throw new InvalidOperationException($"Extension {extensionId} is not compatible with host version {_hostVersion}");
            }

            // Check platform support
            if (!_platformMatcher.IsCurrentPlatformSupported(metadata))
            {
                throw new InvalidOperationException($"Extension {extensionId} does not support current platform");
            }

            // Resolve and download dependencies
            if (!string.IsNullOrWhiteSpace(metadata.Dependencies))
            {
                var dependencies = metadata.Dependencies!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var depId in dependencies)
                {
                    var dep = depId.Trim();
                    var installedDep = ExtensionCatalog.GetInstalledExtensionById(dep);
                    if (installedDep == null)
                    {
                        // Download and install dependency
                        await UpdateExtensionAsync(dep);
                    }
                }
            }

            // Download extension
            var fileName = $"{metadata.Name}_{metadata.Version}{metadata.Format}";
            var savePath = Path.Combine(_extensionsDirectory, fileName);

            var downloaded = await DownloadExtensionAsync(extensionId, savePath);
            if (!downloaded)
            {
                throw new InvalidOperationException("Failed to download extension");
            }

            // Install extension
            var installSuccess = await InstallExtensionAsync(savePath, rollbackOnFailure: true);
            if (!installSuccess)
            {
                throw new InvalidOperationException("Failed to install extension");
            }

            // Update catalog with metadata
            metadata.UploadTime = DateTime.UtcNow;
            ExtensionCatalog.AddOrUpdateInstalledExtension(metadata);

            // Notify success
            ExtensionUpdateStatusChanged?.Invoke(this, new ExtensionUpdateEventArgs
            {
                ExtensionId = extensionId,
                ExtensionName = metadata.Name,
                Status = ExtensionUpdateStatus.UpdateSuccessful,
                Progress = 100
            });

            return true;
        }
        catch (Exception ex)
        {
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

        try
        {
            // Validate extension file exists
            if (!File.Exists(extensionPath))
            {
                throw new FileNotFoundException("Extension file not found", extensionPath);
            }

            // Validate it's a zip file
            if (!extensionPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Extension file must be a .zip file");
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

            // Create backup if extension already exists
            var existingExtension = ExtensionCatalog.GetInstalledExtensions()
                .FirstOrDefault(e => e.Name == extensionName);

            if (existingExtension != null && rollbackOnFailure)
            {
                if (Directory.Exists(targetExtensionDir))
                {
                    backupPath = Path.Combine(_backupDirectory, $"{extensionName}_{DateTime.UtcNow:yyyyMMddHHmmss}");
                    
                    // Create backup directory structure
                    Directory.CreateDirectory(_backupDirectory);
                    
                    // Copy entire extension directory to backup
                    CopyDirectory(targetExtensionDir, backupPath);
                }
            }

            // Remove existing extension directory if it exists
            if (Directory.Exists(targetExtensionDir))
            {
                Directory.Delete(targetExtensionDir, true);
            }

            // Create target directory
            Directory.CreateDirectory(targetExtensionDir);

            // Extract the zip file to the installation directory
            await Task.Run(() => ZipFile.ExtractToDirectory(extensionPath, targetExtensionDir));

            // Delete backup on success
            if (backupPath != null && Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }

            return true;
        }
        catch (Exception)
        {
            // Rollback on failure
            if (rollbackOnFailure && backupPath != null && Directory.Exists(backupPath))
            {
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
                }
                catch
                {
                    // Rollback failed
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
}
