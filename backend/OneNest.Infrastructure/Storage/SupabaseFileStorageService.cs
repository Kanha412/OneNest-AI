using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneNest.Application.Interfaces.Storage;

namespace OneNest.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStorageService"/> backed by Supabase Storage.
///
/// Replaces the local-filesystem <c>FileStorageService</c> so that user-uploaded
/// documents survive Render container restarts (Render has an ephemeral filesystem).
///
/// Storage layout inside the configured private bucket:
///   {userId}/{storedFileName}
///
/// Access is server-side only, authenticated with the Supabase service-role key.
/// The key is never forwarded to the Angular frontend.
///
/// Configuration (all via environment variables in production):
///   Supabase__Url            = https://&lt;project-ref&gt;.supabase.co
///   Supabase__ServiceRoleKey = (Render secret)
///   Supabase__StorageBucket  = onenest-documents
/// </summary>
public sealed class SupabaseFileStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string     _bucketName;
    private readonly string     _storageBase;
    private readonly ILogger<SupabaseFileStorageService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public SupabaseFileStorageService(
        IOptions<SupabaseOptions>                opts,
        ILogger<SupabaseFileStorageService>      logger)
    {
        var cfg = opts.Value;

        _httpClient  = new HttpClient();
        _bucketName  = string.IsNullOrWhiteSpace(cfg.StorageBucket)
                            ? "onenest-documents"
                            : cfg.StorageBucket;
        _storageBase = cfg.Url.TrimEnd('/') + "/storage/v1";
        _logger      = logger;

        // Set the Authorization header once on the shared client instance.
        // SupabaseFileStorageService is registered as Singleton so the client
        // is created once and reused — this is intentional.
        if (!string.IsNullOrWhiteSpace(cfg.ServiceRoleKey))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg.ServiceRoleKey);
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Supabase Storage object path: <c>{userId}/{filename}</c>.
    /// Only the filename component of <paramref name="storedFileName"/> is used
    /// to prevent path-traversal attacks.
    /// </summary>
    private static string BuildObjectPath(Guid userId, string storedFileName)
    {
        // Path.GetFileName strips any directory prefix (including ../ sequences)
        var safeFileName = Path.GetFileName(storedFileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            throw new ArgumentException("Invalid stored file name.", nameof(storedFileName));

        return $"{userId}/{safeFileName}";
    }

    private string ObjectUrl(string objectPath) =>
        $"{_storageBase}/object/{Uri.EscapeDataString(_bucketName)}/{objectPath}";

    // ── IFileStorageService ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> SaveAsync(
        Guid   userId,
        string storedFileName,
        Stream content)
    {
        var objectPath = BuildObjectPath(userId, storedFileName);
        var url        = ObjectUrl(objectPath);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        // x-upsert: true — overwrite silently if the object already exists.
        // GUID-based names make accidental collisions extremely unlikely, but
        // this makes re-uploads (e.g. after a failed transaction) idempotent.
        request.Headers.TryAddWithoutValidation("x-upsert", "true");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Supabase Storage upload failed for {ObjectPath}: HTTP {Status} — {Body}",
                objectPath, (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode(); // throws HttpRequestException
        }

        _logger.LogDebug("Supabase Storage: uploaded {ObjectPath}", objectPath);
        return objectPath; // stored in the DB as the file reference key
    }

    /// <inheritdoc/>
    public async Task<Stream?> OpenReadAsync(Guid userId, string storedFileName)
    {
        var objectPath = BuildObjectPath(userId, storedFileName);
        var url        = ObjectUrl(objectPath);

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage: object not found for {ObjectPath}: HTTP {Status}",
                objectPath, (int)response.StatusCode);
            return null;
        }

        // Copy the response body into a MemoryStream so the HttpResponseMessage
        // can be safely disposed after this method returns.
        // Documents are capped at 25 MB, so buffering the entire file is acceptable.
        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid userId, string storedFileName)
    {
        var objectPath = BuildObjectPath(userId, storedFileName);
        await BatchDeleteAsync([objectPath]);
    }

    /// <inheritdoc/>
    public async Task DeleteUserDirectoryAsync(Guid userId)
    {
        // List all objects stored under {userId}/ and delete them in one batch call.
        var prefix  = $"{userId}/";
        var listUrl = $"{_storageBase}/object/list/{Uri.EscapeDataString(_bucketName)}";

        var listPayload = JsonSerializer.Serialize(new
        {
            prefix,
            limit  = 1000,
            offset = 0,
            search = (string?)null
        });

        using var listRequest = new HttpRequestMessage(HttpMethod.Post, listUrl)
        {
            Content = new StringContent(listPayload, Encoding.UTF8, "application/json")
        };

        var listResponse = await _httpClient.SendAsync(listRequest);
        if (!listResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage: failed to list objects for user {UserId}: HTTP {Status}",
                userId, (int)listResponse.StatusCode);
            return;
        }

        var json  = await listResponse.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<StorageItem>>(json, _jsonOpts);

        if (items is not { Count: > 0 }) return;

        var paths = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => $"{prefix}{i.Name}")
            .ToArray();

        if (paths.Length > 0)
            await BatchDeleteAsync(paths);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task BatchDeleteAsync(string[] objectPaths)
    {
        // Supabase Storage batch-delete:
        //   DELETE /storage/v1/object/{bucket}
        //   Body:  { "prefixes": ["path1", "path2", ...] }
        var url     = $"{_storageBase}/object/{Uri.EscapeDataString(_bucketName)}";
        var payload = JsonSerializer.Serialize(new { prefixes = objectPaths });

        using var request = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage: batch delete returned HTTP {Status} for {Count} object(s)",
                (int)response.StatusCode, objectPaths.Length);
        }
    }

    // ── Private DTO ───────────────────────────────────────────────────────────

    private sealed class StorageItem
    {
        public string? Name { get; set; }
    }
}
