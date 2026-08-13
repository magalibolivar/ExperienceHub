# ExperienceHub — Registro del Trabajo Realizado

> Migración de dominio **ExperienceHub (alquiler de prendas)** → **ExperienceHub (suscripción a
> experiencias presenciales)**, conservando intacta toda la infraestructura técnica.
>
> Documento complementario de [`ANALISIS_MIGRACION.md`](ANALISIS_MIGRACION.md) (que contiene el
> análisis de los 10 puntos). Este archivo detalla **qué se hizo concretamente**, archivo por archivo.

**Estado final:** los 6 proyectos (BE, Seguridad, DAL, Servicios, BLL, GUI) compilan con MSBuild con
**0 errores y 0 warnings**; se genera `GUI/bin/Debug/GUI.exe`. El proyecto `Tests` también compila.

---

## 0. Preparación

- Se clonó el repo original en `2do/Ingenieria_Software/`.
- Se copió la solución a `2do/ExperienceHub/` (sin `bin`/`obj`/`.vs`/`.bak`), conservando la
  estructura de capas y todos los proyectos.
- **Dato técnico clave descubierto:** los `.csproj` NO son SDK-style → usan listas
  `<Compile Include>` explícitas. Cada `.cs` agregado o eliminado se reflejó en el `.csproj` de su capa.

---

## 1. Capa BE (Entidades) — dominio nuevo

### Entidades creadas
| Archivo | Rol |
|---|---|
| `Experiencia.cs` | Evento con fecha/horario/cupo/edad mínima/premium (reemplaza a `Prenda`). Métodos: `TieneCupo`, `EstaCompleta`, `EsFutura`, `DiasDeAnticipacion`, **`SeSuperponeCon`**, `CumpleEdadMinima`. |
| `Reserva.cs` | Reserva de 1 experiencia + invitados (reemplaza a `Pedido`). Máquina de estados `TransicionValida`, `PuedeConfirmarse/Cancelarse/RegistrarAsistencia/Calificarse`, `LugaresOcupados`, `OcupaCupo`. |
| `ReservaHistorial.cs` | Registro de cambios por campo (clon de `PedidoHistorial`). |
| `Organizador.cs` | Empresa/persona que ofrece experiencias. |
| `Categoria.cs` | Catálogo de categorías (antes era un string en Prenda). |
| `Ciudad.cs` | Catálogo de ciudades. |
| `Interes.cs` | Etiqueta de preferencia (N:M con Cliente). |
| `Calificacion.cs` | Puntaje 1-5 + comentario post-asistencia. |
| `ListaEspera.cs` | Entrada FIFO en la cola de una experiencia completa. |
| `Suscripcion.cs` | Vigencia + consumo mensual del plan. Métodos `EstaVigente`, `ReservasRestantes`, `PuedeReservar`, `AnticipacionPermitida`, `ProximaAVencer`. |

### Enums
- Creados: `EstadoReserva` (Pendiente/Confirmada/Cancelada/Asistio/NoAsistio), `EstadoExperiencia`
  (Programada/Completa/EnCurso/Finalizada/Cancelada), `EstadoSuscripcion`, `EstadoListaEspera`.
- Reescrito: `TipoEventoNegocio` (Reserva/Confirmacion/Cancelacion/Asistencia/…/Calificacion).

### Entidades adaptadas
- `Cliente.cs` — se quitó stock/plan directo/método de pago; se agregó `IdCiudad`, lista `Intereses`
  y navegación a `Suscripcion`. Métodos delegados: `Edad`, `TieneSuscripcion`, `SuscripcionVigente`,
  `PuedeReservar`, `ReservasRestantes`.
- `PlanSuscripcion.cs` — `LimitePrendas` → `ReservasMensuales`; se agregó `AnticipacionMaximaDias`,
  `AccesoPremium`, `PrioridadListaEspera`, `CantidadInvitados`.
