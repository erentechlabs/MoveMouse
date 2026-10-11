using MoveMouse.Models;

namespace MoveMouse.ViewModels;

/// <summary>Pairs a <see cref="ScheduledActionType"/> value with its user-facing display name for binding to a ComboBox.</summary>
public sealed record ScheduledActionTypeOption(ScheduledActionType Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
