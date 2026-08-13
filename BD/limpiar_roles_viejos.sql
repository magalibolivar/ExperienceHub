-- Residuo WardrobeFlow: roles y familias desactivados de versiones previas
-- (ControladorDeStock, Supervisor, EncargadoDeStock, Inventario, Sistema, Ventas).
-- Verificado: no están en el árbol Composite (PermisoRelacion) ni son usados por roles vigentes.
USE ExperienceHubDB;
SET NOCOUNT ON;
BEGIN TRAN;

-- Ids: 11 Inventario, 12 Sistema, 13 Ventas (familias) · 15 ControladorDeStock, 17 Supervisor, 22 EncargadoDeStock (roles)
DELETE FROM PermisoRelacion WHERE IdPadre IN (11,12,13,15,17,22) OR IdHijo IN (11,12,13,15,17,22);
DELETE FROM RolPermiso      WHERE Rol IN ('ControladorDeStock','Supervisor','EncargadoDeStock','Inventario','Sistema','Ventas');
DELETE FROM RolPermiso      WHERE IdPermiso IN (11,12,13,15,17,22);
DELETE FROM Permiso         WHERE IdPermiso IN (11,12,13,15,17,22);

COMMIT;

SELECT IdPermiso, Nombre, EsRol, EsFamilia, Estado FROM Permiso ORDER BY EsRol DESC, EsFamilia DESC, IdPermiso;