- `EstadoComercialCliente.cs` — DTO reorientado a reservas mensuales.
- `Patentes.cs` — catálogo de permisos renombrado (`mnuExperiencias`, `mnuReservas`,
  `mnuReservasRealizadas`, `mnuOrganizadores`, `mnuCategorias`, `mnuCiudades`, `mnuListaEspera`, …).
- `TipoPermiso.cs` — enum legacy actualizado.
- `BitacoraNegocio.cs` — `IdPedido`→`IdReserva`, `IdPrenda`→`IdExperiencia`.

### Eliminadas
`Prenda.cs`, `EstadoPrenda.cs`, `Pedido.cs`, `EstadoPedido.cs`, `PedidoHistorial.cs`,
`MantenimientoPrenda.cs`. `BE.csproj` actualizado.

---

## 2. Base de Datos

### `BD/05_Dominio_ExperienceHub.sql` (nuevo, idempotente)
Se ejecuta después de `01_Crear` y `02_Actualizar`. Hace:
1. **Elimina** `PedidoPrenda`, `PedidoHistorial`, `MantenimientoPrenda`, `Pedido`, `Prenda`.
2. **Crea** `Ciudad`, `Categoria`, `Organizador`, `Interes`, `ClienteInteres`, `Experiencia`,
   `Suscripcion`, `Reserva`, `ReservaHistorial`, `ListaEspera`, `Calificacion`.
3. **Modifica** `Cliente` (+`IdCiudad`, FK a Ciudad), `PlanSuscripcion` (+beneficios, migra
   `LimitePrendas`→`ReservasMensuales`), `BitacoraNegocio` (renombra columnas).
4. **Registra** DV de `Experiencia`/`Reserva`/`Organizador` en `DVVertical`.
5. **Re-siembra** patentes (renombra viejas + crea nuevas) y las asocia al rol Administrador.
6. **Semillas**: ciudades, categorías (Taller/Cata/Teatro/Escape Room/…), intereses, organizadores.

Tablas de infraestructura conservadas intactas: `Usuario`, `Permiso`, `PermisoRelacion`,
`RolPermiso`, `ControlMapeado`, `Control`, `Traduccion`, `Idioma`, `Bitacora`, `HistorialUsuario`,
`HistorialIntegridad`, `ClaveRecuperacion`, `Preferencia`, `DVVertical`, `Empleado`.

---

## 3. Capa DAL (Acceso a Datos)

### Clases creadas (ADO.NET parametrizado, patrón calcado del original)
`Ciudad.cs`, `Categoria.cs`, `Organizador.cs`, `Interes.cs` (+ relación N:M `ClienteInteres`),
`Experiencia.cs`, `Suscripcion.cs`, `Reserva.cs`, `ReservaHistorial.cs`, `ListaEspera.cs`,
`Calificacion.cs`.

Puntos destacados:
- **`Reserva.CrearConCupo`** — inserta la reserva y **descuenta el cupo de la experiencia en la
  MISMA transacción** (si el cupo ya no alcanza, revierte y lanza `sin_cupo`).
- **`Reserva.Cancelar`** — al cancelar a tiempo, devuelve los lugares al cupo (misma transacción).
- **`Reserva.RestaurarOperacionAtomica`** — rollback desde historial (Memento a nivel de reserva).
- **`Cliente`** — `OUTER APPLY` a la suscripción más reciente + su plan; DNI cifrado (AES) conservado.
- **`Suscripcion`** — `ContarActivasPorPlan`, `IncrementarConsumo`/`DecrementarConsumo`, `Renovar`.
- DV genérico (`RecalcularTabla`) usado en Reserva/Experiencia/Organizador (columnas declaradas en
  cada DAL como `DV_Columnas`).

### Adaptadas
- `Cliente.cs`, `PlanSuscripcion.cs`, `BitacoraNegocio.cs`.
- `Backup.cs` — el "resumen de actividad reciente" apunta a Reserva/ReservaHistorial/Calificacion.
- `Usuario.cs` — la anonimización al purgar apunta a `ReservaHistorial`.

### Eliminadas
`Prenda.cs`, `Pedido.cs`, `PedidoHistorial.cs`, `MantenimientoPrenda.cs`. `DAL.csproj` actualizado.

