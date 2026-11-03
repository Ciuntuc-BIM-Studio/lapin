---
title: Acero
order: 56
summary: Barras de acero con extremos configurables —prolongados o retranqueados, cortados por un plano, a inglete, ajustados a otra barra, rebajados—, hechas también desde un contorno dibujado; perfiles huecos y conformados en frío; naves de pórticos con cartelas.
---

## La pestaña Acero

![La pestaña Acero](shots/ribbon-steel.webp)

- **Nave de pórticos…**: una nave de acero completa a partir de unas pocas cifras, en el configurador de naves (más abajo).
- **Barra** (<kbd>SM</kbd>): una barra de acero entre dos puntos, en la cota superior de la planta, con la cara superior de la sección sobre el eje, como una viga. Su tipo se elige en Propiedades antes del primer clic, o desde la pestaña **Catálogo**, donde cada tipo de acero se dibuja por su sección.
- **Convertir en barra**: los contornos dibujados seleccionados se convierten en barras (más abajo).
- **Inglete** y **Ajustar**: dos barras unidas por sus extremos (más abajo).
- **Tipos de acero**: los tipos de barra del proyecto, y otros nuevos.

Una barra es un IfcMember: su eje va a cualquier parte del espacio (una diagonal, un par, un montante), su sección viene de su tipo, se puede girar alrededor del eje (**Rotación**) y el eje puede pasar por el centro, la cara superior o la inferior de la sección (**Punto de inserción**). Las vigas y los pilares de la estructura de perfil de acero admiten los mismos extremos.

## Extremos

Selecciona una barra, o una viga o un pilar recto de perfil de acero: su sección **Acero** en Propiedades muestra sus dos extremos, **Inicio** y **Fin**. Cada extremo es un conjunto de reglas; el cuerpo las sigue.

![Los extremos de una barra en Propiedades](shots/steel-ends.webp)

- **Prolongación**: cuánto pasa el extremo de su punto; negativa, el extremo queda retranqueado (una holgura de montaje).
- **Corte**: **Perpendicular al eje**; **Por un plano**, girado desde el corte recto **En planta** (alrededor de la vertical de la sección) y **En alzado** (alrededor de su lado), en grados; **Inglete** con otra barra, en el plano que divide en dos la esquina entre ellas; **Ajustar a una barra**, en la cara de la otra barra a la que llega (el ala de un pilar, o el lado de su alma cuando el pilar está girado).
- **Rebaje**: las alas rebajadas **Superior** y/o **Inferior**, con su **Longitud del rebaje** desde el extremo y su **Profundidad del rebaje** desde el borde de la sección, como en una viga que acomete a una jácena.

El inglete y el ajuste nombran la otra barra: muévela y el extremo la sigue; bórrala y el extremo vuelve a ser recto, y lo dice. **Inglete** en la pestaña une a inglete las dos barras seleccionadas en la esquina donde se encuentran; **Ajustar** ajusta la primera seleccionada a la segunda.

## De un boceto a una barra

Dibuja el contorno de la barra como una polilínea cerrada o un rectángulo (**Dibujo**) y luego **Acero → Convertir en barra** con uno o varios contornos seleccionados. En un solo paso cada contorno se lee y se sustituye por una barra:

![Dos contornos bocetados en la planta](shots/steel-sketch.webp)

- sus **dos lados largos paralelos** (con medio grado de tolerancia) dan el eje, su línea media de extremo a extremo, y el **ancho** entre ellos;
- el **lado de cada extremo** da el corte de ese extremo: recto, o por un plano con la inclinación del lado.

En una **planta** el contorno es la barra vista desde arriba: su ancho es el de las alas, y se busca un perfil de ese ancho entre los tipos de barra del proyecto y el catálogo (si no, el tipo elegido para Barra); la barra queda en la cota superior de la planta. En una **sección** o un **alzado** el contorno es la barra vista de lado: su ancho es el canto de la sección, un extremo inclinado es un corte en alzado y un **escalón** en ángulo recto en un extremo es un **rebaje** arriba o abajo; la barra queda donde se dibujó. Un escalón dibujado en planta se rechaza (un rebaje se ve de lado). Un contorno que no es una banda (un triángulo, una L, lados no paralelos) se deja como está, y Lapin lo dice.

