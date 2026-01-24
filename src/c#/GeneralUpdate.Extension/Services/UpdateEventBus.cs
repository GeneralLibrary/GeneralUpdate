using System;
using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Extension.Interfaces;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Services
{
    /// <summary>
    /// Event bus implementation for plugin update events.
    /// Provides centralized event distribution using an observer pattern.
    /// </summary>
    public class UpdateEventBus : IUpdateEventBus
    {
        private readonly object _lock = new object();
        private event EventHandler<PluginUpdateEvent> _allSubscribers;
        private readonly Dictionary<string, EventHandler<PluginUpdateEvent>> _pluginSubscribers = new Dictionary<string, EventHandler<PluginUpdateEvent>>();
        private readonly Dictionary<UpdateStatus, EventHandler<PluginUpdateEvent>> _statusSubscribers = new Dictionary<UpdateStatus, EventHandler<PluginUpdateEvent>>();

        /// <summary>
        /// Publishes a plugin update event to all relevant subscribers.
        /// </summary>
        public void Publish(PluginUpdateEvent updateEvent)
        {
            if (updateEvent == null)
                throw new ArgumentNullException(nameof(updateEvent));

            lock (_lock)
            {
                // Notify all subscribers
                _allSubscribers?.Invoke(this, updateEvent);

                // Notify plugin-specific subscribers
                if (updateEvent.Plugin != null && !string.IsNullOrEmpty(updateEvent.Plugin.Id))
                {
                    if (_pluginSubscribers.TryGetValue(updateEvent.Plugin.Id, out var pluginHandler))
                    {
                        pluginHandler?.Invoke(this, updateEvent);
                    }
                }

                // Notify status-specific subscribers
                if (_statusSubscribers.TryGetValue(updateEvent.Status, out var statusHandler))
                {
                    statusHandler?.Invoke(this, updateEvent);
                }
            }
        }

        /// <summary>
        /// Subscribes to all plugin update events.
        /// </summary>
        public void Subscribe(EventHandler<PluginUpdateEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                _allSubscribers += handler;
            }
        }

        /// <summary>
        /// Unsubscribes from all plugin update events.
        /// </summary>
        public void Unsubscribe(EventHandler<PluginUpdateEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                _allSubscribers -= handler;
            }
        }

        /// <summary>
        /// Subscribes to events for a specific plugin only.
        /// </summary>
        public void SubscribeToPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_pluginSubscribers.ContainsKey(pluginId))
                {
                    _pluginSubscribers[pluginId] = handler;
                }
                else
                {
                    _pluginSubscribers[pluginId] += handler;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from events for a specific plugin.
        /// </summary>
        public void UnsubscribeFromPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (_pluginSubscribers.ContainsKey(pluginId))
                {
                    _pluginSubscribers[pluginId] -= handler;
                    // Clean up if no more subscribers
                    if (_pluginSubscribers[pluginId] == null)
                    {
                        _pluginSubscribers.Remove(pluginId);
                    }
                }
            }
        }

        /// <summary>
        /// Subscribes to events with a specific status only.
        /// </summary>
        public void SubscribeToStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_statusSubscribers.ContainsKey(status))
                {
                    _statusSubscribers[status] = handler;
                }
                else
                {
                    _statusSubscribers[status] += handler;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from events with a specific status.
        /// </summary>
        public void UnsubscribeFromStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (_statusSubscribers.ContainsKey(status))
                {
                    _statusSubscribers[status] -= handler;
                    // Clean up if no more subscribers
                    if (_statusSubscribers[status] == null)
                    {
                        _statusSubscribers.Remove(status);
                    }
                }
            }
        }
    }
}
