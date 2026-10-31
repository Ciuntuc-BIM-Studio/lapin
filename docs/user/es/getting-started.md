---
title: Primeros pasos
order: 10
summary: La pantalla de inicio, un proyecto nuevo, las partes de la ventana, cómo moverse por un dibujo y cómo elegir las unidades.
---

## La pantalla de inicio

Cuando Lapin se abre, muestra la pantalla de inicio. **Proyecto nuevo** empieza un edificio vacío. **Abrir proyecto…** abre un archivo `.lapin`. Los proyectos que ha abierto antes aparecen en **Recientes**.

![La pantalla de inicio](shots/start-screen.webp)

Un proyecto nuevo ya tiene tres niveles: **Foundations** (−1.50), **Ground floor** (±0.00) y **First floor** (+3.00). Se abre en la planta de Ground floor, lista para el primer muro.

## La ventana

![Las partes de la ventana de Lapin](shots/workspace.webp)

1. **Las fichas de la cinta.** Dibujo, Arquitectura, Acabados, Estructura, Emplazamiento, Studio, Anotar, Gestionar, Cantidades y Vista agrupan cada una sus herramientas. **Archivo** abre, guarda, importa y exporta. **Deshacer** y **Rehacer** están en el extremo derecho de la misma barra (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **La cinta.** Contiene las herramientas de la ficha actual. Pase el cursor sobre una herramienta para ver su nombre y sus letras de teclado.
3. **El navegador del proyecto.** Enumera los niveles, las plantas, las secciones, los alzados, las vistas 3D, las láminas y las tablas. Haga doble clic en uno para abrirlo en una pestaña. Un proyecto se abre en la planta de Ground floor, con la vista 3D en una pestaña al lado.
4. **La vista.** Aquí es donde se dibuja: una planta, una sección, una vista 3D o una lámina. Puede abrir varias vistas una al lado de otra con los dos botones de la parte superior derecha de la vista.
5. **La barra rápida.** Ofrece las herramientas más usadas en las teclas numéricas <kbd>1</kbd>–<kbd>9</kbd>.
6. **La línea de comandos.** Escriba aquí las letras de una herramienta y pulse <kbd>Enter</kbd>. Mientras una herramienta está en marcha, la línea le indica lo que necesita a continuación. Consulte [Dibujo y línea de comandos](drafting.md).
7. **El panel de propiedades.** Muestra lo que esté seleccionado. Sin nada seleccionado, muestra la vista que está delante.
8. **Ask Me.** Es el asistente de ayuda de Lapin. Consulte [Ayuda y comentarios](help.md).

¿Quiere más espacio para el dibujo? **Vista → Interfaz…** elige una ventana más compacta, y los paneles laterales se pliegan. Consulte [La interfaz y los métodos abreviados](interface.md).

## Moverse por el dibujo

- **Zoom.** Gire la rueda del ratón o pellizque en un trackpad. Lapin hace zoom donde está el cursor.
- **Encuadre.** Arrastre con el botón central o el derecho del ratón, o con dos dedos en un trackpad.
- **Órbita en 3D.** Arrastre con el botón izquierdo del ratón.
- **Mostrarlo todo.** Pulse el botón **Ajustar**, escriba <kbd>ZE</kbd> o haga doble clic con el botón central.

## Unidades

Lapin trabaja en milímetros, salvo que usted elija otra cosa. Para cambiar las unidades, abra **Gestionar → Normas de dibujo → Proyecto → Unidades**, o haga clic en **Unidades** en la barra de estado, en la parte inferior de la ventana. Puede elegir milímetros, centímetros, metros, pies y pulgadas, o pulgadas.

![Normas de dibujo, ficha Proyecto: las unidades del proyecto](shots/standards-project.webp)

Todas las longitudes siguen este ajuste: los paneles, los valores que escribe mientras dibuja, la línea de comandos y las cotas. El modelo en sí se guarda en metros, así que cambiar las unidades nunca cambia su edificio.

## Guardar

**Archivo → Guardar** (<kbd>Ctrl</kbd> <kbd>S</kbd>) escribe un archivo `.lapin`. Consulte [Archivos, versiones y recuperación](files.md).
