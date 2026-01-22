using System.Collections.Generic;

namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a menu contribution to the UI.
    /// </summary>
    public interface IMenuContribution
    {
        /// <summary>
        /// Gets the unique identifier of the menu.
        /// </summary>
        string MenuId { get; }

        /// <summary>
        /// Gets the display label of the menu item.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the parent menu identifier, if this is a submenu.
        /// </summary>
        string ParentMenuId { get; }

        /// <summary>
        /// Gets the position or order of the menu item.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the icon for the menu item.
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Gets the command identifier to execute when the menu item is clicked.
        /// </summary>
        string CommandId { get; }

        /// <summary>
        /// Gets a value indicating whether the menu item is visible.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Gets a value indicating whether the menu item is a separator.
        /// </summary>
        bool IsSeparator { get; }

        /// <summary>
        /// Gets the child menu items, if this is a submenu.
        /// </summary>
        List<IMenuContribution> Children { get; }
    }
}
