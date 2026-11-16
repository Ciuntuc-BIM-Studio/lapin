---
title: Dalles et toits
order: 40
summary: Dessinez dalles et toits par leur contour, choisissez la forme et les rives d’un toit, et percez des trémies.
---

## Dalles

Choisissez **Architecture → Dalle** (<kbd>SB</kbd>), puis cliquez les angles de son contour. Fermez le contour sur le premier point, ou appuyez sur <kbd>Enter</kbd>. Une dalle est elle aussi faite de couches, définies dans **Architecture → Types de dalles**. Une couche d’un type de dalle peut avoir une épaisseur de 0 (une étanchéité, une peinture) : elle ne construit rien, est dessinée en trait dans les coupes et est métrée à la surface de la dalle.

Dans **Types de dalles**, chaque couche a une **Priorité** (de 0 à 100) et une **Catégorie**, comme les couches d’un type de mur. Une couche de mur qui descend dans la dalle (**Dans les dalles** dans **Types de murs**) traverse les couches de la dalle de priorité plus faible et s’arrête sur la première de priorité égale ou supérieure. Vide, la priorité est celle du matériau dans **Gérer → Ordre de coupe**, sinon 50 pour le noyau et 0 pour les autres couches. Une couche marquée **Finition** disparaît quand une vue omet les finitions des dalles.

Pour changer le contour, sélectionnez la dalle et faites glisser ses angles. Pour ajouter un angle, maintenez <kbd>Ctrl/⌘</kbd> et cliquez sur le petit losange au milieu d'une arête, ou faites un clic droit sur la dalle sélectionnée près d'une arête et choisissez **Ajouter un coin ici** : le nouveau coin se place là où vous avez cliqué. Pour en retirer un, faites <kbd>Ctrl/⌘</kbd>-clic dessus, ou clic droit dessus et choisissez **Retirer le coin** (un contour garde au moins trois coins). Chaque action est une seule étape d'annulation, et il en va de même pour les trémies, les toits, les pièces et les balcons. **Trémie de dalle** (<kbd>SH</kbd>) perce un trou dans une dalle, pour un escalier ou une gaine.

Pour agrandir ou réduire tout le contour d'un coup, chaque arête de la même distance (le *Offset all edges* d'Archicad), maintenez <kbd>Shift</kbd> et faites glisser le losange au milieu d'une arête quelconque : le contour suit le curseur en direct, en plan ou en 3D, et la distance peut être tapée pendant le glisser (<kbd>Entrée</kbd> la pose). Ou faites un clic droit sur la dalle sélectionnée, choisissez **Décaler le contour…** et tapez la distance : positive vers l'extérieur, négative vers l'intérieur. Les arêtes droites restent parallèles et se rejoignent à leurs coins ; une arête courbe reste un arc autour de son propre centre, et là où un arc rencontre une autre arête à un coin, l'agrandissement arrondit ce coin. Un décalage qui ferait disparaître une arête ou se croiser le contour est refusé, avec la raison au curseur. Cela marche sur les dalles, les trémies, les plafonds, les toits (chaque arête garde sa règle d'avant-toit ou de pignon), les pièces et les balcons, et sur plusieurs éléments sélectionnés à la fois ; chacun est une seule étape d'annulation.

![Le contour d'une dalle décalé en faisant glisser avec Shift le milieu d'une arête](shots/floor-offset-outline.webp)

### Arêtes courbes, arrondis et dalles rondes

La barre de l'outil **Dalle**, dans la ligne de commande, dessine le contour en **Polygone** (ses coins cliqués), **Rectangle** (deux coins opposés), **Cercle** (son centre, puis un point sur lui) ou **À l'intérieur des murs** (un clic à l'intérieur des murs qui la ferment ; la dalle va jusqu'à leurs faces). Dans un polygone, **Lignes (Espace)** devient **Arcs (Espace)** : les arêtes suivantes sont des arcs, chacun tangent à l'arête précédente, jusqu'à ce que vous appuyiez de nouveau sur <kbd>Espace</kbd>. **Trémie de dalle** et **Plafond** ont la même barre, sans **À l'intérieur des murs**.

![L'outil Dalle dessinant un cercle à côté d'une dalle](shots/floor-shapes.webp)

Toute arête d'une dalle, d'une trémie ou d'un faux plafond peut être courbe. Faites un clic droit sur la dalle sélectionnée près d'une arête et choisissez **Faire un arc** : l'arête se bombe vers l'extérieur d'un huitième de sa longueur, et une poignée ronde apparaît au milieu de l'arc. Faites-la glisser pour courber l'arc par le point voulu (le losange, désormais au milieu de la corde, déplace toujours l'arête). **Rendre droite** la refait en ligne. Un coin entre deux arêtes droites peut être arrondi ou chanfreiné : faites un clic droit dessus et choisissez **Arrondir le coin…** (saisissez le rayon) ou **Chanfreiner le coin…** (saisissez le recul le long de chaque arête). Le coin devient deux, là où l'arc ou le chanfrein rencontre les arêtes ; un rayon trop grand pour les arêtes est refusé. Chacun est une seule étape d'annulation.

