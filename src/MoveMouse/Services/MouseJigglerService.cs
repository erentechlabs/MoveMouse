using System.Threading;
using MoveMouse.Interop;
using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// <see cref="IMouseJigglerService"/> implementation driven by a
/// <see cref="System.Threading.Timer"/>. All mutable state is guarded by
/// <see cref="_gate"/> so <see cref="Start"/>, <see cref="Stop"/>, and
/// <see cref="Configure"/> can safely be called from the UI thread while a
/// tick callback is running on a thread-pool thread.
/// </summary>
public sealed class MouseJigglerService : IMouseJigglerService
{
    private readonly object _gate = new();
    private readonly Random _random = Random.Shared;
    private Timer? _timer;

    private int _intervalSeconds = 30;
    private MovementMode _mode = MovementMode.RandomMovement;
    private int _distancePixels = 6;

    // MovementMode.BackAndForth state
    private bool _nextMoveIsRightward = true;

    // MovementMode.CircularNudge state
    private int _circularAngleDegrees;
    private int _circularOffsetX;
    private int _circularOffsetY;

    private volatile bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public DateTimeOffset? LastMovedAt { get; private set; }

    public event EventHandler<bool>? RunningStateChanged;
    public event EventHandler<DateTimeOffset>? CursorMoved;
    public event EventHandler<string>? ErrorOccurred;

    public void Configure(int intervalSeconds, MovementMode mode, int distancePixels)
    {
        if (intervalSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be at least 1 second.");
        }

        if (distancePixels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(distancePixels), "Movement distance must be at least 1 pixel.");
        }

        lock (_gate)
        {
            _intervalSeconds = intervalSeconds;
            _mode = mode;
            _distancePixels = distancePixels;

            // Reset per-mode state so a configuration change starts the pattern cleanly.
            _nextMoveIsRightward = true;
            _circularAngleDegrees = 0;
            _circularOffsetX = 0;
            _circularOffsetY = 0;

            if (_timer is not null)
            {
                var period = TimeSpan.FromSeconds(_intervalSeconds);
                _timer.Change(period, period);
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_isRunning || _disposed)
            {
                return;
            }

            var period = TimeSpan.FromSeconds(_intervalSeconds);
            _timer = new Timer(OnTick, null, period, period);
            _isRunning = true;
        }

        RunningStateChanged?.Invoke(this, true);
    }

    public void Stop()
    {
        Timer? timerToDispose = null;

        lock (_gate)
        {
            if (!_isRunning)
            {
                return;
            }

            timerToDispose = _timer;
            _timer = null;
            _isRunning = false;
        }

        timerToDispose?.Dispose();
        RunningStateChanged?.Invoke(this, false);
    }

    private void OnTick(object? state)
    {
        try
        {
            int dx, dy;
            lock (_gate)
            {
                if (!_isRunning)
                {
                    return;
                }

                (dx, dy) = ComputeRawDelta();
            }

            var (clampedDx, clampedDy) = ClampToScreen(dx, dy);
            if (NativeMethods.MoveCursorBy(clampedDx, clampedDy))
            {
                LastMovedAt = DateTimeOffset.Now;
                CursorMoved?.Invoke(this, LastMovedAt.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not move the mouse cursor: {ex.Message}");
        }
    }

    /// <summary>Computes the next raw (unclamped) relative movement based on the active mode.</summary>
    private (int Dx, int Dy) ComputeRawDelta()
    {
        switch (_mode)
        {
            case MovementMode.BackAndForth:
                {
                    var dx = _nextMoveIsRightward ? _distancePixels : -_distancePixels;
                    _nextMoveIsRightward = !_nextMoveIsRightward;
                    return (dx, 0);
                }

            case MovementMode.CircularNudge:
                {
                    _circularAngleDegrees = (_circularAngleDegrees + 60) % 360;
                    var radians = _circularAngleDegrees * Math.PI / 180.0;
                    var newOffsetX = (int)Math.Round(_distancePixels * Math.Cos(radians));
                    var newOffsetY = (int)Math.Round(_distancePixels * Math.Sin(radians));
                    var dx = newOffsetX - _circularOffsetX;
                    var dy = newOffsetY - _circularOffsetY;
                    _circularOffsetX = newOffsetX;
                    _circularOffsetY = newOffsetY;
                    return (dx, dy);
                }

            case MovementMode.RandomMovement:
            default:
                {
                    int dx, dy;
                    do
                    {
                        dx = _random.Next(-_distancePixels, _distancePixels + 1);
                        dy = _random.Next(-_distancePixels, _distancePixels + 1);
                    } while (dx == 0 && dy == 0);
                    return (dx, dy);
                }
        }
    }

    /// <summary>Ensures the proposed relative move keeps the cursor within the virtual desktop bounds.</summary>
    private static (int Dx, int Dy) ClampToScreen(int dx, int dy)
    {
        if (!NativeMethods.GetCursorPos(out var current))
        {
            return (dx, dy);
        }

        var bounds = NativeMethods.GetVirtualScreenBounds();

        var targetX = Math.Clamp(current.X + dx, bounds.Left, bounds.Right);
        var targetY = Math.Clamp(current.Y + dy, bounds.Top, bounds.Bottom);

        return (targetX - current.X, targetY - current.Y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
