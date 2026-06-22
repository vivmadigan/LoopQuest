using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Domain.Activities;
using LoopQuest.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Activities.Commands.SyncActivities;

public sealed class SyncActivitiesCommandHandler(IStravaClient stravaClient, IAppDbContext db)
    : IRequestHandler<SyncActivitiesCommand, SyncResultDto>
{
    public async Task<SyncResultDto> Handle(SyncActivitiesCommand request, CancellationToken cancellationToken)
    {
        // Beat 1 — load the single athlete. v1 is single-player (one user row, no id filter); SingleOrDefault
        // returns it or null, and throws if a second user ever appears (a lookup by logged-in id when multi-user).
        var user = await db.Users.SingleOrDefaultAsync(cancellationToken);

        if (user?.Connection is null)
        {
            throw new InvalidOperationException(
                "No Strava connection — visit /auth/strava/connect first.");
        }

        // Beat 2 — refresh if the access token is near expiry, in order: get new tokens, write them on
        // the user, save IMMEDIATELY. Strava rotates the refresh token, so persisting now keeps it safe.
        if (user.Connection.IsExpiredOrExpiringWithin(TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow))
        {
            var refreshed = await stravaClient.RefreshAsync(user.Connection.RefreshToken, cancellationToken);
            user.RefreshTokens(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresAt);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Beat 3 — fixed 30-day window; no "last sync" state to store keeps re-runs idempotent.
        var after = DateTimeOffset.UtcNow - TimeSpan.FromDays(30);

        // Beat 4 — fetch every activity after `after` (paging is hidden in StravaClient). Read
        // user.Connection fresh — beat 2 may have replaced it, so an older copy holds a dead token.
        var fetched = await stravaClient.GetActivitiesAsync(user.Connection.AccessToken, after, cancellationToken);

        // Beat 5 — keep only the qualifying sport types; storing just these spares later stages a re-filter.
        var qualifying = fetched.Where(a => QualifyingSportTypes.Includes(a.SportType)).ToList();

        // Beat 6 — upsert by StravaActivityId. Collect the fetched ids, then load every already-known
        // row in ONE query, keyed into a dictionary for instant lookup (not one query per item — N+1).
        var stravaIds = qualifying.Select(a => a.Id).ToList();

        var existing = await db.Activities
            .Where(a => stravaIds.Contains(a.StravaActivityId))
            .ToDictionaryAsync(a => a.StravaActivityId, cancellationToken);

        // Count how many we update vs. add as we loop.
        var added = 0;
        var updated = 0;

        foreach (var item in qualifying)
        {
            if (existing.TryGetValue(item.Id, out var activity))
            {
                // Seen before → mutate the tracked row; EF writes an UPDATE only if a field changed.
                activity.UpdateFromSync(
                    item.Name, item.SportType, item.StartDateLocal, item.DistanceMeters, item.ElevationGainMeters);
                updated++;
            }
            else
            {
                // New → build a fresh Activity (our user's id + Strava's id) and queue an INSERT.
                db.Activities.Add(Activity.Create(
                    user.Id, item.Id, item.Name, item.SportType, item.StartDateLocal, item.DistanceMeters, item.ElevationGainMeters));
                added++;
            }
        }

        // Beat 7 — one save writes every queued insert/update, then report the counts back.
        await db.SaveChangesAsync(cancellationToken);

        return new SyncResultDto(
            Fetched: fetched.Count,
            Qualifying: qualifying.Count,
            Added: added,
            Updated: updated);
    }

}
