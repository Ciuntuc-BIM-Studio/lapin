---
title: Mobiliario
order: 47
summary: Sofás, sofás rinconera, sillones, sillas, camas, armarios, cómodas, librerías, aparadores, mesas, escritorios y cocinas en seis disposiciones, a partir de tipos paramétricos, apoyados en los muros con un clic.
---

## Colocar mobiliario

La pestaña **Interiores** tiene, después de los acabados, una herramienta para cada clase de mueble: **Sofá** (<kbd>SO</kbd>), **Sofá rinconera**, **Sillón** (<kbd>AC</kbd>), **Silla**, **Cama** (<kbd>BD</kbd>), **Mesita de noche**, **Armario** (<kbd>WD</kbd>), **Cómoda**, **Librería**, **Aparador**, **Mesa** (<kbd>TB</kbd>), **Mesa de centro** y **Escritorio**. Elija una y haga clic en la planta.

Haga clic cerca de un muro y el mueble se apoya en él. Su trasera queda sobre la cara del muro (sobre el enlucido, no sobre el núcleo), su frente mira a la estancia, y se desliza a lo largo del muro hasta dejar de entrar en el muro de la esquina. Haga clic lejos de cualquier muro y el mueble queda donde hizo clic.

Mientras una herramienta está activa, la barra de la línea de comandos muestra:

- **el tipo** que coloca la herramienta, elegido entre los tipos de esa clase del proyecto (si aún no hay ninguno, el primero se crea al colocar el mueble);
- **el giro**, 0°, 90°, 180° o 270°, para un mueble exento;
- **Contra los muros**, que puede desactivar para colocar un mueble exactamente donde hace clic, incluso junto a un muro.

El panel de propiedades muestra el mismo tipo antes del clic, con su tamaño y **Editar tipos…**.

![Un salón, una cocina y un dormitorio amueblados, en planta](shots/furniture-plan.webp)

Las plantas dibujan el mobiliario con su símbolo, según la convención de dibujo, no seccionado:

- un sofá con su respaldo, sus brazos y sus cojines;
- una cama con su cabecero, sus almohadas y el edredón doblado;
- un armario con su barra de colgar y sus puertas;
- una cocina con su fregadero y su placa, y los muebles altos a trazos.

Los alzados y las secciones dibujan los cuerpos, con los sombreados de sus materiales. En 3D los muebles son cuerpos low-poly creados por Lapin.

![La casa amueblada en 3D](shots/furniture-3d.webp)

## Sillas alrededor de una mesa, y la cocina

**Sillas alrededor de una mesa:** haga clic en una mesa o un escritorio. Las sillas se colocan a lo largo de los lados largos, aproximadamente una cada 600 mm, y una en cada extremo cuando es lo bastante ancho; una mesa redonda las recibe en círculo. Cada silla mira a la mesa. Un escritorio solo recibe sillas en su frente. Las sillas son del tipo elegido para la herramienta **Silla**.

**Cocina** (<kbd>KT</kbd>) se dibuja con dos puntos a lo largo de un muro:

- la longitud de la línea es la de la cocina, y su trasera queda sobre la cara del muro;
- los muebles bajos se reparten por igual, de unos 600 mm;
- tiene encimera, fregadero y placa (su posición a lo largo de la cocina se ajusta en el tipo), muebles altos encima y una columna para el frigorífico en un extremo.

La **Longitud** de una cocina sigue siendo editable en sus propiedades; los muebles se reparten de nuevo.

### Disposiciones de la cocina

Un tipo de cocina tiene una **Layout** (disposición): **One wall** (la línea recta), **Galley** (dos líneas paralelas con un pasillo entre ellas), **L-shaped**, **U-shaped**, **Island** (una línea en el muro del fondo y una isla separada con su pasillo) o **Peninsula** (una L con una línea que sale de su brazo, con la encimera volada para taburetes). Al elegir una, el tipo parte de las medidas de la disposición, y cada una sigue editable junto a la vista previa: la longitud de los brazos desde el muro del fondo, el pasillo entre líneas, la longitud de la línea opuesta, la longitud, el fondo y el lugar de la isla, la longitud y el voladizo de la península y, en una L o una península, el rincón a la izquierda o a la derecha.

![Las seis disposiciones en planta: en una pared, en paralelo, en L, en U, con isla y con península](shots/kitchen-layouts.webp)

