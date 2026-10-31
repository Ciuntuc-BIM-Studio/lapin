---
title: Ferraillage 2D
order: 52
summary: Le ferraillage dessiné sur les coupes et les plans, des coupes transversales là où vous cliquez, et la nomenclature d’acier.
---

L’onglet **Ferraillage 2D** dessine le ferraillage comme on fait une nomenclature : des barres comme des formes avec leurs dimensions, des cadres répartis le long d’une poutre, des repères dans des bulles et une nomenclature qui les additionne. Il fonctionne dans toute vue : une coupe, un plan, une façade.

![L’onglet Ferraillage 2D](shots/ribbon-rebar.webp)

## Une coupe là où vous travaillez

Le ferraillage a besoin de la coupe transversale à côté de la longitudinale. **Coupe ici** fait les deux : choisissez une poutre, un poteau ou un mur dans la vue où vous êtes, et là où vous cliquez une coupe est faite *en travers*, perpendiculaire à son axe, et dessinée à côté de votre clic, dans la même vue, avec un demi-mètre de ce qui l’entoure (la dalle, le poteau). Un poteau est coupé horizontalement, à la hauteur cliquée.

La vue insérée suit l’élément : déplacez la poutre et la coupe se déplace avec elle. Déplacez l’insertion par sa poignée ; ses propriétés lui donnent un facteur d’échelle et un cadre. **Coupe transversale** fait la même coupe comme vue à part, et **Vue insérée** place toute vue existante dans celle où vous êtes, en vraie grandeur et à l’échelle de la vue hôte, pour que les barres dessinées dessus s’ajustent au béton.

![La coupe longitudinale d’une poutre avec sa coupe transversale insérée et son ferraillage](shots/rebar-section.webp)

## Barres

**Barre droite**, **Barre en L**, **Barre en U**, **Cadre**, **Épingle** et **Barre polyligne** se placent d’un clic (la polyligne par ses sommets). Chacune est une forme à paramètres : tirez une poignée et la dimension qu’elle représente suit ; tapez-la dans le panneau des propriétés, en millimètres. Les barres sont dessinées à leur épaisseur, dans une couleur par diamètre (Réglages du ferraillage), et pliées au rayon demandé par SR EN 1992-1-1 : mandrin de 4Ø jusqu’à Ø16, 7Ø au-delà.

Les crochets se règlent à chaque extrémité : 90°, 135° ou 180°, leur longueur droite selon la pratique (12Ø, 10Ø, 5Ø, jamais moins de 50 mm), de l’autre côté si demandé. Un cadre naît avec deux crochets à 135° tournés vers l’intérieur.

**Barre en coupe** est une barre vue en bout : un point de son diamètre, avec la longueur qu’elle a le long de la barre. **Même barre en coupe** place la barre sélectionnée comme point ailleurs, sans la compter deux fois. Un **Réseau** dans les propriétés répète une barre tous les tant de millimètres, comme les barres d’un lit dans une coupe transversale.

La bulle du repère montre le numéro du repère et ce qu’est la barre : `4Ø12 L=2450`. Tirez la bulle là où elle se lit le mieux. Les longueurs sont les nominales, mesurées sur l’axe de la barre entre ses angles, crochets compris ; la longueur développée autour des pliages est indiquée dans le panneau des propriétés.

## Cadres sur le type de poutre ou de poteau

Les cadres ne se dessinent pas un à un : ils appartiennent au **type**. Dans Structure → Types de structure, un type rectangulaire de poutre ou de poteau reçoit **Cadres sur ce type** : leur diamètre, l’enrobage et leur répartition — un pas sur toute la longueur, ou **trois zones**, chacune avec sa part de la longueur (un tiers par défaut) et son pas, plus serré aux extrémités. Chaque élément du type les montre alors dans toute coupe selon la position de la vue par rapport à l’axe : le rectangle à l’intérieur de l’enrobage en coupe transversale, une ligne par cadre en coupe longitudinale, des lignes sur la poutre dans un plan de structure ; avec l’étiquette `41Ø8/150` et la bulle du repère. Ils sont comptés dans la nomenclature sous le repère du type. Les **barres longitudinales** se règlent sur le type de la même façon : les lits supérieur et inférieur et les barres de chaque côté d’une poutre (nombre × diamètre), les barres de chaque face d’un poteau avec les angles partagés, et de combien elles dépassent chaque extrémité. Elles sont posées dans l’espace à l’intérieur des cadres, si bien que toute coupe les montre là où elles sont : des points de leur diamètre en travers, des lignes le long, chaque lit avec son étiquette `3Ø16 L=6000` et son repère dans la nomenclature. Les corps dans la vue 3D ne sont pas dans cette édition ; les barres hors règle se dessinent toujours à la main, comme ci-dessous.

## Cadres le long d’une poutre

**Cadres le long** prend les deux extrémités d’un tracé ; les cadres sont répartis de sorte qu’il y en ait un à chaque extrémité, au pas que vous fixez (150 mm par défaut). Les **Zones** changent le pas en chemin : **Tiers** met à chaque extrémité un pas deux fois plus serré, comme le demande une poutre sismique ; l’étiquette dit `25Ø8/100-200`. Montrez-les tous, seulement les extrémités et le milieu avec une ligne tiretée entre eux, ou un seul.

## La nomenclature

**Nomenclature d’acier** fait une nomenclature de chaque barre par repère : diamètre, forme, nombre, longueur d’une barre, longueur totale et masse, avec les totaux par diamètre. Un repère dont les barres diffèrent en diamètre ou en longueur est noté. C’est une nomenclature comme les autres : sur les feuilles, en CSV et XLSX.

**Réglages du ferraillage** garde les choix du projet : une couleur par diamètre, les tailles du texte et de la bulle sur papier, l’acier, l’enrobage et les longueurs des segments écrites le long des barres.
