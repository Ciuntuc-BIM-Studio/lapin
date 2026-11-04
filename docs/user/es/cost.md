---
title: Presupuesto
order: 98
summary: El presupuesto desde el modelo: tus listas de precios importadas de CSV o Excel, partidas en los tipos y reglas en los materiales, cantidades de las mediciones por precios unitarios desglosados en material, mano de obra, maquinaria y transporte, gastos generales, beneficio e IVA, en RON o EUR; los formularios rumanos F3, F2 y C6 a C9 en láminas, en Excel y en PDF; presupuestos IFC.
---

## La pestaña Costes

![La pestaña Costes](shots/ribbon-cost.webp)

La pestaña que se llamaba **Cantidades** ahora es **Costes**. Conserva las cantidades y añade el presupuesto:

- **Mediciones**, **Tablas…** y **Exportar XLSX**: las cantidades del modelo (ver [Tablas y gráficos](schedules.md)).
- **Listas de precios…**: tus precios, importados de un archivo CSV o Excel (más abajo).
- **Asignar partidas…**: qué partida da precio a cada tipo y a cada material (más abajo).
- **Presupuesto…**: abre el presupuesto bajo la vista (se crea la primera vez).
- **Formulario en lámina**, **Presupuesto XLSX** y **Presupuesto PDF**: el presupuesto y sus formularios en una lámina, en un libro de cálculo o en un PDF (más abajo).
- **Coste en el tiempo**: el presupuesto puesto sobre el programa de obra, la curva S y la tabla mensual (abajo).

El presupuesto se calcula desde el modelo cada vez que este cambia. Un muro que dibujas ahora está en el presupuesto al momento, con sus cantidades. Solo se guardan los precios, las partidas de los tipos, las reglas y tus líneas propias.

## Listas de precios

![Importar una lista de precios: el sentido de cada columna deducido de su encabezado](shots/price-import.webp)

Los precios son tuyos: Lapin no trae ningún catálogo de precios ni de normas. **Costes → Listas de precios… → Importar…** lee un archivo CSV o XLSX, por ejemplo una exportación de tu programa de presupuestos o la lista de un proveedor. Lapin muestra las primeras filas del archivo bajo sus encabezados. Sobre cada columna está lo que Lapin dedujo que contiene, a partir del encabezado, en inglés o en rumano:

- **Código** y **Descripción** (*Cod*, *Simbol*, *Denumire*), **Unidad** (*U.M.*: m³ o mc, m² o mp, m, pcs o buc, kg, t, 100 m²).
- El precio unitario desglosado en **Material**, **Mano de obra**, **Maquinaria** y **Transporte** (*Material*, *Manoperă*, *Utilaj*, *Transport*), o un solo **Precio unitario (sin desglosar)**, que entonces cuenta como material.
- Opcional: **Horas-hombre por unidad** (*Normă de timp*) y **Oficio** (*Meserie*), para la lista de mano de obra (C6).
- Opcional: los recursos de la partida, filas que nombran la partida a la que pertenecen (**Partida a la que pertenece**), su **Tipo de recurso** (M, Man, U, T) y su **Consumo por unidad**. Una partida dada solo por sus recursos se valora por ellos, y las listas de recursos (C7 a C9) los listan uno a uno.

Cambia cualquier columna mal deducida, elige la fila en la que están los encabezados y si los precios están en RON o en EUR, y haz clic en **Importar**. La lista entra en el proyecto. Con **Guardar también en la biblioteca de este ordenador** marcado, también se queda en este ordenador: en **En este ordenador**, **Añadir al proyecto** la lleva a tu próximo proyecto.

**Plantilla** guarda un CSV con las columnas y algunas filas con los valores orientativos de Lapin, marcados como *orientativo*: rellénalo con tus precios e impórtalo. **Usar los valores orientativos** pone esos valores en el proyecto como una lista, para que puedas probar el presupuesto antes de tener los tuyos. Sustitúyelos antes de enviar un presupuesto a nadie.

## Partidas en los tipos, reglas en los materiales

![Asignar partidas: cada tipo y material con su partida](shots/map-articles.webp)

Una línea del presupuesto sale de una **partida**: un código de tu lista de precios. Hay dos maneras de dar una partida a los elementos:

- **En un tipo.** Cada elemento del tipo se valora por ella. La partida dice qué mide: el elemento entero o una parte de él (el **núcleo** de un tipo por capas, o sus **acabados**), y con qué medida (por la unidad del precio, o volumen neto o bruto, área neta o bruta, longitud, número, peso). También puede añadir un factor y la merma (%).
- **En un material (una regla).** Cada capa de ese material, en cualquier tipo, se valora por ella: una regla para el enlucido de cal da precio al enlucido de todos tus muros.