**Sink on** y **Hob on** los ponen en la línea principal o en la otra parte (el brazo, la línea opuesta, la isla, la península), **Sink at** y **Hob at** dicen dónde a lo largo, y la **Tall unit (fridge)** queda en un extremo libre. Los muebles de rincón son ciegos; los muebles altos solo van en las líneas contra un muro, no en una isla ni en una península.

![Un tipo de cocina: en L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) dibuja cualquier disposición con dos puntos a lo largo del muro donde está la línea principal: la línea es su longitud, su fondo sobre la cara del muro, y una L, una península, una U o una cocina en paralelo se desliza al rincón o entre los muros junto a los que se dibuja. **Flip** da a una L o a una península el rincón al otro lado. La tabla de mobiliario nombra la disposición y da el fondo de la huella. En IFC sigue siendo un IfcFurniture de un tipo Kitchen definido por el usuario.

### Muebles y electrodomésticos de la cocina

Un tipo de cocina puede enumerar sus propios **módulos** bajo sus parámetros, en **Tipos de mobiliario**, en dos líneas:

- la **Línea de muebles bajos**: los módulos de la línea principal desde la izquierda, vistos de frente (después de un rincón ciego);
- la **Línea de muebles altos**: los módulos colgados encima de ella.

**Añadir** pone un módulo de la clase elegida al final de una línea. **Cocina típica** empieza la línea de muebles bajos con un fregadero, un lavavajillas, un módulo de cajones y una placa sobre un horno. Las flechas mueven un módulo a la izquierda o a la derecha, y la papelera lo quita.

Cada módulo tiene un **ancho**. Un mueble toma cualquier ancho de 100 a 800 mm: elija 150, 200, 300, 400, 450, 500, 600 u 800, o escriba otro. Un electrodoméstico toma los tamaños en que se fabrica. Los módulos de la línea de muebles bajos son:

- **Mueble bajo**, cuyo **Frente** es un cajón sobre una puerta, puertas (una o dos, con el lado en que se articula una puerta sola, **Bisagras en el lado**), cajones (1 a 4), cajones para ollas (un cajón bajo sobre dos hondos) o estantes abiertos;
- **Mueble de fregadero**: un fregadero de un seno, de 1½ senos, doble (desde 800) o de rincón, con grifo normal, grifo extraíble o sin grifo. Un fregadero de rincón llega al rincón ciego cuando está junto a él.
- **Placa sobre horno**, de 600, 800 o 900 de ancho, de inducción o de gas, con o sin el horno debajo;
- **Cocina independiente**, de 500, 600 o 900 de ancho, de inducción o de gas. La encimera se detiene a cada lado de ella.
- **Lavavajillas** integrado, de 450 o 600 de ancho, tras un frente de altura completa que combina con los demás;
- **Frigorífico**: bajo encimera, columna integrada (una puerta de frigorífico sobre una de congelador) o independiente;
- **Columna de horno**, con cajones bajo el horno y una puerta encima;
- **Columna**, con puertas o estantes abiertos;
- **Tapajuntas** y **Hueco**.

Una columna, un frigorífico o una columna de horno tiene su propia **Altura**. En la línea de muebles altos, un **Mueble alto** tiene puertas, puertas de vidrio o estantes abiertos, su propia **Altura** y su propia altura **Desde el suelo**. Deje cualquiera de las dos vacía para tomar los valores de la línea. Una **Campana** es una campana de chimenea (a 650 sobre la encimera, con su conducto hasta lo alto de los muebles altos) o una campana integrada en un mueble alto. Un **Hueco** deja el muro libre. Los muebles altos pasan de largo ante una columna.

Sin una línea de muebles altos enumerada, cada mueble bajo recibe un mueble alto de su ancho, y una placa o una cocina recibe una campana de chimenea. En una isla o una península, **Campana sobre la placa allí** cuelga una campana sobre la placa que está en ella.

Lo que los módulos dejan de la línea se reparte por igual como antes, y menos de 100 mm se convierte en un tapajuntas. Los módulos que no caben en la línea se dejan fuera, y la vista previa y el elemento lo dicen. **Ajustar a los módulos**, en las propiedades de una cocina, da a su longitud lo que sus módulos necesitan: la suya, más un rincón ciego y la columna del extremo. Un tipo sin módulos propios se construye como antes.

En planta, cada módulo tiene su propio símbolo:

- el abatimiento de las puertas bajas, a trazos y solo en el detalle más fino;
- los senos del fregadero y su grifo;
- las zonas o los quemadores de la placa;
- las columnas, cruzadas;
- **DW** en el lavavajillas, **F** en un frigorífico y **OV** en una columna de horno;
- los muebles altos y las campanas, por encima.

