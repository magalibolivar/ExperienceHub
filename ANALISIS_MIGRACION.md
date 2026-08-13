# Análisis de Migración: WardrobeFlow → ExperienceHub

> Migración de dominio conservando **toda** la infraestructura técnica: arquitectura en capas,
> patrones de diseño (Singleton, Composite, Observer, Memento, Factory), seguridad, auditoría,
> historial, i18n, backups y dígitos verificadores.
>
> **Idea rectora:** la capa técnica es agnóstica del dominio y se conserva. Solo cambia el
> "anillo de negocio" (`BE` comercial, `BLL` comercial, sus tablas SQL y los forms de negocio).

---

## De qué se trata el cambio

| | WardrobeFlow (origen) | ExperienceHub (nuevo) |
|---|---|---|
| Negocio | Alquiler de prendas por suscripción | Suscripción a experiencias presenciales |
| Ítem central | Prenda (stock físico) | Experiencia (evento con fecha/cupo) |
| Transacción | Pedido (multi-prenda) | Reserva (1 experiencia + invitados) |
| Ciclo | Despacho → Entrega → Devolución → Limpieza | Reserva → Confirmación → Asistencia → Calificación |
| Nuevos conceptos | — | Organizador, Categoría, Ciudad, Lista de espera, Calificación, Recomendación |

**Flujo de dominio:** `Cliente → Suscripción → Reserva → Experiencia → Asistencia → Calificación`

---

## 1. Clases que se reutilizan SIN modificaciones

Todo lo transversal (sin dependencia del dominio "prendas"):

- **Seguridad (100%):** `SessionManager`, `Encriptador`, `CifradorArchivos`, `ContadorSesion`,
  `DigitoVerificador`, `CalculadorDV`, `ICalculadorDV`
- **Servicios (salvo el corpus de textos):** `Bitacora`, `GestorIdioma`, `IIdiomaObserver`, `Idioma`,
  `Etiqueta`, `Traduccion`, `TraductorPerfil`, `SerializadorCsv`, `GeneradorCredenciales`
- **DAL infraestructura:** `Acceso`, `BaseDAL`, `Backup`, `Bitacora`, `DigitoVerificador`, `Permiso`,
  `Traduccion`, `Idioma`, `Usuario`, `Empleado`, `Preferencia`, `Control`, `ControlMapeado`,
  `ClaveRecuperacion`, `EspejoUsuario`, `HistorialIntegridad`, `VersionUsuario`
- **BLL infraestructura:** `BLLHelper`, `PermisosAccion`, `Configuracion`, `Backup`, `Bitacora`,
  `Familia` (Composite), `Idioma`, `Usuario`, `CuidadorHistorial`, `VersionUsuario`, `Preferencia`,
  `RecuperacionAdmin`, `RecuperacionIntegridad`, `ControlMapeado`
- **BE infraestructura:** `Componente`, `Familia`, `Patente`, `Rol`, `Permiso`, `Usuario`, `Empleado`,
  `VersionUsuario`, `Memento/*`, `Idioma`, `Control`, `ControlMapeado`, `Preferencia`, `Bitacora`,
  `Criticidad`, `AppException`, `LoginException`, `SesionException`, `FilaUsuarioDV`, `FilaDV`,
  `HistorialIntegridad`
- **GUI infraestructura:** `Login`, `Menu` (shell MDI), `FormBase`, `GestorPermisos`, `MapeoControlesForm`,
  `FormIdiomas`, `Bitacora`, `BackupForm`, `DiagnosticoIntegridadForm`, `MiPerfilForm`, `Usuarios`,
  `AdministracionUsuariosForm`, `VersionHistorialForm`, `ConfirmarAdminForm`, `DesbloqueoEmergenciaForm`,
  `RecuperacionEspejoForm`, `CambioClaveObligatorioForm`, `OlvideContrasenaForm`, `RegistroControles`,
  `ManejadorSeguridad`, `Program.cs`
- **Tests infraestructura:** `CompositeTests`, `MementoTests`, `DigitoVerificadorTests`, `EncriptadorTests`,
  `SessionManagerTests`, `FamiliaPermisosTests`, `PermisosAccionTests`, `RolAdminTests`,
  `UltimoAdminGuardTests`, `UsuarioAbmTests`, `ClaveEmergenciaTests`, `BackupTests`, `TraductorI18nTests`

---

## 2. Clases que se reutilizan cambiando solo nombres (scaffolding = plantilla)

Estas son el **molde**: se renombran y se reescribe su contenido de dominio, conservando la
estructura (DI por constructor, guards de permiso, doble bitácora, historial, validaciones).

