---
title: Escaleras y rampas
order: 42
summary: Coloque una escalera o una rampa entre plantas o hasta cualquier altura, configúrela en su tipo y haga los pocos escalones hasta un porche o una puerta.
---

## Escaleras

Elija **Arquitectura → Escalera** (<kbd>ST</kbd>). Haga clic en el pie de la escalera (el centro de su primera contrahuella) y luego en un punto en la dirección en que sube. Para una escalera en L o en U, un tercer clic a la izquierda o a la derecha del tramo indica hacia dónde gira. La escalera sube desde su planta hasta la planta superior. Lapin calcula el número de contrahuellas, la contrahuella y la huella a partir de la altura y de las reglas del tipo.

Seleccione la escalera para modificarla en el panel de propiedades:

- **Tipo de escalera.** La forma, las reglas, la construcción y la barandilla vienen del tipo, que se configura en **Arquitectura → Tipos de escalera**.
- **Ancho**, **Giro**, **Desfase de base** y **Desfase superior.**
- **Desnivel.** Déjelo vacío y la escalera subirá hasta la planta superior. Un valor da a la escalera un desnivel propio, sean cuales sean las plantas. Úselo para unos pocos escalones entre dos niveles de la misma planta.
- **Fijo.** Contrahuellas, contrahuella, huella y longitud del rellano, cuando quiera fijarlos. Déjelos vacíos para que decidan las reglas.
- **Comprobaciones.** Cada valor frente a las reglas del tipo, en verde o en rojo.

Una escalera pide a la losa superior un hueco sobre sí misma. Desmarque **Hueco en la losa superior** si la losa debe quedar entera.

## Formas de escalera y tipos ya hechos

En **Arquitectura → Tipos de escalera**, la pestaña **Forma** ofrece: **Recta**, **Recta con descansillo**, **En L** y **En U** (cada una con descansillo o con **peldaños compensados**; una U sin ojo es una escalera de ida y vuelta), **De caracol** (alrededor de un mástil, con su **Radio del mástil**), **Curva** (con su **Radio interior**) e **Imperial (bifurcada)**: un tramo central hasta un descansillo y luego dos tramos de vuelta, uno a cada lado. En una escalera de caracol o curva, **Gira (°)** fija cuánto gira; vacío, deciden las tabicas y la huella, e indicado, la huella sale de él. Cada escalera también puede cambiarlo en sus propiedades.

La biblioteca añade al proyecto tipos ya hechos: escalera de ida y vuelta, escalera compensada, de caracol, curva, volada e imperial.

En la pestaña **Construcción**, **Tipo de construcción** elige losa bajo los peldaños, peldaños macizos sobre el terreno, **Peldaños volados (flotantes)** (cada peldaño una placa empotrada en el muro) o **Peldaños sobre zancas**.

La **Barandilla** tiene su **Diseño**: **Montantes y pasamanos**, **Balaustres** (como mucho a la **Separación de balaustres**), **Paneles de vidrio**, **Cables** o un **Antepecho macizo**; un **Pasamanos** cuadrado o redondo, su altura, el **Retranqueo del borde**, cuánto **Se prolonga más allá de los extremos** y su color. En el panel de propiedades, **Barandilla** la pone en otros lados que el tipo, para una sola escalera.

![El tipo de la escalera de caracol, con los peldaños numerados](shots/stair-spiral.webp)

### Peldaños numerados

En **En planta**, en la pestaña **Forma**: **Numerar los peldaños** escribe el número de cada peldaño en un círculo, desde la primera tabica; **Rótulo** escribe las tabicas, la contrahuella y la huella, y el ancho junto al arranque; **«UP» al pie de la flecha** marca dónde empieza. Las plantas y los planos los muestran.

![Peldaños numerados en planta, una escalera imperial y una volada](shots/stair-numbers-plan.webp)

![Una escalera de caracol, una imperial y una volada en 3D](shots/stair-shapes-3d.webp)

## Escalones de entrada (un porche)

Los escalones que suben a una puerta de entrada tienen un desnivel propio: desde el terreno, unos escalones por debajo de la planta baja, hasta ±0.00. Lapin tiene un tipo de escalera listo para ellos, **Escalones de entrada**: 2 m de ancho, tres contrahuellas de 150 mm, de hormigón macizo vertido sobre el terreno, sin barandilla.

![Escalones de entrada delante de un porche, en planta](shots/entrance-steps-plan.webp)

