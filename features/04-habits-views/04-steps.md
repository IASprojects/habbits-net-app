# Implementation Steps — Habits Dashboard & Tracking Module

Approved decisions:
- Period windows computed in **UTC** (no `TimeZoneId` schema change).
- **Quick Log is idempotent per period** (one log entry per habit per Daily/Weekly/Monthly window), enforced with a unique `(HabitId, PeriodKey)` index.
- UI follows `04-mockup.html` look-and-feel, translated onto the existing "Obsidian & Emerald" tokens in `app.css` (no Tailwind).
- **Global mockup chrome** replaces the slide-out NavMenu: fixed top app bar + mobile floating bottom nav.
- Icons use **Material Symbols Outlined**.

---

## 1. Domain (`src/HabitsApp.Core/HabitsApp.Domain`)

- [ ] Create `Enums/FrequencyType.cs`:
  ```csharp
  public enum FrequencyType { Daily, Weekly, Monthly }
  ```
- [ ] Create `Entities/Habit.cs`:
  - `Guid Id`, `Guid UserId`, `string Title`, `string? Description`
  - `string ColorHex = "#4F46E5"`
  - `FrequencyType Frequency = FrequencyType.Daily`
  - `int TargetCount = 1`
  - `bool IsArchived = false`
  - `DateTime CreatedAtUtc`, `DateTime? UpdatedAtUtc`
  - `ICollection<HabitLog> Logs = []`
- [ ] Create `Entities/HabitLog.cs`:
  - `Guid Id`, `Guid HabitId`, `Guid UserId`, `DateTime CompletedAtUtc`
  - `string PeriodKey` (e.g. `2026-08-19` / `2026-W34` / `2026-08`)

## 2. Infrastructure (`src/HabitsApp.Core/HabitsApp.Infrastructure`)

- [ ] Add `ICurrentUserService` (Infrastructure namespace, e.g. `Abstractions/ICurrentUserService.cs`):
  ```csharp
  public interface ICurrentUserService { Guid? UserId { get; } }
  ```
- [ ] `ApplicationDbContext`:
  - Add `DbSet<Habit> Habits` and `DbSet<HabitLog> HabitLogs`
  - Constructor takes `ICurrentUserService` (keep existing `DbContextOptions` ctor for design-time/migrations)
  - Global query filters: `h.UserId == _currentUserService.UserId` on Habit and HabitLog
- [ ] Model config:
  - Habit: `HasIndex(h => new { h.UserId, h.IsArchived })`
  - HabitLog: `HasIndex(l => new { l.HabitId, l.PeriodKey }).IsUnique()`
  - HabitLog: `HasIndex(l => new { l.HabitId, l.CompletedAtUtc })`
  - HabitLog → Habit FK, `OnDelete(Cascade)`
- [ ] `ICurrentUserService` implementation `CurrentUserService` in `src/HabitsApp.Presentation/HabitsApp.Api/Services/` reading `IHttpContextAccessor.HttpContext?.User.FindFirstValue("sub")`.
- [ ] Register `IHttpContextAccessor` + `ICurrentUserService` (scoped) in API `Program.cs`.

## 3. Migration

- [ ] Add migration `AddHabitTracking` (dotnet-ef) in Infrastructure.
- [ ] Verify snapshot updated.

## 4. Application (`src/HabitsApp.Core/HabitsApp.Application`)

- [ ] Create `Contracts/Habits/HabitDashboardItemDto.cs`:
  `Id`, `Title`, `Description`, `ColorHex`, `Frequency`, `TargetCount`, `CurrentPeriodCount`, `IsCompletedForPeriod`
- [ ] Create `Contracts/Habits/CreateHabitDto.cs` (data annotations):
  `Title` (Required), `Description?`, `ColorHex`, `Frequency`, `TargetCount` (>= 1)
- [ ] Create `Contracts/Habits/UpdateHabitDto.cs` (same fields as Create).
- [ ] Create `Contracts/Habits/HabitResult.cs` mirroring `AuthResult` shape (`Succeeded`, `StatusCode?`, `ErrorType`, `ErrorDetail`, `Data`).
- [ ] Create `Contracts/Habits/IHabitService.cs`:
  - `Task<IReadOnlyList<HabitDashboardItemDto>> GetDashboardAsync(Guid userId, CancellationToken)`
  - `Task<HabitResult> CreateAsync(Guid userId, CreateHabitDto, CancellationToken)`
  - `Task<HabitResult> UpdateAsync(Guid userId, Guid habitId, UpdateHabitDto, CancellationToken)`
  - `Task<HabitResult> QuickLogAsync(Guid userId, Guid habitId, CancellationToken)`
  - `Task<HabitResult> ArchiveAsync(Guid userId, Guid habitId, CancellationToken)`
