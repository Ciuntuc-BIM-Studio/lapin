---
title: Import et export
order: 110
summary: Échangez le modèle en IFC, importez des dessins DXF, liez des fichiers et exportez des modèles 3D pour le rendu et la RA.
---

Tout ce qui entre ou sort se trouve dans le menu **Fichier**.

![Le menu Fichier](shots/file-menu.webp)

## IFC et Fragments

**Exporter IFC ou Fragments…** écrit le modèle en IFC. Chaque mur, dalle, porte et toit est un véritable élément IFC avec ses couches, son type et ses propriétés, si bien que le fichier s’ouvre comme un modèle dans d’autres outils BIM. Les portes et les fenêtres indiquent comment elles s’ouvrent : le sens de chaque vantail et de chaque ouvrant, le côté vers lequel il pivote, et les panneaux, de la manière dont IFC les définit, pour que les autres logiciels dessinent les mêmes ouvertures.

L’export demande le schéma (**IFC4**, lu partout, ou **IFC4X3**), le détail (les pièces comme éléments à part entière ou un corps par élément), la phase et la façon d’écrire les corps. **Géométrie** est un **B-rep à facettes** (exact, ce qu’attendent le plus souvent les autres logiciels) ou des **ensembles de faces** : les mêmes faces dans un fichier bien plus petit. **Simplifier** écrit moins de triangles là où un corps est un maillage fin, comme les gaines et les tuyaux du fichier d’un autre logiciel, jamais plus loin de lui que la **tolérance** que vous saisissez (2 mm au départ). Les corps que Lapin construit lui-même (murs, dalles, portes) gardent leurs faces exactes, et simplifier ne change que la forme d’un corps, jamais ses propriétés.

![Exporter IFC](shots/export-ifc.webp)

**Importer IFC…** lit le fichier IFC d’un autre logiciel. Avant que quoi que ce soit n’entre, Lapin lit le fichier et montre ce qu’il contient : ses éléments par discipline (architecture, structure, fluides, mobilier et équipements, site), chaque discipline par catégorie avec leur nombre, et ses niveaux avec le nombre d’éléments sur chacun. Décochez ce dont vous n’avez pas besoin, par exemple les fluides et le mobilier quand vous travaillez sur la structure, ou ne gardez que les niveaux sur lesquels vous travaillez. **Comment** importe les éléments reconnus comme éléments Lapin, tous comme références, ou liés. Pendant la lecture et l’import du fichier, Lapin montre ce qu’il fait et où il en est ; un gros fichier prend un certain temps.

![Importer IFC](shots/import-ifc.webp)

Certains fichiers portent de la 2D en plus de leur modèle : symboles de plan, emprises, hachures et textes dessinés pour un élément, ou éléments dessinés seulement en 2D (lignes, textes, symboles sans corps). Quand un fichier en a, la boîte de dialogue les compte sous **Dessiné en 2D**. Décochez **la 2D propre des éléments** et le plan dessine ces éléments par leur coupe, comme ceux de Lapin ; décochez **éléments dessinés seulement en 2D** et ils restent dehors. Les deux entrent si vous ne les décochez pas.

![Dessiné en 2D](shots/import-ifc-2d.webp)

**Nettoyage et optimisation** regroupe les règles de l’import, les mêmes pour tout fichier, gardées comme les vôtres pour l’import suivant. **Garder une seule fois un corps répété** (cochée sauf si vous la décochez) garde une fois un corps que le fichier répète sous d’autres noms (un boulon, une bride, un support) ; ses éléments le partagent, rien n’est perdu. Les autres laissent de côté, quand vous les cochez, ce qu’un autre logiciel a amené : les **corps plats** (textes, lignes et hachures changés en triangles), les **éléments plus loin du reste** que la distance saisie (les textes d’un dessin, un cartouche), les **doublons** au même endroit et les **ports et réservations écrits comme objets**. **Simplifier les maillages** écrit moins de triangles dans la tolérance saisie ; **lire les nombres écrits comme texte** change `DoubleLength:0,01` en 0,01 m et `1.15395 kg` en une masse (un nombre entier seul, un code, reste du texte). Sous **Quoi importer**, **Jeux de propriétés** montre les jeux du fichier avec le nombre d’éléments de chacun ; les **techniques** (la transformation de Navisworks, des identifiants internes, des statistiques de géométrie) restent dehors sauf si vous les cochez. Le message de l’import dit ce qu’a fait chaque règle.

![Nettoyage et optimisation](shots/import-ifc-rules.webp)

Ce que le fichier place arrive en un seul groupe à son nom, que vous pouvez ouvrir et nettoyer, ou séparément si vous décochez **En un seul groupe** (voir [Groupes et nettoyage](groups.md)).

**Tout modifiable**, un choix de **Comment**, va plus loin que la reconnaissance : ce qui resterait une référence devient un solide Studio fait de ses propres triangles, le même élément avec sa classe, ses propriétés, son niveau et son groupe, que pousser/tirer, les congés et les booléens prennent aussitôt (voir [Studio](studio.md)). Un corps de plus de 5 000 triangles, comme une pompe ou un raccord finement maillé, reste une référence, et le message de l’import dit combien et pourquoi. **Importer Fragments…** demande la même chose avec **Les rendre modifiables**, et **Importer un maillage** avec **Comme : modifiables**. Une référence importée auparavant se rend modifiable une à une avec Studio → **Faire un solide Studio**, qui la garde dans son groupe et sur son niveau.

