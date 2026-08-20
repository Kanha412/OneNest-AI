# Copilot Instructions

Guidelines for AI assistants working in this repo.

---

## Architecture rules

- Backend follows Clean Architecture: `Domain → Application → Infrastructure → API`. Never import outer layers into inner ones.
- All services depend on interfaces from `OneNest.Application` — never concrete implementations directly.
- AI provider (`IAIProvider`), embedding provider (`IEmbeddingProvider`), and file storage (`IFileStorageService`) must be swappable via DI.
- AI settings (Provider, Model, ApiKey, Temperature, MaxTokens) come from configuration options — never hardcoded.
- Prompt construction is isolated from the provider — `IAIPromptBuilder` or service-level prompt methods, not inline strings in controllers.

## Embedding model

- **Default and preferred**: `LocalEmbeddingProvider` — ONNX `all-MiniLM-L6-v2` INT8, 384-dim, runs in-process.
- `GeminiEmbeddingProvider` (768-dim) is an alternate, not the default.
- `EmbeddingRecords` uses `vector(384)` — do not change the dimension without a new migration.
- `EmbeddingRepository` uses raw ADO.NET for pgvector operations — do not switch to EF Core without verifying Npgsql pgvector support.

## AI model

- Chat model: `gemini-2.5-flash` (not `gemini-3.5-flash` — that was an old placeholder).
- Set via `AI__Model` environment variable; never hardcode in Program.cs.

## Cost constraint

- Do not introduce any paid API for embeddings or AI when a free/local alternative works.
- ONNX model runs locally at zero cost — prefer it for embeddings.
- Gemini API key is only used for chat and RAG generation.

## Settings UI preferences

- Settings page has: Account, AI Preferences, Storage, Health (units), Privacy & Security, About.
- No Notifications or Appearance sections.
- Storage section shows Total Storage Used + Total Stored Files (documents + health reports combined).
- Security section shows only Change Password and Delete Account — each opens a modal.
- No inline Save Changes button in Storage, Security, or About tabs.

## Data ownership

- Every user-owned entity has a `UserId` column.
- All queries in services/repositories must filter by `UserId` from `ICurrentUserService`.
- Never return or expose another user's data.

## Background indexing

- Semantic indexing after note/document CRUD must use `Task.Run()` in a new DI scope.
- HTTP response must return immediately — indexing never blocks the user.
- Indexing errors are logged at Warning level and never propagated.

## When asked for explanations

- Provide explanation only when asked — don't include code changes unless requested.
