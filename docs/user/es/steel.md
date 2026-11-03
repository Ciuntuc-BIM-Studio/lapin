---
title: Acero
order: 56
summary: Barras de acero con extremos configurables —prolongados o retranqueados, cortados por un plano, a inglete, ajustados a otra barra, rebajados—, hechas también desde un contorno dibujado; perfiles huecos y conformados en frío.
---

## La pestaña Acero

![La pestaña Acero](shots/ribbon-steel.webp)

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

## IFC y cantidades

Una barra se exporta como **IfcMember** con su **IfcMemberType**. Su cuerpo se escribe como escriben el acero los demás programas (Tekla, Advance Steel, Revit): su sección **extruida** (IfcExtrudedAreaSolid), **recortada** por los planos de sus extremos (IfcBooleanClippingResult con IfcHalfSpaceSolid), y cada rebaje un **IfcOpeningElement** que la vacía. Una viga o un pilar de acero con extremos se escribe igual. Las cantidades de cada barra dan su longitud entre cortes, el área de su sección, su volumen y su peso neto (7850 kg/m³).
