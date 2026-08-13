-- Limpieza de residuo WardrobeFlow: patente activa "Ver Outfits" (mnuOutfits),
-- que no corresponde al dominio de ExperienceHub y no tiene control de menú asociado.
USE ExperienceHubDB;
SET NOCOUNT ON;
BEGIN TRAN;

DELETE FROM PermisoRelacion WHERE IdHijo = 4;                 -- saca "Ver Outfits" del árbol Composite
DELETE FROM RolPermiso      WHERE IdPermiso = 4;              -- lo saca del rol Administrador
DELETE FROM ControlMapeado  WHERE IdPermiso = 4;             -- (por si hubiera un mapeo)
DELETE FROM Permiso         WHERE IdPermiso = 4 AND NombreMenu = 'mnuOutfits';

COMMIT;

SELECT COUNT(*) AS VerOutfits_restantes FROM Permiso WHERE Nombre = 'Ver Outfits';
SELECT COUNT(*) AS Permisos_Administrador FROM RolPermiso WHERE Rol = 'Administrador';
