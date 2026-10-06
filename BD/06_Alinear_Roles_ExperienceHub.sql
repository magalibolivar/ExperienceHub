-- ============================================================================
-- 06_Alinear_Roles_ExperienceHub.sql
-- Alinea roles, usuarios y árbol de permisos al modelo de 4 ROLES de la consigna:
--     Administrador · Agente de Reservas · Coordinador de Experiencias · Auditor
--
-- Consolida los roles operativos heredados de WardrobeFlow:
--     Vendedor / GerenteComercial / Supervisor          → Agente de Reservas
--     GerenteInventario / OperadorDeInventario /
--     OperadorLogistico / (Controlador|Encargado)DeStock → Coordinador de Experiencias
--
-- Idempotente: se puede ejecutar varias veces sin duplicar ni romper nada.
-- ============================================================================
SET NOCOUNT ON;
GO

-- 1) Renombrar los dos roles que se conservan como base de los nuevos.
UPDATE Permiso SET Nombre = N'Agente de Reservas'
    WHERE Nombre = N'Vendedor'          AND EsRol = 1;
UPDATE Permiso SET Nombre = N'Coordinador de Experiencias'
    WHERE Nombre = N'GerenteInventario' AND EsRol = 1;
GO

-- 1b) IDEMPOTENCIA: si una corrida previa ya renombró el rol y el seed base volvió a crear
--     el rol origen (que el paso 1 vuelve a renombrar), quedan DOS filas con el mismo Nombre
--     y los DECLARE escalares fallan ("subquery returned more than 1 value"). Se eliminan las
--     COPIAS (toda fila-rol con otra del mismo Nombre de menor IdPermiso), conservando la
--     canónica. Como el paso 2 reconstruye las patentes de estos roles, no hace falta repuntar
--     relaciones: se borran primero las relaciones de las copias (FK) y luego las copias.
DELETE r FROM PermisoRelacion r
WHERE EXISTS (SELECT 1 FROM Permiso p WHERE p.IdPermiso IN (r.IdPadre, r.IdHijo) AND p.EsRol = 1
              AND EXISTS (SELECT 1 FROM Permiso o WHERE o.EsRol = 1 AND o.Nombre = p.Nombre AND o.IdPermiso < p.IdPermiso));
GO
DELETE p FROM Permiso p
WHERE p.EsRol = 1
  AND EXISTS (SELECT 1 FROM Permiso o WHERE o.EsRol = 1 AND o.Nombre = p.Nombre AND o.IdPermiso < p.IdPermiso);
GO

DECLARE @agente INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas'          AND EsRol = 1 ORDER BY IdPermiso);
DECLARE @coord  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1 ORDER BY IdPermiso);

-- 2) Limpiar TODAS las relaciones que involucren a los roles reconstruidos u obsoletos
--    (como padre o como hijo), para rearmarlas limpias.
DELETE r FROM PermisoRelacion r
JOIN Permiso p ON p.IdPermiso = r.IdPadre
WHERE p.Nombre IN (N'Agente de Reservas', N'Coordinador de Experiencias',
                   N'GerenteComercial', N'OperadorDeInventario', N'OperadorLogistico');

DELETE r FROM PermisoRelacion r
JOIN Permiso h ON h.IdPermiso = r.IdHijo
WHERE h.Nombre IN (N'GerenteComercial', N'OperadorDeInventario', N'OperadorLogistico',
                   N'Agente de Reservas', N'Coordinador de Experiencias');
GO

DECLARE @agente INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas'          AND EsRol = 1 ORDER BY IdPermiso);
DECLARE @coord  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1 ORDER BY IdPermiso);

-- 3) Patentes del Agente de Reservas (operación comercial + lectura del catálogo).
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT @agente, pat.IdPermiso
FROM Permiso pat
WHERE pat.EsRol = 0
  AND pat.NombreMenu IN ('mnuClientes','mnuReservas','mnuReservasRealizadas','mnuListaEspera','mnuExperiencias')
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = @agente AND x.IdHijo = pat.IdPermiso);

-- 4) Patentes del Coordinador de Experiencias (catálogo + referencias + planes).
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT @coord, pat.IdPermiso
FROM Permiso pat
WHERE pat.EsRol = 0
  AND pat.NombreMenu IN ('mnuExperiencias','mnuOrganizadores','mnuOrganizadoresEditar',
                         'mnuCategorias','mnuCategoriasEditar','mnuCiudades','mnuCiudadesEditar',
                         'mnuPlanSuscripciones')
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = @coord AND x.IdHijo = pat.IdPermiso);
GO

-- 5) Reasignar los usuarios semilla a los 4 roles.
UPDATE Usuario SET Rol = N'Agente de Reservas', Perfil = N'Agente de Reservas'
WHERE Rol    IN (N'Vendedor', N'GerenteComercial', N'Supervisor')
   OR Perfil IN (N'Vendedor', N'GerenteComercial', N'Supervisor');

UPDATE Usuario SET Rol = N'Coordinador de Experiencias', Perfil = N'Coordinador de Experiencias'
WHERE Rol    IN (N'GerenteInventario', N'OperadorDeInventario', N'OperadorLogistico', N'ControladorDeStock', N'EncargadoDeStock')
   OR Perfil IN (N'GerenteInventario', N'OperadorDeInventario', N'OperadorLogistico', N'ControladorDeStock', N'EncargadoDeStock');
GO

-- 6) Eliminar las filas de rol obsoletas (ya sin usuarios ni relaciones).
DELETE FROM Permiso WHERE EsRol = 1
  AND Nombre IN (N'GerenteComercial', N'OperadorDeInventario', N'OperadorLogistico');
GO

-- 7) Normalizar los emails de dominio (resabio de WardrobeFlow).
UPDATE Usuario SET Email = REPLACE(Email, '@wardrobeflow.com', '@experiencehub.com')
WHERE Email LIKE '%@wardrobeflow.com';
GO

-- 8) Forzar recálculo de integridad (DVH/DVV) en el próximo arranque, ya que
--    se modificaron filas de la tabla Usuario (protegida por dígitos verificadores).
UPDATE Usuario SET DVH = 0;
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Usuario')
    UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Usuario';
GO

PRINT 'Roles alineados a ExperienceHub (Administrador, Agente de Reservas, Coordinador de Experiencias, Auditor).';
GO
