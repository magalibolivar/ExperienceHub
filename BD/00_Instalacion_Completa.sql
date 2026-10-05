-- =====================================================================
-- ExperienceHub - Script UNICO de instalacion de base de datos (A01)
--
-- Crea la base ExperienceHubDB completa (esquema + datos iniciales) de
-- una sola pasada, para que el instalador (Instalador\DbInstaller.exe,
-- cliente SQL embebido ADO.NET) la ejecute sin depender de sqlcmd ni de
-- herramientas externas.
--
-- Es la CONCATENACION, en el orden probado 01 -> 09, de los scripts
-- modulares de BD/ (se omiten el 04 de diagnostico/limpieza y los
-- limpiar_/corregir_ que son correcciones puntuales, no parte de una
-- instalacion limpia). Todos los modulos son idempotentes (IF NOT EXISTS
-- / WHERE NOT EXISTS), asi que re-ejecutarlo es seguro.
--
-- Datos iniciales que deja listos para el primer login:
--   - Usuario 'admin' (clave hasheada PBKDF2; ver Credenciales_Iniciales.txt)
--   - Usuario 'caja' con rol Caja (demo de la separacion Venta/Caja, PN01)
--   - Roles/patentes y relaciones de permisos (Composite), ya depurados
--   - Planes de suscripcion
--   - Idiomas y traducciones base
-- Las Experiencias NO se siembran: se cargan desde la app (modulo Experiencias).
--
-- Codificacion: UTF-8 sin BOM. El DbInstaller lo lee como UTF-8.
-- NO editar a mano: regenerar concatenando los modulos de BD/ si cambian.
-- =====================================================================
GO

-- ===================================================================
-- MODULO: 01_Crear_BaseDeDatos.sql
-- ===================================================================
-- ============================================================
-- ExperienceHub — 01. CREAR BASE DE DATOS DE CERO
-- ------------------------------------------------------------
-- Crea la base ExperienceHubDB completa: estructura + datos
-- semilla (permisos, roles, usuarios, idiomas) + árbol Composite.
-- Idempotente: se puede re-ejecutar sin romper datos existentes.
--
-- Usuarios semilla (ver Contraseñas.txt):
--   admin/administrador1!   supervisor/supervisor1!
--   vendedor/vendedor1!     stock/controladorstock1!   operador/operador1!
--
-- Para ACTUALIZAR una BD ya existente, usar: 02_Actualizar_BaseDeDatos.sql
--
-- Orden: 1) crear BD  2) tablas  3) seeds  4) migración Composite  5) datos demo
--
-- NOTA DE CODIFICACIÓN: este archivo es UTF-8 y contiene acentos (Básico, Lucía…).
--   • En SSMS se ejecuta sin problemas.
--   • Con sqlcmd usar el codepage UTF-8:  sqlcmd -S .\SQLEXPRESS -E -f 65001 -i 01_Crear_BaseDeDatos.sql
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ExperienceHubDB')
BEGIN
    CREATE DATABASE ExperienceHubDB;
    PRINT 'Base de datos ExperienceHubDB creada.';
END
ELSE
    PRINT 'Base de datos ExperienceHubDB ya existe — se actualiza su contenido.';
GO

USE ExperienceHubDB;
GO

-- ============================================================
-- TABLAS BASE
-- ============================================================

-- Usuario
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Usuario')
BEGIN
    CREATE TABLE Usuario (
        IdUsuario        INT           IDENTITY(1,1) PRIMARY KEY,
        Username         NVARCHAR(100) NOT NULL,
        Clave            NVARCHAR(500) NOT NULL,
        Rol              NVARCHAR(100) NULL,
        Perfil           NVARCHAR(100) NULL,
        Estado           BIT           NOT NULL DEFAULT 1,
        IntentosFallidos INT           NOT NULL DEFAULT 0,
        DVH              INT           NULL,
        IdIdioma         VARCHAR(5)    NULL,
        Activo           BIT           NOT NULL DEFAULT 1,   -- RF-10: 1=activo, 0=archivado (baja lógica)
        FechaBaja        DATETIME      NULL,                 -- RF-10: fecha de archivado (para purga >1 año)
        CantidadBloqueos INT           NOT NULL DEFAULT 0,   -- Bloqueo progresivo: nº de bloqueos (define duración)
        FechaBloqueo     DATETIME      NULL,                 -- Bloqueo progresivo: instante del último bloqueo
        RequiereCambioClave BIT        NOT NULL DEFAULT 0,   -- 1 = clave temporal/generada pendiente de cambio
        Nombre           NVARCHAR(100) NULL,                 -- ABM: datos administrativos NO sensibles
        Apellido         NVARCHAR(100) NULL,
        FechaNacimiento  DATE          NULL,
        Email            NVARCHAR(200) NULL,
        CONSTRAINT UQ_Usuario_Username UNIQUE (Username)
    );
    PRINT 'Tabla Usuario creada.';
END
ELSE
    PRINT 'Tabla Usuario ya existe — sin cambios.';
GO

-- DVVertical (T07 Dígitos Verificadores)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DVVertical')
BEGIN
    CREATE TABLE DVVertical (
        Id           INT          IDENTITY(1,1) PRIMARY KEY,
        NombreTabla  VARCHAR(100) NOT NULL,
        DVV          INT          NOT NULL,
        FechaCalculo DATETIME     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_DVVertical_Tabla UNIQUE (NombreTabla)
    );
    PRINT 'Tabla DVVertical creada.';
END
ELSE
    PRINT 'Tabla DVVertical ya existe — sin cambios.';
GO

-- PlanSuscripcion
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlanSuscripcion')
BEGIN
    CREATE TABLE PlanSuscripcion (
        IdPlan        INT            IDENTITY(1,1) PRIMARY KEY,
        Nombre        NVARCHAR(100)  NOT NULL,
        LimitePrendas INT            NOT NULL DEFAULT 0,
        Precio        DECIMAL(10, 2) NOT NULL DEFAULT 0,
        Estado        BIT            NOT NULL DEFAULT 1
    );
    PRINT 'Tabla PlanSuscripcion creada.';
END
ELSE
    PRINT 'Tabla PlanSuscripcion ya existe — sin cambios.';
GO

-- Permiso (T04 Composite — EsFamilia discrimina Familia vs Patente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permiso')
BEGIN
    CREATE TABLE Permiso (
        IdPermiso       INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre          NVARCHAR(100) NOT NULL,
        NombreMenu      NVARCHAR(100) NULL,
        TipoComponente  NVARCHAR(100) NULL,
        Estado          BIT           NOT NULL DEFAULT 1,
        EsFamilia       BIT           NOT NULL DEFAULT 0,
        EsRol           BIT           NOT NULL DEFAULT 0
    );
    PRINT 'Tabla Permiso creada.';
END
ELSE
BEGIN
    -- Si ya existe, asegurar que EsFamilia esté presente (migración v6.0)
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = 'Permiso' AND COLUMN_NAME = 'EsFamilia')
    BEGIN
        ALTER TABLE Permiso ADD EsFamilia BIT NOT NULL DEFAULT 0;
        PRINT 'Columna EsFamilia agregada a Permiso (migración).';
    END
    -- EsRol: marca los nodos-rol del Composite (migración v7.0 — T04)
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = 'Permiso' AND COLUMN_NAME = 'EsRol')
    BEGIN
        ALTER TABLE Permiso ADD EsRol BIT NOT NULL DEFAULT 0;
        PRINT 'Columna EsRol agregada a Permiso (migración).';
    END
END
GO

-- Idioma (T05 Multiidioma)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Idioma')
BEGIN
    CREATE TABLE Idioma (
        IdIdioma  INT IDENTITY(1,1) PRIMARY KEY,
        Codigo    VARCHAR(5)    NOT NULL,
        Nombre    NVARCHAR(50)  NOT NULL,
        Activo    BIT           NOT NULL DEFAULT 1,
        EsDefault BIT           NOT NULL DEFAULT 0,
        CONSTRAINT UQ_Idioma_Codigo UNIQUE (Codigo)
    );
    PRINT 'Tabla Idioma creada.';
END
ELSE
    PRINT 'Tabla Idioma ya existe — sin cambios.';
GO

-- Control (claves de traducción — T05)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Control')
BEGIN
    CREATE TABLE Control (
        IdControl  INT IDENTITY(1,1) PRIMARY KEY,
        Clave      VARCHAR(100) NOT NULL,
        Formulario VARCHAR(50)  NOT NULL DEFAULT 'General',
        CONSTRAINT UQ_Control_Clave UNIQUE (Clave)
    );
    PRINT 'Tabla Control creada.';
END
ELSE
    PRINT 'Tabla Control ya existe — sin cambios.';
GO

-- ============================================================
-- TABLAS CON FK
-- ============================================================

-- Bitacora (sistema — refs Usuario)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Bitacora')
BEGIN
    CREATE TABLE Bitacora (
        Id         INT            IDENTITY(1,1) PRIMARY KEY,
        fecha      DATETIME       NOT NULL DEFAULT GETDATE(),
        usuario    INT            NULL REFERENCES Usuario(IdUsuario),
        modulo     NVARCHAR(100)  NULL,
        actividad  NVARCHAR(200)  NULL,
        detalle    NVARCHAR(1000) NULL,
        criticidad INT            NOT NULL DEFAULT 0,
        ip         NVARCHAR(50)   NULL
    );
    PRINT 'Tabla Bitacora creada.';
END
ELSE
    PRINT 'Tabla Bitacora ya existe — sin cambios.';
GO

-- Empleado (refs Usuario — FK nullable: empleado puede no tener usuario del sistema)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Empleado')
BEGIN
    CREATE TABLE Empleado (
        IdEmpleado   INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre       NVARCHAR(100) NOT NULL,
        Apellido     NVARCHAR(100) NOT NULL,
        DNI          NVARCHAR(200) NOT NULL,  -- T03: almacena el DNI CIFRADO (AES Base64)
        Email        NVARCHAR(200) NULL,
        FechaIngreso DATETIME      NOT NULL DEFAULT GETDATE(),
        Puesto       NVARCHAR(100) NULL,
        Legajo       NVARCHAR(50)  NULL,
        IdUsuario    INT           NULL REFERENCES Usuario(IdUsuario),
        DVH          INT           NULL              -- T07: dígito verificador horizontal
    );
    PRINT 'Tabla Empleado creada.';
END
ELSE
    PRINT 'Tabla Empleado ya existe — sin cambios.';
GO

-- Cliente (refs PlanSuscripcion)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Cliente')
BEGIN
    CREATE TABLE Cliente (
        IdCliente        INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre           NVARCHAR(100) NOT NULL,
        Apellido         NVARCHAR(100) NOT NULL,
        DNI              NVARCHAR(200) NOT NULL,  -- T03: almacena el DNI CIFRADO (AES Base64)
        Email            NVARCHAR(200) NULL,
        MetodoPago       NVARCHAR(100) NULL,
        IdPlan           INT           NULL REFERENCES PlanSuscripcion(IdPlan),
        FechaAlta        DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaNacimiento  DATE          NOT NULL,  -- obligatoria: validar mayoría de edad
        Activo           BIT           NOT NULL DEFAULT 1,
        DVH              INT           NULL              -- T07: dígito verificador horizontal
    );
    PRINT 'Tabla Cliente creada.';
END
ELSE
    PRINT 'Tabla Cliente ya existe — sin cambios.';
GO

