---
title: Detección de colisiones
order: 58
summary: Encuentra dónde los elementos del modelo se cruzan o se acercan demasiado: conjuntos de búsqueda por reglas, pruebas duras y de holgura entre ellos, colisiones seguidas de una ejecución a otra, mostradas en 3D y enviadas como BCF.
---

**Gestionar → Detección de colisiones** encuentra dónde los elementos del modelo se cruzan o se acercan más de lo debido. Funciona igual con los elementos de Lapin y con los archivos IFC y Fragments vinculados, así que también coordina los modelos que te envían las demás disciplinas.

![Detección de colisiones, junto a la vista 3D](shots/clash-dialog.webp)

## Pruebas y conjuntos

Una **prueba de colisión** compara dos **conjuntos de búsqueda**: el conjunto A con el B, o un conjunto consigo mismo. Un proyecto empieza con tres conjuntos (Structure, Services, Architecture) y tres pruebas duras: Structure × Services, Services × Services y Architecture × Services.

- Las pruebas **duras** encuentran elementos que se cruzan. Su **tolerancia** (5 mm por defecto) ignora los cruces así de pequeños, para que un acabado metido 3 mm en un muro no se informe.
- Las pruebas **de holgura** encuentran elementos más cercanos que una distancia (50 mm por defecto), y también los cruces.

Algunas parejas nunca se prueban, porque deben encontrarse: un muro y la puerta o ventana de su hueco, las partes de un mismo conjunto, los muros unidos en esquina y los elementos fijados entre sí (una viga apoyada en un pilar, un muro unido a una cubierta).

![Los ajustes de una prueba](shots/clash-test.webp)

## Ejecutar y seguir las colisiones

**Ejecutar prueba** ejecuta la prueba al frente; **Ejecutar todo** ejecuta todas las pruebas activas. Nada se ejecuta solo: los resultados se quedan en el proyecto hasta que vuelvas a ejecutar la prueba, y el diálogo avisa cuando el modelo ha cambiado desde entonces.

Cada colisión conserva su número de una ejecución a otra. Una colisión encontrada por primera vez es **Nueva**; encontrada de nuevo, **Activa**. Márcala a mano como **Revisada** o **Aprobada**, asígnala a alguien (**Asignado a**) y escribe **Notas**. Una colisión que una ejecución ya no encuentra pasa a **Resuelta**; si vuelve, se abre de nuevo con el mismo número.

Haz clic en una colisión para verla en 3D. Lapin selecciona los dos elementos, pone una caja de sección alrededor de donde se encuentran y los mira de lado, para que veas el tubo que atraviesa el muro y no la cara del muro. **solas en 3D** oculta el resto; las flechas recorren las colisiones una a una. El diálogo queda a la derecha, así que puedes mover la cámara mientras está abierto.

![Un tubo que atraviesa un muro, encuadrado por la colisión](shots/clash-3d.webp)

**BCF** convierte cada colisión abierta (o las seleccionadas) en una observación de tipo Clash con su punto de vista y las guarda como archivo BCF para otras herramientas BIM; al enviarlas de nuevo se actualizan las mismas observaciones. **CSV** guarda la lista para una hoja de cálculo.

## Conjuntos de búsqueda

**Conjuntos de búsqueda** contiene las reglas que eligen los elementos de un conjunto. Una regla lee un campo del elemento (su clase, tipo predefinido, tipo, planta, material, nombre, archivo vinculado o cualquier parámetro) con un operador: es, es uno de, no es, no es ninguno de, contiene, no contiene, al menos, como máximo, entre. Los números se leen como los escribe la gente: «600 mm» es 600.

Un conjunto toma los elementos que cumplen alguna regla, o todas. Una regla «no» siempre quita elementos, y un conjunto sin ninguna regla que añada elementos los toma todos. El número junto al conjunto muestra cuántos elementos toman las reglas mientras escribes.

![Las reglas de un conjunto de búsqueda](shots/clash-sets.webp)

## Grupos, informes y nubes

La lista agrupa sus colisiones **por planta**, **por elemento** (el elemento con más colisiones se las lleva, luego el siguiente), **por clases** o **por estado**. La casilla de un grupo selecciona todas sus colisiones, para darles un estado a la vez.

![Colisiones agrupadas por elemento](shots/clash-groups.webp)

- **XML** escribe las colisiones como Navisworks escribe sus informes, en sus grupos, para que otras herramientas de coordinación las lean.
- **PDF** imprime la lista en A4, agrupada igual.
- **HTML** crea una página con una imagen de cada colisión abierta, encuadrada por turno en la vista 3D.
- **Marcar en la vista** dibuja las colisiones abiertas en la planta, sección o alzado al frente como nubes rojas con la etiqueta prueba.colisión (2.14: la segunda prueba, colisión 14), solo en pantalla. Un clic en una nube selecciona el elemento.

![Colisiones como nubes en una planta](shots/clash-marks.webp)

Un proyecto empieza también con una cuarta prueba, **Openings for MEP**: la comprobación de Gestionar → Comprobación de colisiones… ejecutada como prueba, para que sus problemas se numeren y sigan como los demás. Su profundidad es cuánto le falta al tramo para su hueco.

Una prueba dura encuentra también un elemento entero dentro de otro, donde ninguna cara se cruza: un tubo olvidado dentro de un forjado. Su profundidad es la menor anchura del elemento interior.

> Los tramos de instalaciones de Lapin pueden cortar sus propios huecos en muros y forjados (Gestionar → Orden de corte). Entonces un tubo que atraviesa un muro no es una colisión: el muro tiene su hueco. **Gestionar → Comprobación de colisiones…** revisa esos huecos.
