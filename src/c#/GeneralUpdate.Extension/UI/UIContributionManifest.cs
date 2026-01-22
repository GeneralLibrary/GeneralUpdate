using System.Collections.Generic;

namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a unified structure for all UI contribution metadata.
    /// </summary>
    public class UIContributionManifest
    {
        /// <summary>
        /// Gets or sets the command contributions.
        /// </summary>
        public List<CommandMetadata> Commands { get; set; }

        /// <summary>
        /// Gets or sets the menu contributions.
        /// </summary>
        public List<MenuMetadata> Menus { get; set; }

        /// <summary>
        /// Gets or sets the panel contributions.
        /// </summary>
        public List<PanelMetadata> Panels { get; set; }

        /// <summary>
        /// Gets or sets the shortcut contributions.
        /// </summary>
        public List<ShortcutMetadata> Shortcuts { get; set; }

        /// <summary>
        /// Gets or sets the theme contributions.
        /// </summary>
        public List<ThemeMetadata> Themes { get; set; }
    }

    /// <summary>
    /// Represents metadata for a command contribution.
    /// </summary>
    public class CommandMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the command.
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// Gets or sets the display title of the command.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the category of the command.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the icon for the command.
        /// </summary>
        public string Icon { get; set; }
    }

    /// <summary>
    /// Represents metadata for a menu contribution.
    /// </summary>
    public class MenuMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the menu.
        /// </summary>
        public string MenuId { get; set; }

        /// <summary>
        /// Gets or sets the display label of the menu item.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the parent menu identifier.
        /// </summary>
        public string ParentMenuId { get; set; }

        /// <summary>
        /// Gets or sets the command identifier.
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// Gets or sets the order of the menu item.
        /// </summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Represents metadata for a panel contribution.
    /// </summary>
    public class PanelMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the panel.
        /// </summary>
        public string PanelId { get; set; }

        /// <summary>
        /// Gets or sets the display title of the panel.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the location of the panel.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the icon for the panel.
        /// </summary>
        public string Icon { get; set; }
    }

    /// <summary>
    /// Represents metadata for a shortcut contribution.
    /// </summary>
    public class ShortcutMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the shortcut.
        /// </summary>
        public string ShortcutId { get; set; }

        /// <summary>
        /// Gets or sets the key combination.
        /// </summary>
        public string KeyCombination { get; set; }

        /// <summary>
        /// Gets or sets the command identifier.
        /// </summary>
        public string CommandId { get; set; }
    }

    /// <summary>
    /// Represents metadata for a theme contribution.
    /// </summary>
    public class ThemeMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier of the theme.
        /// </summary>
        public string ThemeId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the theme.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the theme type.
        /// </summary>
        public string ThemeType { get; set; }
    }
}
