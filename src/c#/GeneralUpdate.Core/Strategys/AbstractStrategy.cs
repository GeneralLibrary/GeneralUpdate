using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Strategys
{
    public abstract class AbstractStrategy : IStrategy
    {
        protected const string PATCHS = "patchs";

        public virtual void Excute() => throw new NotImplementedException();

        public virtual bool StartApp(string appName, int appType) => throw new NotImplementedException();

        public virtual string GetPlatform() => throw new NotImplementedException();

        public virtual Task ExecuteTaskAsync() => throw new NotImplementedException();

        public virtual void Create<T>(T parameter) where T : class => throw new NotImplementedException();
    }
}