1. **El porche.** Dibuje el rellano como una losa: elija **Arquitectura → Losa** (<kbd>SB</kbd>) y haga clic en su contorno fuera del muro, delante de la puerta. La cara superior de una losa está al nivel de su planta (±0.00 en la planta baja) y su tipo le da el espesor. Para cambiar el espesor, cree un tipo de losa para porches en **Arquitectura → Tipos de losa**, por ejemplo 150 mm de hormigón.
2. **Los escalones.** Elija **Arquitectura → Escalones de entrada**. Haga clic en el centro del borde del porche y luego en el lado hacia el que salen los escalones. Los escalones suben hasta el borde del porche y terminan en ±0.00. Empiezan 450 mm más abajo: su **Desfase de base** es −450 mm.
3. **La altura.** Seleccione los escalones. En **Desnivel**, escriba la diferencia real entre el terreno y el porche, por ejemplo 300. La parte superior se queda en el porche y el pie sube: el desfase de base pasa a −300 mm y los escalones tienen dos contrahuellas. **Ancho** los hace tan anchos como el porche, por ejemplo 2400.

![Las propiedades de los escalones de entrada: desnivel, desfase de base, niveles](shots/entrance-steps-properties.webp)

**Niveles** muestra dónde empiezan y terminan los escalones, por ejemplo −0.45 → ±0.00. Con una sola contrahuella (un desnivel de 150 mm), el escalón es el propio borde del porche y no hay huella que construir. Dé a los escalones dos contrahuellas o más para tener huellas delante del porche.

El tipo se añade al proyecto la primera vez que lo usa. Para modificarlo, abra **Arquitectura → Tipos de escalera** y elija **Escalones de entrada**:

- **Forma → Sube → Una altura propia.** El desnivel del tipo, subido **Hasta el nivel de su planta, desde abajo (una entrada)**; cada escalera puede cambiarlo igualmente con **Desnivel**.
- **Reglas → Conjunto de reglas → escalones de entrada.** Contrahuellas de 120–170 mm y huellas de 300–400 mm, con una sola contrahuella permitida.
- **Construcción → Tipo de construcción.** **Maciza, hormigonada sobre el terreno** rellena los escalones hasta su base. **Losa inclinada bajo los peldaños** los construye como una escalera interior.

![El tipo Escalones de entrada en el configurador de escaleras](shots/stair-types.webp)

Para que cualquier otro tipo de escalera suba una altura propia, elija **Sube → Hasta → Una altura propia** en su ficha **Forma** (vea la sección siguiente). Para convertir una escalera colocada en escalones de entrada, elija **Escalones de entrada** como su **Tipo de escalera**: baja de modo que su parte superior quede al nivel de su planta.

![La casa con su porche y los escalones de entrada en 3D](shots/entrance-steps-3d.webp)

En IFC, los escalones son un IfcStair de tipo STRAIGHT_RUN_STAIR. Su tramo es un IfcStairFlight con sus NumberOfRisers, NumberOfTreads, RiserHeight y TreadLength, y Pset_StairCommon lleva los mismos valores. El porche es un IfcSlab.

## Una escalera hasta cualquier altura

Una escalera no tiene por qué ir de una planta a la siguiente. En la ficha **Forma** del tipo, **Sube** elige **La planta de arriba** o **Una altura propia**, con su **Desnivel**, y **Empieza** indica desde dónde empieza:

- **Hasta el nivel de su planta, desde abajo (una entrada).** Termina al nivel de la planta y empieza el desnivel más abajo, como los escalones de entrada.
- **Desde el nivel de su planta hacia arriba (una terraza, una plataforma).** Empieza al nivel de la planta y sube el desnivel: por ejemplo 1,20 m hasta una terraza o un muelle de carga.

Cada escalera colocada puede subir otra altura: escríbala en **Desnivel** en sus propiedades (vacío: la del tipo). Si sube desde el nivel, un desnivel nuevo mantiene el pie donde está; si viene desde abajo, mantiene la parte superior.

## El espacio bajo la escalera

El espacio bajo un tramo se puede amueblar: un armario de almacenaje, una oficina en armario, un rincón de lectura, un recibidor con banco y percheros, una casita de juegos para los pequeños o una cama para el perro. Selecciona una escalera de tramos rectos y pulsa **Espacio bajo la escalera…** al final de sus propiedades (o **Arquitectura → Espacio bajo la escalera**).

![El configurador del espacio bajo la escalera: el rincón de lectura bajo el tramo](shots/under-stair-dialog.webp)

