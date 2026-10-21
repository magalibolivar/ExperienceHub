/* ============================================================================
   15 — (RETIRADO) Mapeo de controles de "Membresía del cliente" (MembresiaForm)
   ----------------------------------------------------------------------------
   MembresiaForm fue eliminado del sistema. La comercialización se opera ahora por
   NuevaContratacionForm / CobrosPendientesForm / FormalizacionForm, cada una gateada
   por su patente (mnuContratacionVenta / mnuContratacionCaja) sin necesidad de
   ControlMapeado de paneles.

   Este script se deja como NO-OP para preservar el historial de módulos. Además,
   limpia de forma idempotente cualquier mapeo residual de MembresiaForm que pudiera
   haber quedado en bases ya instaladas (no toca ningún otro formulario).
   ============================================================================ */

DELETE FROM ControlMapeado WHERE Formulario = 'MembresiaForm';
PRINT 'MembresiaForm retirado: mapeos residuales eliminados (si los habia).';
GO