| Actual | Nuevo | Qué se conserva |
|---|---|---|
| `BLL/Prenda.cs` | `BLL/Experiencia.cs` | CRUD + `PermisosAccion.Exigir` + doble bitácora + `Validar()` |
| `BLL/Pedido.cs` | `BLL/Reserva.cs` | **molde de oro**: máquina de estados, historial por operación, rollback atómico, urgencia |
| `BLL/Cliente.cs` | `BLL/Cliente.cs` | DI con `IClienteDAL`, validaciones, unicidad, `ObtenerEstadoComercial` |
| `BLL/PlanSuscripcion.cs` | `BLL/PlanSuscripcion.cs` | ABM + baja lógica protegida |
| `DAL/Prenda.cs` | `DAL/Experiencia.cs` | ADO.NET parametrizado, mapeo manual, DV |
| `DAL/Pedido.cs` | `DAL/Reserva.cs` | transacciones atómicas, `RestaurarOperacionAtomica`, `RecalcularDV` |
| `DAL/PedidoHistorial.cs` | `DAL/ReservaHistorial.cs` | historial campo-a-campo |
| `BE/PedidoHistorial.cs` | `BE/ReservaHistorial.cs` | idéntico |
| `BE/EstadoPedido.cs` | `BE/EstadoReserva.cs` | enum con nuevos valores |
| `BE/EstadoComercialCliente.cs` | igual (otros campos) | DTO para la GUI |
| `BE/NivelUrgencia.cs` | igual | reutilizable para reservas próximas |
| `BE/TipoEventoNegocio.cs` | igual (otros valores) | enum bitácora de negocio |
| `Interfaces/IPrendaService`, `IPedidoService` | `IExperienciaService`, `IReservaService` | contrato |

---

## 3. Entidades que se reemplazan COMPLETAMENTE

No es rename: cambian campos, comportamiento e invariantes.

| Se elimina | Entra | Motivo |
|---|---|---|
| `BE/Prenda.cs` | **`BE/Experiencia.cs`** | de ítem-stock con máquina de estados a **evento** con fecha/horario/cupo/edad mínima |
| `BE/EstadoPrenda.cs` | **`BE/EstadoExperiencia.cs`** | `Programada`, `Completa`, `EnCurso`, `Finalizada`, `Cancelada` |
| `BE/Pedido.cs` | **`BE/Reserva.cs`** | de pedido multi-prenda a **reserva de 1 experiencia + invitados** |
| `BE/MantenimientoPrenda.cs` | *(se elimina)* | no hay limpieza |

**Entidades nuevas:** `Organizador`, `Categoria`, `Ciudad`, `Calificacion`, `ListaEspera`,
`Suscripcion` (renovar/suspender con historial), `Interes` (+ `ClienteInteres` M:N).

**Entidades que se conservan pero cambian campos:**
- `BE/Cliente.cs`: quitar `StockUtilizado`/`LimitePrendas` → agregar `IdCiudad`, `Intereses`;
  suscripción se mueve a la entidad `Suscripcion`.
- `BE/PlanSuscripcion.cs`: `LimitePrendas` → `ReservasMensuales`; agregar `AnticipacionMaximaDias`,
  `AccesoPremium`, `PrioridadListaEspera`, `CantidadInvitados`.

---

## 4. Reglas de negocio actuales que YA NO tienen sentido

| Regla actual | Estado | Reemplazo |
|---|---|---|
| Máquina de estados `Disponible→EnUso→EnLimpieza→Baja` | ❌ eliminar | estados de Experiencia por fecha/cupo |
| Devolución de prendas → `EnLimpieza` | ❌ eliminar | Asistencia/Calificación |
| Apertura/cierre de `MantenimientoPrenda` | ❌ eliminar | — |
| Límite de prendas en uso simultáneo (`StockUtilizado ≤ Límite`) | ❌ eliminar | reservas mensuales por plan |
| Bloqueo "cliente con pedido despachado sin entregar" | ❌ eliminar | solapamiento de horarios |
| "No bajar de plan si prendas-en-uso > límite" | ❌ eliminar | — |
| "No eliminar cliente con prendas en uso" | ⚠️ adaptar | "no eliminar cliente con reservas activas/futuras" |
| DVH de `Prenda`/`Pedido` | ⚠️ recalibrar | DVH de `Experiencia`/`Reserva` |

