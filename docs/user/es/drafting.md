---
title: Dibujo y línea de comandos
order: 80
summary: Escriba comandos como en CAD, fuerce el cursor a puntos, desplace y copie, dibuje líneas y anote con cotas, textos y etiquetas.
---

## La línea de comandos

Escriba las letras de una herramienta en cualquier lugar de la ventana y pulse <kbd>Enter</kbd>. Por ejemplo, escriba <kbd>WA</kbd> para un muro, <kbd>CO</kbd> para copiar, <kbd>M</kbd> para desplazar o <kbd>L</kbd> para una línea. La línea de comandos de la parte inferior muestra lo que la herramienta necesita a continuación. Puede hacer clic en un punto o escribir una longitud, un ángulo o unas coordenadas.

Las coordenadas tecleadas están en la unidad del proyecto, como las longitudes junto al cursor: en un proyecto en milímetros, `@4000,4000` tras la primera esquina de un rectángulo lo hace de 4 m por 4 m, y `4000<90` son 4 m hacia arriba.

![Copiar un muro: tras el punto base, la línea de comandos y la entrada en vivo muestran la longitud y el ángulo hasta el ratón](shots/command-line.webp)

Cada herramienta muestra sus letras en su información sobre herramientas; puede cambiarlas en **Vista → Métodos abreviados de teclado…** (consulte [La interfaz y los métodos abreviados](interface.md)). Estas son las más usadas:

| Modelo | | Dibujo | |
|---|---|---|---|
| Muro | <kbd>WA</kbd> | Línea | <kbd>L</kbd> |
| Puerta, ventana | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilínea | <kbd>PL</kbd> |
| Losa | <kbd>SB</kbd> | Rectángulo, círculo, arco | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Cubierta | <kbd>RF</kbd> | Desplazar, copiar | <kbd>M</kbd>, <kbd>CO</kbd> |
| Escalera, habitación | <kbd>ST</kbd>, <kbd>RM</kbd> | Girar, simetría, escala | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Pilar, viga | <kbd>CL</kbd>, <kbd>BM</kbd> | Desfase, recortar, alargar | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Sección | <kbd>SE</kbd> | Empalme, chaflán | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Medir | <kbd>ME</kbd> | Sombreado, texto | <kbd>H</kbd>, <kbd>T</kbd> |
| Ajustar todo | <kbd>ZE</kbd> | Borrar | <kbd>E</kbd> |

## Referencias y ayudas

Lapin fuerza el cursor a los extremos, los puntos medios y las intersecciones de lo dibujado, y a la rejilla. <kbd>F3</kbd> activa o desactiva las referencias, <kbd>F8</kbd> el modo orto y <kbd>F10</kbd> el rastreo polar. Su estado se muestra en la parte inferior derecha de la vista.

## Seleccionar

Haga clic en un elemento para seleccionarlo; <kbd>Mayús</kbd>-clic lo añade a la selección. Para seleccionar varios a la vez, arrastre con el botón izquierdo donde no haya nada seleccionado. Arrastrado hacia la derecha, el recuadro es azul y toma lo que queda entero dentro (ventana); hacia la izquierda, es verde y discontinuo y toma todo lo que toca (captura), como en AutoCAD. Pulse <kbd>Espacio</kbd> mientras arrastra y el recuadro se convierte en un lazo que sigue al ratón, con el modo que tenía el recuadro; <kbd>Espacio</kbd> de nuevo vuelve al recuadro. En la línea de comandos, cuando un comando pide seleccionar objetos, escriba **WP** o **CP** y haga clic en los vértices de un polígono, luego <kbd>Intro</kbd>: un polígono ventana o un polígono captura.

![Un lazo en la planta: el recuadro arrastrado a la derecha, luego Espacio, y el lazo alrededor de los dos muros que toma](shots/select-lasso.webp)

## Copiar y desplazar

Para copiar, seleccione el elemento y escriba <kbd>CO</kbd>. Haga clic en un punto base y luego en un segundo punto. Lapin sigue copiando hasta que pulse <kbd>Enter</kbd> o <kbd>Esc</kbd>. **Desplazar** (<kbd>M</kbd>) funciona igual.

