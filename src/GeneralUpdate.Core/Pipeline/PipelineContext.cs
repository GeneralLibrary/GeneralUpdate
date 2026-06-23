using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Pipeline context that provides thread-safe key-value storage for sharing data across pipeline stages.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PipelineContext"/> is the core communication medium between middleware. Each middleware can read
/// results produced by upstream middleware from the context and write its own processing results into it
/// for downstream middleware to consume. <see cref="PipelineBuilder"/> receives a context instance at construction
/// time, and that instance remains consistent throughout the entire pipeline lifecycle.
/// </para>
/// <para>
/// Internally uses <see cref="ConcurrentDictionary{TKey, TValue}"/> to guarantee thread safety,
/// supporting safe reads and writes in multi-threaded environments.
/// </para>
/// <para>
/// The following are predefined context keys used in the update pipeline and their descriptions:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Key</term>
///     <description>Type</description>
///     <description>Description</description>
///   </listheader>
///   <item>
///     <term><c>"Hash"</c></term>
///     <description><see cref="string"/></description>
///     <description>The expected SHA256 hash value, used to verify the integrity of the downloaded archive.</description>
///   </item>
///   <item>
///     <term><c>"Format"</c></term>
///     <description><see cref="Configuration.Format"/></description>
///     <description>The archive format (e.g., ZIP, GZip) used for decompression operations.</description>
///   </item>
///   <item>
///     <term><c>"Encoding"</c></term>
///     <description><see cref="System.Text.Encoding"/></description>
///     <description>The character encoding used during decompression.</description>
///   </item>
///   <item>
///     <term><c>"ZipFilePath"</c></term>
///     <description><see cref="string"/></description>
///     <description>The full path to the downloaded archive file.</description>
///   </item>
///   <item>
///     <term><c>"SourcePath"</c></term>
///     <description><see cref="string"/></description>
///     <description>The application installation target path.</description>
///   </item>
///   <item>
///     <term><c>"PatchPath"</c></term>
///     <description><see cref="string"/></description>
///     <description>The temporary storage path for differential patch files.</description>
///   </item>
///   <item>
///     <term><c>"PatchEnabled"</c></term>
///     <description><see cref="bool"/></description>
///     <description>Indicates whether the differential patch feature is enabled. If <c>false</c>, decompression results are written directly to <c>"SourcePath"</c>.</description>
///   </item>
///   <item>
///     <term><c>"DiffPipeline"</c></term>
///     <description><see cref="DiffPipeline"/></description>
///     <description>An instance of the differential patch pipeline, built and injected by <see cref="GeneralUpdateBootstrap"/>.</description>
///   </item>
/// </list>
/// </remarks>
public class PipelineContext
{
    private ConcurrentDictionary<string, object?> _context = new();

    /// <summary>
    /// Retrieves a strongly-typed value associated with the specified key from the context.
    /// </summary>
    /// <typeparam name="TValue">The expected type of the value. Returns <c>default</c> if the stored value is not of this type.</typeparam>
    /// <param name="key">The key to look up. Case-sensitive.</param>
    /// <returns>
    /// The strongly-typed value associated with the specified key; or <c>default(TValue)</c> if the key
    /// does not exist or the type does not match. Note that <c>default</c> is <c>null</c> for reference types
    /// and the zero value for value types.
    /// </returns>
    /// <remarks>
    /// Implemented using <see cref="ConcurrentDictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>,
    /// this is a thread-safe read operation. Typical usage:
    /// <code>
    /// var path = context.Get&lt;string&gt;("ZipFilePath");
    /// var format = context.Get&lt;Configuration.Format&gt;("Format");
    /// </code>
    /// </remarks>
    public TValue? Get<TValue>(string key)
    {
        if (_context.TryGetValue(key, out var value))
        {
            return value is TValue typedValue ? typedValue : default;
        }
        return default;
    }

    /// <summary>
    /// Adds or updates the value for the specified key in the context.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to store.</typeparam>
    /// <param name="key">The key name. Must not be <c>null</c> or whitespace.</param>
    /// <param name="value">The value to store. Can be <c>null</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="key"/> is <c>null</c> or consists only of whitespace characters.
    /// </exception>
    /// <remarks>
    /// If the specified key already exists, the existing value is overwritten with the new value
    /// (i.e., implements Upsert semantics). This operation is thread-safe. Typical usage:
    /// <code>
    /// context.Add("Hash", "A1B2C3D4E5F6...");
    /// context.Add("PatchEnabled", true);
    /// </code>
    /// </remarks>
    public void Add<TValue>(string key, TValue? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        _context[key] = value;
    }

    /// <summary>
    /// Removes the specified key and its associated value from the context.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><c>true</c> if the key existed and was successfully removed; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operation uses <see cref="ConcurrentDictionary{TKey, TValue}.TryRemove(TKey, out TValue)"/>
    /// and is thread-safe. If the specified key does not exist, it returns <c>false</c> without throwing an exception.
    /// </remarks>
    public bool Remove(string key) => _context.TryRemove(key, out _);

    /// <summary>
    /// Checks whether the specified key exists in the context.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operation uses <see cref="ConcurrentDictionary{TKey, TValue}.ContainsKey(TKey)"/>
    /// and is a thread-safe read-only check. It does not modify any data in the context.
    /// </remarks>
    public bool ContainsKey(string key) => _context.ContainsKey(key);
}
