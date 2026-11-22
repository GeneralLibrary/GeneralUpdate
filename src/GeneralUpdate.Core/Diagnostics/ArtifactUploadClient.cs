using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>Uploads an already prepared, immutable diagnostic ZIP to GeneralSpace.</summary>
/// <remarks>
/// This transport does not scan or delete Bowl's outbox. Callers must acquire a
/// coordinated snapshot and retain the same ZIP bytes for every retry.
/// </remarks>
public sealed class ArtifactUploadClient
{
    private const long RequestLimit = 20_971_520;
    private const int ResponseLimit = 64 * 1024;
    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly string _tenantId;
    private readonly string _appKey;
    private readonly long _maxZipBytes;

    /// <summary>
    /// Creates an opt-in transport. The host owns the HTTP client, including its
    /// authentication, redirect and TLS policies, and supplies tenant and app identity.
    /// </summary>
    public ArtifactUploadClient(HttpClient client, Uri endpoint, string tenantId, string appKey,
        long maxZipBytes = 19 * 1024 * 1024)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new ArgumentException("The artifact endpoint must be an absolute HTTPS URI without credentials or a fragment.", nameof(endpoint));
        _tenantId = RequireText(tenantId, nameof(tenantId));
        _appKey = RequireText(appKey, nameof(appKey));
        if (maxZipBytes <= 0 || maxZipBytes > RequestLimit)
            throw new ArgumentOutOfRangeException(nameof(maxZipBytes));
        _maxZipBytes = maxZipBytes;
    }

    /// <summary>
    /// Sends one ZIP. Older servers deduplicate by GUID alone and cannot confirm
    /// that their stored bytes match this request.
    /// </summary>
    /// <param name="artifact">Metadata for the snapshot, supplied by the host.</param>
    /// <param name="zip">ZIP bytes that must remain unchanged while copied and across retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ArtifactUploadResult> UploadAsync(ArtifactUploadMetadata artifact, byte[] zip,
        CancellationToken cancellationToken = default)
    {
        if (artifact == null) throw new ArgumentNullException(nameof(artifact));
        if (zip == null) throw new ArgumentNullException(nameof(zip));
        if (zip.Length == 0 || zip.LongLength > _maxZipBytes)
            throw new ArgumentException("The ZIP is empty or exceeds the configured size limit.", nameof(zip));
        var guid = artifact.Guid;
        var kind = artifact.Kind;
        var schema = artifact.Schema;
        if (!Guid.TryParseExact(guid, "D", out _) ||
            !string.Equals(kind, "crash", StringComparison.Ordinal) || schema != 2)
            throw new ArgumentException("Only schema 2 crash artifacts with a canonical GUID are supported.", nameof(artifact));

        var version = RequireText(artifact.Version, nameof(artifact.Version));
        var target = RequireText(artifact.TargetName, nameof(artifact.TargetName));
        var snapshot = (byte[])zip.Clone();
        string hash;
        using (var sha256 = SHA256.Create())
            hash = BitConverter.ToString(sha256.ComputeHash(snapshot)).Replace("-", "").ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        using var form = new MultipartFormDataContent();
        request.Content = form;
        form.Add(new StringContent(guid), "Guid");
        form.Add(new StringContent(kind), "Kind");
        form.Add(new StringContent(schema.ToString(CultureInfo.InvariantCulture)), "Schema");
        form.Add(new StringContent(_appKey), "AppKey");
        form.Add(new StringContent(version), "Version");
        form.Add(new StringContent(target), "TargetName");
        var file = new ByteArrayContent(snapshot);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", guid + ".zip");

        var requestLength = form.Headers.ContentLength;
        if (!requestLength.HasValue || requestLength.Value > RequestLimit)
            throw new InvalidOperationException("The multipart request exceeds the GeneralSpace transport limit.");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > ResponseLimit)
                throw new InvalidDataException("The artifact response exceeds the size limit.");
            buffer.Write(chunk, 0, read);
        }

        using var json = JsonDocument.Parse(buffer.ToArray());
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number ||
            !code.TryGetInt32(out var status) || status != 200 ||
            !root.TryGetProperty("isSuccess", out var success) || success.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("artifactId", out var id) || id.ValueKind != JsonValueKind.String ||
            !string.Equals(id.GetString(), guid, StringComparison.OrdinalIgnoreCase) ||
            !body.TryGetProperty("duplicate", out var duplicate) ||
            (duplicate.ValueKind != JsonValueKind.True && duplicate.ValueKind != JsonValueKind.False) ||
            !body.TryGetProperty("parsed", out var parsed) ||
            (parsed.ValueKind != JsonValueKind.True && parsed.ValueKind != JsonValueKind.False))
            throw new InvalidDataException("GeneralSpace did not confirm the artifact upload.");

        var hashVerified = false;
        if (body.TryGetProperty("contentSha256", out var returnedHash))
        {
            if (returnedHash.ValueKind != JsonValueKind.String ||
                !string.Equals(returnedHash.GetString(), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("GeneralSpace returned a different artifact content hash.");
            hashVerified = true;
        }

        return new ArtifactUploadResult(duplicate.GetBoolean(), parsed.GetBoolean(), hashVerified);
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value!.Length > 256 ||
            value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            throw new ArgumentException("The value must be nonempty, at most 256 characters, and contain no line breaks.", name);
        return value;
    }
}

/// <summary>Metadata for a schema 2 crash snapshot; identity is configured separately.</summary>
public sealed class ArtifactUploadMetadata
{
    public string Guid { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Schema { get; set; }
    public string Version { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
}

/// <summary>The server's acknowledgment; it does not authorize source deletion.</summary>
public sealed class ArtifactUploadResult
{
    internal ArtifactUploadResult(bool duplicate, bool parsed, bool hashVerified)
    {
        Duplicate = duplicate;
        Parsed = parsed;
        HashVerified = hashVerified;
    }

    public bool Duplicate { get; }
    public bool Parsed { get; }
    /// <summary>Whether the server returned a matching SHA-256 for the ZIP bytes.</summary>
    public bool HashVerified { get; }
}
