---
title: Dessin et ligne de commande
order: 80
summary: Tapez des commandes comme en CAO, accrochez-vous aux points, déplacez et copiez, dessinez des lignes, et annotez avec des cotes, des textes et des étiquettes.
---

## La ligne de commande

Tapez les lettres d’un outil n’importe où dans la fenêtre, puis appuyez sur <kbd>Enter</kbd>. Par exemple, tapez <kbd>WA</kbd> pour un mur, <kbd>CO</kbd> pour copier, <kbd>M</kbd> pour déplacer ou <kbd>L</kbd> pour une ligne. La ligne de commande en bas indique ce dont l’outil a besoin ensuite. Vous pouvez cliquer un point ou taper une longueur, un angle ou des coordonnées.

Les coordonnées saisies sont dans l’unité du projet, comme les longueurs près du curseur : dans un projet en millimètres, `@4000,4000` après le premier coin d’un rectangle le fait de 4 m sur 4 m, et `4000<90` fait 4 m droit vers le haut.

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

## Sélectionner

Cliquez sur un élément pour le sélectionner ; <kbd>Maj</kbd>-clic l’ajoute à la sélection. Pour en sélectionner plusieurs à la fois, faites glisser avec le bouton gauche là où rien n’est sélectionné. Tiré vers la droite, le cadre est bleu et prend ce qui est entièrement à l’intérieur (fenêtre) ; tiré vers la gauche, il est vert et pointillé et prend tout ce qu’il touche (capture), comme dans AutoCAD. Appuyez sur <kbd>Espace</kbd> pendant le glissement et le cadre devient un lasso qui suit la souris, en gardant le mode du cadre ; <kbd>Espace</kbd> à nouveau revient au cadre. Sur la ligne de commande, quand une commande demande de sélectionner des objets, tapez **WP** ou **CP** et cliquez les sommets d’un polygone, puis <kbd>Entrée</kbd> : un polygone fenêtre ou un polygone capture.

![Un lasso dans le plan : le cadre tiré vers la droite, puis Espace, et la boucle autour des deux murs qu’il prend](shots/select-lasso.webp)

## Copier et déplacer

Pour copier, sélectionnez l’élément et tapez <kbd>CO</kbd>. Cliquez un point de base, puis un second point. Lapin continue de copier jusqu’à ce que vous appuyiez sur <kbd>Enter</kbd> ou <kbd>Esc</kbd>. **Déplacer** (<kbd>M</kbd>) fonctionne de la même façon.

Pendant que vous déplacez ou copiez, un repère reste sur le point de base et une ligne en tirets va de celui-ci à la souris, avec un contour estompé de ce que vous déplacez laissé à son ancienne place jusqu’à ce que vous cliquiez ou appuyiez sur <kbd>Esc</kbd>. La ligne et le repère changent de couleur selon l’accrochage : vert sur un point accroché, bleu quand le mode ortho ou le repérage polaire tient la direction, la couleur d’accent sur une ligne de repérage, gris en mode libre. À côté de la souris, Lapin indique de combien vous avez déplacé : Δx, Δy, la distance et l’angle, dans les unités du projet. Vous pouvez y taper la distance et l’angle, comme lorsque vous dessinez. Dans l’interface Compact, ces valeurs sont dans la ligne d’état. Faire glisser une poignée affiche la même chose, tout comme faire glisser un élément sélectionné lui-même, depuis le point où vous avez appuyé ; appuyez sur <kbd>Esc</kbd> avant de relâcher et l’élément reste où il était.

![Déplacement d’un mur : le repère sur le point de base, la ligne en tirets jusqu’à la souris, le fantôme du mur à son ancienne place et le décalage près de la souris](shots/move-trace.webp)

Vous pouvez aussi faire glisser. Faites glisser la croix au milieu d’un élément sélectionné pour le déplacer. Maintenez <kbd>Ctrl</kbd> (<kbd>⌘</kbd> sur Mac) pendant que vous faites glisser pour laisser une copie.

Une porte, une fenêtre ou une ouverture de mur se déplace et se copie le long de son mur : seule la part du déplacement le long du mur compte, si bien que taper `500` après le point de base la fait glisser de 500 le long du mur ; déplacée dans un autre mur, elle passe dans ce mur. Si son mur est sélectionné aussi, elle suit le mur.

