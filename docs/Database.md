# OneNest AI — Database

PostgreSQL 17 hosted on Supabase. ORM is Entity Framework Core. Vector search uses the `pgvector` extension.

---

## Entities (tables)

| Entity | What it stores | Owned by user? |
|---|---|---|
| `User` | Account info — name, email, password hash, role, last login | — (root record) |
| `UserSettings` | Per-user preferences — theme, units, AI settings, notifications | Yes |
| `Note` | Personal notes | Yes |
| `TaskItem` | Tasks with priority, due date, completion | Yes |
| `Expense` | Income and expense entries | Yes |
| `Document` | Uploaded file metadata + extracted text | Yes |
| `Medicine` | Medicines with schedule and food timing | Yes |
| `Appointment` | Doctor appointments with status | Yes |
| `MedicalRecord` | Single health profile per user (blood group, height, weight, etc.) | Yes |
| `MedicalReport` | Uploaded health report metadata (lab, prescription, scan, etc.) | Yes |
| `AIConversation` | Chat conversation headers | Yes |
| `AIMessage` | Individual messages inside a conversation | Via conversation |
| `ContactMessage` | Support/feedback messages sent by the user | Yes |
| `EmbeddingRecord` | 384-dim vector for one text chunk of a note or document | Yes |

Every user-owned table has a `UserId` column. All queries in the backend are scoped to the logged-in user's ID — no cross-user data leaks.

---

## Key fields per entity

### User
- `Id` (uuid, PK), `FullName`, `Email` (unique), `PasswordHash`, `Role` (User/Admin), `CreatedAt`, `UpdatedAt`, `LastLoginAt`

### Note / TaskItem / Expense
Standard pattern: `Id`, `UserId`, `CreatedAt`, `UpdatedAt` + domain-specific fields.
- `TaskItem.Priority` — 1=Low, 2=Medium, 3=High
- `Expense.Amount` — decimal(18,2); `TransactionType` — 1=Income, 2=Expense

### Document / MedicalReport (file entities)
Both store file metadata in the DB and the actual file in Supabase Storage:
- `OriginalFileName`, `StoredFileName` (path in bucket), `ContentType`, `FileSize`
- `Document` also has `ExtractedText` and `AISummary` columns

### AIConversation
- `Title`, `IsArchived`, `IsDeleted` (soft delete), `LastMessageAt`

### AIMessage
- `ConversationId` (FK → AIConversations, **cascade delete**), `Role` (0=User, 1=Assistant), `Content`, `IsError`, `TokenCount`, `UsedWorkspaceData`, `WorkspaceToolsUsed`

### EmbeddingRecord
- `UserId`, `SourceType` (0=Note, 1=Document), `SourceId`, `ChunkIndex`, `Embedding vector(384)`, `Title`
- Unique constraint on `(UserId, SourceType, SourceId, ChunkIndex)` — safe to re-index
- **Not managed by EF Core** — uses raw ADO.NET because EF's Npgsql provider doesn't natively map `vector(N)` without the optional pgvector package
- Cosine similarity query: `"Embedding" <=> '{vector}'::vector` (lower = more similar)

### ContactMessage
- `Subject`, `Category` (0=General, 1=Support, 2=Bug, 3=Feedback), `Status` (0=New, 1=Read, 2=Resolved), `AdminReply`

---

## Relationships

```
User ──< Note
User ──< TaskItem
User ──< Expense
User ──< Document
User ──< Medicine
User ──< Appointment
User ─── MedicalRecord (1:1, upsert)
User ──< MedicalReport
User ──< AIConversation ──< AIMessage (cascade delete)
User ─── UserSettings (1:1)
User ──< ContactMessage
User ──< EmbeddingRecord
```

The only explicit EF cascade is `AIConversation → AIMessage`. All other ownership is enforced in service/repository code via `UserId`.

---

## Indexes

| Table | Column(s) | Unique? |
|---|---|---|
| `Users` | `Email` | Yes |
| `UserSettings` | `UserId` | Yes |
| `AIConversations` | `UserId`, `CreatedAt`, `UpdatedAt`, `LastMessageAt` | No |
| `AIMessages` | `ConversationId`, `CreatedAt` | No |
| `EmbeddingRecords` | `(UserId, SourceType, SourceId, ChunkIndex)` | Yes |
| `EmbeddingRecords` | `(UserId, SourceType, SourceId)` | No (fast delete per source) |

---

## Migration timeline

| Migration | When | What changed |
|---|---|---|
| `InitialCreate` | 2026-08-03 | Notes, Tasks tables |
| `AddAuthAndUserOwnership` | 2026-08-03 | Users table, UserId FK on Notes+Tasks, email unique index |
| `AddExpenses` | 2026-08-03 | Expenses table |
| `AddDocuments` | 2026-08-03 | Documents table |
| `AddHealthHub` | 2026-08-03 | Medicines, Appointments, MedicalRecords, MedicalReports |
| `AddAIConversationHistory` | 2026-08-04 | AIConversations, AIMessages, cascade delete, indexes |
| `AddUserSettings` | 2026-08-04 | UserSettings table, defaults (AutoDeleteTrashDays=30) |
| `AddUserLastLoginAt` | 2026-08-05 | LastLoginAt column on Users |
| `AddContactAndUserRole` | 2026-08-17 | ContactMessages table, Role column on Users |
| `AddDocumentTextExtraction` | 2026-08-17 | ExtractedText, AISummary on Documents |
| `AddSemanticEmbeddings` | 2026-08-17 | pgvector extension, EmbeddingRecords with `vector(384)`, indexes |

---

## Running migrations

```bash
cd backend
dotnet ef database update \
  --project OneNest.Infrastructure \
  --startup-project OneNest.API
```

For production (Supabase), pass the connection string explicitly:
```bash
dotnet ef database update \
  --project OneNest.Infrastructure \
  --startup-project OneNest.API \
  --connection "Host=...pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=...;SSL Mode=Require;Trust Server Certificate=true"
```

This is safe to run multiple times — only pending migrations are applied.

---

## Things to improve (planned)

- Add explicit FK constraints from user-owned tables to `Users` (currently enforced in code, not DB)
- Add composite indexes for common filter patterns (`UserId + CreatedAt` on high-volume tables)
- Add soft-delete columns where history matters (notes, documents)
- Add a dev seed script for local onboarding