**Dónde viven las nuevas reglas (1–11):**
- Cupo (1), anticipación, fuera de ciudad (4), ya realizada (5) → `BLL.Reserva.CrearReserva`
- Solapamiento de horarios (2) → método de dominio `BE.Reserva.SeSuperponeCon()` / `BE.Experiencia`
- Reservas mensuales (3) y cancelación tardía consume beneficio (7) → `BE.Suscripcion`
- Cancelación anticipada libera cupo (6) → `BLL.Reserva.Cancelar` (compara `Experiencia.Fecha - Now` vs `Plan.AnticipacionMaxima`)
- Lista de espera (8, 9) → `BLL.ListaEspera` (promoción FIFO al liberarse un cupo)
- Calificación (10) → `BLL.Calificacion` (solo si `Reserva.Estado == Asistió`)
- Recomendaciones (11) → `BLL.Recomendacion` (read-only: historial + intereses + ciudad + calificaciones)

---

## 5. Tablas SQL que se ELIMINAN

| Tabla | Motivo |
|---|---|
| `Prenda` | → `Experiencia` |
| `Pedido` | → `Reserva` |
| `PedidoPrenda` (M:N) | una reserva es 1 experiencia; el M:N desaparece |
| `PedidoHistorial` | → `ReservaHistorial` (mismo diseño) |
| `MantenimientoPrenda` | no hay limpieza |

**Se conservan intactas (infra):** `Usuario`, `Permiso`, `PermisoRelacion`, `RolPermiso`,
`ControlMapeado`, `Control`, `Traduccion`, `Idioma`, `Bitacora`, `HistorialUsuario`,
`HistorialIntegridad`, `ClaveRecuperacion`, `Preferencia`, `DVVertical`, `Empleado`.

**Se modifican:** `Cliente` (quitar stock, agregar `IdCiudad`), `PlanSuscripcion` (renombrar
`LimitePrendas`, +4 columnas), `BitacoraNegocio` (estructura igual).

---

## 6. Tablas NUEVAS

```
Ciudad          (Id, Nombre, Provincia, Estado)
Categoria       (Id, Nombre, Descripcion, Estado)
Organizador     (Id, Nombre, Contacto, Telefono, Mail, Estado, DVH)
Experiencia     (Id, Nombre, Descripcion, IdCategoria FK, IdCiudad FK, Ubicacion,
                 Fecha, HoraInicio, DuracionMin, IdOrganizador FK, CupoMaximo,
                 CupoDisponible, EdadMinima, Premium BIT, Estado, DVH)
Reserva         (Id, IdCliente FK, IdExperiencia FK, IdEmpleado FK, Estado, FechaReserva,
                 CantidadInvitados, FechaCancelacion, MotivoCancelacion, DVH)
ReservaHistorial(Id, IdReserva FK, IdOperacion, Fecha, IdUsuario, Accion, Campo,
                 ValorAnterior, ValorNuevo)          -- clon de PedidoHistorial
ListaEspera     (Id, IdExperiencia FK, IdCliente FK, Posicion, FechaIngreso, Estado)
Calificacion    (Id, IdReserva FK, IdCliente FK, IdExperiencia FK, Puntaje, Comentario, Fecha)
Suscripcion     (Id, IdCliente FK, IdPlan FK, FechaInicio, FechaVencimiento,
                 Estado[Activa/Suspendida/Vencida], ReservasConsumidasMes)
Interes         (Id, Nombre)                          -- catálogo
ClienteInteres  (IdCliente FK, IdInteres FK)          -- M:N
```

- **DV:** agregar `Experiencia`, `Reserva`, `Organizador` a las tablas protegidas (el motor
  `RecalcularTabla(tabla, pk, cols)` ya es genérico: solo cambian las constantes de columnas).
- **Permisos/seed:** renombrar patentes (`mnuPrendas→mnuExperiencias`, `mnuPedidosVenta→mnuReservas`,
  …) y agregar `mnuOrganizadores`, `mnuCategorias`, `mnuCiudades`, `mnuListaEspera`, `mnuCalificaciones`.
  El árbol Composite y los roles se re-siembran; el motor no se toca.

---

## 7. Nuevo modelo de dominio

