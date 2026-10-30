---
title: Estructura
order: 50
summary: Pilares, vigas y zapatas, sus tipos y cómo se encuentran.
---

La ficha **Estructura** contiene los pórticos y la cimentación.

![La ficha Estructura](shots/ribbon-structure.webp)

## Pórticos

- **Pilar** (<kbd>CL</kbd>): haga clic donde se sitúa. Un pilar va desde su nivel hasta el nivel superior.
- **Viga** (<kbd>BM</kbd>): haga clic en sus dos extremos. **Viga curva** (<kbd>BC</kbd>) sigue un arco.
- **Pilares en la rejilla** coloca un pilar en cada cruce de los ejes de la rejilla. Para dibujar los ejes, use **Anotar → Eje de rejilla** (<kbd>GR</kbd>).

## Cimentación

- **Zapata corrida** (<kbd>SF</kbd>) va bajo los muros. Sus capas exteriores se unen a inglete en las esquinas.
- **Zapata corrida curva** (<kbd>CF</kbd>) sigue un arco.
- **Zapata aislada** (<kbd>PF</kbd>) va bajo un pilar.

Dibuje la cimentación en el nivel **Foundations**, o dele una planta propia.

![Pilares, vigas y zapatas aisladas en 3D](shots/structure-3d.webp)

## Tipos y esquinas

**Tipos estructurales** define las secciones de pilares, vigas y zapatas: secciones de hormigón y perfiles de acero.

**Inglete**, **A tope** e **Intercambiar** cambian cómo se encuentran dos vigas o dos zapatas. **Igualar tipo** (<kbd>MA</kbd>) copia el tipo de un elemento a otros, y **Crear similar** (<kbd>CS</kbd>) empieza a dibujar otro elemento del mismo tipo.

## Una vista estructural

La disciplina de una vista decide lo que muestra. Una vista **Estructura** muestra solo los núcleos portantes, sin los acabados. La disciplina se elige en las propiedades de la vista. Consulte [Secciones, alzados y 3D](views.md).
