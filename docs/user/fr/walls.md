---
title: Murs
order: 20
summary: Dessinez les murs angle par angle, modifiez-les dans le panneau des propriétés et définissez des types de murs avec leurs couches.
---

<!-- video walls -->

## Dessiner des murs

Ouvrez l’onglet **Architecture** et choisissez **Mur**, ou tapez <kbd>WA</kbd>.

![L’onglet Architecture](shots/ribbon-architecture.webp)

Cliquez là où le mur commence, puis cliquez à chaque angle. Le mur suivant suit la souris. Pendant que vous dessinez, vous pouvez taper une longueur et un angle dans les cases au-dessus de la vue au lieu de cliquer. Appuyez sur <kbd>Enter</kbd> ou <kbd>Esc</kbd> pour arrêter.

![Dessin de murs : le mur suivant suit la souris, avec sa longueur et son angle affichés](shots/wall-drawing.webp)

Là où deux murs se rencontrent, Lapin les joint de lui-même. Leurs couches se prolongent l’une dans l’autre comme elles seraient construites.

- **Mur courbe** (<kbd>WC</kbd>) dessine un mur le long d’un arc.
- **Mur-rideau** (<kbd>CW</kbd>) dessine un mur vitré avec des meneaux et des panneaux.
- **Joindre** (<kbd>JW</kbd>), **Onglet** et **Bout à bout** changent la façon dont deux murs se rencontrent.
- **Permuter** change lequel des deux murs passe à travers l’autre.

## Modifier un mur

Cliquez sur un mur pour le sélectionner. Ses poignées apparaissent. Faites glisser les extrémités pour l’allonger ou le raccourcir, ou faites glisser la croix au milieu pour le déplacer.

![Un mur sélectionné avec ses poignées](shots/wall-selected.webp)

Le panneau des propriétés affiche le mur sélectionné. Le haut du panneau contient son type et ses couches. En dessous se trouvent sa longueur, sa hauteur, son décalage de base, son niveau et sa phase. Changez une valeur et le modèle suit.

![Les propriétés d’un mur](shots/wall-properties.webp)

**Retourner** fait passer les couches du mur de l’autre côté. Vous pouvez aussi cliquer sur les petites flèches affichées à côté d’un mur sélectionné dans le plan.

## Types de murs

Un type de mur est un ensemble de couches, de la face extérieure à la face intérieure. Chaque couche a un matériau et une épaisseur, et une couche est le **noyau**. La ligne du mur suit le noyau. Pour modifier les types, choisissez **Architecture → Types de murs**.

![Types de murs : les couches d’un mur en brique](shots/wall-types.webp)

- **+ Couche** ajoute une couche. Les flèches montent ou descendent une couche.
- **Zones en hauteur** divise le noyau de bas en haut, par exemple une première assise, la maçonnerie et un chaînage.
- **Linteaux** choisit les linteaux posés au-dessus des portes et des fenêtres : préfabriqués ou coulés en place, avec leur appui et leur hauteur.
- **Dessiner avec** ferme la boîte de dialogue et commence à dessiner des murs de ce type.

Un changement apporté à un type change chaque mur de ce type.

## Murs sous un toit

Les murs du même niveau qu’un toit s’arrêtent d’eux-mêmes dessous et montent jusqu’à ses pignons. Pour un mur d’un autre niveau, sélectionnez-le et choisissez **Architecture → Attacher au toit** (<kbd>AR</kbd>), puis cliquez le toit — ou tout autre corps jusqu’auquel le mur doit monter : un solide Studio, un élément importé. Voir [Dalles et toits](floors-roofs.md).
