# ─────────────────────────────────────────────────────────────────────────────
# OneNest Backend — Production Dockerfile
#
# Architecture:
#   Stage 1 (build) : restore + publish the ASP.NET Core application
#   Stage 2 (model) : download and verify the all-MiniLM-L6-v2 ONNX model
#   Stage 3 (runtime): minimal ASP.NET runtime image with app + bundled model
#
# Embedding provider: LocalEmbeddingProvider
#   Model:  Xenova/all-MiniLM-L6-v2 — INT8 dynamic-quantized ONNX variant
#   Size:   ~22 MB (vs ~86 MB FP32) — same 384-dim output, same interface
#   License: Apache 2.0
#
# The model is downloaded ONCE at docker build time (not at container runtime)
# and verified against a known SHA256 hash for reproducibility.
# If HuggingFace serves a different file, the build aborts immediately.
#
# Secrets must be supplied as environment variables at runtime (Render dashboard):
#   ConnectionStrings__DefaultConnection=<supabase-connection-string>
#   Jwt__Key=<jwt-signing-key>
#   AI__ApiKey=<gemini-api-key>
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and all project files first (layer-cache friendly)
COPY OneNest.slnx ./
COPY OneNest.Domain/OneNest.Domain.csproj             OneNest.Domain/
COPY OneNest.Application/OneNest.Application.csproj   OneNest.Application/
COPY OneNest.Infrastructure/OneNest.Infrastructure.csproj OneNest.Infrastructure/
COPY OneNest.API/OneNest.API.csproj                   OneNest.API/
COPY OneNest.Tests/OneNest.Tests.csproj               OneNest.Tests/

RUN dotnet restore OneNest.API/OneNest.API.csproj

# Copy the full source tree and publish
COPY . .
RUN dotnet publish OneNest.API/OneNest.API.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# ── Stage 2: Download ONNX model ──────────────────────────────────────────────
# Downloads model_quantized.onnx at DOCKER BUILD time — not at container runtime.
# Saved as model.onnx (the filename LocalEmbeddingProvider always looks for).
#
# Source: Xenova/all-MiniLM-L6-v2 (INT8 dynamic-quantized; same interface as FP32)
# Verified inputs:  input_ids (int64), attention_mask (int64), token_type_ids (int64)
# Verified output:  last_hidden_state (float32, shape [-1, -1, 384])
# SHA256 pins the exact artifact — mismatched file → build fails, no silent corruption.
#
# Model:  Xenova/all-MiniLM-L6-v2 / onnx/model_quantized.onnx
# Size:   ~22 MB  (22,972,370 bytes)
# SHA256: afdb6f1a0e45b715d0bb9b11772f032c399babd23bfc31fed1c170afc848bdb1
FROM debian:bookworm-slim AS model-download
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /models/all-MiniLM-L6-v2

RUN curl -fsSL \
    "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx" \
    -o /models/all-MiniLM-L6-v2/model.onnx

# Verify SHA256 — build aborts if the downloaded file does not match.
RUN echo "afdb6f1a0e45b715d0bb9b11772f032c399babd23bfc31fed1c170afc848bdb1  /models/all-MiniLM-L6-v2/model.onnx" \
    | sha256sum -c - \
    && echo "Model SHA256 verified: Xenova/all-MiniLM-L6-v2 INT8 quantized, 384-dim."

# ── Stage 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy verified ONNX model into the image
# The model is now baked into the Docker image layer — no runtime download needed.
COPY --from=model-download /models/ /app/models/

# ── Configuration ─────────────────────────────────────────────────────────────
# Tell LocalEmbeddingProvider where the bundled model lives.
# This overrides the default ~/.onenest/... path used in local development.
ENV LocalEmbedding__ModelDirectory=/app/models/all-MiniLM-L6-v2

# ASP.NET Core listens on port 8080 (Render's expected default)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "OneNest.API.dll"]
