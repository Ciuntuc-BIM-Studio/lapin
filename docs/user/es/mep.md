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

## MEP Pro

La ingeniería sobre la red —la demanda que lleva cada tramo y el tamaño que la soporta, las comprobaciones (extremos abiertos, codos cerrados, pendientes de saneamiento, fuentes sobrecargadas), las reglas de trazado por material (cobre en pasos de 45°, PVC con giros de 45° como máximo), radiadores propuestos bajo las ventanas, recorridos sugeridos alrededor de los muros, el perfil de un tramo y la isometría de un sistema— está en la edición **Lapin MEP**, que añade a esta ficha los grupos **Ingeniería** y **Asistentes**.
