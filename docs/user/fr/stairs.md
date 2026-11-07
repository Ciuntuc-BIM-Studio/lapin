---
title: Escaliers et rampes
order: 42
summary: Placez un escalier ou une rampe entre les étages ou jusqu’à une hauteur quelconque, réglez-le dans son type et faites les quelques marches jusqu’à un perron ou une porte.
---

## Escaliers

Choisissez **Architecture → Escalier** (<kbd>ST</kbd>). Cliquez le pied de l’escalier (le milieu de sa première contremarche), puis un point dans la direction où il monte. Pour un escalier en L ou en U, un troisième clic à gauche ou à droite de la volée indique de quel côté il tourne. L’escalier monte de son niveau au niveau supérieur. Lapin calcule le nombre de contremarches, la hauteur de marche et le giron à partir de la hauteur et des règles du type.

Sélectionnez l’escalier pour le modifier dans le panneau des propriétés :

- **Type d’escalier.** La forme, les règles, la construction et le garde-corps viennent du type, défini dans **Architecture → Types d’escaliers**.
- **Largeur**, **Changement de direction**, **Décalage de base** et **Décalage supérieur.**
- **Hauteur à franchir.** Laissez-la vide et l’escalier monte jusqu’au niveau supérieur. Une valeur donne à l’escalier une hauteur propre, quels que soient les niveaux. Utilisez-la pour quelques marches entre deux niveaux d’un même étage.
- **Fixe.** Nombre de contremarches, hauteur de marche, giron et longueur du palier, quand vous voulez les fixer. Laissez-les vides pour laisser les règles décider.
- **Vérifications.** Chaque nombre comparé aux règles du type, en vert ou en rouge.

Un escalier demande à la dalle du dessus une trémie au-dessus de lui. Décochez **Trémie dans la dalle supérieure** si la dalle doit rester entière.

## Formes d’escalier et types tout faits

Dans **Architecture → Types d’escaliers**, l’onglet **Forme** propose : **Droit**, **Droit avec palier**, **En L** et **En U** (chacun avec palier ou avec **marches balancées** ; un U sans jour est un escalier à retour), **Hélicoïdal** (autour d’un noyau, avec son **Rayon du noyau**), **Courbe** (avec son **Rayon intérieur**) et **Impériale (à deux volées)** : une volée centrale jusqu’à un palier, puis deux volées de retour de part et d’autre. Pour un escalier hélicoïdal ou courbe, **Tourne de (°)** règle jusqu’où il tourne ; vide, les contremarches et le giron décident, et indiqué, le giron en découle. Chaque escalier peut aussi le changer dans ses propriétés.

Un **en U** qui tourne par des **marches balancées** règle chaque angle : **Marches balancées au premier angle**, **Marches balancées au second angle** et **Marches droites entre les angles**, les marches du petit côté entre eux. Avec des marches entre eux, ou un nombre différent à chaque angle, chaque angle tourne seul et le jour entre les volées est aussi large que ces marches : balancées, puis une ou deux marches, puis balancées. L’**Escalier en U balancé** de la bibliothèque en a deux à chaque angle et une marche droite entre eux. Sélectionné, un escalier balancé change les mêmes nombres dans ses propriétés (**Balancées, angle 1**, **Balancées, angle 2**, **Marches entre** ; vide : ceux du type). Le plan dessine chaque contremarche, les marches balancées et la ligne de foulée autour des deux angles ; la 3D, les quantités et l’IFC suivent, un IfcStairFlight pour chaque volée droite et les balancées comme une volée WINDER.

![Un escalier en U avec deux marches balancées à chaque angle et une marche droite entre eux](shots/stair-winder-u.webp)

![Le U balancé en plan : balancées, la marche entre elles, balancées, la ligne de foulée autour des deux angles](shots/stair-winder-u-plan.webp)

![Les propriétés d’un escalier balancé : les balancées de chaque angle et les marches entre eux](shots/stair-winder-u-properties.webp)

La bibliothèque ajoute au projet des types tout faits : escalier à retour, balancé, hélicoïdal, courbe, en console et impérial.

