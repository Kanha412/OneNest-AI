# OneNest AI - Frontend

The frontend application for **OneNest AI**, built with **Angular 22**.

OneNest AI is an AI-powered personal workspace that combines productivity, document management, health tracking, expense management, and contextual AI assistance into a single platform.

---

## Tech Stack

- Angular 22
- TypeScript
- HTML5
- CSS3
- RxJS
- Angular Router
- HttpClient
- Chart.js
- JWT Authentication

---

## Features

### Authentication

- User Registration
- Secure Login
- JWT Authentication
- Protected Routes

### Dashboard

- Overview Cards
- Expense Charts
- Task Statistics
- Health Summary
- Recent Activity

### Notes

- Create Notes
- Edit Notes
- Delete Notes
- Search Notes

### Tasks

- Task Management
- Status Tracking
- Priority Levels
- Due Dates

### Expenses

- Expense Tracking
- Category Management
- Monthly Analytics
- Charts & Reports

### Documents

- File Upload (AI text extraction on upload for PDF/DOCX/TXT)
- Document Library
- Search Documents
- Preview & Download
- Bulk Download (ZIP)

### Health

- Appointments
- Reports
- Medicines
- Health Dashboard

### AI Assistant

- Persistent Conversations
- Workspace-aware Responses
- Conversation History
- Archive Conversations
- AI Tool Selection
- Context-aware Answers

### Settings

- Account Settings
- AI Preferences
- Privacy & Security
- Documents
- Health
- About

---

## Project Structure

```
src/
│
├── app/
│   ├── core/
│   ├── shared/
│   ├── features/
│   │
│   ├── dashboard/
│   ├── notes/
│   ├── tasks/
│   ├── expenses/
│   ├── documents/
│   ├── health/
│   ├── ai-assistant/
│   └── settings/
│
├── assets/
└── environments/
```

---

## Running the Project

Install dependencies

```bash
npm install
```

Start development server

```bash
ng serve
```

Application

```
http://localhost:4200
```

---

## Production Build

```bash
ng build
```

The production build is generated inside:

```
dist/
```

---

## Development Commands

Run Development Server

```bash
ng serve
```

Build

```bash
ng build
```

Run Tests

```bash
ng test
```

Lint

```bash
ng lint
```

---

## Backend

This frontend communicates with the ASP.NET Core backend located at:

```
/backend
```

---

## Documentation

Additional documentation is available in the project root:

- docs/API.md
- docs/Database.md
- docs/Infrastructure.md
- docs/UI.md
- docs/Vision.md
- docs/Roadmap.md

---

## Current Version

**Version:** 1.0.0

Current major features:

- Workspace-aware AI
- Persistent Conversations
- JWT Authentication
- Health Management
- Expense Tracking
- Notes
- Tasks
- Documents (with AI text extraction)
- Modern Settings
- **Semantic Search backend** (pgvector, 768-dim Gemini embeddings)

---

## Future Enhancements

- Semantic Search UI surface
- Dark Mode
- AI Memory Improvements
- Smart Notifications
- Voice Assistant
- Mobile Responsive Enhancements
- Automated tests + CI/CD pipeline

---

## Developed By

**Kanha Gupta**

OneNest AI © 2026