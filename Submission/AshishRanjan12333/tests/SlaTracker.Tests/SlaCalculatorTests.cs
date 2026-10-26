using SlaTracker.Models;
using SlaTracker.Services;
using Xunit;

namespace SlaTracker.Tests;

public class SlaCalculatorTests
{
    private static readonly SlaPolicy DefaultPolicy = new();

    [Fact]
    public void ComputeOne_SimpleTicketInBusinessHours_NoBreach()
    {
        var ticket = new TicketRequest
        {
            Id = "T-001",
            Priority = "P2",
            AssigneeGroup = "DB",
            CreatedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            ResolvedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            StatusHistory = []
        };

        // 3 hours = 180 min, P2 target = 8 hours = 480 min
        var result = SlaCalculator.ComputeOne(ticket, DefaultPolicy);

        Assert.Equal(180, result.ElapsedBusinessMinutes, 1);
        Assert.False(result.Breach);
        Assert.Equal(0, result.BreachMinutes);
        Assert.Equal(480, result.SlaTargetMinutes);
    }

    [Fact]
    public void ComputeOne_ExceedsTarget_Breach()
    {
        var ticket = new TicketRequest
        {
            Id = "T-002",
            Priority = "P1",
            AssigneeGroup = "Net",
            CreatedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            ResolvedAt = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc),
            StatusHistory = []
        };

        // 5 hours = 300 min, P1 target = 4 hours = 240 min
        var result = SlaCalculator.ComputeOne(ticket, DefaultPolicy);

        Assert.Equal(300, result.ElapsedBusinessMinutes, 1);
        Assert.True(result.Breach);
        Assert.Equal(60, result.BreachMinutes, 1);
    }

    [Fact]
    public void ComputeOne_OnHold_PausesClock()
    {
        var ticket = new TicketRequest
        {
            Id = "T-003",
            Priority = "P1",
            AssigneeGroup = "App",
            CreatedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            ResolvedAt = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc),
            StatusHistory =
            [
                new StatusChange { Status = "ON_HOLD", ChangedAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc) },
                new StatusChange { Status = "IN_PROGRESS", ChangedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc) }
            ]
        };

        // Active: 09:00-10:00 (60 min) + 12:00-14:00 (120 min) = 180 min
        // On hold: 10:00-12:00 (120 min) excluded
        var result = SlaCalculator.ComputeOne(ticket, DefaultPolicy);

        Assert.Equal(180, result.ElapsedBusinessMinutes, 1);
        Assert.Equal(120, result.OnHoldMinutes, 1);
        Assert.False(result.Breach);
    }

    [Fact]
    public void ComputeOne_Holiday_Excluded()
    {
        var policy = new SlaPolicy
        {
            Holidays = new HashSet<DateOnly> { new(2026, 10, 5) }
        };
        var ticket = new TicketRequest
        {
            Id = "T-004",
            Priority = "P2",
            AssigneeGroup = "DB",
            CreatedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            ResolvedAt = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc),
            StatusHistory = []
        };

        // Oct 5 is holiday; only Oct 6 09:00-12:00 = 180 min counts
        var result = SlaCalculator.ComputeOne(ticket, policy);

        Assert.Equal(180, result.ElapsedBusinessMinutes, 1);
    }

    [Fact]
    public void ComputeOne_AfterHours_OnlyBusinessTimeCounts()
    {
        var ticket = new TicketRequest
        {
            Id = "T-005",
            Priority = "P3",
            AssigneeGroup = "Net",
            CreatedAt = new DateTime(2026, 10, 2, 17, 0, 0, DateTimeKind.Utc),
            ResolvedAt = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            StatusHistory = []
        };

        // Oct 2 is Friday: 17:00-18:00 = 60 min
        // Oct 3 is Saturday: 0 min
        var result = SlaCalculator.ComputeOne(ticket, DefaultPolicy);

        Assert.Equal(60, result.ElapsedBusinessMinutes, 1);
    }

    [Fact]
    public void ComputeDueBy_P1CreatedAt9_DueAt13()
    {
        var start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        // P1 = 4 hours = 240 min from 09:00 = 13:00
        var dueBy = SlaCalculator.ComputeDueBy(start, 240, DefaultPolicy);

        Assert.Equal(new DateTime(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc), dueBy);
    }

    [Fact]
    public void ComputeDueBy_SpansToNextDay()
    {
        var start = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

        // P1 = 240 min. 15:00-18:00 = 180 min, remaining 60 min spill to next day 09:00-10:00
        var dueBy = SlaCalculator.ComputeDueBy(start, 240, DefaultPolicy);

        Assert.Equal(new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc), dueBy);
    }

    [Fact]
    public void Summarize_ComputesCorrectStats()
    {
        var results = new List<TicketResult>
        {
            new() { TicketId = "A", Priority = "P1", AssigneeGroup = "Net", Breach = true },
            new() { TicketId = "B", Priority = "P1", AssigneeGroup = "Net", Breach = false },
            new() { TicketId = "C", Priority = "P2", AssigneeGroup = "DB", Breach = true },
            new() { TicketId = "D", Priority = "P3", AssigneeGroup = "App", Breach = false }
        };

        var summary = SlaCalculator.Summarize(results);

        Assert.Equal(4, summary.TotalTickets);
        Assert.Equal(2, summary.TotalBreaches);
        Assert.Equal(50, summary.OverallBreachPct);

        Assert.Equal(2, summary.BreachByPriority["P1"].Total);
        Assert.Equal(1, summary.BreachByPriority["P1"].Breached);
        Assert.Equal(50, summary.BreachByPriority["P1"].BreachPct);
        Assert.Equal(100, summary.BreachByPriority["P2"].BreachPct);
        Assert.Equal(0, summary.BreachByPriority["P3"].BreachPct);

        Assert.Equal(100, summary.BreachByAssigneeGroup["DB"].BreachPct);
        Assert.Equal(50, summary.BreachByAssigneeGroup["Net"].BreachPct);
        Assert.Equal(0, summary.BreachByAssigneeGroup["App"].BreachPct);
    }

    [Fact]
    public void BuildActiveSegments_NoHistory_ReturnsOneSegment()
    {
        var ticket = new TicketRequest { StatusHistory = [] };
        var policy = DefaultPolicy;
        var start = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

        var segments = SlaCalculator.BuildActiveSegments(ticket, policy, start, end);

        Assert.Single(segments);
        Assert.Equal(start, segments[0].Start);
        Assert.Equal(end, segments[0].End);
    }
}
