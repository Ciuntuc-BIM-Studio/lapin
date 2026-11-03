---
title: Habitaciones y colores de habitación
order: 45
summary: Coloque habitaciones con un clic dentro de los muros, asígneles nombre y número, y coloréelas o sombréelas en una planta por uso o por departamento, con una leyenda.
---

## Colocar una habitación

Elija **Arquitectura → Habitación** (<kbd>RM</kbd>) y haga clic dentro de un espacio cerrado por muros. Lapin encuentra el contorno de la habitación entre las caras de los muros, los muros cortina y los pilares que rodean el punto. Las puertas no abren la habitación: se cierra a través de ellas. Un pilar situado dentro de la habitación se descuenta de su superficie.

La habitación sigue a sus muros. Desplace un muro y la habitación crece o se reduce con él. Si se borra un muro y ya nada cierra la habitación, esta conserva el contorno que tenía y su sello se vuelve rojo. **Arquitectura → Actualizar habitaciones** guarda los contornos que las habitaciones encuentran ahora.

Para un espacio sin muros alrededor, como una terraza o un extremo de una zona diáfana, use **Habitación por contorno** y haga clic en sus esquinas. Esa habitación conserva el contorno que usted dibuja.

![Una planta con dos habitaciones coloreadas por uso y una leyenda de colores](shots/rooms-plan.webp)

## Las propiedades de una habitación

Seleccione una habitación haciendo clic en su sello o en cualquier punto de su interior. En el panel de propiedades:

- **Nombre** y **Número.** Las habitaciones nuevas se numeran por nivel: 101, 102… en la planta baja. El nombre de una habitación es el de su función hasta que le das uno propio; vaciado, vuelve a la función.
- **Uso**, **Departamento** y **Acabado del suelo.** Escriba lo que quiera o elija un valor que ya tengan otras habitaciones.
- **Altura.** Vacía significa la altura del nivel.
- **Color propio** y **Sombreado propio** sustituyen el esquema de colores para esta habitación.

Las cantidades de debajo muestran la superficie neta de la habitación (sin los pilares), su perímetro y su volumen. Arrastre el pinzamiento del sello para desplazar el sello.

![Las propiedades de una habitación](shots/room-properties.webp)

## Etiqueta de habitación

**Arquitectura → Etiqueta de habitación** (o **Etiqueta de habitación…** en las propiedades de una habitación) elige lo que muestra la etiqueta de cada habitación en las plantas, línea a línea: el nombre, el número, la función, el departamento, la superficie, la superficie bruta, el perímetro, la altura, el volumen o el acabado del suelo, o un texto propio. La primera línea va en negrita. **Normas de dibujo → Etiquetas** muestra la misma etiqueta como texto: `{name}|{number}|{area}`.

![El diálogo Etiqueta de habitación](shots/room-tag.webp)

## Colores de habitación

**Vista → Colores de habitación** colorea las habitaciones de una planta. Elija un esquema, elija por qué colorea (**Uso**, **Nombre**, **Departamento**, **Número** o **Acabado del suelo**) y ajuste **Esta planta muestra** a ese esquema. Cada valor que tienen las habitaciones recibe una fila con:

- un **Color**,
- un **Sombreado** de los patrones del proyecto, con su **Escala** y su color de **Tinta**,
- y una vista previa de su aspecto.

**Rellenar los colores que faltan** da a cada valor sin color uno de una paleta suave. **Nuevo** y **Duplicar** crean más esquemas, por ejemplo uno para una presentación al cliente y otro por departamento.

![El diálogo Colores de habitación](shots/room-colors.webp)

Los colores se dibujan bajo los muros, el mobiliario y los giros de las puertas, de modo que la planta sigue siendo legible. Se imprimen en las láminas y van en las exportaciones a PDF y DXF. Una planta nueva no muestra colores de habitación hasta que usted elige un esquema para ella, en **Colores de habitación** o en las propiedades de la planta.

## Leyenda de colores

**Anotar → Leyenda de colores**: haga clic donde va la leyenda en la planta. Enumera los colores que usan las habitaciones de la planta, con sus nombres, y sigue cualquier cambio del esquema. Desplácela con **Desplazar**.

## Cotas de las habitaciones

**Annotate → Room dimensions** (<kbd>ID</kbd>) acota de una vez el interior de las habitaciones de una planta. Cada lado de una habitación recibe una cadena de cotas alineada, justo dentro de él. La cadena tiene un punto en cada esquina del lado y en las jambas de cada puerta y ventana que hay en él.

- Con habitaciones seleccionadas, solo se acotan esas. Sin selección, se acotan todas las habitaciones del nivel de la planta.
- Los puntos se sujetan a los muros, como en cualquier cota. Mueve un muro y las cadenas lo siguen.
- Vuelve a ejecutarlo en una habitación para rehacer sus cadenas, por ejemplo después de añadir una puerta. Las cadenas antiguas se sustituyen, no se duplican.
- Los lados muy cortos, de menos de 30 cm, se dejan fuera. Cualquier cadena se edita o se borra como una dibujada a mano.

![Dos habitaciones acotadas por dentro: la cadena de cada lado, con la puerta y las ventanas](shots/room-dimensions.webp)

## Acabados de habitaciones

