---
title: Sistemas de instalaciones
order: 57
summary: Dibuje sistemas de calefacción, agua, saneamiento, electricidad y ventilación como esquemas sobre la planta, con fuentes, terminales y los tramos entre ellos, y expórtelos como una red IFC.
---

La ficha **Instalaciones** dibuja las instalaciones del edificio tal como las esboza un ingeniero: un sistema por instalación, su **fuente** (una caldera, un contador de agua, una bajante, un cuadro eléctrico, un ventilador), sus **terminales** (radiadores, lavabos, inodoros, enchufes, luminarias, difusores) y los **tramos** entre ellos. Cada nodo es un elemento IFC real con sus puertos; cada tramo es un segmento de tubería, conducto o cable; las conexiones se mantienen cuando usted desplaza los elementos.

![La ficha Instalaciones](shots/ribbon-mep.webp)

## Un sistema con un clic

En **Sistema nuevo**, elija la instalación (**Calefacción**, **Agua**, **Agua caliente**, **Saneamiento**, **Electricidad**, **Ventilación**) y haga clic en la planta. Lapin coloca el sistema completo: la fuente donde hizo clic, un primer terminal a unos metros y el tramo entre ellos. El sistema se abre para su edición.

![Un sistema de calefacción en la planta baja: la caldera, dos radiadores bajo las ventanas y los tramos entre ellos](shots/mep-plan.webp)

En el modo de edición, todo lo que no pertenece al sistema aparece atenuado, y solo se pueden designar los nodos y tramos del sistema. Arrastre la fuente y el terminal a su sitio: su tramo los sigue. Añada terminales con **+ Radiador**, **+ Lavabo**… en el panel de propiedades, o con las herramientas de **Calefacción**, **Fontanería**, **Electricidad** y **Aire**: cada terminal nuevo se une al sistema con un tramo propio, desde el puerto libre más cercano de la fuente o desde el tramo más cercano (una derivación en T). **Hecho** (o <kbd>Esc</kbd>) sale del modo de edición y le indica cuántos codos y derivaciones tiene el sistema y qué queda aún abierto. **Editar sistema** (<kbd>ES</kbd>) lo vuelve a abrir.

## Tramos

**Tramo** (<kbd>RN</kbd>) dibuja un tramo punto a punto; pulse <kbd>Enter</kbd> para terminar. Un extremo que cae en un puerto libre se conecta a él; un extremo que cae en un tramo del mismo sistema deriva de él.

**Conectar** une el extremo de un tramo a un nodo o a otro tramo; **Desconectar** lo libera. **Puertos libres** selecciona todo lo que sigue sin conectar.

## Cambiar la forma de un tramo

Seleccione un tramo: sus pinzamientos están sobre la línea tal como está dibujada, incluso después de desplazar los nodos que conecta.

- **Arrastre un pinzamiento** para mover libremente ese vértice.
- **Arrastre el centro de un tramo recto** para deslizarlo lateralmente; los tramos rectos contiguos se estiran, de modo que un recorrido ortogonal sigue siendo ortogonal. Deslice el primer o el último tramo recto y Lapin añade allí un quiebro, para que el extremo se quede en su puerto.
- **Ctrl-clic** en un pinzamiento lo quita, y en el centro de un tramo recto añade uno; **Añadir pinzamiento** coloca uno donde haga clic.
- **Desvío** (<kbd>DU</kbd>) hace que un tramo rodee un obstáculo: haga clic en el tramo donde empieza el desvío, haga clic donde termina en el mismo tramo recto, luego mueva el cursor hacia el lado por el que pasa y haga clic (o escriba la distancia). El tramo sale, avanza en paralelo y vuelve, con cuatro vértices nuevos; conserva sus conexiones.
- **Redibujar trayectoria** (<kbd>RP</kbd>) da a un tramo un recorrido nuevo: selecciónelo (o empiece sobre él) y dibuje la trayectoria punto a punto, <kbd>Enter</kbd> para terminar. El tramo conserva su sistema, su tamaño y sus conexiones, y sus extremos vuelven a sus puertos.

