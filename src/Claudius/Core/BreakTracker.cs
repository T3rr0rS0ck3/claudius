namespace Claudius.Core;

/// <summary>Follows how long the assistant has been working without a break and says when one is due.</summary>
public sealed class BreakTracker
{
    /// <summary>This long without the assistant working counts as a break and starts the count over.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMinutes(10);

    private DateTimeOffset? _start;
    private DateTimeOffset _lastWork;
    private int _reminders;

    /// <summary>
    /// Called once a second with whether the assistant is working. True when the work went on for
    /// <paramref name="after"/> without a break, and again after each further <paramref name="after"/>.
    /// </summary>
    public bool Update(bool working, DateTimeOffset now, TimeSpan after)
    {
        if (!working) return false;
        if (_start == null || now - _lastWork >= Pause)
        {
            _start = now;
            _reminders = 0;
        }
        _lastWork = now;
        if (now - _start.Value < after * (_reminders + 1)) return false;
        _reminders++;
        return true;
    }

    /// <summary>How long the current stretch of work has lasted so far.</summary>
    public TimeSpan Worked(DateTimeOffset now) => _start is { } start && now - _lastWork < Pause ? now - start : TimeSpan.Zero;
}