A la izquierda están los estilos ya hechos: **Armario de almacenaje**, **Oficina en armario**, **Rincón de lectura**, **Recibidor**, **Casita de juegos infantil** y **Caseta para perro**, y encima los estilos del proyecto. Elige uno y cámbialo en el centro:

- **Ubicación.** **Bajo el tramo** (en una escalera en L o U), el **Lado abierto** (por defecto el opuesto al giro), dónde **Empieza en** y **Termina en** a lo largo del tramo (vacío: desde donde el intradós está a 300 mm del suelo, hasta la última contrahuella) y su **Profundidad** (vacía: tan ancha como la escalera).
- **El frente.** La **Parte baja** cerrada con **Puertas de armario** (cada una del **Ancho de la puerta**, con **Estantes detrás de las puertas**), un **Panel cerrado** o dejada abierta; una **Librería en el extremo alto**.
- **Elemento principal.** Un **Escritorio con un estante** (y una luz), un **Banco con cojines** (cajones en el frente), un **Banco de recibidor y percheros** (casilleros para zapatos debajo), una **Puerta de casita de juegos** (y una **Ventana junto a la puerta**) o una **Cama para perro** tras un hueco enmarcado, cada uno con sus medidas.
- **Materiales.** El **Espesor del tablero** y los materiales del **Cuerpo**, los **Frentes** y la **Tela**, de los del proyecto.

A la derecha, la vista previa muestra el espacio visto desde su lado abierto bajo el intradós del tramo y desde arriba, su longitud, profundidad y alturas, y sus piezas. Cada pieza se corta con la pendiente; la que no cabe (un escritorio donde la escalera es demasiado baja) se omite y la vista previa lo dice. **Crear** o **Aplicar** es un solo paso de deshacer. **Guardar como estilo…** guarda lo que llena el espacio como estilo del proyecto; **Actualizar el estilo** lo guarda de nuevo y todos los espacios del estilo lo siguen.

![El espacio bajo la escalera en el panel de propiedades](shots/under-stair-properties.webp)

El espacio es un elemento propio, alojado por su escalera. Cuando la escalera cambia (ancho, giro, peldaños, tipo), el espacio se rehace bajo ella, y se elimina con ella. Sus campos de ubicación están en el panel de propiedades y **Configurar…** vuelve a abrir el configurador.

![Los seis estilos, bajo seis escaleras](shots/under-stair-3d.webp)

Las plantas, secciones, alzados y planos lo dibujan como cualquier otro elemento: cortado donde corta la planta, visto por debajo.

![El rincón de lectura en planta, los demás al lado](shots/under-stair-plan.webp)

![Una sección a lo largo del tramo por el rincón de lectura](shots/under-stair-section.webp)

**Arquitectura → Listado de espacios bajo escalera** lista las piezas de cada espacio, con su número, material y volumen, y los totales por material. En IFC, el espacio es un IfcElementAssembly ("Under-stair space") de IfcFurniture (estantes, escritorio, banco, cojines, cama), IfcDoor, IfcPlate, IfcDiscreteAccessory (tiradores, percheros) e IfcLightFixture, conectado a su IfcStair.

## Rampas

Elija **Arquitectura → Rampa** (<kbd>RU</kbd>). Haga clic en el centro del extremo inferior de la rampa y luego en un punto en la dirección en que sube; para una en L o en zigzag, un tercer clic a la izquierda o a la derecha del tramo indica hacia dónde gira. La primera vez, el proyecto recibe de la biblioteca **Ramp 6 %**: 1,20 m de ancho al 6 %, en tramos de 9 m como máximo con descansillos de 1,50 m entre ellos, una losa de hormigón armado bajo un acabado y pasamanos a ambos lados. Sube hasta la planta de arriba salvo que su tipo o su **Desnivel** digan otra cosa.

**Arquitectura → Rampa de entrada** se coloca como los escalones de entrada: haga clic en el centro del borde del porche y luego en el lado hacia el que sale la rampa. Sube 450 mm desde el terreno hasta ±0,00, maciza sobre el terreno, y **Desnivel** la cambia como a los escalones.

Los tipos de rampa se hacen en el mismo configurador, **Arquitectura → Tipos de escalera**, con **Nuevo tipo de rampa**, o desde la biblioteca: **Switchback ramp** (en zigzag) y **Entrance ramp** (de entrada):

