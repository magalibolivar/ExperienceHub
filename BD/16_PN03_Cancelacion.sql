/* ============================================================================
   16 - PN03 : Cancelacion de reserva y reasignacion de cupo por lista de espera
   ----------------------------------------------------------------------------
   Agrega lo que PN03 necesita en BD, todo idempotente:

   1) Tabla [Parametro] (clave/valor) con los parametros de negocio configurables
      de la oferta de cupo (regla 5), leidos por BLL.Configuracion:
        - OfertaVencimientoHoras = 24  -> plazo de respuesta de una oferta.
        - OfertaMargenInicioHoras = 24 -> margen minimo antes del inicio: pasado
          ese punto la oferta vence y el cupo queda libre para reserva directa.

   2) Patentes de accion separadas (regla 9), ADITIVAS (no se quita ninguna
      patente existente, nadie pierde acceso):
        - mnuReservasCancelar     -> Cancelar Reservas      (Agente de Reservas + Admin)
        - mnuExperienciasCancelar -> Cancelar Experiencias  (Coordinador + Admin)
      ("Gestionar Lista de Espera" ya existe como mnuListaEspera).

   No se agregan columnas a tablas con DVH, por lo que no hace falta recalcular
   DVH/DVV en este script. Pensado para ejecutarse sobre bases ya instaladas; en
   instalaciones limpias el mismo contenido va al final de 00_Instalacion_Completa.sql.
   ============================================================================ */

-- 1) Parametros de negocio -------------------------------------------------
IF OBJECT_ID('Parametro','U') IS NULL
BEGIN
    CREATE TABLE Parametro (
        Clave       NVARCHAR(100) NOT NULL PRIMARY KEY,
        Valor       NVARCHAR(400) NOT NULL,
        Descripcion NVARCHAR(400) NULL
    );
    PRINT 'Tabla Parametro creada.';
END
GO
IF NOT EXISTS (SELECT 1 FROM Parametro WHERE Clave = 'OfertaVencimientoHoras')
    INSERT INTO Parametro (Clave, Valor, Descripcion) VALUES
        ('OfertaVencimientoHoras', '24',
         N'PN03 (regla 5) - Horas de vigencia de una oferta de cupo de lista de espera antes de vencer y pasar al siguiente.');
IF NOT EXISTS (SELECT 1 FROM Parametro WHERE Clave = 'OfertaMargenInicioHoras')
    INSERT INTO Parametro (Clave, Valor, Descripcion) VALUES
        ('OfertaMargenInicioHoras', '24',
         N'PN03 (regla 5) - Margen minimo antes del inicio: si falta menos, la oferta vence y el cupo queda libre para reserva directa.');
GO
PRINT 'PN03: parametros de la oferta de cupo verificados.';
GO

-- 2) Patentes de accion separadas (regla 9) --------------------------------
DECLARE @nuevasPat TABLE (NombreMenu NVARCHAR(100), Nombre NVARCHAR(150));
INSERT INTO @nuevasPat VALUES
    (N'mnuReservasCancelar',     N'Cancelar Reservas'),
    (N'mnuExperienciasCancelar', N'Cancelar Experiencias');

INSERT INTO Permiso (Nombre, NombreMenu, EsFamilia, EsRol, Estado)
SELECT n.Nombre, n.NombreMenu, 0, 0, 1
FROM @nuevasPat n
WHERE NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.NombreMenu = n.NombreMenu);
GO

-- 3) Conceder las patentes a los roles correctos (ADITIVO) -----------------
DECLARE @admin  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE EsRol=1 AND Nombre LIKE '%Administrador%' ORDER BY IdPermiso);
DECLARE @agente INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE EsRol=1 AND Nombre = N'Agente de Reservas'          ORDER BY IdPermiso);
DECLARE @coord  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE EsRol=1 AND Nombre = N'Coordinador de Experiencias' ORDER BY IdPermiso);

-- Cancelar Reservas -> Agente de Reservas + Administrador
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT r.IdRol, p.IdPermiso
FROM (SELECT @agente AS IdRol UNION ALL SELECT @admin) r
JOIN Permiso p ON p.NombreMenu = 'mnuReservasCancelar' AND p.EsRol = 0
WHERE r.IdRol IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = r.IdRol AND x.IdHijo = p.IdPermiso);

-- Cancelar Experiencias -> Coordinador de Experiencias + Administrador
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT r.IdRol, p.IdPermiso
FROM (SELECT @coord AS IdRol UNION ALL SELECT @admin) r
JOIN Permiso p ON p.NombreMenu = 'mnuExperienciasCancelar' AND p.EsRol = 0
WHERE r.IdRol IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = r.IdRol AND x.IdHijo = p.IdPermiso);
GO
PRINT 'PN03: patentes de cancelacion (Reservas/Experiencias) creadas y asignadas a los roles.';
GO

