using GeneralUpdate.Core.Pipelines.Attributes;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Pipelines.MiddlewareResolver;
using GeneralUpdate.Core.Pipelines.Pipeline;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public static class MiddlewareExtensions
    {
        internal const string InvokeAsyncMethodName = "InvokeAsync";

        private const DynamicallyAccessedMemberTypes MiddlewareAccessibility =
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods;

        public static IPipelineBuilder UseMiddleware<[DynamicallyAccessedMembers(MiddlewareAccessibility)] TMiddleware>(this IPipelineBuilder pipeline) => pipeline.UseMiddleware(typeof(TMiddleware));

        public static IPipelineBuilder UseMiddleware(
    this IPipelineBuilder pipeline,
    [DynamicallyAccessedMembers(MiddlewareAccessibility)] Type middleware)
        {
            if (!typeof(IMiddleware).IsAssignableFrom(middleware))
                throw new ArgumentException($"The middleware type must implement \"{typeof(IMiddleware)}\".");

            var methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            MethodInfo invokeMethod = null;
            foreach (var method in methods)
            {
                if (string.Equals(method.Name, InvokeAsyncMethodName, StringComparison.Ordinal))
                {
                    if (method == null) throw new InvalidOperationException(InvokeAsyncMethodName);
                    invokeMethod = method;
                    break;
                }
            }

            if (invokeMethod is null)
                throw new InvalidOperationException("No suitable method matched .");

            if (!typeof(Task).IsAssignableFrom(invokeMethod.ReturnType))
                throw new InvalidOperationException($"The method is not an awaitable method {nameof(Task)} !");

            var parameters = invokeMethod.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(BaseContext))
                throw new InvalidOperationException($" The method parameter does not contain an {nameof(BaseContext)} type parameter !");

            return pipeline.Use(((IMiddleware)ActivatorMiddlewareResolver.Resolve(middleware)));
        }

        private readonly struct InvokeMiddlewareState
        {
            public InvokeMiddlewareState([DynamicallyAccessedMembers(MiddlewareAccessibility)] Type middleware) => Middleware = middleware;

            [DynamicallyAccessedMembers(MiddlewareAccessibility)]
            public Type Middleware { get; }
        }
    }
}