---
title: Pièces et couleurs des pièces
order: 45
summary: Placez des pièces d’un clic entre les murs, nommez-les et numérotez-les, puis colorez-les ou hachurez-les sur un plan selon leur fonction ou leur service, avec une légende.
---

## Placer une pièce

Choisissez **Architecture → Pièce** (<kbd>RM</kbd>) et cliquez dans un espace fermé par des murs. Lapin trouve le contour de la pièce entre les faces des murs, des murs-rideaux et des poteaux qui entourent le point. Les portes n’ouvrent pas la pièce : elle se ferme à travers elles. Un poteau situé dans la pièce est exclu de sa surface.

La pièce suit ses murs. Déplacez un mur et la pièce s’agrandit ou rétrécit avec lui. Si un mur est supprimé et que plus rien ne ferme la pièce, elle garde le contour qu’elle avait et son étiquette devient rouge. **Architecture → Mettre à jour les pièces** enregistre les contours que les pièces trouvent maintenant.

Pour un espace qui n’est pas entouré de murs, comme une terrasse ou l’extrémité d’un espace ouvert, utilisez **Pièce par contour** et cliquez ses angles. Cette pièce garde le contour que vous dessinez.

![Un plan avec deux pièces colorées selon leur fonction et une légende de couleurs](shots/rooms-plan.webp)

## Les propriétés d’une pièce

Sélectionnez une pièce en cliquant son étiquette ou n’importe où à l’intérieur. Dans le panneau des propriétés :

- **Nom** et **Numéro.** Les nouvelles pièces sont numérotées par niveau : 101, 102… au rez-de-chaussée. Le nom d’une pièce est celui de sa fonction tant que vous ne lui en donnez pas un ; vidé, il revient à la fonction.
- **Fonction**, **Service** et **Revêtement de sol.** Saisissez ce que vous voulez, ou choisissez une valeur que d’autres pièces ont déjà.
- **Hauteur.** Vide signifie la hauteur du niveau.
- **Couleur propre** et **Hachure propre** remplacent le jeu de couleurs pour cette seule pièce.

Les quantités en dessous indiquent la surface nette de la pièce (sans les poteaux), son périmètre et son volume. Faites glisser la poignée de l’étiquette pour déplacer l’étiquette.

![Les propriétés d’une pièce](shots/room-properties.webp)

## Étiquette de pièce

**Architecture → Étiquette de pièce** (ou **Étiquette de pièce…** dans les propriétés d’une pièce) choisit ce que l’étiquette de chaque pièce montre sur les plans, ligne par ligne : le nom, le numéro, la fonction, le département, la surface, la surface brute, le périmètre, la hauteur, le volume ou le revêtement de sol, ou un texte à vous. La première ligne est en gras. **Normes de dessin → Étiquettes** montre la même étiquette en texte : `{name}|{number}|{area}`.

![La boîte de dialogue Étiquette de pièce](shots/room-tag.webp)

## Couleurs des pièces

**Vue → Couleurs des pièces** colore les pièces d’un plan. Choisissez un jeu de couleurs, ce selon quoi il colore (**Fonction**, **Nom**, **Service**, **Numéro** ou **Revêtement de sol**), et réglez **Ce plan montre** sur ce jeu. Chaque valeur que portent les pièces reçoit une ligne avec :

- une **Couleur**,
- une **Hachure** parmi les motifs du projet, avec son **Échelle** et sa couleur d’**Encre**,
- et un aperçu du rendu.

**Compléter les couleurs manquantes** donne à chaque valeur sans couleur une couleur d’une palette douce. **Nouveau** et **Dupliquer** créent d’autres jeux, par exemple un pour une présentation au client et un par service.

![La boîte de dialogue Couleurs des pièces](shots/room-colors.webp)

Les couleurs sont dessinées sous les murs, le mobilier et les battements des portes, pour que le plan reste lisible. Elles s’impriment sur les feuilles et passent dans les exports PDF et DXF. Un nouveau plan n’affiche aucune couleur de pièce tant que vous ne lui avez pas choisi un jeu, dans **Couleurs des pièces** ou dans les propriétés du plan.

## Légende de couleurs

**Annoter → Légende de couleurs** : cliquez là où la légende doit aller sur le plan. Elle liste les couleurs qu’utilisent les pièces du plan, avec leurs noms, et suit toute modification du jeu. Déplacez-la avec **Déplacer**.

## Cotes des pièces