-- Prenda (refs Cliente — FK nullable: prenda disponible no tiene cliente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Prenda')
BEGIN
    CREATE TABLE Prenda (
        IdPrenda        INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre          NVARCHAR(100) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Talle           NVARCHAR(20)  NULL,
        Color           NVARCHAR(50)  NULL,
        Categoria       NVARCHAR(100) NULL,
        Estado          INT           NOT NULL DEFAULT 0,
        IdClienteActual INT           NULL REFERENCES Cliente(IdCliente),
        FechaAlta       DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabla Prenda creada.';
END
ELSE
    PRINT 'Tabla Prenda ya existe — sin cambios.';
GO

-- Pedido (refs Cliente + Empleado)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Pedido')
BEGIN
    CREATE TABLE Pedido (
        IdPedido          INT           IDENTITY(1,1) PRIMARY KEY,
        IdCliente         INT           NOT NULL REFERENCES Cliente(IdCliente),
        IdEmpleado        INT           NOT NULL REFERENCES Empleado(IdEmpleado),
        Estado            INT           NOT NULL DEFAULT 0,
        FechaPedido       DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaDespacho     DATETIME      NULL,
        FechaEntrega      DATETIME      NULL,
        MotivoCancelacion NVARCHAR(500) NULL,
        DVH               INT           NULL              -- T07: DV horizontal (incluye sus líneas)
    );
    PRINT 'Tabla Pedido creada.';
END
ELSE
    PRINT 'Tabla Pedido ya existe — sin cambios.';
GO

-- PedidoPrenda (junction Pedido-Prenda)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PedidoPrenda')
BEGIN
    CREATE TABLE PedidoPrenda (
        IdPedido INT NOT NULL REFERENCES Pedido(IdPedido),
        IdPrenda INT NOT NULL REFERENCES Prenda(IdPrenda),
        CONSTRAINT PK_PedidoPrenda PRIMARY KEY (IdPedido, IdPrenda)
    );
    PRINT 'Tabla PedidoPrenda creada.';
END
ELSE
    PRINT 'Tabla PedidoPrenda ya existe — sin cambios.';
GO

-- PedidoHistorial (auditoría de cambios de pedidos — refs Pedido + Usuario)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PedidoHistorial')
BEGIN
    CREATE TABLE PedidoHistorial (
        IdHistorial   INT            IDENTITY(1,1) PRIMARY KEY,
        IdPedido      INT            NOT NULL REFERENCES Pedido(IdPedido),
        IdOperacion   INT            NOT NULL,
        Fecha         DATETIME       NOT NULL DEFAULT GETDATE(),
        IdUsuario     INT            NULL REFERENCES Usuario(IdUsuario),
        NombreUsuario NVARCHAR(200)  NULL,
        Accion        NVARCHAR(200)  NOT NULL,
        Campo         NVARCHAR(100)  NOT NULL,
        ValorAnterior NVARCHAR(1000) NULL,
        ValorNuevo    NVARCHAR(1000) NULL
    );
    PRINT 'Tabla PedidoHistorial creada.';
END
ELSE
    PRINT 'Tabla PedidoHistorial ya existe — sin cambios.';
GO

-- BitacoraNegocio (eventos de negocio — refs Usuario, Pedido, Prenda, Cliente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BitacoraNegocio')
BEGIN
    CREATE TABLE BitacoraNegocio (
        IdEvento    INT           IDENTITY(1,1) PRIMARY KEY,
        Fecha       DATETIME      NOT NULL DEFAULT GETDATE(),
        Tipo        NVARCHAR(100) NOT NULL,
        IdUsuario   INT           NULL REFERENCES Usuario(IdUsuario),
        IdPedido    INT           NULL REFERENCES Pedido(IdPedido),
        IdPrenda    INT           NULL REFERENCES Prenda(IdPrenda),
        IdCliente   INT           NULL REFERENCES Cliente(IdCliente),
        Descripcion NVARCHAR(500) NOT NULL
    );
    PRINT 'Tabla BitacoraNegocio creada.';
END
ELSE
    PRINT 'Tabla BitacoraNegocio ya existe — sin cambios.';
GO

-- RolPermiso (asignación PLANA rol→patente) — LEGACY / SOLO SEED-BOOTSTRAP.
-- Se usa únicamente para sembrar el árbol: desde estas asignaciones se generan los nodos-rol y
-- las aristas de [PermisoRelacion], que es la ÚNICA fuente de verdad de autorización en runtime.
-- El sistema NO escribe esta tabla en runtime y solo la lee en fallbacks para BDs sin migrar.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RolPermiso')
BEGIN
    CREATE TABLE RolPermiso (
        Rol       NVARCHAR(100) NOT NULL,
        IdPermiso INT           NOT NULL REFERENCES Permiso(IdPermiso),
        CONSTRAINT PK_RolPermiso PRIMARY KEY (Rol, IdPermiso)
    );
    PRINT 'Tabla RolPermiso creada.';
END
ELSE
    PRINT 'Tabla RolPermiso ya existe — sin cambios.';
GO

-- PermisoRelacion (árbol Composite padre → hijo — T04)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PermisoRelacion')
BEGIN
    CREATE TABLE PermisoRelacion (
        IdPadre INT NOT NULL REFERENCES Permiso(IdPermiso),
        IdHijo  INT NOT NULL REFERENCES Permiso(IdPermiso),
        CONSTRAINT PK_PermisoRelacion PRIMARY KEY (IdPadre, IdHijo)
    );
    PRINT 'Tabla PermisoRelacion creada.';
END
ELSE
    PRINT 'Tabla PermisoRelacion ya existe — sin cambios.';
GO

-- Traduccion (PK compuesta — T05)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Traduccion')
BEGIN
    CREATE TABLE Traduccion (
        IdControl INT            NOT NULL REFERENCES Control(IdControl),
        IdIdioma  INT            NOT NULL REFERENCES Idioma(IdIdioma),
        Texto     NVARCHAR(1000) NOT NULL DEFAULT '',
        CONSTRAINT PK_Traduccion PRIMARY KEY (IdControl, IdIdioma)
    );
    PRINT 'Tabla Traduccion creada.';
END
ELSE
    PRINT 'Tabla Traduccion ya existe — sin cambios.';
GO

-- HistorialUsuario (snapshots de cambios de usuarios — T06)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HistorialUsuario')
BEGIN
    CREATE TABLE HistorialUsuario (
        IdVersion    INT           IDENTITY(1,1) PRIMARY KEY,
        IdUsuario    INT           NOT NULL REFERENCES Usuario(IdUsuario),
        Fecha        DATETIME      NOT NULL DEFAULT GETDATE(),
        Actor        NVARCHAR(100) NOT NULL,
        Detalle      NVARCHAR(500) NOT NULL,
        UsernameSnap NVARCHAR(100) NOT NULL,
        NombreSnap   NVARCHAR(100) NULL,        -- Snapshots de datos administrativos NO sensibles
        ApellidoSnap NVARCHAR(100) NULL,
        FechaNacSnap DATE          NULL,
        EmailSnap    NVARCHAR(200) NULL,
        ClaveSnap    NVARCHAR(500) NOT NULL,    -- Trazabilidad interna: nunca se muestra ni se restaura
        EstadoSnap   BIT           NOT NULL,
        IntentosSnap INT           NOT NULL
    );
    PRINT 'Tabla HistorialUsuario creada.';
END
ELSE
    PRINT 'Tabla HistorialUsuario ya existe — sin cambios.';
GO

-- HistorialIntegridad (historial de verificaciones de integridad DV — T07)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HistorialIntegridad')
BEGIN
    CREATE TABLE HistorialIntegridad (
        Id                INT          IDENTITY(1,1) PRIMARY KEY,
        NombreTabla       VARCHAR(100) NOT NULL,
        DVVAlmacenado     INT          NULL,
        DVVCalculado      INT          NOT NULL,
        Resultado         BIT          NOT NULL,
        FilasCorruptas    INT          NOT NULL DEFAULT 0,
        FechaVerificacion DATETIME     NOT NULL DEFAULT GETDATE(),
        DisparadoPor      VARCHAR(50)  NOT NULL
            CONSTRAINT CHK_HistInteg_Origen CHECK (DisparadoPor IN ('Arranque', 'Timer', 'Manual'))
    );
    PRINT 'Tabla HistorialIntegridad creada.';
END
ELSE
    PRINT 'Tabla HistorialIntegridad ya existe — sin cambios.';
GO

-- ClaveRecuperacion (claves de emergencia de 1 solo uso para desbloquear un Administrador)
-- Las claves se guardan HASHEADAS (PBKDF2); el .txt con las claves en texto plano es la
-- copia física del admin (como los códigos de respaldo de Steam / 2FA).
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ClaveRecuperacion')
BEGIN
    CREATE TABLE ClaveRecuperacion (
        IdClave       INT           IDENTITY(1,1) PRIMARY KEY,
        ClaveHash     NVARCHAR(500) NOT NULL,            -- PBKDF2-SHA256 (igual que Usuario.Clave)
        Usada         BIT           NOT NULL DEFAULT 0,  -- 1 = ya consumida (uso único)
        UsadaPor      NVARCHAR(100) NULL,                -- username del admin que la canjeó
        FechaUso      DATETIME      NULL,
        FechaCreacion DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabla ClaveRecuperacion creada.';
END
ELSE
    PRINT 'Tabla ClaveRecuperacion ya existe — sin cambios.';
GO

-- Preferencia (preferencias de UI por usuario: fuente, tamaño, tema, formato de fecha…).
-- ON DELETE CASCADE: al purgar físicamente un usuario, su fila de preferencias se borra sola.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Preferencia')
BEGIN
    CREATE TABLE Preferencia (
        IdUsuario      INT          NOT NULL PRIMARY KEY,
        FuenteFamilia  NVARCHAR(50) NULL,            -- "Segoe UI", "Verdana", "Calibri"…
        FuenteTamano   VARCHAR(10)  NULL,            -- 'Chico' / 'Normal' / 'Grande'
        Tema           VARCHAR(20)  NULL,            -- 'Claro' / 'Oscuro'
        FormatoFecha   VARCHAR(20)  NULL,            -- 'dd/MM/yyyy', 'yyyy-MM-dd'…
        Notificaciones BIT          NULL,            -- placeholder (se guarda; sin efecto funcional aún)
        CONSTRAINT FK_Preferencia_Usuario FOREIGN KEY (IdUsuario)
            REFERENCES Usuario(IdUsuario) ON DELETE CASCADE
    );
    PRINT 'Tabla Preferencia creada.';
END
ELSE
    PRINT 'Tabla Preferencia ya existe — sin cambios.';
GO

-- ============================================================
-- SEEDS INICIALES
-- ============================================================

-- ── Permisos: PATENTES (permisos simples) ───────────────────────────────────
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT v.Nombre, v.NombreMenu, v.Tipo, 1, 0, 0
FROM (VALUES
    ('Gestionar Usuarios',          'mnuUsuarios',           'Sistema'),
    ('Ver Auditoria',               'mnuAuditoria',          'Sistema'),
    ('Ver Prendas',                 'mnuPrendas',            'Inventario'),
    ('Ver Outfits',                 'mnuOutfits',            'Inventario'),
    ('Ver Categorias',              'mnuCategorias',         'Inventario'),
    ('Gestionar Stock',             'mnuStock',              'Inventario'),
    ('Gestionar Clientes',          'mnuClientes',           'Ventas'),
    ('Gestionar PlanSuscripciones', 'mnuPlanSuscripciones',  'Ventas'),
    ('Realizar Ventas',             'mnuPedidosVenta',       'Ventas'),
    ('Ver Pedidos Realizados',      'mnuPedidosRealizados',  'Ventas')
) AS v(Nombre, NombreMenu, Tipo)
WHERE NOT EXISTS (SELECT 1 FROM Permiso p
                  WHERE p.NombreMenu = v.NombreMenu AND ISNULL(p.EsFamilia,0) = 0);
PRINT 'Patentes (permisos simples) inicializadas.';
GO

-- ── Asignación rol → patente (RolPermiso) ────────────────────────────────────
-- Se asigna por NombreMenu para no depender de IDs de identidad.
INSERT INTO RolPermiso (Rol, IdPermiso)
SELECT r.Rol, p.IdPermiso
FROM (VALUES
    -- Administrador: acceso total
    ('Administrador','mnuUsuarios'),('Administrador','mnuAuditoria'),
    ('Administrador','mnuPrendas'),('Administrador','mnuOutfits'),
    ('Administrador','mnuCategorias'),('Administrador','mnuStock'),
    ('Administrador','mnuClientes'),('Administrador','mnuPlanSuscripciones'),
    ('Administrador','mnuPedidosVenta'),('Administrador','mnuPedidosRealizados'),
    -- Supervisor: auditoría + mismos permisos que Vendedor
    ('Supervisor','mnuAuditoria'),
    ('Supervisor','mnuPrendas'),('Supervisor','mnuClientes'),
    ('Supervisor','mnuPlanSuscripciones'),('Supervisor','mnuPedidosVenta'),
    -- Vendedor: prendas + clientes + planes + ventas
    ('Vendedor','mnuPrendas'),('Vendedor','mnuClientes'),
    ('Vendedor','mnuPlanSuscripciones'),('Vendedor','mnuPedidosVenta'),
    -- ControladorDeStock: prendas + stock
    ('ControladorDeStock','mnuPrendas'),('ControladorDeStock','mnuStock'),
    -- OperadorDeInventario: solo despacho
    ('OperadorDeInventario','mnuPedidosRealizados')
) AS r(Rol, NombreMenu)
JOIN Permiso p ON p.NombreMenu = r.NombreMenu AND ISNULL(p.EsFamilia,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM RolPermiso x WHERE x.Rol = r.Rol AND x.IdPermiso = p.IdPermiso);
PRINT 'Asignaciones rol→patente inicializadas.';
GO

-- ── Usuarios iniciales (clave hasheada PBKDF2; ver Contraseñas.txt) ───────────
-- admin/administrador1!  supervisor/supervisor1!  vendedor/vendedor1!
-- operador/operador1!     stock/controladorstock1!
-- DVH=0 → la app recalcula el DV en el primer arranque.
INSERT INTO Usuario (Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH, IdIdioma)
SELECT v.Username, v.Clave, v.Rol, v.Perfil, 1, 0, 0, 'ES'
FROM (VALUES
    ('admin',      '3ZTrmLBPYN+Dr4uWxFV6gfhtzhqVjnLEaPuUd2v+MNHwAaWlmPPfHwmMMwS0bZuP', 'Administrador',        'Administrador'),
    ('supervisor', 'k2GSNeiFtFw7m/Ipaok3XUqzWKFidcIy9agOxXKfs3MAiTD+1wF1kyFPcB2iYlaj', 'Supervisor',           'Supervisor'),
    ('vendedor',   'VWyQxHK8Dxr+BBWgw63IMTgFG91ZeDZSxRtj5FIpH9qxHbayJVLUBFpErIgLdOmZ', 'Vendedor',             'Vendedor'),
    ('stock',      'jkBe/qjMTd/g8kS1BAZEGO0gp+U2xIXev6mTuPrlTTSXz/aWWjPAyKcqNGrJwstR', 'ControladorDeStock',   'Controlador de Stock'),
    ('operador',   'EMy1Imvv5SfGsRX8ZIW2F6J0u6j86jbqhXXCPeVAloOX790eWYOGIHfUg5hcrOlR', 'OperadorDeInventario', 'Operador de Inventario')
) AS v(Username, Clave, Rol, Perfil)
WHERE NOT EXISTS (SELECT 1 FROM Usuario u WHERE u.Username = v.Username);
PRINT 'Usuarios iniciales creados.';
GO

-- Idiomas (ES, EN, RU)
INSERT INTO Idioma (Codigo, Nombre, Activo, EsDefault)
SELECT v.Codigo, v.Nombre, v.Activo, v.EsDefault
FROM (VALUES
    ('ES', N'Español', 1, 1),
    ('EN', N'English', 1, 0),
    ('PT', N'Português', 1, 0),
    ('RU', N'Русский', 1, 0)
) AS v(Codigo, Nombre, Activo, EsDefault)
WHERE NOT EXISTS (SELECT 1 FROM Idioma WHERE Codigo = v.Codigo);
PRINT 'Idiomas inicializados (ES, EN, RU).';
GO

-- DVV inicial para la tabla Usuario (en 0 — recalcular desde la app)
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Usuario')
BEGIN
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo)
    VALUES ('Usuario', 0, GETDATE());
    PRINT 'DVV inicial insertado para tabla Usuario.';
END
GO

-- DVH = 0 para usuarios existentes sin DVH
UPDATE Usuario SET DVH = 0 WHERE DVH IS NULL;
GO

-- IdIdioma = ES para usuarios existentes sin preferencia
UPDATE Usuario SET IdIdioma = 'ES' WHERE IdIdioma IS NULL;
GO

-- ============================================================
-- MIGRACIÓN COMPOSITE (T04)
-- Genera nodos Familia desde TipoComponente si no existen aún.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE EsFamilia = 1)
BEGIN
    DECLARE @mapa TABLE (Grupo NVARCHAR(100), IdFamilia INT);

    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia)
    OUTPUT INSERTED.Nombre, INSERTED.IdPermiso INTO @mapa (Grupo, IdFamilia)
    SELECT DISTINCT
        TipoComponente,
        TipoComponente,
        TipoComponente,
        1,
        1
    FROM Permiso
    WHERE TipoComponente IS NOT NULL
      AND LTRIM(RTRIM(TipoComponente)) <> ''
      AND EsFamilia = 0;

    INSERT INTO PermisoRelacion (IdPadre, IdHijo)
    SELECT m.IdFamilia, p.IdPermiso
    FROM   Permiso p
    INNER JOIN @mapa m ON p.TipoComponente = m.Grupo
    WHERE  p.EsFamilia = 0;

    PRINT 'Árbol Composite generado desde grupos TipoComponente.';
END
ELSE
    PRINT 'Árbol Composite ya inicializado — sin cambios.';
GO

-- ============================================================
-- MIGRACIÓN COMPOSITE (T04) — Roles como NODOS del árbol
-- A partir de v7.0 [PermisoRelacion] es la única fuente de verdad de la
-- composición. Se crea un nodo-rol por cada rol de [RolPermiso] y se migran
-- las asignaciones planas a aristas rol→permiso.
-- ============================================================

-- Crear nodo-rol faltante por cada rol existente en RolPermiso
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT DISTINCT rp.Rol, rp.Rol, 'Rol', 1, 1, 1
FROM   RolPermiso rp
WHERE  NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.Nombre = rp.Rol AND p.EsRol = 1);

-- Migrar asignaciones planas a aristas Composite (idempotente)
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT pr.IdPermiso, rp.IdPermiso
FROM   RolPermiso rp
INNER JOIN Permiso pr ON pr.Nombre = rp.Rol AND pr.EsRol = 1 AND pr.IdPermiso <> rp.IdPermiso
WHERE  NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                   WHERE x.IdPadre = pr.IdPermiso AND x.IdHijo = rp.IdPermiso);
PRINT 'Nodos-rol y aristas rol→permiso migrados (T04 v7.0).';
GO

-- ============================================================
-- JERARQUÍA DE ROLES (T04) — roles nuevos con vistas + rol-dentro-de-rol
-- Idempotente. Crea los nodos-rol nuevos, les asigna sus patentes (vistas)
-- propias y arma las aristas rol→rol para que el padre HEREDE recursivamente
-- las vistas del hijo (demostración real del Composite con jerarquía de roles).
--
--   Auditor                                → Auditoría
--   GerenteComercial  ⊃ Vendedor           → (Vendedor) + Pedidos Realizados
--   EncargadoDeStock  ⊃ OperadorLogistico  → (despacho) + Prendas + Stock
--   GerenteInventario ⊃ EncargadoDeStock   → (lo anterior) + Categorías + Outfits
-- ============================================================

-- 1) Nodos-rol para los roles nuevos (si faltan).
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT v.Rol, v.Rol, 'Rol', 1, 1, 1
FROM (VALUES
    ('Auditor'), ('GerenteComercial'), ('OperadorLogistico'),
    ('EncargadoDeStock'), ('GerenteInventario')
) AS v(Rol)
WHERE NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.Nombre = v.Rol AND p.EsRol = 1);
GO

-- 2) Patentes PROPIAS (vistas directas) de cada rol nuevo (aristas rol→patente).
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT rol.IdPermiso, pat.IdPermiso
FROM (VALUES
    ('Auditor',           'mnuAuditoria'),
    ('GerenteComercial',  'mnuPedidosRealizados'),
    ('OperadorLogistico', 'mnuPedidosRealizados'),
    ('EncargadoDeStock',  'mnuPrendas'),
    ('EncargadoDeStock',  'mnuStock'),
    ('GerenteInventario', 'mnuCategorias'),
    ('GerenteInventario', 'mnuOutfits')
) AS v(Rol, NombreMenu)
INNER JOIN Permiso rol ON rol.Nombre = v.Rol AND rol.EsRol = 1
INNER JOIN Permiso pat ON pat.NombreMenu = v.NombreMenu
                       AND ISNULL(pat.EsFamilia,0) = 0 AND ISNULL(pat.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                  WHERE x.IdPadre = rol.IdPermiso AND x.IdHijo = pat.IdPermiso);
GO

-- 3) Jerarquía rol→rol: el padre hereda recursivamente las vistas del hijo.
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT padre.IdPermiso, hijo.IdPermiso
FROM (VALUES
    ('GerenteComercial',  'Vendedor'),
    ('EncargadoDeStock',  'OperadorLogistico'),
    ('GerenteInventario', 'EncargadoDeStock')
) AS v(Padre, Hijo)
INNER JOIN Permiso padre ON padre.Nombre = v.Padre AND padre.EsRol = 1
INNER JOIN Permiso hijo  ON hijo.Nombre  = v.Hijo  AND hijo.EsRol  = 1
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                  WHERE x.IdPadre = padre.IdPermiso AND x.IdHijo = hijo.IdPermiso);
GO

-- ============================================================
-- USUARIOS DEMO DE LOS ROLES NUEVOS + SUPERVISOR COMPUESTO
-- Idempotente. Crea un usuario por rol nuevo (claves en Contraseñas.txt) y convierte a
-- Supervisor en rol COMPUESTO (Supervisor ⊃ Vendedor) conservando su Auditoría propia:
-- mismos permisos efectivos que antes, pero ahora obtenidos por HERENCIA (no copiados).
-- Las claves se guardan hasheadas (PBKDF2). Texto plano: usuario1! (ver Contraseñas.txt).
-- ============================================================

