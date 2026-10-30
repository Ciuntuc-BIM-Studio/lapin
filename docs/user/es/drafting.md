---
title: Dibujo y línea de comandos
order: 80
summary: Escriba comandos como en CAD, fuerce el cursor a puntos, desplace y copie, dibuje líneas y anote con cotas, textos y etiquetas.
---

## La línea de comandos

Escriba las letras de una herramienta en cualquier lugar de la ventana y pulse <kbd>Enter</kbd>. Por ejemplo, escriba <kbd>WA</kbd> para un muro, <kbd>CO</kbd> para copiar, <kbd>M</kbd> para desplazar o <kbd>L</kbd> para una línea. La línea de comandos de la parte inferior muestra lo que la herramienta necesita a continuación. Puede hacer clic en un punto o escribir una longitud, un ángulo o unas coordenadas.

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

## Copiar y desplazar

Para copiar, seleccione el elemento y escriba <kbd>CO</kbd>. Haga clic en un punto base y luego en un segundo punto. Lapin sigue copiando hasta que pulse <kbd>Enter</kbd> o <kbd>Esc</kbd>. **Desplazar** (<kbd>M</kbd>) funciona igual.

Mientras desplaza o copia, un marcador permanece en el punto base y una línea discontinua va de él al ratón, con un contorno atenuado de lo que desplaza en el lugar donde estaba, hasta que haga clic o pulse <kbd>Esc</kbd>. La línea y el marcador cambian de color según la referencia: verde en un punto de referencia, azul cuando el modo orto o el rastreo polar fijan la dirección, el color de acento en una línea de rastreo y gris cuando el movimiento es libre. Junto al ratón, Lapin muestra cuánto ha desplazado: Δx, Δy, la distancia y el ángulo, en las unidades del proyecto. Puede escribir ahí la distancia y el ángulo, como cuando dibuja. En la interfaz Compacto, estos valores aparecen en la línea de estado. Arrastrar un pinzamiento muestra lo mismo, y también arrastrar el propio elemento seleccionado, desde el punto en el que pulsó; pulse <kbd>Esc</kbd> antes de soltar y el elemento se queda donde estaba.

![Desplazar un muro: el marcador en el punto base, la línea discontinua hasta el ratón, la silueta del muro donde estaba y el desplazamiento junto al ratón](shots/move-trace.webp)

También puede arrastrar. Arrastre la cruz del centro de un elemento seleccionado para desplazarlo. Mantenga pulsada <kbd>Ctrl</kbd> (<kbd>⌘</kbd> en un Mac) mientras arrastra para dejar una copia.

Un eje de rejilla se desplaza, se copia, se gira y se refleja por sí solo o junto con otros elementos. Una copia es un nuevo eje de la misma rejilla, con el nombre que sigue al último eje en su dirección: con los ejes 1, 2 y 3, la copia de cualquiera de ellos es el 4.

## Dibujo

La ficha **Dibujo** dibuja líneas, polilíneas, rectángulos, círculos y arcos. Los edita con desfase, recortar, alargar, empalme, chaflán y unir, rellena áreas con sombreados y los organiza en **Capas**. **Import DXF** trae un dibujo CAD como líneas a las que puede forzar el cursor y que puede editar.

![La ficha Dibujo](shots/ribbon-draft.webp)

## Anotar

La ficha **Anotar** contiene las cotas, los textos, los ejes de rejilla, los niveles y las etiquetas. Una cota lee el modelo, así que se actualiza cuando un muro se mueve. El aspecto de los textos, las cotas, los ejes y las etiquetas se define en **Gestionar → Normas de dibujo**.

Una cota alineada o lineal se coloca como una cadena: tras los dos primeros puntos y su línea, cada clic añade el punto siguiente en la misma línea, y **Enter** (o **Esc**) convierte toda la cadena en una sola cota (**U** retira el último punto; escriba primero **S** para una cota simple). Una cadena se comporta como una polilínea: un clic la selecciona entera, y se desplaza, se copia y se borra como un todo. Una cadena que mide muros, aristas o ejes de rejilla se queda sobre ellos: arrastrada o desplazada por sí sola, solo se desliza su línea de cota, perpendicular a la cadena, de modo que un desliz del ratón nunca la deja midiendo el vacío; desplazada junto con lo que mide, se mueve con ello. Sus pinzamientos están en cada punto medido (arrastre para moverlo, Ctrl/⌘-clic para quitarlo), en el centro de cada segmento (arrastre para añadir un punto, Ctrl/⌘-clic para añadir uno a mitad) y en los extremos de la línea de cota (arrastre para mover la línea). Para añadir más puntos a una cadena, como en ArchiCAD, selecciónela y haga clic en **Anotar → Añadir puntos** (o escriba **DIMADD**, o use **Añadir puntos** en sus propiedades): cada punto en el que haga clic entra en la cadena donde cae a lo largo de su línea, entre los puntos que lo rodean o más allá de un extremo como nuevo extremo, y se mantiene sobre el muro, la arista o el eje en el que cae; **Enter** o **Esc** termina. Para quitar puntos, haga Ctrl/⌘-clic en sus pinzamientos, o haga clic en **Anotar → Quitar puntos** (**DIMDEL**, o **Quitar puntos** en sus propiedades) y haga clic cerca de cada punto; los segmentos a ambos lados se convierten en uno. El valor de cada segmento también tiene un pinzamiento: arrástrelo para apartar el valor de su sitio donde los segmentos son cortos y los valores se solaparían; en cuanto se separa de su segmento, una fina directriz lo une a él. Ctrl/⌘-clic en el pinzamiento, o **Textos de vuelta en la línea** en las propiedades, lo devuelve a su lugar. Un valor desplazado se queda con su segmento cuando los puntos se mueven y gira con la cadena. Haga doble clic en un segmento para escribir su valor: el elemento que mide se desplaza, y también todo lo que le sigue en la cadena. Las propiedades muestran el valor y el texto de cada segmento, dividen una cadena en cotas simples y unen cotas simples que se continúan en una misma línea.

![La ficha Anotar](shots/ribbon-annotate.webp)
