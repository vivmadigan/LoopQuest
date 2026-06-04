using LoopQuest.Domain.Entities;
using LoopQuest.Domain.Enums;

namespace LoopQuest.Domain.Tests;

/// <summary>
/// Tests for the <see cref="Loop"/> factory guard clauses. These pass out of the box — run them now
/// to confirm your test setup works before you start the ChallengeCalculator exercise.
/// </summary>
public class LoopTests
{
    [Fact]
    public void Create_WithValidValues_SetsPropertiesAndIsActiveByDefault()
    {
        var loop = Loop.Create(
            name: "Test Loop",
            description: "A description",
            category: LoopCategory.Urban,
            tier: LoopTier.Easy,
            targetDistanceMeters: 10_000,
            targetElevationMeters: 200);

        Assert.NotEqual(Guid.Empty, loop.Id);
        Assert.Equal("Test Loop", loop.Name);
        Assert.Equal(10_000, loop.TargetDistanceMeters);
        Assert.Equal(200, loop.TargetElevationMeters);
        Assert.True(loop.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            Loop.Create(name!, "desc", LoopCategory.Urban, LoopTier.Easy, 10_000, 200));
    }

    [Fact]
    public void Create_WithNonPositiveDistance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Loop.Create("Test", "desc", LoopCategory.Urban, LoopTier.Easy, targetDistanceMeters: 0, targetElevationMeters: 200));
    }

    [Fact]
    public void Create_WithNegativeElevation_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Loop.Create("Test", "desc", LoopCategory.Urban, LoopTier.Easy, targetDistanceMeters: 10_000, targetElevationMeters: -1));
    }
}
