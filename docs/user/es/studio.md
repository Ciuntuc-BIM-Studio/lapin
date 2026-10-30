---
title: Studio
order: 85
summary: Modele sólidos libres, dibujados en cualquier cara o plano, y empuje, tire, redondee, vacíe, divida y repita en 3D.
---

## La ficha Studio

La ficha **Studio** crea sólidos que ninguna otra ficha construye: un banco, una marquesina, un pasamanos, una fuente, un zócalo en una fachada. Cada sólido parte de un croquis o de una primitiva. Después lo cambia en 3D con pasos, y cada paso sigue siendo editable.

![La ficha Studio](shots/ribbon-studio.webp)

Un sólido Studio tiene una clase IFC, como cualquier elemento. Elíjala en sus propiedades: una barra, una placa, un revestimiento, una barandilla, una losa u otra. Sus cantidades y su exportación siguen esa clase.

## Dónde se dibuja

El contorno de un sólido se dibuja con las herramientas de **Dibujo**, como polilíneas cerradas, rectángulos y círculos. Las líneas y los arcos que se unen extremo con extremo también forman un contorno cerrado. Una forma cerrada dentro del contorno es un hueco.

Puede dibujar en cuatro clases de vista:

- **Una planta** dibuja a la cota de su planta.
- **Una sección** dibuja sobre su línea de corte, en vertical.
- **Un alzado** o **un plano de referencia**: vea *Planos de referencia* más abajo.

## Crear un sólido

- **Extrusión** (EXTRUDE) empuja el contorno en línea recta. Escriba S para ambos sentidos desde el croquis, T para una conicidad que inclina los lados hacia dentro, y Cut para que corte los elementos seleccionados antes. Para llevarlo hasta la cara de otro elemento, use **Hasta → una cara…** en sus propiedades, o **Extruir hasta cara**.
- **Barrido** (SWEEP) lleva un perfil a lo largo de un recorrido. TWist gira la sección por el camino, SCale la agranda y RIse la eleva. Un círculo que sube se convierte en una hélice y da TUrns vueltas.
- **Solevado** (LOFT) une perfiles dibujados en vistas distintas. Escriba S para que pase suavemente por ellos. Una línea seleccionada con el papel de *segunda trayectoria* lo curva como guía.
- **Revolución** (REVOLVE) gira un perfil alrededor de una línea de eje.
- **Las primitivas** colocan con un clic una caja, un cilindro, una esfera, un cono, un toro, una cuña o una pirámide. Sus medidas y su posición se cambian en sus propiedades.

![Un pasamanos barrido a lo largo de un círculo que sube 3 m en dos vueltas](shots/studio-helix.webp)

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
## Cambiar un sólido en 3D

Estas herramientas trabajan sobre las caras y aristas del cuerpo tal como está construido ahora. La cara bajo el ratón se ilumina. Cuando una herramienta necesita un valor, se abre una pequeña casilla junto al cursor. Escriba el valor y pulse <kbd>Intro</kbd>, o <kbd>Esc</kbd> para dejarlo.

- **Empujar/Tirar** (PU) mueve una cara hacia fuera una distancia, o hacia dentro cuando la distancia es negativa. En una extrusión, su extremo cambia la profundidad y su lado mueve el borde del croquis bajo él. En un muro o cualquier otro elemento, tirar añade un sólido en el material de la cara, y empujar corta el elemento.
- **Redondear arista** (FE) redondea la arista más cercana al clic, y **Achaflanar arista** (CE) la achaflana. Elija *alrededor de la cara* para tratar todas las aristas de la cara a la vez.
- **Vaciado** vacía los sólidos seleccionados, con paredes tan gruesas como escriba. Puede quedar cerrado, o abierto en su extremo, en su croquis o en ambos.
- **Dividir** corta un sólido en dos por el plano de la cara elegida, tan adentro de la cara como escriba. Las dos partes conservan el croquis. **Cortar** conserva solo un lado.
- **Matriz** repite el sólido en fila, y **Matriz polar** lo repite alrededor de una vertical.
- **Girar 3D** gira el sólido alrededor de una arista que elija. **Simetría 3D** lo refleja en el plano de una cara. **Desplazar 3D** lo mueve en x, y y z.

![Empujar/Tirar: la cara superior iluminada y la distancia escrita junto al cursor](shots/studio-push-pull.webp)

![La arista redondeada: el redondeo sigue al bloque cuando cambia su profundidad](shots/studio-fillet.webp)

## Los pasos

Las propiedades de un sólido Studio enumeran sus pasos en orden. Cada paso se puede desactivar, adelantar o retrasar, cambiar o quitar. Cambie el croquis, y cada paso se rehace sobre el cuerpo nuevo. Un paso que ya no encuentra su cara o su arista lo dice en **Problemas**.

![Un cilindro vaciado en un tubo y repetido tres veces: sus dos pasos en sus propiedades](shots/studio-steps.webp)

Las propiedades de una polilínea enumeran sus lados. Escriba la longitud de un lado, y su extremo se mueve a lo largo de él. Los lados siguientes lo siguen hasta el próximo lado que va en la misma dirección, así que un rectángulo sigue siendo un rectángulo. Un sólido dibujado en ella también la sigue.

## Mallas de otros programas

**Archivo → Importar malla (OBJ, STL, glTF)…** trae una malla como referencias que puede ver y a las que puede forzar el cursor. Un archivo OBJ o STL no dice su unidad ni qué eje va hacia arriba, así que Lapin lo pregunta. Seleccione una referencia y haga clic en **Convertir en sólido Studio** para poder editarla. Entonces admite empujar/tirar, redondeos, booleanas y los demás pasos. La misma herramienta funciona con muros, losas y elementos de archivos IFC.

## Booleanas

**Restar**, **Unión** e **Intersección** combinan cualesquiera elementos, siendo el primero seleccionado el objetivo. Las herramientas siguen siendo editables, y el resultado las sigue. **Mostrar operandos** muestra las herramientas ocultas de una unión para editarlas. **Quitar la operación** vuelve a separar los elementos.
