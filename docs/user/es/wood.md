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

## La lista de madera

**Wood → Timber schedule** hace la lista de despiece de toda la madera del proyecto, las piezas de los muros y las de las cubiertas: pieza, sección, clase, longitud, unidades, longitud total y volumen, con los totales por sección. En IFC cada pieza es un IfcMember (STUD, PLATE, el dintel y el alféizar por su nombre) bajo su muro, y el aislamiento un IfcCovering INSULATION.
