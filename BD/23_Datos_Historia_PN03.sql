/* ============================================================================
   23 - DATOS DE HISTORIA (12 meses) para el PN03 - Planificacion
   ----------------------------------------------------------------------------
   Agrega una historia de ~12 meses para que el TABLERO de planificacion y las
   4 reglas del motor de recomendaciones salgan con datos reales:
     - ~11 experiencias PASADAS (Finalizada=3) repartidas mes a mes, variadas en
       categoria/ciudad/organizador, con ocupacion alta y baja.
     - Reservas en TODOS los estados: Asistio(3), NoAsistio(4),
       Cancelada a tiempo (CancelacionATiempo=1) y Cancelada tardia (=0).
     - Calificaciones con SESGO por organizador: 'Eventos Dudosos' saca puntajes
       bajos y muchas inasistencias -> dispara "revisar organizador".
     - Lista de espera (>=6 Esperando) sobre una experiencia reciente -> dispara
       "lista de espera alta".
     - Experiencias de ocupacion < 40% -> dispara "ocupacion baja".

   Marca de demo en Experiencia.Descripcion = 'Historia PN03 (demo).' -> permite
   identificarlas y limpiarlas. IDEMPOTENTE (NOT EXISTS por nombre/reserva).
   Re-sella el DVH/DVV de Experiencia/Reserva/Organizador con el marcador
   __Reseal:<Tabla> (la app recalcula el DVH con su CalculadorDV al arrancar).
   Ejecutar DESPUES de 12_Datos_Demo y 21_PN03_Planificacion.
   ============================================================================ */
USE ExperienceHubDB;
GO
SET NOCOUNT ON;
GO

DECLARE @hoy date = CAST(GETDATE() AS date);
DECLARE @emp INT = (SELECT TOP 1 IdEmpleado FROM Empleado WHERE Legajo='L-001');

/* 0) Organizador "malo" para el sesgo de calidad (idempotente). ------------- */
IF NOT EXISTS (SELECT 1 FROM Organizador WHERE Nombre=N'Eventos Dudosos')
    INSERT INTO Organizador (Nombre, Contacto, Telefono, Mail)
    VALUES (N'Eventos Dudosos', N'R. Dudoso', '11-5555-9000', 'contacto@eventosdudosos.com');
GO

/* 1) Experiencias de historia (Finalizada=3), idempotentes por nombre. ------ */
DECLARE @h date = CAST(GETDATE() AS date);
;WITH perfil AS (
    SELECT * FROM (VALUES
        -- Nombre, Cat, Ciu, Org, OffDias(neg), Cupo, Prem
        (N'[H] Stand-up en el Club',   N'Cultural',     N'CABA',    N'Teatro Central',   -20, 10, 0),
        (N'[H] Cata de Malbec',        N'Cata',         N'Mendoza', N'Bodega Los Andes', -50, 20, 1),
        (N'[H] Escape histórico',      N'Escape Room',  N'CABA',    N'EscapeLab',        -80, 12, 0),
        (N'[H] Trekking de otoño',     N'Aire Libre',   N'Córdoba', N'Aventura Outdoor',-110, 40, 0),
        (N'[H] Foto nocturna',         N'Fotografía',   N'Rosario', N'Club de Arte',    -140, 25, 0),
        (N'[H] Cena a ciegas',         N'Gastronomía',  N'CABA',    N'Eventos Dudosos', -170, 20, 0),
        (N'[H] Taller exprés',         N'Taller',       N'CABA',    N'Eventos Dudosos', -200, 20, 0),
        (N'[H] Pintura libre',         N'Pintura',      N'CABA',    N'Club de Arte',    -230, 15, 0),
        (N'[H] Teatro de verano',      N'Teatro',       N'CABA',    N'Teatro Central',  -260, 50, 0),
        (N'[H] Cocina thai',           N'Cocina',       N'CABA',    N'Sabores Urbanos', -290, 18, 0),
        (N'[H] Ruta del vino',         N'Cata',         N'Mendoza', N'Bodega Los Andes',-320, 24, 1)
    ) AS v(Nombre, Cat, Ciu, Org, OffD, Cupo, Prem)
)
INSERT INTO Experiencia
    (Nombre, Descripcion, IdCategoria, IdCiudad, Ubicacion, Fecha, HoraInicio,
     DuracionMinutos, IdOrganizador, CupoMaximo, CupoDisponible, EdadMinima, Premium, Estado, DVH)
