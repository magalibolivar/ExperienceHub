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
        Cuotas            INT            NULL,                -- cuotas elegidas al cobrar (solo Tarjeta financia; 1 = pago unico)
        RecargoPorcentaje DECIMAL(5, 2)  NULL,                -- recargo por financiacion sellado al cobrar (ej. 20.00 = 20%)
        ImporteTotal      DECIMAL(10, 2) NULL,                -- total financiado cobrado (Importe + recargo)
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
