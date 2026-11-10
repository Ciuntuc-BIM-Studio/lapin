---
title: Muros
order: 20
summary: Dibuje muros esquina a esquina, modifíquelos en el panel de propiedades y configure tipos de muro con sus capas.
---

<!-- video walls -->

## Dibujar muros

Abra la ficha **Arquitectura** y elija **Muro**, o escriba <kbd>WA</kbd>.

Mientras la herramienta está activa, el panel de propiedades muestra **Elemento nuevo**: el tipo con el que se dibujan los muros. Elija otro ahí antes del primer clic y vea sus capas, su espesor y cuántos muros lo usan ya; **Editar tipos…** abre Wall types. Puerta, ventana, forjado, falso techo y muro cortina muestran sus tipos igual.

![La ficha Arquitectura](shots/ribbon-architecture.webp)

Haga clic donde empieza el muro y luego en cada esquina. El muro siguiente sigue al ratón. Mientras dibuja, puede escribir una longitud y un ángulo en las casillas sobre la vista en lugar de hacer clic. Pulse <kbd>Enter</kbd> o <kbd>Esc</kbd> para terminar.

![Dibujar muros: el muro siguiente sigue al ratón, con su longitud y su ángulo a la vista](shots/wall-drawing.webp)

Donde se encuentran dos muros, Lapin los une por sí mismo. Sus capas se encuentran tal como se construirían.

- **Muro curvo** (<kbd>WC</kbd>) dibuja un muro a lo largo de un arco.
- **Muro cortina** (<kbd>CW</kbd>) dibuja un muro acristalado con montantes y paneles; su tipo puede tomar su forma real de una [biblioteca de perfiles](profiles.md).
- Los muros se unen por sí solos donde se encuentran: una esquina a inglete, un muro que termina sobre otro en T, uno que cruza otro en X. En una T o una X las capas entran unas en otras como se construyen: el núcleo del muro que termina sigue a través de los acabados del otro hasta su núcleo, y sus acabados se detienen en la cara del otro, donde encuentran a sus iguales, así que la planta muestra una sola composición sin una línea que la cruce. **Unir** (<kbd>JW</kbd>), **Inglete** y **A tope** cambian cómo se encuentran dos muros. En ambos casos, cada capa dobla la esquina por sí sola: los acabados se encuentran con sus iguales y siguen sin cortes, y los núcleos se encuentran en la diagonal (inglete) o un núcleo sigue hasta la cara exterior del otro (a tope), así que la planta de muros multicapa muestra esquinas limpias. Cada capa entra en el otro muro a través de sus capas de menor **Prioridad** y se detiene en la primera de prioridad igual o mayor (vea **Tipos de muro** más abajo), así que por defecto los núcleos se encuentran con núcleos y los acabados se detienen en las caras; en una esquina, un núcleo de mayor prioridad atraviesa el del otro, a inglete o a tope. La **Prioridad de corte** de un muro ya no lo hace entrar en un muro unido a él: entre muros unidos solo decide qué núcleo pasa cuando ambos núcleos tienen la misma prioridad (en una esquina a tope o en un cruce).
- **Intercambiar** cambia cuál de los dos muros pasa de largo.

**La línea sobre la que dibujas.** Con la herramienta Wall activa, la lista de la barra de la línea de comandos elige la línea en la que haces clic: **Eje del núcleo** (el eje, como antes), **Núcleo, cara exterior**, **Núcleo, cara interior**, **Cara exterior** o **Cara interior**. El exterior es la derecha del muro según lo dibujas (rodea el edificio en sentido antihorario para que quede fuera). El eje del muro sigue en su núcleo, desplazado de la línea, y los muros dibujados uno tras otro sobre la misma cara se encuentran en la esquina de esa cara.

## Modificar un muro

Haga clic en un muro para seleccionarlo. Aparecen sus pinzamientos. Arrastre los extremos para alargarlo o acortarlo, o arrastre la cruz del centro para moverlo.

![Un muro seleccionado con sus pinzamientos](shots/wall-selected.webp)

