# Implementation Steps — Mover `Inactivar` al modal de edición

## Problem

Hoy la acción `Inactivar` vive como un botón de texto independiente debajo de la información de cada
hábito en la tarjeta (`HabitCard.razor:44-47`). Esto dispersa la gestión del hábito: para inactivar hay que
ir a la tarjeta; para editar, al modal. El objetivo es concentrar todas las acciones de gestión del hábito
en un solo lugar: el modal **Edit Habit**.

La propuesta es que la tarjeta conserve únicamente **Editar** y **Quick Log**, y que el footer del modal de
edición de un hábito activo muestre `Cancel · Inactivate · Save` (inglés), reutilizando la confirmación
existente "Confirmo que deseo inactivar".

Es un cambio puramente de interfaz: el endpoint, la confirmación, la preservación de historial y la
idempotencia permanecen intactos.

## Goal

- La tarjeta muestra solo **Editar** y **Quick Log** (más el icono de completado en su caso).
- Al abrir un hábito activo en el modal **Edit Habit**, el footer muestra `Cancel · Inactivate · Save`
  (en inglés, como el resto de la UI).
- `Inactivate` abre la confirmación existente "Confirmo que deseo inactivar"; al confirmar con éxito se cierran
  confirmación y modal, se recarga la lista activa y el hábito desaparece de ella.
- Si se cancela la confirmación, el usuario vuelve al modal de edición exactamente como estaba.
- Para hábitos inactivos se conserva el comportamiento actual: modal de solo lectura, `Guardar` deshabilitado
  y `Reactivar` disponible.

## Approved decisions

- `HabitCard` conserva únicamente **Editar** y **Quick Log**; se elimina el botón `Inactivar` y su callback
  `OnInactivate`.
- `Habits.razor` deja de pasar `OnInactivate` a `HabitCard` y lo pasa a `HabitFormModal`.
- `HabitFormModal` muestra el botón `Inactivate` (estilo warning) solo cuando se edita un hábito activo
  (`Habit is not null && !IsReadOnly`).
- Orden del footer del modal de un hábito activo: `Cancel` · `Inactivate` (warning, `btn-warning`) · `Save`.
  El footer completo queda en inglés, coherente con la UI existente (`Cancel`/`Save`); la etiqueta del botón
  es `Inactivate`, no `Inactivar`.
- `Habits.razor.cs` reutiliza `RequestInactivate` y `HandleInactivateConfirmed`; al confirmar correctamente,
  además de refrescar la lista, cierra el modal.
- `app.css`: se retiran los estilos exclusivos de `.habit-card__inactivate` y se reutiliza la clase existente
  `.btn-warning` para el botón `Inactivate` del modal.
- Mientras está abierta la confirmación de inactivar, se deshabilitan los controles del modal de edición
  subyacente (nuevo parámetro `ControlsDisabled` en `HabitFormModal`, enlazado a
  `ShowInactivateConfirm || IsConfirmBusy`). No se implementa focus trap en `ConfirmDialog`.
- El `<ConfirmDialog>` de inactivación no cambia: título "Inactivate habit", mensaje
  "Confirmo que deseo inactivar" y `ConfirmLabel` "Inactivar", tal como se aprobó en la feature 11.
- El endpoint, la confirmación, la preservación de historial y la idempotencia no se tocan.

---

## 1. HabitCard — retirar `Inactivar`

Files:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor.cs`

- [ ] Eliminar el bloque `@if (Habit.IsActive) { <button class="habit-card__inactivate" ...>Inactivar</button> }`
      de `HabitCard.razor:44-47`.
- [ ] Retirar el parámetro `EventCallback<HabitDashboardItem> OnInactivate` de `HabitCard.razor.cs:19-20`.
- [ ] Comprobar que quedan intactos el botón **Editar** (`HabitCard.razor:22`), **Quick Log**
      (`HabitCard.razor:36`) y el icono de completado (`HabitCard.razor:30-32`).

## 2. HabitFormModal — añadir `Inactivar` al footer

Files:

- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitFormModal.razor`
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitFormModal.razor.cs`

- [ ] Añadir el parámetro `EventCallback<HabitDashboardItem> OnInactivate` junto a los callbacks existentes
      (`HabitFormModal.razor.cs:38-45`).
- [ ] Añadir el parámetro `public bool ControlsDisabled { get; set; }` para deshabilitar los controles del
      modal mientras la confirmación está abierta.
- [ ] Añadir el método `private async Task HandleInactivateAsync() => await OnInactivate.InvokeAsync(Habit);`
      junto a `HandleReactivateAsync` (`HabitFormModal.razor.cs:103-104`).
- [ ] En el footer (`HabitFormModal.razor:66-73`), entre `Cancel` y el botón de submit, renderizar el botón
      `Inactivate` solo cuando `Habit is not null && !IsReadOnly` (se está editando un hábito activo), con
      clase `btn-warning`, `type="button"`, `@onclick="HandleInactivateAsync"` y
      `disabled="@(IsSaving || ControlsDisabled)"`.
- [ ] Mantener el orden del footer para hábito activo: `Cancel` · `Inactivate` (warning) · `Save`.
- [ ] Añadir `ControlsDisabled` al `disabled` del botón `Cancel` (`HabitFormModal.razor:67`) y del botón
      `Save` (`HabitFormModal.razor:72`): `disabled="@(IsSaving || ControlsDisabled)"` y
      `disabled="@(IsSaving || IsReadOnly || ControlsDisabled)"` respectivamente.
- [ ] Verificar que el botón `Inactivate` NO aparece en modo "New Habit" (`Habit is null`) ni en modo solo
      lectura (hábito inactivo), donde se conservan `Cancel` + `Reactivar` + `Save` (deshabilitado).
      (`HabitFormModal.razor:68-72`).

## 3. Habits.razor — recablear callbacks

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor`

