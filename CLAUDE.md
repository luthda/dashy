# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Dashy — a log-watching dashboard. Backend exposes a REST API; frontend is a SPA that consumes it.

## Tech stack

| Layer      | Technology                                                                    |
| ---------- | ----------------------------------------------------------------------------- |
| Backend    | .NET 10, ASP.NET Core minimal API, EF Core 10, SQLite                         |
| Frontend   | React 19, TypeScript, Vite, Tailwind CSS, shadcn/ui, React Query v5, React Router v7 |
| Deployment | Docker Compose — API on :8080, frontend SPA on :3000 (Vite dev server: :5173) |

## Commands

**Backend**
```bash
cd backend
dotnet build                                                     # build
dotnet test                                                      # run all tests
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project Dashy.Api --urls http://localhost:8080               # dev server
```

**Frontend**
```bash
cd frontend
npm run dev              # Vite dev server on :5173
npx tsc --noEmit        # type-check without emitting
npx prettier --check "src/**/*.{ts,tsx,css}"   # format check
npm run build           # production build
npx vitest              # run unit tests
```

## Project layout

```
backend/
  Dashy.Api/
    Controllers/        # Minimal API endpoint files (*Endpoints.cs)
    Domain/             # Entities, models, encryption helpers
    Infrastructure/     # EF Core DbContext, log-source integrations
    Options/            # Strongly-typed configuration classes
    Program.cs          # App bootstrap; maps all endpoint groups
  Dashy.Api.Tests/      # xUnit tests

frontend/src/
  components/           # Feature-organized UI: layout/, logs/, searches/, sources/, tags/, shared/
  hooks/                # React Query data-fetching hooks (one file per domain)
  pages/                # Page-level components (LogsPage, SettingsSourcesPage)
  lib/                  # Shared utilities
```

## Coding conventions

**Backend**
- Use ASP.NET Core minimal API — not MVC controllers. One `*Endpoints.cs` file per domain area.
- Map endpoint groups in `Program.cs` via extension methods defined in each `*Endpoints.cs`.
- Keep domain logic in `Domain/`; keep EF queries in `Infrastructure/Persistence/`.

**Frontend**
- All server state via React Query hooks in `hooks/`; never fetch directly inside components.
- New UI primitives come from `shadcn/ui` — import from `@/components/ui/`.
- Co-locate a component and its sub-components in `components/<feature>/`; share across features via `components/shared/`.
- Run `npx tsc --noEmit` before committing to catch type errors early.

## Skills

Invoke the relevant skill before implementing — each loads focused coding-standards reference files.

| Skill                     | Invoke when                                                                 |
| ------------------------- | --------------------------------------------------------------------------- |
| `/backend-engineer`       | Implementing backend features, endpoints, services, migrations, tests       |
| `/frontend-engineer`      | Implementing frontend features, components, pages, hooks                    |
| `/design-doc`             | Designing a new feature before building — produces problem statement + spec |
| `/design-plan`            | Turning a completed design doc into a phased implementation plan            |
| `/system-architect`       | Architecture decisions, tech selection, ADR creation                        |
| `/code-reviewer-backend`  | Explicit backend code review — invoke by exact name only                    |
| `/code-reviewer-frontend` | Explicit frontend code review — invoke by exact name only                   |
