using MoveMouse.Models;

namespace MoveMouse.ViewModels;

/// <summary>Pairs a <see cref="MovementMode"/> value with its user-facing display name for binding to a ComboBox.</summary>
public sealed record MovementModeOption(MovementMode Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