La partida del tipo va primero. Una capa que ya miden las partidas de su tipo no se valora otra vez por una regla. Una partida sobre el elemento entero lo valora todo, así que las reglas se apartan. Para un muro por capas, da al tipo una partida sobre su **núcleo** (la fábrica por volumen) y reglas para sus acabados (el enlucido por área). El muro se valora entonces capa a capa: la fábrica en **Muros**, el enlucido en **Acabados y aislamientos**.

**Costes → Asignar partidas…** lista los tipos del proyecto que usan los elementos y los materiales de sus capas. Cada fila empieza con la partida que tiene, o la que elegiste para un tipo o material del mismo nombre en un proyecto anterior, o la que propone Lapin: un código escrito en su nombre, o la partida que comparte más palabras con él (*zidărie*, *BCA*, *tencuială*, *C25/30*). Para un tipo por capas propone el núcleo. Escribe o elige un código, elige la parte y la medida, marca las filas que quieres y haz clic en **Aplicar**: un solo paso para deshacer. Tus elecciones se recuerdan en este ordenador para tus próximos proyectos.

Los tipos de encofrado y andamio de la pestaña **Medios auxiliares** ya tenían un código de partida (decisión 152): es una partida en el tipo, medida por la unidad del precio.

Las fases cuentan: un elemento que estaba antes de las obras (hecho en una fase *existente*) no se valora. Uno demolido se valora solo con la partida de su tipo cuya parte es **Su demolición (solo si se demuele)**, en el capítulo **Demolición**.

## El presupuesto

![El presupuesto: capítulos, líneas, los porcentajes y los totales](shots/estimate.webp)

**Costes → Presupuesto…** abre el presupuesto bajo la vista, como el Gantt. Sus líneas se agrupan en **capítulos** por tipo de obra: movimiento de tierras, cimentación, estructura, muros, cubierta, carpintería, acabados y aislamientos, instalaciones, medios auxiliares, otros. Una partida puede indicar un capítulo propio. **Por planta y tipo de obra** pone la planta antes de cada capítulo.

Cada línea muestra el código, la descripción y la unidad de la lista de precios, la **cantidad** del modelo (la misma que la de las mediciones), el precio unitario desglosado en **Material**, **Mano de obra**, **Maquinaria** y **Transporte**, el precio unitario y el **valor**. Haz clic en una línea para seleccionar sus elementos en las vistas. Selecciona elementos en una vista y sus líneas se marcan en el presupuesto.

Bajo las líneas están los totales, como los disponen los presupuestos rumanos:

1. **Costes directos**: la suma de las líneas.
2. **Contribuciones sobre la mano de obra (CAM)**: el 2,25 % de la mano de obra, por defecto.
3. **Gastos generales** (*cheltuieli indirecte*): el 10 % de los costes directos con las contribuciones, por defecto.
4. **Beneficio**: el 5 % de los costes directos y los gastos generales, por defecto.
5. **Total sin IVA**, el **IVA** (21 % por defecto) y el **Total con IVA**.

Cambia cualquier porcentaje en la barra de herramientas. Cada valor se redondea a dos decimales, como lo imprime un presupuesto.

El presupuesto está en **RON** o en **EUR**. **1 EUR =** es el cambio del proyecto: una lista de precios en la otra moneda se convierte con él, y la casilla de al lado muestra también cada valor en la otra moneda.

La barra de herramientas también avisa de lo que no tiene precio: **N tipo(s) sin precio** (haz clic para abrir Asignar partidas) y los códigos que no tiene ninguna lista de precios. **Una línea propia** añade una obra que el modelo no tiene (el vallado de la obra, una provisión): escribe su código, descripción, unidad, cantidad y precios. Una línea con código y sin precios los toma de la lista de precios.

## Los formularios rumanos

![La lista de cantidades de obra (F3) del mismo presupuesto](shots/estimate-f3.webp)

La lista sobre el presupuesto muestra el propio presupuesto o uno de sus formularios. Son agrupaciones del mismo presupuesto, así que suman los mismos totales. Llevan encabezados en rumano, como se presentan en Rumanía:

