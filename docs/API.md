# OneNest AI — API Reference

All endpoints live under the base URL `/api`. Every protected endpoint requires an `Authorization: Bearer <token>` header.

For the full interactive docs when running locally, open Swagger at `https://localhost:5001/swagger`.

---

## Authentication

When you register or log in, you get back an `AuthResponse` with a JWT token. Store it in localStorage (`onenest.token`) and send it with every request using the `Authorization: Bearer` header. Tokens expire after 7 days.

---

## HTTP Status Codes

| Code | What it means |
|---|---|
| `200` | Success |
| `201` | Created (used by some endpoints) |
| `204` | Success but no body (deletes, toggles) |
| `400` | Bad request — validation failed or business rule broken |
| `401` | Not logged in, or wrong password |
| `404` | Item not found (or doesn't belong to you) |
| `409` | Conflict — email already registered |
| `429` | AI rate limit hit (Gemini quota exceeded) |

---

## DTOs

### Auth

**RegisterRequest**

| Field | Type | Rules |
|---|---|---|
| `fullName` | string | required, 2–100 chars |
| `email` | string | required, valid email |
| `password` | string | required, 6–100 chars |

**LoginRequest**

| Field | Type | Rules |
|---|---|---|
| `email` | string | required |
| `password` | string | required |

**AuthResponse** (returned on register and login)

| Field | Type |
|---|---|
| `userId` | guid |
| `fullName` | string |
| `email` | string |
| `token` | string (JWT) |
| `expiresAt` | datetime (UTC) |

**ChangePasswordRequest** — `currentPassword`, `newPassword`, `confirmPassword` (must match)

**DeleteAccountRequest** — `password` (confirmation required)

---

### Notes

- **CreateNoteRequest / UpdateNoteRequest** — `title`, `content`
- **NoteResponse** — `id`, `title`, `content`, `createdAt`, `updatedAt`, `isPinned`, `isArchived`

### Tasks

- **CreateTaskRequest / UpdateTaskRequest** — `title` (max 100), `description` (max 1000), `dueDate?`, `priority` (1=Low, 2=Medium, 3=High)
- **TaskResponse** — above fields + `isCompleted`, `completedAt`

### Expenses

- **CreateExpenseRequest / UpdateExpenseRequest** — `title` (max 100), `amount` (>0), `category`, `transactionType` (1=Income, 2=Expense), `date`, `notes` (max 1000)
- **ExpenseResponse** — all request fields + `id`, `createdAt`, `updatedAt`
- **ExpenseSummaryResponse** — `totalIncome`, `totalExpense`, `currentBalance`, `thisMonthIncome`, `thisMonthExpense`, `topExpenseCategory`, `recentTransactions`, `categoryBreakdown`, `monthlyBreakdown`

### Documents

- **CreateDocumentRequest / UpdateDocumentRequest** — `title` (max 150), `category` (1=Resume, 2=Certificate, 3=Identity, 4=Medical, 5=Invoice, 6=Finance, 7=Education, 8=Personal, 9=Other), `description` (max 1000)
- **DocumentResponse** — `id`, `title`, `originalFileName`, `contentType`, `fileSize`, `category`, `description`, `createdAt`, `updatedAt`
- **DocumentSummaryResponse** — `totalDocuments`, `todayUploads`, `storageUsed`, `recentDocuments`, `categoryDistribution`

### Health — Medicines

- **CreateMedicineRequest / UpdateMedicineRequest** — `name` (max 150), `dosage`, `frequency`, `morning`/`afternoon`/`night` (bool), `startDate`, `endDate?`, `instructions`, `foodTiming` (1=BeforeFood, 2=AfterFood, 3=Anytime), `isActive`
- **MedicineResponse** — above + `id`, `createdAt`, `updatedAt`

### Health — Appointments

- **CreateAppointmentRequest / UpdateAppointmentRequest** — `doctorName` (max 150), `hospital`, `specialty`, `date`, `time`, `notes`, `status` (1=Scheduled, 2=Completed, 3=Cancelled)
- **AppointmentResponse** — above + `id`, `createdAt`, `updatedAt`

### Health — Medical Record

- **SaveMedicalRecordRequest** — `bloodGroup`, `heightCm` (0–300), `weightKg` (0–500), `allergies`, `existingConditions`, `emergencyContactName`, `emergencyContactPhone`
- **MedicalRecordResponse** — above + `id`, `createdAt`, `updatedAt`

### Health — Medical Reports

- **CreateMedicalReportRequest / UpdateMedicalReportRequest** — `title` (max 150), `category` (1=LabReport, 2=Prescription, 3=Scan, 4=DischargeSummary, 5=Other), `doctorName`, `hospital`, `reportDate`, `description`
- **MedicalReportResponse** — above + file metadata (`originalFileName`, `contentType`, `fileSize`)

### Health Summary

- **HealthSummaryResponse** — KPI counts (`activeMedicines`, `todayMedicines`, `expiringSoonMedicines`, `upcomingAppointments`, `pastAppointments`, `totalReports`), `lastRecordUpdate`, `medicineDistribution`, `appointmentTimeline`, `recentReports`, `upcomingAppointmentsList`

### AI Conversations

**Requests**
- `CreateConversationRequest` — `title?` (optional)
- `RenameConversationRequest` — `title` (required, max 120)
- `SendMessageRequest` — `message` (required, max 8000)

**ConversationListResponse** — `id`, `title`, `lastMessageAt`, `createdAt`, `isArchived`

**ConversationResponse** — above + `messages: ChatMessageResponse[]`

**ChatMessageResponse** — `id`, `role` (user/assistant), `content`, `timestamp`, `isError`, `usedWorkspaceData`, `responseMode`, `workspaceToolsUsed`

**ChatResponse** — `response`, `model`, `timestamp`, `usedWorkspaceData`, `responseMode`, `workspaceToolsUsed`

### Semantic Search

**SemanticSearchRequest**

| Field | Type | Notes |
|---|---|---|
| `query` | string | Plain English question |
| `topK` | int | How many results (1–20, default 5) |
| `sourceType` | int? | Filter: 0=Note, 1=Document, omit for all |

**SemanticSearchResult** — `sourceId`, `sourceType`, `title`, `score` (0–1, higher = more relevant)

**BackfillResult** — `notesIndexed`, `documentsIndexed`, `skipped`, `errors`

### Settings

**SettingsResponse** — nested sections: `account`, `aiPreferences`, `notifications`, `documents`, `health`, `appearance`, `privacy`, `about`

**UpdateSettingsRequest** — `displayName`, AI settings (`enableWorkspaceContext`, `contextDepth`, `defaultConversationMode`, `responseStyle`), health units, appearance theme

---

## Endpoints

### Public

| Method | Route | Description |
|---|---|---|
| GET | `/api/health` | Health check — always returns 200 |

**Response:**
```json
{ "status": "Healthy", "application": "OneNest AI", "version": "1.0.0", "timestamp": "..." }
```

---

### Auth

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register — returns `AuthResponse` (200) or 409 if email taken |
| POST | `/api/auth/login` | No | Login — returns `AuthResponse` (200) or 401 |
| POST | `/api/auth/change-password` | Yes | Change password — 200 or 400 |
| POST | `/api/auth/delete-account` | Yes | Delete account + all data — 200 or 400 |

---

### Notes

| Method | Route | Returns |
|---|---|---|
| GET | `/api/notes` | `NoteResponse[]` |
| POST | `/api/notes` | `NoteResponse` |
| PUT | `/api/notes/{id}` | `NoteResponse` (404 if not yours) |
| PATCH | `/api/notes/{id}/pin` | 204 |
| DELETE | `/api/notes/{id}` | 204 |

---

### Tasks

| Method | Route | Returns |
|---|---|---|
| GET | `/api/tasks` | `TaskResponse[]` |
| POST | `/api/tasks` | `TaskResponse` |
| PUT | `/api/tasks/{id}` | `TaskResponse` |
| PATCH | `/api/tasks/{id}/complete` | 204 |
| DELETE | `/api/tasks/{id}` | 204 |

---

### Expenses

| Method | Route | Returns |
|---|---|---|
| GET | `/api/expenses` | `ExpenseResponse[]` |
| GET | `/api/expenses/summary` | `ExpenseSummaryResponse` |
| POST | `/api/expenses` | `ExpenseResponse` |
| PUT | `/api/expenses/{id}` | `ExpenseResponse` |
| DELETE | `/api/expenses/{id}` | 204 |

---

### Documents

| Method | Route | Returns |
|---|---|---|
| GET | `/api/documents?search=&category=` | `DocumentResponse[]` |
| GET | `/api/documents/recent?count=5` | `DocumentResponse[]` |
| GET | `/api/documents/summary` | `DocumentSummaryResponse` |
| GET | `/api/documents/{id}` | `DocumentResponse` |
| GET | `/api/documents/{id}/download` | File stream |
| GET | `/api/documents/{id}/preview` | Inline file stream |
| GET | `/api/documents/download-all` | ZIP of all files |
| POST | `/api/documents` (multipart/form-data) | `DocumentResponse` |
| PUT | `/api/documents/{id}` | `DocumentResponse` |
| DELETE | `/api/documents/{id}` | 204 |
| DELETE | `/api/documents/all` | `{ deletedCount: N }` |

---

### Health

| Method | Route | Returns |
|---|---|---|
| GET | `/api/health-hub/summary` | `HealthSummaryResponse` |
| GET | `/api/medicines?search=&isActive=` | `MedicineResponse[]` |
| GET | `/api/medicines/{id}` | `MedicineResponse` |
| POST | `/api/medicines` | `MedicineResponse` (201) |
| PUT | `/api/medicines/{id}` | `MedicineResponse` |
| DELETE | `/api/medicines/{id}` | 204 |
| GET | `/api/appointments?search=&status=` | `AppointmentResponse[]` |
| GET | `/api/appointments/{id}` | `AppointmentResponse` |
| POST | `/api/appointments` | `AppointmentResponse` (201) |
| PUT | `/api/appointments/{id}` | `AppointmentResponse` |
| DELETE | `/api/appointments/{id}` | 204 |
| GET | `/api/medicalrecords` | `MedicalRecordResponse` (204 if no record yet) |
| PUT | `/api/medicalrecords` | `MedicalRecordResponse` |
| GET | `/api/medicalreports?search=&category=` | `MedicalReportResponse[]` |
| GET | `/api/medicalreports/{id}` | `MedicalReportResponse` |
| GET | `/api/medicalreports/{id}/download` | File stream |
| GET | `/api/medicalreports/{id}/preview` | Inline file stream |
| POST | `/api/medicalreports` (multipart/form-data) | `MedicalReportResponse` |
| PUT | `/api/medicalreports/{id}` | `MedicalReportResponse` |
| DELETE | `/api/medicalreports/{id}` | 204 |

---

### AI Conversations

| Method | Route | Returns |
|---|---|---|
| GET | `/api/ai/conversations?includeArchived=&search=` | `ConversationListResponse[]` |
| GET | `/api/ai/conversations/{id}` | `ConversationResponse` |
| POST | `/api/ai/conversations` | `ConversationResponse` |
| PUT | `/api/ai/conversations/{id}/rename` | `ConversationResponse` |
| DELETE | `/api/ai/conversations/{id}` | 204 (soft delete) |
| POST | `/api/ai/conversations/{id}/archive` | 204 |
| POST | `/api/ai/conversations/{id}/unarchive` | 204 |
| POST | `/api/ai/conversations/{id}/messages` | `ChatResponse` (429 if Gemini rate limited) |
| POST | `/api/ai/chat` | `ChatResponse` (quick chat, creates a conversation internally) |

---

### Semantic Search

| Method | Route | Returns |
|---|---|---|
| POST | `/api/semantic-search` | `SemanticSearchResult[]` (400 if query empty) |
| POST | `/api/semantic-search/backfill` | `BackfillResult` (re-indexes all user content) |

Example search request:
```json
{ "query": "notes about monthly savings", "topK": 5 }
```

Example response:
```json
[
  { "sourceId": "...", "sourceType": 0, "title": "Monthly Budget", "score": 0.78 },
  { "sourceId": "...", "sourceType": 1, "title": "Finance Report.pdf", "score": 0.65 }
]
```

Search results are always scoped to the logged-in user — you'll never see someone else's content.

---

### Settings

| Method | Route | Returns |
|---|---|---|
| GET | `/api/settings` | `SettingsResponse` |
| PUT | `/api/settings` | `SettingsResponse` |

---

## Request examples

**Register:**
```http
POST /api/auth/register
Content-Type: application/json

{ "fullName": "Kanha Gupta", "email": "kanha@example.com", "password": "Pass@123" }
```

**Send AI message:**
```http
POST /api/ai/conversations/{id}/messages
Authorization: Bearer <jwt>
Content-Type: application/json

{ "message": "What tasks do I have due this week?" }
```

**Upload document:**
```http
POST /api/documents
Authorization: Bearer <jwt>
Content-Type: multipart/form-data

file=<binary>, title="Q1 Report", category=6, description="Finance docs"
```

---

## Things to improve (planned)

- Standardize all creation endpoints to return `201 Created` (currently mixed `200`/`201`)
- Add a consistent error response shape (`{ code, message, details }`) instead of plain strings
- Add pagination on list endpoints (notes, tasks, documents, conversations)
- Add API versioning (`/api/v1/`) before making any breaking changes
- Add proper rate-limit headers on 429 responses