Un axe de quadrillage se déplace, se copie, pivote et se reflète seul ou avec d’autres éléments. Une copie est un nouvel axe de la même grille, nommé d’après le dernier axe qui court dans sa direction : avec les axes 1, 2 et 3, la copie de n’importe lequel d’entre eux est 4.

## Dessin

L’onglet **Dessin** trace des lignes, des polylignes, des rectangles, des cercles et des arcs. Il les modifie par décalage, ajustement, prolongement, raccord, chanfrein et jonction, remplit des surfaces de hachures et les organise sur des **Calques**. **Importer DXF** fait entrer un dessin de CAO sous forme de lignes auxquelles vous pouvez vous accrocher et que vous pouvez modifier.

![L’onglet Dessin](shots/ribbon-draft.webp)

**Décaler** (<kbd>O</kbd>) copie une ligne, une polyligne, un rectangle, un cercle ou un arc à une distance. Tapez la distance dans l’unité du projet (dans un projet en millimètres, `500` est un demi-mètre ; `2m` ou `1'6"` disent leur propre unité), cliquez sur la forme, puis sur le côté où elle va ; il demande la forme suivante jusqu’à <kbd>Entrée</kbd> ou <kbd>Échap</kbd>. Tapez plutôt **T** (Through) et chaque copie passe par le point cliqué. Les angles d’une polyligne restent vifs (en onglet), comme en CAO. Décaler fonctionne dans les plans, coupes, élévations, sur les plans de référence et dans la vue 3D.

Toute longueur que demande la ligne de commande est dans l’unité du projet, et l’invite dit laquelle : `Depth (mm)`, `Height (ft-in)`. Les angles, facteurs et nombres sont de simples nombres : `90` après **Rotation** fait 90°, quelle que soit l’unité.

### Décalquer un PDF ou une image

**Fichier → Importer un PDF ou une image…**, ou **Dessin → PDF ou image**, fait entrer un dessin à décalquer : un plan scanné, la photo d’une façade, un PDF d’un autre bureau. Il va dans le plan, la coupe ou l’élévation au premier plan, sous son dessin.

- **Un PDF** : choisissez la **Page** et l’échelle à laquelle son dessin a été fait (**Dessiné au 1:**). Un millimètre de papier mesure alors ici autant de millimètres, si bien qu’un plan au 1:100 arrive à sa vraie taille. Les lignes de la page arrivent en dessin auquel vous pouvez vous accrocher, que vous pouvez modifier et décalquer, et ses images (un scan dans le PDF) en images dessous. L’un ou l’autre peut être laissé de côté. La page arrive en un seul groupe nommé d’après le fichier, si bien que **Déplacer**, **Rotation** et **Échelle** la prennent entière ; double-cliquez dessus pour travailler sur ses parties.
- **Une image** (PNG ou JPEG) : donnez sa **Largeur** ; sa hauteur suit ses pixels. **Dessin → Mettre l’image à l’échelle** la fixe ensuite exactement : sélectionnez l’image, cliquez deux points dessus à une distance connue l’un de l’autre, et tapez cette distance.

Une image est transparente (son **Opacité**, 0,5 par défaut, dans ses propriétés) et ne fait jamais partie du modèle : elle ne s’imprime sur les feuilles que si vous la laissez dans la vue.

![Un plan imprimé en PDF, amené dans un autre niveau pour être décalqué](shots/trace-pdf.webp)

### Dessiner dans la vue 3D

**Ligne**, **Polyligne**, **Rectangle**, **Cercle**, **Arc** et les modifications de Dessin fonctionnent aussi dans la vue 3D, avec la ligne de commande en bas. Pendant une commande, l’étiquette du haut dit où vous dessinez : au niveau de l’étage actif, ou sur le plan de référence sélectionné. **Choisir une face** dans l’étiquette, puis un clic sur une face du modèle, crée un plan de référence sur cette face et dessine dessus. Les points s’accrochent comme sur un plan : aux lignes coupées du plan (angles et faces des murs) et à ce qui est dessiné sur le même plan, avec l’ortho et les lignes de repérage. Ce que vous dessinez est le même élément de dessin que sur le plan, et s’y voit aussi.

![Dessiner dans la vue 3D : l’étiquette dit le plan, la ligne suit la souris au niveau de l’étage](shots/draft-3d.webp)

### Les dessins dans la vue 3D

