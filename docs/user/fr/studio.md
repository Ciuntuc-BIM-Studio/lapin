---
title: Solides Studio et plans de travail
order: 85
summary: Transformez des formes dessinées en solides, donnez une épaisseur à une forme avec Anneau, Caisson et Bande de mur, et dessinez sur des plans de référence n’importe où dans le modèle.
---

## Des solides à partir d’une esquisse

L’onglet **Studio** transforme en solides les formes que vous dessinez. Dessinez la forme avec **Dessin** dans un plan, une coupe, une élévation ou un plan de référence, puis choisissez-la avec un outil Studio :

- **Extrusion** (<kbd>EXT</kbd>) pousse une forme fermée sur une profondeur. Les formes fermées à l’intérieur deviennent des trous.
- **Balayage** (<kbd>SW</kbd>) déplace un profil fermé le long d’un chemin, **Révolution** (<kbd>REV</kbd>) le fait tourner autour d’un axe et **Lissage** (<kbd>LO</kbd>) relie des profils dessinés dans des vues différentes.

![L’onglet Studio](shots/ribbon-studio.webp)

Chaque solide reste lié à son esquisse. Modifiez la forme et le solide la suit, avec le même GlobalId. Sélectionnez un solide pour changer sa classe IFC, son type prédéfini et son type dans le panneau des propriétés. **Modifier l’esquisse** ouvre la vue sur laquelle se trouve l’esquisse.

## Anneau, Caisson et Bande de mur

Ces trois outils donnent à une forme dessinée une épaisseur dans son propre plan, puis une hauteur à partir de ce plan :

- **Anneau** (<kbd>RING</kbd>) prend un cercle, un arc ou un contour fermé et en fait un anneau ou un tube. Son épaisseur est à l’**intérieur**, à l’**extérieur** ou **centré** sur la forme.
- **Caisson** (<kbd>BOX</kbd>) prend un rectangle, une polyligne fermée ou un cercle et fait les murs tout autour. Un fond (**Bas**) et un dessus (**Haut**) sont facultatifs, chacun avec sa propre épaisseur.
- **Bande de mur** (<kbd>TWALL</kbd>) prend une ligne, une polyligne ou un arc et en fait une bande de mur. Son épaisseur est **à gauche**, **à droite** ou **centré**, vue en suivant la ligne dans le sens où vous l’avez dessinée. Ses angles sont assemblés en onglet.

Lancez l’outil et cliquez sur la forme. La ligne de commande propose les options sous forme de lettres : <kbd>I</kbd>, <kbd>O</kbd> et <kbd>C</kbd> (ou <kbd>L</kbd>, <kbd>R</kbd> et <kbd>C</kbd>) pour l’emplacement de l’épaisseur, et pour un caisson <kbd>B</kbd> et <kbd>T</kbd> pour activer ou désactiver le fond et le dessus. Tapez ensuite l’épaisseur et la hauteur, en mètres, ou appuyez sur <kbd>Enter</kbd> pour garder la valeur affichée.

**Dans un plan**, les outils créent par défaut des murs natifs, du type de mur choisi dans **Architecture**. Une forme fermée devient une boucle de murs joints à ses angles, une polyligne une chaîne de murs, et un arc ou un cercle des murs courbes. L’emplacement de l’épaisseur décide de l’endroit où se placent les murs : toute l’épaisseur du type de mur va du côté choisi. Le fond et le dessus d’un caisson deviennent des planchers. Tapez <kbd>S</kbd> avant de choisir la forme pour créer plutôt un solide Studio.

**Dans une coupe, une élévation ou un plan de référence**, les outils créent un solide Studio de la classe IFC IfcWall ; le fond et le dessus d’un caisson sont des dalles séparées (IfcSlab). La hauteur suit la normale du plan, vers vous ; une hauteur négative s’éloigne de vous, comme avec **Extrusion**. Pour donner un type à un tel mur, sélectionnez-le et choisissez-en ou créez-en un sous **Type** dans le panneau des propriétés.

![Un anneau de murs courbes natifs autour d’un cercle, un caisson avec son fond et son dessus, et une bande de mur, dans le plan du rez-de-chaussée](shots/studio-thick-plan.webp)

Tout reste paramétrique. Sélectionnez le solide et modifiez son **Épaisseur**, sa **Hauteur** et **L’épaisseur est** dans le panneau des propriétés. Pour un caisson, cochez **Bas** ou **Haut** et réglez leurs épaisseurs : Lapin ajoute ou retire les dalles. Un caisson avec un fond ou un dessus est regroupé avec eux dans un assemblage (IfcElementAssembly). Faites glisser les poignées de la forme, ou modifiez-la dans sa vue, et le solide est reconstruit.

