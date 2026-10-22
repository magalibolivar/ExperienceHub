/* ============================================================================
   21 — PN03 : Planificacion de la oferta de experiencias (reporteria + analitica)
   ----------------------------------------------------------------------------
   Todo idempotente. Agrega:
     1) Columnas para cerrar huecos de KPI:
        - Reserva.CancelacionATiempo (BIT NULL) -> entra al DVH (manipularla = corrupcion).
          Backfill de cancelaciones existentes: a tiempo si (inicio - cancelacion) >= 48 h.
          Reseal del DVH/DVV de Reserva con marcador de migracion (lo consume el arranque).
        - ListaEspera.MotivoRetiro (NVARCHAR(20) NULL): Vencida / Rechazada / NoRevalido /
          CierreExperiencia. Existentes quedan NULL; BLL.ListaEspera lo completa en cada retiro.
        - Organizador.Estado (BIT NOT NULL DEFAULT 1): habilita suspender un organizador.
     2) Tablas del plan de accion: PlanAccion, RecomendacionPlan, KpiSnapshot.
     3) Umbrales configurables del motor de recomendaciones en [Parametro].
   NOTA: el rol Gerente de Operaciones y sus patentes se agregan en el paso de GUI/permisos.
   ============================================================================ */

-- 1) Reserva.CancelacionATiempo -------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Reserva' AND COLUMN_NAME='CancelacionATiempo')
BEGIN
    ALTER TABLE Reserva ADD CancelacionATiempo BIT NULL;
    PRINT 'PN03: Reserva.CancelacionATiempo agregada.';
END
GO
-- Backfill: cancelada (Estado=2) con fechas conocidas -> a tiempo si faltaban >= 48 h al inicio.
UPDATE r SET CancelacionATiempo =
    CASE WHEN DATEDIFF(HOUR, r.FechaCancelacion,
                       (CAST(e.Fecha AS DATETIME) + CAST(e.HoraInicio AS DATETIME))) >= 48
         THEN 1 ELSE 0 END
FROM Reserva r
JOIN Experiencia e ON e.IdExperiencia = r.IdExperiencia
WHERE r.Estado = 2 AND r.FechaCancelacion IS NOT NULL AND r.CancelacionATiempo IS NULL;
GO
-- CancelacionATiempo entra al DVH de Reserva: resetear DVH/DVV y dejar marcador de re-sellado
-- (el arranque lo consume recalculando una sola vez; sin pasos manuales).
UPDATE Reserva SET DVH = NULL;
DELETE FROM DVVertical WHERE NombreTabla = 'Reserva';
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = '__Reseal:Reserva')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('__Reseal:Reserva', 1, GETDATE());
PRINT 'PN03: DVH/DVV de Reserva reseteado + marcador de re-sellado (CancelacionATiempo en el DVH).';
GO

-- 2) ListaEspera.MotivoRetiro ---------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='ListaEspera' AND COLUMN_NAME='MotivoRetiro')
BEGIN
    ALTER TABLE ListaEspera ADD MotivoRetiro NVARCHAR(20) NULL;  -- Vencida/Rechazada/NoRevalido/CierreExperiencia
    PRINT 'PN03: ListaEspera.MotivoRetiro agregada.';
END
GO

-- 3) Organizador.Estado (baja logica / suspension) ------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Organizador' AND COLUMN_NAME='Estado')
BEGIN
    ALTER TABLE Organizador ADD Estado BIT NOT NULL DEFAULT 1;
    PRINT 'PN03: Organizador.Estado agregada.';
END
GO