Ce que vous dessinez sur un plan (lignes, polylignes, rectangles, cercles, arcs) figure aussi dans la vue 3D, en lignes au niveau de son étage (le niveau lui-même, pas la hauteur de coupe) ; ce que vous dessinez sur un plan de référence, une coupe ou une élévation se tient sur ce plan. Cliquez-en un en 3D pour le sélectionner, puis déplacez-le, copiez-le, décalez-le ou supprimez-le. **Vue → Dessins en 3D**, aussi dans **Vue → Paramètres de vue** d’une vue 3D, les montre ou les cache tous ; c’est activé tant que vous ne l’éteignez pas. Cotes, textes, étiquettes, hachures et lignes de construction (le rôle draft) restent sur leurs vues. Un DXF apporté sur une vue est aussi en 3D, sur le plan de cette vue, avec son propre interrupteur (voir [Import et export](import-export.md)).

### Ordre de tracé

Là où des éléments dessinés se superposent, celui dessiné en dernier est au-dessus. Pour le changer, sélectionnez-les (lignes, polylignes, rectangles, cercles, arcs, hachures et remplissages, textes, cotes, étiquettes, les formes d’un DXF), faites un clic droit et choisissez **Mettre au premier plan**, **Avancer**, **Reculer** ou **Mettre à l’arrière-plan** ; **Dessin → Ordre** et **Annoter → Ordre** ont les mêmes, avec <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> et <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ sur Mac). Avancer et reculer passent l’élément suivant que la sélection recouvre. Le dessin 2D est tracé par-dessus le dessin du modèle ; **Envoyer derrière le modèle** le met sous la coupe des murs et les lignes vues (une hachure sous un mur), et un clic à cet endroit prend alors le mur ; **Mettre au premier plan** ou **Mettre à l’arrière-plan** le remettent au-dessus du modèle. Dans la ligne de commande, **DRAWORDER** (**DRO**) demande les objets, puis **Above objects** ou **Under objects** (cliquez les éléments au-dessus ou au-dessous desquels ils vont, puis <kbd>Entrée</kbd>), **Front**, **Back** ou derrière le modèle, **Model**. L’ordre appartient à la vue, s’annule en une étape, et les planches, le PDF et le DXF l’impriment comme vous le voyez ; ce que vous dessinez ensuite vient au-dessus.

![Clic droit sur un remplissage sélectionné : l’ordre de tracé](shots/draw-order-menu.webp)

![Mis au premier plan, le remplissage gris couvre le texte](shots/draw-order-front.webp)

## Annotation

L’onglet **Annoter** contient les cotes, les textes, les axes de quadrillage, les niveaux et les étiquettes. Une cote lit le modèle : elle se met donc à jour quand un mur bouge. L’aspect des textes, des cotes, des axes et des étiquettes se règle dans **Gérer → Normes de dessin**.

Une cote alignée ou linéaire se pose en chaîne : après les deux premiers points et sa ligne, chaque clic ajoute le point suivant sur la même ligne, et **Enter** (ou **Esc**) fait de toute la chaîne une seule cote (**U** reprend le dernier point ; tapez d’abord **S** pour une cote simple). Une chaîne se comporte comme une polyligne : un clic la sélectionne en entier, et elle se déplace, se copie et se supprime d’un seul tenant. Une chaîne qui mesure des murs, des arêtes ou des axes de quadrillage y reste attachée : glissée ou déplacée seule, seule sa ligne de cote coulisse, perpendiculairement à la chaîne, de sorte qu’un faux mouvement de souris ne la laisse jamais mesurer le vide ; déplacée avec ce qu’elle mesure, elle les suit. Ses poignées se trouvent sur chaque point mesuré (faites glisser pour le déplacer, Ctrl/⌘-clic pour le retirer), au milieu de chaque segment (faites glisser pour ajouter un point, Ctrl/⌘-clic pour en ajouter un à mi-chemin) et aux extrémités de la ligne de cote (faites glisser pour déplacer la ligne). Pour ajouter des points à une chaîne, comme dans ArchiCAD, sélectionnez-la et cliquez sur **Annoter → Ajouter des points** (ou tapez **DIMADD**, ou **Ajouter des points** dans ses propriétés) : chaque point que vous cliquez entre dans la chaîne là où il tombe le long de sa ligne, entre les points qui l’entourent ou au-delà d’une extrémité comme nouvelle extrémité, et reste attaché au mur, à l’arête ou à l’axe sur lequel il tombe ; **Enter** ou **Esc** termine. Pour retirer des points, faites Ctrl/⌘-clic sur leurs poignées, ou cliquez sur **Annoter → Retirer des points** (**DIMDEL**, ou **Retirer des points** dans ses propriétés) et cliquez près de chaque point ; les segments de part et d’autre n’en font plus qu’un. La valeur de chaque segment a aussi une poignée : faites-la glisser pour écarter la valeur de sa place là où les segments sont courts et où les valeurs se chevaucheraient ; dès qu’elle s’écarte de son segment, une fine ligne de rappel l’y relie. Un Ctrl/⌘-clic sur la poignée, ou **Textes replacés sur la ligne** dans les propriétés, la remet en place. Une valeur déplacée reste avec son segment quand les points bougent, et tourne avec la chaîne. Double-cliquez un segment pour taper sa valeur : l’élément qu’il mesure se déplace, ainsi que tout ce qui le suit dans la chaîne. Les propriétés affichent la valeur et le texte de chaque segment, scindent une chaîne en cotes simples et joignent des cotes simples qui se prolongent l’une l’autre sur une même ligne.

