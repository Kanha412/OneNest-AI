# OneNest AI — Vision

## The idea

Most people juggle 5–10 different apps for their personal life — one for notes, one for tasks, a spreadsheet for expenses, a folder for documents, another app for health records. Switching between all of them is tiring, and it means no single tool has the full picture.

OneNest brings all of that into one place, with an AI assistant that actually knows your context. When you ask "what do I need to do this week?" it can see your tasks, upcoming appointments, medicine schedule, and notes — not just answer in generic terms.

---

## What it solves today

- **Too many apps** — notes, tasks, expenses, documents, and health are all in one authenticated workspace
- **Dumb AI assistants** — the AI knows your actual data, not just general knowledge
- **Disconnected documents** — uploaded files are text-extracted and searchable by meaning (semantic search), not just filename
- **Lost context** — conversations are saved, so you can come back and continue from where you left off

---

## Who it's for

- Students and early-career professionals who want to organize their personal and work life in one place
- Anyone who wants an AI assistant that can actually answer questions about their own data
- People who want to keep health records, documents, and finances in one secure spot

---

## What makes it different

| What | Why it matters |
|---|---|
| Workspace-aware AI | The AI reads your real notes, tasks, expenses, health data, and documents to give grounded answers |
| Semantic search | Find notes and documents by what they mean, not just keywords |
| RAG pipeline | Ask the AI a question and it pulls in the most relevant chunks from your content as context |
| Local embeddings | Embedding model runs inside the Docker container — no external API call, zero cost, works offline |
| All modules in one | One login, one place, one AI assistant that sees everything |

---

## Guiding principles

- **User data comes first** — the AI uses your workspace context to improve relevance, not generic training data
- **Keep it simple** — only show what's needed; avoid clutter in the UI
- **Secure by default** — all data is user-scoped; no cross-user access; no data in client-side logs
- **Build incrementally** — add features without breaking existing ones; every phase works completely before the next begins

---

## Where it's going

Near term:
- Better API (pagination, consistent error formats, API versioning)
- Automated tests and CI/CD pipeline
- Mobile-friendly navigation
- Accessibility improvements

Longer term:
- AI memory — the assistant remembers your preferences and recurring patterns
- Smarter dashboard — surface insights like spending trends, medicine compliance, task completion patterns
- Voice input
- Progressive Web App (offline support)

---

## Success looks like

- You can plan your week (tasks + notes + appointments) in one place without switching apps
- Your documents and health records are organized and searchable
- The AI gives you answers that are actually useful because it knows your context
- A new developer can read the docs and understand how the whole thing works in under an hour
