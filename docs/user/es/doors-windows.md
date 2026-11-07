---
title: Puertas y ventanas
order: 30
summary: Coloque puertas y ventanas en los muros, muévalas y diseñe sus tipos en el configurador.
---

<!-- video doors-windows -->

## Colocar una puerta o una ventana

Elija **Arquitectura → Puerta** (<kbd>DR</kbd>) o **Ventana** (<kbd>WN</kbd>) y haga clic en un muro donde quiera el hueco. Lapin corta el hueco en todas las capas del muro y añade el dintel que pide el tipo de muro.

Mientras Door o Window está activa, el panel de propiedades muestra el tipo que colocará, con su tamaño: elija otro ahí antes de hacer clic en el muro.

![Una planta con una puerta y ventanas](shots/openings-plan.webp)

Para mover una puerta o una ventana a lo largo de su muro, selecciónela y arrástrela. Para cambiarla, use el panel de propiedades: su tipo, la altura del alféizar y hacia qué lado abre (**Lado**). Con varias puertas y ventanas seleccionadas, **Mano**, **Lado**, el alféizar y el tipo las cambian todas en un solo paso, y también una flecha de giro en la planta.

Para una distancia exacta, use **Desplazar** (<kbd>M</kbd>) o **Copiar** (<kbd>CO</kbd>) en la línea de comandos, como con cualquier elemento: seleccione la puerta, la ventana o el hueco del muro, haga clic en un punto base y luego escriba la distancia (`500`) o haga clic en un segundo punto. Solo cuenta la parte del desplazamiento a lo largo del muro: el hueco se desliza por su muro y conserva su alféizar. Si lo lleva a otro muro de la misma planta, pasa a ese muro y sigue abriendo igual hacia la habitación. Una copia es una puerta o ventana nueva del mismo tipo, con el número siguiente. Si no cabe (más allá del final del muro o sobre otro hueco), no se mueve nada. **Girar** y **Simetría** lo colocan del mismo modo, y la simetría le cambia la mano.

![Las propiedades de una puerta](shots/door-properties.webp)

## Huecos sin puerta ni ventana

**Architecture → Wall opening** (<kbd>WO</kbd>) abre un hueco solo en un muro donde se hace clic: un paso, una trampilla, un hueco para una puerta que vendrá después. Su ancho, alto y antepecho se fijan en el panel de propiedades antes del clic. Seleccionado, ahí se cambian su tamaño, su posición a lo largo del muro y su antepecho; como una puerta, debe caber en el muro y no tocar los demás huecos. En IFC es una abertura que vacía el muro, sin nada que la rellene.

Seleccionado, un hueco tiene pinzamientos como una ventana: arrastra su centro para deslizarlo a lo largo del muro, o una jamba para ensancharlo o estrecharlo mientras la otra queda quieta; la longitud junto al ratón se puede escribir, como al dibujar. Se elige con un clic en su vano en la planta, o en su agujero en la vista 3D, donde se ilumina su contorno. **Mover** (<kbd>M</kbd>) lo desliza a lo largo del muro una distancia escrita, y su altura y antepecho están en el panel de propiedades.

**Forma superior** da forma a su remate: **Rectángulo**, **Arco rebajado** (un arco poco peraltado), **Arco elíptico** (media elipse, de una cuarta parte del ancho de alto salvo que se indique otra cosa), **Semicírculo** o **Esquinas redondeadas** (un remate recto con las esquinas superiores redondeadas). **Flecha del arco** fija cuánto sube un remate rebajado o elíptico, **Radio de las esquinas** cuán redondas son las esquinas (150 mm salvo que se indique otra cosa, nunca más de la mitad del ancho); vacío, el propio de la forma. El hueco del muro sigue el contorno en 3D, en alzados y secciones y en IFC; la planta muestra sus jambas como antes.

## El configurador

**Arquitectura → Puertas y ventanas** abre el configurador. Todos los tipos de puerta y ventana del proyecto aparecen a la izquierda. Puede cambiar:

- el nombre y la marca del tipo (la letra de las etiquetas);
- su anchura y su altura;
- la profundidad y la anchura del marco;
- sus columnas y filas de hojas, y cómo abre cada hoja;
- el acabado de la mocheta, el recercado en la fachada, el umbral y los colores.

