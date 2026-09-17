# Draft — Inactivar y reactivar hábitos

## Problema

Actualmente el sistema tiene el campo `IsArchived`, pero ese campo pertenece a otra funcionalidad y no debe utilizarse para controlar el estado activo o inactivo de un hábito. El usuario final tampoco puede gestionar el estado `IsActive` desde la interfaz:

- No existe una vista para consultar únicamente los hábitos inactivos.
- No existe una acción de reactivación.
- La inactivación no tiene confirmación visible para el usuario.
- El formulario de un hábito inactivo no distingue correctamente entre consulta y edición.
- `QuickLog` no valida explícitamente que el hábito esté activo antes de registrar actividad.

## Objetivo

Agregar el campo independiente `IsActive` para permitir que cada usuario pueda inactivar y reactivar sus hábitos, consultar los hábitos inactivos mediante el selector exclusivo `🟢 Activos / 🔴 Inactivos` y conservar su historial.

`IsArchived` no será modificado, reutilizado ni incluido en la lógica de esta feature.

## Comportamiento esperado

### Lista de hábitos

- Por defecto se selecciona `🟢 Activos` y se muestran únicamente los hábitos con `IsActive = true`.
- Mostrar el selector `🟢 Activos / 🔴 Inactivos` como filtro exclusivo: solo una opción puede estar seleccionada.
- Al seleccionar `🟢 Activos`, cargar únicamente hábitos activos.
- Al seleccionar `🔴 Inactivos`, cargar únicamente hábitos con `IsActive = false`.
- El filtro debe ejecutarse en el servidor y respetar siempre el `userId` autenticado.
- Las métricas actuales de Momentum (`TotalCount`, `CompletedCount` y `MomentumPercent`) se calculan únicamente con la lista de hábitos activos.
- Ocultar completamente el panel Momentum cuando esté seleccionada la vista `🔴 Inactivos`.
- Si no hay resultados, mostrar un estado vacío apropiado para la vista seleccionada.

### Inactivar un hábito

- Cada hábito activo debe mostrar debajo de su información un botón textual `Inactivar` con color de advertencia.
- Al pulsar la acción, mostrar un cuadro de diálogo de confirmación.
- El hábito solo se inactiva después de confirmar mediante `Confirmo que deseo inactivar`.
- Cancelar el diálogo no debe modificar datos.
- Después de inactivar, actualizar la lista visible sin recargar toda la aplicación.
- El hábito inactivo debe dejar de aparecer entre los hábitos activos y quedar disponible para consultar su historial.
- La inactivación debe conservar los registros históricos del hábito.

### Consultar un hábito inactivo

- Al abrir un hábito inactivo, el formulario debe mostrar todos sus datos.
- Los campos deben ser `readonly` o estar deshabilitados:
  - Título
  - Descripción
  - Color
  - Frecuencia
  - Cantidad objetivo
- El botón `Guardar` debe permanecer visible, pero deshabilitado mientras el hábito esté inactivo.
- No debe ser posible enviar cambios de un hábito inactivo.
- Mostrar un botón `Reactivar`.
- La reactivación debe requerir confirmación mediante `Confirmo que deseo reactivar`.
- Después de reactivar, actualizar la lista y devolver el hábito a la vista de hábitos activos.

## Reglas del backend