---

## 4. Capa BLL (Lógica de Negocio) — las 11 reglas

### Servicios creados
| Archivo | Contenido |
|---|---|
| `Experiencia.cs` | ABM de experiencias (cupo inicial = cupo máximo), guardas de permiso, doble bitácora. |
| `Reserva.cs` | **Núcleo.** `CrearReserva`, `Confirmar`, `RegistrarAsistencia`, `Cancelar`, `RestaurarOperacion`, `CalcularNivelUrgencia`. |
| `Organizador.cs`, `Categoria.cs`, `Ciudad.cs` | ABM de catálogos con baja protegida. |
| `Suscripcion.cs` | `Crear`, `Renovar`, `Suspender`, `Reactivar`. |
| `ListaEspera.cs` | `Ingresar` (regla 8), promoción (regla 9). |
| `Calificacion.cs` | `Calificar` (regla 10). |
| `Recomendacion.cs` | `Recomendar` (regla 11), scoring por intereses/historial/ciudad/calificaciones. |
| `Interfaces/IExperienciaService.cs`, `Interfaces/IReservaService.cs` | Contratos nuevos. |

### Reglas de negocio implementadas (dónde vive cada una)
1. **Sin cupo → no reserva** → `Reserva.CrearReserva` + `DAL.Reserva.CrearConCupo` (atómico).
2. **Sin solapamiento de horarios** → `Reserva.ValidarSinSolapamiento` + `BE.Experiencia.SeSuperponeCon`.
3. **Límite de reservas del plan** → `Cliente.PuedeReservar` / `Suscripcion.ReservasRestantes`.
4. **Ciudad del cliente** → `Reserva.CrearReserva` (const `PERMITIR_FUERA_DE_CIUDAD`).
5. **No repetir experiencia ya asistida** → `DAL.Reserva.ExisteAsistenciaPrevia` (const `BLOQUEAR_SI_YA_REALIZADA`).
6/7. **Cancelación anticipada libera cupo + devuelve beneficio / tardía lo consume** →
     `Reserva.Cancelar` (umbral `HORAS_ANTICIPACION_CANCELACION`).
8. **Lista de espera si está completa** → `ListaEspera.Ingresar`.
9. **Promoción automática del 1° al liberarse cupo** → `Reserva.OfrecerCupoAlPrimeroEnEspera`.
10. **Calificar solo si asistió** → `Calificacion.Calificar` (valida `Reserva.PuedeCalificarse`).
11. **Recomendaciones por perfil** → `Recomendacion.Recomendar`.

Extras: premium (requiere plan con acceso premium), edad mínima, invitados dentro del plan.

### Adaptadas
- `Cliente.cs` — alta crea la suscripción inicial + guarda intereses; baja bloqueada por reservas
  activas; `ObtenerEstadoComercial` reorientado.
- `PlanSuscripcion.cs` — validación de beneficios; baja protegida por suscripciones activas.
- `Configuracion.cs` — verificación/recálculo de DV ahora sobre Reserva/Experiencia/Organizador.
- `PanelAlertas.cs` — métrica de "prendas en limpieza" → "experiencias completas próximas".
- `ReporteJornada.cs` — KPI de prendas → experiencias; marca "ExperienceHub" → "ExperienceHub".
- `Bitacora.cs` — parámetro `idPedido` → `idReserva`.

### Eliminadas
`Prenda.cs`, `Pedido.cs`, `Interfaces/IPrendaService.cs`, `Interfaces/IPedidoService.cs`.
`BLL.csproj` actualizado.

---

## 5. Capa GUI (WinForms MDI)

### Forms nuevos (code-only, sin Designer)
| Archivo | Función |
|---|---|
| `Experiencias.cs` | ABM de experiencias (combos Categoría/Ciudad/Organizador, fecha/hora/cupo/edad/premium). |
| `Reservas.cs` | Crear reserva (cliente + experiencia + invitados), confirmar, cancelar, ingresar a lista de espera. |
| `ReservasRealizadas.cs` | Registrar asistencia (Asistió/No asistió) y calificar. |
| `Organizadores.cs`, `Categorias.cs`, `Ciudades.cs` | ABM de catálogos. |
| `RecomendacionesForm.cs` | Selección de cliente → experiencias recomendadas. |

