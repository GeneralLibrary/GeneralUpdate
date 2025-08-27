using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Common.Internal.Event
{
    public class EventManager : IDisposable
    {
        private static readonly object _lockObj = new();
        private static EventManager _instance;
        private Dictionary<Type, Delegate> _dicDelegates = new();
        private bool _disposed = false;

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
            try
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
            catch (Exception e)
            {
                GeneralTracer.Error("The AddListener method in the EventManager class throws an exception.", e);
            }
        }

        public void RemoveListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            try
            {
                if (listener == null) throw new ArgumentNullException(nameof(listener));
                var delegateType = typeof(Action<object, TEventArgs>);
                if (_dicDelegates.TryGetValue(delegateType, out var existingDelegate))
                {
                    _dicDelegates[delegateType] = Delegate.Remove(existingDelegate, listener);
                }
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The RemoveListener method in the EventManager class throws an exception.", e);
            }
        }

        public void Dispatch<TEventArgs>(object sender, TEventArgs eventArgs) where TEventArgs : EventArgs
        {
            try
            {
                if (sender == null) throw new ArgumentNullException(nameof(sender));
                if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
                var delegateType = typeof(Action<object, TEventArgs>);
                if (_dicDelegates.TryGetValue(delegateType, out var existingDelegate))
                {
                    ((Action<object, TEventArgs>)existingDelegate)?.Invoke(sender, eventArgs);
                }
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The Dispatch method in the EventManager class throws an exception.", e);
            }
        }

        public void Clear() => _dicDelegates.Clear();

        public void Dispose()
        {
            try
            {
                if (!this._disposed)
                {
                    _dicDelegates.Clear();
                    _disposed = true;
                }
            }
            catch (Exception e)
            {
                GeneralTracer.Error("The Dispose method in the EventManager class throws an exception.", e);
            }
        }
    }
}