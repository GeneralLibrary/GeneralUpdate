using System;
using System.Collections.Generic;

namespace GeneralUpdate.Common.Internal.Event
{
    public class EventManager : IDisposable
    {
        private static readonly object _lockObj = new object();
        private static EventManager _instance;
        private Dictionary<Type, Delegate> _dicDelegates = new Dictionary<Type, Delegate>();
        private bool disposed = false;

        private EventManager() { }

        public static EventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                            _instance = new EventManager();
                    }
                }
                return _instance;
            }
        }

        public void AddListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var delegateType = typeof(Action<object, TEventArgs>);
            if (_dicDelegates.ContainsKey(delegateType))
            {
                _dicDelegates[delegateType] = Delegate.Combine(_dicDelegates[delegateType], listener);
            }
            else
            {
                _dicDelegates.Add(delegateType, listener);
            }
        }

        public void RemoveListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var delegateType = typeof(Action<object, TEventArgs>);
            if (_dicDelegates.TryGetValue(delegateType, out var existingDelegate))
            {
                _dicDelegates[delegateType] = Delegate.Remove(existingDelegate, listener);
            }
        }

        public void Dispatch<TEventArgs>(object sender, TEventArgs eventArgs) where TEventArgs : EventArgs
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
            var delegateType = typeof(Action<object, TEventArgs>);
            if (_dicDelegates.TryGetValue(delegateType, out var existingDelegate))
            {
                ((Action<object, TEventArgs>)existingDelegate)?.Invoke(sender, eventArgs);
            }
        }

        public void Clear() => _dicDelegates.Clear();

        public void Dispose()
        {
            if (!this.disposed)
            {
                _dicDelegates.Clear();
                disposed = true;
            }
        }
    }
}