### Adaptados
- `DashboardForm.cs` — **reescrito** con KPIs del nuevo dominio (experiencias disponibles, clientes,
  reservas pendientes, días sin backup) + actividad de negocio reciente + auto-refresh async.
- `Menu.cs` — factory de dashboard simplificado; aberturas rewireadas a Experiencias/Reservas/
  ReservasRealizadas; mapa de permisos actualizado; **menú "Catálogos" agregado por código**
  (Organizadores/Categorías/Ciudades/Recomendaciones).
- `ClienteForm.cs` — combo de método de pago **reutilizado como selector de Ciudad**; plan +
  vencimiento mapeados a la `Suscripcion`.
- `Clientes.cs` — grilla reorientada (plan/reservas/ciudad/vencimiento desde la suscripción).
- `Planes.cs` — `LimitePrendas` → `ReservasMensuales` + beneficios por defecto.
- `ReporteJornadaForm.cs` — KPI de experiencias.

### Eliminados
`Prendas`, `PrendaForm`, `PedidosVenta`, `PedidosRealizados`, `NuevoPedidoForm`,
`PedidoHistorialForm`, `CambioEstadoDialog`, `NotificacionDespachoForm`, `MantenimientoHistorialForm`
(+ sus `.Designer.cs`/`.resx`), y los dashboards de rol `DashboardVendedor`/`DashboardControlStock`/
`DashboardOperador`/`DashboardSupervisor`. `GUI.csproj` actualizado.

### Infraestructura GUI conservada intacta
`Login`, `Menu` (shell), `FormBase`, `GestorPermisos`, `MapeoControlesForm`, `FormIdiomas`,
`Bitacora`, `BackupForm`, `DiagnosticoIntegridadForm`, `MiPerfilForm`, `Usuarios`,
`VersionHistorialForm`, `ConfirmarAdminForm`, `Program.cs`, etc.

---

## 6. Infraestructura conservada SIN cambios

- **Seguridad:** `SessionManager` (Singleton), `Encriptador` (PBKDF2 + AES), `CifradorArchivos`,
  `DigitoVerificador`, `CalculadorDV` (Factory), `ContadorSesion`.
- **Servicios:** `GestorIdioma`/`IIdiomaObserver` (Observer), `Bitacora`, `Traductor`,
  `SerializadorCsv`, `GeneradorCredenciales`.
- **Patrones:** Singleton, Composite (permisos), Observer (idiomas), Memento (historial), Factory (DV).
- **DAL/BLL de infraestructura:** `Acceso`, `BaseDAL`, `Backup`, `Permiso`, `Familia`,
  `CuidadorHistorial`, `Usuario`, `Configuracion`, `BLLHelper`, `PermisosAccion`, etc.

---

## 7. Verificación de compilación

Compilado con MSBuild (VS 2022):

```
BE.dll        ✓
Seguridad.dll ✓
DAL.dll       ✓
Servicios.dll ✓
BLL.dll       ✓
GUI.exe       ✓  → GUI/bin/Debug/GUI.exe
0 errores · 0 warnings
Tests.dll     ✓ (compila)
```

---

## 8. Cómo ponerlo en marcha

1. **Base de datos** (SQL Server): ejecutar en orden
   `BD/01_Crear_BaseDeDatos.sql` → `BD/02_Actualizar_BaseDeDatos.sql` → `BD/05_Dominio_ExperienceHub.sql`.
2. **Compilar/ejecutar**: abrir `IngSoftware-Bolivar,Morana.slnx` en Visual Studio (o
   `MSBuild GUI/GUI.csproj`).
3. **Login inicial**: `admin` / `administrador1!` (usuarios sembrados por `01`).

