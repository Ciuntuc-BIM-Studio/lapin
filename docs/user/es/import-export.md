---
title: Importar y exportar
order: 110
summary: Intercambie el modelo como IFC, traiga dibujos DXF, vincule archivos y exporte modelos 3D para renderizado y RA.
---

Todo lo que entra o sale está en el menú **Archivo**.

![El menú Archivo](shots/file-menu.webp)

## IFC y Fragments

**Exportar IFC o Fragments…** escribe el modelo como IFC. Cada muro, losa, puerta y cubierta es un elemento IFC real con sus capas, su tipo y sus propiedades, de modo que el archivo se abre como un modelo en otras herramientas BIM. Las puertas y ventanas indican cómo se abren: la mano de cada hoja, hacia dónde gira y los paneles, tal como los define IFC, para que otros programas dibujen las mismas aperturas.

La exportación pregunta el esquema (**IFC4**, que se lee en todas partes, o **IFC4X3**), el detalle (las piezas como elementos propios o un cuerpo por elemento), la fase y cómo se escriben los cuerpos. **Geometría** es **B-rep facetado** (exacto, lo que más a menudo esperan otros programas) o **conjuntos de caras**: las mismas caras en un archivo mucho más pequeño. **Simplificar** escribe menos triángulos donde un cuerpo es una malla fina, como los conductos y tuberías del archivo de otro programa, nunca más lejos de él que la **tolerancia** que escriba (2 mm al principio). Los cuerpos que construye Lapin (muros, losas, puertas) conservan sus caras exactas, y simplificar cambia solo la forma de un cuerpo, nunca sus propiedades.

![Exportar IFC](shots/export-ifc.webp)

Las vigas, los pilares y los perfiles de acero se escriben por su perfil paramétrico, con nombre y valores mecánicos ([Bibliotecas de perfiles](profiles.md)).

**Importar IFC…** lee el IFC de otro programa. Antes de que entre nada, Lapin lee el archivo y muestra lo que contiene: sus elementos por disciplina (arquitectura, estructura, MEP, mobiliario y equipamiento, emplazamiento), cada disciplina por clase con cuántos hay, y sus plantas con cuántos elementos hay en cada una. Desmarque lo que no necesite, por ejemplo MEP y mobiliario cuando trabaje en la estructura, o conserve solo las plantas en las que trabaja. **Cómo** trae los elementos reconocidos como elementos de Lapin, todos como referencias, o vinculados. Mientras el archivo se lee y se importa, Lapin muestra lo que está haciendo y cuánto lleva; un archivo grande tarda un poco.

![Importar IFC](shots/import-ifc.webp)

Algunos archivos llevan 2D además de su modelo: símbolos de planta, huellas, rayados y textos dibujados para un elemento, o elementos dibujados solo en 2D (líneas, textos, símbolos sin cuerpo). Cuando un archivo los tiene, el diálogo los cuenta en **Dibujado en 2D**. Desmarque **el 2D propio de los elementos** y la planta dibuja esos elementos por su corte, como los de Lapin; desmarque **elementos dibujados solo en 2D** y se quedan fuera. Ambos entran si no los desmarca.

![Dibujado en 2D](shots/import-ifc-2d.webp)

**Limpieza y optimización** reúne las reglas de la importación, las mismas para cualquier archivo, guardadas como suyas para la próxima importación. **Guardar una sola vez un cuerpo repetido** (marcada salvo que la desmarque) guarda una vez un cuerpo que el archivo repite con otros nombres (un perno, una brida, un soporte); sus elementos lo comparten y nada se pierde. Las demás dejan fuera, cuando las marca, lo que otro programa trajo consigo: los **cuerpos planos** (textos, líneas y rayados convertidos en triángulos), los **elementos más lejos del resto** que la distancia que escriba (los textos de un plano, un cajetín), los **duplicados** en el mismo lugar y los **puertos y huecos escritos como objetos**. **Simplificar las mallas** escribe menos triángulos dentro de la tolerancia que escriba; **leer los números escritos como texto** convierte `DoubleLength:0,01` en 0,01 m y `1.15395 kg` en una masa (un número entero solo, un código, sigue siendo texto). En **Qué traer**, **Conjuntos de propiedades** muestra los conjuntos del archivo con cuántos elementos tiene cada uno; los **técnicos** (la transformación de Navisworks, identificadores internos, estadísticas de geometría) quedan fuera salvo que los marque. El mensaje de la importación dice qué hizo cada regla.

