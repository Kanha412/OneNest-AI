using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneNest.Application.Interfaces.AI;

namespace OneNest.Infrastructure.AI;

/// <summary>
/// Embedding provider backed by Google's <b>text-embedding-004</b> model via
/// the Generative Language REST API.
///
/// <list type="bullet">
///   <item>
///     Produces L2-normalised vectors of <see cref="EmbeddingOptions.Dimension"/>
///     dimensions (default 768).  The Gemini API's <c>outputDimensionality</c>
///     parameter supports any integer from 1 to 768, so the dimension can be
///     shrunk to match an existing schema without retraining.
///   </item>
///   <item>
///     Reuses the API key configured under <c>AI:ApiKey</c> — the same key
///     that powers the Gemini conversational assistant.  No additional
///     credential is required.
///   </item>
///   <item>
///     Returns <c>null</c> on any failure (network error, invalid key, quota
///     exhausted, malformed response) so that note/document CRUD operations
///     are never blocked by embedding unavailability.
///   </item>
///   <item>
///     Free tier limit: 1 500 embedding requests / minute on Google AI Studio
///     — sufficient for personal-portfolio scale.
///   </item>
/// </list>
/// </summary>
public sealed class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private const string EmbedEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent";

    // Owned HttpClient.
    // HttpClientHandler with DangerousAcceptAnyServerCertificateValidator is used in
    // development so that corporate SSL-inspection proxies (Zscaler, etc.) that present
    // their own CA certificate during the TLS handshake do not abort the connection.
    // In production (Render / cloud) the default SocketsHttpHandler is used instead and
    // certificate validation is enforced.
    private static readonly HttpClient _httpClient = BuildHttpClient();

    private static HttpClient BuildHttpClient()
    {
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? false;

        var handler = isDevelopment
            ? new HttpClientHandler
            {
                UseProxy = true,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
            : new HttpClientHandler();

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }
    private readonly AIOptions                        _aiOptions;
    private readonly EmbeddingOptions                 _embeddingOptions;
    private readonly ILogger<GeminiEmbeddingProvider> _logger;

    // Shared options: camelCase serialisation matches Gemini's REST schema.
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public GeminiEmbeddingProvider(
        IOptions<AIOptions>              aiOptions,
        IOptions<EmbeddingOptions>       embeddingOptions,
        ILogger<GeminiEmbeddingProvider> logger)
    {
        _aiOptions        = aiOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _logger           = logger;
    }

    // ── IEmbeddingProvider ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<float[]?> EmbedAsync(
        string            text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (string.IsNullOrWhiteSpace(_aiOptions.ApiKey))
        {
            _logger.LogWarning(
                "GeminiEmbeddingProvider: AI:ApiKey is not configured — " +
                "semantic indexing is disabled.");
            return null;
        }

        try
        {
            var url = $"{EmbedEndpoint}?key={_aiOptions.ApiKey}";

            // Gemini embedding request payload.
            // gemini-embedding-001 default output is 3072 dims; outputDimensionality
            // truncates it server-side to match the vector(768) DB column.
            var payload = new
            {
                model   = "models/gemini-embedding-001",
                content = new { parts = new[] { new { text } } },
                outputDimensionality = _embeddingOptions.Dimension   // 768 from config
            };

            using var response = await _httpClient.PostAsJsonAsync(
                url, payload, SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "GeminiEmbeddingProvider: HTTP {StatusCode} {Reason}. Body: {Body}",
                    (int)response.StatusCode, response.ReasonPhrase, errorBody);
                return null;
            }

            // Parse: { "embedding": { "values": [f32, f32, ...] } }
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);

            using var doc = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("embedding", out var embProp) ||
                !embProp.TryGetProperty("values",            out var valsProp))
            {
                _logger.LogWarning(
                    "GeminiEmbeddingProvider: response is missing " +
                    "'embedding.values' — check model name or API version.");
                return null;
            }

            // Materialise into a float[] — avoids holding the JsonDocument alive.
            var elements = valsProp.EnumerateArray().ToArray();
            if (elements.Length == 0)
                return null;

            var result = new float[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = elements[i].GetSingle();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GeminiEmbeddingProvider: embedding request failed.");
            return null;
        }
    }
}