INSERT INTO Usuario (Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH, IdIdioma, Activo)
SELECT v.Username, v.Clave, v.Rol, v.Perfil, 1, 0, 0, 'ES', 1
FROM (VALUES
  ('auditor',     'SmnGp5hqSC+FXdbLiccYieNpC6vEaWn6nVpgZFrlyyeOXqx8yTjJYOgaw+oXAP7B', 'Auditor',           'Auditor'),
  ('gcomercial',  '1KWgyl8MkMcuisigf4fRBDb2f803LIsA/hvfKpzfwcMbRboUiS5CwuvFQq64GJft', 'GerenteComercial',  'GerenteComercial'),
  ('ginventario', 'G89lxogCulMeAK+WA5rNHSyWpuz5QKF/DBH8PSfiPbUv5SBKStFVQYXylikq+OMw', 'GerenteInventario', 'GerenteInventario'),
  ('encargado',   'H9LyJ5OBv0m2atrETHrimO5mBcVpKLn46uy/hiTgw4130wCo2H7/sgqAoZ/zroA8', 'EncargadoDeStock',  'EncargadoDeStock'),
  ('logistico',   'U3g703lDqDJfLgaXpOaNiAQpDOaBSq1LUMzEu3X7x8hh6097jTcMUNAsPfl1SCVG', 'OperadorLogistico', 'OperadorLogistico')
) AS v(Username, Clave, Rol, Perfil)
WHERE NOT EXISTS (SELECT 1 FROM Usuario u WHERE u.Username = v.Username);
PRINT 'Usuarios demo de roles nuevos inicializados.';
GO

-- ── Datos administrativos (NO sensibles) de los usuarios semilla — ABM ───────
-- Solo completa los que estén vacíos (idempotente). Habilita búsqueda por nombre/apellido/email.
UPDATE u SET u.Nombre = v.Nombre, u.Apellido = v.Apellido, u.Email = v.Email, u.FechaNacimiento = v.FechaNac
FROM Usuario u
JOIN (VALUES
    ('admin',       N'Admin',     N'Sistema',      'admin@experiencehub.com',       '1985-01-15'),
    ('vendedor',    N'Valentina', N'Bolívar',      'vendedor@experiencehub.com',    '1995-06-20'),
    ('operador',    N'Oscar',     N'Pérez',        'operador@experiencehub.com',    '1990-03-10'),
    ('auditor',     N'Ana',       N'Díaz',         'auditor@experiencehub.com',     '1988-09-05'),
    ('gcomercial',  N'Gabriel',   N'Morán',        'gcomercial@experiencehub.com',  '1983-11-25'),
    ('ginventario', N'Gisela',    N'Ortiz',        'ginventario@experiencehub.com', '1986-07-30'),
    ('logistico',   N'Lucas',     N'Gómez',        'logistico@experiencehub.com',   '1992-02-18')
) AS v(Username, Nombre, Apellido, Email, FechaNac) ON u.Username = v.Username
WHERE u.Nombre IS NULL AND u.Apellido IS NULL;
PRINT 'Datos administrativos de usuarios semilla aplicados.';
GO

-- Supervisor → COMPUESTO: se quitan las patentes de Vendedor asignadas directo (quedan por
-- herencia) y se agrega la arista Supervisor → Vendedor; conserva su Auditoría propia.
DELETE r FROM PermisoRelacion r
JOIN Permiso sup ON sup.IdPermiso = r.IdPadre AND sup.Nombre = 'Supervisor' AND sup.EsRol = 1
JOIN Permiso pat ON pat.IdPermiso = r.IdHijo
                AND pat.NombreMenu IN ('mnuPrendas','mnuClientes','mnuPlanSuscripciones','mnuPedidosVenta');
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT sup.IdPermiso, ven.IdPermiso
FROM Permiso sup JOIN Permiso ven ON ven.Nombre = 'Vendedor' AND ven.EsRol = 1
WHERE sup.Nombre = 'Supervisor' AND sup.EsRol = 1
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = sup.IdPermiso AND x.IdHijo = ven.IdPermiso);
PRINT 'Supervisor convertido en rol compuesto (Supervisor → Vendedor).';
GO

-- ============================================================
-- CONSOLIDACIÓN DE ROLES (v8) — decisión de diseño 2da entrega
-- Lleva la jerarquía al ESTADO OBJETIVO (idempotente, sin importar el estado previo):
--   • Inventario — dos operadores con responsabilidades CLARAS, sin duplicados:
--       - OperadorLogistico    → pedidos / despacho       (Ver Pedidos Realizados)
--       - OperadorDeInventario → mantenimiento de prendas (Ver Prendas + Gestionar Stock)
--       - GerenteInventario ⊃ AMBOS                       (+ Categorías + Outfits)
--     Se RETIRAN EncargadoDeStock y ControladorDeStock (eran redundantes con lo anterior).
--   • Comercial — se RETIRA Supervisor; el jefe es GerenteComercial ⊃ Vendedor.
-- Este bloque SUPERSEDE los bloques previos para los nodos afectados.
-- ============================================================

-- (a) Asegurar el nodo-rol OperadorDeInventario (por si la BD no lo tenía).
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT 'OperadorDeInventario', 'OperadorDeInventario', 'Rol', 1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM Permiso WHERE Nombre = 'OperadorDeInventario' AND EsRol = 1);
GO

-- (b) OperadorDeInventario: sus patentes propias deben ser EXACTAMENTE Prendas + Stock.
--     Se quita cualquier patente vieja (p. ej. el legacy 'Ver Pedidos Realizados') y se agregan las nuevas.
DELETE r FROM PermisoRelacion r
JOIN Permiso rol ON rol.IdPermiso = r.IdPadre AND rol.Nombre = 'OperadorDeInventario' AND rol.EsRol = 1
JOIN Permiso pat ON pat.IdPermiso = r.IdHijo  AND ISNULL(pat.EsRol,0) = 0
WHERE pat.NombreMenu NOT IN ('mnuPrendas','mnuStock');
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT rol.IdPermiso, pat.IdPermiso
FROM (VALUES ('mnuPrendas'), ('mnuStock')) AS v(NombreMenu)
JOIN Permiso rol ON rol.Nombre = 'OperadorDeInventario' AND rol.EsRol = 1
JOIN Permiso pat ON pat.NombreMenu = v.NombreMenu AND ISNULL(pat.EsFamilia,0) = 0 AND ISNULL(pat.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = rol.IdPermiso AND x.IdHijo = pat.IdPermiso);
GO

-- (c) GerenteInventario ⊃ OperadorLogistico + OperadorDeInventario (y se quita la arista vieja a EncargadoDeStock).
DELETE r FROM PermisoRelacion r
JOIN Permiso gi ON gi.IdPermiso = r.IdPadre AND gi.Nombre = 'GerenteInventario' AND gi.EsRol = 1
JOIN Permiso ed ON ed.IdPermiso = r.IdHijo  AND ed.Nombre = 'EncargadoDeStock'  AND ed.EsRol = 1;
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT gi.IdPermiso, h.IdPermiso
FROM (VALUES ('OperadorLogistico'), ('OperadorDeInventario')) AS v(Hijo)
JOIN Permiso gi ON gi.Nombre = 'GerenteInventario' AND gi.EsRol = 1
JOIN Permiso h  ON h.Nombre  = v.Hijo AND h.EsRol = 1
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = gi.IdPermiso AND x.IdHijo = h.IdPermiso);
GO

-- (d) Migrar usuarios de los roles que se retiran ANTES de desactivarlos.
DECLARE @rolesCambiados INT = 0;
UPDATE Usuario SET Rol = 'OperadorDeInventario', Perfil = 'OperadorDeInventario'
WHERE Rol IN ('EncargadoDeStock','ControladorDeStock') OR Perfil IN ('EncargadoDeStock','ControladorDeStock','Controlador de Stock');
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;
UPDATE Usuario SET Rol = 'GerenteComercial', Perfil = 'GerenteComercial'
WHERE Rol = 'Supervisor' OR Perfil = 'Supervisor';
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;

-- (e) Retirar los roles redundantes: quitar TODAS sus aristas (como padre o como hijo) y desactivar el nodo.
DELETE r FROM PermisoRelacion r
JOIN Permiso p ON (p.IdPermiso = r.IdPadre OR p.IdPermiso = r.IdHijo)
WHERE p.EsRol = 1 AND p.Nombre IN ('EncargadoDeStock','ControladorDeStock','Supervisor');
UPDATE Permiso SET Estado = 0 WHERE EsRol = 1 AND Nombre IN ('EncargadoDeStock','ControladorDeStock','Supervisor');

-- (f) Si cambió el Rol de algún usuario, el DVH (que incluye el Rol) quedó desfasado:
--     resetear el DV de Usuario para que la app lo recalcule limpio en el próximo arranque.
IF @rolesCambiados > 0
BEGIN
    UPDATE Usuario SET DVH = 0;
    UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Usuario';
    PRINT 'Roles consolidados; DV de Usuario reseteado para recálculo en el próximo arranque.';
END
PRINT 'Consolidación de roles (v8) aplicada.';
GO

-- ============================================================
-- Simplificación de Permisos — ELIMINAR FAMILIAS del árbol Composite
-- Decisión de revisión: los permisos (patentes) son un catálogo fijo y las FAMILIAS se
-- retiran de la experiencia. Para no perder permisos efectivos: (1) se materializan las
-- relaciones Rol→Patente alcanzables a través de familias, (2) se quitan las aristas que
-- tocan familias y (3) se desactivan los nodos Familia. El anidamiento Rol→Rol se preserva,
-- de modo que el patrón Composite sigue vigente (Rol = nodo compuesto, Patente = hoja).
-- Idempotente: si no hay familias activas, no hace nada.
-- ============================================================
IF EXISTS (SELECT 1 FROM Permiso WHERE ISNULL(EsFamilia,0)=1 AND ISNULL(EsRol,0)=0 AND Estado=1)
BEGIN
    -- (1) Patentes alcanzables desde cada Rol descendiendo SOLO por familias (no por roles).
    ;WITH Arbol AS (
        SELECT r.IdPermiso AS IdRol, pr.IdHijo AS IdNodo
        FROM Permiso r
        JOIN PermisoRelacion pr ON pr.IdPadre = r.IdPermiso
        WHERE r.EsRol = 1
        UNION ALL
        SELECT a.IdRol, pr.IdHijo
        FROM Arbol a
        JOIN Permiso n ON n.IdPermiso = a.IdNodo AND ISNULL(n.EsFamilia,0)=1 AND ISNULL(n.EsRol,0)=0
        JOIN PermisoRelacion pr ON pr.IdPadre = a.IdNodo
    )
    INSERT INTO PermisoRelacion (IdPadre, IdHijo)
    SELECT DISTINCT a.IdRol, a.IdNodo
    FROM Arbol a
    JOIN Permiso p ON p.IdPermiso = a.IdNodo AND ISNULL(p.EsFamilia,0)=0 AND ISNULL(p.EsRol,0)=0
    WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre=a.IdRol AND x.IdHijo=a.IdNodo)
    OPTION (MAXRECURSION 50);

    -- (2) Quitar todas las aristas que toquen una Familia (como padre o como hijo).
    DELETE r FROM PermisoRelacion r
    JOIN Permiso p ON (p.IdPermiso = r.IdPadre OR p.IdPermiso = r.IdHijo)
    WHERE ISNULL(p.EsFamilia,0)=1 AND ISNULL(p.EsRol,0)=0;

    -- (3) Desactivar los nodos Familia (no se borran: conservan trazabilidad/FKs).
    UPDATE Permiso SET Estado = 0 WHERE ISNULL(EsFamilia,0)=1 AND ISNULL(EsRol,0)=0;

    PRINT 'Familias eliminadas del Composite: roles aplanados a Rol->Patente.';
END
ELSE
    PRINT 'No hay familias activas — árbol ya aplanado.';
GO

-- ============================================================
-- Etapa 4 — Permisos a nivel de CONTROL (mapeo patente ↔ control de un formulario)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ControlMapeado')
BEGIN
    CREATE TABLE ControlMapeado (
        IdControlMapeado INT           IDENTITY(1,1) PRIMARY KEY,
        IdPermiso        INT           NOT NULL,
        Formulario       NVARCHAR(100) NOT NULL,
        NombreControl    NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_ControlMapeado_Permiso FOREIGN KEY (IdPermiso) REFERENCES Permiso(IdPermiso),
        CONSTRAINT UQ_ControlMapeado UNIQUE (Formulario, NombreControl)
    );
    PRINT 'Tabla ControlMapeado creada (Etapa 4 - permisos por control).';
END
ELSE
    PRINT 'Tabla ControlMapeado ya existe — sin cambios.';
GO

-- Seed inicial: mapea cada patente con NombreMenu al item de menu correspondiente del Menu
-- principal, como punto de partida coherente con la visibilidad actual. Idempotente.
INSERT INTO ControlMapeado (IdPermiso, Formulario, NombreControl)
SELECT p.IdPermiso, v.Formulario, v.NombreControl
FROM (VALUES
    ('mnuPrendas',           'Menu', 'prendasToolStripMenuItem'),
    ('mnuClientes',          'Menu', 'clientesToolStripMenuItem'),
    ('mnuPlanSuscripciones', 'Menu', 'planesToolStripMenuItem'),
    ('mnuPedidosVenta',      'Menu', 'pedidosVentaToolStripMenuItem'),
    ('mnuPedidosRealizados', 'Menu', 'pedidosRealizadosToolStripMenuItem'),
    ('mnuUsuarios',          'Menu', 'usuariosToolStripMenuItem'),
    ('mnuAuditoria',         'Menu', 'bitacoraToolStripMenuItem')
) AS v(NombreMenu, Formulario, NombreControl)
INNER JOIN Permiso p ON p.NombreMenu = v.NombreMenu
                    AND ISNULL(p.EsFamilia,0) = 0 AND ISNULL(p.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM ControlMapeado x
                  WHERE x.Formulario = v.Formulario AND x.NombreControl = v.NombreControl);
PRINT 'Seed de ControlMapeado (mapeos de menu) aplicado.';
GO

-- Si se insertaron usuarios nuevos en una BD ya inicializada, resetear el DV para que la app
-- lo recalcule limpio en el próximo arranque (evita una falsa alarma de integridad por mezcla).
IF EXISTS (SELECT 1 FROM Usuario WHERE Username IN ('auditor','gcomercial','ginventario','encargado','logistico') AND DVH = 0)
   AND EXISTS (SELECT 1 FROM Usuario WHERE DVH <> 0)
BEGIN
    UPDATE Usuario SET DVH = 0;
    UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Usuario';
    PRINT 'DV de Usuario reseteado para recálculo en el próximo arranque.';
END
GO

-- ============================================================
-- MIGRACIONES INCREMENTALES
-- ============================================================

-- FechaVencimiento en Cliente (suscripción con fecha de vencimiento)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Cliente' AND COLUMN_NAME = 'FechaVencimiento')
BEGIN
    ALTER TABLE Cliente ADD FechaVencimiento DATE NULL;
    PRINT 'Columna FechaVencimiento agregada a Cliente.';
END
ELSE
    PRINT 'FechaVencimiento ya existe en Cliente — sin cambios.';
GO

-- FechaNacimiento en Cliente — OBLIGATORIA (NOT NULL) para validar mayoría de edad.
-- 1) Agregar la columna como NULL si todavía no existe.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Cliente' AND COLUMN_NAME = 'FechaNacimiento')
BEGIN
    ALTER TABLE Cliente ADD FechaNacimiento DATE NULL;
    PRINT 'Columna FechaNacimiento agregada a Cliente.';
END
GO
-- 2) Backfill de filas legacy sin fecha (placeholder mayor de edad) para poder imponer NOT NULL.
UPDATE Cliente SET FechaNacimiento = '1990-01-01' WHERE FechaNacimiento IS NULL;
GO
-- 3) Imponer NOT NULL si la columna todavía admite nulos.
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME='Cliente' AND COLUMN_NAME='FechaNacimiento' AND IS_NULLABLE='YES')
BEGIN
    ALTER TABLE Cliente ALTER COLUMN FechaNacimiento DATE NOT NULL;
    PRINT 'Cliente.FechaNacimiento ahora es NOT NULL (obligatoria).';
END
ELSE
    PRINT 'Cliente.FechaNacimiento ya es NOT NULL — sin cambios.';
GO

-- MantenimientoPrenda (historial de limpieza/mantenimiento por prenda)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MantenimientoPrenda')
BEGIN
    CREATE TABLE MantenimientoPrenda (
        IdMantenimiento INT           IDENTITY(1,1) PRIMARY KEY,
        IdPrenda        INT           NOT NULL REFERENCES Prenda(IdPrenda),
        FechaEntrada    DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaSalida     DATETIME      NULL,
        Actor           NVARCHAR(100) NULL
    );
    PRINT 'Tabla MantenimientoPrenda creada.';
END
ELSE
    PRINT 'Tabla MantenimientoPrenda ya existe — sin cambios.';
GO

-- ============================================================
-- DATOS DEMO (presentación / pruebas)
-- ------------------------------------------------------------
-- Idempotente: cada bloque sólo inserta si el registro falta.
-- El DNI se guarda en TEXTO PLANO; la app lo tolera (TryDesencriptar
-- acepta registros legacy sin cifrar) y lo muestra tal cual.
-- DVH=0 → recalcular el DV desde la app (Usuarios → Recalcular DV)
-- antes del primer uso para que la verificación de integridad cierre.
-- ============================================================

