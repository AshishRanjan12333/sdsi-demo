using SlaTracker.Models;

namespace SlaTracker.Services;

public static class SlaCalculator
{
    public static List<TicketResult> Compute(IReadOnlyList<TicketRequest> tickets, SlaPolicy policy)
    {
        var results = new List<TicketResult>(tickets.Count);
        foreach (var ticket in tickets)
            results.Add(ComputeOne(ticket, policy));
        return results;
    }

    public static TicketResult ComputeOne(TicketRequest ticket, SlaPolicy policy)
    {
        double targetHours = policy.PriorityHours.TryGetValue(ticket.Priority, out var h) ? h : 0;
        double targetMinutes = targetHours * 60;
        DateTime start = ticket.CreatedAt;
        DateTime end = ticket.ResolvedAt ?? DateTime.UtcNow;

        var segments = BuildActiveSegments(ticket, policy, start, end);
        double totalBusMin = 0;
        foreach (var seg in segments)
            totalBusMin += CountBusinessMinutes(seg.Start, seg.End, policy);

        double onHoldMin = CountOnHoldMinutes(ticket, policy, start, end);
        DateTime dueBy = ComputeDueBy(start, targetMinutes, policy);
        bool breach = totalBusMin > targetMinutes;
        double breachMin = breach ? totalBusMin - targetMinutes : 0;

        return new TicketResult
        {
            TicketId = ticket.Id,
            Priority = ticket.Priority,
            AssigneeGroup = ticket.AssigneeGroup,
            ElapsedBusinessMinutes = Math.Round(totalBusMin, 2),
            SlaTargetMinutes = targetMinutes,
            DueBy = dueBy,
            Breach = breach,
            BreachMinutes = Math.Round(breachMin, 2),
            OnHoldMinutes = Math.Round(onHoldMin, 2)
        };
    }

    public static SlaSummary Summarize(List<TicketResult> results)
    {
        var summary = new SlaSummary
        {
            TotalTickets = results.Count,
            TotalBreaches = results.Count(r => r.Breach),
            OverallBreachPct = results.Count > 0
                ? Math.Round(100.0 * results.Count(r => r.Breach) / results.Count, 2)
                : 0
        };

        summary.BreachByPriority = results
            .GroupBy(r => r.Priority)
            .ToDictionary(g => g.Key, g => new BreachStat
            {
                Total = g.Count(),
                Breached = g.Count(r => r.Breach),
                BreachPct = Math.Round(100.0 * g.Count(r => r.Breach) / g.Count(), 2)
            });

        summary.BreachByAssigneeGroup = results
            .GroupBy(r => r.AssigneeGroup)
            .ToDictionary(g => g.Key, g => new BreachStat
            {
                Total = g.Count(),
                Breached = g.Count(r => r.Breach),
                BreachPct = Math.Round(100.0 * g.Count(r => r.Breach) / g.Count(), 2)
            });

        return summary;
    }

    internal static List<(DateTime Start, DateTime End)> BuildActiveSegments(
        TicketRequest ticket, SlaPolicy policy, DateTime start, DateTime end)
    {
        var segments = new List<(DateTime Start, DateTime End)>();
        var relevant = ticket.StatusHistory
            .Where(s => s.ChangedAt >= start && s.ChangedAt <= end)
            .OrderBy(s => s.ChangedAt)
            .ToList();

        var preHistory = ticket.StatusHistory
            .Where(s => s.ChangedAt < start)
            .OrderBy(s => s.ChangedAt)
            .LastOrDefault();

        bool currentlyPaused = preHistory is not null
            && string.Equals(preHistory.Status, policy.OnHoldStatus, StringComparison.OrdinalIgnoreCase);

        DateTime segStart = start;

        foreach (var sh in relevant)
        {
            bool isOnHold = string.Equals(sh.Status, policy.OnHoldStatus, StringComparison.OrdinalIgnoreCase);
            if (isOnHold && !currentlyPaused)
            {
                if (segStart < sh.ChangedAt)
                    segments.Add((segStart, sh.ChangedAt));
                currentlyPaused = true;
            }
            else if (!isOnHold && currentlyPaused)
            {
                segStart = sh.ChangedAt;
                currentlyPaused = false;
            }
        }

        if (!currentlyPaused && segStart < end)
            segments.Add((segStart, end));

        return segments;
    }

