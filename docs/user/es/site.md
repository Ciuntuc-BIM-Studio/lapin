---
title: El emplazamiento y sus datos
order: 52
summary: Traiga el levantamiento, la parcela, los edificios vecinos y una ortofoto en coordenadas reales, desde DXF, CSV, GeoJSON, CityJSON y una imagen georreferenciada.
---

La ficha **Emplazamiento** contiene el terreno sobre el que se levanta el edificio: el terreno, la parcela, los edificios vecinos y una foto aérea, cada uno donde realmente está. El grupo **Importar** los trae desde los archivos que entregan los topógrafos y el catastro.

![La ficha Emplazamiento](shots/ribbon-site.webp)

## Coordenadas reales

Un levantamiento llega en coordenadas de mapa: coordenadas este y norte, a cientos de kilómetros del cero. Lapin mantiene el modelo cerca de su origen y lo vincula al mapa mediante el **punto de reconocimiento**. La primera importación en un proyecto lo hace por usted: el punto de reconocimiento se coloca en el origen de la planta, sobre las primeras coordenadas del archivo redondeadas al metro, y todas las importaciones posteriores caen en el mismo lugar.

**Gestionar → Ubicación** muestra el **Sistema de coordenadas** en el que están las coordenadas este y norte, y el punto base y el punto de reconocimiento. Lapin conoce Stereo 70 (EPSG:3844, la red nacional de Rumanía), los husos UTM 34 y 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) y la longitud y latitud (WGS 84, EPSG:4326). Un archivo en otro de estos sistemas se convierte al entrar: un GeoJSON en longitud y latitud cae en la planta Stereo 70 donde le corresponde. La exportación IFC escribe el sistema y el punto de reconocimiento, para que otros programas también sitúen el modelo en el mapa.

![Gestionar → Ubicación: el sistema de coordenadas y el punto de reconocimiento](shots/site-location.webp)

## Importar

- **Terreno a partir de puntos** lee un archivo CSV o de texto de un levantamiento: x y z, o número norte este cota, un punto por línea.
- **Puntos desde DXF** lee un plano topográfico: entidades POINT con sus cotas, bloques insertados a una cota o que la llevan como atributo, cotas escritas junto a los puntos («245.37» junto a su cruz) y curvas de nivel a su cota. Cuando el dibujo tiene puntos en varias capas, Lapin las enumera con cuántos puntos contiene cada una, y usted elige cuáles tomar. Los puntos forman un terreno.
- **GeoJSON** trae parcelas, líneas y puntos. Un archivo con un polígono da la **parcela**; uno con varios da áreas. La parcela también delimita la parcela del terreno, con su superficie. Los archivos del catastro en Stereo 70 lo indican en el propio archivo, y Lapin lo lee; un archivo que no indica nada está en longitud y latitud, como exige la norma GeoJSON.
- **CityJSON** trae los edificios vecinos con sus superficies, en su lugar y a su altura. Son referencias de solo lectura; proyectarán sombras en el estudio solar.
- **Ortofoto** coloca una foto aérea bajo el emplazamiento. Elija la imagen (JPEG o PNG, hasta 8 MB) junto con su archivo de georreferencia (`.jgw`, `.pgw` o `.wld`): el archivo de georreferencia indica dónde se sitúa la imagen. La foto se dibuja bajo la planta y recubre el terreno en 3D.

![El plano de situación: la ortofoto, las curvas de nivel del terreno levantado, la parcela y la huella del vecino](shots/site-plan.webp)

![El mismo emplazamiento en 3D: la foto sobre el suelo, el vecino a su altura](shots/site-3d.webp)

Todo lo importado se guarda en el archivo del proyecto. Un elemento toma su nombre del archivo (el número catastral de una parcela, cuando el archivo lo tiene) y conserva consigo las demás propiedades del archivo; selecciónelo para ver su superficie o su longitud.

## Carreteras, curvas y trazados CAD

Una **carretera** o un **camino** sigue su eje. En **Corredor…** (CR), la pestaña **Curvas** da a cada vértice del eje un radio con clotoide de entrada y de salida, como un proyectista de carreteras traza una curva; un vértice dejado en 0 sigue siendo una esquina, y las curvas que no caben entre sus vecinas se rechazan, con el motivo. El estacionamiento, el perfil longitudinal y las secciones transversales siguen el eje curvo, y IFC 4.3 lo escribe como alineación.

Los trazados CAD se convierten en elementos del sitio: **Carretera desde líneas CAD** (LR) y **Línea de quiebre desde líneas CAD** (BL) toman una línea o polilínea dibujada o traída de un DXF, siguen todas las líneas de su capa que se unen extremo con extremo y hacen una carretera a lo largo de ella o una línea de quiebre del terreno. Cuando las líneas llevan cotas (las líneas 3D de un DXF), dan el perfil de la carretera o las cotas de la línea de quiebre. Esas líneas conservan sus cotas y pueden mostrarse en 3D (la categoría *trazados*).

## Planos de situación y planos de emplazamiento