Dans l’onglet **Construction**, **Construit en** choisit une paillasse, des marches pleines sur le sol, des **Marches en console (flottantes)** (chaque marche une plaque scellée dans le mur) ou des **Marches sur limons**.

Le **Garde-corps** a son **Modèle** : **Poteaux et main courante**, **Barreaux** (au plus à l’**Espacement des barreaux**), **Panneaux de verre**, **Câbles** ou un **Garde-corps plein** ; une **Main courante** carrée ou ronde, sa hauteur, le **Retrait du bord**, de combien elle **Se prolonge au-delà des extrémités**, et sa couleur. Dans le panneau des propriétés, **Garde-corps** le met d’autres côtés que le type, pour un seul escalier.

![Le type de l’escalier hélicoïdal, ses marches numérotées](shots/stair-spiral.webp)

### Marches numérotées

Sous **En plan** dans l’onglet **Forme** : **Numéroter les marches** écrit le numéro de chaque marche dans un cercle, depuis la première contremarche ; **Étiquette** écrit les contremarches, la hauteur et le giron, et la largeur près du départ ; **« UP » au pied de la flèche** marque où il commence. Les plans et les planches les montrent.

![Marches numérotées en plan, un escalier impérial et un escalier en console](shots/stair-numbers-plan.webp)

![Un escalier hélicoïdal, un impérial et un en console en 3D](shots/stair-shapes-3d.webp)

## Marches d’entrée (un perron)

Les marches qui mènent à une porte d’entrée ont une hauteur propre : depuis le terrain, quelques marches sous le rez-de-chaussée, jusqu’à ±0.00. Lapin a pour elles un type d’escalier tout prêt, **Marches d’entrée** : 2 m de large, trois contremarches de 150 mm, en béton massif coulé sur le terrain, sans garde-corps.

![Des marches d’entrée devant un perron, en plan](shots/entrance-steps-plan.webp)

1. **Le perron.** Dessinez le palier comme une dalle : choisissez **Architecture → Dalle** (<kbd>SB</kbd>) et cliquez son contour hors du mur, devant la porte. Le dessus d’une dalle est au niveau de son étage (±0.00 au rez-de-chaussée) et son type donne son épaisseur. Pour changer l’épaisseur, créez un type de dalle pour les perrons dans **Architecture → Types de dalles**, par exemple 150 mm de béton.
2. **Les marches.** Choisissez **Architecture → Marches d’entrée**. Cliquez le milieu du bord du perron, puis cliquez du côté vers lequel sortent les marches. Les marches montent jusqu’au bord du perron et s’arrêtent à ±0.00. Elles commencent 450 mm plus bas : leur **Décalage de base** est de −450 mm.
3. **La hauteur.** Sélectionnez les marches. Dans **Hauteur à franchir**, tapez la différence réelle entre le terrain et le perron, par exemple 300. Le haut reste au perron et le pied remonte : le décalage de base devient −300 mm et les marches ont deux contremarches. **Largeur** les rend aussi larges que le perron, par exemple 2400.

![Les propriétés des marches d’entrée : hauteur à franchir, décalage de base, niveaux](shots/entrance-steps-properties.webp)

**Niveaux** indique où les marches commencent et finissent, par exemple −0.45 → ±0.00. Avec une seule contremarche (une hauteur de 150 mm), la marche est le bord du perron lui-même et il n’y a pas de giron à construire. Donnez aux marches deux contremarches ou plus pour avoir des girons devant le perron.

Le type est ajouté au projet la première fois que vous l’utilisez. Pour le modifier, ouvrez **Architecture → Types d’escaliers** et choisissez **Marches d’entrée** :

- **Forme → Monte → Une hauteur propre.** La hauteur du type, franchie **Jusqu’au niveau de son étage, depuis le bas (une entrée)** ; chaque escalier peut encore la changer avec **Hauteur à franchir**.
- **Règles → Jeu de règles → marches d’entrée.** Contremarches de 120 à 170 mm et girons de 300 à 400 mm, une seule contremarche étant permise.
- **Construction → Construit en.** **Massif, coulé sur le terrain** remplit les marches jusqu’à leur base. **Paillasse sous les marches** les construit comme un escalier intérieur.

