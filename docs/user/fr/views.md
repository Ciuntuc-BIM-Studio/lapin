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

Une coupe n’est pas forcément une seule ligne droite. **Coupe brisée** (<kbd>SJ</kbd>) la dessine comme une polyligne, clic après clic, et <kbd>Entrée</kbd> termine : chaque tronçon droit est une coupe plane qui ne voit qu’entre ses propres extrémités, et les tronçons sont dessinés côte à côte à leur longueur réelle, un trait fin en pointillé marquant chaque décrochement. **Coupe courbe** (<kbd>SV</kbd>) dessine un arc par trois points (son début, sa fin, un point dessus) et le développe de la même façon, pour qu’une façade courbe se lise à sa vraie longueur. **Coupe le long de** (<kbd>SA</kbd>) choisit un élément ayant une ligne en plan, une route ou une allée, un fossé, un mur de soutènement, un mur, une poutre, une semelle ou une polyligne dessinée, et le suit tel qu’il est ; elle écrit les stations en bas (0+020, tous les dixièmes de l’échelle, ou le pas que vous fixez sous **Stations** dans les réglages de la vue). En plan, les points d’une coupe sont des poignées : glissez-en un, glissez le milieu d’un tronçon pour ajouter un décrochement, Ctrl/⌘-clic sur un point pour le retirer. Montrée en 3D, une coupe développée coupe le modèle le long de son premier tronçon et pose son dessin tronçon par tronçon.

![Une coupe brisée à travers la maison : trois tronçons côte à côte, les décrochements marqués](shots/section-jogged.webp)

Sélectionnez la coupe dans l’arborescence pour régler son échelle, sa profondeur, sa discipline et son style dans le panneau des propriétés.

### Où une coupe est repérée

La ligne d’une coupe est dessinée dans chaque plan, et la coupe est repérée dans chaque autre coupe et élévation que son plan traverse : une fine ligne en tirets là où le plan coupe la vue, avec le nom de la coupe et une flèche dans la direction où elle regarde, en haut, de la même taille sur le papier que dans un plan. Une coupe n’est jamais repérée dans elle-même. Un clic sur un repère sélectionne la coupe, un double-clic l’ouvre.

**Taille des repères.** **Gérer → Normes de dessin → Projet** règle la taille des repères, en millimètres sur papier, pour les coupes et les élévations séparément : **Taille du repère de coupe** (les flèches d’une coupe en font 0,7) et **Texte du repère de coupe**, **Taille du repère d’élévation** (le diamètre du cercle de l’élévation) et **Texte du repère d’élévation** ; 10 mm et la hauteur du style de texte standard tant que vous ne les réglez pas. Un projet qui avait une seule taille de repère la garde pour les deux. Une coupe ou une élévation de taille propre a **Taille du repère (mm)** et **Texte du repère (mm)** dans ses propriétés ; vides, elle prend ceux du projet. Le moteur dessine les repères, donc les plans, les coupes qu’ils traversent, les feuilles, le PDF et le DXF les suivent.

**Déplacer une coupe.** Sélectionnez la coupe dans un plan et déplacez-la avec **Déplacer** (ou glissez-la par sa ligne) : comme dans Revit, elle coupe là où vous la mettez, et pivotée ou symétrisée elle regarde du côté pivoté ou symétrisé. Chaque tête (sa flèche et son nom) a sa propre poignée : glissez-la pour écarter la tête de l’extrémité de la ligne, par exemple pour la dégager d’une cote, et un fin trait de rappel la relie de nouveau ; la coupe coupe au même endroit. Un clic avec <kbd>Ctrl</kbd>/<kbd>⌘</kbd> sur la poignée remet la tête en place. Une coupe en travers ou le long d’un élément suit l’élément et se déplace avec lui.

Chaque coupe est sur un **calque de repères** (**Sections** au départ ; **Calque du repère** dans les propriétés de la coupe le change), et chaque vue choisit ce qu’elle repère. **Vue → Coupes…** liste les coupes du projet par calque pour la vue au premier plan, avec une case par calque et par coupe pour cette vue seulement, le calque de chaque coupe (un calque existant, ou **Nouveau calque…**) et dans combien de vues chacune est masquée. Une feuille repère ce que ses vues repèrent.

