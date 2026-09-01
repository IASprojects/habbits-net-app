---
name: code-review
description: Se activa al hacer "code review", "revisar PR", "antes del PR" o al evaluar código. Valida Clean Architecture, pruebas unitarias, UI (Blazor) y ejecuta compilación.
---

# Strict Code Review — C# 14 / .NET 10 + Blazor WebAssembly

Act as a demanding reviewer. The goal is a clean, mergeable PR. Every violation is a blocking comment. Report findings grouped by severity with exact `file:line` references and concrete code fixes.

**1. Determine Changed Files**
- Run `git status --porcelain` and `git diff HEAD` (or against the provided base branch).
- Only review the changed code and its immediate context.

**2. Clean Architecture & Boundaries**
- `Domain` must have zero dependencies on `Application`, `Infrastructure`, or external packages[cite: 1].
- No `Microsoft.EntityFrameworkCore` logic in `Domain` or `Application`[cite: 1].
- Prevent leaking persistence details (`DbSet`, generated keys) into API contracts or Blazor components.

**3. C# 14 & .NET 10 Idioms**
- Enforce primary constructors, collection expressions `[]`, and the `field` keyword for auto-properties[cite: 1].
- No `!` null-suppression unless strictly justified. Ensure `<Nullable>enable</Nullable>` practices.
- Typed Minimal APIs with native validation (`AddValidation`), avoiding `[ApiController]`[cite: 1].

**4. Blazor WebAssembly & UI**
- **Mobile-first & Dark Mode:** Force fluid containers (`md:flex-row`) and native light/dark support (`dark:bg-slate-900`)[cite: 1].
- **Separation of Concerns:** Any `.razor` exceeding 50 lines MUST be split into a `Component.razor.cs` code-behind[cite: 1].

**5. Unit Testing**
- Verify tests in `HabitsApp.Domain.Tests` or `HabitsApp.Application.Tests` exist for new logic.
- Assert observable outcomes, avoiding unnecessary mocking unless at external boundaries.

**6. Verification Execution**
Run the following in the terminal and report the exact output:
- `dotnet build` (Must be 0 errors, 0 warnings).
- `dotnet test` (All must pass).

**Output Format**
Generate a strict report using this format:
- **Summary:** Files reviewed, Build/Test status.
- **🔴 BLOCKERS:** (File:Line) - Concrete fix required before PR.
- **🟡 MAJOR:** Architectural or test coverage issues.
- **🔵 MINOR:** Polish and syntax suggestions.
- **Terminal Evidence:** The result of the `dotnet` commands.