Lapin reconnaît les murs, les dalles, les poteaux, les poutres, les semelles, les portes et les fenêtres, et les transforme en éléments Lapin que vous pouvez modifier. Ce qu’il ne peut pas reconnaître entre comme référence, dessinée mais non modifiable. Les portes et les fenêtres arrivent avec l’ouverture que le fichier indique : leur sens, le côté vers lequel elles s’ouvrent et, pour les fenêtres, la façon dont s’ouvre chaque panneau.

Les espaces du fichier (de Revit, ArchiCAD et autres) entrent comme pièces Lapin, chacune sur son propre niveau, avec leur contour, leur numéro, leur nom, leur hauteur, leur fonction, leur service et leur revêtement de sol : voir [Pièces et couleurs des pièces](rooms.md#pieces-depuis-un-fichier-ifc). Tout ce qu’un élément importé avait dans le fichier (ses jeux de propriétés et ses quantités) est conservé et affiché sous **Propriétés IFC** dans ses propriétés. Une référence sort d’**Exporter IFC…** telle qu’elle est entrée : avec le même GlobalId, la même classe et les mêmes attributs (description, repère, type prédéfini), ses jeux de propriétés et ses quantités avec chaque valeur du type qu’elle avait dans le fichier, et son type, son matériau, ses ports, ses connexions et son système, chacun avec son GlobalId. N’importez que les réseaux (CVC, plomberie, électricité) d’un fichier et exportez-les : le nouveau fichier les contient en entier.

**Importer Fragments…** ouvre le même panneau pour un fichier `.frag` de That Open : ses éléments par discipline et par type, ses niveaux, et **En un seul groupe**. Un fichier exporté de ce projet même n’apporte rien de nouveau quand on l’y réimporte : ses éléments y sont déjà.

![Importer Fragments](shots/import-frag.webp)

**Importer Fragments…** fait entrer un fichier `.frag` : chaque élément avec géométrie devient une référence dessinée d’après son corps, dans la classe que nomme le fichier, sur le niveau où le fichier le place (apparié par nom aux vôtres, créé s’il manque), avec les attributs du fichier sous **Propriétés IFC**. Les éléments au même corps (la même forme posée en plusieurs endroits) le partagent. Importé de nouveau, le même fichier met à jour ce qu’il avait apporté. Rien n’est reconnu comme mur ou dalle : un `.frag` contient des triangles.

**Exporter IFC ou Fragments…** écrit le modèle en IFC ou en Fragments, choisi comme un import : les éléments par discipline et par type, les niveaux, la phase, et seulement ce que montre la vue 3D. L’IFC sort en IFC4 ou IFC4X3, les parties d’un élément en couches sous lui ou un corps chacune ; Fragments depuis le point de base du projet, l’origine interne ou le point topographique.

![Exporter IFC ou Fragments](shots/export-exchange.webp)

## DXF et DWG

**Importer DXF…** fait entrer un dessin de CAO dans la vue au premier plan, avec ses calques, ses hachures, ses textes et ses cotes. Il arrive sous forme de dessin auquel vous pouvez vous accrocher et que vous pouvez modifier. Pour utiliser un DWG, enregistrez-le d’abord en DXF depuis votre logiciel de CAO (AutoCAD, BricsCAD ou DraftSight : **Save As → DXF**). L’import de PDF n’est pas encore disponible. Dans un plan, **Placé : par les coordonnées de la carte** pose un dessin en coordonnées réelles (un plan cadastral en Stereo 70) là où la carte le place (voir [Le site et ses données](site.md)).

## Liens

**Lier IFC…** garde un autre fichier lié au lieu de l’importer. Il est en lecture seule et rechargé quand il change. Cela fonctionne aussi pour un DXF. **Gérer → Liens** affiche, recharge, convertit ou supprime les liens.

## Modèles 3D

**Export 3D (glTF, USD, DAE)…** écrit le modèle sous forme de surfaces pour le rendu, la visualisation et la réalité augmentée (RA) :

| Format | Pour |
|---|---|
| GLB, glTF | Blender, three.js, visionneuses web. Les images propres à vos matériaux sont incluses. |
| USDZ | AR Quick Look d’Apple, Reality Composer, Omniverse. Images incluses. |
| USDA | OpenUSD en texte, un seul fichier. |
| DAE | COLLADA, pour SketchUp et des outils plus anciens. |

**Importer un maillage (OBJ, STL, glTF)…** fait entrer un maillage d’un autre logiciel 3D (OBJ, STL, glTF ou GLB) sous forme de références sur le niveau actif. glTF indique son unité et l’axe vers le haut ; pour OBJ et STL, Lapin le demande. **Faire un solide Studio** dans Studio rend une référence modifiable (voir [Studio](studio.md)).

![Exporter 3D](shots/export-3d.webp)

Vous choisissez ce qu’il faut exporter, quelle phase et l’origine. **Quoi** vaut **tout**, ou le modèle **tel que la vue le montre** : la vue 3D au premier plan, sans ses éléments et catégories masqués ni les niveaux qu’elle n’affiche pas, avec les couches qu’elle affiche (sans les finitions, ou le noyau seulement). Quand cette vue est coupée (la coupe au-dessus des niveaux affichés, une coupe affichée en 3D, la zone de coupe), **Coupe** coupe aussi l’export à cet endroit et ferme chaque coupe dans ses matériaux, si bien que le fichier ressemble à l’écran. Une vue 3D qui omet quelque chose est exportée telle qu’elle s’affiche, sauf si vous en décidez autrement. Chaque élément garde sa classe IFC, son type et son GlobalId. Pour échanger le modèle lui-même plutôt que ses surfaces, utilisez **Exporter IFC…**.