Los dibujos de la derecha muestran el tipo tal como se dibujará en alzado, planta y sección, mientras lo cambia. Debajo, una **vista previa 3D** muestra el tipo en un trozo de muro del primer tipo de muro del proyecto, con su mocheta, alféizares, forro y materiales, mientras lo cambia (también una ventana en mirador, de arco o de caja): arrastre para girarla, use la rueda para ampliar y arrastre con el botón derecho para desplazarla. Empieza vista desde el exterior.

**Izquierda y derecha.** La apertura de una puerta se nombra como la nombran los arquitectos: mire desde la manilla hacia las bisagras; una hoja que abre a la izquierda de esa línea abre a la izquierda, una que abre a la derecha, a la derecha. Las opciones del configurador para una hoja lo dicen así, con el lado de sus bisagras en la **Apertura** de un paño, vista desde el exterior, el lado desde el que se empujan las hojas: *Batiente derecha (bisagras a la izquierda)*. Un archivo IFC nombra la mano a su manera (las bisagras vistas mirando hacia donde abre la hoja), así que la misma puerta se escribe allí como SINGLE_SWING_LEFT; Lapin convierte una en otra al escribir y leer IFC. La izquierda y la derecha de una ventana se ven desde el interior, el lado hacia el que abren sus hojas: *Practicable derecha (bisagras a la izquierda)* en el configurador, cuyos paños se ven desde el exterior. IFC escribe la misma ventana como izquierda. El alzado se ve desde el interior, el lado hacia el que abren las hojas, salvo que marque **Desde el exterior**; como la planta, muestra la puerta tal como está en el modelo. Una puerta volteada a la otra mano (**Mano**, o una flecha de giro a lo largo del muro) es la imagen en espejo de su tipo; las tablas la muestran en una ficha propia (consulte [Tablas](schedules.md)).

![El configurador de puertas y ventanas](shots/joinery-configurator.webp)

**Guardar tipo** conserva sus cambios. Todas las puertas o ventanas de ese tipo los siguen. **Colocar con él** cierra el configurador y empieza a colocar ese tipo.

### Paños, montantes y perfiles

Una puerta o una ventana es una cuadrícula de paños. Cada paño elige cómo abre y qué lo rellena, así que un solo tipo puede tener dos hojas con fijos laterales y un fijo superior encima, o una ventana con un panel ciego bajo una hoja.

**Columnas (relativas)** y **Filas (relativas)** dividen el hueco rápidamente: `1 1` son dos paños iguales, `1 1 1` tres, `2 1` uno ancho junto a uno estrecho. Las filas se cuentan de abajo arriba.

**Anchos de columna** y **Altos de fila** dan las mismas medidas en milímetros. Escríbalas y la última ocupa el resto. En el alzado también puede arrastrar un montante o un travesaño; la distancia se ve mientras arrastra.

**Ancho del montante** y **Ancho del travesaño** son medidas propias, aparte del **Ancho del marco**. Un marco de 50 mm con montantes de 50 mm se lee 50 mm en todas partes.

**Paños** lista todos los paños. Haga clic en uno, en la lista o en el alzado, para editarlo:

- **Apertura**: fijo, una hoja (practicable, oscilobatiente, abatible, proyectante, pivotante, corredera) o, en una puerta, una hoja de puerta.
- **Relleno**: **Vidrio** o **Panel ciego**. Las hojas y los fijos laterales de una puerta pueden ser ciegos, y también un paño de ventana (un panel bajo una hoja, un paño opaco).
- **Dividir**: **En columnas** o **En filas**, a una distancia en milímetros del borde izquierdo o inferior del paño (la mitad por defecto). Dividida en horizontal, una hoja de puerta conserva su parte inferior y la superior pasa a ser un fijo superior acristalado.
- **Unir**: con el paño de al lado, de arriba o de abajo, cuando comparten un borde entero. Los montantes y travesaños que ya no necesita ningún paño desaparecen.

![Una puerta de tienda en un solo tipo: dos hojas, fijos laterales y un fijo superior corrido](shots/joinery-panes.webp)

**Sistema de perfiles** empieza el tipo con un perfil de **PVC**, **Aluminio**, **Madera** o **Acero (esbelto)**: fija las medidas del marco, los montantes, los travesaños y las hojas y da a los perfiles sus formas finas. El marco tiene un **Galce** contra el que cierra la hoja, la hoja solapa el marco en su **Solape de la hoja**, los **junquillos** sujetan el vidrio, y una ventana tiene un goterón bajo su travesaño inferior. Cada medida sigue editable. **Simple (perfiles lisos)** mantiene piezas rectangulares sencillas, como antes.

