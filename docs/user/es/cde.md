---
title: Carpeta del proyecto y emisiones
order: 125
summary: Emita láminas y el modelo como pide ISO 19650, guardados en el archivo del proyecto y, si tiene una, en una carpeta del proyecto, con nombres según la convención, con una transmisión, tras las comprobaciones que hace una oficina técnica.
---

## La carpeta del proyecto

**Manage → Project folder** fija la carpeta en la que el proyecto también se emite: una carpeta de su ordenador, una unidad de red o una carpeta de SharePoint u OneDrive sincronizada en el disco. La carpeta es **opcional**: cada emisión y sus archivos se guardan en el propio archivo del proyecto, así que un proyecto sin carpeta emite igual, en la aplicación de escritorio y en el navegador. Lapin no pide ninguna cuenta ni habla con ningún servidor: escribe archivos, y lo que sincroniza la carpeta es cosa de su oficina. La ruta se guarda en el proyecto, así que viaja con el archivo; una carpeta que no está en este ordenador es un aviso, no un error. Elegir la carpeta requiere la aplicación de escritorio.

![Carpeta del proyecto: la carpeta, los contenedores, la nomenclatura](shots/project-folder.webp)

Dentro de la carpeta Lapin crea, en la primera emisión, los cuatro contenedores ISO 19650: **WIP**, **Shared**, **Published** y **Archived**. El código de estado sugiere el contenedor: S0 va a WIP, S1–S7 a Shared, A, B y CR a Published.

## Nomenclatura