Une coupe faite en travers d’une poutre ou d’un poteau (**Coupe ici**, **Coupe transversale** dans l’onglet Ferraillage simple) a aussi des poignées dans le plan : glissez l’une des extrémités le long de la ligne pour donner à ce côté plus ou moins de marge, et le milieu le long de l’élément pour déplacer la station où elle passe. La fenêtre de la coupe suit.

![Vue → Coupes… pour l’élévation sud](shots/sections-dialog.webp)

## Élévations

Le **+** à côté de **Élévations** crée d’un coup les quatre élévations du bâtiment.

![L’élévation sud](shots/elevation-view.webp)

Chaque plan repère chaque élévation placée devant le bâtiment, à l’écart des têtes de coupes qui s’y trouvent : une fine ligne en traits mixtes en travers de sa direction de regard, et en son milieu un cercle dont la flèche noire pointe vers le bâtiment, avec le nom de l’élévation derrière. Un clic sur le repère sélectionne l’élévation, un double-clic l’ouvre. **Repères d’élévation** dans **Visibilité / Graphismes** (<kbd>VV</kbd>) les masque dans une vue.

**Jusqu’où va une élévation.** Sélectionnez l’élévation dans un plan : sa ligne a une poignée à chaque extrémité. Glissez-en une le long de la ligne pour allonger ou raccourcir l’élévation de ce côté ; l’élévation ne dessine alors que ce qui se trouve entre les deux extrémités, et ses feuilles et le PDF suivent. Chaque glissement est une étape d’annulation. Tant que vous ne les glissez pas, la ligne couvre tout le bâtiment et grandit avec lui.

**Déplacer le repère d’une élévation.** Glissez la poignée du cercle, ou sélectionnez l’élévation et utilisez **Déplacer** : le repère va là où vous le mettez, plus loin du bâtiment ou le long de sa ligne, en une étape d’annulation. Une élévation de Lapin regarde toujours tout le bâtiment de l’extérieur, donc déplacer son repère ne change jamais ce qu’elle montre (dans Revit, la ligne du repère est aussi l’endroit où l’élévation commence à voir ; ici sa portée a ses propres poignées, et une coupe à l’intérieur du bâtiment est une coupe). Une élévation regarde au nord, au sud, à l’est ou à l’ouest, donc elle n’est ni pivotée ni symétrisée. Une élévation ou une coupe épinglée (**Modifier → Épingler**) n’est pas déplacée tant qu’elle n’est pas désépinglée.

![Le repère d’une élévation dans un plan, écarté de la maison, sa ligne raccourcie, la tête d’une coupe écartée de son extrémité](shots/elevation-marker.webp)

### Ombres du soleil

Une façade, une coupe ou un plan de situation peuvent dessiner les ombres que le soleil porte sur ce qu’ils voient, remplies sous leurs traits, de sorte qu’elles s’impriment sur les planches, en PDF et DXF. Elles se choisissent dans les propriétés de la vue, sous **Ombres du soleil** :

- **soleil propre** : un soleil de la vue. Par défaut la convention de dessin : le soleil par-dessus l’épaule gauche, ses rayons selon la diagonale d’un cube (depuis 45°, à 35,26° de hauteur), ainsi une ombre est aussi profonde que ce qui la porte est saillant. **Soleil depuis** et **Hauteur du soleil** le tournent ; **Convention de dessin** le remet.
- **soleil du projet** : le soleil du lieu du projet (Gérer → Emplacement) un jour et à une heure de la vue. **Comme dans Soleil et ombres** prend le jour et l’heure de Vue → Soleil et ombres.

Une coupe porte et reçoit des ombres seulement de ce qui est au-delà de son plan de coupe. Un plan de niveau ne dessine pas d’ombres : ses murs, coupés à la hauteur du plan, les porteraient trop courtes.

![La façade sud avec l’ombre du toit](shots/elevation-shadows.webp)

![Les ombres du soleil d’une vue](shots/view-shadows.webp)

### Hachures de surface

Une élévation ou une coupe hachure chaque face qu’elle voit avec la hachure **Elevation** de son matériau (**Gérer → Matériaux**, onglet **2D graphics**) : motif, échelle, angle, plume et remplissage. Seule la partie d’une face qu’aucun élément plus proche ne cache est hachurée : l’enduit ou la brique d’une façade apparaît autour des portes et fenêtres, et une coupe hachure ce qu’elle voit au-delà de son plan de coupe, pas derrière les éléments qu’elle coupe. Un mur multicouche montre le matériau de sa couche extérieure. La hachure est dessinée sous les traits, elle s’imprime donc sur les planches, en PDF et en DXF (calques SURFACE-HATCH et SURFACE-FILL).

