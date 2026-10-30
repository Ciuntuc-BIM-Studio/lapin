---
title: Medios auxiliares
order: 55
summary: Andamios de fachada y encofrados de forjados y vigas, dispuestos a partir del modelo por sus configuradores.
---

La ficha **Medios auxiliares** modela las obras provisionales de la obra: los andamios de fachada y el encofrado de forjados y vigas. Lapin los dispone a partir del modelo y cuenta lo que necesitan. Cuando el modelo cambia, ellos lo siguen.

![La ficha Medios auxiliares](shots/ribbon-site-works.webp)

Los andamios y encofrados son **temporales**: se montan y se desmontan en la fase **Execution**. El primero que coloque añade esa fase si el proyecto no la tiene, y la convierte en la fase activa. Las vistas de fases anteriores no los muestran. Consulte **Gestionar → Fases**.

![Un andamio alrededor de la casa y el encofrado de la primera planta sobre sus puntales](shots/site-works-3d.webp)

## Andamios

- **Andamio en muros** (<kbd>SW</kbd>): seleccione los muros (Mayús-clic para añadir más) y pulse el botón. El andamio rodea sus caras exteriores, a la distancia del muro que fija su tipo, hasta la parte superior de los muros. Cierra sus esquinas por sí mismo. Si mueve un muro o lo hace más alto, el andamio lo sigue.
- **Andamio** (<kbd>SC</kbd>): dibuje su línea en planta a lo largo de la fachada y pulse Intro para terminar. El andamio se sitúa a la izquierda de la línea. Elija el otro lado en sus propiedades.

Cada tramo recto se divide en las longitudes de módulo del tipo, con los menos módulos posibles para llegar a su extremo. Los niveles suben hasta que la plataforma superior queda al alcance de la cota de trabajo. La barandilla se sitúa por encima de la plataforma superior. Los pies se apoyan en el terreno, sobre husillos de base. Si el terreno baja más de lo que absorben los husillos, Lapin se lo indica.

![Las propiedades de un andamio](shots/scaffold-properties.webp)

En sus propiedades puede fijar la **cota de trabajo**: déjela vacía para tomar la parte superior de los muros. También puede elegir si los pies se apoyan **sobre el terreno** o **sobre la planta**, o separarlo de los muros con **detach**.

Cada andamio y cada encofrado tiene un **tipo**. Lo elige en la parte superior de las propiedades, o pasa el tipo de un elemento a otros con Igualar tipo (MA). En **Parámetros de tipo** están los valores que fija el configurador: el sistema, las longitudes de módulo, la anchura, la altura de nivel, el tablero, las separaciones, la partida. También puede cambiarlos ahí. Un cambio es del tipo, así que todos los elementos de ese tipo lo siguen. Crear similar (CS) dibuja uno nuevo del mismo tipo. El tipo que cree en el configurador es el que toma el siguiente andamio o encofrado.

**Tipos de andamio…** abre el configurador. Fija el sistema (modular o de marcos), las longitudes de módulo, la anchura y la altura de nivel, la distancia al muro, las plataformas, las barandillas y los rodapiés, los anclajes, los husillos de base, los módulos de acceso y la red de protección. El alzado muestra un tramo de la longitud y la altura que elija, con lo que cuenta.

![El configurador de andamios](shots/scaffold-configurator.webp)

## Encofrado

- **Encofrado** (<kbd>FW</kbd>): seleccione forjados y vigas y pulse el botón. Cada uno recibe el encofrado de todas sus caras encofrables.
- **Encofrado en cara** (<kbd>FF</kbd>): elija una cara. En 3D, la cara bajo el cursor se ilumina con su nombre: el fondo de un forjado, el fondo de una viga o uno de sus laterales. En planta, haga clic en el borde de un forjado o dentro de su contorno. Una cara elegida después se añade al mismo encofrado.