- **Forma.** **Recta**, **Recta, con descansillos**, **En L** o **En zigzag (ida y vuelta)** con su **Separación entre tramos**; el **Ancho**, la **Pendiente** en porcentaje, el **Tramo más largo** (más larga, la rampa se divide en tramos iguales con descansillos entre ellos) y la **Longitud del descansillo**. La longitud en planta es la altura dividida por la pendiente: 3 m al 6 % son 50 m. **Sube** funciona como en las escaleras.
- **Reglas.** **Pendiente máxima** (8 % por defecto), **Ancho mínimo** y **Altura libre de paso**. Las comprobaciones muestran en rojo una pendiente mayor de la que permiten las reglas, un tramo más largo que el más largo y un descansillo más corto que 1,50 m o que el ancho de la rampa.
- **Construcción.** **Una losa inclinada de hormigón armado** con su **Espesor de la losa**, o **Maciza, hormigonada sobre el terreno**; el acabado y la barandilla como en las escaleras.

![La rampa en zigzag en el configurador](shots/ramp-types.webp)

Seleccione una rampa para ver su tipo, **Ancho**, **Giro**, **Desfase de base** y **Desnivel**, una **Pendiente** y una **Longitud del descansillo** propias (vacías: las del tipo), las longitudes de sus tramos y sus comprobaciones. Las plantas dibujan sus tramos y descansillos, la flecha de subida y la pendiente en porcentaje a lo largo de cada tramo. Una rampa que sube a la planta de arriba pide un hueco en su forjado, como una escalera.

![La rampa en zigzag y la rampa de entrada en planta, con la pendiente en cada tramo](shots/ramps-plan.webp)

![Las rampas en 3D](shots/ramps-3d.webp)

En IFC una rampa es un IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP o HALF_TURN_RAMP) tipificado por un IfcRampType, formado por IfcRampFlight, descansillos (IfcSlab LANDING) e IfcRailing.

## Barandillas

**Architecture → Railing** (<kbd>RL</kbd>) dibuja una barandilla propia a lo largo del borde de un balcón, una terraza o una galería: haz clic en sus puntos, Enter termina. Toma los diseños de las barandillas de escalera (montantes y pasamanos, balaustres, vidrio, cables o un panel macizo), su altura (1 m por defecto), el desfase de la base, el perfil y los tamaños del pasamanos y la separación de montantes en sus propiedades; sus puntos se arrastran por sus tiradores (Ctrl/⌘-clic en el centro de un tramo añade uno). En IFC es un IfcRailing (GUARDRAIL).

## Balcones

**Architecture → Balcony** (<kbd>BY</kbd>) cuelga un balcón de un muro: elija su estilo en la barra de la línea de comandos y haga clic fuera de la cara del muro, donde va; queda en ese lado del muro, a la cota del suelo de la planta. Un **balcón francés** (Juliet) colocado junto a una puerta o ventana se centra en ella, 200 mm más ancho. Donde no hay ningún muro cerca, el balcón queda donde se hace clic. **Balcony by outline** dibuja cualquier forma: haga clic en sus esquinas; el primer lado queda contra el edificio. **Gallery edge** dibuja el borde de una entreplanta sobre un espacio de doble altura con dos puntos (el vacío a la izquierda, la galería a la derecha).

Los estilos predefinidos cubren los tipos que se piden:

- **Juliet balcony**: una barandilla de balaustres delante de una puerta balconera sobre una pequeña repisa de piedra con dos ménsulas.
- **Cantilevered slab, glass**: una losa de hormigón en voladizo desde el forjado con rotura de puente térmico, embaldosada con pendiente, goterón bajo el borde, vidrio en tres lados.
- **Stacked, on posts**: una losa sobre pilares de acero delante, que se apoyan en el balcón de abajo (o en el terreno); repetida planta sobre planta.
- **On steel brackets**, **Hung from tie rods** (tirantes desde las esquinas delanteras hasta el muro), **Rounded corners**, **Curved front** con antepecho macizo.
- **Loggia** (logia): hundida en la fachada. Corta su hueco en el muro (tan ancho como la logia y tan alto como su **Recess height**), levanta sus muros laterales y una losa de techo detrás de la cara, y su propio **Back wall** (del tipo del muro, igual de alto) a la profundidad de la logia, donde va la puerta. La barandilla está en la cara.
- **Corner balcony**: a lo largo del muro y alrededor de la esquina del edificio, de vuelta por el muro lateral; colocado cerca del extremo de un muro, lo rodea.
- **Mezzanine gallery edge**: la losa de la galería detrás de su borde, un frontal sobre el borde y una barandilla de barrotes.