Avec **Alignment** *view*, le motif continue d’une face à l’autre ; avec *layer*, il se pose sur la face elle-même, le long de son horizontale depuis son coin bas, et une face vue en biais le montre raccourci. Un matériau qui continue dans un même plan sur plusieurs éléments est hachuré d’un seul tenant, sauf si **Join materials** du style de vue est désactivé.

Tant que vous n’en choisissez pas, la hachure d’élévation d’un matériau vient **d’après le nom**, comme sa hachure de coupe, et **Gérer → Materials** indique laquelle (*d’après le nom (BRICK)*) : brique et maçonnerie BRICK, pierre STONE, bois BOARDS, bardage métallique VERTICAL, tuiles, bardeaux et ardoises HORIZONTAL, carrelage TILES, gravier et terre comme en coupe. L’isolant, les membranes, l’enduit, la peinture et le béton n’en reçoivent aucune : le BATT de l’isolant est une hachure de coupe, jamais proposée pour une surface (choisie à la main, elle est dessinée). Choisissez **none** pour ne rien dessiner. Une coupe développée (brisée, courbe ou le long d’un élément) hachure chacun de ses morceaux entre ses extrémités, et y dessine de même les ombres du soleil. **Surface hatches** du style de vue (**Vue → View settings**, **Gérer → View styles**) les désactive pour des vues entières : activé en Architectural, désactivé en Structural, MEP et Coordination. Les plans gardent leurs propres hachures de coupe et de plan. La couverture d’un toit est hachurée selon son matériau : un toit de tuiles montre ses rangs sur les versants que la vue voit, et aucun sur ceux qui lui tournent le dos.

![L’élévation sud, sa brique de parement hachurée d’après son nom](shots/elevation-surface-hatch.webp)

![Une coupe brisée hachure le mur de jardin que voit son dernier morceau](shots/section-developed-surface-hatch.webp)

![Les hachures de surface dans les réglages de la vue](shots/view-surface-hatch.webp)

## Vues 3D

<!-- video section-3d -->

Double-cliquez sur **3D** pour ouvrir la vue 3D. Faites glisser avec le bouton gauche pour tourner autour du modèle. Le plan de repérage en haut à droite montre d’où vous regardez. **Plan**, **Persp** et **Ajuster** changent la caméra. Pour garder une caméra, dupliquez la vue 3D.

La molette zoome vers le point du modèle sous le curseur, qui reste où il est à l’écran ; le zoom s’arrête juste avant ce point et ne le traverse jamais. Un glisser avec le bouton gauche tourne la vue autour du centre des éléments sélectionnés, qui reste où il est à l’écran, pour les voir de tous les côtés ; sans sélection, elle tourne autour du point du modèle sous le curseur là où le glisser commence, et autour du centre de la vue s’il n’y a rien sous le curseur. **Vue → Zoom vers le curseur** et **Vue → Orbite autour de la sélection** désactivent chacun (la molette zoome alors vers le centre de la vue, et la vue tourne autour de son centre), tout comme les cases des propriétés d’une vue 3D. Le choix est gardé sur cet ordinateur.

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

### Réaliste, avec le soleil

Vue → **Qualité d’affichage** règle le rendu de la vue 3D, sur cet ordinateur :

- **Dessin** : la palette du plan et une lumière égale, comme Lapin a toujours dessiné. Rapide sur tout ordinateur.
- **Moyen** : les ombres du soleil, l’ombre dans les angles où les faces se rencontrent, et la lumière du ciel.
- **Réaliste** : le ciel derrière le modèle et un sol dessous (là où le projet n’a pas de terrain), et un vitrage que la lumière traverse. Pour présenter, avec une bonne carte graphique.

![Une maison dans la vue réaliste](shots/view-realistic.webp)

Le soleil est celui du projet : où il se trouve pour le lieu du projet (Gérer → **Emplacement**) un jour à une heure. Vue → **Soleil et ombres** règle le jour (l’équinoxe et les solstices sont à un clic) et l’heure, joue la journée et allume le soleil aussi dans la vue de dessin. Sans système de coordonnées ni point de levé, le lieu est Bucarest. Le fuseau horaire suit la longitude du lieu, avec l’heure d’été européenne en Europe.