El **encofrado de un forjado** cubre su fondo con tablero sobre vigas secundarias, sobre vigas primarias, sobre puntales. Las vigas bajo el forjado no se vuelven a encofrar, ni tampoco los muros y pilares en los que se apoya ni sus huecos. Las separaciones salen de la tabla del tipo, según el canto del forjado. Los puntales bajan hasta el forjado inferior, el terreno o la planta de debajo. Los bordes libres también se encofran, a la altura del forjado más un recrecido. En las propiedades se elige qué caras se encofran y la dirección de las vigas primarias.

El **encofrado de una viga** es su fondo, sobre sopandas y puntales, y sus laterales. Cada lateral tiene la altura de la viga por debajo del forjado hormigonado con ella. Los pilares y las vigas de más canto lo interrumpen. Puede añadir berenjenos a lo largo de las aristas inferiores.

**Tipos de encofrado…** abre el configurador, para forjados y para vigas. Para forjados fija el sistema (vigas de madera y puntales, o paneles), el tablero, las vigas y sus longitudes de catálogo, la tabla de separaciones, los bordes, los puntales y las reutilizaciones. Para vigas fija las sopandas, los puntales, los yugos según el canto de la viga, el berenjeno y las reutilizaciones. La sección muestra las capas y el puntal que elige el motor para la altura que usted indique.

![El configurador de encofrados](shots/formwork-configurator.webp)

## Muros, pilares, vigas de atado y zapatas

**Encofrado** y **Encofrado en cara** también funcionan con muros, pilares y zapatas:

- **Un muro** recibe sus dos caras y sus extremos libres, menos sus puertas y ventanas. Un muro multicapa con una viga de atado o un zuncho de hormigón recibe solo estos. En sus propiedades puede elegir ambas caras, las vigas de atado o los extremos libres.
- **Un pilar** recibe sus laterales, hasta el forjado o la viga hormigonados sobre él.
- **Una zapata** recibe los laterales de cada escalón de una zapata aislada, o los laterales de la base y del alzado de una zapata corrida.

Lo que se apoya contra una cara se omite: otro muro, un pilar. Un forjado hormigonado encima detiene la cara en su fondo. El tipo fija los paneles (sus anchuras y alturas), los latiguillos a través de los muros, las mordazas de pilar y los puntales de aplomado.

![El configurador para muros y pilares](shots/wall-formwork-configurator.webp)

## Plano de encofrado

**Plano de encofrado** crea una planta de la planta activa en la fase Execution, cortada a un metro sobre el suelo, **vista desde abajo**: un plano de techos. Los puntales quedan cortados. El encofrado por encima de ellos se dibuja como lo vería mirando hacia arriba: las vigas primarias y secundarias, el tablero y los bordes. Está orientado igual que la planta, así que ambos coinciden.

**Plano de encofrado desde arriba** crea la misma planta vista desde arriba, con el encofrado dibujado a trazos sobre el corte. Cualquier planta puede alternar entre las dos en sus propiedades (**Visto desde**) o en **Rango de vista**.

![El plano de encofrado de la planta baja](shots/formwork-plan.webp)

## Lo que cuentan

Cada andamio y cada encofrado muestra sus cantidades en sus propiedades:

- del andamio, la superficie, los módulos, los niveles, los montantes, las plataformas y los anclajes;
- del encofrado, la superficie de contacto por cara, los puntales por tamaño y las vigas por longitud de catálogo.

**Tabla de encofrados** y **Tabla de andamios** añaden sus tablas al navegador del proyecto, en Tablas, para colocarlas en una lámina o exportarlas.

La tabla de encofrados termina con una línea por tipo:

- **el juego**: el máximo de encofrado hormigonado a la vez. Se calcula por fase de ejecución cuando la obra se divide en varias, y si no, por planta.
- **lo que hay que comprar**: el juego, o el total dividido entre las reutilizaciones del tipo, lo que sea mayor.

Cada tipo tiene su **partida** para el presupuesto: el código de la base de precios que usa su presupuesto, y su texto. Los introduce en el configurador, y la tabla los muestra en cada línea.

En IFC se escriben como conjuntos (`IfcElementAssembly` FORMWORK y SCAFFOLD) con el estado TEMPORARY. Las piezas son miembros, placas y accesorios. Cada una lleva su recurso de producto de construcción, y el encofrado se conecta con el elemento que encofra.
