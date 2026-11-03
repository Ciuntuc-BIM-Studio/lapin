---
title: Tablas y gráficos
order: 95
summary: Liste puertas, ventanas, dinteles, habitaciones y todo lo que cuenta un módulo; añada filas y columnas propias, escriba sobre los valores del modelo y haga un gráfico de la tabla.
---

## Crear una tabla

Una tabla es una lista que rellena el modelo: puertas y ventanas, dinteles, habitaciones, armaduras, madera, encofrados y más. Haga clic en **+** junto a **Tablas** en el navegador del proyecto y elija lo que lista, o use la herramienta de un módulo (Madera → Lista de madera, Medios auxiliares → Tabla de encofrados…). Haga doble clic en ella en el navegador para abrirla en una pestaña. Una tabla sigue al modelo: mueva un muro, añada una ventana, y la tabla cambia con ellos.

Las tablas de **carpintería**, de **puertas**, de **ventanas** y de **dinteles** muestran una ficha por tipo, con su dibujo (la vista interior por defecto) y sus valores fila a fila. Las demás tablas son una tabla simple, una fila por elemento y una línea de totales al final.

![Una tabla de carpintería con sus filas de resistencia al fuego y fabricante](shots/joinery-schedule.webp)

## Resistencia al fuego, fabricantes y otros parámetros

Los parámetros de una puerta o de una ventana entran solos en la tabla de carpintería:

- **Resistencia al fuego.** La fila **Resistencia al fuego** lee un parámetro llamado Fire rating, Fire Rated, FireRating o Fire resistance. Póngalo en el tipo, para que todas las puertas del tipo lo tengan. En **Arquitectura → Puertas y ventanas**, **Parámetros · en tablas** → **+ Resistencia al fuego** añade el FireRating del tipo a Pset_DoorCommon (Pset_WindowCommon en una ventana), que es donde lo guarda IFC. También se muestra un parámetro que usted añada a una sola puerta, en sus propiedades.
- **Cualquier otro parámetro.** **+ Fabricante**, **+ Código de producto**, **+ Sitio web**, o un parámetro propio, se convierte en una fila propia en cada tabla de carpintería. El valor propio de una puerta se muestra junto al de su tipo, cuando las puertas de un tipo difieren.
- **Escrito en la tabla.** Un valor escrito en la fila de un parámetro (en cursiva) pasa al tipo, y todas las puertas del tipo lo siguen. Vacíelo para quitar el parámetro del tipo.

![Los parámetros del tipo en el configurador de puertas y ventanas](shots/joinery-params.webp)

**Tipos sin usar.** Un tipo que ninguna puerta o ventana del modelo usa queda fuera de la tabla. **Tipos sin usar** en la barra de la tabla los muestra de nuevo, con cuántos hay.

## Filas y columnas propias

Cualquier tabla se puede completar a mano, como una hoja de cálculo:

- **Fila** añade una fila propia. En una tabla de carpintería tiene una etiqueta, y usted escribe su valor para cada tipo (Proveedor, Fecha de entrega…). En cualquier otra tabla es una fila entera, escrita celda a celda (una terraza no modelada, una partida); se sitúa sobre la línea de totales.
- **Columna** añade una columna propia. En una tabla de carpintería es una ficha sin tipo detrás, con todos los valores escritos (una puerta de garaje pedida aparte). En cualquier otra tabla es una columna después de las del modelo (Acabado del suelo, Notas…).
- **Cualquier celda** de la tabla bajo el dibujo se puede sobrescribir. Un valor escrito sobre el del modelo queda marcado; **↺** devuelve el valor del modelo. Lo que escribe se queda con su fila: las notas de una habitación la siguen cuando la lista se ordena o la habitación se renombra.
- **×** junto a una fila o una columna propia la quita, con lo que tenía escrito.
- Las flechas de la cabecera de una tabla **ordenan** sus filas por esa columna: una vez de forma ascendente, otra vez descendente, una tercera vez en el orden del modelo. La línea de totales queda la última.

Todo se imprime: en una lámina, en el PDF y el DXF, y en las exportaciones CSV y XLSX.

## Gráficos

**Gráficos** en la barra de la tabla abre los gráficos junto a la tabla. **Nuevo gráfico** crea uno a partir de la tabla:

- **Columnas**, **Barras**, **Circular** o **Línea**.
- **Categorías de**: la columna que da nombre a cada barra o sector (Uso, Nombre, Marca, Planta…).
- **Cómo**: **Suma**, **Media**, **Mínimo** o **Máximo** de una columna (Área, Total, Volumen…), o **Número de filas**.
- **Como máximo**: conserva solo las categorías mayores y suma el resto como Otros.
- **Título**: un título propio, o el que crea él (Suma de Área por Uso).

El motor dibuja cada gráfico después de las tablas, así que se ve en la tabla, en una lámina que coloca la tabla y en el PDF, exactamente igual. Sigue al modelo y lo que usted escribió: cambie el área de una habitación o añada una fila propia, y el gráfico cambia con ellos. **✎** cambia un gráfico, **×** lo quita.

![Una tabla de habitaciones con una fila propia y un gráfico circular de las áreas por uso](shots/schedule-chart.webp)
