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

## Alzados

**+** junto a **Alzados** crea de una vez los cuatro alzados del edificio.

![El alzado sur](shots/elevation-view.webp)

## Vistas 3D

<!-- video section-3d -->

Haga doble clic en **3D** para abrir la vista 3D. Arrastre con el botón izquierdo para orbitar alrededor del modelo. El plano clave de arriba a la derecha muestra desde dónde está mirando. **Planta**, **Persp** y **Ajustar** cambian la cámara. Para conservar una cámara, duplique la vista 3D.

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

## Qué muestra una vista

La ficha **Vista** configura qué muestra la vista que está delante y cómo se ve.

![La ficha Vista](shots/ribbon-view.webp)

- **Configuración de vista.** La disciplina y el estilo de la vista, su rango (la altura y la profundidad del corte) y sus gráficos.
- **Estilos de vista.** Arquitectónico, Estructural (solo los núcleos), Instalaciones o los suyos propios. Cada estilo elige las capas dibujadas (todas, sin acabados o el núcleo) y cómo se unen los materiales.
- **Símbolos 2D de IFC.** Los elementos importados o vinculados desde un archivo IFC que traen su propio dibujo en planta (una huella, el barrido de una puerta, el símbolo de un aparato, con los colores, rellenos y textos del archivo) lo dibujan en las plantas en lugar de su corte. Desmárquelo en Configuración de vista (o en un estilo de vista) para dibujar en su lugar su corte 3D.
- **Visibilidad** (<kbd>VV</kbd>) y **Gráficos** (<kbd>VG</kbd>). Muestran u ocultan categorías y configuran sus plumas, tipos de línea y rellenos.
- **Dividir** y **Ventana.** Muestran dos vistas una al lado de la otra, o abren una vista en una ventana separada.
