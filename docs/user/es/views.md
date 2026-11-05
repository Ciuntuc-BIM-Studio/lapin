---
title: Secciones, alzados y 3D
order: 70
summary: Cree secciones y alzados, abra vistas 3D y configure qué muestra cada vista y cómo.
---

Todas las vistas están en el explorador del proyecto, clasificadas por su disciplina: Arquitectura, Estructura o Instalaciones. Haga doble clic en una vista para abrirla en una pestaña.

![El explorador del proyecto con una sección y un alzado](shots/browser.webp)

## Secciones

Elija **Vista → Sección** (<kbd>SE</kbd>), o haga clic en **+** junto a **Secciones** en el explorador. Después dibuje la línea de corte en una planta con dos clics, y haga un tercer clic en el lado hacia el que mira la sección. La sección se dibuja a partir del modelo y se mantiene actualizada a medida que usted lo cambia.

![La sección A-A a través de la casa](shots/section-view.webp)

Seleccione la sección en el explorador para configurar su escala, su profundidad, su disciplina y su estilo en el panel de propiedades.

### Dónde se marca una sección

La línea de una sección se dibuja en todas las plantas, y la sección se marca en todas las demás secciones y alzados que atraviesa su plano: una línea fina discontinua donde el plano corta la vista, con el nombre de la sección y una flecha que mira hacia donde mira la sección, arriba, del mismo tamaño en papel que en una planta. Una sección nunca se marca en sí misma. Un clic en un marcador selecciona la sección; un doble clic la abre.

Cada sección está en una **capa de marcadores** (**Sections** al principio; **Capa del marcador** en las propiedades de la sección la cambia), y cada vista elige qué marca. **Vista → Secciones…** lista las secciones del proyecto por capa para la vista en primer plano, con una casilla por capa y por sección solo para esta vista, la capa de cada sección (una existente o **Nueva capa…**) y en cuántas vistas está oculta cada una. Una lámina marca lo que marcan sus vistas.

Una sección cortada a través de una viga o un pilar (**Sección aquí**, **Sección transversal** en la pestaña Armado 2D) también tiene pinzamientos en planta: arrastre cualquiera de los extremos a lo largo de la línea para dar a ese lado más o menos margen, y arrastre el centro a lo largo del elemento para mover la estación en la que corta. La ventana de la sección la sigue.

![Vista → Secciones… para el alzado sur](shots/sections-dialog.webp)

## Alzados

**+** junto a **Alzados** crea de una vez los cuatro alzados del edificio.

![El alzado sur](shots/elevation-view.webp)

### Sombras del sol

Un alzado, una sección o un plano de situación pueden dibujar las sombras que el sol proyecta sobre lo que ven, rellenas bajo sus líneas, de modo que se imprimen en planos, PDF y DXF. Se eligen en las propiedades de la vista, en **Sombras del sol**:

- **sol propio**: un sol de la vista. Por defecto la convención de dibujo: el sol sobre tu hombro izquierdo, con los rayos por la diagonal de un cubo (desde 45°, a 35,26° de altura), así una sombra es tan profunda como sobresale lo que la proyecta. **Sol desde** y **Altura del sol** lo giran; **Convención de dibujo** lo devuelve.
- **sol del proyecto**: el sol del lugar del proyecto (Gestionar → Ubicación) en un día y una hora de la vista. **Como en Sol y sombras** toma el día y la hora de Vista → Sol y sombras.

Una sección proyecta y recibe sombras solo de lo que está más allá de su corte. Una planta de nivel no dibuja sombras: sus muros, cortados a la altura de la planta, las proyectarían demasiado cortas.

![El alzado sur con la sombra de la cubierta](shots/elevation-shadows.webp)

![Las sombras del sol de una vista](shots/view-shadows.webp)

### Tramas de superficie

Un alzado o una sección trama cada cara que ve con la trama **Elevation** de su material (**Gestionar → Materiales**, pestaña **2D graphics**): su patrón, escala, ángulo, plumilla y relleno. Solo se trama la parte de una cara que ningún elemento más cercano oculta, así que el revoco o el ladrillo de una fachada se ve alrededor de sus puertas y ventanas, y una sección trama lo que ve más allá de su corte, no detrás de los elementos que corta. Un muro por capas muestra el material de su capa exterior. La trama se dibuja bajo las líneas, así que se imprime en los planos, en PDF y en DXF (capas SURFACE-HATCH y SURFACE-FILL).

Con **Alignment** *view* el patrón sigue de una cara a la siguiente; con *layer* se apoya en la propia cara, a lo largo de su horizontal desde su esquina inferior, y una cara vista en escorzo lo muestra acortado. Un material que sigue en un mismo plano por varios elementos se trama como uno solo, salvo que **Join materials** del estilo de vista esté desactivado.

