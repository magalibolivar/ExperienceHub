namespace BE
{
    /// <summary>PN03 — Estado del plan de acción (ciclo Borrador → Aprobado → En ejecución → Cerrado).</summary>
    public enum EstadoPlanAccion { Borrador = 0, Aprobado = 1, EnEjecucion = 2, Cerrado = 3 }

    /// <summary>PN03 — Tipo de recomendación que emite el motor por reglas.</summary>
    public enum TipoRecomendacion
    {
        ListaEsperaAlta = 0,   // demanda insatisfecha alta → abrir edición / ampliar cupo
        OcupacionBaja   = 1,   // poca demanda → revisar o dar de baja
        OrganizadorRevisar = 2,// calificación baja o inasistencias altas → revisar organizador
        PlanSubutilizado = 3   // plan con uso bajo → revisar beneficios
    }

    /// <summary>PN03 — Acción concreta sugerida para cada recomendación (la ejecuta el Coordinador/Admin).</summary>
    public enum AccionSugerida
    {
        ProgramarEdicion = 0,    // copia la experiencia con nueva fecha (BLL.Experiencia.ProgramarEdicion)
        AmpliarCupo = 1,
        CancelarExperiencia = 2, // reutiliza la cascada existente
        NoReprogramar = 3,
        SuspenderOrganizador = 4,
        RevisarPlan = 5          // tarea para el Administrador
    }

    /// <summary>PN03 — Decisión del Gerente sobre una recomendación.</summary>
    public enum DecisionRecomendacion { Pendiente = 0, Aprobada = 1, Descartada = 2 }

    /// <summary>PN03 — Momento del snapshot de KPIs (comparativo al cerrar el plan).</summary>
    public enum MomentoKpi { Antes = 0, Despues = 1 }
}
