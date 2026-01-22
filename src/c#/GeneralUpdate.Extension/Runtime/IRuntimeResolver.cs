namespace MyApp.Extensions.Runtime
{
    /// <summary>
    /// Resolves and provides runtime hosts based on runtime type.
    /// </summary>
    public interface IRuntimeResolver
    {
        /// <summary>
        /// Resolves a runtime host for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime to resolve.</param>
        /// <returns>The runtime host for the specified type, or null if not found.</returns>
        IRuntimeHost Resolve(RuntimeType runtimeType);

        /// <summary>
        /// Registers a runtime host for a specific runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime.</param>
        /// <param name="host">The runtime host to register.</param>
        void Register(RuntimeType runtimeType, IRuntimeHost host);

        /// <summary>
        /// Determines whether a runtime host is available for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime to check.</param>
        /// <returns>True if a runtime host is available; otherwise, false.</returns>
        bool IsAvailable(RuntimeType runtimeType);

        /// <summary>
        /// Gets all registered runtime types.
        /// </summary>
        /// <returns>An array of registered runtime types.</returns>
        RuntimeType[] GetRegisteredRuntimeTypes();
    }
}
