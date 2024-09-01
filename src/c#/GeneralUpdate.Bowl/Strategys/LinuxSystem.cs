namespace GeneralUpdate.Bowl.Strategys;

internal class LinuxSystem
{
    internal string Name { get; set; }
    
    internal string Version { get; set; }

    internal LinuxSystem(string name, string version)
    {
        Name = name;
        Version = version;
    }
}