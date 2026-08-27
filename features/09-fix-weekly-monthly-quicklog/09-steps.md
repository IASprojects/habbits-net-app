# Implementation Steps — Fix Weekly/Monthly Quick Log (allow up to `TargetCount` per period)

## Problem

For habits with `Frequency = Weekly` or `Monthly` and `TargetCount > 1` (e.g. "exercise 3x this week"),
Quick Log only ever registers **one** log for the entire period, no matter how many different days pass.
Logging on day 1 of the week works; logging again on day 2, 3, etc. of the *same* week is silently ignored,
so the dashboard gets stuck at `1 / 3 this week` even after completing the habit on 3 different days.

Root cause: `HabitService.QuickLogAsync` (`HabitService.cs:103-143`) dedupes using
`PeriodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, now)`. For Weekly/Monthly this key spans the
**entire** week/month, so the existing-log check (`l.HabitId == habitId && l.PeriodKey == periodKey`) matches
and no-ops for every day after the first one in that period. The unique DB index `(HabitId, PeriodKey)` then
permanently caps the period at 1 log, contradicting the `TargetCount` field and the UI's own
`"{CurrentPeriodCount} / {TargetCount} this week"` progress label (`HabitCard.razor.cs:24-30`), which assumes
multiple logs per period are possible.

## Approved decisions

- Quick Log must allow **up to `TargetCount` logs per period**, **one per calendar day** (UTC date), for
  Weekly and Monthly habits (Daily habits already behave this way since `TargetCount` is effectively checked
  per day already).
- Same-day double-clicks on Quick Log must remain idempotent (no duplicate log for the same calendar day).
- Once `CurrentPeriodCount >= TargetCount` for the active period, further Quick Log calls must no-op (no
  extra rows), even on a new day within the same period.
- `HabitLog.PeriodKey` keeps its column name, type (`varchar(16)`), and unique index
  `(HabitId, PeriodKey)` — only its *value* changes to always be the calendar day key (`yyyy-MM-dd`), which
  makes the DB-level uniqueness mean "one log row per calendar day per habit" (correct for all frequencies).
  No EF Core migration required.
- The per-period cap (`TargetCount`) is enforced at the application level (count logs in the current window),
  not via the `PeriodKey` uniqueness (which now only prevents same-day duplicates).
- No frontend changes required — `HabitCard` already reads `CurrentPeriodCount`/`TargetCount` correctly and
  will reflect the fix automatically.

---

## 1. Application — `HabitPeriodCalculator`

File: `src/HabitsApp.Core/HabitsApp.Application/Services/HabitPeriodCalculator.cs`

- [ ] Add a new static method `GetDayKey(DateTime utcNow)` that returns the calendar day key
      (`yyyy-MM-dd`, invariant culture), independent of `FrequencyType`. Reuse/extract the existing
      Daily-branch formatting from `GetPeriodKey` to avoid duplicating the format string.
- [ ] Keep `GetPeriodKey`, `GetWindowStartUtc`, `GetWindowEndUtc` unchanged (still used for window/period
      calculations and existing tests).

## 2. API — `HabitService.QuickLogAsync`

File: `src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs:103-143`

- [ ] Replace the dedupe key computation:
  ```diff
  - var periodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, now);
  -
  - var existing = await _dbContext.HabitLogs
  -     .FirstOrDefaultAsync(l => l.HabitId == habitId && l.PeriodKey == periodKey, cancellationToken);
  - if (existing is not null)
  - {
  -     return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
  - }
  + var dayKey = HabitPeriodCalculator.GetDayKey(now);
  +
  + var alreadyLoggedToday = await _dbContext.HabitLogs
  +     .AnyAsync(l => l.HabitId == habitId && l.PeriodKey == dayKey, cancellationToken);
  + if (alreadyLoggedToday)
  + {
  +     return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
  + }
  +
  + var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, now);
  + var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, now);
  + var currentPeriodCount = await _dbContext.HabitLogs
  +     .CountAsync(l => l.HabitId == habitId && l.CompletedAtUtc >= windowStart && l.CompletedAtUtc < windowEnd, cancellationToken);
  + if (currentPeriodCount >= habit.TargetCount)
  + {
  +     return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
  + }
  ```