El navegador del proyecto tiene un **Emplazamiento** bajo los niveles: los dibujos propios del emplazamiento, que no pertenecen a ninguna planta. Un proyecto nuevo empieza con dos:

- el **Plano de situación** (en rumano *plan de situație*), a 1:500: todo el modelo visto desde arriba, todas las plantas a la vez, cubiertas incluidas, con el terreno, los caminos, la vegetación, la parcela y los vecinos. Lo que queda por completo bajo ±0.00 (la cimentación) se omite, no se corta nada y no se dibuja nada oculto. Las esquinas de la parcela van numeradas;
- el **Plano de emplazamiento** (*plan de încadrare în zonă*), a 1:2000: el mismo dibujo para el emplazamiento en su entorno, sobre la ortofoto, sin la numeración de las esquinas; los edificios del proyecto se rellenan en rojo y la parcela se contornea en rojo, para que el emplazamiento destaque entre sus vecinos.

**+** junto a Emplazamiento crea otro de cualquiera de los dos; los botones de copia duplican uno. Ambos llevan una **flecha del norte** que apunta al norte geográfico (Gestionar → Ubicación: el ángulo con el norte geográfico), una **escala gráfica** y, cuando el proyecto está vinculado al mapa, la **retícula de coordenadas**: cruces donde se encuentran coordenadas este y norte redondas, cada décima parte de la escala (50 m a 1:500, 200 m a 1:2000), con sus valores. Seleccione la vista en el navegador y configúrelos en sus propiedades, en **Configuración de vista**: qué es (plano de situación o plano de emplazamiento), el paso de la retícula (0: ninguna), la flecha, la escala gráfica y **Resaltar el proyecto** (el relleno y el contorno rojos, activado por defecto en un plano de emplazamiento, y también en un plano de situación cuando lo desee).

**El mapa debajo.** La ortofoto se dibuja bajo cada plano de situación y plano de emplazamiento, sea cual sea la planta en la que se importó, y se imprime con ellos en las láminas y en el PDF. Para el plano catastral o el plano del topógrafo, abra el plano de situación y use **Importar DXF…** con **Colocado: por las coordenadas del mapa**. Lapin lo propone por sí mismo cuando las coordenadas del archivo parecen de mapa, por ejemplo Stereo 70. El dibujo cae entonces donde dicen sus coordenadas este y norte, a través del punto de reconocimiento del proyecto y de su ángulo con el norte geográfico. Si el proyecto aún no está vinculado al mapa, el dibujo lo vincula. Marque **Como vínculo** para mantenerlo de solo lectura y recargarlo cuando el topógrafo envíe uno nuevo. Impórtelo una vez en cada dibujo que lo necesite.

![Un plano de emplazamiento: la casa rellena en rojo, la parcela contorneada en rojo, entre sus vecinos](shots/site-location-plan.webp)

![Un plano de situación: la casa vista desde arriba en su parcela, las esquinas numeradas, la retícula del mapa, la flecha del norte y la escala gráfica](shots/site-drawings.webp)

![El Emplazamiento en el navegador del proyecto](shots/site-browser.webp)

La tabla **Coordenadas de la parcela** (Tablas → + → Coordenadas de la parcela) es el inventario de coordenadas de la parcela: cada esquina tal como la numera el plano de situación, su coordenada norte y este en el sistema del proyecto, el lado hasta la esquina siguiente y la superficie. Como cualquier tabla, se coloca en una lámina junto al plano de situación y se exporta como CSV o XLSX.

## Árboles y arbustos

**Planta** (PN) coloca una planta con un clic y **Zona de plantación** (PZ) siembra un contorno con una mezcla de especies a una densidad; selecciónalas y elige sus especies en las propiedades. Las especies vienen de una biblioteca de plantas nativas o tradicionales de Rumanía, por grupos: **frutales** (manzano, peral, ciruelo, cerezo, guindo, albaricoquero, membrillero), **coníferas** (abeto blanco, pícea, pino silvestre, pino negro), **árboles caducifolios** (nogal, tilo, abedul, arce), **arbustos** (avellano, cornejo macho, saúco, rosal silvestre, lila, espino amarillo, grosellero, tuya, boj) y **cubresuelos**. Cada una lleva su nombre latino, su altura y su copa habituales. El cuadro de plantación muestra los nombres latino y rumano junto a cada especie.

En planta cada planta se dibuja vista desde arriba con una línea fina, tan ancha como su copa, solo por su contorno: un nogal o un tilo una copa de lóbulos suaves, un frutal un círculo, un abeto o una pícea una estrella, un pino un anillo dentado, un arbusto una pequeña nube, una planta de seto recortada un círculo, con una crucecita en el tronco de un árbol. Alzados y secciones dibujan cada planta como un solo contorno: la copa de un árbol sobre su tronco, el cono escalonado de una conífera, la cúpula de un arbusto. Unas pocas líneas cada una, así que un plano de emplazamiento con cientos de árboles sigue siendo ligero. En 3D las coníferas son conos, los demás árboles tienen una copa redondeada sobre su tronco y los arbustos una cúpula baja.

