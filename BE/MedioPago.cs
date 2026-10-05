namespace BE
{
    /// <summary>Medio de pago con el que Caja cobra una contratación (PN01 — CU01-CAJ).</summary>
    public enum MedioPago
    {
        Efectivo = 0,
        Tarjeta = 1,
        Transferencia = 2
    }
}
