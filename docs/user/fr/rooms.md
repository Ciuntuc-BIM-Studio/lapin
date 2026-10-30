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

- **Nom** et **Numéro.** Les nouvelles pièces sont numérotées par niveau : 101, 102… au rez-de-chaussée.
- **Fonction**, **Service** et **Revêtement de sol.** Saisissez ce que vous voulez, ou choisissez une valeur que d’autres pièces ont déjà.
- **Hauteur.** Vide signifie la hauteur du niveau.
- **Couleur propre** et **Hachure propre** remplacent le jeu de couleurs pour cette seule pièce.

Les quantités en dessous indiquent la surface nette de la pièce (sans les poteaux), son périmètre et son volume. Faites glisser la poignée de l’étiquette pour déplacer l’étiquette.

![Les propriétés d’une pièce](shots/room-properties.webp)

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

## Les pièces en 3D et en IFC

Les pièces sont masquées dans la vue 3D, pour ne pas recouvrir le bâtiment. **Visibilité / Graphismes** dans la vue 3D peut les afficher.

Dans un export IFC, chaque pièce est un IfcSpace, avec son numéro, son nom, sa fonction, ses surfaces et son volume.

## Pièces depuis un fichier IFC

**Fichier → Importer IFC…** importe les espaces du fichier comme des pièces, quel que soit le programme qui les a écrits (Revit, ArchiCAD, Allplan, Bonsai…) :

- chacune va sur **son propre niveau**, celui où le fichier la place ;
- son **contour** vient de sa forme (une extrusion, une B-rep ou un maillage, ou l’emprise dessinée à côté) ; elle garde ce contour, elle ne suit pas les murs ;
- son **nom** est le nom long de l’espace et son **numéro** son nom (comme Revit et ArchiCAD les écrivent) ; sa **hauteur**, et son **décalage de base** quand elle commence au-dessus de son niveau (une mezzanine, un plancher surélevé), viennent de sa forme ;
- sa **fonction**, son **service** et son **revêtement de sol** viennent du fichier quand il les indique (le type d’objet ou la catégorie de l’espace, une propriété Department, le revêtement de sol), si bien qu’un jeu de couleurs par fonction ou par service les colore aussitôt ;
- tout le reste de ce que l’espace avait dans le fichier, ses jeux de propriétés et ses quantités (Pset_SpaceCommon, BaseQuantities, les jeux propres au programme), est conservé et listé, en lecture seule, sous **IFC properties** dans ses propriétés, avec un filtre quand il y en a beaucoup.

Le message de l’import indique combien de pièces sont arrivées. Cherchez-les sur les plans de leurs niveaux (chacune affiche son étiquette), dans une nomenclature **Pièces** (**+** sous Nomenclatures dans l’arborescence) ou dans l’onglet IFC de l’arborescence ; la vue 3D masque les pièces.

Un espace dont la forme ne donne aucun contour est quand même importé, avec son nom et ses propriétés, comme une pièce sans contour : il figure dans la nomenclature mais n’est dessiné nulle part. Sélectionnez-le (onglet IFC de l’arborescence), ouvrez le plan de son niveau, puis **Trouver d’un clic** (cliquez entre ses murs) ou **Dessiner le contour** dans ses propriétés.
