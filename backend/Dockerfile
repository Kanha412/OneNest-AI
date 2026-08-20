# ─────────────────────────────────────────────────────────────────────────────
# OneNest Backend — Production Dockerfile
#
# Architecture:
#   Stage 1 (build)  : restore + publish the ASP.NET Core application
#   Stage 2 (model)  : download and verify the all-MiniLM-L6-v2 ONNX model
#   Stage 3 (runtime): minimal ASP.NET runtime image with app + bundled model
#
# Embedding provider: LocalEmbeddingProvider (zero-cost, no API key)
#   Model:   Xenova/all-MiniLM-L6-v2 — INT8 dynamic-quantized ONNX variant
#   Size:    ~22 MB (vs ~86 MB FP32) — same 384-dim output, same interface
#   License: Apache 2.0
#
# The model is downloaded ONCE at docker build time (not at container runtime)
# and verified against a known SHA256 hash for reproducibility.
# If HuggingFace serves a different file the build aborts immediately.
#
# Runtime secrets must be supplied as environment variables (Render dashboard):
#   ConnectionStrings__DefaultConnection  = <supabase-connection-string>
#   Jwt__Key                              = <long-random-key>
#   AI__ApiKey                            = <gemini-api-key>
#   Supabase__Url                         = https://<project-ref>.supabase.co
#   Supabase__ServiceRoleKey              = <service-role-key>
#   Supabase__StorageBucket               = onenest-documents
#   Cors__AllowedOrigins__0               = https://<angular-render-url>
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer-cache-friendly restore
COPY OneNest.slnx ./
COPY OneNest.Domain/OneNest.Domain.csproj             OneNest.Domain/
COPY OneNest.Application/OneNest.Application.csproj   OneNest.Application/
COPY OneNest.Infrastructure/OneNest.Infrastructure.csproj OneNest.Infrastructure/
COPY OneNest.API/OneNest.API.csproj                   OneNest.API/

RUN dotnet restore OneNest.API/OneNest.API.csproj

# Copy the full source tree and publish
COPY . .
RUN dotnet publish OneNest.API/OneNest.API.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# ── Tokenizer vocabulary verification ─────────────────────────────────────────
# BERTTokenizers v1.2.0 ships Vocabularies/base_uncased.txt as a NuGet content
# file that dotnet publish copies to the publish output directory.
# LocalEmbeddingProvider switches CWD to AppContext.BaseDirectory (/app) and
# calls new BertUncasedBaseTokenizer(), which loads this file relative to CWD.
#
# If the vocabulary is missing the tokenizer throws at container startup,
# LocalEmbeddingProvider silently disables itself, and ALL semantic search /
# RAG is broken — with no obvious error message.
#
# This check turns a silent runtime failure into a loud build-time failure.
RUN test -f /app/publish/Vocabularies/base_uncased.txt \
    || (echo "ERROR: Vocabularies/base_uncased.txt is missing from the publish output." \
            "BERTTokenizers v1.2.0 must copy this file via NuGet contentFiles." \
            "Check OneNest.Infrastructure.csproj and BERTTokenizers package version." \
        && exit 1)

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
# Do NOT change this hash unless you have independently verified the new artifact.
RUN echo "afdb6f1a0e45b715d0bb9b11772f032c399babd23bfc31fed1c170afc848bdb1  /models/all-MiniLM-L6-v2/model.onnx" \
    | sha256sum -c - \
    && echo "Model SHA256 verified: Xenova/all-MiniLM-L6-v2 INT8 quantized, 384-dim."

# ── Stage 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application (includes Vocabularies/base_uncased.txt)
COPY --from=build /app/publish .

# Copy verified ONNX model — baked into the image, no runtime download needed
COPY --from=model-download /models/ /app/models/

# ── Configuration ──────────────────────────────────────────────────────────────
# Tell LocalEmbeddingProvider where the bundled model lives.
# This overrides the default ~/.onenest/... path used in local development.
ENV LocalEmbedding__ModelDirectory=/app/models/all-MiniLM-L6-v2

ENV ASPNETCORE_ENVIRONMENT=Production

# ── Port ───────────────────────────────────────────────────────────────────────
# Render automatically sets PORT to the port configured in the Render dashboard
# (default 10000 for new services).  The entrypoint reads $PORT at container
# start time so the app always listens on the correct port.
# For local Docker testing the default is 8080:
#   docker build -t onenest-api ./backend
#   docker run -p 8080:8080 -e PORT=8080 ... onenest-api
ENV PORT=8080
EXPOSE 8080

# Run as the non-root 'app' user pre-built into the .NET aspnet base image
# (available since .NET 8).  The app only reads files that were COPY'd in above,
# so no write access to /app is needed.
USER app

# Use sh -c so $PORT is expanded at container start time, then exec replaces
# the shell with dotnet (making it PID 1) so signals (SIGTERM) are received correctly.
ENTRYPOINT ["sh", "-c", "exec dotnet OneNest.API.dll --urls http://+:${PORT}"]
