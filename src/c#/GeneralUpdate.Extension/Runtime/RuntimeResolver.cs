using System;
using System.Collections.Generic;

namespace MyApp.Extensions.Runtime
{
    /// <summary>
    /// Default implementation of IRuntimeResolver for resolving runtime hosts.
    /// </summary>
    public class RuntimeResolver : IRuntimeResolver
    {
        private readonly Dictionary<RuntimeType, IRuntimeHost> _hosts;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeResolver"/> class.
        /// </summary>
        public RuntimeResolver()
        {
            _hosts = new Dictionary<RuntimeType, IRuntimeHost>();
        }

        /// <summary>
        /// Resolves a runtime host for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime to resolve.</param>
        /// <returns>The runtime host for the specified type, or null if not found.</returns>
        public IRuntimeHost Resolve(RuntimeType runtimeType)
        {
            if (_hosts.TryGetValue(runtimeType, out var host))
            {
                return host;
            }

            // Create default hosts if not registered
            switch (runtimeType)
            {
                case RuntimeType.DotNet:
                    host = new DotNetRuntimeHost();
                    Register(runtimeType, host);
                    return host;
                    
                case RuntimeType.Python:
                    host = new PythonRuntimeHost();
                    Register(runtimeType, host);
                    return host;
                    
                case RuntimeType.Node:
                    host = new NodeRuntimeHost();
                    Register(runtimeType, host);
                    return host;
                    
                case RuntimeType.Lua:
                    host = new LuaRuntimeHost();
                    Register(runtimeType, host);
                    return host;
                    
                case RuntimeType.Exe:
                    host = new ExeRuntimeHost();
                    Register(runtimeType, host);
                    return host;
                    
                default:
                    return null;
            }
        }

        /// <summary>
        /// Registers a runtime host for a specific runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime.</param>
        /// <param name="host">The runtime host to register.</param>
        public void Register(RuntimeType runtimeType, IRuntimeHost host)
        {
            _hosts[runtimeType] = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>
        /// Determines whether a runtime host is available for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The type of runtime to check.</param>
        /// <returns>True if a runtime host is available; otherwise, false.</returns>
        public bool IsAvailable(RuntimeType runtimeType)
        {
            return _hosts.ContainsKey(runtimeType);
        }

        /// <summary>
        /// Gets all registered runtime types.
        /// </summary>
        /// <returns>An array of registered runtime types.</returns>
        public RuntimeType[] GetRegisteredRuntimeTypes()
        {
            var types = new RuntimeType[_hosts.Count];
            _hosts.Keys.CopyTo(types, 0);
            return types;
        }
    }
}
