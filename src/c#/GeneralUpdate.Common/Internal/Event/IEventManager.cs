using System;

namespace GeneralUpdate.Common.Internal.Event
{
    /// <summary>
    /// Event manager interface.
    /// </summary>
    public interface IEventManager
    {
        /// <summary>
        /// Adding Event Listeners.
        /// </summary>
        /// <typeparam name="TDelegate">Generic delegate.</typeparam>
        /// <param name="newDelegate">New delegate that needs to be injected.</param>
        void AddListener<TDelegate>(TDelegate newDelegate) where TDelegate : Delegate;

        /// <summary>
        /// Removing Event Listening.
        /// </summary>
        /// <typeparam name="TDelegate">Generic delegate.</typeparam>
        /// <param name="oldDelegate">Need to remove an existing delegate.</param>
        void RemoveListener<TDelegate>(TDelegate oldDelegate) where TDelegate : Delegate;

        /// <summary>
        /// Triggers notifications of the same event type based on the listening event type.
        /// </summary>
        /// <typeparam name="TDelegate">generic delegate.</typeparam>
        /// <param name="sender">Event handler.</param>
        /// <param name="eventArgs">Event args.</param>
        void Dispatch<TDelegate>(object sender, EventArgs eventArgs) where TDelegate : Delegate;

        /// <summary>
        /// Remove all injected delegates.
        /// </summary>
        void Clear();
    }
}