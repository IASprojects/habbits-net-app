# Implementation Steps — User Time Zone & Habit Reset / Streak

## Problem

Habit day/period boundaries and the "today" used to reset habits are computed entirely in **UTC**
(`HabitPeriodCalculator`, `HabitService`, `Program.cs`, `Calendar.razor.cs` all use `DateTime.UtcNow`).
A user in a non-UTC time zone sees habits reset at the wrong moment (their local midnight ≠ UTC midnight),
the daily/weekly/monthly windows are off, calendar day bucketing is wrong, and the streak shown on each
habit card is a static label ("Completed"/"On track") with no real consecutive-day computation.

There is no timezone field on the user, no way to edit the profile, and no timezone-aware calendar "today".

## Goal

Let each user define their timezone (IANA ID) from a dropdown in the **Settings** page. Habit reset
(daily/weekly/monthly windows), the daily dedupe key, calendar bucketing, the "today" reference in the
calendar, and an automatic **streak** (consecutive local days completed) must all be computed in the user's
timezone.

## Approved decisions

- **Format**: IANA timezone ID (e.g. `America/New_York`) stored on `ApplicationUser.TimeZoneId`
  (`string?`, max length 64). `null`/empty falls back to UTC.
- **Default / first use**: Auto-detect the browser timezone via JS interop
  (`Intl.DateTimeFormat().resolvedOptions().timeZone`) and persist it via the profile update endpoint when the
  user has not set one yet.
- **Timezone list**: server endpoint `GET /api/timezones` returning `TimeZoneInfo.GetSystemTimeZones()`
  (`Id`, `DisplayName`, `BaseUtcOffset`) — authoritative for the server's IANA database.
- **Scope of "reset"**: recompute daily/weekly/monthly period windows and the `PeriodKey`/`GetDayKey` dedupe
  using the user's **local date**; convert local boundaries back to UTC (`TimeZoneInfo.ConvertTimeToUtc`,
  DST-safe) for comparison against `CompletedAtUtc`.
- **Streak**: number of **consecutive local days** completed (applies to all habits, regardless of frequency),
  ending at "today", or "yesterday" if today is not yet completed. Exposed as `int Streak` on the dashboard
  DTO and rendered on `HabitCard` (fire icon + "N day streak").
- **Frontend calendar**: `anchorDate` / `IsToday` / `GoToday` in `Calendar.razor.cs` aligned to the user's
  timezone (from the profile).

---

## 1. Domain / Data

File: `src/HabitsApp.Core/HabitsApp.Domain/Entities/ApplicationUser.cs`

- [ ] Add property `public string? TimeZoneId { get; set; }` to `ApplicationUser`.

Infrastructure (migration):

- [ ] Configure max length (`HasMaxLength(64)`) for `TimeZoneId` in the DbContext `OnModelCreating`
      (`src/HabitsApp.Core/HabitsApp.Infrastructure/Data/ApplicationDbContext.cs`).
- [ ] Generate migration `AddUserTimeZone` under
      `src/HabitsApp.Core/HabitsApp.Infrastructure/Data/Migrations/`.

## 2. Application

### DTOs / contracts

- [ ] `UserProfileDto` (`src/HabitsApp.Core/HabitsApp.Application/Contracts/Auth/UserProfileDto.cs`):
      add `public string? TimeZoneId { get; set; }`.
- [ ] Create `src/HabitsApp.Core/HabitsApp.Application/Contracts/Auth/TimeZoneDto.cs`:
      `{ string Id; string DisplayName; TimeSpan BaseUtcOffset }`.
- [ ] Create `src/HabitsApp.Core/HabitsApp.Application/Contracts/Auth/UpdateProfileCommand.cs`:
      `{ string? FirstName; string? LastName; string? TimeZoneId }`.
- [ ] `HabitDashboardItemDto` (`Contracts/Habits/HabitDashboardItemDto.cs`): add `public int Streak { get; set; }`.

### IAuthService

File: `src/HabitsApp.Core/HabitsApp.Application/Contracts/Auth/IAuthService.cs`

- [ ] Add `Task<UserProfileDto?> UpdateMeAsync(ClaimsPrincipal principal, UpdateProfileCommand command, CancellationToken cancellationToken = default);`
- [ ] Add `IReadOnlyList<TimeZoneDto> GetTimezones();`

### HabitPeriodCalculator (timezone-aware)

File: `src/HabitsApp.Core/HabitsApp.Application/Services/HabitPeriodCalculator.cs`

- [ ] Add overloads that take `TimeZoneInfo tz` + `DateTime utcNow` (keep the existing UTC-only methods
      for compatibility/tests or delegate them with `TimeZoneInfo.Utc`):
  - `GetLocalNow(TimeZoneInfo tz, DateTime utcNow)` → `TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz)`.
  - `GetWindowStartUtc(FrequencyType, TimeZoneInfo tz, DateTime utcNow)` / `GetWindowEndUtc(...)`:
    compute the **local** period start/end and convert to UTC via `TimeZoneInfo.ConvertTimeToUtc(local, tz)`.
  - `GetPeriodKey(FrequencyType, TimeZoneInfo tz, DateTime utcNow)` / `GetDayKey(TimeZoneInfo tz, DateTime utcNow)`:
    keys based on the **local date** (daily `yyyy-MM-dd`, weekly ISO `yyyy-W##`, monthly `yyyy-MM`).
- [ ] Keep DST correctness: boundaries computed on local wall time, then converted to UTC.

## 3. API

### AuthService

File: `src/HabitsApp.Presentation/HabitsApp.Api/Services/AuthService.cs`

