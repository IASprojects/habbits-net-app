# Implementation Steps — Clasificación y orden de hábitos por momento del día

## Problem

Los hábitos no tenían clasificación por momento del día (mañana/tarde/noche). El dashboard mostraba los hábitos ordenados por fecha de creación, sin adaptarse al momento actual. Se necesita clasificar cada hábito como Mañana / Tarde / Noche / Cualquier momento del día, detectar visualmente el momento actual y reordenar la lista según el periodo vigente. Además, la clasificación debe ser opcional para no romper hábitos existentes.

## Goal

- Cada hábito puede tener una clasificación de periodo (`Morning, Afternoon, Night, Any`), opcional; si no se define, se trata como `Any`.
- El dashboard detecta el momento actual según la hora local del usuario y muestra una insignia, aunque no haya hábitos.
- Los hábitos se reordenan al cargar la lista: los del momento actual primero, luego `Any`, luego los demás ordenados por creación ascendente.
- El indicador visual: los hábitos del periodo actual llevan fondo ligeramente resaltado; `Any` lleva chip con estilo secundario distinguible, nunca el énfasis completo.
- El formulario de creación/edición incluye un selector de periodo.
- Los hábitos `Any` permanecen siempre visibles justo después de los prioritarios del periodo.
- El momento actual se resuelve al cargar/re-cargar el dashboard; en v1 no hay temporizador que reordene al cruzar las 05:00/12:00/18:00.

## Approved decisions

- `DayPeriod` enum en `HabitsApp.Domain.Enums` con `Morning, Afternoon, Night, Any` (noche = `Night`, rango 18:00–04:59).
- Columna `Period` nullable `DayPeriod?` en la entidad `Habit`; `null` → se normaliza a `Any` antes de calcular el rango de orden.
- Lógica de periodo en `HabitPeriodCalculator` (métodos `GetDayPeriod`, `GetDisplayOrder`); la normalización `null → Any` se aplica en el servicio al ordenar.
- API: `GetDashboardAsync` devuelve una respuesta raíz `DashboardResponseDto { CurrentPeriod, Habits[] }`. `CurrentPeriod` NO se repite en cada ítem; el ítem conserva solo `Period`.
- Orden: `OrderBy(rango de periodo).ThenBy(h => h.CreatedAtUtc)` con `CreatedAtUtc` **ascendente** (consistente con la consulta actual, `HabitService.cs:25`).
- Frontend: `HabitFormModal` con selector segmentado de periodo; `HabitCard` recibe `CurrentPeriod` por parámetro, muestra chip de periodo y la clase `habit-card--emphasized` solo cuando el periodo efectivo del hábito coincide con el actual (nunca para `Any`).
- UI en inglés para consistencia con el selector Frequency: “Morning”, “Afternoon”, “Night”, “Any time”. Insignia: “Good Morning” / “Good Afternoon” / “Good Evening” (el periodo `Night` usa “Good Evening” en la insignia, más natural en inglés; “Good Night” queda reservado para despedirse).
- Reloj inyectable: `HabitService` recibe `TimeProvider` (`TimeProvider.System` en la API, reloj falso en los tests) para resolver “ahora” de forma determinista.
- Validación de `Period` en create/update: el `JsonStringEnumConverter` se registra con `allowIntegerValues: false` y el servicio valida `Enum.IsDefined(...)`; tanto `"Midday"` como `999` devuelven `400`.
- Migración EF `AddHabitPeriod` añade columna nullable; `null` en registros existentes.
- Pruebas unitarias de `GetDayPeriod` boundaries, `GetDisplayOrder` por cada periodo y pruebas de integración del servicio sobre el orden final devuelto.

---

## 1. Dominio — enum y entidad

Archivos:
- `src/HabitsApp.Core/HabitsApp.Domain/Enums/DayPeriod.cs`: crear nuevo archivo con enum `DayPeriod { Morning, Afternoon, Night, Any }`.
- `src/HabitsApp.Core/HabitsApp.Domain/Entities/Habit.cs`: añadir `public DayPeriod? Period { get; set; }` (nullable; `null` → `Any`).

---

## 2. Aplicación — calculadora de periodo

