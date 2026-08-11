namespace SeitonSense.Core;

public readonly record struct ExecuteAlertDecision(
    ExecuteAlertState NextState,
    bool ShowLabel,
    bool TriggerFlash);
