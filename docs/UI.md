# OneNest AI — Frontend (Angular)

How the Angular frontend is structured and how each part works.

---

## Tech

- Angular 22, TypeScript — standalone components throughout
- Angular Signals (`signal()`, `computed()`, `effect()`, `toSignal()`) for reactive state
- Reactive Forms for form validation
- `HttpClient` with functional interceptors
- Chart.js for dashboard charts
- JWT tokens in `localStorage` for auth state

---

## App structure

```
src/app/
├── app.routes.ts          → all route definitions
├── app.config.ts          → bootstrap configuration
├── core/
│   ├── auth.guard.ts      → authGuard (JWT expiry check)
│   ├── admin.guard.ts     → adminGuard (role check)
│   ├── auth.interceptor.ts       → adds Bearer token to all requests
│   └── unauthorized.interceptor.ts → handles 401 → redirect to login
├── layout/
│   ├── layout.ts/html/css  → sidebar shell, user info, logout
├── features/
│   ├── auth/              → login, register
│   ├── dashboard/
│   ├── notes/
│   ├── tasks/
│   ├── expenses/
│   ├── documents/
│   ├── health/            → medicines, appointments, records, reports (tabbed)
│   ├── ai-assistant/
│   ├── semantic-search/
│   ├── contact/
│   ├── settings/
│   ├── admin/
│   └── legal/             → privacy policy, terms of service
├── services/              → HTTP service per feature area
├── models/                → TypeScript interfaces
└── shared/
    └── components/        → toast, confirm dialog, spinner, paginator, charts
```

---

## Routes

**Public (no auth required)**

| Route | Component |
|---|---|
| `/login` | LoginComponent |
| `/register` | RegisterComponent |
| `/privacy-policy` | PrivacyPolicyComponent |
| `/terms` | TermsComponent |

**Protected (requires valid JWT — checked by `authGuard`)**

| Route | Component | Extra guard |
|---|---|---|
| `/dashboard` | DashboardComponent | — |
| `/notes` | NotesComponent | — |
| `/tasks` | TasksComponent | — |
| `/expenses` | ExpensesComponent | — |
| `/documents` | DocumentsComponent | — |
| `/health` | HealthComponent | — |
| `/ai-assistant` | AiAssistantComponent | — |
| `/semantic-search` | SemanticSearchComponent | — |
| `/contact` | ContactComponent | — |
| `/settings` | SettingsComponent | — |
| `/admin` | AdminComponent | `adminGuard` |

Any other URL redirects to `/dashboard` if logged in, or `/login` if not.

---

## Guards and interceptors

**`authGuard`**
- Checks if a JWT is in `localStorage` and whether it's expired (decodes `exp` claim client-side)
- If missing or expired → redirects to `/login` immediately (no API call)
- Token stored as `onenest.token` in localStorage

**`adminGuard`**
- Checks if the user's role is `Admin` (reads from `onenest.user` in localStorage)
- Non-admins hitting `/admin` → redirect to `/dashboard`

**`authInterceptor`**
- Automatically adds `Authorization: Bearer <token>` to every outgoing HTTP request
- Functional interceptor (no class boilerplate)

**`unauthorizedInterceptor`**
- If a 401 response comes back from the API → clears localStorage → redirects to `/login`
- Handles session expiry without the user having to do anything

---

## Layout (shell)

All protected routes render inside the `Layout` component which provides:
- Left sidebar with navigation links
- User name + email display at the bottom
- Logout button with confirmation dialog
- Global `<app-toast>` for success/error notifications
- Global `<app-confirm>` for destructive action confirmations

---

## Feature pages

### Dashboard
- Summary cards: total notes, pending tasks, total expenses, active medicines
- Bar chart: monthly expenses by category
- Pie chart: expense category distribution
- Upcoming appointments list
- Recent notes list
- Empty states shown when no data exists yet

### Notes
- Create, edit, delete notes
- Pin / archive toggle
- Search by title or content
- Notes are auto-indexed in the background after every save (for semantic search)

