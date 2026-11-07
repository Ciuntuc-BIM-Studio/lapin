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

- **Extrusión** (EXTRUDE) empuja el contorno en línea recta. Escriba S para ambos sentidos desde el croquis, T para una conicidad que inclina los lados hacia dentro, y Cut para que corte los elementos seleccionados antes. Para llevarlo hasta la cara de otro elemento, use **Hasta → una cara…** en sus propiedades, o **Extruir hasta cara**. La profundidad, las alturas y los espesores se escriben en la unidad del proyecto, como dice el mensaje (`Depth (mm)`: `2000` es 2 m).
- **Barrido** (SWEEP) lleva un perfil a lo largo de un recorrido. TWist gira la sección por el camino, SCale la agranda y RIse la eleva. Un círculo que sube se convierte en una hélice y da TUrns vueltas.
- **Solevado** (LOFT) une perfiles dibujados en vistas distintas. Escriba S para que pase suavemente por ellos. Una línea seleccionada con el papel de *segunda trayectoria* lo curva como guía.
- **Revolución** (REVOLVE) gira un perfil alrededor de una línea de eje.
- **Las primitivas** colocan con un clic una caja, un cilindro, una esfera, un cono, un toro, una cuña o una pirámide. Sus medidas y su posición se cambian en sus propiedades.

![Un pasamanos barrido a lo largo de un círculo que sube 3 m en dos vueltas](shots/studio-helix.webp)

Cada sólido queda ligado a su boceto. Cambie la forma y el sólido la sigue, con el mismo GlobalId. Seleccione un sólido para cambiar su clase IFC, su tipo predefinido y su tipo en el panel de propiedades. **Editar boceto** abre la vista en la que está el boceto.

Un sólido recién creado queda seleccionado, así que se ilumina con los pinzamientos de su boceto. En una planta, una sección o un alzado, donde la vista corta un sólido su corte se rellena de blanco: las líneas del boceto se convierten en una forma que se ve maciza. Un material con su propio relleno o sombreado lo dibuja con él.

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

Estas herramientas trabajan sobre las caras y aristas del cuerpo tal como está construido ahora. La cara bajo el ratón se ilumina.

**Empujar/Tirar**, **Dividir**, **Cortar** y **Desplazar 3D** se manejan sobre el propio modelo. Haga clic en una cara y aparece sobre ella una flecha: el ratón mueve la cara a lo largo de la flecha, y lo que hará el paso se dibuja mientras avanza. Empujar/Tirar dibuja en verde lo que añade y en rojo lo que quita. Una división o un corte dibuja su plano en amarillo. La distancia se lee junto a la flecha. Escriba un valor para fijarla con exactitud, como 500 o -250. Haga clic o pulse <kbd>Intro</kbd> para aplicarla, o <kbd>Esc</kbd> para dejarla; la herramienta sigue activa para la cara siguiente. <kbd>Intro</kbd> sin mover ni escribir nada repite la última distancia. Un arrastre avanza de centímetro en centímetro, o de 10 en 10 cm con <kbd>Mayús</kbd>. El botón derecho y el central siguen girando y desplazando la vista. Las demás herramientas piden su valor en una pequeña casilla junto al cursor.

- **Empujar/Tirar** (PU) mueve una cara hacia fuera, o hacia dentro cuando la distancia es negativa. En una extrusión, su extremo cambia la profundidad y su lado mueve el borde del croquis bajo él. En un muro o cualquier otro elemento, tirar añade un sólido en el material de la cara, y empujar corta el elemento.
- **Redondear arista** (FE) redondea la arista más cercana al clic, y **Achaflanar arista** (CE) la achaflana. Elija *alrededor de la cara* para tratar todas las aristas de la cara a la vez.
- **Vaciado** vacía los sólidos seleccionados, con paredes tan gruesas como escriba. Puede quedar cerrado, o abierto en su extremo, en su croquis o en ambos.
- **Dividir** corta un sólido en dos por un plano paralelo a la cara elegida, tan adentro de la cara como arrastre o escriba. Las dos partes conservan el croquis. **Cortar** conserva solo un lado: elija **Conservar detrás** o **Conservar delante** en la barra de la parte inferior de la vista.
- **Matriz** repite el sólido en fila, y **Matriz polar** lo repite alrededor de una vertical.
- **Girar 3D** gira el sólido alrededor de una arista que elija. **Simetría 3D** lo refleja en el plano de una cara. **Desplazar 3D** pone flechas según x, y y z sobre los sólidos seleccionados: arrastre una, o escriba una distancia para el eje que se muestra junto al valor (<kbd>Tab</kbd> pasa al eje siguiente).