## Tipos de acero

**Acero → Tipos de acero** lista los tipos de barra del proyecto con su peso por metro y crea otros: un perfil del catálogo (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** y los genéricos conformados en frío **C**, **Z** y **Σ**) o una sección hueca o conformada en frío **con sus propias dimensiones** (canto, ancho, espesor, labio). El tipo nuevo puede ser un tipo de barra, o un **tipo de viga** o **tipo de pilar** de la estructura, para que las herramientas Viga y Pilar también coloquen acero. Un clic en un tipo de la lista lo convierte en el que coloca Barra.

![El diálogo Tipos de acero](shots/steel-types.webp)

Los perfiles huecos están vacíos por dentro, en la vista 3D, en las secciones y en las cantidades. Los perfiles conformados en frío son formas genéricas con dimensiones, de pliegues vivos, sin catálogo de fabricante.

![Barras en 3D](shots/steel-3d.webp)

## Naves de pórticos

**Acero → Nave de pórticos…** abre el configurador de naves. Como el de cubiertas, tiene los estilos a la izquierda, los campos de la nave en pestañas en el centro y, a la derecha, la nave tal como Lapin la construiría: un pórtico tipo visto a lo largo de la nave, con sus cartelas, y un resumen de lo que pesa.

![El configurador de naves](shots/steel-hall.webp)

- **Estilos**: las naves típicas del catálogo (**Nave 20 m**, **Nave 30 m**, un **Cobertizo 10 m** a un agua) y las del proyecto. Un estilo elegido rellena todos los campos; luego puede cambiarse cualquiera. **Guardar como estilo…** guarda la nave tal como está configurada como estilo del proyecto; **Actualizar el estilo** la guarda de nuevo en su estilo, y todas las naves de ese estilo lo siguen.
- **Pórtico**: la **Luz** (de eje de pilar a eje de pilar), las **Crujías** (iguales como `5 × 6`, o cada una: `6, 6, 7.5`), la **Altura de alero** (la cara superior de los dinteles sobre la cara exterior del pilar), la **Cubierta** (a dos aguas o a un agua) y su **Pendiente**, los perfiles de **Pilares** y **Dinteles**.
- **Cartelas**: la **Cartela en el alero** y la **Cartela en la cumbrera**, cada una con su **Longitud** a lo largo del dintel desde la cara del pilar (o la cumbrera) y su **Canto** bajo el dintel allí, y cómo está **Hecha**: **cortada del perfil del dintel** (un I cortado en diagonal por el alma, una mitad soldada bajo el dintel: el alma y el ala del propio dintel) o **de chapas soldadas** (una **Chapa del alma** y una **Chapa del ala** de su propio espesor, y el ancho del ala).

![Las cartelas de chapas soldadas](shots/steel-hall-haunches.webp)

- **Hastiales**: **pilares de hastial bajo un dintel simple** (pilares de esquina y pilares a la **Separación** elegida, con su eje fuerte perpendicular al muro de hastial, bajo un dintel más ligero que llega a la cara exterior de los muros, los pilares ajustados a su cara inferior), o **un pórtico normal** en cada extremo.

![Un hastial con pilares](shots/steel-hall-gable.webp)

- **Acero secundario**: las correas, las correas laterales, el arriostramiento, la viga de alero y los tornapuntas (abajo).
- **Colocación**: donde se cruzan el eje A y el eje 1, y la dirección de la luz en planta (**Rotación**); la vista previa muestra la retícula de la nave desde arriba.

