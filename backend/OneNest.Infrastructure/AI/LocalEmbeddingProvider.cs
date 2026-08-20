using BERTTokenizers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using OneNest.Application.Interfaces.AI;

namespace OneNest.Infrastructure.AI;

/// <summary>
/// Local, zero-cost ONNX embedding provider using the
/// <b>all-MiniLM-L6-v2</b> sentence-transformer model (Apache 2.0).
///
/// <list type="bullet">
///   <item>Produces 384-dimensional L2-normalised embedding vectors.</item>
///   <item>Requires no API key, no paid service, no external runtime.</item>
///   <item>The INT8-quantized ONNX model (~22 MB) is downloaded from HuggingFace
///         CDN on first use and cached in
///         <see cref="LocalEmbeddingOptions.ModelDirectory"/> as <c>model.onnx</c>.
///         Tokenisation uses the BERT uncased vocabulary bundled with the
///         BERTTokenizers NuGet package — no vocab download required.</item>
///   <item>In production (Docker/Render) the model is bundled in the image —
///         no HuggingFace access is required at container start time.</item>
///   <item>If initialisation fails the provider returns <c>null</c> for every
///         request — semantic search is silently disabled; all other features
///         remain unaffected.</item>
///   <item>Registered as <c>Singleton</c>: the ONNX <see cref="InferenceSession"/>
///         is thread-safe and expensive to construct.</item>
/// </list>
/// </summary>
public sealed class LocalEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    // ── Model source (HuggingFace CDN — no auth required) ────────────────────
    // Source: Xenova/all-MiniLM-L6-v2 — INT8 dynamic-quantized ONNX variant.
    // Same weights as sentence-transformers/all-MiniLM-L6-v2; same 384-dim output.
    // Verified: inputs {input_ids, attention_mask, token_type_ids} → last_hidden_state[1,seq,384].
    // Downloaded as model_quantized.onnx but saved locally as model.onnx (filename transparent to caller).

    private const string ModelUrl =
        "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx";

    // ── Model constants ───────────────────────────────────────────────────────

    private const int MaxSequenceLength  = 512;
    private const int EmbeddingDimension = 384;

    // ── Instance state ────────────────────────────────────────────────────────

    private readonly LocalEmbeddingOptions          _options;
    private readonly ILogger<LocalEmbeddingProvider> _logger;
    private readonly HttpClient                      _httpClient;
    private readonly SemaphoreSlim                   _initLock = new(1, 1);

    private InferenceSession?         _session;
    private BertUncasedBaseTokenizer? _tokenizer;
    private bool _initialized; // set once; guarded by _initLock
    private bool _available;   // false after permanent init failure

    public LocalEmbeddingProvider(
        IOptions<LocalEmbeddingOptions> options,
        ILogger<LocalEmbeddingProvider> logger,
        HttpClient httpClient)
    {
        _options    = options.Value;
        _logger     = logger;
        _httpClient = httpClient;
    }

    // ── IEmbeddingProvider ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<float[]?> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Fast path: permanently unavailable after a failed init
        if (_initialized && !_available)
            return null;

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            if (!_available || _session is null || _tokenizer is null)
            {
                _logger.LogWarning(
                    "LocalEmbeddingProvider: provider not available (initialized={Init}, available={Avail}). " +
                    "Check startup logs for the init failure reason.",
                    _initialized, _available);
                return null;
            }

            // ── Sanitise text before tokenisation ─────────────────────────────
            // PDF-extracted text can contain control characters, zero-width
            // joiners, and other non-printable Unicode that crash or confuse
            // the BERT tokenizer.  Strip anything outside printable ASCII+
            // common Unicode letters/punctuation so inference is always stable.
            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                _logger.LogWarning(
                    "LocalEmbeddingProvider: SanitizeText returned empty for a {Len}-char input " +
                    "(all chars were control/format/surrogate). Skipping inference.",
                    text.Length);
                return null;
            }

            // ── Tokenise ──────────────────────────────────────────────────────
            // Encode returns List<(long InputIds, long TokenTypeIds, long AttentionMask)>
            // — one element per token position, padded to MaxSequenceLength.
            IList<(long InputIds, long TokenTypeIds, long AttentionMask)> encoded;
            try
            {
                encoded = _tokenizer.Encode(MaxSequenceLength, sanitized);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "LocalEmbeddingProvider: tokenizer.Encode threw for a {Len}-char input. " +
                    "Check for unsupported Unicode characters in the document text.",
                    sanitized.Length);
                return null;
            }

            int seqLen = encoded.Count; // == MaxSequenceLength after padding

            var inputIds      = new long[seqLen];
            var tokenTypeIds  = new long[seqLen];
            var attentionMask = new long[seqLen];

            for (int i = 0; i < seqLen; i++)
            {
                inputIds[i]      = encoded[i].InputIds;
                tokenTypeIds[i]  = encoded[i].TokenTypeIds;
                attentionMask[i] = encoded[i].AttentionMask;
            }

            // ── Build ONNX tensors [1, seqLen] via OrtValue (zero-copy) ───────
            var shape = new long[] { 1L, (long)seqLen };

            using var inputIdsOrt  = OrtValue.CreateTensorValueFromMemory(inputIds,      shape);
            using var maskOrt      = OrtValue.CreateTensorValueFromMemory(attentionMask, shape);
            using var typeIdsOrt   = OrtValue.CreateTensorValueFromMemory(tokenTypeIds,  shape);

            // ── Run inference ─────────────────────────────────────────────────
            IDisposableReadOnlyCollection<OrtValue> results;
            try
            {
                using var runOptions = new RunOptions();
                results = _session.Run(
                    runOptions,
                    inputNames:  new[] { "input_ids",  "attention_mask", "token_type_ids"   },
                    inputValues: new[] { inputIdsOrt,  maskOrt,          typeIdsOrt         },
                    outputNames: new[] { "last_hidden_state" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "LocalEmbeddingProvider: ONNX session.Run threw for a {SeqLen}-token input. " +
                    "The model file may be corrupt or incompatible.",
                    seqLen);
                return null;
            }

            using (results)
            {
                // ── Read last_hidden_state [1, seqLen, 384] ───────────────────────
                // GetTensorDataAsSpan returns the flat row-major data:
                //   flat index = (0 * seqLen + t) * EmbeddingDimension + d
                var hiddenFlat = results[0].GetTensorDataAsSpan<float>();

                // ── Mean-pool over non-padding tokens → L2 normalise ──────────────
                var embedding = MeanPool(hiddenFlat, attentionMask, seqLen);
                L2Normalize(embedding);

                return embedding;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LocalEmbeddingProvider: unexpected error during embedding.");
            return null;
        }
    }

    // ── Lazy initialisation ───────────────────────────────────────────────────

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        // Use CancellationToken.None for the lock and all init I/O.
        //
        // WHY: initialization is a one-time, long-running operation (model download
        // + tokenizer construction + ONNX session creation).  Passing the caller's
        // HTTP request token here would permanently kill the provider if the
        // request is cancelled before init completes (e.g. user closes Swagger
        // mid-backfill).  Once _initialized=true / _available=false, ALL
        // subsequent EmbedAsync calls short-circuit and return null — no retry
        // ever occurs.  CancellationToken.None ensures init always completes
        // (or fails for a non-transient reason) regardless of HTTP lifetime.
        await _initLock.WaitAsync(CancellationToken.None);
        try
        {
            if (_initialized) return; // double-check after acquiring lock

            var modelDir  = ResolveModelDirectory();
            Directory.CreateDirectory(modelDir);

            var modelPath = Path.Combine(modelDir, "model.onnx");
            await DownloadIfMissingAsync(ModelUrl, modelPath, "ONNX model (~22 MB, INT8 quantized)", CancellationToken.None);

            // BertUncasedBaseTokenizer resolves its vocabulary file using the
            // process working directory (CWD).  The vocabulary is bundled inside
            // the BERTTokenizers NuGet package and copied to the DLL output folder
            // at build/publish time (Vocabularies/base_uncased.txt).
            // CWD is not guaranteed to equal the DLL output folder (e.g. when
            // running via `dotnet run` from the source directory).
            // Solution: temporarily switch CWD to AppContext.BaseDirectory for the
            // constructor call, then restore it.  SemaphoreSlim guarantees this
            // runs on a single thread at a time, so it is safe.
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
                _tokenizer = new BertUncasedBaseTokenizer();
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
            }

            _session   = new InferenceSession(modelPath);
            _available = true;

            _logger.LogInformation(
                "LocalEmbeddingProvider: all-MiniLM-L6-v2 ready ({Dir}).", modelDir);
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.LogWarning(ex,
                "LocalEmbeddingProvider: failed to initialise. " +
                "Semantic search will be disabled until the service restarts.");
        }
        finally
        {
            _initialized = true; // mark once — do NOT retry on every request
            _initLock.Release();
        }
    }

    private string ResolveModelDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.ModelDirectory))
            return Path.GetFullPath(_options.ModelDirectory);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".onenest", "models", "all-MiniLM-L6-v2");
    }

    private async Task DownloadIfMissingAsync(
        string url,
        string destPath,
        string label,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destPath)) return;

        _logger.LogInformation(
            "LocalEmbeddingProvider: downloading {Label} from {Url} …", label, url);

        // Stream directly to disk — model is ~22 MB
        using var response = await _httpClient.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file   = new FileStream(
            destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(file, cancellationToken);

        _logger.LogInformation(
            "LocalEmbeddingProvider: {Label} saved to {Path}.", label, destPath);
    }

    // ── Text sanitisation ────────────────────────────────────────────────────

    /// <summary>
    /// Sanitizes text for the BERT uncased tokenizer:
    ///
    /// 1. Strips control characters (C0/C1), surrogates, private-use, and
    ///    format characters (zero-width joiners, soft-hyphens, BOM, …).
    ///    These crash or silently mis-encode in BertUncasedBaseTokenizer.
    ///
    /// 2. Breaks runs of non-whitespace longer than 60 characters by inserting
    ///    spaces.  The BERTTokenizers WordPiece tokenizer is O(n²) on unknown
    ///    "words" — a 500-char space-free run (common in PDFs where word
    ///    boundaries are encoded as glyph offsets rather than space glyphs) can
    ///    hang the tokenizer for minutes.  BERT's vocabulary never has entries
    ///    longer than ~30 chars, so any run longer than 60 chars is already
    ///    guaranteed to be tokenized as multiple sub-word tokens — splitting it
    ///    early does not change the final embedding meaningfully.
    ///
    /// Call this before every <see cref="EmbedAsync"/> invocation.
    /// </summary>
    private static string SanitizeText(string text)
    {
        const int MaxTokenRunChars = 60; // BERT vocab max word length is ~30; 60 is a safe upper bound

        var sb      = new System.Text.StringBuilder(text.Length);
        int runLen  = 0; // consecutive non-whitespace chars since last whitespace

        foreach (char c in text)
        {
            var cat = char.GetUnicodeCategory(c);

            // ── Strip invisible/dangerous characters ─────────────────────────
            // The explicit c < ' ' guard covers the ASCII control range
            // (U+0000–U+001F) unconditionally — GetUnicodeCategory may return
            // OtherNotAssigned for U+0000 on some .NET runtime versions.
            if (c < ' '   // U+0000–U+001F: all ASCII control characters
             || c == ''  // U+007F: DEL
             || cat == System.Globalization.UnicodeCategory.Control
             || cat == System.Globalization.UnicodeCategory.Surrogate
             || cat == System.Globalization.UnicodeCategory.PrivateUse
             || cat == System.Globalization.UnicodeCategory.Format)
            {
                // Replace with a space so word boundaries are preserved
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    sb.Append(' ');
                runLen = 0;
                continue;
            }

            // ── Break oversized runs ─────────────────────────────────────────
            // A run of 60+ non-whitespace chars is almost certainly fused PDF
            // text (no glyph spaces).  Insert a space mid-run so WordPiece
            // never sees a token longer than MaxTokenRunChars.
            if (char.IsWhiteSpace(c))
            {
                runLen = 0;
            }
            else
            {
                runLen++;
                if (runLen > MaxTokenRunChars)
                {
                    // Insert boundary space, then start a new run
                    sb.Append(' ');
                    runLen = 1;
                }
            }

            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    // ── Pooling helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Mean-pools the flat <paramref name="hiddenFlat"/> span over non-padding
    /// token positions (where <paramref name="attentionMask"/>[t] != 0).
    ///
    /// <paramref name="hiddenFlat"/> layout: row-major [1, seqLen, 384],
    /// so <c>hiddenFlat[t * EmbeddingDimension + d]</c> gives token t, dim d.
    /// </summary>
    private static float[] MeanPool(
        ReadOnlySpan<float> hiddenFlat,
        long[]              attentionMask,
        int                 seqLen)
    {
        var result      = new float[EmbeddingDimension];
        int nonPadCount = 0;

        for (int t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0L) continue;
            nonPadCount++;
            int offset = t * EmbeddingDimension;
            for (int d = 0; d < EmbeddingDimension; d++)
                result[d] += hiddenFlat[offset + d];
        }

        if (nonPadCount > 0)
            for (int d = 0; d < EmbeddingDimension; d++)
                result[d] /= nonPadCount;

        return result;
    }

    /// <summary>
    /// Normalises <paramref name="vector"/> to unit length (L2 norm = 1.0).
    /// No-op if the norm is near zero.
    /// </summary>
    private static void L2Normalize(float[] vector)
    {
        float norm = 0f;
        foreach (var v in vector) norm += v * v;
        norm = MathF.Sqrt(norm);

        if (norm < 1e-10f) return;
        for (int i = 0; i < vector.Length; i++)
            vector[i] /= norm;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}