![Une dalle avec une arête courbe et un coin arrondi](shots/floor-curved-edge.webp)

La dalle est construite le long de ses arcs, aucune corde à plus de 1 mm d'eux, en 3D, en plan et en coupe ; son aire et son périmètre sont mesurés sur les arcs vrais. Une couche qui dépasse une arête courbe (voir [Extensions de couche](walls.md#layer-extents)) suit l'arc. Une trémie peut aussi avoir des arêtes courbes, et une dalle courbe peut avoir des trémies. En IFC la dalle porte son contour avec les arcs vrais comme empreinte (une IfcIndexedPolyCurve à segments d'arc), à côté de son corps. Les toitures gardent des arêtes droites, car leurs règles d'arête (égout, pignon) se fixent par arête droite.

#### Modifier l’esquisse

Pour plus qu'un coin ou un arc, faites un clic droit sur la dalle sélectionnée (ou la trémie, ou le plafond) et choisissez **Modifier l’esquisse** (aussi **Studio → Modifier l’esquisse**). Son contour s'ouvre en esquisse sur un plan à lui, une ligne ou un arc par arête, ses coins tenus ensemble, la dalle elle-même masquée là ; une barre au-dessus du plan dit de qui est l'esquisse. Dessinez et modifiez avec l'onglet **Dessin** (Ligne, Arc, Raccord, Ajuster, Prolonger, Décaler…), contraignez et cotez avec **Studio → Contraintes** (voir [Contraintes d'esquisse](sketch-constraints.md)) : changez une cote et le contour suit. **Terminer l’esquisse** la réécrit dans la dalle en une seule étape d'annulation, la dalle gardant son identité (son GlobalId) ; **Annuler** laisse la dalle telle qu'elle était. L'esquisse doit être un seul contour fermé (arêtes bout à bout, ou un cercle seul) ; sinon Terminer dit ce qui est ouvert.

![Le contour d'une dalle modifié en esquisse : un raccord et une cote](shots/floor-sketch-edit.webp)

L'esquisse, avec ses contraintes et ses cotes, reste sur la dalle (elle part en IFC avec l'intention de conception de la dalle, dans Lapin_Intent), si bien que **Modifier l’esquisse** la rouvre comme vous l'avez laissée. Si le contour a été changé depuis par ses poignées, l'esquisse repart du contour tel qu'il est, sans ses contraintes. Une dalle reconnue d'un fichier IFC ne reçoit une esquisse que lorsque vous la modifiez ; une dalle gardée comme référence ne se modifie pas ainsi.

![La dalle après Terminer l’esquisse](shots/floor-sketch-done.webp)

Une dalle dessinée hors des murs, devant une porte, est un perron. Pour les marches qui y mènent, voir [Escaliers](stairs.md).

Une couche d'une dalle peut aussi aller au-delà de son contour, ou s'arrêter avant, tout le tour ou à un bord, saisie dans ses propriétés ou tirée en plan ou en coupe avec **Modifier les couches**, et un type de dalle peut donner leurs prolongements à ses dalles : voir [Prolongements des couches](walls.md#prolongements-des-couches). Les toits n'ont pas encore de composition de couches (leur couverture, leur dalle et leur charpente sont des parties à part), donc pas de prolongements de couches.

**Modifier → Scinder l’élément** sur une dalle ou un plafond demande deux points d’une ligne de coupe : elle se prolonge au-delà et doit croiser le contour exactement deux fois. La dalle devient deux dalles ; la partie cliquée garde l’id (GlobalId en IFC), les arcs des bords restent des arcs (un arc coupé reste un arc) et une trémie va avec la partie où elle se trouve. Une trémie que la ligne traverse est refusée. **Modifier → Ajuster/prolonger les éléments** accepte une limite droite (un mur, une poutre ou une ligne dessinée) et coupe la dalle ou le plafond là, en gardant le côté cliqué.

![Une dalle à bord courbe scindée le long d’une ligne : la partie de droite sélectionnée](shots/floor-split.webp)

## Toits

Choisissez **Architecture → Toit** (<kbd>RF</kbd>) et cliquez le contour des murs qu’il couvre. Lapin construit le toit sur ce contour, en haut de son niveau.

![Une maison avec un toit à deux pans dans la vue 3D](shots/house-3d.webp)

Sélectionnez le toit pour le modifier dans le panneau des propriétés :

- **Forme.** À quatre pans, à deux pans, monopente, mansardé, en berceau ou plat. Un toit en voûte est une voûte courbe entre deux pignons ; **Voûte : flèche à la clé** règle la hauteur à laquelle il monte à la clé, au plus la moitié de sa portée.
- **Pente et débord** pour tout le toit.
- **Direction du faîtage (deux pans, monopente)** : auto, x ou y. Sur un toit monopente, le faîtage est le côté haut : sur x il descend vers −y, sur y vers −x ; **Monopente : inverser la pente** le fait descendre de l'autre côté.
- **Chaque rive** avec son propre type (égout ou pignon), sa pente et son débord. Un toit à la Mansart avec des rives en pignon est un comble à la hollandaise.
- **Style.** Les styles de toit donnent tout un aspect en un seul choix : la couverture, les égouts, les gouttières et les descentes.
- **Matériau de la couverture.** La couverture est faite d’un matériau du projet, par défaut celui de la couverture choisie (Clay tile, Metal sheet, Slate…), créé dans la couleur de la couverture la première fois qu’un toit en a besoin. Choisissez-en un autre dans les propriétés ou dans **Couverture et évacuation** du configurateur ; changer de couverture le change aussi, sauf si vous en avez choisi un. Les façades et les coupes hachurent la couverture selon ce matériau (tuiles, bardeaux et ardoises en rangs horizontaux, tôle en joints verticaux, sauf si vous lui donnez une hachure **Elevation** propre), une coupe dessine la couverture coupée avec la hachure de coupe du matériau, et l’IFC, le métré et le devis listent la couverture sous son matériau.
- **Épaisseur de la couverture.** Vide, la couverture est aussi épaisse que celle du catalogue (une membrane 2 cm, la tôle 3 cm, les tuiles 5 cm…) ; tapez-en une pour lui donner la sienne. Un toit importé d’un autre programme garde celle du fichier.
- **Support.** Un toit plat, une voûte en berceau et un toit dont la **Structure** est none ont un support sous la couverture, une pièce à part : **Matériau du support** (Reinforced concrete par défaut, créé la première fois qu’un toit en a besoin) et **Épaisseur du support** (16 cm par défaut), dans les propriétés ou dans **Charpente** du configurateur. La couverture repose dessus, le toit garde donc l’épaisseur des deux ensemble. Une coupe dessine deux bandes, chacune hachurée selon son matériau ; les fenêtres de toit traversent les deux ; un mur attaché à un toit plat monte jusqu’à la sous-face du support ; l’IFC écrit le support et la couverture comme deux dalles de toiture de leurs matériaux, et le métré et le devis mesurent le support (surface et volume) et la couverture (surface), chacun sous son matériau. Les propriétés n’affichent ces deux champs que sur un toit sans charpente, et les champs des chevrons (sections, entraxe, classe, pannes, abouts) que sur un toit avec charpente ; un champ propre à une seule forme (celui de la mansarde, la flèche de la voûte, l’inversion du toit à un pan) n’apparaît que sur cette forme.

![Les propriétés d’un toit](shots/roof-properties.webp)

![Un toit plat en coupe : le support en béton et la couverture dessus, chacun hachuré selon son matériau](shots/roof-deck-section.webp)

**Configurateur de toit** (le bouton à côté du style, ou **Architecture → Roof configurator** avec un toit sélectionné) réunit tout : les styles à gauche, puis la forme et ses rives, la couverture et l’évacuation, la charpente et les ouvertures, au-dessus d’un plan du toit (un clic sur une rive la rend auto, égout ou pignon) et, pour la charpente, une coupe en travers du faîtage avec la fermette ou le couple du milieu. Partez d’un style et changez n’importe quel champ à la main ; **Apply** en fait une seule étape d’annulation. **Enregistrer comme style…** garde le toit tel qu’il est configuré comme style du projet, listé sous **Project** au-dessus de ceux du catalogue (et dans la liste du panneau des propriétés) pour d’autres toits et les nouveaux ; **Mettre à jour le style** y enregistre à nouveau les changements, et tous les toits du style suivent. Un style du projet peut être renommé ou supprimé ; ses toits restent tels quels.

![Le configurateur de toit](shots/roof-configurator.webp)

**Gouttières et descentes.** Dans **Couverture et évacuation** du configurateur, la gouttière prend sa taille, son avancée devant l’égout et sa hauteur sous la couverture, sa pente vers les naissances (mm/m) et ses fonds ; le tableau des rives active ou coupe la gouttière de chaque égout. Les descentes sont d’abord celles du toit, une tous les 10 m ; **Les rendre modifiables** en fait des éléments, et **Architecture → Downpipe** en ajoute une d’un clic sur une gouttière. La poignée d’une descente la fait glisser le long de sa gouttière seulement ; ses propriétés fixent son diamètre, le dauphin en pied et son tracé, une chaîne de tronçons avec leur longueur et leur angle depuis la verticale (vers le mur), le dernier jusqu’au sol. En IFC une gouttière est un IfcPipeSegment GUTTER et une descente un segment rigide nommé Downpipe.

Les murs du niveau du toit s’arrêtent dessous et montent d’eux-mêmes jusqu’à ses pignons, sous un toit courbe aussi. Un mur d’un autre niveau suit le toit après **Architecture → Attacher au toit** (<kbd>AR</kbd>) : sélectionnez les murs, puis cliquez le toit. Le même outil accepte tout autre corps au-dessus des murs : un solide Studio, une dalle, une poutre, un élément importé d’IFC. Les murs montent jusqu’à sa sous-face et sont coupés le long de celle-ci, quelle que soit sa forme. **Détacher du toit** l’annule.

**Fenêtre de toit** et **Lucarne** ajoutent des ouvertures à un toit : cliquez sur un pan. Elles appartiennent au toit. Sélectionnez le toit, et chacune montre ses poignées en plan. Faites glisser la poignée du milieu pour la déplacer sur le toit. Faites glisser les poignées des bords ou des coins d’une fenêtre de toit pour la redimensionner sur son pan (le bord opposé reste en place ; vous pouvez taper la longueur pendant le glissement). La largeur suit l’égout et la longueur monte le long du pan, mesurée sur le pan. Une lucarne a une poignée de chaque côté pour sa largeur. Un changement qui ne tiendrait pas sur le toit est refusé. Pour la retirer, faites un clic droit sur l’une de ses poignées et choisissez **Retirer la fenêtre de toit** (ou **Retirer la lucarne**), appuyez sur **Suppr** quand le curseur est sur sa poignée, ou faites **Ctrl/⌘-clic** sur la poignée du milieu. Les propriétés du toit les listent aussi : tapez-y la largeur et la longueur d’une fenêtre, ou cliquez sur **×** pour la retirer. Le toit d’une lucarne est couvert du matériau de la couverture et mesuré avec elle : les quantités du toit donnent à part la **couverture des lucarnes** (l’aire de ses pans, le nombre de lucarnes), si bien que le métré par matériau, le devis et une tâche de planning sur la couverture comptent les pans du toit (sans les découpes des lucarnes) et les toits des lucarnes, chacun une fois.

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

**Intérieur → Plafond** (<kbd>CG</kbd>) dessine un faux plafond sur un contour cliqué ; **Plafond dans une pièce** remplit la pièce cliquée en suivant son contour. Un plafond est suspendu à sa **Hauteur** au-dessus de l’étage (2700 mm sauf réglage dans ses propriétés).

Son type, dans **Intérieur → Types de plafond**, règle le **Système** : **Dalles sur ossature apparente** (largeur, longueur et épaisseur de la dalle, largeur vue, hauteur et rotation de l’ossature, suspentes jusqu’à la structure à leur espacement) ou **Plaques de plâtre, continu**. L’ossature est centrée sur le plafond, si bien que les dalles coupées en rive sont égales.

**Dalle lumineuse** et **Diffuseur d’air** en posent un dans la case cliquée ; un clic sur la même case avec l’autre outil le change. Dans le panneau des propriétés, **Répartir** pose des dalles lumineuses ou des diffuseurs toutes les tant de cases, et **Vider** les retire. Les nomenclatures comptent les dalles entières, les dalles lumineuses et les diffuseurs, et le plafond part en IFC comme IfcCovering de type CEILING.

![Les propriétés d’un plafond : son type, sa hauteur et ses équipements](shots/ceiling-properties.webp)

![Plafonds à dalles avec dalles lumineuses et diffuseurs, en 3D](shots/ceilings-3d.webp)

## Plinthes

**Intérieur → Plinthe dans la pièce** (<kbd>SK</kbd>) pose une plinthe tout autour de la pièce cliquée, le long des faces des murs, interrompue à chaque porte (toute ouverture qui descend jusqu’au sol) ; un clic entre des murs sans pièce les suit de la même façon. La plinthe d’une pièce suit la pièce : déplacez un mur ou une porte et la plinthe les suit. **Plinthe** en dessine une le long d’une ligne cliquée sur la face d’un mur (Entrée termine) ; la plinthe se place du côté opposé au mur, et **Changer de côté** dans ses propriétés la retourne.

Son type, dans **Intérieur → Types de plinthe**, fixe le **Profil** — **Droite (carrée)**, **Biseautée (MDF)**, **Moulurée (bois)**, **À gorge (métal)**, **Bord demi-rond** ou **Bord arrondi**, dessiné à côté à l’échelle —, la **Hauteur** (40 à 300 mm), l’**Épaisseur** (5 à 40 mm), le **Matériau** et sa couleur. **De la bibliothèque** apporte au projet des types tout faits : MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 et Bullnose 70. Tant qu’un outil de plinthe est actif, le panneau des propriétés montre le type qu’il pose, à choisir avant le clic.

Dans ses propriétés, une plinthe a son type, un **Décalage de base** au-dessus du sol et **Interrompue aux portes** (décoché, elle passe devant). **Nomenclature des plinthes** (ou **Plinthes** dans la liste des nouvelles nomenclatures du navigateur du projet) les liste par type avec leurs morceaux et leur longueur, sans les vides des portes. La plinthe est coupée d’onglet aux angles ; un plan dessine son arête avant, et une plinthe part en IFC comme un IfcCovering de type SKIRTINGBOARD, d’un IfcCoveringType du même type.

![Les types de plinthe : le profil dessiné à l’échelle, la bibliothèque](shots/skirting-types.webp)

![Les propriétés d’une plinthe : son type, le décalage de base et les vides aux portes](shots/skirting-properties.webp)
