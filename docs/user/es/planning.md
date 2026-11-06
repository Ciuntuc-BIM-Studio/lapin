---
title: Planificación
order: 97
summary: El cronograma de obra en un diagrama de Gantt: tareas en una EDT, hitos, vínculos con desfases, la ruta crítica calculada sobre un calendario con los festivos legales de Rumanía; la simulación 4D del edificio creciendo día a día, en vistas, láminas y en vídeo; el avance marcado en obra, también desde un teléfono, frente a una línea base; de y hacia MS Project, IFC y Excel.
---

## La pestaña Planificación

![La pestaña Planificación](shots/ribbon-planning.webp)

- **Generar cronograma…**: un cronograma entero hecho desde el modelo en un solo paso (más abajo).
- **Gantt**: abre el cronograma bajo la vista. La pantalla se divide en dos filas: el modelo arriba y el cronograma abajo.
- **Calendario…**: los días y horas laborables, los festivos y la pausa de invierno (más abajo).
- **Tarea** e **Hito**: una tarea o un hito nuevo antes de la tarea seleccionada en el Gantt (o al final).
- **Aumentar sangría**, **Reducir sangría**, **Vincular tareas** y **Desvincular tareas**: la EDT y los vínculos de las tareas seleccionadas.
- **Añadir a la tarea…** (TA) y **Elementos sin tarea**: los elementos del modelo en las tareas (más abajo).
- **Simular**, **Lámina de etapas…**, **Gantt en lámina** y **Exportar vídeo…**: la simulación 4D, el modelo como lo tiene el cronograma en un día (más abajo).
- **Marcar terminado** (MD), **Marcar iniciado** (MS), **Marcar no iniciado**, **Guardar línea base**, **Borrar línea base** e **Importar avance…**: el avance y la línea base (abajo).
- **Importar MS Project…**, **Exportar MS Project…**, **Exportar XLSX** y **Exportar IFC…**: el cronograma hacia y desde otros programas (más abajo).

Un proyecto tiene un solo cronograma. Se crea la primera vez que añades una tarea y empieza el lunes siguiente. Cambia su inicio en la barra del Gantt.

## Generar el cronograma desde el modelo

![Generar cronograma: las plantillas, la pestaña Inicio y la vista previa](shots/planning-generate.webp)

**Planificación → Generar cronograma…** (también en la barra del Gantt) hace un cronograma completo desde el modelo: una tarea por cada paso de la obra y, donde el paso va planta a planta, una por planta, cada tarea ya con sus elementos. A la izquierda están las **plantillas**: **Estructura de hormigón armado**, **Muros de carga de fábrica**, **Nave de acero** y **Entramado de madera**. Lapin marca como *sugerida* la que pide el modelo: la nave de acero si el modelo tiene una, el entramado de madera si sus muros son entramados, los muros de carga si no tiene pilares y, si no, la estructura de hormigón. Encima están los estilos del proyecto (**Guardar como estilo…** guarda lo que configuraste).

A la derecha está la **vista previa**: las tareas que haría el generador, como barras, cada una con la cantidad que le dio su duración, y debajo los recuentos:

- **Elementos vinculados**: los elementos que toman las tareas, de todos los que necesitan una. Es el número más importante. Si quedan elementos fuera, **Mostrar los elementos sin tarea** los selecciona en la vista y **Un paso para ellos** añade un paso con sus clases.
- **Duraciones por rendimientos** y **Duraciones por defecto (sin rendimiento)**: cuántas tareas cronometró un rendimiento y cuántas tomaron los días de su paso.
- **Encofrados y andamios en las tareas de sus soportes** (más abajo).

**Aplicar** escribe el cronograma en un solo paso para deshacer. La pantalla se divide: el modelo arriba, el Gantt abajo.

### La pestaña Inicio

- **Inicio**: el primer día del cronograma.
- **Dividir por plantas**: una tarea por planta para los pasos que van planta a planta (estructura, fábrica, acabados); desmarcado, una tarea por paso.
- **Primero las demoliciones**: los elementos que las fases demuelen entran en una tarea **Demolición** antes de todas (su clase IFC DEMOLITION). Los elementos existentes que se quedan no son de ninguna tarea.
- **Encofrados** y **Andamios**: si estos elementos provisionales viven según las tareas de sus soportes (más abajo).
- **Conservar las tareas hechas a mano**: cuando el cronograma se genera de nuevo (más abajo).

### La pestaña Secuencia

