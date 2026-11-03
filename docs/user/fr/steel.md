---
title: Acier
order: 56
summary: Barres en acier aux extrémités paramétrables — prolongées ou en retrait, coupées selon un plan, en onglet, ajustées sur une autre barre, grugées —, faites aussi d’un contour dessiné ; profilés creux et formés à froid ; bâtiments à portiques avec jarrets.
---

## L’onglet Acier

![L’onglet Acier](shots/ribbon-steel.webp)

- **Bâtiment à portiques…** : tout un bâtiment métallique à partir de quelques chiffres, dans le configurateur de bâtiment (ci-dessous).
- **Barre** (<kbd>SM</kbd>) : une barre en acier entre deux points, au niveau haut de l’étage, le dessus de sa section sur l’axe, comme une poutre. Son type se choisit dans Propriétés avant le premier clic, ou dans l’onglet **Catalogue**, où chaque type acier est dessiné par sa section.
- **Convertir en barre** : les contours dessinés sélectionnés deviennent des barres (ci-dessous).
- **Onglet** et **Ajuster** : deux barres assemblées par leurs extrémités (ci-dessous).
- **Types acier** : les types de barre du projet, et de nouveaux.

Une barre est un IfcMember : son axe va n’importe où dans l’espace (une diagonale, un chevron, un montant), sa section vient de son type, elle peut tourner autour de son axe (**Rotation**) et son axe peut passer par le centre, le dessus ou le dessous de la section (**Point d’insertion**). Les poutres et poteaux de la structure en profilé acier reçoivent les mêmes extrémités.

## Extrémités

Sélectionnez une barre, ou une poutre ou un poteau droit en profilé acier : sa section **Acier** dans Propriétés montre ses deux extrémités, **Début** et **Fin**. Chaque extrémité est un ensemble de règles ; le corps les suit.

![Les extrémités d’une barre dans Propriétés](shots/steel-ends.webp)

- **Prolongement** : de combien l’extrémité dépasse son point ; négatif, elle est en retrait (un jeu de montage).
- **Coupe** : **Perpendiculaire à l’axe** ; **Selon un plan**, tourné depuis la coupe droite **En plan** (autour de la verticale de la section) et **En élévation** (autour de son côté), en degrés ; **Onglet** avec une autre barre, sur le plan qui partage en deux l’angle entre elles ; **Ajusté sur une barre**, sur la face de l’autre barre qu’elle atteint (l’aile d’un poteau, ou le côté de son âme quand le poteau est tourné).
- **Grugeage** : les ailes grugées en **Haut** et/ou en **Bas**, sur la **Longueur du grugeage** depuis l’extrémité et la **Profondeur du grugeage** depuis le bord de la section, comme pour une solive assemblée sur une poutre maîtresse.

L’onglet et l’ajustement nomment l’autre barre : déplacez-la, l’extrémité suit ; supprimez-la, l’extrémité redevient droite et le dit. **Onglet** dans l’onglet Acier assemble en onglet les deux barres sélectionnées à l’angle où elles se rencontrent ; **Ajuster** ajuste la première sélectionnée sur la seconde.

## D’une esquisse à une barre

Dessinez le contour de la barre en polyligne fermée ou en rectangle (**Dessin**), puis **Acier → Convertir en barre** avec un ou plusieurs contours sélectionnés. En une seule étape chaque contour est lu et remplacé par une barre :

![Deux contours esquissés en plan](shots/steel-sketch.webp)

- ses **deux longs côtés parallèles** (à un demi-degré près) donnent l’axe, leur ligne médiane d’un bout à l’autre, et la **largeur** entre eux ;
- le **côté à chaque extrémité** donne la coupe de cette extrémité : droite, ou selon un plan à l’inclinaison du côté.

Sur un **plan** le contour est la barre vue de dessus : sa largeur est celle des ailes, et un profilé de cette largeur est cherché parmi les types de barre du projet et dans le catalogue (sinon le type choisi pour Barre) ; la barre est posée au niveau haut de l’étage. Sur une **coupe** ou une **élévation** le contour est la barre vue de côté : sa largeur est la hauteur de la section, une extrémité inclinée est une coupe en élévation, et un **redan** à angle droit à une extrémité est un **grugeage** en haut ou en bas ; la barre est là où elle est dessinée. Un redan dessiné en plan est refusé (un grugeage se voit de côté). Un contour qui n’est pas une bande (un triangle, un L, des côtés non parallèles) reste tel quel, et Lapin le dit.

## Types acier

**Acier → Types acier** liste les types de barre du projet avec leur poids au mètre et en crée d’autres : un profilé du catalogue (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** et les génériques formés à froid **C**, **Z** et **Σ**) ou une section creuse ou formée à froid **selon ses propres dimensions** (hauteur, largeur, épaisseur, bord tombé). Le nouveau type peut être un type de barre, ou un **type de poutre** ou **type de poteau** de la structure, pour que les outils Poutre et Poteau posent aussi de l’acier. Un clic sur un type de la liste en fait celui que pose Barre.

![Le dialogue Types acier](shots/steel-types.webp)

Les profilés creux sont vides à l’intérieur, dans la vue 3D, les coupes et les quantités. Les profilés formés à froid sont des formes génériques avec leurs dimensions, aux plis vifs, sans catalogue de fabricant.

![Des barres en 3D](shots/steel-3d.webp)

## Bâtiments à portiques

