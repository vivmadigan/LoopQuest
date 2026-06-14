using LoopQuest.Domain.Activities;

namespace LoopQuest.Domain.Tests;

public class QualifyingSportTypesTests
{
    [Theory]
    [InlineData("Run")]
    [InlineData("TrailRun")]
    [InlineData("Hike")]
    [InlineData("Walk")]
    public void Includes_TrueForQualifyingTypes(string sportType)
        => Assert.True(QualifyingSportTypes.Includes(sportType));

    [Theory]
    [InlineData("VirtualRun")]   // treadmill — excluded by design
    [InlineData("Ride")]
    [InlineData("run")]          // case-sensitive: lowercase doesn't qualify
    public void Includes_FalseForNonQualifyingTypes(string sportType)
        => Assert.False(QualifyingSportTypes.Includes(sportType));
}
