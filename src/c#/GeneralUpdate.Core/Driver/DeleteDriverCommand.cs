using System.Text;

namespace GeneralUpdate.Core.Driver;

public class DeleteDriverCommand(DriverInformation information) : DriverCommand
{
    public override void Execute()
    {
        //Before installing the driver, delete the driver that has been installed on the local system. Otherwise, an exception may occur.
        foreach (var driver in information.Drivers)
        {
            var command = new StringBuilder("/c pnputil /delete-driver ")
                .Append(driver.PublishedName)
                .ToString();
            CommandExecutor.ExecuteCommand(command);
        }
    }
}