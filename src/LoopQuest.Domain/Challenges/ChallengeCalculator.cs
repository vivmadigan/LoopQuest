namespace LoopQuest.Domain.Challenges;

/// <summary>
/// The inputs to a progress calculation. All values are in meters.
/// </summary>
/// <param name="TargetDistanceMeters">Distance the loop requires.</param>
/// <param name="TargetElevationMeters">Climbing the loop requires.</param>
/// <param name="AchievedDistanceMeters">Distance summed from qualifying activities this week.</param>
/// <param name="AchievedElevationMeters">Elevation summed from qualifying activities this week.</param>
public readonly record struct ChallengeProgressInput(
    double TargetDistanceMeters,
    double TargetElevationMeters,
    double AchievedDistanceMeters,
    double AchievedElevationMeters);

/// <summary>
/// The computed result of a progress calculation. This is what the UI's two progress bars render.
/// All values are in meters.
/// </summary>
/// <param name="SurplusDistanceMeters">Distance run beyond the target (the pool that can buy elevation).</param>
/// <param name="ElevationPurchasedMeters">Elevation "bought" with surplus distance, after the cap is applied.</param>
/// <param name="EffectiveElevationMeters">Real elevation plus purchased elevation.</param>
/// <param name="DistanceComplete">Whether the distance target is met.</param>
/// <param name="ElevationComplete">Whether the (effective) elevation target is met.</param>
/// <param name="ChallengeComplete">Whether both targets are satisfied.</param>
public readonly record struct ChallengeProgress(
    double SurplusDistanceMeters,
    double ElevationPurchasedMeters,
    double EffectiveElevationMeters,
    bool DistanceComplete,
    bool ElevationComplete,
    bool ChallengeComplete);

/// <summary>
/// The authoritative game rule: given a loop's two targets and what the athlete has achieved,
/// decide whether the challenge is complete — including the one-directional, capped, lossy
/// "elevation substitution" (surplus distance can buy a limited amount of missing climbing).
///
/// This is pure logic with no dependencies, which is exactly why it lives in the Domain and is
/// trivially unit-testable. See docs/02-domain-model.md (section "Elevation substitution") and
/// the worked examples in the spec for the full reasoning.
/// </summary>
public static class ChallengeCalculator
{
    /// <summary>
    /// Exchange rate: 30 meters of surplus distance buys 1 meter of elevation
    /// (equivalently, 3 km buys 100 m). Deliberately ~3x the real-world effort cost so that
    /// real climbing always stays the efficient path.
    /// </summary>
    public const double SubstitutionRateMetersPerMeter = 30.0;

    /// <summary>
    /// Cap: you can buy at most one third of a loop's required elevation with distance.
    /// The remaining two thirds must always be genuine climbing.
    /// </summary>
    public const double MaxBuyableFraction = 1.0 / 3.0;

    /// <summary>
    /// Computes the full progress/completion result for a challenge.
    /// </summary>
    /// <remarks>
    /// The authoritative behaviour spec is the test suite (ChallengeCalculatorTests) and
    /// docs/02-domain-model.md ("Elevation substitution") — when in doubt, those win.
    ///
    /// Two nuances worth knowing when reading the code:
    /// <list type="bullet">
    /// <item>Only the overflow <i>above</i> the distance target converts to elevation; spent surplus
    /// is never deducted, so buying elevation can never un-complete the distance bar.</item>
    /// <item>Substitution is one-directional — distance buys elevation, never the reverse. That is
    /// why <see cref="ChallengeProgress.DistanceComplete"/> checks <i>achieved</i> distance while
    /// <see cref="ChallengeProgress.ElevationComplete"/> checks <i>effective</i> elevation.</item>
    /// </list>
    /// </remarks>
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
