/* ============================================================================
   08 - PN01: separación de funciones Venta / Caja (T04 Composite)
   ----------------------------------------------------------------------------
   Crea dos patentes distintas para que el backend distinga:
     • mnuContratacionVenta → crear y formalizar la contratación (Venta)
     • mnuContratacionCaja  → cobrar y cancelar por intentos (Caja)
   Asigna Venta al rol "Agente de Reservas" y crea un rol "Caja" con la patente
   de cobro. El Administrador conserva acceso total por bypass de perfil.
   Ejecutar DESPUÉS de 06_Alinear_Roles_ExperienceHub.sql.
   ============================================================================ */

-- Patentes (hojas del Composite: EsFamilia=0, EsRol=0)
IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuContratacionVenta')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Comercialización - Venta (contratación)', 'mnuContratacionVenta', NULL, 1, 0, 0);

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuContratacionCaja')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Comercialización - Caja (cobro)', 'mnuContratacionCaja', NULL, 1, 0, 0);

-- Rol "Caja" (nodo-rol del Composite: EsFamilia=1, EsRol=1)
IF NOT EXISTS (SELECT 1 FROM Permiso WHERE Nombre = N'Caja' AND EsRol = 1)
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Caja', NULL, N'Rol', 1, 1, 1);
GO

DECLARE @venta     INT = (SELECT IdPermiso FROM Permiso WHERE NombreMenu = 'mnuContratacionVenta');
DECLARE @caja      INT = (SELECT IdPermiso FROM Permiso WHERE NombreMenu = 'mnuContratacionCaja');
DECLARE @rolAgente INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas' AND EsRol = 1);
DECLARE @rolCaja   INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Caja' AND EsRol = 1);

-- Venta → Agente de Reservas (quien atiende y formaliza)
IF @rolAgente IS NOT NULL AND @venta IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre = @rolAgente AND IdHijo = @venta)
    INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolAgente, @venta);

-- Caja → rol Caja (quien cobra). Un Administrador puede asignar este rol a un usuario.
IF @rolCaja IS NOT NULL AND @caja IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre = @rolCaja AND IdHijo = @caja)
    INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolCaja, @caja);

PRINT 'Patentes Venta/Caja y rol Caja configurados.';
GO
