---
title: Láminas, PDF y DXF
order: 100
summary: Disponga las vistas en láminas con un cajetín e imprímalas en PDF o DXF.
---

<!-- video sheets -->

## Crear una lámina

Haga clic en **+** junto a **Láminas** en el navegador del proyecto. Para colocar una vista, arrástrela desde el navegador a la lámina, o elíjala en **Colocar vista…** en la barra de la lámina. Cada vista conserva su propia escala y sigue vinculada al modelo, así que la lámina cambia cuando cambia el modelo.

![Una lámina A3 con una planta y una sección](shots/sheet.webp)

Seleccione la lámina para rellenar su **cajetín** en el panel de propiedades: el proyecto, el cliente, quién la dibujó, quién la revisó, la fecha y la revisión. Allí se fijan también su número, el papel y la orientación. Si se deja vacío, el campo del proyecto muestra el nombre del proyecto: el nombre que se le dio, o si no, el nombre de su archivo.

## Lo que muestra una vista

Una vista en la lámina muestra lo que eliges, sin arrastrarla a ciegas:

- **Recórtala en la lámina.** Haz clic en una vista: aparecen tiradores en sus esquinas y bordes. Arrástralos y el marco abarca más o menos de la vista, mientras el dibujo se queda donde está en el papel. Con **Mayús**-arrastrar dentro desplazas el dibujo en su marco.
- **Dibuja su límite en la vista.** **Annotate → View boundary** (<kbd>VB</kbd>): dibuja un rectángulo en una planta, sección o alzado y dale un nombre, único en el proyecto. En **Place view…** la vista aparece entonces con cada uno de sus límites («Ground floor plan › Ala este»), y una vista colocada así muestra exactamente ese rectángulo y lo sigue cuando lo mueves o cambias de tamaño en la vista. La misma vista puede ir en varias láminas, un límite en cada una. Una vista colocada cambia de límite, o a recorte libre, en la lista de su marco. Los límites se ven solo en pantalla, nunca en papel.
- **Fíjala.** La chincheta del marco la mantiene en su sitio: se selecciona, pero no se mueve, desplaza ni recorta por error.

El marco y el cajetín son del papel. Nunca se seleccionan ni se mueven, y un clic toma la vista más pequeña bajo el puntero, así que una vista colocada sobre el cajetín siempre se puede seleccionar.

![Una vista seleccionada en la lámina: sus tiradores de recorte, su límite, la chincheta](shots/sheet-view-boundary.webp)

## Tu propio cajetín

**Gestionar → Cajetines…** muestra los cajetines: los de Lapin (**Lapin standard**, el que tenían todos los planos hasta ahora, el **cartuș** rumano y uno **ISO 7200**), los del proyecto y los de la biblioteca de este equipo, que se ofrecen en todos sus proyectos. Haz clic en uno para verlo con los valores del proyecto. **Duplicar en el proyecto** crea una copia editable y **Nuevo** uno vacío; **Hacer predeterminado del proyecto** lo da a todos los planos que no eligen el suyo, y un plano elige el suyo en **Cajetín** de sus propiedades.

![Cajetines: los de Lapin, los del proyecto y los de este equipo, el elegido con los valores del proyecto](shots/title-blocks.webp)

**Editar…** abre el diseñador. El cajetín es un recuadro en milímetros de papel que se sitúa en una esquina del marco (abajo a la derecha por defecto), así que el mismo cajetín sirve de A4 a A0, vertical u horizontal; los márgenes del marco se ajustan aquí. Dibuja con **Línea**, **Rectángulo**, **Polilínea**, **Texto**, **Imagen** (tu logotipo, PNG o JPEG) y **Tabla de revisiones**; un rectángulo o una polilínea cerrada admite relleno o sombreado, y un texto una altura o un estilo de texto de los Estándares de dibujo. Los clics se ajustan a los extremos, esquinas y puntos medios y a la rejilla; **Mayús** mantiene la línea recta. Arrastra un elemento para moverlo, sus tiradores para cambiar su forma; las flechas lo desplazan, **Supr** lo elimina y **Ctrl+Z** deshace.

La lista **Campos** contiene el texto vivo: del proyecto (nombre, número, cliente, emplazamiento, licencia, proyectista, personas y tus propios parámetros), del plano (número, nombre, escala, papel, revisión, estado, fecha, dibujado, revisado y aprobado por, fase, plano x de y) y del sistema (fecha de impresión, nombre del archivo). Un clic pone el campo en el texto seleccionado o lo coloca como texto nuevo; un texto puede mezclar palabras y campos, `Proyecto: {project.name}`. El diseñador muestra el valor de cada campo en un plano de muestra; un campo sin valor aparece con su nombre, en gris, y uno que Lapin no conoce, en rojo (se imprime como `<campo>` y **Gestionar → Comprobar** lo indica). **Guardar** conserva el cajetín como un solo paso de deshacer.

![El diseñador: un cajetín de estilo finlandés, sus campos con sus valores, un campo añadido bajo el número del plano](shots/title-block-designer.webp)

Los planos, su PDF, Imprimir y la emisión lo dibujan con los valores de cada plano. En DXF es un bloque con atributos, uno por texto vivo (`PROJECT_NAME`, `SHEET_NUMBER`…), como los cajetines de AutoCAD. **Exportar…** guarda un cajetín como archivo `.json`, con sus imágenes, e **Importar…** lo lee en el proyecto o en la biblioteca, para que una oficina lleve sus cajetines de un equipo a otro. Los proyectos antiguos conservan Lapin standard y se dibujan igual que antes.

![El plano con su propio cajetín](shots/sheet-title-block.webp)

## Imprimir

Los botones **PDF** y **DXF** de la barra de la lámina exportan la lámina. Un PDF está listo para imprimir o enviar. Un DXF se abre en AutoCAD y en otros programas de CAD con sus capas, líneas y sombreados. **Imprimir** envía la lámina a una impresora o plóter a su propio tamaño de papel, a 1:1, en líneas vectoriales: elige el plóter y el papel en el diálogo de impresión. En una impresora más pequeña, elige ajustar a la página.
