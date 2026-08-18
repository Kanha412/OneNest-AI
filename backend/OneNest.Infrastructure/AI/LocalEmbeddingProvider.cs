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
///   <item>The ONNX model (~22 MB) is downloaded from HuggingFace CDN on first
///         use and cached in <see cref="LocalEmbeddingOptions.ModelDirectory"/>.
///         Tokenisation uses the BERT uncased vocabulary bundled with the
///         BERTTokenizers NuGet package — no vocab download required.</item>
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

    private const string ModelUrl =
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";

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
                return null;

            // ── Tokenise ──────────────────────────────────────────────────────
            // Encode returns List<(long InputIds, long TokenTypeIds, long AttentionMask)>
            // — one element per token position, padded to MaxSequenceLength.
            var encoded = _tokenizer.Encode(MaxSequenceLength, text);

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
            using var runOptions = new RunOptions();
            using var results = _session.Run(
                runOptions,
                inputNames:  new[] { "input_ids",  "attention_mask", "token_type_ids"   },
                inputValues: new[] { inputIdsOrt,  maskOrt,          typeIdsOrt         },
                outputNames: new[] { "last_hidden_state" });

            // ── Read last_hidden_state [1, seqLen, 384] ───────────────────────
            // GetTensorDataAsSpan returns the flat row-major data:
            //   flat index = (0 * seqLen + t) * EmbeddingDimension + d
            var hiddenFlat = results[0].GetTensorDataAsSpan<float>();

            // ── Mean-pool over non-padding tokens → L2 normalise ──────────────
            var embedding = MeanPool(hiddenFlat, attentionMask, seqLen);
            L2Normalize(embedding);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LocalEmbeddingProvider: inference failed.");
            return null;
        }
    }

    // ── Lazy initialisation ───────────────────────────────────────────────────

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return; // double-check after acquiring lock

            var modelDir  = ResolveModelDirectory();
            Directory.CreateDirectory(modelDir);

            var modelPath = Path.Combine(modelDir, "model.onnx");
            await DownloadIfMissingAsync(ModelUrl, modelPath, "ONNX model (~22 MB)", cancellationToken);

            // Tokeniser uses the BERT uncased vocabulary bundled inside the
            // BERTTokenizers NuGet package — no extra download needed.
            _tokenizer = new BertUncasedBaseTokenizer();
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
