using MediatR;

namespace LoopQuest.Application.Activities.Commands.SyncActivities;

/// <summary>
/// The "pull my recent Strava activities" message. It carries no input data — the user, the 30-day
/// window, and the qualifying sport types are all decided inside the handler — so there's nothing for
/// a caller to pass and nothing to validate. That empty body is exactly why this slice has no
/// validator: the validation pipeline has no inputs to check, so it passes straight through.
/// Handling it ends with the activities table synced; the sender gets back a SyncResultDto of counts.
/// </summary>
public sealed record SyncActivitiesCommand : IRequest<SyncResultDto>
{
}
