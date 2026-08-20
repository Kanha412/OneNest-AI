using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;

namespace OneNest.Infrastructure.AI;

public class GeminiProvider : IAIProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AIOptions _options;

    public GeminiProvider(HttpClient httpClient, IOptions<AIOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("AI API key is not configured.");

        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-2.5-flash"
            : _options.Model.Trim();

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_options.ApiKey}";

        var contents = new List<object>
        {
            new
            {
                role = "user",
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            }
        };

        foreach (var message in conversation)
        {
            var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "model"
                : "user";

            contents.Add(new
            {
                role,
                parts = new[]
                {
                    new { text = message.Content }
                }
            });
        }

        var requestPayload = new
        {
            contents
        };

        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var text = ExtractText(responseBody);
                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("AI returned an empty response.");

                    return text.Trim();
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("AI authentication failed. Please verify the Gemini API key.");

                if (response.StatusCode == HttpStatusCode.BadRequest)
                    throw new InvalidOperationException("Invalid AI request. Please shorten or rephrase your prompt.");

                if ((int)response.StatusCode == 429)
                {
                    if (attempt == maxAttempts)
                        throw new InvalidOperationException("AI rate limit reached. Please wait and try again.");

                    await Task.Delay(TimeSpan.FromMilliseconds(600 * attempt), cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500)
                {
                    if (attempt == maxAttempts)
                        throw new InvalidOperationException("AI service is temporarily unavailable. Please try again shortly.");

                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
                    continue;
                }

                throw new InvalidOperationException($"AI request failed ({(int)response.StatusCode}): {responseBody}");
            }
            catch (TaskCanceledException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("AI request failed due to a network timeout.");
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var chunks = new List<string>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                var value = text.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    chunks.Add(value);
            }
        }

        return string.Join("\n", chunks);
    }
}
