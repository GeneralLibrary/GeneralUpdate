namespace MyApp.Extensions.Runtime
{
    /// <summary>
    /// Represents the type of runtime used by an extension.
    /// </summary>
    public enum RuntimeType
    {
        /// <summary>
        /// .NET runtime for C#/F#/VB.NET extensions.
        /// </summary>
        DotNet,

        /// <summary>
        /// Lua scripting runtime.
        /// </summary>
        Lua,

        /// <summary>
        /// Python scripting runtime.
        /// </summary>
        Python,

        /// <summary>
        /// Node.js runtime for JavaScript/TypeScript extensions.
        /// </summary>
        Node,

        /// <summary>
        /// Native executable runtime for compiled binaries.
        /// </summary>
        Exe,

        /// <summary>
        /// Custom or user-defined runtime.
        /// </summary>
        Custom
    }
}
