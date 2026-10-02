using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class ScheduleDisplayOrderTests
{
    [Fact]
    public void Default_order_starts_with_live_match_then_next_matches_chronologically_and_finished_matches_after()
    {
        var now = new DateTime(2026, 10, 2, 14, 0, 0);
        var items = new[]
        {
            new Match("finished-today", new DateTime(2026, 10, 2, 12, 0, 0), true, false, true),
            new Match("next-later", new DateTime(2026, 10, 3, 15, 0, 0), true, false, false),
            new Match("live", new DateTime(2026, 10, 2, 13, 0, 0), true, true, false),
            new Match("next-soon", new DateTime(2026, 10, 2, 18, 0, 0), true, false, false),
            new Match("next-time-unknown", new DateTime(2026, 10, 2), false, false, false),
            new Match("finished-yesterday", new DateTime(2026, 10, 1, 12, 0, 0), true, false, true),
            new Match("date-unknown", null, false, false, false)
        };

        var ordered = ScheduleDisplayOrder.Sort(
            items,
            item => item.Start,
            item => item.HasKickoff,
            item => item.IsLive,
            item => item.IsFinished,
            now);

        Assert.Equal(new[] { "live", "next-soon", "next-time-unknown", "next-later", "finished-today", "finished-yesterday", "date-unknown" },
            ordered.Select(item => item.Id));
    }

    private sealed record Match(string Id, DateTime? Start, bool HasKickoff, bool IsLive, bool IsFinished);
}