Mientras desplaza o copia, un marcador permanece en el punto base y una línea discontinua va de él al ratón, con un contorno atenuado de lo que desplaza en el lugar donde estaba, hasta que haga clic o pulse <kbd>Esc</kbd>. La línea y el marcador cambian de color según la referencia: verde en un punto de referencia, azul cuando el modo orto o el rastreo polar fijan la dirección, el color de acento en una línea de rastreo y gris cuando el movimiento es libre. Junto al ratón, Lapin muestra cuánto ha desplazado: Δx, Δy, la distancia y el ángulo, en las unidades del proyecto. Puede escribir ahí la distancia y el ángulo, como cuando dibuja. En la interfaz Compacto, estos valores aparecen en la línea de estado. Arrastrar un pinzamiento muestra lo mismo, y también arrastrar el propio elemento seleccionado, desde el punto en el que pulsó; pulse <kbd>Esc</kbd> antes de soltar y el elemento se queda donde estaba.

![Desplazar un muro: el marcador en el punto base, la línea discontinua hasta el ratón, la silueta del muro donde estaba y el desplazamiento junto al ratón](shots/move-trace.webp)

También puede arrastrar. Arrastre la cruz del centro de un elemento seleccionado para desplazarlo. Mantenga pulsada <kbd>Ctrl</kbd> (<kbd>⌘</kbd> en un Mac) mientras arrastra para dejar una copia.

Una puerta, una ventana o un hueco de muro se desplaza y se copia a lo largo de su muro: solo cuenta la parte del desplazamiento a lo largo del muro, así que escribir `500` tras el punto base lo desliza 500 por el muro; llevado a otro muro, pasa a ese muro. Si el muro también está seleccionado, se mueve con él.

Un eje de rejilla se desplaza, se copia, se gira y se refleja por sí solo o junto con otros elementos. Una copia es un nuevo eje de la misma rejilla, con el nombre que sigue al último eje en su dirección: con los ejes 1, 2 y 3, la copia de cualquiera de ellos es el 4.

## Dibujo

La ficha **Dibujo** dibuja líneas, polilíneas, rectángulos, círculos y arcos. Los edita con desfase, recortar, alargar, empalme, chaflán y unir, rellena áreas con sombreados y los organiza en **Capas**. **Importar DXF** trae un dibujo CAD como líneas a las que puede forzar el cursor y que puede editar.

![La ficha Dibujo](shots/ribbon-draft.webp)

**Desfase** (<kbd>O</kbd>) copia una línea, una polilínea, un rectángulo, un círculo o un arco a una distancia. Escribe la distancia en la unidad del proyecto (en un proyecto en milímetros `500` es medio metro; `2m` o `1'6"` dicen su propia unidad), haz clic en la forma y luego en el lado hacia el que va; pide la forma siguiente hasta que pulses <kbd>Enter</kbd> o <kbd>Esc</kbd>. Escribe **T** (Through) en su lugar y cada copia pasa por el punto en que haces clic. Las esquinas de una polilínea quedan vivas (en inglete), como en CAD. El desfase funciona en plantas, secciones, alzados, en planos de referencia y en la vista 3D.

Toda longitud que pide la línea de comandos está en la unidad del proyecto, y el mensaje dice cuál: `Depth (mm)`, `Height (ft-in)`. Los ángulos, factores y recuentos son números simples: `90` tras **Girar** es 90°, sea cual sea la unidad.

### Dibujar en la vista 3D

**Línea**, **Polilínea**, **Rectángulo**, **Círculo**, **Arco** y las ediciones de Dibujo funcionan también en la vista 3D, con la línea de comandos abajo. Mientras corre un comando, la etiqueta de arriba dice dónde dibujas: en el nivel de la planta activa o en el plano de referencia seleccionado. **Elegir una cara** en la etiqueta, y luego un clic en una cara del modelo, crea un plano de referencia en esa cara y dibuja en él. Los puntos se enganchan como en una planta: a las líneas cortadas de la planta (esquinas y caras de los muros) y a lo dibujado en el mismo plano, con orto y las guías de rastreo. Lo que dibujas es el mismo elemento de dibujo que uno hecho en la planta, y se ve también allí.

![Dibujar en la vista 3D: la etiqueta dice el plano, la línea sigue al ratón en el nivel de la planta](shots/draft-3d.webp)

