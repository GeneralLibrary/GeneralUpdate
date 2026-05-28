using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Event
{
    /// <summary>
    /// 线程安全的事件管理器（发布-订阅模式），基于 <see cref="ConcurrentDictionary{TKey, TValue}"/> 实现。
    /// 支持无锁竞争地添加、移除和分发事件处理器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// EventManager 使用单例模式（通过 <see cref="Instance"/> 属性），
    /// 是整个 GeneralUpdate 更新生命周期中所有事件的统一调度中心。
    /// </para>
    /// <para>
    /// 核心设计原则：
    /// <list type="bullet">
    ///   <item><description><b>单例</b>：全局唯一实例，确保所有组件共享同一个事件总线。</description></item>
    ///   <item><description><b>线程安全</b>：使用 <c>ConcurrentDictionary</c> 避免锁竞争。</description></item>
    ///   <item><description><b>泛型事件</b>：以 <c>Action&lt;object, TEventArgs&gt;</c> 为事件委托类型，
    ///   按 <c>TEventArgs</c> 类型自动路由。</description></item>
    ///   <item><description><b>错误隔离</b>：单个处理器的异常不会影响其他处理器。</description></item>
    ///   <item><description><b>IDisposable</b>：支持释放时清除所有已注册的处理器。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 此管理器被 <see cref="GeneralUpdate.Core.Bootstrap.GeneralUpdateBootstrap"/> 内部使用，
    /// 在更新流程的关键节点（如下载进度、异常、完成等）触发事件。
    /// 外部可以通过 <see cref="IUpdateEventListener"/> 接口批量订阅所有事件类型。
    /// </para>
    /// </remarks>
    public class EventManager : IDisposable
    {
        private static readonly Lazy<EventManager> _lazy = new(() => new EventManager());
        private ConcurrentDictionary<Type, Delegate> _dicDelegates = new();
        private bool _disposed;

        private EventManager() { }

        /// <summary>
        /// 获取 <see cref="EventManager"/> 的单例实例。
        /// </summary>
        /// <value>全局唯一的事件管理器实例。</value>
        /// <remarks>
        /// 使用 <see cref="Lazy{T}"/> 实现线程安全的延迟初始化。
        /// </remarks>
        public static EventManager Instance => _lazy.Value;

                /// <summary>
        /// 注册一个指定事件类型的监听器。
        /// </summary>
        /// <typeparam name="TEventArgs">事件参数的类型，必须是 <see cref="EventArgs"/> 的子类。</typeparam>
        /// <param name="listener">要注册的事件处理器委托。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener"/> 为 <c>null</c> 时抛出。</exception>
        /// <remarks>
        /// 同一类型的事件支持注册多个监听器，内部通过 <see cref="Delegate.Combine"/> 实现多播委托。
        /// 重复添加同一监听器实例会导致该监听器被调用多次。
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
        /// 移除指定事件类型的监听器。
        /// </summary>
        /// <typeparam name="TEventArgs">事件参数的类型，必须是 <see cref="EventArgs"/> 的子类。</typeparam>
        /// <param name="listener">要移除的事件处理器委托。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="listener"/> 为 <c>null</c> 时抛出。</exception>
        /// <remarks>
        /// 如果移除后该事件类型的委托列表为空，则自动从字典中移除该事件类型的条目。
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
        /// 分发指定类型的事件到所有已注册的监听器。
        /// </summary>
        /// <typeparam name="TEventArgs">事件参数的类型，必须是 <see cref="EventArgs"/> 的子类。</typeparam>
        /// <param name="sender">事件发送者。</param>
        /// <param name="eventArgs">事件参数。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="sender"/> 或 <paramref name="eventArgs"/> 为 <c>null</c> 时抛出。</exception>
        /// <remarks>
        /// <para>
        /// 分发策略：
        /// <list type="bullet">
        ///   <item><description>通过 <c>TEventArgs</c> 类型自动查找对应的委托列表。</description></item>
        ///   <item><description>逐个调用每个已注册的监听器，确保一个监听器的异常不会影响其他监听器。</description></item>
        ///   <item><description>监听器内发生的异常会被记录到 <see cref="GeneralTracer"/>，不会被重新抛出。</description></item>
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
        /// 清除所有已注册的事件监听器。
        /// </summary>
        /// <remarks>
        /// 此操作不可逆，调用后所有已注册的 <see cref="AddListener{TEventArgs}"/> 注册的处理器将被移除。
        /// </remarks>
        public void Clear() => _dicDelegates.Clear();

        /// <summary>
        /// 释放事件管理器，清除所有已注册的监听器。
        /// </summary>
        /// <remarks>
        /// 实现 <see cref="IDisposable"/> 接口，确保在组件生命周期结束时清理事件订阅，
        /// 防止内存泄漏。多次调用是安全的，第二次及后续调用不会重复清理。
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
