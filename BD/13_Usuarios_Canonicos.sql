/* ============================================================================
   13 - USUARIOS CANONICOS ExperienceHub (un usuario por rol)
   ----------------------------------------------------------------------------
   Normaliza la tabla Usuario al set definitivo del sistema, dejando EXACTAMENTE
   5 usuarios (uno por rol) con clave = usuario + "1!":

     admin / admin1!             -> Administrador
     agente / agente1!           -> Agente de Reservas
     coordinador / coordinador1! -> Coordinador de Experiencias
     caja / caja1!               -> Caja
     auditor / auditor1!         -> Auditor

   Reemplaza los usuarios heredados (vendedor/gcomercial/ginventario/operador/
   stock/logistico/encargado/supervisor/admin2...) del andamiaje WardrobeFlow.

   Las claves son hashes PBKDF2-SHA256 generados con Seguridad.Encriptador.Hash
   (salt+hash Base64); fueron verificados con VerificarContrasena.

   Idempotente y determinista: se puede correr varias veces y siempre converge a
   los 5 usuarios. Ejecutar AL FINAL (despues de 06/08 y del seed base).
   ============================================================================ */
SET NOCOUNT ON;
GO

/* 1) Renombrar los equivalentes heredados (preserva Id, vinculos de Empleado y
      Bitacora). Solo si el nombre canonico no existe todavia. */
IF EXISTS (SELECT 1 FROM Usuario WHERE Username='gcomercial') AND NOT EXISTS (SELECT 1 FROM Usuario WHERE Username='agente')
    UPDATE Usuario SET Username='agente' WHERE Username='gcomercial';
IF EXISTS (SELECT 1 FROM Usuario WHERE Username='encargado') AND NOT EXISTS (SELECT 1 FROM Usuario WHERE Username='coordinador')
    UPDATE Usuario SET Username='coordinador' WHERE Username='encargado';
GO

/* 2) Asegurar que existan los 5 canonicos (insertar los que falten). La clave real
      se fija en el paso 3; aca va un placeholder. */
INSERT INTO Usuario (Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH, IdIdioma, Activo)
SELECT v.U, '-', v.R, v.R, 1, 0, 0, 'ES', 1
FROM (VALUES ('admin','Administrador'), ('agente','Agente de Reservas'),
             ('coordinador','Coordinador de Experiencias'), ('caja','Caja'), ('auditor','Auditor')) AS v(U, R)
WHERE NOT EXISTS (SELECT 1 FROM Usuario u WHERE u.Username = v.U);
GO

/* 3) Fijar Rol/Perfil + clave (hash real) + estado limpio (activo, sin bloqueos,
      sin cambio forzado) en los 5 canonicos. */
UPDATE Usuario SET Rol='Administrador', Perfil='Administrador',
    Clave='/uRWTqyZXDCEcN9brt2JJpHzr4YVzlW6nLZ58fvxdvV21ax6sCilXwplAscXjrTK',
    Activo=1, IntentosFallidos=0, CantidadBloqueos=0, FechaBloqueo=NULL, RequiereCambioClave=0 WHERE Username='admin';
UPDATE Usuario SET Rol='Agente de Reservas', Perfil='Agente de Reservas',
    Clave='sS99ahwf6AeZYIo/lTNYRRWxo0ksv525P1BDlUkqvkfwQy67yczO26L6MIemcJSK',
    Activo=1, IntentosFallidos=0, CantidadBloqueos=0, FechaBloqueo=NULL, RequiereCambioClave=0 WHERE Username='agente';
UPDATE Usuario SET Rol='Coordinador de Experiencias', Perfil='Coordinador de Experiencias',
    Clave='0ruDnB0C/uyzwFbrZeF2unKtiBN7k0OZS+6jZR0ydvcHkY8fHtE0EmJhCLc//1Fm',
    Activo=1, IntentosFallidos=0, CantidadBloqueos=0, FechaBloqueo=NULL, RequiereCambioClave=0 WHERE Username='coordinador';
UPDATE Usuario SET Rol='Caja', Perfil='Caja',
    Clave='kIoCLIxyY+2/wuvPpTyrPB7F5ajwV4oieXky1GZ+bDdPa2c9WjK0egU9BdITegrG',
    Activo=1, IntentosFallidos=0, CantidadBloqueos=0, FechaBloqueo=NULL, RequiereCambioClave=0 WHERE Username='caja';
UPDATE Usuario SET Rol='Auditor', Perfil='Auditor',
    Clave='nK1gJIHA4+8FJqwcISUgD1wHPKl9e+hlwGVO7c+L0EGR64gP5QPix+L1WBlId1+r',
    Activo=1, IntentosFallidos=0, CantidadBloqueos=0, FechaBloqueo=NULL, RequiereCambioClave=0 WHERE Username='auditor';
GO

/* 4) Vincular Empleados a los operativos (el Agente necesita Empleado para reservar). */
IF OBJECT_ID('Empleado','U') IS NOT NULL
BEGIN
    UPDATE Empleado SET IdUsuario = (SELECT IdUsuario FROM Usuario WHERE Username='agente')       WHERE Legajo='L-001';
    UPDATE Empleado SET IdUsuario = (SELECT IdUsuario FROM Usuario WHERE Username='coordinador')  WHERE Legajo='L-002';
    UPDATE Empleado SET IdUsuario = (SELECT IdUsuario FROM Usuario WHERE Username='caja')          WHERE Legajo='L-003';
END
GO

/* 5) Limpiar referencias FK de los usuarios NO canonicos y borrarlos. */
DECLARE @nocanon TABLE (Id INT);
INSERT INTO @nocanon SELECT IdUsuario FROM Usuario WHERE Username NOT IN ('admin','agente','coordinador','caja','auditor');

-- Unicas FKs reales hacia Usuario: HistorialUsuario, Preferencia, Bitacora,
-- BitacoraNegocio, Empleado. Se limpian/null-ean antes de borrar los usuarios.
DELETE FROM HistorialUsuario WHERE IdUsuario IN (SELECT Id FROM @nocanon);
DELETE FROM Preferencia      WHERE IdUsuario IN (SELECT Id FROM @nocanon);
UPDATE Bitacora        SET usuario   = NULL WHERE usuario   IN (SELECT Id FROM @nocanon);
UPDATE BitacoraNegocio SET IdUsuario = NULL WHERE IdUsuario IN (SELECT Id FROM @nocanon);
UPDATE Empleado        SET IdUsuario = NULL WHERE IdUsuario IN (SELECT Id FROM @nocanon);

DELETE FROM Usuario WHERE IdUsuario IN (SELECT Id FROM @nocanon);
GO

/* 6) Recalculo UNICO del DV de Usuario en el proximo arranque (Username/Clave estan
      en el DVH). No bloquea el login: mismo mecanismo que el resto del instalador. */
UPDATE Usuario SET DVH = 0;
GO
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla='Usuario')
    UPDATE DVVertical SET DVV=0 WHERE NombreTabla='Usuario';
ELSE
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('Usuario', 0, GETDATE());
GO
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla='__FormatoDVUsuario__')
    UPDATE DVVertical SET DVV=1, FechaCalculo=GETDATE() WHERE NombreTabla='__FormatoDVUsuario__';
ELSE
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('__FormatoDVUsuario__', 1, GETDATE());
GO

PRINT 'Usuarios canonicos: admin, agente, coordinador, caja, auditor (clave = usuario + 1!).';
GO
