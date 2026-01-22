using System.Collections.Generic;

namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a theme contribution to the UI.
    /// </summary>
    public interface IThemeContribution
    {
        /// <summary>
        /// Gets the unique identifier of the theme.
        /// </summary>
        string ThemeId { get; }

        /// <summary>
        /// Gets the display name of the theme.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the theme.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the type of theme (e.g., "Light", "Dark", "HighContrast").
        /// </summary>
        string ThemeType { get; }

        /// <summary>
        /// Gets the color definitions for the theme.
        /// </summary>
        Dictionary<string, string> Colors { get; }

        /// <summary>
        /// Gets the font definitions for the theme.
        /// </summary>
        Dictionary<string, string> Fonts { get; }

        /// <summary>
        /// Gets the icon set for the theme.
        /// </summary>
        Dictionary<string, string> Icons { get; }

        /// <summary>
        /// Applies the theme to the application.
        /// </summary>
        void Apply();

        /// <summary>
        /// Resets the theme to default settings.
        /// </summary>
        void Reset();
    }
}
