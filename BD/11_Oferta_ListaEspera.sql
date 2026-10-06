/* ============================================================================
   11 - PN02 Lista de espera : oferta de cupo con plazo de vigencia
   ----------------------------------------------------------------------------
   Agrega FechaOferta a ListaEspera: instante en que se ofrecio el cupo liberado
   al primero de la fila. Desde esa fecha corre el plazo de vigencia de la oferta
   (BLL.ListaEspera.HORAS_VIGENCIA_OFERTA); vencida sin confirmar, el cupo pasa al
   siguiente cliente. Migracion idempotente para bases ya instaladas (en
   instalaciones nuevas la columna ya nace en 05_Dominio_ExperienceHub.sql).
   ============================================================================ */
IF COL_LENGTH('ListaEspera','FechaOferta') IS NULL
    ALTER TABLE ListaEspera ADD FechaOferta DATETIME NULL;
GO
PRINT 'ListaEspera: columna FechaOferta verificada.';
GO
