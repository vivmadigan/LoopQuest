namespace LoopQuest.Domain.Enums;

/// <summary>
/// Difficulty banding for a loop. Purely descriptive in v1 (used for display and
/// rough sorting); the calibration logic scores loops on their actual numbers, not this tier.
/// </summary>
public enum LoopTier
{
    Easy = 0,
    Moderate = 1,
    Hard = 2,
    Epic = 3,
}