![Le type Marches d’entrée dans le configurateur d’escaliers](shots/stair-types.webp)

Pour qu’un autre type d’escalier monte une hauteur propre, choisissez **Monte → Jusqu’à → Une hauteur propre** dans son onglet **Forme** (voir la section suivante). Pour changer un escalier placé en marches d’entrée, choisissez **Marches d’entrée** comme **Type d’escalier** : il descend de sorte que son haut soit au niveau de son étage.

![La maison avec son perron et ses marches d’entrée en 3D](shots/entrance-steps-3d.webp)

En IFC, les marches sont un IfcStair de type STRAIGHT_RUN_STAIR. Sa volée est un IfcStairFlight avec ses NumberOfRisers, NumberOfTreads, RiserHeight et TreadLength, et Pset_StairCommon porte les mêmes nombres. Le perron est un IfcSlab.

## Un escalier jusqu’à une hauteur quelconque

Un escalier ne va pas forcément d’un étage au suivant. Dans l’onglet **Forme** du type, **Monte → Jusqu’à** choisit **L’étage au-dessus** ou **Une hauteur propre**, avec sa **Hauteur à franchir**, et **Commence** dit d’où il part :

- **Jusqu’au niveau de son étage, depuis le bas (une entrée).** Il finit au niveau de l’étage et commence plus bas, comme les marches d’entrée.
- **Depuis le niveau de son étage vers le haut (une terrasse, une plateforme).** Il part du niveau de l’étage et monte la hauteur : 1,20 m jusqu’à une terrasse ou un quai de chargement, par exemple.

Chaque escalier placé peut monter une autre hauteur : saisissez-la dans **Hauteur à franchir** dans ses propriétés (vide : celle du type). Quand il monte depuis le niveau, une nouvelle hauteur garde le pied en place ; quand il vient d’en bas, elle garde le haut.

## L’espace sous l’escalier

L’espace sous une volée peut être aménagé : un placard de rangement, un bureau placard, un coin lecture, une entrée avec banc et patères, une cabane pour tout-petits ou un lit pour chien. Sélectionnez un escalier à volées droites et cliquez sur **Espace sous l’escalier…** en bas de ses propriétés (ou **Architecture → Espace sous l’escalier**).

![Le configurateur de l’espace sous l’escalier : le coin lecture sous la volée](shots/under-stair-dialog.webp)

À gauche, les styles tout faits : **Placard de rangement**, **Bureau placard**, **Coin lecture**, **Entrée et vestiaire**, **Cabane pour tout-petits** et **Niche pour chien**, et au-dessus les styles du projet. Choisissez-en un et modifiez-le au milieu :

- **Placement.** **Sous la volée** (pour un escalier en L ou en U), le **Côté ouvert** (par défaut à l’opposé du tournant), où il **Commence à** et **Finit à** le long de la volée (vide : là où la sous-face est à 300 mm du sol, jusqu’à la dernière contremarche) et sa **Profondeur** (vide : aussi large que l’escalier).
- **La façade.** La **Partie basse** fermée par des **Portes de placard** (chacune de la **Largeur de porte**, avec des **Étagères derrière les portes**), un **Panneau fermé** ou laissée ouverte ; une **Bibliothèque au bout haut**.
- **Élément principal.** Un **Bureau avec une étagère** (et une lampe), un **Banc avec coussins** (tiroirs en façade), un **Banc d’entrée et patères** (casiers à chaussures dessous), une **Porte de cabane de jeu** (et une **Fenêtre à côté de la porte**) ou un **Lit pour chien** derrière une ouverture encadrée, chacun avec ses dimensions.
- **Matériaux.** L’**Épaisseur des panneaux** et les matériaux du **Caisson**, des **Façades** et du **Tissu**, parmi ceux du projet.

