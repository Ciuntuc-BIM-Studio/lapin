---
title: Materiales, texturas y sombreados
order: 90
summary: Defina el color, los sombreados y las juntas de cada material, ponga en él su propia imagen y traiga sus propios patrones de sombreado .pat.
---

**Gestionar → Materiales** enumera los materiales del proyecto. Elija uno para modificarlo.

![El cuadro de diálogo Materiales](shots/materials-dialog.webp)

## Apariencia: cómo se renderiza un material

La pestaña **Apariencia** define cómo se ve el material en 3D y en los programas de render a los que exportas. La pestaña **Gráficos 2D** conserva sus sombreados y plumas, como antes.

![La apariencia de un material, con su vista previa](shots/material-appearance.webp)

- **Color** y **Textura**: una textura dibujada (ladrillo, tablas de madera, baldosas…) o tu propia imagen. **W** y **H** fijan el tamaño de una pieza sobre el modelo, en metros; **Rotación** la gira.
- **Rugosidad**: 0 es un espejo, 1 es tiza. **Metalicidad**: 1 para metal desnudo (aluminio anodizado, acero, cromo), 0 para todo lo pintado o lacado en polvo, la madera y la piedra.
- **Transparencia**: cuánto se ve a través. **Vidrio (transmisión)** e **IOR** (1,52 para el vidrio) hacen que el vidrio refracte en un programa de render.
- **Brilla** emite luz, como el difusor de una lámpara.

La esfera muestra el resultado al momento. Lo que no fijas viene del nombre: un material llamado *vidrio* o *glass* es transparente, *acero* o *steel* metálico, *parquet* y *laminado* son tablas de madera. **Apariencia desde el nombre** quita de nuevo tus valores.

## De la biblioteca

**De la biblioteca…** ofrece materiales listos: metales (aluminio RAL 7016 antracita, anodizado, acero, cobre…), vidrio (claro, azul, verde, gris, mate, reflectante), maderas en tablas, hormigón, ladrillo, yeso y pintura, baldosas y piedra, plástico y tejido. Un clic añade uno al proyecto; cámbialo como cualquier otro.

![La biblioteca de materiales](shots/material-library.webp)

## Datos: descripción, parámetros, cómo se mide

La pestaña **Datos** guarda lo que el material dice de sí mismo. Lo escribes una vez; lo lee cada tipo en que está el material: la etiqueta de composición, las mediciones, el presupuesto, la exportación IFC.

![Los datos de un material: su descripción, sus parámetros, cómo se mide y sus espesores de catálogo](shots/material-data.webp)

- **Descripción**: el texto del pliego, en varias líneas (*Paneles de lana mineral, λ = 0,035 W/mK, fijados con tacos*). Una etiqueta de composición lo escribe en lugar del nombre.
- **Parámetros**: un nombre, un tipo y un valor, como en los elementos. **Añadir uno habitual…** ofrece **Conductividad térmica λ**, **Resistencia al vapor μ** y **Densidad**, que van a IFC con sus nombres estándar (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, en unidades SI), y **Reacción al fuego** y **Resistencia a compresión**, que quedan como tuyos. Tus parámetros van a IFC como *Lapin_Material*.
- **Medido por** dice cómo lo cuentan **Costes → Mediciones** (por material) y el presupuesto: **área** (m², una fila por espesor: placas, aislamientos, láminas), **volumen** (m³: hormigón, grava, solera), **masa** (kg, por su densidad) o **piezas** (de una placa del tamaño que indiques). Vacío, se mide como sus elementos.
- **Espesores de catálogo (mm)**: los espesores en que lo fabrica el fabricante, por ejemplo *50, 80, 100, 150, 200, 240*. Las tablas de capas de **Tipos de muro** y **Tipos de losa** los ofrecen junto al espesor (puedes escribir otro) y una capa de otro espesor se marca con un aviso.

La exportación IFC escribe la descripción como Description del material y los parámetros como sus conjuntos de propiedades; la importación los lee de vuelta, también del archivo de otro programa.

## Un material para todos los marcos y todo el vidrio

Las puertas y ventanas nuevas se hacen con materiales compartidos, parte por parte: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** y **Hardware**. Cambia **Window frame** una vez y cambian todos los marcos de ventana.

Para usar otros materiales, abre **Arquitectura → Puertas y ventanas**, elige el **Material del marco**, el **Material del vidrio** y los demás para un tipo, y pulsa **Usar para todas las ventanas** (o **Usar para todas las puertas**). Todos los tipos de esa clase los toman, en un solo paso de deshacer. Los muros cortina también tienen un material de marco y otro de vidrio.

