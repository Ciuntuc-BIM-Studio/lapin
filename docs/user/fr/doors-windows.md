---
title: Portes et fenêtres
order: 30
summary: Placez des portes et des fenêtres dans les murs, déplacez-les et concevez leurs types dans le configurateur.
---

<!-- video doors-windows -->

## Placer une porte ou une fenêtre

Choisissez **Architecture → Porte** (<kbd>DR</kbd>) ou **Fenêtre** (<kbd>WN</kbd>), puis cliquez sur un mur là où vous voulez l’ouverture. Lapin découpe l’ouverture dans chaque couche du mur et ajoute le linteau que demande le type de mur.

![Un plan avec une porte et des fenêtres](shots/openings-plan.webp)

Pour déplacer une porte ou une fenêtre le long de son mur, sélectionnez-la et faites-la glisser. Pour la modifier, utilisez le panneau des propriétés : son type, sa hauteur d’allège, et le côté vers lequel elle s’ouvre (**Côté**).

![Les propriétés d’une porte](shots/door-properties.webp)

## Le configurateur

**Architecture → Portes et fenêtres** ouvre le configurateur. Chaque type de porte et de fenêtre du projet est listé à gauche. Vous pouvez modifier :

- le nom et le repère du type (la lettre dans les étiquettes) ;
- sa largeur et sa hauteur ;
- la profondeur et la largeur du cadre ;
- ses colonnes et ses rangées de vantaux, et la façon dont chaque vantail s’ouvre ;
- la finition de l’embrasure, l’encadrement en façade, le seuil et les couleurs.

Les dessins à droite montrent le type tel qu’il sera dessiné en élévation, en plan et en coupe, pendant que vous le modifiez.

![Le configurateur des portes et fenêtres](shots/joinery-configurator.webp)

**Enregistrer le type** conserve vos modifications. Chaque porte ou fenêtre de ce type les suit. **Placer avec** ferme le configurateur et commence à placer ce type.

### Fenêtres à plusieurs vantaux

**Colonnes de vantaux** reçoit les largeurs relatives des vantaux, un nombre pour chacun : `1 1` donne deux vantaux égaux, `1 1 1` trois, `1 2 1` un large entre deux étroits. **Rangées de vantaux** divise la fenêtre en hauteur de la même façon, de bas en haut : `1 1` donne deux rangées. Six vantaux, c’est `1 1 1` dans Colonnes de vantaux et `1 1` dans Rangées de vantaux. Ensuite, dans **Vantaux**, choisissez comment s’ouvre chaque vantail : fixe, à la française, oscillo-battant, à soufflet, à l’italienne, pivotant ou coulissant.

### Volets, grilles, jardinières et auvents

<!-- video window-dressings -->

Sous **Sur la façade**, un type de fenêtre peut avoir :

- des **Volets** : à lames, pleins ou à planches, à un ou deux vantaux, représentés ouverts (rabattus contre le mur à côté de la fenêtre) ou fermés devant elle ;
- une **Grille de défense** : dans le tableau côté intérieur (par défaut, là où se posent d’ordinaire les grilles de défense), dans le tableau côté extérieur, sur la façade ou sur la face intérieure, avec des barreaux horizontaux, verticaux ou dans les deux sens, leur section et leur espacement ;
- une **Jardinière** sous la fenêtre ;
- un **Auvent** au-dessus, avec sa saillie et sa pente.

Chacun se place d’après les faces réelles du mur à l’endroit de la fenêtre, à partir des couches du mur, et reste donc juste sur n’importe quel type de mur. Quand la fenêtre a un encadrement, ce qui est sur la façade se tient devant l’encadrement. Vous pouvez déplacer chacun à la main : **Depuis le nu du mur** règle de combien il s’écarte du mur (pour une grille dans le tableau, **En retrait du nu** règle sa profondeur), **Depuis l’axe de la fenêtre** le déplace le long du mur, et **Vers le haut ou le bas** le déplace en hauteur. Ils apparaissent en 3D, dans les plans, les élévations et les coupes, et les nomenclatures les mesurent.

![Des volets, une grille, une jardinière et un auvent dans le configurateur](shots/joinery-dressings.webp)

![Les fenêtres habillées, en 3D](shots/window-dressings-3d.webp)

## Étiquettes en plan

**Annoter → Étiquettes de portes et fenêtres** place un marqueur sur chaque porte et fenêtre d’un plan : une ligne en travers du mur, le repère à l’extérieur, et les dimensions et l’allège à l’intérieur. Pour changer l’aspect des étiquettes, allez dans **Gérer → Normes de dessin → Étiquettes**.
