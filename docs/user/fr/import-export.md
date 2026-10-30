---
title: Import et export
order: 110
summary: Échangez le modèle en IFC, importez des dessins DXF, liez des fichiers et exportez des modèles 3D pour le rendu et la RA.
---

Tout ce qui entre ou sort se trouve dans le menu **Fichier**.

![Le menu Fichier](shots/file-menu.webp)

## IFC

**Exporter IFC…** écrit le modèle en IFC. Chaque mur, dalle, porte et toit est un véritable élément IFC avec ses couches, son type et ses propriétés, si bien que le fichier s’ouvre comme un modèle dans d’autres outils BIM. Les portes et les fenêtres indiquent comment elles s’ouvrent : le sens de chaque vantail et de chaque ouvrant, le côté vers lequel il pivote, et les panneaux, de la manière dont IFC les définit, pour que les autres logiciels dessinent les mêmes ouvertures.

**Importer IFC…** lit le fichier IFC d’un autre logiciel. Avant que quoi que ce soit n’entre, Lapin lit le fichier et montre ce qu’il contient : ses éléments par discipline (architecture, structure, fluides, mobilier et équipements, site), chaque discipline par catégorie avec leur nombre, et ses niveaux avec le nombre d’éléments sur chacun. Décochez ce dont vous n’avez pas besoin, par exemple les fluides et le mobilier quand vous travaillez sur la structure, ou ne gardez que les niveaux sur lesquels vous travaillez. **Comment** importe les éléments reconnus comme éléments Lapin, tous comme références, ou liés. Pendant la lecture et l’import du fichier, Lapin montre ce qu’il fait et où il en est ; un gros fichier prend un certain temps.

![Importer IFC](shots/import-ifc.webp)

Lapin reconnaît les murs, les dalles, les poteaux, les poutres, les semelles, les portes et les fenêtres, et les transforme en éléments Lapin que vous pouvez modifier. Ce qu’il ne peut pas reconnaître entre comme référence, dessinée mais non modifiable. Les portes et les fenêtres arrivent avec l’ouverture que le fichier indique : leur sens, le côté vers lequel elles s’ouvrent et, pour les fenêtres, la façon dont s’ouvre chaque panneau.

Les espaces du fichier (de Revit, ArchiCAD et autres) entrent comme pièces Lapin, chacune sur son propre niveau, avec leur contour, leur numéro, leur nom, leur hauteur, leur fonction, leur service et leur revêtement de sol : voir [Pièces et couleurs des pièces](rooms.md#pieces-depuis-un-fichier-ifc). Tout ce qu’un élément importé avait dans le fichier (ses jeux de propriétés et ses quantités) est conservé et affiché sous **Propriétés IFC** dans ses propriétés. Une référence sort d’**Exporter IFC…** telle qu’elle est entrée : avec le même GlobalId, la même classe et les mêmes attributs (description, repère, type prédéfini), ses jeux de propriétés et ses quantités avec chaque valeur du type qu’elle avait dans le fichier, et son type, son matériau, ses ports, ses connexions et son système, chacun avec son GlobalId. N’importez que les réseaux (CVC, plomberie, électricité) d’un fichier et exportez-les : le nouveau fichier les contient en entier.

## DXF et DWG

**Importer DXF…** fait entrer un dessin de CAO dans la vue au premier plan, avec ses calques, ses hachures, ses textes et ses cotes. Il arrive sous forme de dessin auquel vous pouvez vous accrocher et que vous pouvez modifier. Pour utiliser un DWG, enregistrez-le d’abord en DXF depuis votre logiciel de CAO (AutoCAD, BricsCAD ou DraftSight : **Save As → DXF**). L’import de PDF n’est pas encore disponible. Dans un plan, **Placé : par les coordonnées de la carte** pose un dessin en coordonnées réelles (un plan cadastral en Stereo 70) là où la carte le place (voir [Le site et ses données](site.md)).

## Liens

**Lier IFC…** garde un autre fichier lié au lieu de l’importer. Il est en lecture seule et rechargé quand il change. Cela fonctionne aussi pour un DXF. **Gérer → Liens** affiche, recharge, convertit ou supprime les liens.

## Modèles 3D

**Exporter 3D…** écrit le modèle sous forme de surfaces pour le rendu, la visualisation et la réalité augmentée (RA) :

| Format | Pour |
|---|---|
| GLB, glTF | Blender, three.js, visionneuses web. Les images propres à vos matériaux sont incluses. |
| USDZ | AR Quick Look d’Apple, Reality Composer, Omniverse. Images incluses. |
| USDA | OpenUSD en texte, un seul fichier. |
| DAE | COLLADA, pour SketchUp et des outils plus anciens. |
| Fragments | `.frag`, le format de That Open Engine, pour les visionneuses BIM web bâties dessus (That Open Components, Bubbles). Un seul fichier ; les corps identiques une seule fois. |

**Importer Fragments…** fait entrer un fichier `.frag` : chaque élément avec géométrie devient une référence dessinée d’après son corps, dans la classe que nomme le fichier, sur le niveau où le fichier le place (apparié par nom aux vôtres, créé s’il manque), avec les attributs du fichier sous **Propriétés IFC**. Les éléments au même corps (la même forme posée en plusieurs endroits) le partagent. Importé de nouveau, le même fichier met à jour ce qu’il avait apporté. Rien n’est reconnu comme mur ou dalle : un `.frag` contient des triangles.

![Exporter 3D](shots/export-3d.webp)

Vous choisissez ce qu’il faut exporter, quelle phase et l’origine. **Quoi** vaut **tout**, ou le modèle **tel que la vue le montre** : la vue 3D au premier plan, sans ses éléments et catégories masqués ni les niveaux qu’elle n’affiche pas, avec les couches qu’elle affiche (sans les finitions, ou le noyau seulement). Quand cette vue est coupée (la coupe au-dessus des niveaux affichés, une coupe affichée en 3D, la zone de coupe), **Coupe** coupe aussi l’export à cet endroit et ferme chaque coupe dans ses matériaux, si bien que le fichier ressemble à l’écran. Une vue 3D qui omet quelque chose est exportée telle qu’elle s’affiche, sauf si vous en décidez autrement. Chaque élément garde sa classe IFC, son type et son GlobalId. Pour échanger le modèle lui-même plutôt que ses surfaces, utilisez **Exporter IFC…**.
