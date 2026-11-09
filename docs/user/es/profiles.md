---
title: Bibliotecas de perfiles
order: 57
summary: Los perfiles que tu trabajo usa, sea cual sea su norma — el pequeño conjunto EN de Lapin, tus tablas y archivos IFC, las carpetas de la empresa; dibujados con sus propiedades calculadas y los valores del archivo comprobados, copiados al proyecto según se usan.
---

Lapin no puede incluir todos los perfiles de acero del mundo, así que trae un pequeño conjunto europeo y lee el resto de lo que ya tienes: un catálogo en hoja de cálculo (AISC Shapes Database, la tabla de un fabricante, la de tu empresa), una biblioteca IFC de perfiles (la de Bonsai, por ejemplo) o cualquier modelo IFC, un contorno cerrado dibujado en Lapin o en un DXF. **Gestionar → Bibliotecas de perfiles** los reúne todos.

![Bibliotecas de perfiles](shots/profile-libraries.webp)

## Las bibliotecas

- **Integrada**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, angulares iguales y desiguales, SHS, RHS y CHS acabados en caliente en tamaños habituales, y C, Z y Σ genéricos conformados en frío. Solo dimensiones normalizadas; Lapin calcula todas las propiedades.
- **En este ordenador**: las bibliotecas que importas, para cualquier proyecto. La versión de navegador también las guarda, en ese navegador.
- **Carpetas de la empresa**: en la aplicación de escritorio, tantas carpetas locales, de red o sincronizadas con archivos `.lapinprofiles` como quieras (**Añadir una carpeta…**). Una carpeta es de solo lectura hasta que marcas **escritura**; entonces el botón de guardar a su lado escribe allí la biblioteca elegida, para tus compañeros.
- **En el proyecto**: solo se copian los perfiles que el proyecto usa, con el nombre de su biblioteca, para que el archivo se abra en cualquier sitio.

Escribe en la búsqueda para mirar en todas las bibliotecas a la vez por nombre, familia, norma o tipo; elige un perfil para verlo dibujado con sus dimensiones y propiedades.

## Propiedades

A partir de las dimensiones, Lapin calcula el área, Iy, Iz, los módulos elástico y plástico, los radios de giro, los ejes principales y su ángulo (angulares, Z, cualquier contorno asimétrico), la constante de torsión It y la de alabeo Iw donde hay fórmula, la masa por metro según la densidad del material y la superficie de pintura por metro. Coinciden con las tablas publicadas en torno al 1 % (It e Iw de perfiles I laminados al 3 %; Iw de un canal con alas inclinadas solo al 20 %). Un contorno arbitrario no tiene It ni Iw salvo que los dé su archivo.

Los valores de un archivo se conservan y se muestran junto a los de Lapin. Una diferencia de más del 2 % (área, masa), 3 % (inercias, módulos) o 10 % (It, Iw) se marca y entra en la lista de comprobación de la importación; de una columna entera desviada una potencia de diez se avisa que está en otra unidad.

![Un valor del archivo que difiere del de Lapin](shots/profile-check.webp)

## Importar una tabla

