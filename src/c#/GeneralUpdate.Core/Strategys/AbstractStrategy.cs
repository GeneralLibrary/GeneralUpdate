using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys
{
    public abstract class AbstractStrategy : IStrategy
    {
        protected const string PATCHS = "patchs";

        public virtual void Create(Entity entity, Action<object, MutiDownloadProgressChangedEventArgs> eventAction, Action<object, ExceptionEventArgs> errorEventAction) =>
            throw new NotImplementedException();

        public virtual void Excute() => throw new NotImplementedException();

        public virtual bool StartApp(string appName, int appType) => throw new NotImplementedException();

        public virtual string GetPlatform() => throw new NotImplementedException();

        public virtual Task ExcuteTaskAsync() => throw new NotImplementedException();

        public virtual void Create<T>(T parameter) where T : class => throw new NotImplementedException();
    }
}