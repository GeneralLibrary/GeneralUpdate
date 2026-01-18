namespace GeneralUpdate.Common.Shared.Object.Enum
{
    /// <summary>
    /// Plugin type enumeration for different plugin formats.
    /// </summary>
    public enum PluginType
    {
        /// <summary>
        /// JavaScript plugin using embedded script engine.
        /// </summary>
        JavaScript = 1,

        /// <summary>
        /// Lua plugin using embedded script engine.
        /// </summary>
        Lua = 2,

        /// <summary>
        /// Python plugin using embedded script engine.
        /// </summary>
        Python = 3,

        /// <summary>
        /// WebAssembly plugin.
        /// </summary>
        WASM = 4,

        /// <summary>
        /// External executable program with protocol communication.
        /// </summary>
        ExternalExecutable = 5
    }
}
