---
title: Carpeta del proyecto y emisiones
order: 125
summary: Emita láminas y el modelo en una carpeta del proyecto como pide ISO 19650, con los nombres de la convención, con un albarán de transmisión, tras las comprobaciones que hace una oficina de proyectos.
---

## La carpeta del proyecto

**Manage → Project folder** fija la carpeta en la que se emite el proyecto: una carpeta de su equipo, un recurso compartido de red o una carpeta de SharePoint u OneDrive sincronizada en el disco. Lapin no pide ninguna cuenta ni habla con ningún servidor: escribe archivos, y lo que sincroniza la carpeta es cosa de su oficina. La ruta se guarda en el proyecto, así que viaja con el archivo; una carpeta que no está en este equipo es un aviso, no un error.

![Carpeta del proyecto: la carpeta, los contenedores, la nomenclatura](shots/project-folder.webp)

Dentro de la carpeta Lapin crea, en la primera emisión, los cuatro contenedores ISO 19650: **WIP**, **Shared**, **Published** y **Archived**. El código de estado sugiere el contenedor: S0 va a WIP, S1–S7 a Shared, A, B y CR a Published.

## Nomenclatura

Los archivos se nombran según ISO 19650-2: **Proyecto-Originador-Volumen-Nivel-Tipo-Rol-Número**, luego el estado y la revisión, por ejemplo `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. En el diálogo Project folder fija el código del proyecto, el originador (su oficina), el volumen y el nivel y rol predeterminados. El resto viene del proyecto:

- el **nivel** de una lámina es la planta que dibujan sus planos (`00` en ±0,00, `01` arriba, `B1` abajo, `ZZ` cuando dibuja varias o ninguna);
- su **rol** es el de su disciplina (arquitectura A, estructura S, MEP M, civil C);
- su **número** son las cifras del número de lámina (`A-101` pasa a `0101`);
- el modelo es `M3`, el albarán `IE`.

## Comprobaciones

**Manage → Check** ejecuta, sobre todo el proyecto, las reglas que una oficina de proyectos comprueba a ojo antes de una emisión. Un hallazgo **bloqueante** detiene la emisión; un **aviso** puede pasarse por alto. Pulse un hallazgo para ir a la lámina, la vista o el elemento.

![Comprobación: los hallazgos por regla](shots/cde-check.webp)

Bloqueantes: un cajetín sin número, sin título o sin quién lo dibujó; una vista sin escala; una vista fuera del marco o sobre el cajetín; una lámina que cambió desde su última emisión pero sigue en esa revisión; un número de lámina que no da número de archivo. Avisos: quién comprobó, la fecha y la fase vacías; un plano coloreado por un esquema de habitaciones, o con muchas tramas, sin leyenda; un plano, una sección o un alzado en ninguna lámina; un plano de situación sin flecha del norte; elementos sin planta, sin tipo o sin material; puertas y ventanas sin etiqueta; habitaciones sin número; las propiedades IFC obligatorias (IsExternal, LoadBearing, FireRating en puertas) vacías.

## Emitir

**Manage → Issue…** escribe una emisión en un contenedor (solo en la aplicación de escritorio, porque escribe archivos):

![Emisión: las láminas, la revisión, el estado, las comprobaciones](shots/cde-issue.webp)

1. Dé un nombre a la emisión, su **revisión** (`P01`, `P02`… mientras es preliminar, `C01`… una vez contractual; la siguiente se propone a partir de la última emisión), su **estado** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) y el **contenedor**.
2. Marque las láminas que van, el modelo como IFC, cada lámina también como DXF, o todas las láminas también en un solo PDF. El nombre de cada archivo se muestra tal como se escribirá.
3. Las comprobaciones se ejecutan mientras elige. Con un hallazgo bloqueante la emisión no puede salir; solo con avisos, marque **Emitir de todos modos**.
4. **Emitir** escribe cada lámina como PDF, el IFC y el resto en el contenedor, con los nombres de la convención, calcula el hash (SHA-256) de cada archivo, escribe el **albarán de transmisión** (un PDF y un CSV: proyecto, emisión, fecha, revisión, estado, los archivos) y un **manifiesto** (JSON, con todos los hashes) junto a ellos, y registra la emisión en el proyecto. Cada lámina impresa toma la revisión y el estado de la emisión en su cajetín, y su tabla de revisiones lista las emisiones en las que salió.
5. El proyecto se guarda después como una **versión** con el nombre de la emisión (vea [Archivos, versiones y recuperación](files.md)), para que una emisión pueda reabrirse, restaurarse o compararse con el proyecto tal como está ahora.

Las emisiones se listan al final del diálogo Project folder, cada una con sus archivos y **Abrir carpeta**.
