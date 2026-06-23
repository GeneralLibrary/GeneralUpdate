namespace GeneralUpdate.Bowl;

/// <summary>Dump capture type for the procdump child process.</summary>
public enum DumpType
{
    /// <summary>Full memory dump (-ma on Windows).</summary>
    Full = 0,

    /// <summary>Mini dump (-mm on Windows). Smaller, faster.</summary>
    Mini = 1,

    /// <summary>Mini dump with heap (-mh on Windows).</summary>
    Heap = 2,
}
