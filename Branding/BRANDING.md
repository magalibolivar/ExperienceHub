# ExperienceHub — Identidad de Marca (SUNSET)

> **Descubrí experiencias. Viví momentos.**
>
> Rebranding integral del sistema. Deja de percibirse como WardrobeFlow para convertirse en una
> plataforma moderna, cálida y luminosa para **descubrir, reservar y disfrutar experiencias** por
> suscripción mensual. La única capa que se conserva es la **infraestructura técnica**
> (arquitectura en capas, patrones, seguridad, auditoría, i18n, historial, permisos, autenticación).

---

## 1. Personalidad de marca

Descubrimiento · Emoción · Cercanía · Diversión · Inspiración · Comunidad · Confianza · Simplicidad · Modernidad · Calidad.

**No** es un ERP, ni software administrativo, ni una app bancaria. El usuario **explora actividades
para disfrutar**, no administra información. Tono de los textos: amigable, cálido, en primera persona
("Descubrí", "Reservá", "¡A disfrutar!").

Inspiración de diseño (principios, no copia): **Airbnb Experiences, Eventbrite, Fever, Meetup, Notion**
→ interfaces limpias, mucho espacio en blanco, tarjetas atractivas, navegación simple, jerarquía clara.

---

## 2. Logo

`Branding/logo-experiencehub.svg` — un **sol naciente sobre el horizonte** con un **punto de lugar**
(pin de experiencia). Representa descubrimiento, movimiento, salida, exploración y momentos.
Construido con el degradé SUNSET (coral→naranja→amarillo) + wordmark en azul oscuro con "Hub" en coral.
**Sin ningún elemento de ropa/moda.**

---

## 3. Paleta SUNSET

| Rol | Color | HEX |
|---|---|---|
| Acción principal / marca | Coral | `#FF6B6B` |
| Hover | Naranja | `#FF8C42` |
| Advertencia | Amarillo | `#FFD166` |
| Texto | Azul oscuro | `#243B53` |
| Fondos | Blanco cálido | `#FFFDF9` |
| Gris fondo | | `#F8F9FA` |
| Gris borde | | `#E5E7EB` |
| Gris apagado | | `#9CA3AF` |
| Gris texto sec. | | `#374151` |
| Acción positiva | Verde | `#2E9E5B` |
| Error | Rojo | `#E5484D` |

**Reglas:** botones = coral (hover naranja); positivas = verde; advertencias = amarillo; errores = rojo;
texto = azul oscuro; fondos = blanco cálido. **Evitar fondos oscuros y bloques negros** — la interfaz
debe sentirse luminosa.

---

## 4. Categorías (color + icono)

Cada tipo de experiencia se identifica con color e ícono propios (en tarjetas, badges, filtros,
etiquetas, listados). Implementado en `Estilo.Categoria(nombre)` (match por palabra clave).

| Categoría | Icono | Color |
|---|---|---|
| Gastronomía / Cata / Cocina | 🍷 | `#FF8C42` |
| Arte / Pintura | 🎨 | `#F06BA8` |
| Cultura / Teatro | 🎭 | `#8B5CF6` |
| Aventura / Aire libre | 🏕️ | `#2E9E5B` |
| Tecnología | 💻 | `#3B82F6` |
| Bienestar | 🧘 | `#14B8A6` |
| Educación / Taller | 📚 | `#38BDF8` |
| Música | 🎵 | `#D946EF` |
| Fotografía | 📷 | `#FF6B6B` |
| Escape Room | 🧩 | `#FFD166` |

---

## 5. Tipografía

**Segoe UI** (WinForms no trae Poppins/Manrope por defecto; Segoe UI es la alternativa moderna
sin serif del sistema). Jerarquía: título 20 pt bold · subtítulos 13 pt bold · cuerpo 9.5–10 pt ·
slogan en itálica. **Nunca serif.**

---

## 6. Iconografía

Se eliminó **toda** referencia a WardrobeFlow (marca de agua "WF" del fondo MDI, prendas, perchas,
armarios, limpieza, planchado, moda). La iconografía usa **emojis del sistema** temáticos:
🎭 cultura · 🍷 gastronomía · 🎨 arte · 🏕️ aventura · 💻 tecnología · 🧘 bienestar · 📚 educación ·
🎵 música · 📷 fotografía · 🧩 escape · ✨ genérico.

---

## 7. Modernización de componentes

Todo se centraliza en **`GUI/Estilo.cs`** (fuente única de la identidad) y se aplica de forma
**automática y recursiva** desde `FormBase.OnLoad` → cada formulario (viejo o nuevo) se moderniza
sin tocar su Designer. Así no conviven pantallas "viejas" con "nuevas".

