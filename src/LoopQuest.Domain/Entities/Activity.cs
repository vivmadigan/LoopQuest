
namespace LoopQuest.Domain.Entities;

public class Activity
{
    private Activity()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public long StravaActivityId { get; private set; }

    public string Name { get; private set; } = null!;

    public string SportType { get; private set; } = null!;

    public DateTimeOffset StartDateLocal { get; private set; }

    public double DistanceMeters { get; private set; }

    public double ElevationGainMeters { get; private set; }

    public DateTimeOffset IngestedAt { get; private set; }

    public static Activity Create(
        Guid userId,
         long stravaActivityId,
         string name,
         string sportType,
         DateTimeOffset startDateLocal,
         double distanceMeters,
         double elevationGainMeters)
    {
        Validate(distanceMeters, elevationGainMeters);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (stravaActivityId <= 0)
        {
            throw new ArgumentException("Strava Activity ID must be a positive integer.", nameof(stravaActivityId));
        }


        return new Activity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StravaActivityId = stravaActivityId,
            Name = name.Trim(),
            SportType = sportType.Trim(),
            StartDateLocal = startDateLocal,
            DistanceMeters = distanceMeters,
            ElevationGainMeters = elevationGainMeters,
            IngestedAt = DateTimeOffset.UtcNow,
        };

    }
    public void UpdateFromSync(string name, string sportType, DateTimeOffset startDateLocal,
       double distanceMeters, double elevationGainMeters)
    {
        // validate, then assign to THIS object's properties
        Validate(distanceMeters, elevationGainMeters);
        Name = name.Trim();
        SportType = sportType.Trim();
        StartDateLocal = startDateLocal;
        DistanceMeters = distanceMeters;
        ElevationGainMeters = elevationGainMeters;
    }

    private static void Validate(double distanceMeters, double elevationGainMeters)
    {
        if (distanceMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMeters), distanceMeters, "Distance cannot be negative.");
        }
        if (elevationGainMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elevationGainMeters), elevationGainMeters, "Elevation gain cannot be negative.");
        }
    }
}
