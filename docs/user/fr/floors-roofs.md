---
title: Dalles et toits
order: 40
summary: Dessinez dalles et toits par leur contour, choisissez la forme et les rives d’un toit, et percez des trémies.
---

## Dalles

Choisissez **Architecture → Dalle** (<kbd>SB</kbd>), puis cliquez les angles de son contour. Fermez le contour sur le premier point, ou appuyez sur <kbd>Enter</kbd>. Une dalle est elle aussi faite de couches, définies dans **Architecture → Types de dalles**. Une couche d’un type de dalle peut avoir une épaisseur de 0 (une étanchéité, une peinture) : elle ne construit rien, est dessinée en trait dans les coupes et est métrée à la surface de la dalle.

Pour changer le contour, sélectionnez la dalle et faites glisser ses angles. **Trémie de dalle** (<kbd>SH</kbd>) perce un trou dans une dalle, pour un escalier ou une gaine.

Une dalle dessinée hors des murs, devant une porte, est un perron. Pour les marches qui y mènent, voir [Escaliers](stairs.md).

## Toits

Choisissez **Architecture → Toit** (<kbd>RF</kbd>) et cliquez le contour des murs qu’il couvre. Lapin construit le toit sur ce contour, en haut de son niveau.

![Une maison avec un toit à deux pans dans la vue 3D](shots/house-3d.webp)

Sélectionnez le toit pour le modifier dans le panneau des propriétés :

- **Forme.** À quatre pans, à deux pans, monopente, mansardé, en berceau ou plat. Un toit en voûte est une voûte courbe entre deux pignons ; **Voûte : flèche à la clé** règle la hauteur à laquelle il monte à la clé, au plus la moitié de sa portée.
- **Pente et débord** pour tout le toit.
- **Direction du faîtage (deux pans, monopente)** : auto, x ou y. Sur un toit monopente, le faîtage est le côté haut : sur x il descend vers −y, sur y vers −x ; **Monopente : inverser la pente** le fait descendre de l'autre côté.
- **Chaque rive** avec son propre type (égout ou pignon), sa pente et son débord. Un toit à la Mansart avec des rives en pignon est un comble à la hollandaise.
- **Style.** Les styles de toit donnent tout un aspect en un seul choix : la couverture, les égouts, les gouttières et les descentes.

![Les propriétés d’un toit](shots/roof-properties.webp)

**Configurateur de toit** (le bouton à côté du style, ou **Architecture → Roof configurator** avec un toit sélectionné) réunit tout : les styles à gauche, puis la forme et ses rives, la couverture et l’évacuation, la charpente et les ouvertures, au-dessus d’un plan du toit (un clic sur une rive la rend auto, égout ou pignon) et, pour la charpente, une coupe en travers du faîtage avec la fermette ou le couple du milieu. Partez d’un style et changez n’importe quel champ à la main ; **Apply** en fait une seule étape d’annulation. **Enregistrer comme style…** garde le toit tel qu’il est configuré comme style du projet, listé sous **Project** au-dessus de ceux du catalogue (et dans la liste du panneau des propriétés) pour d’autres toits et les nouveaux ; **Mettre à jour le style** y enregistre à nouveau les changements, et tous les toits du style suivent. Un style du projet peut être renommé ou supprimé ; ses toits restent tels quels.

![Le configurateur de toit](shots/roof-configurator.webp)

**Gouttières et descentes.** Dans **Couverture et évacuation** du configurateur, la gouttière prend sa taille, son avancée devant l’égout et sa hauteur sous la couverture, sa pente vers les naissances (mm/m) et ses fonds ; le tableau des rives active ou coupe la gouttière de chaque égout. Les descentes sont d’abord celles du toit, une tous les 10 m ; **Les rendre modifiables** en fait des éléments, et **Architecture → Downpipe** en ajoute une d’un clic sur une gouttière. La poignée d’une descente la fait glisser le long de sa gouttière seulement ; ses propriétés fixent son diamètre, le dauphin en pied et son tracé, une chaîne de tronçons avec leur longueur et leur angle depuis la verticale (vers le mur), le dernier jusqu’au sol. En IFC une gouttière est un IfcPipeSegment GUTTER et une descente un segment rigide nommé Downpipe.

Les murs du niveau du toit s’arrêtent dessous et montent d’eux-mêmes jusqu’à ses pignons, sous un toit courbe aussi. Un mur d’un autre niveau suit le toit après **Architecture → Attacher au toit** (<kbd>AR</kbd>) : sélectionnez les murs, puis cliquez le toit. Le même outil accepte tout autre corps au-dessus des murs : un solide Studio, une dalle, une poutre, un élément importé d’IFC. Les murs montent jusqu’à sa sous-face et sont coupés le long de celle-ci, quelle que soit sa forme. **Détacher du toit** l’annule.

**Fenêtre de toit** et **Lucarne** ajoutent des ouvertures à un toit : cliquez sur un pan. Elles appartiennent au toit. Sélectionnez le toit, et chacune montre ses poignées en plan. Faites glisser la poignée du milieu pour la déplacer sur le toit. Faites glisser les poignées des bords ou des coins d’une fenêtre de toit pour la redimensionner sur son pan (le bord opposé reste en place ; vous pouvez taper la longueur pendant le glissement). La largeur suit l’égout et la longueur monte le long du pan, mesurée sur le pan. Une lucarne a une poignée de chaque côté pour sa largeur. Un changement qui ne tiendrait pas sur le toit est refusé. Pour la retirer, faites un clic droit sur l’une de ses poignées et choisissez **Retirer la fenêtre de toit** (ou **Retirer la lucarne**), appuyez sur **Suppr** quand le curseur est sur sa poignée, ou faites **Ctrl/⌘-clic** sur la poignée du milieu. Les propriétés du toit les listent aussi : tapez-y la largeur et la longueur d’une fenêtre, ou cliquez sur **×** pour la retirer.