-- Planes de suscripción
INSERT INTO PlanSuscripcion (Nombre, LimitePrendas, Precio, Estado)
SELECT v.Nombre, v.Limite, v.Precio, 1
FROM (VALUES
    (N'Básico',   5,  8000.00),
    (N'Estándar', 15, 15000.00),
    (N'Premium',  30, 25000.00)
) AS v(Nombre, Limite, Precio)
WHERE NOT EXISTS (SELECT 1 FROM PlanSuscripcion p WHERE p.Nombre = v.Nombre);
PRINT 'Demo: planes de suscripción.';
GO

-- Empleados (vinculados a los usuarios del sistema por Username)
INSERT INTO Empleado (Nombre, Apellido, DNI, Email, FechaIngreso, Puesto, Legajo, IdUsuario, DVH)
SELECT v.Nombre, v.Apellido, v.DNI, v.Email, GETDATE(), v.Puesto, v.Legajo,
       (SELECT TOP 1 IdUsuario FROM Usuario u WHERE u.Username = v.Username), 0
FROM (VALUES
    (N'Valentina', N'Morana', '33111000', 'vendedor@experiencehub.com', N'Vendedora',           'L-001', 'vendedor'),
    (N'Bruno',     N'Díaz',   '31222000', 'operador@experiencehub.com', N'Operador Inventario', 'L-002', 'operador'),
    (N'Carla',     N'Méndez', '32333000', 'stock@experiencehub.com',    N'Controlador Stock',   'L-003', 'stock')
) AS v(Nombre, Apellido, DNI, Email, Puesto, Legajo, Username)
WHERE NOT EXISTS (SELECT 1 FROM Empleado e WHERE e.Legajo = v.Legajo);
PRINT 'Demo: empleados.';
GO

-- Clientes (todos mayores de edad; FechaNacimiento obligatoria)
INSERT INTO Cliente (Nombre, Apellido, DNI, Email, MetodoPago, IdPlan, FechaAlta, FechaNacimiento, Activo, DVH)
SELECT v.Nombre, v.Apellido, v.DNI, v.Email, v.MetodoPago,
       (SELECT TOP 1 IdPlan FROM PlanSuscripcion p WHERE p.Nombre = v.PlanNom),
       GETDATE(), v.FechaNac, 1, 0
FROM (VALUES
    (N'Lucía',  N'Fernández', '30111222', 'lucia.fernandez@mail.com', 'Efectivo',      N'Premium',  CONVERT(date,'1990-03-15')),
    (N'Martín', N'Gómez',     '28999111', 'martin.gomez@mail.com',    'Crédito',       N'Estándar', CONVERT(date,'1985-07-22')),
    (N'Sofía',  N'Rossi',     '35444555', 'sofia.rossi@mail.com',     'Débito',        N'Básico',   CONVERT(date,'1998-11-02')),
    (N'Diego',  N'Paz',       '27333444', 'diego.paz@mail.com',       'Transferencia', N'Estándar', CONVERT(date,'1982-01-30')),
    (N'Camila', N'Torres',    '40222333', 'camila.torres@mail.com',   'Efectivo',      N'Premium',  CONVERT(date,'2001-06-10'))
) AS v(Nombre, Apellido, DNI, Email, MetodoPago, PlanNom, FechaNac)
WHERE NOT EXISTS (SELECT 1 FROM Cliente c WHERE c.Nombre = v.Nombre AND c.Apellido = v.Apellido);
PRINT 'Demo: clientes.';
GO

-- Prendas (catálogo de stock; todas Disponible inicialmente)
INSERT INTO Prenda (Nombre, Descripcion, Talle, Color, Categoria, Estado, IdClienteActual, FechaAlta)
SELECT v.Nombre, v.Descripcion, v.Talle, v.Color, v.Categoria, 0, NULL, GETDATE()
FROM (VALUES
    (N'Vestido Largo Negro',    N'Vestido de fiesta largo',  'M',  N'Negro',      N'Vestido'),
    (N'Blazer Beige',           N'Blazer entallado',         'L',  N'Beige',      N'Saco'),
    (N'Camisa Blanca Clásica',  N'Camisa de algodón',        'M',  N'Blanco',     N'Camisa'),
    (N'Pantalón Sastre Gris',   N'Pantalón de vestir',       '42', N'Gris',       N'Pantalón'),
    (N'Abrigo Largo Camel',     N'Tapado de paño',           'L',  N'Camel',      N'Abrigo'),
    (N'Vestido Floral',         N'Vestido estampado verano', 'S',  N'Estampado',  N'Vestido'),
    (N'Camisa Celeste',         N'Camisa de lino',           'L',  N'Celeste',    N'Camisa'),
    (N'Jean Recto Azul',        N'Jean clásico',             '40', N'Azul',       N'Pantalón'),
    (N'Saco a Cuadros',         N'Saco príncipe de Gales',   'M',  N'Multicolor', N'Saco'),
    (N'Falda Plisada Negra',    N'Falda midi plisada',       'S',  N'Negro',      N'Falda'),
    (N'Sweater Oversize Crema', N'Sweater de lana',          'L',  N'Crema',      N'Sweater'),
    (N'Gabardina Verde',        N'Gabardina impermeable',    'M',  N'Verde',      N'Abrigo')
) AS v(Nombre, Descripcion, Talle, Color, Categoria)
WHERE NOT EXISTS (SELECT 1 FROM Prenda pr WHERE pr.Nombre = v.Nombre);
PRINT 'Demo: prendas (stock).';
GO

-- Pedidos demo (sólo si aún no hay pedidos) + prendas EnUso asignadas a su cliente,
-- replicando lo que hace la app al crear/despachar (Prenda.Estado=EnUso, IdClienteActual).
IF NOT EXISTS (SELECT 1 FROM Pedido)
   AND EXISTS (SELECT 1 FROM Empleado WHERE Legajo = 'L-001')
   AND EXISTS (SELECT 1 FROM Cliente WHERE Nombre = N'Lucía' AND Apellido = N'Fernández')
BEGIN
    DECLARE @emp     INT = (SELECT TOP 1 IdEmpleado FROM Empleado WHERE Legajo = 'L-001');
    DECLARE @cLucia  INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Lucía'  AND Apellido=N'Fernández');
    DECLARE @cMartin INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Martín' AND Apellido=N'Gómez');
    DECLARE @cSofia  INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Sofía'  AND Apellido=N'Rossi');

    DECLARE @pr1 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Vestido Largo Negro');
    DECLARE @pr2 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Blazer Beige');
    DECLARE @pr3 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Camisa Blanca Clásica');
    DECLARE @pr4 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Pantalón Sastre Gris');

    -- Pedido 1: Lucía — Entregado (2 prendas)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, FechaDespacho, FechaEntrega, DVH)
    VALUES (@cLucia, @emp, 2, DATEADD(day,-20,GETDATE()), DATEADD(day,-18,GETDATE()), DATEADD(day,-15,GETDATE()), 0);
    DECLARE @ped1 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped1, @pr1), (@ped1, @pr2);

    -- Pedido 2: Martín — Despachado (1 prenda)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, FechaDespacho, DVH)
    VALUES (@cMartin, @emp, 1, DATEADD(day,-5,GETDATE()), DATEADD(day,-3,GETDATE()), 0);
    DECLARE @ped2 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped2, @pr3);

    -- Pedido 3: Sofía — Pendiente (1 prenda)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, DVH)
    VALUES (@cSofia, @emp, 0, DATEADD(day,-1,GETDATE()), 0);
    DECLARE @ped3 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped3, @pr4);

    -- Marcar prendas EnUso (Estado=1) con su cliente
    UPDATE Prenda SET Estado=1, IdClienteActual=@cLucia  WHERE IdPrenda IN (@pr1,@pr2);
    UPDATE Prenda SET Estado=1, IdClienteActual=@cMartin WHERE IdPrenda=@pr3;
    UPDATE Prenda SET Estado=1, IdClienteActual=@cSofia  WHERE IdPrenda=@pr4;

    PRINT 'Demo: 3 pedidos (Entregado/Despachado/Pendiente) + prendas asignadas.';
END
ELSE
    PRINT 'Demo: pedidos ya existen o faltan datos base — sin cambios.';
GO

-- ============================================================
-- ÍNDICES NO-CLUSTERED Y RESTRICCIONES DE INTEGRIDAD
-- Idempotente: solo crea lo que falta. Mejoran los joins por FK y los filtros por fecha,
-- y el CHECK respalda en el motor la prohibición de auto-referencia del árbol Composite.
-- ============================================================

