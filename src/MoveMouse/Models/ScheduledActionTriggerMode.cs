namespace MoveMouse.Models;

/// <summary>How the run time of a scheduled power action is determined.</summary>
public enum ScheduledActionTriggerMode
{
    /// <summary>Runs once per day at a fixed time of day, every day, until turned off.</summary>
    AtSpecificTime,

    /// <summary>Runs once, after a fixed number of minutes have elapsed since being enabled, then turns itself off.</summary>
    AfterCountdown
}
