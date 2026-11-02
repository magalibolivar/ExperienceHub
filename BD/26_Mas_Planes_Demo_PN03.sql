/* ============================================================================
   26 - PN03: MAS PLANES DE ACCION de demostracion
   ----------------------------------------------------------------------------
   Complementa a 24_Planes_Demo_PN03.sql (que sembro 1 plan por estado) con 4
   planes adicionales, tambien uno por estado, pero con mas variedad de tipos de
   accion (ProgramarEdicion/AmpliarCupo/CancelarExperiencia/SuspenderOrganizador)
   y, en el plan Cerrado, un comparativo Antes/Despues MIXTO (algunos indicadores
   mejoran, otros empeoran) en vez de una mejora pareja en todos — mas realista
   para mostrar que la evaluacion es un analisis real, no un tramite.

   IDEMPOTENTE: cada INSERT esta guardado por "IF NOT EXISTS (...Titulo = N'...')",
   asi que correr el script varias veces no duplica los planes.
   Ejecutar DESPUES de 24_Planes_Demo_PN03.sql (no lo reemplaza ni lo toca).
   ============================================================================ */
SET NOCOUNT ON;
GO

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @idPlan INT;

/* ============================================================================
   A) BORRADOR — lanzamiento de experiencias de primavera
   ============================================================================ */
IF NOT EXISTS (SELECT 1 FROM PlanAccion WHERE Titulo = N'Lanzamiento de experiencias de primavera')
BEGIN
    INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
    VALUES (N'Lanzamiento de experiencias de primavera', DATEADD(MONTH, -2, @hoy), @hoy, 0, DATEADD(DAY, -1, GETDATE()));
    SET @idPlan = SCOPE_IDENTITY();
    INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, Ejecutada) VALUES
        (@idPlan, 0, N'Picnic fotográfico en el parque',      N'17 clientes en lista de espera.',               0, 0, 0),
        (@idPlan, 0, N'Clase de huerta urbana',                N'12 clientes en lista de espera.',               1, 0, 0),
        (@idPlan, 1, N'Recorrido en bicicleta nocturno',       N'Ocupación 19% (6/32) en el período.',           2, 0, 0),
        (@idPlan, 1, N'Taller de acuarela al aire libre',      N'Ocupación 24% (9/38) en el período.',           3, 0, 0),
        (@idPlan, 2, N'Rutas Urbanas SRL',                     N'Calificación promedio 2,4 e inasistencia 38%.', 4, 0, 0),
        (@idPlan, 3, N'Plan Explorador',                       N'Uso del plan 16% en el período.',               5, 0, 0);
END
GO

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @idPlan INT;

/* ============================================================================
   B) APROBADO — reorganización de talleres gastronómicos
   ============================================================================ */
IF NOT EXISTS (SELECT 1 FROM PlanAccion WHERE Titulo = N'Reorganización de talleres gastronómicos')
BEGIN
    INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
    VALUES (N'Reorganización de talleres gastronómicos', DATEADD(MONTH, -3, @hoy), @hoy, 1, DATEADD(DAY, -8, GETDATE()));
    SET @idPlan = SCOPE_IDENTITY();
    INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada) VALUES
        (@idPlan, 0, N'Taller de pastas caseras',       N'21 clientes en lista de espera.',               0, 1, N'Hay demanda sostenida: se abre una nueva edición.',  0),
        (@idPlan, 0, N'Masterclass de parrilla',         N'13 clientes en lista de espera.',               1, 1, N'Se amplía cupo sin abrir edición nueva.',            0),
        (@idPlan, 1, N'Cata de cervezas artesanales',    N'Ocupación 15% (3/20) en el período.',           2, 1, N'Baja demanda sostenida: se da de baja.',             0),
        (@idPlan, 1, N'Taller de fermentos',             N'Ocupación 28% (7/25) en el período.',           3, 2, N'Se mantiene una edición más antes de decidir.',      0),
        (@idPlan, 2, N'Cocina Viva Experiencias',        N'Calificación promedio 2,2 e inasistencia 41%.', 4, 1, N'Reiteradas quejas: se suspende al organizador.',     0);
END
GO

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @idPlan INT;

/* ============================================================================
   C) EN EJECUCION — optimización de cupos en Buenos Aires
   ============================================================================ */
