using MoveMouse.Models;

namespace MoveMouse.ViewModels;

/// <summary>Pairs a <see cref="ScheduledActionTriggerMode"/> value with its user-facing display name for binding to a ComboBox.</summary>
public sealed record ScheduledActionTriggerModeOption(ScheduledActionTriggerMode Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