![Un tramo de radiador que rodea un obstáculo con Desvío: sale, avanza y vuelve](shots/mep-detour.webp)


## Nodos

Cada nodo tiene una **función**: una caldera, un radiador, un colector, un contador de agua, un lavabo, un fregadero, un inodoro, una ducha, una bañera, una bajante, un registro, un cuadro eléctrico, un enchufe, una luminaria, un interruptor, un ventilador, un difusor, una rejilla de extracción, una válvula, una subida/bajada, un tapón. La función le da su clase IFC, sus puertos, su tamaño y su símbolo en la planta. Una válvula, un contador o un interruptor colocados sobre un tramo lo dividen y quedan en medio. Una **rise/drop** (subida/bajada) lleva un tramo a otra cota o a la planta superior.

![Las propiedades de un radiador: su sistema, su función y sus puertos, su tamaño y su potencia](shots/mep-radiator-properties.webp)

En el panel de propiedades, un nodo muestra su sistema, su función y sus puertos (libres o conectados); un tramo muestra sus dos extremos. Ambos toman los valores por defecto del sistema, salvo que usted les asigne un valor propio: tamaño, cota, el plano por el que discurren (suelo, techo, muro), doble o simple, material, pendiente.

## Sistemas

**Sistemas…** enumera los sistemas del proyecto y los valores por defecto del sistema actual: el tamaño, el plano y las cotas de cada plano, la pendiente (saneamiento), si los tramos se dibujan dobles (calefacción: ida y retorno) y a qué distancia, el material y el aislamiento. Cambie un valor por defecto y todos los nodos y tramos sin un valor propio lo siguen.

![El diálogo Sistemas](shots/mep-systems.webp)

## Plantas, listas e IFC

**Planta de instalaciones** crea una planta de la disciplina de instalaciones para el nivel, archivada en el conjunto de instalaciones del navegador. La **Lista de equipos** y la **Lista de tramos** de **Sistema** son tablas como cualquier otra: van en las láminas y se exportan con ellas.

En una exportación IFC, cada sistema es un IfcDistributionSystem, cada nodo y cada tramo tienen sus IfcDistributionPorts, y las conexiones se escriben entre los puertos, de modo que la red llega a otro programa conectada desde la fuente hasta el último terminal.

## Gas y gasóleo

**Gas** y **Gasóleo** son servicios propios en **Sistema nuevo**. Un sistema de gas dice su gas (**Gas natural** o **GLP**) y su **Clase de presión** (baja dentro de la casa, media en la calle) en **Sistemas…**; sus tuberías son por defecto de acero negro por las paredes a 2,1 m, y pueden ser de cobre, multicapa o **PE 100** para lo enterrado. El gasóleo va en cobre.

La instalación de gas de una casa empieza en el **armario de contador** de la fachada. Haz clic en **Nuevo sistema → Gas**: el armario va donde haces clic y una **caldera de gas** dentro, unidas por un tramo. Por el camino, las herramientas de **Gas** colocan sobre el tramo el **regulador de gas**, el **contador de gas**, la **llave general de gas**, las **llaves de aparato** y los **conectores flexibles**, que lo cortan como cualquier accesorio; la **cocina de gas** y el **calentador de gas** se unen a la instalación con un tramo propio. Donde una tubería de gas o de gasóleo atraviesa un muro pasa por una **vaina**, dibujada y contada con el tramo. Una caldera de gas dibujada en el sistema de calefacción como su fuente se une a la instalación de gas desde sus propiedades (el botón **+** con el nombre del sistema de gas).

![La instalación de gas de una casa: el armario, el regulador y el contador fuera, la caldera y la cocina en la cocina](shots/mep-gas-plan.webp)