**Annotate → Room dimensions** (<kbd>ID</kbd>) cote d’un coup l’intérieur des pièces d’un plan. Chaque côté d’une pièce reçoit une chaîne de cotes alignée, juste à l’intérieur. La chaîne a un point à chaque angle du côté et aux tableaux de chaque porte et fenêtre qui s’y trouve.

- Avec des pièces sélectionnées, seules celles-ci sont cotées. Sans sélection, toutes les pièces du niveau du plan le sont.
- Demandées sur un plan de masse, qui voit tout le modèle d’en haut et ne coupe aucun mur, les chaînes vont au plan du niveau. La hauteur du texte d’une cote se règle à part dans les Propriétés (**Text height**, mm sur le papier ; vide : celle de son style de cote). Les hauteurs de texte sont sur le papier : sur un plan de masse au 1:500, le même texte est cinq fois plus grand dans le modèle que sur un plan au 1:100.
- Les points tiennent aux murs, comme pour toute cote. Déplacez un mur et les chaînes le suivent.
- Relancez-le sur une pièce pour refaire ses chaînes, par exemple après avoir ajouté une porte. Les anciennes chaînes sont remplacées, pas doublées.
- Les côtés très courts, de moins de 30 cm, sont laissés de côté. Toute chaîne se modifie ou se supprime comme une chaîne dessinée à la main.

![Deux pièces cotées à l’intérieur : la chaîne de chaque côté, avec la porte et les fenêtres](shots/room-dimensions.webp)

## Finitions des pièces

**Intérieur → Revêtir la pièce** (<kbd>FR</kbd>) pose une finition dans la pièce où vous cliquez : son sol, la finition de ses murs et son plafond, chacun suivant le contour de la pièce comme la pièce le fait. Déplacez un mur et les finitions le suivent. **Intérieur → Finitions des pièces** modifie les finitions ; **De la bibliothèque** en apporte de toutes faites : Séjour, Chambre, Salle de bains, Cuisine, Rez-de-chaussée sur dalle et Local technique. Tant que l’outil est actif, le panneau des propriétés montre la finition qu’il pose, à choisir avant le clic. La **Finition** d’une pièce, dans ses propriétés, en pose une autre ou l’enlève.

Une finition a :

- **Sol**, ses couches depuis le sol fini vers le bas (carrelage, colle, chape…). Les trémies de la dalle dans la pièce (une cage d’escalier) sont déduites. **À travers les portes, jusqu’à l’axe du mur** prolonge le sol sous chaque porte, pour que le sol de la pièce voisine le rejoigne à l’axe du mur.
- **Murs**, leurs couches depuis la face du mur vers la pièce, autour des îlots de la pièce aussi. Chaque couche peut changer de matériau sur sa hauteur, comme la couche d’un type de mur : dans la Salle de bains, de la peinture au-dessus d’un carrelage jusqu’à 2100 (13 mm d’épaisseur, carrelage et colle) ; dans la Cuisine, une bande de carrelage de 900 à 1500. Un morceau sans matériau ne laisse rien à cet endroit. Les portes et fenêtres des murs autour de la pièce sont découpées, et leurs tableaux revêtus des mêmes couches, de la face de la pièce jusqu’au cadre (pour une baie nue, jusqu’à l’axe du mur) ; le bas d’une fenêtre aussi, sauf si une tablette le couvre côté pièce. **Hauteur** : vide, jusqu’au plafond.
- **Plafond**, ses couches depuis la dalle au-dessus de la pièce vers le bas (depuis la hauteur de la pièce quand il n’y a pas de dalle), ses trémies déduites.
- **Plinthe** : un type de plinthe posé autour de la pièce, interrompu à ses portes (voir Plinthes, dans Dalles et toits).
- **Une dalle structurelle sous le sol** : une dalle de béton armé à part sous les couches du sol, avec son épaisseur et de combien elle dépasse le contour de la pièce (sous les murs). C’est une dalle comme les autres, dans la structure et dans sa nomenclature.

**Enregistrer** modifie la finition, et chaque pièce qui l’a est reposée. **Revêtir les pièces sélectionnées** la pose dans les pièces sélectionnées.

