using System.Text.Json.Serialization;

namespace SlaTracker.Models;

/// <summary>
/// SLA policy: target hours per priority, business hours, holidays, on-hold rule.
/// </summary>
public sealed class SlaPolicy
{
    public Dictionary<string, double> PriorityHours { get; init; } = new()
    {
        ["P1"] = 4, ["P2"] = 8, ["P3"] = 24, ["P4"] = 72
    };

    public DayOfWeek[] BusinessDays { get; init; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public TimeSpan BusinessStart { get; init; } = new(9, 0, 0);
    public TimeSpan BusinessEnd { get; init; } = new(18, 0, 0);
    public string OnHoldStatus { get; init; } = "ON_HOLD";
    public HashSet<DateOnly> Holidays { get; init; } = [];

    public bool IsBusinessDay(DateOnly date)
    {
        return BusinessDays.Contains(date.DayOfWeek) && !Holidays.Contains(date);
    }

    public bool IsBusinessHour(TimeSpan time)
    {
        return time >= BusinessStart && time < BusinessEnd;
    }
}

public sealed class SlaPolicyFile
{
    [JsonPropertyName("priority_hours")]
    public Dictionary<string, double>? PriorityHours { get; set; }

    [JsonPropertyName("business_days")]
    public string[]? BusinessDays { get; set; }

    [JsonPropertyName("business_hours_start")]
    public string? BusinessHoursStart { get; set; }

    [JsonPropertyName("business_hours_end")]
    public string? BusinessHoursEnd { get; set; }

    [JsonPropertyName("on_hold_status")]
    public string? OnHoldStatus { get; set; }

    [JsonPropertyName("holidays")]
    public string[]? Holidays { get; set; }

    public SlaPolicy ToPolicy()
    {
        var policy = new SlaPolicy();
        if (PriorityHours is not null) policy = policy with { PriorityHours = PriorityHours };
        if (BusinessDays is not null)
            policy = policy with { BusinessDays = BusinessDays.Select(d => Enum.Parse<DayOfWeek>(d, true)).ToArray() };
        if (BusinessHoursStart is not null)
            policy = policy with { BusinessStart = TimeSpan.Parse(BusinessHoursStart) };
        if (BusinessHoursEnd is not null)
            policy = policy with { BusinessEnd = TimeSpan.Parse(BusinessHoursEnd) };
        if (OnHoldStatus is not null)
            policy = policy with { OnHoldStatus = OnHoldStatus };
        if (Holidays is not null)
            policy = policy with { Holidays = Holidays.Select(d => DateOnly.Parse(d)).ToHashSet() };
        return policy;
    }
}
