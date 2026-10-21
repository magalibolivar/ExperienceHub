/* ============================================================================
   19 - DATOS DEMO : clientes en DISTINTAS SITUACIONES de PN01/PN02
   ----------------------------------------------------------------------------
   Suma clientes de demostración que cubren cada estado del proceso, para poder
   recorrer las pantallas sin cargar datos a mano:
     • Suscripción: Activa con cupo / Activa sin cupo / Por vencer / Vencida /
       Suspendida / Premium.
     • Sin suscripción (para contratar).
     • Contratación Pendiente de pago (0, 1 y 2 intentos fallidos) → Caja.
     • Contratación Pagada sin formalizar (pago único y en cuotas) → Para formalizar.
     • Clientes extra (volumen / lista de espera).

   Idempotente: cada cliente se inserta por DNI con WHERE NOT EXISTS; las
   suscripciones/contrataciones solo se crean si el cliente aún no las tiene.
   El DNI va en texto plano (la app lo tolera: TryDesencriptar acepta legacy).
   Cliente es tabla protegida con DV → al final se deja el marcador de re-sellado
   para que el próximo arranque recalcule DVH/DVV y la integridad quede ÍNTEGRO.

   Enums:  Suscripcion 0=Activa 1=Suspendida 2=Vencida
           Contratacion 0=Pendiente 1=Pagada 2=Cancelada 3=Formalizada
           MedioPago    0=Efectivo 1=Tarjeta 2=Transferencia
   ============================================================================ */

-- 0) Arreglo de dato roto: nombre con mojibake por encoding ('NicolÃ¡s' → 'Nicolás').
UPDATE Cliente SET Nombre = N'Nicolás'
WHERE Apellido = N'Herrera' AND Nombre LIKE N'Nicol%s' AND Nombre <> N'Nicolás';
GO

-- 1) Alta de clientes demo (por DNI, idempotente) -------------------------------
DECLARE @nuevos TABLE (DNI VARCHAR(20), Nombre NVARCHAR(100), Apellido NVARCHAR(100), IdCiudad INT, FechaNac DATE);
INSERT INTO @nuevos VALUES
 ('40100001', N'Juliana',   N'Ríos',      1, '1992-04-12'),
 ('40100002', N'Tomás',     N'Vega',      1, '1988-09-03'),
 ('40100003', N'Carla',     N'Núñez',     2, '1995-01-25'),
 ('40100004', N'Marcos',    N'Benítez',   3, '1983-11-17'),
 ('40100005', N'Paula',     N'Sosa',      4, '1990-06-30'),
 ('40100006', N'Iván',      N'Córdoba',   1, '1986-02-08'),
 ('40100011', N'Florencia', N'Díaz',      2, '1999-07-19'),
 ('40100012', N'Ramiro',    N'Acosta',    1, '1991-03-05'),
 ('40100013', N'Lucas',     N'Medina',    5, '1987-12-22'),
 ('40100014', N'Agustina',  N'Ferreyra',  3, '1996-08-14'),
 ('40100015', N'Bruno',     N'Castro',    1, '1984-05-11'),
 ('40100016', N'Daniela',   N'Ponce',     4, '1993-10-02'),
 ('40100021', N'Hernán',    N'Rivas',     2, '1989-01-09'),
 ('40100022', N'Micaela',   N'Luna',      1, '1997-04-27'),
 ('40100023', N'Gonzalo',   N'Ibarra',    5, '1985-07-07'),
 ('40100024', N'Rocío',     N'Herrera',   3, '1994-09-16'),
 ('40100025', N'Emiliano',  N'Paz',       1, '1990-12-01');

INSERT INTO Cliente (Nombre, Apellido, DNI, Email, MetodoPago, IdCiudad, FechaAlta, FechaNacimiento, Activo, DVH)
SELECT n.Nombre, n.Apellido, n.DNI,
       LOWER(n.Nombre) + '.' + LOWER(n.Apellido) + '@demo.com', 'N/A', n.IdCiudad, GETDATE(), n.FechaNac, 1, 0
FROM @nuevos n
WHERE NOT EXISTS (SELECT 1 FROM Cliente c WHERE c.DNI = n.DNI);
GO
PRINT 'Demo situaciones: clientes verificados.';
GO

-- 2) Suscripciones según situación (solo si el cliente aún no tiene ninguna) -----
DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @perMes DATE = DATEFROMPARTS(YEAR(@hoy), MONTH(@hoy), 1);
-- (DNI, IdPlan, Estado, OffsetVencDias, ConsumoMes)
DECLARE @subs TABLE (DNI VARCHAR(20), IdPlan INT, Estado INT, OffVenc INT, Consumo INT);
INSERT INTO @subs VALUES
 ('40100001', 2, 0,  60,  0),   -- Activa con cupo (Estándar, 15/mes)
 ('40100002', 2, 0,  60, 15),   -- Activa SIN cupo (consumió las 15)
 ('40100003', 2, 0,   5,  2),   -- Por vencer (5 días)
 ('40100004', 2, 2, -10,  3),   -- Vencida
 ('40100005', 2, 1,  60,  1),   -- Suspendida
 ('40100006', 3, 0,  90,  4),   -- Premium activa
 ('40100021', 1, 0,  45,  1),   -- Básico activa
 ('40100022', 1, 0,  45,  0),   -- Básico activa
 ('40100023', 2, 0,  45,  5);   -- Estándar activa

