using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;

namespace GeneralUpdate.Core.Bootstrap
{
    public abstract class UpdateOption : AbstractConstant<UpdateOption>
    {
        private class UpdateOptionPool : ConstantPool
        {
            protected override IConstant NewConstant<T>(int id, string name) => new UpdateOption<T>(id, name);
        }

        private static readonly UpdateOptionPool Pool = new UpdateOptionPool();

        public static UpdateOption<T> ValueOf<T>(string name) => (UpdateOption<T>)Pool.ValueOf<T>(name);

        #region parameter configuration

        /// <summary>
        /// Update the file format of the package.
        /// </summary>
        public static readonly UpdateOption<string> Format = ValueOf<string>("COMPRESSFORMAT");

        /// <summary>
        /// Compress encoding.
        /// </summary>
        public static readonly UpdateOption<Encoding> Encoding = ValueOf<Encoding>("COMPRESSENCODING");

        /// <summary>
        /// Main program name.
        /// </summary>
        public static readonly UpdateOption<string> MainApp = ValueOf<string>("MAINAPP");

        /// <summary>
        /// Timeout period (unit: second). If this parameter is not specified, the default timeout period is 30 seconds.
        /// </summary>
        public static readonly UpdateOption<int> DownloadTimeOut = ValueOf<int>("DOWNLOADTIMEOUT");

        /// <summary>
        /// Whether to enable configuration file update.
        /// </summary>
        public static readonly UpdateOption<bool> UpdateConfig = ValueOf<bool>("UPDATECONFIG");

        #endregion parameter configuration

        internal UpdateOption(int id, string name)
          : base(id, name) { }

        public abstract bool Set(IUpdateConfiguration configuration, object value);
    }

    public sealed class UpdateOption<T> : UpdateOption
    {
        internal UpdateOption(int id, string name)
            : base(id, name)
        {
        }

        public void Validate(T value) => Contract.Requires(value != null);

        public override bool Set(IUpdateConfiguration configuration, object value) => configuration.SetOption(this, (T)value);
    }

    public abstract class ConstantPool
    {
        private readonly Dictionary<string, IConstant> constants = new Dictionary<string, IConstant>();
        private int nextId = 1;

        /// <summary>Shortcut of <c>this.ValueOf(firstNameComponent.Name + "#" + secondNameComponent)</c>.</summary>
        public IConstant ValueOf<T>(Type firstNameComponent, string secondNameComponent)
        {
            Contract.Requires(firstNameComponent != null);
            Contract.Requires(secondNameComponent != null);
            return this.ValueOf<T>(firstNameComponent.Name + '#' + secondNameComponent);
        }

        /// <summary>
        ///     Returns the <see cref="IConstant" /> which is assigned to the specified <c>name</c>.
        ///     If there's no such <see cref="IConstant" />, a new one will be created and returned.
        ///     Once created, the subsequent calls with the same <c>name</c> will always return the previously created one
        ///     (i.e. singleton.)
        /// </summary>
        /// <param name="name">the name of the <see cref="IConstant" /></param>
        public IConstant ValueOf<T>(string name)
        {
            IConstant c;
            lock (this.constants)
            {
                if (this.constants.TryGetValue(name, out c))
                {
                    return c;
                }
                else
                {
                    c = this.NewInstance0<T>(name);
                }
            }
            return c;
        }

        /// <summary>Returns <c>true</c> if a <see cref="AttributeKey{T}" /> exists for the given <c>name</c>.</summary>
        public bool Exists(string name)
        {
            CheckNotNullAndNotEmpty(name);
            lock (this.constants)
                return this.constants.ContainsKey(name);
        }

        /// <summary>
        ///     Creates a new <see cref="IConstant" /> for the given <c>name</c> or fail with an
        ///     <see cref="ArgumentException" /> if a <see cref="IConstant" /> for the given <c>name</c> exists.
        /// </summary>
        public IConstant NewInstance<T>(string name)
        {
            if (this.Exists(name)) throw new ArgumentException($"'{name}' is already in use");
            IConstant c = this.NewInstance0<T>(name);
            return c;
        }

        // Be careful that this dose not check whether the argument is null or empty.
        private IConstant NewInstance0<T>(string name)
        {
            lock (this.constants)
            {
                IConstant c = this.NewConstant<T>(this.nextId, name);
                this.constants[name] = c;
                this.nextId++;
                return c;
            }
        }

        private static void CheckNotNullAndNotEmpty(string name) => Contract.Requires(!string.IsNullOrEmpty(name));

        protected abstract IConstant NewConstant<T>(int id, string name);

        [Obsolete]
        public int NextId()
        {
            lock (this.constants)
            {
                int id = this.nextId;
                this.nextId++;
                return id;
            }
        }
    }

    public interface IConstant
    {
        /// <summary>Returns the unique number assigned to this <see cref="IConstant" />.</summary>
        int Id { get; }

        /// <summary>Returns the name of this <see cref="IConstant" />.</summary>
        string Name { get; }
    }

    public interface IUpdateConfiguration
    {
        T GetOption<T>(UpdateOption<T> option);

        bool SetOption(UpdateOption option, object value);

        bool SetOption<T>(UpdateOption<T> option, T value);
    }

    public abstract class AbstractConstant : IConstant
    {
        private static long nextUniquifier;
        private long volatileUniquifier;

        protected AbstractConstant(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        public int Id { get; }

        public string Name { get; }

        public override sealed string ToString() => this.Name;

        protected long Uniquifier
        {
            get
            {
                long result;
                if ((result = Volatile.Read(ref this.volatileUniquifier)) == 0)
                {
                    result = Interlocked.Increment(ref nextUniquifier);
                    long previousUniquifier = Interlocked.CompareExchange(ref this.volatileUniquifier, result, 0);
                    if (previousUniquifier != 0) result = previousUniquifier;
                }
                return result;
            }
        }
    }

    public abstract class AbstractConstant<T> : AbstractConstant, IComparable<T>, IEquatable<T>
        where T : AbstractConstant<T>
    {
        /// <summary>Creates a new instance.</summary>
        protected AbstractConstant(int id, string name)
            : base(id, name) { }

        public override sealed int GetHashCode() => base.GetHashCode();

        public override sealed bool Equals(object obj) => base.Equals(obj);

        public bool Equals(T other) => ReferenceEquals(this, other);

        public int CompareTo(T o)
        {
            if (ReferenceEquals(this, o)) return 0;
            AbstractConstant<T> other = o;
            int returnCode = this.GetHashCode() - other.GetHashCode();
            if (returnCode != 0) return returnCode;
            long thisUV = this.Uniquifier;
            long otherUV = other.Uniquifier;
            if (thisUV < otherUV) return -1;
            if (thisUV > otherUV) return 1;
            throw new Exception("failed to compare two different constants");
        }
    }
}