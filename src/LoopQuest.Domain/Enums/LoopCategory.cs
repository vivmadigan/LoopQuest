namespace LoopQuest.Domain.Enums;

/// <summary>
/// Broad theme of a loop. Used for grouping/filtering in the library and UI.
/// </summary>
public enum LoopCategory
{
    /// <summary>A real, published race or well-known route (e.g. Zermatt Marathon).</summary>
    Classic = 0,

    /// <summary>An urban multi-lap route (e.g. "Five Loops of Central Park").</summary>
    Urban = 1,

    /// <summary>An invented or fictional challenge (e.g. "The Long Climb to Mordor").</summary>
    Fantasy = 2,
}
