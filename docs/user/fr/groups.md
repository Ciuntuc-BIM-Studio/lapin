---
title: Groupes et nettoyage
order: 112
summary: Gardez des éléments ensemble dans un groupe, ouvrez un groupe pour le modifier, sélectionnez par zone ou au lasso en 3D, et masquez ce qui gêne.
---

## Groupes

Un groupe garde des éléments ensemble. Sélectionnez-les et cliquez sur **Modifier → Créer un groupe** (<kbd>GP</kbd>). Un clic sur n’importe quel membre sélectionne alors tout le groupe, en plan et en 3D. Le groupe se déplace, se copie, pivote, se symétrise et se supprime d’un seul tenant, et une copie est un groupe à part entière. En IFC, un groupe est un IfcGroup : les autres logiciels le voient aussi.

Les propriétés d’un groupe sélectionné donnent le nombre de ses éléments, avec trois boutons : **Modifier le groupe**, **Dissocier** et **Sélectionner les membres**.

![Un groupe sélectionné : ses membres en surbrillance, ses propriétés](shots/group-selected.webp)

## Modifier un groupe

Double-cliquez sur un membre, ou cliquez sur **Modifier le groupe** (<kbd>EG</kbd>). Le reste du modèle est dessiné estompé et ne peut pas être sélectionné, et un clic sélectionne un membre à la fois. Supprimez, déplacez ou changez les membres comme d’habitude. **Retirer du groupe** en sort les membres sélectionnés. **Terminé**, ou <kbd>Échap</kbd>, ferme le groupe. Un groupe disparaît quand son dernier membre est supprimé.

![Un groupe ouvert pour modification : le mur du jardin, hors du groupe, estompé](shots/group-edit.webp)

## Fichiers importés

Chaque fichier IFC, Fragments ou maillage que vous importez arrive en un seul groupe au nom du fichier. Décochez **En un seul groupe** à l’import IFC pour apporter les éléments séparément. Un fichier lié n’est pas groupé : c’est déjà un lien, en lecture seule (voir [Import et export](import-export.md)). Les éléments lus d’un fichier IFC comme références restent où le fichier les a mis : un groupe de ces éléments peut être nettoyé et supprimé, pas déplacé.

## Sélectionner une zone en 3D

**Vue → Sélection par zone** (<kbd>SR</kbd>) fait d’un glissement du bouton gauche dans la vue 3D une sélection, comme en plan. Glissée vers la droite, une fenêtre prend ce qui est entièrement dedans. Glissée vers la gauche, une capture prend ce qu’elle touche. Appuyez sur <kbd>Espace</kbd> en glissant pour tracer un lasso, et sur <kbd>Maj</kbd> pour ajouter à la sélection. Tant qu’elle est active, le bouton droit fait tourner la vue et celui du milieu la déplace. Le **×** de son étiquette, ou <kbd>SR</kbd> à nouveau, la désactive.

![Une capture glissée sur la maison en 3D](shots/select-region-3d.webp)

## Masquer pour l’instant

**Vue → Masquer** (<kbd>HH</kbd>) masque la sélection dans la vue 3D. **Isoler** (<kbd>HI</kbd>) masque tout le reste. **Tout afficher** (<kbd>HR</kbd>), ou le **×** de l’étiquette, ramène tout. Rien n’est enregistré ni supprimé : c’est un moyen d’atteindre ce qui se trouve à l’intérieur d’un modèle importé pendant que vous le nettoyez.
