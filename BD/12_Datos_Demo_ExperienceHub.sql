/* ============================================================================
   12 - DATOS DEMO ExperienceHub (experiencias, suscripciones y reservas)
   ----------------------------------------------------------------------------
   Puebla la base con datos ficticios para DEMO/presentacion:
     • Beneficios de los planes (AccesoPremium / invitados / anticipacion).
     • Ciudad de los clientes + una suscripcion ACTIVA y vigente para cada uno.
     • ~18 experiencias repartidas desde hoy hasta ENERO (pasadas finalizadas,
       futuras programadas, una COMPLETA para lista de espera).
     • Reservas en varios estados (Asistio historico, Confirmada, Pendiente,
       Cancelada) vinculando clientes con experiencias de su ciudad.
     • Una lista de espera FIFO sobre la experiencia completa.

   IDEMPOTENTE: los bloques estan guardados con IF NOT EXISTS, se puede correr
   varias veces sin duplicar. Las fechas son RELATIVAS a GETDATE() (hoy), asi la
   demo siempre tiene experiencias futuras "hasta enero".

   Las tablas de negocio (Cliente/Experiencia/Reserva) se auto-reparan el DVH/DVV
   en el arranque (BLL.Configuracion.VerificarUnaTabla), por eso se insertan con
   DVH=0 y se resetea el DVV: el proximo login recalcula y re-sella sin bloquear.
   Ejecutar DESPUES de 00/05/07 (requiere el dominio completo + clientes semilla).
   ============================================================================ */
USE ExperienceHubDB;
GO
SET NOCOUNT ON;
GO

DECLARE @hoy date = CAST(GETDATE() AS date);

/* ----------------------------------------------------------------------------
   1) Beneficios de los planes (para que las experiencias premium e invitados
      sean reservables). Idempotente: fija los valores de cada plan conocido.
   ---------------------------------------------------------------------------- */
UPDATE PlanSuscripcion SET ReservasMensuales=5,  AccesoPremium=0, CantidadInvitados=0, AnticipacionMaximaDias=30, PrioridadListaEspera=0 WHERE Nombre=N'Básico';
UPDATE PlanSuscripcion SET ReservasMensuales=15, AccesoPremium=0, CantidadInvitados=1, AnticipacionMaximaDias=45, PrioridadListaEspera=0 WHERE Nombre=N'Estándar';
UPDATE PlanSuscripcion SET ReservasMensuales=30, AccesoPremium=1, CantidadInvitados=3, AnticipacionMaximaDias=60, PrioridadListaEspera=1 WHERE Nombre=N'Premium';
GO

/* ----------------------------------------------------------------------------
   2) Organizadores adicionales (idempotente) para experiencias mas variadas.
   ---------------------------------------------------------------------------- */
INSERT INTO Organizador (Nombre, Contacto, Telefono, Mail)
SELECT v.Nombre, v.Contacto, v.Telefono, v.Mail
FROM (VALUES
    (N'Sabores Urbanos', N'Paula Giménez', '11-5555-4000', 'reservas@saboresurbanos.com'),
    (N'Club de Arte',    N'Tomás Vega',    '11-5555-5000', 'info@clubdearte.com'),
    (N'Aventura Outdoor',N'Lucas Ríos',    '11-5555-6000', 'hola@aventuraoutdoor.com')
) AS v(Nombre, Contacto, Telefono, Mail)
WHERE NOT EXISTS (SELECT 1 FROM Organizador o WHERE o.Nombre = v.Nombre);
GO

/* ----------------------------------------------------------------------------
   3) Clientes nuevos para la demo (Nicolás y Valeria — para la lista de espera).
      Los 5 clientes semilla (Lucía, Martín, Sofía, Diego, Camila) ya existen.
   ---------------------------------------------------------------------------- */
INSERT INTO Cliente (Nombre, Apellido, DNI, Email, MetodoPago, IdPlan, FechaAlta, FechaNacimiento, Activo, DVH)
SELECT v.Nombre, v.Apellido, v.DNI, v.Email, v.MetodoPago,
       (SELECT TOP 1 IdPlan FROM PlanSuscripcion p WHERE p.Nombre = v.PlanNom),
       GETDATE(), v.FechaNac, 1, 0
