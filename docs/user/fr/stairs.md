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
