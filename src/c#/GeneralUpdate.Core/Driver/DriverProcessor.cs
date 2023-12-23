using System.Collections.Generic;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Handle all drive-related.
    /// </summary>
    public class DriverProcessor
    {
        private List<IDriverCommand> _commands = new List<IDriverCommand>();

        public void AddCommand(IDriverCommand command)
        {
            _commands.Add(command);
        }

        /// <summary>
        /// Execute all driver-related commands.
        /// </summary>
        public void ProcessCommands()
        {
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