SELECT p.Nombre, N'Historia PN03 (demo).',
       (SELECT IdCategoria   FROM Categoria   WHERE Nombre=p.Cat),
       (SELECT IdCiudad      FROM Ciudad      WHERE Nombre=p.Ciu),
       N'Sede demo', DATEADD(day, p.OffD, @h), CONVERT(time(0), '19:00'),
       120,
       (SELECT IdOrganizador FROM Organizador WHERE Nombre=p.Org),
       p.Cupo, p.Cupo, 0, p.Prem, 3 /*Finalizada*/, 0
FROM perfil p
WHERE NOT EXISTS (SELECT 1 FROM Experiencia e WHERE e.Nombre=p.Nombre)
  AND (SELECT IdCategoria FROM Categoria WHERE Nombre=p.Cat) IS NOT NULL
  AND (SELECT IdCiudad    FROM Ciudad    WHERE Nombre=p.Ciu) IS NOT NULL;
PRINT 'Historia PN03: experiencias verificadas.';
GO

/* 2) Reservas de historia (estados + cancelaciones a tiempo/tardias) + calif.
      Perfil por experiencia: nA Asistio, nI NoAsistio, nCat Cancel a tiempo,
      nCt Cancel tardia, punt = puntaje de las calificaciones de esa experiencia. */
DECLARE @emp2 INT = (SELECT TOP 1 IdEmpleado FROM Empleado WHERE Legajo='L-001');
;WITH
nums AS (SELECT 1 i UNION ALL SELECT i+1 FROM nums WHERE i < 8),
cli AS (
    SELECT c.IdCliente, s.IdSuscripcion,
           ROW_NUMBER() OVER (ORDER BY c.IdCliente) rn
    FROM Cliente c
    CROSS APPLY (SELECT TOP 1 IdSuscripcion FROM Suscripcion s2 WHERE s2.IdCliente=c.IdCliente ORDER BY s2.FechaInicio DESC) s
    WHERE c.Activo=1
),
perfilR AS (
    SELECT * FROM (VALUES
        -- Nombre, nA, nI, nCat, nCt, punt
        (N'[H] Stand-up en el Club',  7, 1, 0, 0, 5),  -- ocupacion alta (8/10)
        (N'[H] Cata de Malbec',       5, 0, 1, 1, 5),
        (N'[H] Escape histórico',     4, 1, 0, 1, 4),
        (N'[H] Trekking de otoño',    3, 0, 0, 0, 5),  -- ocupacion baja (3/40)
        (N'[H] Foto nocturna',        2, 0, 1, 0, 4),  -- ocupacion baja (2/25)
        (N'[H] Cena a ciegas',        2, 4, 0, 0, 2),  -- organizador malo: calif 2 + inasist 66%
        (N'[H] Taller exprés',        2, 3, 1, 0, 2),  -- organizador malo
        (N'[H] Pintura libre',        5, 0, 0, 1, 5),
        (N'[H] Teatro de verano',     4, 1, 1, 1, 5),
        (N'[H] Cocina thai',          6, 0, 0, 0, 4),
        (N'[H] Ruta del vino',        5, 1, 1, 1, 5)
    ) AS v(Nombre, nA, nI, nCat, nCt, punt)
),
exp AS (
    SELECT e.IdExperiencia, e.Fecha, p.nA, p.nI, p.nCat, p.nCt, p.punt,
           (p.nA + p.nI + p.nCat + p.nCt) AS N
    FROM Experiencia e JOIN perfilR p ON p.Nombre = e.Nombre
)
INSERT INTO Reserva
    (IdCliente, IdExperiencia, IdEmpleado, Estado, FechaReserva, CantidadInvitados,
     FechaCancelacion, MotivoCancelacion, DVH, IdSuscripcion, CancelacionATiempo)
SELECT c.IdCliente, exp.IdExperiencia, @emp2,
       CASE WHEN n.i <= exp.nA THEN 3
            WHEN n.i <= exp.nA + exp.nI THEN 4
            ELSE 2 END,
       DATEADD(day, -7, CAST(exp.Fecha AS datetime)), 0,
       CASE WHEN n.i > exp.nA + exp.nI THEN DATEADD(day, -2, CAST(exp.Fecha AS datetime)) END,
       CASE WHEN n.i > exp.nA + exp.nI THEN N'Cancelación demo' END,
       0, c.IdSuscripcion,
       CASE WHEN n.i <= exp.nA + exp.nI THEN NULL
            WHEN n.i <= exp.nA + exp.nI + exp.nCat THEN 1   -- a tiempo
            ELSE 0 END                                       -- tardia