- Un usuario solo puede consultar, inactivar o reactivar sus propios hábitos.
- `IsActive` es el único campo de estado controlado por esta feature.
- `IsArchived` debe permanecer sin cambios y no debe participar en filtros, inactivación, reactivación, edición ni Quick Log.
- Un hábito inactivo no debe poder modificarse mediante `PUT`.
- `QuickLogAsync` debe aplicar estas validaciones en orden estricto: primero buscar con `Id == habitId && UserId == userId`; si no existe, devolver `404`; después validar `IsActive == true`; si el hábito pertenece al usuario, pero está inactivo, devolver `409 Conflict` como error de negocio; solo después calcular límites, verificar duplicados e insertar el log.
- Un hábito inactivo no debe aceptar `QuickLog`.
- Reactivar un hábito debe establecer `IsActive = true` y actualizar `UpdatedAtUtc`.
- Inactivar un hábito activo debe establecer `IsActive = false` y actualizar `UpdatedAtUtc`.
- Si el hábito ya tiene `IsActive = false`, la solicitud de inactivación será un no-op idempotente: responderá `204 NoContent` sin modificar `UpdatedAtUtc`, `IsArchived` ni sus registros históricos.
- Las operaciones deben ser idempotentes cuando sea razonable: inactivar un hábito ya inactivo debe responder `204 NoContent` sin modificarlo nuevamente, y reactivar uno ya activo no debe crear datos duplicados.

## Propuesta de API

### Datos y listado

Agregar `IsActive` a `HabitDashboardItemDto` y a su modelo equivalente en Blazor.

Extender `GET /api/habits` con un filtro:

- `GET /api/habits` → hábitos con `IsActive = true`.
- `GET /api/habits?activeOnly=false` → hábitos con `IsActive = false`.

El parámetro puede conservar otro nombre si el proyecto ya tiene una convención establecida, pero no debe filtrar mediante `IsArchived`.

### Inactivar

Conservar el endpoint actual:

- `DELETE /api/habits/{id}` → establece `IsActive = false`.
- Si el hábito ya tiene `IsActive = false`, debe responder igualmente `204 NoContent` sin modificar `UpdatedAtUtc`, `IsArchived` ni sus registros históricos.

### Reactivar

Agregar un endpoint explícito:

- `POST /api/habits/{id}/restore` → establece `IsActive = true`.

El backend debe resolver el hábito mediante la combinación `Id == id && UserId == userId` antes de cambiar cualquier campo. Si el hábito existe, pero pertenece a otro usuario, debe responder `404` sin revelar su existencia y sin modificar datos. Esta validación es obligatoria para prevenir BOLA (Broken Object Level Authorization).

`DELETE /api/habits/{id}` debe devolver `204 NoContent` tanto al inactivar por primera vez como al repetir la misma operación sobre un hábito ya inactivo. `POST /api/habits/{id}/restore` devuelve el hábito actualizado o un error `404` si no pertenece al usuario autenticado.

## Cambios previstos por capa

### Domain / Infrastructure

- [ ] Agregar `public bool IsActive { get; set; } = true;` a `Habit`.
- [ ] Configurar el valor predeterminado para nuevos hábitos como `true`.
- [ ] Crear una migración que agregue `IsActive` con valor `true` para los hábitos existentes.
- [ ] No modificar la columna ni la configuración de `IsArchived`.

### Application

- [ ] Agregar un parámetro de filtro basado en actividad a `IHabitService.GetDashboardAsync`.
- [ ] Agregar `InactivateAsync` y `ReactivateAsync` a `IHabitService`.
- [ ] Agregar `IsActive` a `HabitDashboardItemDto`.
- [ ] Revisar los mensajes y resultados para operaciones sobre hábitos inactivos.

### API