### Los dibujos en la vista 3D

Lo que dibujas en una planta (líneas, polilíneas, rectángulos, círculos, arcos) aparece también en la vista 3D, como líneas en el nivel de su planta (el nivel mismo, no la altura de corte); lo dibujado en un plano de referencia, una sección o un alzado queda en ese plano. Haz clic en uno en 3D para seleccionarlo y luego muévelo, cópialo, desfásalo o bórralo. **Vista → Dibujos en 3D**, también en **Vista → Configuración de vista** de una vista 3D, los muestra u oculta todos; está activado mientras no lo apagues. Las cotas, textos, etiquetas, sombreados y líneas auxiliares (el rol draft) quedan en sus vistas, y un DXF traído a una planta sigue siendo un dibujo 2D, salvo sus líneas con cotas.

## Anotar

La ficha **Anotar** contiene las cotas, los textos, los ejes de rejilla, los niveles y las etiquetas. Una cota lee el modelo, así que se actualiza cuando un muro se mueve. El aspecto de los textos, las cotas, los ejes y las etiquetas se define en **Gestionar → Normas de dibujo**.

Una cota alineada o lineal se coloca como una cadena: tras los dos primeros puntos y su línea, cada clic añade el punto siguiente en la misma línea, y **Enter** (o **Esc**) convierte toda la cadena en una sola cota (**U** retira el último punto; escriba primero **S** para una cota simple). Una cadena se comporta como una polilínea: un clic la selecciona entera, y se desplaza, se copia y se borra como un todo. Una cadena que mide muros, aristas o ejes de rejilla se queda sobre ellos: arrastrada o desplazada por sí sola, solo se desliza su línea de cota, perpendicular a la cadena, de modo que un desliz del ratón nunca la deja midiendo el vacío; desplazada junto con lo que mide, se mueve con ello. Sus pinzamientos están en cada punto medido (arrastre para moverlo, Ctrl/⌘-clic para quitarlo), en el centro de cada segmento (arrastre para añadir un punto, Ctrl/⌘-clic para añadir uno a mitad) y en los extremos de la línea de cota (arrastre para mover la línea). Para añadir más puntos a una cadena, como en ArchiCAD, selecciónela y haga clic en **Anotar → Añadir puntos** (o escriba **DIMADD**, o use **Añadir puntos** en sus propiedades): cada punto en el que haga clic entra en la cadena donde cae a lo largo de su línea, entre los puntos que lo rodean o más allá de un extremo como nuevo extremo, y se mantiene sobre el muro, la arista o el eje en el que cae; **Enter** o **Esc** termina. Para quitar puntos, haga Ctrl/⌘-clic en sus pinzamientos, o haga clic en **Anotar → Quitar puntos** (**DIMDEL**, o **Quitar puntos** en sus propiedades) y haga clic cerca de cada punto; los segmentos a ambos lados se convierten en uno. El valor de cada segmento también tiene un pinzamiento: arrástrelo para apartar el valor de su sitio donde los segmentos son cortos y los valores se solaparían; en cuanto se separa de su segmento, una fina directriz lo une a él. Ctrl/⌘-clic en el pinzamiento, o **Textos de vuelta en la línea** en las propiedades, lo devuelve a su lugar. Un valor desplazado se queda con su segmento cuando los puntos se mueven y gira con la cadena. Haga doble clic en un segmento para escribir su valor: el elemento que mide se desplaza, y también todo lo que le sigue en la cadena. Las propiedades muestran el valor y el texto de cada segmento, dividen una cadena en cotas simples y unen cotas simples que se continúan en una misma línea.

Las anotaciones de una planta (cotas, textos, líneas dibujadas, etiquetas) pueden pasar a otra planta en el mismo sitio, ya que las plantas comparten coordenadas: selecciónalas y elige la planta en sus propiedades, en **View** para moverlas o en **Copy to** para copiarlas. Así las cotas hechas en un plano de situación llegan a la planta del nivel. Los puntos de una cota se sujetan entonces a lo que la nueva planta corta en ese sitio: muros, aristas, ejes.

### Etiquetas

