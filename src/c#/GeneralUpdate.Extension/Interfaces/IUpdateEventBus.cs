using System;
using GeneralUpdate.Extension.Models;

namespace GeneralUpdate.Extension.Interfaces
{
    /// <summary>
    /// Event bus for publishing and subscribing to plugin update events.
    /// Implements an observer pattern for decoupled event handling.
    /// </summary>
    public interface IUpdateEventBus
    {
        /// <summary>
        /// Publishes a plugin update event to all subscribers.
        /// </summary>
        /// <param name="updateEvent">The event to publish.</param>
        void Publish(PluginUpdateEvent updateEvent);

        /// <summary>
        /// Subscribes to all plugin update events.
        /// </summary>
        /// <param name="handler">Event handler to invoke when events are published.</param>
        void Subscribe(EventHandler<PluginUpdateEvent> handler);

        /// <summary>
        /// Unsubscribes from plugin update events.
        /// </summary>
        /// <param name="handler">Event handler to remove.</param>
        void Unsubscribe(EventHandler<PluginUpdateEvent> handler);

        /// <summary>
        /// Subscribes to events for a specific plugin only.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <param name="handler">Event handler to invoke.</param>
        void SubscribeToPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler);

        /// <summary>
        /// Unsubscribes from events for a specific plugin.
        /// </summary>
        /// <param name="pluginId">Unique plugin identifier.</param>
        /// <param name="handler">Event handler to remove.</param>
        void UnsubscribeFromPlugin(string pluginId, EventHandler<PluginUpdateEvent> handler);

        /// <summary>
        /// Subscribes to events with a specific status only.
        /// </summary>
        /// <param name="status">Update status to filter by.</param>
        /// <param name="handler">Event handler to invoke.</param>
        void SubscribeToStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler);

        /// <summary>
        /// Unsubscribes from events with a specific status.
        /// </summary>
        /// <param name="status">Update status to filter by.</param>
        /// <param name="handler">Event handler to remove.</param>
        void UnsubscribeFromStatus(UpdateStatus status, EventHandler<PluginUpdateEvent> handler);
    }
}
