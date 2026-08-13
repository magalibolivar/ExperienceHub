-- Terminología ExperienceHub en traducciones aún usadas por forms vigentes
-- (ReporteJornada, Planes, Bitácora): Prendas→Experiencias, Pedido→Reserva.
USE ExperienceHubDB;
SET NOCOUNT ON;
BEGIN TRAN;

DECLARE @m TABLE (Clave NVARCHAR(100), Cod CHAR(2), Texto NVARCHAR(400));
INSERT INTO @m VALUES
 ('rpt.kpi.prendas','ES','Experiencias disponibles'),
 ('rpt.kpi.prendas','EN','Available experiences'),
 ('rpt.kpi.prendas','RU','Доступные впечатления'),
 ('rpt.kpi.prendas','PT','Experiências disponíveis'),
 ('rpt.txt.prendas','ES','Experiencias disponibles'),
 ('rpt.txt.prendas','EN','Available experiences'),
 ('rpt.txt.prendas','RU','Доступные впечатления'),
 ('rpt.txt.prendas','PT','Experiências disponíveis'),
 ('lbl.limiteprendas','ES','Reservas mensuales *'),
 ('lbl.limiteprendas','EN','Monthly bookings *'),
 ('lbl.limiteprendas','RU','Брони в месяц *'),
 ('lbl.limiteprendas','PT','Reservas mensais *'),
 ('lbl.idpedido','ES','ID Reserva:'),
 ('lbl.idpedido','EN','Booking ID:'),
 ('lbl.idpedido','RU','ID брони:'),
 ('lbl.idpedido','PT','ID Reserva:'),
 ('col.neg.idpedido','ES','Id Reserva'),
 ('col.neg.idpedido','EN','Booking Id'),
 ('col.neg.idpedido','RU','Id брони'),
 ('col.neg.idpedido','PT','Id Reserva'),
 ('col.neg.idprenda','ES','Id Experiencia'),
 ('col.neg.idprenda','EN','Experience Id'),
 ('col.neg.idprenda','RU','Id впечатления'),
 ('col.neg.idprenda','PT','Id Experiência');

UPDATE t SET t.Texto = m.Texto
FROM Traduccion t
JOIN Control c ON c.IdControl = t.IdControl
JOIN Idioma  i ON i.IdIdioma  = t.IdIdioma
JOIN @m m ON m.Clave = c.Clave AND m.Cod = i.Codigo;

COMMIT;

SELECT c.Clave, i.Codigo, t.Texto
FROM Control c JOIN Traduccion t ON t.IdControl=c.IdControl JOIN Idioma i ON i.IdIdioma=t.IdIdioma
WHERE c.Clave IN ('rpt.kpi.prendas','lbl.limiteprendas','lbl.idpedido','col.neg.idprenda')
ORDER BY c.Clave, i.IdIdioma;
