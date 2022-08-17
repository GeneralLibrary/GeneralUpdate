using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using System;

namespace GeneralUpdate.Core.Strategys
{
    public abstract class AbstractStrategy : IStrategy
    {
        public virtual void Create(Entity file, Action<object, MutiDownloadProgressChangedEventArgs> eventAction, Action<object, ExceptionEventArgs> errorEventAction) =>
            throw new NotImplementedException();

        public virtual void Excute() => throw new NotImplementedException();

        protected virtual bool StartApp(string appName) => throw new NotImplementedException();

        protected virtual string GetPlatform() => throw new NotImplementedException();
    }
}