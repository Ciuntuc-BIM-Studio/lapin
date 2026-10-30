---
title: Studio
order: 85
summary: Modelez des solides libres, esquissés sur n’importe quelle face ou plan, puis poussez, tirez, arrondissez, évidez, divisez et répétez-les en 3D.
---

## L’onglet Studio

L’onglet **Studio** crée des solides qu’aucun autre onglet ne construit : un banc, un auvent, une main courante, une fontaine, un socle sur une façade. Chaque solide part d’une esquisse ou d’une primitive. Vous le modifiez ensuite en 3D par étapes, et chaque étape reste modifiable.

![L’onglet Studio](shots/ribbon-studio.webp)

Un solide Studio a une classe IFC, comme tout élément. Choisissez-la dans ses propriétés : une barre, une plaque, un revêtement, un garde-corps, une dalle ou une autre. Ses quantités et son export suivent cette classe.

## Où esquisser

Le contour d’un solide se dessine avec les outils de **Dessin**, en polylignes fermées, rectangles et cercles. Des lignes et des arcs qui se rejoignent bout à bout forment aussi un contour fermé. Une forme fermée à l’intérieur du contour est un trou.

Vous pouvez esquisser sur quatre sortes de vues :

- **Un plan** dessine à la cote de son niveau.
- **Une coupe** dessine sur sa ligne de coupe, à la verticale.
- **Une élévation** ou **un plan de référence** : voir *Plans de référence* plus bas.

## Créer un solide

- **Extrusion** (EXTRUDE) pousse le contour tout droit. Tapez S pour les deux côtés de l’esquisse, T pour une dépouille qui incline les côtés vers l’intérieur, et Cut pour qu’il coupe les éléments sélectionnés avant. Pour le mener jusqu’à la face d’un autre élément, utilisez **Jusqu’à → une face…** dans ses propriétés, ou **Extruder jusqu’à la face**.
- **Balayage** (SWEEP) fait suivre un chemin à un profil. TWist tord la section en chemin, SCale l’agrandit, RIse la soulève. Un cercle qui monte devient une hélice et fait TUrns tours.
- **Lissage** (LOFT) relie des profils dessinés sur des vues différentes. Tapez S pour passer en douceur par eux. Une ligne sélectionnée avec le rôle *rail* le courbe, comme guide.
- **Révolution** (REVOLVE) fait tourner un profil autour d’une ligne d’axe.
- **Les primitives** posent d’un clic une boîte, un cylindre, une sphère, un cône, un tore, un coin ou une pyramide. Leurs dimensions et leur position se changent dans leurs propriétés.

![Une main courante balayée le long d’un cercle qui monte de 3 m en deux tours](shots/studio-helix.webp)

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
## Modifier un solide en 3D

Ces outils travaillent sur les faces et les arêtes du corps tel qu’il est construit maintenant. La face sous la souris s’allume. Quand un outil a besoin d’une valeur, une petite case s’ouvre près du curseur. Tapez la valeur et appuyez sur <kbd>Entrée</kbd>, ou sur <kbd>Échap</kbd> pour abandonner.

- **Pousser/Tirer** (PU) déplace une face vers l’extérieur d’une distance, ou vers l’intérieur quand la distance est négative. Sur une extrusion, son extrémité change la profondeur et son côté déplace l’arête de l’esquisse dessous. Sur un mur ou un autre élément, tirer ajoute un solide dans le matériau de la face, et pousser découpe l’élément.
- **Arrondir l’arête** (FE) arrondit l’arête la plus proche du clic, et **Chanfreiner l’arête** (CE) la chanfreine. Choisissez *tout autour de la face* pour traiter toutes les arêtes de la face d’un coup.
- **Coque** évide les solides sélectionnés, avec des parois aussi épaisses que vous le tapez. Elle peut rester fermée, ou être laissée ouverte à son extrémité, à son esquisse ou aux deux.
- **Diviser** coupe un solide en deux par le plan de la face choisie, aussi loin dans la face que vous le tapez. Les deux parties gardent l’esquisse. **Trancher** ne garde qu’un côté.
- **Réseau** répète le solide en ligne, et **Réseau polaire** le répète autour d’une verticale.
- **Rotation 3D** fait tourner le solide autour d’une arête choisie. **Symétrie 3D** le reflète dans le plan d’une face. **Déplacer 3D** le déplace en x, y et z.

![Pousser/Tirer : la face du dessus allumée et la distance tapée près du curseur](shots/studio-push-pull.webp)

![L’arête arrondie : l’arrondi suit le bloc quand sa profondeur change](shots/studio-fillet.webp)

## Les étapes

Les propriétés d’un solide Studio listent ses étapes dans l’ordre. Chaque étape peut être désactivée, avancée ou reculée, modifiée ou retirée. Modifiez l’esquisse, et chaque étape est refaite sur le nouveau corps. Une étape qui ne retrouve plus sa face ou son arête le dit sous **Problèmes**.

![Un cylindre évidé en tuyau et répété trois fois : ses deux étapes dans ses propriétés](shots/studio-steps.webp)

Les propriétés d’une polyligne listent ses côtés. Tapez la longueur d’un côté, et son extrémité glisse le long de celui-ci. Les côtés suivants suivent, jusqu’au prochain côté qui court dans la même direction, de sorte qu’un rectangle reste un rectangle. Un solide dessiné dessus suit aussi.

## Les maillages d’autres logiciels

**Fichier → Importer un maillage (OBJ, STL, glTF)…** fait entrer un maillage sous forme de références que vous voyez et auxquelles vous vous accrochez. Un fichier OBJ ou STL ne dit ni son unité ni quel axe est vers le haut, donc Lapin le demande. Sélectionnez une référence et cliquez sur **Faire un solide Studio** pour la rendre modifiable. Elle accepte alors pousser/tirer, arrondis, booléens et les autres étapes. Le même outil fonctionne sur les murs, les dalles et les éléments des fichiers IFC.

## Booléens

**Soustraire**, **Union** et **Intersection** combinent n’importe quels éléments, le premier sélectionné étant la cible. Les outils restent modifiables, et le résultat les suit. **Afficher les opérandes** montre les outils cachés d’une union pour les modifier. **Supprimer l’opération** sépare à nouveau les éléments.