**Crear nave** la hace en un solo paso, que un deshacer revierte: su propia **retícula** (ejes 1, 2… en los pórticos, A y B en los pilares), y en cada pórtico dos **pilares** (pilares de la estructura de un perfil de acero, con el alma en el plano del pórtico y la cabeza cortada según la pendiente) y dos **dinteles** a inglete en la cumbrera (uno solo, del pilar bajo al alto, en una cubierta a un agua): barras de acero ajustadas a las caras de los pilares, con sus cartelas. La nave está en la planta actual; el alero y la cumbrera son alturas de sus pórticos, no plantas nuevas. El resumen da los pórticos, las alturas y el acero por tipo: pilares, dinteles, cartelas, pilares de hastial, en total y por m² de planta.

![La nave en 3D](shots/steel-hall-3d.webp)

La nave sigue siendo editable. Selecciona una de sus barras: su sección **Acero** muestra su nave y **Editar nave…**, que abre el configurador sobre ella; **Aplicar** vuelve a construir la nave sobre las mismas barras, que conservan su identidad (una barra que ya no hace falta se va, una nueva se añade). Allí se listan también las cartelas del dintel, cada una cambiada por separado (tipo, longitud, canto, chapas): una cartela ajustada así se conserva cuando la nave se reconstruye; **×** la quita.

![Las cartelas de un dintel en Propiedades](shots/steel-hall-props.webp)

## Uniones

Los pórticos de una nave vienen con sus **uniones** (Steel → Portal frame hall…, pestaña **Nodos**): una **base de pilar** bajo cada pilar y pilar hastial, una **unión de alero** donde cada dintel llega a su pilar, una **unión de cumbrera** donde se encuentran dos dinteles y una cartela bajo el dintel sobre cada pilar hastial. Cada una es un elemento propio que sabe qué barras une; sus placas, tornillos y anclajes se hacen de sus parámetros y de las barras tal como están, así que las siguen cuando la nave cambia.

![La pestaña Nodos](shots/steel-nodes-hall.webp)

- **Uniones**: activadas o no para toda la nave.
- **Bases de pilares**: **articuladas** (una placa base, dos anclajes a cada lado del alma o cuatro entre las alas) o **empotradas** (una placa más larga y gruesa, de cuatro a ocho anclajes fuera de las alas, nervios en el plano del alma). Los **Anclajes** son **rectos**, **con gancho** o **con placa de anclaje** en el pie; el **Número de anclajes** y la **Longitud de anclaje** (bajo la cara del hormigón) los elige el perfil cuando quedan vacíos. Si bajo el pilar hay una zapata de la estructura, los anclajes entran en ella (y se detienen 50 mm sobre su fondo); si no, bajan su longitud bajo el mortero.
- **Unión de alero**: una placa de testa extendida en el dintel atornillada al ala del pilar: su **Espesor de la placa**, el **Diámetro de los tornillos** y su **Calidad**, cuánto sobresale **Sobre el dintel** (con una fila de tornillos allí) y los **Rigidizadores del pilar** a la altura del ala superior del dintel y del ala de su cartela.
- **Unión de cumbrera**: las placas de testa de los dos dinteles atornilladas entre sí sobre el inglete, con sus cartelas.

Lo que se deja vacío lo eligen los perfiles: tornillos de M16 a M27 según el canto de la barra atornillada, una placa tan gruesa como sus tornillos, distancias al borde y entre filas según los agujeros. Una unión retranquea los extremos de sus barras con lo que pone entre ellas (el dintel con su placa de testa, el pilar con su placa base, el pilar hastial con una holgura de 20 mm), mediante las reglas de extremo de las propias barras.

![Una unión de alero](shots/steel-nodes-eaves.webp)

![Una base empotrada sobre anclajes con gancho](shots/steel-nodes-base.webp)

Selecciona una unión para verla en Propiedades: las barras que une, cada parámetro tal como está (un tamaño elegido por el perfil se muestra así) y sus piezas: placas, tornillos y anclajes por tamaño y calidad, las soldaduras por garganta con su longitud, su peso y la zapata en la que entran los anclajes. Un parámetro cambiado allí se fija solo en esa unión, y la nave reconstruida lo conserva. **Editar nave…** abre su nave.

![Una unión en Propiedades](shots/steel-nodes-props.webp)

