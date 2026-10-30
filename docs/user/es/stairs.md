---
title: Escaleras
order: 42
summary: Coloque una escalera entre plantas, configúrela en su tipo y cree los pocos escalones que suben a un porche o a una puerta.
---

## Escaleras

Elija **Arquitectura → Escalera** (<kbd>ST</kbd>). Haga clic en el pie de la escalera (el centro de su primera contrahuella) y luego en un punto en la dirección en que sube. Para una escalera en L o en U, un tercer clic a la izquierda o a la derecha del tramo indica hacia dónde gira. La escalera sube desde su planta hasta la planta superior. Lapin calcula el número de contrahuellas, la contrahuella y la huella a partir de la altura y de las reglas del tipo.

Seleccione la escalera para modificarla en el panel de propiedades:

- **Tipo de escalera.** La forma, las reglas, la construcción y la barandilla vienen del tipo, que se configura en **Arquitectura → Tipos de escalera**.
- **Ancho**, **Giro**, **Desfase de base** y **Desfase superior.**
- **Desnivel.** Déjelo vacío y la escalera subirá hasta la planta superior. Un valor da a la escalera un desnivel propio, sean cuales sean las plantas. Úselo para unos pocos escalones entre dos niveles de la misma planta.
- **Fijo.** Contrahuellas, contrahuella, huella y longitud del rellano, cuando quiera fijarlos. Déjelos vacíos para que decidan las reglas.
- **Comprobaciones.** Cada valor frente a las reglas del tipo, en verde o en rojo.

Una escalera pide a la losa superior un hueco sobre sí misma. Desmarque **Hueco en la losa superior** si la losa debe quedar entera.

## Escalones de entrada (un porche)

Los escalones que suben a una puerta de entrada tienen un desnivel propio: desde el terreno, unos escalones por debajo de la planta baja, hasta ±0.00. Lapin tiene un tipo de escalera listo para ellos, **Escalones de entrada**: 2 m de ancho, tres contrahuellas de 150 mm, de hormigón macizo vertido sobre el terreno, sin barandilla.

![Escalones de entrada delante de un porche, en planta](shots/entrance-steps-plan.webp)

1. **El porche.** Dibuje el rellano como una losa: elija **Arquitectura → Losa** (<kbd>SB</kbd>) y haga clic en su contorno fuera del muro, delante de la puerta. La cara superior de una losa está al nivel de su planta (±0.00 en la planta baja) y su tipo le da el espesor. Para cambiar el espesor, cree un tipo de losa para porches en **Arquitectura → Tipos de losa**, por ejemplo 150 mm de hormigón.
2. **Los escalones.** Elija **Arquitectura → Escalones de entrada**. Haga clic en el centro del borde del porche y luego en el lado hacia el que salen los escalones. Los escalones suben hasta el borde del porche y terminan en ±0.00. Empiezan 450 mm más abajo: su **Desfase de base** es −450 mm.
3. **La altura.** Seleccione los escalones. En **Desnivel**, escriba la diferencia real entre el terreno y el porche, por ejemplo 300. La parte superior se queda en el porche y el pie sube: el desfase de base pasa a −300 mm y los escalones tienen dos contrahuellas. **Ancho** los hace tan anchos como el porche, por ejemplo 2400.

![Las propiedades de los escalones de entrada: desnivel, desfase de base, niveles](shots/entrance-steps-properties.webp)

**Niveles** muestra dónde empiezan y terminan los escalones, por ejemplo −0.45 → ±0.00. Con una sola contrahuella (un desnivel de 150 mm), el escalón es el propio borde del porche y no hay huella que construir. Dé a los escalones dos contrahuellas o más para tener huellas delante del porche.

El tipo se añade al proyecto la primera vez que lo usa. Para modificarlo, abra **Arquitectura → Tipos de escalera** y elija **Escalones de entrada**:

- **Forma → Un desnivel propio.** El desnivel del tipo; cada escalera puede cambiarlo igualmente con **Desnivel**.
- **Reglas → Conjunto de reglas → escalones de entrada.** Contrahuellas de 120–170 mm y huellas de 300–400 mm, con una sola contrahuella permitida.
- **Construcción → Tipo de construcción.** **Maciza, hormigonada sobre el terreno** rellena los escalones hasta su base. **Losa inclinada bajo los peldaños** los construye como una escalera interior.

![El tipo Escalones de entrada en el configurador de escaleras](shots/stair-types.webp)

Para que otro tipo de escalera suba un desnivel propio, marque **Un desnivel propio** en su ficha **Forma**. Para convertir una escalera colocada en escalones de entrada, elija **Escalones de entrada** como su **Tipo de escalera**: baja de modo que su parte superior quede al nivel de su planta.

![La casa con su porche y los escalones de entrada en 3D](shots/entrance-steps-3d.webp)

En IFC, los escalones son un IfcStair de tipo STRAIGHT_RUN_STAIR. Su tramo es un IfcStairFlight con sus NumberOfRisers, NumberOfTreads, RiserHeight y TreadLength, y Pset_StairCommon lleva los mismos valores. El porche es un IfcSlab.