![Una ventana de PVC: las hojas solapando el marco, los junquillos, un panel ciego bajo un paño fijo](shots/joinery-profiles-3d.webp)

En **Detalles**, **Vierteaguas interior** y **Vierteaguas exterior** activan y desactivan los vierteaguas, y **Revestimiento de jambas** activa el acabado del vano en las jambas y el dintel. Donde no se usen vierteaguas ni revestimientos, desmárquelos.

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

**Forma** da a una puerta o ventana un remate en arco (**Arco de medio punto**, **Arco rebajado**, **Arco elíptico**, **Esquinas redondeadas**, **Arco apuntado**, **Remate triangular**) o la hace **Redonda u oval** (redonda cuando el ancho y el alto son iguales). Un tipo con arco conserva sus hojas bajo un travesaño en el arranque del arco, y encima un fijo que sigue el arco, con barrotillos si se quiere: **Abanico**, **Los montantes prolongados** o **Cruz**. **Flecha del arco** fija cuánto sube un arco rebajado, elíptico, apuntado o triangular, **Radio de las esquinas** cuán redondas son las esquinas de un remate con esquinas redondeadas. El hueco del muro, el marco, el vidrio, el recercado y los dibujos siguen el contorno.

**Junquillos** dividen los vidrios: **Cuadrícula (colonial)** con sus vidrios a lo ancho y a lo alto, **Prairie** con barras cerca de los bordes, o **Rombos**.

En la fachada, además del recercado, una ventana puede tener **Clave**, **Remate sobre el hueco** (**Cornisa**, **Frontón**, **Frontón curvo** o un **Guardapolvo** que sigue un remate en arco) y un **Antepecho bajo el vierteaguas** con sus ménsulas.

**Aplicar un estilo…** pone a la vez la forma, los junquillos y los ornamentos: Moderno, Clásico, Georgiano, Victoriano, Románico, Barroco, Gótico, Tudor, Prairie, Tradicional o una ventana redonda. Todo lo que pone sigue editable.

![Una ventana barroca: arco de medio punto con abanico, clave y frontón curvo](shots/joinery-styles.webp)

![Ventanas con arco en 3D](shots/window-styles-3d.webp)

### Miradores, ventanas curvas y en caja

Un tipo de ventana puede sobresalir del muro. En el configurador, **Projects** la convierte en **Bay** (un centro y dos lados en ángulo con el muro), **Bow** (paneles iguales en un arco) o **Box** (lados en escuadra con el muro), y **New bay window** empieza un tipo nuevo como mirador a 45°. La ventana sigue llenando un solo hueco en el muro, tan ancho y alto como el tipo: se coloca, mueve, copia, voltea, numera y lista como cualquier ventana. **Architecture → Bay window** (<kbd>BW</kbd>) pone una en el muro en que haces clic: el tipo elegido en el configurador si sobresale, si no el primer mirador del proyecto, si no un mirador a 45°.

Las columnas de la cuadrícula son sus caras. En un mirador y en una caja, la primera y la última columna son sus lados y las de en medio su centro; una ventana curva tiene una cara por columna, sus anchos repartiéndose el arco. Cada paño abre como dice su **Apertura**, así que los lados pueden abrir y el centro quedar fijo. Después:

- **Projection**, desde la cara exterior del muro hasta la cara exterior de los marcos, y en un mirador el ángulo **Sides to the wall** (15° a 90°);
- **Sides**: los lados de la caja acristalados o macizos, con su material;
- **Seat inside**: el asiento en el alféizar, cuánto entra en la estancia, su material;
- **Sill outside** bajo las caras;
- **Base**: ninguna (colgado del muro), **muros hasta el suelo**, **una losa en voladizo** o **una losa sobre ménsulas**, con su espesor, cuánto pasa de las caras y su material;
- **Head**: una cubierta **a cuatro aguas**, **a un agua** o **plana**, o una **losa**, su espesor en el alero, su vuelo, su pendiente y material, y un **babero contra el muro**.