![Limpieza y optimización](shots/import-ifc-rules.webp)

Lo que coloca el archivo entra como un solo grupo con su nombre, que puede abrir y limpiar, o suelto si desmarca **Como un solo grupo** (vea [Grupos y limpieza](groups.md)).

**Plantas.** Las plantas de una importación son las del archivo: sustituyen a las de su proyecto. Cada planta del archivo entra con su nombre, su GlobalId y su cota tal como la da el archivo, leída en la unidad del archivo (milímetros o metros). Una planta de su proyecto a la cota de una del archivo (con 5 cm de margen) se fusiona con ella: sus elementos se quedan donde están, y sus vistas, su plano y lo dibujado en él pasan a la planta del archivo, que conserva el nombre del archivo. Una planta suya que no está en ninguna cota del archivo se elimina cuando no contiene nada (ningún elemento, nada dibujado en su plano), y sus vistas pasan a la planta del archivo más cercana; una que contiene algo se queda, y el mensaje de la importación dice cuál. Así, los Cimientos, la Planta baja y la Planta primera de un proyecto nuevo dejan sitio a las del archivo. Todo es un solo paso de deshacer junto con la importación. Un archivo vinculado guarda sus plantas para sí y no cambia ninguna de las suyas. Al terminar la importación, la vista 3D encuadra el modelo, salvo que haya movido la cámara mientras entraba.

**Todo editable**, una opción de **Cómo**, va más allá del reconocimiento: lo que quedaría como referencia se convierte en un sólido de Studio hecho de sus propios triángulos, el mismo elemento con su clase, sus propiedades, su planta y su grupo, al que se aplican enseguida empujar/tirar, redondeos y booleanas (vea [Studio](studio.md)). Un cuerpo de más de 5.000 triángulos, como una bomba o un accesorio con una malla fina, queda como referencia, y el mensaje de la importación dice cuántos y por qué. **Importar Fragments…** tiene el mismo **Cómo**: un archivo Fragments solo contiene triángulos, y aun así sus muros (con sus puertas y ventanas), losas y pilares se reconocen a partir de ellos como elementos de Lapin, el resto queda como referencia o, con **Todo editable**, se convierte en sólidos de Studio. Los elementos de un IFC que el reconocimiento no pudo leer del archivo (un muro con los huecos cortados en su cuerpo) se intentan también a partir de sus triángulos. **Importar malla** pregunta **Como: editables**. Las referencias importadas antes se convierten con Modificar → **Convertir a nativo** (NV): reconocidas donde se puede, el resto sólidos de Studio; un grupo seleccionado equivale a sus miembros. Una referencia importada antes se hace editable de una en una con Studio → **Convertir en sólido Studio**, que la mantiene en su grupo y en su planta.

