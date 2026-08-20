namespace OneNest.Infrastructure.Storage;

/// <summary>
/// Configuration options for Supabase Storage.
/// Bind via the <c>"Supabase"</c> configuration section.
///
/// All production values must be supplied as environment variables
/// (Render secret environment variables) — never commit them.
///
///   Supabase__Url            = https://&lt;project-ref&gt;.supabase.co
///   Supabase__ServiceRoleKey = (Render secret — server-side only)
///   Supabase__StorageBucket  = onenest-documents
/// </summary>
public class SupabaseOptions
{
    /// <summary>
    /// Your Supabase project URL, e.g. https://abcxyz.supabase.co
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The Supabase service-role secret key.
    /// SERVER-SIDE ONLY — must never be forwarded to the Angular frontend.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the private Supabase Storage bucket used to store user documents.
    /// Defaults to "onenest-documents".
    /// </summary>
    public string StorageBucket { get; set; } = "onenest-documents";
}