**Acier → Bâtiment à portiques…** ouvre le configurateur de bâtiment. Comme celui de la toiture, il a les styles à gauche, les champs du bâtiment en onglets au milieu et, à droite, le bâtiment tel que Lapin le construirait : un portique courant vu le long du bâtiment, avec ses jarrets, et un résumé de ce qu’il pèse.

![Le configurateur de bâtiment](shots/steel-hall.webp)

- **Styles** : les bâtiments types du catalogue (**Bâtiment 20 m**, **Bâtiment 30 m**, un **Appentis 10 m** monopente) et ceux du projet. Un style choisi remplit tous les champs ; chacun peut ensuite être modifié. **Enregistrer comme style…** garde le bâtiment tel que configuré comme style du projet ; **Mettre à jour le style** l’enregistre à nouveau dans son style, et tous les bâtiments de ce style suivent.
- **Portique** : la **Portée** (d’axe de poteau à axe de poteau), les **Travées** (égales comme `5 × 6`, ou chacune : `6, 6, 7.5`), la **Hauteur à la sablière** (le dessus des traverses au droit de la face extérieure du poteau), la **Toiture** (deux versants ou monopente) et sa **Pente**, les profilés des **Poteaux** et des **Traverses**, et les **Pieds de poteaux** (articulés ou encastrés, gardés pour les assemblages à venir).
- **Jarrets** : le **Jarret à la sablière** et le **Jarret au faîtage**, chacun avec sa **Longueur** le long de la traverse depuis la face du poteau (ou le faîtage) et sa **Hauteur** sous la traverse à cet endroit, et comment il est **Réalisé** : **découpé dans le profil de la traverse** (un I coupé en biais le long de son âme, une moitié soudée sous la traverse : l’âme et la semelle de la traverse) ou **en tôles soudées** (une **Tôle d’âme** et une **Tôle de semelle** de leur propre épaisseur, et la largeur de semelle).

![Les jarrets en tôles soudées](shots/steel-hall-haunches.webp)

- **Pignons** : des **potelets de pignon sous une traverse simple** (potelets d’angle et potelets à l’**Entraxe** choisi, leur axe fort perpendiculaire au mur de pignon, sous une traverse plus légère qui va jusqu’au nu extérieur des murs, les potelets ajustés sur sa face inférieure), ou **un portique courant** à chaque extrémité.

![Un pignon à potelets](shots/steel-hall-gable.webp)

- **Acier secondaire** : l’**Entraxe des pannes**, l’**Entraxe des lisses**, les **Travées contreventées** (leurs numéros, depuis la première) et une **Poutre sablière**. Ils sont gardés avec le bâtiment pour les pannes, lisses et contreventements à venir ; ils ne sont pas encore construits.
- **Placement** : le croisement de l’axe A et de l’axe 1, et la direction de la portée en plan (**Rotation**) ; l’aperçu montre la trame du bâtiment vue du dessus.

**Créer le bâtiment** le fait en une seule étape, qu’une annulation défait : sa propre **trame** (axes 1, 2… aux portiques, A et B aux poteaux), et à chaque portique deux **poteaux** (poteaux de la structure en profilé acier, l’âme dans le plan du portique, la tête coupée selon la pente) et deux **traverses** en onglet au faîtage (une seule, du poteau bas au poteau haut, en monopente) : des barres en acier ajustées sur les faces des poteaux, avec leurs jarrets. Le bâtiment est sur l’étage courant ; la sablière et le faîtage sont des hauteurs de ses portiques, pas de nouveaux étages. Le résumé donne les portiques, les hauteurs et l’acier par nature : poteaux, traverses, jarrets, potelets de pignon, au total et par m² de surface.

![Le bâtiment en 3D](shots/steel-hall-3d.webp)

Le bâtiment reste modifiable. Sélectionnez une de ses barres : sa section **Acier** indique son bâtiment et **Modifier le bâtiment…**, qui ouvre le configurateur dessus ; **Appliquer** reconstruit le bâtiment sur les mêmes barres, qui gardent leur identité (une barre devenue inutile disparaît, une nouvelle est ajoutée). Les jarrets de la traverse y sont aussi listés, chacun modifiable seul (nature, longueur, hauteur, tôles) : un jarret réglé ainsi reste tel quel quand le bâtiment est reconstruit ; **×** le retire.

![Les jarrets d’une traverse dans les Propriétés](shots/steel-hall-props.webp)

## IFC et quantités

Une barre s’exporte en **IfcMember** avec son **IfcMemberType**. Son corps est écrit comme les autres logiciels écrivent l’acier (Tekla, Advance Steel, Revit) : sa section **extrudée** (IfcExtrudedAreaSolid), **coupée** par les plans de ses extrémités (IfcBooleanClippingResult avec IfcHalfSpaceSolid), et chaque grugeage un **IfcOpeningElement** qui l’évide. Une poutre ou un poteau en acier avec extrémités s’écrit de même. Les quantités de chaque barre donnent sa longueur entre ses coupes, l’aire de sa section, son volume et son poids net (7850 kg/m³).

Un bâtiment s’exporte comme un **IfcBuildingSystem** (LOADBEARING) auquel ses barres sont affectées, ses données dans le jeu de propriétés `Lapin_SteelHall`, avec sa propre **IfcGrid**. Une traverse avec jarrets est un **IfcElementAssembly** de la traverse et de ses jarrets : un jarret découpé dans le profil est un **IfcMember**, un jarret en tôles soudées deux **IfcPlate**, chacun extrudé (IfcExtrudedAreaSolid). Les jarrets ne comptent pas dans les quantités de la traverse ; le résumé du configurateur les pèse.
