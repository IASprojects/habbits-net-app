# Implementation Steps — Inactivar y reactivar hábitos

## Problem

The application currently has `Habit.IsArchived`, but that field is reserved for another feature. This feature needs an independent `Habit.IsActive` state so users can inactivate and restore habits without changing `IsArchived` or deleting historical logs.

The WebBlazor client must support the exclusive `🟢 Activos / 🔴 Inactivos` selector, confirmation dialogs, read-only inactive habits, inactive-habit history in the calendar, and protection against editing or Quick Log while inactive.

## Approved decisions

- Use the independent `IsActive` field for this feature. Do not read or modify `IsArchived`.
- Use the exclusive selector `🟢 Activos / 🔴 Inactivos`, with `🟢 Activos` selected by default.
- Show a textual `Inactivar` button below the habit information using the warning color.
- Confirm inactivation with `Confirmo que deseo inactivar`.
- Confirm restoration with `Confirmo que deseo reactivar`.
- Keep inactive habits available in calendar filters so their history remains visible.
- Inactive habits open read-only. Keep `Guardar` visible but disabled.
- Validate ownership before `IsActive` in `QuickLogAsync`; only then perform any count or insert.
- Calculate and show Momentum only in the active-habits view.

---

## 1. Domain and infrastructure

### Habit entity

File: `src/HabitsApp.Core/HabitsApp.Domain/Entities/Habit.cs`

- [ ] Add `public bool IsActive { get; set; } = true;`.
- [ ] Keep `IsArchived` unchanged and independent from `IsActive`.
- [ ] Ensure newly created habits explicitly remain active.

### EF Core configuration and migration

Files:

- `src/HabitsApp.Core/HabitsApp.Infrastructure/Data/ApplicationDbContext.cs`
- `src/HabitsApp.Core/HabitsApp.Infrastructure/Data/Migrations/`

- [ ] Configure the `IsActive` column with a database default of `true` where appropriate.
- [ ] Create a migration that adds `IsActive` as non-nullable with `true` for existing habits.
- [ ] Do not rename, remove, or alter the existing `IsArchived` column or index.
- [ ] Verify the migration leaves all existing habits active.

---

## 2. Application contracts and DTOs

### Service contract

File: `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/IHabitService.cs`

- [ ] Add an activity filter parameter to `GetDashboardAsync`, defaulting to active habits.
- [ ] Prefer a name that reflects the behavior, such as `activeOnly = true`; do not use `archivedOnly`.
- [ ] Add `Task<HabitResult> InactivateAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default);`.
- [ ] Add `Task<HabitResult> ReactivateAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default);`.

### Dashboard DTO

File: `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/HabitDashboardItemDto.cs`

- [ ] Add `public bool IsActive { get; set; }`.
- [ ] Keep the existing progress and streak properties unchanged.
- [ ] Do not add `IsArchived` for this feature.

---

## 3. API habit service

File: `src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs`

### Dashboard filter

- [ ] Change `GetDashboardAsync` to accept the activity filter.
- [ ] Filter with both `h.UserId == userId` and `h.IsActive == activeOnly`.
- [ ] Keep ordering by `CreatedAtUtc`.
- [ ] Keep the current timezone-aware progress and streak calculations for active and inactive lists.
- [ ] Map `IsActive` in `ToDto`.
- [ ] Never use `IsArchived` in this query.

### Inactivation

- [ ] Add `InactivateAsync` as the dedicated service operation behind `DELETE /api/habits/{id}`.
- [ ] Query the habit using `h.Id == habitId && h.UserId == userId`.
- [ ] Set `IsActive = false` and update `UpdatedAtUtc`.
- [ ] Leave `IsArchived` unchanged.
- [ ] Preserve every `HabitLog` row.
- [ ] Make the operation idempotent: if `IsActive` is already `false`, treat it as a successful no-op.
- [ ] Return `204 NoContent` from `DELETE /api/habits/{id}` both when the habit changes from active to inactive and when it is already inactive.
- [ ] When the habit is already inactive, do not update `UpdatedAtUtc`, `IsArchived`, or any `HabitLog` row.

### Restoration and BOLA protection

- [ ] Add `ReactivateAsync` as the dedicated service operation behind `POST /api/habits/{id}/restore`.
- [ ] Resolve the habit with the combined predicate `h.Id == habitId && h.UserId == userId` before changing any field.
- [ ] If the ID belongs to another user, return the existing not-found result (`404`) without revealing the resource exists.
- [ ] Ensure the other user's habit is not modified, even if it is inactive.
- [ ] Set `IsActive = true`, update `UpdatedAtUtc`, and leave `IsArchived` unchanged.
- [ ] Save without changing or deleting historical logs.
- [ ] Return a dashboard DTO with current progress and streak.

