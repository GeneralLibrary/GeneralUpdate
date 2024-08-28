namespace GeneralUpdate.Core.Bootstrap
{
    public abstract class UpdateOptionValue
    {
        public abstract UpdateOption Option { get; }

        public abstract bool Set(IUpdateConfiguration config);

        public abstract object GetValue();
    }

    public sealed class UpdateOptionValue<T> : UpdateOptionValue
    {
        public override UpdateOption Option { get; }
        private readonly T value;

        public UpdateOptionValue(UpdateOption<T> option, T value)
        {
            this.Option = option;
            this.value = value;
        }

        public override object GetValue() => this.value;

        public override bool Set(IUpdateConfiguration config) => config.SetOption(this.Option, this.value);

        public override string ToString() => this.value.ToString();
    }
}