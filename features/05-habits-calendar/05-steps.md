# Implementation Steps — Habits Calendar View

Approved decisions:
- **Day mapping:** a habit's color appears only on the day its log was recorded (`HabitLog.CompletedAtUtc` date). Daily habits produce one log per day; Weekly/Monthly habits produce a single log per period, so they are painted only on that one day (no range expansion).
- **Placement:** a new `/calendar` page with an entry in `BottomNav`.
- **Day grouping in UTC**, consistent with `HabitPeriodCalculator` (no `TimeZoneId` schema change).
- **Navigation floor:** cannot navigate before **2025-01-01**.
- **Color dedup:** if multiple habits share the same `ColorHex` on the same day, only one dot is rendered.

---

## 1. Backend DTO (`src/HabitsApp.Core/HabitsApp.Application`)

- [ ] Create `Contracts/Habits/CalendarDayDto.cs`:
  ```csharp
  public sealed class CalendarDayDto
  {
      public DateOnly Date { get; set; }
      public IReadOnlyList<string> Colors { get; set; } = [];
  }
  ```

## 2. Application contract (`Contracts/Habits/IHabitService.cs`)

- [ ] Add method:
  ```csharp
  Task<IReadOnlyList<CalendarDayDto>> GetCalendarAsync(
      Guid userId,
      DateOnly start,
      DateOnly end,
      Guid? habitId,
      CancellationToken cancellationToken = default);
  ```

## 3. Service implementation (`src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs`)

- [ ] Implement `GetCalendarAsync`:
  - Convert range to UTC: `startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`, `endUtcExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`.
  - Query `HabitLogs` joined to `Habits` (for `ColorHex`), filtered by `CompletedAtUtc >= startUtc && < endUtcExclusive`.
  - If `habitId.HasValue`, filter `l.HabitId == habitId`.
  - Do **not** filter `IsArchived` (historical logs for archived habits must still show their color).
  - Project `(Date = l.CompletedAtUtc.Date, l.Habit.ColorHex)` to memory, then group by date and `Distinct` the colors.
  - Return days sorted ascending by `Date`.
- [ ] Add the method to the `IHabitService` DI registration already present in `Program.cs` (no new registration needed).

## 4. API endpoint (`src/HabitsApp.Presentation/HabitsApp.Api/Program.cs`)

- [ ] Add under `habitsGroup`:
  ```csharp
  habitsGroup.MapGet("/calendar", async (
      DateOnly? start,
      DateOnly? end,
      Guid? habitId,
      ClaimsPrincipal principal,
      IHabitService habitService,
      CancellationToken cancellationToken) =>
  {
      var today = DateOnly.FromDateTime(DateTime.UtcNow);
      var from = start ?? new DateOnly(today.Year, today.Month, 1);
      var to = end ?? new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
      var days = await habitService.GetCalendarAsync(GetUserId(principal), from, to, habitId, cancellationToken);
      return Results.Ok(days);
  });
  ```
  - Note: `GetUserId(principal)` is already defined in `Program.cs`.
  - `DateOnly` binds from query string via the built-in Minimal API binder (`?start=2025-02-01&end=2025-02-28&habitId=...`).

## 5. Backend verification

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] (Optional) Add a test for `GetCalendarAsync` covering: grouping by day, color dedup, `habitId` filter, and range boundaries.

## 6. Client model (`src/HabitsApp.Presentation/HabitsApp.WebBlazor`)

- [ ] Create `Models/Habits/CalendarDay.cs`:
  ```csharp
  public sealed class CalendarDay
  {
      public DateOnly Date { get; set; }
      public List<string> Colors { get; set; } = [];
  }
  ```

## 7. Client service (`Services/IHabitService.cs` + `Services/HabitService.cs`)

- [ ] Add to `IHabitService`:
  ```csharp
  Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
      DateOnly start, DateOnly end, Guid? habitId, CancellationToken cancellationToken = default);
  ```