Las barras dibujadas a mano reciben sus uniones con **Steel → Buscar uniones**: una base bajo cada barra vertical, una unión de alero donde una barra se ajusta a un pilar, la de un pilar hastial donde un pilar se ajusta bajo una barra, una de cumbrera donde dos barras están ingleteadas entre sí, un empalme donde dos van en línea, extremo con extremo; para las barras seleccionadas, o todo el modelo si no hay nada seleccionado. **Steel → Piezas de acero** crea la lista de placas, tornillos, anclajes y soldaduras de las uniones. Eliminar una unión devuelve a sus barras sus extremos; eliminar una barra se lleva sus uniones; **Eliminar nave** en el configurador quita la nave con sus barras, uniones y ejes, en un solo paso.

## Acero secundario

Una nave viene con su **acero secundario** (Steel → Portal frame hall…, pestaña **Acero secundario**), hecho con ella y de nuevo cuando se cambia, cada barra conservando su identidad:

![La pestaña Acero secundario](shots/steel-secondary-hall.webp)

- **Acero secundario**: activado o no para toda la nave.
- **Correas**: su **Perfil** (Z, C y Σ conformados en frío, un U o un I ligero), la **Separación de correas** a lo largo de la pendiente, cuánto van **Más allá de los hastiales** y sus **Longitudes**: **una por crujía** (de pórtico a pórtico) o **continua** (todo el largo de la nave). Apoyan sobre la cara superior de los cabios, con el alma perpendicular a la cubierta, desde 150 mm dentro de la cara de los pilares hasta 200 mm de la cumbrera, en tantas líneas como pide la separación.
- **Correas laterales**: su **Perfil** y la **Separación de correas laterales**, desde la **Primera correa a** hasta bajo el alero, una por crujía en la cara exterior de los pilares, con el alma horizontal y las pestañas hacia abajo; en los hastiales, de pilar a pilar (o entre los pilares de un pórtico de hastial), tan altas como lo permite el cabio de encima. Una **Viga de alero** ocupa lo alto de las fachadas largas (con su **Perfil**), las correas una separación por debajo.
- **Arriostramiento**: las **Crujías arriostradas** (sus números, desde la primera), el **Perfil** de las barras (redondos de Ø16 a Ø30, angulares o tubos) y dónde: **En la cubierta** (una X en el plano de la cubierta, bajo el ala superior de los cabios, en paneles de más o menos la crujía) y **En las fachadas largas** (una X en el plano de las fachadas, por los ejes de los pilares, desde la altura de la primera correa hasta bajo la cartela).
- **Tornapuntas**: desde una de cada dos correas y correas laterales hasta el ala inferior del cabio y el ala interior del pilar, a ambos lados, fuera de las cartelas.

![El acero secundario en 3D](shots/steel-secondary-3d.webp)

Cada correa apoya sobre un **ejión** en cada cabio, pilar o pilar de hastial que cruza: una placa soldada a la cara de la barra, junto al alma de la correa (fuera del alma de una C, del lado de una Z opuesto a su ala inferior), atornillada por el alma, dos tornillos por extremo de correa (M12 hasta 250 mm de canto, M16 por encima). Un pilar de esquina más estrecho que los pilares lleva un ejión más alto, hasta la correa. Cada X apoya sobre **placas de nudo** soldadas al alma de los cabios o de los pilares, una diagonal en cada cara; un angular o un tubo se fija con dos tornillos, el extremo roscado de un redondo lleva su tuerca. Un tornapuntas va atornillado en sus dos extremos. Son uniones como las de los pórticos: selecciona una para ver su placa y sus tornillos en Propiedades y cambiarlos allí. El resumen del configurador cuenta y pesa las correas, las correas laterales, la viga de alero, el arriostramiento y los tornapuntas; la tabla **Steel parts** tiene sus ejiones, placas de nudo y tornillos.

![Una correa sobre su ejión, un tornapuntas debajo](shots/steel-secondary-cleat.webp)

