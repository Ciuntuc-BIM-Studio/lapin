---
title: Planificación
order: 97
summary: El cronograma de obra en un diagrama de Gantt: tareas en una EDT, hitos, vínculos con desfases, la ruta crítica calculada sobre un calendario con los festivos legales de Rumanía; de y hacia MS Project, IFC y Excel.
---

## La pestaña Planificación

![La pestaña Planificación](shots/ribbon-planning.webp)

- **Gantt**: abre el cronograma bajo la vista. La pantalla se divide en dos filas: el modelo arriba y el cronograma abajo.
- **Calendario…**: los días y horas laborables, los festivos y la pausa de invierno (más abajo).
- **Tarea** e **Hito**: una tarea o un hito nuevo antes de la tarea seleccionada en el Gantt (o al final).
- **Aumentar sangría**, **Reducir sangría**, **Vincular tareas** y **Desvincular tareas**: la EDT y los vínculos de las tareas seleccionadas.
- **Añadir a la tarea…** (TA) y **Elementos sin tarea**: los elementos del modelo en las tareas (más abajo).
- **Importar MS Project…**, **Exportar MS Project…**, **Exportar XLSX** y **Exportar IFC…**: el cronograma hacia y desde otros programas (más abajo).

Un proyecto tiene un solo cronograma. Se crea la primera vez que añades una tarea y empieza el lunes siguiente. Cambia su inicio en la barra del Gantt.

## El Gantt

![El Gantt bajo la vista 3D, con una tarea seleccionada](shots/planning-gantt.webp)

A la izquierda está la tabla de tareas: el número de fila, el código **EDT**, el nombre, la **Duración** en días laborables, el **Inicio** y el **Fin**, las **Predecesoras** y la **Holgura** total. A la derecha, cada tarea es una barra sobre la escala de tiempo:

- una **tarea** es una barra azul, roja cuando está en la ruta crítica;
- un **resumen** (una tarea con tareas debajo) es un corchete oscuro desde el inicio de su primera tarea hasta el fin de la última;
- un **hito** es un rombo.

Los días no laborables aparecen sombreados y hoy es una línea discontinua. **Días**, **Semanas**, **Meses** y **Trimestres** cambian la escala, igual que <kbd>Ctrl</kbd> + la rueda del ratón. El botón de las cuatro esquinas muestra todo el cronograma y **Hoy** se desplaza a hoy. **Ruta crítica** activa y desactiva el rojo. La flecha junto al nombre de un resumen pliega y despliega las tareas que tiene debajo.

El Gantt es una vista como las demás: tiene una pestaña y, en el escritorio, puede abrirse en una ventana propia para un segundo monitor.

### Edición en la tabla

Haz doble clic en una celda para escribir en ella. <kbd>Enter</kbd> conserva el valor y <kbd>Escape</kbd> lo descarta.

- **Duración**: días laborables; 0 convierte la tarea en un hito.
- **Inicio**: la tarea empieza no antes de ese día (una restricción, véase más abajo).
- **Fin**: la duración pasa a ser los días laborables desde el inicio hasta ese día.
- **Predecesoras**: como en MS Project, números de fila separados por comas, cada uno con un tipo y un desfase: `3`, `5SS`, `7FF+2`, `2FS-1d`.

El teclado funciona como en una tabla: <kbd>↑</kbd> y <kbd>↓</kbd> mueven la selección, <kbd>Insert</kbd> añade una tarea antes de la seleccionada (<kbd>Shift</kbd>+<kbd>Insert</kbd> un hito), <kbd>Delete</kbd> elimina las tareas seleccionadas con lo que tienen debajo, <kbd>Tab</kbd> aumenta la sangría y <kbd>Shift</kbd>+<kbd>Tab</kbd> la reduce, <kbd>F2</kbd> cambia el nombre.

### Edición en el diagrama