Cada aparato guarda su **consumo** (m³/h) y su **tipo de aparato** según su evacuación (A sin chimenea, B de tiro natural, C estanco), para las listas y el dimensionado.

**Gasóleo** coloca un **depósito de gasóleo** (interior, exterior o enterrado) y una **caldera de gasóleo**; la **boca de carga** y el **sombrerete de ventilación** se unen a la carga y la ventilación del depósito, y el **filtro de gasóleo** va en la ida.

### El suministro en la calle

En **Calle**, **Red de gas a lo largo de…** (<kbd>GM</kbd>) tiende una red de gas a lo largo de lo que eliges — una calle, un alineamiento, una línea dibujada, un muro — a la **distancia a la línea** y la **profundidad (recubrimiento)** de la barra de la línea de comandos; bajo un terreno la profundidad se mide desde el suelo, si no desde el nivel. **Red de gas** la dibuja punto a punto. La red va en un sistema de gas de media presión, de tubo de PE, con su **cinta de señalización**.

**Acometida** (<kbd>GS</kbd>): elige la red y luego el armario. Lapin pone una **toma en carga** en la red, tiende la tubería de acometida en perpendicular a ella a su profundidad y hasta el armario, y sube allí al armario.

![La acometida desde la red de la calle al armario de contador, seleccionada](shots/mep-gas-street.webp)

![La red y la acometida bajo tierra, la instalación en la fachada](shots/mep-gas-3d.webp)

![Las propiedades de una acometida: de la toma en carga al armario, su recubrimiento y su diámetro](shots/mep-gas-service-properties.webp)

![Los valores por defecto de un sistema de gas: su gas y su clase de presión](shots/mep-gas-systems.webp)

**Mediciones de gas y gasóleo** en **Sistema** lista, sistema por sistema, las tuberías por material y diámetro con su longitud (y de ella los montantes), los codos, tes y vainas, la cinta de señalización, las llaves, contadores y armarios, y los aparatos con su consumo y potencia instalados. En IFC un sistema de gas es un IfcDistributionSystem de tipo GAS y uno de gasóleo FUEL; el armario es un IfcDistributionChamberElement, el contador un IfcFlowMeter GASMETER, las llaves IfcValve, la cocina un IfcBurner, el depósito un IfcTank.

![Las mediciones de gas y gasóleo](shots/mep-gas-list.webp)

Dimensionar las tuberías por el consumo de los aparatos y comprobar la pérdida de carga es ingeniería, en la edición **Lapin MEP**.

## Extractores

El grupo **Extractores** de la pestaña Instalaciones coloca extractores de baño y de cocina como elementos propios, cada uno de un tipo que usted configura:

- **Extractor de pared** (<kbd>VF</kbd>): un ventilador axial en un muro, que expulsa el aire directamente a través de él;
- **Extractor de techo**: un ventilador bajo el techo con un conducto que sale por su lateral;
- **Extractor en línea**: un ventilador tubular en el falso techo, sobre el conducto;
- **Extractor de ventana**: un ventilador montado en el acristalamiento de una ventana;
- **Extractor de techo con luz**: un extractor de techo con una luz en su rejilla;
- **Extractor con panel de lamas**: un panel de pared con lamas junto al ventilador y una tapa debajo.

Elija una herramienta y haga clic. Un extractor de pared, de ventana o con panel de lamas va sobre la cara del muro más cercana al clic (a menos de 600 mm), con su frente hacia el local y su centro a 2,1 m sobre el suelo (uno de ventana a 1,9 m). Un extractor de techo va donde hace clic, bajo el techo del local; uno en línea 250 mm por encima. La barra de la línea de comandos elige el tipo que coloca la herramienta, entre los tipos del proyecto o los incluidos en Lapin (un extractor de pared Ø100 con sensor de humedad, un extractor de techo con luz y detector de movimiento, un extractor en línea para dos baños…).

