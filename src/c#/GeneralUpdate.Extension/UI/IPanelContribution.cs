namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a panel or view contribution to the UI.
    /// </summary>
    public interface IPanelContribution
    {
        /// <summary>
        /// Gets the unique identifier of the panel.
        /// </summary>
        string PanelId { get; }

        /// <summary>
        /// Gets the display title of the panel.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Gets the icon for the panel.
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Gets the location where the panel should be displayed (e.g., "Left", "Right", "Bottom", "Main").
        /// </summary>
        string Location { get; }

        /// <summary>
        /// Gets the order of the panel in its location.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets a value indicating whether the panel is visible by default.
        /// </summary>
        bool IsVisibleByDefault { get; }

        /// <summary>
        /// Gets a value indicating whether the panel can be closed by the user.
        /// </summary>
        bool IsCloseable { get; }

        /// <summary>
        /// Creates the content for the panel.
        /// </summary>
        /// <returns>The panel content.</returns>
        object CreateContent();

        /// <summary>
        /// Called when the panel is activated or shown.
        /// </summary>
        void OnActivate();

        /// <summary>
        /// Called when the panel is deactivated or hidden.
        /// </summary>
        void OnDeactivate();
    }
}
