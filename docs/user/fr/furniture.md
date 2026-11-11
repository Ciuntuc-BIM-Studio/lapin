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

## Meubles à caissons et mobilier moderne

**Intérieur → Meubles à caissons** place du mobilier fait de panneaux : armoires, bibliothèques, buffets, meubles TV, commodes, meubles de salle de bain avec vasque, bureaux, meubles d’entrée, lits et tables de chevet. Chacun se configure dans **Types de mobilier**. Choisissez-y **Meubles à caissons** et un **Modèle**, puis **Nouveau type**. Le modèle donne les dimensions, la base, le plateau et les caissons, et chaque valeur reste modifiable. Si vous choisissez un autre modèle plus tard, le type repart des valeurs de ce modèle et garde ses matériaux.

Un meuble à caissons est fait d’un ou de plusieurs **caissons** côte à côte, listés de la gauche, vus de face :

- **Caisson** en ajoute un à la fin, le bouton de division le coupe en deux moitiés, les flèches le déplacent et la corbeille le retire.
- Un caisson avec une **Largeur** la garde. Les caissons sans largeur se partagent ce que les autres laissent de la largeur du meuble. **Ajuster la largeur** rend le meuble aussi large que ses caissons quand tous ont une largeur. Les caissons qui ne tiennent pas sont laissés de côté, et l’aperçu le dit.
- Un caisson avec sa propre **Hauteur** est plus bas que le meuble, comme la partie basse d’un meuble d’entrée.
- Un **Espace genoux** est un caisson sans caisse, comme la place sous un bureau. Le plateau passe dessus, et un côté le soutient au bout du meuble.

Chaque caisson contient des **zones** empilées du bas vers le haut, avec une séparation entre deux. La liste les montre telles qu’elles sont, celle du haut d’abord. La flèche ajoute une zone au-dessus de celle du haut. Une zone a sa propre **Hauteur**, ou une **Part du reste**, et sa **Face** :

- **Étagères ouvertes** ;
- **Portes** : de 1 à 4, une seule porte avec **Charnières côté** gauche ou droit, **Vitrés** ou non ;
- **Tiroirs** : de 1 à 6, chacun avec son coffre derrière. **Hauteurs en proportion, depuis le haut** donne leurs hauteurs : 1:1:2 rend le tiroir du bas deux fois plus haut que les autres. Vide, ils sont tous pareils.
- **Abattant**, qui **se relève** ou **se rabat**.

Derrière des façades ouvertes, des portes ou un abattant, une zone peut avoir des **Étagères** (réglables sauf si vous décochez **Tablettes réglables**) et une **Tringle à vêtements**. Derrière des portes, elle peut aussi avoir des **Tiroirs intérieurs** en bas. Des tiroirs sous des portes sont deux zones dans un même caisson : les tiroirs en bas, avec leur hauteur, et les portes au-dessus.

Les sections sous le modèle donnent le reste :

- **Construction** : l’épaisseur des panneaux du caisson, du panneau arrière, des tablettes et des façades ; le dessus et le dessous **entre les côtés** ou **sur les côtés** ; le dos **encastré** (à 10 mm du bord arrière) ou **appliqué sur le dos**.
- **Base** : un **Socle**, avec sa hauteur et son retrait ; des **Pieds** (métal fin, bois fuselé ou luge), avec leur hauteur ; **Suspendu au mur**, avec la hauteur de son dessous ; ou **Aucun**. Sur un socle ou des pieds, la hauteur du meuble va jusqu’à son plateau. Suspendu au mur, la hauteur est celle du meuble, et **Suspendu à** dit où se trouve son dessous.
- **Plateau** : **Aucun** (les dessus des caissons), **En débord** (au-delà de la face et des bouts de son débord) ou un **Plan de travail** (au-delà de la face seulement), avec son épaisseur. **Découpe de vasque dans le plateau** découpe un emplacement pour la vasque d’un meuble de salle de bain et la dessine avec sa robinetterie.
- **Façades** : **Lisse**, **À cadre** (montants et traverses autour d’un panneau en retrait) ou **Rainuré** (rainures verticales taillées dans la façade). Les poignées sont **Aucun** (push-to-open), une **Poignée profil J** fraisée dans le chant des façades, une **Barre**, un **Bouton** ou une **Poignée de chant**. **Portes coulissantes sur toute la largeur** met ce nombre de portes coulissantes sur deux rails devant tous les caissons, comme sur une armoire.

Un lit fait avec le modèle **Lit** a un cadre, une tête de lit aussi haute que le type, un fond de lit, le matelas et des oreillers. Il a le **Dessus du matelas** et des tiroirs dessous, des deux côtés, à gauche ou à droite.

Les boutons de **Styles** appliquent un aspect moderne sur les valeurs du type en une seule étape :