![Soleil et ombres](shots/sun-dialog.webp)

La vue réaliste reprend certains chiffres (l’ombre dans les angles, la qualité des ombres, la couleur du soleil) de [BimGo](https://github.com/aussieBIMguru/BimGo), d’Aussie BIM Guru (MIT-0).

## Modéliser en 3D

La vue 3D est un lieu où l'on modélise, pas seulement où l'on regarde : les éléments s'y déplacent et s'y copient comme en plan, accrochés aux coins, arêtes et faces des autres éléments.

### Déplacer et copier en 3D

Sélectionnez les éléments, puis **Move** (M) ou **Copy** (CO), la vue 3D devant. Cliquez le point de base : un coin, une arête ou une face de ce que vous déplacez. Le curseur dit à quoi il s'accroche : **Endpoint** (extrémité), **Midpoint** (milieu), **On edge** (sur l'arête), **On face** (sur la face) ou **On level** (sur le niveau) dans le vide ; ce qui est derrière la face sous le curseur n'est pas pris. Les lignes d'un DXF importé, affiché en 3D, accrochent aussi. Cliquez ensuite où il va, par exemple le coin d'une dalle : la sélection suit le curseur en 3D, vers le haut et le bas aussi, et y est posée. Depuis le point de base, les axes rouge, vert et bleu sont déduits quand le curseur en suit un ; → ← ↑ verrouillent l'axe rouge, vert ou bleu (la même touche le libère), Maj maintient l'axe déduit. Un axe verrouillé, un coin survolé est ramené sur l'axe, pour aligner le déplacement sur lui sans y aller.

Tapez une valeur à tout moment, sans cliquer dans une case : une longueur va dans la direction du curseur (1200), et dx,dy ou dx,dy,dz est le déplacement lui-même (0,0,450 monte de 450 mm). Entrée l'applique, Échap annule. Copy continue de copier depuis le même point de base jusqu'à Entrée ou Échap.

Ce qui a une hauteur propre monte ou descend avec le déplacement : la base d'un mur, d'un poteau ou d'un objet, la base et le haut d'un escalier (sa hauteur gardée), le dessus d'une poutre, le décalage haut d'une dalle, la sablière d'un toit ; un solide Studio reçoit une étape de déplacement. Une porte ou une fenêtre glisse le long de son mur.

Ce qui repose sur son niveau (un mur, un escalier, un poteau, une dalle, une poutre, un garde-corps, un toit, un meuble, une porte ou une fenêtre) garde son niveau quand le déplacement s'accroche d'un point à un autre : il y va en plan seulement, de sorte qu'un escalier déplacé par son coin haut sur le coin d'une dalle ne monte pas des quelques centimètres entre la structure de l'escalier et le revêtement de la dalle (le haut d'un escalier arrive déjà au nu du revêtement du niveau qu'il dessert). Pour le monter ou le descendre, suivez ou verrouillez l'axe bleu (↑), ou tapez dx,dy,dz ; l'étiquette indique quand le niveau est gardé. Sans rien de sélectionné, Move et Copy demandent les éléments : cliquez-les en 3D, puis Entrée.

Vous pouvez aussi appuyer sur un élément sélectionné et le faire glisser : il se déplace de la même façon, depuis le point appuyé, accroché, et se pose où vous relâchez. **View → Drag to move in 3D** désactive cela, pour qu'un glisser sur la sélection fasse orbiter la vue.

![Un escalier déplacé en 3D par le coin de sa marche haute sur le coin de la dalle](shots/model-3d-move.webp)

### Poser des éléments en 3D

Tout outil qui prend des points en plan fonctionne aussi dans la vue 3D : murs, murs-rideaux, dalles, toits, plafonds, trémies, pièces, poteaux, poutres, fondations, garde-corps, plinthes, mobilier, ascenseurs, routes, plantes et les autres. Les points vont sur le niveau actif : le curseur s'accroche au modèle comme pour un déplacement (le coin d'une dalle, le bout d'une poutre, le milieu d'une arête, les lignes d'un DXF) et le point est pris juste en dessous, sur le niveau ; une ligne pointillée montre où, quand l'accroche est au-dessus ou en dessous. Les points et le contour qu'ils forment sont dessinés sur le niveau au fur et à mesure : la ligne d'un mur, le contour d'une dalle qui se referme sur son premier point, un arc par son troisième point. Depuis le dernier point, les axes rouge et vert sont déduits ; → et ← les verrouillent.

