---
title: Importar y exportar
order: 110
summary: Intercambie el modelo como IFC, traiga dibujos DXF, vincule archivos y exporte modelos 3D para renderizado y RA.
---

Todo lo que entra o sale está en el menú **Archivo**.

![El menú Archivo](shots/file-menu.webp)

## IFC

**Exportar IFC…** escribe el modelo como IFC. Cada muro, losa, puerta y cubierta es un elemento IFC real con sus capas, su tipo y sus propiedades, de modo que el archivo se abre como un modelo en otras herramientas BIM. Las puertas y ventanas indican cómo se abren: la mano de cada hoja, hacia dónde gira y los paneles, tal como los define IFC, para que otros programas dibujen las mismas aperturas.

La exportación pregunta el esquema (**IFC4**, que se lee en todas partes, o **IFC4X3**), el detalle (las piezas como elementos propios o un cuerpo por elemento), la fase y cómo se escriben los cuerpos. **Geometría** es **B-rep facetado** (exacto, lo que más a menudo esperan otros programas) o **conjuntos de caras**: las mismas caras en un archivo mucho más pequeño. **Simplificar** escribe menos triángulos donde un cuerpo es una malla fina, como los conductos y tuberías del archivo de otro programa, nunca más lejos de él que la **tolerancia** que escriba (2 mm al principio). Los cuerpos que construye Lapin (muros, losas, puertas) conservan sus caras exactas, y simplificar cambia solo la forma de un cuerpo, nunca sus propiedades.

![Exportar IFC](shots/export-ifc.webp)

**Importar IFC…** lee el IFC de otro programa. Antes de que entre nada, Lapin lee el archivo y muestra lo que contiene: sus elementos por disciplina (arquitectura, estructura, MEP, mobiliario y equipamiento, emplazamiento), cada disciplina por clase con cuántos hay, y sus plantas con cuántos elementos hay en cada una. Desmarque lo que no necesite, por ejemplo MEP y mobiliario cuando trabaje en la estructura, o conserve solo las plantas en las que trabaja. **Cómo** trae los elementos reconocidos como elementos de Lapin, todos como referencias, o vinculados. Mientras el archivo se lee y se importa, Lapin muestra lo que está haciendo y cuánto lleva; un archivo grande tarda un poco.

![Importar IFC](shots/import-ifc.webp)

Algunos archivos llevan 2D además de su modelo: símbolos de planta, huellas, rayados y textos dibujados para un elemento, o elementos dibujados solo en 2D (líneas, textos, símbolos sin cuerpo). Cuando un archivo los tiene, el diálogo los cuenta en **Dibujado en 2D**. Desmarque **el 2D propio de los elementos** y la planta dibuja esos elementos por su corte, como los de Lapin; desmarque **elementos dibujados solo en 2D** y se quedan fuera. Ambos entran si no los desmarca.

![Dibujado en 2D](shots/import-ifc-2d.webp)

**Limpieza y optimización** reúne las reglas de la importación, las mismas para cualquier archivo, guardadas como suyas para la próxima importación. **Guardar una sola vez un cuerpo repetido** (marcada salvo que la desmarque) guarda una vez un cuerpo que el archivo repite con otros nombres (un perno, una brida, un soporte); sus elementos lo comparten y nada se pierde. Las demás dejan fuera, cuando las marca, lo que otro programa trajo consigo: los **cuerpos planos** (textos, líneas y rayados convertidos en triángulos), los **elementos más lejos del resto** que la distancia que escriba (los textos de un plano, un cajetín), los **duplicados** en el mismo lugar y los **puertos y huecos escritos como objetos**. **Simplificar las mallas** escribe menos triángulos dentro de la tolerancia que escriba; **leer los números escritos como texto** convierte `DoubleLength:0,01` en 0,01 m y `1.15395 kg` en una masa (un número entero solo, un código, sigue siendo texto). En **Qué traer**, **Conjuntos de propiedades** muestra los conjuntos del archivo con cuántos elementos tiene cada uno; los **técnicos** (la transformación de Navisworks, identificadores internos, estadísticas de geometría) quedan fuera salvo que los marque. El mensaje de la importación dice qué hizo cada regla.

