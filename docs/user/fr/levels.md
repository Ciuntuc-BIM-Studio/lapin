---
title: Niveaux et plans
order: 60
summary: Ajoutez des niveaux, réglez leurs hauteurs et voyez un autre niveau sous le plan sur lequel vous dessinez.
---

<!-- video levels -->

## Niveaux

L’arborescence du projet liste les niveaux en haut, le plus haut en premier. Le **+** à côté de **Niveaux** ajoute un niveau au-dessus du plus haut.

Sélectionnez un niveau pour voir son **niveau de base** et son **niveau supérieur**. L’un comme l’autre peut se trouver au-dessus ou au-dessous de ±0.00. Sa hauteur est la distance entre les deux. Quand vous déplacez la base, le haut reste où il est. Un nouveau projet commence avec Foundations, Ground floor et First floor. Un niveau fait au plus 100 m de haut et reste à ±10 km de ±0.00 : une valeur saisie dans la mauvaise unité est refusée. Un escalier qui monterait de plus de 50 m jusqu’au niveau au-dessus s’arrête là et dit pourquoi.

![Les propriétés d’un niveau : niveau de base, niveau supérieur et hauteur](shots/level-properties.webp)

Chaque niveau a son propre plan sous **Vues en plan**. Double-cliquez sur un plan pour dessiner sur ce niveau. Le niveau sur lequel vous dessinez est indiqué dans la case en haut à gauche de la vue.

Dans les coupes et les façades, chaque niveau est repéré à droite par son nom et sa cote. **Gérer → Styles d’objets → Lignes cachées** règle aussi le **repère de niveau** : triangle vide, triangle à moitié gauche noire (comme le dessine la norme roumaine), triangle noir ou trait, et sa taille sur papier.

Les altitudes s’écrivent dans les unités du projet : en mètres (+3.00) dans un projet métrique, comme les plans les écrivent, et en pieds et pouces (+9'-10 1/8") dans un projet impérial. Le navigateur, le cadre en haut à gauche de la vue, les repères de niveau des coupes et des façades et les cotes de niveau les suivent.

## Voir le niveau du dessous

Pour voir un autre niveau pendant que vous dessinez, choisissez-le dans **Calque de fond** dans la barre du plan. Le plan de ce niveau est dessiné en dessous, estompé et gris. Vous pouvez vous y accrocher, mais vous ne pouvez pas le sélectionner.

![Le premier étage dessiné par-dessus le rez-de-chaussée, affiché en calque de fond](shots/underlay.webp)

Le calque de fond ne sert qu’à dessiner ; il n’est pas imprimé. Pour imprimer les éléments d’autres niveaux sur un plan, ouvrez les propriétés de la vue en plan et choisissez-les dans **Niveaux affichés**. Vous pouvez afficher certains niveaux ou tous, en demi-teinte si vous le souhaitez.

## Copier vers d’autres niveaux

Pour répéter des murs, des dalles ou un plan entier sur d’autres niveaux, sélectionnez-les (murs avec leurs portes et fenêtres, dalles, pièces, un groupe…) et choisissez **Modifier → Copier vers des niveaux**. Vous pouvez aussi taper `COPYSTOREY` dans la ligne de commande, ou faire un clic droit sur la sélection dans un plan ou dans la vue 3D. Cochez les niveaux de destination. **Tous au-dessus** et **Tous en dessous** cochent tous les niveaux au-dessus ou en dessous de celui de la sélection, et un Maj-clic coche tous les niveaux entre celui cliqué et le dernier cliqué. Le niveau sur lequel se trouve la sélection ne peut pas être coché.

![Copier vers des niveaux : deux niveaux cochés, et les options](shots/copy-to-storeys.webp)

- **Relatif** (par défaut) : chaque copie se trouve à la même hauteur au-dessus de son nouveau niveau que l’original au-dessus du sien. Un mur dessiné au rez-de-chaussée se retrouve au premier étage.
- **Absolu** : chaque copie reste là où est l’original dans l’espace, sur le nouveau niveau. Son décalage de base compense la différence.
- **Les éléments hébergés aussi** : les portes et fenêtres suivent leurs murs, et les ouvertures ce qu’elles percent. Les étiquettes et cotes des éléments copiés passent du plan de leur niveau au plan du nouveau niveau. Une pièce emporte ses finitions et un escalier l’espace sous lui. Décochez l’option pour copier les murs sans leurs portes et fenêtres.
- **Refaire les relations entre les copies** : les murs joints en angle ou en T sont joints de la même façon sur chaque nouveau niveau. Un mur attaché à une dalle ou à une toiture copiée avec lui est attaché à sa copie. Un groupe devient un groupe des copies, et un assemblage est copié entier. Un escalier monte jusqu’au niveau au-dessus du nouveau et y perce sa propre trémie.
- **Ignorer les doublons** : un niveau qui a déjà le même élément au même endroit n’en reçoit pas un second. Le message en bas indique combien ont été ignorés.

Les copies utilisent les mêmes types que les originaux et reçoivent de nouveaux GlobalId IFC. Toutes les copies forment une seule étape d’annulation et restent sélectionnées ensuite, pour les déplacer ou les modifier ensemble. Dans une session partagée, seul l’invité annule ses propres copies.

![Les murs et la dalle du rez-de-chaussée copiés sur les deux niveaux au-dessus, en 3D](shots/copy-to-storeys-3d.webp)

Pour déplacer la sélection vers un autre niveau, choisissez **Modifier → Déplacer vers un niveau**. Les éléments gardent leurs hauteurs au-dessus du nouveau niveau, ou restent là où ils sont dans l’espace.
