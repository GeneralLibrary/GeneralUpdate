using System.Runtime.Versioning;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Windows.Helpers;

namespace GeneralUpdate.Drivelution.Windows.Implementation;

/// <summary>
/// Windows驱动验证器实现
/// Windows driver validator implementation
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsDriverValidator : IDriverValidator
{
    public WindowsDriverValidator()
    {
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateIntegrityAsync(
        string filePath,
        string expectedHash,
        string hashAlgorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Validating file integrity: {filePath}");

        try
        {
            var isValid = await HashValidator.ValidateHashAsync(filePath, expectedHash, hashAlgorithm, cancellationToken);

            if (isValid)
            {
                GeneralTracer.Info("File integrity validation succeeded");
            }
            else
            {
                GeneralTracer.Warn("File integrity validation failed - hash mismatch");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("File integrity validation failed with exception", ex);
            throw new DriverValidationException(
                $"Failed to validate file integrity: {ex.Message}",
                "Integrity",
                ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSignatureAsync(
        string filePath,
        IEnumerable<string> trustedPublishers,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Validating driver signature: {filePath}");

        try
        {
            var isValid = await WindowsSignatureHelper.ValidateAuthenticodeSignatureAsync(filePath, trustedPublishers);

            if (isValid)
            {
                GeneralTracer.Info("Driver signature validation succeeded");
            }
            else
            {
                GeneralTracer.Warn("Driver signature validation failed");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Driver signature validation failed with exception", ex);
            throw new DriverValidationException(
                $"Failed to validate driver signature: {ex.Message}",
                "Signature",
                ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateCompatibilityAsync(
        DriverInfo driverInfo,
        CancellationToken cancellationToken = default)
    {
        GeneralTracer.Info($"Validating driver compatibility for: {driverInfo.Name}");

        try
        {
            var isCompatible = await CompatibilityChecker.CheckCompatibilityAsync(driverInfo, cancellationToken);

            if (isCompatible)
            {
                GeneralTracer.Info("Driver compatibility validation succeeded");
            }
            else
            {
                GeneralTracer.Warn("Driver compatibility validation failed");
                var report = CompatibilityChecker.GetCompatibilityReport(driverInfo);
                GeneralTracer.Warn($"Compatibility report: Current OS={report.CurrentOS}, Target OS={report.TargetOS}, " +
                              $"Current Arch={report.CurrentArchitecture}, Target Arch={report.TargetArchitecture}");
            }

            return isCompatible;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Driver compatibility validation failed with exception", ex);
            throw new DriverValidationException(
                $"Failed to validate driver compatibility: {ex.Message}",
                "Compatibility",
                ex);
        }
    }
}