![La pestaña Secuencia: los pasos en orden](shots/planning-generate-sequence.webp)

Los pasos de la plantilla, en orden. Cada uno se puede desactivar, renombrar y subir o bajar con sus flechas. Dice si va **una vez** para el edificio o **por planta**, y cómo sigue al paso anterior en la misma planta: fin a comienzo (FS), o comienzo a comienzo (SS) con desfase, para los acabados que suben tras la estructura. El lápiz abre lo que construye el paso: las clases de elementos, elegidas entre las del modelo, y la **Parte** de ellos (enteros, su núcleo o sus acabados).

Los vínculos salen del orden: cada tarea sigue al paso anterior en la misma planta y al mismo paso en la planta inferior. En los muros de carga y el entramado de madera, los muros de una planta esperan también al forjado de la planta inferior.

Cada tarea se hace con una **regla** (sus clases, su planta), así que conserva sus elementos: un muro dibujado después de generar entra solo en su tarea.

### La pestaña Duraciones

![La pestaña Duraciones: los rendimientos y, por paso, rendimiento, cuadrilla y días](shots/planning-generate-durations.webp)

La duración de una tarea sale de la cantidad de sus elementos (de las mediciones: el volumen del núcleo, la superficie de los acabados, las puertas y ventanas por unidad, el acero por su peso) y de un **rendimiento**:

*duración = cantidad × horas-hombre por unidad ÷ (cuadrilla × horas al día)*, en días laborables enteros. Por ejemplo 4,5 m³ de fábrica a 4 horas-hombre por metro cúbico, con una cuadrilla de 2 que trabaja 8 horas, son 18 ÷ 16 = 1,125, o sea 2 días.

Los rendimientos son tuyos: Lapin no incluye ningún prontuario. **Importar rendimientos…** lee tu tabla de un archivo CSV o XLSX: una columna para el código, otra para el texto, otra para la unidad (m3, m2, m, ud, kg, t…), otra para las horas-hombre por unidad (o un rendimiento en unidades por hora) y, opcionalmente, otra para la cuadrilla. **Plantilla (CSV)** guarda las columnas con unas filas de valores aproximados escritos por Lapin, cada una marcada como *orientativa*. **Usar los valores orientativos de Lapin** las pone en el proyecto para probar el generador; el diálogo avisa entonces de que las duraciones salen de valores orientativos, no de un rendimiento real.

Debajo, cada paso elige su **Rendimiento**, su **Cuadrilla** (0: la del rendimiento) y sus **Días**, usados cuando ningún rendimiento la da. La pestaña Calendario muestra el calendario en que se cuentan las duraciones.

### Generado de nuevo

Abre de nuevo el generador y pulsa **Aplicar**: el cronograma se rehace sobre las mismas tareas, que conservan sus identificadores. Lo que cambiaste a mano se queda:

- una tarea generada que editaste (nombre, duración, fechas, regla, parte) queda **Conservada como se editó**, marcado en sus Propiedades; desmárcalo para que se rehaga desde su paso;
- los elementos que añadiste o quitaste a mano siguen siendo sus excepciones;
- las tareas y los vínculos que hiciste tú se quedan; desmarca **Conservar las tareas hechas a mano** para quedarte solo con las generadas.

![El cronograma generado: el modelo arriba, el Gantt abajo](shots/planning-generated.webp)

En Propiedades, una tarea generada dice de dónde salió su duración: la cantidad, el rendimiento y la cuadrilla, o los días del paso.

![Una tarea generada en Propiedades](shots/planning-generated-task.webp)

### Encofrados y andamios

Un encofrado o un andamio de **Medios auxiliares** no tiene tarea propia: vive según las tareas de lo que sirve. El encofrado se monta cuando empieza su forjado, viga o muro, y se retira cuando pasan los días que dice su tipo tras terminarlos (**Desencofrado tras**, en días laborables: 21 en forjados y vigas, 7 en muros, pilares y zapatas, salvo que el tipo diga otra cosa). El andamio está desde la primera tarea en sus muros hasta la última, acabados incluidos. Selecciona uno y sus Propiedades dicen cuándo está **En obra** y según qué tareas **Vive según**.

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