INSERT INTO Suscripcion (IdCliente, IdPlan, FechaInicio, FechaVencimiento, Estado, ReservasConsumidasMes, PeriodoConsumo)
SELECT c.IdCliente, s.IdPlan, DATEADD(day, -15, @hoy), DATEADD(day, s.OffVenc, @hoy), s.Estado, s.Consumo, @perMes
FROM @subs s
JOIN Cliente c ON c.DNI = s.DNI
WHERE NOT EXISTS (SELECT 1 FROM Suscripcion x WHERE x.IdCliente = c.IdCliente);
GO
PRINT 'Demo situaciones: suscripciones verificadas.';
GO

-- 3) Contrataciones Pendientes de pago (cola de Caja) ----------------------------
DECLARE @pend TABLE (DNI VARCHAR(20), IdPlan INT, Intentos INT, OffAltaDias INT);
INSERT INTO @pend VALUES
 ('40100012', 2, 0,  0),   -- recién creada
 ('40100013', 2, 1, -1),   -- 1 intento fallido
 ('40100014', 3, 2, -4);   -- 2 intentos (al 3º se cancela)

INSERT INTO Contratacion (IdCliente, IdPlan, Importe, Estado, IntentosPago, FechaAlta)
SELECT c.IdCliente, p.IdPlan, p.Precio, 0 /*Pendiente*/, q.Intentos, DATEADD(day, q.OffAltaDias, GETDATE())
FROM @pend q
JOIN Cliente c ON c.DNI = q.DNI
JOIN PlanSuscripcion p ON p.IdPlan = q.IdPlan
WHERE NOT EXISTS (SELECT 1 FROM Contratacion x WHERE x.IdCliente = c.IdCliente AND x.Estado IN (0,1));
GO

-- 4) Contrataciones PAGADAS sin formalizar (cola "Para formalizar") --------------
--    Bruno Castro: pago único (Efectivo). Daniela Ponce: 3 cuotas con Tarjeta (+10%).
DECLARE @pag TABLE (DNI VARCHAR(20), IdPlan INT, Medio INT, Cuotas INT, Recargo DECIMAL(9,2), OffPagoDias INT);
INSERT INTO @pag VALUES
 ('40100015', 2, 0, 1,  0, -2),    -- Efectivo, pago único
 ('40100016', 3, 1, 3, 10, -1);    -- Tarjeta, 3 cuotas, +10%

INSERT INTO Contratacion (IdCliente, IdPlan, Importe, Estado, IntentosPago, FechaAlta,
                          MedioPago, FechaPago, Cuotas, RecargoPorcentaje, ImporteTotal)
SELECT c.IdCliente, p.IdPlan, p.Precio, 1 /*Pagada*/, 0, DATEADD(day, g.OffPagoDias - 1, GETDATE()),
       g.Medio, DATEADD(day, g.OffPagoDias, GETDATE()), g.Cuotas, g.Recargo,
       ROUND(p.Precio * (1 + g.Recargo/100.0), 2)
FROM @pag g
JOIN Cliente c ON c.DNI = g.DNI
JOIN PlanSuscripcion p ON p.IdPlan = g.IdPlan
WHERE NOT EXISTS (SELECT 1 FROM Contratacion x WHERE x.IdCliente = c.IdCliente AND x.Estado IN (0,1));
GO

-- Número de comprobante para las Pagadas demo que aún no lo tengan (formato de la app).
UPDATE Contratacion
SET NumeroComprobante = 'CMP-' + RIGHT('000000' + CAST(IdContratacion AS VARCHAR(10)), 6) + '-'
                        + FORMAT(ISNULL(FechaPago, GETDATE()), 'yyyyMMddHHmmss'),
    FechaComprobante  = ISNULL(FechaPago, GETDATE())
WHERE Estado = 1 AND NumeroComprobante IS NULL;
GO
PRINT 'Demo situaciones: contrataciones (pendientes y pagadas) verificadas.';
GO

-- 5) Re-sellado de DV de Cliente (se agregaron filas → DVV quedó desactualizado) --
IF NOT EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = '__Reseal:Cliente')
    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES ('__Reseal:Cliente', 1, GETDATE());
GO
PRINT 'Demo situaciones: OK (marcador de re-sellado de Cliente dejado para el arranque).';
GO
