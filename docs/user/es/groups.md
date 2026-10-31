---
title: Grupos y limpieza
order: 112
summary: Mantenga elementos juntos en un grupo, abra un grupo para editarlo, seleccione por región o con lazo en 3D y oculte lo que estorba.
---

## Grupos

Un grupo mantiene elementos juntos. Selecciónelos y haga clic en **Modificar → Crear grupo** (<kbd>GP</kbd>). Un clic en cualquier miembro selecciona entonces todo el grupo, en plantas y en 3D. El grupo se desplaza, copia, gira, refleja y borra como uno solo, y una copia es un grupo propio. En IFC un grupo es un IfcGroup, así que otros programas también lo ven.

Las propiedades de un grupo seleccionado indican cuántos elementos tiene, con tres botones: **Editar grupo**, **Desagrupar** y **Seleccionar miembros**.

![Un grupo seleccionado: sus miembros resaltados, sus propiedades](shots/group-selected.webp)

## Editar un grupo

Haga doble clic en un miembro, o haga clic en **Editar grupo** (<kbd>EG</kbd>). El resto del modelo se dibuja atenuado y no se puede seleccionar, y un clic selecciona un miembro cada vez. Borre, desplace o cambie miembros como siempre. **Quitar del grupo** saca los miembros seleccionados. **Hecho**, o <kbd>Esc</kbd>, cierra el grupo. Un grupo desaparece cuando se borra su último miembro.

![Un grupo abierto para editar: el muro del jardín, fuera de él, atenuado](shots/group-edit.webp)

## Archivos importados

Cada archivo IFC, Fragments o de malla que importe entra como un solo grupo con el nombre del archivo. Desmarque **Como un solo grupo** en la importación IFC para traer los elementos sueltos. Un archivo vinculado no se agrupa: ya es un vínculo, de solo lectura (vea [Importar y exportar](import-export.md)). Los elementos leídos de un archivo IFC como referencias se quedan donde los puso el archivo: un grupo de ellos se puede limpiar y borrar, no desplazar.

## Seleccionar una región en 3D

**Vista → Seleccionar región** (<kbd>SR</kbd>) convierte un arrastre con el botón izquierdo en la vista 3D en una selección, como en las plantas. Arrastrada a la derecha, una ventana toma lo que queda entero dentro. Arrastrada a la izquierda, una captura toma lo que toca. Pulse <kbd>Espacio</kbd> mientras arrastra para dibujar un lazo, y <kbd>Mayús</kbd> para añadir a la selección. Mientras está activa, el botón derecho gira la vista y el central la desplaza. La **×** de su etiqueta, o <kbd>SR</kbd> de nuevo, la desactiva.

![Una captura arrastrada sobre la casa en 3D](shots/select-region-3d.webp)

## Ocultar por ahora

**Vista → Ocultar** (<kbd>HH</kbd>) oculta la selección en la vista 3D. **Aislar** (<kbd>HI</kbd>) oculta todo lo demás. **Mostrar todo** (<kbd>HR</kbd>), o la **×** de la etiqueta, lo devuelve todo. Nada se guarda ni se borra: es una forma de llegar a lo que hay dentro de un modelo importado mientras lo limpia.