Un extractor de pared corta una abertura redonda en su muro, 20 mm más ancha que su conducto: el pasamuros del muro, como el de un tramo de instalaciones (se ve como volumen de hueco en las plantas y pasa al IFC como abertura del muro). Se encuentra por el lugar donde está el extractor, así que un extractor desplazado a lo largo del muro se lleva su abertura. Un extractor de ventana está en el acristalamiento y no corta nada.

![Un baño con un extractor de pared, un extractor de techo con luz conducido al exterior a través del muro y un extractor con panel de lamas](shots/fans-plan.webp)

Las plantas dibujan un extractor con su símbolo de instalaciones: un círculo con sus aspas, su frente sobre el muro, su manguito discontinuo a través del muro y una flecha en el sentido del aire; la rejilla de un extractor de techo discontinua, ya que está por encima del corte. Las plantas de techo, las secciones, los alzados y el 3D dibujan los cuerpos.

**Conducto a la salida** lleva fuera el aire de un extractor de techo o en línea: haga clic en el extractor y luego donde sale el conducto. El conducto es un tramo del sistema **Bathroom extract** del proyecto (creado con el primer conducto; corta los muros que atraviesa), tan ancho como el conducto del extractor, a la altura de su boca. Termina en una rejilla en la cara exterior del muro o, con **Por la cubierta** activo en la barra, en un tubo que sube hasta un sombrerete. Desplazado o elevado, el extractor se lleva el inicio del conducto.

![Los extractores en 3D: el conducto hasta la rejilla exterior, la rejilla del extractor de pared en la fachada](shots/fans-3d.webp)

### Tipos de extractor

**Tipos de extractor** abre los tipos: los del proyecto y los incluidos, que se añaden con un clic. Un tipo define:

- su **montaje** (los que admite su clase) y su **diámetro de conducto** (Ø100, 125, 150 o 200 mm);
- la **forma** de su rejilla (redonda o cuadrada), su **tamaño**, su **dibujo** (lamas, ranuras, anillos o lisa) y su **acabado** (blanco, plata, negro, inoxidable o cromo);
- su **caudal** (m³/h) y su **potencia** (W);
- su **control**: un interruptor, un sensor de humedad, un temporizador, ambos o un detector de movimiento;
- una **compuerta antirretorno**, y la **luz** de un extractor de techo, la longitud del cuerpo de uno en línea, el ancho y el alto de un panel de lamas, mostrados solo donde aplican.

La vista previa de al lado es del motor: el símbolo en planta arriba, el frente abajo. Todos los extractores de un tipo siguen un cambio.

![Los tipos de extractor con la vista previa de un panel de lamas](shots/fans-types.webp)

Seleccione un extractor para cambiar su tipo, su **altura sobre el suelo**, su giro y, cuando tiene conducto, su **salida** (muro o cubierta) y cuánto sube la salida sobre el conducto. Arrastre el pinzamiento del centro para moverlo y el de delante para girarlo.

![Las propiedades de un extractor de techo](shots/fans-properties.webp)

### Tablas, costes e IFC

La **Tabla de extractores** lista cada extractor con su local, clase, tamaño, caudal, montaje, control, altura y salida, y el caudal total. La **Comprobación de extracción por local** lista cada baño, aseo y cocina (y cada local con un extractor) con su volumen, la extracción que necesita y la que dan sus extractores: un baño 8 renovaciones por hora y al menos 54 m³/h, un aseo 6 y 25 m³/h, una cocina 6 y 108 m³/h. Son valores orientativos; compruébelos con la norma con la que proyecta.

Un tipo nuevo tiene la partida de coste `LAP-FAN`, contada por extractor (Costes → Asignar partidas… la cambia).

El IFC escribe un extractor como **IfcFan** de un **IfcFanType** con su tipo predefinido (PROPELLORAXIAL para uno de pared o de ventana, CENTRIFUGALRADIAL para uno de techo, TUBEAXIAL para uno en línea) y **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate); la abertura del muro como IfcOpeningElement. Lapin los vuelve a leer como sus propios extractores.

