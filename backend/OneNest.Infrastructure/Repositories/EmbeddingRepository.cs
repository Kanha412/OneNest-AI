using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

/// <summary>
/// Persists and retrieves semantic embedding vectors using pgvector's
/// cosine-distance operator (<c>&lt;=&gt;</c>).
///
/// All operations use raw ADO.NET via the Npgsql connection exposed by
/// EF Core's <c>Database.GetDbConnection()</c>.  This avoids any EF Core
/// type-mapping dependency on pgvector NuGet packages: vectors are passed
/// as the PostgreSQL literal string <c>[v0,v1,…]</c> which PostgreSQL
/// casts to <c>vector(384)</c> automatically when the vector extension is
/// installed.
///
/// A source item can have multiple rows in <c>EmbeddingRecords</c> — one per
/// text chunk.  The unique constraint is on
/// (UserId, SourceType, SourceId, ChunkIndex).
/// </summary>
public class EmbeddingRepository : IEmbeddingRepository
{
    private readonly OneNestDbContext _context;

    public EmbeddingRepository(OneNestDbContext context)
    {
        _context = context;
    }

    // ── Upsert ───────────────────────────────────────────────────────────────

    public async Task UpsertAsync(EmbeddingRecord record, CancellationToken cancellationToken = default)
    {
        var vectorLiteral = ToLiteral(record.Embedding);

        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = $"""
            INSERT INTO "EmbeddingRecords"
                ("Id", "UserId", "SourceType", "SourceId", "Title", "ChunkIndex",
                 "Embedding", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @userId, @sourceType, @sourceId, @title, @chunkIndex,
                 '{vectorLiteral}'::vector, @createdAt, NULL)
            ON CONFLICT ("UserId", "SourceType", "SourceId", "ChunkIndex")
            DO UPDATE SET
                "Title"      = EXCLUDED."Title",
                "Embedding"  = EXCLUDED."Embedding",
                "UpdatedAt"  = NOW() AT TIME ZONE 'UTC';
            """;

        cmd.Parameters.AddWithValue("id",         record.Id);
        cmd.Parameters.AddWithValue("userId",     record.UserId);
        cmd.Parameters.AddWithValue("sourceType", (int)record.SourceType);
        cmd.Parameters.AddWithValue("sourceId",   record.SourceId);
        cmd.Parameters.AddWithValue("title",      (object?)record.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("chunkIndex", record.ChunkIndex);
        cmd.Parameters.AddWithValue("createdAt",  record.CreatedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes every chunk embedding for the given source item
    /// (all ChunkIndex values).
    /// </summary>
    public async Task DeleteBySourceAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = """
            DELETE FROM "EmbeddingRecords"
            WHERE "UserId"     = @userId
              AND "SourceType" = @sourceType
              AND "SourceId"   = @sourceId;
            """;

        cmd.Parameters.AddWithValue("userId",     userId);
        cmd.Parameters.AddWithValue("sourceType", (int)sourceType);
        cmd.Parameters.AddWithValue("sourceId",   sourceId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Removes every embedding record of the given source type owned by the user.
    /// Used for bulk-clear operations (e.g. "delete all documents").
    /// </summary>
    public async Task DeleteAllBySourceTypeAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = """
            DELETE FROM "EmbeddingRecords"
            WHERE "UserId"     = @userId
              AND "SourceType" = @sourceType;
            """;

        cmd.Parameters.AddWithValue("userId",     userId);
        cmd.Parameters.AddWithValue("sourceType", (int)sourceType);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Removes ALL embedding records owned by the user.
    /// Called during account deletion — must run before the Users row is removed.
    /// </summary>
    public async Task DeleteAllByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = """
            DELETE FROM "EmbeddingRecords"
            WHERE "UserId" = @userId;
            """;

        cmd.Parameters.AddWithValue("userId", userId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    // ── Similarity search ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <paramref name="topK"/> chunk rows most similar to
    /// <paramref name="queryVector"/> for the given user.
    /// The caller (<c>SemanticSearchService</c>) is responsible for
    /// deduplicating multiple chunks from the same source.
    /// </summary>
    public async Task<List<EmbeddingSearchResult>> SearchAsync(
        Guid userId,
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (queryVector is null || queryVector.Length == 0)
            return [];

        var literal = ToLiteral(queryVector);

        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = $"""
            SELECT "Id", "UserId", "SourceType", "SourceId", "Title", "ChunkIndex",
                   1 - ("Embedding" <=> '{literal}'::vector) AS "Score"
            FROM "EmbeddingRecords"
            WHERE "UserId" = @userId
            ORDER BY "Embedding" <=> '{literal}'::vector
            LIMIT @topK;
            """;

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("topK",   topK);

        var results = new List<EmbeddingSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var record = new EmbeddingRecord
            {
                Id         = reader.GetGuid(0),
                UserId     = reader.GetGuid(1),
                SourceType = (EmbeddingSourceType)reader.GetInt32(2),
                SourceId   = reader.GetGuid(3),
                Title      = reader.IsDBNull(4) ? null : reader.GetString(4),
                ChunkIndex = reader.GetInt32(5),
                Embedding  = [] // not needed for search results
            };

            var score = reader.IsDBNull(6) ? 0.0 : reader.GetDouble(6);
            results.Add(new EmbeddingSearchResult(record, score));
        }

        return results;
    }

    // ── Count ─────────────────────────────────────────────────────────────────

    public async Task<int> CountByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = await OpenCommandAsync(cancellationToken);
        cmd.CommandText = """
            SELECT COUNT(*)::int FROM "EmbeddingRecords" WHERE "UserId" = @userId;
            """;
        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is int n ? n : 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a float array to the pgvector literal format: <c>[v0,v1,…]</c>.
    /// PostgreSQL accepts this string and casts it to <c>vector</c> automatically.
    /// </summary>
    private static string ToLiteral(float[] values)
    {
        var parts = values.Select(v => v.ToString("G", CultureInfo.InvariantCulture));
        return "[" + string.Join(",", parts) + "]";
    }

    /// <summary>
    /// Opens the underlying Npgsql connection (if not already open) and
    /// returns a new command on that connection.
    /// </summary>
    private async Task<NpgsqlCommand> OpenCommandAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        return connection.CreateCommand();
    }
}
