---
title: Láminas, PDF y DXF
order: 100
summary: Disponga las vistas en láminas con un cajetín e imprímalas en PDF o DXF.
---

<!-- video sheets -->

## Crear una lámina

Haga clic en **+** junto a **Láminas** en el navegador del proyecto. Para colocar una vista, arrástrela desde el navegador a la lámina, o elíjala en **Colocar vista…** en la barra de la lámina. Cada vista conserva su propia escala y sigue vinculada al modelo, así que la lámina cambia cuando cambia el modelo.

![Una lámina A3 con una planta y una sección](shots/sheet.webp)

Seleccione la lámina para rellenar su **cajetín** en el panel de propiedades: el proyecto, el cliente, quién la dibujó, quién la revisó, la fecha y la revisión. Allí se fijan también su número, el papel y la orientación. El papel es un formato A (A0 a A4), uno de los formatos alargados ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), un formato norteamericano (Letter, Tabloid, ARCH D, ARCH E) o **personalizado**, con su **Ancho del papel** y **Alto del papel** en milímetros. Si se deja vacío, el campo del proyecto muestra el nombre del proyecto: el nombre que se le dio, o si no, el nombre de su archivo.

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

**Editar…** abre el cajetín en un tablero: una vista de dibujo propia, en milímetros de papel, con el recuadro del cajetín discontinuo. El cajetín es un recuadro en una esquina del marco, así que el mismo sirve de A4 a A0, vertical u horizontal. Se dibuja con las mismas herramientas de **Dibujo** que en cualquier vista (línea, polilínea, rectángulo, círculo, arco, texto, sombreado y las herramientas de edición); los ajustes y los tiradores funcionan como siempre. Los textos conservan sus campos vivos tal como se escribieron, `{project.name}`, `{sheet.number}`. El logotipo es una imagen: **Logotipo o imagen…** en el panel o **Dibujo → Importar imagen**. La tabla de revisiones aparece como su recuadro; muévelo o cambia su tamaño. Un panel al lado tiene el **Nombre**, el ancho y el alto del recuadro (mm), la esquina de la hoja, **Marco alrededor de la hoja**, dónde va la tabla de revisiones y los campos para copiar. **Terminar boceto** escribe el cajetín como un solo paso de deshacer y sus planos lo siguen; sin cambios, queda exactamente como estaba. **Cancelar** descarta la edición.

La lista **Campos** contiene el texto vivo: del proyecto (nombre, número, cliente, emplazamiento, licencia, proyectista, personas y tus propios parámetros), del plano (número, nombre, escala, papel, revisión, estado, fecha, dibujado, revisado y aprobado por, fase, plano x de y) y del sistema (fecha de impresión, nombre del archivo). Un clic copia un campo, para pegarlo en un texto escrito con **Texto**; un texto puede mezclar palabras y campos, `Proyecto: {project.name}`. Un campo que Lapin no conoce se imprime como `<campo>` y **Gestionar → Comprobar** lo indica.

![El cajetín en el tablero: su recuadro discontinuo, el dibujo y el panel con los campos para copiar](shots/title-block-designer.webp)

Los planos, su PDF, Imprimir y la emisión lo dibujan con los valores de cada plano. En DXF es un bloque con atributos, uno por texto vivo (`PROJECT_NAME`, `SHEET_NUMBER`…), como los cajetines de AutoCAD. **Exportar…** guarda un cajetín como archivo `.json`, con sus imágenes, e **Importar…** lo lee en el proyecto o en la biblioteca, para que una oficina lleve sus cajetines de un equipo a otro. Los proyectos antiguos conservan Lapin standard y se dibujan igual que antes.

![El plano con su propio cajetín](shots/sheet-title-block.webp)

## Dibujar en la lámina