> La cadena de conexión apunta al catálogo `ExperienceHubDB` (`GUI/App.config` + `DAL/Acceso.cs`),
> consistente con los scripts SQL.

---

## 9. Segunda ronda — pendientes resueltos

- [x] **Etiquetas del menú** actualizadas (Prendas→Experiencias, Pedidos→Reservas/Reservas realizadas;
      grupo "Inventario"→"Experiencias") en `Menu.Designer.cs` (Text + Tag).
- [x] **Marca completa** renombrada `WardrobeFlow`→`ExperienceHub` en todo el código, incluido el
      **catálogo de BD** `WardrobeFlowDB`→`ExperienceHubDB` (SQL, `App.config`, `DAL/Acceso.cs`).
- [x] **Estado automático de la Experiencia**: pasa a `Completa` cuando el cupo llega a 0 y vuelve a
      `Programada` al liberarse (dentro de las transacciones de `DAL.Reserva`).
- [x] **Renovar / Suspender / Reactivar suscripción**: nuevo form `SuscripcionesForm` (menú Catálogos).
- [x] **Lista de espera — cierre de la regla 9**: `ListaEsperaForm` + `BLL.ListaEspera.ConfirmarOferta`
      (convierte la oferta de cupo en reserva real).
- [x] **Selector de intereses** en `ClienteForm` (CheckedListBox; se preselecciona en edición y se
      persiste en `ClienteInteres`). `BLL.Interes` agregado.
- [x] **Tests de las reglas nuevas**: `Tests/ReglasNegocioTests.cs` (12 tests puros, verdes);
      `PanelAlertasTests` adaptado. **46 tests puros pasan** (vstest.console).
- [x] **Diagramas UML** nuevos en `DIAGRAMAS/`: `G07 - Modelo de Datos Integrado.puml`,
      `CU - Secuencia - Crear Reserva.puml`, `CU - Secuencia - Cancelar y Lista de Espera.puml`.

### Verificación (segunda ronda)
`GUI.exe` reconstruido con **0 errores / 0 warnings**; `Tests` compila; **46/46** tests puros verdes
(los que dependen de SQL Server no se corrieron por no tener la base levantada).

## 10. Tercera ronda — i18n y puesta en marcha

- [x] **i18n del menú**: ítems renombrados + submenú "Catálogos" traducidos en **ES/EN/RU/PT**
      (claves `mnu.*` nuevas en `traducciones.tsv`; traducidos por Tag en `Menu.Traducir`).
- [x] **i18n de los módulos nuevos**: los 9 forms code-only ahora implementan `IIdiomaObserver`
      (se suscriben/desuscriben al `GestorIdioma`) y traducen su **título y botones** por Tag `eh.*`
      vía el helper `GUI/I18n.cs`. 116 claves `eh.*` agregadas al `traducciones.tsv` (ES/EN/RU/PT).
      El cambio de idioma se refleja en caliente igual que en los forms clásicos.
- [x] **Script de puesta en marcha** `BD/Setup-BaseDeDatos.ps1`: corre `01`→`02`→`05` con `sqlcmd`
      (Windows Auth o usuario/clave), con verificación de errores.

> **Nota i18n:** en una **instalación fresca** el auto-seed puebla la tabla `Traduccion` desde el
> `.tsv` embebido (que ya incluye las claves nuevas en los 4 idiomas) → menú y forms nuevos quedan
> totalmente multilingües. Sobre una BD **ya sembrada**, las claves nuevas caen a español (fallback
> por clave) hasta re-sembrar traducciones; el resto sigue traducido.

## 11. Cuarta ronda — verificación END-TO-END contra SQL Server real

Ejecutado contra **SQL Server 2022** (`.\SQLEXPRESS`, Windows Auth):

- [x] **Migración de BD**: `01`→`02`→`05` corridos con `sqlcmd`. Se creó `ExperienceHubDB` sin tocar
      la `WardrobeFlowDB` original. Durante la corrida se detectaron y corrigieron 2 bugs del script
      `05`: (a) FKs de `BitacoraNegocio`→`Pedido/Prenda` que impedían el DROP (ahora se quitan antes),
      (b) el borrado de la patente `mnuStock` chocaba con `RolPermiso`/`PermisoRelacion` (ahora se
      limpian las referencias primero).