![Empujar/Tirar: la cara superior estirada hacia arriba, lo que añade dibujado en verde y la distancia junto a la flecha](shots/studio-push-pull.webp)

![La arista redondeada: el redondeo sigue al bloque cuando cambia su profundidad](shots/studio-fillet.webp)

## Dibujar sobre una cara

En la vista 3D puede dibujar directamente sobre una cara y empujar o tirar de lo dibujado. El grupo **En cara** de la ficha **Studio** tiene **Rectángulo en cara** (FRE), **Círculo en cara** (FCI) y **Polígono en cara** (FPL).

1. Haga clic en una cara de cualquier elemento: un sólido Studio, un muro, una losa o un pilar. El clic es el primer punto: la esquina de un rectángulo, el centro de un círculo o la primera esquina de un polígono. La cara queda contorneada mientras dibuja.
2. Mueva el ratón. La forma lo sigue sobre la cara, con su tamaño al lado. Los puntos se enganchan a las esquinas de la cara, a los puntos medios de sus aristas y a su centro; si no, avanzan de centímetro en centímetro, o de 10 en 10 cm con <kbd>Mayús</kbd>. Haga clic en la esquina opuesta o en un punto del círculo. También puede escribir el tamaño: el ancho y el alto de un rectángulo como `1200, 600`, o el radio de un círculo. Un polígono toma un clic en cada esquina, o una longitud escrita en la dirección del ratón. Haga clic en su primera esquina o pulse <kbd>Intro</kbd> para cerrarlo; <kbd>Retroceso</kbd> quita la última esquina.
3. En cuanto la forma se cierra, aparece sobre ella una flecha, como con **Empujar/Tirar**. Tire hacia fuera y se convierte en un sólido apoyado en la cara, en el material de la cara. Empuje hacia dentro y se convierte en un corte en el elemento: un nicho, un cajeado o un agujero cuando lo atraviesa. Haga clic o pulse <kbd>Intro</kbd> para aplicarlo. <kbd>Esc</kbd>, o una distancia de 0, deja la forma sobre la cara como croquis, para extruirla más tarde.

La forma es un croquis sobre un plano de referencia que descansa en la cara. El plano lleva el nombre del elemento y de la cara, como *Extrude 1, north face*, y las formas dibujadas sobre la misma cara lo comparten. La vista 3D no dibuja estos planos, porque la cara ya muestra dónde están. Abra uno desde el explorador del proyecto para cambiar el croquis en 2D; el sólido o el corte lo sigue. **Empujar/Tirar** sobre el extremo del sólido cambia su profundidad. **Empujar/Tirar** sobre el fondo de un cajeado hace el corte más profundo o menos profundo, en lugar de añadir al elemento.

![Un rectángulo dibujado en el lateral de un bloque, con su tamaño junto al cursor](shots/studio-on-face.webp)

## Bocetar en 3D

El grupo **Boceto** de la ficha **Studio** dibuja líneas y formas directamente en la vista 3D, de dos maneras que guardan lo mismo: un boceto en el plano de referencia de la cara, como arriba.

**Directo sobre la cara, como en SketchUp.** Sin ningún boceto abierto, **Línea** (KL), **Rectángulo** (KR) y **Círculo** (KC) dibujan sobre la cara bajo el cursor. La cara se ilumina al pasar sobre ella; en el espacio vacío dibuja en el nivel de la planta activa. El primer clic fija el plano.