FROM (VALUES
    (N'Nicolás', N'Herrera', '34555666', 'nicolas.herrera@mail.com', 'Tarjeta',  N'Estándar', CONVERT(date,'1993-05-20')),
    (N'Valeria', N'Soto',    '36777888', 'valeria.soto@mail.com',    'Efectivo', N'Básico',   CONVERT(date,'1996-09-12'))
) AS v(Nombre, Apellido, DNI, Email, MetodoPago, PlanNom, FechaNac)
WHERE NOT EXISTS (SELECT 1 FROM Cliente c WHERE c.Nombre = v.Nombre AND c.Apellido = v.Apellido);
GO

/* ----------------------------------------------------------------------------
   4) Ciudad de cada cliente (para respetar la regla de ciudad al reservar).
   ---------------------------------------------------------------------------- */
UPDATE c SET c.IdCiudad = ci.IdCiudad
FROM Cliente c
JOIN (VALUES
    (N'Lucía',   N'Fernández', N'CABA'),
    (N'Camila',  N'Torres',    N'CABA'),
    (N'Martín',  N'Gómez',     N'CABA'),
    (N'Nicolás', N'Herrera',   N'CABA'),
    (N'Valeria', N'Soto',      N'CABA'),
    (N'Sofía',   N'Rossi',     N'Córdoba'),
    (N'Diego',   N'Paz',       N'Rosario')
) AS v(Nombre, Apellido, Ciudad) ON v.Nombre = c.Nombre AND v.Apellido = c.Apellido
JOIN Ciudad ci ON ci.Nombre = v.Ciudad;
GO

/* ----------------------------------------------------------------------------
   5) Suscripcion ACTIVA y vigente para cada cliente (si no tiene ninguna).
      Inicio: 1ro del mes actual. Vencimiento: fin del mes +2 (futuro).
   ---------------------------------------------------------------------------- */
DECLARE @hoy2 date = CAST(GETDATE() AS date);
INSERT INTO Suscripcion (IdCliente, IdPlan, FechaInicio, FechaVencimiento, Estado, ReservasConsumidasMes)
SELECT c.IdCliente, c.IdPlan,
       DATEFROMPARTS(YEAR(@hoy2), MONTH(@hoy2), 1),
       EOMONTH(@hoy2, 2),
       0 /*Activa*/, 0
FROM Cliente c
WHERE c.IdPlan IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Suscripcion s WHERE s.IdCliente = c.IdCliente);
GO

-- 5b) Normalizar: a lo sumo UNA suscripcion ACTIVA por cliente (la mas reciente). Si por
--     cargas previas hay varias activas, las demas pasan a Vencida (no se borran, por la FK
--     que Contratacion puede tener a IdSuscripcion). Deja coherente lo que toma la app
--     (ObtenerVigentePorCliente = la mas reciente por FechaInicio).
;WITH activas AS (
    SELECT IdSuscripcion,
           ROW_NUMBER() OVER (PARTITION BY IdCliente ORDER BY FechaInicio DESC, IdSuscripcion DESC) AS rn
    FROM Suscripcion WHERE Estado = 0 /*Activa*/
)
UPDATE s SET Estado = 2 /*Vencida*/
FROM Suscripcion s
JOIN activas a ON a.IdSuscripcion = s.IdSuscripcion
WHERE a.rn > 1;
GO

/* ----------------------------------------------------------------------------
   6) EXPERIENCIAS (aditivo e idempotente por nombre: agrega las que falten sin
      tocar las que ya existan). Fechas relativas a hoy:
      offset negativo = pasada (Finalizada=3); positivo = futura (Programada=0).
   ---------------------------------------------------------------------------- */