Los alzados y el 3D muestran cada frente, tirador y electrodoméstico.

Los materiales tienen dos funciones más: **Zócalo** y **Tiradores**, además de los frentes, el cuerpo, la encimera (tablero), el metal y el vidrio. Un tipo de cocina hecho antes toma para el zócalo el material de su cuerpo, y para los tiradores el de su metal.

![Los módulos de un tipo de cocina: la línea de muebles bajos y la de muebles altos, con la vista previa del motor](shots/kitchen-units-types.webp)

![Una cocina de módulos y electrodomésticos mezclados, en 3D](shots/kitchen-units-3d.webp)

**Interiores → Muebles de cocina** hace una tabla de los módulos de cada cocina, desde la izquierda, la línea de muebles bajos y luego la de muebles altos, con su ancho, su altura y su frente o electrodoméstico. En IFC la cocina sigue siendo un solo IfcFurniture de su tipo, y los módulos viajan con el tipo, así que Lapin los vuelve a leer.

## Las propiedades de un mueble

Seleccione un mueble para ver:

- **Tipo**, su clase y su tamaño;
- **Rotación**;
- **Sobre el suelo**, para un mueble elevado sobre una tarima;
- **Longitud**, en una cocina.

**Voltear** da al mueble su otra mano, en su sitio: la chaise longue al otro lado, el frigorífico en el otro extremo, el rincón del sofá rinconera al otro lado. Su frente sigue donde estaba. **Modificar → Simetría** hace lo mismo.

Arrastre el pinzamiento del centro de un mueble para moverlo; arrastre el que tiene delante para girar su frente hacia el cursor.

![Las propiedades de un sofá](shots/furniture-properties.webp)

## Tipos de mobiliario

**Interiores → Tipos de mobiliario** lista los tipos del proyecto por grupos (asientos, camas, almacenaje, mesas, cocina), cada uno con cuántos muebles lo usan. Elija una clase y **Tipo nuevo**, o **Duplicar** un tipo para cambiar una copia. Un tipo tiene:

- su **nombre**, su **anchura**, su **fondo** y su **altura** (en un sofá, la del respaldo; en una cama, la del cabecero; en una cocina, la de la encimera);
- los parámetros propios de su clase:
  - sofás y sillones: plazas, brazos, ancho del brazo, altura del asiento, una chaise longue a la izquierda o a la derecha y su largo, patas;
  - un sofá rinconera: el lado del rincón y el largo de ese lado;
  - una cama: la cara superior del colchón, el cabecero, el piecero, las almohadas, un edredón doblado;
  - un armario: puertas, batientes o correderas, cajones abajo, zócalo;
  - una cómoda: filas y columnas de cajones;
  - una librería: baldas y puertas abajo hasta una altura;
  - un aparador: puertas y cajones;
  - una mesa: rectangular, redonda u ovalada, sobre cuatro patas o un pie central;
  - un escritorio: una cajonera a la izquierda o a la derecha;
  - una cocina: ancho de los muebles bajos, zócalo, encimera, posición del fregadero y de la placa, muebles altos, columna y sus propios módulos (arriba);
- sus **materiales** por función: cuerpo, tapicería, tablero, frentes, ropa de cama, metal, vidrio y, en una cocina, el zócalo y los tiradores. Escriba cualquier nombre de material; uno que el proyecto no tenga se crea, y **Gestionar → Materiales** ajusta su color y sus sombreados.

La vista previa junto a los campos muestra el símbolo en planta y el frente del tipo a medida que lo cambia. Cada mueble de un tipo sigue cualquier cambio.

![El diálogo Tipos de mobiliario con un sofá y su chaise longue](shots/furniture-types.webp)

## Tablas e IFC

**Tabla de mobiliario** lista cada tipo con su clase, su tamaño, cuántos muebles lo usan y las estancias en que están, con el total. **Mobiliario por estancia** lista cada estancia con lo que hay en ella. Ambas están en el navegador del proyecto, en **Tablas**, y se imprimen y exportan como cualquier tabla. Un tipo de mobiliario con una partida de precio se valora por unidad en el presupuesto.

En IFC un mueble es un **IfcFurniture**, con el tipo predefinido de su clase (SOFA, CHAIR, BED, TABLE, DESK, SHELF, o USERDEFINED con Wardrobe, Kitchen…). Su **IfcFurnitureType** se vincula con IfcRelDefinesByType, y sus cuerpos llevan sus materiales. Lapin vuelve a leer su propio mobiliario como muebles nativos y editables. El mobiliario de otros programas entra como referencias.
