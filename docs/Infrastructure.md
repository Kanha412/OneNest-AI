# OneNest AI — Backend Infrastructure

How the backend is put together — architecture layers, dependency injection, AI integration, file storage, and embedding design.

---

## Architecture overview

OneNest follows Clean Architecture. Each layer has a clear responsibility:

```
OneNest.API              → HTTP endpoints, middleware, auth config
      ↓ depends on
OneNest.Application      → interfaces, DTOs, service contracts
      ↓ depends on
OneNest.Domain           → entities, enums — zero external dependencies
      ↑ implemented by
OneNest.Infrastructure   → EF Core, Gemini, ONNX, Supabase Storage, repositories
```

The rule: outer layers depend on inner; inner layers never import outer ones.

---

## Layer responsibilities

### OneNest.API
- 15 controllers covering all feature areas
- `Program.cs` — DI registration, middleware pipeline, auth/CORS/Swagger config
- Exception → HTTP status code mapping
- ForwardedHeaders middleware (trusts Render's TLS proxy)

### OneNest.Application
- All DTO records (request + response types)
- Service interfaces (`IAuthService`, `IDocumentService`, `IRagService`, etc.)
- Storage interface (`IFileStorageService`)
- Embedding interface (`IEmbeddingProvider`)
- Application-level exceptions (`AuthException`, etc.)

### OneNest.Domain
- 14 entities: `User`, `Note`, `TaskItem`, `Expense`, `Document`, `Medicine`, `Appointment`, `MedicalRecord`, `MedicalReport`, `AIConversation`, `AIMessage`, `UserSettings`, `ContactMessage`, `EmbeddingRecord`
- All enumerations: `TaskPriority`, `ExpenseCategory`, `TransactionType`, `DocumentCategory`, `AppointmentStatus`, `MedicineFoodTiming`, `MedicalReportCategory`, `MessageRole`, `ContactCategory`, `ContactStatus`
- No NuGet package dependencies

### OneNest.Infrastructure
- `OneNestDbContext` — EF Core DbContext with 11 migrations
- `EmbeddingRepository` — raw ADO.NET for pgvector operations (EF can't map `vector(N)` types natively)
- `SupabaseFileStorageService` — file upload/download via Supabase Storage REST API
- `LocalEmbeddingProvider` — ONNX inference (all-MiniLM-L6-v2, 384-dim, runs in-process)
- `GeminiProvider` — AI chat and RAG generation
- `RagService` — 9-step RAG pipeline
- `TextChunker` — sentence-aware text splitting (1200-char chunks, 240-char overlap)
- `SemanticIndexService` / `SemanticSearchService` / `BackfillService`
- `AIConversationService` + `AIWorkspaceOrchestrator` + 5 workspace tools
- `JwtTokenGenerator` + `CurrentUserService`

---

## Middleware pipeline (Program.cs order)

1. `UseForwardedHeaders` — reads `X-Forwarded-For`/`X-Forwarded-Proto` from Render proxy
2. `UseHttpsRedirection`
3. `UseCors` — env-driven allowed origins (`Cors__AllowedOrigins__*`)
4. `UseAuthentication` — JWT Bearer validation
5. `UseAuthorization` — `[Authorize]` / `[Authorize(Roles="Admin")]` enforcement
6. `MapControllers`

---

## Authentication

- Tokens issued by `JwtTokenGenerator`. Claims: `sub` (userId), `nameidentifier` (userId), `email`, `role`, `jti`
- Token lifetime: 7 days (configurable via `Jwt:ExpiryMinutes`)
- `CurrentUserService` reads `ClaimTypes.NameIdentifier` from `HttpContext.User` — throws `UnauthorizedAccessException` if not authenticated
- Passwords hashed with ASP.NET Core's `PasswordHasher<User>`

---

## AI integration

### Chat (Gemini)

- Provider: `GeminiProvider` → `gemini-2.5-flash`
- 30 s HTTP timeout
- Retry on 429 / 5xx: up to 3 attempts with 300–600 ms exponential backoff
- Error mapping: auth failure → 400; rate limit → 429

### Embedding (local ONNX)

- Provider: `LocalEmbeddingProvider`
- Model: `all-MiniLM-L6-v2` INT8 quantized, ~22 MB
- Output: 384-dimensional float vector
- Runs in-process inside the Docker container — no network call, no API key, zero cost
- Model downloaded and SHA256-verified at Docker build time; never downloaded at startup

**Embedding provider is selected by config:**

| `Embeddings:Provider` | Provider class | Dimensions |
|---|---|---|
| `Local` (default/recommended) | `LocalEmbeddingProvider` | 384 |
| `Gemini` (alternate) | `GeminiEmbeddingProvider` | 768 |

For production, use `Local` — it's free and works offline.

### Text chunker

Splits long text into overlapping chunks before embedding:

| Config key | Default | What it does |
|---|---|---|
| `TextChunker:ChunkSizeChars` | 1200 | Max characters per chunk |
| `TextChunker:ChunkOverlapChars` | 240 | Overlap between adjacent chunks |

Sentence-aware: won't cut in the middle of a sentence.

---

## Semantic indexing flow

When a note or document is created or updated:

1. HTTP response is returned immediately (indexing never blocks the user)
2. `Task.Run()` fires a background job in a new DI scope
3. `SemanticIndexService` deletes old chunks for that source
4. `TextChunker` splits the content into chunks
5. `LocalEmbeddingProvider.EmbedAsync()` converts each chunk to `float[384]`
6. `EmbeddingRepository.UpsertAsync()` saves to `EmbeddingRecords` (pgvector)

Any error in this chain is caught and logged at Warning — CRUD operations are never blocked by indexing failures.

When a note or document is deleted: `SemanticIndexService.DeleteIndexAsync()` removes all its embedding records.

---

## RAG pipeline (9 steps)

Triggered by `POST /api/ai/rag` or when the AI assistant uses RAG context:

1. Validate query (non-empty, max 2000 chars)
2. Embed query with `LocalEmbeddingProvider` → `float[384]`
3. Cosine search in pgvector: `SELECT … ORDER BY embedding <=> @vec WHERE UserId = @userId`
4. Filter by similarity threshold ≥ 0.70, deduplicate by (SourceType, SourceId), take topK
5. Fetch source text from Notes or Documents table
6. XML-escape source content (protection against prompt injection)
7. Assemble system prompt: sources block + conversation history (last 10 messages) + user query
8. Call Gemini with retry (max 3×, 300–600 ms backoff on 429/5xx)
9. Return `RagResponse { Answer, Sources[], HasSources, Model, Timestamp }`

Config: `Rag:TopK=5`, `Rag:SimilarityThreshold=0.70`, `Rag:MaxContextCharacters=12000`, `Rag:MaxConversationMessages=10`

---

## Workspace-aware AI

When a user sends a chat message:

1. `AIWorkspaceOrchestrator` receives the prompt
2. **Gemini Planner** (LLM call) selects relevant tools by name (JSON response)
3. **Keyword matcher** (`CanHandle(prompt)`) also picks tools
4. Union of both selections (no duplicates)
5. Selected tools fetch live data from the DB:
   - `NotesWorkspaceTool` — recent notes + count
   - `TasksWorkspaceTool` — pending + overdue tasks
   - `ExpensesWorkspaceTool` — income/expense summary
   - `HealthWorkspaceTool` — active medicines + upcoming appointments
   - `DocumentsWorkspaceTool` — document count + storage
6. Context block is built with `[ToolName]` headers and compact data
7. System prompt: user name + UTC datetime + context block + last 20 messages + response style
8. Gemini generates the response
9. Metadata (`usedWorkspaceData`, `workspaceToolsUsed`) is embedded as an HTML comment in the message content and persisted

Tool failures are silently ignored — partial context is always better than no response.

---

## File storage

`IFileStorageService` → `SupabaseFileStorageService`

- Files saved to Supabase private bucket: `onenest-documents`
- Path pattern: `{userId}/{guid}{ext}`
- Upload uses `x-upsert: true` header
- Download returns a stream via presigned URL or direct API fetch
- Used by: `DocumentService` and `MedicalReportService`

Nothing is written to the container filesystem (which is ephemeral on Render).

---

## Exception → HTTP mapping

| Exception type | Scenario | HTTP code |
|---|---|---|
| `AuthException` | Duplicate email on register | 409 |
| `AuthException` | Bad password on login | 401 |
| `AuthException` | Bad password on change/delete | 400 |
| `InvalidOperationException` | Business rule failure | 400 |
| `InvalidOperationException` | AI rate limit text | 429 |
| null / not found | Resource not found | 404 or 204 |

---

## Things to improve (planned)

- Add centralized exception middleware for a consistent error envelope (`{ code, message, details }`)
- Add structured observability: correlation IDs, request logging, metrics
- Add circuit breaker + timeout policies for Gemini calls
- Add resilience policies for Supabase Storage calls
- Add background processing infrastructure for bulk exports/imports