- [ ] Cambiar `HabitService.GetDashboardAsync` para filtrar por `IsActive` y `UserId`, según la opción seleccionada.
- [ ] Implementar `HabitService.InactivateAsync` usando `IsActive`.
- [ ] Implementar `HabitService.ReactivateAsync` usando `IsActive`.
- [ ] Hacer `InactivateAsync` idempotente: un hábito ya inactivo debe responder con éxito y el endpoint debe devolver `204 NoContent`.
- [ ] Actualizar `UpdatedAtUtc` únicamente cuando el estado cambie de activo a inactivo.
- [ ] En el caso ya inactivo, no actualizar `UpdatedAtUtc`, no tocar `IsArchived` y no modificar `HabitLog`.
- [ ] Rechazar `UpdateAsync` cuando el hábito tenga `IsActive = false`.
- [ ] En `QuickLogAsync`, ejecutar primero la consulta combinada `h.Id == habitId && h.UserId == userId`; devolver `404` si no existe, sin evaluar ni revelar el estado de un ID ajeno.
- [ ] Después de confirmar la pertenencia, validar `IsActive == true`; devolver `409 Conflict` si el hábito propio está inactivo.
- [ ] Ejecutar conteos, verificación de duplicados e inserción únicamente después de completar esas dos validaciones.
- [ ] Mapear `IsActive` desde `GET /api/habits`.
- [ ] Agregar `POST /api/habits/{id}/restore`.
- [ ] En `ReactivateAsync`, consultar siempre con `h.Id == habitId && h.UserId == userId` antes de modificar `IsActive`.
- [ ] Si el ID pertenece a otro usuario, devolver `404` sin modificar el hábito y sin revelar que existe.
- [ ] Cubrir `ReactivateAsync` y su endpoint contra BOLA con una prueba de aislamiento por usuario.
- [ ] Mantener los hábitos inactivos disponibles en los filtros del calendario para consultar su historial.
- [ ] Mantener autorización y aislamiento por usuario en todas las operaciones.

### WebBlazor

- [ ] Extender el modelo `HabitDashboardItem` con `IsActive`.
- [ ] Extender `IHabitService` y el servicio HTTP con el filtro de actividad y la reactivación.
- [ ] Agregar el selector `🟢 Activos / 🔴 Inactivos` en `Habits.razor`.
- [ ] Cargar la lista apropiada cuando cambie la opción seleccionada.
- [ ] Mostrar el panel Momentum únicamente cuando la opción seleccionada sea `🟢 Activos`.
- [ ] Agregar el botón textual `Inactivar`, debajo de la información del hábito y con color de advertencia, en `HabitCard`.
- [ ] Crear o reutilizar un diálogo de confirmación visual consistente con la aplicación.
- [ ] Adaptar `HabitFormModal` para soportar modo de solo lectura cuando `IsActive = false`.
- [ ] Mantener visible el botón `Guardar`, pero deshabilitado para hábitos inactivos.
- [ ] Mostrar `Reactivar` únicamente para hábitos inactivos.
- [ ] Mostrar errores de API y estados de carga durante inactivación/reactivación.

### Estilos y accesibilidad

- [ ] Mantener los estilos visuales actuales de tarjetas, modales y botones.
- [ ] El selector `🟢 Activos / 🔴 Inactivos` debe tener etiqueta visible y estado accesible.
- [ ] El botón `Inactivar` debe conservar su texto visible y color de advertencia.
- [ ] El botón `Guardar` deshabilitado debe seguir siendo legible y comunicar su estado.
- [ ] El diálogo debe poder cancelarse y cerrarse de forma clara.
- [ ] Las acciones con iconos deben conservar `title` y `aria-label`.
- [ ] Los campos deshabilitados deben seguir siendo legibles en tema claro y oscuro.
- [ ] La vista debe funcionar en móvil, tablet y escritorio.

## Calendario e historial

- [ ] Los hábitos inactivos deben seguir apareciendo en los filtros del calendario.
- [ ] La inactivación y reactivación no deben eliminar ni modificar `HabitLog`.
- [ ] Seleccionar un hábito inactivo en el calendario debe mostrar sus registros históricos.
- [ ] El estado `IsActive` no debe ocultar registros históricos ya existentes.

## Pruebas propuestas

### Application / API

