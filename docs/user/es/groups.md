---
title: Grupos y limpieza
order: 112
summary: Mantenga elementos juntos en un grupo, abra un grupo para editarlo, seleccione por región o con lazo en 3D y oculte lo que estorba.
---

## Grupos

Un grupo mantiene elementos juntos. Selecciónelos y haga clic en **Modificar → Crear grupo** (<kbd>GP</kbd>). Un clic en cualquier miembro selecciona entonces todo el grupo, en plantas y en 3D. El grupo se desplaza, copia, gira, refleja y borra como uno solo, y una copia es un grupo propio. En IFC un grupo es un IfcGroup, así que otros programas también lo ven.

El menú del clic derecho en una planta, una sección o un alzado tiene los mismos comandos: **Crear grupo** para la selección y, sobre un grupo, **Desagrupar** y **Editar grupo**. También se agrupan las líneas, textos y sombreados dibujados en una vista, entre ellos las líneas de un plano DXF importado, de modo que un dibujo entero se mueve como uno solo.

![Líneas dibujadas en la planta, agrupadas desde el menú del clic derecho](shots/group-drafting.webp)

Las propiedades de un grupo seleccionado indican cuántos elementos tiene, con tres botones: **Editar grupo**, **Desagrupar** y **Seleccionar miembros**.

![Un grupo seleccionado: sus miembros resaltados, sus propiedades](shots/group-selected.webp)

## Editar un grupo

Haga doble clic en un miembro, o haga clic en **Editar grupo** (<kbd>EG</kbd>). El resto del modelo se dibuja atenuado y no se puede seleccionar, y un clic selecciona un miembro cada vez. Borre, desplace o cambie miembros como siempre. **Quitar del grupo** saca los miembros seleccionados. **Hecho**, o <kbd>Esc</kbd>, cierra el grupo. Un grupo desaparece cuando se borra su último miembro.

![Un grupo abierto para editar: el muro del jardín, fuera de él, atenuado](shots/group-edit.webp)

## Profundizar en conjuntos

Algunos elementos son partes de un todo: los pilares, dinteles y correas de una nave de acero, las partes de un ensamblaje (una caja de Studio, un ensamblaje leído de un archivo IFC) y los miembros de un grupo. **Vista → Profundizar en conjuntos**, desactivado al principio, los elige tal como está construido el todo. El primer clic en una parte selecciona el todo superior: el grupo o la nave. Mientras está seleccionado, un clic dentro de él selecciona la parte un nivel más abajo, camino de lo que ha pulsado, y cada clic siguiente baja otro nivel, hasta el propio elemento. Un clic en otra parte del mismo todo la selecciona en el mismo nivel. <kbd>Esc</kbd> sube un nivel y, arriba, suelta la selección; un clic en el vacío también la suelta. <kbd>Ctrl</kbd>+clic (<kbd>Cmd</kbd>+clic en un Mac) selecciona el elemento directamente, y <kbd>Mayús</kbd> añade el todo superior a la selección. Una ruta sobre la vista nombra los niveles, de la planta a la selección: haga clic en uno para seleccionarlo. Mientras está seleccionada una parte dentro de un todo, el resto del modelo se dibuja algo atenuado. Funciona igual en 3D, plantas, secciones y alzados. Un cuadro o un lazo de selección toma los todos superiores. Un doble clic sigue abriendo un grupo para editarlo; dentro de él, la profundización empieza bajo el grupo. La elección se guarda en este ordenador y también está en **Vista → Configuración de vista**.

![Dentro de una nave de acero: el dintel seleccionado, la ruta sobre el 3D](shots/drill-select.webp)

## Archivos importados

Cada archivo IFC, Fragments o de malla que importe entra como un solo grupo con el nombre del archivo. Desmarque **Como un solo grupo** en la importación IFC para traer los elementos sueltos. Un archivo vinculado no se agrupa: ya es un vínculo, de solo lectura (vea [Importar y exportar](import-export.md)). Los elementos leídos de un archivo como referencias (de un IFC, de una malla) se desplazan, giran, reflejan y copian con su grupo como cualquier elemento; su forma sigue siendo la del archivo.

## Seleccionar una región en 3D

**Vista → Seleccionar región** (<kbd>SR</kbd>) convierte un arrastre con el botón izquierdo en la vista 3D en una selección, como en las plantas. Arrastrada a la derecha, una ventana toma lo que queda entero dentro. Arrastrada a la izquierda, una captura toma lo que toca. Pulse <kbd>Espacio</kbd> mientras arrastra para dibujar un lazo, y <kbd>Mayús</kbd> para añadir a la selección. Mientras está activa, el botón derecho gira la vista y el central la desplaza. La **×** de su etiqueta, o <kbd>SR</kbd> de nuevo, la desactiva.

![Una captura arrastrada sobre la casa en 3D](shots/select-region-3d.webp)

## Ocultar por ahora

**Vista → Ocultar** (<kbd>HH</kbd>) oculta la selección en la vista 3D. **Aislar** (<kbd>HI</kbd>) oculta todo lo demás. **Mostrar todo** (<kbd>HR</kbd>), o la **×** de la etiqueta, lo devuelve todo. Nada se guarda ni se borra: es una forma de llegar a lo que hay dentro de un modelo importado mientras lo limpia.
