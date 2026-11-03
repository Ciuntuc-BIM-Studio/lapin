---
title: Losas y cubiertas
order: 40
summary: Dibuje losas y cubiertas por su contorno, elija la forma y los bordes de una cubierta, y abra huecos.
---

## Losas

Elija **Arquitectura → Losa** (<kbd>SB</kbd>) y haga clic en las esquinas de su contorno. Cierre el contorno en el primer punto o pulse <kbd>Enter</kbd>. Una losa también está hecha de capas, que se definen en **Arquitectura → Tipos de losa**.

Para cambiar el contorno, seleccione la losa y arrastre sus esquinas. **Hueco en losa** (<kbd>SH</kbd>) abre un hueco en una losa, para una escalera o un patinillo.

Una losa dibujada fuera de los muros, delante de una puerta, es un porche. Para los peldaños que suben a él, consulte [Escaleras](stairs.md).

## Cubiertas

Elija **Arquitectura → Cubierta** (<kbd>RF</kbd>) y haga clic en el contorno de los muros que quedan debajo. Lapin construye la cubierta sobre ese contorno, en la parte superior de su nivel.

![Una casa con cubierta a dos aguas en la vista 3D](shots/house-3d.webp)

Seleccione la cubierta para cambiarla en el panel de propiedades:

- **Forma.** A cuatro aguas, a dos aguas, a un agua, mansarda, bóveda o plana. Una cubierta abovedada es una bóveda curva entre dos hastiales; **Bóveda: flecha en la clave** fija cuánto sube en la clave, como máximo la mitad de su luz.
- **Pendiente y vuelo** de toda la cubierta.
- **Dirección de la cumbrera (a dos aguas, a un agua)**: auto, x o y. En una cubierta a un agua la cumbrera es el lado alto: en x cae hacia −y, en y hacia −x; **A un agua: invertir la pendiente** la hace caer al otro lado.
- **Cada borde** con su propio tipo (alero o hastial), pendiente y vuelo. Una mansarda con bordes de hastial es una cubierta holandesa.
- **Estilo.** Los estilos de cubierta dan un aspecto completo en una sola elección: el material de cobertura, los aleros, los canalones y las bajantes.

![Las propiedades de una cubierta](shots/roof-properties.webp)

**Configurador de cubierta** (el botón junto al estilo, o **Architecture → Roof configurator** con una cubierta seleccionada) lo reúne todo: los estilos a la izquierda, luego la forma y sus bordes, la cobertura y el desagüe, la estructura y los huecos, sobre una planta de la cubierta (un clic en un borde lo vuelve auto, alero o hastial) y, para la estructura, una sección transversal a la cumbrera con la cercha o la pareja del centro. Parte de un estilo y cambia a mano cualquier campo; **Apply** lo deja en un solo paso de deshacer. **Guardar como estilo…** guarda la cubierta tal como está configurada como estilo del proyecto, listado en **Project** sobre los del catálogo (y en la lista del panel de propiedades) para otras cubiertas y las nuevas; **Actualizar el estilo** vuelve a guardar en él los cambios, y todas las cubiertas del estilo los siguen. Un estilo del proyecto se puede renombrar o borrar; sus cubiertas quedan como están.

![El configurador de cubierta](shots/roof-configurator.webp)

**Canalones y bajantes.** En **Cobertura y desagüe** del configurador el canalón toma su tamaño, cuánto sale del alero y cuánto baja bajo la cobertura, su pendiente hacia las salidas (mm/m) y sus tapas; la tabla de bordes activa o desactiva el canalón de cada alero. Las bajantes empiezan siendo de la cubierta, una cada 10 m; **Hacerlas editables** las vuelve elementos, y **Architecture → Downpipe** añade una con un clic en un canalón. El tirador de una bajante la desliza solo a lo largo de su canalón; sus propiedades fijan el diámetro, el codo de salida y su recorrido, una cadena de tramos con su longitud y su ángulo desde la vertical (hacia el muro), el último hasta el suelo. En IFC el canalón es un IfcPipeSegment GUTTER y la bajante un segmento rígido llamado Downpipe.

