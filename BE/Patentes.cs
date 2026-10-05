namespace BE
{
    /// <summary>
    /// Nombres de las patentes (permisos simples) del sistema, tal como figuran en
    /// la columna [Permiso].NombreMenu. Centraliza los literales usados por los guards
    /// de la BLL y por el menú, para evitar errores de tipeo.
    /// </summary>
    public static class Patentes
    {
        public const string Usuarios           = "mnuUsuarios";
        public const string Auditoria          = "mnuAuditoria";
        public const string Experiencias       = "mnuExperiencias";
        public const string Reservas           = "mnuReservas";
        public const string ReservasRealizadas = "mnuReservasRealizadas";
        public const string Clientes           = "mnuClientes";
        public const string PlanSuscripciones  = "mnuPlanSuscripciones";
        public const string Organizadores      = "mnuOrganizadores";
        public const string Categorias         = "mnuCategorias";
        public const string Ciudades           = "mnuCiudades";
        public const string ListaEspera        = "mnuListaEspera";

        // ── PN01 Comercialización — separación de funciones Venta / Caja ─────────────
        // Dos patentes distintas para que el backend distinga quién crea/formaliza la
        // contratación (Venta) de quién la cobra/cancela por intentos (Caja), en vez de
        // gobernarlo todo con la patente genérica de Clientes.
        public const string ContratacionVenta = "mnuContratacionVenta";
        public const string ContratacionCaja  = "mnuContratacionCaja";

        // ── Patentes de ACCIÓN granular ("Configurar") — separan VER de EDITAR ───────
        // Cada una gobierna las operaciones de escritura (alta/modificación/baja) del módulo.
        // Convención: <patente de ver> + "Editar". Si la patente no existe en el catálogo,
        // BLL.PermisosAccion cae al permiso de VER (retrocompatibilidad — ver PermisosAccion).
        public const string ExperienciasEditar       = "mnuExperienciasEditar";
        public const string ClientesEditar           = "mnuClientesEditar";
        public const string PlanSuscripcionesEditar  = "mnuPlanSuscripcionesEditar";
        public const string ReservasEditar           = "mnuReservasEditar";
        public const string ReservasRealizadasEditar = "mnuReservasRealizadasEditar";
        public const string OrganizadoresEditar      = "mnuOrganizadoresEditar";
        public const string CategoriasEditar         = "mnuCategoriasEditar";
        public const string CiudadesEditar           = "mnuCiudadesEditar";
    }
}