IF NOT EXISTS (SELECT 1 FROM PlanAccion WHERE Titulo = N'Optimización de cupos — Buenos Aires')
BEGIN
    INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion)
    VALUES (N'Optimización de cupos — Buenos Aires', DATEADD(MONTH, -3, @hoy), @hoy, 2, DATEADD(DAY, -15, GETDATE()));
    SET @idPlan = SCOPE_IDENTITY();
    INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada, FechaEjecucion) VALUES
        (@idPlan, 0, N'Caminata de observación de aves', N'15 clientes en lista de espera.',               0, 1, N'Nueva edición ejecutada.',             1, DATEADD(DAY, -9, GETDATE())),
        (@idPlan, 0, N'Clase de coctelería',              N'10 clientes en lista de espera.',               1, 1, N'Cupo ampliado.',                       1, DATEADD(DAY, -7, GETDATE())),
        (@idPlan, 1, N'Visita guiada al puerto',          N'Ocupación 17% (4/24) en el período.',           2, 1, N'Experiencia dada de baja.',             1, DATEADD(DAY, -4, GETDATE())),
        (@idPlan, 2, N'Travesías del Río SA',             N'Calificación promedio 2,3 e inasistencia 44%.', 4, 1, N'Organizador suspendido.',              1, DATEADD(DAY, -2, GETDATE())),
        (@idPlan, 1, N'Taller de fotografía analógica',  N'Ocupación 26% (6/23) en el período.',           3, 1, N'Se decide no reprogramar por ahora.',  0, NULL),
        (@idPlan, 3, N'Plan Aventura',                    N'Uso del plan 20% en el período.',               5, 1, N'Pendiente de revisión con Marketing.', 0, NULL);
    -- Snapshot "Antes" = KPIs del período analizado (se congela al crear el plan).
    INSERT INTO KpiSnapshot (IdPlanAccion, Momento, Clave, Valor) VALUES
        (@idPlan, 0, 'EnEspera', 34), (@idPlan, 0, 'OfertasVencidas', 9),
        (@idPlan, 0, 'CancelacionesATiempo', 16), (@idPlan, 0, 'CancelacionesTardias', 10),
        (@idPlan, 0, 'Inasistencias', 21), (@idPlan, 0, 'Altas', 24),
        (@idPlan, 0, 'Renovaciones', 9), (@idPlan, 0, 'IngresosPlanes', 103000);
END
GO

DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @idPlan INT;

/* ============================================================================
   D) CERRADO — reestructuración de oferta de invierno (comparativo MIXTO:
      algunos indicadores mejoran, otros empeoran — no es una mejora pareja).
   ============================================================================ */
IF NOT EXISTS (SELECT 1 FROM PlanAccion WHERE Titulo = N'Reestructuración de oferta — invierno anterior')
BEGIN
    INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, Estado, FechaCreacion, FechaCierre)
    VALUES (N'Reestructuración de oferta — invierno anterior', DATEADD(MONTH, -9, @hoy), DATEADD(MONTH, -6, @hoy), 3,
            DATEADD(DAY, -50, GETDATE()), DATEADD(DAY, -10, GETDATE()));
    SET @idPlan = SCOPE_IDENTITY();
    INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, NombreEntidad, Evidencia, AccionSugerida, Decision, MotivoDecision, Ejecutada, FechaEjecucion) VALUES
        (@idPlan, 0, N'Noche de juegos de mesa',      N'19 clientes en lista de espera.',               0, 1, N'Nueva edición ejecutada.',              1, DATEADD(DAY, -35, GETDATE())),
        (@idPlan, 0, N'Clase de tejido',                N'8 clientes en lista de espera.',                1, 1, N'Cupo ampliado.',                        1, DATEADD(DAY, -30, GETDATE())),
        (@idPlan, 1, N'Avistaje de estrellas',          N'Ocupación 14% (4/28) en el período.',           2, 1, N'Experiencia dada de baja.',              1, DATEADD(DAY, -25, GETDATE())),
        (@idPlan, 2, N'Noches Urbanas Eventos',         N'Calificación promedio 2,1 e inasistencia 47%.', 4, 1, N'Organizador suspendido.',               1, DATEADD(DAY, -20, GETDATE())),
        (@idPlan, 3, N'Plan Social',                     N'Uso del plan 17% en el período.',               5, 2, N'Se mantienen los beneficios sin cambios.', 0, NULL);
    -- Comparativo Antes/Despues MIXTO: espera/ingresos/renovaciones mejoran, pero
    -- cancelaciones e inasistencias empeoran (otros factores externos al plan).
    INSERT INTO KpiSnapshot (IdPlanAccion, Momento, Clave, Valor) VALUES
        (@idPlan, 0, 'EnEspera', 27),  (@idPlan, 1, 'EnEspera', 9),
        (@idPlan, 0, 'OfertasVencidas', 5),  (@idPlan, 1, 'OfertasVencidas', 4),
        (@idPlan, 0, 'CancelacionesATiempo', 11), (@idPlan, 1, 'CancelacionesATiempo', 15),
        (@idPlan, 0, 'CancelacionesTardias', 6),  (@idPlan, 1, 'CancelacionesTardias', 10),
        (@idPlan, 0, 'Inasistencias', 14), (@idPlan, 1, 'Inasistencias', 18),
        (@idPlan, 0, 'Altas', 16), (@idPlan, 1, 'Altas', 22),
        (@idPlan, 0, 'Renovaciones', 6), (@idPlan, 1, 'Renovaciones', 11),
        (@idPlan, 0, 'IngresosPlanes', 79000), (@idPlan, 1, 'IngresosPlanes', 98500);
END
GO

PRINT 'PN03: hasta 4 planes de accion adicionales sembrados (si no existian).';
GO
