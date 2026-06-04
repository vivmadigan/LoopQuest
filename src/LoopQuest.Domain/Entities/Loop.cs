using LoopQuest.Domain.Enums;

namespace LoopQuest.Domain.Entities;

/// <summary>
/// A challenge destination in the library. A loop is fundamentally just two targets:
/// a distance and an amount of climbing. This is true whether it represents a real race,
/// a route measured off a map, or pure invention — they are all rows in the same table.
///
/// All distances and elevations are stored in <b>meters</b> (the base unit). Conversion to
/// kilometres happens only at the display edge (the frontend), never in the domain.
/// </summary>
public class Loop
{
    // EF Core needs a parameterless constructor to materialise entities from the database.
    // It is private so application code is forced through the Create factory, which enforces invariants.
    private Loop()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public LoopCategory Category { get; private set; }

    public LoopTier Tier { get; private set; }

    /// <summary>Required total distance, in meters.</summary>
    public double TargetDistanceMeters { get; private set; }

    /// <summary>Required total elevation gain, in meters.</summary>
    public double TargetElevationMeters { get; private set; }

    /// <summary>Optional real-world location/race this loop is based on.</summary>
    public string? RealWorldReference { get; private set; }

    /// <summary>Optional image used by the UI.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>Inactive loops stay in the table for history but are hidden from the picker.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Creates a valid loop. Guard clauses keep impossible loops out of the domain:
    /// a loop must have a name, a positive distance target, and a non-negative elevation target.
    /// </summary>
    public static Loop Create(
        string name,
        string description,
        LoopCategory category,
        LoopTier tier,
        double targetDistanceMeters,
        double targetElevationMeters,
        string? realWorldReference = null,
        string? imageUrl = null,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A loop must have a name.", nameof(name));
        }

        if (targetDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetDistanceMeters), targetDistanceMeters, "Distance target must be greater than zero.");
        }

        if (targetElevationMeters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetElevationMeters), targetElevationMeters, "Elevation target cannot be negative.");
        }

        return new Loop
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = category,
            Tier = tier,
            TargetDistanceMeters = targetDistanceMeters,
            TargetElevationMeters = targetElevationMeters,
            RealWorldReference = realWorldReference?.Trim(),
            ImageUrl = imageUrl?.Trim(),
            IsActive = isActive,
        };
    }
}
