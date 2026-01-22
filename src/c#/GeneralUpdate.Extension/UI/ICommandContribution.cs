using System;
using System.Collections.Generic;

namespace MyApp.Extensions.UI
{
    /// <summary>
    /// Represents a command contribution to the UI.
    /// </summary>
    public interface ICommandContribution
    {
        /// <summary>
        /// Gets the unique identifier of the command.
        /// </summary>
        string CommandId { get; }

        /// <summary>
        /// Gets the display title of the command.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Gets the category of the command.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the icon for the command.
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Gets a value indicating whether the command is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameters">Optional parameters for the command.</param>
        void Execute(Dictionary<string, object> parameters);

        /// <summary>
        /// Determines whether the command can execute with the given parameters.
        /// </summary>
        /// <param name="parameters">Optional parameters for the command.</param>
        /// <returns>True if the command can execute; otherwise, false.</returns>
        bool CanExecute(Dictionary<string, object> parameters);
    }
}