![Limpieza y optimización](shots/import-ifc-rules.webp)

Lapin reconoce los muros, losas, pilares, vigas, zapatas, puertas y ventanas y los convierte en elementos de Lapin que puede editar. Lo que no puede reconocer entra como referencia, dibujada pero no editable. Las puertas y ventanas entran abriéndose como dice el archivo: su mano, el lado hacia el que abren y, en las ventanas, cómo se abre cada panel.

Los espacios del archivo (de Revit, ArchiCAD y otros) entran como habitaciones de Lapin, cada una en su propio nivel, con su contorno, número, nombre, altura, función, departamento y acabado de suelo: consulte [Habitaciones y colores de habitación](rooms.md#habitaciones-desde-un-archivo-ifc). Todo lo que un elemento importado tenía en el archivo (sus conjuntos de propiedades y cantidades) se conserva y se muestra en **Propiedades IFC**, en sus propiedades. Una referencia sale de **Exportar IFC…** tal como entró: con el mismo GlobalId, la misma clase y los mismos atributos (descripción, etiqueta, tipo predefinido), sus conjuntos de propiedades y cantidades con cada valor del tipo que tenía en el archivo, y su tipo, material, puertos, conexiones y sistema, cada uno con su GlobalId. Si trae solo las instalaciones de un archivo y las exporta, el archivo nuevo las lleva enteras.

## DXF y DWG

**Importar DXF…** trae un dibujo CAD a la vista que tiene delante, con sus capas, sombreados, textos y cotas. Entra como dibujo que puede editar y al que se puede forzar el cursor. Para usar un DWG, guárdelo primero como DXF desde su programa CAD (AutoCAD, BricsCAD o DraftSight: **Save As → DXF**). La importación de PDF aún no está disponible. En una planta, **Colocado: por las coordenadas del mapa** sitúa un dibujo en coordenadas reales (un plano catastral en Stereo 70) donde lo pone el mapa (consulte [El emplazamiento y sus datos](site.md)).

## Vínculos

**Vincular IFC…** mantiene otro archivo vinculado en lugar de importarlo. Es de solo lectura y se vuelve a cargar cuando cambia. Esto también funciona con un DXF. **Gestionar → Vínculos** muestra, recarga, convierte o elimina los vínculos.

## Modelos 3D

**Exportar 3D…** escribe el modelo como superficies para renderizado, visualización y realidad aumentada (RA):

| Formato | Para |
|---|---|
| GLB, glTF | Blender, three.js, visores web. Se incluyen las imágenes propias de sus materiales. |
| USDZ | AR Quick Look de Apple, Reality Composer, Omniverse. Imágenes incluidas. |
| USDA | OpenUSD como texto, un solo archivo. |
| DAE | COLLADA, para SketchUp y herramientas más antiguas. |

![Exportar 3D](shots/export-3d.webp)

Usted elige qué exportar, qué fase y el origen. **Qué** es **todo**, o el modelo **tal como lo muestra la vista**: la vista 3D que tiene delante, sin sus elementos y categorías ocultos ni las plantas que no muestra, con las capas que muestra (sin acabados, o solo los núcleos). Cuando esa vista está cortada (el corte por encima de las plantas mostradas, una sección mostrada en 3D, la caja de sección), **Corte** corta también la exportación ahí y cierra cada corte con sus materiales, para que el archivo se vea como la pantalla. Una vista 3D que deja algo fuera se exporta tal como lo muestra, salvo que elija otra cosa. Cada elemento conserva su clase IFC, su tipo y su GlobalId. Para intercambiar el modelo en sí y no sus superficies, use **Exportar IFC…**.
