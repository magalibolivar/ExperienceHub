/* ============================================================================
   24 - PN03: PLANES DE ACCION de demostracion (uno por estado del ciclo)
   ----------------------------------------------------------------------------
   El paso 23 sembro la HISTORIA (reservas/calificaciones) para que el motor
   dispare recomendaciones, pero la pantalla "Planes de accion" nace vacia hasta
   que alguien crea un plan. Este script siembra 4 planes de demo que recorren el
   ciclo completo, para poder VER y demostrar el recorrido de 3 etapas
   (Analizar -> Ejecutar -> Evaluar) y el comparativo Antes/Despues sin tener que
   armarlos a mano:

     A) [DEMO] ... Borrador      (estado 0) -> recomendaciones Pendientes
     B) [DEMO] ... Aprobado      (estado 1) -> decisiones tomadas (aprob/descart)
     C) [DEMO] ... En ejecucion  (estado 2) -> tareas ejecutadas + pendientes + snapshot Antes
     D) [DEMO] ... Cerrado       (estado 3) -> todo ejecutado + snapshots Antes/Despues

   Las recomendaciones "aprobadas sin ejecutar" usan acciones NO destructivas
   (RevisarPlan / NoReprogramar) para que el boton "Ejecutar tarea" sea seguro en
   la demo (no tocan el catalogo). IdEntidad va NULL (no es FK): es solo demo.

   IDEMPOTENTE: borra y recrea solo los planes marcados con el prefijo '[DEMO]'
   en el Titulo (no toca los planes reales del usuario). Se puede correr N veces.
   Ejecutar DESPUES de 21_PN03_Planificacion.sql.
   ============================================================================ */
SET NOCOUNT ON;
GO

/* 0) Limpieza idempotente: borra SOLO los planes de demo previos (hijos primero). */
DELETE ks FROM KpiSnapshot ks
    JOIN PlanAccion p ON p.IdPlanAccion = ks.IdPlanAccion
    WHERE p.Titulo LIKE '[[]DEMO]%';
DELETE rp FROM RecomendacionPlan rp
    JOIN PlanAccion p ON p.IdPlanAccion = rp.IdPlanAccion
    WHERE p.Titulo LIKE '[[]DEMO]%';
DELETE FROM PlanAccion WHERE Titulo LIKE '[[]DEMO]%';
GO

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @desde DATE = DATEADD(MONTH, -3, @hoy);
DECLARE @idPlan INT;

/* ============================================================================
   A) BORRADOR — recomendaciones a la espera de decision (etapa 1 -> 2)
   ============================================================================ */
INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
VALUES (N'[DEMO] Invierno Palermo — Borrador', @desde, @hoy, 0, DATEADD(DAY, -2, GETDATE()));
SET @idPlan = SCOPE_IDENTITY();
INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, Ejecutada) VALUES
    (@idPlan, 1, N'Clase de cerámica artesanal', N'Ocupación 22% (11/50) en el período.',               2, 0, 0),
    (@idPlan, 0, N'Tour nocturno por el casco histórico', N'14 clientes en lista de espera.',            1, 0, 0),
    (@idPlan, 2, N'Eventos Dudosos',                 N'Calificación promedio 2,1 e inasistencia 45%.',    4, 0, 0);

/* ============================================================================
   B) APROBADO — decisiones ya tomadas (aprobadas/descartadas), falta ejecutar
   ============================================================================ */
INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
VALUES (N'[DEMO] Trimestre Córdoba — Aprobado', @desde, @hoy, 1, DATEADD(DAY, -6, GETDATE()));
SET @idPlan = SCOPE_IDENTITY();
INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada) VALUES
    (@idPlan, 0, N'Trekking de otoño',   N'9 clientes en lista de espera.',          1, 1, N'Hay demanda suficiente para ampliar.', 0),
    (@idPlan, 1, N'Taller exprés',        N'Ocupación 18% (9/50).',                    3, 2, N'Se mantiene una edición más por contrato.', 0),
    (@idPlan, 3, N'Plan Premium',         N'Uso del plan 19% en el período.',          5, 1, N'Revisar beneficios con Marketing.', 0);

