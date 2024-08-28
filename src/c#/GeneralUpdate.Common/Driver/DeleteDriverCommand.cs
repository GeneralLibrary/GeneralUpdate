using System.Text;

namespace GeneralUpdate.Common.Driver
{
    public class DeleteDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public DeleteDriverCommand(DriverInformation information) => _information = information;

        public void Execute()
        {
            //Before installing the driver, delete the driver that has been installed on the local system. Otherwise, an exception may occur.
            foreach (var driver in _information.Drivers)
            {
                var command = new StringBuilder("/c pnputil /delete-driver ")
                                  .Append(driver)
                                  .ToString();
                CommandExecutor.ExecuteCommand(command);
            }
        }
    }
}