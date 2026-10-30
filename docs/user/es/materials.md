---
title: Materiales, texturas y sombreados
order: 90
summary: Defina el color, los sombreados y las juntas de cada material, ponga en él su propia imagen y traiga sus propios patrones de sombreado .pat.
---

**Gestionar → Materiales** enumera los materiales del proyecto. Elija uno para modificarlo.

![El cuadro de diálogo Materiales](shots/materials-dialog.webp)

## Lo que define un material

- **Color (3D)** y las caras dibujadas sobre él en 3D. Lapin las deduce del nombre, por ejemplo las hiladas de un ladrillo o la retícula de una baldosa. Los **Extremos** y **Superior e inferior** pueden verse distintos de las caras.
- **Sombreado** para cada lugar donde se dibuja: cortado en una sección, cortado en una planta y visto en una elevación. Cada uno tiene su escala, ángulo, alineación, relleno y pluma.
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