-- Índices sobre columnas FK que NO son la columna LÍDER de su PK (la PK ya indexa su primera
-- columna) y sobre columnas usadas para filtrar/ordenar (fechas).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bitacora_fecha' AND object_id = OBJECT_ID('Bitacora'))
    CREATE NONCLUSTERED INDEX IX_Bitacora_fecha ON Bitacora(fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bitacora_usuario' AND object_id = OBJECT_ID('Bitacora'))
    CREATE NONCLUSTERED INDEX IX_Bitacora_usuario ON Bitacora(usuario);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BitacoraNegocio_Fecha' AND object_id = OBJECT_ID('BitacoraNegocio'))
    CREATE NONCLUSTERED INDEX IX_BitacoraNegocio_Fecha ON BitacoraNegocio(Fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PermisoRelacion_IdHijo' AND object_id = OBJECT_ID('PermisoRelacion'))
    CREATE NONCLUSTERED INDEX IX_PermisoRelacion_IdHijo ON PermisoRelacion(IdHijo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolPermiso_IdPermiso' AND object_id = OBJECT_ID('RolPermiso'))
    CREATE NONCLUSTERED INDEX IX_RolPermiso_IdPermiso ON RolPermiso(IdPermiso);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Traduccion_IdIdioma' AND object_id = OBJECT_ID('Traduccion'))
    CREATE NONCLUSTERED INDEX IX_Traduccion_IdIdioma ON Traduccion(IdIdioma);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_IdCliente' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_IdCliente ON Pedido(IdCliente);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_IdEmpleado' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_IdEmpleado ON Pedido(IdEmpleado);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_FechaPedido' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_FechaPedido ON Pedido(FechaPedido);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PedidoPrenda_IdPrenda' AND object_id = OBJECT_ID('PedidoPrenda'))
    CREATE NONCLUSTERED INDEX IX_PedidoPrenda_IdPrenda ON PedidoPrenda(IdPrenda);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PedidoHistorial_IdPedido' AND object_id = OBJECT_ID('PedidoHistorial'))
    CREATE NONCLUSTERED INDEX IX_PedidoHistorial_IdPedido ON PedidoHistorial(IdPedido);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prenda_IdClienteActual' AND object_id = OBJECT_ID('Prenda'))
    CREATE NONCLUSTERED INDEX IX_Prenda_IdClienteActual ON Prenda(IdClienteActual);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HistorialUsuario_IdUsuario' AND object_id = OBJECT_ID('HistorialUsuario'))
    CREATE NONCLUSTERED INDEX IX_HistorialUsuario_IdUsuario ON HistorialUsuario(IdUsuario);
PRINT 'Índices no-clustered verificados/creados.';
GO

-- CHECK anti auto-referencia del árbol Composite: un permiso no puede ser su propio hijo.
-- (La validación de ciclos completa vive en BE.Familia; esto la respalda en el motor por si
-- alguien escribe por SQL directo.) Se limpian filas inválidas preexistentes para que no falle.
IF EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre = IdHijo)
BEGIN
    DELETE FROM PermisoRelacion WHERE IdPadre = IdHijo;
    PRINT 'PermisoRelacion: filas auto-referenciadas (IdPadre = IdHijo) eliminadas.';
END
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PermisoRelacion_NoAutoref')
BEGIN
    ALTER TABLE PermisoRelacion ADD CONSTRAINT CK_PermisoRelacion_NoAutoref CHECK (IdPadre <> IdHijo);
    PRINT 'CHECK CK_PermisoRelacion_NoAutoref agregado (IdPadre <> IdHijo).';
END
ELSE
    PRINT 'CHECK CK_PermisoRelacion_NoAutoref ya existe — sin cambios.';
GO

PRINT '';
PRINT '=== ExperienceHubDB deploy completo. ===';
PRINT 'IMPORTANTE: Ejecutar recálculo de DVH/DVV desde la aplicación';
PRINT '            antes del primer uso (Administrar → Usuarios → Recalcular DV).';
PRINT 'Las traducciones se seedean automáticamente en el primer uso de la app.';
GO

GO

-- ===================================================================
-- MODULO: 02_Actualizar_BaseDeDatos.sql
-- ===================================================================
-- ============================================================
-- ExperienceHub — 02. ACTUALIZAR BASE DE DATOS EXISTENTE
-- ------------------------------------------------------------
-- Aplica de forma IDEMPOTENTE todas las migraciones sobre una
-- ExperienceHubDB ya creada:
--   • columnas nuevas (Permiso.EsFamilia/EsRol, Cliente.FechaVencimiento, etc.)
--   • tablas nuevas (PermisoRelacion, Control, Idioma, Traduccion,
--     HistorialUsuario, HistorialIntegridad, MantenimientoPrenda, …)
--   • migración Composite T04: nodos-rol + RolPermiso → PermisoRelacion.
-- NO inserta datos semilla base (la BD existente ya los tiene).
--
-- Para crear la BD de cero, usar: 01_Crear_BaseDeDatos.sql
--
-- NOTA DE CODIFICACIÓN: este archivo es UTF-8 y contiene acentos (Básico, Lucía…).
--   • En SSMS se ejecuta sin problemas.
--   • Con sqlcmd usar el codepage UTF-8:  sqlcmd -S .\SQLEXPRESS -E -f 65001 -i 02_Actualizar_BaseDeDatos.sql
-- ============================================================

USE ExperienceHubDB;
GO

-- ============================================================
-- TABLAS BASE
-- ============================================================

-- Usuario
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Usuario')
BEGIN
    CREATE TABLE Usuario (
        IdUsuario        INT           IDENTITY(1,1) PRIMARY KEY,
        Username         NVARCHAR(100) NOT NULL,
        Clave            NVARCHAR(500) NOT NULL,
        Rol              NVARCHAR(100) NULL,
        Perfil           NVARCHAR(100) NULL,
        Estado           BIT           NOT NULL DEFAULT 1,
        IntentosFallidos INT           NOT NULL DEFAULT 0,
        DVH              INT           NULL,
        IdIdioma         VARCHAR(5)    NULL,
        CONSTRAINT UQ_Usuario_Username UNIQUE (Username)
    );
    PRINT 'Tabla Usuario creada.';
END
ELSE
    PRINT 'Tabla Usuario ya existe — sin cambios.';
GO

-- DVVertical (T07 Dígitos Verificadores)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DVVertical')
BEGIN
    CREATE TABLE DVVertical (
        Id           INT          IDENTITY(1,1) PRIMARY KEY,
        NombreTabla  VARCHAR(100) NOT NULL,
        DVV          INT          NOT NULL,
        FechaCalculo DATETIME     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_DVVertical_Tabla UNIQUE (NombreTabla)
    );
    PRINT 'Tabla DVVertical creada.';
END
ELSE
    PRINT 'Tabla DVVertical ya existe — sin cambios.';
GO

-- Usuario_Seguridad (T07 — tabla ESPEJO de integridad)
-- Copia sombra de los campos que entran al DVH de cada usuario, más su DVH.
-- Se mantiene en sincronía con cada escritura LEGÍTIMA (la app la actualiza junto al DVH).
-- Permite, ante una manipulación: (1) diagnosticar QUÉ campo cambió comparando contra el
-- espejo y (2) REPARAR restaurando el valor legítimo sin necesitar un backup completo.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Usuario_Seguridad')
BEGIN
    CREATE TABLE Usuario_Seguridad (
        IdUsuario          INT           PRIMARY KEY,   -- mismo Id que Usuario (NO identity: lo fija la app)
        Username           NVARCHAR(100) NOT NULL,
        Clave              NVARCHAR(500) NOT NULL,
        Rol                NVARCHAR(100) NULL,
        Perfil             NVARCHAR(100) NULL,
        Estado             BIT           NOT NULL DEFAULT 1,
        IntentosFallidos   INT           NOT NULL DEFAULT 0,
        DVH                INT           NULL,
        FechaActualizacion DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabla Usuario_Seguridad (espejo de integridad) creada.';
END
ELSE
    PRINT 'Tabla Usuario_Seguridad ya existe — sin cambios.';
GO

-- PlanSuscripcion
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlanSuscripcion')
BEGIN
    CREATE TABLE PlanSuscripcion (
        IdPlan        INT            IDENTITY(1,1) PRIMARY KEY,
        Nombre        NVARCHAR(100)  NOT NULL,
        LimitePrendas INT            NOT NULL DEFAULT 0,
        Precio        DECIMAL(10, 2) NOT NULL DEFAULT 0,
        Estado        BIT            NOT NULL DEFAULT 1
    );
    PRINT 'Tabla PlanSuscripcion creada.';
END
ELSE
    PRINT 'Tabla PlanSuscripcion ya existe — sin cambios.';
GO

-- Permiso (T04 Composite — EsFamilia discrimina Familia vs Patente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permiso')
BEGIN
    CREATE TABLE Permiso (
        IdPermiso       INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre          NVARCHAR(100) NOT NULL,
        NombreMenu      NVARCHAR(100) NULL,
        TipoComponente  NVARCHAR(100) NULL,
        Estado          BIT           NOT NULL DEFAULT 1,
        EsFamilia       BIT           NOT NULL DEFAULT 0,
        EsRol           BIT           NOT NULL DEFAULT 0
    );
    PRINT 'Tabla Permiso creada.';
END
ELSE
BEGIN
    -- Si ya existe, asegurar que EsFamilia esté presente (migración v6.0)
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = 'Permiso' AND COLUMN_NAME = 'EsFamilia')
    BEGIN
        ALTER TABLE Permiso ADD EsFamilia BIT NOT NULL DEFAULT 0;
        PRINT 'Columna EsFamilia agregada a Permiso (migración).';
    END
    -- EsRol: marca los nodos-rol del Composite (migración v7.0 — T04)
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = 'Permiso' AND COLUMN_NAME = 'EsRol')
    BEGIN
        ALTER TABLE Permiso ADD EsRol BIT NOT NULL DEFAULT 0;
        PRINT 'Columna EsRol agregada a Permiso (migración).';
    END
END
GO

-- Idioma (T05 Multiidioma)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Idioma')
BEGIN
    CREATE TABLE Idioma (
        IdIdioma  INT IDENTITY(1,1) PRIMARY KEY,
        Codigo    VARCHAR(5)    NOT NULL,
        Nombre    NVARCHAR(50)  NOT NULL,
        Activo    BIT           NOT NULL DEFAULT 1,
        EsDefault BIT           NOT NULL DEFAULT 0,
        CONSTRAINT UQ_Idioma_Codigo UNIQUE (Codigo)
    );
    PRINT 'Tabla Idioma creada.';
END
ELSE
    PRINT 'Tabla Idioma ya existe — sin cambios.';
GO

-- Control (claves de traducción — T05)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Control')
BEGIN
    CREATE TABLE Control (
        IdControl  INT IDENTITY(1,1) PRIMARY KEY,
        Clave      VARCHAR(100) NOT NULL,
        Formulario VARCHAR(50)  NOT NULL DEFAULT 'General',
        CONSTRAINT UQ_Control_Clave UNIQUE (Clave)
    );
    PRINT 'Tabla Control creada.';
END
ELSE
    PRINT 'Tabla Control ya existe — sin cambios.';
GO

-- ============================================================
-- TABLAS CON FK
-- ============================================================

-- Bitacora (sistema — refs Usuario)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Bitacora')
BEGIN
    CREATE TABLE Bitacora (
        Id         INT            IDENTITY(1,1) PRIMARY KEY,
        fecha      DATETIME       NOT NULL DEFAULT GETDATE(),
        usuario    INT            NULL REFERENCES Usuario(IdUsuario),
        modulo     NVARCHAR(100)  NULL,
        actividad  NVARCHAR(200)  NULL,
        detalle    NVARCHAR(1000) NULL,
        criticidad INT            NOT NULL DEFAULT 0,
        ip         NVARCHAR(50)   NULL
    );
    PRINT 'Tabla Bitacora creada.';
END
ELSE
    PRINT 'Tabla Bitacora ya existe — sin cambios.';
GO

-- Empleado (refs Usuario — FK nullable: empleado puede no tener usuario del sistema)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Empleado')
BEGIN
    CREATE TABLE Empleado (
        IdEmpleado   INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre       NVARCHAR(100) NOT NULL,
        Apellido     NVARCHAR(100) NOT NULL,
        DNI          NVARCHAR(200) NOT NULL,  -- T03: almacena el DNI CIFRADO (AES Base64)
        Email        NVARCHAR(200) NULL,
        FechaIngreso DATETIME      NOT NULL DEFAULT GETDATE(),
        Puesto       NVARCHAR(100) NULL,
        Legajo       NVARCHAR(50)  NULL,
        IdUsuario    INT           NULL REFERENCES Usuario(IdUsuario),
        DVH          INT           NULL              -- T07: dígito verificador horizontal
    );
    PRINT 'Tabla Empleado creada.';
END
ELSE
    PRINT 'Tabla Empleado ya existe — sin cambios.';
GO

-- Cliente (refs PlanSuscripcion)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Cliente')
BEGIN
    CREATE TABLE Cliente (
        IdCliente   INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre      NVARCHAR(100) NOT NULL,
        Apellido    NVARCHAR(100) NOT NULL,
        DNI         NVARCHAR(200) NOT NULL,  -- T03: almacena el DNI CIFRADO (AES Base64)
        Email       NVARCHAR(200) NULL,
        MetodoPago  NVARCHAR(100) NULL,
        IdPlan      INT           NULL REFERENCES PlanSuscripcion(IdPlan),
        FechaAlta   DATETIME      NOT NULL DEFAULT GETDATE(),
        Activo      BIT           NOT NULL DEFAULT 1,
        DVH         INT           NULL              -- T07: dígito verificador horizontal
    );
    PRINT 'Tabla Cliente creada.';
END
ELSE
    PRINT 'Tabla Cliente ya existe — sin cambios.';
GO

-- Prenda (refs Cliente — FK nullable: prenda disponible no tiene cliente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Prenda')
BEGIN
    CREATE TABLE Prenda (
        IdPrenda        INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre          NVARCHAR(100) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Talle           NVARCHAR(20)  NULL,
        Color           NVARCHAR(50)  NULL,
        Categoria       NVARCHAR(100) NULL,
        Estado          INT           NOT NULL DEFAULT 0,
        IdClienteActual INT           NULL REFERENCES Cliente(IdCliente),
        FechaAlta       DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabla Prenda creada.';
END
ELSE
    PRINT 'Tabla Prenda ya existe — sin cambios.';
GO

-- Pedido (refs Cliente + Empleado)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Pedido')
BEGIN
    CREATE TABLE Pedido (
        IdPedido          INT           IDENTITY(1,1) PRIMARY KEY,
        IdCliente         INT           NOT NULL REFERENCES Cliente(IdCliente),
        IdEmpleado        INT           NOT NULL REFERENCES Empleado(IdEmpleado),
        Estado            INT           NOT NULL DEFAULT 0,
        FechaPedido       DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaDespacho     DATETIME      NULL,
        FechaEntrega      DATETIME      NULL,
        MotivoCancelacion NVARCHAR(500) NULL,
        DVH               INT           NULL              -- T07: DV horizontal (incluye sus líneas)
    );
    PRINT 'Tabla Pedido creada.';
END
ELSE
    PRINT 'Tabla Pedido ya existe — sin cambios.';
GO

-- PedidoPrenda (junction Pedido-Prenda)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PedidoPrenda')
BEGIN
    CREATE TABLE PedidoPrenda (
        IdPedido INT NOT NULL REFERENCES Pedido(IdPedido),
        IdPrenda INT NOT NULL REFERENCES Prenda(IdPrenda),
        CONSTRAINT PK_PedidoPrenda PRIMARY KEY (IdPedido, IdPrenda)
    );
    PRINT 'Tabla PedidoPrenda creada.';
END
ELSE
    PRINT 'Tabla PedidoPrenda ya existe — sin cambios.';
GO

-- PedidoHistorial (auditoría de cambios de pedidos — refs Pedido + Usuario)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PedidoHistorial')
BEGIN
    CREATE TABLE PedidoHistorial (
        IdHistorial   INT            IDENTITY(1,1) PRIMARY KEY,
        IdPedido      INT            NOT NULL REFERENCES Pedido(IdPedido),
        IdOperacion   INT            NOT NULL,
        Fecha         DATETIME       NOT NULL DEFAULT GETDATE(),
        IdUsuario     INT            NULL REFERENCES Usuario(IdUsuario),
        NombreUsuario NVARCHAR(200)  NULL,
        Accion        NVARCHAR(200)  NOT NULL,
        Campo         NVARCHAR(100)  NOT NULL,
        ValorAnterior NVARCHAR(1000) NULL,
        ValorNuevo    NVARCHAR(1000) NULL
    );
    PRINT 'Tabla PedidoHistorial creada.';
END
ELSE
    PRINT 'Tabla PedidoHistorial ya existe — sin cambios.';
GO

-- BitacoraNegocio (eventos de negocio — refs Usuario, Pedido, Prenda, Cliente)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BitacoraNegocio')
BEGIN
    CREATE TABLE BitacoraNegocio (
        IdEvento    INT           IDENTITY(1,1) PRIMARY KEY,
        Fecha       DATETIME      NOT NULL DEFAULT GETDATE(),
        Tipo        NVARCHAR(100) NOT NULL,
        IdUsuario   INT           NULL REFERENCES Usuario(IdUsuario),
        IdPedido    INT           NULL REFERENCES Pedido(IdPedido),
        IdPrenda    INT           NULL REFERENCES Prenda(IdPrenda),
        IdCliente   INT           NULL REFERENCES Cliente(IdCliente),
        Descripcion NVARCHAR(500) NOT NULL
    );
    PRINT 'Tabla BitacoraNegocio creada.';
END
ELSE
    PRINT 'Tabla BitacoraNegocio ya existe — sin cambios.';
GO

-- RolPermiso (asignación PLANA rol→patente) — LEGACY / SOLO SEED-BOOTSTRAP.
-- Se usa únicamente para sembrar el árbol: desde estas asignaciones se generan los nodos-rol y
-- las aristas de [PermisoRelacion], que es la ÚNICA fuente de verdad de autorización en runtime.
-- El sistema NO escribe esta tabla en runtime y solo la lee en fallbacks para BDs sin migrar.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RolPermiso')
BEGIN
    CREATE TABLE RolPermiso (
        Rol       NVARCHAR(100) NOT NULL,
        IdPermiso INT           NOT NULL REFERENCES Permiso(IdPermiso),
        CONSTRAINT PK_RolPermiso PRIMARY KEY (Rol, IdPermiso)
    );
    PRINT 'Tabla RolPermiso creada.';
END
ELSE
    PRINT 'Tabla RolPermiso ya existe — sin cambios.';
GO

-- PermisoRelacion (árbol Composite padre → hijo — T04)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PermisoRelacion')
BEGIN
    CREATE TABLE PermisoRelacion (
        IdPadre INT NOT NULL REFERENCES Permiso(IdPermiso),
        IdHijo  INT NOT NULL REFERENCES Permiso(IdPermiso),
        CONSTRAINT PK_PermisoRelacion PRIMARY KEY (IdPadre, IdHijo)
    );
    PRINT 'Tabla PermisoRelacion creada.';
END
ELSE
    PRINT 'Tabla PermisoRelacion ya existe — sin cambios.';
GO

-- Traduccion (PK compuesta — T05)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Traduccion')
BEGIN
    CREATE TABLE Traduccion (
        IdControl INT            NOT NULL REFERENCES Control(IdControl),
        IdIdioma  INT            NOT NULL REFERENCES Idioma(IdIdioma),
        Texto     NVARCHAR(1000) NOT NULL DEFAULT '',
        CONSTRAINT PK_Traduccion PRIMARY KEY (IdControl, IdIdioma)
    );
    PRINT 'Tabla Traduccion creada.';
END
ELSE
    PRINT 'Tabla Traduccion ya existe — sin cambios.';
GO

-- HistorialUsuario (snapshots de cambios de usuarios — T06)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HistorialUsuario')
BEGIN
    CREATE TABLE HistorialUsuario (
        IdVersion    INT           IDENTITY(1,1) PRIMARY KEY,
        IdUsuario    INT           NOT NULL REFERENCES Usuario(IdUsuario),
        Fecha        DATETIME      NOT NULL DEFAULT GETDATE(),
        Actor        NVARCHAR(100) NOT NULL,
        Detalle      NVARCHAR(500) NOT NULL,
        UsernameSnap NVARCHAR(100) NOT NULL,
        ClaveSnap    NVARCHAR(500) NOT NULL,
        EstadoSnap   BIT           NOT NULL,
        IntentosSnap INT           NOT NULL
    );
    PRINT 'Tabla HistorialUsuario creada.';
END
ELSE
    PRINT 'Tabla HistorialUsuario ya existe — sin cambios.';
GO

-- T06 (revisión) — Snapshots de datos administrativos NO sensibles en el Historial de Cambios.
-- El historial pasa a versionar nombre/apellido/fecha nac./email (además del username);
-- la clave queda solo como trazabilidad interna y NUNCA se muestra ni se restaura.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='HistorialUsuario' AND COLUMN_NAME='NombreSnap')
    ALTER TABLE HistorialUsuario ADD NombreSnap NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='HistorialUsuario' AND COLUMN_NAME='ApellidoSnap')
    ALTER TABLE HistorialUsuario ADD ApellidoSnap NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='HistorialUsuario' AND COLUMN_NAME='FechaNacSnap')
    ALTER TABLE HistorialUsuario ADD FechaNacSnap DATE NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='HistorialUsuario' AND COLUMN_NAME='EmailSnap')
    ALTER TABLE HistorialUsuario ADD EmailSnap NVARCHAR(200) NULL;
GO
PRINT 'HistorialUsuario: columnas de snapshot administrativo verificadas.';
GO

-- HistorialIntegridad (historial de verificaciones de integridad DV — T07)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HistorialIntegridad')
BEGIN
    CREATE TABLE HistorialIntegridad (
        Id                INT          IDENTITY(1,1) PRIMARY KEY,
        NombreTabla       VARCHAR(100) NOT NULL,
        DVVAlmacenado     INT          NULL,
        DVVCalculado      INT          NOT NULL,
        Resultado         BIT          NOT NULL,
        FilasCorruptas    INT          NOT NULL DEFAULT 0,
        FechaVerificacion DATETIME     NOT NULL DEFAULT GETDATE(),
        DisparadoPor      VARCHAR(50)  NOT NULL
            CONSTRAINT CHK_HistInteg_Origen CHECK (DisparadoPor IN ('Arranque', 'Timer', 'Manual'))
    );
    PRINT 'Tabla HistorialIntegridad creada.';
END
ELSE
    PRINT 'Tabla HistorialIntegridad ya existe — sin cambios.';
GO

-- ============================================================
-- SEEDS INICIALES
-- ============================================================

-- Idiomas (ES, EN, RU)
INSERT INTO Idioma (Codigo, Nombre, Activo, EsDefault)
SELECT v.Codigo, v.Nombre, v.Activo, v.EsDefault
FROM (VALUES
    ('ES', N'Español', 1, 1),
    ('EN', N'English', 1, 0),
    ('PT', N'Português', 1, 0),
    ('RU', N'Русский', 1, 0)
) AS v(Codigo, Nombre, Activo, EsDefault)
WHERE NOT EXISTS (SELECT 1 FROM Idioma WHERE Codigo = v.Codigo);
PRINT 'Idiomas inicializados (ES, EN, RU).';
GO

-- DVV inicial para la tabla Usuario (en 0 — recalcular desde la app)
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Usuario')
BEGIN
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo)
    VALUES ('Usuario', 0, GETDATE());
    PRINT 'DVV inicial insertado para tabla Usuario.';
END
GO

-- DVH = 0 para usuarios existentes sin DVH
UPDATE Usuario SET DVH = 0 WHERE DVH IS NULL;
GO

-- IdIdioma = ES para usuarios existentes sin preferencia
UPDATE Usuario SET IdIdioma = 'ES' WHERE IdIdioma IS NULL;
GO

-- RF-10 — Baja lógica de usuarios (archivado) + purga diferida.
-- Activo:    1=activo, 0=archivado (no puede loguear ni aparece en la lista).
-- FechaBaja: cuándo se archivó (la purga física solo aplica a archivados con >1 año).
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='Activo')
BEGIN
    ALTER TABLE Usuario ADD Activo BIT NOT NULL DEFAULT 1;
    PRINT 'Columna Activo agregada a Usuario (RF-10 baja lógica).';
END
ELSE
    PRINT 'Usuario.Activo ya existe — sin cambios.';
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='FechaBaja')
BEGIN
    ALTER TABLE Usuario ADD FechaBaja DATETIME NULL;
    PRINT 'Columna FechaBaja agregada a Usuario (RF-10 purga diferida).';
END
ELSE
    PRINT 'Usuario.FechaBaja ya existe — sin cambios.';
GO
-- Usuarios existentes quedan activos por defecto.
UPDATE Usuario SET Activo = 1 WHERE Activo IS NULL;
GO

-- Bloqueo de login PROGRESIVO (Etapa 3): escalado temporal de bloqueos por intentos fallidos.
-- CantidadBloqueos: cuántas veces se bloqueó la cuenta (define la duración: 1/5/15/60 min).
-- FechaBloqueo:     instante del último bloqueo (para calcular cuándo expira). NULL = no bloqueada
--                   por tiempo (o bloqueo manual del admin, que no auto-expira).
-- NO forman parte del DVH: son metadata operativa anti-fuerza-bruta, no identidad protegida.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='CantidadBloqueos')
BEGIN
    ALTER TABLE Usuario ADD CantidadBloqueos INT NOT NULL DEFAULT 0;
    PRINT 'Columna CantidadBloqueos agregada a Usuario (bloqueo progresivo).';
