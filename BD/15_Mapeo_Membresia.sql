/* ============================================================================
   15 — Mapeo de controles de la pantalla "Membresía del cliente" (MembresiaForm)
   ----------------------------------------------------------------------------
   La comercialización y la gestión de la suscripción se unificaron en un solo
   formulario centrado en el cliente. La separación de roles del PN la hace el
   modelo de permisos: cada sección del form se mapea a su patente vía
   [ControlMapeado], y ManejadorSeguridad oculta la que el usuario no tenga.

     • panelVenta  → mnuContratacionVenta  (Agente: registrar/formalizar/renovar/suspender)
     • panelCaja   → mnuContratacionCaja   (Caja: cobrar y emitir comprobante)

   El Administrador ve TODO por bypass (no depende de este mapeo).
   Idempotente: no duplica filas si ya existen (UQ Formulario+NombreControl).
   ============================================================================ */

INSERT INTO ControlMapeado (IdPermiso, Formulario, NombreControl)
SELECT p.IdPermiso, v.Formulario, v.NombreControl
FROM (VALUES
    ('mnuContratacionVenta', 'MembresiaForm', 'panelVenta'),
    ('mnuContratacionCaja',  'MembresiaForm', 'panelCaja')
) AS v(NombreMenu, Formulario, NombreControl)
INNER JOIN Permiso p ON p.NombreMenu = v.NombreMenu
                    AND ISNULL(p.EsFamilia,0) = 0 AND ISNULL(p.EsRol,0) = 0
WHERE NOT EXISTS (SELECT 1 FROM ControlMapeado x
                  WHERE x.Formulario = v.Formulario AND x.NombreControl = v.NombreControl);

PRINT 'Mapeo de MembresiaForm (panelVenta/panelCaja) aplicado.';
GO
