---
title: Ascensores
order: 43
summary: Ascensores desde un configurador de doce tipos dimensionados según EN 81-20 y EN 81-70, con su hueco, foso, huida, sala de máquinas, cabina y puertas de piso, las aberturas en los forjados que atraviesan, plantas en cada nivel, una tabla e IFC.
---

## Colocar un ascensor

**Arquitectura → Ascensor** (<kbd>LF</kbd>) coloca un ascensor con un clic en la planta. El clic marca el centro de su hueco, y sus puertas miran hacia abajo del plano (gírelo con 0°, 90°, 180° o 270° en la barra, o después con su pinzamiento). El ascensor va desde el nivel de la planta hasta el nivel más alto del proyecto y para en cada nivel intermedio. Su configurador se abre de inmediato.

Mientras la herramienta está activa, la barra de la línea de comandos y el panel de propiedades muestran **el estilo** que coloca: uno de los doce tipos del catálogo o uno de los estilos propios del proyecto.

## El configurador

![El configurador de ascensores, con el estilo accesible en silla de ruedas](shots/elevator-configurator.webp)

Los estilos están a la izquierda. Un estilo rellena todos los campos, y después puede cambiar cualquiera a mano. El catálogo tiene estos tipos:

| Estilo | Personas | Carga nominal | Cabina (mm) | Accionamiento |
|---|---|---|---|---|
| Residencial pequeño | 4 | 320 kg | 900 × 1000 | sin sala de máquinas |
| Pasajeros estándar | 6 | 450 kg | 1000 × 1250 | sin sala de máquinas |
| Pasajeros mediano | 8 | 630 kg | 1100 × 1400 | eléctrico, sala de máquinas arriba |
| Pasajeros grande | 10 | 800 kg | 1350 × 1400 | eléctrico |
| Pasajeros de alto tráfico | 13 | 1000 kg | 1600 × 1400 | eléctrico, 1,6 m/s |
| Accesible en silla de ruedas | 8 | 630 kg | 1100 × 1400, puerta 900 | EN 81-70 tipo 2 |
| Montacamas de hospital | 17 | 1275 kg | 1200 × 2300 | eléctrico, cabina pasante |
| Montacargas | 26 | 2000 kg | 1500 × 2700 | eléctrico, 0,5 m/s |
| Hidráulico (baja altura) | 8 | 630 kg | 1100 × 1400 | pistón, central hidráulica junto al hueco |
| Sin sala de máquinas (MRL) | 13 | 1000 kg | 1100 × 2100 | máquina en la huida |
| Panorámico de vidrio | 8 | 630 kg | 1100 × 1400 | cabina de vidrio en un hueco de vidrio |
| Ascensor doméstico | 3 | 250 kg | 800 × 950 | 0,15 m/s, foso poco profundo |

A la derecha, tres pestañas:

- **Cabina y carga**: el accionamiento (eléctrico, sin sala de máquinas, hidráulico, ascensor doméstico), la carga nominal y las personas, la velocidad, el tipo accesible (EN 81-70 tipo 1, 2 o 3), el ancho, el fondo y el alto de la cabina, paredes de cabina de vidrio, ascensor de bomberos, ascensor de evacuación. **Carga y personas según la cabina** fija la menor carga nominal que EN 81-20 da al área de la cabina, y las personas que lleva.
- **Puertas y paradas**: un acceso o dos (cabina pasante), el ancho y el alto de la puerta, apertura central o lateral y sus hojas (dos o tres hojas se deslizan una detrás de otra), la parada más alta y los niveles en los que para. En un nivel que el ascensor pasa sin parar, el muro del hueco queda cerrado y no hay puerta de piso.
- **Hueco**: el lado del contrapeso (o del pistón de un ascensor hidráulico), el foso bajo la parada más baja y la huida sobre la más alta (ambos siguen al accionamiento y a la velocidad hasta que escriba sus propios valores), la sala de máquinas (arriba para el eléctrico, junto al hueco abajo para el hidráulico, ninguna en los demás), los muros del hueco (un tipo de muro del proyecto, u hormigón del espesor que indique), un hueco de vidrio, el giro.

