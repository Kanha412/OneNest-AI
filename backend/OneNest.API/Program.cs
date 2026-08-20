using OneNest.Infrastructure;
using OneNest.Application.Interfaces.AI;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT token. Example: eyJhb...",
    });

    options.DocumentFilter<OneNest.API.Swagger.SecurityRequirementsDocumentFilter>();
});

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"]!))
        };
    });

builder.Services.AddAuthorization();

const string AngularCorsPolicy = "AngularDevClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(AngularCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Embedding provider warm-up ─────────────────────────────────────────────
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