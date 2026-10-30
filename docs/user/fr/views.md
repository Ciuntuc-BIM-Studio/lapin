---
title: Coupes, élévations et 3D
order: 70
summary: Créez des coupes et des élévations, ouvrez des vues 3D, et réglez ce que montre chaque vue et comment.
---

Toutes les vues se trouvent dans l’arborescence du projet, classées sous leur discipline : Architecture, Structure ou Fluides. Double-cliquez sur une vue pour l’ouvrir dans un onglet.

![L’arborescence du projet avec une coupe et une élévation](shots/browser.webp)

## Coupes

Choisissez **Vue → Coupe** (<kbd>SE</kbd>), ou cliquez sur le **+** à côté de **Coupes** dans l’arborescence. Tracez ensuite la ligne de coupe dans un plan en deux clics, puis cliquez une troisième fois du côté vers lequel regarde la coupe. La coupe est dessinée à partir du modèle et reste à jour à mesure que vous le modifiez.

![La coupe A-A à travers la maison](shots/section-view.webp)

Sélectionnez la coupe dans l’arborescence pour régler son échelle, sa profondeur, sa discipline et son style dans le panneau des propriétés.

## Élévations

Le **+** à côté de **Élévations** crée d’un coup les quatre élévations du bâtiment.

![L’élévation sud](shots/elevation-view.webp)

## Vues 3D

<!-- video section-3d -->

Double-cliquez sur **3D** pour ouvrir la vue 3D. Faites glisser avec le bouton gauche pour tourner autour du modèle. Le plan de repérage en haut à droite montre d’où vous regardez. **Plan**, **Persp** et **Ajuster** changent la caméra. Pour garder une caméra, dupliquez la vue 3D.

### Une coupe en 3D

Faites un clic droit sur une coupe dans l’arborescence et choisissez **Afficher en 3D**. La vue 3D coupe le modèle le long de la ligne de la coupe. Elle garde le côté vers lequel regarde la coupe, jusqu’à la profondeur de la coupe si elle en a une. Chaque élément coupé est fermé là où il est coupé, couche par couche, chaque couche dans son propre matériau : un mur montre son noyau en brique et son enduit, aussi plein que l’est le mur. **Hachures** sur la puce en bas de la vue 3D pose à la place le dessin de la coupe sur la section, avec les remplissages et les hachures de coupe qu’imprime une feuille. Les cotes, les repères de niveau, les étiquettes et les textes de la coupe se trouvent sur le même plan, à la même taille et au même endroit que dans la coupe. Les lignes gardent leurs épaisseurs de plume et leurs motifs de tirets complets. Celles dessinées à côté du bâtiment se dressent dans l’air à côté de la coupe. **Annotations** sur la puce en bas de la vue 3D les masque ou les affiche de nouveau. La commande **Afficher en 3D** d’une élévation tourne la caméra pour regarder dans le même sens que l’élévation.

![Le menu contextuel de la coupe dans l’arborescence](shots/section-3d-menu.webp)

![La coupe B-B vue en 3D](shots/section-3d.webp)

Pour revoir tout le modèle, cliquez sur le **×** de la puce **Coupe** en bas de la vue 3D, ou choisissez de nouveau **Afficher en 3D**.

![La coupe B-B avec Hachures activé : la coupe telle que la feuille l’imprime](shots/section-3d-hatches.webp)

### La zone de coupe

