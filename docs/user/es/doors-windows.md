---
title: Puertas y ventanas
order: 30
summary: Coloque puertas y ventanas en los muros, muévalas y diseñe sus tipos en el configurador.
---

<!-- video doors-windows -->

## Colocar una puerta o una ventana

Elija **Arquitectura → Puerta** (<kbd>DR</kbd>) o **Ventana** (<kbd>WN</kbd>) y haga clic en un muro donde quiera el hueco. Lapin corta el hueco en todas las capas del muro y añade el dintel que pide el tipo de muro.

![Una planta con una puerta y ventanas](shots/openings-plan.webp)

Para mover una puerta o una ventana a lo largo de su muro, selecciónela y arrástrela. Para cambiarla, use el panel de propiedades: su tipo, la altura del alféizar y hacia qué lado abre (**Lado**).

![Las propiedades de una puerta](shots/door-properties.webp)

## El configurador

**Arquitectura → Puertas y ventanas** abre el configurador. Todos los tipos de puerta y ventana del proyecto aparecen a la izquierda. Puede cambiar:

- el nombre y la marca del tipo (la letra de las etiquetas);
- su anchura y su altura;
- la profundidad y la anchura del marco;
- sus columnas y filas de hojas, y cómo abre cada hoja;
- el acabado de la mocheta, el recercado en la fachada, el umbral y los colores.

Los dibujos de la derecha muestran el tipo tal como se dibujará en alzado, planta y sección, mientras lo cambia.

![El configurador de puertas y ventanas](shots/joinery-configurator.webp)

**Guardar tipo** conserva sus cambios. Todas las puertas o ventanas de ese tipo los siguen. **Colocar con él** cierra el configurador y empieza a colocar ese tipo.

### Ventanas con más hojas

**Columnas de hojas** recibe las anchuras relativas de las hojas, un número para cada una: `1 1` son dos hojas iguales, `1 1 1` tres, `1 2 1` una ancha entre dos estrechas. **Filas de hojas** divide la ventana en altura de la misma manera, de abajo arriba: `1 1` da dos filas. Seis hojas son `1 1 1` en Columnas de hojas y `1 1` en Filas de hojas. Después, en **Hojas**, elija cómo abre cada hoja: fija, practicable, oscilobatiente, abatible, proyectante, pivotante o corredera.

### Contraventanas, rejas, jardineras y marquesinas

<!-- video window-dressings -->

En **En la fachada**, un tipo de ventana puede tener:

- **Contraventanas**: de lamas, ciegas o de tablas, de una o dos hojas, mostradas abiertas (plegadas contra el muro junto a la ventana) o cerradas sobre ella;
- una **Reja de seguridad**: en el derrame por dentro (por defecto, donde suelen ir las rejas de seguridad), en el derrame por fuera, en la fachada o en la cara interior, con barrotes horizontales, verticales o en ambos sentidos, su sección y su separación;
- una **Jardinera** bajo la ventana;
- una **Marquesina** encima, con su vuelo y su pendiente.

Cada una se sitúa según las caras reales del muro donde está la ventana, a partir de las capas del muro, así que queda bien en cualquier tipo de muro. Cuando la ventana tiene recercado, lo que está en la fachada se separa del recercado. Puede mover cada una a mano: **Desde la cara del muro** fija cuánto se separa del muro (para una reja en el derrame, **Retranqueada desde la cara** fija cuánto entra), **Desde el eje de la ventana** la mueve a lo largo del muro, y **Arriba o abajo** la mueve en altura. Se ven en 3D, en plantas, alzados y secciones, y las tablas las miden.

![Contraventanas, una reja, una jardinera y una marquesina en el configurador](shots/joinery-dressings.webp)

![Las ventanas con sus complementos, en 3D](shots/window-dressings-3d.webp)

### Formas, estilos y ornamentos

**Forma** da a una puerta o ventana un remate en arco (**Arco de medio punto**, **Arco rebajado**, **Arco apuntado**, **Remate triangular**) o la hace **Redonda u oval** (redonda cuando el ancho y el alto son iguales). Un tipo con arco conserva sus hojas bajo un travesaño en el arranque del arco, y encima un fijo que sigue el arco, con barrotillos si se quiere: **Abanico**, **Los montantes prolongados** o **Cruz**. **Flecha del arco** fija cuánto sube un arco rebajado, apuntado o triangular. El hueco del muro, el marco, el vidrio, el recercado y los dibujos siguen el contorno.

**Junquillos** dividen los vidrios: **Cuadrícula (colonial)** con sus vidrios a lo ancho y a lo alto, **Prairie** con barras cerca de los bordes, o **Rombos**.

En la fachada, además del recercado, una ventana puede tener **Clave**, **Remate sobre el hueco** (**Cornisa**, **Frontón**, **Frontón curvo** o un **Guardapolvo** que sigue un remate en arco) y un **Antepecho bajo el vierteaguas** con sus ménsulas.

**Aplicar un estilo…** pone a la vez la forma, los junquillos y los ornamentos: Moderno, Clásico, Georgiano, Victoriano, Románico, Barroco, Gótico, Tudor, Prairie, Tradicional o una ventana redonda. Todo lo que pone sigue editable.

![Una ventana barroca: arco de medio punto con abanico, clave y frontón curvo](shots/joinery-styles.webp)

![Ventanas con arco en 3D](shots/window-styles-3d.webp)

### Manillas

**Manilla** elige la manilla del tipo: **Manilla de palanca**, **Pomo**, **Tirador de barra**, **Manilla de ventana** o ninguna. Por defecto, una puerta lleva manilla de palanca y una ventana manilla de ventana. Una puerta tiene manilla en ambas caras, a la **Altura de la manilla** que se indique; **Color de la manilla** le da su color. Las manillas se ven en 3D, en secciones y alzados, y en los dibujos del configurador cuando está marcado **Herrajes**.

## Etiquetas en planta

**Anotar → Etiquetas de puertas y ventanas** pone una marca en cada puerta y ventana de una planta: una línea a través del muro, la marca fuera, y el tamaño y el alféizar dentro. Para cambiar el aspecto de las etiquetas, vaya a **Gestionar → Normas de dibujo → Etiquetas**.
