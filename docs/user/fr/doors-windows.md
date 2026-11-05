---
title: Portes et fenêtres
order: 30
summary: Placez des portes et des fenêtres dans les murs, déplacez-les et concevez leurs types dans le configurateur.
---

<!-- video doors-windows -->

## Placer une porte ou une fenêtre

Choisissez **Architecture → Porte** (<kbd>DR</kbd>) ou **Fenêtre** (<kbd>WN</kbd>), puis cliquez sur un mur là où vous voulez l’ouverture. Lapin découpe l’ouverture dans chaque couche du mur et ajoute le linteau que demande le type de mur.

Tant que Door ou Window est actif, le panneau des propriétés montre le type qu’il posera, avec sa taille : choisissez-en un autre là avant de cliquer sur le mur.

![Un plan avec une porte et des fenêtres](shots/openings-plan.webp)

Pour déplacer une porte ou une fenêtre le long de son mur, sélectionnez-la et faites-la glisser. Pour la modifier, utilisez le panneau des propriétés : son type, sa hauteur d’allège, et le côté vers lequel elle s’ouvre (**Côté**). Avec plusieurs portes et fenêtres sélectionnées, **Main**, **Côté**, l’allège et le type les changent toutes en une seule étape, de même qu’une flèche de retournement en plan.

Pour une distance exacte, utilisez **Déplacer** (<kbd>M</kbd>) ou **Copier** (<kbd>CO</kbd>) dans la ligne de commande, comme pour tout élément : sélectionnez la porte, la fenêtre ou l’ouverture du mur, cliquez un point de base, puis tapez la distance (`500`) ou cliquez un second point. Seule la part du déplacement le long du mur compte : l’ouverture glisse le long de son mur et garde son allège. Déplacée dans un autre mur du même niveau, elle passe dans ce mur et s’ouvre toujours de la même façon dans la pièce. Une copie est une nouvelle porte ou fenêtre du même type, avec le numéro suivant. Si elle ne tient pas (au-delà de l’extrémité du mur, ou sur une autre ouverture), rien ne bouge. **Faire pivoter** et **Symétrie** la placent de la même façon, et une symétrie change sa main.

![Les propriétés d’une porte](shots/door-properties.webp)

## Baies sans porte ni fenêtre

**Architecture → Wall opening** (<kbd>WO</kbd>) découpe une baie seule dans un mur là où vous cliquez : un passage, une trappe, un trou pour une porte qui viendra plus tard. Sa largeur, sa hauteur et son allège se règlent dans le panneau des propriétés avant le clic. Sélectionnée, sa taille, sa place le long du mur et son allège s’y modifient ; comme une porte, elle doit tenir dans le mur et rester à l’écart de ses autres baies. En IFC, c’est une ouverture qui évide le mur, sans rien pour la remplir.

Sélectionnée, une baie a des poignées comme une fenêtre : faites glisser son milieu pour la déplacer le long du mur, ou un tableau pour l’élargir ou la rétrécir tandis que l’autre reste en place ; la longueur près de la souris peut se taper, comme en dessinant. On la prend d’un clic dans son vide sur le plan, ou dans son trou dans la vue 3D, où son contour s’allume. **Déplacer** (<kbd>M</kbd>) la fait glisser le long du mur d’une distance tapée, et sa hauteur et son allège sont dans le panneau des propriétés.

**Couvrement** donne la forme de son haut : **Rectangle**, **Arc segmentaire** (un arc surbaissé), **Arc elliptique** (une demi-ellipse, haute d’un quart de la largeur sauf indication contraire), **Plein cintre** ou **Angles arrondis** (un haut droit aux angles supérieurs arrondis). **Flèche de l’arc** règle la montée d’un arc segmentaire ou elliptique, **Rayon des angles** l’arrondi des angles (150 mm sauf indication contraire, jamais plus de la moitié de la largeur) ; vide, celui de la forme. La baie dans le mur suit le contour en 3D, dans les élévations et les coupes et dans l’IFC ; le plan montre ses tableaux comme avant.

## Le configurateur

**Architecture → Portes et fenêtres** ouvre le configurateur. Chaque type de porte et de fenêtre du projet est listé à gauche. Vous pouvez modifier :

- le nom et le repère du type (la lettre dans les étiquettes) ;
- sa largeur et sa hauteur ;
- la profondeur et la largeur du cadre ;
- ses colonnes et ses rangées de vantaux, et la façon dont chaque vantail s’ouvre ;
- la finition de l’embrasure, l’encadrement en façade, le seuil et les couleurs.

Les dessins à droite montrent le type tel qu’il sera dessiné en élévation, en plan et en coupe, pendant que vous le modifiez.

**Gauche et droite.** Le sens d’ouverture d’une porte est nommé comme le nomment les architectes : regardez de la poignée vers les paumelles ; un vantail qui s’ouvre à gauche de cette ligne s’ouvre à gauche, un qui s’ouvre à droite, à droite. Les choix du configurateur pour un vantail le disent ainsi, avec le côté de ses paumelles dans la grille **Vantaux**, vue de l’extérieur, du côté d’où l’on pousse les vantaux : *Ouverture à droite (paumelles à gauche)*. Un fichier IFC nomme la main à sa façon (les paumelles vues en regardant dans le sens où s’ouvre le vantail), si bien que la même porte y est écrite SINGLE_SWING_LEFT ; Lapin passe de l’une à l’autre en écrivant et en lisant l’IFC. La gauche et la droite d’une fenêtre sont celles d’IFC : ses paumelles vues de l’extérieur. L’élévation est vue de l’intérieur, du côté vers lequel s’ouvrent les vantaux, sauf si **Depuis l’extérieur** est coché ; comme le plan, elle montre la porte telle qu’elle est dans le modèle. Une porte retournée sur l’autre main (**Main**, ou une flèche de retournement le long du mur) est l’image en miroir de son type ; les nomenclatures la mettent sur une fiche à part (voir [Nomenclatures](schedules.md)).

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

