using OneNest.Infrastructure;
using OneNest.Application.Interfaces.AI;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Forwarded-headers (Render TLS proxy) ──────────────────────────────────────
// Render terminates public HTTPS at its edge and forwards requests to the
// container over plain HTTP.  Without this, X-Forwarded-Proto / X-Forwarded-For
// are ignored and middleware further down the pipeline sees the wrong scheme/IP.
// KnownNetworks/KnownProxies are cleared so Render's proxy IPs are trusted
// regardless of their CIDR range (Render may change proxy IPs between deployments).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();   // IPNetwork (System.Net, .NET 8+)
    options.KnownProxies.Clear();
});

// ── Application services ───────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter the JWT token. Example: eyJhb...",
    });

    options.DocumentFilter<OneNest.API.Swagger.SecurityRequirementsDocumentFilter>();
});

// ── JWT Authentication ─────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                            Encoding.UTF8.GetBytes(jwtSection["Key"]!))
        };
    });

builder.Services.AddAuthorization();

// ── CORS — environment-driven ─────────────────────────────────────────────────
// Origins are loaded from configuration so the production Render URL can be
// added without modifying source code.
//
// Default (appsettings.json):   Cors:AllowedOrigins:0 = http://localhost:4200
// Production (Render env vars): Cors__AllowedOrigins__0=https://<angular-render-url>
//                               Cors__AllowedOrigins__1=https://<any-additional-origin>
//
// AllowCredentials() is intentionally omitted — this API uses JWT in the
// Authorization header (not cookies), so credentials=false is correct.
const string CorsPolicy = "OneNestCors";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (allowedOrigins.Length == 0)
    allowedOrigins = ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────

// MUST be first: restores the original scheme (https) and client IP from
// Render's reverse-proxy headers before any other middleware inspects them.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// NOTE: app.UseHttpsRedirection() is intentionally omitted.
//
// Render terminates public HTTPS at its edge.  The container receives plain HTTP
// from Render's internal network.  If we redirected to HTTPS here, every request
// would loop:  container redirects → Render edge forwards HTTP again → repeat.
// HTTPS enforcement for public traffic is handled entirely by Render's infrastructure.

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Embedding provider warm-up ─────────────────────────────────────────────────
// Trigger ONNX model initialisation at startup (not silently on the first
// upload) so any init failure (missing model file, ONNX version mismatch,
// tokenizer vocab not found) is immediately visible in the startup logs
// rather than appearing as a silent "no embedding records" mystery later.
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(2)); // let the server fully start first
    try
    {
        using var scope = app.Services.CreateScope();
        var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.EmbeddingWarmup");

        logger.LogInformation("EmbeddingWarmup: probing LocalEmbeddingProvider…");
        var vector = await embeddingProvider.EmbedAsync("startup health check");

        if (vector is { Length: > 0 })
            logger.LogInformation(
                "EmbeddingWarmup: ✓ provider ready — {Dims}-dim vectors. Semantic indexing is enabled.",
                vector.Length);
        else
            logger.LogWarning(
                "EmbeddingWarmup: ✗ provider returned null — semantic indexing is DISABLED. " +
                "Check LocalEmbeddingProvider logs above for the failure reason.");
    }
    catch (Exception ex)
    {
        app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.EmbeddingWarmup")
            .LogError(ex, "EmbeddingWarmup: probe threw unexpectedly.");
    }
});

app.Run();
