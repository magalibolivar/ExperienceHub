/* ============================================================================
   ExperienceHub — Migración de DOMINIO sobre la base ExperienceHub.
   Ejecutar DESPUÉS de 01_Crear_BaseDeDatos.sql y 02_Actualizar_BaseDeDatos.sql.

   Conserva toda la infraestructura (Usuario, Permiso, PermisoRelacion, RolPermiso,
   ControlMapeado, Control, Traduccion, Idioma, Bitacora, HistorialUsuario,
   HistorialIntegridad, ClaveRecuperacion, Preferencia, DVVertical, Empleado).

   Reemplaza el dominio de negocio:
     - Elimina : Prenda, Pedido, PedidoPrenda, PedidoHistorial, MantenimientoPrenda
     - Crea    : Ciudad, Categoria, Organizador, Interes, ClienteInteres,
                 Experiencia, Suscripcion, Reserva, ReservaHistorial,
                 ListaEspera, Calificacion
     - Modifica: Cliente (ciudad en vez de método de pago/plan directo),
                 PlanSuscripcion (beneficios de experiencias),
                 BitacoraNegocio (IdReserva/IdExperiencia)
   Idempotente: puede re-ejecutarse.
   ============================================================================ */
SET NOCOUNT ON;
USE ExperienceHubDB;   -- (renombrar catálogo a ExperienceHubDB es opcional; ver README)
GO

/* ----------------------------------------------------------------------------
   1) ELIMINAR TABLAS DE NEGOCIO OBSOLETAS (respetando dependencias FK)
   ---------------------------------------------------------------------------- */
-- BitacoraNegocio tiene FKs a Pedido/Prenda (columnas IdPedido/IdPrenda). Se quitan esos
-- constraints antes de dropear las tablas; las columnas se renombran más abajo (sección 11).
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

IF OBJECT_ID('PedidoPrenda','U')      IS NOT NULL DROP TABLE PedidoPrenda;
IF OBJECT_ID('PedidoHistorial','U')   IS NOT NULL DROP TABLE PedidoHistorial;
IF OBJECT_ID('MantenimientoPrenda','U') IS NOT NULL DROP TABLE MantenimientoPrenda;
IF OBJECT_ID('Pedido','U')            IS NOT NULL DROP TABLE Pedido;
IF OBJECT_ID('Prenda','U')            IS NOT NULL DROP TABLE Prenda;
GO

/* ----------------------------------------------------------------------------
   2) CATÁLOGOS NUEVOS
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('Ciudad','U') IS NULL
BEGIN
    CREATE TABLE Ciudad (
        IdCiudad  INT IDENTITY(1,1) PRIMARY KEY,
        Nombre    NVARCHAR(100) NOT NULL,
        Provincia NVARCHAR(100) NULL,
        Estado    BIT NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID('Categoria','U') IS NULL
BEGIN
    CREATE TABLE Categoria (
        IdCategoria INT IDENTITY(1,1) PRIMARY KEY,
        Nombre      NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(400) NULL,
        Estado      BIT NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID('Organizador','U') IS NULL
BEGIN
    CREATE TABLE Organizador (
        IdOrganizador INT IDENTITY(1,1) PRIMARY KEY,
        Nombre        NVARCHAR(150) NOT NULL,
        Contacto      NVARCHAR(150) NULL,
        Telefono      NVARCHAR(50)  NULL,
        Mail          NVARCHAR(150) NULL,
        Estado        BIT NOT NULL DEFAULT 1,
        DVH           INT NULL
    );
END
GO

IF OBJECT_ID('Interes','U') IS NULL
BEGIN
    CREATE TABLE Interes (
        IdInteres INT IDENTITY(1,1) PRIMARY KEY,
        Nombre    NVARCHAR(80) NOT NULL UNIQUE
    );
END
GO

/* ----------------------------------------------------------------------------
   3) MODIFICAR CLIENTE  (ciudad + intereses; se retira lógica de stock/plan directo)
   ---------------------------------------------------------------------------- */
