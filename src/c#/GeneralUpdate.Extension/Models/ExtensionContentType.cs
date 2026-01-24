namespace GeneralUpdate.Extension.Models
{
    /// <summary>
    /// Represents the type of content that an extension provides.
    /// </summary>
    public enum ExtensionContentType
    {
        /// <summary>
        /// JavaScript-based extension (requires JS engine).
        /// </summary>
        JavaScript = 0,

        /// <summary>
        /// Lua-based extension (requires Lua engine).
        /// </summary>
        Lua = 1,

        /// <summary>
        /// Python-based extension (requires Python engine).
        /// </summary>
        Python = 2,

        /// <summary>
        /// WebAssembly-based extension.
        /// </summary>
        WebAssembly = 3,

        /// <summary>
        /// External executable with protocol-based communication.
        /// </summary>
        ExternalExecutable = 4,

        /// <summary>
        /// Native library (.dll/.so/.dylib).
        /// </summary>
        NativeLibrary = 5,

        /// <summary>
        /// Other/custom extension type.
        /// </summary>
        Other = 99
    }
}