END
ELSE
    PRINT 'Usuario.CantidadBloqueos ya existe — sin cambios.';
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='FechaBloqueo')
BEGIN
    ALTER TABLE Usuario ADD FechaBloqueo DATETIME NULL;
    PRINT 'Columna FechaBloqueo agregada a Usuario (bloqueo progresivo).';
END
ELSE
    PRINT 'Usuario.FechaBloqueo ya existe — sin cambios.';
GO
-- Cambio de clave OBLIGATORIO en el primer login tras un alta o un reset de contraseña.
-- RequiereCambioClave = 1 mientras la cuenta tenga una clave temporal/generada que el usuario
-- todavía no reemplazó. NO forma parte del DVH (metadata operativa, no identidad protegida).
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='RequiereCambioClave')
BEGIN
    ALTER TABLE Usuario ADD RequiereCambioClave BIT NOT NULL DEFAULT 0;
    PRINT 'Columna RequiereCambioClave agregada a Usuario (cambio de clave obligatorio).';
END
ELSE
    PRINT 'Usuario.RequiereCambioClave ya existe — sin cambios.';
GO
UPDATE Usuario SET CantidadBloqueos = 0 WHERE CantidadBloqueos IS NULL;
GO

-- ABM (revisión) — Datos administrativos NO sensibles del usuario (nombre, apellido,
-- fecha de nacimiento, email). Editables desde Administración de Usuarios y versionados
-- por el Historial de Cambios. NO entran al DVH (metadata administrativa).
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='Nombre')
    ALTER TABLE Usuario ADD Nombre NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='Apellido')
    ALTER TABLE Usuario ADD Apellido NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='FechaNacimiento')
    ALTER TABLE Usuario ADD FechaNacimiento DATE NULL;
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Usuario' AND COLUMN_NAME='Email')
    ALTER TABLE Usuario ADD Email NVARCHAR(200) NULL;
GO
PRINT 'Usuario: columnas de datos administrativos (Nombre/Apellido/FechaNacimiento/Email) verificadas.';
GO

-- Completar datos administrativos de los usuarios semilla que estén vacíos (idempotente).
UPDATE u SET u.Nombre = v.Nombre, u.Apellido = v.Apellido, u.Email = v.Email, u.FechaNacimiento = v.FechaNac
FROM Usuario u
JOIN (VALUES
    ('admin',       N'Admin',     N'Sistema',  'admin@experiencehub.com',       '1985-01-15'),
    ('vendedor',    N'Valentina', N'Bolívar',  'vendedor@experiencehub.com',    '1995-06-20'),
    ('operador',    N'Oscar',     N'Pérez',    'operador@experiencehub.com',    '1990-03-10'),
    ('auditor',     N'Ana',       N'Díaz',     'auditor@experiencehub.com',     '1988-09-05'),
    ('gcomercial',  N'Gabriel',   N'Morán',    'gcomercial@experiencehub.com',  '1983-11-25'),
    ('ginventario', N'Gisela',    N'Ortiz',    'ginventario@experiencehub.com', '1986-07-30'),
    ('logistico',   N'Lucas',     N'Gómez',    'logistico@experiencehub.com',   '1992-02-18')
) AS v(Username, Nombre, Apellido, Email, FechaNac) ON u.Username = v.Username
WHERE u.Nombre IS NULL AND u.Apellido IS NULL;
GO

-- ============================================================
-- Etapa 4 — Permisos a nivel de CONTROL (mapeo patente ↔ control de un formulario)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ControlMapeado')
BEGIN
    CREATE TABLE ControlMapeado (
        IdControlMapeado INT           IDENTITY(1,1) PRIMARY KEY,
        IdPermiso        INT           NOT NULL,
        Formulario       NVARCHAR(100) NOT NULL,
        NombreControl    NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_ControlMapeado_Permiso FOREIGN KEY (IdPermiso) REFERENCES Permiso(IdPermiso),
        CONSTRAINT UQ_ControlMapeado UNIQUE (Formulario, NombreControl)
    );
    PRINT 'Tabla ControlMapeado creada (Etapa 4 - permisos por control).';
END
ELSE
    PRINT 'Tabla ControlMapeado ya existe — sin cambios.';
GO

-- Seed inicial: mapea cada patente con NombreMenu al item de menu correspondiente del Menu
-- principal, como punto de partida coherente con la visibilidad actual. Idempotente.
INSERT INTO ControlMapeado (IdPermiso, Formulario, NombreControl)
SELECT p.IdPermiso, v.Formulario, v.NombreControl
FROM (VALUES
    ('mnuPrendas',           'Menu', 'prendasToolStripMenuItem'),
    ('mnuClientes',          'Menu', 'clientesToolStripMenuItem'),
    ('mnuPlanSuscripciones', 'Menu', 'planesToolStripMenuItem'),
    ('mnuPedidosVenta',      'Menu', 'pedidosVentaToolStripMenuItem'),
    ('mnuPedidosRealizados', 'Menu', 'pedidosRealizadosToolStripMenuItem'),
    ('mnuUsuarios',          'Menu', 'usuariosToolStripMenuItem'),
    ('mnuAuditoria',         'Menu', 'bitacoraToolStripMenuItem')
) AS v(NombreMenu, Formulario, NombreControl)
INNER JOIN Permiso p ON p.NombreMenu = v.NombreMenu
                    AND ISNULL(p.EsFamilia,0) = 0 AND ISNULL(p.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM ControlMapeado x
                  WHERE x.Formulario = v.Formulario AND x.NombreControl = v.NombreControl);
PRINT 'Seed de ControlMapeado (mapeos de menu) aplicado.';
GO

-- Claves de emergencia (1 solo uso) para autodesbloqueo de un Administrador.
-- Hasheadas (PBKDF2); el .txt con las claves en texto plano es la copia del admin.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ClaveRecuperacion')
BEGIN
    CREATE TABLE ClaveRecuperacion (
        IdClave       INT           IDENTITY(1,1) PRIMARY KEY,
        ClaveHash     NVARCHAR(500) NOT NULL,
        Usada         BIT           NOT NULL DEFAULT 0,
        UsadaPor      NVARCHAR(100) NULL,
        FechaUso      DATETIME      NULL,
        FechaCreacion DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabla ClaveRecuperacion creada (claves de emergencia).';
END
ELSE
    PRINT 'Tabla ClaveRecuperacion ya existe — sin cambios.';
GO

-- Preferencia (preferencias de UI por usuario: fuente, tamaño, tema, formato de fecha…).
-- ON DELETE CASCADE: al purgar físicamente un usuario, su fila de preferencias se borra sola.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Preferencia')
BEGIN
    CREATE TABLE Preferencia (
        IdUsuario      INT          NOT NULL PRIMARY KEY,
        FuenteFamilia  NVARCHAR(50) NULL,
        FuenteTamano   VARCHAR(10)  NULL,
        Tema           VARCHAR(20)  NULL,
        FormatoFecha   VARCHAR(20)  NULL,
        Notificaciones BIT          NULL,
        CONSTRAINT FK_Preferencia_Usuario FOREIGN KEY (IdUsuario)
            REFERENCES Usuario(IdUsuario) ON DELETE CASCADE
    );
    PRINT 'Tabla Preferencia creada.';
END
ELSE
    PRINT 'Tabla Preferencia ya existe — sin cambios.';
GO

-- ============================================================
-- MIGRACIÓN COMPOSITE (T04)
-- Genera nodos Familia desde TipoComponente si no existen aún.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM Permiso WHERE EsFamilia = 1)
BEGIN
    DECLARE @mapa TABLE (Grupo NVARCHAR(100), IdFamilia INT);

    INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia)
    OUTPUT INSERTED.Nombre, INSERTED.IdPermiso INTO @mapa (Grupo, IdFamilia)
    SELECT DISTINCT
        TipoComponente,
        TipoComponente,
        TipoComponente,
        1,
        1
    FROM Permiso
    WHERE TipoComponente IS NOT NULL
      AND LTRIM(RTRIM(TipoComponente)) <> ''
      AND EsFamilia = 0;

    INSERT INTO PermisoRelacion (IdPadre, IdHijo)
    SELECT m.IdFamilia, p.IdPermiso
    FROM   Permiso p
    INNER JOIN @mapa m ON p.TipoComponente = m.Grupo
    WHERE  p.EsFamilia = 0;

    PRINT 'Árbol Composite generado desde grupos TipoComponente.';
END
ELSE
    PRINT 'Árbol Composite ya inicializado — sin cambios.';
GO

-- ============================================================
-- MIGRACIÓN COMPOSITE (T04) — Roles como NODOS del árbol
-- A partir de v7.0 [PermisoRelacion] es la única fuente de verdad de la
-- composición. Se crea un nodo-rol por cada rol de [RolPermiso] y se migran
-- las asignaciones planas a aristas rol→permiso.
-- ============================================================

-- Crear nodo-rol faltante por cada rol existente en RolPermiso
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT DISTINCT rp.Rol, rp.Rol, 'Rol', 1, 1, 1
FROM   RolPermiso rp
WHERE  NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.Nombre = rp.Rol AND p.EsRol = 1);

-- Migrar asignaciones planas a aristas Composite (idempotente)
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT pr.IdPermiso, rp.IdPermiso
FROM   RolPermiso rp
INNER JOIN Permiso pr ON pr.Nombre = rp.Rol AND pr.EsRol = 1 AND pr.IdPermiso <> rp.IdPermiso
WHERE  NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                   WHERE x.IdPadre = pr.IdPermiso AND x.IdHijo = rp.IdPermiso);
PRINT 'Nodos-rol y aristas rol→permiso migrados (T04 v7.0).';
GO

-- ============================================================
-- JERARQUÍA DE ROLES (T04) — roles nuevos con vistas + rol-dentro-de-rol
-- Idempotente. Crea los nodos-rol nuevos, les asigna sus patentes (vistas)
-- propias y arma las aristas rol→rol para que el padre HEREDE recursivamente
-- las vistas del hijo (demostración real del Composite con jerarquía de roles).
--
--   Auditor                                → Auditoría
--   GerenteComercial  ⊃ Vendedor           → (Vendedor) + Pedidos Realizados
--   EncargadoDeStock  ⊃ OperadorLogistico  → (despacho) + Prendas + Stock
--   GerenteInventario ⊃ EncargadoDeStock   → (lo anterior) + Categorías + Outfits
-- ============================================================

-- 1) Nodos-rol para los roles nuevos (si faltan).
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT v.Rol, v.Rol, 'Rol', 1, 1, 1
FROM (VALUES
    ('Auditor'), ('GerenteComercial'), ('OperadorLogistico'),
    ('EncargadoDeStock'), ('GerenteInventario')
) AS v(Rol)
WHERE NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.Nombre = v.Rol AND p.EsRol = 1);
GO

-- 2) Patentes PROPIAS (vistas directas) de cada rol nuevo (aristas rol→patente).
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT rol.IdPermiso, pat.IdPermiso
FROM (VALUES
    ('Auditor',           'mnuAuditoria'),
    ('GerenteComercial',  'mnuPedidosRealizados'),
    ('OperadorLogistico', 'mnuPedidosRealizados'),
    ('EncargadoDeStock',  'mnuPrendas'),
    ('EncargadoDeStock',  'mnuStock'),
    ('GerenteInventario', 'mnuCategorias'),
    ('GerenteInventario', 'mnuOutfits')
) AS v(Rol, NombreMenu)
INNER JOIN Permiso rol ON rol.Nombre = v.Rol AND rol.EsRol = 1
INNER JOIN Permiso pat ON pat.NombreMenu = v.NombreMenu
                       AND ISNULL(pat.EsFamilia,0) = 0 AND ISNULL(pat.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                  WHERE x.IdPadre = rol.IdPermiso AND x.IdHijo = pat.IdPermiso);
GO

-- 3) Jerarquía rol→rol: el padre hereda recursivamente las vistas del hijo.
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT padre.IdPermiso, hijo.IdPermiso
FROM (VALUES
    ('GerenteComercial',  'Vendedor'),
    ('EncargadoDeStock',  'OperadorLogistico'),
    ('GerenteInventario', 'EncargadoDeStock')
) AS v(Padre, Hijo)
INNER JOIN Permiso padre ON padre.Nombre = v.Padre AND padre.EsRol = 1
INNER JOIN Permiso hijo  ON hijo.Nombre  = v.Hijo  AND hijo.EsRol  = 1
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                  WHERE x.IdPadre = padre.IdPermiso AND x.IdHijo = hijo.IdPermiso);
GO

-- ============================================================
-- USUARIOS DEMO DE LOS ROLES NUEVOS + SUPERVISOR COMPUESTO
-- Idempotente. Crea un usuario por rol nuevo (claves en Contraseñas.txt) y convierte a
-- Supervisor en rol COMPUESTO (Supervisor ⊃ Vendedor) conservando su Auditoría propia:
-- mismos permisos efectivos que antes, pero ahora obtenidos por HERENCIA (no copiados).
-- Las claves se guardan hasheadas (PBKDF2). Texto plano: usuario1! (ver Contraseñas.txt).
-- ============================================================

INSERT INTO Usuario (Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH, IdIdioma, Activo)
SELECT v.Username, v.Clave, v.Rol, v.Perfil, 1, 0, 0, 'ES', 1
FROM (VALUES
  ('auditor',     'SmnGp5hqSC+FXdbLiccYieNpC6vEaWn6nVpgZFrlyyeOXqx8yTjJYOgaw+oXAP7B', 'Auditor',           'Auditor'),
  ('gcomercial',  '1KWgyl8MkMcuisigf4fRBDb2f803LIsA/hvfKpzfwcMbRboUiS5CwuvFQq64GJft', 'GerenteComercial',  'GerenteComercial'),
  ('ginventario', 'G89lxogCulMeAK+WA5rNHSyWpuz5QKF/DBH8PSfiPbUv5SBKStFVQYXylikq+OMw', 'GerenteInventario', 'GerenteInventario'),
  ('encargado',   'H9LyJ5OBv0m2atrETHrimO5mBcVpKLn46uy/hiTgw4130wCo2H7/sgqAoZ/zroA8', 'EncargadoDeStock',  'EncargadoDeStock'),
  ('logistico',   'U3g703lDqDJfLgaXpOaNiAQpDOaBSq1LUMzEu3X7x8hh6097jTcMUNAsPfl1SCVG', 'OperadorLogistico', 'OperadorLogistico')
) AS v(Username, Clave, Rol, Perfil)
WHERE NOT EXISTS (SELECT 1 FROM Usuario u WHERE u.Username = v.Username);
PRINT 'Usuarios demo de roles nuevos inicializados.';
GO

-- Supervisor → COMPUESTO: se quitan las patentes de Vendedor asignadas directo (quedan por
-- herencia) y se agrega la arista Supervisor → Vendedor; conserva su Auditoría propia.
DELETE r FROM PermisoRelacion r
JOIN Permiso sup ON sup.IdPermiso = r.IdPadre AND sup.Nombre = 'Supervisor' AND sup.EsRol = 1
JOIN Permiso pat ON pat.IdPermiso = r.IdHijo
                AND pat.NombreMenu IN ('mnuPrendas','mnuClientes','mnuPlanSuscripciones','mnuPedidosVenta');
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT sup.IdPermiso, ven.IdPermiso
FROM Permiso sup JOIN Permiso ven ON ven.Nombre = 'Vendedor' AND ven.EsRol = 1
WHERE sup.Nombre = 'Supervisor' AND sup.EsRol = 1
  AND NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = sup.IdPermiso AND x.IdHijo = ven.IdPermiso);
PRINT 'Supervisor convertido en rol compuesto (Supervisor → Vendedor).';
GO

-- Si se insertaron usuarios nuevos en una BD ya inicializada, resetear el DV para que la app
-- lo recalcule limpio en el próximo arranque (evita una falsa alarma de integridad por mezcla).
IF EXISTS (SELECT 1 FROM Usuario WHERE Username IN ('auditor','gcomercial','ginventario','encargado','logistico') AND DVH = 0)
   AND EXISTS (SELECT 1 FROM Usuario WHERE DVH <> 0)
BEGIN
    UPDATE Usuario SET DVH = 0;
    UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Usuario';
    PRINT 'DV de Usuario reseteado para recálculo en el próximo arranque.';
END
GO

