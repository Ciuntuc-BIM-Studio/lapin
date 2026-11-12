---
title: Losas y cubiertas
order: 40
summary: Dibuje losas y cubiertas por su contorno, elija la forma y los bordes de una cubierta, y abra huecos.
---

## Losas

Elija **Arquitectura → Losa** (<kbd>SB</kbd>) y haga clic en las esquinas de su contorno. Cierre el contorno en el primer punto o pulse <kbd>Enter</kbd>. Una losa también está hecha de capas, que se definen en **Arquitectura → Tipos de losa**. Una capa de un tipo de losa puede tener espesor 0 (una impermeabilización, la pintura): no construye nada, se dibuja como una línea en las secciones y se mide por la superficie de la losa.

En **Tipos de losa**, cada capa tiene una **Prioridad** (de 0 a 100) y una **Categoría**, como las capas de un tipo de muro. Una capa de muro que baja a la losa (**En losas** en **Tipos de muro**) atraviesa las capas de la losa de menor prioridad y se detiene sobre la primera de prioridad igual o mayor. Vacía, la prioridad es la del material en **Gestionar → Orden de corte**, si no 50 para el núcleo y 0 para las demás capas. Una capa marcada **Acabado** se va cuando una vista omite los acabados de las losas.

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
- **Material de la cubierta.** La cubierta es de un material del proyecto, por defecto el de la cubierta elegida (Clay tile, Metal sheet, Slate…), creado con el color de la cubierta la primera vez que un tejado lo necesita. Elija otro en las propiedades o en **Cobertura y desagüe** del configurador; al cambiar la cubierta cambia también, salvo que haya elegido uno. Los alzados y las secciones rayan la cubierta según ese material (tejas, ripias y pizarra en hiladas horizontales, chapa en juntas verticales, salvo que le dé un rayado **Elevation** propio), una sección dibuja la cubierta cortada con el rayado de sección del material, y el IFC, la medición y el presupuesto listan la cubierta bajo su material.
- **Espesor de la cubrición.** Vacío, la cubrición es tan gruesa como la del catálogo (una membrana 2 cm, chapa 3 cm, teja 5 cm…); escriba uno para darle el suyo. Una cubierta importada de otro programa conserva el del archivo.
- **Forjado.** Una cubierta plana, una bóveda de cañón y una cubierta con **Structure** none tienen un forjado bajo la cubierta, una parte propia: **Material del forjado** (por defecto Reinforced concrete, creado la primera vez que una cubierta lo necesita) y **Espesor del forjado** (por defecto 16 cm), en las propiedades o en **Estructura** del configurador. La cubierta se apoya en él, así que el tejado sigue tan grueso como los dos juntos. Una sección dibuja dos bandas, cada una sombreada por su material; las ventanas de tejado atraviesan ambas; un muro unido a una cubierta plana sube hasta la cara inferior del forjado; el IFC escribe el forjado y la cubierta como dos losas de cubierta de sus materiales, y la medición y el presupuesto miden el forjado (superficie y volumen) y la cubierta (superficie), cada uno bajo su material. Las propiedades muestran estos dos campos solo en una cubierta sin armadura, y los campos de los cabios (secciones, separación, clase, correas, extremos) solo en una con armadura; un campo propio de una sola forma (el de la mansarda, la flecha de la bóveda, el giro de la cubierta a un agua) aparece solo en esa forma.

![Las propiedades de una cubierta](shots/roof-properties.webp)

![Una cubierta plana en sección: el forjado de hormigón y la cubierta encima, cada uno sombreado por su material](shots/roof-deck-section.webp)

**Configurador de cubierta** (el botón junto al estilo, o **Architecture → Roof configurator** con una cubierta seleccionada) lo reúne todo: los estilos a la izquierda, luego la forma y sus bordes, la cobertura y el desagüe, la estructura y los huecos, sobre una planta de la cubierta (un clic en un borde lo vuelve auto, alero o hastial) y, para la estructura, una sección transversal a la cumbrera con la cercha o la pareja del centro. Parte de un estilo y cambia a mano cualquier campo; **Apply** lo deja en un solo paso de deshacer. **Guardar como estilo…** guarda la cubierta tal como está configurada como estilo del proyecto, listado en **Project** sobre los del catálogo (y en la lista del panel de propiedades) para otras cubiertas y las nuevas; **Actualizar el estilo** vuelve a guardar en él los cambios, y todas las cubiertas del estilo los siguen. Un estilo del proyecto se puede renombrar o borrar; sus cubiertas quedan como están.

![El configurador de cubierta](shots/roof-configurator.webp)

**Canalones y bajantes.** En **Cobertura y desagüe** del configurador el canalón toma su tamaño, cuánto sale del alero y cuánto baja bajo la cobertura, su pendiente hacia las salidas (mm/m) y sus tapas; la tabla de bordes activa o desactiva el canalón de cada alero. Las bajantes empiezan siendo de la cubierta, una cada 10 m; **Hacerlas editables** las vuelve elementos, y **Architecture → Downpipe** añade una con un clic en un canalón. El tirador de una bajante la desliza solo a lo largo de su canalón; sus propiedades fijan el diámetro, el codo de salida y su recorrido, una cadena de tramos con su longitud y su ángulo desde la vertical (hacia el muro), el último hasta el suelo. En IFC el canalón es un IfcPipeSegment GUTTER y la bajante un segmento rígido llamado Downpipe.