- [ ] Implement in `HabitService`:
  ```csharp
  public async Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
      DateOnly start, DateOnly end, Guid? habitId, CancellationToken cancellationToken = default)
  {
      var query = $"/api/habits/calendar?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}";
      if (habitId.HasValue)
      {
          query += $"&habitId={habitId.Value}";
      }
      var response = await _httpClient.GetAsync(query, cancellationToken);
      return await HandleResponseAsync<IReadOnlyList<CalendarDay>>(response, cancellationToken);
  }
  ```

## 8. Calendar page (`Pages/Calendar.razor` + `Pages/Calendar.razor.cs`)

- [ ] Create page at `@page "/calendar"` with `@attribute [Authorize]`.
- [ ] State:
  - `ViewMode Mode` (`Month` / `Week`).
  - `DateOnly AnchorDate` (current date, clamped to `>= 2025-01-01`).
  - `List<HabitDashboardItem> Habits` (for the filter dropdown, loaded via existing `GetDashboardAsync`).
  - `Guid? SelectedHabitId` (`null` = all habits).
  - `IReadOnlyList<CalendarDay> Days` (loaded via `GetCalendarAsync`).
  - `bool IsLoading`, `string? ErrorMessage`.
- [ ] `OnInitializedAsync`: load habits + calendar for the current month/week.
- [ ] Navigation handlers (recompute `AnchorDate`, then reload calendar):
  - `Prev` / `Next`: `-1/+1` month in Month mode; `-7/+7` days in Week mode.
  - Disable `Prev` when the resulting range would start before 2025-01-01.
  - `Today` button resets `AnchorDate` to today.
- [ ] Header title:
  - Month: `"February 2025"`.
  - Week: `"3 – 9 Feb 2025"` (Monday-based week).
- [ ] Filter: a `<select>`/dropdown bound to `SelectedHabitId`; changing it reloads the calendar.
- [ ] Mode toggle: segmented control switching `Month`/`Week`.

## 9. Day cell component (`Components/Calendar/DayCell.razor` + `.razor.cs`)

- [ ] Create a reusable day cell component (keeps `Calendar.razor` under 50 lines):
  - Parameters: `DateOnly Date`, `IReadOnlyList<string> Colors`, `bool IsInRange`, `bool IsToday`.
  - Renders the day number and up to N color dots (deduped; `N` capped, e.g. show `+k` for overflow).
  - Dot style uses inline `background: {color}` for each `ColorHex`.
- [ ] Keep light/dark support via existing CSS tokens; colors come from `ColorHex` inline.

## 10. Navigation wiring (`Layout/BottomNav.razor`)

- [ ] Add a `NavLink` to `/calendar` between Habits and Settings:
  ```html
  <NavLink class="bottom-nav__item" href="/calendar" title="Calendar">
      <span class="material-symbols-outlined">calendar_month</span>
  </NavLink>
  ```

## 11. Styling (`wwwroot/css/app.css`)

- [ ] Add calendar styles on existing tokens (mobile-first, light/dark):
  - `.calendar` container, `.calendar__header`, `.calendar__controls` (segmented toggle + prev/next + today).
  - `.calendar-grid` (week: 7 columns; month: 7 columns × rows) using `grid-cols-7`-style CSS grid.
  - `.calendar__day`, `.calendar__day--outside`, `.calendar__day--today`, `.calendar__day__dots` (flex, wrap).
  - `.calendar__dot` (small colored circle).
- [ ] Responsive: single column-friendly on narrow screens; ensure tap targets are large enough on mobile.

## 12. Final verification

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual smoke check:
  - Login → `/calendar` from bottom nav.
  - Toggle Month/Week; navigate prev/next; `Today`.
  - Confirm `Prev` disabled at 2025-01-01.
  - Select a single habit vs "All habits".
  - Verify color dots render (deduped) and dark mode / mobile layout.