![La sección por la cabina, del foso a la sala de máquinas](shots/elevator-section.webp)

La vista previa muestra la planta en la parada más baja y, en las otras pestañas, una sección por la cabina con todas las paradas (las servidas en rojo, las que pasa a trazos). Debajo, lo que dice EN 81: una cabina mayor de lo que permite su carga nominal, más personas de las que admiten la carga o el área, una cabina o una puerta accesible demasiado pequeña, un ascensor hidráulico con un recorrido de más de unos 18 m. Más abajo, las medidas libres y exteriores del hueco, el área de la cabina frente a la que permite su carga, las paradas, el recorrido y los forjados que abre el hueco.

**Aplicar** modifica el ascensor en un solo paso de deshacer. **Guardar como estilo…** lo guarda como estilo del proyecto; **Actualizar el estilo** lo vuelve a guardar y todos los ascensores de ese estilo lo siguen. Un estilo del proyecto se puede renombrar o borrar (sus ascensores quedan como están).

## En los planos

![Un ascensor en planta: el hueco seccionado, la cabina con su cruz, el contrapeso y las puertas de piso](shots/elevator-plan.webp)

El hueco abre cada forjado que atraviesa, con una abertura propia en cada uno. Cuando mueve, gira o copia el ascensor, o cambia sus niveles, las aberturas lo siguen. Para un forjado dibujado después del ascensor, **Arquitectura → Huecos del ascensor** lo abre.

Cada planta de un nivel al que llega el ascensor lo dibuja: los muros del hueco seccionados, la cabina con su cruz diagonal, el contrapeso con su diagonal (el pistón de un ascensor hidráulico como un círculo) y, en los niveles en los que para, las hojas de la puerta de piso y de la puerta de cabina, a trazos donde se deslizan al abrirse. Las secciones y los alzados dibujan todo el hueco, el foso, la cubierta, la sala de máquinas, la cabina en la parada más baja y las puertas de piso. En 3D la cabina está en la parada más baja con las puertas cerradas.

![Un ascensor en 3D, con su sala de máquinas sobre la casa](shots/elevator-3d.webp)

## Sus propiedades, su tabla e IFC

![Las propiedades de un ascensor](shots/elevator-properties.webp)

Seleccione un ascensor para cambiar su parada más baja y más alta, marcar los niveles en los que para, elegir el tipo de muro de su hueco, leer los avisos de EN 81 y abrir **Configurador…**. Sus campos (accionamiento, carga, cabina, puertas, foso, huida…) están debajo; los que no se aplican se ocultan (sin sala de máquinas en un ascensor sin sala de máquinas, sin lado del contrapeso en uno hidráulico). Arrastre su pinzamiento central para moverlo y el que está delante de sus puertas para girarlo. **Desplazar**, **Copiar**, **Girar** y **Simetría** funcionan como en cualquier elemento; un ascensor en simetría lleva su contrapeso al otro lado.

**Arquitectura → Tabla de ascensores** lista cada ascensor con su tipo, accionamiento, carga nominal, personas, cabina, puertas, las paradas que sirve, los niveles entre los que va, su recorrido, su velocidad y su hueco.

Cada ascensor tiene un tipo creado a partir de su clase, su carga y su cabina (Pasajeros mediano 630 kg, 1100 × 1400): los ascensores iguales lo comparten, de modo que una partida de precio puesta en el tipo (**Costes → Asignar partidas…**) da precio a todos, y la planificación los encuentra por su clase (IfcTransportElement).

En IFC un ascensor es un **IfcTransportElement** con el tipo predefinido **ELEVATOR**, de un **IfcTransportElementType**, con los conjuntos propios de IFC **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) y **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin vuelve a leer su propio archivo como un ascensor nativo, con sus paradas y las aberturas de los forjados.
