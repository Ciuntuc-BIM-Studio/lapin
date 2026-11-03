---
title: Acier
order: 56
summary: Barres en acier aux extrémités paramétrables — prolongées ou en retrait, coupées selon un plan, en onglet, ajustées sur une autre barre, grugées —, faites aussi d’un contour dessiné ; profilés creux et formés à froid.
---

## L’onglet Acier

![L’onglet Acier](shots/ribbon-steel.webp)

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

## IFC et quantités

Une barre s’exporte en **IfcMember** avec son **IfcMemberType**. Son corps est écrit comme les autres logiciels écrivent l’acier (Tekla, Advance Steel, Revit) : sa section **extrudée** (IfcExtrudedAreaSolid), **coupée** par les plans de ses extrémités (IfcBooleanClippingResult avec IfcHalfSpaceSolid), et chaque grugeage un **IfcOpeningElement** qui l’évide. Une poutre ou un poteau en acier avec extrémités s’écrit de même. Les quantités de chaque barre donnent sa longueur entre ses coupes, l’aire de sa section, son volume et son poids net (7850 kg/m³).
