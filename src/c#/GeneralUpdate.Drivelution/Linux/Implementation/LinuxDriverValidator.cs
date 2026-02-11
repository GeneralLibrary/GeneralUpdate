using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Events;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Linux.Helpers;

namespace GeneralUpdate.Drivelution.Linux.Implementation;

/// <summary>
/// Linux驱动验证器实现
/// Linux driver validator implementation
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxDriverValidator : IDriverValidator
{
    private readonly IDrivelutionLogger _logger;

    public LinuxDriverValidator(IDrivelutionLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateIntegrityAsync(
        string filePath,
        string expectedHash,
        string hashAlgorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        return await HashValidator.ValidateHashAsync(filePath, expectedHash, hashAlgorithm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSignatureAsync(
        string filePath,
        IEnumerable<string> trustedPublishers,
        CancellationToken cancellationToken = default)
    {
        // Check for GPG signature file
        var signaturePath = filePath + ".sig";
        if (!File.Exists(signaturePath))
        {
            signaturePath = filePath + ".asc";
        }

        if (File.Exists(signaturePath))
        {
            return await LinuxSignatureHelper.ValidateGpgSignatureAsync(filePath, signaturePath, trustedPublishers);
        }

        _logger.Warning($"No signature file found for: {filePath}");
        return !trustedPublishers.Any(); // If no trusted publishers specified, accept unsigned
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateCompatibilityAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        return await CompatibilityChecker.CheckCompatibilityAsync(driverInfo, cancellationToken);
    }
}
