using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Base class for strongly-typed update option keys.
    /// Simple implementation using ConcurrentDictionary registry,
    /// replacing the earlier Netty ConstantPool pattern.
    /// </summary>
    public class UpdateOption
    {
        private static readonly ConcurrentDictionary<string, UpdateOption> _registry = new();
        private static readonly object _lock = new();

        /// <summary>Unique option name.</summary>
        public string Name { get; }

        /// <summary>Protected constructor — use <see cref="ValueOf{T}(string, T)"/> to create instances.</summary>
        protected UpdateOption(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Returns the <see cref="UpdateOption{T}"/> for the given name, creating one with the
        /// provided default value if it does not exist. Subsequent calls with the same name
        /// return the same instance (singleton).
        /// </summary>
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

        /// <summary>Returns the option name.</summary>
        public override string ToString() => Name;

        /// <summary>Hash code based on name.</summary>
        public override int GetHashCode() => Name.GetHashCode();

        /// <summary>Equality based on name.</summary>
        public override bool Equals(object? obj)
            => obj is UpdateOption other && Name == other.Name;
    }

    /// <summary>
    /// Strongly-typed update option key with a default value.
    /// Instances are obtained via <see cref="UpdateOption.ValueOf{T}(string, T)"/>.
    /// </summary>
    public sealed class UpdateOption<T> : UpdateOption
    {
        /// <summary>Default value used when the option is not explicitly set.</summary>
        public T DefaultValue { get; }

        internal UpdateOption(string name, T defaultValue) : base(name)
        {
            DefaultValue = defaultValue;
        }
    }
}
