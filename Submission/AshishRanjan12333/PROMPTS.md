# PROMPTS.md — AI Collaboration Log

## Task
Build PS6: IT Service-Desk SLA Tracker in .NET 10 C#.

## Prompt sequence

1. "Clone sdsi-demo repo" — cloned the fork
2. "Check if fork" — confirmed fork from Reskilll/sdsi-demo
3. Provided PS6 brief — full requirements with SLA targets, business hours, holidays, on-hold pause, reporting
4. "Write code in this folder" — started scaffolding .NET 10 solution
5. "Do not install anything. Just write code in C# compatible with .NET 10" — validated SDK 10.0.403 already installed
6. "There is time limit, done before 10:35 AM IST" — fast-tracked implementation
7. "Just writes correct code. This is test exam" — validated existing model files work, used printf for reliable file writes
8. "Writes code in simple which can run in console app" — kept single console entry point, simple arg parsing

## AI-Assisted Decisions

- Chose classlib + console + xunit project structure
- Made SlaPolicy immutable with `with` expressions for the file parser
- Used `List<(DateTime, DateTime)>` value tuples for active time segments
- Business minutes counting: iterative day-by-day approach for correctness
- Input validation before processing: duplicate IDs, invalid priorities, chronology, date formats
- Default data paths relative to runner project for quick `dotnet run`

## Edge cases handled

- Holiday exclusion
- ON_HOLD pause before SLA window starts (pre-history lookup)
- Multiple ON_HOLD/IN_PROGRESS cycles
- Tickets without resolved_at (uses DateTime.UtcNow)
- Tickets created outside business hours
- Tickets that span weekends
- Duplicate ticket IDs
- Invalid priority values
- Non-chronological status history
- Null/empty input files
