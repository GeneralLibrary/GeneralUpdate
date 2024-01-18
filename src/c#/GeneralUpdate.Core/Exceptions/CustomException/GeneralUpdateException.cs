using GeneralUpdate.Core.Exceptions.CustomArgs;
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace GeneralUpdate.Core.Exceptions.CustomException
{
    /// <summary>
    /// Exception of GeneralUpdate framework.
    /// </summary>
    [Serializable]
    public sealed class GeneralUpdateException<TExceptionArgs> : Exception, ISerializable
        where TExceptionArgs : ExceptionArgs
    {
        private const String c_args = "Args";
        private readonly TExceptionArgs m_args;

        public TExceptionArgs Args => m_args;

        public GeneralUpdateException(String message = null, Exception innerException = null) : this(null, message, innerException)
        {
        }

        public GeneralUpdateException(TExceptionArgs args, String message = null, Exception innerException = null) : base(message, innerException) => m_args = args;

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        private GeneralUpdateException(SerializationInfo info, StreamingContext context) : base(info, context) => m_args = (TExceptionArgs)info.GetValue(c_args, typeof(TExceptionArgs));

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(c_args, typeof(TExceptionArgs));
            base.GetObjectData(info, context);
        }

        public override string Message
        {
            get
            {
                String baseMsg = base.Message;
                return (m_args == null) ? baseMsg : $"{baseMsg}({m_args.Message})";
            }
        }

        public override bool Equals(object obj)
        {
            GeneralUpdateException<TExceptionArgs> other = obj as GeneralUpdateException<TExceptionArgs>;
            if (other == null) return false;
            return Object.Equals(m_args, other.m_args) && base.Equals(obj);
        }

        public override int GetHashCode() => base.GetHashCode();
    }
}