IF COL_LENGTH('Cliente','IdCiudad') IS NULL
    ALTER TABLE Cliente ADD IdCiudad INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Cliente_Ciudad')
    ALTER TABLE Cliente ADD CONSTRAINT FK_Cliente_Ciudad
        FOREIGN KEY (IdCiudad) REFERENCES Ciudad(IdCiudad);
GO
-- MetodoPago ya no aplica; se deja la columna por compatibilidad de datos, opcional dropear:
-- IF COL_LENGTH('Cliente','MetodoPago') IS NOT NULL ALTER TABLE Cliente DROP COLUMN MetodoPago;

-- Relación N:M Cliente-Interes
IF OBJECT_ID('ClienteInteres','U') IS NULL
BEGIN
    CREATE TABLE ClienteInteres (
        IdCliente INT NOT NULL REFERENCES Cliente(IdCliente),
        IdInteres INT NOT NULL REFERENCES Interes(IdInteres),
        CONSTRAINT PK_ClienteInteres PRIMARY KEY (IdCliente, IdInteres)
    );
END
GO

/* ----------------------------------------------------------------------------
   4) MODIFICAR PLANSUSCRIPCION  (LimitePrendas → ReservasMensuales + beneficios)
   ---------------------------------------------------------------------------- */
IF COL_LENGTH('PlanSuscripcion','ReservasMensuales') IS NULL
    ALTER TABLE PlanSuscripcion ADD ReservasMensuales INT NOT NULL DEFAULT 4;
GO
IF COL_LENGTH('PlanSuscripcion','AnticipacionMaximaDias') IS NULL
    ALTER TABLE PlanSuscripcion ADD AnticipacionMaximaDias INT NOT NULL DEFAULT 30;
GO
IF COL_LENGTH('PlanSuscripcion','AccesoPremium') IS NULL
    ALTER TABLE PlanSuscripcion ADD AccesoPremium BIT NOT NULL DEFAULT 0;
GO
IF COL_LENGTH('PlanSuscripcion','PrioridadListaEspera') IS NULL
    ALTER TABLE PlanSuscripcion ADD PrioridadListaEspera BIT NOT NULL DEFAULT 0;
GO
IF COL_LENGTH('PlanSuscripcion','CantidadInvitados') IS NULL
    ALTER TABLE PlanSuscripcion ADD CantidadInvitados INT NOT NULL DEFAULT 0;
GO
-- Migrar el viejo LimitePrendas a ReservasMensuales si venía cargado y aún existe:
IF COL_LENGTH('PlanSuscripcion','LimitePrendas') IS NOT NULL
    UPDATE PlanSuscripcion SET ReservasMensuales = LimitePrendas WHERE ReservasMensuales = 4;
GO

