using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Handle all drive-related.
    /// </summary>
    public class DriverProcessor
    {
        private readonly List<DriverCommand> _commands = new ();

        public void AddCommand(DriverCommand command) => _commands.Add(command);

        /// <summary>
        /// Execute all driver-related commands.
        /// </summary>
        public void ProcessCommands()
        {
            if (!_commands.Any()) return;

            /*
             * This section describes the PnPUtil command.
             * https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax
             */
            foreach (var command in _commands)
            {
                command.Execute();
            }
            _commands.Clear();
        }
    }
}