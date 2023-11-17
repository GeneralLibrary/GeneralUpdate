using System.Collections.Generic;

namespace GeneralUpdate.Core.Driver
{
    public class DriverProcessor
    {
        private List<IDriverCommand> _commands = new List<IDriverCommand>();

        public void AddCommand(IDriverCommand command)
        {
            _commands.Add(command);
        }

        public void ProcessCommands()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }

            _commands.Clear();
        }
    }
}