El panel de propiedades muestra el muro seleccionado. La parte superior del panel contiene su tipo y sus capas. Debajo están su longitud, altura, desfase de base, planta y fase. Cambie cualquier valor y el modelo lo sigue. Los valores de cada muro son suyos. Su **grosor** (Thickness) viene de las capas de su tipo: escriba uno nuevo y los muros seleccionados toman el tipo con la misma composición y ese grosor, con el núcleo redimensionado y los acabados conservados. Lapin usa el tipo del proyecto si alguno tiene exactamente esa composición; si no, crea uno con el nombre del suyo (*Partition wall 130mm* pasa a *Partition wall 150mm*). Los demás muros del tipo anterior quedan como están, y <kbd>Ctrl</kbd> <kbd>Z</kbd> lo deshace en un solo paso.

![Las propiedades de un muro](shots/wall-properties.webp)

Más abajo, **Dependencias** muestra lo que se construye a partir del muro y a partir de qué se construye el muro. **Usado por** enumera los elementos que cambian cuando cambia el muro: los muros con los que se une, sus puertas y ventanas, un muro unido a él. Haga clic en un nombre para seleccionar ese elemento. **Construido a partir de** enumera los elementos de los que depende el muro, y después su tipo y sus materiales. Cuando elimina un elemento del que dependen otros, un mensaje nombra los que se reconstruyeron; <kbd>Ctrl</kbd> <kbd>Z</kbd> lo devuelve todo.

**Voltear** pasa las capas del muro al otro lado. También puede hacer clic en las pequeñas flechas que aparecen junto a un muro seleccionado en la planta.

Un muro reconocido de un IFC importado cuyo extremo el archivo corta en ángulo conserva ese corte: **Ángulo de corte inicial** y **Ángulo de corte final** en sus propiedades, en grados respecto al corte recto. Una unión en ese extremo lo sustituye.

**Alineación del sombreado** fija cómo se sombrean en planta las capas de este muro. **material** sigue el ajuste de cada material. **axis** coloca el sombreado de cada capa a lo largo del eje del muro, desde el inicio del eje, así un muro de ladrillo en ángulo muestra sus hiladas a lo largo de él, continuas de una capa a otra. **view** empieza cada sombreado en el origen de la vista, sin girar.

### Un muro por su perfil

**Perfil…** en las propiedades de un muro dibuja su coronación en alzado, vista de su inicio a su final: un hastial, una pendiente, escalones, un muro que sigue una escalera. Arrastra los puntos (**Mayús** mantiene la altura de un punto), haz clic en la línea superior para añadir uno, clic derecho en un punto para quitarlo, o escríbelos en la tabla en milímetros. **Empezar desde** da un **Hastial**, una **Pendiente** o **Escalones** hasta la altura que escribas. Un escalón son dos puntos uno sobre otro. **Aplicar** da la forma al muro; **Coronación plana** lo vuelve plano.

El muro sigue siendo un muro: su espesor, sus capas, sus uniones y sus puertas y ventanas quedan como estaban, y se exporta a IFC con su forma.

![Un hastial dibujado en el editor de perfil](shots/wall-profile-editor.webp)

![El muro oeste de la casa como hastial, con su ventana conservada](shots/wall-profile-3d.webp)

### El contorno y las barras de un muro cortina

**Perfil…** en las propiedades de un muro cortina dibuja su contorno en alzado, como en un muro: un hastial, una pendiente, escalones o cualquier contorno, con puntos que arrastrar, añadir o quitar y una tabla en milímetros. El muro sube hasta su punto más alto. Su retícula gana una línea donde el contorno se quiebra (un quiebro a menos de un montante y medio de una línea de la retícula se lleva a ella); cada vano conserva los travesaños que caben bajo su punto más bajo; el panel sobre el último llena hasta el contorno y una barra de borde lo sigue, inclinada; los montantes se cortan a él. Los ajustes del tipo se mantienen, y **Perfil…** abre de nuevo el contorno para cambiarlo, sin reconstruir; **Coronación plana** devuelve el rectángulo. Se ve en 3D, alzados y secciones, las tablas miden los paneles y las barras tal como están dibujados, y el IFC los lleva como piezas.

![Un hastial dibujado como contorno de un muro cortina](shots/curtain-boundary-editor.webp)

![El muro cortina bajo su hastial, en 3D](shots/curtain-boundary-3d.webp)

