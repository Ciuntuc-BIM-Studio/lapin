---
title: Escaliers
order: 42
summary: Placez un escalier entre deux niveaux, réglez-le dans son type, et créez les quelques marches qui mènent à un perron ou à une porte.
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

## Marches d’entrée (un perron)

Les marches qui mènent à une porte d’entrée ont une hauteur propre : depuis le terrain, quelques marches sous le rez-de-chaussée, jusqu’à ±0.00. Lapin a pour elles un type d’escalier tout prêt, **Marches d’entrée** : 2 m de large, trois contremarches de 150 mm, en béton massif coulé sur le terrain, sans garde-corps.

![Des marches d’entrée devant un perron, en plan](shots/entrance-steps-plan.webp)

1. **Le perron.** Dessinez le palier comme une dalle : choisissez **Architecture → Dalle** (<kbd>SB</kbd>) et cliquez son contour hors du mur, devant la porte. Le dessus d’une dalle est au niveau de son étage (±0.00 au rez-de-chaussée) et son type donne son épaisseur. Pour changer l’épaisseur, créez un type de dalle pour les perrons dans **Architecture → Types de dalles**, par exemple 150 mm de béton.
2. **Les marches.** Choisissez **Architecture → Marches d’entrée**. Cliquez le milieu du bord du perron, puis cliquez du côté vers lequel sortent les marches. Les marches montent jusqu’au bord du perron et s’arrêtent à ±0.00. Elles commencent 450 mm plus bas : leur **Décalage de base** est de −450 mm.
3. **La hauteur.** Sélectionnez les marches. Dans **Hauteur à franchir**, tapez la différence réelle entre le terrain et le perron, par exemple 300. Le haut reste au perron et le pied remonte : le décalage de base devient −300 mm et les marches ont deux contremarches. **Largeur** les rend aussi larges que le perron, par exemple 2400.

![Les propriétés des marches d’entrée : hauteur à franchir, décalage de base, niveaux](shots/entrance-steps-properties.webp)

**Niveaux** indique où les marches commencent et finissent, par exemple −0.45 → ±0.00. Avec une seule contremarche (une hauteur de 150 mm), la marche est le bord du perron lui-même et il n’y a pas de giron à construire. Donnez aux marches deux contremarches ou plus pour avoir des girons devant le perron.

Le type est ajouté au projet la première fois que vous l’utilisez. Pour le modifier, ouvrez **Architecture → Types d’escaliers** et choisissez **Marches d’entrée** :

- **Forme → Une hauteur propre.** La hauteur du type ; chaque escalier peut encore la changer avec **Hauteur à franchir**.
- **Règles → Jeu de règles → marches d’entrée.** Contremarches de 120 à 170 mm et girons de 300 à 400 mm, une seule contremarche étant permise.
- **Construction → Construit en.** **Massif, coulé sur le terrain** remplit les marches jusqu’à leur base. **Paillasse sous les marches** les construit comme un escalier intérieur.

![Le type Marches d’entrée dans le configurateur d’escaliers](shots/stair-types.webp)

Pour qu’un autre type d’escalier monte une hauteur propre, cochez **Une hauteur propre** dans son onglet **Forme**. Pour changer un escalier placé en marches d’entrée, choisissez **Marches d’entrée** comme **Type d’escalier** : il descend de sorte que son haut soit au niveau de son étage.

![La maison avec son perron et ses marches d’entrée en 3D](shots/entrance-steps-3d.webp)

En IFC, les marches sont un IfcStair de type STRAIGHT_RUN_STAIR. Sa volée est un IfcStairFlight avec ses NumberOfRisers, NumberOfTreads, RiserHeight et TreadLength, et Pset_StairCommon porte les mêmes nombres. Le perron est un IfcSlab.