Les annotations d’un plan (cotes, textes, lignes dessinées, étiquettes) peuvent passer sur un autre plan, au même endroit, puisque les plans partagent leurs coordonnées : sélectionnez-les et choisissez le plan dans leurs propriétés, sous **View** pour les déplacer ou **Copy to** pour les copier. Les cotes faites sur un plan de masse vont ainsi au plan du niveau. Les points d’une cote s’accrochent alors à ce que le nouveau plan coupe à cet endroit : murs, arêtes, axes.

**Hauteurs des baies.** Un segment d’une chaîne de cotes qui va d’un tableau à l’autre d’une porte, d’une fenêtre ou d’une baie écrit la largeur au-dessus de la ligne et la hauteur de la baie en dessous, comme les plans roumains et européens. Elle est lue dans le modèle à chaque fois, donc elle suit la baie quand elle est redimensionnée, déplacée ou change de type ; un segment sur lequel vous avez écrit un texte n’en écrit pas. **Gérer → Normes de dessin → Dimensions → Hauteur de l’ouverture** la règle pour chaque style de cote : désactivée, `150`, `h=150`, `h=150` avec l’allège `p=90` en dessous, ou `150/90`, dans l’unité du style. Cela marche dans les plans, les coupes et les façades, et les planches, le PDF et le DXF l’impriment. Les nouveaux projets l’ont activée (la hauteur seule) ; un projet créé avant garde ses cotes telles quelles jusqu’à ce que vous l’activiez là.

![Les hauteurs de la porte et de la fenêtre sous leurs segments](shots/dimension-opening-height.webp)

![La hauteur de l’ouverture dans les styles de cote](shots/dimension-opening-height-style.webp)

### Étiquettes

**Annoter → Étiquette** (<kbd>TG</kbd>) pose sur une vue un cadre avec ce qu’un élément dit de lui-même, et une ligne de rappel jusqu’à lui. Tant que l’outil est actif, la barre sous la vue choisit ce que l’étiquette affiche : le repère, le nom, la classe ou le type, **Résistance au feu**, **Isolement acoustique**, **Transmission thermique** ou **Autre paramètre…** par son nom ; ou un type d’étiquette des normes de dessin ; et **RAPPEL** active ou désactive la ligne de rappel. Cliquez sur l’élément, puis là où va l’étiquette (tapez d’abord **E** pour couder la ligne). **Annoter → Étiqueter un paramètre** demande le nom du paramètre et lance l’outil avec lui.

![La barre de l’outil Étiquette sous la vue](shots/tag-options.webp)

Une étiquette lit n’importe quel paramètre : celui de l’élément, celui de son type (tous les éléments du type l’affichent alors), une propriété standard comme `Pset_WallCommon.FireRating`, ou une propriété venue avec un élément importé. Un nom est trouvé même sans ses espaces : **Fire rating** et `FireRating` sont donc le même. Pour le remplir, utilisez **Paramètres** dans les propriétés de l’élément ou de son type : tapez un nom (Fire rating, Sound rating, Moisture resistance et Thermal resistance sont proposés) ou choisissez **Ajouter une propriété standard…** pour les propriétés IFC de sa classe. L’étiquette suit dès qu’il y a une valeur, et les longueurs s’écrivent dans les unités du projet.

