---
title: Documentos
order: 105
summary: Las piezas escritas del proyecto — la memoria técnica, el pliego de condiciones, la relación de planos — hechas desde plantillas, rellenadas desde el modelo y exportadas a DOCX, ODT y PDF sin ningún otro programa.
---

## Qué es un documento

Un proyecto es algo más que sus planos: tiene también sus piezas escritas, una memoria técnica, un pliego de condiciones, una relación de planos. En Lapin son **documentos**: cada uno parte de una plantilla y se rellena desde el proyecto — sus datos, sus superficies, sus plantas y habitaciones, sus tipos de muro, sus tablas y sus láminas. Lo que solo usted sabe (la estructura, la seguridad contra incendios, la organización de la obra) lo escribe en las entradas del documento. Un documento sigue al modelo: cambie un tipo de muro o añada una habitación, y el documento lo dirá la próxima vez que lo abra.

Lapin escribe los archivos él mismo, en DOCX, ODT y PDF, sin Word ni LibreOffice en el ordenador, en la aplicación de escritorio y en el navegador.

## Información del proyecto

**Gestionar → Información del proyecto…** guarda lo que los documentos piden al proyecto: su nombre, número y fase, el cliente y su dirección, la dirección del emplazamiento, el número catastral y el registro de la propiedad, el proyectista y su dirección, el arquitecto jefe, quién dibujó y quién revisó. Escríbalos una vez: todos los documentos los leen, y los cajetines de las láminas toman de aquí quién dibujó y quién revisó cuando una lámina no lo dice. Cada campo se guarda al salir de él.

![Información del proyecto: el número, la fase, el cliente, el emplazamiento y el proyectista](shots/project-info.webp)

Los mismos campos están en la pestaña **Información del proyecto** del diálogo Documentos.

## Hacer un documento

**Gestionar → Documentos…** (o el **+** junto a **Documentos** en el navegador del proyecto) abre los documentos del proyecto. A la izquierda están los documentos ya hechos y, bajo **Nuevo desde plantilla**, las plantillas; haga clic en una y el nuevo documento se crea y se rellena al instante.

![Documentos: los documentos del proyecto y las plantillas, el documento en sus páginas, sus entradas y lo que falta por completar](shots/documents.webp)

En el centro está el documento tal como se imprimirá, página tras página, con su índice, sus tablas y sus cifras. Un campo que el proyecto aún no tiene queda marcado donde está. El navegador del proyecto lista los documentos bajo **Documentos**; un clic abre el diálogo en uno.

## Entradas

La pestaña **Entradas** tiene los textos que solo el proyectista puede escribir, agrupados como los capítulos del documento. En las plantillas rumanas cada uno empieza con una indicación marcada **[De completat]** (por completar) que dice qué va ahí. Escriba encima: la vista previa sigue un momento después de que deje de escribir. **Restablecer valor predeterminado** devuelve el texto de la plantilla.

![Entradas: la estructura escrita sobre la indicación de la plantilla](shots/documents-inputs.webp)

## Por completar

**Por completar** lista lo que aún le falta al documento: un campo del proyecto vacío, un valor que el modelo no tiene. Haga clic en uno para ver dónde está en el documento e ir al campo que lo rellena (un campo del proyecto abre la pestaña **Información del proyecto**). La lista se vacía a medida que la rellena.

![Por completar: los campos que aún le faltan al documento](shots/documents-missing.webp)

## Actualizar, renombrar, copiar

- **Actualizar desde la plantilla** toma de nuevo el texto y la composición de la plantilla, para un documento hecho antes de que la plantilla cambiara. Sus entradas se conservan.
- **Renombrar**, **Duplicar** y **Eliminar** actúan sobre el documento mostrado. Una eliminación es un solo paso de deshacer.

## Editar un documento

**Editar**, abajo en el diálogo Documentos, convierte la página del centro en un editor del texto propio del documento; **Vista previa** lo muestra de nuevo en sus páginas. Lo que cambie pertenece solo a este documento: la plantilla queda como estaba (**Guardar como plantilla…**, más abajo, guarda sus cambios como plantilla).

![Editar un documento: los campos como etiquetas con sus valores, una tabla del proyecto seleccionada y sus ajustes a la derecha](shots/documents-editor.webp)