Lapin reconoce los muros, losas, pilares, vigas, zapatas, puertas y ventanas y los convierte en elementos de Lapin que puede editar. Lo que no puede reconocer entra como referencia, dibujada como la da el archivo: su forma no se puede editar, pero se desplaza, gira, refleja, copia y repite como cualquier elemento. Un elemento se convierte en elemento de Lapin solo cuando es el del propio archivo: Lapin mide cada uno que reconoce frente al cuerpo que le da el archivo, y uno que difiere en más de 1 mm o del 0,5 % de su volumen (un pilar cuya cabeza el archivo inclina, un extremo de muro escalonado, que un elemento de Lapin dejaría recto) entra como la referencia que era; el mensaje de la importación dice cuántos y por qué. Las puertas y ventanas entran abriéndose como dice el archivo: su mano, el lado hacia el que abren y, en las ventanas, cómo se abre cada panel. Una cubierta plana de otro programa entra como cubierta de Lapin cuando su IfcRoof está hecho de dos o más losas apiladas una sobre otra, o de un cuerpo con un conjunto de capas: la losa (o capa) superior es la cubrición, con su material y su espesor, las de debajo el forjado, del material de la más gruesa; el contorno es el del forjado, sin alero, en su cara inferior. Una cubierta inclinada, o una de una sola losa sin capas, se queda como llegó. Los elementos reconocidos reciben solo lo que tiene el archivo: sin dinteles, alféizares, umbrales ni mochetas propios de Lapin, y cada hueco con el tamaño del archivo; actívalos en los tipos de los elementos si los quieres.

Los acabados que otro programa exportó como muros propios (un revoco de 2,5 cm en la fachada, un enlucido interior) pasan a ser una capa del muro que cubren, cuando están pegados a su cara, tan altos como él y a lo largo de él: el muro recibe un tipo con esa capa (*Masonry 350 + Lime render*), sus propias capas quedan donde estaban y el mensaje de la importación dice cuántos. Un acabado separado de su muro, o que cubre solo una parte, sigue siendo un muro delgado. Un pilar que el archivo da como un cuerpo cortado en cabeza por una pendiente (bajo una viga o una cubierta inclinada) entra como pilar de su sección, hasta su punto más alto.

**Asignar tipos importados** (Gestionar) lista los tipos que creó una importación (tipos de muros, losas, puertas, ventanas, pilares, vigas y cimentaciones), cada uno con su archivo y cuántos elementos tiene. Elija en **Pasa a ser** un tipo del proyecto de la misma clase y pulse **Asignar**: sus elementos toman el tipo del proyecto, un muro conserva su núcleo donde estaba y el tipo importado desaparece. Con **Recordar** marcado, una importación posterior de un tipo de esa clase y nombre toma enseguida el del proyecto, y su mensaje lo dice. Las elecciones recordadas aparecen en el diálogo, cada una con **Olvidar**.

![Asignar tipos importados](shots/map-types.webp)