**Tabla…** lee un CSV o XLSX, una fila por perfil. El significado y la unidad de cada columna se deducen de su encabezado en inglés, rumano, alemán o francés (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), con sus unidades (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Una tabla con Ix y sin Iz se lee a la americana: x el eje fuerte, en pulgadas. Corrige en el diálogo lo que esté mal; Lapin recuerda tu elección para el próximo archivo con la misma disposición.

![Las columnas de una tabla asignadas](shots/profile-table.webp)

El tipo de una fila sale de su columna de tipo (W, HP, UPN, HSS, PIPE…) o de su nombre; para las demás elige el tipo en el diálogo.

## IFC

**IFC…** lee todas las definiciones de perfil de cualquier archivo IFC: una biblioteca o un modelo de Tekla, Revit o ArchiCAD, donde los perfiles que usan sus elementos toman el nombre de sus tipos. I, I asimétrico, U, T, L, C, Z, tubos, rectángulo, círculo y elipse conservan su tipo y medidas; los arbitrarios, compuestos y derivados pasan a contornos. Llegan también las propiedades mecánicas de `Pset_ProfileMechanical` (y las de IFC2x3); los exportadores no coinciden en sus unidades ni en cuál es el eje Y, así que Lapin decide ambas cosas por la geometría. Los perfiles de línea central y trapecio se omiten y se dice.

![Perfiles leídos de un archivo IFC](shots/profile-ifc.webp)

**IFC** y **CSV** bajo la lista escriben la biblioteca elegida (o los perfiles del proyecto) como biblioteca de perfiles IFC4 o como una tabla que Lapin vuelve a leer.

También en el IFC del modelo las vigas, los pilares y los perfiles de acero se escriben por su perfil paramétrico (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, rectángulos y círculos), con el nombre del perfil y `Pset_ProfileMechanical`, de modo que otros programas ven un IPE 300 y no una polilínea. Los contornos, Σ y Z con labios siguen como perfiles arbitrarios; un elemento cortado por otro sigue siendo el sólido que es.

## Un contorno como perfil

Dibuja la sección con Delineación como polilíneas cerradas y círculos (los que están dentro de otro son huecos), selecciónalos y pulsa **De la selección**, o lee el dibujo de un fabricante con **DXF…**. Ponle nombre, elige su material y biblioteca y guárdalo. Así entran los perfiles de aluminio y PVC, las secciones prefabricadas y las formas de madera: el mismo almacén, con su material y densidad.

![Un contorno dibujado convertido en perfil](shots/profile-outline.webp)

**Dibujar su contorno** hace lo contrario: el contorno del perfil elegido como polilíneas cerradas en la vista activa, para que Studio lo extruya o lo barra.

## Usar un perfil

En **Acero → Tipos de acero**, **Estructura → Tipos estructurales** (pilares y vigas) y los perfiles de la nave de pórticos, elige **De una biblioteca de perfiles…** y escribe para buscar.

![El selector busca en todas las bibliotecas](shots/profile-picker.webp)

El perfil se copia al proyecto y el tipo lo conserva. Los cuerpos son ligeros, con esquinas vivas, en todos los niveles menos uno: una vista a **LOD 400** (planta, sección, alzado, plano o 3D) dibuja la sección real, con radios de acuerdo, radios en las puntas y alas inclinadas. Las exportaciones y la selección conservan el cuerpo de esquinas vivas, pero las cantidades, los pesos, las mediciones, la lista de elementos de acero y los presupuestos usan el área real de la sección (un IPE 300 tiene 53,81 cm², no los 51,88 del contorno de esquinas vivas); una sección sin radios no cambia.

![Una viga y un pilar de acero en una vista 3D a LOD 400: radios de acuerdo dibujados](shots/profile-real-section.webp)

En otros sitios el perfil da lo que esa herramienta puede usar: molduras, rozas y zapatas corridas toman su exterior como perfil 2D (**De una biblioteca de perfiles…** en su lista), y los montantes y travesaños de los muros cortina y los marcos y hojas de puertas y ventanas toman su contorno real, con huecos, a lo largo de cada pieza, con su ancho y canto; la x del perfil va a través de la pieza (hacia el vidrio en marco y hoja), su y hacia fuera del edificio. **Simple** quita el contorno y conserva el tamaño.

![Montantes y travesaños de muro cortina de una sección de aluminio](shots/profile-real-mullions.webp)

![Marco y hoja de una ventana de la misma sección](shots/profile-real-frame.webp)

Una biblioteca puede cambiar después de usarla. Un perfil del proyecto distinto del de igual nombre en su biblioteca se marca **más nuevo en la biblioteca**; **Actualizar desde la biblioteca** lo sustituye y todos los tipos hechos con él lo siguen, como un solo paso de deshacer. Nada cambia hasta que lo pides. Los proyectos anteriores conservan sus tipos.