![Balcones en la primera planta: uno francés en la puerta, uno sobre pilares, uno en esquina, una logia, uno curvo, uno redondeado y uno colgado; el balcón de la planta superior, a trazos](shots/balconies-plan.webp)

La planta dibuja la losa, el acabado y la barandilla como los corta o los ve, y los balcones de la planta superior a trazos, por encima. Secciones y alzados dibujan su losa, barandilla, pilares y ménsulas.

### El configurador

**Architecture → Balcony configurator** (o el engranaje junto a **Style** en las propiedades del balcón) funciona como el de cubiertas: los estilos a la izquierda (los del proyecto, luego los predefinidos), los campos del balcón en pestañas a la derecha, sobre su planta o una sección por su centro tal como se construiría el borrador:

- **Shape**: **Kind**, **Outline** (rectángulo, esquinas redondeadas con **Corner radius**, frente curvo con **Front bulge** o el polígono dibujado), **Width** y **Depth**, el retorno del balcón en esquina, la losa y **Fascia height** de la galería.
- **Slab and finish**: **Slab thickness** y material, **Edge** (recto, con **drip** —goterón—, con **nosing** del acabado o **tapered** hasta **Edge thickness**), espesor y material del acabado y su **Fall (%)** hacia el frente, el intradós y **Thermal break** (80 mm entre la losa y el forjado interior; lo indican la tabla y el IFC).
- **Railing**: los diseños de barandilla (postes y pasamanos, balaustres, vidrio, cables, panel macizo) o un **parapet** con su espesor y material, o ninguno; altura, solo delante o también en los lados, retranqueo, pasamanos, postes y color.
- **Supports**: **Carried by** nada (voladizo), **posts** (redondos o cuadrados, tamaño y separación), **brackets** (vuelo, altura y separación) o **hangers** (altura a la que se anclan los tirantes) y su material.
- **Loggia**: altura del hueco, espesor de los muros laterales, losa más allá de la cara, losa de techo y muro trasero.

![El configurador de balcones: los estilos, la forma y la planta](shots/balcony-configurator.webp)

![Sus apoyos, sobre una sección del balcón](shots/balcony-configurator-section.webp)

**Apply** cambia el balcón en un paso. **Save as style…** lo guarda como estilo del proyecto (un tipo de balcón), que ofrecen la barra de la herramienta y las propiedades; **Update style** lo vuelve a guardar en su estilo y todos los balcones del estilo lo toman; junto al nombre de un estilo se renombra o se elimina (sus balcones quedan como están). Un estilo es un tipo: sus artículos de coste (Ejecución → Map articles) miden sus balcones.

![Las propiedades de un balcón: su estilo y los campos de su tipo](shots/balcony-properties.webp)

En el panel de propiedades un balcón muestra solo los campos que usa su tipo. Sus pinzamientos lo deslizan a lo largo del muro, el centro del frente fija la profundidad (en una logia, la del hueco), los centros de los lados la anchura; uno dibujado se arrastra por sus esquinas. El hueco y el muro trasero de una logia la siguen y se van con ella al eliminarla.

**Stack on storeys above** (en el configurador, en las propiedades o **Architecture → Stack on storeys above**) copia los balcones seleccionados en todas las plantas superiores en un paso, con Copy to storeys: la copia cuelga del muro de su planta que está donde su muro, una logia apilada corta el muro de esa planta, los pilares se apoyan en el balcón de abajo.

![La fachada sur en 3D: el balcón francés, los balcones apilados sobre pilares, el de esquina y el colgado de tirantes](shots/balconies-3d.webp)

![Desde el norte: la logia cortada en el muro, el balcón curvo con su antepecho y el redondeado](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** lista cada balcón con su planta, estilo, tipo, anchura, profundidad, superficie, longitud y diseño de barandilla, apoyo y rotura de puente térmico, con los totales. En IFC un balcón es un IfcElementAssembly (USERDEFINED, "Balcony") de un IfcElementAssemblyType, formado por sus partes: la losa un IfcSlab (FLOOR), acabado e intradós IfcCovering (FLOORING, CEILING), la barandilla un IfcRailing (GUARDRAIL) o el antepecho un IfcWall (PARAPET), pilares IfcColumn, ménsulas IfcDiscreteAccessory (BRACKET), tirantes IfcMember, la rotura de puente térmico un IfcBuildingElementPart; el hueco de una logia es un IfcOpeningElement en su muro. Lapin vuelve a leer sus balcones como balcones.