À droite, l’aperçu montre l’espace vu du côté ouvert sous la sous-face de la volée et vu de dessus, sa longueur, sa profondeur et ses hauteurs, et ses pièces. Chaque pièce est coupée par la pente ; une pièce sans place (un bureau là où l’escalier est trop bas) est omise et l’aperçu le dit. **Créer** ou **Appliquer** est une seule étape d’annulation. **Enregistrer comme style…** garde ce qui remplit l’espace comme style du projet ; **Mettre à jour le style** l’enregistre à nouveau, et tous les espaces du style suivent.

![L’espace sous l’escalier dans les propriétés](shots/under-stair-properties.webp)

L’espace est un élément à part entière, porté par son escalier. Quand l’escalier change (largeur, rotation, marches, type), l’espace est reconstruit dessous, et il est supprimé avec lui. Ses champs de placement sont dans les propriétés, et **Configurer…** rouvre le configurateur.

![Les six styles, sous six escaliers](shots/under-stair-3d.webp)

Les plans, coupes, élévations et feuilles le dessinent comme tout autre élément : coupé là où le plan le coupe, vu dessous.

![Le coin lecture en plan, les autres à côté](shots/under-stair-plan.webp)

![Une coupe le long de la volée à travers le coin lecture](shots/under-stair-section.webp)

**Architecture → Nomenclature des espaces sous escalier** liste les pièces de chaque espace, avec leur nombre, matériau et volume, et les totaux par matériau. En IFC, l’espace est un IfcElementAssembly ("Under-stair space") d’IfcFurniture (étagères, bureau, banc, coussins, lit), d’IfcDoor, d’IfcPlate, d’IfcDiscreteAccessory (poignées, patères) et d’IfcLightFixture, relié à son IfcStair.

## Rampes

Choisissez **Architecture → Rampe** (<kbd>RU</kbd>). Cliquez le milieu de l’extrémité basse de la rampe, puis un point dans la direction où elle monte ; pour une rampe en L ou en lacet, un troisième clic à gauche ou à droite de la volée indique de quel côté elle tourne. La première fois, le projet reçoit de la bibliothèque **Ramp 6 %** : 1,20 m de large à 6 %, en volées de 9 m au plus avec des paliers de 1,50 m entre elles, une dalle en béton armé sous un revêtement, des mains courantes des deux côtés. Elle monte jusqu’à l’étage au-dessus, sauf si son type ou sa **Hauteur à franchir** dit autre chose.

**Architecture → Rampe d’entrée** se place comme des marches d’entrée : cliquez le milieu du bord du perron, puis le côté où sort la rampe. Elle monte de 450 mm du terrain à ±0,00, massive sur le terrain, et **Hauteur à franchir** la change comme pour les marches.

Les types de rampe se font dans le même configurateur, **Architecture → Types d’escaliers**, avec **Nouveau type de rampe**, ou depuis la bibliothèque : **Switchback ramp** (en lacet) et **Entrance ramp** (d’entrée) :

- **Forme.** **Droit**, **Droit, avec paliers**, **En L** ou **En lacet (aller-retour)** avec son **Écart entre volées** ; la **Largeur**, la **Pente** en pour cent, la **Volée la plus longue** (plus longue, la rampe est divisée en volées égales avec des paliers entre elles) et la **Longueur du palier**. La longueur en plan est la hauteur divisée par la pente : 3 m à 6 % font 50 m. **Monte** fonctionne comme pour les escaliers.
- **Règles.** **Pente maximale** (8 % par défaut), **Largeur minimale** et **Échappée**. Les vérifications montrent en rouge une pente plus forte que ne le permettent les règles, une volée plus longue que la plus longue et un palier plus court que 1,50 m ou que la largeur de la rampe.
- **Construction.** **Une dalle inclinée en béton armé** de son **Épaisseur de la dalle**, ou **Massif, coulé sur le terrain** ; le revêtement et le garde-corps comme pour les escaliers.

![La rampe en lacet dans le configurateur](shots/ramp-types.webp)

Sélectionnez une rampe pour voir son type, **Largeur**, **Changement de direction**, **Décalage de base** et **Hauteur à franchir**, une **Pente** et une **Longueur du palier** propres (vides : celles du type), les longueurs de ses volées et ses vérifications. Les plans dessinent ses volées et ses paliers, la flèche de montée et la pente en pour cent le long de chaque volée. Une rampe qui monte à l’étage au-dessus demande une trémie dans son plancher, comme un escalier.