- [ ] Implement `GetTimezones()` → `TimeZoneInfo.GetSystemTimeZones()` mapped to `TimeZoneDto`.
- [ ] Implement `UpdateMeAsync(principal, command, ct)`:
  - Resolve user via `sub` → `UserManager.FindByIdAsync`.
  - If `command.TimeZoneId` is not null/empty: validate with `TimeZoneInfo.FindSystemTimeZoneById`
    (fallback/invalid → 400 with a clear error); otherwise set UTC.
  - Update `FirstName`/`LastName` when provided (optional, keeps endpoint reusable).
  - Return refreshed `UserProfileDto` (including `TimeZoneId`).

### Program.cs (Minimal APIs)

File: `src/HabitsApp.Presentation/HabitsApp.Api/Program.cs`

- [ ] `GET /api/timezones` → `Results.Ok(authService.GetTimezones())` (public, no auth required).
- [ ] `PUT /api/auth/me` → requires auth; binds `UpdateProfileCommand`, calls `UpdateMeAsync`,
      returns `Ok(profile)` / `Unauthorized()` / `Problem(...)` for invalid timezone.
- [ ] `/api/habits/calendar` default "today": compute `today` from the **user's** timezone instead of
      `DateOnly.FromDateTime(DateTime.UtcNow)` (resolve tz for `userId`, convert `utcNow`).

### HabitService (timezone + streak + calendar bucketing)

File: `src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs`

- [ ] Resolve the user's `TimeZoneInfo` once per operation: query
      `_dbContext.Users` (`DbSet<ApplicationUser>`) for `TimeZoneId` by `userId`; fallback `TimeZoneInfo.Utc`.
- [ ] `GetDashboardAsync` / `BuildDashboardItemAsync`: compute `localNow` from the user's tz and use the
      timezone-aware window methods for `CurrentPeriodCount`.
- [ ] `QuickLogAsync`: `dayKey = GetDayKey(tz, now)`; window bounds tz-aware for the period cap.
- [ ] `Streak` computation (private helper, shared by dashboard/quicklog/create/update):
  - Distinct **local dates** (`yyyy-MM-dd`) per habit from `CompletedAtUtc` converted to the user's tz.
  - Count consecutive days back from today (or yesterday if today not completed yet).
- [ ] `GetCalendarAsync`: load `CompletedAtUtc` (+`HabitId`), convert to **local date** in memory and
      group by that local `DateOnly` (remove the DB `l.CompletedAtUtc.Date` projection);
      range bounds (`start/end` local dates → UTC) using the user's tz.
- [ ] Pass `Streak` through `ToDto`.

## 4. Frontend (WebBlazor)

### Models

- [ ] `UserProfileDto` mirror (`Models/Auth/UserProfileDto.cs`): add `TimeZoneId`.
- [ ] Create `Models/Auth/TimeZoneDto.cs` and `Models/Auth/UpdateProfileRequest.cs` (mirrors).
- [ ] `HabitDashboardItem` (`Models/Habits/HabitDashboardItem.cs`): add `int Streak`.

### IAuthService / AuthService (client)

Files: `Services/IAuthService.cs`, `Services/AuthService.cs`

- [ ] `GetTimezonesAsync()` → `GET /api/timezones`.
- [ ] `UpdateMeAsync(UpdateProfileRequest)` → `PUT /api/auth/me`.
- [ ] `GetMeAsync()` already returns the profile; ensure `TimeZoneId` is read.

### JS interop for auto-detect

- [ ] Create `Services/TimeZoneJsInterop.cs` — `IJSRuntime` helper exposing
      `Task<string?> GetBrowserTimeZoneIdAsync()` calling `Intl.DateTimeFormat().resolvedOptions().timeZone`.

### Settings page

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Settings.razor` + `.razor.cs`

- [ ] New section **"Time Zone"** (`.glass-panel--settings`):
  - `<select>` populated from `GetTimezonesAsync()`, pre-selected with `UserProfileDto.TimeZoneId`.
  - "Save" button → `UpdateMeAsync` → refresh profile; show success/error.
- [ ] On load, if the profile's `TimeZoneId` is empty/null: auto-detect via `TimeZoneJsInterop` and
      persist immediately (first-use detection); fall back to `UTC` if detection fails.
- [ ] Register `TimeZoneJsInterop` (and a small `ProfileService`/state helper if needed) in `Program.cs`.

### HabitCard streak

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor.cs`

- [ ] Replace `StreakLabel` to render the real value:
      `Habit.Streak > 0 ? $"{Habit.Streak} day streak" : "No streak"` (or similar, keeping the fire icon
      in `HabitCard.razor`).

### Calendar "today" alignment

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Calendar.razor.cs`

- [ ] `anchorDate`, `GoToday`, `IsToday` use the user's local "today" (from profile `TimeZoneId`,
      converted via JS interop or a client-side conversion helper consistent with `Intl`).

## 5. Tests

- [ ] `HabitPeriodCalculatorTests` (`tests/HabitsApp.Application.Tests/`): add timezone-aware cases
  (e.g. same UTC instant across `UTC` and `America/New_York` yields different `GetDayKey`/window bounds).
- [ ] `HabitServiceTests`: streak computation (1-day gap breaks streak; today incomplete → starts from
  yesterday; 0 logs → 0).
- [ ] `AuthService`/API: timezone update persists and round-trips in `GET /api/auth/me`; invalid tz → 400.

## 6. Verification

- [ ] `dotnet build` — 0 warnings / 0 errors.
- [ ] `dotnet test`.
- [ ] Apply migration locally and smoke-test:
  - Settings > Time Zone shows the dropdown (server list) and auto-detects/saves on first use.
  - Changing timezone changes the dashboard "today" window and the calendar "today" highlight.
  - Habit card shows a real streak that resets when a local day is skipped.