- [ ] Quitar `OnInactivate="@RequestInactivate"` del elemento `<HabitCard ... />` (`Habits.razor:66`).
- [ ] Añadir `OnInactivate="@RequestInactivate"` al elemento `<HabitFormModal ... />` (`Habits.razor:77-83`).
- [ ] Mantener el `<ConfirmDialog>` de inactivación (`Habits.razor:85-93`) sin cambios.

## 4. Habits.razor.cs — reutilizar el flujo de confirmación y cerrar el modal

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor.cs`

- [ ] Reutilizar `RequestInactivate` (`Habits.razor.cs:181-187`) sin cambios: abre el diálogo de confirmación
      con `ShowModal` aún `true`, de modo que el modal de edición permanece abierto debajo.
- [ ] En `CancelConfirmation` (`Habits.razor.cs:198-210`) no hay cambios: al cancelar se cierra solo el
      diálogo de confirmación y el usuario vuelve al modal de edición exactamente como estaba.
- [ ] En `HandleInactivateConfirmed` (`Habits.razor.cs:212-244`), tras el éxito de `HabitService.InactivateAsync`:
      añadir `ShowModal = false` (además del `ShowInactivateConfirm = false` y `PendingActionHabit = null`
      existentes) y limpiar `EditingHabit` antes de `LoadHabitsAsync()`, de modo que el hábito desaparezca de
      la lista activa.
- [ ] Mantener intactos `RequestReactivate` (`Habits.razor.cs:189-196`) y `HandleRestoreConfirmed`
      (`Habits.razor.cs:246-278`).
- [ ] Verificar que mientras el `<ConfirmDialog>` está abierto, su `modal-overlay` bloquea la interacción con
      el modal de edición subyacente (evita acciones duplicadas); `IsConfirmBusy` ya deshabilita los botones
      del diálogo (`ConfirmDialog.razor:20-21`).

## 5. app.css — retirar estilos exclusivos y reutilizar warning de modal

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/wwwroot/css/app.css`

- [ ] Eliminar los bloques `.habit-card__inactivate` y `.habit-card__inactivate:hover`
      (`app.css:1503-1520`).
- [ ] Reutilizar la clase existente `.btn-warning` (`app.css:364-370`) para el botón `Inactivar` del modal.
- [ ] Verificar el espaciado dentro de `.modal__footer` (`app.css:1626`) con el nuevo botón (tres botones:
      `Cancelar`, `Inactivar`, `Guardar`) en vista móvil y escritorio; ajustar solo si es necesario.

## 6. Verification

- [ ] `dotnet build` — 0 warnings / 0 errors.
- [ ] `dotnet test`.
- [ ] Smoke manual: abrir un hábito activo en **Edit Habit** → el footer muestra
      `Cancelar · Inactivar (warning) · Guardar` y no hay botón `Inactivar` en la tarjeta.
- [ ] Pulsar `Inactivar` → se abre "Confirmo que deseo inactivar"; cancelar → el modal de edición queda
      exactamente como estaba y no se llama a la API.
- [ ] Confirmar con éxito → se cierran confirmación y modal, la lista activa se recarga y el hábito
      desaparece.
- [ ] El modal "New Habit" no muestra `Inactivar`.
- [ ] Un hábito inactivo abre en solo lectura: `Guardar` deshabilitado y `Reactivar` disponible.
- [ ] Comprobar el flujo en tamaños de pantalla móvil y escritorio.