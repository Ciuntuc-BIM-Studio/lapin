---
title: Fichiers, versions et récupération
order: 120
summary: Enregistrez et ouvrez des projets, récupérez le travail non enregistré après un plantage, et conservez des versions dans le fichier du projet.
---

<!-- video files -->

## Enregistrer et ouvrir

**Fichier → Enregistrer** (<kbd>Ctrl</kbd> <kbd>S</kbd>) écrit un fichier `.lapin`, et **Fichier → Ouvrir…** (<kbd>Ctrl</kbd> <kbd>O</kbd>) en ouvre un.

- **Application de bureau.** Le fichier est enregistré là où vous le choisissez.
- **Navigateur, Chrome et Edge.** Lapin enregistre à chaque fois dans le même fichier.
- **Navigateur, Safari et Firefox.** Chaque enregistrement télécharge le fichier.

Un fichier `.lapin` contient tout le projet : le modèle, les vues, les feuilles, les matériaux avec leurs propres images, et les versions.

## Récupérer le travail non enregistré

Lapin conserve votre travail non enregistré au fur et à mesure. Si l’application ou l’ordinateur s’arrête avant que vous n’enregistriez, l’écran d’accueil propose de le récupérer la fois suivante.

## Versions

Une version est le projet tel qu’il est au moment où vous l’enregistrez comme version : avant une réunion avec le client, ou à la fin d’une phase de conception. Ouvrez **Fichier → Enregistrer une version…** ou **Gérer → Versions**.

![Versions : enregistrer le projet tel qu’il est maintenant](shots/versions.webp)

Les versions se trouvent dans le fichier du projet, et les parties qu’une version partage avec le projet ne sont conservées qu’une fois. Depuis la liste, vous pouvez :

- renommer une version ;
- la mettre à jour vers l’état actuel, avec une révision ;
- la comparer avec le projet, élément par élément ;
- la restaurer, en une seule étape que vous pouvez annuler ;
- l’exporter ou l’ouvrir séparément ;
- la supprimer.

## Annuler

<kbd>Ctrl</kbd> <kbd>Z</kbd> annule, et <kbd>Ctrl</kbd> <kbd>Y</kbd> ou <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>Z</kbd> rétablit. Les boutons **Annuler** et **Rétablir** de la barre du haut, à droite de **Commandes…**, font de même dans tous les modes d’interface, et leur infobulle indique ce qu’ils annulent ; **Fichier → Annuler** et **Fichier → Rétablir**, ainsi que la palette de commandes (<kbd>Ctrl</kbd> <kbd>K</kbd>, tapez « annuler »), aussi.