Un material no tiene trama de alzado hasta que se la das (**none (default)**). **Surface hatches** del estilo de vista (**Vista → View settings**, **Gestionar → View styles**) las desactiva para vistas enteras: activado en Architectural, desactivado en Structural, MEP y Coordination. Las plantas conservan sus propias tramas de corte y de planta.

![El alzado sur con su revoco tramado](shots/elevation-surface-hatch.webp)

![Las tramas de superficie en los ajustes de la vista](shots/view-surface-hatch.webp)

## Vistas 3D

<!-- video section-3d -->

Haga doble clic en **3D** para abrir la vista 3D. Arrastre con el botón izquierdo para orbitar alrededor del modelo. El plano clave de arriba a la derecha muestra desde dónde está mirando. **Planta**, **Persp** y **Ajustar** cambian la cámara. Para conservar una cámara, duplique la vista 3D.

La rueda del ratón hace zoom hacia el punto del modelo bajo el cursor, que se queda donde está en la pantalla; el zoom se detiene justo antes de ese punto y nunca lo atraviesa. Arrastrar con el botón izquierdo gira la vista alrededor del centro de los elementos seleccionados, que se queda donde está en la pantalla, para verlos desde todos los lados; sin nada seleccionado gira alrededor del punto del modelo bajo el cursor donde empieza el arrastre, y alrededor del centro de la vista si bajo el cursor no hay nada. **Vista → Zoom al cursor** y **Vista → Orbitar alrededor de la selección** desactivan cada uno (la rueda hace entonces zoom hacia el centro de la vista, y la vista gira alrededor de su centro), igual que las casillas de las propiedades de una vista 3D. La elección se guarda en este ordenador.

### Una sección en 3D

Haga clic con el botón derecho en una sección del explorador y elija **Mostrar en 3D**. La vista 3D corta el modelo por la línea de la sección. Conserva el lado hacia el que mira la sección, hasta la profundidad de la sección si la tiene. Cada elemento cortado se cierra donde se corta, capa por capa, cada capa en su propio material: un muro muestra su núcleo de ladrillo y su enlucido, tan macizo como es el muro. **Sombreados**, en el indicador de la parte inferior de la vista 3D, coloca en su lugar el dibujo de la sección sobre el corte, con los rellenos y sombreados de corte que imprime una lámina. Las cotas, las marcas de nivel, las etiquetas y los textos de la sección se sitúan en el mismo plano, con el mismo tamaño y en el mismo lugar que en la sección. Las líneas conservan sus grosores de pluma y sus patrones de trazo completos. Las dibujadas al lado del edificio quedan en el aire junto al corte. **Anotaciones**, en el indicador de la parte inferior de la vista 3D, las oculta o las vuelve a mostrar. **Mostrar en 3D** de un alzado gira la cámara para mirar en la dirección en que mira el alzado.

![El menú contextual de la sección en el explorador](shots/section-3d-menu.webp)

![La sección B-B vista en 3D](shots/section-3d.webp)

Para volver a ver todo el modelo, haga clic en la **×** del indicador **Sección** de la parte inferior de la vista 3D, o elija de nuevo **Mostrar en 3D**.

![La sección B-B con Sombreados activado: el corte tal como lo imprime la lámina](shots/section-3d-hatches.webp)

### La caja de sección

**Vista → Caja de sección** (método abreviado BX) recorta la vista 3D a una caja. La caja empieza rodeando todo el modelo. Cada cara tiene una flecha en su centro: arrástrela para mover esa cara. Cada elemento que corta la caja se cierra en esa cara, capa por capa, en sus materiales, y las caras se encuentran limpiamente en las aristas de la caja. **Cortar en planta** sitúa la parte superior de la caja a la altura del corte de planta del nivel activo (su rango de vista), de modo que el nivel se lee en 3D tal como lo corta su planta. **Ajustar** devuelve la caja alrededor de todo el modelo. Para girar la caja, arrastre el tirador redondo situado más allá de su esquina superior (grado a grado; mantenga pulsada Mayús para pasos de 15°), o seleccione un muro y haga clic en **Alinear**: la caja gira según la dirección del muro. **Sombreados** dibuja los cortes de la caja como lo hace una lámina, cada material con su relleno y su sombreado de corte, en lugar de los materiales. Una vista 3D guardada conserva su caja de sección: guarde la vista con la caja activada, o abra una vista guardada y configure su caja, y la caja estará ahí cada vez que se abra la vista. La **×** del indicador **Caja de sección** desactiva la caja.

![La caja de sección cortada en la planta baja, con su cara este arrastrada hacia dentro](shots/section-box.webp)

![La misma caja con Sombreados activado: los cortes tal como los dibuja una lámina](shots/section-box-hatches.webp)