- **Lista de cantidades de obra (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, capítulo a capítulo, los precios unitarios y los valores desglosados en material, mano de obra, maquinaria y transporte, y los totales debajo.
- **Presupuesto por objeto (F2)**: *Deviz pe obiect*, el valor sin IVA de cada capítulo, su IVA y su valor con IVA.
- **Mano de obra (C6)**: las horas y el valor por oficio (del oficio y las horas-hombre de la lista de precios, o de sus recursos de mano de obra).
- **Materiales (C7)**, **Maquinaria (C8)** y **Transporte (C9)**: los recursos con sus cantidades, precios unitarios y valores. Una partida sin recursos cuenta como un recurso propio.

Los formularios siguen la práctica habitual según la metodología de la Orden 863/2008. Compruébalos con lo que pide tu cliente o el programa de financiación: pregúntanos si necesitas otra disposición.

**Colocar en lámina** pone la tabla mostrada en una lámina nueva, lista para imprimir con las demás. **XLSX** guarda el presupuesto y cada formulario en un libro de cálculo, una hoja cada uno. **PDF** los guarda como PDF, A4 apaisado, listo para imprimir.

## Coste en el tiempo

![Coste en el tiempo: la curva S sobre las barras de los meses, la tabla mensual debajo](shots/cost-time.webp)

Con un programa de obra en el proyecto (la pestaña **Planificación**), el presupuesto se pone sobre él sin ningún enlace propio. El valor de cada partida se reparte entre sus elementos, y la parte de cada elemento va a las tareas que construyen la parte que mide la partida: la de la fábrica a la tarea que toma el núcleo de los muros, la del enlucido a la que toma sus acabados. Una partida sobre el elemento entero, construido en dos tareas, se reparte por igual entre ellas. Una partida propia va a la tarea que elija junto a su descripción (**En ninguna tarea** por defecto). El coste de una tarea se reparte por igual en sus días laborables: nada se gasta un fin de semana, un festivo o, en una tarea húmeda, en la pausa de invierno.

**Costes → Coste en el tiempo** abre, bajo la vista:

- la **curva S**: el coste acumulado planificado a lo largo del programa, el de la **línea base** (discontinuo) cuando hay una guardada, y el **trabajo hecho** (grueso) hasta la fecha de estado: el coste de cada tarea por su % completado, repartido desde su inicio real;
- las barras de los meses, en la escala de la derecha: planificado y hecho;
- encima, el **presupuesto a la conclusión** (los costes directos del presupuesto), el **coste a la fecha** y lo que está **sin programar**, si hay algo: partidas cuyos elementos no construye ninguna tarea (un clic abre el presupuesto);
- debajo, la **tabla mensual**: el coste de cada capítulo al mes, lo sin programar y su total.

Los valores son costes directos, como las partidas del presupuesto. El Gantt muestra el coste de cada tarea en su columna **Coste** (vea [Planificación](planning.md)).

![La curva S en una lámina](shots/cost-curve-sheet.webp)

**Colocar en lámina** pone la curva en una lámina nueva, dibujada en vectores con su leyenda. **Tabla mensual en lámina** coloca la tabla mensual, que es también el formulario **Coste por mes** de la lista del presupuesto: los capítulos mes a mes, luego los costes directos, los totales sin IVA y con IVA (repartidos en proporción), los costes acumulados y, con avance, el trabajo hecho. Cada fila suma el total del presupuesto. **XLSX** guarda la curva día a día, la tabla mensual y los costes de las tareas en un libro; **PDF** guarda la curva y la tabla mensual.

**Gestionar → Versiones → Comparar** dice, junto a los elementos y el programa, qué cambió en el presupuesto: el total con IVA, los costes directos y la diferencia, cada capítulo cuyo coste cambió, y las partidas añadidas, quitadas o cambiadas (su cantidad y su valor).

Las certificaciones mensuales (*situații de lucrări*), el valor ganado y varias líneas base son de la edición premium.

## IFC

**Archivo → Exportar IFC…** escribe el presupuesto como un **IfcCostSchedule**: sus capítulos y líneas como **IfcCostItem**, la cantidad de cada línea y sus precios unitarios como cuatro **IfcCostValue** (Material, Mano de obra, Maquinaria, Transporte), los elementos que mide cada línea asignados a ella (**IfcRelAssignsToControl**), la moneda como la **IfcMonetaryUnit** del proyecto. Importado, un presupuesto conserva las líneas como las tiene el archivo, con los mismos totales. Marca **Líneas del modelo** para añadir las líneas que da tu modelo.

Con un programa de obra, cada partida se asigna también a las tareas en que se ejecuta (**IfcRelAssignsToControl** hacia sus **IfcTask**). Importadas, las partidas conservan sus tareas, así que la curva S vuelve igual.