![La rampe en lacet et la rampe d’entrée en plan, la pente le long de chaque volée](shots/ramps-plan.webp)

![Les rampes en 3D](shots/ramps-3d.webp)

En IFC, une rampe est un IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP ou HALF_TURN_RAMP) typé par un IfcRampType, fait d’IfcRampFlight, de paliers (IfcSlab LANDING) et d’IfcRailing.

## Garde-corps

**Architecture → Railing** (<kbd>RL</kbd>) dessine un garde-corps à part le long du bord d’un balcon, d’une terrasse ou d’une galerie : cliquez ses points, Entrée termine. Il prend les modèles des garde-corps d’escalier (poteaux et main courante, barreaux, verre, câbles ou panneau plein), sa hauteur (1 m par défaut), le décalage de la base, le profil et les tailles de la main courante et l’entraxe des poteaux dans ses propriétés ; ses points se déplacent par leurs poignées (Ctrl/⌘-clic au milieu d’un tronçon en ajoute un). En IFC c’est un IfcRailing (GUARDRAIL).

## Balcons

**Architecture → Balcony** (<kbd>BY</kbd>) accroche un balcon à un mur : choisissez son style dans la barre de la ligne de commande et cliquez hors de la face du mur, là où il va ; il se place de ce côté du mur, au niveau du sol de l’étage. Un **balconnet** (Juliet) cliqué près d’une porte ou d’une fenêtre se centre dessus, 200 mm plus large. Là où aucun mur n’est proche, le balcon se place où vous cliquez. **Balcony by outline** dessine n’importe quelle forme : cliquez ses angles, le premier côté est contre le bâtiment. **Gallery edge** dessine le bord d’une mezzanine au-dessus d’un double volume avec deux points (le vide à gauche, la galerie à droite).

Les styles prédéfinis couvrent les genres demandés :

- **Juliet balcony** : un garde-corps à balustres devant une porte-fenêtre, sur une petite tablette de pierre portée par deux consoles.
- **Cantilevered slab, glass** : une dalle béton en porte-à-faux depuis le plancher par un rupteur thermique, carrelée en pente, une goutte d’eau sous le bord, du verre sur trois côtés.
- **Stacked, on posts** : une dalle sur poteaux acier à l’avant, posés sur le balcon du dessous (ou le terrain) ; répétée niveau sur niveau.
- **On steel brackets**, **Hung from tie rods** (tirants des angles avant jusqu’au mur), **Rounded corners**, **Curved front** avec une allège pleine.
- **Loggia** : en retrait dans la façade. Elle découpe son enfoncement dans le mur (aussi large que la loggia, aussi haut que sa **Recess height**), construit ses murs latéraux et une dalle de plafond derrière la face, et dresse son propre **Back wall** (du type du mur, aussi haut) à la profondeur de la loggia, là où va la porte. Le garde-corps est au nu de la façade.
- **Corner balcony** : le long du mur et autour de l’angle du bâtiment, en retour sur le mur latéral ; cliqué près de l’extrémité d’un mur, il l’enveloppe.
- **Mezzanine gallery edge** : la dalle de la galerie derrière son bord, un bandeau sur le bord et un garde-corps à barreaux.

![Des balcons à l’étage : un balconnet à la porte, un sur poteaux, un d’angle, une loggia, un courbe, un arrondi et un suspendu ; le balcon de l’étage au-dessus, en pointillé](shots/balconies-plan.webp)

Le plan dessine la dalle, le revêtement et le garde-corps d’un balcon comme il les coupe ou les voit, et les balcons de l’étage au-dessus en pointillé. Coupes et élévations dessinent sa dalle, son garde-corps, ses poteaux et ses consoles.

### Le configurateur

**Architecture → Balcony configurator** (ou la roue dentée près de **Style** dans les propriétés du balcon) fonctionne comme celui des toitures : les styles à gauche (ceux du projet, puis les prédéfinis), les champs du balcon en onglets à droite, au-dessus de son plan ou d’une coupe en son milieu, tels que le brouillon les construirait :