**Acabados → Acabar habitación** (<kbd>FR</kbd>) coloca un acabado en la habitación en la que hace clic: su suelo, el acabado de sus paredes y su techo, cada uno siguiendo el contorno de la habitación como lo hace la habitación. Mueva un muro y los acabados lo siguen. **Acabados → Acabados de habitaciones** edita los acabados; **De la biblioteca** trae algunos ya hechos: Salón, Dormitorio, Baño, Cocina, Planta baja sobre losa y Cuarto técnico. Mientras la herramienta está activa, el panel de propiedades muestra el acabado que coloca, para elegirlo antes del clic. El **Acabado** de una habitación, en sus propiedades, coloca otro o lo quita.

Un acabado tiene:

- **Suelo**, sus capas desde el suelo terminado hacia abajo (baldosas, adhesivo, recrecido…). Los huecos de losa en la habitación (una caja de escalera) se descuentan. **A través de las puertas, hasta el eje del muro** prolonga el suelo bajo cada puerta, para que el suelo de la habitación vecina lo encuentre en el eje del muro.
- **Paredes**, sus capas desde la cara del muro hacia la habitación, también alrededor de las islas de la habitación. Cada capa puede cambiar de material en su altura, como la capa de un tipo de muro: en el Baño, pintura sobre azulejos hasta 2100 (13 mm de espesor, azulejos y adhesivo); en la Cocina, una franja de azulejos de 900 a 1500. Una pieza sin material no deja nada allí. Las puertas y ventanas de los muros alrededor de la habitación se recortan. **Altura**: vacía, hasta el techo.
- **Techo**, sus capas desde la losa sobre la habitación hacia abajo (desde la altura de la habitación cuando no hay losa), con sus huecos descontados.
- **Rodapié**: un tipo de rodapié colocado alrededor de la habitación, interrumpido en sus puertas (vea Rodapiés, en Losas y cubiertas).
- **Una losa estructural bajo el suelo**: una losa de hormigón armado propia bajo las capas del suelo, con su espesor y cuánto sobresale del contorno de la habitación (bajo los muros). Es una losa como cualquier otra, en la estructura y en su tabla.

**Guardar** cambia el acabado, y cada habitación que lo tiene se vuelve a colocar. **Acabar habitaciones seleccionadas** lo coloca en las habitaciones seleccionadas.

Cada acabado mide, por material, su superficie **bruta** (el contorno de la habitación, sin descontar nada: el área del suelo, el perímetro de las paredes por la altura de cada pieza) y la **neta** (lo construido: puertas, ventanas y huecos de losa descontados, el suelo bajo las puertas añadido), y su volumen. **Acabados → Tabla de acabados** lista cada habitación acabada por parte y material, con los totales por material al final; como cualquier tabla, hace gráficos, se exporta y va en una lámina. En IFC un acabado es un IfcCovering: FLOORING, CLADDING o CEILING.

Un acabado reviste lo que ya tienen los muros de la habitación: si los tipos de muro llevan su propio enlucido, dé a las paredes de la habitación solo lo que va encima (pintura, azulejos), o use tipos de muro solo con el núcleo.

![Los acabados de habitaciones: el suelo del Baño, las paredes con azulejos hasta 2100 y el techo](shots/room-finishes.webp)

![Las propiedades de una habitación con su acabado](shots/room-finish-properties.webp)

## Habitaciones en 3D y en IFC

Las habitaciones están ocultas en la vista 3D, para que no tapen el edificio. **Visibilidad / Gráficos** en la vista 3D puede mostrarlas.

En una exportación IFC, cada habitación es un IfcSpace, con su número, nombre, uso, superficies y volumen.

## Habitaciones desde un archivo IFC

**Archivo → Importar IFC…** trae los espacios del archivo como habitaciones, sea cual sea el programa que los escribió (Revit, ArchiCAD, Allplan, Bonsai…):

- cada una va a **su propio nivel**, aquel en el que la sitúa el archivo;
- su **contorno** procede de su forma (una extrusión, un B-rep o una malla, o la huella dibujada junto a ella); conserva ese contorno y no sigue a los muros;
- su **nombre** es el nombre largo del espacio y su **número** es su nombre (tal como los escriben Revit y ArchiCAD); su **altura**, y su **desfase de base** cuando empieza por encima de su nivel (una galería, un suelo elevado), proceden de su forma;
- su **uso**, su **departamento** y su **acabado del suelo** proceden del archivo cuando este los indica (el tipo de objeto o la categoría del espacio, una propiedad Department, el revestimiento del suelo), de modo que un esquema de colores por uso o por departamento las colorea de inmediato;
- todo lo demás que el espacio tenía en el archivo, sus conjuntos de propiedades y cantidades (Pset_SpaceCommon, BaseQuantities, los conjuntos propios del programa), se conserva y se enumera, de solo lectura, en **Propiedades IFC** dentro de sus propiedades, con un filtro cuando son muchos.

El mensaje de la importación indica cuántas habitaciones han entrado. Búsquelas en las plantas de sus niveles (cada una muestra su sello), en una tabla de **Habitaciones** (**+** bajo **Tablas** en el navegador) o en la ficha IFC del navegador; la vista 3D oculta las habitaciones.

Un espacio cuya forma no da ningún contorno entra igualmente, con su nombre y sus propiedades, como una habitación sin contorno: aparece en la tabla, pero no se dibuja en ningún sitio. Selecciónelo (en la ficha IFC del navegador), abra la planta de su nivel y luego use **Buscar con un clic** (haga clic dentro de sus muros) o **Dibujar contorno** en sus propiedades.
