# Propuesta de Proyecto — Negocio

**UNIVERSIDAD ABIERTA INTERAMERICANA — Facultad de Tecnología Informática**

| | |
|---|---|
| **Materia** | Trabajo de Campo I / Trabajo de Diploma |
| **Sistema** | ExperienceHub |
| **Documento** | Propuesta de Proyecto (Negocio) |
| **Versión** | 1.0 |
| **Alumnos** | Bolívar · Morana |
| **Localización / Comisión / Turno** | Sede Centro · 3.º A · Turno Noche |
| **Año de cursada** | 2026 |
| **Docente** | *(a completar)* |

> Este documento constituye la **base de negocio** del proyecto. Sobre él se construyen las entregas
> posteriores de la Carpeta de Proyecto (Documento Visión completo, Especificaciones de Casos de Uso,
> Modelo Conceptual, DER Global, Diagrama de Clases, Casos de Prueba y manuales). No incluye todavía
> el detalle técnico de implementación, que se documenta en los artefactos siguientes.

---

## Historial de Revisión

| Fecha | Versión | Autor | Descripción |
|-------|---------|-------|-------------|
| 2026-08-10 | 1.0 | Bolívar · Morana | Versión inicial de la propuesta de negocio. Propósito, alcance, roles, gestiones y procesos de negocio. |

---

## 1. Descripción Global del Producto

### 1.1 Propósito

En la actualidad, quien quiere disfrutar de experiencias presenciales de ocio y cultura —talleres,
catas, teatro, escape rooms, gastronomía, actividades al aire libre— debe buscar cada actividad por
separado, comparar proveedores dispersos, pagar de forma individual y no cuenta con un criterio unificado
para descubrir propuestas afines a sus gustos. Del lado de la organización que intermedia entre clientes
y proveedores, la gestión manual de cupos, reservas, listas de espera, asistencias y cobros por
suscripción es propensa a errores, sobreventa de cupos y pérdida de trazabilidad.

**ExperienceHub** nace para resolver ese problema. Es una plataforma de escritorio que permite a una
organización comercializar el **acceso a experiencias presenciales bajo un modelo de suscripción
mensual**: el cliente contrata un plan que le otorga una cantidad de reservas por mes, y el personal
interno de la organización administra el catálogo de experiencias, gestiona las reservas de los clientes,
controla los cupos y las listas de espera, registra las asistencias y recopila las calificaciones.

El sistema se propone **mejorar**:

- La **experiencia del cliente**: descubrimiento de actividades afines a sus intereses y reserva ágil
  dentro de los límites de su plan.
- La **eficiencia operativa** de la organización: control automático de cupos, listas de espera FIFO,
  validación de límites del plan y trazabilidad completa de cada operación.
- La **integridad y seguridad** de la información: control de acceso por perfiles, auditoría de todas
  las operaciones, verificación de integridad de datos y resguardo mediante copias de seguridad.

### 1.2 Descripción funcional del producto y Alcance

A nivel global, el sistema de información abarca las siguientes **gestiones organizacionales**:

| Gestión | Descripción (a nivel de gestión, sin llegar al proceso) |
|---------|----------------------------------------------------------|
| **Gestión de Clientes** | Alta, modificación y baja de clientes suscriptores, con sus datos de contacto, ciudad e intereses declarados. |
| **Gestión de Planes de Suscripción** | Definición de los planes comerciales: reservas mensuales incluidas, anticipación máxima de reserva, acceso a experiencias premium, prioridad en lista de espera y cantidad de invitados permitidos. |
| **Gestión de Suscripciones** | Vinculación de un cliente con un plan, con su período de vigencia, estado y consumo de reservas del mes; renovación y suspensión. |
| **Gestión del Catálogo de Experiencias** | Alta y mantenimiento de las experiencias ofrecidas (categoría, ciudad, organizador, fecha/hora, duración, cupo, edad mínima, carácter premium) y su ciclo de vida. |
| **Gestión de Organizadores** | Registro de los proveedores que producen las experiencias, con sus datos de contacto. |
| **Gestión de Reservas** | Toma de reservas de clientes sobre experiencias, respetando el límite del plan y la disponibilidad de cupo. |
| **Gestión de Listas de Espera** | Administración FIFO de la demanda excedente cuando una experiencia está completa, con promoción automática al liberarse cupo. |
| **Gestión de Asistencias y Calificaciones** | Registro de la asistencia efectiva del cliente y recolección de su calificación posterior a la experiencia. |
| **Gestión de Recomendaciones** | Sugerencia de experiencias afines según los intereses y el historial del cliente. |
| **Gestión de Catálogos de Referencia** | Mantenimiento de ciudades, categorías e intereses que estructuran el resto del dominio. |
| **Gestión de Seguridad y Administración** | Perfiles y permisos, usuarios internos, login/logout, bitácora, multiidioma, copias de seguridad, dígitos verificadores y encriptado. |

