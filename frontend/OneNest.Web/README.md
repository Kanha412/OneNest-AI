# OneNest AI — Frontend

The Angular frontend for OneNest AI.

Built with **Angular 22** using standalone components, signals, and functional guards/interceptors.

---

## Screenshots

> Screenshots are in `assets/screenshots/` at the project root. Add your own by saving images with the filenames below.

| Screen | Filename |
|---|---|
| Login | `01-login.png` |
| Register | `02-register.png` |
| Dashboard | `03-dashboard.png` |
| Notes | `04-notes.png` |
| Tasks | `05-tasks.png` |
| Expenses | `06-expenses.png` |
| Documents | `07-documents.png` |
| Health Hub | `08-health.png` |
| AI Assistant | `09-ai-assistant.png` |
| Semantic Search | `10-semantic-search.png` |
| Settings | `11-settings.png` |
| Admin Panel | `12-admin.png` |

---

## Tech used

- Angular 22 (standalone components)
- TypeScript
- Angular Signals (`signal()`, `computed()`, `effect()`, `toSignal()`)
- Angular Router (with functional guards)
- HttpClient (with functional interceptors)
- Reactive Forms
- Chart.js (dashboard charts)
- JWT for auth (stored in localStorage)

---

## Pages / Features

**Auth**
- `/login` — email + password login
- `/register` — full name + email + password

**Dashboard** (`/dashboard`)
- Overview cards: total notes, tasks pending, total expenses, active medicines
- Expense chart (monthly bar chart by category)
- Upcoming appointments and recent notes

**Notes** (`/notes`)
- Create, edit, delete notes
- Pin and archive
- Search across titles and content
- Notes are auto-indexed in the background for semantic search

**Tasks** (`/tasks`)
- Create tasks with title, description, due date, priority
- Mark complete / incomplete
- Filter by status, sort by date or priority
- Pagination

**Expenses** (`/expenses`)
- Log income or expense with category (Food, Travel, Bills, etc.)
- Filter by category, type, date range
- Income and expense totals shown

**Documents** (`/documents`)
- Upload files (PDF, Word, Excel, images, and more)
- Text extraction happens automatically on upload (for PDF, DOCX, TXT)
- Preview, download individual files
- Bulk download as ZIP
- Delete files
- Storage usage shown (max 150 MB per user)

**Health Hub** (`/health`) — 4 tabs:
- *Medicines* — add medicines with dosage, timing (before/after/anytime food), mark active/inactive
- *Appointments* — schedule appointments with doctor name, date, time, status
- *Medical Records* — single record with height, weight, blood group, allergies; BMI auto-calculated
- *Reports* — upload health report files (lab reports, prescriptions, scans, etc.)

**AI Assistant** (`/ai-assistant`)
- Chat with Gemini; the AI can see your notes, tasks, expenses, health data, and documents
- Multiple conversations, each saved separately
- Archive or delete conversations
- Adjustable response style (short / balanced / detailed) in settings

**Semantic Search** (`/semantic-search` or via AI chat)
- Type a query in plain English and find notes/documents by meaning, not just keywords
- Results show title, type (note or document), and similarity score
- Filter by source type

**Contact** (`/contact`)
- Send a support/feedback message to admin
- View your own submitted tickets and their status

**Settings** (`/settings`)
- Account: update display name (email is read-only)
- Storage: see how much space you've used, download everything as a ZIP, clear all files
- Health: set preferred units (cm/ft for height, kg/lbs for weight)
- Privacy & Security: change password, delete account (with password confirmation)
- AI: set response depth preference
- About: app version and links

**Admin** (`/admin`) — only visible if your role is `Admin`
- *Messages tab*: view all contact messages, reply, update status (New / Read / Resolved)
- *Users tab*: see all users, toggle between User and Admin role (cannot change your own role)

---

## Routing and guards

Routes are defined in `app.routes.ts`.

- `authGuard` — checks if a valid (non-expired) JWT is in localStorage. If not, redirects to `/login`. This runs client-side without making an API call.
- `adminGuard` — checks if the user's role is `Admin`. Redirects non-admins to `/dashboard`.
- `authInterceptor` — automatically adds `Authorization: Bearer <token>` to every outgoing HTTP request.
- `unauthorizedInterceptor` — if a 401 response comes back, clears the stored token and redirects to `/login`.

---

## Project structure

```
src/
└── app/
    ├── core/
    │   ├── guards/          → authGuard, adminGuard
    │   ├── interceptors/    → authInterceptor, unauthorizedInterceptor
    │   ├── models/          → TypeScript interfaces
    │   └── services/        → HTTP services per feature
    │
    ├── shared/
    │   └── components/      → reusable UI pieces (toast, confirm dialog, etc.)
    │
    └── features/
        ├── auth/            → login, register
        ├── dashboard/
        ├── notes/
        ├── tasks/
        ├── expenses/
        ├── documents/
        ├── health/          → medicines, appointments, records, reports
        ├── ai-assistant/
        ├── semantic-search/
        ├── contact/
        ├── settings/
        ├── admin/
        └── legal/           → privacy policy, terms
```

---

## Running locally

```bash
# Install dependencies
npm install

# Start dev server
npm start
# or: ng serve
```

App opens at `http://localhost:4200`.

The API base URL defaults to `https://localhost:5001/api`. You can override it by setting `VITE_API_URL` (or the Angular environment variable) before building.

---

## Build for production

```bash
ng build
```

Output goes to `dist/OneNest.Web/browser/`. This is what gets deployed to Render as a static site.

---

## Other commands

```bash
ng test     # run unit tests
ng lint     # lint check
```

---

## Backend

The backend is in `/backend`. It's an ASP.NET Core .NET 10 Web API.

When running locally the API is at `https://localhost:5001` and Swagger is at `https://localhost:5001/swagger`.

---

OneNest AI — © 2026 Kanha Gupta
