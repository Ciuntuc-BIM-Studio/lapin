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

Tout autre profilé vient des bibliothèques : **D'une bibliothèque de profilés…** les parcourt toutes (voir [Bibliothèques de profilés](profiles.md)).

Les profilés creux sont vides à l’intérieur, dans la vue 3D, les coupes et les quantités. Les profilés formés à froid sont des formes génériques avec leurs dimensions, aux plis vifs, sans catalogue de fabricant.

![Des barres en 3D](shots/steel-3d.webp)

## Bâtiments à portiques

**Acier → Bâtiment à portiques…** ouvre le configurateur de bâtiment. Comme celui de la toiture, il a les styles à gauche, les champs du bâtiment en onglets au milieu et, à droite, le bâtiment tel que Lapin le construirait : un portique courant vu le long du bâtiment, avec ses jarrets, et un résumé de ce qu’il pèse.

![Le configurateur de bâtiment](shots/steel-hall.webp)

- **Styles** : les bâtiments types du catalogue (**Bâtiment 20 m**, **Bâtiment 30 m**, un **Appentis 10 m** monopente) et ceux du projet. Un style choisi remplit tous les champs ; chacun peut ensuite être modifié. **Enregistrer comme style…** garde le bâtiment tel que configuré comme style du projet ; **Mettre à jour le style** l’enregistre à nouveau dans son style, et tous les bâtiments de ce style suivent.
- **Portique** : la **Portée** (d’axe de poteau à axe de poteau), les **Travées** (égales comme `5 × 6`, ou chacune : `6, 6, 7.5`), la **Hauteur à la sablière** (le dessus des traverses au droit de la face extérieure du poteau), la **Toiture** (deux versants ou monopente) et sa **Pente**, les profilés des **Poteaux** et des **Traverses**.
- **Jarrets** : le **Jarret à la sablière** et le **Jarret au faîtage**, chacun avec sa **Longueur** le long de la traverse depuis la face du poteau (ou le faîtage) et sa **Hauteur** sous la traverse à cet endroit, et comment il est **Réalisé** : **découpé dans le profil de la traverse** (un I coupé en biais le long de son âme, une moitié soudée sous la traverse : l’âme et la semelle de la traverse) ou **en tôles soudées** (une **Tôle d’âme** et une **Tôle de semelle** de leur propre épaisseur, et la largeur de semelle).

![Les jarrets en tôles soudées](shots/steel-hall-haunches.webp)

- **Pignons** : des **potelets de pignon sous une traverse simple** (potelets d’angle et potelets à l’**Entraxe** choisi, leur axe fort perpendiculaire au mur de pignon, sous une traverse plus légère qui va jusqu’au nu extérieur des murs, les potelets ajustés sur sa face inférieure), ou **un portique courant** à chaque extrémité.

![Un pignon à potelets](shots/steel-hall-gable.webp)

- **Acier secondaire** : les pannes, les lisses, le contreventement, la poutre sablière et les bracons (ci-dessous).
- **Placement** : le croisement de l’axe A et de l’axe 1, et la direction de la portée en plan (**Rotation**) ; l’aperçu montre la trame du bâtiment vue du dessus.

**Créer le bâtiment** le fait en une seule étape, qu’une annulation défait : sa propre **trame** (axes 1, 2… aux portiques, A et B aux poteaux), et à chaque portique deux **poteaux** (poteaux de la structure en profilé acier, l’âme dans le plan du portique, la tête coupée selon la pente) et deux **traverses** en onglet au faîtage (une seule, du poteau bas au poteau haut, en monopente) : des barres en acier ajustées sur les faces des poteaux, avec leurs jarrets. Le bâtiment est sur l’étage courant ; la sablière et le faîtage sont des hauteurs de ses portiques, pas de nouveaux étages. Le résumé donne les portiques, les hauteurs et l’acier par nature : poteaux, traverses, jarrets, potelets de pignon, au total et par m² de surface.

![Le bâtiment en 3D](shots/steel-hall-3d.webp)

