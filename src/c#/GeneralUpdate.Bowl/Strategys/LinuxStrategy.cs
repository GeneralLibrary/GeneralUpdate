using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Bowl.Strategys;

internal class LinuxStrategy : AbstractStrategy
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
        GeneralTracer.Info("LinuxStrategy.Launch: starting Linux surveillance launch.");
        try
        {
            Install();
            GeneralTracer.Info("LinuxStrategy.Launch: procdump installation completed, invoking base launch.");
            base.Launch();
            GeneralTracer.Info("LinuxStrategy.Launch: launch lifecycle completed.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("LinuxStrategy.Launch: exception occurred during Linux surveillance launch.", ex);
            throw;
        }
    }

    private void Install()
    {
        GeneralTracer.Info("LinuxStrategy.Install: determining procdump package and running install script.");
        string scriptPath = "./install.sh";
        string packageFile = GetPacketName();

        if (string.IsNullOrEmpty(packageFile))
        {
            GeneralTracer.Warn("LinuxStrategy.Install: no matching procdump package found for the current Linux distribution.");
            return;
        }

        GeneralTracer.Info($"LinuxStrategy.Install: executing install.sh with package={packageFile}.");
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

            if (!string.IsNullOrEmpty(output))
                GeneralTracer.Info($"LinuxStrategy.Install output: {output}");

            if (!string.IsNullOrEmpty(error))
                GeneralTracer.Warn($"LinuxStrategy.Install error output: {error}");

            if (process.ExitCode != 0)
            {
                GeneralTracer.Error($"LinuxStrategy.Install: install script exited with code {process.ExitCode}.");
            }
            else
            {
                GeneralTracer.Info("LinuxStrategy.Install: procdump installation succeeded.");
            }
        }
        catch (Exception e)
        {
            GeneralTracer.Error("LinuxStrategy.Install: exception occurred while running install script.", e);
        }
    }

    private string GetPacketName()
    {
        GeneralTracer.Info("LinuxStrategy.GetPacketName: detecting Linux distribution.");
        var packageFileName = string.Empty;
        var system = GetSystem();
        GeneralTracer.Info($"LinuxStrategy.GetPacketName: detected distribution={system.Name}, version={system.Version}.");
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

        if (string.IsNullOrEmpty(packageFileName))
            GeneralTracer.Warn($"LinuxStrategy.GetPacketName: no matching package for distribution={system.Name}.");
        else
            GeneralTracer.Info($"LinuxStrategy.GetPacketName: resolved package={packageFileName}.");

        return packageFileName;
    }

    private LinuxSystem GetSystem()
    {
        GeneralTracer.Info("LinuxStrategy.GetSystem: reading /etc/os-release.");
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
            
            GeneralTracer.Info($"LinuxStrategy.GetSystem: distro={distro}, version={version}.");
            return new LinuxSystem(distro, version);
        }

        GeneralTracer.Fatal("LinuxStrategy.GetSystem: /etc/os-release not found, cannot determine Linux distribution.");
        throw new FileNotFoundException("Cannot determine the Linux distribution. The /etc/os-release file does not exist.");
    }
}