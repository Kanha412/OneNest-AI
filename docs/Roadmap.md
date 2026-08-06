# OneNest AI Product Roadmap

This roadmap summarizes progress based on what is currently present in the repository and outlines practical next phases.

> Completed items below reflect implemented code in backend/frontend and migrations.

## Product Direction Snapshot

OneNest AI is currently a personal productivity + life-management platform that combines:

- productivity modules (notes, tasks)
- personal finance tracking
- document vault
- health records/reports management
- workspace-aware AI assistant

## Delivery Phases

## Phase 1 — Foundation (Completed)

- Core Angular app shell with authenticated route guard
- Core ASP.NET API with JWT authentication
- Initial note/task modules
- User ownership model introduced (`UserId` pattern)

## Phase 2 — Domain Expansion (Completed)

- Expenses module with summaries
- Documents module with upload/preview/download
- Health modules:
  - medicines
  - appointments
  - medical record
  - medical reports
- Health summary endpoint for dashboard aggregation

## Phase 3 — AI Assistant + Personalization (Completed)

- AI conversation history model (`AIConversations`, `AIMessages`)
- Workspace tools for contextual AI responses
- Gemini-based provider integration
- User settings model (`UserSettings`) + settings UI integration
- Last login tracking added to user profile

## Phase 4 — Product Polish & UX Hardening (Current Phase)

Current repository state shows active product refinement focused on UX consistency and production readiness:

- AI preferences simplified in UI to context depth + response style
- Storage experience aligned to combined documents + health reports usage
- Security tab streamlined to change password + account deletion flows
- Dashboard empty-state and card alignment improvements
- AI assistant empty/composer behavior improved for no-active-conversation cases
- Privacy policy and terms pages added and wired

## Milestone View

| Milestone | Status | Evidence in code |
|---|---|---|
| Auth + user ownership | Done | Auth controller + migration `AddAuthAndUserOwnership` |
| AI history persistence | Done | migration `AddAIConversationHistory`, AI services |
| Settings persistence | Done | migration `AddUserSettings`, settings controller/service |
| Last login tracking | Done | migration `AddUserLastLoginAt`, settings account payload |
| Unified storage controls | Done | settings + documents/health aggregation in frontend and backend endpoints |
| Legal pages integrated | Done | `privacy-policy` and `terms` routes/pages |

## Upcoming Phases (Planned Improvements)

## Phase 5 — API and Platform Maturity

- Standardize API error envelopes and response conventions
- Add pagination contracts for list-heavy endpoints
- Add stronger DB referential constraints and index tuning
- Add centralized exception middleware + structured observability

## Phase 6 — Quality and Release Engineering

- Add comprehensive automated tests (backend + frontend)
- Add CI pipeline for build/test/lint/doc checks
- Add environment templates and deployment documentation
- Add release versioning and changelog process

## Phase 7 — UX, Accessibility, and Mobile Readiness

- Responsive navigation for smaller screens
- Accessibility audit/remediation
- Performance budgets and lazy loading strategy

## Long-Term Vision

- Evolve from a productivity app into a reliable personal operating system that combines:
  - personal knowledge
  - health context
  - finance context
  - assistant-guided decision support
- Keep user data context central so AI responses stay practical and personalized.

## Roadmap Governance Notes

- This roadmap is implementation-informed (code-first), not marketing-first.
- Reprioritization should be driven by:
  1. reliability/security gaps
  2. user workflow friction
  3. deployment readiness
