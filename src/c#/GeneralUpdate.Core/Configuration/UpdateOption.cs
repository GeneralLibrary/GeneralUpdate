using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     强类型更新选项键的基类。
    ///     使用基于 <see cref="ConcurrentDictionary{TKey, TValue}" /> 的简单注册表实现，
    ///     替代了早期版本的 Netty ConstantPool 模式。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOption</c> 实现了选项名称的全局注册表，确保每个名称对应唯一的选项实例。
    ///         通过 <see cref="ValueOf{T}(string, T)" /> 静态方法获取或创建选项实例。
    ///     </para>
    ///     <para>
    ///         该类使用双重检查锁定（通过 <c>_lock</c> 对象）来保证线程安全地创建和注册选项实例。
    ///         底层的 <c>_registry</c> 字典使用 <see cref="ConcurrentDictionary{TKey, TValue}" />
    ///         以支持高并发读取。
    ///     </para>
    ///     <para>
    ///         使用示例：
    ///         <code>
    ///             var option = UpdateOption.ValueOf&lt;int&gt;("MAXCONCURRENCY", 3);
    ///             int defaultValue = option.DefaultValue; // 3
    ///         </code>
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOption{T}" />
    /// <seealso cref="UpdateOptions" />
    public class UpdateOption
    {
        /// <summary>
        ///     全局选项注册表，存储所有已创建的选项实例。
        ///     键为选项名称（字符串），值为 <see cref="UpdateOption" /> 实例。
        /// </summary>
        private static readonly ConcurrentDictionary<string, UpdateOption> _registry = new();

        /// <summary>
        ///     用于线程安全创建选项的同步锁对象。
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        ///     选项的唯一名称标识。
        ///     在整个应用程序生命周期中保持唯一。
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     受保护的构造函数，防止直接实例化。
        ///     应使用 <see cref="ValueOf{T}(string, T)" /> 静态方法创建实例。
        /// </summary>
        /// <param name="name">选项的唯一名称标识。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="name" /> 为 <c>null</c> 时抛出。</exception>
        protected UpdateOption(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        ///     获取或创建指定名称的 <see cref="UpdateOption{T}" /> 实例。
        ///     如果指定名称的选项已存在，则返回现有实例；否则使用提供的默认值创建新实例。
        ///     后续使用相同名称的调用将始终返回同一个实例（单例模式）。
        /// </summary>
        /// <typeparam name="T">选项值的类型。</typeparam>
        /// <param name="name">选项的唯一名称标识。</param>
        /// <param name="defaultValue">当选项未显式设置时使用的默认值。</param>
        /// <returns>
        ///     与<paramref name="name" />对应的 <see cref="UpdateOption{T}" /> 实例。
        /// </returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="name" /> 为 <c>null</c> 时抛出。</exception>
        public static UpdateOption<T> ValueOf<T>(string name, T defaultValue = default!)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(name, out var existing) && existing is UpdateOption<T> typed)
                    return typed;

                var option = new UpdateOption<T>(name, defaultValue);
                _registry[name] = option;
                return option;
            }
        }

        /// <summary>
        ///     返回选项的名称字符串。
        /// </summary>
        /// <returns>选项的唯一名称标识。</returns>
        public override string ToString() => Name;

        /// <summary>
        ///     基于选项名称的哈希码。
        /// </summary>
        /// <returns>选项名称的哈希码。</returns>
        public override int GetHashCode() => Name.GetHashCode();

        /// <summary>
        ///     基于选项名称判断两个选项是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>
        ///     如果 <paramref name="obj" /> 是 <see cref="UpdateOption" /> 且名称相同，
        ///     则返回 <c>true</c>；否则返回 <c>false</c>。
        /// </returns>
        public override bool Equals(object? obj)
            => obj is UpdateOption other && Name == other.Name;
    }

    /// <summary>
    ///     带默认值的强类型更新选项键。
    ///     通过 <see cref="UpdateOption.ValueOf{T}(string, T)" /> 获取实例。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOption{T}</c> 是一个密封类，继承自 <see cref="UpdateOption" />，
    ///         在其基类基础上添加了类型安全的默认值存储。
    ///     </para>
    ///     <para>
    ///         此类与 <see cref="UpdateOptionValue{T}" /> 配合使用——
    ///         <c>UpdateOption{T}</c> 定义选项的"键"和默认值，
    ///         <c>UpdateOptionValue{T}</c> 则存储选项的运行时"值"。
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOption" />
    /// <seealso cref="UpdateOptionValue{T}" />
    /// <seealso cref="UpdateOptions" />
    public sealed class UpdateOption<T> : UpdateOption
    {
        /// <summary>
        ///     当选项未显式设置时使用的默认值。
        /// </summary>
        public T DefaultValue { get; }

        /// <summary>
        ///     内部构造函数，通过 <see cref="UpdateOption.ValueOf{T}(string, T)" /> 调用。
        /// </summary>
        /// <param name="name">选项的唯一名称标识。</param>
        /// <param name="defaultValue">选项的默认值。</param>
        internal UpdateOption(string name, T defaultValue) : base(name)
        {
            DefaultValue = defaultValue;
        }
    }
}
