﻿using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Common.Driver
{
    /// <summary>
    /// Handle all drive-related.
    /// </summary>
    public class DriverProcessor
    {
        private readonly List<IDriverCommand> _commands = new List<IDriverCommand>();

        public void AddCommand(IDriverCommand command)
        {
            _commands.Add(command);
        }

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