### Una planta cada vez

Para mirar dentro del edificio, muestre solo algunas de sus plantas. Haga clic con el botón derecho en un nivel del explorador del proyecto y elija **Mostrar solo este nivel en 3D**, o **Mostrar este nivel y los inferiores en 3D** para ver el edificio cortado en ese nivel. El nivel pasa a ser el activo, y la vista 3D sigue al nivel activo: haga clic en otro nivel del explorador para subir o bajar una planta.

![El menú contextual del nivel en el explorador](shots/storeys-3d-menu.webp)

![La planta baja sola en 3D](shots/storeys-3d.webp)

**Vista → Plantas mostradas**, o las propiedades de la vista 3D, configuran el resto:

- **Mostrar**: todas las plantas, las plantas marcadas, solo la planta activa, o la planta activa y las inferiores.
- **En ninguna planta**: lo que no está en ninguna planta (el emplazamiento, el terreno, los archivos vinculados) se muestra o se oculta.
- **Cortar por encima de su nivel**: el modelo se corta a esa distancia por encima de la planta más alta mostrada, 1,20 m al principio, como lo corta una planta. Cada elemento cortado allí se cierra en el corte, capa por capa, en sus materiales, igual que una sección mostrada en 3D.

![La planta baja cortada a 1,20 m: sus muros cerrados, capa por capa](shots/storeys-3d-cut.webp)

Las plantas ocultas no se dibujan, y no puede seleccionarlas, ajustar la vista a ellas ni caminar sobre ellas. Lo que un corte elimina (el corte por encima de las plantas mostradas, una sección mostrada en 3D, la caja de sección) tampoco está ahí para un recorrido: usted pasa junto a la parte superior de un muro que el corte ha eliminado, y las tapas del corte cierran lo que este abre. **Archivo → Exportar 3D…** toma la vista 3D tal como la muestra (consulte [Importar y exportar](import-export.md)). Una vista 3D guardada conserva su propia elección. El indicador **Plantas** de la parte inferior de la vista 3D indica lo que se muestra; su **×** vuelve a mostrar todas las plantas.

### Sin acabados, o solo los núcleos

Una vista 3D puede mostrar solo la estructura del edificio, como lo hace una planta estructural. En las propiedades de la vista 3D (sin nada seleccionado), o en **Vista → Configuración de vista** con la vista 3D delante, **Capas** muestra:

- **todas las capas**: los muros, forjados y zapatas tal como se construyen;
- **sin acabados**: sin las capas cuyo material es un acabado (enlucido, revoco, pintura, baldosas, tableros, revestimientos de suelo, o un material cuya categoría es Acabado en Gestionar → Materiales); el aislamiento se mantiene;
- **solo el núcleo**: solo la capa marcada como núcleo (el ladrillo, la losa de hormigón).

![Una casa en 3D con solo el núcleo: los muros son su ladrillo, los forjados su losa](shots/layers-3d-core.webp)

Cada capa conservada sigue siendo un sólido cerrado en su propio material, de modo que un corte o la caja de sección la cierran como antes. Las puertas y ventanas permanecen en sus huecos; un derrame revestido con un acabado va con los acabados del muro. Una vista 3D toma sus capas de su estilo, como una planta: configure su **Disciplina** como Estructura y mostrará solo los núcleos; configure **Capas** para elegir otra cosa, y el botón de al lado vuelve a las del estilo. El indicador **Capas mostradas** de la parte inferior de la vista 3D indica lo que se omite; su **×** vuelve a mostrar todas las capas. Las capas ocultas no se seleccionan ni detienen un recorrido, y **Archivo → Exportar 3D…** tal como la vista lo muestra exporta las mismas capas.

### Entrar caminando en una habitación

En una planta, haga clic con el botón derecho dentro de una habitación y elija **Recorrer en 3D**. La vista 3D le sitúa dentro de la habitación, 1,60 m por encima de su suelo, mirando a lo largo de su lado más largo.

![El menú contextual de una habitación en la planta](shots/room-walk-menu.webp)

![De pie en el salón](shots/room-walk.webp)

- Arrastre con el botón izquierdo para mirar alrededor.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> o las teclas de flecha le hacen caminar. Mantenga pulsada <kbd>Shift</kbd> para correr. <kbd>Space</kbd> salta.
- La rueda del ratón da pasos hacia delante y hacia atrás.

Usted camina como lo haría una persona. Los muros, los pilares, el mobiliario y las ventanas cerradas le detienen, y usted se desliza a lo largo de ellos. Se apoya en los forjados, sube y baja escaleras y rampas, y cae si sale caminando por un borde. Las puertas están abiertas para usted: atraviese sus huecos. Solo le detiene lo que dibuja la vista 3D: los elementos ocultos, las plantas que no muestra y lo que eliminan sus cortes no se interponen en su camino.