**Barras…** da a montantes y travesaños elegidos un perfil propio. El alzado del muro muestra cada barra: haga clic en una (<kbd>Mayús</kbd> o <kbd>Ctrl</kbd>/<kbd>⌘</kbd> añade más, o tome todos los montantes o todos los travesaños), elija un perfil de las [bibliotecas de perfiles](profiles.md) o una caja simple, su **Rotación** y su **Simetría**, su alineación transversal a la barra (cara frontal o posterior sobre su plano) y a lo largo (borde inicial o final sobre la línea de la retícula o el nivel) y sus desfases transversal y hacia el exterior del edificio; el dibujo de al lado muestra la sección tal como queda. **Aplicar a los elegidos** les da el estilo, **Volver al del tipo** se lo quita. El perfil propio de un tipo tiene los mismos campos de posición en **Muros cortina…**, para todos los muros del tipo. El perfil sigue siendo una copia en el proyecto: **Actualizar desde la biblioteca** cambia todas las barras hechas con él, en este muro y en otros.

![Barras: un perfil colocado en los montantes elegidos](shots/curtain-members-dialog.webp)

![Dos montantes con perfil propio, en 3D](shots/curtain-members-3d.webp)

**Paneles y puertas…** da a las celdas elegidas un panel propio o una puerta. El alzado del muro muestra cada celda tal como la construye el motor: haz clic en una (<kbd>Shift</kbd> o <kbd>Ctrl</kbd>/<kbd>⌘</kbd> añade más). Un **tipo de panel** se guarda una vez en el proyecto y se da a celdas de cualquier muro cortina: vidrio, macizo, un **relleno enmarcado** (un marco de su ancho y su fondo alrededor de la celda, el relleno de vidrio o macizo retranqueado de su cara exterior según el retranqueo) o **abierto** (sin panel); su espesor, su desfase fuera del eje y sus materiales (los del tipo de muro cortina si no se elige ninguno). **Dar a las elegidas** lo aplica, **El panel del tipo** lo quita; un tipo de panel cambiado cambia todas sus celdas. **Puerta en las elegidas** sustituye cada celda elegida por una puerta: una nueva puerta de muro cortina (sencilla o doble, batiente o corredera, su ancho y su alto, hojas acristaladas o macizas, el perfil del marco de las [bibliotecas de perfiles](profiles.md)) o cualquier tipo de puerta del proyecto. **Llenar la celda** le da el tamaño de la celda; si no, conserva el de su tipo, se coloca a la izquierda, al centro o a la derecha de la celda con un desfase, y lleva un travesaño encima y un montante al lado donde queda vidrio, acristalados con el panel de la celda. En la fila inferior la puerta apoya en el suelo y el travesaño inferior se corta bajo ella. Los ajustes se guardan por celda, así que siguen a la retícula cuando cambia: una puerta que llena su celda crece o mengua con ella, y una cuya celda ha desaparecido lo dice y vuelve con ella. La puerta es una puerta: selecciónala para cambiar su mano o su lado, su tipo, o moverla a otra celda (Mover a lo largo del muro); las etiquetas, las tablas de puertas (al tamaño al que está) y la exportación IFC (una IfcDoor que forma parte de su IfcCurtainWall) la cuentan.

![Paneles y puertas: un antepecho enmarcado y una puerta en las celdas elegidas](shots/curtain-cells-dialog.webp)

![Un muro cortina con una puerta y antepechos enmarcados, en 3D](shots/curtain-cells-3d.webp)

Bajo un contorno dibujado con **Perfil…** una puerta entra en una celda recortada dondequiera que su rectángulo quepa bajo el contorno: los montantes de al lado suben hasta el contorno, los paños junto a ellos se llenan hasta él, un travesaño cruza su cabeza y el vidrio de encima sigue la pendiente. Al llenar una celda así, su cabeza se detiene un travesaño por debajo del lado más bajo del contorno. Donde no cabe, Lapin dice cuánto falta y qué hacer (bajarla, moverla hacia el lado más alto o elegir una celda más alta). Un panel enmarcado en una celda recortada lleva el marco a lo largo de todo su contorno, también en el lado inclinado. Un alzado que mira al exterior de una puerta dibuja sus símbolos de apertura, a trazos, como en cualquier puerta o ventana.

