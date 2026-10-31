---
title: Losas y cubiertas
order: 40
summary: Dibuje losas y cubiertas por su contorno, elija la forma y los bordes de una cubierta, y abra huecos.
---

## Losas

Elija **Arquitectura → Losa** (<kbd>SB</kbd>) y haga clic en las esquinas de su contorno. Cierre el contorno en el primer punto o pulse <kbd>Enter</kbd>. Una losa también está hecha de capas, que se definen en **Arquitectura → Tipos de losa**.

Para cambiar el contorno, seleccione la losa y arrastre sus esquinas. **Hueco en losa** (<kbd>SH</kbd>) abre un hueco en una losa, para una escalera o un patinillo.

Una losa dibujada fuera de los muros, delante de una puerta, es un porche. Para los peldaños que suben a él, consulte [Escaleras](stairs.md).

## Cubiertas

Elija **Arquitectura → Cubierta** (<kbd>RF</kbd>) y haga clic en el contorno de los muros que quedan debajo. Lapin construye la cubierta sobre ese contorno, en la parte superior de su nivel.

![Una casa con cubierta a dos aguas en la vista 3D](shots/house-3d.webp)

Seleccione la cubierta para cambiarla en el panel de propiedades:

- **Forma.** A cuatro aguas, a dos aguas, a un agua, mansarda, bóveda o plana. Una cubierta abovedada es una bóveda curva entre dos hastiales; **Bóveda: flecha en la clave** fija cuánto sube en la clave, como máximo la mitad de su luz.
- **Pendiente y vuelo** de toda la cubierta.
- **Cada borde** con su propio tipo (alero o hastial), pendiente y vuelo. Una mansarda con bordes de hastial es una cubierta holandesa.
- **Estilo.** Los estilos de cubierta dan un aspecto completo en una sola elección: el material de cobertura, los aleros, los canalones y las bajantes.

![Las propiedades de una cubierta](shots/roof-properties.webp)

Los muros del nivel de la cubierta se detienen bajo ella y suben por sí solos hasta sus hastiales, también bajo una cubierta curva. Un muro de otro nivel sigue la cubierta tras **Arquitectura → Enlazar a cubierta** (<kbd>AR</kbd>): seleccione los muros y haga clic en la cubierta. La misma herramienta acepta cualquier otro cuerpo sobre los muros: un sólido de Studio, una losa, una viga, un elemento importado de IFC. Los muros suben hasta su cara inferior y se cortan a lo largo de ella, sea cual sea su forma. **Desenlazar de la cubierta** lo deshace.

**Ventana de cubierta** y **Buhardilla** añaden huecos a una cubierta.

## Falsos techos

**Arquitectura → Falso techo** (<kbd>CL</kbd>) dibuja un falso techo sobre un contorno que se marca con clics; **Falso techo en un local** llena el local donde se hace clic, siguiendo su contorno. Un falso techo cuelga a su **Altura** sobre la planta (2700 mm salvo que se cambie en sus propiedades).

Su tipo, en **Arquitectura → Tipos de falso techo**, fija el **Sistema**: **Placas sobre perfilería vista** (ancho, largo y espesor de la placa, ancho visto, altura y giro de la perfilería, cuelgues hasta la estructura con su separación) o **Placa de yeso, continuo**. La perfilería se centra en el techo, así que las placas cortadas en los bordes salen iguales.

**Panel de luz** y **Difusor de aire** ponen uno en la celda donde se hace clic; un clic en la misma celda con la otra herramienta lo cambia. En el panel de propiedades, **Distribuir** pone paneles de luz o difusores cada tantas celdas, y **Vaciar** los quita. Las tablas cuentan las placas enteras, los paneles de luz y los difusores, y el techo va a IFC como IfcCovering de tipo CEILING.

![Las propiedades de un falso techo: su tipo, su altura y sus elementos](shots/ceiling-properties.webp)

![Falsos techos registrables con paneles de luz y difusores, en 3D](shots/ceilings-3d.webp)