Los marcos, hojas, vidrios y manillas son los del tipo, así que sus materiales y manillas valen para todas las caras. **Bay style** lo fija todo a la vez: mirador a 45° o poco profundo a 30°, mirador apoyado en el suelo, ventana curva de cinco o cuatro paneles, caja con lados acristalados o macizos, mirador sobre ménsulas. **Save as bay style…** guarda el configurado como estilo del proyecto, junto a los de Lapin; se renombra o elimina en la lista bajo los campos.

En planta las caras se cortan, el asiento, el vierteaguas y la base se dibujan bajo el corte y la cubierta a trazos encima; alzados, secciones y 3D muestran todo el cuerpo. La tabla de ventanas tiene una ficha para el tipo, con sus caras desplegadas y una fila **Projecting** (tipo, vuelo, caras); su área es la de las caras acristaladas y sus detalles miden la cubierta, el asiento y la base. En IFC es un único IfcWindow de particionado definido por el usuario (*Bay 45°, projection 600, 3 faces, 4 panels*) cuyo cuerpo es todo el mirador, con sus paneles cara a cara; Lapin lee el mirador de vuelta.

![Una ventana curva en el configurador](shots/bay-configurator.webp)

![Mirador, ventana curva, caja y mirador sobre ménsulas en planta](shots/bay-plan.webp)

![Mirador, ventana curva, caja y mirador sobre ménsulas en 3D](shots/bay-3d.webp)

### Manillas

**Manilla** elige la manilla del tipo: **Manilla de palanca**, **Pomo**, **Tirador de barra**, **Manilla de ventana** o ninguna. Por defecto, una puerta lleva manilla de palanca y una ventana manilla de ventana. Una puerta tiene manilla en ambas caras, a la **Altura de la manilla** que se indique; **Color de la manilla** le da su color. Las manillas se ven en 3D, en secciones y alzados, y en los dibujos del configurador cuando está marcado **Herrajes**.

### Parámetros

**Parámetros · en tablas**, al pie del configurador, guarda los parámetros propios del tipo: **+ Resistencia al fuego** (guardada en Pset_DoorCommon o Pset_WindowCommon de IFC como FireRating), **+ Fabricante**, **+ Código de producto**, **+ Sitio web**, o cualquier parámetro al que dé nombre. Las puertas y ventanas del tipo los heredan, y cada uno se convierte en una fila de las tablas de carpintería: vea [Tablas y gráficos](schedules.md).

## Parasoles

**Architecture → Sunshade** (<kbd>SN</kbd>) pone un parasol en la puerta o ventana en que haces clic: del tamaño de su hueco, pasando las jambas, sobre el dintel o delante, y la sigue cuando se mueve. **Sunshade along a facade** toma dos puntos: en un muro va a lo largo de su cara exterior, en otro sitio a lo largo de la línea, hacia su derecha. La barra de ambas herramientas elige el tipo, o un estilo del que se crea un tipo nuevo.

Los tipos:

- **Canopy** (marquesina): losa, de lamas o de vidrio, con su vuelo, espesor, inclinación y un frente, sobre ménsulas, tirantes o postes;
- **Vertical fins** (lamas verticales): su profundidad, espesor, separación y ángulo, rieles arriba y abajo;
- **Louvres (brise-soleil)**: lamas horizontales, planas, aerodinámicas o en caja, su profundidad, separación y ángulo, separadas del muro, entre placas de extremo;
- **Egg-crate**: placas horizontales y verticales juntas;
- **Perforated screen**: una chapa separada del muro, con agujeros redondos o cuadrados o ranuras, su tamaño y paso;
- **Awning** (toldo): tela inclinada, faldón, cofre y brazos articulados;
- **Pergola**: cabios, una viga, listones encima y postes hasta el suelo;
- **External venetian blind**: lamas, cuánto está bajada, su cajón y guías.

**Sunshade types** (en Types) edita un tipo: su clase y cada parámetro, sus materiales, junto a su dibujo desde fuera, en sección y en planta. Cada parasol del tipo sigue un cambio. Un tipo nuevo parte de un estilo: marquesina de hormigón, de madera sobre ménsulas, de vidrio con tirantes, de lamas, tejadillo de teja, brise-soleil, lamas verticales, retícula de hormigón, pantalla perforada de acero corten, toldo de tela, pérgola de madera, veneciana exterior; **Save as style…** guarda un tipo como estilo del proyecto.