Una cubierta también se construye por capas: su estructura (el forjado de una cubierta plana, una bóveda o una cubierta sin armadura; los cabios o cerchas de una con armadura) es su núcleo, y su cubrición (con las cubiertas de las buhardillas) y sus rastreles sus acabados. Una tarea **Estructura de cubierta** (núcleo) y otra **Cubrición** (acabados) toman cada una su parte, una regla por material encuentra el forjado por su hormigón, y la simulación y una vista en una fecha muestran la cubrición solo cuando está puesta. Las plantillas del generador construyen una cubierta en estos dos pasos, cada uno con la duración del área de su parte, y las bajantes en un paso propio (**Canalones y bajantes**); la cubrición de una nave de acero sigue siendo un paso.

### A mano

![La tarjeta de la selección sobre el 3D, las dos tareas de un muro marcadas en el Gantt](shots/planning-element-tasks.webp)

Con el Gantt abierto, una tarjeta sobre la vista del modelo dice cuántos elementos hay seleccionados y en qué tareas están. Arrástrala por su asa a la fila de una tarea: los elementos entran en esa tarea. Mantén <kbd>Shift</kbd> al soltar para sacarlos de ella. **Añadir a la tarea…** (<kbd>T</kbd> <kbd>A</kbd>, también en la pestaña Planificación) elige la tarea de una lista.

![Añadir a la tarea](shots/planning-assign.webp)

Lo que añades o sacas a mano son las **excepciones** de la tarea: la regla queda como está. Propiedades las cuenta, y la × junto a cada una las olvida. Las Propiedades de un elemento listan sus tareas en **Cronograma de obra**, cada una con una × para sacarlo. Cada cambio es un paso para deshacer.

### Elementos sin tarea

En cuanto una tarea toma elementos, la barra del Gantt dice cuántos elementos no tienen tarea o tienen capas que ninguna toma; un clic los selecciona, listos para arrastrarlos a una tarea. **Planificación → Elementos sin tarea** hace lo mismo. **Gestionar → Comprobación** también los lista, en *Elements with a task*: un aviso por cada uno, un clic lo selecciona. Los elementos existentes que se quedan no necesitan tarea.

### En IFC y MS Project

La exportación IFC escribe lo que construye cada tarea como IfcRelAssignsToProduct (el elemento, con las tareas que lo construyen), y aquello sobre lo que actúa una tarea de demolición, retirada o desmontaje como IfcRelAssignsToProcess. La regla, la parte y las excepciones viajan en `Lapin_Task`, así Lapin relee la regla; un archivo de otro programa da tareas con los elementos que les asigna. MS Project XML las lleva en el campo Text1, llamado *Lapin elements*. El libro XLSX tiene las columnas **Elements** y **Rule**.

## La simulación 4D

![La casa en un día de su cronograma: la losa de la planta alta en curso en su encofrado, los muros de arriba sin empezar](shots/planning-simulation.webp)

**Planificación → Simular** (también **Simular** en la barra del Gantt) muestra el modelo como lo tiene el cronograma en un día. El Gantt se abre bajo la vista si no está abierto, con el reproductor arriba y el día como una franja sobre el diagrama:

- lo que **no ha empezado** está oculto (o se muestra como un fantasma pálido, en los ajustes);
- lo que está **en curso** se dibuja en su color (ámbar);
- lo que está **construido** se dibuja tal como es;
- lo que está **en demolición** es rojo mientras corre su tarea, y luego desaparece;
- los **encofrados y andamios** están, en su color (azul), solo entre los días que les dan las tareas de sus elementos: el encofrado de una losa desde el inicio de su hormigonado hasta el desencofrado, un andamio desde la primera tarea en sus muros hasta la última.

Un muro de capas levantado en una tarea y revocado en otra crece en dos pasos: primero su núcleo, luego el revoque. Los elementos que ninguna tarea toma quedan como están. Una tarjeta sobre la vista del modelo dice el día, qué significa cada color y cuántos elementos hay en cada estado.

### Reproducirla

![El reproductor arriba del Gantt](shots/planning-simulation-bar.webp)

- **Reproducir** (▶) pasa los días a la velocidad elegida (de 1 a 60 días por segundo) hasta el final; **Pausa** la detiene.
- Las flechas a cada lado retroceden o avanzan **un día** o **una semana**.
- Arrastra el control deslizante, escribe una fecha, o haz clic y arrastra sobre la escala de tiempo del Gantt: la franja sobre el diagrama y el modelo la siguen.
- Junto a la fecha: las tareas en curso ese día.

De un día a otro solo cambian los colores y la visibilidad de los elementos: nada se construye de nuevo, así que un modelo grande se reproduce tan fluido como uno pequeño.

### Sus colores