FROM exp
JOIN nums n ON n.i <= exp.N
JOIN cli  c ON c.rn = n.i
WHERE NOT EXISTS (SELECT 1 FROM Reserva r WHERE r.IdExperiencia = exp.IdExperiencia)
OPTION (MAXRECURSION 100);
PRINT 'Historia PN03: reservas verificadas.';
GO

/* 2b) Calificaciones para las reservas Asistio, con el puntaje del perfil. --- */
;WITH perfilR AS (
    SELECT * FROM (VALUES
        (N'[H] Stand-up en el Club',5),(N'[H] Cata de Malbec',5),(N'[H] Escape histórico',4),
        (N'[H] Trekking de otoño',5),(N'[H] Foto nocturna',4),(N'[H] Cena a ciegas',2),
        (N'[H] Taller exprés',2),(N'[H] Pintura libre',5),(N'[H] Teatro de verano',5),
        (N'[H] Cocina thai',4),(N'[H] Ruta del vino',5)
    ) AS v(Nombre, punt)
)
INSERT INTO Calificacion (IdReserva, IdCliente, IdExperiencia, Puntaje, Comentario, Fecha)
SELECT r.IdReserva, r.IdCliente, r.IdExperiencia, p.punt, N'Historia PN03 (demo).',
       DATEADD(day, 1, CAST(e.Fecha AS datetime))
FROM Reserva r
JOIN Experiencia e ON e.IdExperiencia = r.IdExperiencia
JOIN perfilR p ON p.Nombre = e.Nombre
WHERE r.Estado = 3
  AND NOT EXISTS (SELECT 1 FROM Calificacion c WHERE c.IdReserva = r.IdReserva);
PRINT 'Historia PN03: calificaciones verificadas.';
GO

/* 3) Lista de espera (>=6 Esperando) sobre la experiencia reciente de alta
      ocupacion -> dispara "lista de espera alta" sin marcar ocupacion baja. */
IF EXISTS (SELECT 1 FROM Experiencia WHERE Nombre=N'[H] Stand-up en el Club')
   AND NOT EXISTS (SELECT 1 FROM ListaEspera le JOIN Experiencia e ON e.IdExperiencia=le.IdExperiencia
                   WHERE e.Nombre=N'[H] Stand-up en el Club')
BEGIN
    DECLARE @idExp INT = (SELECT IdExperiencia FROM Experiencia WHERE Nombre=N'[H] Stand-up en el Club');
    ;WITH cli AS (
        SELECT c.IdCliente, ROW_NUMBER() OVER (ORDER BY c.IdCliente) rn
        FROM Cliente c WHERE c.Activo=1
    )
    INSERT INTO ListaEspera (IdExperiencia, IdCliente, Posicion, FechaIngreso, Estado, FechaOferta, MotivoRetiro)
    SELECT @idExp, c.IdCliente, c.rn, DATEADD(day, -15, GETDATE()), 0 /*Esperando*/, NULL, NULL
    FROM cli c WHERE c.rn BETWEEN 1 AND 6;
    PRINT 'Historia PN03: lista de espera cargada (6 en espera).';
END
GO

/* 4) Recalcular cupo disponible de las experiencias de historia segun reservas. */
UPDATE e SET CupoDisponible =
    CASE WHEN e.CupoMaximo - ISNULL(r.Usados,0) < 0 THEN 0 ELSE e.CupoMaximo - ISNULL(r.Usados,0) END
FROM Experiencia e
LEFT JOIN (SELECT IdExperiencia, SUM(1 + CantidadInvitados) Usados FROM Reserva WHERE Estado IN (0,1,3,4) GROUP BY IdExperiencia) r
    ON r.IdExperiencia = e.IdExperiencia
WHERE e.Descripcion = N'Historia PN03 (demo).';
GO

/* 5) Re-sellado del DV de las tablas tocadas (la app recalcula el DVH al arrancar). */
UPDATE Experiencia SET DVH = 0;
UPDATE Reserva     SET DVH = 0;
UPDATE Organizador SET DVH = 0;
DELETE FROM DVVertical WHERE NombreTabla IN ('Experiencia','Reserva','Organizador');
INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo)
SELECT v.T, 1, GETDATE() FROM (VALUES ('__Reseal:Experiencia'),('__Reseal:Reserva'),('__Reseal:Organizador')) v(T)
WHERE NOT EXISTS (SELECT 1 FROM DVVertical d WHERE d.NombreTabla = v.T);
GO

PRINT 'Historia PN03 cargada (experiencias, reservas, calificaciones, lista de espera).';
GO