DECLARE @h date = CAST(GETDATE() AS date);
;WITH nuevas AS (
    SELECT * FROM (VALUES
        -- Pasadas (Finalizadas = 3) -> historial de asistencia
        (N'Noche de Teatro: Clásicos', N'Selección de escenas clásicas en vivo.',      N'Teatro',      N'CABA',    N'Av. Corrientes 1234', -20, '20:30', 120, N'Teatro Central',    40, 0, 0, 3),
        (N'Escape Room: El Faro',      N'Resolvé los enigmas del viejo faro.',         N'Escape Room', N'CABA',    N'Palermo',             -16, '17:00',  60, N'EscapeLab',          6,12, 0, 3),
        (N'Cata de Vinos de Autor',    N'Degustación guiada de vinos de autor.',       N'Cata',        N'CABA',    N'Recoleta',            -12, '19:00',  90, N'Bodega Los Andes',  12,18, 0, 3),
        -- Futuras (Programadas = 0)
        (N'Taller de Pintura al Óleo', N'Iniciación a la pintura al óleo.',            N'Pintura',     N'CABA',    N'Villa Crespo',          4, '16:00', 120, N'Club de Arte',      15, 0, 0, 0),
        (N'Clase de Cocina Italiana',  N'Pastas frescas paso a paso.',                 N'Cocina',      N'CABA',    N'Caballito',             7, '18:30', 150, N'Sabores Urbanos',   12, 0, 0, 0),
        (N'Cata de Vinos Premium',     N'Etiquetas premium con sommelier.',            N'Cata',        N'CABA',    N'Puerto Madero',        12, '19:30', 120, N'Bodega Los Andes',  10,18, 1, 0),
        (N'Escape Room: La Bóveda',    N'Robá el botín antes de que suene la alarma.', N'Escape Room', N'CABA',    N'Belgrano',             15, '17:00',  60, N'EscapeLab',          6,12, 0, 0),
        (N'Trekking Cerro Mirador',    N'Caminata guiada con vistas panorámicas.',     N'Aire Libre',  N'Córdoba', N'Sierras Chicas',       18, '08:00', 300, N'Aventura Outdoor',  20,14, 0, 0),
        (N'Salida de Fotografía Urbana',N'Recorrido fotográfico por el centro.',       N'Fotografía',  N'Rosario', N'Centro',               20, '10:00', 180, N'Club de Arte',      12, 0, 0, 0),
        (N'Obra: Tango y Luna',        N'Espectáculo de tango con orquesta en vivo.',  N'Teatro',      N'CABA',    N'San Telmo',            25, '21:00', 110, N'Teatro Central',    50, 0, 0, 0),
        (N'Degustación Gastronómica',  N'Menú de pasos con maridaje.',                 N'Gastronomía', N'Mendoza', N'Chacras de Coria',     30, '20:00', 120, N'Sabores Urbanos',   16,18, 1, 0),
        (N'Taller de Cerámica',        N'Modelado y esmaltado de piezas.',             N'Taller',      N'CABA',    N'Chacarita',            40, '15:00', 120, N'Club de Arte',      10, 0, 0, 0),
        (N'Ruta del Vino Premium',     N'Visita a bodegas con cata exclusiva.',        N'Cata',        N'Mendoza', N'Luján de Cuyo',        55, '11:00', 240, N'Bodega Los Andes',  14,18, 1, 0),
        (N'Escape Room Navideño',      N'Edición especial de fin de año.',             N'Escape Room', N'CABA',    N'Núñez',                72, '18:00',  60, N'EscapeLab',          6,10, 0, 0),
        (N'Concierto de Fin de Año',   N'Gran concierto para despedir el año.',        N'Cultural',    N'CABA',    N'Teatro Colón',         86, '21:30', 150, N'Teatro Central',    80, 0, 0, 0),
        (N'Clase de Cocina de Verano', N'Platos frescos y livianos para el verano.',   N'Cocina',      N'Rosario', N'Pichincha',           100, '19:00', 150, N'Sabores Urbanos',   12, 0, 0, 0),
        (N'Taller de Acuarela de Verano',N'Técnicas de acuarela al aire libre.',       N'Pintura',     N'CABA',    N'Parque Centenario',   110, '16:00', 120, N'Club de Arte',      15, 0, 0, 0),
        -- Experiencia COMPLETA (cupo chico) -> para demostrar la lista de espera
        (N'Masterclass de Alta Cocina',N'Clase magistral con chef invitado (cupo limitado).', N'Cocina', N'CABA', N'Palermo Hollywood',    22, '20:00', 180, N'Sabores Urbanos',    3, 0, 0, 0)
    ) AS v(Nombre, Descripcion, Cat, Ciu, Ubic, OffDias, Hora, Dur, Org, Cupo, Edad, Prem, Estado)
)
INSERT INTO Experiencia
    (Nombre, Descripcion, IdCategoria, IdCiudad, Ubicacion, Fecha, HoraInicio,
     DuracionMinutos, IdOrganizador, CupoMaximo, CupoDisponible, EdadMinima, Premium, Estado, DVH)
SELECT v.Nombre, v.Descripcion,
       (SELECT IdCategoria   FROM Categoria    WHERE Nombre = v.Cat),
       (SELECT IdCiudad      FROM Ciudad       WHERE Nombre = v.Ciu),
       v.Ubic, DATEADD(day, v.OffDias, @h), CONVERT(time(0), v.Hora),
       v.Dur,
       (SELECT IdOrganizador FROM Organizador  WHERE Nombre = v.Org),
       v.Cupo, v.Cupo, v.Edad, v.Prem, v.Estado, 0
