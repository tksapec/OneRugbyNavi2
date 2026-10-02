namespace OneRugbyNavi2;

public static class ScheduleDisplayOrder
{
    public static IEnumerable<T> Sort<T>(
        IEnumerable<T> items,
        Func<T, DateTime?> scheduledStart,
        Func<T, bool> hasKickoff,
        Func<T, bool> isInProgress,
        Func<T, bool> isFinished,
        DateTime now)
    {
        return items
            .Select(item => new
            {
                Item = item,
                Start = scheduledStart(item),
                HasKickoff = hasKickoff(item),
                IsLive = isInProgress(item),
                IsFinished = isFinished(item)
            })
            .OrderBy(entry => GetGroup(entry.Start, entry.HasKickoff, entry.IsLive, entry.IsFinished, now))
            .ThenBy(entry => GetOrderTicks(entry.Start, entry.HasKickoff, entry.IsLive, entry.IsFinished, now))
            .Select(entry => entry.Item);
    }

    private static int GetGroup(DateTime? start, bool hasKickoff, bool isLive, bool isFinished, DateTime now)
    {
        if (isLive) return 0;
        if (!isFinished && start.HasValue &&
            (start.Value.Date > now.Date ||
             (start.Value.Date == now.Date && (!hasKickoff || start.Value >= now)))) return 1;
        if (!isFinished && start.HasValue) return 2;
        if (isFinished && start.HasValue) return 3;
        return 4;
    }

    private static long GetOrderTicks(DateTime? start, bool hasKickoff, bool isLive, bool isFinished, DateTime now)
    {
        if (!start.HasValue) return long.MaxValue;
        if (isFinished && GetGroup(start, hasKickoff, isLive, isFinished, now) == 3)
            return DateTime.MaxValue.Ticks - start.Value.Ticks;
        if (!hasKickoff && GetGroup(start, hasKickoff, isLive, isFinished, now) == 1)
            return Math.Min(DateTime.MaxValue.Ticks, start.Value.Date.Ticks + TimeSpan.TicksPerDay - 1);
        return start.Value.Ticks;
    }
}