- **Sans poignées, push-to-open** ;
- **Poignée profil J** ;
- **Pieds métalliques fins** ;
- **Flottant, suspendu au mur** ;
- **Façades rainurées** ;
- **Bicolore, bois et couleur** : un caisson et un plateau en chêne avec des façades vert sauge ;
- **Façades à cadre, boutons**.

Les matériaux ont leurs rôles : corps (le caisson), façades, plateau, dos, socle, pieds, poignées, verre, métal (la tringle, la robinetterie) et linge (le matelas d’un lit). Les matériaux avec lesquels Lapin démarre sont indicatifs : donnez les vôtres.

Chaque panneau est un solide de son matériau. En plan, le meuble montre son contour, la ligne de ses façades, les joints entre ses caissons et, au niveau de détail le plus fin, le débattement de ses portes en tirets. Un meuble suspendu au-dessus de 1,2 m est dessiné en surplomb. Les élévations et la 3D montrent les panneaux, les façades et les poignées.

L’aperçu liste la **Liste de débit** du type : chaque pièce (côtés, fond, dessus, dos, séparations, tablettes, socle, coffres de tiroirs, puis les façades à part) avec son caisson, sa longueur dans le sens du fil × largeur × épaisseur et le nombre. **Intérieur → Liste de débit** fait la liste de débit de tous les types de meubles à caissons du projet, multipliée par leurs meubles. Elle ne liste que des panneaux : coulisses, charnières, pieds et poignées sont de la quincaillerie.

![Le configurateur de meubles à caissons : un modèle, les styles, la construction, les caissons et leurs zones, à côté de l’aperçu et de la liste de débit du moteur](shots/casework-types.webp)

![Des meubles à caissons en 3D : un buffet sur pieds fuselés, un meuble TV rainuré suspendu au mur, une armoire et un bureau](shots/casework-3d.webp)

En IFC, un meuble à caissons est un IfcFurniture de son type, avec le type prédéfini de son modèle (SHELF pour une bibliothèque, DESK, BED, ou USERDEFINED avec Wardrobe, Sideboard, TV unit, Bathroom vanity…). Ses caissons voyagent avec le type, donc Lapin les relit. Les types armoire, buffet et commode créés avant restent tels qu’ils étaient.

## Mobilier à partir de modèles

**Intérieur → À partir d’un modèle** place du mobilier souple et de formes, fait à partir de modèles tout prêts : canapés, un canapé avec méridienne, éléments de canapé d’angle, un pouf, fauteuils, un fauteuil inclinable, chaises, une chaise de bureau, tabourets de bar, bancs, tables à manger, rondes et basses, une table d’appoint, lampadaires, lampes de table, suspensions et appliques, plantes en pot et tapis. Les modèles sont des maillages low-poly sous licence CC0 (Furniture Kit de Kenney), livrés avec Lapin, leur source et leur licence notées.

Un modèle est **étiré par zones** : chaque modèle a, sur chaque axe, une bande qui s’étire, tandis que ce qui la précède et la suit garde sa taille. Élargissez un canapé et ses accoudoirs et ses pieds restent tels quels pendant que l’assise grandit : un deux places devient un trois places. Montez une table et seuls ses pieds grandissent, son plateau garde son épaisseur. Les tables rondes, les abat-jour et les plantes sont mis à l’échelle en entier. Chaque modèle a un minimum et un maximum ; une taille au-delà est ramenée à la limite.

Dans **Types de mobilier**, choisissez **À partir d’un modèle** et un modèle, puis **Nouveau type**. Un type issu d’un modèle a :

- la **galerie** des modèles par groupe (canapés, fauteuils, chaises, tables, lampes, plantes, tapis) : cliquez sur un autre pour changer le modèle du type, qui prend sa taille et ses matériaux ;
- sa **largeur**, sa **profondeur** et sa **hauteur**, chacune dans la plage du modèle (survolez un champ pour la voir). L’aperçu à côté est celui du moteur : il montre l’étirement pendant que vous saisissez, le plan vu du dessus et la face avant aux couleurs du type ;
- **Suspendu à**, pour une suspension, une applique ou une lampe de table : la hauteur de son dessous ;
- l’**Aspect** (Nordique, Sombre, Chaud, Doux), qui règlent d’un coup les matériaux de toutes les parties, et une rangée de couleurs pour chaque partie : garnissage, accent (le bord d’un tapis, un second tissu), corps, haut, pieds, métal, verre, abat-jour, feuillage et pot. Les matériaux sont indicatifs : saisissez les vôtres dans les matériaux plus bas.

En plan, une pièce issue d’un modèle est son corps vu du dessus, dessiné par le moteur sans les lignes cachées ; un canapé montre aussi ses assises au 1:50 et plus fin. Une lampe suspendue à plus de 1,2 m est dessinée en surplomb. Les tables, chaises, tapis et lampes sur pied se posent là où vous cliquez ; les canapés, fauteuils et tables d’appoint s’adossent au mur le plus proche comme le reste du mobilier.

