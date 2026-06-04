namespace LoopQuest.Application.Loops.Queries.GetLoops;

/// <summary>
/// Read model for a loop returned by the API. We never return the EF entity directly — DTOs keep the
/// API contract decoupled from the database shape, and let us expose enums as readable strings.
///
/// Distances stay in meters (the base unit); the frontend converts to km for display.
/// </summary>
public sealed record LoopDto(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Tier,
    double TargetDistanceMeters,
    double TargetElevationMeters,
    string? RealWorldReference,
    string? ImageUrl);