Las notas, marcas y leyendas que pertenecen a la lámina y no a una de sus vistas se dibujan en la propia lámina. Pulsa **Anotar** en la barra de la lámina o haz doble clic en el papel fuera de las vistas (**Anotar → Anotar lámina** hace lo mismo). La lámina se abre en el panel de dibujo como su papel, a 1:1, con las vistas, el marco y el cajetín debajo, y la barra dice *dibujo en la lámina*. Todas las herramientas de **Dibujo** y **Anotar** funcionan allí como en una vista, en milímetros de papel: escribe 50 y una línea mide 50 mm en el papel, y la altura de un texto es su altura en el papel. Los puntos se ajustan al marco, al cajetín, a los recuadros de las vistas (azul discontinuo) y a lo que dibujan las vistas. **Lámina** en la barra vuelve a la disposición de las vistas.

- Los **textos** pueden llevar campos vivos, como los de un cajetín: `Lámina {sheet.number} de {sheet.count}`, `{project.name}`, `{sheet.date}`. En una vista, un texto se lee tal como se escribió.
- **Nota con directriz** (Anotar → Texto): haz clic donde apunta la flecha, luego donde va el texto, y escríbelo. Al moverla, la nota se lleva su flecha; **Línea de referencia → quitar** en sus propiedades quita la flecha. Seleccionada, la punta de la flecha y cada quiebro tienen pinzamientos propios: arrastra uno para moverlo solo.
- **Líneas, polilíneas, rectángulos, círculos, arcos, sombreados, símbolos e imágenes** son los de las herramientas de Dibujo, con sus pinzamientos, capas y herramientas de modificación. Una flecha de norte es uno de los símbolos de Lapin (**Anotar → Símbolo**). Los símbolos colocados con **Colocar símbolo…** en la barra de la lámina también se toman aquí con un clic y se mueven en milímetros de papel.
- **Nube de revisión** (Dibujo → Relleno, Anotar → Láminas o REVCLOUD en la línea de comandos): dos esquinas, o **P** para un polígono que se cierra con Intro. **Nube de revisión** en las propiedades de cualquier polilínea cerrada o círculo la convierte en nube o la devuelve a su forma, y fija su arco en el papel.
- **Leyenda** (Anotar → Láminas): una lista viva de lo que dibujan las vistas de la lámina: los materiales con que se sombrean y rellenan sus cortes o, con **Listas** en sus propiedades, sus líneas por función, tipo y grosor. Cambia con las vistas y con el modelo, y su título también admite campos. Sus propias palabras (el encabezado, las funciones y los tipos de las líneas) se escriben en el idioma que tenía la interfaz al colocarla; **Idioma** en sus propiedades las cambia.
- **Escala gráfica** (Anotar → Láminas): a la escala de la primera vista de la lámina o a la que indiquen sus propiedades, con una longitud redonda de unos 80 mm o la que elijas. En una planta, sección o alzado, la escala gráfica toma la escala de la vista.

Lo dibujado en la lámina se imprime en ella, sobre las vistas, tal como se ve: en su PDF, al imprimir y en su DXF. Pertenece a la lámina: **Duplicar la lámina** (su botón en el explorador del proyecto) lo copia, junto con los símbolos colocados en la lámina, y borrar la lámina lo borra. Cada dibujo es un paso de deshacer. Las láminas hechas antes no tienen nada dibujado y se imprimen como antes. Una emisión también lo cuenta: una lámina cambiada solo por lo dibujado en su papel o por sus símbolos se avisa como cambiada desde su última emisión (Gestionar → Comprobar, CDE).

![Dibujo en una lámina: una nota con directriz, una nube de revisión, un texto vivo, una leyenda de los materiales de la planta y una escala gráfica](shots/sheet-annotations.webp)

![La misma lámina, tal como se imprime](shots/sheet-annotated.webp)

## Imprimir

Los botones **PDF** y **DXF** de la barra de la lámina exportan la lámina. Un PDF está listo para imprimir o enviar. Un DXF se abre en AutoCAD y en otros programas de CAD con sus capas, líneas y sombreados; los círculos, arcos y elipses (dibujados, o los bordes curvos de forjados y otros contornos en una planta) se escriben como entidades CIRCLE, ARC y ELLIPSE reales, no como líneas cortas, y vuelven como curvas al importar de nuevo el DXF. **Imprimir** envía la lámina a una impresora o plóter a su propio tamaño de papel, a 1:1, en líneas vectoriales: elige el plóter y el papel en el diálogo de impresión. En una impresora más pequeña, elige ajustar a la página.
