/* ============================================================================
   09 - Ajustes de entrega (higiene de roles / datos iniciales)
   ----------------------------------------------------------------------------
   Idempotente y resuelto POR NOMBRE (no por IdPermiso: los Id cambian en una
   instalacion limpia). Ejecutar DESPUES de 06/07/08.

   (a) Crea un usuario 'caja' con el rol Caja, para poder demostrar la
       separacion Venta/Caja del PN01 sin tener que crearlo a mano.
   (b) Elimina roles/familias residuales del proyecto base (WardrobeFlow) que
       ningun usuario vigente usa (Supervisor, ControladorDeStock, etc.).
   (c) Mueve la patente "Planes de Suscripcion" (producto comercial del PN01)
       del Coordinador de Experiencias al Agente de Reservas (quien vende).
   ============================================================================ */
USE ExperienceHubDB;
GO

-- (a) Usuario 'caja' (rol Caja). Clave: usuario1! (mismo hash PBKDF2 que los otros
--     usuarios de ejemplo). DVH=0 -> se recalcula en el arranque (ver bloque final).
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE Username = 'caja')
    INSERT INTO Usuario (Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH, IdIdioma)
    VALUES ('caja', 'SmnGp5hqSC+FXdbLiccYieNpC6vEaWn6nVpgZFrlyyeOXqx8yTjJYOgaw+oXAP7B',
            N'Caja', N'Caja', 1, 0, 0, 'ES');
GO

-- (c) "Planes de Suscripcion" es el producto comercial del PN01: lo ve quien vende
--     (Agente de Reservas), no el curador de catalogo (Coordinador de Experiencias).
--     El Administrador lo sigue viendo por bypass de perfil.
DECLARE @plan   INT = (SELECT IdPermiso FROM Permiso WHERE NombreMenu = 'mnuPlanSuscripciones');
DECLARE @agente INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas' AND EsRol = 1);
DECLARE @coord  INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1);

IF @plan IS NOT NULL AND @agente IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre = @agente AND IdHijo = @plan)
    INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@agente, @plan);

IF @plan IS NOT NULL AND @coord IS NOT NULL
    DELETE FROM PermisoRelacion WHERE IdPadre = @coord AND IdHijo = @plan;
GO

-- (b) Roles/familias residuales del proyecto base que NINGUN usuario vigente usa.
--     Se resuelven por nombre y se borran en cascada (relaciones + asignaciones + nodo).
DECLARE @ghost TABLE (IdPermiso INT);
INSERT INTO @ghost (IdPermiso)
SELECT p.IdPermiso FROM Permiso p
WHERE p.Nombre IN (N'ControladorDeStock', N'Supervisor', N'EncargadoDeStock',
                   N'Vendedor', N'OperadorDeInventario',
                   N'Inventario', N'Sistema', N'Ventas')
  AND NOT EXISTS (SELECT 1 FROM Usuario u WHERE u.Rol = p.Nombre);

DELETE FROM PermisoRelacion
 WHERE IdPadre IN (SELECT IdPermiso FROM @ghost)
    OR IdHijo  IN (SELECT IdPermiso FROM @ghost);

DELETE FROM RolPermiso
 WHERE IdPermiso IN (SELECT IdPermiso FROM @ghost)
    OR Rol IN (N'ControladorDeStock', N'Supervisor', N'EncargadoDeStock',
               N'Vendedor', N'OperadorDeInventario', N'Inventario', N'Sistema', N'Ventas');

DELETE FROM Permiso WHERE IdPermiso IN (SELECT IdPermiso FROM @ghost);
GO

-- Forzar un recalculo UNICO del DV de la tabla Usuario en el proximo arranque, para
-- que el usuario 'caja' recien insertado (DVH=0) no dispare el bloqueo de integridad
-- en una base YA inicializada. Se hace bajando el marcador de formato por debajo del
-- actual: BLL.Configuracion lo detecta y recalcula una sola vez sin bloquear. En una
-- instalacion limpia es redundante (todos los DVH arrancan en 0 y ya se recalculan).
IF EXISTS (SELECT 1 FROM Usuario WHERE DVH = 0 OR DVH IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = '__FormatoDVUsuario__')
        UPDATE DVVertical SET DVV = 1, FechaCalculo = GETDATE() WHERE NombreTabla = '__FormatoDVUsuario__';
    ELSE
        INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('__FormatoDVUsuario__', 1, GETDATE());
END
GO
