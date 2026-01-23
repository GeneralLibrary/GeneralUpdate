using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for resolving and validating extension dependencies.
    /// </summary>
    public interface IExtensionDependencyResolver
    {
        /// <summary>
        /// Resolves the dependencies for an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of dependency manifests.</returns>
        Task<List<ExtensionManifest>> ResolveDependenciesAsync(string extensionId);

        /// <summary>
        /// Validates that all dependencies for an extension are satisfied.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing the validation result.</returns>
        Task<DependencyValidationResult> ValidateDependenciesAsync(string extensionId);

        /// <summary>
        /// Gets the dependency tree for an extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing the dependency tree.</returns>
        Task<DependencyTree> GetDependencyTreeAsync(string extensionId);

        /// <summary>
        /// Finds all extensions that depend on a specific extension.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, containing a list of dependent extensions.</returns>
        Task<List<ExtensionManifest>> FindDependentsAsync(string extensionId);

        /// <summary>
        /// Checks for circular dependencies in the dependency graph.
        /// </summary>
        /// <param name="extensionId">The unique identifier of the extension.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether circular dependencies exist.</returns>
        Task<bool> HasCircularDependenciesAsync(string extensionId);
    }

    /// <summary>
    /// Represents the result of a dependency validation.
    /// </summary>
    public class DependencyValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether all dependencies are satisfied.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of missing dependencies.
        /// </summary>
        public List<ExtensionDependency> MissingDependencies { get; set; }

        /// <summary>
        /// Gets or sets the list of incompatible dependencies.
        /// </summary>
        public List<ExtensionDependency> IncompatibleDependencies { get; set; }

        /// <summary>
        /// Gets or sets any error messages.
        /// </summary>
        public List<string> Errors { get; set; }
    }

    /// <summary>
    /// Represents a dependency tree for an extension.
    /// </summary>
    public class DependencyTree
    {
        /// <summary>
        /// Gets or sets the root extension.
        /// </summary>
        public ExtensionManifest Root { get; set; }

        /// <summary>
        /// Gets or sets the list of direct dependencies.
        /// </summary>
        public List<DependencyNode> Dependencies { get; set; }
    }

    /// <summary>
    /// Represents a node in a dependency tree.
    /// </summary>
    public class DependencyNode
    {
        /// <summary>
        /// Gets or sets the extension manifest for this node.
        /// </summary>
        public ExtensionManifest Extension { get; set; }

        /// <summary>
        /// Gets or sets the child dependencies.
        /// </summary>
        public List<DependencyNode> Children { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; }
    }
}