- **Arrastra una barra** para mover la tarea. Entonces empieza no antes del día en que la sueltes. La fecha se ve mientras arrastras.
- **Arrastra el extremo de una barra** para cambiar su duración. Los días laborables se ven mientras arrastras.
- **Vincula dos tareas**: pasa el ratón sobre una barra y arrastra desde el círculo pequeño de uno de sus extremos hasta otra barra. Los extremos que unes dan el tipo: del fin al inicio de la otra es fin a comienzo (FS), inicio a inicio SS, fin a fin FF, inicio a fin SF.
- **Haz clic en una flecha** para cambiar el tipo y el desfase del vínculo (en días laborables; negativo es un solape) o para quitarlo.

Cada cambio es un solo paso que se puede deshacer.

## Una tarea en Propiedades

![Una tarea en Propiedades](shots/planning-task.webp)

Selecciona una tarea y Propiedades la muestra: su nombre, su tipo IFC, su duración, si es un hito, sus fechas y:

- **Restricción**: **Lo antes posible** (por defecto), **No comenzar antes del** una fecha o **Debe comenzar el** una fecha. Una tarea que debe empezar antes de que terminen sus predecesoras les quita holgura. Su holgura pasa entonces a ser negativa y se vuelven críticas.
- **Obras húmedas**: hormigón, soleras, revoques. Una tarea húmeda no trabaja en la pausa de invierno del calendario, cuando la pausa está activada.
- **Holgura total** y **Holgura libre**: cuántos días laborables puede retrasarse la tarea sin mover el fin, o sin mover lo que la sigue. **Comienzo tardío** y **Fin tardío** son las últimas fechas que mantienen el fin.
- **Predecesoras**: cada vínculo de entrada, con su tipo y desfase para cambiar, o para quitar. **Sucesoras** enumera lo que sigue a la tarea.

## La ruta crítica

Lapin calcula el cronograma después de cada cambio. Un cálculo hacia delante halla las fechas más tempranas y uno hacia atrás las más tardías. Las tareas sin holgura forman la ruta crítica y un retraso en cualquiera de ellas mueve el fin. Un vínculo hacia un resumen sujeta todas las tareas que tiene debajo. Un vínculo desde un resumen parte del inicio de su primera tarea y termina en el fin de su última. Lapin rechaza un vínculo que cree un bucle.

## Los elementos del modelo en las tareas

![La tarea Albañilería seleccionada: sus muros dibujados seleccionados en 3D](shots/planning-links.webp)

Una tarea toma elementos del modelo, así el cronograma sabe qué se construye y cuándo. Selecciona una tarea y sus elementos se dibujan seleccionados en 3D y en las plantas; doble clic en su barra los selecciona y los encuadra. Selecciona elementos en una vista y las filas de sus tareas se marcan en el Gantt. La columna **Elementos** cuenta los elementos de cada tarea (los de un resumen, con los de las tareas que tiene debajo).

### Una regla

![Los elementos de una tarea en Propiedades](shots/planning-task-elements.webp)

En **Elementos** de Propiedades, marca **Encontrarlos por una regla** y elige qué toma la tarea, como en el filtro de una tabla: **Clases**, **Plantas**, **Tipos**, **Materiales**, **Grupos** y **Fases**, cada valor con cuántos elementos lo tienen. Cada condición puesta debe cumplirse; una vacía lo deja pasar todo. Se guarda la regla, no los elementos: un muro dibujado mañana en la planta baja entra solo en la tarea «Albañilería planta baja». **Regla a partir de la selección** hace una regla con las clases y plantas de los elementos seleccionados.

### Una parte de los elementos

**Parte** dice qué toma la tarea de cada elemento: los **elementos enteros**, su **núcleo** (la capa estructural), sus **acabados** (todas las capas salvo el núcleo), las capas de un material o las de una categoría de material (Acabado, Aislamiento…). Así, un muro por capas se levanta en una tarea de Albañilería (su núcleo) y se enluce en una de Enlucido (sus acabados). Un elemento sin capas es su propio núcleo y no tiene acabados.

### A mano

![La tarjeta de la selección sobre el 3D, las dos tareas de un muro marcadas en el Gantt](shots/planning-element-tasks.webp)

