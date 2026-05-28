using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Base class for strongly-typed update option keys.
    ///     Uses a simple registry pattern backed by <see cref="ConcurrentDictionary{TKey, TValue}" />,
    ///     replacing the Netty ConstantPool pattern used in earlier versions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOption</c> implements a global registry for option names, ensuring each name maps to a
    ///         unique option instance. Instances are obtained or created via the
    ///         <see cref="ValueOf{T}(string, T)" /> static method.
    ///     </para>
    ///     <para>
    ///         This class uses double-checked locking (via the <c>_lock</c> object) to ensure thread-safe creation
    ///         and registration of option instances. The underlying <c>_registry</c> dictionary uses
    ///         <see cref="ConcurrentDictionary{TKey, TValue}" /> to support high-concurrency reads.
    ///     </para>
    ///     <para>
    ///         Usage example:
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
        ///     The global option registry storing all created option instances.
        ///     Keys are option names (strings), values are <see cref="UpdateOption" /> instances.
        /// </summary>
        private static readonly ConcurrentDictionary<string, UpdateOption> _registry = new();

        /// <summary>
        ///     The synchronization lock object for thread-safe option creation.
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        ///     The unique name identifier for this option.
        ///     Remains unique throughout the application lifetime.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Protected constructor to prevent direct instantiation.
        ///     Instances should be created via the <see cref="ValueOf{T}(string, T)" /> static method.
        /// </summary>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
        protected UpdateOption(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        ///     Gets or creates an <see cref="UpdateOption{T}" /> instance for the specified name.
        ///     If an option with the specified name already exists, the existing instance is returned;
        ///     otherwise, a new instance is created with the provided default value.
        ///     Subsequent calls with the same name will always return the same instance (singleton pattern).
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <param name="defaultValue">The default value used when the option is not explicitly set.</param>
        /// <returns>
        ///     The <see cref="UpdateOption{T}" /> instance corresponding to <paramref name="name" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
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
        ///     Returns the string representation of the option name.
        /// </summary>
        /// <returns>The unique name identifier of the option.</returns>
        public override string ToString() => Name;

        /// <summary>
        ///     Returns the hash code based on the option name.
        /// </summary>
        /// <returns>The hash code of the option name.</returns>
        public override int GetHashCode() => Name.GetHashCode();

        /// <summary>
        ///     Determines whether two options are equal based on their names.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>
        ///     <c>true</c> if <paramref name="obj" /> is an <see cref="UpdateOption" /> with the same name;
        ///     otherwise <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
            => obj is UpdateOption other && Name == other.Name;
    }

    /// <summary>
    ///     A strongly-typed update option key with a default value.
    ///     Instances are obtained via <see cref="UpdateOption.ValueOf{T}(string, T)" />.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOption{T}</c> is a sealed class that inherits from <see cref="UpdateOption" />,
    ///         adding type-safe default value storage on top of the base class.
    ///     </para>
    ///     <para>
    ///         This class works together with <see cref="UpdateOptionValue{T}" /> —
    ///         <c>UpdateOption{T}</c> defines the option's "key" and default value,
    ///         while <c>UpdateOptionValue{T}</c> stores the option's runtime "value".
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOption" />
    /// <seealso cref="UpdateOptionValue{T}" />
    /// <seealso cref="UpdateOptions" />
    public sealed class UpdateOption<T> : UpdateOption
    {
        /// <summary>
        ///     The default value used when the option is not explicitly set.
        /// </summary>
        public T DefaultValue { get; }

        /// <summary>
        ///     Internal constructor called by <see cref="UpdateOption.ValueOf{T}(string, T)" />.
        /// </summary>
        /// <param name="name">The unique name identifier for the option.</param>
        /// <param name="defaultValue">The default value for the option.</param>
        internal UpdateOption(string name, T defaultValue) : base(name)
        {
            DefaultValue = defaultValue;
        }
    }
}
