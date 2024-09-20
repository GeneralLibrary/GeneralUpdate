using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Bowl.Strategys;

public class LinuxStrategy : AbstractStrategy
{
    /*procdump-3.3.0-0.cm2.x86_64.rpm:
      Compatible Systems: This RPM package may be suitable for certain CentOS or RHEL-based derivatives, specifically the CM2 version. CM2 typically refers to ClearOS 7.x or similar community-maintained versions.
      
      procdump-3.3.0-0.el8.x86_64.rpm:
      Compatible Systems: This RPM package is suitable for Red Hat Enterprise Linux 8 (RHEL 8), CentOS 8, and other RHEL 8-based distributions.

      procdump_3.3.0_amd64.deb:
      Compatible Systems: This DEB package is suitable for Debian and its derivatives, such as Ubuntu, for 64-bit systems (amd64 architecture).*/
    
    private IReadOnlyList<string> _rocdumpAmd64 = new List<string> { "Ubuntu", "Debian" };
    private IReadOnlyList<string> procdump_el8_x86_64 = new List<string> { "Red Hat", "CentOS", "Fedora" };
    private IReadOnlyList<string> procdump_cm2_x86_64 = new List<string> { "ClearOS" };
    
    public override void Launch()
    {
        Install();
        base.Launch();
    }

    private void Install()
    {
        string scriptPath = "./install.sh";
        string packageFile = GetPacketName();
        
        ProcessStartInfo processStartInfo = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"{scriptPath} {packageFile}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process process = Process.Start(processStartInfo);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine("Output:");
            Console.WriteLine(output);

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("Error:");
                Console.WriteLine(error);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }
    }

    private string GetPacketName()
    {
        string packageFileName = string.Empty;
        LinuxSystem system = GetSystem();
        if (_rocdumpAmd64.Contains(system.Name))
        {
            packageFileName = $"procdump_3.3.0_amd64.deb";
        }
        else if (procdump_el8_x86_64.Contains(system.Name))
        {
            packageFileName = $"procdump-3.3.0-0.el8.x86_64.rpm";
        }
        else if (procdump_cm2_x86_64.Contains(system.Name))
        {
            packageFileName = $"procdump-3.3.0-0.cm2.x86_64.rpm";
        }

        return packageFileName;
    }

    private LinuxSystem GetSystem()
    {
        string osReleaseFile = "/etc/os-release";
        if (File.Exists(osReleaseFile))
        {
            var lines = File.ReadAllLines(osReleaseFile);
            string distro = string.Empty;
            string version = string.Empty;
            
            foreach (var line in lines)
            {
                if (line.StartsWith("ID="))
                {
                    distro = line.Substring(3).Trim('\"');
                }
                else if (line.StartsWith("VERSION_ID="))
                {
                    version = line.Substring(11).Trim('\"');
                }
            }
            
            return new LinuxSystem(distro, version);
        }
        else
        {
            throw new FileNotFoundException("Cannot determine the Linux distribution. The /etc/os-release file does not exist.");
        }
    }
}