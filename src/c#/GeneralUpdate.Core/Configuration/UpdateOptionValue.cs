namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Abstract base class that encapsulates a specific option value for storage in an option dictionary.
    ///     Provides a unified, type-erased interface that allows option values of different types to be stored
    ///     polymorphically in the same collection.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptionValue</c> is the abstract base class for <see cref="UpdateOptionValue{T}" />.
    ///         It provides a unified interface for accessing the option key and value through the non-generic
    ///         <see cref="Option" /> and <see cref="GetValue" /> methods, without requiring the caller to know
    ///         the concrete type.
    ///     </para>
    ///     <para>
    ///         Relationship with <see cref="UpdateOption{T}" />: <c>UpdateOption{T}</c> defines the option's
    ///         "key" (with name and default value), while <c>UpdateOptionValue{T}</c> stores the option's
    ///         runtime "value". The two are linked via the <see cref="Option" /> property.
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOptionValue{T}" />
    /// <seealso cref="UpdateOption{T}" />
    public abstract class UpdateOptionValue
    {
        /// <summary>
        ///     The option key that this value corresponds to.
        ///     Returns the <see cref="UpdateOption" /> instance identifying which option this value belongs to.
        /// </summary>
        public abstract UpdateOption Option { get; }

        /// <summary>
        ///     Returns the stored value as an <see cref="object" />.
        ///     Callers must cast to the correct type themselves.
        /// </summary>
        /// <returns>The stored option value as an <see cref="object" />. May require unboxing or type casting.</returns>
        public abstract object GetValue();
    }

    /// <summary>
    ///     A strongly-typed option value wrapper.
    ///     Stores the runtime value of an option and associates it with the corresponding
    ///     <see cref="UpdateOption{T}" /> key.
    /// </summary>
    /// <typeparam name="T">The concrete type of the option value.</typeparam>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptionValue{T}</c> is a strongly-typed wrapper around a runtime option value.
    ///         It holds a reference to the <see cref="UpdateOption{T}" /> (via the <see cref="Option" /> property)
    ///         and the current value (via the <c>_value</c> field).
    ///     </para>
    ///     <para>
    ///         Usage example:
    ///         <code>
    ///             var option = UpdateOption.ValueOf&lt;int&gt;("MAXCONCURRENCY", 3);
    ///             var value = new UpdateOptionValue&lt;int&gt;(option, 5);
    ///             int currentValue = (int)value.GetValue(); // 5
    ///         </code>
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOptionValue" />
    /// <seealso cref="UpdateOption{T}" />
    public sealed class UpdateOptionValue<T> : UpdateOptionValue
    {
        /// <summary>
        ///     The option key that this value corresponds to.
        /// </summary>
        public override UpdateOption Option { get; }

        /// <summary>
        ///     The internally stored option value.
        /// </summary>
        private readonly T _value;

        /// <summary>
        ///     Creates an <see cref="UpdateOptionValue{T}" /> instance with the specified option key and value.
        /// </summary>
        /// <param name="option">The <see cref="UpdateOption{T}" /> option key associated with this value.</param>
        /// <param name="value">The option value to store.</param>
        public UpdateOptionValue(UpdateOption<T> option, T value)
        {
            Option = option;
            _value = value;
        }

        /// <summary>
        ///     Returns the stored value as an <see cref="object" />.
        /// </summary>
        /// <returns>The stored option value of type <typeparamref name="T" /> returned as <see cref="object" />.</returns>
        public override object GetValue() => _value!;

        /// <summary>
        ///     Returns the string representation of the value.
        /// </summary>
        /// <returns>
        ///     <c>_value.ToString()</c> if the value is not <c>null</c>;
        ///     otherwise, <see cref="string.Empty" />.
        /// </returns>
        public override string ToString() => _value?.ToString() ?? string.Empty;
    }
}
