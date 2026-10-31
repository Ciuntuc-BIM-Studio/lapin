---
title: Armado 2D
order: 52
summary: El armado dibujado en secciones y plantas, secciones transversales cortadas donde haces clic, y el cuadro de armado.
---

La pestaña **Armado 2D** dibuja el armado como se hace un cuadro de barras: barras como formas con sus dimensiones, estribos repartidos a lo largo de una viga, marcas en globos y un cuadro que las suma. Funciona en cualquier vista: una sección, una planta, un alzado.

![La pestaña Armado 2D](shots/ribbon-rebar.webp)

## Una sección donde trabajas

El armado necesita la sección transversal junto a la longitudinal. **Sección aquí** hace ambas: eliges una viga, un pilar o un muro en la vista en la que estás, y donde haces clic se corta una sección *transversal*, perpendicular a su eje, dibujada junto al clic, en la misma vista, con medio metro de lo que la rodea (la losa, el pilar). Un pilar se corta en horizontal, a la altura del clic.

La vista insertada sigue al elemento: mueve la viga y la sección se mueve con ella. Arrastra la vista insertada por su pinzamiento; sus propiedades le dan un factor de escala y un marco. **Sección transversal** hace la misma sección como vista propia, y **Vista insertada** pone cualquier vista existente en la que estás, a tamaño real y a la escala de la vista anfitriona, para que las barras que dibujes encima encajen en el hormigón.

![La sección longitudinal de una viga con su sección transversal insertada y su armado](shots/rebar-section.webp)

## Barras

**Barra recta**, **Barra en L**, **Barra en U**, **Estribo**, **Horquilla** y **Barra polilínea** se colocan con un clic (la polilínea por sus vértices). Cada una es una forma con parámetros: arrastra un pinzamiento y la dimensión que representa le sigue; escríbela en el panel de propiedades, en milímetros. Las barras se dibujan con su grosor, en un color para cada diámetro (Ajustes de armado), y se doblan con el radio que pide SR EN 1992-1-1: mandril de 4Ø hasta Ø16, 7Ø por encima.

Las patillas se fijan en cada extremo: 90°, 135° o 180°, con la longitud recta de la práctica (12Ø, 10Ø, 5Ø, nunca menos de 50 mm), al otro lado cuando lo pides. Un estribo nace con dos patillas de 135° vueltas hacia dentro.

**Barra en sección** es una barra vista de frente: un punto de su diámetro, con la longitud que tiene a lo largo de la barra. **La misma barra en sección** pone la barra seleccionada como punto en otro lugar, sin contarla dos veces. Una **Matriz** en las propiedades repite una barra cada tantos milímetros, como las barras de una fila en una sección transversal.

El globo de la marca muestra el número de marca y qué es la barra: `4Ø12 L=2450`. Arrastra el globo donde mejor se lea. Las longitudes son las nominales, medidas en el eje de la barra entre sus esquinas, con las patillas; la longitud desarrollada por los dobleces se indica en el panel de propiedades.

## Estribos en el tipo de viga o de pilar

Los estribos no se dibujan uno a uno: pertenecen al **tipo**. En Estructura → Tipos de estructura, un tipo rectangular de viga o de pilar admite **Estribos en este tipo**: su diámetro, el recubrimiento de hormigón y cómo se colocan: un paso en toda la longitud o **tres zonas**, cada una con su parte de la longitud (un tercio por defecto) y su paso, más cerrado en los extremos. Cada elemento del tipo los muestra entonces en toda sección según la posición de la vista respecto al eje: el rectángulo dentro del recubrimiento en la sección transversal, una línea por estribo en la longitudinal, líneas sobre la viga en una planta de estructura; con la etiqueta `41Ø8/150` y el globo de la marca. Se cuentan en el cuadro de armado bajo la marca del tipo. Las **barras longitudinales** no van en el tipo (corren sobre varios vanos): dibújalas en la sección longitudinal o en la planta, con sus patillas. Una barra dibujada dentro del corte de una viga pertenece a esa viga y está en el espacio: **A través del dibujo** la coloca tantas veces en la dirección de la vista, **por el recubrimiento** (la primera y la última en las caras, dentro del recubrimiento y del estribo, el resto repartidas) o **a un paso**. La sección transversal muestra entonces las barras como puntos donde están, una planta de estructura como líneas, y el cuadro las cuenta todas: `3Ø16 L=6400`. Los cuerpos en la vista 3D no están en esta edición.

## Estribos a lo largo de una viga

**Estribos a lo largo** toma los dos extremos de un recorrido; los estribos se reparten de modo que uno quede en cada extremo, al paso que fijes (150 mm por defecto). Las **Zonas** cambian el paso por el camino: **Tercios** pone en cada extremo un paso dos veces más cerrado, como pide una viga sísmica; la etiqueta dice `25Ø8/100-200`. Muéstralos todos, solo los extremos y el centro con una línea discontinua entre ellos, o uno.

## El cuadro

**Cuadro de armado** hace un cuadro con cada barra por marca: diámetro, forma, número, longitud de una, longitud total y masa, con los totales por diámetro. Una marca cuyas barras difieren en diámetro o longitud queda anotada. Es un cuadro como los demás: en planos, en CSV y XLSX.

**Ajustes de armado** guarda las elecciones del proyecto: un color para cada diámetro, los tamaños del texto y del globo en papel, el acero, el recubrimiento y las longitudes de los tramos escritas en las barras.