**Alcance incluido:**

- Todo el ciclo comercial del modelo de suscripción: cliente → plan → suscripción → reserva → asistencia → calificación.
- Control automático de cupos, límite mensual de reservas del plan y listas de espera.
- Recomendación de experiencias por afinidad de intereses e historial.
- Operación por personal interno con perfiles de acceso diferenciados.
- Servicios técnicos transversales (seguridad, auditoría, integridad, idioma, backup) exigidos por la cátedra.

**Alcance excluido (fuera de esta versión):**

- Cobro y conciliación de pagos con pasarelas o medios electrónicos externos (la suscripción se
  administra a nivel de estado y vigencia, no de transacción financiera).
- Autogestión del cliente final vía web o app móvil: en esta versión el cliente **no** opera el sistema;
  las reservas las realiza el personal interno en su nombre.
- Notificaciones automáticas por correo electrónico o mensajería a los clientes.
- Facturación electrónica y reportes contables.

### 1.3 Definiciones, Acrónimos y Abreviaturas

**Definiciones:**

- **Cliente:** Persona física suscriptora de la plataforma que accede a las experiencias según su plan.
- **Experiencia:** Actividad presencial ofrecida en el catálogo, producida por un organizador, con fecha,
  cupo y ubicación determinados (ej.: una cata de vinos, un taller de cerámica, una obra de teatro).
- **Organizador:** Proveedor externo que produce y ejecuta una experiencia.
- **Plan de Suscripción:** Paquete comercial que define cuántas reservas mensuales puede realizar el
  cliente y qué beneficios obtiene (acceso premium, prioridad en lista de espera, invitados).
- **Suscripción:** Vínculo activo entre un cliente y un plan durante un período, que registra el consumo
  de reservas del mes en curso.
- **Reserva:** Compromiso de asistencia de un cliente a una experiencia puntual, tomado dentro del límite
  de su plan y sujeto a la disponibilidad de cupo.
- **Lista de Espera:** Cola ordenada (FIFO) de clientes que desean una experiencia sin cupo disponible;
  al liberarse un lugar, se promueve al primero de la cola.
- **Cupo:** Cantidad máxima de asistentes admitidos en una experiencia.
- **Calificación:** Valoración (1 a 5) y comentario que el cliente registra luego de asistir.
- **Interés:** Etiqueta temática declarada por el cliente (ej.: Vinos, Teatro, Cocina) usada para recomendar.
- **Bitácora:** Registro cronológico de las operaciones realizadas en el sistema (auditoría).
- **Perfil / Rol:** Conjunto de permisos que determina qué puede hacer un usuario interno.
- **Patente / Permiso:** Autorización atómica (o compuesta) sobre una funcionalidad del sistema.

**Acrónimos:**

- **UAI:** Universidad Abierta Interamericana.
- **ABM:** Alta, Baja y Modificación.
- **CU:** Caso de Uso.
- **RF:** Requerimiento Funcional.
- **DER:** Diagrama de Entidad-Relación.
- **DV / DVH / DVV:** Dígito Verificador / Horizontal / Vertical.
- **FIFO:** First In, First Out (primero en entrar, primero en salir).

**Abreviaturas:**

- **ARG** = Argentina.
- **BD** = Base de Datos.
- **UI / GUI** = Interfaz Gráfica de Usuario.

---

## 2. Participantes en el desarrollo y Usuarios (Roles)

### 2.1 Participantes (interesados / involucrados)

| Nombre | Descripción | Responsabilidad |
|--------|-------------|-----------------|
| Bolívar | Alumno desarrollador | Análisis, diseño, desarrollo y documentación del sistema. |
| Morana | Alumno desarrollador | Análisis, diseño, desarrollo y documentación del sistema. |
| Docente de la cátedra | Cliente / evaluador del proyecto | Define pautas, valida artefactos y aprueba las entregas. |
| Gerente Comercial (rol de negocio) | Referente del negocio de suscripciones | Define planes, beneficios y reglas comerciales. |
| Coordinador de Experiencias (rol de negocio) | Responsable del catálogo | Define qué experiencias se ofrecen y negocia con organizadores. |

### 2.2 Usuarios del sistema (Roles funcionales)

Los siguientes roles se establecen como referencia inicial de granularidad. El sistema permite la
**asignación dinámica de roles y permisos**, por lo que estos roles son configurables (crear, renombrar,
eliminar y componer roles-en-rol) sin recompilar.