## Coordinación: volúmenes de hueco y comprobación de colisiones

Cada hueco en un muro, losa o cubierta puede ser un **volumen** propio: un sólido del tamaño del hueco, a través de todo el espesor de su anfitrión, acabados incluidos. Los programas de colisiones (Navisworks, Solibri, BIMcollab) comprueban las instalaciones contra esos volúmenes con «completamente dentro»: una tubería, un conducto o una bandeja de cables entero dentro de un volumen pasa; uno que sobresale, o que atraviesa un muro donde no hay hueco, es una colisión. Lapin hace la misma comprobación.

**Gestionar → Comprobación de colisiones…** ejecuta la comprobación solo cuando se pide: nada la ejecuta por sí solo, ni una edición ni **Gestionar → Comprobación**. Su pestaña **Configuración** fija para qué huecos se hacen volúmenes (huecos en muros, de puertas, de ventanas, en forjados y en cubiertas), cuánto mayores o menores que sus huecos son (**Desfase del volumen**) y la **Holgura** que un elemento de instalaciones debe guardar respecto a los lados de un volumen. **Ejecutar comprobación de colisiones** recorre una vez cada tubería, conducto y bandeja de cables — los tramos de Lapin y los segmentos de un modelo de instalaciones importado por igual, y los huecos previstos de un modelo de instalaciones — y guarda lo que encontró como el **informe de colisiones** del proyecto, con la fecha en que se ejecutó, hasta que se ejecute de nuevo. Cuando el modelo cambia después, el informe dice que está **desactualizado**. Sus problemas se agrupan por tipo y se numeran, y cada número se mantiene de una ejecución a la siguiente:

- **Sin hueco**: atraviesa el anfitrión donde no tiene ninguno;
- **Sin cortar: portante u orden de corte**: sin hueco, porque los huecos propios de los tramos (abajo) no cortan ese anfitrión;
- **Hueco demasiado pequeño**: pasa por un hueco pero sobresale de él, holgura incluida; **Falta** dice cuántos milímetros en el peor lado;
- **Volumen vacío** (con **Huecos vacíos** marcado en **Configuración**): un hueco de muro o forjado por el que no pasa nada (un hueco de escalera nunca lo es).

![El informe de colisiones: sin hueco, un hueco demasiado pequeño y un volumen vacío, cada uno numerado](shots/mep-voids-check.webp)

Cada problema está **Por corregir**, **Resuelta** o **Ignorada**, con un comentario. Un clic en una fila muestra la pareja en 3D, ambas seleccionadas (**solas en 3D** oculta el resto por ahora); las flechas van a la anterior y a la siguiente. **Marcar en la vista** (también **Vista → Marcas de colisiones**) dibuja cada problema abierto en la planta, sección o alzado al frente como una nube roja con su número, solo en pantalla.

![La planta con las colisiones marcadas: nubes rojas con sus números](shots/mep-voids-clouds.webp)

**Hacer hueco** corta un hueco simple alrededor del elemento en el muro (redondo para una tubería o un cable, rectangular para un conducto) o en el forjado, con sus lados a la holgura y su tamaño redondeado hacia arriba según la regla de instalaciones del orden de corte; **Ampliar hueco** agranda el hueco demasiado pequeño. **Corregir selección** lo hace para cada fila marcada, **Ignorar** y **Reabrir** cambian su estado; cada uno es un solo paso para deshacer. El informe dice entonces que está desactualizado: ejecútalo de nuevo y lo corregido aparece **Resuelta** (**mostrar resueltas**). Su línea dice entonces en qué ejecución se encontró resuelta y cómo está ahora: el hueco por el que pasa y el tamaño del tramo en él (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), o que el tramo ya no pasa; cada ejecución posterior actualiza los tamaños.

