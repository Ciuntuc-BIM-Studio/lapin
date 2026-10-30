---
title: L’assistant de modèle
order: 127
summary: Interrogez un modèle d’IA local sur votre projet, obtenez de courts rapports et laissez-le proposer des valeurs de propriétés que vous approuvez. Application de bureau uniquement.
---

L’**Assistant** répond à des questions sur le projet ouvert, formulées avec vos propres mots : combien de murs compte chaque niveau et quelle est leur longueur, quelles portes font plus de 1 m de large, lesquelles n’ont pas encore de degré de résistance au feu. Il produit de courts rapports sous forme de tableaux que vous pouvez exporter, et il peut **proposer** des valeurs de propriétés (un degré de résistance au feu, un statut, vos propres paramètres) que vous examinez et appliquez.

Il fait tourner un modèle de langage **sur votre ordinateur**, par l’intermédiaire d’[Ollama](https://ollama.com). Rien de votre projet ne quitte la machine, et rien n’est envoyé à nous ni à qui que ce soit.

> L’assistant n’existe que dans l’**application de bureau**. La version de Lapin pour navigateur n’a pas d’assistant.

## Ce qu’il peut faire et ne peut pas faire

Il **peut** :

- lire le projet : les niveaux, les éléments, leurs types, matériaux, jeux de propriétés et quantités, ainsi que les nomenclatures ;
- compter, additionner et faire des moyennes (longueurs, surfaces, volumes, toute quantité), regroupés par niveau, par type ou par n’importe quelle propriété. Lapin calcule les nombres ; le modèle ne fait que les rapporter ;
- proposer des valeurs de propriétés, dans les jeux standard d’IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) ou dans les vôtres.

Il **ne peut pas** :

- dessiner, déplacer, remodeler, créer ou supprimer quoi que ce soit. Il ne modifie pas la géométrie ;
- exécuter des commandes ou écrire du code ;
- appliquer quoi que ce soit de lui-même. Chaque valeur qu’il propose attend votre accord.

Un modèle local peut mal comprendre une question. Lisez sa réponse, et vérifiez les valeurs proposées avant de les appliquer.

## Mise en place

1. Installez Ollama depuis [ollama.com](https://ollama.com) et lancez-le (l’application Ollama, ou `ollama serve` dans un terminal).
2. Installez un modèle. Celui qui a donné les meilleurs résultats dans nos essais est **qwen3-coder:30b** (rapide et fiable avec les outils de Lapin, il demande environ 20 Go de mémoire) :

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Sur un ordinateur doté de moins de mémoire, **gemma4:12b** fonctionne aussi, plus lentement.
3. Dans Lapin, ouvrez l’**Assistant** : l’onglet **Assistant** à côté de **Propriétés** à droite, **Vue → Assistant**, ou <kbd>Ctrl</kbd>+<kbd>K</kbd> puis tapez *Assistant*.

Lapin trouve Ollama à l’adresse `http://127.0.0.1:11434` et choisit le meilleur modèle dont vous disposez. Le bouton en forme d’engrenage en haut du panneau affiche l’**Adresse d’Ollama**, le **modèle** à utiliser et indique si Ollama est lancé. Seul un Ollama installé sur cet ordinateur est accepté.

## Poser une question

Tapez une question et appuyez sur <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> pour une nouvelle ligne). Posez-la en français, en anglais, en roumain ou dans une autre langue : la réponse vient dans la même langue.

![L’Assistant : un rapport par niveau, et un degré de résistance au feu proposé pour une porte, en attente d’approbation](shots/assistant.webp)

Pendant qu’il travaille, le panneau affiche chaque étape sur une ligne, par exemple *A listé 12 sur 12 · IfcDoor* ou *A calculé 2 groupes sur 40 éléments*. Survolez une étape pour voir exactement ce qu’il a demandé. **Arrêter** interrompt la réponse ; **Nouvelle conversation** recommence.

Les listes et les totaux apparaissent aussi sous forme de **tableaux** sous la réponse. Le bouton de téléchargement d’un tableau l’enregistre en fichier CSV, pour un tableur.

Quelques questions pour commencer :

- *Combien de murs y a-t-il par niveau, et quelle est leur longueur totale ?*
- *Liste les fenêtres du premier étage avec leur largeur et leur hauteur.*
- *Quelles portes n’ont pas de degré de résistance au feu ?*

## Approuver les valeurs qu’il propose

Demandez, par exemple : *Portes de plus de 1 m de large : donne-leur FireRating EI30.* L’assistant trouve les portes et propose les valeurs. Elles apparaissent sous **Modifications proposées** : chaque élément, le jeu de propriétés et le nom, sa valeur actuelle et la nouvelle, avec une case à cocher.

- Décochez ce que vous ne voulez pas ; la case de l’en-tête coche ou décoche tout.
- Cliquez le nom d’un élément pour le sélectionner dans les vues.
- **Appliquer** écrit les valeurs cochées, toutes ensemble, en **une seule** étape : <kbd>Ctrl</kbd>+<kbd>Z</kbd> les annule toutes.
- **Rejeter** les abandonne.

Les valeurs vont dans les paramètres des éléments, affichés dans le panneau **Propriétés** avec leur jeu, et dans l’export IFC : une propriété standard rejoint son jeu (`Pset_DoorCommon`), les vôtres vont dans un jeu à part.

Les règles que l’assistant suit (et que Lapin vérifie) : un jeu IFC standard (commençant par `Pset_`) ne prend que ses propres noms de propriétés, sur les éléments auxquels il est destiné ; un jeu à vous ne commence jamais par `Pset_`.
