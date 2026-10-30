---
title: L’interface et les raccourcis
order: 15
summary: Les modes Confortable, Compact et Concentration, le repli des panneaux latéraux, et les raccourcis clavier que vous pouvez modifier.
---

<!-- video interface -->

## Trois modes

**Vue → Interface…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) choisit ce qui entoure la vue. Choisissez l’un des trois modes en haut de la boîte de dialogue ; le changement s’applique aussitôt.

![La boîte de dialogue Interface : les trois modes, et chaque élément réglable séparément](shots/interface-dialog.webp)

- **Confortable** est la fenêtre telle que les autres pages la montrent : le ruban avec de grands outils et leurs noms, une ligne de commande sous chaque dessin, et sur la vue le HUD, l’indication, les champs de longueur et d’angle, le plan de repérage et les outils rapides.
- **Compact** donne à la vue l’essentiel de la fenêtre :

![Compact : plus de place pour le dessin](shots/interface-compact.webp)

1. **Le ruban** tient sur une seule bande d’icônes. De fins traits séparent les groupes. Survolez un outil pour voir son nom, ses touches et son groupe.
2. **L’arborescence du projet** se replie en une barre. Ses icônes ouvrent un instant le projet, les niveaux ou l’arborescence IFC sur la vue.
3. **Les outils rapides** <kbd>1</kbd>–<kbd>9</kbd> se placent sur le bord gauche de la vue.
4. **Le plan de repérage** devient un bouton. Cliquez-le pour ouvrir la carte, et sur son × pour la replier.
5. **Une seule ligne d’état** contient le nom de l’outil et ce qu’il demande, ce que vous tapez, les champs de longueur et d’angle pendant que vous dessinez, SNAP, ORTHO et POLAR, le niveau et la vue, puis le projet.

- **Concentration** ne garde que les onglets et une fine ligne de commande. Cliquez un onglet et ses outils se déroulent sur la vue ; la bande se replie dès que vous choisissez un outil, ou avec <kbd>Esc</kbd> ou un clic ailleurs. Les panneaux se masquent. Quand quelque chose est sélectionné, une petite carte indique ce que c’est et ses premières propriétés ; **Toutes les propriétés…** ouvre le panneau des propriétés sur la vue. <kbd>Tab</kbd> fait revenir les deux panneaux, et les masque à nouveau.

![Concentration : la sélection dans une carte](shots/interface-focus.webp)

Dans tous les modes, la barre du haut garde **Annuler** et **Rétablir** à son extrémité droite : avec leurs noms en Confortable, sous forme de deux flèches en Compact et en Concentration (<kbd>Ctrl</kbd> <kbd>Z</kbd> et <kbd>Ctrl</kbd> <kbd>Y</kbd> ; leur info-bulle indique ce qu’ils annulent).

Sous les modes, chaque élément se règle séparément : le ruban, les outils rapides (ou aucun : les touches numériques fonctionnent toujours), la ligne de commande, le HUD, l’invite et les champs de l’outil, le plan de repérage, les panneaux et la hauteur des lignes de propriétés. Modifier un élément fait passer le choix à **Personnalisé**.

Lapin garde votre choix sur cet ordinateur, pour vous. L’application de bureau et le navigateur gardent chacun le leur. Dans le navigateur, Lapin démarre en Compact quand la fenêtre fait moins de 800 pixels de haut, jusqu’à ce que vous choisissiez.

## Replier les panneaux latéraux

Dans tous les modes, l’arborescence du projet et le panneau des propriétés se replient en une fine barre de leur côté :

- le bouton en haut à droite du panneau le replie, et la flèche sur sa barre le déplie ;
- double-cliquez la bordure entre le panneau et la vue ;
- <kbd>Ctrl</kbd> <kbd>B</kbd> replie ou déplie l’arborescence, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> les propriétés, et <kbd>Tab</kbd> les deux.

Cliquez une icône sur la barre d’un panneau replié pour ouvrir le panneau sur la vue sans le déplier. Il disparaît avec <kbd>Esc</kbd> ou un clic ailleurs ; son épingle le garde ouvert. Faites glisser la bordure d’un panneau pour l’élargir ; Lapin retient la largeur.

![Une arborescence repliée ouverte sur la vue depuis sa barre](shots/browser-overlay.webp)

## Raccourcis clavier

**Vue → Raccourcis clavier…** (<kbd>KS</kbd>) liste, par onglet, toutes les commandes accessibles au clavier. Chaque ligne affiche les raccourcis de la commande.

![Les raccourcis clavier, filtrés sur « wall »](shots/shortcuts-dialog.webp)

- **Recherchez** par le nom d’une commande ou par une touche.
- **+** ajoute un raccourci : appuyez sur une combinaison (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), ou tapez jusqu’à trois lettres et appuyez sur <kbd>Enter</kbd>. <kbd>Esc</kbd> interrompt.
- Si une autre commande a déjà ce raccourci, Lapin indique laquelle. **Reassign** le lui retire, **Garder les deux** le laisse aux deux (la première de la liste l’emporte), **Annuler** ne change rien.
- **×** supprime un raccourci. La flèche au bout d’une ligne remet cette commande telle que le jeu prédéfini la définit, et **Tout réinitialiser** le fait pour toutes les commandes.
- **Jeu prédéfini** choisit l’ensemble de départ : **Style Revit** (par défaut : les lettres habituelles de Revit, là où Lapin a la commande) ou **Lapin classique** (les lettres propres à Lapin d’avant).
- **Exporter…** écrit vos raccourcis dans un fichier, et **Importer…** en lit un, pour un autre ordinateur ou un collègue.

Les info-bulles, les menus et la palette de commandes (<kbd>Ctrl</kbd> <kbd>K</kbd>) affichent toujours les raccourcis tels qu’ils sont actuellement.

### Les lettres et la ligne de commande

Les lettres comme <kbd>WA</kbd> fonctionnent quand aucun champ n’a le focus et que la ligne de commande est vide : tapez-les sur la vue. La commande s’exécute aussitôt. Quand une commande de la ligne de commande commence par les mêmes lettres (<kbd>DI</kbd> et <kbd>DIMADD</kbd>), Lapin attend un instant : continuez à taper pour écrire la commande plus longue, ou appuyez sur <kbd>Enter</kbd> ou <kbd>Space</kbd> pour exécuter tout de suite la plus courte. Pendant qu’une commande demande un point ou une valeur, les lettres et les chiffres lui reviennent. Les commandes de la ligne de commande comme <kbd>L</kbd>, <kbd>M</kbd> ou <kbd>TRIM</kbd> suivies de <kbd>Enter</kbd> fonctionnent avec tous les jeux prédéfinis. <kbd>Esc</kbd> annule toujours.

### Les raccourcis par défaut

Voici les raccourcis Style Revit, tels que Lapin les a actuellement :

<!-- shortcuts -->

Les touches numériques <kbd>1</kbd>–<kbd>9</kbd> choisissent les outils rapides.
