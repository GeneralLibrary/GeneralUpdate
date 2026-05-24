using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Event
{
    /// <summary>
    /// Thread-safe event manager using ConcurrentDictionary.
    /// Supports add/remove/dispatch without lock contention.
    /// </summary>
    public class EventManager : IDisposable
    {
        private static readonly Lazy<EventManager> _lazy = new(() => new EventManager());
        private ConcurrentDictionary<Type, Delegate> _dicDelegates = new();
        private bool _disposed;

        private EventManager() { }

        public static EventManager Instance => _lazy.Value;

        public void AddListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var type = typeof(Action<object, TEventArgs>);
            _dicDelegates.AddOrUpdate(type,
                _ => listener,
                (_, existing) => Delegate.Combine(existing, listener));
        }

        public void RemoveListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var type = typeof(Action<object, TEventArgs>);
            if (_dicDelegates.TryGetValue(type, out var existing))
            {
                var updated = Delegate.Remove(existing, listener);
                if (updated == null)
                    _dicDelegates.TryRemove(type, out _);
                else
                    _dicDelegates.TryUpdate(type, updated, existing);
            }
        }

        public void Dispatch<TEventArgs>(object sender, TEventArgs eventArgs) where TEventArgs : EventArgs
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));

            var type = typeof(Action<object, TEventArgs>);
            if (_dicDelegates.TryGetValue(type, out var existingDelegate))
            {
                // Invoke each handler individually so one handler's exception
                // doesn't prevent others from being called.
                foreach (var handler in existingDelegate.GetInvocationList())
                {
                    try
                    {
                        ((Action<object, TEventArgs>)handler).Invoke(sender, eventArgs);
                    }
                    catch (Exception e)
                    {
                        GeneralTracer.Error("EventManager.Dispatch handler threw an exception.", e);
                    }
                }
            }
        }

        public void Clear() => _dicDelegates.Clear();

        public void Dispose()
        {
            if (!_disposed)
            {
                _dicDelegates.Clear();
                _disposed = true;
            }
        }
    }
}
