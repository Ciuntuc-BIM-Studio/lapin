---
title: Trabajar juntos
order: 125
summary: Comparta el proyecto abierto y trabaje en él en directo con otras personas, desde Lapin o desde un navegador.
---

<!-- video sharing -->

Varias personas pueden trabajar en el mismo proyecto al mismo tiempo, como los jugadores que se unen al servidor de un juego. Uno de ustedes comparte el proyecto desde Lapin en su ordenador: es el **anfitrión**. Los demás se unen como **invitados**, desde Lapin en su propio ordenador o desde un navegador, con el enlace que les envía el anfitrión.

El proyecto se queda en el ordenador del anfitrión, y el Lapin del anfitrión decide cada edición. Los invitados ven el modelo a medida que cambia, dibujan con las mismas herramientas y ven dónde están los demás.

## Compartir el proyecto

**Archivo → Compartir sesión…**, indique su nombre, elija cómo se unen los invitados y pulse **Compartir**:

- los **editores** modelan como usted;
- los **observadores** miran y se desplazan, y no cambian nada.

Lapin muestra un **enlace** y un **código**. Envíe el enlace a quien se una desde un navegador, y el código a quien se una desde Lapin. Cualquiera que los tenga puede unirse hasta que pulse **Dejar de compartir**. Cada vez que comparte, el enlace y el código son nuevos.

![Compartir: el enlace, el código y quién está en la sesión](shots/share-dialog.webp)

En el mismo cuadro de diálogo puede hacer que un invitado sea editor u observador, o **pedir que salga**. Ciérrelo y la sesión continúa. El icono de personas junto a los participantes, en la barra superior, lo vuelve a abrir.

Mientras el proyecto está compartido y tiene un archivo, Lapin lo guarda cada 5 minutos. Solo guarda el anfitrión; los invitados pueden exportar archivos IFC y 3D para sí mismos.

## Unirse a una sesión

- **Desde un navegador**: abra el enlace, indique su nombre y pulse **Unirse**. No hay nada que instalar.
- **Desde Lapin**: en la pantalla de inicio, o en **Archivo**, elija **Unirse a una sesión…**, pegue el enlace o el código e indique su nombre.

![Unirse desde Lapin](shots/join-dialog.webp)

Una barra en la parte inferior indica en la sesión de quién está. **Salir** le devuelve a su propio Lapin. Si el anfitrión deja de compartir o cierra Lapin, Lapin se lo indica, y usted vuelve con **Volver a Lapin**.

## Quién está dónde

Los participantes están en la barra superior, cada uno con su color. Haga clic en uno para ir a la vista que está mirando.

![Los participantes](shots/participants.webp)

En planta ve los cursores de los demás con sus nombres, y lo que tienen seleccionado, contorneado en su color.

![El cursor y la selección de un invitado en la planta del anfitrión](shots/sharing-plan.webp)

## Deshacer es solo suyo

<kbd>Ctrl Z</kbd> deshace su última edición, no la de otra persona. Si alguien la cambió después que usted, Lapin la deja como está y le indica quién lo hizo. A partir de ahí, su deshacer no va más atrás de esa edición.

## Seguro por diseño

- Todo lo que circula entre el anfitrión y los invitados pasa por el relé de Lapin, **cifrado de extremo a extremo** con la clave de la invitación. El relé lo transmite y no puede leerlo.
- Un invitado no puede abrir, guardar ni leer archivos en el ordenador del anfitrión, ni abrir allí otro proyecto.
- Lapin no abre nada a la red hasta que usted comparte, y se detiene cuando deja de compartir.
