namespace DuplicateFileTool;

/// <summary>
/// Limits how often rapid-fire progress events are pushed to the UI.
/// Not thread-safe by design: a race between concurrent callers only lets an extra update through, which is harmless.
/// </summary>
internal sealed class UpdateThrottle(int intervalMs)
{
    private long _nextUpdateTicks;

    public bool IsUpdateDue()
    {
        var now = Environment.TickCount64;
        if (now < _nextUpdateTicks)
            return false;
        _nextUpdateTicks = now + intervalMs;
        return true;
    }
}
