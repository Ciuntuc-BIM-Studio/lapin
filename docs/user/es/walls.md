---
title: Muros
order: 20
summary: Dibuje muros esquina a esquina, modifíquelos en el panel de propiedades y configure tipos de muro con sus capas.
---

<!-- video walls -->

## Dibujar muros

Abra la ficha **Arquitectura** y elija **Muro**, o escriba <kbd>WA</kbd>.

![La ficha Arquitectura](shots/ribbon-architecture.webp)

Haga clic donde empieza el muro y luego en cada esquina. El muro siguiente sigue al ratón. Mientras dibuja, puede escribir una longitud y un ángulo en las casillas sobre la vista en lugar de hacer clic. Pulse <kbd>Enter</kbd> o <kbd>Esc</kbd> para terminar.

![Dibujar muros: el muro siguiente sigue al ratón, con su longitud y su ángulo a la vista](shots/wall-drawing.webp)

Donde se encuentran dos muros, Lapin los une por sí mismo. Sus capas se encuentran tal como se construirían.

- **Muro curvo** (<kbd>WC</kbd>) dibuja un muro a lo largo de un arco.
- **Muro cortina** (<kbd>CW</kbd>) dibuja un muro acristalado con montantes y paneles.
- **Unir** (<kbd>JW</kbd>), **Inglete** y **A tope** cambian cómo se encuentran dos muros. En ambos casos, cada capa dobla la esquina por sí sola: los acabados se encuentran con sus iguales y siguen sin cortes, y los núcleos se encuentran en la diagonal (inglete) o un núcleo sigue hasta la cara exterior del otro (a tope), así que la planta de muros multicapa muestra esquinas limpias.
- **Intercambiar** cambia cuál de los dos muros pasa de largo.

## Modificar un muro

Haga clic en un muro para seleccionarlo. Aparecen sus pinzamientos. Arrastre los extremos para alargarlo o acortarlo, o arrastre la cruz del centro para moverlo.

![Un muro seleccionado con sus pinzamientos](shots/wall-selected.webp)

El panel de propiedades muestra el muro seleccionado. La parte superior del panel contiene su tipo y sus capas. Debajo están su longitud, altura, desfase de base, planta y fase. Cambie cualquier valor y el modelo lo sigue.

![Las propiedades de un muro](shots/wall-properties.webp)

**Voltear** pasa las capas del muro al otro lado. También puede hacer clic en las pequeñas flechas que aparecen junto a un muro seleccionado en la planta.

## Tipos de muro

Un tipo de muro es un conjunto de capas, desde la cara exterior hasta la cara interior. Cada capa tiene un material y un espesor, y una capa es el **núcleo**. La línea del muro va por el núcleo. Para editar los tipos, elija **Arquitectura → Tipos de muro**. Una capa que no sea el núcleo puede tener su propia altura: **Desde** y **Hasta** fijan hasta dónde llega sobre la base del muro (un alicatado hasta 1500, un aplacado desde 300); vacío, ocupa toda la altura.

![Tipos de muro: las capas de un muro de ladrillo](shots/wall-types.webp)

- **+ Capa** añade una capa. Las flechas suben o bajan una capa.
- **Zonas en altura** divide el núcleo de abajo arriba, por ejemplo una primera hilada, la fábrica y una viga de atado.
- **Dinteles** elige los dinteles colocados sobre puertas y ventanas: prefabricados u hormigonados in situ, con su apoyo y su altura.
- **Dibujar con él** cierra el cuadro de diálogo y empieza a dibujar muros de este tipo.

Un cambio en un tipo cambia todos los muros de ese tipo.

## Muros bajo una cubierta

Los muros del mismo nivel que una cubierta se detienen bajo ella por sí mismos y suben hasta sus hastiales. Para un muro de otro nivel, selecciónelo y elija **Arquitectura → Enlazar a cubierta** (<kbd>AR</kbd>), y luego haga clic en la cubierta — o en cualquier otro cuerpo hasta el que deba subir el muro: un sólido de Studio, un elemento importado. Consulte [Losas y cubiertas](floors-roofs.md).