![La boîte Types de mobilier avec un canapé issu d’un modèle : la galerie, sa taille, les aspects et les couleurs, à côté de l’aperçu du moteur](shots/furniture-models-types.webp)

![Un séjour meublé à partir de modèles : un canapé étiré à 2,6 m, des fauteuils, une table basse sur un tapis, un lampadaire et des plantes](shots/furniture-models-3d.webp)

En IFC, une pièce issue d’un modèle est un IfcFurniture de son type avec le type prédéfini du modèle (SOFA, CHAIR, TABLE ou USERDEFINED avec Floor lamp, Plant, Rug…), et son corps est le maillage étiré, en B-rep facetté ou en jeu de faces selon l’export. Le modèle et la taille voyagent avec le type, de sorte que Lapin les relit. La nomenclature du mobilier les liste par groupe et par modèle.

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

## Ma bibliothèque de mobilier

Un type que vous avez bien configuré (une cuisine, une armoire, un bureau en panneaux, un canapé) peut être gardé pour tous les projets dans **Mon mobilier**, votre propre bibliothèque sur cet ordinateur.

**Enregistrer dans ma bibliothèque…** dans **Types de mobilier** garde le type sélectionné. Donnez-lui :

- un **nom** ;
- une **catégorie** : cuisine, séjour, chambre, salle de bains, bureau, rangement ou autre ;
- des **étiquettes**, séparées par des virgules (chêne, moderne…) ;
- un **code article** et un **prix indicatif**, si vous le souhaitez. Les deux sont à vous : Lapin ne fournit aucun prix.

La vignette est dessinée à partir de l’aperçu du type. Le type emporte tout ce dont il a besoin : sa configuration et ses matériaux, avec leurs couleurs, leurs hachures et leurs images.

![Enregistrer dans ma bibliothèque : le nom, la catégorie, les étiquettes, le code article et le prix à côté de la vignette](shots/furniture-save-library.webp)

**Intérieur → Mon mobilier…** (ou **Mon mobilier…** dans Types de mobilier) montre la bibliothèque sous forme de vignettes. Cherchez-y par nom, étiquette, article ou genre, et filtrez par catégorie et par étiquette. Pour chaque élément :

- **Insérer** met le type dans le projet ouvert et le choisit pour son outil. Un matériau que le projet a déjà sous ce nom est utilisé tel quel, et les autres sont créés. Si le projet a déjà un type identique, celui-ci est utilisé et aucune copie n’est faite. Chaque insertion est une étape d’annulation.
- **Poser** l’insère et lance son outil, de sorte que le clic suivant pose une pièce. Une cuisine se dessine le long d’un mur.
- **Renommer**, **Étiquettes**, **Dupliquer** et **Supprimer** ne changent que la bibliothèque. Les projets qui ont déjà le type le gardent.
- **Exporter** écrit l’élément en fichier `.lapinlib`. **Tout exporter…** écrit toute la bibliothèque, et **Importer…** en relit un. L’import saute un élément déjà présent dans la bibliothèque et inchangé. Un élément de même identifiant mais au contenu modifié est ajouté à côté de celui que vous avez, si bien que rien n’est écrasé.
- **Ajouter à un modèle…** met le type dans l’un de vos modèles de projet, de sorte que tout projet créé à partir de lui ait ce type.

![Mon mobilier : les vignettes de la bibliothèque, recherchées et filtrées, chacune prête à insérer ou à poser](shots/furniture-library.webp)

Sur le bureau, la bibliothèque est gardée dans votre dossier Lapin, un fichier par élément. Dans le navigateur, elle est gardée par le navigateur. Pour la passer de l’un à l’autre, ou sur un autre ordinateur, exportez-la et importez le fichier. Un modèle de projet garde les types de mobilier du projet à partir duquel il est enregistré. **Enregistrer comme modèle…** peut aussi ajouter tous les types de Mon mobilier (**Ajouter tous les types de Mon mobilier**).

## Nomenclatures et IFC

**Nomenclature du mobilier** liste chaque type avec sa sorte, sa taille, le nombre de meubles et les pièces où ils se trouvent, avec le total. **Mobilier par pièce** liste chaque pièce avec ce qui s’y trouve. Les deux sont dans le navigateur du projet, sous **Nomenclatures**, et s’impriment et s’exportent comme toute nomenclature. Un type de mobilier avec un article de prix est chiffré à la pièce dans le devis.

En IFC, un meuble est un **IfcFurniture**, avec le type prédéfini de sa sorte (SOFA, CHAIR, BED, TABLE, DESK, SHELF, ou USERDEFINED avec Wardrobe, Kitchen…). Son **IfcFurnitureType** est rattaché par IfcRelDefinesByType, et ses corps portent leurs matériaux. Lapin relit son propre mobilier comme des meubles natifs et modifiables. Le mobilier d’autres logiciels entre comme références.
