using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

/// <summary>
/// Persists a semantic embedding vector for one <em>chunk</em> of a
/// user-owned workspace item (note or document).
///
/// A single source item may produce multiple <see cref="EmbeddingRecord"/>s —
/// one per text chunk — identified together by
/// (<see cref="UserId"/>, <see cref="SourceType"/>, <see cref="SourceId"/>)
/// and individually by <see cref="ChunkIndex"/>.
///
/// The <see cref="Embedding"/> field is stored as <c>vector(N)</c> in
/// PostgreSQL via the pgvector extension, where N matches
/// <c>Embeddings:Dimension</c> in configuration (default 768 for Gemini,
/// 384 for Local/MiniLM).  The Domain layer carries a plain <c>float[]</c>
/// so it stays free of any Infrastructure dependency.
/// </summary>
public class EmbeddingRecord
{
    public Guid Id { get; set; }

    /// <summary>Owner — every search is scoped to a single user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Whether the source item is a Note or a Document.</summary>
    public EmbeddingSourceType SourceType { get; set; }

    /// <summary>PK of the source entity (Note.Id or Document.Id).</summary>
    public Guid SourceId { get; set; }

    /// <summary>Human-readable label — returned with search results so the
    /// UI can show a title without an extra join.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// Zero-based position of this chunk within the source document.
    /// Chunk 0 is the beginning of the text; higher indices follow in order.
    /// The unique database index is on (UserId, SourceType, SourceId, ChunkIndex).
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// L2-normalised embedding vector whose length equals
    /// <c>Embeddings:Dimension</c> in configuration (768 by default).
    /// Stored as <c>vector(N)</c> in PostgreSQL; accessed via raw ADO.NET
    /// in <c>EmbeddingRepository</c> — no EF Core type mapping required.
    /// </summary>
    public float[] Embedding { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