/* ----------------------------------------------------------------------------
   5) EXPERIENCIA  (reemplaza a Prenda)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('Experiencia','U') IS NULL
BEGIN
    CREATE TABLE Experiencia (
        IdExperiencia   INT IDENTITY(1,1) PRIMARY KEY,
        Nombre          NVARCHAR(150) NOT NULL,
        Descripcion     NVARCHAR(1000) NULL,
        IdCategoria     INT NOT NULL REFERENCES Categoria(IdCategoria),
        IdCiudad        INT NOT NULL REFERENCES Ciudad(IdCiudad),
        Ubicacion       NVARCHAR(250) NULL,
        Fecha           DATE NOT NULL,
        HoraInicio      TIME(0) NOT NULL,
        DuracionMinutos INT NOT NULL DEFAULT 60,
        IdOrganizador   INT NOT NULL REFERENCES Organizador(IdOrganizador),
        CupoMaximo      INT NOT NULL,
        CupoDisponible  INT NOT NULL,
        EdadMinima      INT NOT NULL DEFAULT 0,
        Premium         BIT NOT NULL DEFAULT 0,
        Estado          INT NOT NULL DEFAULT 0,   -- EstadoExperiencia
        DVH             INT NULL
    );
END
GO

/* ----------------------------------------------------------------------------
   6) SUSCRIPCION  (Cliente → Suscripción → Plan; renovar/suspender)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('Suscripcion','U') IS NULL
BEGIN
    CREATE TABLE Suscripcion (
        IdSuscripcion         INT IDENTITY(1,1) PRIMARY KEY,
        IdCliente             INT NOT NULL REFERENCES Cliente(IdCliente),
        IdPlan                INT NOT NULL REFERENCES PlanSuscripcion(IdPlan),
        FechaInicio           DATE NOT NULL,
        FechaVencimiento      DATE NULL,
        Estado                INT NOT NULL DEFAULT 0,   -- EstadoSuscripcion
        ReservasConsumidasMes INT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_Suscripcion_Cliente ON Suscripcion(IdCliente);
END
GO

/* ----------------------------------------------------------------------------
   7) RESERVA  (reemplaza a Pedido)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('Reserva','U') IS NULL
BEGIN
    CREATE TABLE Reserva (
        IdReserva         INT IDENTITY(1,1) PRIMARY KEY,
        IdCliente         INT NOT NULL REFERENCES Cliente(IdCliente),
        IdExperiencia     INT NOT NULL REFERENCES Experiencia(IdExperiencia),
        IdEmpleado        INT NULL REFERENCES Empleado(IdEmpleado),
        Estado            INT NOT NULL DEFAULT 0,   -- EstadoReserva
        FechaReserva      DATETIME NOT NULL DEFAULT GETDATE(),
        CantidadInvitados INT NOT NULL DEFAULT 0,
        FechaCancelacion  DATETIME NULL,
        MotivoCancelacion NVARCHAR(300) NULL,
        DVH               INT NULL
    );
    CREATE INDEX IX_Reserva_Cliente     ON Reserva(IdCliente);
    CREATE INDEX IX_Reserva_Experiencia ON Reserva(IdExperiencia);
END
GO

/* ----------------------------------------------------------------------------
   8) RESERVAHISTORIAL  (clon de PedidoHistorial)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('ReservaHistorial','U') IS NULL
BEGIN
    CREATE TABLE ReservaHistorial (
        IdHistorial   INT IDENTITY(1,1) PRIMARY KEY,
        IdReserva     INT NOT NULL REFERENCES Reserva(IdReserva),
        IdOperacion   INT NOT NULL,
        Fecha         DATETIME NOT NULL DEFAULT GETDATE(),
        IdUsuario     INT NULL,
        NombreUsuario NVARCHAR(100) NULL,
        Accion        NVARCHAR(30)  NOT NULL,
        Campo         NVARCHAR(50)  NOT NULL,
        ValorAnterior NVARCHAR(400) NULL,
        ValorNuevo    NVARCHAR(400) NULL
    );
    CREATE INDEX IX_ReservaHistorial_Reserva ON ReservaHistorial(IdReserva);
END
GO

/* ----------------------------------------------------------------------------
   9) LISTAESPERA  (FIFO por experiencia)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('ListaEspera','U') IS NULL
BEGIN
    CREATE TABLE ListaEspera (
        IdListaEspera INT IDENTITY(1,1) PRIMARY KEY,
        IdExperiencia INT NOT NULL REFERENCES Experiencia(IdExperiencia),
        IdCliente     INT NOT NULL REFERENCES Cliente(IdCliente),
        Posicion      INT NOT NULL,
        FechaIngreso  DATETIME NOT NULL DEFAULT GETDATE(),
        Estado        INT NOT NULL DEFAULT 0    -- EstadoListaEspera
    );
    CREATE INDEX IX_ListaEspera_Experiencia ON ListaEspera(IdExperiencia);
END
GO

/* ----------------------------------------------------------------------------
   10) CALIFICACION  (post-asistencia)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('Calificacion','U') IS NULL
BEGIN
    CREATE TABLE Calificacion (
        IdCalificacion INT IDENTITY(1,1) PRIMARY KEY,
        IdReserva      INT NOT NULL REFERENCES Reserva(IdReserva),
        IdCliente      INT NOT NULL REFERENCES Cliente(IdCliente),
        IdExperiencia  INT NOT NULL REFERENCES Experiencia(IdExperiencia),
        Puntaje        INT NOT NULL CHECK (Puntaje BETWEEN 1 AND 5),
        Comentario     NVARCHAR(600) NULL,
        Fecha          DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_Calificacion_Experiencia ON Calificacion(IdExperiencia);
END
GO

/* ----------------------------------------------------------------------------
   11) BITACORANEGOCIO  (renombrar columnas IdPedido/IdPrenda → IdReserva/IdExperiencia)
   ---------------------------------------------------------------------------- */