- El cursor infiere sobre qué está y lo dice a su lado: **Extremo**, **Punto medio**, **En arista**, **Intersección**, **En cara**. Desde el último punto sigue los ejes del modelo que están sobre la cara, dibujados en sus colores (rojo x, verde y, azul z), y va **Paralelo a la arista** o **Perpendicular a la arista** en magenta.
- <kbd>→</kbd> fija el rojo, <kbd>←</kbd> el verde, <kbd>↑</kbd> el azul, <kbd>↓</kbd> la dirección de la arista inferida por última vez; la misma tecla lo suelta. Mantenga <kbd>Mayús</kbd> para conservar la dirección inferida ahora.
- Escriba un valor en cualquier momento, sin hacer clic en un cuadro, y pulse <kbd>Intro</kbd>: `1200` es la longitud del siguiente segmento en la dirección que señala el ratón (`1000<30` a 30°), `1200,600` el ancho y el alto de un rectángulo, `300` o `r300` el radio de un círculo.
- Una línea sigue punto a punto. De vuelta en su primer punto el lazo se cierra y la flecha de empujar/tirar queda enseguida sobre él; <kbd>Esc</kbd> lo mantiene como boceto. <kbd>Intro</kbd> o <kbd>Esc</kbd> antes de eso dejan la línea abierta, como un boceto en la cara. <kbd>Retroceso</kbd> quita el último punto. La herramienta sigue activa para la forma siguiente; <kbd>Esc</kbd> sin nada dibujado la termina.

![Una línea dibujada en el lateral de un bloque, el eje rojo inferido desde su último punto](shots/studio-sketch-3d.webp)

**En un plano de trabajo, como en CAD.** **Boceto en cara** (KF) y un clic en una cara hacen de esa cara el plano de trabajo activo: se ilumina y una etiqueta dice *Boceto en* la cara. **Boceto en plano** hace lo mismo con el plano de referencia seleccionado. Mientras está activo, **Línea**, **Rectángulo**, **Círculo** y **Arco** (KA) inician sobre él los propios LINE, RECTANGLE, CIRCLE y ARC de la línea de comandos, y cada comando de **Dibujo** que escriba funciona sobre él en las coordenadas propias del plano, con las referencias a objetos, el modo ortogonal y el rastreo de una planta, entre ellas las aristas de la cara. **Terminar boceto** (KE, o <kbd>Intro</kbd> sin ningún comando en marcha) suelda las líneas dibujadas y pone la flecha de empujar/tirar en la última forma cerrada dibujada; <kbd>Esc</kbd> sale del boceto y lo dibujado se queda. **Mirar al plano** (KP) gira la vista perpendicular al plano.

![Boceto en cara: la parte superior de un bloque iluminada como plano de trabajo, un rectángulo dibujado en ella con la línea de comandos](shots/studio-sketch-plane.webp)

**Las líneas dividen las caras.** Una línea trazada a través de una cara, de arista a arista (o un lazo cerrado de líneas y arcos dentro de ella), la divide en regiones. La cara se ilumina región por región bajo el cursor, y **Empujar/Tirar** sobre una región empuja o tira solo de esa región: un sólido propio sobre la cara o un corte en ella. Las líneas que se quedan cortas de una arista no dividen nada.

- **Arco** (KA), sin ningún boceto abierto, dibuja también sobre la cara, a la manera de SketchUp: haga clic en el inicio, en el final y luego en cuánto se curva (o escriba la longitud de la cuerda y después la flecha).
- **Desfase** (KO) hace clic en una cara, o en una región de ella, y dibuja su contorno de nuevo hacia dentro o hacia fuera por el cursor, o con la distancia que escriba; la forma cerrada recibe enseguida la flecha de empujar/tirar.
- Con un boceto abierto, **Desfase**, **Recortar**, **Alargar** y **Empalme** ejecutan sobre el plano de trabajo los OFFSET, TRIM, EXTEND y FILLET de la línea de comandos.
- Una forma dibujada en un plano de referencia y seleccionada en la vista 3D muestra allí sus puntos. Arrastre uno: se mueve sobre el plano, con las referencias de las herramientas de boceto, y el sólido hecho con el boceto lo sigue.
- Ambas maneras también se ajustan a las aristas de otros elementos que están en el plano, como el lateral de un bloque vecino a ras de la cara.