| Rol | Acceso / Responsabilidad principal |
|-----|-------------------------------------|
| **Administrador** | Acceso total: seguridad, usuarios, perfiles y permisos, bitácora, backup, dígitos verificadores y todos los módulos de negocio. |
| **Agente de Reservas** | Gestión de clientes, toma de reservas, listas de espera y consulta de planes. Es quien opera el mostrador comercial. |
| **Coordinador de Experiencias** | Mantenimiento del catálogo de experiencias, organizadores, categorías y ciudades. |
| **Auditor** | Acceso de solo lectura a la bitácora y a la auditoría del sistema. |

> Los permisos se resuelven de forma recursiva desde un árbol de permisos (patrón Composite): un permiso
> puede ser **atómico** (una funcionalidad, p. ej. *Ver Reservas*, *Gestionar Reservas*) o **compuesto**
> (un rol que agrupa permisos u otros roles). El perfil de un usuario es un permiso compuesto.

---

## 3. Especificación Funcional — Procesos de Negocio

Se detallan los procesos de negocio de valor agregado. Para cada uno se identifican los roles
intervinientes y se describe la funcionalidad en términos de **Entrada / Comportamiento / Salida**.
Los diagramas de proceso (actividad), de secuencia y el modelo conceptual (clases de dominio) se
desarrollarán en las Especificaciones de Casos de Uso de las entregas posteriores.

### 3.1 Proceso: Contratación / Renovación de Suscripción

**Roles intervinientes:** Agente de Reservas, Administrador.

| | |
|---|---|
| **Entrada** | Cliente (nuevo o existente) y plan de suscripción elegido; fecha de inicio. |
| **Comportamiento** | Se registra o selecciona el cliente, se le asigna un plan y se genera la suscripción con su vigencia. El sistema inicializa el consumo de reservas del mes en cero y valida que el cliente no tenga otra suscripción activa incompatible. |
| **Salida** | Suscripción **Activa** con fecha de vencimiento y saldo de reservas del mes disponible. Queda registrada en la bitácora de negocio. |

**Reglas de negocio:**
- No se puede desactivar un plan que tenga clientes con suscripción activa.
- La renovación reinicia el consumo mensual de reservas y actualiza la fecha de vencimiento.

### 3.2 Proceso: Registro / Mantenimiento de Experiencia

**Roles intervinientes:** Coordinador de Experiencias, Administrador.

| | |
|---|---|
| **Entrada** | Datos de la experiencia: nombre, categoría, ciudad, organizador, fecha, hora, duración, cupo máximo, edad mínima y carácter premium. |
| **Comportamiento** | Se da de alta la experiencia en estado **Programada** con el cupo disponible igual al cupo máximo. A lo largo de su vida, el estado evoluciona según la fecha, el cupo y las acciones del organizador. |
| **Salida** | Experiencia publicada en el catálogo, disponible para ser reservada. |

**Ciclo de vida de la experiencia (estados):**
`Programada` → `Completa` (sin cupo) → `EnCurso` → `Finalizada`; o `Cancelada` (por el organizador, estado final).

### 3.3 Proceso: Toma de Reserva

**Roles intervinientes:** Agente de Reservas.

| | |
|---|---|
| **Entrada** | Cliente con suscripción activa, experiencia elegida y cantidad de invitados. |
| **Comportamiento** | El sistema valida: (a) que el cliente tenga **saldo de reservas** en el mes según su plan; (b) que la experiencia esté **Programada y con cupo disponible**; (c) que se respete la **anticipación máxima** del plan; (d) la **edad mínima** y el carácter **premium** de la experiencia contra el plan; (e) los **invitados** permitidos. Si todo es válido, crea la reserva, descuenta el cupo de la experiencia e incrementa el consumo mensual de la suscripción. |
| **Salida** | Reserva en estado **Pendiente** (luego **Confirmada**), cupo actualizado y consumo del plan incrementado. Se registra en bitácora de negocio. |

**Reglas de negocio:**
- Si no hay cupo, la experiencia pasa a **Completa** y el flujo deriva a la **Lista de Espera** (proceso 3.4).
- Si el cliente agotó su límite mensual, la reserva se rechaza con mensaje descriptivo.

**Estados de la reserva:** `Pendiente` → `Confirmada` → `Asistió` / `NoAsistió`; o `Cancelada`.

### 3.4 Proceso: Cancelación y Promoción de Lista de Espera

**Roles intervinientes:** Agente de Reservas.

| | |
|---|---|
| **Entrada** | Reserva a cancelar (con motivo) o alta en lista de espera sobre una experiencia completa. |
| **Comportamiento** | Al cancelar una reserva, se libera el cupo y se **devuelve** el saldo al plan del cliente. Si la experiencia tenía **lista de espera**, el sistema **promueve automáticamente** al primer cliente en la cola (FIFO), respetando la prioridad definida por su plan, y le genera la reserva. |
| **Salida** | Reserva **Cancelada**, cupo liberado y —si corresponde— nueva reserva generada para el primer cliente en espera. Ambos eventos quedan en bitácora. |