![Les propriétés d’un caisson : son épaisseur, sa hauteur, son fond et son dessus](shots/studio-box-properties.webp)

## Plans de référence

Un plan de référence est un plan placé n’importe où dans le modèle, avec sa propre vue. Vous dessinez dessus avec Dessin, comme dans une coupe, et Studio y crée des solides. Créez-en un depuis l’onglet **Studio** :

- **Plan depuis la vue** (<kbd>REFPLANE</kbd>) est parallèle au plan, à la coupe, à l’élévation ou au plan de référence devant vous. Tapez à quelle distance il se trouve vers vous (une valeur négative l’éloigne).
- **Plan sur une face** : dans la vue 3D, cliquez sur une face de n’importe quel élément. Le plan se pose sur la face, tourné vers l’extérieur de celle-ci.
- **Plan par 3 points** : dans la vue 3D, cliquez sur trois points du modèle. Le plan passe par eux.

Le nouveau plan s’ouvre aussitôt. Il figure sous **Plans de référence** dans l’arborescence du projet. Son dessin est une coupe sur le plan, regardant contre le côté vers lequel il est tourné.

Dans la vue 3D, chaque plan de référence est dessiné comme une feuille translucide portant son nom, aussi grande que le modèle sur lui (ou selon **Largeur dessinée (3D)** et **Hauteur dessinée (3D)**, 2 × 2 m au moins), mise en évidence sous le curseur. Cliquez dessus pour sélectionner le plan, double-cliquez pour ouvrir sa vue. Le plan sélectionné, glissez la flèche en son milieu pour le déplacer le long de sa normale : le décalage se lit à côté de la flèche, et ce qui est dessiné sur le plan suit. **Vue → Plans de référence** masque et affiche les feuilles ; elles n’entrent jamais dans les exports, les rendus ni les images. Dans les plans, les coupes et les élévations, un plan qui coupe la vue est dessiné comme une ligne en tirets avec son nom, sur toute l’étendue de la vue : un clic sélectionne le plan, un double clic l’ouvre. Un plan parallèle à la vue n’y est pas dessiné (il n’en coupe rien). Ces lignes ne sont que pour l’écran : les planches ne les impriment jamais.

![Un plan de référence sur la façade sud, avec un anneau dessiné dessus](shots/studio-plane.webp)

Sélectionnez le plan dans l’arborescence pour le déplacer. **Origine x**, **y** et **z** le placent. **Orienté** est la direction en plan vers laquelle il est tourné, **Inclinaison** de combien il penche en arrière (à 90° il est à plat, tourné vers le haut) et **Rotation dans le plan** fait tourner son dessin autour de sa normale. **Déplacer selon sa normale…** et **Tourner autour de la verticale…** font la même chose en une seule étape. Ce qui est dessiné sur le plan, et chaque solide créé à partir de lui, se déplace avec lui.

![Les propriétés d’un plan de référence](shots/studio-plane-properties.webp)

Les coupes et les élévations sont aussi des plans. Un solide esquissé dans une coupe repose sur sa ligne de coupe, et suit la ligne quand vous la déplacez. Dans une coupe développée, il repose sur le tronçon sur lequel il est dessiné. Une élévation prend son propre plan la première fois que vous y créez un solide : à l’avant du modèle, avec son x tel qu’il est dessiné maintenant. Modifiez sa **Profondeur du plan d’esquisse** dans les propriétés de l’élévation pour déplacer le plan, et ses solides, le long de la vue.

![La maison avec ses solides Studio en 3D](shots/studio-3d.webp)

En IFC, un anneau, les murs d’un caisson et une bande de mur sont des éléments IfcWall, le fond et le dessus d’un caisson des éléments IfcSlab, et un caisson avec eux un IfcElementAssembly avec IfcRelAggregates ; leurs types s’écrivent IfcWallType et IfcSlabType. Chaque export 3D (IFC, Fragments, glTF, USD, COLLADA) écrit un solide Studio là où il se trouve dans le modèle, quel que soit le plan sur lequel il a été esquissé, avec ses trous ; un assemblage est un nœud de groupe au-dessus de ses parties. Rouvert, l’IFC propre à Lapin ramène les solides comme références, à leur place, avec leur classe, leur type et leur assemblage, mais ils ne sont plus modifiables (leurs esquisses ne sont pas dans le fichier). L’historique de chaque solide est conservé dans le jeu de propriétés propre à Lapin, Lapin_Intent. Les solides Studio ne prennent que des types simples, sans couches : c’est la modélisation libre pour laquelle ils existent ; les murs à couches sont les murs natifs de Lapin.