- **Shape** : **Kind**, **Outline** (rectangle, angles arrondis avec **Corner radius**, façade courbe avec **Front bulge** ou le polygone dessiné), **Width** et **Depth**, le retour du balcon d’angle, la dalle et **Fascia height** de la galerie.
- **Slab and finish** : **Slab thickness** et matériau, **Edge** (droit, avec **drip** — goutte d’eau —, avec **nosing** du revêtement ou **tapered** jusqu’à **Edge thickness**), épaisseur et matériau du revêtement et sa **Fall (%)** vers l’avant, la sous-face et **Thermal break** (80 mm entre la dalle et le plancher intérieur ; la nomenclature et l’IFC l’indiquent).
- **Railing** : les modèles de garde-corps (poteaux et main courante, balustres, verre, câbles, panneau plein) ou une **parapet** de son épaisseur et de son matériau, ou aucun ; hauteur, à l’avant seul ou aussi sur les côtés, retrait du bord, main courante, poteaux et couleur.
- **Supports** : **Carried by** rien (porte-à-faux), **posts** (ronds ou carrés, taille et espacement), **brackets** (profondeur, hauteur et espacement) ou **hangers** (hauteur de fixation des tirants au-dessus de la dalle) et leur matériau.
- **Loggia** : hauteur de l’enfoncement, épaisseur des murs latéraux, dalle au-delà de la face, dalle de plafond et mur du fond.

![Le configurateur de balcon : les styles, la forme et le plan](shots/balcony-configurator.webp)

![Ses appuis, au-dessus d’une coupe du balcon](shots/balcony-configurator-section.webp)

**Apply** modifie le balcon en une étape. **Save as style…** le garde comme style du projet (un type de balcon), que proposent la barre de l’outil et les propriétés ; **Update style** l’enregistre à nouveau dans son style, et chaque balcon du style le reprend ; près du nom d’un style, on le renomme ou on le supprime (ses balcons restent tels quels). Un style est un type : ses articles de coût (Exécution → Map articles) métrent ses balcons.

![Les propriétés d’un balcon : son style et les champs de son genre](shots/balcony-properties.webp)

Dans les propriétés, un balcon ne montre que les champs que son genre utilise. Ses poignées le font glisser le long du mur, le milieu de l’avant règle la profondeur (pour une loggia, celle de l’enfoncement), les milieux des côtés la largeur ; un balcon dessiné se tire par ses angles. L’enfoncement et le mur du fond d’une loggia la suivent et partent avec elle.

**Stack on storeys above** (dans le configurateur, les propriétés ou **Architecture → Stack on storeys above**) copie les balcons sélectionnés sur chaque étage au-dessus du leur en une étape, avec Copy to storeys : la copie s’accroche au mur de son étage placé comme son mur, une loggia empilée découpe le mur de cet étage, les poteaux se posent sur le balcon du dessous.

![La façade sud en 3D : le balconnet, les balcons empilés sur poteaux, le balcon d’angle et celui suspendu à des tirants](shots/balconies-3d.webp)

![Depuis le nord : la loggia découpée dans le mur, le balcon courbe avec son allège et le balcon arrondi](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** liste chaque balcon avec son étage, son style, son genre, sa largeur, sa profondeur, sa surface, la longueur et le modèle du garde-corps, ce qui le porte et son rupteur thermique, avec les totaux. En IFC, un balcon est un IfcElementAssembly (USERDEFINED, « Balcony ») d’un IfcElementAssemblyType, fait de ses parties : la dalle un IfcSlab (FLOOR), revêtement et sous-face des IfcCovering (FLOORING, CEILING), le garde-corps un IfcRailing (GUARDRAIL) ou l’allège un IfcWall (PARAPET), les poteaux des IfcColumn, les consoles des IfcDiscreteAccessory (BRACKET), les tirants des IfcMember, le rupteur thermique un IfcBuildingElementPart ; l’enfoncement d’une loggia est un IfcOpeningElement dans son mur. Lapin relit ses balcons comme des balcons.
