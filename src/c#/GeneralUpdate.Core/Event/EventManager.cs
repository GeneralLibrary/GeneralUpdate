using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Event
{
    /// <summary>
    /// Thread-safe event manager implementing the publish-subscribe pattern, backed by
    /// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Supports lock-free addition, removal, and dispatching of event handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EventManager uses the singleton pattern (via the <see cref="Instance"/> property)
    /// and serves as the central dispatch hub for all events throughout the GeneralUpdate
    /// update lifecycle.
    /// </para>
    /// <para>
    /// Core design principles:
    /// <list type="bullet">
    ///   <item><description><b>Singleton</b>: A single global instance ensures all components share the same event bus.</description></item>
    ///   <item><description><b>Thread-safe</b>: Uses <c>ConcurrentDictionary</c> to avoid lock contention.</description></item>
    ///   <item><description><b>Generic events</b>: Uses <c>Action&lt;object, TEventArgs&gt;</c> as the event delegate type,
    ///   automatically routing by <c>TEventArgs</c> type.</description></item>
    ///   <item><description><b>Error isolation</b>: An exception in one handler does not affect other handlers.</description></item>
    ///   <item><description><b>IDisposable</b>: Clears all registered handlers on disposal.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This manager is used internally by <see cref="GeneralUpdate.Core.Bootstrap.GeneralUpdateBootstrap"/>
    /// to fire events at key points in the update flow (e.g., download progress, exceptions, completion).
    /// External consumers can subscribe to all event types in bulk via the <see cref="IUpdateEventListener"/> interface.
    /// </para>
    /// </remarks>
    public class EventManager : IDisposable
    {
        private static readonly Lazy<EventManager> _lazy = new(() => new EventManager());
        private ConcurrentDictionary<Type, Delegate> _dicDelegates = new();
        private bool _disposed;

        private EventManager() { }

        /// <summary>
        /// Gets the singleton instance of <see cref="EventManager"/>.
        /// </summary>
        /// <value>The global unique event manager instance.</value>
        /// <remarks>
        /// Uses <see cref="Lazy{T}"/> for thread-safe lazy initialization.
        /// </remarks>
        public static EventManager Instance => _lazy.Value;

        /// <summary>
        /// Registers a listener for a specified event type.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event argument, which must be a subclass of <see cref="EventArgs"/>.</typeparam>
        /// <param name="listener">The event handler delegate to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listener"/> is <c>null</c>.</exception>
        /// <remarks>
        /// Multiple listeners can be registered for the same event type. Internally, this uses
        /// <see cref="Delegate.Combine"/> to build a multicast delegate.
        /// Adding the same listener instance multiple times will cause it to be invoked multiple times.
        /// </remarks>
        public void AddListener<TEventArgs>(Action<object, TEventArgs> listener) where TEventArgs : EventArgs
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var type = typeof(Action<object, TEventArgs>);
            _dicDelegates.AddOrUpdate(type,
                _ => listener,
                (_, existing) => Delegate.Combine(existing, listener));
        }

        /// <summary>
        /// Removes a listener for a specified event type.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event argument, which must be a subclass of <see cref="EventArgs"/>.</typeparam>
        /// <param name="listener">The event handler delegate to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listener"/> is <c>null</c>.</exception>
        /// <remarks>
        /// If the delegate list for this event type becomes empty after removal, the entry is
        /// automatically removed from the dictionary.
        /// </remarks>
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

        /// <summary>
        /// Dispatches an event of the specified type to all registered listeners.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event argument, which must be a subclass of <see cref="EventArgs"/>.</typeparam>
        /// <param name="sender">The event sender.</param>
        /// <param name="eventArgs">The event arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sender"/> or <paramref name="eventArgs"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// Dispatch strategy:
        /// <list type="bullet">
        ///   <item><description>Looks up the delegate list automatically by the <c>TEventArgs</c> type.</description></item>
        ///   <item><description>Invokes each registered listener individually, ensuring that an exception
        ///   in one listener does not prevent others from being called.</description></item>
        ///   <item><description>Exceptions thrown inside listeners are logged via <see cref="GeneralTracer"/>
        ///   and are not rethrown.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Clears all registered event listeners.
        /// </summary>
        /// <remarks>
        /// This operation is irreversible. After calling this method, all handlers registered via
        /// <see cref="AddListener{TEventArgs}"/> are removed.
        /// </remarks>
        public void Clear() => _dicDelegates.Clear();

        /// <summary>
        /// Releases all resources used by the <see cref="EventManager"/> and clears all registered listeners.
        /// </summary>
        /// <remarks>
        /// Implements <see cref="IDisposable"/> to ensure event subscriptions are cleaned up when the
        /// component lifecycle ends, preventing memory leaks. Multiple calls are safe; subsequent calls
        /// will not perform any cleanup.
        /// </remarks>
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
