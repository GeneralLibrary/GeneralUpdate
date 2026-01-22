namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a keyboard shortcut contribution to the UI.
    /// </summary>
    public interface IShortcutContribution
    {
        /// <summary>
        /// Gets the unique identifier of the shortcut.
        /// </summary>
        string ShortcutId { get; }

        /// <summary>
        /// Gets the key combination for the shortcut (e.g., "Ctrl+Shift+P").
        /// </summary>
        string KeyCombination { get; }

        /// <summary>
        /// Gets the command identifier to execute when the shortcut is triggered.
        /// </summary>
        string CommandId { get; }

        /// <summary>
        /// Gets the context in which the shortcut is active (e.g., "Editor", "Global").
        /// </summary>
        string Context { get; }

        /// <summary>
        /// Gets a value indicating whether the shortcut is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets the display description of the shortcut.
        /// </summary>
        string Description { get; }
    }
}