### Protect inactive habits

- [ ] Make `UpdateAsync` reject an inactive habit before applying changes.
- [ ] In `QuickLogAsync`, first query the habit with the combined predicate `h.Id == habitId && h.UserId == userId`.
- [ ] If that user-scoped habit lookup returns no result, return `404 NotFound` before reading or evaluating `IsActive`; this prevents leaking whether another user's ID exists or is inactive.
- [ ] Only after ownership is confirmed, validate `habit.IsActive == true`.
- [ ] If the authenticated user owns the habit but it is inactive, return `409 Conflict` as the business error.
- [ ] Only after both validations succeed, calculate period limits, check duplicates, and insert the log.
- [ ] Return a consistent error title/detail for inactive-habit mutations.
- [ ] Keep every state-changing operation scoped to the authenticated `userId`.

---

## 4. API endpoints

File: `src/HabitsApp.Presentation/HabitsApp.Api/Program.cs`

### List endpoint

- [ ] Bind `bool activeOnly = true` in `GET /api/habits`.
- [ ] Pass the value to `habitService.GetDashboardAsync(userId, activeOnly, cancellationToken)`.
- [ ] Keep authorization and unauthorized responses unchanged.
- [ ] Confirm the endpoint never allows a caller to select another `userId`.

### Restore endpoint

- [ ] Add `POST /api/habits/{id}/restore` to the authorized habits group.
- [ ] Resolve the authenticated user ID from the claims principal.
- [ ] Pass both the route `id` and authenticated `userId` to `ReactivateAsync`.
- [ ] Call `ReactivateAsync(userId, id, cancellationToken)` from the restore endpoint.
- [ ] Return `Ok(result.Data)` on successful reactivation.
- [ ] Return `Problem(...)` for service failures, including `404` for another user's ID.
- [ ] Do not add an endpoint path or request field that accepts a caller-supplied owner ID.

### Existing delete endpoint

- [ ] Keep `DELETE /api/habits/{id}` as the inactivation endpoint.
- [ ] Confirm it returns `204 NoContent` after both a first inactivation and a repeated request for an already inactive habit.
- [ ] Return `404` only when the habit does not belong to the authenticated user or does not exist; never return the other user's resource.

---

## 5. WebBlazor models and HTTP service

### Model

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Models/Habits/HabitDashboardItem.cs`

- [ ] Add `bool IsActive`.
- [ ] Do not add or use `IsArchived` for this flow.

### Client contract

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Services/IHabitService.cs`

- [ ] Add the `activeOnly` parameter to `GetDashboardAsync`, defaulting to `true`.
- [ ] Add `ReactivateAsync(Guid habitId, CancellationToken cancellationToken = default)`.

### Client implementation

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Services/HabitService.cs`

- [ ] Keep `GET /api/habits` as the default active request.
- [ ] Request inactive habits with the agreed query value, for example `?activeOnly=false`.
- [ ] Add `ReactivateAsync` calling `POST /api/habits/{habitId}/restore`.
- [ ] Preserve existing API error handling and deserialize the restored dashboard item.

---

## 6. Habits page, selector, and Momentum

Files:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor.cs`

- [ ] Add the exclusive selector `🟢 Activos / 🔴 Inactivos`.
- [ ] Initialize the selector to `🟢 Activos`.
- [ ] Add clearly named state for the selected activity view, such as `ActiveOnly`.
- [ ] Reload using `GetDashboardAsync(activeOnly: ...)` when the selection changes.
- [ ] Keep the selected option when an operation fails.
- [ ] Calculate `TotalCount`, `CompletedCount`, and `MomentumPercent` only from active habits.
- [ ] Render the Momentum panel only when `ActiveOnly` is `true`.
- [ ] Use a distinct empty state for active and inactive lists.
- [ ] Reload the current list after inactivation or restoration.
- [ ] Ensure inactivation removes the item from `🟢 Activos` and restoration removes it from `🔴 Inactivos`.

---

## 7. Cards, confirmations, and form state

### Habit card

Files:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor.cs`

- [ ] Add an `OnInactivate` callback for active habits.
- [ ] Render the textual `Inactivar` button below the habit information with warning styling.
- [ ] Render the button only when `Habit.IsActive` is `true`.
- [ ] Do not show Quick Log for inactive habits.
- [ ] Keep Edit available so inactive habits can be inspected read-only.

### Confirmation flow

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor.cs`

- [ ] Track the habit pending inactivation or restoration.
- [ ] Show a confirmation dialog before each operation.
- [ ] Use `Confirmo que deseo inactivar` and `Confirmo que deseo reactivar` exactly.
- [ ] Keep Cancel and dialog close side-effect free.
- [ ] Disable repeated submissions while the request is running.
- [ ] Reload the current activity view after success and show API errors on failure.

