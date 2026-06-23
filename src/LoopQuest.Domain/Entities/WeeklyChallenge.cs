using LoopQuest.Domain.Enums;
using LoopQuest.Domain.Time;

namespace LoopQuest.Domain.Entities;

/// <summary>A loop the user committed to for one Monday→Sunday week. It snapshots the loop's two
/// targets at selection time, so later edits to the loop library never change a goal already chosen.</summary>
public class WeeklyChallenge
{
    private WeeklyChallenge()
    { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LoopId { get; private set; }
    public DateOnly WeekStart { get; private set; }
    public DateOnly WeekEnd { get; private set; }
    public ChallengeStatus Status { get; private set;  }
    public double SnapshotTargetDistanceMeters { get; private set; }
    public double SnapshotTargetElevationMeters { get; private set; }
    public DateTimeOffset SelectedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public double AchievedDistanceMeters { get; private set; }
    public double AchievedElevationMeters { get; private set; }
    public double ElevationPurchasedMeters { get; private set; }

    public static WeeklyChallenge Create(
    Guid userId,
    Guid loopId,
    Week week,
    double snapshotTargetDistanceMeters,
    double snapshotTargetElevationMeters,
    DateTimeOffset selectedAt)
    {
        Validate(snapshotTargetDistanceMeters, snapshotTargetElevationMeters);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (loopId == Guid.Empty)
        {
            throw new ArgumentException("Loop ID cannot be empty.", nameof(loopId));
        }

        return new WeeklyChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoopId = loopId,
            WeekStart = week.Start,
            WeekEnd = week.End,
            Status = ChallengeStatus.Active,
            SnapshotTargetDistanceMeters = snapshotTargetDistanceMeters,
            SnapshotTargetElevationMeters = snapshotTargetElevationMeters,
            SelectedAt = selectedAt,

        };
    }
    private static void Validate(double distanceMeters, double elevationGainMeters)
    {
        if (distanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMeters), distanceMeters, "Distance target must be greater than zero.");
        }
        if (elevationGainMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elevationGainMeters), elevationGainMeters, "Elevation gain cannot be negative.");
        }
    }

}