![Una puerta bajo el contorno inclinado de un muro cortina, con paneles enmarcados a lo largo de la pendiente, en 3D](shots/curtain-cells-gable.webp)

![El símbolo de apertura de la puerta en el alzado](shots/curtain-cells-elevation.webp)

## Tipos de muro

Un tipo de muro es un conjunto de capas, desde la cara a la derecha del sentido en que se dibuja el muro (el exterior, si dibuja alrededor del edificio en sentido antihorario) hasta la cara de su izquierda. Cada capa tiene un material y un espesor, y una capa es el **núcleo**. La línea del muro va por el núcleo. Para editar los tipos, elija **Arquitectura → Tipos de muro**. Una capa que no sea el núcleo puede tener su propia altura: **Desde** y **Hasta** fijan hasta dónde llega sobre la base del muro (un alicatado hasta 1500, un aplacado desde 300); vacío, ocupa toda la altura.

La columna **Prioridad** (de 0 a 100) indica cómo se encuentra la capa con las capas de otro muro donde los muros se unen: atraviesa las capas del otro de menor prioridad y se detiene en la primera de prioridad igual o mayor. Si la deja vacía, la capa toma la prioridad de su material en **Gestionar → Orden de corte**, si no 50 para el núcleo y 0 para las demás capas (el número que se muestra en gris). Así, un núcleo de hormigón de 100 no queda interrumpido por un núcleo de montantes de 99 y atraviesa el tabique, y un enlucido duro de 60 detiene el núcleo de un muro que termina en él. La exportación IFC la escribe como la Priority de la capa, y la importación la vuelve a leer.

La columna **Nota** dice cómo se coloca la capa en este tipo (*en capa continua, Baumit SupraFix*): pertenece al tipo, no al material, y una etiqueta de composición la escribe tras la capa; IFC la lleva como Description de la capa. Cuando el material de la capa tiene espesores de catálogo (**Gestionar → Materiales → Datos**), la pequeña flecha junto al espesor los ofrece y un aviso marca una capa de otro espesor.

Una capa que no sea el núcleo también puede cambiar de material en su altura: el botón **capas** al final de su fila añade una pieza (azulejos, 2100 por defecto), desde la base de la capa hacia arriba; añada más para una franja (pintura hasta 900, azulejos hasta 1500), y por encima de la última pieza la capa sigue en su propio material hasta arriba. Cada pieza se dibuja, se corta en planta y en secciones y se mide en su propio material.

Una capa que no sea el núcleo puede tener espesor 0, para un material demasiado fino para modelarlo pero que hay que medir: una lámina impermeabilizante, una barrera de vapor, la pintura. No forma ningún sólido; las plantas y las secciones la dibujan como una línea gruesa discontinua donde queda entre dos capas (en una cara, la línea de la cara es la suya), el dibujo de Composición de los tipos la nombra como a las demás, y se mide por superficie: la cara de la capa sobre la que va, descontados los huecos, en las cantidades del muro y en la medición por material.

![Una capa de enlucido con azulejos hasta 2100 y el enlucido encima](shots/wall-layer-parts.webp)

![Tipos de muro: las capas de un muro de ladrillo](shots/wall-types.webp)

La leyenda sobre las capas muestra el muro en planta tal como lo muestra un muro seleccionado: el eje discontinuo con una punta de flecha en el sentido en que se dibujó, **D** en su cara derecha e **I** en su cara izquierda. Las capas se numeran desde 1, en la cara D, hasta la última, en la cara I, con los mismos números que las filas de la tabla; el núcleo, por el que va la línea del muro, aparece resaltado. Un muro volteado con **Voltear** tiene las capas al revés, de I a D. El mensaje de un guardado rechazado desaparece en cuanto vuelve a cambiar el tipo.

- **+ Capa** añade una capa. Las flechas suben o bajan una capa.
- **Zonas en altura** divide el núcleo de abajo arriba, por ejemplo una primera hilada, la fábrica y una viga de atado.
- **Dinteles** elige los dinteles colocados sobre puertas y ventanas: prefabricados u hormigonados in situ, con su apoyo y su altura.
- **Dibujar con él** cierra el cuadro de diálogo y empieza a dibujar muros de este tipo.

