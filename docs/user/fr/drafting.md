---
title: Dessin et ligne de commande
order: 80
summary: Tapez des commandes comme en CAO, accrochez-vous aux points, déplacez et copiez, dessinez des lignes, et annotez avec des cotes, des textes et des étiquettes.
---

## La ligne de commande

Tapez les lettres d’un outil n’importe où dans la fenêtre, puis appuyez sur <kbd>Enter</kbd>. Par exemple, tapez <kbd>WA</kbd> pour un mur, <kbd>CO</kbd> pour copier, <kbd>M</kbd> pour déplacer ou <kbd>L</kbd> pour une ligne. La ligne de commande en bas indique ce dont l’outil a besoin ensuite. Vous pouvez cliquer un point ou taper une longueur, un angle ou des coordonnées.

![Copie d’un mur : après le point de base, la ligne de commande et la saisie dynamique affichent la longueur et l’angle jusqu’à la souris](shots/command-line.webp)

Chaque outil affiche ses lettres dans son info-bulle ; vous pouvez les modifier dans **Vue → Raccourcis clavier…** (voir [L’interface et les raccourcis](interface.md)). Voici les plus utilisées :

| Modèle | | Dessin | |
|---|---|---|---|
| Mur | <kbd>WA</kbd> | Ligne | <kbd>L</kbd> |
| Porte, fenêtre | <kbd>DR</kbd>, <kbd>WN</kbd> | Polyligne | <kbd>PL</kbd> |
| Dalle | <kbd>SB</kbd> | Rectangle, cercle, arc | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Toit | <kbd>RF</kbd> | Déplacer, copier | <kbd>M</kbd>, <kbd>CO</kbd> |
| Escalier, pièce | <kbd>ST</kbd>, <kbd>RM</kbd> | Faire pivoter, symétrie, échelle | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Poteau, poutre | <kbd>CL</kbd>, <kbd>BM</kbd> | Décaler, ajuster, prolonger | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Coupe | <kbd>SE</kbd> | Raccord, chanfrein | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Mesurer | <kbd>ME</kbd> | Hachure, texte | <kbd>H</kbd>, <kbd>T</kbd> |
| Tout ajuster | <kbd>ZE</kbd> | Effacer | <kbd>E</kbd> |

## Accrochages et aides

Lapin s’accroche aux extrémités, aux milieux et aux intersections de ce qui est dessiné, ainsi qu’à la grille. <kbd>F3</kbd> active ou désactive l’accrochage, <kbd>F8</kbd> le mode ortho et <kbd>F10</kbd> le repérage polaire. Leur état s’affiche en bas à droite de la vue.

## Copier et déplacer

Pour copier, sélectionnez l’élément et tapez <kbd>CO</kbd>. Cliquez un point de base, puis un second point. Lapin continue de copier jusqu’à ce que vous appuyiez sur <kbd>Enter</kbd> ou <kbd>Esc</kbd>. **Déplacer** (<kbd>M</kbd>) fonctionne de la même façon.

Pendant que vous déplacez ou copiez, un repère reste sur le point de base et une ligne en tirets va de celui-ci à la souris, avec un contour estompé de ce que vous déplacez laissé à son ancienne place jusqu’à ce que vous cliquiez ou appuyiez sur <kbd>Esc</kbd>. La ligne et le repère changent de couleur selon l’accrochage : vert sur un point accroché, bleu quand le mode ortho ou le repérage polaire tient la direction, la couleur d’accent sur une ligne de repérage, gris en mode libre. À côté de la souris, Lapin indique de combien vous avez déplacé : Δx, Δy, la distance et l’angle, dans les unités du projet. Vous pouvez y taper la distance et l’angle, comme lorsque vous dessinez. Dans l’interface Compact, ces valeurs sont dans la ligne d’état. Faire glisser une poignée affiche la même chose, tout comme faire glisser un élément sélectionné lui-même, depuis le point où vous avez appuyé ; appuyez sur <kbd>Esc</kbd> avant de relâcher et l’élément reste où il était.

![Déplacement d’un mur : le repère sur le point de base, la ligne en tirets jusqu’à la souris, le fantôme du mur à son ancienne place et le décalage près de la souris](shots/move-trace.webp)

