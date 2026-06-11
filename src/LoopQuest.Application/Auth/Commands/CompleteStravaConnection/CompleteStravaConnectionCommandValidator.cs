using FluentValidation;

namespace LoopQuest.Application.Auth.Commands.CompleteStravaConnection;

/// <summary>
/// Runs automatically before the handler (ValidationBehavior finds it by existing in this assembly).
/// No Code means nothing to trade with Strava, so the request dies here as a 400 — the handler never
/// runs. Scope has no rule on purpose: it's allowed to be missing.
/// </summary>
public sealed class CompleteStravaConnectionCommandValidator
      : AbstractValidator<CompleteStravaConnectionCommand>
{
    public CompleteStravaConnectionCommandValidator()
        => RuleFor(c => c.Code).NotEmpty();
}
