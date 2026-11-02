/* ============================================================================
   22 - PN03: rol Gerente de Operaciones + patentes de Planificacion (T04 Composite)
   ----------------------------------------------------------------------------
   El paso 21 (21_PN03_Planificacion.sql) dejo el esquema y los datos del plan de
   accion, pero anoto: "el rol Gerente de Operaciones y sus patentes se agregan en
   el paso de GUI/permisos". Este script cierra ese hueco:

     1) Crea las 4 patentes hoja de PN03 (EsFamilia=0, EsRol=0):
          mnuPlanificacion       -> ver tablero + generar recomendaciones
          mnuPlanAccion          -> ver / crear plan de accion
          mnuPlanAccionAprobar   -> aprobar / descartar recomendaciones y cerrar plan
          mnuPlanAccionEjecutar  -> ejecutar las tareas aprobadas del plan
     2) Crea el rol "Gerente de Operaciones" (EsFamilia=1, EsRol=1) y le asigna
        Planificacion + PlanAccion + PlanAccionAprobar (analiza y decide).
     3) Asigna PlanAccionEjecutar + PlanAccion al "Coordinador de Experiencias"
        (ejecuta las tareas y ve el plan).
     El Administrador conserva acceso total por bypass de perfil (no necesita asignacion).

   Para demostrar el circuito con un login propio de Gerente: desde Administrar ->
   Cuentas de Usuario crear un usuario y, en Perfiles y Permisos, asignarle el rol
   "Gerente de Operaciones" (la clave se hashea desde la app; por eso no se siembra
   aqui por SQL, que no puede generar el PBKDF2).

   IDEMPOTENTE: se puede correr varias veces sin duplicar filas. Reiniciar la app
   despues (el catalogo de patentes se cachea al inicio - BLL.PermisosAccion).
   Ejecutar DESPUES de 06_Alinear_Roles_ExperienceHub.sql y 21_PN03_Planificacion.sql.
   ============================================================================ */
SET NOCOUNT ON;
GO

-- 1) Patentes hoja de PN03 ------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuPlanificacion')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Planificación - Tablero y recomendaciones', 'mnuPlanificacion', NULL, 1, 0, 0);

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuPlanAccion')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Planificación - Plan de acción (ver/crear)', 'mnuPlanAccion', NULL, 1, 0, 0);

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuPlanAccionAprobar')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Planificación - Aprobar / cerrar plan', 'mnuPlanAccionAprobar', NULL, 1, 0, 0);

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE NombreMenu = 'mnuPlanAccionEjecutar')
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Planificación - Ejecutar tareas del plan', 'mnuPlanAccionEjecutar', NULL, 1, 0, 0);

-- 2) Rol "Gerente de Operaciones" (nodo-rol del Composite) ----------------------
IF NOT EXISTS (SELECT 1 FROM Permiso WHERE Nombre = N'Gerente de Operaciones' AND EsRol = 1)
    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
    VALUES (N'Gerente de Operaciones', NULL, N'Rol', 1, 1, 1);
GO

DECLARE @planif    INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE NombreMenu = 'mnuPlanificacion'      ORDER BY IdPermiso);
DECLARE @plan      INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE NombreMenu = 'mnuPlanAccion'         ORDER BY IdPermiso);
DECLARE @aprobar   INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE NombreMenu = 'mnuPlanAccionAprobar'  ORDER BY IdPermiso);
DECLARE @ejecutar  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE NombreMenu = 'mnuPlanAccionEjecutar' ORDER BY IdPermiso);
DECLARE @rolGer    INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Gerente de Operaciones'      AND EsRol = 1 ORDER BY IdPermiso);
DECLARE @rolCoord  INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1 ORDER BY IdPermiso);

-- Helper inline: asignar hijo a padre si no existe aun.
-- Gerente de Operaciones -> ver tablero + plan + aprobar/cerrar.
IF @rolGer IS NOT NULL
BEGIN
    IF @planif  IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre=@rolGer AND IdHijo=@planif)
        INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolGer, @planif);
    IF @plan    IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre=@rolGer AND IdHijo=@plan)
        INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolGer, @plan);
    IF @aprobar IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre=@rolGer AND IdHijo=@aprobar)
        INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolGer, @aprobar);
END

-- Coordinador de Experiencias -> ver plan + ejecutar las tareas aprobadas.
IF @rolCoord IS NOT NULL
BEGIN
    IF @plan     IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre=@rolCoord AND IdHijo=@plan)
        INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolCoord, @plan);
    IF @ejecutar IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre=@rolCoord AND IdHijo=@ejecutar)
        INSERT INTO PermisoRelacion (IdPadre, IdHijo) VALUES (@rolCoord, @ejecutar);
END

PRINT 'PN03: patentes de planificacion + rol Gerente de Operaciones configurados.';
GO
