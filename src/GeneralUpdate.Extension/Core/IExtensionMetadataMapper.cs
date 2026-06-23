using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Models;
using Newtonsoft.Json;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Maps between DTO and domain metadata representations of extensions.
/// Injectable to support unit testing of mapping logic.
/// </summary>
public interface IExtensionMetadataMapper
{
    /// <summary>
    /// Convert a server-side ExtensionDTO into internal ExtensionMetadata.
    /// </summary>
    /// <param name="dto">The DTO received from the server.</param>
    /// <returns>Domain metadata object.</returns>
    ExtensionMetadata ToMetadata(ExtensionDTO dto);
}

/// <summary>
/// Default implementation of <see cref=\"IExtensionMetadataMapper\"/>.
/// </summary>
public class DefaultExtensionMetadataMapper : IExtensionMetadataMapper
{
    /// <inheritdoc/>
    public ExtensionMetadata ToMetadata(ExtensionDTO dto)
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
            CustomProperties = dto.CustomProperties != null
                ? JsonConvert.SerializeObject(dto.CustomProperties)
                : null
        };
    }
}
