using SlaTracker.Models;

namespace SlaTracker.Services;

/// <summary>
/// Validates input data (tickets, SLA policy, holidays) before processing.
/// </summary>
public static class InputValidator
{
    private static readonly HashSet<string> ValidPriorities = ["P1", "P2", "P3", "P4"];
    private static readonly HashSet<string> AllowedStatuses = ["OPEN", "IN_PROGRESS", "ON_HOLD", "RESOLVED", "CLOSED"];

    public static List<ValidationError> ValidateTickets(IReadOnlyList<TicketRequest> tickets)
    {
        var errors = new List<ValidationError>();

        if (tickets is null || tickets.Count == 0)
        {
            errors.Add(new ValidationError { Field = "tickets", Message = "Ticket list is null or empty." });
            return errors;
        }

        var seenIds = new HashSet<string>();

        for (int i = 0; i < tickets.Count; i++)
        {
            var t = tickets[i];
            string prefix = $"tickets[{i}]";

            if (string.IsNullOrWhiteSpace(t.Id))
                errors.Add(new ValidationError { Field = $"{prefix}.id", Message = "Ticket ID is required." });
            else if (!seenIds.Add(t.Id))
                errors.Add(new ValidationError { Field = $"{prefix}.id", Message = $"Duplicate ticket ID: {t.Id}." });

            if (!ValidPriorities.Contains(t.Priority))
                errors.Add(new ValidationError { Field = $"{prefix}.priority", Message = $"Invalid priority '{t.Priority}'. Must be one of: {string.Join(", ", ValidPriorities)}." });

            if (string.IsNullOrWhiteSpace(t.AssigneeGroup))
                errors.Add(new ValidationError { Field = $"{prefix}.assignee_group", Message = "Assignee group is required." });

            if (t.CreatedAt == default)
                errors.Add(new ValidationError { Field = $"{prefix}.created_at", Message = "Created date is missing or invalid." });

            if (t.ResolvedAt.HasValue && t.ResolvedAt.Value < t.CreatedAt)
                errors.Add(new ValidationError { Field = $"{prefix}.resolved_at", Message = "Resolved date cannot be before created date." });

            if (t.StatusHistory is not null)
            {
                for (int j = 0; j < t.StatusHistory.Count; j++)
                {
                    var sh = t.StatusHistory[j];
                    if (string.IsNullOrWhiteSpace(sh.Status))
                        errors.Add(new ValidationError { Field = $"{prefix}.status_history[{j}].status", Message = "Status is required." });
                    else if (!AllowedStatuses.Contains(sh.Status))
                        errors.Add(new ValidationError { Field = $"{prefix}.status_history[{j}].status", Message = $"Unknown status '{sh.Status}'." });

                    if (sh.ChangedAt == default)
                        errors.Add(new ValidationError { Field = $"{prefix}.status_history[{j}].changed_at", Message = "Changed date is missing or invalid." });
                }

                // Check chronology
                for (int j = 1; j < t.StatusHistory.Count; j++)
                {
                    if (t.StatusHistory[j].ChangedAt < t.StatusHistory[j - 1].ChangedAt)
                        errors.Add(new ValidationError { Field = $"{prefix}.status_history[{j}].changed_at", Message = "Status history is not chronological." });
                }
            }
        }

        return errors;
    }

    public static List<ValidationError> ValidatePolicyFile(SlaPolicyFile policyFile)
    {
        var errors = new List<ValidationError>();

        if (policyFile is null)
        {
            errors.Add(new ValidationError { Field = "sla_policy", Message = "SLA policy file is missing." });
            return errors;
        }

        if (policyFile.PriorityHours is not null)
        {
            foreach (var (k, v) in policyFile.PriorityHours)
            {
                if (!ValidPriorities.Contains(k))
                    errors.Add(new ValidationError { Field = "priority_hours", Message = $"Unknown priority key: {k}." });
                if (v <= 0)
                    errors.Add(new ValidationError { Field = $"priority_hours.{k}", Message = $"SLA target must be positive, got {v}." });
            }
        }

        if (policyFile.BusinessHoursStart is not null && !TimeSpan.TryParse(policyFile.BusinessHoursStart, out _))
            errors.Add(new ValidationError { Field = "business_hours_start", Message = $"Invalid time format: '{policyFile.BusinessHoursStart}'." });

        if (policyFile.BusinessHoursEnd is not null && !TimeSpan.TryParse(policyFile.BusinessHoursEnd, out _))
            errors.Add(new ValidationError { Field = "business_hours_end", Message = $"Invalid time format: '{policyFile.BusinessHoursEnd}'." });

        if (policyFile.Holidays is not null)
        {
            for (int i = 0; i < policyFile.Holidays.Length; i++)
            {
                if (!DateOnly.TryParse(policyFile.Holidays[i], out _))
                    errors.Add(new ValidationError { Field = $"holidays[{i}]", Message = $"Invalid date format: '{policyFile.Holidays[i]}'." });
            }
        }

        return errors;
    }
}
