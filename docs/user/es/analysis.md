---
title: Análisis estructural
order: 52
summary: Ejecute un análisis estático lineal del pórtico con sus cargas, lea los diagramas de esfuerzos, momentos y deformada en 3D y fije las liberaciones de los extremos de las barras.
---

**Ejecutar análisis** del grupo **Cargas** de la pestaña **Estructura** (y del cuadro Cargas) analiza las barras, losas y muros del modelo con las cargas y combinaciones de [Cargas](loads.md) y muestra los esfuerzos en ellas.

![El grupo Cargas, con Ejecutar análisis](shots/ribbon-analysis.webp)

> Los resultados son **orientativos**. Las cargas, las combinaciones y el análisis debe comprobarlos usted antes de basarse en ellos.

## Qué se analiza

- Las **barras** (pilares, vigas, elementos de acero y de madera) entran con su propia rigidez, según su material y su sección. Una barra cuyo material no tiene módulo de elasticidad se deja fuera y se enumera.
- Los **forjados y losas de hormigón** se analizan como **láminas**: cada uno se divide en elementos pequeños (una malla, de 0,5 m por defecto) que flectan y se estiran, y comparten sus nudos con las vigas, pilares y muros que encuentran. La losa lleva sus cargas por sí misma a lo que la sostiene, por su rigidez y la de ellos. Una viga bajo una losa conserva su propio eje por debajo y trabaja con la losa como una viga en T.
- Los **muros de carga de hormigón** también son láminas: se apoyan en sus cimentaciones (un muro sin nada modelado debajo se sujeta a lo largo de su pie) y llevan los forjados que descansan en ellos, y el viento u otras cargas laterales en su plano.
- Los **muros de fábrica** y los de otros materiales son apoyos: sostienen las losas y barras que descansan en ellos, pero no se analizan como muros.
- Los **forjados de otros materiales** (un forjado de madera) pasan sus cargas a las barras y muros que tienen debajo por la **regla de 45°**: las bisectrices de las esquinas dividen la losa en las zonas que lleva cada lado, y cada una se convierte en una carga trapezoidal o triangular sobre la barra de debajo.
- Las **cubiertas con estructura** pasan la carga a cada cabio por su **ancho tributario**, hasta la mitad de la distancia a los vecinos; la nieve se toma por proyección horizontal.
- Las **varillas y tirantes** (una barra redonda maciza que arriostra, un tirante o un cable) trabajan axialmente: están articulados en sus extremos y su peso va a los extremos, de modo que no flectan como vigas. El campo **Barra con esfuerzo axil** en las propiedades de **Análisis** de una barra lo cambia: **Según su sección y uso**, **Sí (una varilla o tirante)** o **No (una viga)**.
- Las **vigas de distinto canto** que llegan a un pilar conservan cada una su nivel; el pilar tiene un solo nudo, y las vigas se atan a él con un brazo rígido.
- El **viento sobre las fachadas** va a las vigas, travesaños y bordes de forjado a lo largo de ellas; la franja a su pie va directamente al terreno y se lista aparte de las sumas de las barras.
- El **peso propio** de las barras se toma a lo largo de ellas, y las cargas que usted dibujó y las derivadas van donde encuentran algo debajo. Una carga que no encuentra nada se deja fuera y se indica.

## Ejecución

Elija **Ejecutar análisis**. Se abre el cuadro **Resultados del análisis**:

![El cuadro Resultados del análisis](shots/analysis-results.webp)

- El resumen: las barras, los elementos de lámina, nudos e incógnitas del cálculo y lo que tardó.
- Para cada hipótesis, la **suma de sus cargas** frente a la **suma de las reacciones**, y si **equilibran** (una marca) o no (una cruz: no se fíe de esos resultados). Las cargas sin asignar se dan en kN.
- Lo que se **dejó fuera** del análisis, y por qué.
- Los valores **máximos y mínimos** de esfuerzos y momentos, el desplazamiento y la flecha máximos (con su relación con la luz, L/300) de la hipótesis, combinación o envolvente que elija; debajo, los valores máximos y mínimos en las **losas y muros**, y dónde están.
- **Láminas**: qué losas y muros se analizan como láminas (**Forjados y muros**, **Solo forjados**, **Solo muros** o **Ninguno**: las losas por la regla de 45° y los muros como apoyos) y el tamaño de la **Malla** en metros. Una malla más fina es más exacta y más lenta; una pieza estrecha siempre recibe al menos cuatro elementos a lo ancho. Un cambio es un solo paso de deshacer; **Ejecutar de nuevo** lo recoge.