- [ ] Create `Services/HabitPeriodCalculator.cs` (pure, static):
  - `GetWindowStartUtc(FrequencyType, DateTime utcNow)` / `GetWindowEndUtc(...)`
    - Daily: start/end of current UTC day
    - Weekly: Monday 00:00 → next Monday 00:00
    - Monthly: first of month → first of next month
  - `GetPeriodKey(FrequencyType, DateTime utcNow)`

## 5. API (`src/HabitsApp.Presentation/HabitsApp.Api`)

- [ ] Create `Services/HabitService.cs` implementing `IHabitService` (pattern follows `AuthService`):
  - GetDashboard: active habits + per-habit log count in window → `CurrentPeriodCount`, `IsCompletedForPeriod`
  - Create: persist with `CreatedAtUtc`
  - Update: set `UpdatedAtUtc = DateTime.UtcNow`; 404 if not found/not owned
  - QuickLog: if log exists for `(HabitId, PeriodKey)` return current state (idempotent); else insert + return updated item; catch unique-violation race
  - Archive: `IsArchived = true`, `UpdatedAtUtc = DateTime.UtcNow`
- [ ] Register `IHabitService` (scoped) in `Program.cs`.
- [ ] Add `MapGroup("/api/habits").RequireAuthorization()`:
  - `GET /` → 200 list
  - `POST /` → 201
  - `PUT /{id:guid}` → 200 / 404
  - `POST /{id:guid}/quick-log` → 200 + item / 404
  - `DELETE /{id:guid}` → 204
  - Resolve userId from `ClaimsPrincipal` "sub".

## 6. Backend verification

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Add tests:
  - `tests/HabitsApp.Application.Tests/HabitPeriodCalculatorTests.cs` (window boundaries + PeriodKey for Daily/Weekly/Monthly)
  - Dashboard aggregation + idempotent quick-log logic tests

## 7. Client services (`src/HabitsApp.Presentation/HabitsApp.WebBlazor`)

- [ ] `Models/Habits/HabitDashboardItem.cs`, `CreateHabitRequest.cs`, `UpdateHabitRequest.cs`
- [ ] `Services/IHabitService.cs` + `Services/HabitService.cs` (typed HttpClient, pattern mirrors `AuthService`)
- [ ] Register `IHabitService`/`HabitService` in `Program.cs`

## 8. Global chrome (mockup)

- [ ] `wwwroot/index.html`: add Material Symbols Outlined font `<link>`
- [ ] `Layout/TopAppBar.razor`: fixed top bar (brand mark + "HabitsApp", avatar initials circle, notification button)
- [ ] `Layout/BottomNav.razor`: mobile-only floating pill nav (Home / Habits / Settings), Material Symbols icons
- [ ] `Layout/MainLayout.razor`: render TopAppBar + BottomNav inside `AuthorizeView` (hidden for Login/Register); remove NavMenu usage
- [ ] Remove `Layout/NavMenu.razor` + `NavMenu.razor.css` + `NavMenu.razor.cs`
- [ ] `app.css`: add `.glass-panel`, `.glass-panel-level-2`, `.neon-glow-primary`, `.top-app-bar`, `.bottom-nav`, `.fab`, `.habit-card`, `.accent-bar`, `.chip`, `.momentum-ring`, `.modal` styles on existing tokens

## 9. Habits dashboard UI

- [ ] `Pages/Habits.razor` (`/habits`, `[Authorize]`) + `Pages/Habits.razor.cs`
  - Greeting ("Hello, {firstName}") + Daily Momentum ring (% of habits completed for period)
  - "Today's Focus" list of `HabitCard` components
  - FAB `+` (desktop bottom-right; raised above bottom-nav on mobile)
- [ ] `Components/Habits/HabitCard.razor` (+ `HabitCard.razor.cs`): accent bar in `ColorHex`, chip, progress (`2 / 3 this week`), streak label, edit button, QUICK LOG pill or check icon
- [ ] `Components/Habits/HabitFormModal.razor` (+ `HabitFormModel.cs` with data annotations, + `.razor.cs`): shared create/edit modal (pre-filled on edit)
- [ ] `Components/Habits/MomentumRing.razor` (+ `MomentumRing.razor.cs`): SVG progress ring

## 10. Navigation wiring

- [ ] `Pages/Login.razor.cs`: default redirect `/` → `/habits`
- [ ] Confirm `/habits` reachable from bottom nav

## 11. Final verification

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual smoke check: login → `/habits`, create/edit/quick-log/archive, idempotent re-log, dark mode, mobile layout