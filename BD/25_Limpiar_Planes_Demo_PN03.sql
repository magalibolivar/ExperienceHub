/* ============================================================================
   25 - PN03: LIMPIEZA SELECTIVA de los planes de accion de demostracion
   ----------------------------------------------------------------------------
   Borra UNICAMENTE los planes sembrados por 24_Planes_Demo_PN03.sql, es decir los
   que tienen el prefijo '[DEMO]' en el Titulo, junto con sus recomendaciones y
   snapshots. NO toca ningun plan real creado desde la aplicacion.

   ⚠ NO ES AUTOMATICO: este script existe para ejecutarse MANUALMENTE y con
   autorizacion. Primero MOSTRA lo que se afectaria (bloque de diagnostico) y, solo
   si estas de acuerdo, descomenta el bloque de borrado.

   Idempotente y acotado por 'Titulo LIKE [[]DEMO]%' → no puede afectar planes
   operativos (que nunca llevan ese prefijo). No modifica esquema.
   ============================================================================ */
SET NOCOUNT ON;
GO

-- 1) DIAGNOSTICO: que registros se afectarian (solo lectura). -------------------
PRINT '=== Planes [DEMO] que se eliminarian ===';
SELECT IdPlanAccion, Titulo, Estado FROM PlanAccion WHERE Titulo LIKE '[[]DEMO]%' ORDER BY IdPlanAccion;
SELECT
    (SELECT COUNT(*) FROM PlanAccion WHERE Titulo LIKE '[[]DEMO]%') AS PlanesDemo,
    (SELECT COUNT(*) FROM RecomendacionPlan r JOIN PlanAccion p ON p.IdPlanAccion=r.IdPlanAccion WHERE p.Titulo LIKE '[[]DEMO]%') AS RecsDemo,
    (SELECT COUNT(*) FROM KpiSnapshot k JOIN PlanAccion p ON p.IdPlanAccion=k.IdPlanAccion WHERE p.Titulo LIKE '[[]DEMO]%') AS SnapsDemo,
    (SELECT COUNT(*) FROM PlanAccion WHERE Titulo NOT LIKE '[[]DEMO]%') AS PlanesReales_SE_CONSERVAN;
GO

-- 2) BORRADO (DESCOMENTAR SOLO TRAS AUTORIZACION). -----------------------------
-- BEGIN TRANSACTION;
--     DELETE ks FROM KpiSnapshot ks JOIN PlanAccion p ON p.IdPlanAccion=ks.IdPlanAccion WHERE p.Titulo LIKE '[[]DEMO]%';
--     DELETE rp FROM RecomendacionPlan rp JOIN PlanAccion p ON p.IdPlanAccion=rp.IdPlanAccion WHERE p.Titulo LIKE '[[]DEMO]%';
--     DELETE FROM PlanAccion WHERE Titulo LIKE '[[]DEMO]%';
-- COMMIT;   -- revisá los conteos antes de confirmar; si algo no cuadra, ROLLBACK;
-- PRINT 'Planes [DEMO] eliminados. Los planes reales quedaron intactos.';
GO
