---
title: Extensiones
order: 126
summary: Añade mobiliario, plantillas de proyecto, perfiles, patrones de sombreado, cajetines y listas de precios hechos por otros desde el catálogo Lapin Extensions, y quítalos de nuevo.
---

## El catálogo

**Lapin Extensions** es un catálogo gratuito de cosas hechas para Lapin por sus usuarios y por nosotros: paquetes de mobiliario, plantillas de proyecto, bibliotecas de perfiles, patrones de sombreado, cajetines y listas de precios. Cada una va a la biblioteca que Lapin ya tiene para ella, así que la usas como si la hubieras hecho tú.

Abre **Archivo → Extensiones…**. **Explorar** muestra el catálogo: busca por nombre, etiqueta o por lo que contiene una extensión, o elige una categoría.

![Extensiones: Explorar, los detalles de un paquete de mobiliario](shots/extensions-browse.webp)

Haz clic en una extensión para ver sus detalles antes de instalarla:

- **Autor** y **Licencia**. Cada extensión tiene una licencia, elegida por su autor. Una *permisiva* (MIT, CC-BY, CC0…) te deja usar lo que contiene en cualquier proyecto; con otra, *lee sus condiciones* antes.
- **No comercial**: una extensión cuya licencia prohíbe el uso comercial (CC-BY-NC, PolyForm Noncommercial…) se marca así en la lista y en sus detalles. Puedes instalarla y aprender de ella, pero no usarla en trabajos remunerados.
- **Funciona con**: las versiones de Lapin para las que se hizo. Si no encaja con tu Lapin, lo dice en lugar de ofrecer **Instalar**.
- **Tamaño**, el archivo y su **SHA-256**: la huella del archivo que revisó el catálogo.
- **Qué contiene**: sus elementos, cuando el autor los enumera.
- **Página web** y **Tema del foro**: dónde preguntar a su autor.

## Instalar, actualizar, desinstalar

Haz clic en **Instalar**. Lapin descarga el archivo, comprueba que su tamaño y su SHA-256 son exactamente los que indica el catálogo (un archivo cambiado por el camino se rechaza y no se instala nada) y lo incorpora a su biblioteca:

| Tipo | Adónde va |
|---|---|
| Mobiliario | **Interiores → Mi mobiliario…** |
| Plantillas de proyecto | **Archivo → Nuevo desde plantilla…** |
| Perfiles | las bibliotecas de perfiles de este ordenador |
| Patrones de sombreado | Mis patrones, que ofrece **Gestionar → Materiales** |
| Cajetines | los cajetines de este ordenador |
| Listas de precios | **Ejecución → Listas de precios…**, marcadas como orientativas: los precios son del autor, compáralos con los tuyos |
| Tipos | el proyecto abierto cuando la instalas, en un paso para deshacer (**Gestionar → Paquetes de tipos…** exporta los tuyos) |

Si tu biblioteca ya tiene algo con el mismo nombre, Lapin pregunta antes de sustituirlo. Nada de lo que trae una extensión se ejecuta jamás: las extensiones son contenido.

**Instaladas** muestra lo que tiene este ordenador. Cada fila abre el lugar adonde fue a parar (**Mi mobiliario…**, **Plantillas de proyecto…**), ofrece **Actualizar** cuando el catálogo tiene una versión más nueva, y **Desinstalar**.

![Instaladas: el paquete de mobiliario, con el botón que abre Mi mobiliario](shots/extensions-installed.webp)

![Los tipos de mobiliario del paquete en Mi mobiliario](shots/extensions-my-furniture.webp)

**Desinstalar** quita de tus bibliotecas lo que añadió la extensión, y solo eso. Lo que ya pusiste en tus proyectos se queda en ellos: un tipo de mobiliario insertado en un proyecto, un proyecto iniciado desde una plantilla.

## Tipos

Una extensión de **tipos** trae tipos de muros, forjados, puertas, ventanas, pilares, vigas o escaleras con aquello de lo que están hechos: sus capas, materiales, perfiles y parámetros. Instalada, sus tipos entran en el proyecto abierto ahora, en un paso para deshacer: un material del mismo nombre ya en el proyecto se usa, cualquier otro se crea; un tipo ya presente exactamente igual se reutiliza, y uno del mismo nombre que difiere se añade a su lado con (2) tras el nombre. **Instaladas → Añadir sus tipos a este proyecto** los lleva a otro proyecto.

![Los detalles de una extensión de tipos: su licencia marcada como no comercial](shots/extensions-types.webp)

Tus propios tipos van a un paquete desde **Gestionar → Paquetes de tipos…**: marca los tipos, da un nombre al paquete y **Exportar tipos…** guarda un archivo `.lapintypes`. **Importar tipos…** en el mismo diálogo lee uno, te deja elegir sus tipos y los añade del mismo modo.

![Gestionar → Paquetes de tipos…: un tipo de muro elegido para exportar](shots/type-packs-export.webp)

## Scripts

El catálogo también incluye **scripts de Python**. En la aplicación de escritorio ya puedes instalar uno; se ejecuta cuando Lapin tenga su consola de Python, y solo cuando tú lo ejecutes. Antes de instalarlo, sus detalles dicen qué pide (leer o cambiar el proyecto abierto, acceder a archivos o a internet). Un script puede hacer en tu ordenador lo que puedes hacer tú, así que instala scripts de autores en quienes confíes. El navegador solo admite paquetes de contenido.

![Los detalles de un script: qué pide, y que aún no se puede ejecutar](shots/extensions-script.webp)

## Otro catálogo

**Catálogo…**, al pie del diálogo, fija la dirección del catálogo: una dirección https, o la ruta de un `index.json` en este ordenador o en una carpeta compartida (el catálogo propio de una empresa). Déjalo vacío para usar el de Lapin. La flecha de al lado vuelve a leer el catálogo.

## Publicar las tuyas

Cualquiera puede publicar una extensión: expórtala desde Lapin (Mi mobiliario → Exportar, Guardar como plantilla…, una biblioteca de perfiles, un cajetín), adjunta el archivo a una release de un repositorio tuyo de GitHub y añade su manifiesto al catálogo con un pull request en [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Su README explica cómo, y qué miran las comprobaciones automáticas.

Un paquete de tipos sale de **Gestionar → Paquetes de tipos… → Exportar tipos…**; una lista de precios de **Listas de precios…**, con el botón de exportar de una lista de este ordenador (un archivo `.lapinprices`, con sus precios marcados como orientativos).