![Ejecutada de nuevo tras las correcciones: las dos colisiones resueltas](shots/mep-voids-fixed.webp)

**BCF** convierte las colisiones abiertas en observaciones de tipo **Clash** con sus puntos de vista (**Gestionar → Observaciones en el modelo…**) y las guarda como archivo BCF; ejecutada de nuevo, actualiza las mismas observaciones. **CSV** y **PDF** guardan el informe como lista. **Gestionar → Comprobación** solo lee el informe: sus colisiones abiertas son avisos, y también lo es un informe desactualizado.

### Tramos que cortan sus propios huecos

El orden de corte (**Gestionar → Orden de corte**) incluye también los tramos de instalaciones, por encima de muros, forjados y cubiertas. Con **Los tramos cortan muros, forjados y cubiertas** activado, un tramo que atraviesa un muro, un forjado o una cubierta corta allí su propio hueco: redondo para una tubería o un cable que pasa en perpendicular, rectangular para un conducto o un tramo inclinado, tan grande como el exterior del tramo (su aislamiento, la vaina de una tubería de gas) más la **Holgura** alrededor, redondeado hacia arriba al paso, nunca menor que el **Hueco mínimo**. El hueco no se guarda aparte: se mueve y crece con el tramo y desaparece cuando el tramo desaparece o deja de atravesar. Es un volumen como cualquier hueco (dibujado, listado, comprobado), y la exportación IFC lo escribe como **IfcOpeningElement** con **Lapin_ProvisionOpening** (y como hueco previsto con **Volúmenes de hueco** marcado), así que una comprobación ejecutada de nuevo no encuentra nada que informar. Un muro, forjado o cubierta situado por encima de los tramos en el orden de corte (por su clase o su material) no se corta, ni uno portante (LoadBearing en su Pset_…Common) salvo que **cortarlos también** esté marcado: la comprobación de colisiones los informa como **Sin cortar**. Un tramo demolido no corta nada, y donde un tramo ya pasa por un hueco no se corta otro. Un sistema puede decidir otra cosa en **Instalaciones → Sistemas…** (**Los tramos cortan muros, forjados y cubiertas**: según el orden de corte, sí, no). Está activado en un proyecto nuevo; un proyecto hecho antes lo mantiene desactivado hasta que se active.

En un muro curvo el hueco se encuentra donde el tramo corta el arco, con su anchura medida a lo largo de él; uno rectangular tiene los lados radiales, como el de una puerta o una ventana. En una cubierta se corta en el faldón por el que pasa el tramo: vertical para una tubería en pie (una ventilación), alargado en la dirección del tramo para uno inclinado, como en un forjado; las correas y la cumbrera bajo el faldón no se cortan, y la comprobación de colisiones informa de un tramo que las atraviesa. Mover o cambiar de tamaño un tramo reconstruye solo los muros, forjados y cubiertas que atravesaba o atraviesa ahora, por grande que sea el modelo. El IFC del propio Lapin, leído de nuevo, no vuelve a tomar estos huecos cuando llegan también sus tramos (los tramos los cortan de nuevo, nunca dos veces); sin su tramo (dejado fuera de la importación, o un proyecto cuyos tramos no cortan nada) cada uno entra como hueco de su anfitrión, así que nada se pierde.

![Los tramos de instalaciones en el orden de corte](shots/mep-voids-cut-order.webp)

La pestaña **Volúmenes** lista cada volumen con su marca (V1, V2… planta por planta), su anfitrión, la puerta o ventana que contiene, su tamaño y profundidad; un clic selecciona su hueco.

![Los volúmenes con sus marcas y tamaños](shots/mep-voids-list.webp)

**Vista → Volúmenes de hueco** con la vista 3D delante los dibuja en 3D, violeta translúcido, con sus marcas; un clic en uno selecciona su hueco.

![Los volúmenes del muro interior, con las tuberías que los atraviesan](shots/mep-voids-3d.webp)

