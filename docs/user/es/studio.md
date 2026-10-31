---
title: Sólidos de Studio y planos de trabajo
order: 85
summary: Convierta formas dibujadas en sólidos, dé espesor a una forma con Anillo, Caja y Franja de muro, y dibuje sobre planos de referencia en cualquier lugar del modelo.
---

## Sólidos a partir de un boceto

La ficha **Studio** convierte en sólidos las formas que usted dibuja. Dibuje la forma con **Dibujo** en una planta, una sección, un alzado o un plano de referencia, y después selecciónela con una herramienta de Studio:

- **Extrusión** (<kbd>EXT</kbd>) empuja una forma cerrada una profundidad. Las formas cerradas que hay dentro se convierten en huecos.
- **Barrido** (<kbd>SW</kbd>) desplaza un perfil cerrado a lo largo de un recorrido, **Revolución** (<kbd>REV</kbd>) lo gira alrededor de un eje y **Solevado** (<kbd>LO</kbd>) une perfiles dibujados en vistas distintas.

![La ficha Studio](shots/ribbon-studio.webp)

Cada sólido queda ligado a su boceto. Cambie la forma y el sólido la sigue, con el mismo GlobalId. Seleccione un sólido para cambiar su clase IFC, su tipo predefinido y su tipo en el panel de propiedades. **Editar boceto** abre la vista en la que está el boceto.

## Anillo, Caja y Franja de muro

Estas tres herramientas dan a una forma dibujada un espesor en su propio plano y luego una altura respecto a ese plano:

- **Anillo** (<kbd>RING</kbd>) toma un círculo, un arco o un contorno cerrado y crea un anillo o un tubo. Su espesor va por el **interior**, por el **exterior** o **centrado** en la forma.
- **Caja** (<kbd>BOX</kbd>) toma un rectángulo, una polilínea cerrada o un círculo y crea los muros a su alrededor. Un fondo (**Inferior**) y una tapa (**Superior**) son opcionales, cada uno con su propio espesor.
- **Franja de muro** (<kbd>TWALL</kbd>) toma una línea, una polilínea o un arco y crea una franja de muro. Su espesor va **a la izquierda**, **a la derecha** o **centrado**, visto recorriendo la línea en el sentido en que la dibujó. Sus esquinas se unen a inglete.

Inicie la herramienta y haga clic en la forma. La línea de comandos ofrece las opciones como letras: <kbd>I</kbd>, <kbd>O</kbd> y <kbd>C</kbd> (o <kbd>L</kbd>, <kbd>R</kbd> y <kbd>C</kbd>) para indicar dónde va el espesor, y en una caja <kbd>B</kbd> y <kbd>T</kbd> para activar o desactivar el fondo y la tapa. Después escriba el espesor y la altura, en metros, o pulse <kbd>Enter</kbd> para mantener el valor mostrado.

**En una planta**, las herramientas crean muros nativos de forma predeterminada, del tipo de muro elegido en **Arquitectura**. Una forma cerrada se convierte en un bucle de muros unidos en sus esquinas, una polilínea en una cadena de muros, y un arco o un círculo en muros curvos. El lado donde va el espesor decide dónde se colocan los muros: todo el espesor del tipo de muro va en el lado elegido. El fondo y la tapa de una caja se convierten en forjados. Escriba <kbd>S</kbd> antes de seleccionar la forma para crear en su lugar un sólido de Studio.

**En una sección, un alzado o un plano de referencia**, las herramientas crean un sólido de Studio de la clase IFC IfcWall; el fondo y la tapa de una caja son losas separadas (IfcSlab). La altura va según la normal del plano, hacia usted; una altura negativa se aleja de usted, como con **Extrusión**. Para dar un tipo a ese muro, selecciónelo y elija o cree uno en **Tipo** en el panel de propiedades.

![Un anillo de muros curvos nativos alrededor de un círculo, una caja con su fondo y su tapa, y una franja de muro, en la planta baja](shots/studio-thick-plan.webp)

Todo sigue siendo paramétrico. Seleccione el sólido y cambie su **Espesor**, su **Altura** y **El espesor va** en el panel de propiedades. En una caja, marque **Inferior** o **Superior** y defina sus espesores: Lapin añade o quita las losas. Una caja con fondo o tapa se agrupa con ellos en un conjunto (IfcElementAssembly). Arrastre los pinzamientos de la forma, o edítela en su vista, y el sólido se reconstruye.