### Tasks
- Create tasks: title, description, due date, priority (Low/Medium/High)
- Mark complete / incomplete (toggle)
- Filter by status, sort by date or priority
- Pagination
- Search

### Expenses
- Log income or expense: title, amount (₹), date, category, notes
- Categories: Food, Travel, Shopping, Bills, Entertainment, Health, Education, Salary, Investment, Other
- Filter by category, type (income/expense), date
- Income/expense badges color-coded

### Documents
- Upload files — PDF, Word, Excel, PowerPoint, text, CSV, images (max 25 MB per file, 150 MB total)
- Text is extracted automatically on upload (PDF, DOCX, TXT)
- Preview inline or download individual files
- Bulk download all as ZIP
- Delete individual or all files
- Storage usage bar shown
- Search and category filter

### Health Hub — 4 tabs
1. **Medicines** — add medicines with name, dosage, frequency, food timing, morning/afternoon/night schedule; filter active
2. **Appointments** — schedule with doctor name, hospital, specialty, date, time, status; upcoming badge count
3. **Medical Records** — single editable record: blood group, height, weight (units from settings), allergies, conditions, emergency contact; BMI auto-calculated
4. **Reports** — upload lab reports, prescriptions, scans, discharge summaries; preview and download

### AI Assistant
- Left panel: conversation list with search, filter (all/active/archived)
- Create new conversation, rename, archive, delete
- Right panel: message thread with user/assistant bubbles
- Workspace context indicators on assistant messages (which tools were used)
- Suggested prompts shown when starting a conversation
- Response depth controlled in Settings (short / balanced / detailed)

### Semantic Search
- Type a plain-English query and get ranked results from your notes and documents
- Filter by source type (all / notes only / documents only)
- Each result shows title, type, and similarity score

### Contact
- Send a message to admin: subject, category (General/Support/Bug/Feedback), message
- View your submitted tickets and their status (New/Read/Resolved)
- See admin reply when available

### Settings — sections
1. **Account** — update display name (email is read-only)
2. **AI Preferences** — context depth, response style
3. **Storage** — total used (documents + health reports combined), remaining space, download archive ZIP, clear all storage
4. **Health** — height unit (cm/ft), weight unit (kg/lbs)
5. **Privacy & Security** — change password modal, delete account modal (both need password confirmation)
6. **About** — app version, links to Privacy Policy and Terms

### Admin (role=Admin only)
- **Messages tab** — all contact messages, expandable cards, reply form, status update dropdown; new message count badge
- **Users tab** — all users, role column with toggle (cannot change own role)

---

## Service → API mapping

| Angular service | Backend routes |
|---|---|
| `AuthService` | `/api/auth/*` |
| `NotesService` | `/api/notes/*` |
| `TasksService` | `/api/tasks/*` |
| `ExpensesService` | `/api/expenses/*` |
| `DocumentsService` | `/api/documents/*` |
| `HealthHubService` | `/api/medicines`, `/api/appointments`, `/api/medicalrecords`, `/api/medicalreports`, `/api/health-hub/summary` |
| `AiService` | `/api/ai/*` |
| `SemanticSearchService` | `/api/semantic-search` |
| `ContactService` | `/api/contact/*` |
| `AdminService` | `/api/admin/*` |
| `SettingsService` | `/api/settings` |

---

## Design system

Styles are in `src/styles.css` with CSS custom properties:
- `--color-primary`, `--color-danger`, `--color-success`, etc.
- Border radius and shadow tokens
- Shared CSS classes: `.page`, `.card`, `.btn`, `.form-group`, `.actions`, `.field-error`

Each feature module has its own CSS file for layout specifics.

---

## Things to improve (planned)

- Mobile navigation (collapsible sidebar / bottom nav for small screens)
- Accessibility pass: keyboard focus, ARIA labels, color contrast
- Route-level code splitting for faster initial load
- Component unit tests + E2E tests for auth and document flows
- Richer error boundary UX for network failures
