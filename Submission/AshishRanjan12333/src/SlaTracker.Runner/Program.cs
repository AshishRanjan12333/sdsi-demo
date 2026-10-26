using System.Text.Json;
using System.Text.Json.Serialization;
using SlaTracker.Models;
using SlaTracker.Services;

namespace SlaTracker.Runner;

// Usage: dotnet run --project src/SlaTracker.Runner -- [--tickets path] [--policy path] [--holidays path] [--output path]
// If omitted, defaults to ../../data/tickets.json etc. relative to the runner project.

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args)
    {
        try
        {
            var opts = ParseArgs(args);
            var tickets = Load<List<TicketRequest>>(opts.TicketsPath);
            var policyFile = Load<SlaPolicyFile>(opts.PolicyPath);
            var policy = policyFile.ToPolicy();

            // Validate
            var errors = InputValidator.ValidateTickets(tickets);
            errors.AddRange(InputValidator.ValidatePolicyFile(policyFile));
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("VALIDATION ERRORS:");
                foreach (var e in errors)
                    Console.Error.WriteLine($"  - [{e.Field}] {e.Message}");
                return 1;
            }

            var results = SlaCalculator.Compute(tickets, policy);
            var summary = SlaCalculator.Summarize(results);

            var output = new { per_ticket = results, summary = summary };
            string json = JsonSerializer.Serialize(output, JsonOpts);
            if (opts.OutputPath is not null)
                File.WriteAllText(opts.OutputPath, json);
            else
                Console.WriteLine(json);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private static (string TicketsPath, string PolicyPath, string? OutputPath) ParseArgs(string[] args)
    {
        string tickets = "../../data/tickets.json";
        string policy = "../../data/sla_policy.json";
        string? output = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tickets" when i + 1 < args.Length: tickets = args[++i]; break;
                case "--policy" when i + 1 < args.Length: policy = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                default: break;
            }
        }
        return (tickets, policy, output);
    }

    private static T Load<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        string json = File.ReadAllText(path);
        var obj = JsonSerializer.Deserialize<T>(json, JsonOpts);
        return obj ?? throw new InvalidOperationException($"Failed to parse: {path}");
    }
}