Archivo:
- `src/HabitsApp.Core/HabitsApp.Application/Services/HabitPeriodCalculator.cs`:
  - `GetDayPeriod(TimeZoneInfo tz, DateTime utcNow)`: devuelve `DayPeriod.Morning` si hora local 05:00–11:59; `Afternoon` si 12:00–17:59; `Night` si 18:00–04:59 (cruza medianoche). Nunca devuelve `Any`.
  - `GetDisplayOrder(DayPeriod current)`: retorna `IReadOnlyList<DayPeriod>` según la tabla explícita. `GetDayPeriod` nunca devuelve `Any`, así que solo existen **3 filas**:

    ```text
    Morning:   [Morning, Any, Afternoon, Night]
    Afternoon: [Afternoon, Any, Morning, Night]
    Night:     [Night, Any, Afternoon, Morning]
    ```

  - `GetDisplayOrder(DayPeriod.Any)`: lanza `ArgumentOutOfRangeException` (evita una lista con `Any` repetido o un orden silenciosamente inválido).
  - `GetDisplayRank(DayPeriod? period, DayPeriod current)`: helper explícito que normaliza `period ?? DayPeriod.Any` y devuelve el índice dentro de `GetDisplayOrder(current)`. Esta es la normalización `null → Any` que debe aplicarse antes de ordenar.

---

## 3. Aplicación — DTOs

Archivos:
- `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/CreateHabitDto.cs`: añadir `public DayPeriod? Period { get; set; }`.
- `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/UpdateHabitDto.cs`: añadir `public DayPeriod? Period { get; set; }`.
- `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/HabitDashboardItemDto.cs`: añadir solo `public DayPeriod? Period { get; set; }` (sin `CurrentPeriod`; este vive en la respuesta raíz).
- `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/DashboardResponseDto.cs`: crear con `public DayPeriod CurrentPeriod { get; set; }` y `public IReadOnlyList<HabitDashboardItemDto> Habits { get; set; } = [];`.
- `src/HabitsApp.Core/HabitsApp.Application/Contracts/Habits/IHabitService.cs`: cambiar la firma de `GetDashboardAsync` para devolver `Task<DashboardResponseDto>` en lugar de `Task<IReadOnlyList<HabitDashboardItemDto>>` (línea 5).

---

## 4. API — servicio de hábitos

Archivos:
- `src/HabitsApp.Presentation/HabitsApp.Api/Services/HabitService.cs`:
  - Inyectar `TimeProvider` en el constructor (junto a `ApplicationDbContext` y `ILogger<HabitService>`). `TimeProvider.GetUtcNow()` devuelve `DateTimeOffset`, pero el código existente (entidad, calculadora y métodos de ventana) trabaja con `DateTime`, así que debe usarse `.UtcDateTime` en todos los flujos que hoy usan `DateTime.UtcNow` (GetDashboard/QuickLog/Inactivate/Reactivate) y en los campos de persistencia:
    - `var now = _time.GetUtcNow().UtcDateTime;` para la hora de referencia de ventanas/streak/periodo.
    - `habit.CreatedAtUtc = _time.GetUtcNow().UtcDateTime;` y `habit.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;` para los timestamps de auditoría.
  - `CreateAsync`: mapear `Period = dto.Period`; antes de persistir, validar `dto.Period is not null && !Enum.IsDefined(typeof(DayPeriod), dto.Period.Value)` → `400`.
  - `UpdateAsync`: mapear `habit.Period = dto.Period`; misma validación `Enum.IsDefined` → `400`.
  - `GetDashboardAsync`: resolver `now` con `_time.GetUtcNow().UtcDateTime` y `currentPeriod` con `HabitPeriodCalculator.GetDayPeriod(tz, now)` **antes** de consultar hábitos; eliminar la salida temprana de lista vacía (líneas 28–31) y devolver siempre `DashboardResponseDto { CurrentPeriod, Habits = items }`, incluido el caso `Habits` vacío. Ordenar con `OrderBy(h => GetDisplayRank(h.Period, currentPeriod)).ThenBy(h => h.CreatedAtUtc)` (ascendente). `habitIds` para logs se calcula tras el orden; aplicar el orden sobre la colección en memoria (la consulta base solo filtra por `UserId`/`IsActive`).
  - `ToDto(Habit habit, int currentPeriodCount, int streak)`: añadir `Period = habit.Period` y **no** `CurrentPeriod`. Esta misma sobrecarga sirve para Create/Update/QuickLog/Inactivate/Reactivate/Archive; esos flujos no resuelven zona horaria ni periodo (el frontend re-carga el dashboard con `LoadHabitsAsync` tras cada acción).
- `src/HabitsApp.Presentation/HabitsApp.Api/Program.cs`:
  - Cerca de la línea 32 (`AddScoped<IHabitService, HabitService>`): `builder.Services.AddSingleton(TimeProvider.System);`.
  - Línea 94: registrar el conversor como `new JsonStringEnumConverter(allowIntegerValues: false)` para rechazar valores numéricos (`999`) con `400` en lugar de aceptarlos como enum no definido.

---

## 5. Migración EF