/* ============================================================================
   C) EN EJECUCION — algunas tareas ejecutadas, otras pendientes + snapshot Antes
   ============================================================================ */
INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
VALUES (N'[DEMO] Otoño Rosario — En ejecución', @desde, @hoy, 2, DATEADD(DAY, -12, GETDATE()));
SET @idPlan = SCOPE_IDENTITY();
INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada, FechaEjecucion) VALUES
    (@idPlan, 0, N'Foto nocturna',        N'11 clientes en lista de espera.',         1, 1, N'Ampliar cupo aprobado.',        1, DATEADD(DAY, -5, GETDATE())),
    (@idPlan, 3, N'Plan Básico',          N'Uso del plan 21% en el período.',         5, 1, N'Revisar beneficios.',           0, NULL),
    (@idPlan, 1, N'Pintura libre',        N'Ocupación 30% (5/15).',                    3, 1, N'Se deja una edición más.',       0, NULL);
-- Snapshot "Antes" = KPIs del período analizado (se congela al crear el plan).
INSERT INTO KpiSnapshot (IdPlanAccion, Momento, Clave, Valor) VALUES
    (@idPlan, 0, 'EnEspera', 28), (@idPlan, 0, 'OfertasVencidas', 6),
    (@idPlan, 0, 'CancelacionesATiempo', 12), (@idPlan, 0, 'CancelacionesTardias', 9),
    (@idPlan, 0, 'Inasistencias', 15), (@idPlan, 0, 'Altas', 20),
    (@idPlan, 0, 'Renovaciones', 8), (@idPlan, 0, 'IngresosPlanes', 95000);

/* ============================================================================
   D) CERRADO — todo ejecutado + comparativo Antes/Despues completo (etapa 3)
   ============================================================================ */
INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion, FechaCierre)
VALUES (N'[DEMO] Verano CABA — Cerrado', DATEADD(MONTH, -6, @hoy), DATEADD(MONTH, -3, @hoy), 3,
        DATEADD(DAY, -40, GETDATE()), DATEADD(DAY, -3, GETDATE()));
SET @idPlan = SCOPE_IDENTITY();
INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada, FechaEjecucion) VALUES
    (@idPlan, 0, N'Cena a ciegas',        N'16 clientes en lista de espera.',         1, 1, N'Ampliación ejecutada.',        1, DATEADD(DAY, -20, GETDATE())),
    (@idPlan, 2, N'Eventos Dudosos',      N'Calificación 2,0 e inasistencia 48%.',     4, 1, N'Organizador suspendido.',      1, DATEADD(DAY, -18, GETDATE())),
    (@idPlan, 1, N'Taller de velas',      N'Ocupación 25% (5/20).',                    3, 2, N'Se decidió no reprogramar.',    0, NULL);
-- Comparativo Antes/Despues (muestra mejora tras las acciones).
INSERT INTO KpiSnapshot (IdPlanAccion, Momento, Clave, Valor) VALUES
    (@idPlan, 0, 'EnEspera', 32),  (@idPlan, 1, 'EnEspera', 11),
    (@idPlan, 0, 'OfertasVencidas', 7),  (@idPlan, 1, 'OfertasVencidas', 3),
    (@idPlan, 0, 'CancelacionesATiempo', 14), (@idPlan, 1, 'CancelacionesATiempo', 10),
    (@idPlan, 0, 'CancelacionesTardias', 11), (@idPlan, 1, 'CancelacionesTardias', 5),
    (@idPlan, 0, 'Inasistencias', 19), (@idPlan, 1, 'Inasistencias', 8),
    (@idPlan, 0, 'Altas', 18), (@idPlan, 1, 'Altas', 27),
    (@idPlan, 0, 'Renovaciones', 7), (@idPlan, 1, 'Renovaciones', 12),
    (@idPlan, 0, 'IngresosPlanes', 88000), (@idPlan, 1, 'IngresosPlanes', 121000);
GO

PRINT 'PN03: 4 planes de accion de demo sembrados (Borrador/Aprobado/EnEjecucion/Cerrado).';
GO
