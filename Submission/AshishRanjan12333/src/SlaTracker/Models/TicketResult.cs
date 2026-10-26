using System.Text.Json.Serialization;

namespace SlaTracker.Models;

public sealed class TicketResult
{
    [JsonPropertyName("ticket_id")]
    public string TicketId { get; init; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = string.Empty;

    [JsonPropertyName("assignee_group")]
    public string AssigneeGroup { get; init; } = string.Empty;

    [JsonPropertyName("elapsed_business_minutes")]
    public double ElapsedBusinessMinutes { get; init; }

    [JsonPropertyName("sla_target_minutes")]
    public double SlaTargetMinutes { get; init; }

    [JsonPropertyName("due_by")]
    public DateTime DueBy { get; init; }

    [JsonPropertyName("breach")]
    public bool Breach { get; init; }

    [JsonPropertyName("breach_minutes")]
    public double BreachMinutes { get; init; }

    [JsonPropertyName("on_hold_minutes")]
    public double OnHoldMinutes { get; init; }
}

public sealed class SlaSummary
{
    [JsonPropertyName("total_tickets")]
    public int TotalTickets { get; set; }

    [JsonPropertyName("total_breaches")]
    public int TotalBreaches { get; set; }

    [JsonPropertyName("overall_breach_pct")]
    public double OverallBreachPct { get; set; }

    [JsonPropertyName("breach_by_priority")]
    public Dictionary<string, BreachStat> BreachByPriority { get; set; } = [];

    [JsonPropertyName("breach_by_assignee_group")]
    public Dictionary<string, BreachStat> BreachByAssigneeGroup { get; set; } = [];
}

public sealed class BreachStat
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("breached")]
    public int Breached { get; set; }

    [JsonPropertyName("breach_pct")]
    public double BreachPct { get; set; }
}
