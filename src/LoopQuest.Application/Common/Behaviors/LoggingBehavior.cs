using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoopQuest.Application.Common.Behaviors;

/// <summary>
/// A MediatR pipeline behaviour that logs every request flowing through the mediator, along with
/// how long it took. Cross-cutting concerns like this live in one place instead of being copy-pasted
/// into every handler.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        stopwatch.Stop();

        logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds} ms", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
