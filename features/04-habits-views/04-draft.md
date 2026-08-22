# Feature Draft: Habits Dashboard & Tracking Module

## Overview

This feature introduces the main Habits Dashboard (`/habits`) after user authentication. It allows users to track habits with flexible frequencies (Daily, Weekly, Monthly), perform Quick Logs, create/edit habits, and view real-time completion progress while enforcing strict multi-tenant data isolation.

---

## 1. Core Domain Models (`HabitsApp.Domain`)

### Enums

* `FrequencyType`: `Daily`, `Weekly`, `Monthly`

### Entities

* **`Habit`**
* `Guid Id`
* `Guid UserId` (Owner identifier for multi-tenant isolation)
* `string Title` (Required)
* `string? Description`
* `string ColorHex` (Default: `#4F46E5`)
* `FrequencyType Frequency` (Default: `Daily`)
* `int TargetCount` (Default: `1`; e.g., 3 times per week)
* `bool IsArchived` (Default: `false`)
* `DateTime CreatedAtUtc`
* `DateTime? UpdatedAtUtc`
* `ICollection<HabitLog> Logs`


* **`HabitLog`**
* `Guid Id`
* `Guid HabitId`
* `Guid UserId`
* `DateTime CompletedAtUtc`



---

## 2. Infrastructure & Multi-Tenancy (`HabitsApp.Infrastructure`)

* **Global Query Filter:** Apply automatic filtering on `DbContext` level so users can only access their own data:
```csharp
builder.Entity<Habit>().HasQueryFilter(h => h.UserId == _currentUserService.UserId);

```


* **Composite Database Indexes:**
* `Habit`: Index on `(UserId, IsArchived)`
* `HabitLog`: Index on `(HabitId, CompletedAtUtc)`


* **Timezone & Period Windows:** Calculate active evaluation windows based on UTC converted to the user's local timezone (`ApplicationUser.TimeZoneId`):
* *Daily:* Start/end of current local day.
* *Weekly:* Monday 00:00 to Sunday 23:59 local time.
* *Monthly:* First to last day of current local month.



---

## 3. Application Use Cases & DTOs (`HabitsApp.Application`)

### DTOs

* `HabitDashboardItemDto`: `Id`, `Title`, `Description`, `ColorHex`, `Frequency`, `TargetCount`, `CurrentPeriodCount`, `IsCompletedForPeriod`
* `CreateHabitDto`: `Title`, `Description`, `ColorHex`, `Frequency`, `TargetCount`
* `UpdateHabitDto`: `Title`, `Description`, `ColorHex`, `Frequency`, `TargetCount`

### Core Operations

1. `GetDashboardHabitsQuery`: Fetches all active habits (`IsArchived == false`) for the authenticated user, evaluates period logs, and populates `CurrentPeriodCount` and `IsCompletedForPeriod`.
2. `CreateHabitCommand`: Creates a new habit for the current user.
3. `UpdateHabitCommand`: Modifies habit details and updates `UpdatedAtUtc = DateTime.UtcNow`.
4. `QuickLogCommand`: Registers a log entry (`HabitLog`) for the current timestamp. Validates ownership (BOLA prevention) and handles concurrency/idempotency.

---

## 4. API Layer (`HabitsApp.Api`)

Minimal API group mapped under `/api/habits` requiring `[Authorize]`:

* `GET /api/habits`: Returns `IEnumerable<HabitDashboardItemDto>`.
* `POST /api/habits`: Accepts `CreateHabitDto` → `201 Created`.
* `PUT /api/habits/{id:guid}`: Accepts `UpdateHabitDto` → `200 OK` or `404 NotFound`.
* `POST /api/habits/{id:guid}/quick-log`: Triggers a log entry → `200 OK` with updated count.
* `DELETE /api/habits/{id:guid}`: Soft-deletes/archives habit (`IsArchived = true`, `UpdatedAtUtc = DateTime.UtcNow`).

---

## 5. UI & Presentation (`HabitsApp.Web`)

### Navigation Flow

* On successful authentication in `Login.razor`, redirect directly to `/habits`.

### View Layout (`Habits.razor`)

* **Header / Momentum Summary:** Displays overall completion status for the current period (e.g., `% of target habits logged`). Includes **"+ Create Habit"** modal trigger.
* **Habit List / Grid:** Renders cards or rows using `ColorHex`:
* Displays title, target frequency (e.g., `2 / 3 this week`), and progress status.
* **Quick Log Action:** Interactive button to instantly increment count and update state locally.
* **Edit Action:** Opens modal pre-filled with habit data for updating.