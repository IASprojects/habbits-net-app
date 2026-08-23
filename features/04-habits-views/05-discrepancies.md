# Discrepancy Fixes — Habits Dashboard & Tracking Module

Follow-up steps to resolve the discrepancies found during review of `04-steps.md`.
Grouped by nature: documentation-only, production-logic bug, and optional performance.

---

## Group 1 — Documentation (no code changes)

The code is correct; `04-steps.md` lists slightly wrong file names. Fix = edit text only.

- [ ] `04-steps.md:120`: change `HabitCard.razor` (+ `.razor.css`) → `HabitCard.razor` (+ `HabitCard.razor.cs`)
- [ ] `04-steps.md:121`: add `HabitFormModel.cs` (form model with data annotations) alongside `HabitFormModal.razor` (+ `.razor.cs`)
- [ ] `04-steps.md:122`: change `MomentumRing.razor` → `MomentumRing.razor` (+ `MomentumRing.razor.cs`) for consistency

## Group 2 — Production bug: weekly `PeriodKey` across ISO year boundary

`HabitPeriodCalculator.GetPeriodKey` builds the weekly key with `windowStart.Year` + `ISOWeek.GetWeekOfYear`.
This is inconsistent for a Monday that belongs to the previous ISO year (e.g. Monday 3-Jan-2027 → ISO week 53 of 2026, but `windowStart.Year` = 2027 → produces invalid `2027-W53`).

- [ ] `src/HabitsApp.Core/HabitsApp.Application/Services/HabitPeriodCalculator.cs:33`:
  change `$"{windowStart.Year}-W{GetIsoWeekOfYear(windowStart):D2}"`
  to `$"{ISOWeek.GetYear(windowStart)}-W{ISOWeek.GetWeekOfYear(windowStart):D2}"`
  (`System.Globalization` is already imported; remove the `GetIsoWeekOfYear` helper if now unused)
- [ ] Add a test in `tests/HabitsApp.Application.Tests/HabitPeriodCalculatorTests.cs` covering a Monday in early January that belongs to the previous ISO year (2026→2027 / 2027→2028 boundary)

## Group 3 — Performance (optional)

`HabitService.GetDashboardAsync` loads all `HabitLog` rows for the habits into memory and counts in C# (`src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs:34-49`).

- [ ] Replace with a SQL-side aggregation keyed by period/window:
  - filter `CompletedAtUtc` to each habit's window range, or
  - `GroupBy(l => l.HabitId)` with `CountAsync`

---

## Verification

- [ ] `dotnet build`
- [ ] `dotnet test`