![Una línea a través del lateral de un bloque lo divide: la región bajo ella se saca por sí sola](shots/studio-sketch-region.webp)

**Cinta métrica, guías y Sígueme.**

- **Cinta métrica** (KM) mide y deja guías, como la de SketchUp. Haga clic en una arista, aléjese de ella y haga clic: una línea guía corre paralela a la arista por ese punto, o a la distancia que escriba. Haga clic en un punto en cambio, y el segundo clic muestra la distancia y deja allí un punto guía. Las guías son discontinuas, ambas maneras de bocetar se ajustan a ellas (y a donde cruzan la cara), se guardan con el proyecto y nunca se exportan ni se imprimen. **Borrar guías** las quita todas.
- **Sígueme** (FM) barre una cara a lo largo de una trayectoria donde está: haga clic en la cara (o en una región suya dividida por líneas), luego en la trayectoria, ya sea una línea dibujada, polilínea, arco o círculo, o bien otra cara cuyas aristas sigue alrededor, como una cara superior para una cornisa. Las esquinas se cortan a inglete. La cara se convierte en un boceto en su plano y el resultado es un único sólido de Studio que sigue ambos bocetos.
- **Atajos de SketchUp** activa las teclas de una letra de SketchUp en la vista 3D, para este equipo: <kbd>L</kbd> línea, <kbd>R</kbd> rectángulo, <kbd>C</kbd> círculo, <kbd>A</kbd> arco, <kbd>P</kbd> empujar/tirar, <kbd>F</kbd> desfase, <kbd>T</kbd> cinta métrica, <kbd>M</kbd> mover, <kbd>E</kbd> borrar, <kbd>Espacio</kbd> seleccionar. Están desactivados de forma predeterminada porque ocuparían el lugar de los atajos de dos letras de Lapin que empiezan por esas letras en la vista 3D; los atajos de dos letras y la línea de comandos funcionan como antes en todos los demás sitios.

![Una guía a una distancia escrita de la arista del bloque y una cornisa barrida alrededor de la parte superior con Sígueme](shots/studio-sketch-follow.webp)

## Los pasos

Las propiedades de un sólido Studio enumeran sus pasos en orden. Cada paso se puede desactivar, adelantar o retrasar, cambiar o quitar. Cambie el croquis, y cada paso se rehace sobre el cuerpo nuevo. Un paso que ya no encuentra su cara o su arista lo dice en **Problemas**.

![Un cilindro vaciado en un tubo y repetido tres veces: sus dos pasos en sus propiedades](shots/studio-steps.webp)

Las propiedades de una polilínea enumeran sus lados. Escriba la longitud de un lado, y su extremo se mueve a lo largo de él. Los lados siguientes lo siguen hasta el próximo lado que va en la misma dirección, así que un rectángulo sigue siendo un rectángulo. Un sólido dibujado en ella también la sigue.

## Mallas de otros programas

**Archivo → Importar malla (OBJ, STL, glTF)…** trae una malla como referencias que puede ver y a las que puede forzar el cursor. Un archivo OBJ o STL no dice su unidad ni qué eje va hacia arriba, así que Lapin lo pregunta. Seleccione una referencia y haga clic en **Convertir en sólido Studio** para poder editarla. Entonces admite empujar/tirar, redondeos, booleanas y los demás pasos. La misma herramienta funciona con muros, losas y elementos de archivos IFC.

## Booleanas

**Restar**, **Unión** e **Intersección** combinan cualesquiera elementos, siendo el primero seleccionado el objetivo. Las herramientas siguen siendo editables, y el resultado las sigue. **Mostrar operandos** muestra las herramientas ocultas de una unión para editarlas. **Quitar la operación** vuelve a separar los elementos.