![Los ajustes de la simulación](shots/planning-simulation-settings.webp)

El engranaje abre los ajustes de la simulación, guardados en el proyecto: los colores de lo que está en curso, en demolición, de los encofrados y andamios y de los fantasmas; si lo que **no ha empezado** se oculta o se muestra **como fantasmas**; si los **elementos sin tarea** se muestran u ocultan; y si las **capas construidas por separado** se ven por separado. **Colores por defecto** devuelve los de Lapin.

### Estado en la fecha en vistas y láminas

![El Estado en la fecha de una planta en sus propiedades](shots/planning-view-date.webp)

Una planta, una sección, un alzado o una vista 3D guardada tiene en sus propiedades **Estado en la fecha**: la vista dibuja entonces el modelo como lo tiene el cronograma ese día, con los mismos colores, y sus láminas lo imprimen así (el PDF muestra lo que muestra la pantalla). Con la simulación en marcha, **la fecha del cursor** toma su día. Una vista en una fecha dibuja los estados del cronograma, no los de su fase. La × junto a la fecha devuelve el modelo tal como está. Una vista 3D con una fecha muestra ese día incluso con la simulación apagada.

### La lámina de etapas

![Lámina de etapas: las fechas de los finales de capítulo](shots/planning-stages-dialog.webp)

**Planificación → Lámina de etapas…** (también en el reproductor) pone el modelo en varias fechas uno junto a otro en una lámina nueva. Se dibuja desde **la vista 3D, tal como se ve ahora** (una imagen en cada fecha) o desde una planta, una sección o un alzado, que el motor dibuja en cada fecha. Las fechas son **el final de cada capítulo** (cada tarea en lo alto de la EDT), **el final de cada mes**, o fechas **elegidas**. Cada cuadro lleva su fecha y las tareas en curso.

![Una lámina de etapas](shots/planning-stages-sheet.webp)

### El Gantt en una lámina

![El Gantt en una lámina](shots/planning-gantt-sheet.webp)

**Planificación → Gantt en lámina** pone el cronograma en una lámina nueva: la tabla (fila, EDT y tarea, días, inicio, fin) y las barras sobre una escala de tiempo, las tareas críticas en rojo, los resúmenes como corchetes, los hitos como rombos, los vínculos. Un cronograma más alto que la lámina va en tantas láminas como necesite, con su encabezado en cada una. Se dibuja en vectores, como las vistas, y se imprime en el PDF.

### Exportar vídeo

![Exportar vídeo](shots/planning-video.webp)

**Planificación → Exportar vídeo…** (también en el reproductor) graba la vista 3D, desde donde mira ahora, de un día a otro, un fotograma por **un día** o por **una semana**, con la fecha y las tareas en curso escritas debajo. El archivo es un vídeo WebM, o MP4 donde el sistema solo ofrece eso (la aplicación de macOS). Un navegador que no graba vídeo guarda en su lugar los fotogramas como archivos PNG en un ZIP.

## El avance

![El Gantt con el avance: la columna %, la parte hecha más oscura en cada barra, la línea base debajo y la desviación](shots/planning-progress.webp)

A medida que avanzan las obras, el cronograma dice cuánto se ha hecho y mueve lo que queda:

- La **fecha de estado**, en la barra del Gantt, es el día hasta el que se informa el avance. El trabajo restante empieza el día laborable siguiente: una tarea sin empezar no puede empezar antes, y una en curso hace lo que le queda a partir de entonces. El diagrama la dibuja como una línea azul.
- El **% completado** de una tarea se escribe en su columna o en Propiedades. Sin él, sale de sus elementos marcados en obra: la cantidad hecha de la cantidad de la tarea (en su unidad, cuando el generador le dio una), si no, su número. El de una tarea resumen sale de sus tareas, ponderado por sus duraciones.
- El **comienzo real** y el **fin real** se escriben en Propiedades. Vacíos, salen de las marcas: el primer día en que se marcó un elemento, y el día del último elemento cuando todos están terminados. Una tarea terminada queda en sus fechas reales, fuera de la ruta crítica.
- Una tarea en curso conserva su comienzo real y hace la parte de su duración aún no hecha (redondeada hacia arriba) después de la fecha de estado; las tareas siguientes se mueven con ella.

Las barras muestran la parte hecha más oscura. Propiedades dice de dónde viene el porcentaje y cuántos días laborables quedan.

![El avance de una tarea en Propiedades](shots/planning-progress-task.webp)

