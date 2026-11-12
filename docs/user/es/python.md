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

## Graba lo que haces como script

La pestaña **Grabadora**, junto a la consola, escribe lo que haces en la ventana como Python, una línea por paso.

1. Haz clic en **Grabar**. Un punto rojo en la pestaña indica que se está grabando.
2. Trabaja como de costumbre: dibuja muros, cambia una propiedad, borra algo. Cada paso aparece al instante como una línea, por ejemplo `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Haz clic en **Detener** y luego en **Copiar** (para pegar el script en otro sitio) o en **Guardar como script** (pasa a Míos y se abre en el editor).

![La grabadora tras dibujar dos muros](shots/python-recorder.webp)

El script se escribe de modo que también funcione en otro proyecto:
- lo que creó un paso se guarda en una variable (`wall1`), y los pasos siguientes la usan (`wall1.id`);
- la planta en la que dibujaste se encuentra por su nombre, y si no, por su cota (`lapin.model.storey("Ground floor", 0)`), y un tipo por su nombre (`lapin.model.find("IfcWallType", "Brick 250")`);
- otro elemento que ya estaba en el proyecto se nombra por su id (`lapin.model["…"]`), así que esa línea solo funciona en este proyecto;
- con **Selección como lapin.selection()** marcada, un comando sobre lo que tenías seleccionado se escribe `lapin.selection()`: el script trabaja entonces sobre lo que esté seleccionado cuando lo ejecutes.

Un **Deshacer** mientras se graba quita el último paso del script, y **Rehacer** lo devuelve, de modo que el script contiene lo que hiciste al final. Lo que hace un script nunca se graba, ni tampoco las consultas que no cambian nada (leer una tabla, una medición).

![El script grabado, ejecutado en un proyecto nuevo: los mismos dos muros](shots/python-recorder-new.webp)

## Los scripts del proyecto

Un script puede guardarse **en el proyecto**, para que viaje con el archivo: ábrelo o escríbelo en el editor y haz clic en **Guardar en el proyecto**. Aparece en **Del proyecto**, a la izquierda. Guardar, renombrar y borrar un script del proyecto son cada uno un solo paso de deshacer, y las versiones del proyecto conservan sus scripts. En el archivo `.lapin` cada uno es además un archivo de texto sencillo, `scripts/<name>.py`, así que puede leerse sin Lapin.

**Un script del proyecto nunca se ejecuta solo**, ni al abrir el proyecto ni al guardarlo. La primera vez que lo ejecutas, y de nuevo cada vez que su código ha cambiado, Lapin pregunta:
- **Ejecutar una vez**: se ejecuta esta vez, y la siguiente ejecución vuelve a preguntar;
- **Confiar**: se ejecuta, y este código queda recordado como de confianza en este ordenador (un cambio en él vuelve a preguntar);
- **Cancelar**: no se ejecuta nada.

Un script que guardas tú mismo en el proyecto es de confianza desde que lo guardas. Tus propios scripts (Míos) nunca preguntan.

## Qué está seleccionado

`lapin.selection()` devuelve lo que está seleccionado en la ventana, como elementos:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Los campos de un elemento se leen por sus nombres y se cambian por asignación: `wall.height = 3` cambia el muro (un solo paso de deshacer, o parte de la Ejecución). `with lapin.step("Name"):` convierte varios comandos en un solo paso de deshacer, y cada uno sigue respondiendo al instante, de modo que uno posterior puede usar lo que creó uno anterior.

## Seguridad

Un script puede hacer todo lo que tú puedes hacer en este ordenador: se ejecuta en tu propio Python, con tus derechos. Ejecuta scripts que hayas escrito tú o en los que confíes; un script del proyecto pregunta primero.

Un invitado de una sesión compartida no puede ejecutar scripts en tu ordenador ni poner uno en tu proyecto, y un script solo se conecta al Lapin de su propio ordenador.

## Para desarrolladores

Lo que ofrece `lapin` (los comandos, las consultas, los lotes, el modelo, los eventos) se describe en la guía para desarrolladores, *Lapin from Python* (`docs/python.md` en el repositorio de Lapin); el paquete está en PyPI como `lapinbim`.
