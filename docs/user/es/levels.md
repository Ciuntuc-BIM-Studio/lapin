---
title: Niveles y plantas
order: 60
summary: Añada niveles, fije sus cotas y vea otro nivel bajo la planta en la que dibuja.
---

<!-- video levels -->

## Niveles

El navegador del proyecto muestra los niveles arriba, el más alto primero. **+** junto a **Niveles** añade un nivel por encima del más alto.

Seleccione un nivel para ver su **Nivel de base** y su **Nivel superior**. Cualquiera de los dos puede estar por encima o por debajo de ±0.00. Su altura es la distancia entre ellos. Cuando mueve la base, la parte superior se queda donde está. Un proyecto nuevo empieza con Foundations, Ground floor y First floor. Un nivel mide como máximo 100 m de alto y está a ±10 km de ±0.00: un valor escrito en otra unidad se rechaza. Una escalera que subiría más de 50 m hasta el nivel superior se detiene ahí y dice por qué.

![Las propiedades de un nivel: nivel de base, nivel superior y altura](shots/level-properties.webp)

Cada nivel tiene su propia planta en **Plantas**. Haga doble clic en una planta para dibujar en ese nivel. El nivel en el que dibuja aparece en el recuadro de arriba a la izquierda de la vista.

En secciones y alzados cada nivel se marca a la derecha con su nombre y su cota. **Gestionar → Estilos de objeto → Líneas ocultas** fija también la **marca de nivel**: triángulo vacío, triángulo con la mitad izquierda negra (como lo dibuja la norma rumana), triángulo negro o trazo, y su tamaño en papel.

Las cotas de nivel se escriben en las unidades del proyecto: en metros (+3.00) en un proyecto métrico, como las escriben los planos, y en pies y pulgadas (+9'-10 1/8") en uno imperial. Las siguen el navegador, el recuadro de arriba a la izquierda de la vista, las marcas de nivel de secciones y alzados y las cotas de nivel.

## Ver el nivel inferior

Para ver otro nivel mientras dibuja, elíjalo en **Calco** en la barra de la planta. La planta de ese nivel se dibuja debajo, atenuada y en gris. Puede forzar el cursor a ella, pero no puede seleccionarla.

![La primera planta dibujada sobre la planta baja, mostrada como calco](shots/underlay.webp)

El calco solo le ayuda a dibujar; no se imprime. Para imprimir en una planta los elementos de otros niveles, abra las propiedades de la vista de planta y elíjalos en **Plantas mostradas**. Puede mostrar algunos niveles o todos, en medio tono si lo desea.

## Copiar a otras plantas

Para repetir muros, losas o una planta entera en otros niveles, selecciónelos (muros con sus puertas y ventanas, losas, locales, un grupo…) y elija **Modificar → Copiar a plantas**. También puede escribir `COPYSTOREY` en la línea de comandos o hacer clic derecho sobre la selección en una planta o en la vista 3D. Marque los niveles a los que copiar. **Todas las de arriba** y **Todas las de abajo** marcan todos los niveles por encima o por debajo del de la selección, y Mayús+clic marca todos los niveles entre el pulsado y el último pulsado. El nivel en el que está la selección no se puede marcar.

![Copiar a plantas: dos niveles marcados y las opciones](shots/copy-to-storeys.webp)

- **Relativa** (por defecto): cada copia queda a la misma altura sobre su nuevo nivel que el original sobre el suyo. Un muro dibujado en la planta baja queda en la primera planta.
- **Absoluta**: cada copia se queda donde está el original en el espacio, en el nuevo nivel. Su desfase de base compensa la diferencia.
- **También los elementos alojados**: las puertas y ventanas van con sus muros y los huecos con lo que cortan. Las etiquetas y cotas de los elementos copiados pasan de la planta de su nivel a la del nuevo nivel. Un local se lleva sus acabados y una escalera el espacio bajo ella. Desmarque la opción para copiar los muros sin sus puertas y ventanas.
- **Rehacer las relaciones entre las copias**: los muros unidos en esquina o en T se unen igual en cada nuevo nivel. Un muro fijado a una losa o a una cubierta copiada con él se fija a esa copia. Un grupo se convierte en un grupo de las copias y un conjunto se copia entero. Una escalera sube hasta el nivel sobre el nuevo y corta su propio hueco en la losa de allí.
- **Omitir duplicados**: un nivel que ya tiene el mismo elemento en el mismo lugar no recibe otro. El mensaje de abajo dice cuántos se omitieron.

Las copias usan los mismos tipos que los originales y reciben GlobalIds IFC nuevos. Todas las copias son un solo paso de deshacer y quedan seleccionadas después, para moverlas o cambiarlas juntas. En una sesión compartida, solo el invitado deshace sus propias copias.

![Los muros y la losa de la planta baja copiados a los dos niveles de arriba, en 3D](shots/copy-to-storeys-3d.webp)

Para mover la selección a otro nivel, elija **Modificar → Mover a planta**. Conserva las alturas de los elementos sobre el nuevo nivel o los deja donde están en el espacio.

## La elevación de un elemento

Un elemento de un nivel está a la elevación del nivel y a un desfase de ella. **Propiedades → Colocación** muestra su **Planta**, su **Desfase del nivel** y su **Elevación** sobre ±0,00: la base de un muro o pilar, la parte superior de una losa o viga (el desfase de una viga es desde la parte superior del nivel), la parte inferior de una zapata (el nivel inferior de su tipo bajo el nivel, más el desfase). Escriba una de las cifras y la otra la sigue: el elemento se mueve en todas las vistas, en un solo paso de deshacer. Para poner un elemento en otro nivel, cambie su **Planta**. Lo que dibuje en una planta está en el nivel de esa planta.