![Las propiedades de una caja: su espesor, su altura, su fondo y su tapa](shots/studio-box-properties.webp)

## Planos de referencia

Un plano de referencia es un plano situado en cualquier lugar del modelo, con una vista propia. Se dibuja en él con Dibujo, como en una sección, y Studio crea sólidos sobre él. Cree uno desde la ficha **Studio**:

- **Plano desde la vista** (<kbd>REFPLANE</kbd>) queda paralelo a la planta, la sección, el alzado o el plano que tiene delante. Escriba a qué distancia está hacia usted (un valor negativo lo aleja).
- **Plano en cara**: en la vista 3D, haga clic en una cara de cualquier elemento. El plano queda sobre la cara, mirando hacia fuera de ella.
- **Plano por 3 puntos**: en la vista 3D, haga clic en tres puntos del modelo. El plano pasa por ellos.

El plano nuevo se abre al momento. Aparece en **Planos de referencia** en el explorador del proyecto. Su dibujo es una sección sobre el plano, mirando contra el lado hacia el que está orientado.

En la vista 3D, cada plano de referencia se dibuja como una lámina translúcida con su nombre, tan grande como el modelo sobre él (o según **Ancho dibujado (3D)** y **Alto dibujado (3D)**, 2 × 2 m como mínimo), resaltada bajo el cursor. Haga clic en ella para seleccionar el plano, doble clic para abrir su vista. Con el plano seleccionado, arrastre la flecha de su centro para moverlo a lo largo de su normal: el desplazamiento se lee junto a la flecha, y lo dibujado en el plano va con él. **Vista → Planos de referencia** oculta y muestra las láminas; nunca entran en las exportaciones, los renders ni las imágenes. En plantas, secciones y alzados, un plano que corta la vista se dibuja como una línea discontinua con su nombre, a lo ancho de la vista: un clic selecciona el plano, un doble clic lo abre. Un plano paralelo a la vista no se dibuja en ella (no corta nada de ella). Estas líneas son solo para la pantalla: nunca se imprimen en las hojas.

![Un plano de referencia en el alzado sur, con un anillo dibujado sobre él](shots/studio-plane.webp)

Seleccione el plano en el explorador para moverlo. **Origen x**, **y** y **z** lo sitúan. **Orientado** es la dirección en planta hacia la que mira, **Inclinación** cuánto se inclina hacia atrás (a 90° queda tumbado, mirando hacia arriba) y **Giro en el plano** gira su dibujo alrededor de su normal. **Mover según su normal…** y **Girar sobre la vertical…** hacen lo mismo en un solo paso. Lo que está dibujado en el plano, y cada sólido creado a partir de él, se mueve con él.

![Las propiedades de un plano de referencia](shots/studio-plane-properties.webp)

Las secciones y los alzados también son planos. Un sólido bocetado en una sección se apoya en su línea de corte, y la sigue cuando usted la mueve. En una sección desarrollada, se apoya en el tramo en el que está dibujado. Un alzado toma un plano propio la primera vez que crea un sólido en él: en la parte delantera del modelo, con su x tal como está dibujada ahora. Cambie su **Profundidad del plano de boceto** en las propiedades del alzado para mover el plano, y sus sólidos, a lo largo de la vista.

![La casa con sus sólidos de Studio en 3D](shots/studio-3d.webp)

En IFC, un anillo, los muros de una caja y una franja de muro son elementos IfcWall, el fondo y la tapa de una caja elementos IfcSlab, y una caja con ellos un IfcElementAssembly con IfcRelAggregates; sus tipos se escriben como IfcWallType e IfcSlabType. Toda exportación 3D (IFC, Fragments, glTF, USD, COLLADA) escribe un sólido de Studio donde está en el modelo, sea cual sea el plano en el que se bocetó, con sus huecos; un conjunto es un nodo de grupo sobre sus partes. Al abrir de nuevo el IFC propio de Lapin, los sólidos vuelven como referencias, en su sitio, con su clase, su tipo y su conjunto, pero ya no se pueden editar (sus bocetos no están en el archivo). El historial de cada sólido se guarda en el conjunto de propiedades propio de Lapin, Lapin_Intent. Los sólidos de Studio solo admiten tipos simples, sin capas: ese es el modelado libre para el que existen; los muros con capas son los muros nativos de Lapin.
