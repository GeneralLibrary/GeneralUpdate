using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Factory for creating update pipelines.
/// Injected via <c>Bootstrap.PipelineFactory&lt;T&gt;()</c>.
/// </summary>
public interface IUpdatePipelineFactory
{
    /// <summary>Create a pipeline for the given context.</summary>
    Task ExecutePipelineAsync(PipelineContext context, CancellationToken token = default);
}
