---
title: Premiers pas
order: 10
summary: L’écran d’accueil, un nouveau projet, les parties de la fenêtre, se déplacer dans un dessin et choisir vos unités.
---

## L’écran d’accueil

À son ouverture, Lapin affiche l’écran d’accueil. **Nouveau projet** démarre un bâtiment vide. **Ouvrir un projet…** ouvre un fichier `.lapin`. Les projets que vous avez déjà ouverts sont listés sous **Récents**.

![L’écran d’accueil](shots/start-screen.webp)

Un nouveau projet a déjà trois niveaux : **Foundations** (−1.50), **Ground floor** (±0.00) et **First floor** (+3.00). Il s’ouvre sur le plan du rez-de-chaussée, prêt pour le premier mur.

## La fenêtre

![Les parties de la fenêtre de Lapin](shots/workspace.webp)

1. **Les onglets du ruban.** Dessin, Architecture, Structure, Site, Studio, Annoter, Gérer, Métrés et Vue regroupent chacun leurs outils. **Fichier** ouvre, enregistre, importe et exporte. **Annuler** et **Rétablir** sont à l’extrémité droite de la même barre (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **Le ruban.** Il contient les outils de l’onglet courant. Survolez un outil pour voir son nom et ses lettres au clavier.
3. **L’arborescence du projet.** Elle liste les niveaux, les plans, les coupes, les élévations, les vues 3D, les feuilles et les nomenclatures. Double-cliquez sur l’un d’eux pour l’ouvrir dans un onglet. Un projet s’ouvre sur le plan du rez-de-chaussée, avec la vue 3D dans un onglet à côté.
4. **La vue.** C’est là que vous dessinez : un plan, une coupe, une vue 3D ou une feuille. Vous pouvez ouvrir plusieurs vues côte à côte avec les deux boutons en haut à droite de la vue.
5. **La barre d’accès rapide.** Elle donne les outils les plus utilisés sur les touches numériques <kbd>1</kbd>–<kbd>9</kbd>.
6. **La ligne de commande.** Tapez ici les lettres d’un outil et appuyez sur <kbd>Enter</kbd>. Pendant qu’un outil s’exécute, la ligne vous dit ce dont il a besoin ensuite. Voir [Dessin et ligne de commande](drafting.md).
7. **Le panneau des propriétés.** Il montre ce qui est sélectionné. Quand rien n’est sélectionné, il montre la vue au premier plan.
8. **Ask Me.** C’est l’assistant d’aide de Lapin. Voir [Aide et retours](help.md).

Besoin de plus de place pour le dessin ? **Vue → Interface…** choisit une fenêtre plus compacte, et les panneaux latéraux se replient. Voir [L’interface et les raccourcis](interface.md).

## Se déplacer

- **Zoom.** Tournez la molette de la souris ou pincez sur un pavé tactile. Lapin zoome là où se trouve le curseur.
- **Panoramique.** Faites glisser avec le bouton du milieu ou le bouton droit de la souris, ou avec deux doigts sur un pavé tactile.
- **Orbite en 3D.** Faites glisser avec le bouton gauche de la souris.
- **Tout afficher.** Appuyez sur le bouton **Ajuster**, tapez <kbd>ZE</kbd>, ou double-cliquez avec le bouton du milieu.

## Unités

Lapin travaille en millimètres, sauf si vous choisissez autre chose. Pour changer les unités, ouvrez **Gérer → Normes de dessin → Projet → Units**, ou cliquez sur **Unités** dans la barre d’état en bas de la fenêtre. Vous pouvez choisir les millimètres, les centimètres, les mètres, les pieds et pouces, ou les pouces.

![Normes de dessin, onglet Projet : les unités du projet](shots/standards-project.webp)

Chaque longueur suit ce réglage : les panneaux, les valeurs que vous tapez en dessinant, la ligne de commande et les cotes. Le modèle lui-même est conservé en mètres, si bien que changer d’unités ne modifie jamais votre bâtiment.

## Enregistrer

**Fichier → Enregistrer** (<kbd>Ctrl</kbd> <kbd>S</kbd>) écrit un fichier `.lapin`. Voir [Fichiers, versions et récupération](files.md).
