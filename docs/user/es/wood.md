---
title: Madera
order: 54
summary: Muros de entramado de madera desde el configurador: montantes en módulo, durmientes, travesaños, y cada puerta y ventana enmarcada por sus reglas.
---

## Muros de entramado de madera

Un tipo de muro puede construirse como entramado de madera: **Wood → Timber frame** abre el configurador, con los tipos de muro del proyecto a la izquierda. Marca **Timber frame** y el núcleo del tipo se hace de piezas en lugar de una sola capa: los durmientes y carreras, los montantes en su módulo, las filas de travesaños y, alrededor de cada puerta y ventana, los montantes enteros, los de apoyo, el dintel y el alféizar. Las demás capas quedan como están, y el aislamiento llena los huecos entre las piezas.

![El configurador de entramado](shots/wood-frame.webp)

- **Preajustes**: **nórdico** (45 × 145 a 600 mm, C24) o **rumano** (50 × 150 a 625 mm, C24, una fila de travesaños a 1,25 m). Luego se puede cambiar cualquier campo.
- **Montantes, separación, clase, durmientes, montantes de extremo, filas de travesaños**. **Módulo desde** fija dónde empieza el módulo: el inicio de cada muro o el origen del proyecto, para que los muros de una fachada lo compartan.
- **Portante**: un tabique no carga nada y lleva sobre sus huecos una pieza plana en lugar de dintel.

## Puertas y ventanas

Las puertas y ventanas siguen siendo del arquitecto: si mueves una o cambias su tipo, su entramado la sigue. El marco se mide desde el **hueco bruto**: la puerta o ventana más las holguras de montaje (laterales, arriba, bajo la ventana; la puerta baja hasta el suelo).

Cada hueco toma la regla de su **tramo de anchura** (la tabla bajo **Openings**: hasta 900 mm, hasta 1800 mm, más, por defecto): sus montantes enteros y de apoyo por lado, su dintel (ninguno, macizo, LVL o laminado, su sección y piezas) y su alféizar. Ningún montante queda en un hueco bruto; los cortos sobre el dintel y bajo el alféizar mantienen el módulo, para que las juntas de los tableros caigan sobre montantes. Dos huecos más cercanos que el **machón mínimo** comparten un dintel y sus montantes enteros exteriores. Un hueco demasiado cerca del extremo del muro se avisa en las propiedades del muro, nunca se mueve.

Selecciona una puerta o ventana: su sección **Wood** muestra el hueco bruto, la regla que toma y lo que recibe. Marca **Regla propia** para dar solo a ese hueco otros montantes, otro dintel u otro alféizar.

## Esquinas, tableros y alzado del entramado

- **Esquinas**: donde otro muro termina contra el extremo de este (esquina a tope), el muro que pasa lleva **tres montantes** (uno tras la cara interior del otro), una esquina **California** (un montante plano en la cara interior, para el revestimiento) o **solo sus montantes de extremo**. Una esquina a inglete conserva los montantes de extremo de ambos muros.
- **Encuentros en T**: donde un muro termina contra este en su recorrido, este lleva **un montante a cada lado** o **montantes y travesaños en escalera** entre ellos.
- **Tableros**: marca una capa (OSB, tablero de fibras, placa de yeso) y da el tamaño de la hoja: la capa se coloca en hojas verticales, con las juntas en el módulo de los montantes, cortadas en los huecos brutos y a lo largo de la coronación. **Wood → Boards schedule** cuenta las hojas de cada muro, su área neta y lo que se recorta.
- **Alzado del entramado**: en la sección **Wood** de un muro de entramado, el botón crea una sección a lo largo del muro por el centro de su núcleo, a 1:20, con todas las piezas cortadas, una cadena de cotas por los huecos brutos, el módulo de montantes debajo y el tamaño de cada hueco bruto. Se coloca en una lámina como cualquier vista.

## Paneles prefabricados

Marca **Prefabricated panels** para hacer el entramado en taller: indica el **panel más largo** (lo que permitan la mesa del taller y el camión) y el **más corto**. Cada muro se corta en paneles por la junta más larga dentro de esa longitud, en el módulo de los montantes cuando se puede, nunca a través de los montantes completos, los de apoyo, el dintel o el alféizar de un hueco, ni del refuerzo de un encuentro en T. Cada panel se entrama por separado, con sus propios montantes de extremo y soleras en las juntas. Un panel que no cabe en la longitud (un hueco demasiado ancho) queda entero y el muro lo avisa.