```
                         ┌──────────────┐
                         │    Ciudad    │
                         └──────┬───────┘
                                │
     ┌──────────┐   N:M   ┌─────▼──────┐        ┌───────────────┐
     │  Interes │◄───────►│  Cliente   │───────►│  Suscripcion  │──► PlanSuscripcion
     └──────────┘         └─────┬──────┘  1:N    └───────────────┘     (beneficios)
                                │ 1:N
                                ▼
                          ┌───────────┐        ┌────────────────┐
                          │  Reserva  │───────►│  Experiencia   │
                          │ Pendiente │  N:1   │  (fecha, cupo, │
                          │ Confirmada│        │   edad mín.)   │
                          │ Cancelada │        └───┬────────┬───┘
                          │ Asistió   │            │        │
                          │ NoAsistió │        N:1 │    N:1 │
                          └─────┬─────┘        ┌───▼───┐ ┌──▼──────────┐
                                │              │Categoría│ │ Organizador │
              ┌─────────────────┼──────────┐  └───────┘ └─────────────┘
              ▼                 ▼          ▼
       ┌────────────┐   ┌─────────────┐  (Experiencia)
       │Calificacion│   │ ListaEspera │◄──── cuando CupoDisponible = 0
       └────────────┘   └─────────────┘
       (solo si Asistió)  (promoción FIFO al liberarse cupo)
```

---

## 8. Diagramas UML a modificar

| Diagrama (`Entregas/Tercera Entrega/DIAGRAMAS/`) | Cambio |
|---|---|
| `G07 - Modelo de Datos Integrado.puml` | rehacer bloque de negocio: fuera Prenda/Pedido/PedidoPrenda/Mantenimiento; entran las tablas nuevas |
| `G06 - Diagrama de Clases por Capa.puml` | reemplazar clases amarillas (negocio); las celestes (técnicas) quedan |
| `CU04/CU05` (Perfiles / Idioma) | **sin cambios** (infra) |
| `CU07` (Verificación integridad) | solo cambia la lista de tablas verificadas |
| `Diagrama de Componentes.puml` | **sin cambios** |
| **Nuevos** | `Crear Reserva`, `Cancelar Reserva + Lista de Espera`, `Calificar`, `Recomendar` |

---

## 9. Formularios WinForms

**Se reemplazan (plantilla = el actual):**

| Actual | Nuevo |
|---|---|
| `Prendas.cs` / `PrendaForm.cs` | `Experiencias.cs` / `ExperienciaForm.cs` |
| `PedidosVenta.cs` / `NuevoPedidoForm.cs` | `Reservas.cs` / `NuevaReservaForm.cs` |
| `PedidosRealizados.cs` | `ReservasRealizadas.cs` |
| `PedidoHistorialForm.cs` | `ReservaHistorialForm.cs` |
| `CambioEstadoDialog.cs` | `ConfirmarAsistenciaDialog.cs` |
| `NotificacionDespachoForm.cs` | `NotificacionListaEsperaForm.cs` |

**Nuevos:** `Organizadores`, `Categorias`, `Ciudades`, `ListaEsperaForm`, `CalificacionForm`,
`RecomendacionesForm`.

**Se adaptan:** `Clientes`/`ClienteForm` (ciudad + intereses), `Planes` (nuevos beneficios).
**Dashboards:** mismos mecanismos (Factory en `Menu.cs`, async, Observer); solo cambian los KPIs.
**Sin cambios:** todos los del punto 1.

---

## 10. Código que se conserva prácticamente igual

- **100%:** `Seguridad/*`, `Servicios/*` (salvo strings de negocio en `Traductor.cs`), DAL infra,
  BLL infra, BE infra, `Program.cs`, `Menu` (shell), `FormBase`, tests de infra.
- **Corpus de traducciones:** claves de infra intactas; se agregan/reescriben solo las de negocio.
- **Build/config:** `build-and-test.ps1`, `.slnx`, `.csproj`, `.gitignore`, `App.config` → idénticos.
- **Convenciones/estilo:** namespaces por capa, guards `PermisosAccion.Exigir` al inicio de cada
  método, doble bitácora, `AppException` con clave i18n, DI por constructor → se respetan al
  calcar los servicios nuevos sobre los viejos.

---

## Resumen de esfuerzo

| Zona | Acción | Volumen |
|---|---|---|
| Seguridad + Servicios + DAL/BLL infra | conservar | ~60% del código, 0 cambios |
| BE/BLL/DAL de negocio | reescribir sobre plantilla | Prenda→Experiencia, Pedido→Reserva + 7 entidades nuevas |
| SQL | 5 tablas fuera, ~11 nuevas, 2 modificadas, re-seed de patentes | 1 script |
| GUI | 6 forms reemplazados, 6 nuevos, 2 adaptados | resto igual |
| UML | 2 diagramas de datos/clases + CU de negocio nuevos | resto igual |

**Resultado:** sistema claramente distinto en el negocio (experiencias, reservas, cupos, lista de
espera, calificaciones, recomendaciones) conservando los 5 patrones, seguridad, auditoría, i18n,
DV y arquitectura en capas.

---

## Estado de la implementación