Un cambio en un tipo cambia todos los muros de ese tipo.

**Import…**, **Export CSV** y **Export JSON**, bajo la lista de tipos, llevan los tipos de muro y de forjado de un proyecto a otro, o a una hoja de cálculo y de vuelta: el CSV tiene una fila por capa (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), las filas de un tipo seguidas, y también se leen el punto y coma y la coma decimal de Excel. Al importar, un tipo del mismo nombre toma las capas del archivo y los demás se crean, en un solo paso que deshacer; un material que el proyecto no tiene se crea por su nombre. La tabla **Composición de los tipos** (Schedules → + → Type assemblies) lista cada tipo de muro y de forjado con su marca, su espesor total, cuántos elementos lo usan y sus capas en orden.

## Molduras y ranuras de muro

**Arquitectura → Moldura de muro** (<kbd>WB</kbd>) lleva un perfil a lo largo de la cara de un muro: una cornisa, una imposta, un zócalo, un vierteaguas. **Ranura de muro** (<kbd>RV</kbd>) lo corta en el muro: una ranura, una junta de sombra. Elija el perfil y la altura en la barra de la línea de comandos y haga clic en la cara del muro. En 3D la moldura va a la altura en la que hace clic; en planta, a la altura escrita, en el lado en el que hace clic. **desde arriba** mide la altura hacia abajo desde la coronación del muro, para una cornisa bajo el alero. **Moldura en los muros seleccionados** y **Ranura en los muros seleccionados** ponen lo mismo en cada muro seleccionado, en su cara exterior.

Una moldura recorre toda la cara y se interrumpe en las puertas y ventanas que cruzaría. Donde dos muros se encuentran en una esquina y ambos llevan una moldura del mismo perfil a la misma altura en las caras que se encuentran, las dos se unen a inglete, como una sola moldura que dobla la esquina. Seleccione una moldura para cambiarla en Propiedades: su perfil, su material, su cara (**Cambiar de lado**), su altura, su separación de la cara, dónde empieza y dónde termina a lo largo del muro y si se interrumpe en los huecos. Las propiedades de un muro muestran las molduras y ranuras que lleva.

Las molduras y ranuras van con su muro cuando se copia, se mueve a otro nivel o se borra. Se miden por longitud y volumen (en una ranura, el volumen quitado). En IFC una moldura es un IfcCovering MOLDING y una ranura un IfcOpeningElement RECESS en su muro; ambas vuelven como estaban.

![Cornisas, impostas y una ranura en los muros exteriores de una casa](shots/wall-sweeps-3d.webp)

### Molduras en el tipo de muro

Una moldura que deben llevar todos los muros de un tipo (una imposta de zócalo, una cornisa, una junta de sombra) pertenece al tipo. En **Arquitectura → Tipos de muro**, bajo las capas, **Molduras y ranuras** las muestra, una fila cada una: el perfil, **Moldura de muro** o **Ranura de muro**, la cara (D, la cara de la primera capa del tipo, o I, como las nombra la leyenda), la altura sobre la base del muro (o bajo su coronación con **Desde arriba**), el desfase hacia fuera de la cara, el material de una moldura y sus retornos. **Moldura de muro** y **Ranura de muro** bajo la tabla añaden una fila. Guarde el tipo: todos sus muros las reciben, y también un muro dibujado con el tipo o cambiado a él. Son molduras como las demás: medidas, en la tabla de molduras de muro, a inglete en las esquinas donde se encuentran muros del tipo, escritas en IFC. Las molduras de un muro volteado cambian de cara con sus capas. **Exportar JSON**, bajo la lista de tipos, las lleva consigo, con el contorno de los perfiles y los materiales por su nombre (el CSV solo lleva las capas).

Seleccione una en un muro: Propiedades indica **Del tipo**. Si cambia su altura, su desfase o cualquier otro campo, ese campo pasa a ser propio del muro (**Propios:** altura): los cambios posteriores del tipo no lo tocan. **Volver al tipo** le devuelve los valores del tipo. **Desactivar en este muro**, o borrarla, deja la moldura del tipo desactivada en ese muro; **Volver a activar las molduras del tipo**, en **Molduras y ranuras** del muro, las recupera.