Los espacios del archivo (de Revit, ArchiCAD y otros) entran como habitaciones de Lapin, cada una en su propio nivel, con su contorno, número, nombre, altura, función, departamento y acabado de suelo: consulte [Habitaciones y colores de habitación](rooms.md#habitaciones-desde-un-archivo-ifc). Todo lo que un elemento importado tenía en el archivo (sus conjuntos de propiedades y cantidades) se conserva y se muestra en **Propiedades IFC**, en sus propiedades. Una referencia sale de **Exportar IFC…** tal como entró: con el mismo GlobalId, la misma clase y los mismos atributos (descripción, etiqueta, tipo predefinido), sus conjuntos de propiedades y cantidades con cada valor del tipo que tenía en el archivo, y su tipo, material, puertos, conexiones y sistema, cada uno con su GlobalId. Si trae solo las instalaciones de un archivo y las exporta, el archivo nuevo las lleva enteras.

**Importar Fragments…** abre el mismo panel para un archivo `.frag` de That Open: sus elementos por disciplina y tipo, sus plantas y **Como un solo grupo**. Un archivo exportado de este mismo proyecto no trae nada nuevo al importarlo de vuelta: sus elementos ya están aquí.

![Importar Fragments](shots/import-frag.webp)

**Importar Fragments…** trae un archivo `.frag`: cada elemento con geometría se convierte en una referencia dibujada a partir de su cuerpo, en la clase que indica el archivo, en la planta donde el archivo lo coloca (las plantas del archivo sustituyen a las suyas: vea **Plantas** más arriba), con los atributos del archivo en **Propiedades IFC**. Los elementos con el mismo cuerpo (la misma forma colocada en muchos sitios) lo comparten. Importar de nuevo el mismo archivo actualiza lo que trajo antes. Lo que se reconoce a partir de sus triángulos depende de **Cómo**, como arriba. Sus conjuntos de propiedades y de cantidades, su tipo y sus materiales llegan con los atributos; el vidrio sigue siendo transparente; un archivo hecho por el conversor de That Open queda donde lo pondría su IFC, y sus caras con huecos (un muro alrededor de su ventana) los conservan.

**Exportar IFC o Fragments…** escribe el modelo como IFC o como Fragments, elegido como una importación: los elementos por disciplina y tipo, las plantas, la fase y solo lo que muestra la vista 3D. El IFC sale en IFC4 o IFC4X3, con las partes de un elemento por capas bajo él o un cuerpo cada una; Fragments desde el punto base del proyecto, el origen interno o el punto topográfico. Un archivo Fragments lleva lo que muestra un visor IFC: el tipo de cada elemento, sus cantidades, tus parámetros, sus materiales y las coordenadas del mapa, como los escribe el conversor de That Open.

![Exportar IFC o Fragments](shots/export-exchange.webp)

## JG Structures (cálculo estructural)

**Exportar JG Structures (JSON)…** escribe el modelo analítico del proyecto en el archivo de intercambio de JG Structures, un programa de cálculo estructural: un archivo JSON según su contrato (versión 1.0.0). Lapin deriva el modelo de lo que dibujaste. Pilares, vigas y barras de acero pasan a ser barras sobre sus ejes; los muros portantes (Pset_WallCommon.LoadBearing en el muro o en su tipo) y los forjados pasan a ser láminas en el centro de su capa resistente, sin acabados. Comparten sus nudos: una viga que sostiene un forjado queda en el plano medio del forjado, los extremos de pilares y muros llegan a la viga o al forjado de su cabeza y de su pie, las esquinas de un forjado pasan a los pilares y muros de debajo, y el forjado se divide a lo largo de las vigas que lo cruzan. Los pies de pilares y muros sobre una cimentación quedan empotrados; la base de un pilar de acero es articulada o empotrada según su unión.

Elige las plantas, los tipos de elementos y las unidades: los centímetros y kilogramos-fuerza del contrato, o SI (metros y kilonewtons). Los materiales toman sus valores de sus datos (Gestionar → Materiales, Pset_MaterialMechanical en pascales) o de la clase resistente de su nombre, C25/30 o S355, según los Eurocódigos; el área, las inercias, los módulos y la constante de torsión de una sección se calculan de su geometría. Lo que Lapin no sabe queda vacío (null) en el archivo, nunca cero. Antes de exportar, el informe cuenta lo que entra, comprueba el archivo con las reglas del contrato y lista lo que aún falta al cálculo: un material sin clase, un pilar que no se apoya en nada.

Donde los elementos no se encuentran en sus ejes (una correa sobre un dintel, una correa lateral en un pilar, un arriostramiento en su cartela, los pares sobre la solera), Lapin pone el nudo en el elemento que los sostiene y conserva la distancia como excentricidad en el extremo de la barra, así que la estructura secundaria de una nave también queda conectada. Los muros conservan sus puertas, ventanas y huecos, y los forjados sus huecos: como los elementos lámina del contrato no tienen huecos, se dividen en piezas alrededor de ellos, y un forjado en L en piezas convexas a lo largo de las vigas que lo soportan. Un muro bajo una cubierta sube hasta ella, y la cubierta también se exporta: su losa como láminas, o los pares, correas y soleras de una cubierta de madera como barras de su clase resistente (C24 según EN 338). Los angulares y demás secciones no simétricas se dan según sus ejes principales. **Apoyos sobre cimentaciones** elige cómo se sujetan los pies de pilares y muros sobre una cimentación: empotrados (por defecto) o articulados. La base de un pilar de acero conserva su propia unión.

![Exportar JG Structures](shots/export-jg.webp)

## Optimizar un archivo IFC

**Optimizar IFC…** (en Archivo, o en la pantalla de inicio: no hace falta abrir ningún proyecto) hace una copia más ligera de uno o varios archivos IFC sin cambiar lo que dicen. La copia conserva cada GlobalId, nombre, conjunto de propiedades, cantidad, tipo, material y relación exactamente como los escribió el archivo, y también su cabecera; no se añade nada de Lapin. Solo cambia la geometría: las mallas (conjuntos de caras, B-reps facetados de caras planas) se limpian (vértices duplicados soldados, triángulos degenerados y repetidos y coordenadas sin uso eliminados) y se simplifican, nunca más lejos del original que la **Precisión** (Rápido 5 mm, Equilibrado 1 mm, Preciso 0,1 mm, o la suya), medida en ambos sentidos; los cuerpos idénticos se escriben una vez y los comparten sus productos; y lo que nada referencia (puntos, estilos sin uso, representaciones vacías) desaparece. Las extrusiones, las operaciones booleanas y los demás cuerpos exactos quedan como están. Redondear las coordenadas, escribir los B-reps como conjuntos de caras (solo IFC4 e IFC4X3) y un `.ifczip` comprimido están ahí cuando los quiera.

![Optimizar IFC](shots/optimize-ifc.webp)

La copia se escribe junto al original, o en una carpeta que elija, con el nombre del original y una **Terminación** (`.optimized` al principio): `casa.ifc` da `casa.optimized.ifc`. El original nunca se escribe, y si la copia ya existe Lapin pregunta antes de sustituirla. Para cada archivo, el informe da el tamaño, las entidades y los triángulos antes y después, los cuerpos compartidos, los huérfanos eliminados, la mayor desviación medida y la duración; **Abrir carpeta** muestra la copia y **Abrir en Lapin** la abre en otra ventana. En el navegador la copia se descarga. Un archivo ya traído al proyecto conserva su camino de vuelta: **Optimizar el archivo de origen…** en el grupo de un IFC importado (en sus propiedades) o en un vínculo (Gestionar → Vínculos) hace lo mismo con el archivo del que vino, mientras siga en el disco.

![El informe](shots/optimize-ifc-report.webp)

## DXF y DWG

**Importar DXF…** trae un dibujo CAD a la vista que tiene delante, con sus capas, sombreados, textos y cotas. Entra como dibujo que puede editar y al que se puede forzar el cursor. Para usar un DWG, guárdelo primero como DXF desde su programa CAD (AutoCAD, BricsCAD o DraftSight: **Save As → DXF**). Un PDF o una imagen entra para calcar con **Archivo → Importar PDF o imagen…** o **Dibujo → PDF o imagen**: las líneas de un PDF vectorial como dibujo a la escala a la que se dibujó, sus imágenes y cualquier PNG o JPEG como imágenes translúcidas (ver [Dibujo](drafting.md)). En una planta, **Colocado: por las coordenadas del mapa** sitúa un dibujo en coordenadas reales (un plano catastral en Stereo 70) donde lo pone el mapa (consulte [El emplazamiento y sus datos](site.md)).

El dibujo está también en la **vista 3D**, sobre el plano de la vista a la que llegó: en una planta al nivel de su planta del edificio, en una sección o un alzado sobre el plano de esa vista, en los colores de sus capas, sus sombreados tenues, como líneas finas que nunca ocultan el modelo. Una capa apagada en **Dibujo → Capas** sigue apagada en 3D. **Vista → DXF en 3D** (también en **Vista → Configuración de vista** de una vista 3D) muestra u oculta todas las importaciones; una importación se conmuta con **En 3D** en las propiedades de cualquiera de sus formas, un DXF vinculado con **3D** en **Gestionar → Vínculos**. Al dibujar en 3D en ese nivel, con las herramientas de Dibujo o el Boceto de Studio, se hace snap a sus líneas, así que puedes calcarlo en 3D como en la planta. Las líneas a las que el archivo dio cotas quedan en el espacio a esas cotas, como antes.

![Un DXF de emplazamiento en la vista 3D, al nivel de la planta baja](shots/dxf-3d.webp)

Un DXF importado forma parte del proyecto: **File → Save** lo guarda en el archivo `.lapin` con sus capas, sombreados, textos y cualquier grupo que hayas hecho con él, y está ahí cuando el proyecto se vuelve a abrir, en la planta del nivel. La vista en planta en la que entró es desde entonces la planta del nivel, la que muestra el navegador del proyecto, y lo que estaba dibujado antes en el nivel pasa a ella. Lapin pregunta si quieres guardar antes de cerrarse con cambios sin guardar. Para mantener el dibujo como un archivo propio, marca **As a link** en el diálogo de importación: queda vinculado, de solo lectura, y **Manage → Links** lo recarga cuando el archivo cambia.

## Vínculos

**Vincular IFC…** mantiene otro archivo vinculado en lugar de importarlo. Es de solo lectura y se vuelve a cargar cuando cambia. Esto también funciona con un DXF. **Gestionar → Vínculos** muestra, recarga, convierte o elimina los vínculos. Un DXF vinculado está en 3D como uno importado; su casilla **3D** en **Gestionar → Vínculos** lo quita. **Visibilidad / Gráficos** (<kbd>VV</kbd>) oculta un vínculo, lo muestra en semitono o le da sus propias categorías en una sola vista (*Secciones, alzados y 3D*, Visibilidad / Gráficos).

Un IFC vinculado conserva lo que su archivo dice de los materiales de sus elementos. Muros, forjados y cubiertas con conjuntos de capas se dibujan en sus capas incluso como referencias, de modo que un ingeniero oculta los acabados del vínculo de arquitectura (**Muros ▸ Acabados**, **Forjados ▸ Acabados**) y un estilo de vista estructural dibuja solo sus núcleos; las barras de un modelo estructural vinculado están en la categoría **Armaduras**, que toda vista 3D deja fuera hasta que se muestra. Un vínculo hecho con una versión anterior recibe las capas de sus muros al recargarlo (las de los forjados enseguida).

## Nubes de puntos

**File → Link point cloud…** vincula un escaneo láser o una nube de fotogrametría, solo en formatos abiertos: **LAS** (1.0–1.4, todos los formatos de punto), **LAZ** (LAS comprimido), **E57**, **PLY** (ASCII y binario) y puntos como texto, **XYZ**, **PTS** y **CSV**. Los formatos propietarios (RCP y RCS de Autodesk ReCap, FLS de FARO, LGS de Leica) no se leen: exporta primero el escaneo desde su programa como E57 o LAS.

Lapin pregunta dónde está la nube y en qué unidad:

- **por las coordenadas de mapa del proyecto**: la x y la y del archivo son este y norte, colocadas mediante el punto topográfico y el ángulo al norte de **Manage → Location**, sus alturas sobre la cota del punto topográfico. Lapin lo sugiere cuando el archivo nombra un sistema de coordenadas o sus coordenadas están lejos de cero y el proyecto tiene una ubicación;
- **origen con origen**: el origen del archivo sobre el del proyecto;
- **centrada en el origen del proyecto**: su centro en el origen, su punto más bajo en ±0.00;
- **su unidad**: como dice el archivo (LAS y E57 la dicen), si no, metros; se ofrecen centímetros, milímetros, pies, pies topográficos de EE. UU. y pulgadas. Cuando la nube resulta poco probable para un edificio o un terreno (menos de 20 cm o más de 50 km), o el archivo dice otra unidad, Lapin avisa.

La nube se queda en su archivo. No se copia en el proyecto: el proyecto guarda solo el vínculo (la ruta del archivo, dónde está la nube, cómo se dibuja). Lapin la indexa una vez, en segundo plano, en un octree de teselas en su propia carpeta de este ordenador (no junto al proyecto), y **Manage → Links** muestra cuánto falta. Después la vista 3D trae las teselas que la cámara necesita, primero las cercanas y más finas a medida que te acercas, dentro de un presupuesto de puntos fijado por **View → Display quality** (1,5, 3 o 6 millones de puntos dibujados a la vez), así que un escaneo de cien millones de puntos gira con la misma fluidez que uno pequeño.

![Un escaneo de la casa vinculado como nube de puntos, en la vista 3D](shots/point-cloud-3d.webp)

En **Manage → Links** una nube tiene su propia fila: sus puntos, cuánto lleva su índice, **Shown**, **Halftone**, **3D**, qué colorea sus puntos (**RGB**, **Intensity**, **Elevation** o **Classification**, según las clases ASPRS: terreno, vegetación, edificio, agua…) y el **Size** de un punto en píxeles. El botón del alfiler la vuelve a colocar (por el mapa, origen con origen o centrada, su unidad, movida en x, y y z, girada); el botón de recarga vuelve a leer su archivo y la indexa de nuevo solo si cambió. **Unlink** quita el vínculo; el índice queda en la carpeta de Lapin y sirve la nube otra vez si se vuelve a vincular.

![Manage → Links con una nube de puntos](shots/point-cloud-links.webp)

Una nube no se exporta con el modelo: IFC y Fragments llevan el modelo, no el escaneo, y **Export 3D…** toma una muestra como puntos en GLB y glTF solo si se pide. Plantas, secciones y alzados también la dibujan (*Secciones, alzados y 3D*, Nubes de puntos en las vistas). En el navegador se puede vincular una nube de hasta 160 MB; se indexa en la página y hay que vincularla de nuevo tras recargar la página. Las nubes mayores necesitan la aplicación de escritorio.

## Modelos 3D

**Exportar 3D (glTF, USD, DAE)…** escribe el modelo como superficies para renderizado, visualización y realidad aumentada (RA):

| Formato | Para |
|---|---|
| GLB, glTF | Blender, three.js, visores web. Se incluyen las imágenes propias de sus materiales. |
| USDZ | AR Quick Look de Apple, Reality Composer, Omniverse. Imágenes incluidas. |
| USDA | OpenUSD como texto, un solo archivo. |
| DAE | COLLADA, para SketchUp y herramientas más antiguas. |
| OBJ + MTL | Un .zip para Twinmotion, Lumion, 3ds Max y cualquier programa de render. Con las imágenes. |

**Importar malla (OBJ, STL, glTF)…** trae una malla de otro programa 3D (OBJ, STL, glTF o GLB) como referencias en la planta activa. glTF indica su unidad y qué eje va hacia arriba; para OBJ y STL, Lapin lo pregunta. Un mueble o una decoración traídos así se colocan como cualquier elemento: **Desplazar**, **Copiar**, **Girar**, **Simetría** y **Matriz** en Modificar, **Desplazar 3D** en Studio para su altura. Sus copias comparten sus triángulos, así que una fila de sillas pesa lo que una. **Convertir en sólido Studio** en Studio hace editable la forma de una referencia (consulte [Studio](studio.md)).

![Seis sillas de un archivo OBJ alrededor de una mesa, hechas con Modificar → Matriz](shots/reference-array.webp)

![Exportar 3D](shots/export-3d.webp)

Usted elige qué exportar, qué fase y el origen. **Qué** es **todo**, o el modelo **tal como lo muestra la vista**: la vista 3D que tiene delante, sin sus elementos y categorías ocultos ni las plantas que no muestra, con las capas que muestra (sin acabados, o solo los núcleos). Cuando esa vista está cortada (el corte por encima de las plantas mostradas, una sección mostrada en 3D, la caja de sección), **Corte** corta también la exportación ahí y cierra cada corte con sus materiales, para que el archivo se vea como la pantalla. Una vista 3D que deja algo fuera se exporta tal como lo muestra, salvo que elija otra cosa. Cada elemento conserva su clase IFC, su tipo y su GlobalId. Para intercambiar el modelo en sí y no sus superficies, use **Exportar IFC…**.