- **La barra de herramientas** tiene los estilos del texto: **Párrafo**, **Título 1**–**3**, negrita, cursiva, listas con viñetas y numeradas, deshacer y rehacer. También funcionan las teclas habituales (Ctrl+B, Ctrl+I, Ctrl+Z dentro del editor).
- **Los campos** aparecen como etiquetas con su nombre y su valor actual, por ejemplo el cliente, *Popescu Ion și Maria*. Un campo que el proyecto aún no rellena aparece en naranja.
- **Los bloques del proyecto** — una tabla, una imagen, el índice — se dibujan con su contenido real. Haga clic en uno para seleccionarlo: sus ajustes aparecen a la derecha, en lugar de las pestañas.

El guardado es automático: un momento después de dejar de escribir y al volver a **Vista previa**; cada cambio es un paso de deshacer del proyecto.

### Insertar

**Insertar** coloca en el cursor:

- **Campo…**: un campo del proyecto, buscado por su nombre; la lista muestra el valor de cada uno. Dentro de una repetición se ofrecen también los campos de sus elementos (el nombre de una planta, la superficie de una habitación).

  ![Insertar → Campo…: «client» buscado, los campos con sus valores](shots/documents-insert-field.webp)

- **Tabla del proyecto…**: una tabla de planificación o una lista del proyecto (la de carpintería, las habitaciones, los materiales…). En sus ajustes: el **Origen**, el **Título**, las columnas que se muestran y la fila de total.
- **Imagen de una vista…**: una planta, sección, alzado o vista 3D. En sus ajustes: **Escala 1:** o **Ancho (mm)** (nunca más ancha que el texto) y una **Leyenda**.
- **Campos clave–valor**: una tabla de dos columnas con etiquetas y valores; un valor puede contener campos, como `{{project.name}}`.
- **Repetir para…**: el texto de dentro se escribe una vez por cada elemento de una colección: las plantas, las habitaciones, las cubiertas, los materiales, las láminas, los documentos… Una banda encima dice **Para cada** y cuántos elementos hay; sus flechas **‹ ›** cambian el elemento que el editor muestra mientras escribe. En sus ajustes, **Solo los elementos donde** conserva algunos, y **Texto cuando no hay ninguno** añade lo que se escribe cuando no hay ninguno.
- **Condición…**: el texto de dentro se escribe solo cuando un campo tiene valor (o, marcado, solo cuando está vacío). **Si no: texto cuando no se cumple** añade una segunda parte, escrita en su lugar.
- **Texto por completar…**: una entrada nueva, con su etiqueta, grupo y texto predeterminado; su texto se escribe en la pestaña **Entradas**, en la vista previa.
- **Índice** y **Salto de página**.

![Una repetición sobre las plantas, una condición dentro y las habitaciones de la planta mostrada, cambiada con ‹ ›](shots/documents-repeat.webp)

En los ajustes de un bloque, **Quitar** lo elimina; en una repetición o una condición, **Conservar solo el contenido** quita la banda y conserva lo que hay dentro.

## Sus plantillas

**Guardar como plantilla…** guarda el texto, la composición, las entradas y el estilo del documento como plantilla. Dele un título y una descripción y elija dónde guardarla:

- **Este proyecto**: va con el archivo del proyecto, a quien lo abra.
- **La biblioteca de este ordenador**: la ofrece cualquier proyecto de este ordenador.
- **Ambos**.

**Guardar como** puede sustituir una plantilla guardada antes; los documentos ya hechos con ella conservan su texto hasta **Actualizar desde la plantilla**. La plantilla nueva aparece enseguida en **Nuevo desde plantilla**.

![Guardar como plantilla: el título, la descripción y dónde se guarda](shots/documents-save-template.webp)

**Gestionar → Plantillas de documentos…** muestra las plantillas en tres grupos: **Integradas** (las de Lapin), **Este proyecto** y **Este ordenador**. Para la elegida:

- **Renombrar** y **Eliminar** (eliminar una plantilla del proyecto es un paso de deshacer; los documentos hechos con ella conservan su texto).
- **Duplicar en el proyecto** o **Duplicar en la biblioteca**: una plantilla integrada solo se cambia a través de una copia.
- **Copiar a la biblioteca** o **Copiar al proyecto**, para pasarla de un sitio a otro.
- **Exportar…** escribe la plantilla como archivo `.json`; **Importar…** lee una en este proyecto o en la biblioteca de este ordenador. Así comparten los compañeros las plantillas de una empresa.

