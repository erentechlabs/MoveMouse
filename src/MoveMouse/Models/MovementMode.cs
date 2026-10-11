namespace MoveMouse.Models;

/// <summary>
/// The pattern used to simulate cursor activity on each jiggle tick.
/// </summary>
public enum MovementMode
{
    /// <summary>
    /// Moves the cursor by a small random offset every tick, clamped to the
    /// visible screen bounds. Produces an organic, unpredictable motion.
    /// </summary>
    RandomMovement,

    /// <summary>
    /// Alternates the cursor a few pixels left and right (or right and left)
    /// each tick, producing a subtle, predictable back-and-forth wiggle.
    /// </summary>
    BackAndForth,

    /// <summary>
    /// Nudges the cursor around a tiny hexagonal approximation of a circle,
    /// returning to its starting point once per full revolution.
    /// </summary>
    CircularNudge
}