-- ============================================================
-- Simplificación de Permisos — ELIMINAR FAMILIAS del árbol Composite (revisión)
-- Los permisos (patentes) son un catálogo fijo y las FAMILIAS se retiran de la experiencia.
-- (1) Se materializan las relaciones Rol→Patente alcanzables a través de familias (por si un
--     usuario había anidado familias dentro de roles), (2) se quitan las aristas que tocan
--     familias y (3) se desactivan los nodos Familia. El anidamiento Rol→Rol se preserva
--     (el patrón Composite sigue vigente). Idempotente.
-- ============================================================
IF EXISTS (SELECT 1 FROM Permiso WHERE ISNULL(EsFamilia,0)=1 AND ISNULL(EsRol,0)=0 AND Estado=1)
BEGIN
    ;WITH Arbol AS (
        SELECT r.IdPermiso AS IdRol, pr.IdHijo AS IdNodo
        FROM Permiso r
        JOIN PermisoRelacion pr ON pr.IdPadre = r.IdPermiso
        WHERE r.EsRol = 1
        UNION ALL
        SELECT a.IdRol, pr.IdHijo
        FROM Arbol a
        JOIN Permiso n ON n.IdPermiso = a.IdNodo AND ISNULL(n.EsFamilia,0)=1 AND ISNULL(n.EsRol,0)=0
        JOIN PermisoRelacion pr ON pr.IdPadre = a.IdNodo
    )
    INSERT INTO PermisoRelacion (IdPadre, IdHijo)
    SELECT DISTINCT a.IdRol, a.IdNodo
    FROM Arbol a
    JOIN Permiso p ON p.IdPermiso = a.IdNodo AND ISNULL(p.EsFamilia,0)=0 AND ISNULL(p.EsRol,0)=0
    WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre=a.IdRol AND x.IdHijo=a.IdNodo)
    OPTION (MAXRECURSION 50);

    DELETE r FROM PermisoRelacion r
    JOIN Permiso p ON (p.IdPermiso = r.IdPadre OR p.IdPermiso = r.IdHijo)
    WHERE ISNULL(p.EsFamilia,0)=1 AND ISNULL(p.EsRol,0)=0;

    UPDATE Permiso SET Estado = 0 WHERE ISNULL(EsFamilia,0)=1 AND ISNULL(EsRol,0)=0;

    PRINT 'Familias eliminadas del Composite: roles aplanados a Rol->Patente.';
END
ELSE
    PRINT 'No hay familias activas — árbol ya aplanado.';
GO

-- ============================================================
-- MIGRACIONES INCREMENTALES
-- ============================================================

-- T03 — Preparar DNI para almacenar el valor CIFRADO (AES Base64 ~44 chars).
-- 1) Quitar la constraint UNIQUE sobre DNI: el cifrado usa IV aleatorio (mismo DNI →
--    distinto texto), por lo que la unicidad a nivel BD ya no aplica. La unicidad se
--    valida en la capa de negocio (DAL.Cliente/Empleado.ExisteDNI, descifrando).
DECLARE @uqCli SYSNAME = (SELECT TOP 1 tc.CONSTRAINT_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON tc.CONSTRAINT_NAME=ccu.CONSTRAINT_NAME
    WHERE tc.CONSTRAINT_TYPE='UNIQUE' AND tc.TABLE_NAME='Cliente' AND ccu.COLUMN_NAME='DNI');
IF @uqCli IS NOT NULL EXEC('ALTER TABLE Cliente DROP CONSTRAINT ' + @uqCli);

DECLARE @uqEmp SYSNAME = (SELECT TOP 1 tc.CONSTRAINT_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON tc.CONSTRAINT_NAME=ccu.CONSTRAINT_NAME
    WHERE tc.CONSTRAINT_TYPE='UNIQUE' AND tc.TABLE_NAME='Empleado' AND ccu.COLUMN_NAME='DNI');
IF @uqEmp IS NOT NULL EXEC('ALTER TABLE Empleado DROP CONSTRAINT ' + @uqEmp);
GO

-- 2) Ensanchar la columna para el texto cifrado.
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME='Cliente' AND COLUMN_NAME='DNI' AND CHARACTER_MAXIMUM_LENGTH < 200)
BEGIN
    ALTER TABLE Cliente ALTER COLUMN DNI NVARCHAR(200) NOT NULL;
    PRINT 'Cliente.DNI ensanchado a NVARCHAR(200) (cifrado T03).';
END
GO
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME='Empleado' AND COLUMN_NAME='DNI' AND CHARACTER_MAXIMUM_LENGTH < 200)
BEGIN
    ALTER TABLE Empleado ALTER COLUMN DNI NVARCHAR(200) NOT NULL;
    PRINT 'Empleado.DNI ensanchado a NVARCHAR(200) (cifrado T03).';
END
GO

-- T07 — Columna DVH (dígito verificador horizontal) en Cliente y Empleado.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Cliente' AND COLUMN_NAME='DVH')
BEGIN
    ALTER TABLE Cliente ADD DVH INT NULL;
    PRINT 'Columna DVH agregada a Cliente (T07).';
END
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Empleado' AND COLUMN_NAME='DVH')
BEGIN
    ALTER TABLE Empleado ADD DVH INT NULL;
    PRINT 'Columna DVH agregada a Empleado (T07).';
END
GO
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Pedido' AND COLUMN_NAME='DVH')
BEGIN
    ALTER TABLE Pedido ADD DVH INT NULL;
    PRINT 'Columna DVH agregada a Pedido (T07 multi-tabla).';
END
GO

-- FechaVencimiento en Cliente (suscripción con fecha de vencimiento)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Cliente' AND COLUMN_NAME = 'FechaVencimiento')
BEGIN
    ALTER TABLE Cliente ADD FechaVencimiento DATE NULL;
    PRINT 'Columna FechaVencimiento agregada a Cliente.';
END
ELSE
    PRINT 'FechaVencimiento ya existe en Cliente — sin cambios.';
GO

-- FechaNacimiento en Cliente — OBLIGATORIA (NOT NULL) para validar mayoría de edad.
-- 1) Agregar la columna como NULL si todavía no existe.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Cliente' AND COLUMN_NAME = 'FechaNacimiento')
BEGIN
    ALTER TABLE Cliente ADD FechaNacimiento DATE NULL;
    PRINT 'Columna FechaNacimiento agregada a Cliente.';
END
GO
-- 2) Backfill de filas legacy sin fecha (placeholder mayor de edad) para poder imponer NOT NULL.
--    Revisá estos clientes en la app y cargales su fecha real.
UPDATE Cliente SET FechaNacimiento = '1990-01-01' WHERE FechaNacimiento IS NULL;
GO
-- 3) Imponer NOT NULL si la columna todavía admite nulos.
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME='Cliente' AND COLUMN_NAME='FechaNacimiento' AND IS_NULLABLE='YES')
BEGIN
    ALTER TABLE Cliente ALTER COLUMN FechaNacimiento DATE NOT NULL;
    PRINT 'Cliente.FechaNacimiento ahora es NOT NULL (obligatoria).';
END
ELSE
    PRINT 'Cliente.FechaNacimiento ya es NOT NULL — sin cambios.';
GO

-- Permisos adicionales para Supervisor (mismo acceso que Vendedor + auditoría ya tenía)
INSERT INTO RolPermiso (Rol, IdPermiso)
SELECT r.Rol, p.IdPermiso
FROM (VALUES
    ('Supervisor','mnuPrendas'),
    ('Supervisor','mnuClientes'),
    ('Supervisor','mnuPlanSuscripciones'),
    ('Supervisor','mnuPedidosVenta')
) AS r(Rol, NombreMenu)
JOIN Permiso p ON p.NombreMenu = r.NombreMenu AND ISNULL(p.EsFamilia,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM RolPermiso x WHERE x.Rol = r.Rol AND x.IdPermiso = p.IdPermiso);

-- Regenerar aristas Composite para el nodo Supervisor
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT pr.IdPermiso, rp.IdPermiso
FROM   RolPermiso rp
INNER JOIN Permiso pr ON pr.Nombre = rp.Rol AND pr.EsRol = 1 AND pr.IdPermiso <> rp.IdPermiso
WHERE  NOT EXISTS (SELECT 1 FROM PermisoRelacion x
                   WHERE x.IdPadre = pr.IdPermiso AND x.IdHijo = rp.IdPermiso);
PRINT 'Permisos de Supervisor actualizados (mnuPrendas/mnuClientes/mnuPlanSuscripciones/mnuPedidosVenta).';
GO

-- ============================================================
-- CONSOLIDACIÓN DE ROLES (v8) — decisión de diseño 2da entrega
-- Va AL FINAL de todas las migraciones de roles (después de la última regeneración de
-- aristas desde RolPermiso) para que el estado objetivo no sea sobreescrito. Idempotente.
--   • Inventario — dos operadores con responsabilidades CLARAS, sin duplicados:
--       - OperadorLogistico    → pedidos / despacho       (Ver Pedidos Realizados)
--       - OperadorDeInventario → mantenimiento de prendas (Ver Prendas + Gestionar Stock)
--       - GerenteInventario ⊃ AMBOS                       (+ Categorías + Outfits)
--     Se RETIRAN EncargadoDeStock y ControladorDeStock (eran redundantes con lo anterior).
--   • Comercial — se RETIRA Supervisor; el jefe es GerenteComercial ⊃ Vendedor.
-- ============================================================

-- (a) Asegurar el nodo-rol OperadorDeInventario (por si la BD no lo tenía).
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT 'OperadorDeInventario', 'OperadorDeInventario', 'Rol', 1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM Permiso WHERE Nombre = 'OperadorDeInventario' AND EsRol = 1);
GO

-- (b) OperadorDeInventario: sus patentes propias deben ser EXACTAMENTE Prendas + Stock.
--     Se quita cualquier patente vieja (p. ej. el legacy 'Ver Pedidos Realizados') y se agregan las nuevas.
DELETE r FROM PermisoRelacion r
JOIN Permiso rol ON rol.IdPermiso = r.IdPadre AND rol.Nombre = 'OperadorDeInventario' AND rol.EsRol = 1
JOIN Permiso pat ON pat.IdPermiso = r.IdHijo  AND ISNULL(pat.EsRol,0) = 0
WHERE pat.NombreMenu NOT IN ('mnuPrendas','mnuStock');
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT rol.IdPermiso, pat.IdPermiso
FROM (VALUES ('mnuPrendas'), ('mnuStock')) AS v(NombreMenu)
JOIN Permiso rol ON rol.Nombre = 'OperadorDeInventario' AND rol.EsRol = 1
JOIN Permiso pat ON pat.NombreMenu = v.NombreMenu AND ISNULL(pat.EsFamilia,0) = 0 AND ISNULL(pat.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = rol.IdPermiso AND x.IdHijo = pat.IdPermiso);
GO

-- (c) GerenteInventario ⊃ OperadorLogistico + OperadorDeInventario (y se quita la arista vieja a EncargadoDeStock).
DELETE r FROM PermisoRelacion r
JOIN Permiso gi ON gi.IdPermiso = r.IdPadre AND gi.Nombre = 'GerenteInventario' AND gi.EsRol = 1
JOIN Permiso ed ON ed.IdPermiso = r.IdHijo  AND ed.Nombre = 'EncargadoDeStock'  AND ed.EsRol = 1;
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT gi.IdPermiso, h.IdPermiso
FROM (VALUES ('OperadorLogistico'), ('OperadorDeInventario')) AS v(Hijo)
JOIN Permiso gi ON gi.Nombre = 'GerenteInventario' AND gi.EsRol = 1
JOIN Permiso h  ON h.Nombre  = v.Hijo AND h.EsRol = 1
WHERE NOT EXISTS (SELECT 1 FROM PermisoRelacion x WHERE x.IdPadre = gi.IdPermiso AND x.IdHijo = h.IdPermiso);
GO

-- (d) Migrar usuarios de los roles que se retiran + normalizar Perfil a código interno.
DECLARE @rolesCambiados INT = 0;
UPDATE Usuario SET Rol = 'OperadorDeInventario', Perfil = 'OperadorDeInventario'
WHERE Rol IN ('EncargadoDeStock','ControladorDeStock') OR Perfil IN ('EncargadoDeStock','ControladorDeStock','Controlador de Stock');
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;
UPDATE Usuario SET Rol = 'GerenteComercial', Perfil = 'GerenteComercial'
WHERE Rol = 'Supervisor' OR Perfil = 'Supervisor';
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;
-- Normalizar perfiles legacy guardados con el NOMBRE VISIBLE (con espacios) al código interno,
-- para que el switch de dashboard por Perfil y las comparaciones de rol funcionen igual que con los nuevos.
UPDATE Usuario SET Perfil = 'OperadorDeInventario' WHERE Perfil = 'Operador de Inventario';
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;
UPDATE Usuario SET Perfil = 'OperadorLogistico' WHERE Perfil = 'Operador Logístico';
SET @rolesCambiados = @rolesCambiados + @@ROWCOUNT;

-- (e) Retirar los roles redundantes: quitar TODAS sus aristas (como padre o como hijo) y desactivar el nodo.
DELETE r FROM PermisoRelacion r
JOIN Permiso p ON (p.IdPermiso = r.IdPadre OR p.IdPermiso = r.IdHijo)
WHERE p.EsRol = 1 AND p.Nombre IN ('EncargadoDeStock','ControladorDeStock','Supervisor');
UPDATE Permiso SET Estado = 0 WHERE EsRol = 1 AND Nombre IN ('EncargadoDeStock','ControladorDeStock','Supervisor');

-- (f) Si cambió el Rol/Perfil de algún usuario, el DVH (que incluye el Rol) quedó desfasado:
--     resetear el DV de Usuario para que la app lo recalcule limpio en el próximo arranque.
IF @rolesCambiados > 0
BEGIN
    UPDATE Usuario SET DVH = 0;
    UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Usuario';
    PRINT 'Roles consolidados; DV de Usuario reseteado para recálculo en el próximo arranque.';
END
PRINT 'Consolidación de roles (v8) aplicada.';
GO

-- MantenimientoPrenda (historial de limpieza/mantenimiento por prenda)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MantenimientoPrenda')
BEGIN
    CREATE TABLE MantenimientoPrenda (
        IdMantenimiento INT           IDENTITY(1,1) PRIMARY KEY,
        IdPrenda        INT           NOT NULL REFERENCES Prenda(IdPrenda),
        FechaEntrada    DATETIME      NOT NULL DEFAULT GETDATE(),
        FechaSalida     DATETIME      NULL,
        Actor           NVARCHAR(100) NULL
    );
    PRINT 'Tabla MantenimientoPrenda creada.';
END
ELSE
    PRINT 'Tabla MantenimientoPrenda ya existe — sin cambios.';
GO

-- ============================================================
-- DATOS DEMO (presentación / pruebas)
-- ------------------------------------------------------------
-- Idempotente: cada bloque sólo inserta si el registro falta.
-- El DNI se guarda en TEXTO PLANO; la app lo tolera (TryDesencriptar
-- acepta registros legacy sin cifrar) y lo muestra tal cual.
-- DVH=0 → recalcular el DV desde la app (Usuarios → Recalcular DV)
-- antes del primer uso para que la verificación de integridad cierre.
-- ============================================================

-- Planes de suscripción
INSERT INTO PlanSuscripcion (Nombre, LimitePrendas, Precio, Estado)
SELECT v.Nombre, v.Limite, v.Precio, 1
FROM (VALUES
    (N'Básico',   5,  8000.00),
    (N'Estándar', 15, 15000.00),
    (N'Premium',  30, 25000.00)
) AS v(Nombre, Limite, Precio)
WHERE NOT EXISTS (SELECT 1 FROM PlanSuscripcion p WHERE p.Nombre = v.Nombre);
PRINT 'Demo: planes de suscripción.';
GO

-- Empleados (vinculados a los usuarios del sistema por Username)
INSERT INTO Empleado (Nombre, Apellido, DNI, Email, FechaIngreso, Puesto, Legajo, IdUsuario, DVH)
SELECT v.Nombre, v.Apellido, v.DNI, v.Email, GETDATE(), v.Puesto, v.Legajo,
       (SELECT TOP 1 IdUsuario FROM Usuario u WHERE u.Username = v.Username), 0
FROM (VALUES
    (N'Valentina', N'Morana', '33111000', 'vendedor@experiencehub.com', N'Vendedora',           'L-001', 'vendedor'),
    (N'Bruno',     N'Díaz',   '31222000', 'operador@experiencehub.com', N'Operador Inventario', 'L-002', 'operador'),
    (N'Carla',     N'Méndez', '32333000', 'stock@experiencehub.com',    N'Controlador Stock',   'L-003', 'stock')
) AS v(Nombre, Apellido, DNI, Email, Puesto, Legajo, Username)
WHERE NOT EXISTS (SELECT 1 FROM Empleado e WHERE e.Legajo = v.Legajo);
PRINT 'Demo: empleados.';
GO

-- Clientes (todos mayores de edad; FechaNacimiento obligatoria)
INSERT INTO Cliente (Nombre, Apellido, DNI, Email, MetodoPago, IdPlan, FechaAlta, FechaNacimiento, Activo, DVH)
SELECT v.Nombre, v.Apellido, v.DNI, v.Email, v.MetodoPago,
       (SELECT TOP 1 IdPlan FROM PlanSuscripcion p WHERE p.Nombre = v.PlanNom),
       GETDATE(), v.FechaNac, 1, 0
