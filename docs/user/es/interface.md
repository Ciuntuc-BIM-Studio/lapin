---
title: La interfaz y los métodos abreviados
order: 15
summary: Los modos Cómodo, Compacto y Enfoque, cómo plegar los paneles laterales y los métodos abreviados de teclado que puede cambiar.
---

<!-- video interface -->

## Tres modos

**Vista → Interfaz…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) elige cuánto rodea a la vista. Elija uno de los tres modos en la parte superior del cuadro de diálogo; el cambio se ve al instante.

![El cuadro de diálogo Interfaz: los tres modos, y cada parte por separado](shots/interface-dialog.webp)

- **Cómodo** es la ventana tal como la muestran las demás páginas: la cinta con herramientas grandes y sus nombres, una línea de comandos bajo cada dibujo, y el HUD, la indicación, los campos de longitud y ángulo, el plano clave y las herramientas rápidas sobre la vista.
- **Compacto** da a la vista la mayor parte de la ventana:

![Compacto: más espacio para el dibujo](shots/interface-compact.webp)

1. **La cinta** es una sola banda de iconos. Unas líneas finas separan los grupos. Pase el ratón sobre una herramienta para ver su nombre, sus teclas y su grupo.
2. **El explorador del proyecto** se pliega en un carril. Sus iconos abren por un momento el proyecto, los niveles o el árbol IFC sobre la vista.
3. **Las herramientas rápidas** <kbd>1</kbd>–<kbd>9</kbd> se sitúan en el borde izquierdo de la vista.
4. **El plano clave** es un botón. Haga clic en él para abrir el mapa, y en su × para plegarlo de nuevo.
5. **Una sola línea de estado** contiene el nombre de la herramienta y lo que pide, lo que usted escribe, los campos de longitud y ángulo mientras dibuja, SNAP, ORTHO y POLAR, el nivel y la vista, y luego el proyecto.

- **Enfoque** conserva solo las fichas y una línea de comandos fina. Haga clic en una ficha y sus herramientas se despliegan sobre la vista; la banda se pliega de nuevo cuando elige una herramienta, o con <kbd>Esc</kbd> o un clic en otro lugar. Los paneles se ocultan. Cuando hay algo seleccionado, una pequeña tarjeta muestra qué es y sus primeras propiedades; **Todas las propiedades…** abre el panel de propiedades sobre la vista. <kbd>Tab</kbd> recupera ambos paneles y los vuelve a ocultar.

![Enfoque: la selección en una tarjeta](shots/interface-focus.webp)

En todos los modos, la barra superior mantiene **Deshacer** y **Rehacer** en su extremo derecho: con sus nombres en Cómodo, como dos flechas en Compacto y Enfoque (<kbd>Ctrl</kbd> <kbd>Z</kbd> y <kbd>Ctrl</kbd> <kbd>Y</kbd>; su información sobre herramientas indica qué deshacen).

Debajo de los modos, cada parte se puede configurar por separado: la cinta, las herramientas rápidas (o ninguna: las teclas numéricas siguen funcionando), la línea de comandos, el HUD, el mensaje y los campos de la herramienta, el plano clave, los paneles y la altura de las filas de propiedades. Cambiar una parte convierte la elección en **Personalizado**.

Lapin guarda su elección en este ordenador, para usted. La aplicación de escritorio y el navegador guardan la suya por separado. En el navegador, Lapin empieza en Compacto cuando la ventana mide menos de 800 píxeles de alto, hasta que usted elija.

## Plegar los paneles laterales

En todos los modos, el explorador del proyecto y el panel de propiedades se pliegan en un carril fino a su lado:

- el botón de arriba a la derecha del panel lo pliega, y la flecha de su carril lo despliega;
- haga doble clic en el borde entre el panel y la vista;
- <kbd>Ctrl</kbd> <kbd>B</kbd> pliega o despliega el explorador, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> las propiedades, y <kbd>Tab</kbd> ambos.

Haga clic en un icono del carril de un panel plegado para abrir el panel sobre la vista sin desplegarlo. Desaparece con <kbd>Esc</kbd> o un clic en otro lugar; su chincheta lo mantiene abierto. Arrastre el borde de un panel para ensancharlo; Lapin recuerda el ancho.

![Un explorador plegado abierto sobre la vista desde su carril](shots/browser-overlay.webp)

## Métodos abreviados de teclado

**Vista → Métodos abreviados de teclado…** (<kbd>KS</kbd>) enumera todos los comandos a los que llega el teclado, por ficha. Cada fila muestra los métodos abreviados del comando.

![Los métodos abreviados de teclado, buscando "wall"](shots/shortcuts-dialog.webp)

- **Busque** por el nombre de un comando o por una tecla.
- **+** añade un método abreviado: pulse una combinación (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), o escriba hasta tres letras y pulse <kbd>Enter</kbd>. <kbd>Esc</kbd> detiene la captura.
- Si otro comando ya tiene ese método abreviado, Lapin le dice cuál. **Reasignar** se lo quita a ese comando, **Mantener ambos** lo deja a los dos (gana el primero de la lista), **Cancelar** no cambia nada.
- **×** elimina un método abreviado. La flecha al final de una fila devuelve ese comando a como lo tiene el conjunto, y **Restablecer todo** lo hace para todos los comandos.
- **Conjunto** elige el juego del que parte: **Estilo Revit** (el predeterminado: las letras habituales de Revit, donde Lapin tiene el comando) o **Lapin clásico** (las letras propias de Lapin de antes).
- **Exportar…** escribe sus métodos abreviados en un archivo, e **Importar…** lee uno, para otro ordenador o para un colega.

Las informaciones sobre herramientas, los menús y la paleta de comandos (<kbd>Ctrl</kbd> <kbd>K</kbd>) muestran siempre los métodos abreviados tal como están ahora.

### Las letras y la línea de comandos

Las letras como <kbd>WA</kbd> funcionan cuando ningún campo tiene el foco y la línea de comandos está vacía: escríbalas sobre la vista. El comando se ejecuta al instante. Cuando un comando de la línea de comandos empieza por las mismas letras (<kbd>DI</kbd> y <kbd>DIMADD</kbd>), Lapin espera un momento: siga escribiendo para escribir el comando más largo, o pulse <kbd>Enter</kbd> o <kbd>Space</kbd> para ejecutar ahora el más corto. Mientras un comando pide un punto o un valor, las letras y los dígitos van a él. Los comandos de la línea de comandos como <kbd>L</kbd>, <kbd>M</kbd> o <kbd>TRIM</kbd> seguidos de <kbd>Enter</kbd> funcionan en todos los conjuntos. <kbd>Esc</kbd> siempre cancela.

### Los métodos abreviados predeterminados

Estos son los métodos abreviados de estilo Revit, tal como Lapin los tiene ahora:

<!-- shortcuts -->

Las teclas numéricas <kbd>1</kbd>–<kbd>9</kbd> eligen las herramientas rápidas.