![Caminando hacia la puerta del dormitorio](shots/room-walk-door.webp)

![Subiendo la escalera](shots/room-walk-stair.webp)

**Volar**, en el indicador del recorrido, o <kbd>F</kbd>, le permite volar en su lugar: sin gravedad, y atraviesa muros y forjados. <kbd>E</kbd> y <kbd>Q</kbd> suben y bajan. Pulse <kbd>F</kbd> de nuevo para caminar; caerá sobre el suelo que tenga debajo.

<kbd>Esc</kbd> o **Salir del recorrido** recupera la cámara que tenía antes.

### Realista, con el sol

Vista → **Calidad de visualización** fija cómo se dibuja la vista 3D, en este ordenador:

- **Dibujo**: la paleta del plano y una luz uniforme, como Lapin dibujó siempre. Rápido en cualquier ordenador.
- **Medio**: las sombras del sol, sombra en las esquinas donde se juntan las caras y luz del cielo.
- **Realista**: el cielo detrás del modelo y un terreno debajo (donde el proyecto no tiene terreno), y vidrio que deja pasar la luz. Para presentar, con una buena tarjeta gráfica.

![Una casa en la vista realista](shots/view-realistic.webp)

El sol es el del proyecto: dónde está para el lugar del proyecto (Gestionar → **Ubicación**) un día a una hora. Vista → **Sol y sombras** fija el día (el equinoccio y los solsticios están a un clic) y la hora, reproduce el día y enciende el sol también en la vista de dibujo. Sin sistema de coordenadas ni punto de reconocimiento, el lugar es Bucarest. El huso horario sigue la longitud del lugar, con el horario de verano europeo en Europa.

![Sol y sombras](shots/sun-dialog.webp)

La vista realista toma algunas de sus cifras (la sombra en las esquinas, la calidad de las sombras, el color del sol) de [BimGo](https://github.com/aussieBIMguru/BimGo), de Aussie BIM Guru (MIT-0).

## Qué muestra una vista

La ficha **Vista** configura qué muestra la vista que está delante y cómo se ve.

**Grid** (<kbd>F7</kbd>) oculta o muestra la rejilla de fondo de la planta, y **Líneas finas** (<kbd>TL</kbd>) dibuja todas las líneas de un píxel, para leer un dibujo denso. Las dos son solo para la pantalla: los planos, el PDF y el DXF conservan sus plumas, y el forzado a la rejilla sigue. La rejilla del suelo de la vista 3D sigue el mismo interruptor.

![La ficha Vista](shots/ribbon-view.webp)

- **Configuración de vista.** La disciplina y el estilo de la vista, su rango (la altura y la profundidad del corte) y sus gráficos.
- **Estilos de vista.** Arquitectónico, Estructural (solo los núcleos), Instalaciones o los suyos propios. Cada estilo elige las capas dibujadas (todas, sin acabados o el núcleo) y cómo se unen los materiales.
- **Símbolos 2D de IFC.** Los elementos importados o vinculados desde un archivo IFC que traen su propio dibujo en planta (una huella, el barrido de una puerta, el símbolo de un aparato, con los colores, rellenos y textos del archivo) lo dibujan en las plantas en lugar de su corte. Desmárquelo en Configuración de vista (o en un estilo de vista) para dibujar en su lugar su corte 3D.
- **Visibilidad** (<kbd>VV</kbd>) y **Gráficos** (<kbd>VG</kbd>). Muestran u ocultan categorías y configuran sus plumas, tipos de línea y rellenos.
- **Dividir** y **Ventana.** Muestran dos vistas una al lado de la otra, o abren una vista en una ventana separada.
- **Calidad de visualización.** Cómo se dibuja la vista 3D (**Dibujo**, **Medio** o **Realista**, guardado en este ordenador: ver *Realista, con el sol*) y cómo dibuja la vista 3D las piezas de muchos triángulos (una planta importada, mobiliario, tuberías) cuando están lejos. **Equilibrada** dibuja una pieza lejana más basta solo donde la diferencia queda bajo un píxel. **Rápida** la deja desviarse hasta 3 píxeles, para modelos grandes y ordenadores más lentos. **Precisa** dibuja cada pieza tal como es. El ajuste pertenece al proyecto. El modelo, sus dibujos, exportaciones y cantidades nunca se simplifican: una curva está hecha de tramos rectos que se desvían de ella como mucho un milímetro.
- **Sol y sombras.** El sol del proyecto un día a una hora, iluminando la vista 3D con sus sombras (ver *Realista, con el sol*).

![Calidad de visualización](shots/display-quality.webp)
