namespace BE
{
    /// <summary>
    /// Tipo de contratación (PN01). Distingue el alta de una suscripción nueva de la renovación
    /// cobrada de una existente. Ambas recorren el mismo flujo Venta → Caja → Venta (formaliza),
    /// pero al formalizar: el Alta crea una suscripción vigente; la Renovación extiende la existente.
    /// </summary>
    public enum TipoContratacion
    {
        Alta = 0,
        Renovacion = 1
    }
}