FROM (VALUES
    (N'Lucía',  N'Fernández', '30111222', 'lucia.fernandez@mail.com', 'Efectivo',      N'Premium',  CONVERT(date,'1990-03-15')),
    (N'Martín', N'Gómez',     '28999111', 'martin.gomez@mail.com',    'Crédito',       N'Estándar', CONVERT(date,'1985-07-22')),
    (N'Sofía',  N'Rossi',     '35444555', 'sofia.rossi@mail.com',     'Débito',        N'Básico',   CONVERT(date,'1998-11-02')),
    (N'Diego',  N'Paz',       '27333444', 'diego.paz@mail.com',       'Transferencia', N'Estándar', CONVERT(date,'1982-01-30')),
    (N'Camila', N'Torres',    '40222333', 'camila.torres@mail.com',   'Efectivo',      N'Premium',  CONVERT(date,'2001-06-10'))
) AS v(Nombre, Apellido, DNI, Email, MetodoPago, PlanNom, FechaNac)
WHERE NOT EXISTS (SELECT 1 FROM Cliente c WHERE c.Nombre = v.Nombre AND c.Apellido = v.Apellido);
PRINT 'Demo: clientes.';
GO

-- Prendas (catálogo de stock; todas Disponible inicialmente)
INSERT INTO Prenda (Nombre, Descripcion, Talle, Color, Categoria, Estado, IdClienteActual, FechaAlta)
SELECT v.Nombre, v.Descripcion, v.Talle, v.Color, v.Categoria, 0, NULL, GETDATE()
FROM (VALUES
    (N'Vestido Largo Negro',    N'Vestido de fiesta largo',  'M',  N'Negro',      N'Vestido'),
    (N'Blazer Beige',           N'Blazer entallado',         'L',  N'Beige',      N'Saco'),
    (N'Camisa Blanca Clásica',  N'Camisa de algodón',        'M',  N'Blanco',     N'Camisa'),
    (N'Pantalón Sastre Gris',   N'Pantalón de vestir',       '42', N'Gris',       N'Pantalón'),
    (N'Abrigo Largo Camel',     N'Tapado de paño',           'L',  N'Camel',      N'Abrigo'),
    (N'Vestido Floral',         N'Vestido estampado verano', 'S',  N'Estampado',  N'Vestido'),
    (N'Camisa Celeste',         N'Camisa de lino',           'L',  N'Celeste',    N'Camisa'),
    (N'Jean Recto Azul',        N'Jean clásico',             '40', N'Azul',       N'Pantalón'),
    (N'Saco a Cuadros',         N'Saco príncipe de Gales',   'M',  N'Multicolor', N'Saco'),
    (N'Falda Plisada Negra',    N'Falda midi plisada',       'S',  N'Negro',      N'Falda'),
    (N'Sweater Oversize Crema', N'Sweater de lana',          'L',  N'Crema',      N'Sweater'),
    (N'Gabardina Verde',        N'Gabardina impermeable',    'M',  N'Verde',      N'Abrigo')
) AS v(Nombre, Descripcion, Talle, Color, Categoria)
WHERE NOT EXISTS (SELECT 1 FROM Prenda pr WHERE pr.Nombre = v.Nombre);
PRINT 'Demo: prendas (stock).';
GO

-- Pedidos demo (sólo si aún no hay pedidos) + prendas EnUso asignadas a su cliente,
-- replicando lo que hace la app al crear/despachar (Prenda.Estado=EnUso, IdClienteActual).
IF NOT EXISTS (SELECT 1 FROM Pedido)
   AND EXISTS (SELECT 1 FROM Empleado WHERE Legajo = 'L-001')
   AND EXISTS (SELECT 1 FROM Cliente WHERE Nombre = N'Lucía' AND Apellido = N'Fernández')
BEGIN
    DECLARE @emp     INT = (SELECT TOP 1 IdEmpleado FROM Empleado WHERE Legajo = 'L-001');
    DECLARE @cLucia  INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Lucía'  AND Apellido=N'Fernández');
    DECLARE @cMartin INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Martín' AND Apellido=N'Gómez');
    DECLARE @cSofia  INT = (SELECT TOP 1 IdCliente FROM Cliente WHERE Nombre=N'Sofía'  AND Apellido=N'Rossi');

    DECLARE @pr1 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Vestido Largo Negro');
    DECLARE @pr2 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Blazer Beige');
    DECLARE @pr3 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Camisa Blanca Clásica');
    DECLARE @pr4 INT = (SELECT IdPrenda FROM Prenda WHERE Nombre=N'Pantalón Sastre Gris');

    -- Pedido 1: Lucía — Entregado (2 prendas)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, FechaDespacho, FechaEntrega, DVH)
    VALUES (@cLucia, @emp, 2, DATEADD(day,-20,GETDATE()), DATEADD(day,-18,GETDATE()), DATEADD(day,-15,GETDATE()), 0);
    DECLARE @ped1 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped1, @pr1), (@ped1, @pr2);

    -- Pedido 2: Martín — Despachado (1 prenda)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, FechaDespacho, DVH)
    VALUES (@cMartin, @emp, 1, DATEADD(day,-5,GETDATE()), DATEADD(day,-3,GETDATE()), 0);
    DECLARE @ped2 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped2, @pr3);

    -- Pedido 3: Sofía — Pendiente (1 prenda)
    INSERT INTO Pedido (IdCliente, IdEmpleado, Estado, FechaPedido, DVH)
    VALUES (@cSofia, @emp, 0, DATEADD(day,-1,GETDATE()), 0);
    DECLARE @ped3 INT = SCOPE_IDENTITY();
    INSERT INTO PedidoPrenda (IdPedido, IdPrenda) VALUES (@ped3, @pr4);

    -- Marcar prendas EnUso (Estado=1) con su cliente
    UPDATE Prenda SET Estado=1, IdClienteActual=@cLucia  WHERE IdPrenda IN (@pr1,@pr2);
    UPDATE Prenda SET Estado=1, IdClienteActual=@cMartin WHERE IdPrenda=@pr3;
    UPDATE Prenda SET Estado=1, IdClienteActual=@cSofia  WHERE IdPrenda=@pr4;

    PRINT 'Demo: 3 pedidos (Entregado/Despachado/Pendiente) + prendas asignadas.';
END
ELSE
    PRINT 'Demo: pedidos ya existen o faltan datos base — sin cambios.';
GO

-- ============================================================
-- ÍNDICES NO-CLUSTERED Y RESTRICCIONES DE INTEGRIDAD
-- Idempotente: solo crea lo que falta. Mejoran los joins por FK y los filtros por fecha,
-- y el CHECK respalda en el motor la prohibición de auto-referencia del árbol Composite.
-- ============================================================

-- Índices sobre columnas FK que NO son la columna LÍDER de su PK (la PK ya indexa su primera
-- columna) y sobre columnas usadas para filtrar/ordenar (fechas).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bitacora_fecha' AND object_id = OBJECT_ID('Bitacora'))
    CREATE NONCLUSTERED INDEX IX_Bitacora_fecha ON Bitacora(fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bitacora_usuario' AND object_id = OBJECT_ID('Bitacora'))
    CREATE NONCLUSTERED INDEX IX_Bitacora_usuario ON Bitacora(usuario);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BitacoraNegocio_Fecha' AND object_id = OBJECT_ID('BitacoraNegocio'))
    CREATE NONCLUSTERED INDEX IX_BitacoraNegocio_Fecha ON BitacoraNegocio(Fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PermisoRelacion_IdHijo' AND object_id = OBJECT_ID('PermisoRelacion'))
    CREATE NONCLUSTERED INDEX IX_PermisoRelacion_IdHijo ON PermisoRelacion(IdHijo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolPermiso_IdPermiso' AND object_id = OBJECT_ID('RolPermiso'))
    CREATE NONCLUSTERED INDEX IX_RolPermiso_IdPermiso ON RolPermiso(IdPermiso);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Traduccion_IdIdioma' AND object_id = OBJECT_ID('Traduccion'))
    CREATE NONCLUSTERED INDEX IX_Traduccion_IdIdioma ON Traduccion(IdIdioma);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_IdCliente' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_IdCliente ON Pedido(IdCliente);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_IdEmpleado' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_IdEmpleado ON Pedido(IdEmpleado);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pedido_FechaPedido' AND object_id = OBJECT_ID('Pedido'))
    CREATE NONCLUSTERED INDEX IX_Pedido_FechaPedido ON Pedido(FechaPedido);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PedidoPrenda_IdPrenda' AND object_id = OBJECT_ID('PedidoPrenda'))
    CREATE NONCLUSTERED INDEX IX_PedidoPrenda_IdPrenda ON PedidoPrenda(IdPrenda);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PedidoHistorial_IdPedido' AND object_id = OBJECT_ID('PedidoHistorial'))
    CREATE NONCLUSTERED INDEX IX_PedidoHistorial_IdPedido ON PedidoHistorial(IdPedido);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prenda_IdClienteActual' AND object_id = OBJECT_ID('Prenda'))
    CREATE NONCLUSTERED INDEX IX_Prenda_IdClienteActual ON Prenda(IdClienteActual);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HistorialUsuario_IdUsuario' AND object_id = OBJECT_ID('HistorialUsuario'))
    CREATE NONCLUSTERED INDEX IX_HistorialUsuario_IdUsuario ON HistorialUsuario(IdUsuario);
PRINT 'Índices no-clustered verificados/creados.';
GO

-- CHECK anti auto-referencia del árbol Composite: un permiso no puede ser su propio hijo.
-- (La validación de ciclos completa vive en BE.Familia; esto la respalda en el motor por si
-- alguien escribe por SQL directo.) Se limpian filas inválidas preexistentes para que no falle.
IF EXISTS (SELECT 1 FROM PermisoRelacion WHERE IdPadre = IdHijo)
BEGIN
    DELETE FROM PermisoRelacion WHERE IdPadre = IdHijo;
    PRINT 'PermisoRelacion: filas auto-referenciadas (IdPadre = IdHijo) eliminadas.';
END
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PermisoRelacion_NoAutoref')
BEGIN
    ALTER TABLE PermisoRelacion ADD CONSTRAINT CK_PermisoRelacion_NoAutoref CHECK (IdPadre <> IdHijo);
    PRINT 'CHECK CK_PermisoRelacion_NoAutoref agregado (IdPadre <> IdHijo).';
END
ELSE
    PRINT 'CHECK CK_PermisoRelacion_NoAutoref ya existe — sin cambios.';
GO

PRINT '';
PRINT '=== ExperienceHubDB deploy completo. ===';
PRINT 'IMPORTANTE: Ejecutar recálculo de DVH/DVV desde la aplicación';
PRINT '            antes del primer uso (Administrar → Usuarios → Recalcular DV).';
PRINT 'Las traducciones se seedean automáticamente en el primer uso de la app.';
GO

GO

-- ===================================================================
-- MODULO: 03_Permisos_Granulares.sql
-- ===================================================================
/* ============================================================================
   03_Permisos_Granulares.sql
   Tier 2 — Permisos de ACCIÓN granular: separar "Ver" de "Configurar".

   Crea una patente de EDICIÓN por cada módulo de negocio (alta/modificación/baja)
   y la propaga ("grandfather") a los roles que hoy pueden editar, de modo que el
   comportamiento NO cambie al aplicar el script. Luego, desde el Gestor de Perfiles,
   el Administrador puede QUITAR la patente de edición a los roles que deban quedar
   de solo-lectura (verán el módulo pero no podrán modificarlo).

   Resolución en runtime: BLL.PermisosAccion. Si estas patentes NO existen, la BLL
   cae al permiso de VER (retrocompatibilidad), así que aplicar el código sin correr
   este script tampoco rompe nada.

   IDEMPOTENTE: se puede ejecutar varias veces sin duplicar filas.
   Reiniciá la aplicación luego de correrlo (el catálogo de patentes se cachea al inicio).
   ============================================================================ */

SET NOCOUNT ON;

DECLARE @map TABLE (VerMenu NVARCHAR(100), EditarMenu NVARCHAR(100), EditarNombre NVARCHAR(200));
INSERT INTO @map (VerMenu, EditarMenu, EditarNombre) VALUES
 ('mnuStock',             'mnuStockEditar',             'Configurar Prendas / Stock'),
 ('mnuClientes',          'mnuClientesEditar',          'Configurar Clientes'),
 ('mnuPlanSuscripciones', 'mnuPlanSuscripcionesEditar', 'Configurar Planes de Suscripción'),
 ('mnuPedidosVenta',      'mnuPedidosVentaEditar',      'Configurar Pedidos de Venta'),
 ('mnuPedidosRealizados', 'mnuPedidosRealizadosEditar', 'Configurar Pedidos Realizados');

-- 1) Crear las patentes de EDICIÓN que falten (EsFamilia=0, EsRol=0 → hojas / patentes).
INSERT INTO Permiso (Nombre, NombreMenu, TipoComponente, Estado, EsFamilia, EsRol)
SELECT m.EditarNombre, m.EditarMenu, 'Acción', 1, 0, 0
FROM   @map m
WHERE  EXISTS     (SELECT 1 FROM Permiso v WHERE v.NombreMenu = m.VerMenu)
  AND  NOT EXISTS (SELECT 1 FROM Permiso e WHERE e.NombreMenu = m.EditarMenu);

-- 2) "Grandfather": donde una patente de VER esté asignada DIRECTAMENTE a un rol/familia,
--    asignar también su patente de EDITAR (preserva el comportamiento actual).
INSERT INTO PermisoRelacion (IdPadre, IdHijo)
SELECT r.IdPadre, e.IdPermiso
FROM   PermisoRelacion r
JOIN   Permiso v ON v.IdPermiso  = r.IdHijo
JOIN   @map    m ON m.VerMenu    = v.NombreMenu
JOIN   Permiso e ON e.NombreMenu = m.EditarMenu
WHERE  NOT EXISTS (SELECT 1 FROM PermisoRelacion r2
                   WHERE r2.IdPadre = r.IdPadre AND r2.IdHijo = e.IdPermiso);

PRINT 'OK: patentes de acción creadas y propagadas (idempotente).';

/* ----------------------------------------------------------------------------
   OPCIONAL — ocultar también los botones de edición en la GUI para los usuarios
   sin la patente de edición. ExperienceHub ya tiene el mecanismo data-driven
   (ControlMapeado + ManejadorSeguridad): basta mapear los botones Guardar/Eliminar
   de cada formulario a su patente "…Editar" desde la pantalla "Mapeo de Controles".
   La re-validación de backend (BLL.PermisosAccion) ya protege la operación aunque
   el botón no se oculte.
   ---------------------------------------------------------------------------- */

GO

-- ===================================================================
-- MODULO: 05_Dominio_ExperienceHub.sql
-- ===================================================================
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

GO

-- ===================================================================
-- MODULO: 06_Alinear_Roles_ExperienceHub.sql
-- ===================================================================
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

DECLARE @agente INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas'          AND EsRol = 1);
DECLARE @coord  INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1);

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

DECLARE @agente INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Agente de Reservas'          AND EsRol = 1);
DECLARE @coord  INT = (SELECT IdPermiso FROM Permiso WHERE Nombre = N'Coordinador de Experiencias' AND EsRol = 1);

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

GO

-- ===================================================================
-- MODULO: 07_Contratacion_PN01.sql
-- ===================================================================
/* ============================================================================
   07 - PN01 Comercializacion de la suscripcion : tabla Contratacion
   ----------------------------------------------------------------------------
   Registra la venta de una suscripcion en curso:
   Venta la crea PendienteDePago -> Caja la cobra (y emite comprobante) ->
   Venta la formaliza como Suscripcion vigente; o se Cancela al agotar intentos.
   Ejecutar DESPUES de 05_Dominio_ExperienceHub.sql (requiere Cliente,
   PlanSuscripcion y Suscripcion).
   ============================================================================ */
IF OBJECT_ID('Contratacion','U') IS NULL
BEGIN
    CREATE TABLE Contratacion (
        IdContratacion    INT            IDENTITY(1,1) PRIMARY KEY,
        IdCliente         INT            NOT NULL REFERENCES Cliente(IdCliente),
        IdPlan            INT            NOT NULL REFERENCES PlanSuscripcion(IdPlan),
        Importe           DECIMAL(10, 2) NOT NULL DEFAULT 0,
        Estado            INT            NOT NULL DEFAULT 0,  -- 0 Pendiente, 1 Pagada, 2 Cancelada, 3 Formalizada
        IntentosPago      INT            NOT NULL DEFAULT 0,
        MedioPago         INT            NULL,                -- 0 Efectivo, 1 Tarjeta, 2 Transferencia
        FechaPago         DATETIME       NULL,
        NumeroComprobante NVARCHAR(40)   NULL,
        FechaComprobante  DATETIME       NULL,
        IdSuscripcion     INT            NULL REFERENCES Suscripcion(IdSuscripcion),
        FechaAlta         DATETIME       NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_Contratacion_Cliente ON Contratacion(IdCliente);
    CREATE INDEX IX_Contratacion_Estado  ON Contratacion(Estado);
    PRINT 'Tabla Contratacion creada.';
END
ELSE
    PRINT 'Tabla Contratacion ya existe.';
GO

GO

-- ===================================================================
-- MODULO: 08_Patentes_Venta_Caja.sql
-- ===================================================================
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

GO

-- ===================================================================
-- MODULO: 09_Ajustes_Entrega.sql
-- ===================================================================
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

GO
