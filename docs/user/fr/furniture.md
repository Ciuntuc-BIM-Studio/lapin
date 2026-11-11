---
title: Mobilier
order: 47
summary: Canapés, canapés d’angle, fauteuils, chaises, lits, armoires, commodes, bibliothèques, buffets, tables, bureaux et cuisines en six dispositions, à partir de types paramétriques, posés contre les murs d’un clic.
---

## Placer du mobilier

L’onglet **Intérieur** a, après les finitions, un outil pour chaque sorte de meuble : **Canapé** (<kbd>SO</kbd>), **Canapé d’angle**, **Fauteuil** (<kbd>AC</kbd>), **Chaise**, **Lit** (<kbd>BD</kbd>), **Table de chevet**, **Armoire** (<kbd>WD</kbd>), **Commode**, **Bibliothèque**, **Buffet**, **Table** (<kbd>TB</kbd>), **Table basse** et **Bureau**. Choisissez-en un et cliquez dans le plan.

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

### Dispositions de la cuisine

Un type de cuisine a une **Layout** (disposition) : **One wall** (la ligne droite), **Galley** (deux lignes parallèles avec une allée entre elles), **L-shaped**, **U-shaped**, **Island** (une ligne sur le mur du fond et un îlot séparé avec son allée) ou **Peninsula** (un L avec une ligne qui sort de sa branche, le plan de travail en débord pour des tabourets). En choisir une fait partir le type des dimensions de la disposition, et chacune reste modifiable à côté de l’aperçu : la longueur des branches depuis le mur du fond, l’allée entre les lignes, la longueur de la ligne opposée, la longueur, la profondeur et la place de l’îlot, la longueur et le débord de la presqu’île et, pour un L ou une presqu’île, l’angle à gauche ou à droite.

![Les six dispositions en plan : un mur, parallèle, en L, en U, avec îlot et avec presqu’île](shots/kitchen-layouts.webp)

**Sink on** et **Hob on** les placent sur la ligne principale ou sur l’autre partie (la branche, la ligne opposée, l’îlot, la presqu’île), **Sink at** et **Hob at** disent où le long de celle-ci, et la **Tall unit (fridge)** se tient à une extrémité libre. Les meubles d’angle sont aveugles ; les meubles hauts ne sont que sur les lignes contre un mur, pas sur un îlot ni sur une presqu’île.

![Un type de cuisine : en L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) dessine n’importe quelle disposition avec deux points le long du mur contre lequel se tient la ligne principale : la ligne donne sa longueur, son dos sur la face du mur, et un L, une presqu’île, un U ou une cuisine parallèle se glisse dans l’angle ou entre les murs près desquels on la dessine. **Flip** donne à un L ou à une presqu’île l’angle de l’autre côté. La nomenclature du mobilier nomme la disposition et donne la profondeur de l’emprise. En IFC, c’est toujours un IfcFurniture d’un type Kitchen défini par l’utilisateur.

### Meubles et appareils de la cuisine

Un type de cuisine peut énumérer ses propres **meubles** sous ses paramètres, dans **Types de mobilier**, sur deux lignes :

- la **Ligne basse** : les meubles de la ligne principale depuis la gauche, vus de face (après un angle aveugle) ;
- la **Ligne haute** : les meubles suspendus au-dessus.

**Ajouter** met un meuble de la sorte choisie au bout d’une ligne. **Cuisine type** commence la ligne basse par un évier, un lave-vaisselle, un meuble à tiroirs et une plaque sur un four. Les flèches déplacent un meuble vers la gauche ou la droite, et la corbeille le retire.

Chaque meuble a une **largeur**. Un meuble accepte toute largeur de 100 à 800 mm : choisissez 150, 200, 300, 400, 450, 500, 600 ou 800, ou tapez-en une autre. Un appareil prend les dimensions dans lesquelles il existe. Les meubles de la ligne basse sont :

- **Meuble bas**, dont la **Face** est un tiroir sur une porte, des portes (une ou deux, avec le côté où une porte seule a ses charnières, **Charnières côté**), des tiroirs (1 à 4), des tiroirs à casseroles (un tiroir plat sur deux profonds) ou des étagères ouvertes ;
- **Meuble sous évier** : un évier à une cuve, à 1½ cuve, double (à partir de 800) ou d’angle, avec un robinet ordinaire, un robinet extractible ou sans robinet. Un évier d’angle entre dans l’angle aveugle quand il est à côté.
- **Plaque sur four**, de 600, 800 ou 900 de large, à induction ou à gaz, avec ou sans le four dessous ;
- **Cuisinière indépendante**, de 500, 600 ou 900 de large, à induction ou à gaz. Le plan de travail s’arrête de chaque côté.
- **Lave-vaisselle** intégré, de 450 ou 600 de large, derrière une façade de toute la hauteur assortie aux autres ;
- **Réfrigérateur** : sous plan, colonne intégrée (une porte de réfrigérateur sur une porte de congélateur) ou indépendant ;
- **Colonne four**, avec des tiroirs sous le four et une porte au-dessus ;
- **Colonne**, avec des portes ou des étagères ouvertes ;
- **Joue de finition** et **Espace**.

