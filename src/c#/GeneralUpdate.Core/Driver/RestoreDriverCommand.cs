using System.Text;

namespace GeneralUpdate.Core.Driver
{
    public class RestoreDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public RestoreDriverCommand(DriverInformation information)
        {
            _information = information;
        }

        public void Execute()
        {
            //Restore all drives in the backup directory.
            var command = new StringBuilder("/c pnputil /add-driver \"")
                .Append(_information.OutPutDirectory)
                .Append("\"")
                .ToString();
            CommandExecutor.ExecuteCommand(command);
        }
    }
}