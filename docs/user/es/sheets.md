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

## Imprimir

Los botones **PDF** y **DXF** de la barra de la lámina exportan la lámina. Un PDF está listo para imprimir o enviar. Un DXF se abre en AutoCAD y en otros programas de CAD con sus capas, líneas y sombreados. **Imprimir** envía la lámina a una impresora o plóter a su propio tamaño de papel, a 1:1, en líneas vectoriales: elige el plóter y el papel en el diálogo de impresión. En una impresora más pequeña, elige ajustar a la página.