Los muros del nivel de la cubierta se detienen bajo ella y suben por sí solos hasta sus hastiales, también bajo una cubierta curva. Un muro de otro nivel sigue la cubierta tras **Arquitectura → Enlazar a cubierta** (<kbd>AR</kbd>): seleccione los muros y haga clic en la cubierta. La misma herramienta acepta cualquier otro cuerpo sobre los muros: un sólido de Studio, una losa, una viga, un elemento importado de IFC. Los muros suben hasta su cara inferior y se cortan a lo largo de ella, sea cual sea su forma. **Desenlazar de la cubierta** lo deshace.

**Ventana de cubierta** y **Buhardilla** añaden huecos a una cubierta: haga clic en un faldón. Pertenecen a la cubierta. Seleccione la cubierta, y cada una muestra un pinzamiento en planta en su centro: arrastre el pinzamiento para moverla sobre la cubierta (se rechaza donde no cabría), o haga Ctrl/⌘-clic en él para quitarla. Las propiedades de la cubierta también las listan, cada una con **×** para quitarla, y el configurador de cubiertas fija su tamaño.

![El pinzamiento de una ventana de cubierta en la cubierta seleccionada](shots/roof-window-grip.webp)

### Estructura de madera

**Estructura** fija cómo se estructura la cubierta:

- **rafters**: cabios sobre correas, con viga cumbrera.
- **collar**: parejas de cabios con un tirante alto horizontal en **Tirantes altos: fracción de la altura** (dos tercios por defecto), sin correas.
- **trusses**: cerchas prefabricadas a lo largo de la cumbrera principal, a la **Separación de cabios**: cordones superiores en los faldones, un cordón inferior sobre las carreras, diagonales en W (cercha de un agua en una cubierta a un agua). Los extremos de una cubierta a cuatro aguas conservan sus cabios.
- **none**: un tablero, sin madera.

**Cabios y cordones** admite las secciones antiguas en cm o las nórdicas en mm (45×95 a 45×245); con ellas van **Montantes y diagonales de las cerchas** y **Clase resistente de la madera** (C14 a C30, laminada). Los cabios apoyan en la carrera con un rebaje (**Apoyo de los cabios en la carrera**) y terminan en el alero a plomo o a escuadra (**Remate de los cabios en el alero**). Los estilos **Swedish truss roof** y **Nordic collar-tie roof** traen su estructura.

Cada pieza es una parte propia: la tabla **Roof timber** las lista como despiece (pieza, sección, clase, longitud, unidades, volumen), y en IFC cada una es un IfcMember de su tipo, cada cercha un IfcElementAssembly.

## Falsos techos

**Acabados → Falso techo** (<kbd>CG</kbd>) dibuja un falso techo sobre un contorno que se marca con clics; **Falso techo en un local** llena el local donde se hace clic, siguiendo su contorno. Un falso techo cuelga a su **Altura** sobre la planta (2700 mm salvo que se cambie en sus propiedades).

Su tipo, en **Acabados → Tipos de falso techo**, fija el **Sistema**: **Placas sobre perfilería vista** (ancho, largo y espesor de la placa, ancho visto, altura y giro de la perfilería, cuelgues hasta la estructura con su separación) o **Placa de yeso, continuo**. La perfilería se centra en el techo, así que las placas cortadas en los bordes salen iguales.

**Panel de luz** y **Difusor de aire** ponen uno en la celda donde se hace clic; un clic en la misma celda con la otra herramienta lo cambia. En el panel de propiedades, **Distribuir** pone paneles de luz o difusores cada tantas celdas, y **Vaciar** los quita. Las tablas cuentan las placas enteras, los paneles de luz y los difusores, y el techo va a IFC como IfcCovering de tipo CEILING.

![Las propiedades de un falso techo: su tipo, su altura y sus elementos](shots/ceiling-properties.webp)

![Falsos techos registrables con paneles de luz y difusores, en 3D](shots/ceilings-3d.webp)