IF COL_LENGTH('BitacoraNegocio','IdReserva') IS NULL AND COL_LENGTH('BitacoraNegocio','IdPedido') IS NOT NULL
    EXEC sp_rename 'BitacoraNegocio.IdPedido', 'IdReserva', 'COLUMN';
GO
IF COL_LENGTH('BitacoraNegocio','IdExperiencia') IS NULL AND COL_LENGTH('BitacoraNegocio','IdPrenda') IS NOT NULL
    EXEC sp_rename 'BitacoraNegocio.IdPrenda', 'IdExperiencia', 'COLUMN';
GO
-- Si la tabla se crea desde cero en otra instalación, ambas columnas nacen con el nombre nuevo.
IF COL_LENGTH('BitacoraNegocio','IdReserva')     IS NULL ALTER TABLE BitacoraNegocio ADD IdReserva INT NULL;
IF COL_LENGTH('BitacoraNegocio','IdExperiencia') IS NULL ALTER TABLE BitacoraNegocio ADD IdExperiencia INT NULL;
GO

/* ----------------------------------------------------------------------------
   12) DÍGITOS VERIFICADORES — registrar las tablas nuevas protegidas
   ---------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Experiencia')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('Experiencia', 0, GETDATE());
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Reserva')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('Reserva', 0, GETDATE());
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Organizador')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('Organizador', 0, GETDATE());
GO
-- Quitar registros DV de tablas eliminadas
DELETE FROM DVVertical WHERE NombreTabla IN ('Prenda','Pedido');
GO

/* ----------------------------------------------------------------------------
   13) PERMISOS (PATENTES) — renombrar viejas + crear nuevas del dominio
        Permiso: EsFamilia=0, EsRol=0  →  patente (hoja del Composite)
   ---------------------------------------------------------------------------- */
-- Renombrar patentes de módulos que cambian de nombre pero conservan semántica de "ver/editar"
UPDATE Permiso SET NombreMenu='mnuExperiencias',       Nombre='Ver Experiencias'        WHERE NombreMenu='mnuPrendas';
UPDATE Permiso SET NombreMenu='mnuExperienciasEditar', Nombre='Gestionar Experiencias'  WHERE NombreMenu='mnuStockEditar';
UPDATE Permiso SET NombreMenu='mnuReservas',           Nombre='Ver Reservas'            WHERE NombreMenu='mnuPedidosVenta';
UPDATE Permiso SET NombreMenu='mnuReservasEditar',     Nombre='Gestionar Reservas'      WHERE NombreMenu='mnuPedidosVentaEditar';
UPDATE Permiso SET NombreMenu='mnuReservasRealizadas',       Nombre='Ver Reservas Realizadas'       WHERE NombreMenu='mnuPedidosRealizados';
UPDATE Permiso SET NombreMenu='mnuReservasRealizadasEditar', Nombre='Gestionar Reservas Realizadas' WHERE NombreMenu='mnuPedidosRealizadosEditar';
GO
-- Retirar la patente de "ver stock" que ya no aplica (queda cubierta por Experiencias).
-- Se limpian primero todas las referencias (PermisoRelacion + RolPermiso) para no violar FKs.
DECLARE @idStock INT = (SELECT IdPermiso FROM Permiso WHERE NombreMenu='mnuStock');
IF @idStock IS NOT NULL
BEGIN
    DELETE FROM PermisoRelacion WHERE IdPadre = @idStock OR IdHijo = @idStock;
    IF COL_LENGTH('RolPermiso','IdPermiso')    IS NOT NULL DELETE FROM RolPermiso    WHERE IdPermiso = @idStock;
    IF COL_LENGTH('ControlMapeado','IdPermiso') IS NOT NULL DELETE FROM ControlMapeado WHERE IdPermiso = @idStock;
    DELETE FROM Permiso WHERE IdPermiso = @idStock;