**Anotar → Etiqueta** (<kbd>TG</kbd>) pone en una vista un recuadro con lo que un elemento dice de sí mismo y una línea de referencia hasta él. Mientras la herramienta está activa, la barra bajo la vista elige lo que muestra la etiqueta: la marca, el nombre, la clase o el tipo, **Resistencia al fuego**, **Aislamiento acústico**, **Transmitancia térmica** u **Otro parámetro…** por su nombre; o un tipo de etiqueta de las normas de dibujo; y **REFERENCIA** activa o desactiva la línea de referencia. Haga clic en el elemento y luego donde va la etiqueta (escriba antes **E** para quebrar la línea en un codo). **Anotar → Etiquetar un parámetro** pide el nombre del parámetro e inicia la herramienta con él.

![La barra de la herramienta Etiqueta bajo la vista](shots/tag-options.webp)

Una etiqueta lee cualquier parámetro: el del elemento, el de su tipo (entonces lo muestran todos los elementos del tipo), una propiedad estándar como `Pset_WallCommon.FireRating` o una con la que llegó un elemento importado. El nombre se encuentra también sin espacios, así que **Fire rating** y `FireRating` son lo mismo. Para rellenarlo, use **Parámetros** en las propiedades del elemento o de su tipo: escriba un nombre (se proponen Fire rating, Sound rating, Moisture resistance y Thermal resistance) o elija **Añadir una propiedad estándar…** para las propiedades IFC de su clase. La etiqueta lo sigue en cuanto hay un valor, y las longitudes se escriben en las unidades del proyecto.

Una etiqueta seleccionada tiene pinzamientos. Arrastre el recuadro para mover la etiqueta: la línea de referencia sigue señalando donde señalaba. Una etiqueta va con su elemento: cuando el elemento se desplaza, gira, se refleja o se modifica con sus pinzamientos, su etiqueta lo sigue manteniendo su sitio junto a él, y la línea de referencia sigue sobre él; una etiqueta que usted ha movido conserva la distancia que le dio. Arrastre el centro de la línea para quebrarla en un codo (Ctrl/⌘-clic en el codo la endereza) y su extremo para que señale otro punto (Ctrl/⌘-clic: de vuelta al centro del elemento). Las propiedades cambian lo que la etiqueta **Muestra**, le dan un **Texto** propio (`{mark}|{FireRating}` escribe dos líneas), un **Tipo de etiqueta**, o desactivan la **Línea de referencia**. El motor dibuja las etiquetas, así que los planos y los PDF las imprimen como las ve. Los tipos de etiqueta de **Gestionar → Normas de dibujo → Etiquetas** leen los mismos campos en sus plantillas; un proyecto nuevo trae entre ellos Resistencia al fuego, Aislamiento acústico y Transmitancia térmica.

![La línea de referencia de una etiqueta quebrada en su codo, con sus pinzamientos](shots/tag-leader.webp)

![Las propiedades de una etiqueta](shots/tag-properties.webp)

El marcador de una puerta o ventana se edita parte por parte, cada texto movido o girado y cada extremo de su línea alargado: ver [Puertas y ventanas](doors-windows.md).

#### Etiqueta de composición

Una etiqueta de composición escribe las capas de un elemento como una nota de arquitectura: una cabecera (*EXTERIOR*), una línea por capa, un pie (*INTERIOR*). Un proyecto nuevo tiene **Build-up (exterior → interior)**: cada línea es la descripción del material (su nombre si no tiene), su λ si lo tiene y el espesor de la capa en mm. Elígela en la barra de la herramienta **Etiqueta** y haz clic en un muro, un forjado, una cubierta o el acabado de un local; los demás elementos reciben el campo elegido. El texto se lee del modelo cada vez que se dibuja: cambia una capa, la descripción de un material o su λ, y la etiqueta lo sigue.

![Dos etiquetas de composición en un muro de fachada: una directriz, y una directriz por capa](shots/buildup-tag.webp)

Su forma se define en **Gestionar → Normas de dibujo → Etiquetas**: pon la **Forma** de un tipo de etiqueta en *composición*, o haz clic en **Composición…** en uno, para editarlo junto a una vista previa sobre uno de los tipos de muro o de forjado del proyecto.

