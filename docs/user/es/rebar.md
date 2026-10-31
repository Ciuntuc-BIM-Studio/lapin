---
title: Armado 2D
order: 52
summary: El armado dibujado en secciones y plantas, secciones transversales cortadas donde haces clic, y el cuadro de armado.
---

La pestaña **Armado 2D** dibuja el armado como se hace un cuadro de barras: barras como formas con sus dimensiones, estribos repartidos a lo largo de una viga, marcas en globos y un cuadro que las suma. Funciona en cualquier vista: una sección, una planta, un alzado.

![La pestaña Armado 2D](shots/ribbon-rebar.webp)

## Una sección donde trabajas

El armado necesita la sección transversal junto a la longitudinal. **Sección aquí** hace ambas: eliges una viga, un pilar o un muro en la vista en la que estás, y donde haces clic se corta una sección *transversal*, perpendicular a su eje, dibujada junto al clic, en la misma vista, con medio metro de lo que la rodea (la losa, el pilar). Un pilar se corta en horizontal, a la altura del clic.

La vista insertada sigue al elemento: mueve la viga y la sección se mueve con ella. Arrastra la vista insertada por su pinzamiento; sus propiedades le dan un factor de escala y un marco. **Sección transversal** hace la misma sección como vista propia, e **Vista insertada** pone cualquier vista existente en la que estás.

![La sección longitudinal de una viga con su sección transversal insertada y su armado](shots/rebar-section.webp)

## Barras

**Barra recta**, **Barra en L**, **Barra en U**, **Estribo**, **Horquilla** y **Barra polilínea** se colocan con un clic (la polilínea por sus vértices). Cada una es una forma con parámetros: arrastra un pinzamiento y la dimensión que representa le sigue; escríbela en el panel de propiedades, en milímetros. Las barras se dibujan con su grosor, en un color para cada diámetro (Ajustes de armado), y se doblan con el radio que pide SR EN 1992-1-1: mandril de 4Ø hasta Ø16, 7Ø por encima.

Las patillas se fijan en cada extremo: 90°, 135° o 180°, con la longitud recta de la práctica (12Ø, 10Ø, 5Ø, nunca menos de 50 mm), al otro lado cuando lo pides. Un estribo nace con dos patillas de 135° vueltas hacia dentro.

**Barra en sección** es una barra vista de frente: un punto de su diámetro, con la longitud que tiene a lo largo de la barra. **La misma barra en sección** pone la barra seleccionada como punto en otro lugar, sin contarla dos veces. Una **Matriz** en las propiedades repite una barra cada tantos milímetros, como las barras de una fila en una sección transversal.

El globo de la marca muestra el número de marca y qué es la barra: `4Ø12 L=2450`. Arrastra el globo donde mejor se lea. Las longitudes son las nominales, medidas en el eje de la barra entre sus esquinas, con las patillas; la longitud desarrollada por los dobleces se indica en el panel de propiedades.

## Estribos a lo largo de una viga

**Estribos a lo largo** toma los dos extremos de un recorrido; los estribos se reparten de modo que uno quede en cada extremo, al paso que fijes (150 mm por defecto). Las **Zonas** cambian el paso por el camino: **Tercios** pone en cada extremo un paso dos veces más cerrado, como pide una viga sísmica; la etiqueta dice `25Ø8/100-200`. Muéstralos todos, solo los extremos y el centro con una línea discontinua entre ellos, o uno.

## El cuadro

**Cuadro de armado** hace un cuadro con cada barra por marca: diámetro, forma, número, longitud de una, longitud total y masa, con los totales por diámetro. Una marca cuyas barras difieren en diámetro o longitud queda anotada. Es un cuadro como los demás: en planos, en CSV y XLSX.

**Ajustes de armado** guarda las elecciones del proyecto: un color para cada diámetro, los tamaños del texto y del globo en papel, el acero, el recubrimiento y las longitudes de los tramos escritas en las barras.