Le bâtiment reste modifiable. Sélectionnez une de ses barres : sa section **Acier** indique son bâtiment et **Modifier le bâtiment…**, qui ouvre le configurateur dessus ; **Appliquer** reconstruit le bâtiment sur les mêmes barres, qui gardent leur identité (une barre devenue inutile disparaît, une nouvelle est ajoutée). Les jarrets de la traverse y sont aussi listés, chacun modifiable seul (nature, longueur, hauteur, tôles) : un jarret réglé ainsi reste tel quel quand le bâtiment est reconstruit ; **×** le retire.

![Les jarrets d’une traverse dans les Propriétés](shots/steel-hall-props.webp)

## Assemblages

Les portiques d'un bâtiment viennent avec leurs **assemblages** (Steel → Portal frame hall…, onglet **Nœuds**) : un **pied de poteau** sous chaque poteau et potelet de pignon, un **assemblage en rive** où chaque traverse arrive à son poteau, un **assemblage au faîtage** où deux traverses se rencontrent, et un gousset sous la traverse au-dessus de chaque potelet. Chacun est un élément à part qui connaît les barres qu'il assemble ; ses plaques, boulons et ancrages sont faits de ses paramètres et des barres telles qu'elles sont, et les suivent quand le bâtiment change.

![L'onglet Nœuds](shots/steel-nodes-hall.webp)

- **Assemblages** : activés ou non pour tout le bâtiment.
- **Pieds de poteaux** : **articulés** (une platine, deux ancrages de part et d'autre de l'âme ou quatre entre les ailes) ou **encastrés** (une platine plus longue et plus épaisse, quatre à huit ancrages hors des ailes, des raidisseurs dans le plan de l'âme). Les **Ancrages** sont **droits**, **à crochet** ou **avec plaque d'ancrage** au pied ; le **Nombre d'ancrages** et la **Longueur d'ancrage** (sous le dessus du béton) sont choisis par le profilé quand ils restent vides. Si une semelle de la structure se trouve sous le poteau, les ancrages y entrent (et s'arrêtent à 50 mm au-dessus de son fond) ; sinon ils descendent de leur longueur sous le mortier de calage.
- **Assemblage en rive** : une platine d'about débordante sur la traverse, boulonnée à l'aile du poteau : son **Épaisseur de la plaque**, le **Diamètre des boulons** et leur **Classe**, combien elle dépasse **Au-dessus de la traverse** (avec une rangée de boulons) et les **Raidisseurs du poteau** au droit de l'aile supérieure de la traverse et de l'aile du jarret.
- **Assemblage au faîtage** : les platines d'about des deux traverses boulonnées l'une à l'autre sur la coupe d'onglet, avec leurs jarrets.

Ce qui reste vide est choisi par les profilés : boulons de M16 à M27 selon la hauteur de la barre boulonnée, une plaque aussi épaisse que ses boulons, pinces et entraxes d'après les trous. Un assemblage recule les extrémités de ses barres de ce qu'il met entre elles (la traverse de sa platine, le poteau de sa platine de pied, le potelet d'un jeu de 20 mm), par les règles d'extrémité des barres elles-mêmes.

![Un assemblage en rive](shots/steel-nodes-eaves.webp)

![Un pied de poteau encastré sur ancrages à crochet](shots/steel-nodes-base.webp)

Sélectionnez un assemblage pour le voir dans les Propriétés : les barres qu'il assemble, chaque paramètre tel qu'il est (une taille choisie par le profilé est montrée comme telle) et ses pièces : plaques, boulons et ancrages par taille et classe, les soudures par gorge avec leur longueur, son poids, la semelle où vont ses ancrages. Un paramètre changé là ne vaut que pour cet assemblage, et le bâtiment reconstruit le garde. **Modifier le bâtiment…** ouvre son bâtiment.

![Un assemblage dans les Propriétés](shots/steel-nodes-props.webp)

Les barres dessinées à la main reçoivent leurs assemblages par **Steel → Trouver les assemblages** : un pied sous chaque barre verticale, un assemblage en rive où une barre est ajustée sur un poteau, celui d'un potelet où un poteau est ajusté sous une barre, un faîtage où deux barres sont coupées d'onglet l'une avec l'autre, un éclissage où deux barres se suivent bout à bout ; pour les barres sélectionnées, ou tout le modèle si rien n'est sélectionné. **Steel → Pièces métalliques** crée la liste des plaques, boulons, ancrages et soudures des assemblages. Supprimer un assemblage rend à ses barres leurs extrémités ; supprimer une barre emporte ses assemblages ; **Supprimer le bâtiment** dans le configurateur enlève le bâtiment avec ses barres, assemblages et axes, en une seule étape.

## Acier secondaire

Un bâtiment vient avec son **acier secondaire** (Steel → Portal frame hall…, onglet **Acier secondaire**), fait avec lui et refait avec lui quand il change, chaque barre gardant son identité :

![L’onglet Acier secondaire](shots/steel-secondary-hall.webp)

- **Acier secondaire** : activé ou non pour tout le bâtiment.
- **Pannes** : leur **Profil** (Z, C et Σ formés à froid, un U ou un I léger), l’**Entraxe des pannes** le long du rampant, de combien elles vont **Au-delà des pignons**, et leurs **Longueurs** : **une par travée** (de portique à portique) ou **continue** (toute la longueur du bâtiment). Elles reposent sur le dessus des chevrons, l’âme perpendiculaire à la toiture, de 150 mm à l’intérieur de la face des poteaux jusqu’à 200 mm du faîtage, en autant de files que l’entraxe le demande.
- **Lisses de bardage** : leur **Profil** et l’**Entraxe des lisses**, de la **Première lisse à** jusque sous l’égout, une par travée sur la face extérieure des poteaux, l’âme horizontale et les bords tombés vers le bas ; aux pignons, de potelet à potelet (ou entre les poteaux d’un portique de pignon), aussi haut que le chevron au-dessus le permet. Une **Poutre sablière** prend le haut des longs pans (avec son **Profil**), les lisses un entraxe plus bas.
- **Contreventement** : les **Travées contreventées** (leurs numéros, depuis la première), le **Profil** des barres (ronds de Ø16 à Ø30, cornières ou tubes) et où : **En toiture** (une croix dans le plan de la toiture, sous la semelle supérieure des chevrons, en panneaux à peu près longs comme la travée) et **Dans les longs pans** (une croix dans le plan des parois, par les axes des poteaux, de la hauteur de la première lisse jusque sous le jarret).
- **Bracons** : d’une panne et d’une lisse sur deux jusqu’à la semelle inférieure du chevron et la semelle intérieure du poteau, de part et d’autre, hors des jarrets.

![L’acier secondaire en 3D](shots/steel-secondary-3d.webp)

Chaque panne et chaque lisse repose sur une **échantignolle** sur chaque chevron, poteau ou potelet qu’elle croise : une plaque soudée sur la face de la barre, contre l’âme de la panne (à l’extérieur de l’âme d’un C, du côté d’un Z opposé à sa semelle inférieure), boulonnée à travers l’âme, deux boulons par bout de panne (M12 jusqu’à 250 mm de hauteur, M16 au-delà). Un poteau d’angle plus étroit que les poteaux reçoit une échantignolle plus haute, jusqu’à la lisse. Chaque croix repose sur des **goussets** soudés à l’âme des chevrons ou des poteaux, une diagonale sur chaque face ; une cornière ou un tube y est boulonné par deux boulons, le bout fileté d’un rond prend son écrou. Un bracon est boulonné à ses deux bouts. Ce sont des assemblages comme ceux des portiques : sélectionnez-en un pour voir sa plaque et ses boulons dans les Propriétés, et les y changer. Le résumé du configurateur compte et pèse les pannes, les lisses, la poutre sablière, le contreventement et les bracons ; la nomenclature **Steel parts** donne leurs échantignolles, goussets et boulons.

![Une panne sur son échantignolle, un bracon dessous](shots/steel-secondary-cleat.webp)

![Le contreventement de toiture sur ses goussets](shots/steel-secondary-bracing.webp)

## Listes et dessins

**Steel → Steel members** fait la liste de l’acier du projet : les barres, et les poutres et poteaux d’un profilé acier, par bâtiment (ceux d’aucun bâtiment à la fin), un **Repère** pour les barres d’un même type identiques en profil, longueur et coupes d’extrémité (C poteaux, R chevrons, GP potelets de pignon, P pannes, SR lisses, EB poutres sablières, B contreventement, FB bracons, M autres barres, BM poutres), numérotés du profil le plus lourd et du plus long. Une ligne donne le **Profil**, la **Longueur**, la **Coupe au début** et la **Coupe à la fin** (de combien le plan de l’extrémité tourne par rapport à l’équerre, et un grugeage : les angles de la liste de débit), le **Nombre**, le poids au mètre, **Unitaire** et total, et la surface de **Peinture** ; un total sous chaque bâtiment et un général. **Steel parts** donne les plaques, boulons, ancrages et soudures des assemblages, avec leurs échantignolles et goussets.

![La nomenclature des barres en acier](shots/steel-members.webp)

**Steel → Anchor plan** dessine les pieds de poteaux du bâtiment pour les fondations : un plan du niveau du bâtiment au 1:50, coupé juste au-dessus des platines, chaque ancrage cerclé, sa distance à l’axe (± quand les ancrages sont symétriques par rapport à lui), chaque pied repéré par son type (**B1**, **B2**… : même platine, ancrages, classe et calage), les travées et la portée de la trame, et le tableau des types à côté du bâtiment : nombre, type, platine, ancrages (nombre, taille, forme), classe et calage. **Steel → Frame elevation** dessine un portique (le courant, ou celui de la barre sélectionnée) vu le long du bâtiment au 1:50, avec sa portée et ses hauteurs à l’égout et au faîtage. Les deux sont des vues du bâtiment (demandées à nouveau, la même s’ouvre) et se posent sur des planches comme les autres ; elles suivent le bâtiment quand il change.

![Un plan des ancrages](shots/steel-anchor-plan.webp)

![L’élévation d’un portique](shots/steel-frame-view.webp)

## IFC et quantités

Une barre s’exporte en **IfcMember** avec son **IfcMemberType**. Son corps est écrit comme les autres logiciels écrivent l’acier (Tekla, Advance Steel, Revit) : sa section **extrudée** (IfcExtrudedAreaSolid), **coupée** par les plans de ses extrémités (IfcBooleanClippingResult avec IfcHalfSpaceSolid), et chaque grugeage un **IfcOpeningElement** qui l’évide. Une poutre ou un poteau en acier avec extrémités s’écrit de même. Les quantités de chaque barre donnent sa longueur entre ses coupes, l’aire de sa section, son volume et son poids net (7850 kg/m³).

Un bâtiment s’exporte comme un **IfcBuildingSystem** (LOADBEARING) auquel ses barres sont affectées, ses données dans le jeu de propriétés `Lapin_SteelHall`, avec sa propre **IfcGrid**. Une traverse avec jarrets est un **IfcElementAssembly** de la traverse et de ses jarrets : un jarret découpé dans le profil est un **IfcMember**, un jarret en tôles soudées deux **IfcPlate**, chacun extrudé (IfcExtrudedAreaSolid). Les jarrets ne comptent pas dans les quantités de la traverse ; le résumé du configurateur les pèse. Les pannes s’exportent comme **IfcMember** PURLIN, le contreventement et les bracons comme BRACE, les lisses et la poutre sablière comme USERDEFINED (**Side rail**, **Eaves beam**), tous affectés au système du bâtiment avec leurs assemblages.

Un assemblage s'exporte comme **IfcElementAssembly** de ses plaques (**IfcPlate**), boulons (**IfcMechanicalFastener** BOLT), ancrages (ANCHORBOLT) et plaques d'ancrage (**IfcDiscreteAccessory** ANCHORPLATE), chacun avec sa taille et sa classe dans `Lapin_SteelPart` ; il réalise la liaison des barres qu'il assemble (**IfcRelConnectsWithRealizingElements** ; un pied, entre la semelle et son poteau). Les soudures restent des données, pas des corps.