    internal static double CountOnHoldMinutes(
        TicketRequest ticket, SlaPolicy policy, DateTime start, DateTime end)
    {
        double total = 0;
        DateTime pauseStart = DateTime.MinValue;
        bool inPause = false;

        foreach (var sh in ticket.StatusHistory.OrderBy(s => s.ChangedAt))
        {
            if (sh.ChangedAt < start) continue;
            if (sh.ChangedAt > end) break;

            bool isOnHold = string.Equals(sh.Status, policy.OnHoldStatus, StringComparison.OrdinalIgnoreCase);
            if (isOnHold && !inPause)
            {
                pauseStart = sh.ChangedAt;
                inPause = true;
            }
            else if (!isOnHold && inPause)
            {
                total += (sh.ChangedAt - pauseStart).TotalMinutes;
                inPause = false;
            }
        }
        if (inPause && pauseStart < end)
            total += (end - pauseStart).TotalMinutes;

        return total;
    }

    internal static double CountBusinessMinutes(DateTime start, DateTime end, SlaPolicy policy)
    {
        if (end <= start) return 0;

        double total = 0;
        var current = start;

        while (current < end)
        {
            var date = DateOnly.FromDateTime(current);
            if (!policy.IsBusinessDay(date))
            {
                current = date.ToDateTime(TimeSpan.Zero).AddDays(1);
                var nextDate = DateOnly.FromDateTime(current);
                if (policy.IsBusinessDay(nextDate))
                    current = nextDate.ToDateTime(policy.BusinessStart);
                continue;
            }

            var timeOfDay = current.TimeOfDay;
            if (timeOfDay < policy.BusinessStart)
            {
                current = date.ToDateTime(policy.BusinessStart);
                continue;
            }
            if (timeOfDay >= policy.BusinessEnd)
            {
                current = date.AddDays(1).ToDateTime(policy.BusinessStart);
                continue;
            }

            var dayEnd = date.ToDateTime(policy.BusinessEnd);
            var effectiveEnd = end < dayEnd ? end : dayEnd;
            total += (effectiveEnd - current).TotalMinutes;
            current = effectiveEnd;

            if (current == dayEnd && current < end)
                current = date.AddDays(1).ToDateTime(policy.BusinessStart);
        }

        return total;
    }

    internal static DateTime ComputeDueBy(DateTime start, double targetMinutes, SlaPolicy policy)
    {
        if (targetMinutes <= 0) return start;

        double remaining = targetMinutes;
        var current = start;

        while (true)
        {
            var date = DateOnly.FromDateTime(current);
            if (!policy.IsBusinessDay(date))
            {
                current = date.ToDateTime(TimeSpan.Zero).AddDays(1);
                continue;
            }
            var timeOfDay = current.TimeOfDay;
            if (timeOfDay < policy.BusinessStart)
            {
                current = date.ToDateTime(policy.BusinessStart);
                continue;
            }
            if (timeOfDay >= policy.BusinessEnd)
            {
                current = date.AddDays(1).ToDateTime(policy.BusinessStart);
                continue;
            }
            break;
        }

        while (remaining > 0)
        {
            var date = DateOnly.FromDateTime(current);
            var dayEnd = date.ToDateTime(policy.BusinessEnd);
            double minsLeftToday = (dayEnd - current).TotalMinutes;

            if (minsLeftToday >= remaining)
                return current.AddMinutes(remaining);

            remaining -= minsLeftToday;
            do { date = date.AddDays(1); }
            while (!policy.IsBusinessDay(date));

            current = date.ToDateTime(policy.BusinessStart);
        }

        return current;
    }
}
