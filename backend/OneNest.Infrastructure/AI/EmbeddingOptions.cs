namespace OneNest.Infrastructure.AI;

/// <summary>
/// Top-level embedding configuration.
/// Bind via <c>"Embeddings"</c> in appsettings.json.
/// </summary>
public class EmbeddingOptions
{
    /// <summary>
    /// Which embedding provider to activate.
    /// Accepted values (case-insensitive): <c>"Gemini"</c> (default) | <c>"Local"</c>.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <b>Gemini</b> — calls Google Generative AI <c>text-embedding-004</c>
    ///     via HTTPS.  Requires <c>AI:ApiKey</c>.  Produces
    ///     <see cref="Dimension"/>-dimensional vectors (default 768).
    ///     Free tier: 1 500 requests / minute, $0 cost.
    ///     Best for hosted / Render+Supabase deployments.
    ///   </item>
    ///   <item>
    ///     <b>Local</b> — runs all-MiniLM-L6-v2 ONNX on-device via
    ///     <c>Microsoft.ML.OnnxRuntime</c>.  No API key required.
    ///     Produces 384-dimensional vectors.  Set <see cref="Dimension"/>
    ///     to 384 and re-create the <c>EmbeddingRecords</c> table accordingly.
    ///     Best for offline or local-dev usage.
    ///   </item>
    /// </list>
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// Output vector length.  Must match the <c>vector(N)</c> column width
    /// declared in the <c>EmbeddingRecords</c> PostgreSQL table.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <b>768</b> (default) — matches <c>text-embedding-004</c>'s native
    ///     output and the default database migration.
    ///   </item>
    ///   <item>
    ///     <b>384</b> — required when using the Local/MiniLM provider, whose
    ///     model architecture is fixed at 384 dimensions.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// <b>Warning:</b> changing this value after embeddings have been stored
    /// requires dropping and re-creating the <c>EmbeddingRecords</c> table and
    /// re-running the <c>POST /api/semantic-search/backfill</c> endpoint for
    /// every user.
    /// </para>
    /// </summary>
    public int Dimension { get; set; } = 768;
}