-- 4) PlanAccion -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PlanAccion')
BEGIN
    CREATE TABLE PlanAccion (
        IdPlanAccion   INT           IDENTITY(1,1) PRIMARY KEY,
        Titulo         NVARCHAR(200) NOT NULL,
        PeriodoDesde   DATE          NOT NULL,
        PeriodoHasta   DATE          NOT NULL,
        FiltroCiudad       INT       NULL,
        FiltroCategoria    INT       NULL,
        FiltroOrganizador  INT       NULL,
        Estado         INT           NOT NULL DEFAULT 0,   -- 0 Borrador, 1 Aprobado, 2 EnEjecucion, 3 Cerrado
        IdGerente      INT           NULL REFERENCES Usuario(IdUsuario),
        FechaCreacion  DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaCierre    DATETIME      NULL
    );
    PRINT 'PN03: tabla PlanAccion creada.';
END
GO

-- 5) RecomendacionPlan ----------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='RecomendacionPlan')
BEGIN
    CREATE TABLE RecomendacionPlan (
        IdRecomendacion INT           IDENTITY(1,1) PRIMARY KEY,
        IdPlanAccion    INT           NOT NULL REFERENCES PlanAccion(IdPlanAccion),
        Tipo            INT           NOT NULL,   -- 0 ListaEsperaAlta,1 OcupacionBaja,2 OrganizadorRevisar,3 PlanSubutilizado
        IdEntidad       INT           NULL,       -- id de experiencia/organizador/plan segun Tipo
        NombreEntidad   NVARCHAR(200) NULL,
        Evidencia       NVARCHAR(1000) NOT NULL,  -- datos que justifican la recomendacion (texto legible)
        AccionSugerida  INT           NOT NULL,   -- 0 ProgramarEdicion,1 AmpliarCupo,2 CancelarExperiencia,3 NoReprogramar,4 SuspenderOrganizador,5 RevisarPlan
        Decision        INT           NOT NULL DEFAULT 0,  -- 0 Pendiente, 1 Aprobada, 2 Descartada
        MotivoDecision  NVARCHAR(400) NULL,
        Ejecutada       BIT           NOT NULL DEFAULT 0,
        FechaEjecucion  DATETIME      NULL
    );
    PRINT 'PN03: tabla RecomendacionPlan creada.';
END
GO

-- 6) KpiSnapshot (comparativo antes/despues al cerrar el plan) -------------------
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='KpiSnapshot')
BEGIN
    CREATE TABLE KpiSnapshot (
        IdSnapshot    INT           IDENTITY(1,1) PRIMARY KEY,
        IdPlanAccion  INT           NOT NULL REFERENCES PlanAccion(IdPlanAccion),
        Momento       INT           NOT NULL,   -- 0 Antes, 1 Despues
        Clave         NVARCHAR(100) NOT NULL,
        Valor         DECIMAL(18,4) NOT NULL,
        Fecha         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'PN03: tabla KpiSnapshot creada.';
END
GO

-- 7) Umbrales configurables del motor de recomendaciones ------------------------
IF OBJECT_ID('Parametro','U') IS NOT NULL
BEGIN
    MERGE Parametro AS t
    USING (VALUES
        ('PN03.UmbralListaEsperaAlta', '5',   'Inscripciones en espera a partir de las cuales se recomienda abrir edicion/ampliar cupo.'),
        ('PN03.UmbralOcupacionBaja',   '40',  'Ocupacion (%) por debajo de la cual se recomienda revisar/cancelar la experiencia.'),
        ('PN03.UmbralCalifBaja',       '3.0', 'Calificacion promedio por debajo de la cual se recomienda revisar al organizador.'),
        ('PN03.UmbralInasistencia',    '30',  'Inasistencia (%) por encima de la cual se recomienda revisar al organizador.'),
        ('PN03.UmbralPlanSubutil',     '25',  'Uso del plan (%) por debajo del cual se recomienda revisar beneficios.')
    ) AS s(Clave, Valor, Descripcion)
    ON t.Clave = s.Clave
    WHEN NOT MATCHED THEN
        INSERT (Clave, Valor, Descripcion) VALUES (s.Clave, s.Valor, s.Descripcion);
    PRINT 'PN03: umbrales del motor de recomendaciones verificados en Parametro.';
END
GO
