using System.Text;
using System.Text.Json;
using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;

namespace OneNest.Infrastructure.AI;

public class AIWorkspacePlanner : IAIWorkspacePlanner
{
    private readonly IAIProvider _provider;

    public AIWorkspacePlanner(IAIProvider provider)
    {
        _provider = provider;
    }

    public async Task<WorkspaceToolPlanResult> PlanAsync(
        WorkspaceToolExecutionContext context,
        IReadOnlyList<WorkspaceToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkspaceToolPlanResult();

        var prompt = context.UserPrompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt) || tools.Count == 0)
        {
            return result;
        }

        var registryJson = JsonSerializer.Serialize(
            tools.Select(x => new
            {
                name = x.Name,
                description = x.Description
            }));

        var plannerInstruction = new StringBuilder();
        plannerInstruction.AppendLine("You are a planning agent for OneNest AI.");
        plannerInstruction.AppendLine("Never answer the user's question.");
        plannerInstruction.AppendLine("Your only task is selecting tools.");
        plannerInstruction.AppendLine("Select the minimum number of tools needed.");
        plannerInstruction.AppendLine("Never invent tool names.");
        plannerInstruction.AppendLine("Return ONLY strict JSON, no markdown, no explanation.");
        plannerInstruction.AppendLine("Expected JSON shape:");
        plannerInstruction.AppendLine("{\"tools\":[{\"name\":\"tasks\",\"confidence\":0.96}]}");
        plannerInstruction.AppendLine("Confidence must be between 0 and 1.");
        plannerInstruction.AppendLine("If no tool is required, return {\"tools\":[]}.");
        plannerInstruction.AppendLine("Select up to 3 tools maximum.");
        plannerInstruction.AppendLine();
        plannerInstruction.AppendLine("Available tools (strict allow-list):");
        plannerInstruction.Append(registryJson);

        var plannerConversation = context.History
            .Append(new ConversationMessage
            {
                Role = "user",
                Content = prompt,
                Timestamp = context.UtcNow
            })
            .ToList();

        string raw;
        try
        {
            raw = await _provider.GenerateResponseAsync(
                plannerInstruction.ToString(),
                plannerConversation,
                cancellationToken);
        }
        catch
        {
            return result;
        }

        var json = ExtractFirstJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            var allowed = tools
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            const double minConfidence = 0.70;

            if (doc.RootElement.TryGetProperty("tools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in toolsEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddIfAllowed((item.GetString() ?? string.Empty).Trim(), 1d);
                        continue;
                    }

                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var name = item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                            ? (nameEl.GetString() ?? string.Empty).Trim()
                            : string.Empty;

                        var confidence = item.TryGetProperty("confidence", out var confEl) &&
                                         (confEl.ValueKind == JsonValueKind.Number) &&
                                         confEl.TryGetDouble(out var parsedConfidence)
                            ? parsedConfidence
                            : 0d;

                        AddIfAllowed(name, confidence);
                    }

                    if (result.SelectedTools.Count >= 3)
                    {
                        break;
                    }
                }
            }
            else if (doc.RootElement.TryGetProperty("selectedTools", out var legacyArr) && legacyArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in legacyArr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    AddIfAllowed((item.GetString() ?? string.Empty).Trim(), 1d);
                    if (result.SelectedTools.Count >= 3)
                    {
                        break;
                    }
                }
            }

            void AddIfAllowed(string name, double confidence)
            {
                if (string.IsNullOrWhiteSpace(name) || !allowed.Contains(name) || confidence < minConfidence)
                {
                    return;
                }

                if (result.SelectedTools.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                result.SelectedTools.Add(name);
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static string ExtractFirstJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        return value.Substring(start, end - start + 1).Trim();
    }
}
