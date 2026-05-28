using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// 管道上下文，为中间件提供线程安全的键值对存储，用于在管道的各个阶段之间共享数据。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PipelineContext"/> 是中间件之间通信的核心载体。每个中间件可以从上下文中读取上游中间件产生的结果，
/// 并将自身的处理结果写入上下文，供下游中间件使用。
/// <see cref="PipelineBuilder"/> 在构造时接收一个上下文实例，该实例在整个管道生命周期内保持不变。
/// </para>
/// <para>
/// 内部使用 <see cref="ConcurrentDictionary{TKey, TValue}"/> 保证线程安全，支持在多线程环境中安全地读写。
/// </para>
/// <para>
/// 以下是更新管道中预定义的上下文键及其说明：
/// </para>
/// <list type="table">
///   <listheader>
///     <term>键名</term>
///     <description>类型</description>
///     <description>说明</description>
///   </listheader>
///   <item>
///     <term><c>"Hash"</c></term>
///     <description><see cref="string"/></description>
///     <description>期望的 SHA256 哈希值，用于验证下载的压缩包完整性。</description>
///   </item>
///   <item>
///     <term><c>"Format"</c></term>
///     <description><see cref="Configuration.Format"/></description>
///     <description>压缩包格式（如 ZIP、GZip 等），用于解压缩操作。</description>
///   </item>
///   <item>
///     <term><c>"Encoding"</c></term>
///     <description><see cref="System.Text.Encoding"/></description>
///     <description>解压缩时使用的字符编码。</description>
///   </item>
///   <item>
///     <term><c>"ZipFilePath"</c></term>
///     <description><see cref="string"/></description>
///     <description>已下载的压缩包文件完整路径。</description>
///   </item>
///   <item>
///     <term><c>"SourcePath"</c></term>
///     <description><see cref="string"/></description>
///     <description>应用程序安装目标路径。</description>
///   </item>
///   <item>
///     <term><c>"PatchPath"</c></term>
///     <description><see cref="string"/></description>
///     <description>差异补丁文件的临时存放路径。</description>
///   </item>
///   <item>
///     <term><c>"PatchEnabled"</c></term>
///     <description><see cref="bool"/></description>
///     <description>指示是否启用了差异补丁功能。若为 <c>false</c>，解压缩结果将直接写入 <c>"SourcePath"</c>。</description>
///   </item>
///   <item>
///     <term><c>"DiffPipeline"</c></term>
///     <description><see cref="DiffPipeline"/></description>
///     <description>差异补丁管道的实例，由 <see cref="GeneralUpdateBootstrap"/> 构建并注入。</description>
///   </item>
/// </list>
/// </remarks>
public class PipelineContext
{
    private ConcurrentDictionary<string, object?> _context = new();

    /// <summary>
    /// 从上下文中获取指定键的强类型值。
    /// </summary>
    /// <typeparam name="TValue">值的期望类型。如果存储的值不是此类型，则返回 <c>default</c>。</typeparam>
    /// <param name="key">要查找的键。区分大小写。</param>
    /// <returns>
    /// 与指定键关联的强类型值；如果键不存在或类型不匹配，则返回 <c>default(TValue)</c>。
    /// 注意，对于引用类型，<c>default</c> 为 <c>null</c>；对于值类型，<c>default</c> 为零值。
    /// </returns>
    /// <remarks>
    /// 使用 <see cref="ConcurrentDictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
    /// 实现，是线程安全的读取操作。典型用法：
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
    /// 向上下文添加或更新指定键的值。
    /// </summary>
    /// <typeparam name="TValue">要存储的值的类型。</typeparam>
    /// <param name="key">键名，不能为 <c>null</c> 或空白字符串。</param>
    /// <param name="value">要存储的值。可以为 <c>null</c>。</param>
    /// <exception cref="ArgumentException">
    /// 当 <paramref name="key"/> 为 <c>null</c> 或仅包含空白字符时引发。
    /// </exception>
    /// <remarks>
    /// 如果指定键已存在，则使用新值覆盖旧值（即实现 Upsert 语义）。
    /// 此操作是线程安全的。典型用法：
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
    /// 从上下文中移除指定键及其关联值。
    /// </summary>
    /// <param name="key">要移除的键名。</param>
    /// <returns>如果键存在并被成功移除，则为 <c>true</c>；否则为 <c>false</c>。</returns>
    /// <remarks>
    /// 此操作使用 <see cref="ConcurrentDictionary{TKey, TValue}.TryRemove(TKey, out TValue)"/>
    /// 实现，是线程安全的。如果指定的键不存在，则返回 <c>false</c> 且不引发异常。
    /// </remarks>
    public bool Remove(string key) => _context.TryRemove(key, out _);

    /// <summary>
    /// 检查上下文中是否包含指定的键。
    /// </summary>
    /// <param name="key">要检查的键名。</param>
    /// <returns>如果键存在，则为 <c>true</c>；否则为 <c>false</c>。</returns>
    /// <remarks>
    /// 此操作使用 <see cref="ConcurrentDictionary{TKey, TValue}.ContainsKey(TKey)"/>
    /// 实现，是线程安全的只读检查。不会修改上下文中的任何数据。
    /// </remarks>
    public bool ContainsKey(string key) => _context.ContainsKey(key);
}