**Vue → Zone de coupe** (raccourci BX) coupe la vue 3D selon une boîte. La boîte entoure d’abord tout le modèle. Chaque face a une flèche en son milieu : faites-la glisser pour déplacer cette face. Chaque élément que coupe la boîte est fermé sur cette face, couche par couche, dans ses matériaux, et les faces se rejoignent proprement aux arêtes de la boîte. **Couper au plan** place le dessus de la boîte à la hauteur de coupe du plan du niveau actif (sa plage de vue), de sorte que le niveau se lit en 3D comme son plan le coupe. **Ajuster** remet la boîte autour de tout le modèle. Pour faire tourner la boîte, faites glisser la poignée ronde au-delà de son coin supérieur (degré par degré ; maintenez Shift pour des pas de 15°), ou sélectionnez un mur et cliquez sur **Aligner** : la boîte tourne dans le sens du mur. **Hachures** dessine les coupes de la boîte comme le fait une feuille, chaque matériau avec son remplissage et sa hachure de coupe, au lieu des matériaux. Une vue 3D enregistrée garde sa zone de coupe : enregistrez la vue avec la boîte active, ou ouvrez une vue enregistrée et réglez sa boîte, et la boîte est là chaque fois que la vue s’ouvre. Le **×** de la puce **Zone de coupe** désactive la boîte.

![La zone de coupe coupée au plan du rez-de-chaussée, sa face est rentrée](shots/section-box.webp)

![La même boîte avec Hachures activé : les coupes telles qu’une feuille les dessine](shots/section-box-hatches.webp)

### Un niveau à la fois

Pour regarder à l’intérieur du bâtiment, n’affichez que certains de ses niveaux. Faites un clic droit sur un niveau dans l’arborescence du projet et choisissez **Afficher seulement ce niveau en 3D**, ou **Afficher ce niveau et ceux du dessous en 3D** pour voir le bâtiment coupé à ce niveau. Le niveau devient le niveau actif, et la vue 3D suit le niveau actif : cliquez sur un autre niveau dans l’arborescence pour monter ou descendre d’un étage.

![Le menu contextuel du niveau dans l’arborescence](shots/storeys-3d-menu.webp)

![Le rez-de-chaussée seul en 3D](shots/storeys-3d.webp)

**Vue → Niveaux affichés**, ou les propriétés de la vue 3D, règlent le reste :

- **Afficher** : tous les niveaux, les niveaux cochés, le niveau actif seulement, ou le niveau actif et ceux du dessous.
- **Sur aucun niveau** : ce qui ne se trouve sur aucun niveau (le site, le terrain, les fichiers liés) est affiché ou masqué.
- **Couper au-dessus de son niveau** : le modèle est coupé à cette distance au-dessus du plus haut niveau affiché, 1.20 m au départ, comme un plan le coupe. Chaque élément coupé là est fermé sur la coupe, couche par couche, dans ses matériaux, comme l’est une coupe affichée en 3D.

![Le rez-de-chaussée coupé à 1.20 m : ses murs fermés, couche par couche](shots/storeys-3d-cut.webp)

Les niveaux masqués ne sont pas dessinés, et vous ne pouvez ni les sélectionner, ni cadrer sur eux, ni y marcher. Ce qu’une coupe enlève (la coupe au-dessus des niveaux affichés, une coupe affichée en 3D, la zone de coupe) n’est pas là non plus pour une visite : vous passez à côté de la partie haute d’un mur que la coupe a enlevée, et les fermetures sur la coupe ferment ce qu’elle ouvre. **Fichier → Exporter 3D…** prend la vue 3D telle qu’elle s’affiche (voir [Import et export](import-export.md)). Une vue 3D enregistrée garde son propre choix. La puce **Niveaux** en bas de la vue 3D indique ce qui est affiché ; son **×** affiche de nouveau tous les niveaux.

### Sans les finitions, ou les noyaux seulement

Une vue 3D peut montrer la structure du bâtiment seule, comme le fait un plan de structure. Dans les propriétés de la vue 3D (sans rien de sélectionné), ou dans **Vue → Paramètres de vue** avec la vue 3D au premier plan, **Couches** affiche :

- **toutes les couches** : les murs, les dalles et les semelles tels qu’ils sont construits ;
- **sans les finitions** : sans les couches dont le matériau est une finition (enduit, crépi, peinture, carrelage, plaques, revêtements de sol, ou un matériau de catégorie Finition dans Gérer → Matériaux) ; l’isolation reste ;
- **le noyau seulement** : seulement la couche marquée comme noyau (la brique, la dalle en béton).