FROM nuevas v
WHERE NOT EXISTS (SELECT 1 FROM Experiencia e WHERE e.Nombre = v.Nombre);
PRINT 'Demo: experiencias verificadas.';
GO

/* ----------------------------------------------------------------------------
   7) RESERVAS (aditivo e idempotente por cliente+experiencia). Vinculan
      cliente<->experiencia. Estado: 0 Pendiente, 1 Confirmada, 2 Cancelada, 3 Asistio.
      El empleado (vendedor) se toma del semilla L-001 si existe.
   ---------------------------------------------------------------------------- */
DECLARE @emp INT = (SELECT TOP 1 IdEmpleado FROM Empleado WHERE Legajo = 'L-001');
;WITH reservas AS (
    SELECT * FROM (VALUES
        -- Historial (Asistio = 3) sobre experiencias pasadas
        (N'Lucía',  N'Fernández', N'Noche de Teatro: Clásicos', 3, 1, -25, NULL),
        (N'Martín', N'Gómez',     N'Noche de Teatro: Clásicos', 3, 0, -25, NULL),
        (N'Camila', N'Torres',    N'Escape Room: El Faro',      3, 0, -18, NULL),
        (N'Lucía',  N'Fernández', N'Cata de Vinos de Autor',    3, 0, -14, NULL),
        (N'Camila', N'Torres',    N'Cata de Vinos de Autor',    3, 1, -14, NULL),
        -- Futuras confirmadas (1)
        (N'Lucía',  N'Fernández', N'Cata de Vinos Premium',     1, 1,  -3, NULL),
        (N'Camila', N'Torres',    N'Cata de Vinos Premium',     1, 2,  -3, NULL),
        (N'Lucía',  N'Fernández', N'Taller de Pintura al Óleo', 1, 0,  -4, NULL),
        (N'Martín', N'Gómez',     N'Taller de Pintura al Óleo', 1, 1,  -4, NULL),
        (N'Camila', N'Torres',    N'Clase de Cocina Italiana',  1, 0,  -5, NULL),
        (N'Martín', N'Gómez',     N'Escape Room: La Bóveda',    1, 0,  -6, NULL),
        (N'Sofía',  N'Rossi',     N'Trekking Cerro Mirador',    1, 0,  -2, NULL),
        (N'Diego',  N'Paz',       N'Salida de Fotografía Urbana',1,1,  -2, NULL),
        (N'Diego',  N'Paz',       N'Clase de Cocina de Verano', 1, 0,  -1, NULL),
        -- Pendiente (0)
        (N'Nicolás',N'Herrera',   N'Obra: Tango y Luna',        0, 0,  -1, NULL),
        -- Cancelada (2) con motivo
        (N'Valeria',N'Soto',      N'Taller de Cerámica',        2, 0,  -6, N'El cliente no puede asistir por viaje.'),
        -- Experiencia COMPLETA: 3 reservas llenan el cupo (CupoMaximo = 3)
        (N'Lucía',  N'Fernández', N'Masterclass de Alta Cocina',1, 0,  -5, NULL),
        (N'Camila', N'Torres',    N'Masterclass de Alta Cocina',1, 0,  -5, NULL),
        (N'Martín', N'Gómez',     N'Masterclass de Alta Cocina',1, 0,  -5, NULL)
    ) AS v(Nombre, Apellido, ExpNombre, Estado, Inv, ROff, Motivo)
)
INSERT INTO Reserva
    (IdCliente, IdExperiencia, IdEmpleado, Estado, FechaReserva, CantidadInvitados, FechaCancelacion, MotivoCancelacion, DVH)
SELECT cl.IdCliente, ex.IdExperiencia, @emp, v.Estado,
       DATEADD(day, v.ROff, GETDATE()), v.Inv,
       CASE WHEN v.Estado = 2 THEN DATEADD(day, v.ROff + 1, GETDATE()) END,
       CASE WHEN v.Estado = 2 THEN v.Motivo END,
       0
FROM reservas v
JOIN Cliente     cl ON cl.Nombre = v.Nombre AND cl.Apellido = v.Apellido
JOIN Experiencia ex ON ex.Nombre = v.ExpNombre
WHERE NOT EXISTS (SELECT 1 FROM Reserva r WHERE r.IdCliente = cl.IdCliente AND r.IdExperiencia = ex.IdExperiencia);
PRINT 'Demo: reservas verificadas.';
GO