![La poignée d’une fenêtre de toit sur le toit sélectionné](shots/roof-window-grip.webp)

### Charpente bois

**Structure** fixe la charpente du toit :

- **rafters** : chevrons sur pannes, avec une panne faîtière.
- **collar** : des couples de chevrons avec un entrait retroussé horizontal à **Entraits retroussés : part de la hauteur** (deux tiers par défaut), sans pannes.
- **trusses** : des fermettes préfabriquées le long du faîtage principal, à l’**Entraxe des chevrons** : membrures supérieures sur les versants, une membrure inférieure sur les sablières, diagonales en W (fermette monopente sur un toit monopente). Les extrémités d’un toit à quatre pans gardent leurs chevrons.
- **none** : un plancher, sans bois.

**Chevrons et membrures** accepte les anciennes sections en cm ou les nordiques en mm (45×95 à 45×245) ; avec elles, **Diagonales des fermettes** et **Classe de résistance du bois** (C14 à C30, lamellé-collé). Les chevrons reposent sur la sablière par une entaille (**Appui des chevrons sur la sablière**) et finissent à l’égout d’aplomb ou d’équerre (**About des chevrons à l’égout**). Les styles **Swedish truss roof** et **Nordic collar-tie roof** apportent leur charpente.

Chaque pièce est une partie à part : le tableau **Roof timber** les donne en liste de débit (pièce, section, classe, longueur, nombre, volume), et en IFC chacune est un IfcMember de son type, chaque fermette un IfcElementAssembly.

## Faux plafonds

**Finitions → Plafond** (<kbd>CG</kbd>) dessine un faux plafond sur un contour cliqué ; **Plafond dans une pièce** remplit la pièce cliquée en suivant son contour. Un plafond est suspendu à sa **Hauteur** au-dessus de l’étage (2700 mm sauf réglage dans ses propriétés).

Son type, dans **Finitions → Types de plafond**, règle le **Système** : **Dalles sur ossature apparente** (largeur, longueur et épaisseur de la dalle, largeur vue, hauteur et rotation de l’ossature, suspentes jusqu’à la structure à leur espacement) ou **Plaques de plâtre, continu**. L’ossature est centrée sur le plafond, si bien que les dalles coupées en rive sont égales.

**Dalle lumineuse** et **Diffuseur d’air** en posent un dans la case cliquée ; un clic sur la même case avec l’autre outil le change. Dans le panneau des propriétés, **Répartir** pose des dalles lumineuses ou des diffuseurs toutes les tant de cases, et **Vider** les retire. Les nomenclatures comptent les dalles entières, les dalles lumineuses et les diffuseurs, et le plafond part en IFC comme IfcCovering de type CEILING.

![Les propriétés d’un plafond : son type, sa hauteur et ses équipements](shots/ceiling-properties.webp)

![Plafonds à dalles avec dalles lumineuses et diffuseurs, en 3D](shots/ceilings-3d.webp)

## Plinthes

**Finitions → Plinthe dans la pièce** (<kbd>SK</kbd>) pose une plinthe tout autour de la pièce cliquée, le long des faces des murs, interrompue à chaque porte (toute ouverture qui descend jusqu’au sol) ; un clic entre des murs sans pièce les suit de la même façon. La plinthe d’une pièce suit la pièce : déplacez un mur ou une porte et la plinthe les suit. **Plinthe** en dessine une le long d’une ligne cliquée sur la face d’un mur (Entrée termine) ; la plinthe se place du côté opposé au mur, et **Changer de côté** dans ses propriétés la retourne.

Son type, dans **Finitions → Types de plinthe**, fixe le **Profil** — **Droite (carrée)**, **Biseautée (MDF)**, **Moulurée (bois)**, **À gorge (métal)**, **Bord demi-rond** ou **Bord arrondi**, dessiné à côté à l’échelle —, la **Hauteur** (40 à 300 mm), l’**Épaisseur** (5 à 40 mm), le **Matériau** et sa couleur. **De la bibliothèque** apporte au projet des types tout faits : MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 et Bullnose 70. Tant qu’un outil de plinthe est actif, le panneau des propriétés montre le type qu’il pose, à choisir avant le clic.

Dans ses propriétés, une plinthe a son type, un **Décalage de base** au-dessus du sol et **Interrompue aux portes** (décoché, elle passe devant). **Nomenclature des plinthes** (ou **Plinthes** dans la liste des nouvelles nomenclatures du navigateur du projet) les liste par type avec leurs morceaux et leur longueur, sans les vides des portes. La plinthe est coupée d’onglet aux angles ; un plan dessine son arête avant, et une plinthe part en IFC comme un IfcCovering de type SKIRTINGBOARD, d’un IfcCoveringType du même type.

![Les types de plinthe : le profil dessiné à l’échelle, la bibliothèque](shots/skirting-types.webp)

![Les propriétés d’une plinthe : son type, le décalage de base et les vides aux portes](shots/skirting-properties.webp)
