namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Wraps a concrete option value for storage in the options dictionary.
    /// </summary>
    public abstract class UpdateOptionValue
    {
        /// <summary>The option key this value belongs to.</summary>
        public abstract UpdateOption Option { get; }

        /// <summary>Returns the stored value as an object.</summary>
        public abstract object GetValue();
    }

    /// <summary>
    /// Strongly-typed option value wrapper.
    /// </summary>
    public sealed class UpdateOptionValue<T> : UpdateOptionValue
    {
        public override UpdateOption Option { get; }
        private readonly T _value;

        public UpdateOptionValue(UpdateOption<T> option, T value)
        {
            Option = option;
            _value = value;
        }

        public override object GetValue() => _value!;

        public override string ToString() => _value?.ToString() ?? string.Empty;
    }
}