**Mostrar los diagramas** cierra el cuadro y muestra los resultados en la vista 3D.

## Los diagramas en 3D

Una tarjeta sobre la vista 3D elige lo que se dibuja:

![El momento flector de la envolvente en estado límite último](shots/analysis-moments.webp)

- **Qué**: una hipótesis de carga, una combinación o la **envolvente** de las combinaciones en estado límite último (ELU) o de servicio (ELS): el valor máximo y mínimo en cada punto.
- **Magnitud**: **N** esfuerzo axil (tracción positiva), **Vy** y **Vz** esfuerzos cortantes, **T** torsión, **My** y **Mz** momentos flectores (**Mz** es el eje fuerte de una viga horizontal, positivo si tracciona abajo) o la **Deformada**.
- **Escala**: el tamaño del diagrama en pantalla; el modelo no cambia.
- **Reacciones**: flechas en los apoyos con sus valores en kN.

Cada barra muestra su línea de base, la curva, rayas entre ellas y los valores en los extremos; el rojo es positivo y el azul negativo. Los momentos se dibujan del lado en que la barra está traccionada.

![La forma deformada](shots/analysis-deformed.webp)

## Losas y muros

Cuando las losas o los muros se analizan como láminas, la tarjeta tiene una segunda fila de botones, **Forjados y muros**, y las láminas se colorean en bandas según la magnitud que elija (un segundo clic en ella quita los colores):

![El momento my del forjado, en bandas](shots/analysis-slab.webp)

- **w**: el desplazamiento perpendicular a la losa o al muro (positivo hacia arriba en una losa).
- **mx**, **my**: los momentos flectores en las direcciones x e y de la losa (X e Y globales en una losa; a lo largo y hacia arriba en un muro), en kN·m por metro. En una losa, **positivo significa que su cara inferior está traccionada** (el vano), negativo su cara superior (sobre un apoyo); en un muro, la cara a la derecha de su sentido.
- **mxy**: el momento torsor; **nx**, **ny**, **nxy**: los esfuerzos en el plano de la losa (tracción positiva), en kN por metro; **vx**, **vy**: los esfuerzos cortantes transversales.

La leyenda de la tarjeta da los valores de cada banda y la unidad:

![La leyenda de los contornos](shots/analysis-legend.webp)

Las mismas bandas se dibujan en la planta de la planta de la losa, para la pantalla; las láminas aún no las imprimen. En una envolvente, cada punto muestra el mayor en valor absoluto entre su valor mínimo y su máximo.

## Liberaciones de los extremos

Seleccione un pilar, una viga o un elemento: sus propiedades tienen una sección **Análisis** con las liberaciones **Inicio (i)** y **Fin (j)**.

![La sección Análisis de una viga](shots/analysis-bar.webp)

- **Por defecto**: lo que dice la unión (una chapa, una cartela o un arriostramiento articula el elemento), un elemento de madera de cubierta articulado en su eje fuerte, cualquier otra barra empotrada.
- **Empotrado**: el extremo transmite momentos. **Articulado**: gira libremente alrededor de los dos ejes de flexión.
- **Personalizado**: seis casillas, una por grado de libertad (desplazamiento a lo largo de la barra, a lo largo de cada eje de cortante, torsión, giro alrededor de cada eje de flexión); un grado marcado queda liberado.

Un cambio es un solo paso de deshacer.

## Cuando el modelo cambia

Los resultados se calculan a partir del modelo tal como está y nunca se guardan. Cuando cambia el modelo, los diagramas desaparecen de la vista y la tarjeta lo dice:

![La tarjeta tras un cambio](shots/analysis-pick.webp)

**Ejecutar de nuevo** analiza el modelo nuevo.

## Un mecanismo

Si la estructura puede moverse sin resistencia (demasiadas liberaciones o una parte que nada sujeta), el análisis se detiene y dice dónde:

![Un mecanismo](shots/analysis-mechanism.webp)

Nombra el nudo, el grado de libertad y las barras implicadas; **Seleccionar los elementos implicados** los selecciona para que pueda corregirlos, normalmente empotrando un extremo o añadiendo un apoyo.