![Plantillas de documentos: las integradas, las del proyecto y las del ordenador, la elegida con su estilo](shots/document-templates.webp)

## El estilo de la empresa

Un documento, y una plantilla del proyecto o de la biblioteca, tiene su propio estilo: la pestaña **Estilo** del diálogo Documentos para el documento mostrado, la parte **Estilo** de Gestionar → Plantillas de documentos… para una plantilla. Las plantillas integradas conservan el aspecto de Lapin: duplique una para darle el suyo.

- **Fuente (DOCX, ODT)**: Arial (la de Lapin), Calibri, Times New Roman, Georgia, Verdana o Liberation Sans.
- **Fuente del PDF**: Helvetica o Times, las fuentes propias del PDF; por defecto la más parecida a la fuente de arriba.
- **Tamaño del texto (pt)**: el tamaño del texto normal, de 6 a 24 pt.
- **Color de los títulos**.
- **Logotipo en el encabezado**: **Elegir el logotipo…** toma un PNG o JPEG (menos de 1 MB); fije su **Ancho (mm)** y si va **A la izquierda** o **A la derecha**. Se escribe igual en DOCX, ODT y PDF.

Una muestra bajo los campos enseña la parte superior de la página como la mostrarán los archivos. **El aspecto propio de Lapin** quita el estilo. Cada cambio es un paso de deshacer.

![La pestaña Estilo: el color de los títulos y un logotipo en el encabezado, con la muestra](shots/documents-style.webp)

## Exportar

- **Exportar DOCX** y **Exportar ODT** escriben un archivo que puede abrir y editar en Word o LibreOffice, con sus títulos como estilos propios del programa, su índice y la numeración de páginas como campos, y las imágenes del proyecto.
- **Exportar PDF** escribe el documento compuesto por Lapin, listo para imprimir o enviar.

Un campo aún vacío queda en blanco en los archivos, así que revise **Por completar** antes de exportar.

## Emitir documentos

**Gestionar → Emitir…** lleva los documentos con las láminas y el modelo (vea [Carpeta del proyecto y emisiones](cde.md)). Bajo **Documentos, cada uno como PDF:** cada documento está marcado de entrada; marque **También como DOCX** para tener además una copia editable. Cada archivo se nombra según la convención ISO 19650 (`RP` para una memoria, `SP` para un pliego), se calcula su hash y figura en la hoja de transmisión, y el documento toma la revisión y el estado de la emisión.

![Emitir: los documentos con las láminas y el modelo](shots/issue-documents.webp)

## Las plantillas

Lapin trae tres plantillas, por ahora en rumano:

- **Memoriu tehnic de arhitectură** (la memoria técnica de arquitectura, según la Ley 50/1991 rumana): los datos generales de Información del proyecto; el número de plantas (por ejemplo `P+1E`) y la altura máxima; las superficies — construida, total, útil y del sótano — en total y por planta; las habitaciones de cada planta con sus áreas; los muros por tipo, con sus capas; los forjados y las cubiertas; la tabla de carpintería; los acabados de las habitaciones; entradas para el estado actual, el uso, la estructura, las fachadas, las instalaciones, la seguridad contra incendios, la accesibilidad, la eficiencia energética y la organización de la obra.
- **Caiet de sarcini: tâmplărie și finisaje** (el pliego de carpintería y acabados): los materiales del proyecto por categoría, la tabla de carpintería con sus cantidades, los acabados habitación por habitación, y entradas para las generalidades, los requisitos, la ejecución y la recepción, con textos por defecto para adaptar.
- **Borderou de piese scrise și desenate** (la relación de piezas escritas y dibujadas): los documentos y las láminas del proyecto, con su número, título, escala y revisión.

Parta de una de ellas, cámbiela en el editor y luego **Guardar como plantilla…** para tener la suya.

## Para asistentes de IA

Un asistente conectado mediante el puente MCP de Lapin puede crear un documento, escribir sus entradas y exportarlo con `lapin_document_export`: vea [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (en inglés).