Une étiquette sélectionnée a des poignées. Faites glisser le cadre pour déplacer l’étiquette : la ligne de rappel continue de pointer au même endroit. Une étiquette suit son élément : quand l’élément est déplacé, tourné, mis en miroir ou remodelé par ses poignées, son étiquette le suit en gardant sa place à côté de lui, et la ligne de rappel reste sur lui ; une étiquette que vous avez déplacée garde le décalage que vous lui avez donné. Faites glisser le milieu de la ligne pour la couder (Ctrl/⌘-clic sur le coude la redresse) et son extrémité pour la faire pointer ailleurs (Ctrl/⌘-clic : de nouveau au milieu de l’élément). Les propriétés changent ce que l’étiquette **Affiche**, lui donnent un **Texte** propre (`{mark}|{FireRating}` écrit deux lignes), un **Type d’étiquette**, ou désactivent la **Ligne de rappel**. Le moteur dessine les étiquettes : les feuilles et les PDF les impriment telles que vous les voyez. Les types d’étiquette de **Gérer → Normes de dessin → Étiquettes** lisent les mêmes champs dans leurs modèles ; un nouveau projet compte parmi eux Résistance au feu, Isolement acoustique et Transmission thermique.

**Lignes de repère comme les étiquettes d’ArchiCAD.** Par défaut, la ligne de repère d’une étiquette est *coudée* : une ligne oblique de l’élément jusqu’à un coude, puis un épaulement qui passe sous le texte et souligne sa première ligne, sans cadre autour du texte. Après le clic sur l’élément, le clic suivant est le coude, et le texte se place à côté. Faites glisser la poignée du coude : la ligne oblique garde 30°, 45°, 60° ou 90° depuis son extrémité (maintenez <kbd>Maj</kbd> pour un angle libre), et glissé au-delà de l’extrémité, le texte passe de ce côté, aligné vers le coude. Faites glisser le texte pour le déplacer avec son coude. Dans **Gérer → Normes de dessin → Étiquettes**, **Ligne de repère…** sur un type d’étiquette (et l’éditeur de composition) règle sa **Forme de la ligne de repère** (coudée, ou droite depuis un cadre), ce que souligne l’épaulement (**Soulignement** : la première ligne, chaque ligne, la dernière ligne ou rien, quand il s’arrête au bord du texte) et **Prolongement mm**, de combien l’épaulement dépasse du texte, à côté de l’aperçu du moteur. Avec une ligne de repère par couche, chacune finit sous sa propre ligne, les coudes en une colonne ; faire glisser le coude déplace la colonne. **Forme de la ligne de repère** dans les propriétés d’une étiquette prime sur celle de son type ; une étiquette dont la ligne de repère a été coudée en un point libre garde sa ligne droite. Les étiquettes des projets faits auparavant gardent leur ligne de repère droite dans un cadre.

![La ligne de rappel d’une étiquette coudée, avec ses poignées](shots/tag-leader.webp)

![Les propriétés d’une étiquette](shots/tag-properties.webp)

Le repère d’une porte ou d’une fenêtre se modifie partie par partie, chaque texte déplacé ou tourné et chaque extrémité de sa ligne allongée : voir [Portes et fenêtres](doors-windows.md).

#### Étiquette de composition

Une étiquette de composition écrit les couches d’un élément comme une note d’architecte : un en-tête (*EXTERIOR*), une ligne par couche, un pied (*INTERIOR*). Un nouveau projet a **Build-up (exterior → interior)** : chaque ligne est la description du matériau (son nom s’il n’en a pas), son λ s’il en a un et l’épaisseur de la couche en mm. Choisissez-la dans la barre de l’outil **Étiquette** et cliquez sur un mur, une dalle, une toiture ou la finition d’une pièce ; les autres éléments reçoivent le champ choisi. Le texte est lu dans le modèle à chaque dessin : changez une couche, la description d’un matériau ou son λ, et l’étiquette suit.

![Deux étiquettes de composition sur un mur de façade : une ligne de rappel, et une par couche](shots/buildup-tag.webp)

Sa forme se règle dans **Gérer → Normes de dessin → Étiquettes** : mettez la **Forme** d’un type d’étiquette sur *composition*, ou cliquez sur **Composition…** sur l’un d’eux, pour le modifier à côté d’un aperçu sur un des types de mur ou de dalle du projet.