- **Cabecera** y **Pie**: se escriben encima y debajo de las capas (vacío: ninguno; `|` empieza una línea nueva).
- **Cada capa**: la línea escrita una vez por capa. Lee los campos de la capa, `{description}`, `{material}`, `{thickness}` (en la unidad elegida en **Espesor en**), `{note}` (la nota de la capa en su tipo), `{function}` (núcleo, acabado, aislamiento…) e `{index}`; cualquier parámetro del material por su nombre o su símbolo, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (consulta los [Datos](materials.md#datos-descripcion-parametros-como-se-mide) del material); y los del elemento, `{name}`, `{type}`, `{mark}`. **Insertar un campo…** pone uno donde está el cursor y lista los parámetros que tienen los materiales del proyecto.
- **Los corchetes** hacen opcional una parte: `[, λ = {λ} W/mK]` solo se escribe donde el material tiene λ; una parte desaparece entera cuando un campo en ella está vacío. **[ ]** convierte la selección en una parte así. Para escribir un corchete, teclea `\[` o `\]`.
- **Orden**: del exterior al interior de un muro (el lado de su primera capa es su exterior, así que un muro volteado empieza ahí igualmente), de arriba abajo en un forjado o una cubierta; o al revés. Elige el orden en que las capas quedan junto a la nota, para que las directrices por capa no se crucen.
- **Las capas sin espesor** (membranas, láminas, pintura) se escriben salvo que desmarques **visibles**. Una capa que cambia de material en altura escribe una línea por material.
- **Directrices**: una, al elemento, o **una por capa**, cada una desde su línea hasta un punto dentro de su capa, donde la vista distingue las capas (un muro en plantas y secciones, un forjado en secciones; en otros casos la etiqueta mantiene una). Arrastra el extremo de la directriz a lo largo del elemento para cruzarlo en otro sitio.
- **Las líneas empiezan con** una viñeta, un guion, su número o nada; se cortan al **Ancho mm** en el papel, sangradas tras la viñeta, y un valor se queda junto a su símbolo y su unidad (`λ = 0.035 W/mK`).

![El editor de un tipo de etiqueta de composición junto a su vista previa](shots/buildup-tag-type.webp)

Como cualquier etiqueta, la etiqueta de composición sigue a su elemento, y los planos y los PDF la imprimen tal como la ves, con λ, μ y ρ. Un **Texto** propio en sus propiedades sustituye la línea por capa solo para esa etiqueta.

### Ejes de rejilla

**Anotar → Eje de rejilla** (<kbd>GR</kbd>) dibuja un eje por sus dos extremos; toma la siguiente etiqueta de su dirección. **Anotar → Retícula de ejes…** coloca una retícula entera de una vez: las separaciones **En horizontal** (los ejes numerados, de izquierda a derecha) y **En vertical** (los ejes con letras, de abajo arriba) en las unidades del proyecto, como `6000 6000 4500` o `3*6000`; el **Origen** donde se cruzan los primeros ejes, un **Ángulo**, cuánto se prolongan los ejes **Más allá de la retícula**, las **Primeras etiquetas** y el **Tipo de eje**. Con **sustituir los ejes de la retícula** marcado, los ejes dibujados hasta entonces desaparecen.

![El diálogo Retícula de ejes](shots/grid-dialog.webp)

Haga clic en un eje para seleccionarlo. Sus propiedades muestran su **Inicio** y su **Fin**, la **Longitud**, el **Ángulo** y la separación desde el eje paralelo anterior (↔). Escriba una separación nueva y el eje se mueve; los ejes que siguen también se mueven, salvo que desmarque **los ejes que siguen también se mueven**. **Burbujas** elige qué extremos llevan burbuja. **Estilos de eje…** abre **Normas de dibujo → Ejes de rejilla**, donde se ajustan la burbuja, su tamaño, el tipo de línea y la primera etiqueta. Seleccione toda la retícula para ver sus ejes y sus separaciones.

![Las propiedades de un eje](shots/axis-properties.webp)

Todas estas longitudes siguen las unidades del proyecto. En pies y pulgadas, los extremos y las separaciones se leen 19'-8 1/4", y los niveles también se escriben en pies y pulgadas.

![El mismo eje en pies y pulgadas](shots/axis-properties-imperial.webp)

![La ficha Anotar](shots/ribbon-annotate.webp)