-- 4) Reserva.IdSuscripcion: con que suscripcion se hizo la reserva -----------
-- Necesario para devolver el beneficio del plan a la suscripcion CORRECTA (regla 2):
-- la que consumio la reserva, y solo si sigue vigente. NO entra al DVH de Reserva
-- (DV_Columnas no cambia), por lo que no hace falta recalcular DVH/DVV por esta columna.
IF COL_LENGTH('Reserva','IdSuscripcion') IS NULL
    ALTER TABLE Reserva ADD IdSuscripcion INT NULL REFERENCES Suscripcion(IdSuscripcion);
GO
-- Backfill para filas previas a PN03 (solo las que no tienen suscripcion asignada): la suscripcion
-- del cliente cuyo PERIODO [FechaInicio <= FechaReserva < FechaVencimiento] contiene la fecha de la
-- reserva. Si hay 0 o mas de 1 coincidencia, queda NULL (ambiguo / indeterminable). Idempotente:
-- nunca pisa un valor ya asignado por la app.
UPDATE r
SET r.IdSuscripcion = x.IdSub
FROM Reserva r
CROSS APPLY (
    SELECT CASE WHEN COUNT(*) = 1 THEN MIN(s.IdSuscripcion) ELSE NULL END AS IdSub
    FROM Suscripcion s
    WHERE s.IdCliente   = r.IdCliente
      AND s.FechaInicio <= r.FechaReserva
      AND (s.FechaVencimiento IS NULL OR r.FechaReserva < s.FechaVencimiento)
) x
WHERE r.IdSuscripcion IS NULL;
GO
PRINT 'PN03: columna Reserva.IdSuscripcion verificada y backfilleada.';
GO
-- IdSuscripcion ahora entra al DVH de Reserva (DAL.Reserva.DV_Columnas). El DVH/DVV lo calcula
-- el algoritmo de la app (C#), NO se puede recalcular en T-SQL. Para re-sellar con el formato nuevo
-- SIN que la verificacion lo trate como corrupcion, se resetea el DVH de Reserva, se borra su DVV y
-- se deja un MARCADOR de re-sellado (__Reseal:Reserva): en el primer arranque la verificacion ve el
-- marcador, re-sella el DVH/DVV (ahora con IdSuscripcion) y consume el marcador (queda ÍNTEGRO).
-- Sin ese marcador, un DVH en NULL se trataria como manipulacion. Idempotente.
UPDATE Reserva SET DVH = NULL WHERE DVH IS NOT NULL;
GO
DELETE FROM DVVertical WHERE NombreTabla = 'Reserva';
GO
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = '__Reseal:Reserva')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('__Reseal:Reserva', 1, GETDATE());
GO
PRINT 'PN03: DVH/DVV de Reserva reseteado + marcador de re-sellado (IdSuscripcion en el DVH).';
GO

-- Instalacion limpia: las tablas de negocio protegidas nacen con DVH sin calcular (el algoritmo es
-- de la app). Se deja un marcador de re-sellado por tabla AUN NO SELLADA (sin fila DVV propia) para
-- que el primer arranque las selle de forma AUTORIZADA. En bases ya selladas no se toca nada (el
-- criterio de integridad trata un DVH en NULL sin marcador como corrupcion). Idempotente.
DECLARE @prot TABLE (T NVARCHAR(100));
INSERT INTO @prot VALUES (N'Cliente'), (N'Empleado'), (N'Experiencia'), (N'Organizador');
INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo)
SELECT N'__Reseal:' + p.T, 1, GETDATE()
FROM @prot p
-- "aun no sellada" = sin fila DVV propia O con DVV = 0 (placeholder sin sellar). Un DVV<>0 significa
-- que la app ya sello la tabla: en ese caso NO se pone marcador (un DVH en NULL sin marcador = corrupcion).
WHERE NOT EXISTS (SELECT 1 FROM DVVertical d WHERE d.NombreTabla = p.T AND d.DVV <> 0)
  AND NOT EXISTS (SELECT 1 FROM DVVertical d WHERE d.NombreTabla = N'__Reseal:' + p.T);
GO
PRINT 'PN03: marcadores de re-sellado de tablas de negocio verificados (instalacion limpia).';
GO

-- 5) Mapeo de los botones de cancelacion a su patente (regla 9) --------------
-- FormBase aplica ManejadorSeguridad por form.Name + NombreControl: cada boton de cancelar
-- queda visible solo para los roles con la patente correspondiente (el Admin ve todo por bypass).
INSERT INTO ControlMapeado (IdPermiso, Formulario, NombreControl)
SELECT p.IdPermiso, v.Formulario, v.NombreControl
FROM (VALUES
    ('mnuReservasCancelar',     'Reservas',     'btnCancelarReserva'),
    ('mnuExperienciasCancelar', 'Experiencias', 'btnCancelarExperiencia')
) AS v(NombreMenu, Formulario, NombreControl)
INNER JOIN Permiso p ON p.NombreMenu = v.NombreMenu
                    AND ISNULL(p.EsFamilia,0) = 0 AND ISNULL(p.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM ControlMapeado x
                  WHERE x.Formulario = v.Formulario AND x.NombreControl = v.NombreControl);
GO
PRINT 'PN03: mapeo de botones Cancelar (Reservas/Experiencias) aplicado.';
GO