![Une maison en 3D avec le noyau seulement : les murs sont leur brique, les dalles leur dalle en béton](shots/layers-3d-core.webp)

Chaque couche conservée reste un solide fermé dans son propre matériau, si bien qu’une coupe ou la zone de coupe la ferme comme avant. Les portes et les fenêtres restent dans leurs baies ; un tableau habillé d’une finition part avec les finitions du mur. Une vue 3D prend ses couches de son style, comme un plan : réglez sa **Discipline** sur Structure et elle ne montre que les noyaux ; réglez **Couches** pour choisir autrement, et le bouton à côté revient à celles du style. La puce **Couches affichées** en bas de la vue 3D indique ce qui est laissé de côté ; son **×** affiche de nouveau toutes les couches. Les couches masquées ne sont ni sélectionnées ni heurtées pendant une visite, et **Fichier → Exporter 3D…** telle que la vue l’affiche exporte les mêmes couches.

### Entrer dans une pièce

Dans un plan, faites un clic droit à l’intérieur d’une pièce et choisissez **Visite en 3D**. La vue 3D vous place dans la pièce, 1.60 m au-dessus de son sol, regardant le long de son plus grand côté.

![Le menu contextuel d’une pièce dans le plan](shots/room-walk-menu.webp)

![Debout dans le séjour](shots/room-walk.webp)

- Faites glisser avec le bouton gauche pour regarder autour de vous.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> ou les flèches font marcher. Maintenez <kbd>Shift</kbd> pour courir. <kbd>Space</kbd> fait sauter.
- La molette de la souris avance et recule pas à pas.

Vous marchez comme le ferait une personne. Les murs, les poteaux, les meubles et les fenêtres fermées vous arrêtent, et vous glissez le long d’eux. Vous vous tenez sur les dalles, montez et descendez les escaliers et les rampes, et tombez si vous sortez d’un bord. Les portes sont ouvertes pour vous : passez par leurs baies. Seul ce que dessine la vue 3D vous arrête : les éléments masqués, les niveaux qu’elle n’affiche pas et ce que ses coupes enlèvent ne vous gênent pas.

![En marchant vers la porte de la chambre](shots/room-walk-door.webp)

![En haut de l’escalier](shots/room-walk-stair.webp)

**Voler** dans la puce de la visite, ou <kbd>F</kbd>, vous permet de voler à la place : pas de gravité, et vous traversez les murs et les dalles. <kbd>E</kbd> et <kbd>Q</kbd> font monter et descendre. Appuyez de nouveau sur <kbd>F</kbd> pour marcher ; vous retombez sur la dalle sous vous.

<kbd>Esc</kbd> ou **Quitter la visite** rétablit la caméra que vous aviez avant.

## Ce que montre une vue

L’onglet **Vue** règle ce que montre la vue au premier plan et son aspect.

![L’onglet Vue](shots/ribbon-view.webp)

- **Paramètres de vue.** La discipline et le style de la vue, sa plage (la hauteur et la profondeur de coupe) et ses graphismes.
- **Styles de vue.** Architectural, Structurel (les noyaux seulement), Fluides ou les vôtres. Chaque style choisit les couches dessinées (toutes, sans les finitions, ou le noyau) et la façon dont les matériaux se joignent.
- **Symboles 2D IFC.** Les éléments importés ou liés depuis un fichier IFC qui apportent leur propre dessin en plan (une emprise, le débattement d’une porte, le symbole d’un appareil, avec les couleurs, remplissages et textes du fichier) le dessinent dans les plans au lieu de leur coupe. Décochez-le dans Paramètres de vue (ou dans un style de vue) pour dessiner plutôt leur coupe 3D.
- **Visibilité** (<kbd>VV</kbd>) et **Graphismes** (<kbd>VG</kbd>). Ils affichent ou masquent des catégories et règlent leurs plumes, types de ligne et remplissages.
- **Scinder** et **Fenêtre.** Ils affichent deux vues côte à côte, ou ouvrent une vue dans une fenêtre séparée.