/* ----------------------------------------------------------------------------
   8) Recalcular el cupo disponible de cada experiencia segun sus reservas NO
      canceladas (1 lugar + invitados por reserva), y marcar Completa las que
      quedaron sin cupo (futuras). Mantiene coherente lo que ve la UI.
   ---------------------------------------------------------------------------- */
UPDATE e SET CupoDisponible =
    CASE WHEN e.CupoMaximo - ISNULL(r.Usados, 0) < 0 THEN 0
         ELSE e.CupoMaximo - ISNULL(r.Usados, 0) END
FROM Experiencia e
LEFT JOIN (
    SELECT IdExperiencia, SUM(1 + CantidadInvitados) AS Usados
    FROM Reserva WHERE Estado IN (0,1,3,4)   -- no cuentan las canceladas (2)
    GROUP BY IdExperiencia
) r ON r.IdExperiencia = e.IdExperiencia;
GO

-- Futuras sin cupo -> Completa (candidatas a lista de espera).
UPDATE Experiencia SET Estado = 1 /*Completa*/
WHERE Estado = 0 /*Programada*/ AND CupoDisponible <= 0
  AND (CAST(Fecha AS datetime) + CAST(HoraInicio AS datetime)) > GETDATE();
GO

/* ----------------------------------------------------------------------------
   9) LISTA DE ESPERA sobre la experiencia completa (FIFO). Solo si no hay cola.
   ---------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM Experiencia WHERE Nombre = N'Masterclass de Alta Cocina' AND CupoDisponible <= 0)
   AND NOT EXISTS (SELECT 1 FROM ListaEspera le
                   JOIN Experiencia e ON e.IdExperiencia = le.IdExperiencia
                   WHERE e.Nombre = N'Masterclass de Alta Cocina')
BEGIN
    DECLARE @idExpLE INT = (SELECT IdExperiencia FROM Experiencia WHERE Nombre = N'Masterclass de Alta Cocina');
    INSERT INTO ListaEspera (IdExperiencia, IdCliente, Posicion, FechaIngreso, Estado, FechaOferta)
    SELECT @idExpLE, cl.IdCliente, v.Pos, DATEADD(day, v.OffDias, GETDATE()), 0 /*Esperando*/, NULL
    FROM (VALUES
        (N'Nicolás', N'Herrera', 1, -4),
        (N'Valeria', N'Soto',    2, -3)
    ) AS v(Nombre, Apellido, Pos, OffDias)
    JOIN Cliente cl ON cl.Nombre = v.Nombre AND cl.Apellido = v.Apellido;
    PRINT 'Demo: lista de espera cargada.';
END
GO

/* ----------------------------------------------------------------------------
   10) ReservasConsumidasMes por suscripcion = reservas NO canceladas del cliente
       cuya experiencia cae en el mes actual (para que "X de N este mes" sea real).
   ---------------------------------------------------------------------------- */
UPDATE s SET ReservasConsumidasMes = ISNULL(x.Consumidas, 0)
FROM Suscripcion s
LEFT JOIN (
    SELECT r.IdCliente, COUNT(*) AS Consumidas
    FROM Reserva r
    JOIN Experiencia e ON e.IdExperiencia = r.IdExperiencia
    WHERE r.Estado IN (0,1,3,4)
      AND YEAR(e.Fecha) = YEAR(GETDATE()) AND MONTH(e.Fecha) = MONTH(GETDATE())
    GROUP BY r.IdCliente
) x ON x.IdCliente = s.IdCliente;
GO

-- 10b) Sellar el PeriodoConsumo al mes actual en las suscripciones que no lo tengan, para que la
--      tarea de arranque (ciclo de vida) NO reinicie el consumo recien calculado.
UPDATE Suscripcion SET PeriodoConsumo = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
WHERE PeriodoConsumo IS NULL;
GO

/* ----------------------------------------------------------------------------
   11) Forzar el recalculo de integridad (DVH/DVV) en el proximo arranque para
       las tablas de negocio tocadas (se auto-reparan sin bloquear el login).
   ---------------------------------------------------------------------------- */
UPDATE Experiencia SET DVH = 0;
UPDATE Reserva     SET DVH = 0;
UPDATE Cliente     SET DVH = 0;
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Experiencia') UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Experiencia';
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Reserva')     UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Reserva';
IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = 'Cliente')     UPDATE DVVertical SET DVV = 0 WHERE NombreTabla = 'Cliente';
GO

PRINT 'ExperienceHub — datos demo cargados (experiencias, suscripciones, reservas, lista de espera).';
GO