![Una moldura en el tipo de muro, en el diálogo Tipos de muro](shots/wall-type-sweeps.webp)

### Retornos

Una moldura que llega a un extremo libre de su muro (sin otro muro unido ahí) hace retorno: a inglete en el extremo, su perfil dobla la esquina sobre la testa del muro y se detiene en el plano de la cara, de modo que la moldura queda cerrada en lugar de mostrar su corte. **Retornos en extremos libres**, en Propiedades, elige ambos (por defecto en una moldura), al inicio, al final o ninguno, para extremos rectos. Una ranura nunca hace retorno. En una esquina unida a otro muro la moldura no hace retorno: llega hasta la esquina, o se une a una moldura igual del otro muro. En puertas y ventanas se interrumpe en recto; marque **Retornos en huecos** para que también haga retorno alrededor de las jambas. **Retranqueo desde el inicio** y **Retranqueo desde el final** la detienen antes de los extremos de la cara (en recto ahí), y **Perfil invertido** da la vuelta a su perfil.

### Mover y copiar una moldura

Seleccione una moldura y use **MOVE** o **COPY** como con cualquier elemento; su muro no se mueve. Lo que el desplazamiento recorre a lo largo del muro la desliza sobre él (una moldura en toda la cara recibe entonces su propio inicio y final); lo que recorre a través del muro se ignora. Movida hacia arriba o hacia abajo en un alzado o una sección, cambia su altura. Una copia queda en el mismo muro, desplazada, o en otro muro cuando cae dentro de él, en la cara en la que cae. **ROTATE** y **MIRROR** la mantienen en su muro: reflejada respecto al eje del muro, pasa a la otra cara. Una moldura del tipo que usted mueva conserva su nuevo sitio como propio; movida a otro muro, pasa a ser una moldura propia de ese muro y la del tipo queda desactivada en el primero.

### Perfiles

**Gestionar → Perfiles** guarda los perfiles del proyecto, los contornos con los que se barren las molduras, las ranuras y las zapatas corridas. **Perfil nuevo** parte del catálogo con sus medidas: un rectángulo, una banda achaflanada, una cornisa, una media caña, una nariz de vierteaguas, una banda con goterón; una ranura en V, rectangular o redonda; para zapatas, rectangular, escalonada en 2 o 3 escalones, con la cara superior inclinada, excéntrica, con murete. O elija **Vértices escritos**: cada vértice u v en milímetros, con punto y coma entre ellos, y un tercer número para que el lado que sale de ese vértice sea un arco. **De un contorno dibujado** lee una polilínea cerrada o un rectángulo dibujado con Dibujo y seleccionado en su vista. El círculo de la vista previa es el punto de inserción: en una moldura está en la cara del muro, a la altura de la moldura, con u hacia fuera de la cara (hacia dentro del muro en una ranura). Si cambia un perfil, todo lo que lo usa lo sigue.

![El diálogo Perfiles](shots/profiles.webp)

### Revestimientos

Modele los revestimientos con las capas del propio muro, no con molduras. Añada una capa de acabado en el exterior del tipo de muro (piedra, tablas de madera, fibrocemento, un SATE), con su espesor y su material. **Desde** y **Hasta** le dan una altura propia (un zócalo de piedra hasta 600, revoco encima), y el botón **capas** de la capa cambia su material en franjas a lo largo de su altura. Para un revestimiento separado del muro, dibuje un muro propio sobre la cara (la opción de línea **Cara exterior** de la herramienta de muro) con un tipo hecho de las capas del revestimiento. El revestimiento colocado tabla a tabla (tablas verticales u horizontales, rastreles) todavía no se modela.

## Muros bajo una cubierta

Los muros del mismo nivel que una cubierta se detienen bajo ella por sí mismos y suben hasta sus hastiales. Para un muro de otro nivel, selecciónelo y elija **Arquitectura → Enlazar a cubierta** (<kbd>AR</kbd>), y luego haga clic en la cubierta — o en cualquier otro cuerpo hasta el que deba subir el muro: un sólido de Studio, un elemento importado. Consulte [Losas y cubiertas](floors-roofs.md).