### Marcar elementos en obra

**Planificación → Marcar terminado** (MD) y **Marcar iniciado** (MS) marcan los elementos seleccionados en 3D o en una planta, en la fecha de estado (hoy, si no hay ninguna), en cada tarea que los incluye; **Marcar no iniciado** quita las marcas. Las propiedades de un elemento seleccionado muestran su estado en obra con los mismos tres botones y, en cada una de sus tareas, el día en que se terminó o se inició, con un botón para marcarlo terminado solo en esa tarea. Un muro multicapa en una tarea de albañilería y otra de enlucido se marca en ambas, o en una desde su fila.

![Las marcas de un muro en sus propiedades](shots/planning-element-marks.webp)

En obra, desde un teléfono: entra en la sesión compartida del proyecto desde el navegador (**Archivo → Compartir sesión…** en el ordenador que lo tiene abierto). Un invitado que entró solo para mirar puede igualmente marcar elementos y escribir el avance de una tarea: marcar es la única edición abierta a todo invitado. Las fotos y los informes sobre las tareas son de la edición premium.

### La línea base

**Planificación → Guardar línea base** guarda las fechas y la duración de cada tarea tal como están ahora; guardada de nuevo, se sustituye (una sola línea base: varias líneas base y escenarios son de la edición premium). El Gantt la dibuja entonces como una barra gris fina bajo la barra de cada tarea (**Línea base**, en la barra de herramientas, la oculta), añade la columna **Desviación** (los días laborables que se movió el fin: positivo es retraso, la fila marcada en rojo) y dice en su barra cuántas tareas van retrasadas y cuánto se movió el fin. Propiedades da las fechas de la línea base y las desviaciones del comienzo y del fin. **Borrar línea base** la quita.

### Retrasado y adelantado en la simulación

![La simulación en la fecha de estado: las ventanas retrasadas respecto a la línea base](shots/planning-progress-simulation.webp)

Con una línea base, la simulación compara: un elemento sin construir (o sin empezar) un día en que la línea base lo tenía así va **retrasado** (carmesí), y uno en curso o construido antes de que la línea base lo empezara va **adelantado** (verde). Hasta la fecha de estado los elementos quedan en sus días reales: uno marcado terminado se construye el día en que se marcó. **Comparado con la línea base**, en los ajustes de la simulación, lo desactiva; allí se eligen también sus dos colores. Las vistas y los planos a una fecha muestran lo mismo.

### El avance en los archivos

- **Exportar XLSX** añade las columnas **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** y **GlobalId** (el libro está en inglés). Rellénalas en obra y léelas de vuelta con **Planificación → Importar avance…**: las filas se encuentran por GlobalId, EDT o ID, y una *Status date* escrita encima de la tabla fija la fecha de estado. También sirve un CSV.
- **MS Project XML** lleva la fecha de estado, el porcentaje de cada tarea, el comienzo y el fin reales, la duración restante y la línea base (Baseline 0); las marcas de los elementos viajan con los elementos, en *Lapin elements*.
- **IFC** los escribe en el IfcTaskTime de cada tarea (la hora de estado, el comienzo y el fin reales, el tiempo restante, el grado de avance), y la línea base como un segundo IfcWorkSchedule de tipo BASELINE; los campos propios de Lapin (el porcentaje escrito, las marcas) van en `Lapin_Task`. Ambos vuelven a las mismas fechas.
- **Gestionar → Versiones → Comparar** dice, además de los elementos, qué cambió en el cronograma: el fin, los días laborables, la fecha de estado, y las tareas añadidas, eliminadas, movidas (cuántos días laborables), con la duración o el avance cambiados.

## El coste de las tareas

![El Gantt con la columna Coste](shots/planning-cost.webp)

Con un presupuesto (**Costes → Presupuesto…**), el Gantt recibe una columna **Coste**: la parte del presupuesto de cada tarea, de las partidas que valoran sus elementos (la de un resumen es la de sus tareas). La barra de herramientas muestra el **presupuesto** (los costes directos) y el coste **a la fecha** (el coste de cada tarea por su % completado); Propiedades muestra el coste de una tarea y la parte hecha. **Costes → Coste en el tiempo** dibuja la curva S y la tabla mensual (vea [Presupuesto](cost.md)).

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

El coste en el tiempo está en [Presupuesto](cost.md). La nivelación de recursos, el valor ganado, las certificaciones mensuales y varias líneas base y escenarios son de la edición premium.
