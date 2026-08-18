using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneNest.Infrastructure.AI;
using Xunit;

namespace OneNest.Tests.SemanticSearch;

/// <summary>
/// Unit tests for <see cref="GeminiEmbeddingProvider"/>.
/// HTTP calls are intercepted by <see cref="FakeHttpHandler"/> — no real
/// network traffic is generated.
/// </summary>
public class GeminiEmbeddingProviderTests
{
    // ── Test infrastructure ───────────────────────────────────────────────────

    /// <summary>
    /// Minimal fake message handler: always returns the pre-configured response.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private HttpRequestMessage?          _lastRequest;

        public FakeHttpHandler(HttpResponseMessage response) => _response = response;

        /// <summary>The last request sent through this handler, for assertions.</summary>
        public HttpRequestMessage? LastRequest => _lastRequest;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage  request,
            CancellationToken   cancellationToken)
        {
            _lastRequest = request;
            return Task.FromResult(_response);
        }
    }

    /// <summary>Builds a 200 OK response with a Gemini-shaped JSON body.</summary>
    private static HttpResponseMessage GeminiOkResponse(float[] values)
    {
        var csv  = string.Join(",", Array.ConvertAll(values, v => v.ToString("G")));
        var json = "{\"embedding\":{\"values\":[" + csv + "]}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static GeminiEmbeddingProvider MakeProvider(
        HttpResponseMessage response,
        string              apiKey    = "test-api-key",
        int                 dimension = 768)
    {
        var handler    = new FakeHttpHandler(response);
        var httpClient = new HttpClient(handler);

        var aiOpts  = Options.Create(new AIOptions { ApiKey = apiKey });
        var embOpts = Options.Create(new EmbeddingOptions { Dimension = dimension });
        var logger  = NullLogger<GeminiEmbeddingProvider>.Instance;

        return new GeminiEmbeddingProvider(httpClient, aiOpts, embOpts, logger);
    }

    // ── 1. Null / empty / whitespace input → null, no HTTP call ──────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task EmbedAsync_NullOrWhitespace_ReturnsNullWithoutHttpCall(string? text)
    {
        // Response that should never be reached
        var svc = MakeProvider(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await svc.EmbedAsync(text!);

        Assert.Null(result);
    }

    // ── 2. Missing API key → null, no HTTP call ───────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmbedAsync_NoApiKey_ReturnsNull(string? apiKey)
    {
        var svc = MakeProvider(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            apiKey: apiKey!);

        var result = await svc.EmbedAsync("some text");

        Assert.Null(result);
    }

    // ── 3. Successful response → correct float[] ─────────────────────────────

    [Fact]
    public async Task EmbedAsync_SuccessfulResponse_ReturnsFloatArray()
    {
        var expected = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var svc      = MakeProvider(GeminiOkResponse(expected));

        var result = await svc.EmbedAsync("Hello world");

        Assert.NotNull(result);
        Assert.Equal(expected.Length, result!.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], result[i], precision: 5);
    }

    // ── 4. HTTP error → null (no rethrow) ────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task EmbedAsync_HttpError_ReturnsNull(HttpStatusCode status)
    {
        var svc    = MakeProvider(new HttpResponseMessage(status));
        var result = await svc.EmbedAsync("some text");

        Assert.Null(result);
    }

    // ── 5. Malformed response — missing "embedding" key → null ───────────────

    [Fact]
    public async Task EmbedAsync_MissingEmbeddingKey_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error":"unexpected"}""",
                Encoding.UTF8, "application/json")
        };
        var svc    = MakeProvider(response);
        var result = await svc.EmbedAsync("text");

        Assert.Null(result);
    }

    // ── 6. Malformed response — missing "values" key → null ──────────────────

    [Fact]
    public async Task EmbedAsync_MissingValuesKey_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"embedding":{}}""",
                Encoding.UTF8, "application/json")
        };
        var svc    = MakeProvider(response);
        var result = await svc.EmbedAsync("text");

        Assert.Null(result);
    }

    // ── 7. Empty values array → null ─────────────────────────────────────────

    [Fact]
    public async Task EmbedAsync_EmptyValuesArray_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"embedding":{"values":[]}}""",
                Encoding.UTF8, "application/json")
        };
        var svc    = MakeProvider(response);
        var result = await svc.EmbedAsync("text");

        Assert.Null(result);
    }

    // ── 8. Network exception → null (no rethrow) ─────────────────────────────

    [Fact]
    public async Task EmbedAsync_NetworkException_ReturnsNull()
    {
        // Handler that throws, simulating a network failure
        var handler = new ThrowingHttpHandler();
        var client  = new HttpClient(handler);

        var svc = new GeminiEmbeddingProvider(
            client,
            Options.Create(new AIOptions { ApiKey = "key" }),
            Options.Create(new EmbeddingOptions { Dimension = 768 }),
            NullLogger<GeminiEmbeddingProvider>.Instance);

        var ex     = await Record.ExceptionAsync(() => svc.EmbedAsync("text"));
        var result = await svc.EmbedAsync("text");

        Assert.Null(ex);     // no exception escapes
        Assert.Null(result);
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("simulated network failure"));
    }

    // ── 9. Configured dimension is forwarded to the API ───────────────────────

    [Fact]
    public async Task EmbedAsync_SendsOutputDimensionalityInPayload()
    {
        const int configuredDim = 512;
        string?   capturedBody  = null;

        var handler = new CapturingHttpHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"embedding":{"values":[0.1,0.2]}}""",
                    Encoding.UTF8, "application/json")
            };
        });

        var svc = new GeminiEmbeddingProvider(
            new HttpClient(handler),
            Options.Create(new AIOptions { ApiKey = "k" }),
            Options.Create(new EmbeddingOptions { Dimension = configuredDim }),
            NullLogger<GeminiEmbeddingProvider>.Instance);

        await svc.EmbedAsync("test");

        Assert.NotNull(capturedBody);
        // outputDimensionality is serialised as a JSON integer, not a string.
        Assert.Contains($"\"outputDimensionality\":{configuredDim}", capturedBody!);
    }

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public CapturingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> h) => _handler = h;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage req, CancellationToken ct) =>
            Task.FromResult(_handler(req));
    }

    // ── 10. API key is included in the request URL ────────────────────────────

    [Fact]
    public async Task EmbedAsync_IncludesApiKeyInUrl()
    {
        const string apiKey = "my-secret-api-key";
        Uri?         requestUri = null;

        var handler = new CapturingHttpHandler(req =>
        {
            requestUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"embedding":{"values":[0.5]}}""",
                    Encoding.UTF8, "application/json")
            };
        });

        var svc = new GeminiEmbeddingProvider(
            new HttpClient(handler),
            Options.Create(new AIOptions { ApiKey = apiKey }),
            Options.Create(new EmbeddingOptions { Dimension = 768 }),
            NullLogger<GeminiEmbeddingProvider>.Instance);

        await svc.EmbedAsync("test");

        Assert.NotNull(requestUri);
        Assert.Contains($"key={apiKey}", requestUri!.ToString());
    }
}
