---
title: Mobilier
order: 47
summary: Canapés, canapés d’angle, fauteuils, chaises, lits, armoires, commodes, bibliothèques, buffets, tables, bureaux et cuisines linéaires, à partir de types paramétriques, posés contre les murs d’un clic.
---

## Placer du mobilier

L’onglet **Mobilier** a un outil pour chaque sorte de meuble : **Canapé** (<kbd>SO</kbd>), **Canapé d’angle**, **Fauteuil** (<kbd>AC</kbd>), **Chaise**, **Lit** (<kbd>BD</kbd>), **Table de chevet**, **Armoire** (<kbd>WD</kbd>), **Commode**, **Bibliothèque**, **Buffet**, **Table** (<kbd>TB</kbd>), **Table basse** et **Bureau**. Choisissez-en un et cliquez dans le plan.

Cliquez près d’un mur et le meuble s’y adosse. Son dos se pose sur la face du mur (sur l’enduit, pas sur le noyau), son avant regarde la pièce, et il glisse le long du mur jusqu’à ne plus entrer dans le mur d’en face à un angle. Cliquez plus loin de tout mur et le meuble se place là où vous avez cliqué.

Tant qu’un outil est actif, la barre de la ligne de commande affiche :

- **le type** que pose l’outil, choisi parmi les types de cette sorte dans le projet (s’il n’y en a pas encore, le premier est créé quand vous placez le meuble) ;
- **l’orientation**, 0°, 90°, 180° ou 270°, pour un meuble isolé ;
- **Contre les murs**, que vous pouvez désactiver pour placer un meuble exactement là où vous cliquez, même près d’un mur.

Le panneau des propriétés affiche ce même type avant le clic, avec sa taille et **Modifier les types…**.

![Un séjour, une cuisine et une chambre meublés, en plan](shots/furniture-plan.webp)

Les plans dessinent le mobilier par son symbole, selon la convention de dessin, et non coupé :

- un canapé avec son dossier, ses accoudoirs et ses coussins ;
- un lit avec sa tête de lit, ses oreillers et sa couette rabattue ;
- une armoire avec sa tringle et ses portes ;
- une cuisine avec son évier et sa plaque, ses meubles hauts en tirets.

Les élévations et les coupes dessinent les corps, avec les hachures de leurs matériaux. En 3D, les meubles sont des corps low-poly créés par Lapin.

![La maison meublée en 3D](shots/furniture-3d.webp)

## Chaises autour d’une table, et la cuisine

**Chaises autour d’une table :** cliquez une table ou un bureau. Les chaises se placent le long des grands côtés, environ une tous les 600 mm, et une à chaque bout quand il est assez large ; une table ronde les reçoit en cercle. Chaque chaise fait face à la table. Un bureau ne reçoit des chaises que sur sa face avant. Les chaises sont du type choisi pour l’outil **Chaise**.

**Cuisine** (<kbd>KT</kbd>) se dessine par deux points le long d’un mur :

- la longueur de la ligne est celle de la cuisine, et son dos se pose sur la face du mur ;
- les meubles bas sont répartis également, à environ 600 mm ;
- elle a un plan de travail, un évier et une plaque (leur place le long de la cuisine se règle sur le type), des meubles hauts au-dessus et une colonne pour le réfrigérateur à un bout.

La **Longueur** d’une cuisine reste modifiable dans ses propriétés ; les meubles sont de nouveau répartis.

## Les propriétés d’un meuble

Sélectionnez un meuble pour voir :

- **Type**, sa sorte et sa taille ;
- **Rotation** ;
- **Au-dessus du sol**, pour un meuble posé sur une estrade ;
- **Longueur**, pour une cuisine.

**Retourner** donne au meuble son autre main, sur place : la méridienne de l’autre côté, le réfrigérateur à l’autre bout, l’angle du canapé d’angle de l’autre côté. Son avant reste où il était. **Modifier → Symétrie** fait de même.

Faites glisser la poignée au milieu d’un meuble pour le déplacer ; faites glisser la poignée devant lui pour tourner son avant vers le curseur.

![Les propriétés d’un canapé](shots/furniture-properties.webp)

## Types de mobilier

**Mobilier → Types de mobilier** liste les types du projet par groupe (assises, lits, rangement, tables, cuisine), chacun avec le nombre de meubles qui l’utilisent. Choisissez une sorte et **Nouveau type**, ou **Dupliquer** un type pour en modifier une copie. Un type contient :

- son **nom**, sa **largeur**, sa **profondeur** et sa **hauteur** (celle du dossier pour un canapé, de la tête de lit pour un lit, du plan de travail pour une cuisine) ;
- les paramètres propres à sa sorte :
  - canapés et fauteuils : places, accoudoirs, largeur des accoudoirs, hauteur d’assise, une méridienne à gauche ou à droite et sa longueur, pieds ;
  - un canapé d’angle : le côté de son angle et la longueur de ce côté ;
  - un lit : le dessus du matelas, la tête de lit, le pied de lit, les oreillers, une couette rabattue ;
  - une armoire : les portes, battantes ou coulissantes, des tiroirs en bas, le socle ;
  - une commode : rangées et colonnes de tiroirs ;
  - une bibliothèque : tablettes, et portes en bas jusqu’à une hauteur ;
  - un buffet : portes et tiroirs ;
  - une table : rectangulaire, ronde ou ovale, sur quatre pieds ou un pied central ;
  - un bureau : un caisson à tiroirs à gauche ou à droite ;
  - une cuisine : largeur des meubles bas, socle, plan de travail, place de l’évier et de la plaque, meubles hauts, colonne ;
- ses **matériaux** par rôle : caisson, garnissage, plateau, façades, linge de lit, métal, verre. Tapez n’importe quel nom de matériau ; un matériau que le projet n’a pas est créé, et **Gérer → Matériaux** règle sa couleur et ses hachures.

L’aperçu à côté des champs montre le symbole en plan et la face du type à mesure que vous le modifiez. Chaque meuble d’un type suit toute modification.

![La boîte de dialogue Types de mobilier avec un canapé et sa méridienne](shots/furniture-types.webp)

## Nomenclatures et IFC

**Nomenclature du mobilier** liste chaque type avec sa sorte, sa taille, le nombre de meubles et les pièces où ils se trouvent, avec le total. **Mobilier par pièce** liste chaque pièce avec ce qui s’y trouve. Les deux sont dans le navigateur du projet, sous **Nomenclatures**, et s’impriment et s’exportent comme toute nomenclature. Un type de mobilier avec un article de prix est chiffré à la pièce dans le devis.

En IFC, un meuble est un **IfcFurniture**, avec le type prédéfini de sa sorte (SOFA, CHAIR, BED, TABLE, DESK, SHELF, ou USERDEFINED avec Wardrobe, Kitchen…). Son **IfcFurnitureType** est rattaché par IfcRelDefinesByType, et ses corps portent leurs matériaux. Lapin relit son propre mobilier comme des meubles natifs et modifiables. Le mobilier d’autres logiciels entre comme références.