END
GO

-- Alta idempotente de patentes nuevas
DECLARE @nuevas TABLE (NombreMenu NVARCHAR(100), Nombre NVARCHAR(150));
-- Literales Unicode con prefijo N (evita el mojibake de acentos en columnas NVARCHAR).
INSERT INTO @nuevas VALUES
    (N'mnuOrganizadores',       N'Ver Organizadores'),
    (N'mnuOrganizadoresEditar', N'Gestionar Organizadores'),
    (N'mnuCategorias',          N'Ver Categorías'),
    (N'mnuCategoriasEditar',    N'Gestionar Categorías'),
    (N'mnuCiudades',            N'Ver Ciudades'),
    (N'mnuCiudadesEditar',      N'Gestionar Ciudades'),
    (N'mnuListaEspera',         N'Gestionar Lista de Espera');

INSERT INTO Permiso (Nombre, NombreMenu, EsFamilia, EsRol, Estado)
SELECT n.Nombre, n.NombreMenu, 0, 0, 1
FROM @nuevas n
WHERE NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.NombreMenu = n.NombreMenu);
GO

-- Conceder todas las patentes nuevas al rol Administrador (que las tiene todas)
DECLARE @idAdmin INT = (SELECT TOP 1 IdPermiso FROM Permiso WHERE EsRol=1 AND Nombre LIKE '%Administrador%');
IF @idAdmin IS NOT NULL
    INSERT INTO PermisoRelacion (IdPadre, IdHijo)
    SELECT @idAdmin, p.IdPermiso
    FROM Permiso p
    WHERE p.NombreMenu IN ('mnuOrganizadores','mnuOrganizadoresEditar','mnuCategorias',
                           'mnuCategoriasEditar','mnuCiudades','mnuCiudadesEditar','mnuListaEspera')
      AND NOT EXISTS (SELECT 1 FROM PermisoRelacion r WHERE r.IdPadre=@idAdmin AND r.IdHijo=p.IdPermiso);
GO

/* ----------------------------------------------------------------------------
   14) DATOS SEMILLA MÍNIMOS del nuevo dominio (idempotentes)
   ---------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM Ciudad)
    INSERT INTO Ciudad (Nombre, Provincia) VALUES
        ('CABA','Buenos Aires'), ('La Plata','Buenos Aires'),
        ('Córdoba','Córdoba'),   ('Rosario','Santa Fe'), ('Mendoza','Mendoza');

IF NOT EXISTS (SELECT 1 FROM Categoria)
    INSERT INTO Categoria (Nombre, Descripcion) VALUES
        ('Taller','Talleres y clases prácticas'),
        ('Cata','Degustaciones de vinos y bebidas'),
        ('Teatro','Obras y espectáculos en vivo'),
        ('Escape Room','Juegos de escape presenciales'),
        ('Gastronomía','Experiencias culinarias'),
        ('Pintura','Arte y pintura'),
        ('Cocina','Clases de cocina'),
        ('Fotografía','Salidas y talleres de foto'),
        ('Aire Libre','Actividades al aire libre'),
        ('Cultural','Eventos culturales');

IF NOT EXISTS (SELECT 1 FROM Interes)
    INSERT INTO Interes (Nombre) VALUES
        ('Arte'), ('Vinos'), ('Teatro'), ('Cocina'), ('Fotografía'),
        ('Naturaleza'), ('Música'), ('Historia'), ('Juegos'), ('Gastronomía');

IF NOT EXISTS (SELECT 1 FROM Organizador)
    INSERT INTO Organizador (Nombre, Contacto, Telefono, Mail) VALUES
        ('Bodega Los Andes','María Pérez','11-5555-1000','contacto@losandes.com'),
        ('Teatro Central','Juan Gómez','11-5555-2000','info@teatrocentral.com'),
        ('EscapeLab','Sofía Ruiz','11-5555-3000','hola@escapelab.com');
GO

PRINT 'ExperienceHub — dominio migrado correctamente.';
GO
