---
title: Cargas
order: 51
summary: Hipótesis de carga, cargas superficiales, lineales y puntuales, las sobrecargas de uso de las estancias, las cargas derivadas y las combinaciones de EN 1990, para un cálculo estructural.
---

El grupo **Cargas** de la pestaña **Estructura** tiene las cargas que toma un cálculo estructural: **Hipótesis de carga…**, **Carga superficial**, **Carga lineal** y **Carga puntual**. Los valores están en kN, kN/m y kN/m².

![El grupo Cargas de la pestaña Estructura](shots/ribbon-loads.webp)

> Los valores que trae Lapin (las sobrecargas de uso de EN 1991-1-1 y los coeficientes de EN 1990, los recomendados) son **orientativos**. Pon los valores de tu anexo nacional y compruébalos antes de usar los resultados.

## Hipótesis de carga

**Hipótesis de carga…** abre el cuadro Cargas. **Añadir hipótesis estándar** crea G1 (peso propio), G2 (acabados y tabiques), Q-A (sobrecarga de uso, categoría A), S (nieve) y W (viento).

![La pestaña Hipótesis del cuadro Cargas](shots/loads-cases.webp)

Una hipótesis tiene un **Código**, un **Nombre**, un **Tipo** (Permanente, Variable o Accidental), un **Origen** (Peso propio / carga permanente, Acabados, Sobrecarga de uso, Nieve, Viento, Sismo o Definida por el usuario) y, si es de sobrecarga de uso, una **Categoría**. Los tres campos ψ0, ψ1 y ψ2 son los coeficientes de combinación; déjalos vacíos para tomar los de la categoría en la tabla. Un valor que no es un número de 0 a 1 se marca y no se guarda.

**Añadir hipótesis que faltan** crea una hipótesis de sobrecarga de uso por cada categoría que usan las estancias y que no tiene una.

## Colocar cargas

- **Carga superficial**: haz clic en las esquinas y pulsa <kbd>Enter</kbd>.
- **Carga lineal**: haz clic en sus dos extremos.
- **Carga puntual**: haz clic donde actúa.

Con una herramienta activa, la barra de la línea de comandos ofrece el **valor** de la carga y la **hipótesis de carga** en la que entra (por defecto **Sobrecarga de uso (Q-A)**). La carga va en el nivel activo.

![Una carga superficial de 2,5 kN/m² dibujada en el salón](shots/loads-plan.webp)

## Lo que Lapin calcula por ti

Estas no las dibujas; Lapin las deriva del modelo cada vez que las pides y siguen cada cambio:

