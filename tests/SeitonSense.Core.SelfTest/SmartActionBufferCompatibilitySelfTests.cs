using SeitonSense.Core;

internal static class SmartActionBufferCompatibilitySelfTests
{
    internal static void AuditedNonMutatingProfileIsAllowed()
    {
        var input = new SmartActionBufferCompatibilityInput(
            SmartActionBufferReActionProfile.AuditedSafe,
            MOActionLoaded: true,
            MOActionOwnershipPublished: true,
            AssessmentAvailable: true,
            Quarantined: false);

        True(SmartActionBufferCompatibilityRules.AllowsMutation(input), "safe profile");
    }

    internal static void UnknownAndMutatingReActionProfilesFailClosed()
    {
        var unknown = new SmartActionBufferCompatibilityInput(
            SmartActionBufferReActionProfile.LoadedUnknown,
            MOActionLoaded: false,
            MOActionOwnershipPublished: false,
            AssessmentAvailable: true,
            Quarantined: false);
        var mutating = unknown with
        {
            ReActionProfile = SmartActionBufferReActionProfile.AuditedMutationActive,
        };

        False(SmartActionBufferCompatibilityRules.AllowsMutation(unknown), "unknown ReAction");
        False(SmartActionBufferCompatibilityRules.AllowsMutation(mutating), "mutating ReAction");
    }

    internal static void UnreadableMOActionOwnershipFailsClosed()
    {
        var input = new SmartActionBufferCompatibilityInput(
            SmartActionBufferReActionProfile.NotLoaded,
            MOActionLoaded: true,
            MOActionOwnershipPublished: false,
            AssessmentAvailable: true,
            Quarantined: false);

        False(SmartActionBufferCompatibilityRules.AllowsMutation(input), "unreadable MOAction IPC");
    }

    internal static void QuarantineConsumesExactlyOneCleanFrame()
    {
        var remaining = SmartActionBufferCompatibilityRules.MarkChanged(0);

        Equal(1, remaining, "marked frames");
        remaining = SmartActionBufferCompatibilityRules.ConsumeCleanFrameworkFrame(remaining);
        Equal(0, remaining, "one clean frame consumed");
        Equal(
            1,
            SmartActionBufferCompatibilityRules.MarkChanged(1),
            "repeated dirty signal remains bounded");
    }

    internal static void InitialSignatureIsBaselineAndLaterDriftIsDetected()
    {
        False(
            SmartActionBufferCompatibilityRules.SignatureChanged(
                hasPreviousAssessment: false,
                previousSignature: string.Empty,
                currentSignature: "first"),
            "initial baseline");
        False(
            SmartActionBufferCompatibilityRules.SignatureChanged(
                hasPreviousAssessment: true,
                previousSignature: "same",
                currentSignature: "same"),
            "stable signature");
        True(
            SmartActionBufferCompatibilityRules.SignatureChanged(
                hasPreviousAssessment: true,
                previousSignature: "before",
                currentSignature: "after"),
            "drift");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
