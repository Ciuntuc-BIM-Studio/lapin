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

Una viga trazada de eje de pilar a eje de pilar se detiene en las caras de los pilares; su longitud, su volumen y sus estribos cuentan la luz libre entre ellas. **Inglete**, **A tope** e **Intercambiar** cambian cómo se encuentran dos vigas o dos zapatas. **Igualar tipo** (<kbd>MA</kbd>) copia el tipo de un elemento a otros, y **Crear similar** (<kbd>CS</kbd>) empieza a dibujar otro elemento del mismo tipo.

## Zapatas corridas con perfil

Un tipo de zapata corrida es el estándar, un murete con sus capas sobre una zapata, o cualquier perfil barrido a lo largo del eje de la zapata, como los perfiles de zapata de Revit. En **Tipos estructurales → Strip**, elija **Forma: Perfil** y un perfil: uno del catálogo (rectangular, escalonada en 2 o 3 escalones, con la cara superior inclinada, excéntrica con el murete en un borde, con murete), hecho con sus medidas, o uno del proyecto (**Gestionar → Perfiles**, donde también puede escribir sus vértices o leer un contorno dibujado). El punto de inserción del perfil va en el eje de la zapata, a la cota **Inferior** del tipo.

Las zapatas con perfil pueden ser rectas o curvas. Dos que se encuentran en una esquina se unen a inglete, y una que termina contra otra entra en ella y queda recortada por su cuerpo, de modo que cada escalón se encuentra con el escalón de la otra. Las plantas, secciones y alzados cortan el cuerpo barrido. Las cantidades dan su longitud, el área de su sección y su volumen, y sus caras verticales son las que encofran los Medios auxiliares. En IFC una recta es un IfcFooting STRIP_FOOTING cuyo cuerpo es su perfil extruido (un IfcArbitraryClosedProfileDef), cortado por los planos de las esquinas; una curva va como sus caras. Elija **Estándar** para volver al murete sobre zapata.

![Zapatas corridas escalonadas bajo una casa, a inglete en las esquinas](shots/footing-profile-3d.webp)

![Un tipo de zapata corrida con perfil](shots/footing-profile-type.webp)

## Una vista estructural

La disciplina de una vista decide lo que muestra. Una vista **Estructura** muestra solo los núcleos portantes, sin los acabados. La disciplina se elige en las propiedades de la vista. Consulte [Secciones, alzados y 3D](views.md).
