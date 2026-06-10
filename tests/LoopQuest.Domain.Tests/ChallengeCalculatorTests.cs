using LoopQuest.Domain.Challenges;

namespace LoopQuest.Domain.Tests;

/// <summary>
/// STAGE 1 EXERCISE.
///
/// These tests encode the authoritative game rules (spec section 2.5 and the worked examples). They are
/// all marked <c>Skip</c> so the suite stays green until you implement
/// <see cref="ChallengeCalculator.Calculate"/>. Work one test at a time:
///   1. Remove the Skip from a test.
///   2. Run it (red).
///   3. Implement just enough in ChallengeCalculator to make it green.
///   4. Repeat. When all pass, the substitution logic is done.
/// </summary>
public class ChallengeCalculatorTests
{
    // Doubles are compared to 2 decimal places throughout (everything is in meters).
    private const int Precision = 2;

    [Fact]
    public void BothTargetsMetByRealEffort_IsComplete()
    {
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 10_000,
            TargetElevationMeters: 500,
            AchievedDistanceMeters: 10_000,
            AchievedElevationMeters: 500));

        Assert.True(result.DistanceComplete);
        Assert.True(result.ElevationComplete);
        Assert.True(result.ChallengeComplete);
        Assert.Equal(0, result.ElevationPurchasedMeters, Precision);
    }

    [Fact]
    public void SurplusDistance_BuysElevationUpToTheCap_CompletingAnAlpineLoop()
    {
        // 50 km / 3000 m. Max buyable = 3000 / 3 = 1000 m, which needs 1000 * 30 = 30 km of surplus.
        // An 80 km week (30 km surplus) with 2000 m real climbing buys the last 1000 m and completes.
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 50_000,
            TargetElevationMeters: 3_000,
            AchievedDistanceMeters: 80_000,
            AchievedElevationMeters: 2_000));

        Assert.Equal(1_000, result.ElevationPurchasedMeters, Precision);
        Assert.Equal(3_000, result.EffectiveElevationMeters, Precision);
        Assert.True(result.ChallengeComplete);
    }

    [Fact]
    public void CapPreventsAFlatWeekFromCompletingAVerticalLoop()
    {
        // Enormous flat distance can still only buy 1/3 of the required elevation.
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 50_000,
            TargetElevationMeters: 3_000,
            AchievedDistanceMeters: 200_000,
            AchievedElevationMeters: 0));

        Assert.Equal(1_000, result.ElevationPurchasedMeters, Precision); // capped at 1/3 of 3000
        Assert.Equal(1_000, result.EffectiveElevationMeters, Precision);
        Assert.False(result.ElevationComplete);
        Assert.False(result.ChallengeComplete);
    }

    [Fact]
    public void ExtraElevation_NeverBuysDistance()
    {
        // Substitution is one-directional. Tons of climbing but short on distance => not complete.
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 10_000,
            TargetElevationMeters: 500,
            AchievedDistanceMeters: 5_000,
            AchievedElevationMeters: 5_000));

        Assert.False(result.DistanceComplete);
        Assert.False(result.ChallengeComplete);
        Assert.Equal(0, result.ElevationPurchasedMeters, Precision);
    }

    [Fact]
    public void ExchangeRateAndCap_MeetAtTheBoundary()
    {
        // Target 10 km / 900 m. Cap = 900 / 3 = 300 m. To buy 300 m needs 300 * 30 = 9 km surplus.
        // 19 km (9 km surplus) + 600 m real climbing => buys exactly 300 m => effective 900 => complete.
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 10_000,
            TargetElevationMeters: 900,
            AchievedDistanceMeters: 19_000,
            AchievedElevationMeters: 600));

        Assert.Equal(300, result.ElevationPurchasedMeters, Precision);
        Assert.Equal(900, result.EffectiveElevationMeters, Precision);
        Assert.True(result.ChallengeComplete);
    }

    [Fact]
    public void JustShortOnEffectiveElevation_IsNotComplete()
    {
        var result = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            TargetDistanceMeters: 50_000,
            TargetElevationMeters: 3_000,
            AchievedDistanceMeters: 80_000,
            AchievedElevationMeters: 1_900));

        Assert.Equal(2_900, result.EffectiveElevationMeters, Precision);
        Assert.False(result.ChallengeComplete);
    }
}
