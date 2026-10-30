---
title: Installations de chantier
order: 55
summary: Échafaudages de façade et coffrages des dalles et des poutres, disposés à partir du modèle par leurs configurateurs.
---

L’onglet **Installations de chantier** modélise les ouvrages provisoires du chantier : les échafaudages de façade et le coffrage des dalles et des poutres. Lapin les dispose à partir du modèle et compte ce qu’il leur faut. Quand le modèle change, ils le suivent.

![L’onglet Installations de chantier](shots/ribbon-site-works.webp)

Les échafaudages et les coffrages sont **temporaires** : ils sont montés et démontés dans la phase **Execution**. Le premier que vous placez ajoute cette phase si le projet n’en a pas, et en fait la phase active. Les vues des phases antérieures les omettent. Voir **Gérer → Phases**.

![Un échafaudage autour de la maison, le coffrage du premier étage sur ses étais](shots/site-works-3d.webp)

## Échafaudages

- **Échafaudage sur les murs** (<kbd>SW</kbd>) : sélectionnez les murs (Maj-clic pour en ajouter), puis appuyez sur l’outil. L’échafaudage fait le tour de leurs faces extérieures, à la distance du mur fixée par son type, jusqu’au sommet des murs. Il ferme lui-même ses angles. Déplacez un mur ou rehaussez-le, et l’échafaudage suit.
- **Échafaudage** (<kbd>SC</kbd>) : dessinez sa ligne en plan le long de la façade, et appuyez sur Entrée pour terminer. L’échafaudage se place à gauche de la ligne. Choisissez l’autre côté dans ses propriétés.

Chaque tronçon droit est découpé selon les longueurs de travée du type, avec le moins de travées possible pour atteindre son extrémité. Les niveaux de plancher montent jusqu’à ce que le plancher supérieur permette d’atteindre le haut de l’ouvrage. Le garde-corps se dresse au-dessus du plancher supérieur. Les pieds reposent sur le terrain, sur des vérins de pied. Si le sol descend plus que les vérins ne peuvent rattraper, Lapin le signale.

![Les propriétés d’un échafaudage](shots/scaffold-properties.webp)

Dans ses propriétés, vous pouvez régler le **haut de l’ouvrage** : laissez-le vide pour prendre le sommet des murs. Vous pouvez aussi choisir si les pieds reposent **sur le terrain** ou **sur le niveau**, ou le **détacher** des murs.

Chaque échafaudage et chaque coffrage a un **type**. Vous le choisissez en haut des propriétés, ou vous donnez le type d’un élément à d’autres avec Appliquer le type (MA). Sous **Paramètres de type** se trouvent les valeurs que fixe le configurateur : le système, les longueurs de travée, la largeur, la hauteur de niveau, le contreplaqué, les entraxes, l’article. Vous pouvez aussi les modifier à cet endroit. Une modification appartient au type : tous les éléments de ce type la suivent. Créer un similaire (CS) en dessine un nouveau du même type. Un type que vous créez dans le configurateur est celui que prend le prochain échafaudage ou coffrage.

**Types d’échafaudage…** ouvre le configurateur. Il règle le système (multidirectionnel ou à cadres), les longueurs de travée, la largeur et la hauteur de niveau, la distance au mur, les planchers, les garde-corps et les plinthes, les ancrages, les vérins de pied, les travées d’accès et le filet de protection. L’élévation montre un tronçon de la longueur et de la hauteur que vous choisissez, avec ce qu’il compte.

![Le configurateur d’échafaudage](shots/scaffold-configurator.webp)

## Coffrage

- **Coffrage** (<kbd>FW</kbd>) : sélectionnez des dalles et des poutres, puis appuyez sur l’outil. Chacune reçoit le coffrage de toutes ses faces coffrables.
- **Coffrage sur une face** (<kbd>FF</kbd>) : choisissez une face. En 3D, la face sous le curseur s’allume avec son nom : la sous-face d’une dalle, le fond d’une poutre ou l’une de ses joues. En plan, cliquez sur le bord d’une dalle ou à l’intérieur de son contour. Une face choisie ensuite s’ajoute au même coffrage.

Le **coffrage d’une dalle** couvre sa sous-face de contreplaqué sur des poutrelles secondaires, sur des poutrelles primaires, sur des étais. Les poutres sous la dalle ne sont pas coffrées une seconde fois, pas plus que les murs et les poteaux sur lesquels elle repose, ni ses trémies. Les entraxes viennent du tableau du type, selon l’épaisseur de la dalle. Les étais descendent jusqu’à la dalle inférieure, jusqu’au sol ou jusqu’au niveau du dessous. Les rives libres sont coffrées aussi, sur la hauteur de la dalle plus un relevé. Dans les propriétés, vous choisissez quelles faces sont coffrées, et la direction des poutrelles primaires.

