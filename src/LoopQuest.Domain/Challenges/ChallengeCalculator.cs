namespace LoopQuest.Domain.Challenges;

/// <summary>
/// Inputs to a progress calculation, in meters: the loop's two targets, plus the week's summed
/// distance and elevation from qualifying activities.
/// </summary>
public readonly record struct ChallengeProgressInput(
    double TargetDistanceMeters,
    double TargetElevationMeters,
    double AchievedDistanceMeters,
    double AchievedElevationMeters);

/// <summary>
/// The computed result, in meters — what the UI's two progress bars render. Purchased elevation is
/// the surplus-bought portion (after the cap); effective elevation is real plus purchased.
/// </summary>
public readonly record struct ChallengeProgress(
    double SurplusDistanceMeters,
    double ElevationPurchasedMeters,
    double EffectiveElevationMeters,
    bool DistanceComplete,
    bool ElevationComplete,
    bool ChallengeComplete);

/// <summary>
/// The signature game rule: both targets must be met, but surplus distance can buy a capped amount
/// of missing elevation — never the reverse. Pure, dependency-free domain logic; the authoritative
/// spec is docs/02-domain-model.md ("Elevation substitution") plus ChallengeCalculatorTests.
/// </summary>
public static class ChallengeCalculator
{
    /// <summary>
    /// 30 m of surplus distance buys 1 m of elevation (3 km per 100 m) — roughly 3x the real
    /// effort cost, so genuine climbing always stays the efficient path.
    /// </summary>
    public const double SubstitutionRateMetersPerMeter = 30.0;

    /// <summary>
    /// At most one third of a loop's required elevation can be bought with distance;
    /// the other two thirds must be real climbing.
    /// </summary>
    public const double MaxBuyableFraction = 1.0 / 3.0;

    /// <summary>
    /// Computes progress and completion for a challenge. Note the deliberate asymmetry: distance
    /// completion uses achieved distance (spent surplus is never deducted), while elevation
    /// completion uses effective (real + purchased) elevation.
    /// </summary>
    public static ChallengeProgress Calculate(ChallengeProgressInput input)
    {
        double surplusDistance = Math.Max(0, input.AchievedDistanceMeters - input.TargetDistanceMeters);
        double elevationAffordable = surplusDistance / SubstitutionRateMetersPerMeter;
        double maxBuyableElevation = input.TargetElevationMeters * MaxBuyableFraction;
        double elevationPurchased = Math.Min(elevationAffordable, maxBuyableElevation);
        double effectiveElevation = input.AchievedElevationMeters + elevationPurchased;

        bool distanceComplete = input.AchievedDistanceMeters >= input.TargetDistanceMeters;
        bool elevationComplete = effectiveElevation >= input.TargetElevationMeters;

        bool challengeComplete = distanceComplete && elevationComplete;

        return new ChallengeProgress(
            SurplusDistanceMeters: surplusDistance,
            ElevationPurchasedMeters: elevationPurchased,
            EffectiveElevationMeters: effectiveElevation,
            DistanceComplete: distanceComplete,
            ElevationComplete: elevationComplete,
            ChallengeComplete: challengeComplete);
    }
}
