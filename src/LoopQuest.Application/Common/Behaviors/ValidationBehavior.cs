using FluentValidation;
using MediatR;

namespace LoopQuest.Application.Common.Behaviors;

/// <summary>
/// A MediatR pipeline behaviour that runs any FluentValidation validators registered for the
/// incoming request before the handler executes. If validation fails, it throws a
/// <see cref="ValidationException"/>, which the API translates into an HTTP 400 with problem details.
///
/// Requests with no registered validator (such as parameterless queries) simply pass straight through.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
