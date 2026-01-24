namespace GeneralUpdate.Extension.Metadata
{
    /// <summary>
    /// Defines the content type of an extension package.
    /// Used to identify the runtime requirements and execution model.
    /// </summary>
    public enum ExtensionContentType
    {
        /// <summary>
        /// JavaScript-based extension requiring a JavaScript runtime.
        /// </summary>
        JavaScript = 0,

        /// <summary>
        /// Lua-based extension requiring a Lua interpreter.
        /// </summary>
        Lua = 1,

        /// <summary>
        /// Python-based extension requiring a Python interpreter.
        /// </summary>
        Python = 2,

        /// <summary>
        /// WebAssembly module for sandboxed execution.
        /// </summary>
        WebAssembly = 3,

        /// <summary>
        /// External executable with inter-process communication.
        /// </summary>
        ExternalProcess = 4,

        /// <summary>
        /// Native library (.dll, .so, .dylib) for direct integration.
        /// </summary>
        NativeLibrary = 5,

        /// <summary>
        /// Custom or unspecified content type.
        /// </summary>
        Custom = 99
    }
}
