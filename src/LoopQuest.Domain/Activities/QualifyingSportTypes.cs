using System;
using System.Collections.Generic;
using System.Text;

namespace LoopQuest.Domain.Activities;

public static class QualifyingSportTypes
{
    // VirtualRun (treadmill) is deliberately excluded — adding it here is the "one-line change".
    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { "Run", "TrailRun", "Hike", "Walk" };

    public static bool Includes(string sportType) => All.Contains(sportType);
}
