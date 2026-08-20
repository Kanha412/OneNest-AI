# OneNest AI — Roadmap

What's been built and what's coming next.

---

## What's done

### Phase 1 — Foundation
- Angular app shell with JWT auth guard
- ASP.NET Core API with JWT authentication
- Notes and Tasks modules
- Per-user data isolation (`UserId` pattern on all tables)

### Phase 2 — Domain expansion
- Expenses module with category breakdown and monthly summaries
- Documents module — upload, preview, download, bulk ZIP
- Health Hub — medicines, appointments, medical record, medical reports
- Health summary endpoint for dashboard aggregation

### Phase 3 — AI Assistant
- Persistent conversation model (`AIConversations`, `AIMessages`)
- Workspace tools — AI reads your real notes, tasks, expenses, health data, documents
- Gemini integration (`gemini-2.5-flash`)
- User settings stored in DB (`UserSettings`)
- Last login tracking

### Phase 4 — UX polish
- AI preferences simplified to context depth + response style
- Storage section aggregates documents + health reports under 150 MB
- Security tab: change password modal + delete account modal
- Dashboard empty states and card alignment
- Privacy policy and terms pages

### Phase 5 — AI Document Intelligence
- Text extraction on upload (PDF, DOCX, TXT)
- `ExtractedText` column added to Documents
- AI assistant can reference document content via workspace tool
- On-demand AI document summarization endpoint

### Phase 6 — Contact Us & Admin
- Contact form — users can send support/feedback messages
- Admin dashboard — view all users, manage roles, reply to contact messages
- `[Authorize(Roles="Admin")]` protection on admin endpoints

### Phase 7 — Semantic Search & RAG (latest)
- `pgvector` extension enabled on PostgreSQL
- `EmbeddingRecords` table with `vector(384)` column
- Local ONNX embedding model (`all-MiniLM-L6-v2` INT8, 384-dim) — runs in-process, zero cost
- `TextChunker` — 1200-char chunks with 240-char overlap, sentence-aware
- Auto-indexing on every note/document create/update (background, never blocks user)
- Semantic search endpoint — cosine similarity search scoped to the logged-in user
- RAG pipeline — 9-step flow: embed query → vector search → filter → fetch text → build context → Gemini → return answer with source citations
- Backfill endpoint — re-index all existing notes and documents
- Supabase Storage for all file persistence (replaces local disk)
- Docker multi-stage build with SHA256-verified ONNX model
- Production-ready: ForwardedHeaders, env-driven CORS, non-root Docker user, health check

---

## What's planned

### Next — Platform maturity
- Consistent API error response format (`{ code, message, details }`)
- Pagination on list endpoints (notes, tasks, documents, conversations)
- Stronger DB referential integrity (explicit FK constraints)
- Centralized exception middleware
- Structured observability (correlation IDs, request logging)

### Quality & CI/CD
- Unit and integration tests for backend services
- Component and E2E tests for the Angular frontend
- CI pipeline for build / test / lint on every push
- Release versioning and changelog

### UX & accessibility
- Mobile-first navigation (collapsible sidebar)
- Accessibility pass — keyboard flows, ARIA labels, contrast
- Performance budgets and lazy-loaded feature modules
- Richer error states and retry UX

### Long-term ideas
- Smart AI memory — remembers user preferences and patterns across conversations
- AI-powered dashboard insights — surface patterns in expenses, health, and tasks
- Voice assistant integration
- Offline-capable PWA
- API versioning (`/api/v1/`) for stable integrations

---

## Milestone summary

| Feature | Status |
|---|---|
| Auth + user isolation | ✅ Done |
| Notes, Tasks, Expenses | ✅ Done |
| Documents with storage | ✅ Done |
| Health Hub | ✅ Done |
| AI chat with workspace context | ✅ Done |
| Persistent conversations | ✅ Done |
| User settings | ✅ Done |
| AI document text extraction | ✅ Done |
| Contact form + admin panel | ✅ Done |
| Semantic search (pgvector + ONNX) | ✅ Done |
| RAG pipeline | ✅ Done |
| Production Docker + Render deploy | ✅ Done |
| Pagination on list endpoints | 🔜 Planned |
| Automated tests | 🔜 Planned |
| Mobile navigation | 🔜 Planned |