![Las propiedades de una marquesina tras el cálculo de mediodía](shots/sunshade-properties.webp)

Seleccionado, un parasol muestra su tipo, sobre qué está y su propio **Projection** (vacío: el del tipo). **Full shade at noon, 21 June** calcula, para una marquesina, un toldo o una pérgola sobre una ventana, cuánto debe volar para que toda la ventana quede en sombra cuando el sol está más alto en el día más largo, en el lugar del proyecto (**Manage → Location**; Bucarest hasta que se fije uno), y lo fija. En una fachada sur a 45° N el sol está entonces a 68,4°: una ventana de 1,5 m bajo una marquesina 150 mm sobre el dintel necesita 650 mm. Sus pinzamientos arrastran sus extremos a lo largo del muro o línea y su vuelo.

![Los tipos de parasol: lamas, desde fuera, en sección y en planta](shots/sunshade-types.webp)

Las plantas dibujan a trazos un parasol por encima del corte, cortado uno que lo atraviesa (lamas verticales, una pantalla) y visto uno por debajo; alzados, secciones y 3D muestran su cuerpo, con el sombreado de su material, y proyecta su sombra con el resto (**View → Sun and shadows**). **Sunshade schedule** los lista por tipo con dónde están, su longitud y vuelo, cuántos y el área que sombrean; sus cantidades (longitud, vuelo, área, volumen) toman las partidas de coste de su tipo. En IFC son IfcShadingDevice de un IfcShadingDeviceType: un toldo AWNING, una veneciana JALOUSIE, las lamas LOUVER en IFC 4.3 (definidas por el usuario en IFC4), los demás definidos por el usuario (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin los lee de vuelta tal como eran.

![Parasoles en planta](shots/sunshades-plan.webp)

![Una marquesina, lamas, lamas verticales, un toldo, una pérgola y una veneciana en 3D](shots/sunshades-3d.webp)

## Etiquetas en planta

**Anotar → Etiquetas de puertas y ventanas** pone una marca en cada puerta y ventana de una planta: una línea a través del muro, la marca fuera, y el tamaño y el alféizar dentro. Para cambiar el aspecto de las etiquetas, vaya a **Gestionar → Normas de dibujo → Etiquetas**.

### Editar un marcador en pantalla

Cada marcador puede colocarse a mano, parte por parte. Haz clic en su línea o en uno de sus textos para seleccionarlo: cada texto tiene entonces un pinzamiento cuadrado (arrástralo para mover el texto) y uno redondo pasado su final (arrástralo para girar el texto), y cada extremo de la línea tiene un pinzamiento (arrástralo para alargar o acortar la línea; el texto de ese extremo va con él). Ctrl/⌘-clic en un pinzamiento devuelve esa parte a como estaba. Cada arrastre es un paso de deshacer. Con varios marcadores de la misma planta seleccionados, una parte arrastrada en uno cambia la misma parte en todos.

![El marcador de una puerta seleccionado: un pinzamiento por texto, su pinzamiento de giro y uno en cada extremo de la línea](shots/marker-grips.webp)

![La marca movida y girada, el extremo exterior de la línea alargado](shots/marker-edited.webp)

Los cambios pertenecen al marcador, y por tanto a esa planta: la misma puerta puede colocarse de otra forma en otra planta. Se guardan a lo largo y a través del muro, así que siguen a la puerta cuando se mueve, gira o cambia de lado. En las propiedades del marcador, **Marcador** lista sus partes: escribe el giro de un texto en grados o cuánto llega un extremo en milímetros de papel, devuelve una parte con ↺, o todas con **Devolver la etiqueta a su sitio**. El motor dibuja los marcadores tal como se editan, así que los planos, el PDF y el DXF los imprimen igual.

![Las partes de un marcador en sus propiedades](shots/marker-properties.webp)

Si los textos van a través de la línea del marcador (a lo largo del muro, por defecto) o a lo largo de ella se elige para cada tipo de etiqueta, en **Gestionar → Normas de dibujo → Etiquetas**, columna **Dirección del texto**: **perpendicular a la línea de la etiqueta** o **a lo largo de la línea de la etiqueta**. En ambos casos se dibujan legibles, nunca boca abajo; un texto girado a mano también se mantiene legible.

![Los textos del marcador de la puerta a lo largo de la línea de la etiqueta](shots/marker-along.webp)
