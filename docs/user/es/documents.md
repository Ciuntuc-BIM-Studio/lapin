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

Un editor visual para sus propias plantillas llegará más adelante.