| Componente | Antes | Ahora (SUNSET) |
|---|---|---|
| **Botones** | vino plano | Coral, texto blanco, flat, hover naranja, cursor mano; secundarios (Cancelar) en blanco con borde gris |
| **DataGridView** | grilla clásica | Header azul oscuro, filas alternadas, selección coral suave, sin bordes duros, **oculta columnas técnicas (Id/DVH) automáticamente** |
| **TextBox / ComboBox / Numeric** | 3D | Borde plano, fondo blanco, texto azul oscuro |
| **Menú (MenuStrip)** | barra clásica | Fondo blanco, texto azul oscuro, **hover durazno** (renderer SUNSET) |
| **Barra de idioma** | bloque oscuro `#282837` | Gris claro con texto oscuro |
| **Fondo MDI** | rosa con marca de agua "WF" | Degradé cálido blanco→durazno + sol difuso + marca/slogan tenue |
| **Diálogos (MessageBox)** | nativos | `Estilo.Info/Exito/Error/Confirmar`: tarjeta luminosa con barra de color, ícono grande y botón coral |
| **Login** | tema rosa | Marca "ExperienceHub" en coral + slogan; botón coral |
| **Dashboard** | tarjetas pastel varias | Marca + slogan; KPIs en tonos coral/amarillo/naranja |

---

## 8. Análisis de formularios

| Formulario | Estado | Nota |
|---|---|---|
| Login | **rediseñado** (marca + slogan + coral) | conserva Observer/i18n |
| Menú MDI | **rediseñado** (fondo sunset, menú claro, "Catálogos") | |
| Dashboard | **rediseñado** (KPIs sunset + slogan) | genérico por permisos |
| Experiencias | **nuevo** (reemplaza Prendas) + emoji/color por categoría | |
| Reservas | **nuevo** (reemplaza Pedidos de Venta) | |
| Reservas Realizadas | **nuevo** (asistencia + calificación) | |
| Organizadores / Categorías / Ciudades | **nuevos** (catálogos) | |
| Suscripciones | **nuevo** (renovar/suspender) | |
| Lista de Espera | **nuevo** (confirmar oferta) | |
| Recomendaciones | **nuevo** (regla 11) | |
| Clientes / Planes | **reutilizados** y re-skineados (ciudad + intereses / beneficios) | |
| Bitácora, Usuarios, Perfiles, Idiomas, Backup, Diagnóstico, Mi Perfil | **reutilizados** (infra) — re-skineados automáticamente por `Estilo` | |
| Prendas, Pedidos*, Dashboards de rol, diálogos de despacho/mantenimiento | **eliminados** | ver `TRABAJO_REALIZADO.md` |

**Navegación:** MDI con menú superior claro. Agrupación por intención: *Experiencias*, *Ventas*
(Clientes/Planes/Reservas), *Catálogos* (Organizadores/Categorías/Ciudades/Suscripciones/Lista de
espera/Recomendaciones), *Administrar* y *Bitácora*. Panel de control como pantalla de inicio.

---

## 9. Terminología

| WardrobeFlow | ExperienceHub |
|---|---|
| Prenda | Experiencia |
| Pedido | Reserva |
| Despacho | Confirmación |
| Entrega | Asistencia |
| Limpieza | (eliminado) |
| Stock | Cupos disponibles |
| Disponibilidad de prendas | Disponibilidad de experiencias |
| Catálogo de prendas | Catálogo de experiencias |

Aplicado en entidades, BLL, DAL, SQL, menús, mensajes y traducciones (ES/EN/RU/PT).

---

## 10. Mejoras de UX aplicadas

- **Mensajes amigables**: diálogos SUNSET con ícono y tono cálido ("¡Reserva confirmada! ¡A disfrutar!").
- **Grillas más limpias**: se ocultan columnas técnicas automáticamente; filas alternadas; selección suave.
- **Botones más visibles**: coral con hover, cursor mano.
- **Jerarquía clara**: marca + slogan en login/dashboard; títulos grandes.
- **Categorías reconocibles** de un vistazo por color + emoji.
- **Carga asíncrona** en el dashboard (sin bloquear la UI) + auto-refresh.

### Pendiente sugerido (mejora continua)
- Vista de experiencias en **tarjetas** (además de la grilla), estilo Airbnb.
- **Estados vacíos** ilustrados ("Todavía no hay experiencias — creá la primera").
- **Indicadores de carga** (spinner) en operaciones largas.
- Definir columnas explícitas por grilla (además del ocultado automático).

---

*ExperienceHub — identidad SUNSET. La infraestructura técnica del proyecto original se conserva intacta;
todo lo demás (dominio, negocio, identidad, terminología y UX) es nuevo.*
