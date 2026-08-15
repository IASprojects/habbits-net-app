# AI Development Guide & Rules — HabitsApp

Target Audience: AI Coding Assistants (Copilot, Cursor, Roo Code, Cline, Continue) & Human Developers.
Project Goal: Build a modern, cross-platform habit-tracking application using .NET 10.

---

## 1. Quick Commands

### Project Setup
dotnet new sln -n HabitsApp
dotnet new classlib -n HabitsApp.Domain -o src/HabitsApp.Core/HabitsApp.Domain
dotnet new classlib -n HabitsApp.Application -o src/HabitsApp.Core/HabitsApp.Application
dotnet new classlib -n HabitsApp.Infrastructure -o src/HabitsApp.Core/HabitsApp.Infrastructure
dotnet new webapi -n HabitsApp.Api -o src/HabitsApp.Presentation/HabitsApp.Api
dotnet new blazorwasm -n HabitsApp.WebBlazor -o src/HabitsApp.Presentation/HabitsApp.WebBlazor

dotnet sln add src/HabitsApp.Core/HabitsApp.Domain/HabitsApp.Domain.csproj
dotnet sln add src/HabitsApp.Core/HabitsApp.Application/HabitsApp.Application.csproj
dotnet sln add src/HabitsApp.Core/HabitsApp.Infrastructure/HabitsApp.Infrastructure.csproj
dotnet sln add src/HabitsApp.Presentation/HabitsApp.Api/HabitsApp.Api.csproj
dotnet sln add src/HabitsApp.Presentation/HabitsApp.WebBlazor/HabitsApp.WebBlazor.csproj

dotnet new xunit -n HabitsApp.Domain.Tests -o tests/HabitsApp.Domain.Tests
dotnet new xunit -n HabitsApp.Application.Tests -o tests/HabitsApp.Application.Tests

dotnet sln add tests/HabitsApp.Domain.Tests/HabitsApp.Domain.Tests.csproj
dotnet sln add tests/HabitsApp.Application.Tests/HabitsApp.Application.Tests.csproj

dotnet add tests/HabitsApp.Domain.Tests/HabitsApp.Domain.Tests.csproj reference src/HabitsApp.Core/HabitsApp.Domain/HabitsApp.Domain.csproj
dotnet add tests/HabitsApp.Application.Tests/HabitsApp.Application.Tests.csproj reference src/HabitsApp.Core/HabitsApp.Application/HabitsApp.Application.csproj

### Build and Run
dotnet build
dotnet test
dotnet run --project src/HabitsApp.Presentation/HabitsApp.Api
dotnet run --project src/HabitsApp.Presentation/HabitsApp.WebBlazor

---

## 2. Technical Stack

* Language & Runtime: C# 14 / .NET 10 SDK (LTS)
* Backend: ASP.NET Core Web API 10 (Minimal APIs with native validation)
* Frontend: Blazor WebAssembly 10 (PWA)
* Database & ORM: EF Core 10 with SQLite (Local) / PostgreSQL (Production)
* UI & Styling: Tailwind CSS or MudBlazor

---

## 3. System Architecture

```text
habits-net-app/
├── src/
│   ├── HabitsApp.Core/
│   │   ├── HabitsApp.Domain/           # Entities, Value Objects, Enums
│   │   └── HabitsApp.Application/      # Business logic, DTOs, Interfaces
│   │
│   ├── HabitsApp.Infrastructure/
│   │   └── HabitsApp.Infrastructure/   # EF Core 10 DbContext, Migrations
│   │
│   └── HabitsApp.Presentation/
│       ├── HabitsApp.Api/              # ASP.NET Core 10 Web API
│       └── HabitsApp.WebBlazor/        # Blazor WebAssembly UI (PWA Client)
│
├── tests/
│   ├── HabitsApp.Domain.Tests/
│   └── HabitsApp.Application.Tests/
│
├── AGENTS.md                           # Core AI context file
└── HabitsApp.sln

```

### Layer Responsibilities

| Layer | Project | Description & Contents |
| --- | --- | --- |
| Domain | HabitsApp.Domain | Core domain models (Habit, LogEntry, User, Streak), Interfaces. Zero external dependencies. |
| Application | HabitsApp.Application | Business logic, Services, DTOs, Validators. |
| Infrastructure | HabitsApp.Infrastructure | EF Core 10 DbContext, Repositories, Migrations, Auth. |
| API | HabitsApp.Api | Minimal APIs with native validation (`AddValidation`), OpenAPI 3.1, Middleware. |
| Client | HabitsApp.Web | Blazor WASM PWA components, Client Services, State Management. |

---

## 4. Coding Rules & Conventions

### C# 14 & .NET 10 Standards

1. C# 14 Features: Use primary constructors, collection expressions (`[]`), extension members, and the `field` keyword for auto-properties.
2. Nullable Reference Types: Keep `<Nullable>enable</Nullable>`. Do not use `!` unless strictly necessary.
3. Asynchronous Code: Use `async`/`await` end-to-end. Always pass `CancellationToken` to database and network operations.

### Backend (ASP.NET Core 10 API)

1. Minimal APIs: Use Typed Minimal APIs (`Results<Ok<T>, ValidationProblem, NotFound>`).
2. Native Validation: Prefer .NET 10 built-in Minimal API validation over custom middleware.
3. EF Core 10: Use Complex Types for value objects and LINQ `LeftJoin`/`RightJoin` operators where appropriate.

### Frontend & UI Rules (Blazor WebAssembly & Tailwind CSS / MudBlazor)

1. Mobile-First & Responsive Layout: All Razor components MUST be designed mobile-first and adapt cleanly to all screen sizes (mobile, tablet, desktop) using fluid containers and responsive utility classes (e.g., flex-col md:flex-row, grid-cols-1 md:grid-cols-3).
2. Native Dark Theme Support: All components MUST support both Light and Dark themes out-of-the-box using semantic color tokens or CSS dark mode variants (e.g., bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-100).
3. Component Separation: If a `.razor` file exceeds 50 lines of code, separate logic into a `Component.razor.cs` partial class.


---

## 5. Instructions for AI Assistants

* Direct Code Output: Provide production-ready C# 14 and Razor code without preamble or filler text.
* File Scoping: Edits must be modular and respect layer boundaries defined above.
* Refactoring: Preserve existing project structures, naming conventions, and dependencies.

### 6. Agent Workflow & Execution Protocol

When handling any feature request, database setup, or task, you MUST follow this 3-step workflow:

1. **Phase 1: Draft Analysis & Architectural Proposal**
   - Read the user's draft or requirements.
   - Formulate a high-level proposal (Domain entities, Use cases, DTOs, API endpoints, UI design).
   - Ask for user feedback or approval before generating any code.

2. **Phase 2: Step-by-Step Implementation Plan**
   - Once the proposal is approved, create a granular, ordered checklist of tasks (files to create, commands to run, references to add).
   - Wait for explicit user confirmation (e.g., "Proceed", "Go ahead", or "Execute") before making modifications.

3. **Phase 3: Execution & Verification**
   - Implement the solution step-by-step following the approved plan.
   - Strictly respect all rules defined in this `AGENTS.md` (Clean Architecture, .NET 10, C# 14, Responsive UI, Dark Mode support).