---
title: Mobiliario
order: 47
summary: Sofás, sofás rinconera, sillones, sillas, camas, armarios, cómodas, librerías, aparadores, mesas, escritorios y cocinas lineales, a partir de tipos paramétricos, apoyados en los muros con un clic.
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
  - una cocina: ancho de los muebles bajos, zócalo, encimera, posición del fregadero y de la placa, muebles altos, columna;
- sus **materiales** por función: cuerpo, tapicería, tablero, frentes, ropa de cama, metal, vidrio. Escriba cualquier nombre de material; uno que el proyecto no tenga se crea, y **Gestionar → Materiales** ajusta su color y sus sombreados.

La vista previa junto a los campos muestra el símbolo en planta y el frente del tipo a medida que lo cambia. Cada mueble de un tipo sigue cualquier cambio.

![El diálogo Tipos de mobiliario con un sofá y su chaise longue](shots/furniture-types.webp)

## Tablas e IFC

**Tabla de mobiliario** lista cada tipo con su clase, su tamaño, cuántos muebles lo usan y las estancias en que están, con el total. **Mobiliario por estancia** lista cada estancia con lo que hay en ella. Ambas están en el navegador del proyecto, en **Tablas**, y se imprimen y exportan como cualquier tabla. Un tipo de mobiliario con una partida de precio se valora por unidad en el presupuesto.

En IFC un mueble es un **IfcFurniture**, con el tipo predefinido de su clase (SOFA, CHAIR, BED, TABLE, DESK, SHELF, o USERDEFINED con Wardrobe, Kitchen…). Su **IfcFurnitureType** se vincula con IfcRelDefinesByType, y sus cuerpos llevan sus materiales. Lapin vuelve a leer su propio mobiliario como muebles nativos y editables. El mobiliario de otros programas entra como referencias.