Con el Gantt abierto, una tarjeta sobre la vista del modelo dice cuántos elementos hay seleccionados y en qué tareas están. Arrástrala por su asa a la fila de una tarea: los elementos entran en esa tarea. Mantén <kbd>Shift</kbd> al soltar para sacarlos de ella. **Añadir a la tarea…** (<kbd>T</kbd> <kbd>A</kbd>, también en la pestaña Planificación) elige la tarea de una lista.

![Añadir a la tarea](shots/planning-assign.webp)

Lo que añades o sacas a mano son las **excepciones** de la tarea: la regla queda como está. Propiedades las cuenta, y la × junto a cada una las olvida. Las Propiedades de un elemento listan sus tareas en **Cronograma de obra**, cada una con una × para sacarlo. Cada cambio es un paso para deshacer.

### Elementos sin tarea

En cuanto una tarea toma elementos, la barra del Gantt dice cuántos elementos no tienen tarea o tienen capas que ninguna toma; un clic los selecciona, listos para arrastrarlos a una tarea. **Planificación → Elementos sin tarea** hace lo mismo. **Gestionar → Comprobación** también los lista, en *Elements with a task*: un aviso por cada uno, un clic lo selecciona. Los elementos existentes que se quedan no necesitan tarea.

### En IFC y MS Project

La exportación IFC escribe lo que construye cada tarea como IfcRelAssignsToProduct (el elemento, con las tareas que lo construyen), y aquello sobre lo que actúa una tarea de demolición, retirada o desmontaje como IfcRelAssignsToProcess. La regla, la parte y las excepciones viajan en `Lapin_Task`, así Lapin relee la regla; un archivo de otro programa da tareas con los elementos que les asigna. MS Project XML las lleva en el campo Text1, llamado *Lapin elements*. El libro XLSX tiene las columnas **Elements** y **Rule**.

## El calendario

![El calendario de trabajo](shots/planning-calendar.webp)

**Planificación → Calendario…** establece:

- los **días laborables** de la semana (de lunes a viernes por defecto) y las **horas** de un día (8 desde las 08:00);
- los **festivos**: los festivos legales de Rumanía, calculados para cada año (incluidos el Viernes Santo, la Pascua y Pentecostés ortodoxos), o ninguno. La lista de debajo muestra los festivos de un año para comprobarlos;
- la **pausa de invierno** para las obras húmedas, desactivada por defecto: del 15 de diciembre al 1 de marzo, salvo que fijes otros días;
- las **excepciones**: días libres del proyecto (una obra cerrada) o días trabajados que normalmente no lo serían (un sábado).

Las duraciones y los desfases cuentan días laborables de este calendario.

## Intercambio del cronograma

- **Exportar MS Project…** escribe un archivo XML de MS Project. Lo abren MS Project, ProjectLibre y GanttProject, y Primavera P6 lo importa. Contiene las tareas con su EDT, duraciones, fechas, restricciones y holguras, los vínculos con sus tipos y desfases, y el calendario con cada festivo como excepción. **Importar MS Project…** lee un archivo así en lugar del cronograma del proyecto. Un cronograma escrito por Lapin y leído de nuevo conserva los id y las fechas de sus tareas.
- **Exportar IFC…** (también en **Archivo**) escribe el cronograma en el archivo IFC junto con el modelo: un IfcWorkSchedule, sus IfcTask con sus tiempos, los vínculos IfcRelSequence y el IfcWorkCalendar. Al importar un archivo IFC que tenga un cronograma, este también se incorpora.
- **Exportar XLSX** escribe un libro de cálculo: la tabla del Gantt y los días libres del calendario.

Los campos propios de Lapin para los que otros programas no tienen sitio (la restricción de una tarea, si es húmeda, la pausa de invierno) viajan en IFC como los conjuntos de propiedades `Lapin_Task` y `Lapin_Calendar`. MS Project no tiene tareas húmedas, así que la pausa de invierno se queda en Lapin.

## Lo que viene

Un cronograma generado a partir del modelo, la simulación 4D del edificio creciendo en 3D, el avance y las líneas base, y el presupuesto llegan en las próximas etapas.