![Los materiales de las partes en el configurador de puertas y ventanas](shots/joinery-materials.webp)

## Duplicar, sustituir, seleccionar

- **Duplicar** copia un material con un nombre nuevo.
- **Sustituir por** un material y luego **Sustituir**: cada capa, tipo, puerta, ventana y elemento que usaba el primero usa ahora el segundo, en un solo paso de deshacer. **Sustituir y eliminar** quita además el primero. Si no, un material en uso no se puede eliminar.
- **Seleccionar elementos** selecciona todos los elementos con una parte de ese material.
- **Buscar materiales** y la lista de categorías filtran la lista.

## Materiales para render

Twinmotion, Lumion, Enscape, Blender y otros programas de render sustituyen los materiales por su nombre. Lapin escribe cada material **una sola vez, con su propio nombre**, con su color, textura, rugosidad, metalicidad y vidrio, y divide la malla de cada elemento por material.

1. Nombra tus materiales como los quieres en el programa de render, por ejemplo *Aluminium RAL 7016 anthracite* para todos los marcos y *Glass blue tinted* para todo el vidrio.
2. **Archivo → Exportar 3D (glTF, USD, DAE)…** y elige **GLB** (recomendado: un archivo con las texturas, los nombres exactos y el vidrio), **USDZ** u **OBJ + MTL** (un .zip con las texturas; los espacios de los nombres pasan a guiones bajos).
3. Importa el archivo en el programa de render. Allí todos los marcos comparten un material; sustitúyelo una vez por un metal del programa y cambian todos.

![Una casa con marcos gris oscuro y vidrio azul](shots/materials-render-3d.webp)

La exportación IFC también nombra el estilo de superficie de cada material como él, con su transparencia y cómo refleja.

## Lo que define un material

- **Color (3D)** y las caras dibujadas sobre él en 3D. Lapin las deduce del nombre, por ejemplo las hiladas de un ladrillo o la retícula de una baldosa. Los **Extremos** y **Superior e inferior** pueden verse distintos de las caras.
- **Sombreado** para cada lugar donde se dibuja: cortado en una sección, cortado en una planta y visto en una elevación. Cada uno tiene su escala, ángulo, alineación, relleno y pluma. Un alzado o una sección trama las caras que ve con el sombreado de elevación, donde nada más cercano las oculta (Vistas, Tramas de superficie); hasta que se lo das, su nombre propone uno (ladrillo BRICK, piedra STONE, madera BOARDS, revestimiento metálico VERTICAL), nunca el BATT del aislamiento.
- **Categoría**, por ejemplo acabado, aislamiento o estructura. Los estilos de vista la usan para dejar fuera los acabados en una vista estructural.
- **Membrana** dibuja una capa fina como una línea discontinua en lugar de una banda.
- **Dibujar juntas.** De forma predeterminada, un material continúa de un elemento al siguiente sin línea, como dos muros alineados o un revoco sobre dos plantas. Márquelo para dibujar la junta.

## Su propia imagen en un material

Un material puede llevar una imagen suya: una foto del ladrillo, la madera o la piedra reales.

1. Elija el material y haga clic en **Imagen propia → Importar imagen…**. Elija un archivo PNG o JPEG.
2. Defina **W** y **H**: el ancho y el alto, en metros, de una copia de la imagen sobre el modelo. La imagen se repite por las caras.

![La imagen propia de un material, aplicada a 1.2 × 1.2 m](shots/own-image.webp)

La imagen se muestra en la vista 3D y se guarda dentro del archivo del proyecto. También va a las exportaciones 3D: **GLB**, **glTF** y **USDZ** la llevan. **DAE** y **USDA** son archivos únicos sin espacio para imágenes, por lo que conservan el color del material.

![La misma casa con la imagen propia del material en 3D](shots/own-image-3d.webp)

Las imágenes de más de 2048 píxeles se reducen a ese tamaño al importarlas. El botón de la papelera quita la imagen del material.

## Sus propios patrones de sombreado (.pat)

**Importar .pat** lee un archivo de patrones de sombreado de AutoCAD. Cada patrón que contiene se añade al proyecto, listo para elegirse como sombreado de un material.

Lapin también guarda el archivo en **Mis patrones** en su ordenador. En cualquier otro proyecto, **Añadir a este proyecto** vuelve a traer esos patrones. Un proyecto conserva los patrones que usa, de modo que se abre igual en otro ordenador.

![Mis patrones: los archivos .pat que ha importado, guardados para todos los proyectos](shots/my-patterns.webp)
