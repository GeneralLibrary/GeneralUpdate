namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     封装具体选项值以便存储在选项字典中的抽象基类。
    ///     提供统一的类型擦除接口，允许不同类型的选项值以多态方式存储在同一个集合中。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptionValue</c> 是 <see cref="UpdateOptionValue{T}" /> 的抽象基类，
    ///         通过非泛型的 <see cref="Option" /> 和 <see cref="GetValue" /> 方法提供了
    ///         统一访问选项键和值的接口，而无需在调用方感知具体类型。
    ///     </para>
    ///     <para>
    ///         与 <see cref="UpdateOption{T}" /> 的关系：<c>UpdateOption{T}</c> 定义了选项的
    ///         "键"（含名称和默认值），而 <c>UpdateOptionValue{T}</c> 存储了该选项的运行时"值"。
    ///         两者通过 <see cref="Option" /> 属性关联。
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateOptionValue{T}" />
    /// <seealso cref="UpdateOption{T}" />
    public abstract class UpdateOptionValue
    {
        /// <summary>
        ///     此值所对应的选项键。
        ///     返回 <see cref="UpdateOption" /> 实例，标识此值属于哪个选项。
        /// </summary>
        public abstract UpdateOption Option { get; }

        /// <summary>
        ///     以 <see cref="object" /> 类型返回存储的值。
        ///     调用方需要自行转换为正确的类型。
        /// </summary>
        /// <returns>存储的选项值。<c>object</c> 类型，可能需要拆箱或强制类型转换。</returns>
        public abstract object GetValue();
    }

    /// <summary>
    ///     强类型的选项值包装器。
    ///     存储选项的运行时值，并与对应的 <see cref="UpdateOption{T}" /> 键关联。
    /// </summary>
    /// <typeparam name="T">选项值的具体类型。</typeparam>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateOptionValue{T}</c> 是对运行时选项值的强类型封装。
    ///         它持有一个对 <see cref="UpdateOption{T}" /> 的引用（通过 <see cref="Option" /> 属性），
    ///         以及该选项的当前值（通过 <c>_value</c> 字段）。
    ///     </para>
    ///     <para>
    ///         使用示例：
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
        ///     此值所对应的选项键。
        /// </summary>
        public override UpdateOption Option { get; }

        /// <summary>
        ///     内部存储的选项值。
        /// </summary>
        private readonly T _value;

        /// <summary>
        ///     使用指定的选项键和值创建 <see cref="UpdateOptionValue{T}" /> 实例。
        /// </summary>
        /// <param name="option">与此值关联的 <see cref="UpdateOption{T}" /> 选项键。</param>
        /// <param name="value">要存储的选项值。</param>
        public UpdateOptionValue(UpdateOption<T> option, T value)
        {
            Option = option;
            _value = value;
        }

        /// <summary>
        ///     以 <see cref="object" /> 类型返回存储的值。
        /// </summary>
        /// <returns>存储的选项值，类型为 <typeparamref name="T" /> 但以 <see cref="object" /> 形式返回。</returns>
        public override object GetValue() => _value!;

        /// <summary>
        ///     返回值的字符串表示形式。
        /// </summary>
        /// <returns>
        ///     如果值不为 <c>null</c>，则返回 <c>_value.ToString()</c>；
        ///     否则返回 <see cref="string.Empty" />。
        /// </returns>
        public override string ToString() => _value?.ToString() ?? string.Empty;
    }
}