- [x] Carpeta `ExperienceHub/` creada (copia de la solución, sin `bin`/`obj`/`.vs`/`.bak`)
- [x] Documento de análisis (este archivo)
- [x] **Capa BE completa** — nuevas entidades y enums:
  - Entidades: `Experiencia`, `Reserva`, `ReservaHistorial`, `Organizador`, `Categoria`,
    `Ciudad`, `Interes`, `Calificacion`, `ListaEspera`, `Suscripcion`
  - Enums: `EstadoReserva`, `EstadoExperiencia`, `EstadoSuscripcion`, `EstadoListaEspera`,
    `TipoEventoNegocio` (reescrito)
  - Adaptadas: `Cliente` (ciudad + intereses + navegación a `Suscripcion`),
    `PlanSuscripcion` (nuevos beneficios), `EstadoComercialCliente`, `Patentes`, `TipoPermiso`
  - Eliminadas: `Prenda`, `EstadoPrenda`, `Pedido`, `EstadoPedido`, `PedidoHistorial`,
    `MantenimientoPrenda`
  - `BE.csproj` actualizado (listas `<Compile>` explícitas)
  - `BitacoraNegocio`: `IdPedido→IdReserva`, `IdPrenda→IdExperiencia` (BE + Servicios + DAL)
- [x] **Script SQL** `BD/05_Dominio_ExperienceHub.sql` — DROP de tablas viejas + CREATE de las
      11 nuevas + modificación de Cliente/Plan/BitacoraNegocio + re-seed de patentes/roles + semillas.
- [x] **DAL de negocio** — Experiencia, Reserva (cupo atómico en transacción), ReservaHistorial,
      Organizador, Categoria, Ciudad, Interes, Suscripcion, Calificacion, ListaEspera; adaptadas
      `Cliente` (OUTER APPLY a Suscripcion) y `PlanSuscripcion`; DV de Reserva/Experiencia/Organizador;
      `DAL.csproj` actualizado; `Backup`/`Usuario` reapuntados a las tablas nuevas.
- [x] **BLL de negocio con reglas 1–11** — `Reserva` (cupo, solapamiento, límite mensual, ciudad,
      ya-realizada, cancelación anticipada libera cupo + devuelve beneficio, promoción de lista de
      espera), `Experiencia`, `Organizador`, `Categoria`, `Ciudad`, `Suscripcion` (renovar/suspender),
      `ListaEspera`, `Calificacion`, `Recomendacion`; adaptadas `Cliente`/`PlanSuscripcion`/
      `Configuracion` (DV)/`PanelAlertas`/`ReporteJornada`/`Bitacora`; `BLL.csproj` actualizado.
- [x] **GUI** — forms nuevos code-only: `Experiencias`, `Reservas`, `ReservasRealizadas`,
      `Organizadores`, `Categorias`, `Ciudades`, `RecomendacionesForm`; `DashboardForm` reescrito con
      KPIs nuevos; `Menu` rewireado (+ menú "Catálogos" por código); `ClienteForm`/`Clientes`/`Planes`
      adaptados; forms/dashboards obsoletos eliminados; `GUI.csproj` actualizado.
- [x] **Compilación verificada con MSBuild**: los 6 proyectos (BE, Seguridad, DAL, Servicios, BLL,
      GUI) compilan con **0 errores y 0 warnings**; se genera `GUI/bin/Debug/GUI.exe`. El proyecto
      `Tests` también compila (algunos tests de negocio quedan como pendientes de re-adaptar a las
      nuevas reglas, pero no rompen el build).

> **Nota técnica:** los `.csproj` NO son SDK-style: usan listas `<Compile Include>` explícitas.
> Cada archivo agregado/eliminado debe reflejarse en el `.csproj` de su capa, o no compila.

## Cómo ponerlo en marcha

1. Crear la BD base y migrar el dominio (SQL Server):
   `BD/01_Crear_BaseDeDatos.sql` → `BD/02_Actualizar_BaseDeDatos.sql` → `BD/05_Dominio_ExperienceHub.sql`
2. Abrir `IngSoftware-Bolivar,Morana.slnx` en Visual Studio (o compilar `GUI/GUI.csproj` con MSBuild).
3. Ejecutar. Login inicial `admin` / `administrador1!` (usuarios sembrados por `01`).

> La cadena de conexión sigue apuntando al catálogo `ExperienceHubDB` (`GUI/App.config`,
> `DAL/Acceso.cs`). Es interno; si querés renombrarlo a `ExperienceHubDB`, cambialo en esos dos
> lugares y en los `USE` de los scripts SQL.