Vous pouvez aussi faire glisser. Faites glisser la croix au milieu d’un élément sélectionné pour le déplacer. Maintenez <kbd>Ctrl</kbd> (<kbd>⌘</kbd> sur Mac) pendant que vous faites glisser pour laisser une copie.

Un axe de quadrillage se déplace, se copie, pivote et se reflète seul ou avec d’autres éléments. Une copie est un nouvel axe de la même grille, nommé d’après le dernier axe qui court dans sa direction : avec les axes 1, 2 et 3, la copie de n’importe lequel d’entre eux est 4.

## Dessin

L’onglet **Dessin** trace des lignes, des polylignes, des rectangles, des cercles et des arcs. Il les modifie par décalage, ajustement, prolongement, raccord, chanfrein et jonction, remplit des surfaces de hachures et les organise sur des **Calques**. **Import DXF** fait entrer un dessin de CAO sous forme de lignes auxquelles vous pouvez vous accrocher et que vous pouvez modifier.

![L’onglet Dessin](shots/ribbon-draft.webp)

## Annotation

L’onglet **Annoter** contient les cotes, les textes, les axes de quadrillage, les niveaux et les étiquettes. Une cote lit le modèle : elle se met donc à jour quand un mur bouge. L’aspect des textes, des cotes, des axes et des étiquettes se règle dans **Gérer → Normes de dessin**.

Une cote alignée ou linéaire se pose en chaîne : après les deux premiers points et sa ligne, chaque clic ajoute le point suivant sur la même ligne, et **Enter** (ou **Esc**) fait de toute la chaîne une seule cote (**U** reprend le dernier point ; tapez d’abord **S** pour une cote simple). Une chaîne se comporte comme une polyligne : un clic la sélectionne en entier, et elle se déplace, se copie et se supprime d’un seul tenant. Une chaîne qui mesure des murs, des arêtes ou des axes de quadrillage y reste attachée : glissée ou déplacée seule, seule sa ligne de cote coulisse, perpendiculairement à la chaîne, de sorte qu’un faux mouvement de souris ne la laisse jamais mesurer le vide ; déplacée avec ce qu’elle mesure, elle les suit. Ses poignées se trouvent sur chaque point mesuré (faites glisser pour le déplacer, Ctrl/⌘-clic pour le retirer), au milieu de chaque segment (faites glisser pour ajouter un point, Ctrl/⌘-clic pour en ajouter un à mi-chemin) et aux extrémités de la ligne de cote (faites glisser pour déplacer la ligne). Pour ajouter des points à une chaîne, comme dans ArchiCAD, sélectionnez-la et cliquez sur **Annoter → Ajouter des points** (ou tapez **DIMADD**, ou **Ajouter des points** dans ses propriétés) : chaque point que vous cliquez entre dans la chaîne là où il tombe le long de sa ligne, entre les points qui l’entourent ou au-delà d’une extrémité comme nouvelle extrémité, et reste attaché au mur, à l’arête ou à l’axe sur lequel il tombe ; **Enter** ou **Esc** termine. Pour retirer des points, faites Ctrl/⌘-clic sur leurs poignées, ou cliquez sur **Annoter → Retirer des points** (**DIMDEL**, ou **Retirer des points** dans ses propriétés) et cliquez près de chaque point ; les segments de part et d’autre n’en font plus qu’un. La valeur de chaque segment a aussi une poignée : faites-la glisser pour écarter la valeur de sa place là où les segments sont courts et où les valeurs se chevaucheraient ; dès qu’elle s’écarte de son segment, une fine ligne de rappel l’y relie. Un Ctrl/⌘-clic sur la poignée, ou **Textes replacés sur la ligne** dans les propriétés, la remet en place. Une valeur déplacée reste avec son segment quand les points bougent, et tourne avec la chaîne. Double-cliquez un segment pour taper sa valeur : l’élément qu’il mesure se déplace, ainsi que tout ce qui le suit dans la chaîne. Les propriétés affichent la valeur et le texte de chaque segment, scindent une chaîne en cotes simples et joignent des cotes simples qui se prolongent l’une l’autre sur une même ligne.

![L’onglet Annoter](shots/ribbon-annotate.webp)
