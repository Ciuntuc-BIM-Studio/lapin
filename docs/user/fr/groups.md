---
title: Groupes et nettoyage
order: 112
summary: Gardez des éléments ensemble dans un groupe, ouvrez un groupe pour le modifier, sélectionnez par zone ou au lasso en 3D, et masquez ce qui gêne.
---

## Groupes

Un groupe garde des éléments ensemble. Sélectionnez-les et cliquez sur **Modifier → Créer un groupe** (<kbd>GP</kbd>). Un clic sur n’importe quel membre sélectionne alors tout le groupe, en plan et en 3D. Le groupe se déplace, se copie, pivote, se symétrise et se supprime d’un seul tenant, et une copie est un groupe à part entière. En IFC, un groupe est un IfcGroup : les autres logiciels le voient aussi.

Le menu du clic droit dans un plan, une coupe ou une élévation propose les mêmes commandes : **Créer un groupe** pour la sélection, et sur un groupe **Dissocier** et **Modifier le groupe**. Les lignes, textes et hachures dessinés sur une vue se groupent aussi, dont les lignes d’un plan DXF importé : un dessin entier se déplace alors d’un seul tenant.

![Des lignes dessinées sur le plan, groupées depuis le menu du clic droit](shots/group-drafting.webp)

Les propriétés d’un groupe sélectionné donnent le nombre de ses éléments, avec trois boutons : **Modifier le groupe**, **Dissocier** et **Sélectionner les membres**.

![Un groupe sélectionné : ses membres en surbrillance, ses propriétés](shots/group-selected.webp)

## Modifier un groupe

Double-cliquez sur un membre, ou cliquez sur **Modifier le groupe** (<kbd>EG</kbd>). Le reste du modèle est dessiné estompé et ne peut pas être sélectionné, et un clic sélectionne un membre à la fois. Supprimez, déplacez ou changez les membres comme d’habitude. **Retirer du groupe** en sort les membres sélectionnés. **Terminé**, ou <kbd>Échap</kbd>, ferme le groupe. Un groupe disparaît quand son dernier membre est supprimé.

![Un groupe ouvert pour modification : le mur du jardin, hors du groupe, estompé](shots/group-edit.webp)

## Sélection par niveaux d'assemblage

Certains éléments sont les parties d'un tout : les poteaux, traverses et pannes d'une halle métallique, les parties d'un assemblage (une boîte Studio, un assemblage lu dans un fichier IFC) et les membres d'un groupe. **Vue → Sélection par niveaux d'assemblage**, désactivée au départ, les choisit comme le tout est construit. Le premier clic sur une partie sélectionne le tout du haut : le groupe ou la halle. Tant qu'il est sélectionné, un clic à l'intérieur sélectionne la partie un niveau plus bas, sur le chemin de ce que vous avez cliqué, et chaque clic suivant descend d'un niveau, jusqu'à l'élément lui-même. Un clic sur une autre partie du même tout la sélectionne au même niveau. <kbd>Échap</kbd> remonte d'un niveau et, en haut, abandonne la sélection ; un clic dans le vide l'abandonne aussi. <kbd>Ctrl</kbd>+clic (<kbd>Cmd</kbd>+clic sur Mac) sélectionne directement l'élément, et <kbd>Maj</kbd> ajoute le tout du haut à la sélection. Un fil d'Ariane au-dessus de la vue nomme les niveaux, de l'étage à la sélection : cliquez sur l'un pour le sélectionner. Tant qu'une partie d'un tout est sélectionnée, le reste du modèle est dessiné légèrement estompé. Cela fonctionne de même en 3D, en plans, coupes et élévations. Un rectangle ou un lasso de sélection prend les touts du haut. Un double-clic ouvre toujours un groupe pour l'éditer ; à l'intérieur, la descente commence sous le groupe. Le choix est gardé sur cet ordinateur et se trouve aussi dans **Vue → Paramètres de vue**.

![Dans une halle métallique : la traverse sélectionnée, le fil d'Ariane au-dessus de la 3D](shots/drill-select.webp)

## Fichiers importés

Chaque fichier IFC, Fragments ou maillage que vous importez arrive en un seul groupe au nom du fichier. Décochez **En un seul groupe** à l’import IFC pour apporter les éléments séparément. Un fichier lié n’est pas groupé : c’est déjà un lien, en lecture seule (voir [Import et export](import-export.md)). Les éléments lus d’un fichier comme références (d’un IFC, d’un maillage) se déplacent, pivotent, se reflètent et se copient avec leur groupe comme n’importe quel élément ; leur forme reste celle du fichier.

## Sélectionner une zone en 3D

**Vue → Sélection par zone** (<kbd>SR</kbd>) fait d’un glissement du bouton gauche dans la vue 3D une sélection, comme en plan. Glissée vers la droite, une fenêtre prend ce qui est entièrement dedans. Glissée vers la gauche, une capture prend ce qu’elle touche. Appuyez sur <kbd>Espace</kbd> en glissant pour tracer un lasso, et sur <kbd>Maj</kbd> pour ajouter à la sélection. Tant qu’elle est active, le bouton droit fait tourner la vue et celui du milieu la déplace. Le **×** de son étiquette, ou <kbd>SR</kbd> à nouveau, la désactive.

![Une capture glissée sur la maison en 3D](shots/select-region-3d.webp)

## Masquer pour l’instant

**Vue → Masquer** (<kbd>HH</kbd>) masque la sélection dans la vue 3D. **Isoler** (<kbd>HI</kbd>) masque tout le reste. **Tout afficher** (<kbd>HR</kbd>), ou le **×** de l’étiquette, ramène tout. Rien n’est enregistré ni supprimé : c’est un moyen d’atteindre ce qui se trouve à l’intérieur d’un modèle importé pendant que vous le nettoyez. Le clic droit dans la vue 3D propose les mêmes entrées (**Masquer**, **Isoler** et, tant que quelque chose est masqué, **Tout afficher**) : ce qui est sous le curseur est d’abord sélectionné s’il ne l’est pas déjà.