- **En-tête** et **Pied** : écrits au-dessus et au-dessous des couches (vide : aucun ; `|` commence une nouvelle ligne).
- **Chaque couche** : la ligne écrite une fois par couche. Elle lit les champs de la couche, `{description}`, `{material}`, `{thickness}` (dans l’unité choisie sous **Épaisseur en**), `{note}` (la note de la couche dans son type), `{function}` (âme, finition, isolant…) et `{index}` ; tout paramètre du matériau par son nom ou son symbole, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (voir les [Données](materials.md#donnees-description-parametres-comment-il-se-mesure) du matériau) ; et ceux de l’élément, `{name}`, `{type}`, `{mark}`. **Insérer un champ…** en place un à la place du curseur et liste les paramètres des matériaux du projet.
- **Les crochets** rendent une partie facultative : `[, λ = {λ} W/mK]` n’est écrit que là où le matériau a un λ ; une partie disparaît entière quand un champ y est vide. **[ ]** fait de la sélection une telle partie. Pour écrire un crochet lui-même, tapez `\[` ou `\]`.
- **Ordre** : de l’extérieur vers l’intérieur d’un mur (le côté de sa première couche est son extérieur, un mur retourné commence donc toujours là), du haut vers le bas d’une dalle ou d’une toiture ; ou l’inverse. Choisissez l’ordre dans lequel les couches se trouvent à côté de la note, pour que les lignes de rappel par couche ne se croisent pas.
- **Les couches sans épaisseur** (membranes, films, peinture) sont écrites sauf si vous décochez **visibles**. Une couche qui change de matériau sur sa hauteur écrit une ligne par matériau.
- **Lignes de rappel** : une, vers l’élément, ou **une par couche**, chacune de sa ligne jusqu’à un point dans sa propre couche, là où la vue distingue les couches (un mur en plan et en coupe, une dalle en coupe ; ailleurs l’étiquette en garde une). Faites glisser l’extrémité de la ligne de rappel le long de l’élément pour le traverser ailleurs.
- **Les lignes commencent par** une puce, un tiret, leur numéro ou rien ; elles passent à la ligne à **Largeur mm** sur le papier, alignées après la puce, et une valeur reste avec son symbole et son unité (`λ = 0.035 W/mK`).

![L’éditeur d’un type d’étiquette de composition à côté de son aperçu](shots/buildup-tag-type.webp)

Comme toute étiquette, l’étiquette de composition suit son élément, et les feuilles et les PDF l’impriment comme vous la voyez, λ, μ et ρ compris. Un **Texte** propre dans ses propriétés remplace la ligne par couche pour cette seule étiquette.

### Axes du quadrillage

**Annoter → Axe de quadrillage** (<kbd>GR</kbd>) dessine un axe par ses deux extrémités ; il prend le repère suivant de sa direction. **Annoter → Trame d’axes…** pose une trame entière d’un coup : les espacements **En travers** (les axes numérotés, de gauche à droite) et **En long** (les axes lettrés, de bas en haut) dans les unités du projet, comme `6000 6000 4500` ou `3*6000` ; l’**Origine** où se croisent les premiers axes, un **Angle**, de combien les axes vont **Au-delà de la trame**, les **Premiers repères** et le **Type d’axe**. Avec **remplacer les axes de la trame** coché, les axes dessinés jusque-là disparaissent.

![Le dialogue Trame d’axes](shots/grid-dialog.webp)

Cliquez sur un axe pour le sélectionner. Ses propriétés montrent son **Début** et sa **Fin**, sa **Longueur**, son **Angle** et son espacement depuis l’axe parallèle précédent (↔). Tapez un nouvel espacement et l’axe se déplace ; les axes au-delà se déplacent aussi, sauf si vous décochez **les axes au-delà se déplacent aussi**. **Bulles** choisit les extrémités qui portent une bulle. **Styles d’axes…** ouvre **Normes de dessin → Axes du quadrillage**, où se règlent la bulle, sa taille, le type de ligne et le premier repère. Sélectionnez toute la trame pour voir ses axes et leurs espacements.

![Les propriétés d’un axe](shots/axis-properties.webp)

Toutes ces longueurs suivent les unités du projet. En pieds et pouces, les extrémités et les espacements se lisent 19'-8 1/4", et les niveaux s’écrivent aussi en pieds et pouces.

![Le même axe en pieds et pouces](shots/axis-properties-imperial.webp)

![L’onglet Annoter](shots/ribbon-annotate.webp)
