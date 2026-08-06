<div align="center">

# 🏡 OneNest AI

### Your Intelligent Personal Life Management Platform

A modern, AI-powered personal productivity platform that unifies **Notes**, **Tasks**, **Expenses**, **Documents**, **Health Tracking**, and a **Workspace-Aware AI Assistant** into one seamless application.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?logo=postgresql&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-success)
![Status](https://img.shields.io/badge/Status-Active-success)

</div>

---

# 📸 Application Preview

## Dashboard

![Dashboard](assets/screenshots/dashboard.png)

---

## AI Assistant

![AI Assistant](assets/screenshots/ai-assistant.png)

---

## Settings

![Settings](assets/screenshots/settings.png)

---

# ✨ Overview

OneNest AI is a **full-stack SaaS-inspired personal management platform** designed to centralize everyday productivity.

Instead of using multiple applications for notes, expenses, health records, appointments, documents, and AI assistance, OneNest provides everything inside one secure workspace.

The application combines a modern Angular frontend, ASP.NET Core backend, PostgreSQL database, and AI-powered contextual assistance to deliver an intelligent personal workspace.

---

# 🚀 Current Features

## 🔐 Authentication

- JWT Authentication
- User Registration
- Login
- Change Password
- Delete Account
- Protected Routes

---

## 📝 Notes

- Create Notes
- Edit Notes
- Delete Notes
- Search Notes

---

## ✅ Tasks

- Task Management
- Due Dates
- Completion Tracking
- Filtering

---

## 💰 Expenses

- Expense Tracking
- Monthly Summary
- Category Analytics
- Dashboard Charts

---

## 📂 Documents

- Upload Documents
- Download Documents
- Preview Files
- Bulk Download (ZIP)
- Delete All
- Storage Summary

---

## 🏥 Health Workspace

- Medicines
- Appointments
- Medical Records
- Medical Reports
- Dashboard Integration

---

## 🤖 AI Assistant

- Persistent Conversations
- Conversation History
- Conversation Archive
- Workspace-aware Responses
- Context Injection
- Tool Orchestration
- AI Metadata
- Smart Conversation Management

---

## ⚙ Settings

- Account Settings
- AI Preferences
- Documents
- Health Preferences
- Privacy & Security
- About

---

# 🧠 AI Architecture

OneNest AI doesn't simply send prompts to an LLM.

Instead, it intelligently determines whether the user's question requires workspace data before generating a response.

```text
                    User

                      │

                      ▼

             AI Conversation Service

                      │

                      ▼

          Workspace Tool Orchestrator

       ┌────────┬─────────┬─────────┬──────────┬─────────┐
       │ Notes  │ Tasks   │Expenses │Documents │ Health  │
       └────────┴─────────┴─────────┴──────────┴─────────┘

                      │

             Workspace Context

                      │

                      ▼

               AI Model (Gemini)

                      │

                      ▼

             Intelligent Response

                      │

                      ▼

      Conversation Stored + Metadata
```

---

# 🏗 Technology Stack

## Frontend

- Angular
- TypeScript
- RxJS
- Angular Signals
- Reactive Forms
- Chart.js

---

## Backend

- ASP.NET Core Web API
- .NET 10
- Entity Framework Core
- Repository Pattern
- Clean Architecture
- JWT Authentication

---

## Database

- PostgreSQL

---

## AI

- Gemini API
- Workspace Tool Orchestrator
- Persistent Conversation Memory
- AI Context Injection

---

# 🏛 Project Architecture

```
OneNest-AI
│
├── backend
│   ├── OneNest.API
│   ├── OneNest.Application
│   ├── OneNest.Domain
│   └── OneNest.Infrastructure
│
├── frontend
│   └── OneNest.Web
│
├── docs
│
├── database
│
├── scripts
│
└── assets
```

---

# 📚 Documentation

Detailed project documentation is available under the **docs** folder.

| Document | Description |
|-----------|-------------|
| API.md | REST API documentation |
| Database.md | Database schema |
| Infrastructure.md | Backend architecture |
| UI.md | Frontend architecture |
| Roadmap.md | Product roadmap |
| Vision.md | Long-term vision |

---

# ⚡ Getting Started

## Prerequisites

- .NET 10 SDK
- Node.js
- npm
- PostgreSQL

---

## Clone Repository

```bash
git clone https://github.com/Kanha412/OneNest-AI.git

cd OneNest-AI
```

---

## Backend

```bash
cd backend/OneNest.API

dotnet restore

dotnet ef database update \
--project ../OneNest.Infrastructure \
--startup-project .

dotnet run
```

---

## Frontend

```bash
cd frontend/OneNest.Web

npm install

npm start
```

---

# 🌐 Local URLs

Frontend

```
http://localhost:4200
```

Backend

```
https://localhost:5001
```

Swagger

```
https://localhost:5001/swagger
```

---

# 🛣 Roadmap

## ✅ Phase 1

Authentication

---

## ✅ Phase 2

Workspace Modules

- Notes
- Tasks
- Expenses
- Documents
- Health

---

## ✅ Phase 3

Workspace-aware AI

- Persistent Conversations
- Conversation Archive
- Workspace Context
- Tool Orchestration

---

## 🚧 Phase 4 (Current)

- AI Tool Planner
- Smarter Context Selection
- Better Prompt Orchestration

---

## 🔜 Upcoming

- Smart AI Memory
- AI Recommendations
- Dashboard Insights
- Semantic Search
- AI Document Understanding
- Voice Assistant
- Mobile Responsive Improvements

---

# 🤝 Contributing

Contributions are welcome.

Feel free to:

- Open Issues
- Suggest Features
- Submit Pull Requests

---

# 📄 License

This project is licensed under the MIT License.

---

# 👨‍💻 Developer

**Kanha Gupta**

Custom Software Engineering Analyst @ Accenture

.NET Full Stack Developer

GitHub

https://github.com/Kanha412

LinkedIn

https://linkedin.com/in/kanhagupta412

---

<div align="center">

### ⭐ If you like this project, consider giving it a Star!

</div>