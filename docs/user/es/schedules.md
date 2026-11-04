---
title: Tablas y gráficos
order: 95
summary: Liste puertas, ventanas, dinteles, habitaciones y todo lo que cuenta un módulo; añada filas y columnas propias, escriba sobre los valores del modelo y haga un gráfico de la tabla.
---

## Obtener cantidades

Todo lo que Lapin modela se mide a partir de su geometría: la longitud, la altura, el área lateral bruta y neta y el volumen de un muro, el área y el volumen de cada capa, las áreas y el volumen de un forjado, el área y el perímetro de un local, el número de puertas. Hay tres caminos:

- **Un elemento.** Selecciónalo: la sección **Cantidades** del panel de propiedades muestra lo que mide, y después cada capa o parte.
- **Todo el modelo.** **Cantidades → Mediciones** crea (o abre) las mediciones: todos los elementos que el modelo mide, sumados fila a fila, con el número, la longitud, el área bruta y neta, el volumen bruto y neto y el peso. Su barra elige qué se suma junto: **Por clase y tipo** (por defecto: los muros de cada tipo de muro, los forjados de cada tipo…), **Por material** (cada capa bajo su material: el ladrillo, el enlucido y el aislamiento de los muros por separado, el hormigón de los dinteles), por planta o combinados. **Todos los elementos** elige qué clases lista (solo muros y forjados, por ejemplo). Las mediciones siguen al modelo como cualquier tabla y admiten filas y columnas propias (una partida alzada, un precio unitario).
- **Cualquier otra lista.** **Cantidades → Tablas…** crea cualquier tabla que ofrezca la edición: locales, acabados, carpintería, composiciones de tipos, madera, armaduras, encofrados, acero… La misma lista está en **+** junto a **Tablas** en el navegador del proyecto.

![Las mediciones agrupadas por clase y material](shots/quantity-takeoff.webp)

**Cantidades → Exportar XLSX** guarda directamente las mediciones como libro de cálculo (en el navegador se descarga).

## Cantidades en una lámina

Una tabla se coloca en una lámina como una vista y se dibuja allí con todas sus filas y valores, sus totales y sus gráficos:

- **Colocar en lámina…** en la barra de la tabla: **Lámina nueva** crea una lámina A3 con la tabla y la abre, o elige una lámina que ya tengas.
- O arrastra la tabla desde el navegador del proyecto a una lámina abierta: queda donde la sueltas. Plantas, secciones y alzados se arrastran igual.
- O **Colocar vista…** en la barra de la propia lámina.

En la lámina, arrastra la tabla para moverla; una tabla más larga que el marco sigue en más páginas (la lámina lo indica, y **colocarla en una lámina nueva** la continúa). La tabla sigue al modelo: añade un muro y los números de la lámina cambian. Se imprime con **Imprimir** y va al PDF y al DXF de la lámina tal como se dibuja.

![Las mediciones colocadas en una lámina nueva](shots/takeoff-sheet.webp)

## Crear una tabla

Una tabla es una lista que rellena el modelo: puertas y ventanas, dinteles, habitaciones, armaduras, madera, encofrados y más. Haga clic en **+** junto a **Tablas** en el navegador del proyecto y elija lo que lista, o use la herramienta de un módulo (Madera → Lista de madera, Medios auxiliares → Tabla de encofrados…). Haga doble clic en ella en el navegador para abrirla en una pestaña. Una tabla sigue al modelo: mueva un muro, añada una ventana, y la tabla cambia con ellos.

Las tablas de **carpintería**, de **puertas**, de **ventanas** y de **dinteles** muestran una ficha por tipo, con su dibujo (la vista interior por defecto) y sus valores fila a fila. Las demás tablas son una tabla simple, una fila por elemento y una línea de totales al final.

Una puerta o ventana volteada a la otra mano de su tipo (**Mano**, o una sola flecha de giro; volteada en los dos sentidos solo está girada) es la imagen en espejo de su tipo y recibe una ficha propia: su marca termina en **-M** (D-M), su nombre en «(mirrored)», y su fila **Opening**, su dibujo y sus cantidades son los de la otra mano. Si la vuelve a voltear, regresa a la ficha de su tipo. Izquierda y derecha son las de IFC: las bisagras vistas por quien la empuja para abrirla. Los dibujos muestran cada puerta tal como está en el modelo, vista desde el interior, el lado hacia el que abre (así, las bisagras de una puerta *swing left* quedan a la derecha). Una nota bajo las fichas lo indica.

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
- **Cualquier celda** de la tabla bajo el dibujo se puede sobrescribir. Un valor escrito sobre el del modelo queda marcado; **↺** devuelve el valor del modelo. Lo que escribe se queda con su fila: las notas de una habitación la siguen cuando la lista se ordena o la habitación se renombra. En una tabla **Rooms**, un número, nombre, función, departamento, acabado del suelo o altura escritos en la fila de una habitación van a la habitación misma, como si se escribieran en sus propiedades; la etiqueta de la planta y las demás tablas los siguen.
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

## Composición de los tipos

La tabla **Composición de los tipos** (**+** → **Composición de los tipos**) lista cada tipo de muro y de forjado: su marca, su espesor total, cuántos elementos lo usan y luego sus capas en orden, con sus materiales y espesores.

**Dibujos de la composición**, en su barra, dibuja cada tipo tras la tabla, cortado por sus capas a la escala de la tabla (1:50 si no eliges otra): un muro como lo corta una planta, un forjado como lo corta una sección, la capa 1 arriba. Cada capa se rellena y se sombrea como su material en las secciones, las caras del núcleo se dibujan más gruesas, el espesor total se acota y cada capa se nombra a su lado como la nombra la tabla. Los dibujos se imprimen con la tabla, en una lámina y en el PDF. Una tabla de composición nueva los dibuja; desmarca **Dibujos de la composición** para dejar solo la tabla.

Los dibujos **En cada ficha** (vista interior, vista exterior, planta, sección), en la **Configuración** de una tabla, pertenecen a las fichas de las tablas de carpintería; una tabla simple no tiene fichas, así que allí no se ofrecen.

![La tabla de composición de los tipos con la composición de cada tipo](shots/type-assemblies.webp)

## Molduras de muro

La tabla **Molduras de muro** (**Arquitectura → Tabla de molduras de muro**, o **Cantidades → Tablas…** → **Molduras de muro**) suma las molduras y ranuras de muro del proyecto por perfil, clase y material: cuántas hay, sus piezas (una moldura cortada por huecos son varias), su longitud a lo largo de las caras y su volumen; en una ranura, el volumen que quita al muro. Un total para las molduras y otro para las ranuras cierran la tabla. Su barra las suma primero **por perfil**, **por planta** o **por tipo de muro**. Los retornos cuentan en el volumen, no en la longitud.

## Exportar

**CSV** y **XLSX** en la barra exportan la tabla como hoja de cálculo (un XLSX tiene una hoja por cada tabla). En la aplicación de escritorio, Lapin pregunta dónde guardar el archivo y propone el nombre de la tabla; en un navegador el archivo se descarga. Los tipos de muro y de forjado exportados se guardan del mismo modo.