![Un pequeño huerto en planta: frutales, un nogal, coníferas y arbustos](shots/planting-symbols.webp)

Pon la **Ambientación en 2D** del estilo de vista en **detallado** para los símbolos de un plano de paisajismo, en negro: un frutal como una copa lobulada con ramas bifurcadas, un nogal o un tilo como una copa festoneada alrededor de un núcleo oscuro, un abeto o una pícea como una estrella de ramas con acículas, un pino como un penacho redondo de acículas, un arbusto como una pequeña nube. Lapin dibuja cada símbolo por sí mismo, así que los árboles vecinos difieren un poco y una zona nunca parece estampada; a 1:1000 y menos (un plano de situación) solo se dibujan los contornos de las copas.

![El mismo huerto dibujado detallado](shots/planting-symbols-detailed.webp)

## Personas y coches

**Ambientación** (EN) coloca una persona o un coche con un clic; elige cuál en la barra de la línea de comandos mientras la herramienta está activa: un **hombre de pie**, una **mujer caminando**, un **niño**, una **persona al teléfono**, un **coche** o un **SUV / furgoneta**. **Fila de ambientación** (ER) coloca una fila a lo largo de una línea que dibujas: coches cada 2,5 m, aparcados uno junto a otro de través, con el frente a la izquierda de la línea, y personas cada 1,2 m, caminando a lo largo de ella. Cada uno se apoya en el terreno que tiene debajo, o en el suelo de su planta donde no hay terreno.

![Personas en la puerta y coches aparcados junto a la casa](shots/entourage-3d.webp)

Selecciona uno para cambiarlo en las propiedades: su tipo, su **rotación**, su **altura** (por defecto la habitual de su tipo), cuánto está **sobre el terreno** (en una terraza o un balcón) y el **color** de su ropa o carrocería. Se mueve, gira, refleja y copia como cualquier elemento.

En 3D son figuras sencillas: personas con sus extremidades, ropa, pelo y zapatos, coches con sus cristales, ruedas y faros. Los dibujos los mantienen sencillos, como un arquitecto dibuja la ambientación, con una línea fina: plantas y planos de emplazamiento desde arriba, los hombros y la cabeza de una persona, el contorno de un coche con las esquinas redondeadas y su parabrisas; alzados y secciones como siluetas, un solo contorno cada uno, la figura de una persona, el lateral o el frente de un coche según lo ve la vista, nunca las líneas de sus cuerpos.

![En planta](shots/entourage-plan.webp)

![En el alzado sur](shots/entourage-elevation.webp)

![Las propiedades de un coche](shots/entourage-properties.webp)

Cómo se dibujan en 2D las personas, los coches y las plantas es un ajuste del estilo de vista, **Ambientación en 2D** (en las propiedades de la vista o en **Vista → Configuración de vista**; para un estilo entero, **Gestionar → Estilos de vista**): **contornos** (por defecto), **abstracto** (una persona un círculo en planta y una cabeza sobre un cuerpo en un alzado, un coche una caja redondeada, un árbol un círculo con su tronco), **detallado** (los símbolos de paisajismo de las plantas y cada línea de los cuerpos, con las sombras solares de la vista, como Lapin los dibujaba antes) o **sin dibujar**. **Relleno de siluetas** rellena las siluetas de alzados y secciones de gris claro o verde pálido. Se dibujan con la pluma 2 (0,13 mm a 1:100) salvo que **Gestionar → Estilos de objeto** dé otra a la categoría **Ambientación**, y **Visibilidad / Gráficos** oculta esa categoría en una vista. Los planos, el PDF y el DXF los imprimen como los dibuja la vista; sus cuerpos 3D nunca cambian. Los proyectos hechos antes también los dibujan como contornos.

Dan escala a los dibujos y a las imágenes, pero no forman parte del edificio. Una exportación IFC o Fragments (File → Export…) los deja fuera salvo que marques **Ambientación** en su lista de tipos; IFC los escribe como IfcBuildingElementProxy «Entourage». Las exportaciones 3D (glTF, USD, COLLADA) los incluyen. Las pruebas de colisiones los dejan fuera.

Las figuras siguen a las personas y los coches de [BimGo](https://github.com/aussieBIMguru/BimGo) de Aussie BIM Guru (MIT-0): sus proporciones, posturas y perfiles.

## Normas urbanísticas y el sol (Lapin Urban)

La edición **Lapin Urban** añade a esta ficha un grupo **Urbanismo**: las normas urbanísticas de la parcela (POT, CUT, las alturas máximas, los retranqueos a la calle, a los linderos laterales, al fondo y al eje de la vía), el **volumen edificable** que dejan, los indicadores que alcanza el modelo frente a ellas, y el **estudio de soleamiento**: el sol y sus sombras en 3D para cualquier día y hora en el lugar del proyecto, y planos de sombras para el equinoccio y los solsticios.
