---
title: Scripts de Python
order: 127
summary: Controla Lapin con Python desde el panel Script, un extra para desarrolladores: una consola y un editor que ejecutan tu propio Python, cada Ejecución un solo paso de deshacer.
---

## Activa los Extras para desarrolladores

Lapin se puede controlar con Python: cada herramienta es un comando, y un script envía los mismos comandos. El panel **Script** es un *extra para desarrolladores*, desactivado hasta que lo actives, así que de otro modo no se ve nada de él.

Abre **Archivo → Preferencias…** y, en **General**, marca **Extras para desarrolladores**.

![Preferencias: Extras para desarrolladores](shots/python-preferences.webp)

- **Python**: el Python que ejecuta el panel. Vacío, Lapin busca uno en este ordenador (`python3`, `py -3` en Windows, el de python.org y el de Homebrew). Debe ser **Python 3.11 o posterior**; Lapin no instala Python. Descárgalo de [python.org](https://www.python.org/downloads/) si no tienes, o escribe la ruta del que quieras (el de un entorno virtual, por ejemplo).
- **Editor externo**: el editor que usa **Abrir en el editor externo**, como comando (`code` para Visual Studio Code). Vacío, el editor de texto del sistema.

Python solo se ejecuta en la aplicación de escritorio: en Lapin para el navegador, el panel lo dice.

## El panel Script

Abre **Vista → Script**. El panel se abre a lo largo del borde inferior de la ventana; arrastra su borde superior para hacerlo más alto. Su cabecera dice qué Python encontró, o por qué no encontró ninguno.

![El panel Script tras una ejecución: cuatro muros, un solo paso de deshacer](shots/python-panel.webp)

- **Míos**, a la izquierda: tus propios scripts, archivos `.py` normales guardados en la carpeta de Lapin de este ordenador. Haz clic en uno para abrirlo; **Script nuevo** empieza desde un ejemplo.
- **El editor**, en el centro: Python con sus colores y su sangría. Escribe `lapin.` y te ofrece lo que tiene Lapin (los comandos de todos los módulos de tu edición y sus propias llamadas); `lapin.architecture.` ofrece los comandos de arquitectura con sus argumentos. Ponle un nombre al script y **guárdalo** en Míos; **Abrir en el editor externo** abre su archivo en tu editor, y lo que guardes allí vuelve al panel.
- **La consola**, a la derecha: lo que imprimen los scripts, sus errores con la línea en la que se detuvieron (marcada en el editor) y una línea para escribir Python. **Enter** la ejecuta, **Mayús+Enter** empieza una línea nueva, **Arriba** y **Abajo** traen las líneas escritas antes. Un nombre que defines en la consola se conserva para la línea siguiente y para los scripts que ejecutes.

En el panel, `lapin` es el paquete `lapinbim`, ya cargado. El mismo script se ejecuta desde un terminal con `import lapinbim as lapin`, mientras Lapin esté abierto.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Las longitudes van en metros y los ángulos en grados, sea cual sea la unidad que muestre el proyecto.

## Ejecutar, Detener, Reiniciar, Deshacer

- **Ejecutar** (**Ctrl+Enter** en el editor) ejecuta el script. Todo lo que hace es **un solo paso de deshacer**, con el nombre del script: un solo **Deshacer** lo quita todo.

  ![Un solo Deshacer quitó los cuatro muros de la ejecución](shots/python-undo.webp)

- Mientras se ejecuta un script, el modelo es del script: una herramienta que uses en la ventana se rechaza con *A script is editing the model*; las vistas, la selección y las tablas siguen funcionando.
- Un script que falla no conserva **nada** de lo que hizo: el modelo queda como estaba antes de **Ejecutar**, y la consola muestra el error y su línea.
- **Detener** detiene el script; no se conserva nada de lo que hizo. A un script que no se detiene en dos segundos se le cierra el Python.
- **Reiniciar** vuelve a iniciar Python: se olvidan los nombres que guardaba la consola.

## Seguridad

Un script puede hacer todo lo que tú puedes hacer en este ordenador: se ejecuta en tu propio Python, con tus permisos. Ejecuta scripts que hayas escrito tú o en los que confíes.

Un invitado de una sesión compartida no puede ejecutar scripts en tu ordenador, y un script solo se conecta al Lapin de su propio ordenador.

## Para desarrolladores

Lo que ofrece `lapin` (los comandos, las consultas, los lotes, el modelo, los eventos) se describe en la guía para desarrolladores, *Lapin from Python* (`docs/python.md` en el repositorio de Lapin); el paquete está en PyPI como `lapinbim`.
