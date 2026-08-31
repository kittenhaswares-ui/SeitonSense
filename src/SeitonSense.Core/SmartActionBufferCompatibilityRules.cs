namespace SeitonSense.Core;

/// <summary>
/// The only ReAction profiles for which Seiton may replay a captured action.
/// Unknown profiles and profiles which can rewrite the action or its target are
/// deliberately distinct so diagnostics can explain the fail-closed decision.
/// </summary>
public enum SmartActionBufferReActionProfile
{
    NotLoaded,
    AuditedSafe,
    AuditedMutationActive,
    LoadedUnknown,
}

public readonly record struct SmartActionBufferCompatibilityInput(
    SmartActionBufferReActionProfile ReActionProfile,
    bool MOActionLoaded,
    bool MOActionOwnershipPublished,
    bool AssessmentAvailable,
    bool Quarantined);

/// <summary>
/// Pure policy shared by the runtime compatibility boundary and self-tests.
/// This policy applies only to synthetic generic-buffer replay; it never gates
/// ordinary native input or native Turbo cadence.
/// </summary>
public static class SmartActionBufferCompatibilityRules
{
    public const int CleanFrameworkFramesAfterChange = 1;

    public static bool AllowsMutation(SmartActionBufferCompatibilityInput input) =>
        input.AssessmentAvailable &&
        !input.Quarantined &&
        input.ReActionProfile is
            SmartActionBufferReActionProfile.NotLoaded or
            SmartActionBufferReActionProfile.AuditedSafe &&
        (!input.MOActionLoaded || input.MOActionOwnershipPublished);

    /// <summary>
    /// Narrower policy for one synchronously validated self-only action. An
    /// audited ReAction installation may have unrelated Auto Target or Action
    /// Stacks enabled; those settings are admissible only after the live
    /// configuration proves that no stack can select this exact self action.
    /// Unknown ReAction builds and unreadable MOAction ownership still fail
    /// closed exactly like the generic buffer boundary.
    /// </summary>
    public static bool AllowsExactReviewedSelfAction(
        SmartActionBufferCompatibilityInput input,
        bool reActionOwnsExactAction) =>
        input.AssessmentAvailable &&
        !input.Quarantined &&
        input.ReActionProfile is
            SmartActionBufferReActionProfile.NotLoaded or
            SmartActionBufferReActionProfile.AuditedSafe or
            SmartActionBufferReActionProfile.AuditedMutationActive &&
        !reActionOwnsExactAction &&
        (!input.MOActionLoaded || input.MOActionOwnershipPublished);

    public static bool SignatureChanged(
        bool hasPreviousAssessment,
        string previousSignature,
        string currentSignature) =>
        hasPreviousAssessment &&
        !string.Equals(previousSignature, currentSignature, StringComparison.Ordinal);

    public static int MarkChanged(int remainingCleanFrames) =>
        Math.Max(
            Math.Max(0, remainingCleanFrames),
            CleanFrameworkFramesAfterChange);

    public static int ConsumeCleanFrameworkFrame(int remainingCleanFrames) =>
        Math.Max(0, remainingCleanFrames - 1);
}