![El arriostramiento de cubierta sobre sus placas de nudo](shots/steel-secondary-bracing.webp)

## Listas y dibujos

**Steel → Steel members** hace la lista del acero del proyecto: las barras, y las vigas y pilares de un perfil de acero, por nave (las que no son de ninguna al final), una **Marca** para las barras de un tipo iguales en perfil, longitud y cortes de extremo (C pilares, R cabios, GP pilares de hastial, P correas, SR correas laterales, EB vigas de alero, B arriostramiento, FB tornapuntas, M otras barras, BM vigas), numeradas desde el perfil más pesado y la más larga. Una fila da el **Perfil**, la **Longitud**, el **Corte inicial** y el **Corte final** (cuánto gira el plano del extremo respecto del corte recto, y un rebaje: los ángulos de la lista de corte), el **Número**, el peso por metro, **Cada una** y en total, y la superficie de **Pintura**; un total bajo cada nave y uno general. **Steel parts** lista las placas, tornillos, anclajes y soldaduras de las uniones, con sus ejiones y placas de nudo.

![La lista de barras de acero](shots/steel-members.webp)

**Steel → Anchor plan** dibuja las bases de los pilares de la nave para la cimentación: una planta de la planta de la nave a 1:50, cortada justo sobre las placas base, con cada anclaje en un círculo, su distancia al eje (± cuando los anclajes están simétricos respecto de él), cada base marcada por su tipo (**B1**, **B2**…: la misma placa, anclajes, calidad y mortero), las crujías y la luz de la retícula, y la tabla de los tipos junto a la nave: su número, tipo, placa, anclajes (número, tamaño, forma), calidad y mortero. **Steel → Frame elevation** dibuja un pórtico (el típico, o el de la barra seleccionada) visto a lo largo de la nave a 1:50, con su luz y sus alturas de alero y cumbrera. Ambos son vistas de la nave (pedidas de nuevo, se abre la misma) y van en planos como cualquier otra; siguen a la nave cuando cambia.

![Un plano de anclajes](shots/steel-anchor-plan.webp)

![El alzado de un pórtico](shots/steel-frame-view.webp)

## IFC y cantidades

Una barra se exporta como **IfcMember** con su **IfcMemberType**. Su cuerpo se escribe como escriben el acero los demás programas (Tekla, Advance Steel, Revit): su sección **extruida** (IfcExtrudedAreaSolid), **recortada** por los planos de sus extremos (IfcBooleanClippingResult con IfcHalfSpaceSolid), y cada rebaje un **IfcOpeningElement** que la vacía. Una viga o un pilar de acero con extremos se escribe igual. Las cantidades de cada barra dan su longitud entre cortes, el área de su sección, su volumen y su peso neto (7850 kg/m³).

Una nave se exporta como **IfcBuildingSystem** (LOADBEARING) al que se asignan sus barras, con sus datos en el conjunto de propiedades `Lapin_SteelHall` y su propia **IfcGrid**. Un dintel con cartelas es un **IfcElementAssembly** del dintel y sus cartelas: una cartela cortada del perfil es un **IfcMember**, una de chapas soldadas dos **IfcPlate**, cada una extruida (IfcExtrudedAreaSolid). Las cartelas no entran en las cantidades del dintel; el resumen del configurador las pesa. Las correas se exportan como **IfcMember** PURLIN, el arriostramiento y los tornapuntas como BRACE, las correas laterales y la viga de alero como USERDEFINED (**Side rail**, **Eaves beam**), todos asignados al sistema de la nave, con sus uniones.

Una unión se exporta como **IfcElementAssembly** de sus placas (**IfcPlate**), tornillos (**IfcMechanicalFastener** BOLT), anclajes (ANCHORBOLT) y placas de anclaje (**IfcDiscreteAccessory** ANCHORPLATE), cada uno con su tamaño y calidad en `Lapin_SteelPart`; realiza la conexión de las barras que une (**IfcRelConnectsWithRealizingElements**; una base, entre la zapata y su pilar). Las soldaduras quedan como datos, no como cuerpos.