Con una planta, sección o alzado delante, **Vista → Volúmenes de hueco** los dibuja en esa vista, como un plano de coordinación muestra los huecos previstos: cortados donde la vista los corta (el hueco de un muro en todo su espesor, el de un forjado a través de la losa), vistos donde los ve (un hueco de forjado bajo el corte de la planta, el hueco de un muro en la cara que mira un alzado), a trazos cuando el hueco de un muro queda bajo el corte o el de un forjado sobre él; cada uno en cruz, con su marca y, debajo, su tamaño y la cota inferior de un hueco en muro (**400×200 +0.90**, **Ø200**). Los dibuja el motor con el resto del dibujo, así que los planos, el PDF y el DXF (capa **VOIDS**) los imprimen como los muestra la pantalla.

![La planta con los volúmenes: cortados en los muros, el hueco del forjado visto, cada uno en cruz y con su marca](shots/mep-voids-plan.webp)

La etiqueta de un hueco se coloca junto a él, a través de su muro, donde no hay nada dibujado: el barrido de una puerta, el marcador de la puerta o ventana, otras etiquetas. Si ambos lados del muro están ocupados, se desplaza a lo largo del muro más allá del hueco, o más lejos.

![El hueco de una puerta: su etiqueta libre del barrido y del marcador de la puerta](shots/void-label.webp)

Para mover una etiqueta, haz clic en ella (o en el hueco): así se selecciona la abertura, cuya etiqueta muestra un pinzamiento cuadrado para moverla y uno redondo para girarla. Ctrl/⌘-clic en un pinzamiento la devuelve a su sitio por defecto, o de nuevo horizontal. Una etiqueta movida la guarda esa vista, a lo largo y a través del muro desde el centro del hueco, así que sigue a la abertura cuando el muro se mueve; los planos y el PDF la imprimen donde la pusiste.

![La etiqueta del hueco movida por su pinzamiento](shots/void-label-moved.webp)

Es un ajuste de la vista, también en sus propiedades (**Vista → Configuración de vista**): **Volúmenes de hueco**, y **Tamaños de huecos** para el tamaño bajo la marca. Por defecto ninguna vista los dibuja; el estilo de vista **Coordinación** (**Gestionar → Estilos de vista**, elegido como **Estilo** de la vista) los dibuja en cada vista que lo toma. Sus líneas son la categoría **Volúmenes**: sus plumas y colores en **Gestionar → Estilos de objeto** (líneas de corte, proyección, ocultas, por encima), mostradas u ocultas por vista en **Vista → Visibilidad / Gráficos…**.

Para pasar los volúmenes a un programa de colisiones, marca **Volúmenes de hueco** en **Archivo → Exportar IFC o Fragments…** (o **Exportar 3D…**). En IFC cada volumen es un **IfcBuildingElementProxy** de tipo **PROVISIONFORVOID** con **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter si es redondo, Depth) y el conjunto de Lapin **Lapin_OpeningVoid** (marca, tipo, anfitrión, hueco y desfase), relacionado con su anfitrión; los huecos siguen siendo IfcOpeningElement. Cada volumen conserva su GlobalId de una exportación a otra. En una exportación de mallas son un grupo propio, **Opening voids**. Sin marcar la casilla, las exportaciones quedan como estaban.

## MEP Pro

La ingeniería sobre la red —la demanda que lleva cada tramo y el tamaño que la soporta, las comprobaciones (extremos abiertos, codos cerrados, pendientes de saneamiento, fuentes sobrecargadas), las reglas de trazado por material (cobre en pasos de 45°, PVC con giros de 45° como máximo), radiadores propuestos bajo las ventanas, recorridos sugeridos alrededor de los muros, el perfil de un tramo y la isometría de un sistema— está en la edición **Lapin MEP**, que añade a esta ficha los grupos **Ingeniería** y **Asistentes**.