### Dialog component

- [ ] Reuse an existing confirmation component if available; otherwise create a focused component under `Components`.
- [ ] Keep it keyboard and screen-reader accessible.
- [ ] Preserve warning styling for the inactivation action.

### Read-only form and restoration

Files:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitFormModal.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitFormModal.razor.cs`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor.cs`

- [ ] Derive the read-only state from `Habit?.IsActive == false`.
- [ ] Disable or make read-only title, description, color, frequency, and target controls for inactive habits.
- [ ] Keep the `Guardar` button visible but disabled when the habit is inactive.
- [ ] Prevent the form from invoking the update callback for an inactive habit.
- [ ] Show `Reactivar` only for inactive habits.
- [ ] Route `Reactivar` through the page so it uses the confirmation dialog and user-scoped API call.
- [ ] Keep the close action available in read-only mode.
- [ ] Ensure correct initialization for new, active, and inactive habits.

---

## 8. Calendar history

Files to inspect and update as needed:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Calendar.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Calendar.razor.cs`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Services/HabitService.cs`

- [ ] Ensure calendar habit filters include inactive habits.
- [ ] Do not delete or modify `HabitLog` rows during inactivation or restoration.
- [ ] Keep existing calendar date range and timezone behavior unchanged.
- [ ] Verify selecting an inactive habit displays its historical entries.
- [ ] Keep the habits-page activity selector independent from calendar filter state.

---

## 9. Tests

### Application / API

Add or update focused tests in `tests/HabitsApp.Application.Tests/`:

- [ ] Dashboard returns `IsActive = true` habits by default.
- [ ] The inactive filter returns only `IsActive = false` habits.
- [ ] Dashboard never returns another user's habits.
- [ ] The migration gives existing habits `IsActive = true`.
- [ ] Inactivation sets `IsActive = false`, preserves logs, and does not change `IsArchived`.
- [ ] Repeating `DELETE /api/habits/{id}` for an already inactive habit returns `204 NoContent` and does not change `UpdatedAtUtc`, `IsArchived`, or logs.
- [ ] Restoration sets `IsActive = true`, preserves logs, and does not change `IsArchived`.
- [ ] An inactive habit cannot be updated.
- [ ] `QuickLogAsync` performs the user-scoped habit lookup before the `IsActive` check.
- [ ] `QuickLogAsync` returns `404 NotFound` for an unknown or another user's habit without revealing its active state.
- [ ] `QuickLogAsync` returns `409 Conflict` for an owned inactive habit and performs no period count, duplicate check, or insert.
- [ ] `ReactivateAsync` returns `404` when the requested habit belongs to another user.
- [ ] The BOLA test verifies the other user's habit remains unchanged, including `IsActive`, `IsArchived`, and logs.
- [ ] Restoration returns current progress and streak without changing historical logs.

### Frontend behavior

- [ ] The active option is selected initially.
- [ ] Switching to `🔴 Inactivos` loads only inactive habits.
- [ ] Momentum is visible and calculated only in `🟢 Activos`.
- [ ] Canceling inactivation does not call the API.
- [ ] Confirming inactivation removes the habit from the active list.
- [ ] Inactive habits open with all editable controls read-only.
- [ ] `Guardar` remains visible and disabled for inactive habits.
- [ ] Canceling restoration does not call the API.
- [ ] Confirming restoration returns the habit to the active list.
- [ ] Inactive habits remain available in the calendar filter.
- [ ] API errors and loading states render correctly.

---

## 10. Verification

- [ ] Run `dotnet build HabitsApp.slnx` and confirm there are no new warnings or errors.
- [ ] Run `dotnet test`.
- [ ] Verify the migration adds `IsActive = true` to existing habits and leaves `IsArchived` unchanged.
- [ ] Create a habit and verify it appears under `🟢 Activos` and contributes to Momentum.
- [ ] Inactivate it and verify it disappears from `🟢 Activos` and Momentum recalculates.
- [ ] Repeat `DELETE /api/habits/{id}` for the inactive habit and verify it still returns `204 NoContent` without changing its timestamp, archive state, or logs.
- [ ] Select `🔴 Inactivos`, open the habit, and verify fields are read-only and `Guardar` is disabled.
- [ ] Confirm `QuickLog` does not create a log for an inactive habit.
- [ ] Verify the inactive habit and its historical logs remain available in the calendar filter.
- [ ] Restore the habit and verify it returns to `🟢 Activos` and contributes to Momentum again.
- [ ] Attempt `POST /api/habits/{otherUserHabitId}/restore` as another user and verify `404` with no state or log changes.
- [ ] Verify the flow at mobile and desktop viewport sizes.
