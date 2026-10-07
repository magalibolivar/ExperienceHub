/* ============================================================================
   14 - Ciclo de vida de la suscripcion : columna PeriodoConsumo
   ----------------------------------------------------------------------------
   Agrega el sello del periodo de consumo mensual a Suscripcion. La tarea de
   sistema (BLL.Suscripcion.ActualizarEstadosPorFecha) usa esta columna para:
     - reiniciar ReservasConsumidasMes cuando cambia el mes (cupo mensual renovado)
     - marcar Activa -> Vencida cuando pasa FechaVencimiento
   Migracion idempotente (en instalaciones nuevas la columna ya nace en
   05_Dominio_ExperienceHub.sql). Ejecutar DESPUES del 05.
   ============================================================================ */
IF COL_LENGTH('Suscripcion','PeriodoConsumo') IS NULL
    ALTER TABLE Suscripcion ADD PeriodoConsumo DATE NULL;
GO
PRINT 'Suscripcion: columna PeriodoConsumo verificada.';
GO