- El **peso propio** de forjados, muros, pilares y vigas, a partir de las densidades de sus materiales, en la hipótesis cuyo origen es el peso propio (G1).
- Los **acabados de los forjados**, a partir de sus capas, y los **muros no portantes**, como cargas lineales, en la hipótesis que enumera acabados y tabiques (G2).
- Las **estancias**: la sobrecarga de uso de la categoría de una estancia sobre su superficie, en la hipótesis de esa categoría (Q-A, Q-B…).
- La **nieve** y el **viento**, una vez que das los valores del emplazamiento (véase [Nieve y viento](#nieve-y-viento)).

Un material sin densidad se deja fuera, y el Resumen dice cuál.

## La sobrecarga de uso de una estancia

Selecciona una estancia: sus propiedades tienen una sección **Sobrecarga de uso**. Su **Categoría** es **Según la función** (un salón, un dormitorio o una cocina es A, una oficina B, un trastero E1, un garaje F) salvo que elijas otra, o **None**. Debajo está el **qk** que da la categoría.

![La sobrecarga de uso de una estancia en sus propiedades](shots/loads-room.webp)

## La tabla de sobrecargas de uso

La pestaña **Sobrecargas de uso** lista cada categoría con su uso y su **qk** en kN/m², como recomienda EN 1991-1-1. Escribe en el campo el valor de tu anexo nacional; **Modificado** vuelve al valor por defecto.

![La pestaña Sobrecargas de uso](shots/loads-imposed.webp)

## Nieve y viento

La pestaña **Nieve y viento** guarda el clima del emplazamiento. Lapin no incluye los mapas nacionales: lee los valores de tu emplazamiento en **CR 1-1-3/2012** (nieve, su figura 3.1 o anexo A) y **CR 1-1-4/2012** (viento, su figura 2.1 o anexo A), o en el anexo nacional con el que trabajas, y escríbelos.

![La pestaña Nieve y viento](shots/loads-climate.webp)

- **Nieve**: **Carga de nieve sobre el terreno s0,k (kN/m²)**, **Exposición Ce** (completa 0,8, parcial 1,0, protegida 1,2), **Coeficiente térmico Ct** (1,0 salvo que la cubierta sea acristalada y pierda calor) y **Coeficiente de importancia γIs** (1,0 para edificios ordinarios). **Aplicar la nieve**.
- **Viento**: **Presión básica del viento qb (kN/m²)**, **Categoría de terreno** (de 0 a IV, del mar al centro de una ciudad) y **Coeficiente de importancia γIw**. **Aplicar el viento**.

Al aplicarlos se crean las hipótesis en las que entran sus cargas:

- **S**, la nieve sin acumulación, y **S+X**, **S-X**, **S+Y**, **S-Y**, la nieve acumulada por un viento que sopla hacia cada lado: en una cubierta a dos aguas, el faldón que mira al viento toma la mitad. Una acumulación que aquí no cambia nada (ningún faldón mira a ese viento) se deja fuera, y el Resumen lo dice.
- **W+X/s**, **W+X/p**, y así para las cuatro direcciones: el viento que sopla hacia +X, −X, +Y, −Y, una vez con la **succión** (/s) de las cubiertas y otra con su **presión** (/p).

Las disposiciones de la nieve y las direcciones del viento son alternativas: una combinación toma solo una de cada.

Lo que Lapin calcula a partir de ellos, cada vez que lo pides:

- **Nieve** en cada faldón de cubierta, por m² en planta: s = γIs · μ1 · Ce · Ct · s0,k, con μ1 = 0,8 hasta 30°, luego menos, 0 a 60° y más inclinado; en un forjado que nada cubre (una terraza, una cubierta plana) μ1 = 0,8.
- **Viento** sobre muros y cubiertas: el contorno del edificio es la envolvente convexa de sus muros y pilares; las fachadas toman las zonas A a E, las cubiertas planas F a I, una cubierta a dos aguas F a J, según los coeficientes de EN 1991-1-4, a la presión de la altura del edificio para la categoría de terreno. Una fachada pasa su viento a las vigas, travesaños y bordes de forjado a lo largo de ella, su franja más baja al terreno; una cubierta, a sus cabios o correas. Las cubiertas a cuatro aguas, la presión interior y las acumulaciones de nieve en los petos aún no se derivan.

> Estos valores son **orientativos**: los coeficientes son los recomendados por las normas. Compruébalos con tu anexo nacional.

## Resumen

La pestaña **Resumen** muestra, para cada hipótesis, las cargas que dibujaste y el total en kN, en conjunto y en cada nivel, y luego las cargas derivadas una a una, con su origen. Avisa de las estancias cuya categoría no tiene hipótesis y anota lo que se dejó fuera.

![La pestaña Resumen](shots/loads-summary.webp)

## Combinaciones

La pestaña **Combinaciones** lista las combinaciones de las hipótesis, según EN 1990 con los valores recomendados, orientativas: para el estado límite último 1,35 en las acciones permanentes y 1,5 en la variable principal, las demás con su ψ0; para el estado límite de servicio las características, frecuentes y cuasipermanentes. Una hipótesis que no contiene nada no participa.

![La pestaña Combinaciones](shots/loads-combinations.webp)

## Analizar

**Ejecutar análisis** (en el mismo grupo y en el cuadro Cargas) analiza el pórtico con estas cargas: véase [Análisis estructural](analysis.md).

## En el archivo IFC

**Exportar IFC, Fragments o BOS…** tiene **Structural Analysis View (modelo analítico y cargas)**: el archivo lleva entonces el modelo analítico de los muros portantes, forjados, pilares y vigas, con las hipótesis de carga, las cargas y las combinaciones, como un IFC Structural Analysis View. Véase [Importar y exportar](import-export.md).