![Un muro prefabricado en el configurador](shots/wood-panels.webp)

Los paneles se marcan con el nombre del muro y su número a lo largo de él (W3-P1, W3-P2…). La sección **Wood** del muro muestra sus longitudes, el alzado del entramado dibuja las juntas y las marcas sobre el muro, y **Wood → Wall panels** lista cada panel con su muro, longitud, altura, huecos, piezas, volumen de madera y masa, para pedirlos, fabricarlos y elevarlos por orden. Los tableros de un panel también llevan su marca. En IFC cada panel es un IfcElementAssembly (Timber frame panel) con sus piezas y tableros.

## CLT

En el mismo configurador un tipo de muro o forjado puede construirse **en paneles CLT**: indica las capas (el espesor de cada una, 3, 5, 7 o 9), la clase, el panel más largo y el más alto (o ancho) que hace la prensa, la junta entre paneles (a tope, media madera, lengüeta), la distancia de un hueco al borde del panel y su holgura. Un muro se corta en paneles a lo largo, con las juntas apartadas de los huecos; cada panel es el propio cuerpo del núcleo, con sus huecos brutos recortados. Un forjado se corta en paneles a lo largo de su luz (el sentido corto, o según x o y). **Wood → CLT panels** lista cada panel con su marca, tamaño, capas, clase, junta, área y volumen; en IFC cada uno es un IfcBuildingElementPart llamado CLT panel.

- **Juntas**: en un **solape a media madera** (half lap) las dos mitades se solapan lo indicado; una junta con **lengüeta** (spline) tiene un rebaje en una cara y una tira de contrachapado dentro, como pieza propia. Ambas se modelan en los paneles, así que los volúmenes y la tabla son lo que corta el taller.
- **Esquinas de los huecos**: una fresadora no corta una esquina interior recta. **El radio de la fresa** lo deja en cada esquina; el **sobrecorte en hueso de perro** corta un círculo en cada esquina para que quepa un marco recto. El configurador dibuja el corte.
- **Alzado de paneles**: la sección **Wood** de un muro CLT crea una sección a lo largo de él a 1:20, con las juntas y marcas de los paneles y las medidas de los huecos.
- **Cubiertas CLT**: selecciona una cubierta inclinada y marca **CLT panels** en su sección **Wood**: sus faldones se hacen con placas CLT en lugar de pares o cerchas (las soleras y la cubrición se quedan), con las ventanas de tejado y buhardillas recortadas, en franjas del ancho del panel a lo largo del alero, unidas a tope donde el faldón es más largo que un panel.

## Forjados de viguetas

Un tipo de forjado puede construirse **sobre viguetas**: elígelo en el configurador y escoge **on joists**. Indica la sección, separación y clase de las viguetas, la dirección en que salvan la luz (la corta, o según x o y), si una **vigueta de borde** cierra los bordes, las **filas de riostras** y, alrededor de cada hueco del forjado (una escalera, un patinillo), los **brochales** a cada lado y las **capas del cabecero** en sus extremos. Las viguetas que corta el hueco terminan en el cabecero, y se añade una vigueta donde los brochales dejan un hueco mayor que la separación. **Fill the cavities** pone el material del núcleo entre viguetas. El configurador muestra un forjado de muestra con hueco de escalera.

![Un tipo de forjado sobre viguetas](shots/wood-joists.webp)

Las viguetas sustituyen al núcleo del forjado; las capas de encima y de debajo se quedan. Sus piezas están en la lista de madera, y en IFC cada una es un IfcBeam (JOIST, vigueta de borde o cabecero por nombre) bajo el forjado.

## La lista de madera

**Wood → Timber schedule** hace la lista de despiece de toda la madera del proyecto, las piezas de los muros y las de las cubiertas: pieza, sección, clase, longitud, unidades, longitud total y volumen, con los totales por sección. En IFC cada pieza es un IfcMember (STUD, PLATE, el dintel y el alféizar por su nombre) bajo su muro, y el aislamiento un IfcCovering INSULATION.
