/* ============================================================================
   17 - Limpieza de objetos LEGACY (WardrobeFlow) que ExperienceHub no usa
   ----------------------------------------------------------------------------
   Idempotente (IF EXISTS / COL_LENGTH). Elimina las tablas, patentes y marcas de
   dígito verificador heredadas del base WardrobeFlow y que el runtime de
   ExperienceHub no referencia (usa Reserva/ReservaHistorial/Experiencia/ListaEspera).

   Se aplica tanto en instalaciones nuevas (al final de 00_Instalacion_Completa.sql)
   como en la migración post-restore (DAL.Migracion), para que un backup viejo quede
   limpio al restaurarlo. Con el criterio de integridad nuevo, dejar una tabla
   "registrada" (en DVVertical) que no existe NO debe ocurrir: por eso también se
   quitan sus filas/marcadores de DVVertical.
   ============================================================================ */

-- 1) Desarmar FKs de BitacoraNegocio -> Pedido/Prenda si un backup viejo las trajo.
IF OBJECT_ID('BitacoraNegocio','U') IS NOT NULL
BEGIN
    DECLARE @dropFk NVARCHAR(MAX) = N'';
    SELECT @dropFk = @dropFk + 'ALTER TABLE BitacoraNegocio DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';'
    FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = OBJECT_ID('BitacoraNegocio')
      AND OBJECT_NAME(fk.referenced_object_id) IN ('Pedido','Prenda');
    IF @dropFk <> N'' EXEC sp_executesql @dropFk;
END
GO
-- 1b) Si el backup viejo trae las columnas con el nombre legacy, renombrarlas a las vivas.
IF COL_LENGTH('BitacoraNegocio','IdReserva') IS NULL AND COL_LENGTH('BitacoraNegocio','IdPedido') IS NOT NULL
    EXEC sp_rename 'BitacoraNegocio.IdPedido', 'IdReserva', 'COLUMN';
GO
IF COL_LENGTH('BitacoraNegocio','IdExperiencia') IS NULL AND COL_LENGTH('BitacoraNegocio','IdPrenda') IS NOT NULL
    EXEC sp_rename 'BitacoraNegocio.IdPrenda', 'IdExperiencia', 'COLUMN';
GO

-- 2) Tablas legacy (hijas primero por las FKs).
IF OBJECT_ID('PedidoPrenda','U')        IS NOT NULL DROP TABLE PedidoPrenda;
IF OBJECT_ID('PedidoHistorial','U')     IS NOT NULL DROP TABLE PedidoHistorial;
IF OBJECT_ID('MantenimientoPrenda','U') IS NOT NULL DROP TABLE MantenimientoPrenda;
IF OBJECT_ID('Pedido','U')              IS NOT NULL DROP TABLE Pedido;
IF OBJECT_ID('Prenda','U')              IS NOT NULL DROP TABLE Prenda;
GO

-- 3) Filas de DVVertical de tablas legacy + sus marcadores de re-sellado.
IF OBJECT_ID('DVVertical','U') IS NOT NULL
    DELETE FROM DVVertical WHERE NombreTabla IN
        ('Pedido','Prenda','PedidoPrenda','PedidoHistorial','MantenimientoPrenda',
         '__Reseal:Pedido','__Reseal:Prenda','__Reseal:PedidoPrenda','__Reseal:PedidoHistorial','__Reseal:MantenimientoPrenda');
GO

-- 4) Patentes muertas que el módulo 05 NO renombra a dominio: mnuOutfits (y mnuStock,
--    que 05 ya elimina; acá de forma defensiva). NO se tocan mnuPrendas/mnuStockEditar/
--    mnuPedidos*, que 05 RENOMBRA a las patentes vivas (mnuExperiencias*/mnuReservas*).
--    Se limpian primero las referencias (PermisoRelacion / RolPermiso / ControlMapeado)
--    para no violar FKs, y recién después la fila de Permiso. Patrón idéntico al del 05.
DECLARE @muerta NVARCHAR(100);
DECLARE curMuertas CURSOR LOCAL FAST_FORWARD FOR
    SELECT NombreMenu FROM (VALUES (N'mnuOutfits'), (N'mnuStock')) AS v(NombreMenu);
OPEN curMuertas;
FETCH NEXT FROM curMuertas INTO @muerta;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @idPat INT = (SELECT IdPermiso FROM Permiso WHERE NombreMenu = @muerta);
    IF @idPat IS NOT NULL
    BEGIN
        DELETE FROM PermisoRelacion WHERE IdPadre = @idPat OR IdHijo = @idPat;
        IF COL_LENGTH('RolPermiso','IdPermiso')     IS NOT NULL DELETE FROM RolPermiso     WHERE IdPermiso = @idPat;
        IF COL_LENGTH('ControlMapeado','IdPermiso') IS NOT NULL DELETE FROM ControlMapeado WHERE IdPermiso = @idPat;
        DELETE FROM Permiso WHERE IdPermiso = @idPat;
    END
    FETCH NEXT FROM curMuertas INTO @muerta;
END
CLOSE curMuertas;
DEALLOCATE curMuertas;
GO
PRINT 'Limpieza legacy (tablas/patentes/DV de WardrobeFlow) aplicada.';
GO
