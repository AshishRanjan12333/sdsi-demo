# PS6: IT Service-Desk SLA Tracker

A .NET 10 library + console app that computes SLA breaches for support tickets.

## What it does

- Computes business-hours elapsed time (Mon-Fri, 09:00-18:00)
- Excludes holidays from SLA time
- Pauses the SLA clock when a ticket is ON_HOLD (from status history)
- Per ticket: reports elapsed business minutes, due-by time, breach status, breach minutes
- Summary: breach % per priority and per assignee group

## Structure

```
src/SlaTracker/          -- core library (models + calculator)
src/SlaTracker.Runner/   -- console runner
tests/SlaTracker.Tests/  -- xUnit tests
data/                    -- sample input files
```

## Build & Run

```bash
# build
cd Submission/AshishRanjan12333
dotnet build

# run with default sample data
dotnet run --project src/SlaTracker.Runner

# customize inputs
dotnet run --project src/SlaTracker.Runner -- \
  --tickets data/tickets.json \
  --policy data/sla_policy.json \
  --output results.json

# run tests
dotnet test
```

## Input format

### tickets.json
```json
[
  {
    "id": "T-1001",
    "priority": "P1",
    "assignee_group": "Network",
    "created_at": "2026-10-05T09:00:00",
    "resolved_at": "2026-10-05T14:30:00",
    "status_history": [
      { "status": "OPEN", "changed_at": "2026-10-05T09:00:00" }
    ]
  }
]
```

### sla_policy.json
```json
{
  "priority_hours": { "P1": 4, "P2": 8, "P3": 24, "P4": 72 },
  "business_days": ["Monday","Tuesday","Wednesday","Thursday","Friday"],
  "business_hours_start": "09:00",
  "business_hours_end": "18:00",
  "on_hold_status": "ON_HOLD"
}
```

SLA targets: P1=4h, P2=8h, P3=24h, P4=72h (business hours only).

## SLA Logic

1. Count only business hours (Mon-Fri 09:00-18:00) excluding holidays
2. Pause the SLA clock while ticket is ON_HOLD (tracked via status_history)
3. Compute due-by from creation time + SLA target in business hours
4. Breach = elapsed business minutes > SLA target minutes
