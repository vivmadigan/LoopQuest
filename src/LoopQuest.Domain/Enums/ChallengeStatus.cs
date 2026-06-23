namespace LoopQuest.Domain.Enums;

/// <summary>The lifecycle of a weekly challenge. Stored by name, not number (the EF config converts it
/// to a string), so renaming a member would orphan old rows — the numbers below are just cosmetic.</summary>
public enum ChallengeStatus
{
    Active = 0,
    Completed = 1,
    Failed = 2,
}
