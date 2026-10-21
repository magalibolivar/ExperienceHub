namespace GUI
{
    /// <summary>
    /// Helper de presentación para resolver visibilidad por PERMISO real (no por "es admin"):
    /// delega en SessionManager.TienePermiso, que ya aplica el bypass del Administrador. Lo usan
    /// los atajos entre formularios de PN01 para mostrarse solo si el usuario tiene la patente del
    /// paso siguiente (un Agente puro no ve "Ir a cobrar"; un Cajero puro no ve "Ir a formalizar").
    /// </summary>
    internal static class PermisosGui
    {
        public static bool Permite(string patente)
            => Seguridad.SessionManager.IsLoggedIn
               && Seguridad.SessionManager.GetInstance().TienePermiso(patente);
    }
}