- [ ] `GetDashboardAsync` devuelve hábitos activos por defecto usando `IsActive = true`.
- [ ] El filtro de inactivos devuelve únicamente hábitos con `IsActive = false`.
- [ ] Un usuario no puede ver hábitos de otro usuario.
- [ ] La migración deja los hábitos existentes con `IsActive = true`.
- [ ] Inactivar establece `IsActive = false`, conserva logs y no modifica `IsArchived`.
- [ ] Reactivar establece `IsActive = true`, conserva logs y no modifica `IsArchived`.
- [ ] `POST /api/habits/{id}/restore` devuelve `404` cuando el hábito pertenece a otro usuario y no cambia ningún dato.
- [ ] No se puede editar un hábito inactivo.
- [ ] `QuickLogAsync` valida primero `IsActive` y no registra actividad para un hábito inactivo.
- [ ] `QuickLogAsync` devuelve `404` para un ID inexistente o perteneciente a otro usuario, sin filtrar si el hábito está inactivo.
- [ ] `QuickLogAsync` devuelve `409 Conflict` para un hábito propio inactivo y no ejecuta conteos, deduplicación ni inserción.
- [ ] Reactivar conserva los registros históricos.

### Frontend

- [ ] El selector inicia en `🟢 Activos`.
- [ ] Seleccionar `🔴 Inactivos` carga únicamente hábitos inactivos.
- [ ] Momentum se muestra solo en `🟢 Activos` y se calcula únicamente con hábitos activos.
- [ ] Cancelar la confirmación no ejecuta la operación.
- [ ] Confirmar inactivación elimina el hábito de la lista activa.
- [ ] Un hábito inactivo abre sus campos en solo lectura.
- [ ] El botón `Guardar` permanece visible y deshabilitado para un hábito inactivo.
- [ ] Reactivar devuelve el hábito a la lista activa.
- [ ] Los hábitos inactivos siguen disponibles en el filtro del calendario.
- [ ] Los estados de error y carga se muestran correctamente.

## Decisiones aprobadas

- [x] El estado de esta feature se manejará con el campo independiente `IsActive`.
- [x] `IsArchived` queda reservado para otra funcionalidad y no será modificado por esta feature.
- [x] El selector visible será `🟢 Activos / 🔴 Inactivos` y funcionará como un filtro exclusivo; `🟢 Activos` será la opción inicial.
- [x] La reactivación también requerirá un diálogo de confirmación.
- [x] La acción será un botón textual `Inactivar`, ubicado debajo de la información del hábito y con color de advertencia.
- [x] Los textos de confirmación serán `Confirmo que deseo inactivar` y `Confirmo que deseo reactivar`.
- [x] Los hábitos inactivos seguirán apareciendo en los filtros del calendario para conservar la consulta de su historial.
- [x] El botón `Guardar` permanecerá visible, pero estará deshabilitado cuando el hábito esté inactivo.
- [x] `QuickLogAsync` validará primero la pertenencia mediante `Id == habitId && UserId == userId`, después `IsActive`, y solo entonces calculará límites, verificará duplicados e insertará el log.
- [x] Momentum se calculará únicamente con hábitos activos y se ocultará en la vista `🔴 Inactivos`.
- [x] `UpdatedAtUtc` solo se actualizará cuando cambie el estado de activo a inactivo; repetir la inactivación no modificará el timestamp.

## Verificación

- [ ] `dotnet build` sin errores ni warnings nuevos.
- [ ] `dotnet test`.
- [ ] Verificar que la migración agrega `IsActive = true` a los hábitos existentes.
- [ ] Crear hábito, confirmar que aparece en `🟢 Activos` y que Momentum lo considera.
- [ ] Inactivar un hábito y confirmar que desaparece de `🟢 Activos` y Momentum se recalcula.
- [ ] Seleccionar `🔴 Inactivos`, abrir el hábito y comprobar que los campos son de solo lectura y Guardar está deshabilitado.
- [ ] Confirmar que `QuickLog` no registra actividad para un hábito inactivo.
- [ ] Confirmar que el historial del hábito inactivo sigue disponible en el calendario.
- [ ] Reactivar el hábito y comprobar que vuelve a `🟢 Activos` y Momentum vuelve a considerarlo.
- [ ] Verificar que `IsArchived` no cambió durante ningún flujo.
- [ ] Verificar el comportamiento con dos usuarios distintos.
- [ ] Verificar el flujo en viewport móvil y escritorio.