*(Ver diagrama de secuencia `DIAGRAMAS/CU - Secuencia - Cancelar y Lista de Espera.puml`.)*

### 3.5 Proceso: Registro de Asistencia y Calificación

**Roles intervinientes:** Agente de Reservas, Cliente (aporta la calificación).

| | |
|---|---|
| **Entrada** | Reserva **Confirmada** sobre una experiencia **Finalizada**. |
| **Comportamiento** | Se registra si el cliente **Asistió** o **NoAsistió**. Si asistió, se habilita el registro de una **calificación** (puntaje 1–5 y comentario opcional). |
| **Salida** | Reserva marcada como Asistió/NoAsistió y, en su caso, calificación almacenada; alimenta las estadísticas de la experiencia y las recomendaciones futuras. |

### 3.6 Proceso: Recomendación de Experiencias

**Roles intervinientes:** Agente de Reservas (consulta en nombre del cliente).

| | |
|---|---|
| **Entrada** | Cliente con intereses declarados e historial de reservas/calificaciones. |
| **Comportamiento** | El sistema cruza los **intereses** del cliente y su **historial** con el catálogo de experiencias **Programadas** afines (por categoría/interés, ciudad y calificaciones), excluyendo las ya reservadas. |
| **Salida** | Listado priorizado de experiencias sugeridas para ofrecer al cliente. |

---

## 4. Modelo Conceptual (entidades de dominio)

Entidades principales que surgen de los procesos anteriores (el detalle de atributos, relaciones,
cardinalidad y normalización se formaliza en el **DER Global** de la entrega siguiente):

**Cliente**, **Interés**, **PlanSuscripción**, **Suscripción**, **Experiencia**, **Organizador**,
**Categoría**, **Ciudad**, **Reserva**, **ListaEspera**, **Calificación**, **Empleado**/**Usuario**.

Relaciones clave:
- Un **Cliente** declara varios **Intereses** (N:M) y contrata una **Suscripción** vinculada a un **Plan**.
- Una **Experiencia** pertenece a una **Categoría**, se realiza en una **Ciudad** y la produce un **Organizador**.
- Una **Reserva** relaciona un **Cliente** con una **Experiencia** (y el **Empleado** que la tomó).
- Una **Experiencia** puede tener una **ListaEspera** (cola de Clientes) y varias **Calificaciones**.

---

## 5. Gestiones Técnicas Transversales (a documentar en detalle en el Documento Visión)

El sistema provee, como base predeterminada exigida por la cátedra, las siguientes gestiones técnicas.
En esta propuesta de negocio sólo se enuncian; su objetivo, funcionamiento, diagramas y aspectos técnicos
se desarrollan en el Documento Visión y en las Especificaciones de Caso de Uso correspondientes:

- **Gestión de Perfiles de Usuario** — permisos atómicos y compuestos, asignación dinámica de perfiles.
- **Gestión de Log In / Log Out** — autenticación, política de acceso y bloqueo progresivo.
- **Gestión de Múltiples Idiomas** — cambio dinámico de idioma en las interfaces.
- **Gestión de Bitácora** — registro y búsqueda combinada de operaciones.
- **Gestión de Backup** — catálogo y archivos físicos de copias de seguridad.
- **Gestión de Dígitos Verificadores** — verificación de integridad de datos (DVH/DVV).
- **Gestión de Encriptado** — protección de datos sensibles.
- **Esquema de persistencia** — mapeo objeto-relacional entre el modelo OO y la BD relacional.

---

## 6. Supuestos y Restricciones

**Supuestos:**
- La organización cuenta con personal interno que opera el sistema en representación de los clientes.
- Los organizadores proveen la información de sus experiencias con antelación suficiente.
- Cada cliente tiene, a lo sumo, una suscripción activa por vez.

**Restricciones:**
- Aplicación de escritorio (no web ni móvil) para uso interno de la organización.
- La información persiste en una base de datos relacional.
- El sistema debe cumplir los requisitos técnicos transversales exigidos por la cátedra (seguridad,
  auditoría, integridad, multiidioma y backup).

---

## 7. Próximas entregas (a partir de esta propuesta)

Esta propuesta de negocio se extenderá, en las entregas siguientes, con: Documento Visión completo,
Especificaciones de Casos de Uso (por cada proceso de negocio y por cada gestión técnica), Modelo
Conceptual detallado, Mapa de Navegación, Prototipos de Interfaz, Diagrama de Clases Global, Diagrama
de Componentes, DER Global normalizado, Especificación de Casos de Prueba y los manuales de usuario e
instalación.
