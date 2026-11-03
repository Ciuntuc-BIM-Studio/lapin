---
title: Acero
order: 56
summary: Barras de acero con extremos configurables —prolongados o retranqueados, cortados por un plano, a inglete, ajustados a otra barra, rebajados—, hechas también desde un contorno dibujado; perfiles huecos y conformados en frío; naves de pórticos con cartelas.
---

## La pestaña Acero

![La pestaña Acero](shots/ribbon-steel.webp)

- **Nave de pórticos…**: una nave de acero completa a partir de unas pocas cifras, en el configurador de naves (más abajo).
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

## Naves de pórticos

**Acero → Nave de pórticos…** abre el configurador de naves. Como el de cubiertas, tiene los estilos a la izquierda, los campos de la nave en pestañas en el centro y, a la derecha, la nave tal como Lapin la construiría: un pórtico tipo visto a lo largo de la nave, con sus cartelas, y un resumen de lo que pesa.

![El configurador de naves](shots/steel-hall.webp)

- **Estilos**: las naves típicas del catálogo (**Nave 20 m**, **Nave 30 m**, un **Cobertizo 10 m** a un agua) y las del proyecto. Un estilo elegido rellena todos los campos; luego puede cambiarse cualquiera. **Guardar como estilo…** guarda la nave tal como está configurada como estilo del proyecto; **Actualizar el estilo** la guarda de nuevo en su estilo, y todas las naves de ese estilo lo siguen.
- **Pórtico**: la **Luz** (de eje de pilar a eje de pilar), las **Crujías** (iguales como `5 × 6`, o cada una: `6, 6, 7.5`), la **Altura de alero** (la cara superior de los dinteles sobre la cara exterior del pilar), la **Cubierta** (a dos aguas o a un agua) y su **Pendiente**, los perfiles de **Pilares** y **Dinteles**, y las **Bases de pilares** (articuladas o empotradas, guardadas para los nudos que vendrán).
- **Cartelas**: la **Cartela en el alero** y la **Cartela en la cumbrera**, cada una con su **Longitud** a lo largo del dintel desde la cara del pilar (o la cumbrera) y su **Canto** bajo el dintel allí, y cómo está **Hecha**: **cortada del perfil del dintel** (un I cortado en diagonal por el alma, una mitad soldada bajo el dintel: el alma y el ala del propio dintel) o **de chapas soldadas** (una **Chapa del alma** y una **Chapa del ala** de su propio espesor, y el ancho del ala).

![Las cartelas de chapas soldadas](shots/steel-hall-haunches.webp)

- **Hastiales**: **pilares de hastial bajo un dintel simple** (pilares de esquina y pilares a la **Separación** elegida, con su eje fuerte perpendicular al muro de hastial, bajo un dintel más ligero que llega a la cara exterior de los muros, los pilares ajustados a su cara inferior), o **un pórtico normal** en cada extremo.

![Un hastial con pilares](shots/steel-hall-gable.webp)

- **Acero secundario**: la **Separación de correas**, la **Separación de correas laterales**, las **Crujías arriostradas** (sus números, desde la primera) y una **Viga de alero**. Se guardan con la nave para las correas y arriostramientos que vendrán; aún no se construyen.
- **Colocación**: donde se cruzan el eje A y el eje 1, y la dirección de la luz en planta (**Rotación**); la vista previa muestra la retícula de la nave desde arriba.

**Crear nave** la hace en un solo paso, que un deshacer revierte: su propia **retícula** (ejes 1, 2… en los pórticos, A y B en los pilares), y en cada pórtico dos **pilares** (pilares de la estructura de un perfil de acero, con el alma en el plano del pórtico y la cabeza cortada según la pendiente) y dos **dinteles** a inglete en la cumbrera (uno solo, del pilar bajo al alto, en una cubierta a un agua): barras de acero ajustadas a las caras de los pilares, con sus cartelas. La nave está en la planta actual; el alero y la cumbrera son alturas de sus pórticos, no plantas nuevas. El resumen da los pórticos, las alturas y el acero por tipo: pilares, dinteles, cartelas, pilares de hastial, en total y por m² de planta.

![La nave en 3D](shots/steel-hall-3d.webp)

La nave sigue siendo editable. Selecciona una de sus barras: su sección **Acero** muestra su nave y **Editar nave…**, que abre el configurador sobre ella; **Aplicar** vuelve a construir la nave sobre las mismas barras, que conservan su identidad (una barra que ya no hace falta se va, una nueva se añade). Allí se listan también las cartelas del dintel, cada una cambiada por separado (tipo, longitud, canto, chapas): una cartela ajustada así se conserva cuando la nave se reconstruye; **×** la quita.

![Las cartelas de un dintel en Propiedades](shots/steel-hall-props.webp)

## IFC y cantidades

Una barra se exporta como **IfcMember** con su **IfcMemberType**. Su cuerpo se escribe como escriben el acero los demás programas (Tekla, Advance Steel, Revit): su sección **extruida** (IfcExtrudedAreaSolid), **recortada** por los planos de sus extremos (IfcBooleanClippingResult con IfcHalfSpaceSolid), y cada rebaje un **IfcOpeningElement** que la vacía. Una viga o un pilar de acero con extremos se escribe igual. Las cantidades de cada barra dan su longitud entre cortes, el área de su sección, su volumen y su peso neto (7850 kg/m³).

Una nave se exporta como **IfcBuildingSystem** (LOADBEARING) al que se asignan sus barras, con sus datos en el conjunto de propiedades `Lapin_SteelHall` y su propia **IfcGrid**. Un dintel con cartelas es un **IfcElementAssembly** del dintel y sus cartelas: una cartela cortada del perfil es un **IfcMember**, una de chapas soldadas dos **IfcPlate**, cada una extruida (IfcExtrudedAreaSolid). Las cartelas no entran en las cantidades del dintel; el resumen del configurador las pesa.