Comando:
```bash
dotnet ef migrations add AddHabitPeriod --project src/HabitsApp.Core/HabitsApp.Infrastructure --startup-project src/HabitsApp.Presentation/HabitsApp.Api
```
Genera `2026..._AddHabitPeriod.cs` con columna `Period` nullable (`int` sobre `Habits`, nombre del enum → `Night` para el nuevo valor). No toca datos existentes; los registros actuales quedan como `NULL`.

---

## 6. Frontend — modelos y formulario

Archivos:
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Models/Habits/HabitDashboardItem.cs`: añadir `public string? Period { get; set; }` (sin `CurrentPeriod`).
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Models/Habits/HabitDashboardResponse.cs`: crear con `public string CurrentPeriod { get; set; } = "Morning";` y `public List<HabitDashboardItem> Habits { get; set; } = [];`.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Models/Habits/CreateHabitRequest.cs` y `UpdateHabitRequest.cs`: añadir `public string? Period { get; set; }`.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Models/Habits/HabitFormModel.cs`: añadir `public string Period { get; set; } = "Any";`.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Services/IHabitService.cs` y `HabitService.cs`: `GetDashboardAsync` devuelve `Task<HabitDashboardResponse>` y deserializa la respuesta raíz.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitFormModal.razor`/`.cs`:
  - array `Periods = ["Morning", "Afternoon", "Night", "Any"]`.
  - selector segmentado estilo Frequency, con `SelectPeriod(string)` y `InitializeForm` que setea `Period = Habit.Period ?? "Any"`.
  - disabled en modo read‑only.

---

## 7. Frontend — dashboard y tarjeta