Le **coffrage d’une poutre** comprend son fond, sur des traverses et des étais, et ses joues. Chaque joue a la hauteur de la poutre sous la dalle coulée avec elle. Les poteaux et les poutres plus hautes l’interrompent. Vous pouvez ajouter des baguettes de chanfrein le long des arêtes inférieures.

**Types de coffrage…** ouvre le configurateur, pour les dalles et pour les poutres. Pour les dalles, il règle le système (poutrelles bois et étais, ou panneaux), le contreplaqué, les poutrelles et leurs longueurs de catalogue, le tableau des entraxes, les rives, les étais et les réemplois. Pour les poutres, il règle les traverses, les étais, les serre-joints selon la hauteur de la poutre, le chanfrein et les réemplois. La coupe montre les couches et l’étai que le moteur choisit pour la hauteur que vous indiquez.

![Le configurateur de coffrage](shots/formwork-configurator.webp)

## Murs, poteaux, chaînages et semelles

**Coffrage** et **Coffrage sur une face** s’appliquent aussi aux murs, aux poteaux et aux semelles :

- **Un mur** reçoit ses deux faces et ses extrémités libres, moins ses portes et ses fenêtres. Un mur multicouche avec un chaînage ou un bandeau en béton ne reçoit que ceux-ci. Dans ses propriétés, vous pouvez choisir les deux faces, les chaînages ou les extrémités libres.
- **Un poteau** reçoit ses faces latérales, jusqu’à la dalle ou la poutre coulée dessus.
- **Une semelle** reçoit les côtés de chaque gradin d’une semelle isolée, ou les côtés de la base et du fût d’une semelle filante.

Tout ce qui s’appuie contre une face est exclu : un autre mur, un poteau. Une dalle coulée par-dessus arrête la face à sa sous-face. Le type règle les panneaux (leurs largeurs et hauteurs), les tiges de serrage à travers les murs, les cadres de serrage des poteaux et les étais tirant-poussant.

![Le configurateur pour murs et poteaux](shots/wall-formwork-configurator.webp)

## Plan de coffrage

**Plan de coffrage** crée un plan du niveau actif dans la phase Execution, coupé à un mètre au-dessus du sol, **vu de dessous** : un plan de plafond. Les étais sont coupés. Le coffrage au-dessus d’eux est dessiné tel que vous le verriez en levant les yeux : les poutrelles primaires et secondaires, le contreplaqué et les rives. Il est orienté comme le plan, de sorte que les deux concordent.

**Plan de coffrage vu de dessus** crée le même plan vu de dessus, avec le coffrage dessiné en tirets par-dessus la coupe. Tout plan peut passer de l’un à l’autre dans ses propriétés (**Vu depuis**) ou dans **Plage de vue**.

![Le plan de coffrage du rez-de-chaussée](shots/formwork-plan.webp)

## Ce qu’ils comptent

Chaque échafaudage et chaque coffrage indique ses quantités dans ses propriétés :

- pour l’échafaudage, la surface, les travées, les niveaux, les montants, les planchers et les ancrages ;
- pour le coffrage, la surface de contact par face, les étais par taille et les poutrelles par longueur de catalogue.

**Nomenclature de coffrage** et **Nomenclature d’échafaudage** ajoutent leurs tableaux à l’arborescence du projet, sous Nomenclatures, pour les placer sur une feuille ou les exporter.

La nomenclature de coffrage se termine par une ligne par type :

- **le jeu** : le plus grand coffrage coulé en une fois. C’est par phase d’exécution quand les travaux sont découpés en plusieurs, sinon par niveau.
- **ce qu’il faut acheter** : le jeu, ou le total divisé par le nombre de réemplois du type, la plus grande des deux valeurs.

Chaque type a son **article** pour le devis quantitatif : le code dans les normes qu’utilise votre devis, et son libellé. Vous les saisissez dans le configurateur, et la nomenclature les affiche sur chaque ligne.

En IFC, ils sont écrits comme des assemblages (`IfcElementAssembly` FORMWORK et SCAFFOLD) avec le statut TEMPORARY. Les pièces sont des membrures, des plaques et des accessoires. Chacune est accompagnée de sa ressource de produit de construction, et le coffrage est relié à l’élément qu’il coffre.
