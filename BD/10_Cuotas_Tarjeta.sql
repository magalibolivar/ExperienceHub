/* ============================================================================
   10 - PN01 Cuotas en tarjeta : columnas de financiacion en Contratacion
   ----------------------------------------------------------------------------
   Agrega el soporte de pago en cuotas (solo Tarjeta financia). Los valores se
   SELLAN al cobrar y no se recalculan:
     • Cuotas            cantidad de cuotas elegidas (1 = pago unico)
     • RecargoPorcentaje recargo por financiacion aplicado (ej. 20.00 = 20%)
     • ImporteTotal      total financiado cobrado (Importe base + recargo)
   Migracion idempotente para bases ya instaladas (en instalaciones nuevas las
   columnas ya nacen en 07_Contratacion_PN01.sql). Ejecutar DESPUES del 07.
   ============================================================================ */
IF COL_LENGTH('Contratacion','Cuotas') IS NULL
    ALTER TABLE Contratacion ADD Cuotas INT NULL;
GO
IF COL_LENGTH('Contratacion','RecargoPorcentaje') IS NULL
    ALTER TABLE Contratacion ADD RecargoPorcentaje DECIMAL(5, 2) NULL;
GO
IF COL_LENGTH('Contratacion','ImporteTotal') IS NULL
    ALTER TABLE Contratacion ADD ImporteTotal DECIMAL(10, 2) NULL;
GO
PRINT 'Contratacion: columnas de cuotas (Cuotas, RecargoPorcentaje, ImporteTotal) verificadas.';
GO