Archivos:
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor.cs`:
  - Guardar `DashboardResponse` completo (p. ej. `Data`) y exponer `CurrentPeriodBadge => Data?.CurrentPeriod ?? "Morning"` — nunca derivado del primer ítem.
  - Pasar `CurrentPeriod="@Data.CurrentPeriod"` a cada `HabitCard`.
  - `HandleModalSave`: `Period = model.Period == "Any" ? null : model.Period;` en `CreateHabitRequest`/`UpdateHabitRequest`.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Habits.razor`: mostrar la insignia `.period-badge` en el header (icono material + etiqueta), fuera del listado.
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Components/Habits/HabitCard.razor`/`.cs`:
  - `[Parameter] public string CurrentPeriod { get; set; } = "Morning";`.
  - clase `habit-card--emphasized` solo cuando el periodo **efectivo** coincide con el actual: `IsCurrentPeriod => Habit.Period is not null && Habit.Period != "Any" && Habit.Period == CurrentPeriod`.
  - chip `.habit-card__period` con el ícono del periodo (`wb_sunny`, `wb_twilight`, `nightlight`, `schedule` para Any); los chips `Any` usan la clase secundaria `.habit-card__period--any`.

---

## 8. Frontend — estilos

Archivo:
- `src/HabitsApp.Presentation/HabitsApp.WebBlazor/wwwroot/css/app.css`:
  - `.habit-card--emphasized`: fondo resaltado con variables de token (soporte dark/light). Solo hábitos del periodo actual.
  - `.habit-card__period`: estilos del chip de periodo al lado del chip de frecuencia.
  - `.habit-card__period--any`: variante secundaria (contorno sutil/en gris), nunca el énfasis completo.
  - `.period-badge`: insignia en el header del dashboard.

---

## 9. Pruebas

Archivos:
- `tests/HabitsApp.Application.Tests/HabitPeriodCalculatorTests.cs`:
  - Teorías `GetDayPeriod_ReturnsExpectedPeriod` para los bordes: 04:59/05:00, 11:59/12:00, 17:59/18:00, medianoche (Night).
  - Teorías `GetDisplayOrder_ReturnsExpectedOrder` con las 3 filas de la tabla (Morning/Afternoon/Night).
  - Teoría `GetDisplayOrder_DayPeriodAny_Throws`: `GetDisplayOrder(DayPeriod.Any)` lanza `ArgumentOutOfRangeException`.
  - Teoría `GetDisplayRank_NormalizesNullToAny`: `null` mapea al mismo rango que `Any`.
- `tests/HabitsApp.Application.Tests/HabitServiceTests.cs`:
  - Construir `HabitService` con un reloj falso (p. ej. `FakeTimeProvider` de `Microsoft.Extensions.Time.Testing`) para fijar `now` de forma determinista en los tests de orden y zona horaria.
  - Assert `Period` poblado tras `CreateAsync`/`UpdateAsync`; assert create/update con `Period = 999` no definido devuelve `400`.
  - Assert `CurrentPeriod` poblado en la **raíz** del dashboard y `Period` presente en cada ítem.
  - Dashboard vacío: devuelve `DashboardResponseDto` con `Habits` vacío y `CurrentPeriod` correcto según la hora local.
  - Las tres secuencias completas (mañana/tarde/noche): dados hábitos en cada periodo, el orden devuelto coincide con la fila correspondiente de la tabla de la sección 2 (p. ej. con `current = Night`: `Night → Any → Afternoon → Morning`).
  - `Period = null` ordenado como `Any` (mismo rango, mismo desempate).
  - Desempate: dentro de cada categoría, `CreatedAtUtc` ascendente.
  - Zona horaria: con el reloj fijo en `2026-08-20T01:30Z` y `America/New_York`, se espera el periodo nocturno local (`Night`) aunque la hora UTC cruce el día local (estilo `TimeZoneAware_*` existente).
  - Endpoint/serialización: tanto `"Midday"` (string inválida) como `999` (numérico, rechazado por `allowIntegerValues: false`) en `Period` devuelven `400`; `null` y `"Any"` se aceptan.

---

## 10. Verificación

- `dotnet ef migrations add` — generar migración limpia.
- `dotnet ef database update` — aplicar en BD de desarrollo.
- Confirmar en la BD que la columna es nullable y que los registros existentes permanecen como `NULL`.
- Revisar `Down()`, o `dotnet ef migrations remove` tras validar, para probar el rollback.
- `dotnet build` — 0 warnings / 0 errors.
- `dotnet test` — todas las pruebas pasan.
- Smoke manual: crear hábito sin periodo (Any); hábito con Mañana/Tarde/Noche; ver insignia en header; reordenar lista al recargar; dark mode funcional.

---

## 11. Smoke manual detallado

- Abrir dashboard → ver insignia “Good Morning” / “Good Afternoon” / “Good Evening” (periodo `Night`).
- Crear hábito sin periodo → aparece como `Any`, con chip secundario y sin énfasis.
- Editar hábito → asignar Mañana/Tarde/Noche → chip aparece y la tarjeta se resalta solo al corresponder con el momento actual.
- Cambiar la hora del día (simular): recargar dashboard → el reorden y la insignia actualizan según la nueva hora local.
- El periodo NO cambia automáticamente al cruzar las 05:00/12:00/18:00 en pantalla; solo se actualiza al recargar o volver a cargar datos (sin temporizador en v1).

---

## 12. Limpieza

- Verificar que hábitos existentes (sin `Period`) se comportan como `Any` sin errores (orden y sin resalte).
- Confirmar que `dotnet test` incluye todas las nuevas pruebas sin regressions.

---

## 13. Cierre

- Revisar `app.css` con el inspector de temas claros/oscuros.
- Documentar en `AGENTS.md` el nuevo enum, `GetDayPeriod`/`GetDisplayOrder`/`GetDisplayRank` y la respuesta raíz del dashboard si procede.

---

## 14. Plan de ejecución

Seguir este orden para evitar cambios intermedios incompatibles entre API, base de datos y frontend:

1. **Preparar el reloj y pruebas.** Registrar `TimeProvider.System` en la API, inyectarlo en `HabitService` y añadir el reloj falso requerido por los tests. Sustituir los usos afectados de `DateTime.UtcNow` por la hora UTC del reloj.
2. **Crear el modelo de dominio.** Añadir `DayPeriod` y `Habit.Period`. Implementar en `HabitPeriodCalculator` la detección del periodo, la tabla de orden y la normalización/rango de `null` como `Any`.
3. **Actualizar contratos de la API.** Añadir `Period` a los DTOs de creación, actualización e ítem; crear `DashboardResponseDto`; cambiar la firma de `GetDashboardAsync`.
4. **Implementar persistencia y reglas de servicio.** Mapear/validar el periodo en crear y editar, normalizar `Any` si se adopta como valor canónico `null`, y devolver el dashboard ordenado junto con `CurrentPeriod`.
5. **Crear y validar la migración.** Generar `AddHabitPeriod`, revisar `Up()`/`Down()` y aplicarla en la base de desarrollo, comprobando que los hábitos existentes permanecen con `Period = NULL`.
6. **Adaptar el cliente Blazor al nuevo contrato.** Crear los modelos de respuesta y requests con `Period`; actualizar el servicio HTTP y el estado de `Habits.razor.cs` para consumir la respuesta raíz.
7. **Implementar la interfaz.** Añadir el selector en el modal, la insignia global del periodo, los chips de cada hábito y el estilo de énfasis/variante secundaria para `Any`.
8. **Completar pruebas automatizadas.** Cubrir límites horarios, las tres tablas de orden, `null → Any`, desempates, zona horaria, dashboard vacío, creación/edición y validación de valores inválidos.
9. **Verificar de punta a punta.** Ejecutar migración, compilación y pruebas; después realizar el smoke manual en mañana, tarde y noche, incluidos los temas claro y oscuro.