- [ ] Update the insert to use `dayKey` instead of `periodKey`:
  ```diff
    _dbContext.HabitLogs.Add(new HabitLog
    {
        Id = Guid.NewGuid(),
        HabitId = habitId,
        UserId = userId,
        CompletedAtUtc = now,
  -     PeriodKey = periodKey
  +     PeriodKey = dayKey
    });
  ```
- [ ] Update the race-condition catch log message/variable references from `periodKey` to `dayKey`
      (`DbUpdateException` on Postgres `23505` now represents a same-day race, not a same-period race).
- [ ] Consider extracting the `currentPeriodCount` computation into a small private helper shared with
      `BuildDashboardItemAsync` (both now compute the same window-bounded count) to avoid duplicated logic —
      optional refactor, not required for correctness.

## 3. Domain — doc comment

File: `src/HabitsApp.Core/HabitsApp.Domain/Entities/HabitLog.cs`

- [ ] Update the XML doc / inline comment on `PeriodKey` to reflect its new meaning: "calendar day key
      (`yyyy-MM-dd`, UTC) used to prevent duplicate logs on the same day; the per-period completion target is
      enforced separately via `TargetCount` and the window bounds from `HabitPeriodCalculator`."

## 4. Tests — `HabitPeriodCalculatorTests.cs`

File: `tests/HabitsApp.Application.Tests/HabitPeriodCalculatorTests.cs`

- [ ] Add a test for `GetDayKey` confirming it returns `yyyy-MM-dd` regardless of time-of-day component
      (e.g. `2026-08-19T23:59:00Z` → `"2026-08-19"`).

## 5. Tests — `HabitServiceTests.cs`

File: `tests/HabitsApp.Application.Tests/HabitServiceTests.cs`

- [ ] `QuickLogAsync_AllowsMultipleDaysWithinSameWeek_UpToTargetCount`
  - Weekly habit, `TargetCount = 3`.
  - Seed two prior `HabitLog` rows within the same ISO week (different days, `PeriodKey` = each day's
    `yyyy-MM-dd`) to simulate two earlier check-ins.
  - Call `QuickLogAsync` for "today" (same week, third distinct day) → assert `Succeeded`,
    `CurrentPeriodCount == 3`, `IsCompletedForPeriod == true`, and total `HabitLogs` count for the habit is 3.
- [ ] `QuickLogAsync_SameDayDoubleClick_IsIdempotent_ForWeeklyHabit`
  - Weekly habit, `TargetCount = 3`.
  - Call `QuickLogAsync` twice in a row (same simulated "now").
  - Assert only 1 `HabitLog` row exists and `CurrentPeriodCount == 1`.
- [ ] `QuickLogAsync_NoOpsOnceTargetReached_EvenOnNewDayWithinSamePeriod`
  - Weekly habit, `TargetCount = 2`.
  - Seed two prior logs on two different days within the current week (target already reached).
  - Call `QuickLogAsync` for a third distinct day still in the same week → assert no new row is added
    (`HabitLogs.Count()` stays at 2) and `CurrentPeriodCount == 2`.
- [ ] `QuickLogAsync_AllowsMultipleDaysWithinSameMonth_UpToTargetCount`
  - Mirror the first Weekly test for `FrequencyType.Monthly` (e.g. `TargetCount = 2`, two distinct days in
    the same month) to cover both non-daily frequencies.
- [ ] Review/keep `QuickLogAsync_IsIdempotentForPeriod` (Daily) unchanged — should still pass since Daily's
      day key and period key are equivalent.

## 6. Verification

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual smoke check: create a Weekly habit with `TargetCount = 3`, Quick Log it "today"; use the API
      directly (or adjust server clock/test) to confirm logging on subsequent days within the same week
      increments `2 / 3 this week` → `3 / 3 this week`, and a 4th attempt in the same week no-ops.