### Formes, styles et ornements

**Forme** donne à une porte ou une fenêtre une tête cintrée (**Arc en plein cintre**, **Arc segmentaire**, **Arc elliptique**, **Angles arrondis**, **Arc brisé**, **Tête triangulaire**) ou la rend **Ronde ou ovale** (ronde quand la largeur et la hauteur sont égales). Un type cintré garde ses vantaux sous une traverse à la naissance de l’arc, et au-dessus une imposte fixe qui suit l’arc, avec des petits-bois si on le souhaite : **Éventail**, **Les meneaux prolongés** ou **Croix**. **Flèche de l’arc** règle la montée d’un arc segmentaire, elliptique, brisé ou triangulaire, **Rayon des angles** l’arrondi des angles d’une tête aux angles arrondis. La baie dans le mur, le cadre, le vitrage, l’encadrement et les dessins suivent le contour.

**Petits-bois** divisent les vitrages : **Grille (colonial)** avec ses carreaux en largeur et en hauteur, **Prairie** avec des barres près des bords, ou des **Losanges**.

Sur la façade, en plus de l’encadrement, une fenêtre peut avoir une **Clé de voûte**, un **Couronnement au-dessus de la baie** (une **Corniche**, un **Fronton**, un **Fronton cintré** ou un **Larmier** qui suit une tête cintrée) et une **Allège sous l’appui** avec ses consoles.

**Appliquer un style…** règle d’un coup la forme, les petits-bois et les ornements : Moderne, Classique, Géorgien, Victorien, Roman, Baroque, Gothique, Tudor, Prairie, Traditionnel ou une fenêtre ronde. Tout reste modifiable.

![Une fenêtre baroque : plein cintre avec éventail, clé de voûte et fronton cintré](shots/joinery-styles.webp)

![Fenêtres cintrées en 3D](shots/window-styles-3d.webp)

### Poignées

**Poignée** choisit la poignée du type : **Béquille**, **Bouton**, **Barre de tirage**, **Poignée de fenêtre** ou aucune. Par défaut, une porte a une béquille et une fenêtre une poignée de fenêtre. Une porte a sa poignée sur les deux faces, à la **Hauteur de la poignée** indiquée ; **Couleur de la poignée** règle sa couleur. Les poignées se voient en 3D, en coupe et en élévation, et dans les dessins du configurateur quand **Quincaillerie** est cochée.

### Paramètres

**Paramètres · dans les nomenclatures**, en bas du configurateur, contient les paramètres propres du type : **+ Résistance au feu** (rangée dans Pset_DoorCommon ou Pset_WindowCommon d’IFC comme FireRating), **+ Fabricant**, **+ Code produit**, **+ Site web**, ou tout paramètre que vous nommez. Les portes et fenêtres du type en héritent, et chacun devient une ligne des nomenclatures des menuiseries : voir [Nomenclatures et graphiques](schedules.md).

## Étiquettes en plan

**Annoter → Étiquettes de portes et fenêtres** place un marqueur sur chaque porte et fenêtre d’un plan : une ligne en travers du mur, le repère à l’extérieur, et les dimensions et l’allège à l’intérieur. Pour changer l’aspect des étiquettes, allez dans **Gérer → Normes de dessin → Étiquettes**.

### Modifier un repère à l’écran

Chaque repère peut être disposé à la main, partie par partie. Cliquez sur sa ligne ou sur l’un de ses textes pour le sélectionner : chaque texte a alors une poignée carrée (glissez-la pour déplacer le texte) et une ronde après sa fin (glissez-la pour tourner le texte), et chaque extrémité de la ligne a une poignée (glissez-la pour allonger ou raccourcir la ligne ; le texte de cette extrémité suit). Ctrl/⌘-clic sur une poignée remet cette partie comme avant. Chaque glissement est un pas d’annulation. Avec plusieurs repères du même plan sélectionnés, une partie glissée sur l’un change la même partie de tous.

![Le repère d’une porte sélectionné : une poignée par texte, sa poignée de rotation et une à chaque extrémité de la ligne](shots/marker-grips.webp)

![Le repère déplacé et tourné, l’extrémité extérieure de la ligne allongée](shots/marker-edited.webp)

Les modifications appartiennent au repère, donc à ce plan : la même porte peut être disposée autrement dans un autre plan. Elles sont gardées le long et en travers du mur, si bien qu’elles suivent la porte quand elle se déplace, tourne ou passe de l’autre côté. Dans les propriétés du repère, **Repère** liste ses parties : tapez la rotation d’un texte en degrés ou l’avancée d’une extrémité en millimètres papier, remettez une partie avec ↺, ou toutes avec **Remettre l’étiquette en place**. Le moteur dessine les repères tels qu’ils sont modifiés, donc les planches, le PDF et le DXF les impriment pareil.

![Les parties d’un repère dans ses propriétés](shots/marker-properties.webp)

Que les textes soient en travers de la ligne du repère (le long du mur, par défaut) ou le long de celle-ci se règle pour chaque type d’étiquette, dans **Gérer → Normes de dessin → Étiquettes**, colonne **Direction du texte** : **perpendiculaire à la ligne d’étiquette** ou **le long de la ligne d’étiquette**. Dans les deux cas ils sont dessinés lisibles, jamais à l’envers ; un texte tourné à la main reste lisible aussi.

![Les textes du repère de la porte le long de la ligne d’étiquette](shots/marker-along.webp)