Los muros del nivel de la cubierta se detienen bajo ella y suben por sí solos hasta sus hastiales, también bajo una cubierta curva. Un muro de otro nivel sigue la cubierta tras **Arquitectura → Enlazar a cubierta** (<kbd>AR</kbd>): seleccione los muros y haga clic en la cubierta. La misma herramienta acepta cualquier otro cuerpo sobre los muros: un sólido de Studio, una losa, una viga, un elemento importado de IFC. Los muros suben hasta su cara inferior y se cortan a lo largo de ella, sea cual sea su forma. **Desenlazar de la cubierta** lo deshace.

**Ventana de cubierta** y **Buhardilla** añaden huecos a una cubierta: haga clic en un faldón. Pertenecen a la cubierta. Seleccione la cubierta, y cada una muestra sus pinzamientos en planta. Arrastre el pinzamiento central para moverla sobre la cubierta. Arrastre los pinzamientos de los bordes o esquinas de una ventana de cubierta para cambiar su tamaño en el faldón (el borde opuesto se queda donde está; puede escribir la longitud mientras arrastra). El ancho va a lo largo del alero y la longitud hacia arriba del faldón, medida sobre él. Una buhardilla tiene un pinzamiento en cada lado para su ancho. Un cambio que no cabría en la cubierta se rechaza. Para quitarla, haga clic derecho en un pinzamiento suyo y elija **Quitar ventana de cubierta** (o **Quitar buhardilla**), pulse **Supr** con el cursor sobre su pinzamiento, o haga **Ctrl/⌘-clic** en el pinzamiento central. Las propiedades de la cubierta también las listan: escriba allí el ancho y la longitud de una ventana, o pulse **×** para quitarla. La cubierta de una buhardilla se cubre con el material de la cubrición y se mide con ella: las cantidades de la cubierta dan aparte la **cubrición de las buhardillas** (el área de sus faldones, el número de buhardillas), así la medición por material, el presupuesto y una tarea de planificación sobre la cubrición cuentan los faldones de la cubierta (sin los recortes de las buhardillas) y las cubiertas de las buhardillas, cada uno una vez.

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

**Interiores → Falso techo** (<kbd>CG</kbd>) dibuja un falso techo sobre un contorno que se marca con clics; **Falso techo en un local** llena el local donde se hace clic, siguiendo su contorno. Un falso techo cuelga a su **Altura** sobre la planta (2700 mm salvo que se cambie en sus propiedades).

Su tipo, en **Interiores → Tipos de falso techo**, fija el **Sistema**: **Placas sobre perfilería vista** (ancho, largo y espesor de la placa, ancho visto, altura y giro de la perfilería, cuelgues hasta la estructura con su separación) o **Placa de yeso, continuo**. La perfilería se centra en el techo, así que las placas cortadas en los bordes salen iguales.

**Panel de luz** y **Difusor de aire** ponen uno en la celda donde se hace clic; un clic en la misma celda con la otra herramienta lo cambia. En el panel de propiedades, **Distribuir** pone paneles de luz o difusores cada tantas celdas, y **Vaciar** los quita. Las tablas cuentan las placas enteras, los paneles de luz y los difusores, y el techo va a IFC como IfcCovering de tipo CEILING.

![Las propiedades de un falso techo: su tipo, su altura y sus elementos](shots/ceiling-properties.webp)

![Falsos techos registrables con paneles de luz y difusores, en 3D](shots/ceilings-3d.webp)

## Rodapiés

**Interiores → Rodapié en la habitación** (<kbd>SK</kbd>) coloca un rodapié alrededor de la habitación en la que haces clic, por las caras de los muros, interrumpido en cada puerta (cualquier hueco que baje hasta el suelo); con un clic dentro de muros sin habitación, los sigue igual. El rodapié de una habitación la sigue: mueve un muro o una puerta y el rodapié va con ellos. **Rodapié** dibuja uno a lo largo de una línea que marcas con clics sobre la cara de un muro (Intro termina); la pieza va del lado contrario al muro, y **Cambiar de lado** en sus propiedades la da la vuelta.

Su tipo, en **Interiores → Tipos de rodapié**, fija el **Perfil** — **Recto (cuadrado)**, **Biselado (MDF)**, **Moldurado (madera)**, **Con pie curvo (metal)**, **Canto redondo (lápiz)** o **Borde redondeado**, dibujado al lado a escala —, la **Altura** (40 a 300 mm), el **Espesor** (5 a 40 mm), el **Material** y su color. **De la biblioteca** trae al proyecto tipos ya hechos: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 y Bullnose 70. Mientras una herramienta de rodapié está activa, el panel de propiedades muestra el tipo que coloca, para elegirlo antes del clic.

En sus propiedades un rodapié tiene su tipo, un **Desfase de base** sobre el suelo y **Huecos en las puertas** (desactivado, sigue por delante de ellas). **Tabla de rodapiés** (o **Rodapiés** en la lista de tablas nuevas del navegador del proyecto) los lista por tipo con sus piezas y su longitud, sin los huecos de las puertas. La pieza se corta a inglete en las esquinas; la planta dibuja su canto frontal, y un rodapié va a IFC como un IfcCovering de tipo SKIRTINGBOARD, de un IfcCoveringType del mismo tipo.

![Los tipos de rodapié: el perfil dibujado a escala, la biblioteca](shots/skirting-types.webp)

![Las propiedades de un rodapié: su tipo, el desfase de base y los huecos en las puertas](shots/skirting-properties.webp)