- [x] **Esquema verificado**: 11 tablas nuevas presentes, 0 tablas viejas, `BitacoraNegocio` con
      `IdReserva`/`IdExperiencia`, `PlanSuscripcion.ReservasMensuales`, `Cliente.IdCiudad`, 7 patentes
      nuevas, DV de `Experiencia`/`Reserva`/`Organizador`, y seeds (5 ciudades, 10 categorías,
      10 intereses, 3 organizadores).
- [x] **Arranque de la app**: `GUI.exe` lanzado contra la BD real → pasa verificación de conexión y
      **verificación de integridad DV pre-login** (fresh DB recalcula DVH/DVV y continúa),
      queda vivo y `Responding` en la ventana de login. Sin crash.
- [x] **Flujo de negocio (integración)**: `Tests/IntegracionReservaTests.cs` contra la BD real crea
      una experiencia (cupo 2) → reserva (cupo→1) → cancela liberando (cupo→2), con limpieza propia.
      **Verifica el descuento/restauración ATÓMICA de cupo (reglas 1 y 6)**. La corrida detectó y
      corrigió un bug de robustez: `DAL.Reserva.CrearConCupo` ahora manda `NULL` cuando `IdEmpleado`
      es 0 (evita violar la FK a `Empleado`).
- [x] **Suite completa**: **125 tests → 123 correctos, 0 fallidos, 2 omitidos** (vstest.console).

### Estado final
La solución compila (0/0), la BD migra limpio, la app arranca contra SQL Server real, y el circuito
de reservas funciona verificado por integración. Migración **completa y validada end-to-end**.

## 12. Quinta ronda — cierre de los últimos opcionales

- [x] **i18n de las etiquetas de campo** de los forms nuevos: los labels ahora se taguean con claves
      `eh.lbl.*` (22 nuevas × 4 idiomas = 88 líneas en `traducciones.tsv`) y se traducen en caliente
      junto con títulos y botones. Verificado por `ReglasNegocioTests.I18n_ClavesNuevas_...` y por el
      test existente `TodosLosIdiomasTienenLasMismasClaves` (todas las claves presentes en ES/EN/RU/PT).
- [x] **Finalización de experiencias por fecha**: `DAL/BLL.Experiencia.ActualizarEstadosPorFecha()`
      marca `Finalizada` las que ya terminaron y `EnCurso` las que están ocurriendo. Se ejecuta al
      arranque en `Program.cs` (tarea de sistema, sin sesión). Verificado por integración
      (`IntegracionReservaTests.EstadoPorFecha_FinalizaVencidas`, contra la BD real).
- [x] **Bug corregido (encontrado por el test de integración)**: `new SqlParameter("@x", (int)Enum)`
      con un enum de **valor 0** (Pendiente/Programada/Esperando/Activa) resolvía a la sobrecarga
      `SqlParameter(string, SqlDbType)` en vez de `(string, object)`, dejando el parámetro sin valor.
      Se casteó a `(object)` en todas las consultas afectadas (`DAL.Reserva`, `Experiencia`,
      `ListaEspera`, `Suscripcion`). Habría roto en runtime las listas que filtran por esos estados.

### Verificación final
`GUI.exe` recompila **0 errores / 0 warnings**. Suite: **127 tests → 125 correctos, 0 fallidos,
2 omitidos** (los 2 omitidos son tests que se auto-marcan `Inconclusive` sin datos). Incluye 2 tests
de integración contra SQL Server real (cupo atómico + finalización por fecha) y tests de i18n.

**La migración WardrobeFlow → ExperienceHub está 100% completa, compilando, multilingüe y validada
end-to-end contra SQL Server real. No quedan pendientes.**

---

*Documento generado durante la migración WardrobeFlow → ExperienceHub — Ingeniería de Software.*
