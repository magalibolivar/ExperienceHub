/* ============================================================================
   20 - PN01 : Renovación cobrada (Contratacion.Tipo + IdSuscripcionRenovada)
   ----------------------------------------------------------------------------
   Agrega a [Contratacion] la distinción Alta / Renovación:
     • Tipo                  0 = Alta (default; deja intactas las contrataciones previas),
                             1 = Renovación.
     • IdSuscripcionRenovada FK a Suscripcion: qué suscripción extiende la renovación
                             (NULL en las altas).
   La renovación reutiliza TODO el flujo de PN01 (misma bandeja de Cobros, mismas reglas
   de 3 intentos → Cancelada). Al formalizarla NO se crea una suscripción nueva: se
   extiende la existente un mes (lo resuelve la BLL/DAL).

   Contratacion NO es tabla protegida por dígito verificador, así que agregar columnas
   no requiere recalcular DVH/DVV. Idempotente (COL_LENGTH). En instalación limpia la
   tabla ya nace con estas columnas (07/00); acá el ALTER queda como no-op.
   ============================================================================ */
IF COL_LENGTH('Contratacion','Tipo') IS NULL
    ALTER TABLE Contratacion ADD Tipo INT NOT NULL DEFAULT 0;   -- 0 Alta, 1 Renovacion
GO
IF COL_LENGTH('Contratacion','IdSuscripcionRenovada') IS NULL
    ALTER TABLE Contratacion ADD IdSuscripcionRenovada INT NULL
        REFERENCES Suscripcion(IdSuscripcion);
GO
PRINT 'PN01: Contratacion.Tipo / IdSuscripcionRenovada verificadas (renovación cobrada).';
GO
