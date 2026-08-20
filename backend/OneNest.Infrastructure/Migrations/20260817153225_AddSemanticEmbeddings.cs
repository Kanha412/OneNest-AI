using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneNest.Infrastructure.Migrations
{
    /// <summary>
    /// Phase 8 — Semantic Search Foundation.
    ///
    /// Creates the <c>EmbeddingRecords</c> table that stores 384-dimensional
    /// pgvector embeddings for user-owned notes and documents.
    ///
    /// DDL is written manually because EF Core's Npgsql provider cannot
    /// generate <c>vector(384)</c> column types without the optional
    /// Pgvector.EntityFrameworkCore package that is deliberately excluded.
    /// All embedding I/O goes through raw ADO.NET (<see cref="EmbeddingRepository"/>);
    /// EF Core does not own this table.
    ///
    /// Schema design:
    /// <list type="bullet">
    ///   <item>A source item (note or document) can produce many rows — one per
    ///         text chunk — identified by <c>ChunkIndex</c> (0-based).</item>
    ///   <item>UNIQUE constraint on (UserId, SourceType, SourceId, ChunkIndex)
    ///         enables O(1) upsert and clean re-index on edit.</item>
    ///   <item>Column width is <c>vector(384)</c>, matching the default
    ///         <c>LocalEmbeddingProvider</c> (all-MiniLM-L6-v2, 384 dims).
    ///         To switch to <c>GeminiEmbeddingProvider</c> (768 dims) set
    ///         <c>Embeddings:Provider=Gemini</c> and change the column type here
    ///         before applying this migration to a fresh database.</item>
    ///   <item>No ANN index: at personal-scale (&lt;10 K rows) a sequential
    ///         cosine scan completes in &lt;5 ms.  Add
    ///         <c>USING hnsw (…) WITH (m=16, ef_construction=64)</c> when
    ///         row count exceeds ~50 K.</item>
    /// </list>
    /// </summary>
    public partial class AddSemanticEmbeddings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Enable the pgvector extension (idempotent)
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS vector;");

            // 2. Create the EmbeddingRecords table
            //    ChunkIndex: zero-based position of this chunk within its source document.
            //    Multiple rows share the same SourceId (one per chunk).
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""EmbeddingRecords"" (
                    ""Id""         UUID         NOT NULL,
                    ""UserId""     UUID         NOT NULL,
                    ""SourceType"" INTEGER      NOT NULL,
                    ""SourceId""   UUID         NOT NULL,
                    ""Title""      TEXT,
                    ""ChunkIndex"" INTEGER      NOT NULL DEFAULT 0,
                    ""Embedding""  vector(384)  NOT NULL,
                    ""CreatedAt""  TIMESTAMP    NOT NULL,
                    ""UpdatedAt""  TIMESTAMP,
                    CONSTRAINT ""PK_EmbeddingRecords"" PRIMARY KEY (""Id"")
                );");

            // 3. Unique index for fast upsert / delete / re-index
            //    Includes ChunkIndex so each chunk of a document has its own row.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EmbeddingRecords_Source_Chunk""
                    ON ""EmbeddingRecords"" (""UserId"", ""SourceType"", ""SourceId"", ""ChunkIndex"");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EmbeddingRecords"";");
        }
    }
}