Tapez une valeur à tout moment : une longueur part du dernier point dans la direction du curseur (3000, ou 3000<30 avec un angle), largeur,hauteur d'un rectangle depuis son premier coin. Entrée l'applique, ferme une dalle ou termine un garde-corps ; Retour arrière retire le dernier point ; Échap abandonne les points et, la seconde fois, l'outil. Un glisser fait toujours tourner la vue, pour tourner autour du modèle entre deux clics. La puce du bas indique le niveau où vont les points.

Les portes et fenêtres se posent là où vous cliquez la face d'un mur en 3D, à l'endroit cliqué le long du mur.

![Un mur commencé au coin de la dalle en 3D, l'axe vert déduit depuis son début](shots/model-3d-wall.webp)

### Poignées et composants en 3D

Les poignées d'un élément sélectionné (les extrémités d'un mur, les coins d'une dalle) se tirent en 3D comme en plan et s'accrochent comme avec Move : d'abord un coin, puis un milieu, puis une arête à portée, puis les axes rouge et vert par le point de départ de la poignée, puis une face. Le point est marqué et nommé au curseur et pris à la hauteur de la poignée. Tapez une longueur ou un angle pendant le glisser, comme en plan ; Entrée y pose la poignée.

Faites glisser une carte du **Catalog** ou un outil du ruban sur la vue 3D : son outil démarre avec ce type. Un outil qui pose d'un clic (un poteau, une semelle isolée, un meuble, un ascenseur, une plante) est posé aussitôt là où vous le lâchez, accroché au modèle. Lâché, ou cliqué, sur la dalle d'un autre niveau, l'élément va sur ce niveau : une table posée sur la dalle du premier étage appartient au premier étage, quel que soit le niveau actif.

## Ce que montre une vue

L’onglet **Vue** règle ce que montre la vue au premier plan et son aspect.

**Grid** (<kbd>F7</kbd>) masque ou montre la grille de fond du plan, et **Traits fins** (<kbd>TL</kbd>) dessine chaque trait d’un pixel, pour lire un dessin chargé. Les deux ne valent que pour l’écran : les planches, le PDF et le DXF gardent leurs plumes, et l’accrochage à la grille reste. La grille au sol de la vue 3D suit le même interrupteur.

![L’onglet Vue](shots/ribbon-view.webp)

- **Paramètres de vue.** La discipline et le style de la vue, sa plage (la hauteur et la profondeur de coupe) et ses graphismes.
- **Styles de vue.** Architectural, Structurel (les noyaux seulement), Fluides ou les vôtres. Chaque style choisit les couches dessinées (toutes, sans les finitions, ou le noyau) et la façon dont les matériaux se joignent.
- **Symboles 2D IFC.** Les éléments importés ou liés depuis un fichier IFC qui apportent leur propre dessin en plan (une emprise, le débattement d’une porte, le symbole d’un appareil, avec les couleurs, remplissages et textes du fichier) le dessinent dans les plans au lieu de leur coupe. Décochez-le dans Paramètres de vue (ou dans un style de vue) pour dessiner plutôt leur coupe 3D.
- **Visibilité** (<kbd>VV</kbd>) et **Graphismes** (<kbd>VG</kbd>). Ils affichent ou masquent des catégories et règlent leurs plumes, types de ligne et remplissages.
- **Scinder** et **Fenêtre.** Ils affichent deux vues côte à côte, ou ouvrent une vue dans une fenêtre séparée.
- **Qualité d’affichage.** Le rendu de la vue 3D (**Dessin**, **Moyen** ou **Réaliste**, gardé sur cet ordinateur : voir *Réaliste, avec le soleil*) et comment la vue 3D dessine les pièces de nombreux triangles (une installation importée, du mobilier, des tuyaux) quand elles sont loin. **Équilibrée** dessine une pièce éloignée plus grossièrement seulement là où l’écart reste sous un pixel. **Rapide** la laisse s’écarter jusqu’à 3 pixels, pour les grands modèles et les ordinateurs plus lents. **Précise** dessine chaque pièce telle quelle. Le réglage appartient au projet. Le modèle, ses dessins, ses exports et ses quantités ne sont jamais simplifiés : une courbe est faite de morceaux droits qui s’en écartent d’un millimètre au plus.
- **Soleil et ombres.** Le soleil du projet un jour à une heure, qui éclaire la vue 3D avec ses ombres (voir *Réaliste, avec le soleil*).

![Qualité d’affichage](shots/display-quality.webp)

### Visibilité / Graphismes

**Visibilité** (<kbd>VV</kbd>) ou **Graphismes** (<kbd>VG</kbd>) ouvre la visibilité / les graphismes de la vue au premier plan : un plan, une coupe, une élévation ou une vue 3D. Ce qu’elle masque l’est aussi sur les feuilles de la vue, dans son PDF et son DXF, et dans un export 3D.

- **Catégories de modèle.** Chaque catégorie est affichée ou masquée, avec sa couleur et son épaisseur de ligne, son remplissage et sa hachure de coupe. La flèche à côté d’une catégorie ouvre ses sous-catégories : le **Noyau**, les **Finitions**, l’**Isolation**, les **Membranes** et les **Autres couches** d’un mur, d’un plancher ou d’une toiture (selon le matériau de chaque couche et sa catégorie dans Gérer → Matériaux), les **Tableaux** d’une porte ou d’une fenêtre. Décochez **Finitions** sous **Murs** : l’enduit disparaît, la brique reste. Les armatures, les revêtements, les coffrages, les échafaudages, les fluides, les assemblages acier, les ascenseurs et les protections solaires sont des catégories à part.
- **Catégories d’annotation.** Cotes, textes, étiquettes, étiquettes de pièce, quadrillages, niveaux, repères de coupe et d’élévation, bulles de détail, plans de référence, limites de vue et légendes, chacune affichée ou masquée dans cette vue.
- **Catégories importées.** Chaque DXF importé dans la vue, par son fichier : affiché, en demi-teinte, et ses calques un par un (la flèche à côté). Un PDF ou une image à décalquer s’affiche ou se masque ici aussi.
- **Filtres.** Par règles, comme dans Revit : **Nouveau filtre**, puis ses règles (classe IFC, catégorie, type, nom, matériau, niveau, groupe, lien, phase de création, type prédéfini ou un paramètre, comme *Résistance au feu* ou *Pset_WallCommon.IsExternal* ; est égal à, contient, commence par, supérieur, inférieur, a une valeur…), toutes ou l’une d’elles. La vue utilise les filtres de sa liste, dans l’ordre ; le premier qui prend un élément décide s’il est affiché. **Gérer → Filtres de vue** ouvre cet onglet.
- **Liens.** Chaque fichier lié, avec **Visible** et **Demi-teinte** pour cette seule vue. **Affichage** est **Selon la vue hôte** (le lien prend les catégories de cette vue) ou **Personnalisé** : **Catégories…** donne alors au lien ses propres catégories, sous-catégories et classes IFC dans cette vue. Le plan de l’architecte masque les armatures du lien de structure ; celui de l’ingénieur, les finitions du lien d’architecture.
- **Classes IFC.** Les classes des éléments propres au projet, affichées ou masquées.
- **Remplacements graphiques.** Une catégorie prend aussi un **Motif** pour ses lignes de coupe et de projection, **Demi-teinte** et une **Transparence** (en 3D, de 0 opaque à 95) ; une catégorie d’annotation une **Couleur** et **Demi-teinte** ; un filtre une **Couleur**, un **Motif**, **Demi-teinte** et une **Transparence** pour ce qu’il prend, le premier filtre de la liste donnant chaque champ. **Niveau de détail** (grossier, moyen, fin) règle ce que dessinent les symboles d’un plan, sinon selon l’échelle.
- **Niveau de développement (LOD).** Une manière de voir, jamais tirée de l’échelle : une vue dessine au niveau que lui donne son style (Gérer → Styles de vue : 350, un style de structure 400) ou au sien (Vue → Réglages de la vue, ou l’en-tête de Visibilité / Graphismes, pour une vue 3D aussi). **LOD 100** (volumes) dessine murs, planchers et toitures comme un seul contour hachuré comme leur âme, les profilés d’acier comme une seule ligne sur leur axe, et une porte ou une fenêtre comme sa baie et son ouverture ; **LOD 200** (couches) ajoute les couches, et les dormants et ouvrants des portes et fenêtres simplifiés (sans petits-bois ni quincaillerie) ; **LOD 300** (détails) leurs profils complets et leurs détails (tableaux, appuis, encadrements, seuils, tablettes, quincaillerie, volets, grilles) et les habillages des baies ; **LOD 350** (assemblages) les platines, boulons et ancrages des assemblages métalliques ; **LOD 400** (fabrication) les armatures d’une maquette de structure liée et la section réelle des profilés d’acier : congés, rayons de bout d’aile, ailes inclinées. Un projet fait auparavant garde ses dessins : chaque vue reçoit le niveau que lui donnait son échelle (1:50 LOD 400, 1:100 LOD 200, 1:200 LOD 100), une vue 3D LOD 400. Les murs et planchers d’un IFC lié ou importé sont découpés selon les compositions que leur donne leur fichier, si bien que leurs **Finitions**, leur **Isolation** et leur **Âme** se masquent comme celles d’un mur natif et que le dessin des seules âmes du style Structure les atteint ; l’enduit d’un mur apporté comme pièce à part, ou un revêtement d’une seule finition, va avec **Finitions**. Toute vue 3D laisse de côté les **Armatures** jusqu’à ce qu’on les affiche.

Les réglages appartiennent à la vue, ou à un jeu nommé que plusieurs vues partagent (**Enregistrer comme jeu…**). Un style de vue peut nommer un jeu (Gérer → Styles de vue) : une vue sans réglages propres prend le jeu de son style, comme les gabarits de vue de Revit ; **En faire ceux de cette vue** part de lui pour les siens.

![Visibilité / Graphismes : les finitions d’un mur masquées](shots/vg-categories.webp)

![Une structure liée, personnalisée dans une vue](shots/vg-links.webp)

![Un filtre masquant ce que prennent ses règles](shots/vg-filters.webp)

![Catégories d’annotation](shots/vg-annotations.webp)

![Un import DXF par calques](shots/vg-imports.webp)

![La couleur et le motif d’un filtre dans le plan](shots/vg-overrides.webp)

![Le plan avec son filtre](shots/vg-plan.webp)

![Un plan au LOD 350 de son style : les couches des murs et les détails de la fenêtre](shots/detail-fine.webp)

![Le même plan au LOD 100 : un seul contour hachuré comme l’âme](shots/detail-coarse.webp)

### Nuages de points dans les vues

Un nuage de points lié (*Import et export*, Nuages de points) apparaît dans toutes les vues. Un **plan** dessine la tranche que prend sa coupe, 10 cm autour du plan de coupe, en points sous les murs ; une **coupe** la tranche juste derrière son plan ; une **élévation** tous les points qu'elle regarde, le plus proche devant. Les points ont les couleurs du scan, assombries pour se lire sur papier, et s'impriment sur les planches et dans le PDF comme l'écran les montre. Leurs points sont des accrochages : des murs dessinés sur la tranche d'un scan tombent sur ses faces. Dans la **vue 3D** les outils de placement et Move s'accrochent aux points du nuage comme aux coins. Un nouveau plan s'ouvre cadré sur le modèle, ou sur la tranche du nuage quand il n'a rien d'autre ; seule la partie de la tranche à l'écran est dessinée, si bien que le relevé d'un quartier entier reste rapide en zoomant sur un bâtiment.

**Visibility / Graphics** (<kbd>VV</kbd>) a un onglet **Point clouds** : par nuage, pour cette vue seule, **Shown**, **Halftone**, ses **Colours** (comme son lien, RVB, intensité, altitude ou classification), la **Slice** que dessine un plan ou une coupe (cm, jusqu'à 100 m : les façades d'un relevé aérien demandent une tranche épaisse), et **Classes…** : les classes ASPRS de ses points (sol, végétation, bâtiment, bruit…), chacune visible ou masquée. L'onglet d'une vue 3D règle ce que dessine la 3D. **Export 3D…** prend un échantillon des nuages visibles (au plus 5 millions de points) en points dans GLB et glTF quand **Point clouds** est coché ; aucun autre export ne les porte.

![La tranche d'un scan en plan, un mur dessiné dessus](shots/point-cloud-plan.webp)

![Visibility / Graphics : la végétation d'un nuage masquée dans une vue](shots/point-cloud-vg.webp)