Los archivos se nombran según ISO 19650-2: **Proyecto-Originador-Volumen-Nivel-Tipo-Rol-Número**, luego el estado y la revisión, por ejemplo `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. En el diálogo Project folder fija el código del proyecto, el originador (su oficina), el volumen y el nivel y rol predeterminados. El resto viene del proyecto:

- el **nivel** de una lámina es la planta que dibujan sus planos (`00` en ±0,00, `01` arriba, `B1` abajo, `ZZ` cuando dibuja varias o ninguna);
- su **rol** es el de su disciplina (arquitectura A, estructura S, MEP M, civil C);
- su **número** son las cifras del número de lámina (`A-101` pasa a `0101`);
- el modelo es `M3`, el albarán `IE`.

## Comprobaciones

**Manage → Check** ejecuta, sobre todo el proyecto, las reglas que una oficina de proyectos comprueba a ojo antes de una emisión. Un hallazgo **bloqueante** detiene la emisión; un **aviso** puede pasarse por alto. Pulse un hallazgo para ir a la lámina, la vista o el elemento.

![Comprobación: los hallazgos por regla](shots/cde-check.webp)

Bloqueantes: un cajetín sin número, sin título o sin quién lo dibujó; una vista sin escala; una vista fuera del marco o sobre el cajetín; una lámina que cambió desde su última emisión pero sigue en esa revisión; un número de lámina que no da número de archivo. Avisos: quién comprobó, la fecha y la fase vacías; un plano coloreado por un esquema de habitaciones, o con muchas tramas, sin leyenda; un plano, una sección o un alzado en ninguna lámina; un plano de situación sin flecha del norte; elementos sin planta, sin tipo o sin material; puertas y ventanas sin etiqueta; habitaciones sin número; las propiedades IFC obligatorias (IsExternal, LoadBearing, FireRating en puertas) vacías.

## Emitir

**Manage → Issue…** hace una emisión, guardada en el archivo del proyecto y, cuando el proyecto tiene una carpeta en este ordenador, escrita también en su contenedor:

![Emisión: las láminas, la revisión, el estado, las comprobaciones](shots/cde-issue.webp)

1. Dé un nombre a la emisión, su **revisión** (`P01`, `P02`… mientras es preliminar, `C01`… una vez contractual; la siguiente se propone a partir de la última emisión), su **estado** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) y el **contenedor**.
2. Marque las láminas que van, el modelo como IFC, cada lámina también como DXF, o todas las láminas también en un solo PDF. El nombre de cada archivo se muestra tal como se escribirá. El IFC se guarda en el archivo del proyecto solo si marca **Guardar el IFC en el archivo del proyecto** (un modelo grande ocupa decenas de MB por emisión); sin marcar, va a la carpeta del proyecto y el archivo del proyecto guarda su nombre y su hash. **También a la carpeta del proyecto** escribe los archivos también en el contenedor (marcado por defecto cuando el proyecto tiene carpeta).
3. Las comprobaciones se ejecutan mientras elige. Con un hallazgo bloqueante la emisión no puede salir; solo con avisos, marque **Emitir de todos modos**.
4. **Emitir** hace cada lámina como PDF, el IFC y lo demás, con nombres según la convención, calcula el hash (SHA-256) de cada archivo, hace la **transmisión** (un PDF y un CSV: proyecto, emisión, fecha, revisión, estado, los archivos) y un **manifiesto** (JSON, con todos los hashes), los guarda todos en el archivo del proyecto y registra la emisión en el proyecto. Cada lámina impresa lleva en su cajetín la revisión y el estado de la emisión, y en su tabla de revisiones la fila de la emisión, tal como se emitió.
5. En el mismo paso el proyecto se guarda como una **versión** con el nombre de la emisión (vea [Archivos, versiones y recuperación](files.md)), para que una emisión pueda reabrirse, restaurarse o compararse con el proyecto tal como está ahora, y el proyecto queda guardado, sin cambios pendientes. El diálogo muestra entonces la emisión tal como salió —su nombre, revisión y archivos, cada uno para abrir— hasta que lo cierre o empiece una **Nueva emisión**.

![El diálogo Emitir tras una emisión: la emisión tal como salió, sus archivos](shots/cde-issued.webp)

## Las emisiones en el archivo del proyecto

Las emisiones se listan al pie del diálogo Project folder, con lo que ocupan en el archivo del proyecto (los archivos idénticos se guardan una sola vez, por muchas emisiones que los compartan). Haga clic en una emisión para ver sus archivos:

![Las emisiones en el archivo del proyecto: lo que ocupan, una emisión abierta con sus archivos](shots/cde-issues.webp)

- un clic en un archivo lo **abre** desde el archivo del proyecto (el escritorio lo abre con su programa; el navegador lo descarga), o **Guardar una copia…**;
- **Descargar como zip** da todos los archivos guardados de una emisión en un zip, con el nombre de su transmisión: lo que envía cuando no hay una carpeta compartida;
- **Publicar en carpeta…** (la aplicación de escritorio) escribe una emisión hecha antes en el contenedor de la carpeta del proyecto, o en cualquier carpeta que elija: emita primero, elija la carpeta después;
- **Borrar los archivos guardados** saca los archivos de una emisión antigua del archivo del proyecto al guardar, para que el archivo siga siendo pequeño; el registro de la emisión, los nombres de sus archivos y sus hashes se quedan;
- **Abrir carpeta** muestra el contenedor en el que se escribió la emisión.

## En el navegador

El CDE funciona también en el navegador: Comprobación, Emisión, los archivos de las emisiones (abrir, Descargar como zip) y los dos tipos de observaciones. El archivo del proyecto lo contiene todo y vuelve a usted como el archivo que guarda. Una carpeta del proyecto, publicar en ella y explorarla requieren la aplicación de escritorio.

## Observaciones (BCF)

**Manage → Observaciones en el modelo…** guarda las observaciones hechas sobre el modelo —las tuyas y las de un revisor— como temas BCF (el BIM Collaboration Format que intercambian BIMcollab, Solibri, Navisworks y Revit), en el proyecto, nunca en el IFC. Una observación tiene un título, una descripción, un tipo, un estado (Open, In progress, Resolved, Closed), una prioridad, un responsable, una fecha límite y etiquetas; sus comentarios; y sus puntos de vista: la cámara 3D, los elementos seleccionados, la caja de sección como planos de corte y una captura.

![Observaciones: la lista con sus filtros, una observación con su captura y sus comentarios](shots/remarks.webp)

- **Nueva observación** crea una desde la vista 3D de delante: la cámara tal como está, los elementos seleccionados, la caja de sección y una imagen tomada de la vista. Rellena los campos y **Crear**.
- La lista se filtra por un texto, un estado, una prioridad, el responsable y **Solo abiertas**. Elige una observación para ver y cambiar sus campos, leer sus comentarios y responder en la caja (**Comentar**; marca **Con la vista 3D** para adjuntar la vista tal como está a la respuesta).
- **Mostrar en 3D** abre el punto de vista de la observación: la vista 3D va a su cámara, sus elementos quedan seleccionados (los que el proyecto tiene, por su GlobalId IFC) y sus planos de corte pasan a ser la caja de sección. Un comentario con vista propia tiene **Mostrar** al lado.
- **Volver a tomar de la vista 3D** sustituye el punto de vista de la observación por la vista tal como está ahora.
- **Importar BCF…** lee un archivo `.bcfzip` (BCF 2.1) o `.bcf` (BCF 3.0): un tema que el proyecto ya tiene (el mismo GUID) se actualiza —ganan los campos del archivo, y los comentarios hechos aquí que el archivo no tiene se quedan—, los demás se añaden. **Exportar todas**, **Exportar abiertas** o **Exportar seleccionada** escriben un archivo BCF, 2.1 salvo que elijas 3.0, para entregar al revisor o dejar en el contenedor Shared de la carpeta del proyecto. Los temas conservan sus GUID, así que un viaje de ida y vuelta por otra herramienta vuelve a las mismas observaciones.

![Una observación abierta en su punto de vista en la vista 3D](shots/remark-viewpoint.webp)

Tu nombre en lo que escribes se pone en la caja **Tu nombre** del diálogo (se guarda en este ordenador).
## Observaciones en las láminas

Un revisor abre un PDF emitido en Acrobat, Bluebeam, Preview o Drawboard y lo marca: notas adhesivas, texto, resaltados y tachados, trazos a lápiz, rectángulos, círculos, flechas, nubes, sellos, mediciones, respuestas. **Manage → Observaciones en los planos…** lee esas marcas de vuelta al proyecto como observaciones en la lámina:

![Observaciones: la lista por lámina, un PDF leído](shots/cde-remarks.webp)

- **Leer observaciones del PDF…** elige el archivo. La lámina a la que pertenece viene de la emisión (el manifiesto junto al archivo, o el registro de emisiones del propio proyecto), si no, de la convención del nombre del archivo; cuando nada encaja, usted elige la lámina. Las páginas de un PDF de conjunto van a sus láminas en orden. Se muestra lo encontrado — páginas, anotaciones, cómo se emparejó la lámina, cualquier aviso — e **Importar** trae las observaciones. Leer el mismo archivo otra vez actualiza sus observaciones en lugar de duplicarlas; una observación que usted resolvió sigue resuelta. Un archivo renombrado por el camino se empareja igual: los bytes emitidos son aquellos con los que empieza el archivo del revisor.
- **Explorar la carpeta del proyecto** (la aplicación de escritorio) lista los PDF emitidos en Shared y Published, cuáles cambiaron después de su emisión y si sus observaciones se leyeron desde entonces.
- La lista se agrupa por lámina y se filtra por estado: **abiertas**, **resueltas**, **cerradas**. Un clic abre la lámina con la observación delante. Cada observación puede resolverse, cerrarse, reabrirse o eliminarse; el estado «Completed» o «Accepted» del revisor llega como resuelta.

![Una lámina con sus observaciones encima](shots/cde-remarks-sheet.webp)

En la lámina, las observaciones se dibujan sobre el papel en su sitio: el contorno en el color de revisión, un marcador con las iniciales del autor, el texto en una descripción emergente; la que está delante tiene su tarjeta con el texto, quién y cuándo, las respuestas y el estado. El botón de la barra de la lámina las oculta o las muestra, y el navegador del proyecto cuenta las abiertas junto a la lámina. Son una capa solo para la pantalla: una lámina se imprime y se emite sin sus observaciones.

Lapin lee el PDF por sí mismo, sin otro programa: archivos clásicos y archivos guardados con flujos de referencias cruzadas, guardados incrementales (lo que añade el programa del revisor), texto en cualquier codificación. Un archivo que no puede leer entero da lo que puede, con un aviso. Lapin nunca escribe en el PDF.