Une colonne, un réfrigérateur ou une colonne four a sa propre **Hauteur**. Sur la ligne haute, un **Meuble haut** a des portes, des portes vitrées ou des étagères ouvertes, sa propre **Hauteur** et sa propre hauteur **Depuis le sol**. Laissez l’une ou l’autre vide pour prendre les valeurs de la ligne. Une **Hotte** est une hotte cheminée (à 650 au-dessus du plan de travail, son conduit montant jusqu’au haut des meubles hauts) ou une hotte intégrée à un meuble haut. Un **Espace** laisse le mur nu. Les meubles hauts contournent une colonne.

Sans ligne haute énumérée, chaque meuble bas reçoit un meuble haut de sa largeur, et une plaque ou une cuisinière reçoit une hotte cheminée. Sur un îlot ou une presqu’île, **Hotte au-dessus de la plaque là** suspend une hotte au-dessus de la plaque qui s’y trouve.

Ce que les meubles laissent de la ligne se répartit à parts égales comme avant, et moins de 100 mm devient une joue de finition. Les meubles qui ne tiennent pas dans la ligne sont laissés de côté, et l’aperçu et l’élément le disent. **Ajuster aux meubles**, dans les propriétés d’une cuisine, donne à sa longueur ce que ses meubles demandent : la leur, plus un angle aveugle et la colonne du bout. Un type sans meubles propres se construit comme avant.

En plan, chaque meuble a son propre symbole :

- le débattement des portes basses, en tirets et seulement au niveau de détail le plus fin ;
- les cuves de l’évier et son robinet ;
- les zones ou les brûleurs de la plaque ;
- les colonnes, barrées d’une croix ;
- **DW** sur le lave-vaisselle, **F** sur un réfrigérateur et **OV** sur une colonne four ;
- les meubles hauts et les hottes, au-dessus.

Les élévations et la 3D montrent chaque façade, chaque poignée et chaque appareil.

Les matériaux ont deux rôles de plus : **Socle** et **Poignées**, en plus des façades, du caisson, du plan de travail (plateau), du métal et du verre. Un type de cuisine fait avant prend pour le socle le matériau de son caisson, et pour les poignées celui de son métal.

![Les meubles d’un type de cuisine : la ligne basse et la ligne haute, avec l’aperçu du moteur](shots/kitchen-units-types.webp)

![Une cuisine de meubles et d’appareils variés, en 3D](shots/kitchen-units-3d.webp)

**Intérieur → Meubles de cuisine** fait une nomenclature des meubles de chaque cuisine, depuis la gauche, la ligne basse puis la ligne haute, avec leur largeur, leur hauteur et leur façade ou appareil. En IFC, la cuisine reste un seul IfcFurniture de son type, et les meubles voyagent avec le type, si bien que Lapin les relit.

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

**Intérieur → Types de mobilier** liste les types du projet par groupe (assises, lits, rangement, tables, cuisine), chacun avec le nombre de meubles qui l’utilisent. Choisissez une sorte et **Nouveau type**, ou **Dupliquer** un type pour en modifier une copie. Un type contient :

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
  - une cuisine : largeur des meubles bas, socle, plan de travail, place de l’évier et de la plaque, meubles hauts, colonne, et ses propres meubles (ci-dessus) ;
- ses **matériaux** par rôle : caisson, garnissage, plateau, façades, linge de lit, métal, verre, et pour une cuisine le socle et les poignées. Tapez n’importe quel nom de matériau ; un matériau que le projet n’a pas est créé, et **Gérer → Matériaux** règle sa couleur et ses hachures.

L’aperçu à côté des champs montre le symbole en plan et la face du type à mesure que vous le modifiez. Chaque meuble d’un type suit toute modification.

![La boîte de dialogue Types de mobilier avec un canapé et sa méridienne](shots/furniture-types.webp)

## Nomenclatures et IFC

**Nomenclature du mobilier** liste chaque type avec sa sorte, sa taille, le nombre de meubles et les pièces où ils se trouvent, avec le total. **Mobilier par pièce** liste chaque pièce avec ce qui s’y trouve. Les deux sont dans le navigateur du projet, sous **Nomenclatures**, et s’impriment et s’exportent comme toute nomenclature. Un type de mobilier avec un article de prix est chiffré à la pièce dans le devis.

En IFC, un meuble est un **IfcFurniture**, avec le type prédéfini de sa sorte (SOFA, CHAIR, BED, TABLE, DESK, SHELF, ou USERDEFINED avec Wardrobe, Kitchen…). Son **IfcFurnitureType** est rattaché par IfcRelDefinesByType, et ses corps portent leurs matériaux. Lapin relit son propre mobilier comme des meubles natifs et modifiables. Le mobilier d’autres logiciels entre comme références.