**Hachure en plan.** Sur les plans, le sol d’une pièce finie est hachuré comme le dit sa finition, sous les murs, donc les planches, le PDF et le DXF l’impriment (calque FINISH-HATCH). Dans le **Sol** de la finition, **Hachure en plan** choisit **selon son matériau** (par défaut : le motif de surface de la couche supérieure, sa hachure pour les élévations, là où il est vu, dans **Gérer → Matériaux**, sinon celle que suggère son nom : carreaux, lames pour le parquet et le stratifié, une croix pour la moquette, des lignes pour le vinyle, des points pour un sol coulé, sinon des lignes obliques), **motif propre** (tout motif du projet, avec son échelle, son angle et sa couleur) ou **sans hachure** ; l’échelle et l’angle s’appliquent aussi au motif du matériau. Un plan vu de dessous (un plan de plafond réfléchi) hachure à la place les plafonds, selon la **Hachure en plan** du **Plafond**, de même. **Hachure du sol**, dans les propriétés de la pièce, la désactive pour cette pièce. **Hachures des revêtements de sol** d’un style de vue (**Vue → Paramètres de vue**, **Gérer → Styles de vue**) la désactive pour des vues entières : active dans Architectural, désactivée dans Structural, MEP et Coordination. Avec les couleurs des pièces sur le plan, la couleur remplit la pièce et la hachure est dessinée par-dessus.

![Un plan avec le sol de la salle de bains hachuré en carreaux et celui du séjour en lames](shots/room-finish-hatch.webp)

Chaque finition mesure, par matériau, sa surface **brute** (le contour de la pièce, rien de déduit : l’aire du sol, le périmètre des murs fois la hauteur de chaque morceau) et sa surface **nette** (ce qui est construit : portes, fenêtres et trémies déduites, les tableaux et le sol sous les portes ajoutés ; la part des tableaux figure dans les quantités, comme RevealArea), et son volume. **Intérieur → Nomenclature des finitions** liste chaque pièce finie par partie et par matériau, avec les totaux par matériau à la fin ; comme toute nomenclature, elle fait des graphiques, s’exporte et se place sur une feuille. En IFC, une finition est un IfcCovering : FLOORING, CLADDING ou CEILING.

Une finition revêt ce que les murs de la pièce ont déjà : si les types de murs portent leur propre enduit, ne donnez aux murs de la pièce que ce qui vient par-dessus (peinture, carrelage), ou utilisez des types de murs réduits au noyau.

![Les finitions des pièces : le sol de la Salle de bains, les murs carrelés jusqu’à 2100 et le plafond](shots/room-finishes.webp)

![Les propriétés d’une pièce avec sa finition](shots/room-finish-properties.webp)

## Les pièces en 3D et en IFC

Les pièces sont masquées dans la vue 3D, pour ne pas recouvrir le bâtiment. **Visibilité / Graphismes** dans la vue 3D peut les afficher.

Dans un export IFC, chaque pièce est un IfcSpace, avec son numéro, son nom, sa fonction, ses surfaces et son volume.

## Pièces depuis un fichier IFC

**Fichier → Importer IFC…** importe les espaces du fichier comme des pièces, quel que soit le programme qui les a écrits (Revit, ArchiCAD, Allplan, Bonsai…) :

- chacune va sur **son propre niveau**, celui où le fichier la place ;
- son **contour** vient de sa forme (une extrusion, une B-rep ou un maillage, ou l’emprise dessinée à côté) ; elle garde ce contour, elle ne suit pas les murs ;
- son **nom** est le nom long de l’espace et son **numéro** son nom (comme Revit et ArchiCAD les écrivent) ; sa **hauteur**, et son **décalage de base** quand elle commence au-dessus de son niveau (une mezzanine, un plancher surélevé), viennent de sa forme ;
- sa **fonction**, son **service** et son **revêtement de sol** viennent du fichier quand il les indique (le type d’objet ou la catégorie de l’espace, une propriété Department, le revêtement de sol), si bien qu’un jeu de couleurs par fonction ou par service les colore aussitôt ;
- tout le reste de ce que l’espace avait dans le fichier, ses jeux de propriétés et ses quantités (Pset_SpaceCommon, BaseQuantities, les jeux propres au programme), est conservé et listé, en lecture seule, sous **Propriétés IFC** dans ses propriétés, avec un filtre quand il y en a beaucoup.

Le message de l’import indique combien de pièces sont arrivées. Cherchez-les sur les plans de leurs niveaux (chacune affiche son étiquette), dans une nomenclature **Pièces** (**+** sous Nomenclatures dans l’arborescence) ou dans l’onglet IFC de l’arborescence ; la vue 3D masque les pièces.

Un espace dont la forme ne donne aucun contour est quand même importé, avec son nom et ses propriétés, comme une pièce sans contour : il figure dans la nomenclature mais n’est dessiné nulle part. Sélectionnez-le (onglet IFC de l’arborescence), ouvrez le plan de son niveau, puis **Trouver d’un clic** (cliquez entre ses murs) ou **Dessiner le contour** dans ses propriétés.
