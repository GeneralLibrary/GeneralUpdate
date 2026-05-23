using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Bootstrap;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Environment variable accessor. Abstracts away static environment APIs for testability.
/// </summary>
internal interface IEnvironmentProvider
{
    /// <summary>Gets an environment variable value. Returns <c>null</c> if not set.</summary>
    string? GetVariable(string name);

    /// <summary>Sets an environment variable.</summary>
    void SetVariable(string name, string value);
}

/// <summary>
/// Default environment provider backed by <see cref="Environments"/>.
/// </summary>
internal sealed class EnvironmentProvider : IEnvironmentProvider
{
    public string? GetVariable(string name)
        => Environments.GetEnvironmentVariable(name);

    public void SetVariable(string name, string value)
        => Environments.SetEnvironmentVariable(name, value);
}
