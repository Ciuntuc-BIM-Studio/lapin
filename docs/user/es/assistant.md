---
title: El asistente de modelo
order: 127
summary: Pregunte a un modelo de IA local sobre su proyecto, obtenga informes breves y deje que proponga valores de propiedades que usted aprueba. Solo en la aplicación de escritorio.
---

El **Asistente** responde preguntas sobre el proyecto abierto con sus propias palabras: cuántos muros tiene cada planta y qué longitud tienen, qué puertas miden más de 1 m de ancho, cuáles aún no tienen resistencia al fuego. Hace informes breves en forma de tablas que puede exportar y puede **proponer** valores de propiedades (una resistencia al fuego, un estado, sus propios parámetros) que usted revisa y aplica.

Ejecuta un modelo de lenguaje **en su ordenador**, mediante [Ollama](https://ollama.com). Nada de su proyecto sale del equipo, y nada se envía a nosotros ni a nadie más.

> El asistente solo está en la **aplicación de escritorio**. La versión de Lapin para el navegador no tiene asistente.

## Lo que puede y no puede hacer

**Puede**:

- leer el proyecto: plantas, elementos, sus tipos, materiales, conjuntos de propiedades y cantidades, y las tablas;
- contar, sumar y promediar (longitudes, áreas, volúmenes, cualquier cantidad), agrupando por planta, tipo o cualquier propiedad. Lapin calcula los números; el modelo solo los comunica;
- proponer valores de propiedades, en los conjuntos estándar de IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) o en los suyos propios.

**No puede**:

- dibujar, mover, cambiar la forma, crear ni borrar nada. No cambia la geometría;
- ejecutar comandos ni escribir código;
- aplicar nada por sí mismo. Cada valor que propone espera su decisión.

Un modelo local puede malinterpretar una pregunta. Lea su respuesta y compruebe los valores propuestos antes de aplicarlos.

## Configurarlo

1. Instale Ollama desde [ollama.com](https://ollama.com) e inícielo (la aplicación Ollama, o `ollama serve` en un terminal).
2. Instale un modelo. El que mejor resultado dio en nuestras pruebas es **qwen3-coder:30b** (rápido y fiable con las herramientas de Lapin; necesita unos 20 GB de memoria):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   En un ordenador con menos memoria, **gemma4:12b** también funciona, más despacio.
3. En Lapin, abra el **Asistente**: la ficha **Asistente** junto a **Propiedades**, a la derecha, **Vista → Asistente**, o <kbd>Ctrl</kbd>+<kbd>K</kbd> y escriba *Asistente*.

Lapin encuentra Ollama en `http://127.0.0.1:11434` y elige el mejor modelo que tenga. El botón del engranaje, en la parte superior del panel, muestra la **Dirección de Ollama**, el **Modelo** que se usa y si Ollama está en marcha. Solo se acepta un Ollama de este ordenador.

## Preguntar

Escriba una pregunta y pulse <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> para una línea nueva). Pregunte en español, inglés, rumano u otro idioma: la respuesta llega en el mismo.

![El Asistente: un informe por planta y una resistencia al fuego propuesta para una puerta, a la espera de aprobación](shots/assistant.webp)

Mientras trabaja, el panel muestra cada paso en una línea, por ejemplo *Listed 12 of 12 · IfcDoor* o *Computed 2 group(s) over 40 element(s)*. Pase el ratón sobre un paso para ver exactamente lo que pidió. **Detener** termina la respuesta; **Nueva conversación** empieza de nuevo.

Las listas y los totales también aparecen como **tablas** debajo de la respuesta. El botón de descarga de una tabla la guarda como archivo CSV, para una hoja de cálculo.

Algunas preguntas para empezar:

- *¿Cuántos muros hay en cada planta y cuál es su longitud total?*
- *Enumera las ventanas de la primera planta con su ancho y su altura.*
- *¿Qué puertas no tienen resistencia al fuego?*

## Aprobar los valores que propone

Pregunte, por ejemplo, *Puertas de más de 1 m de ancho: dales FireRating EI30.* El asistente encuentra las puertas y propone los valores. Aparecen en **Cambios propuestos**: cada elemento, el conjunto de propiedades y el nombre, su valor actual y el nuevo, con una casilla.

- Desmarque lo que no quiera; la casilla de la cabecera marca o desmarca todo.
- Haga clic en el nombre de un elemento para seleccionarlo en las vistas.
- **Aplicar** escribe los valores marcados, todos juntos, como **un solo** paso: <kbd>Ctrl</kbd>+<kbd>Z</kbd> los deshace todos.
- **Rechazar** los descarta.

Los valores van a los parámetros de los elementos, que se muestran en el panel **Propiedades** con su conjunto, y a la exportación IFC: una propiedad estándar se une a su conjunto (`Pset_DoorCommon`), y las suyas propias van a un conjunto propio.

Las reglas que sigue el asistente (y que Lapin comprueba): un conjunto estándar de IFC (que empieza por `Pset_`) solo admite sus propios nombres de propiedad, en los elementos a los que está destinado; un conjunto propio nunca empieza por `Pset_`.
