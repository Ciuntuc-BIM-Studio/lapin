---
title: Extensions
order: 126
summary: Ajoutez du mobilier, des modèles de projet, des profilés, des motifs de hachures, des cartouches et des listes de prix créés par d'autres depuis le catalogue Lapin Extensions, puis retirez-les.
---

## Le catalogue

**Lapin Extensions** est un catalogue gratuit de contenus créés pour Lapin par ses utilisateurs et par nous : packs de mobilier, modèles de projet, bibliothèques de profilés, motifs de hachures, cartouches et listes de prix. Chacun va dans la bibliothèque que Lapin a déjà pour lui, et vous l'utilisez comme si vous l'aviez fait vous-même.

Ouvrez **Fichier → Extensions…**. **Parcourir** liste le catalogue : cherchez par nom, par étiquette ou par ce qu'une extension contient, ou choisissez une catégorie.

![Extensions : Parcourir, les détails d'un pack de mobilier](shots/extensions-browse.webp)

Cliquez sur une extension pour voir ses détails avant de l'installer :

- **Auteur** et **Licence**. Chaque extension a une licence, choisie par son auteur. Une licence *permissive* (MIT, CC-BY, CC0…) vous permet d'utiliser son contenu dans n'importe quel projet ; pour une autre, *lisez ses conditions* d'abord.
- **Non commerciale** : une extension dont la licence interdit l'usage commercial (CC-BY-NC, PolyForm Noncommercial…) est marquée ainsi dans la liste et dans ses détails. Vous pouvez l'installer et en apprendre, mais pas l'utiliser dans un travail rémunéré.
- **Fonctionne avec** : les versions de Lapin pour lesquelles elle a été faite. Une extension qui ne convient pas à votre Lapin le dit au lieu de proposer **Installer**.
- **Taille**, le fichier et son **SHA-256** : l'empreinte du fichier examiné par le catalogue.
- **Contenu** : ses éléments, quand son auteur les énumère.
- **Page d'accueil** et **Sujet du forum** : où poser vos questions à son auteur.

## Installer, mettre à jour, désinstaller

Cliquez sur **Installer**. Lapin télécharge le fichier, vérifie que sa taille et son SHA-256 sont exactement ceux du catalogue (un fichier modifié en route est refusé, et rien n'est installé), puis le range dans sa bibliothèque :

| Type | Où il va |
|---|---|
| Mobilier | **Intérieur → Mon mobilier…** |
| Modèles de projet | **Fichier → Nouveau depuis un modèle…** |
| Profilés | les bibliothèques de profilés de cet ordinateur |
| Motifs de hachures | Mes motifs, proposés par **Gérer → Matériaux** |
| Cartouches | les cartouches de cet ordinateur |
| Listes de prix | **Exécution → Listes de prix…**, marquées indicatives : les prix sont ceux de l'auteur, comparez-les aux vôtres |
| Types | le projet ouvert quand vous l'installez, en une étape à annuler (**Gérer → Paquets de types…** exporte les vôtres) |

Si votre bibliothèque contient déjà quelque chose du même nom, Lapin demande avant de le remplacer. Rien de ce qu'apporte une extension n'est jamais exécuté : les extensions sont du contenu.

Le catalogue de Lapin est aussi **signé** par Lapin Extensions : le catalogue et chacun de ses fichiers portent une signature que Lapin vérifie avant de lire la liste ou d'installer un fichier, si bien qu'une copie modifiée en chemin est refusée. Une ligne au-dessus de la liste indique **Signé par Lapin Extensions** quand c'est le cas.

**Installées** liste ce que cet ordinateur possède. Chaque ligne ouvre l'endroit où le contenu est allé (**Mon mobilier…**, **Modèles de projet…**), propose **Mettre à jour** quand le catalogue a une version plus récente, et **Désinstaller**.

![Installées : le pack de mobilier, avec le bouton qui ouvre Mon mobilier](shots/extensions-installed.webp)

![Les types de mobilier du pack dans Mon mobilier](shots/extensions-my-furniture.webp)

**Désinstaller** retire de vos bibliothèques ce que l'extension a ajouté, et seulement cela. Ce que vous avez déjà mis dans vos projets y reste : un type de mobilier inséré dans un projet, un projet commencé depuis un modèle.

## Types

Une extension de **types** apporte des types de murs, dalles, portes, fenêtres, poteaux, poutres ou escaliers avec ce dont ils sont faits : leurs couches, matériaux, profilés et paramètres. Installée, ses types entrent dans le projet ouvert, en une étape à annuler : un matériau du même nom déjà dans le projet est utilisé, tout autre est créé ; un type déjà présent exactement identique est réutilisé, et un du même nom qui diffère est ajouté à côté avec (2) après son nom. **Installées → Ajouter ses types à ce projet** les apporte dans un autre projet.

![Les détails d'une extension de types : sa licence marquée non commerciale](shots/extensions-types.webp)

Vos propres types vont dans un paquet depuis **Gérer → Paquets de types…** : cochez les types, donnez un nom au paquet, et **Exporter des types…** enregistre un fichier `.lapintypes`. **Importer des types…** dans le même dialogue en lit un, vous laisse choisir ses types et les ajoute de la même façon.

![Gérer → Paquets de types… : un type de mur choisi pour l'export](shots/type-packs-export.webp)

## Scripts

Le catalogue liste aussi des **scripts Python**. Dans l'application de bureau, vous pouvez en installer un dès maintenant ; il s'exécute quand Lapin aura sa console Python, et seulement quand vous le lancez. Avant de l'installer, ses détails indiquent ce qu'il demande (lire ou modifier le projet ouvert, accéder aux fichiers ou à internet). Un script peut faire sur votre ordinateur ce que vous pouvez faire, alors n'installez que des scripts d'auteurs de confiance. Le navigateur n'accepte que des packs de contenu.

![Les détails d'un script : ce qu'il demande, et qu'il n'est pas encore exécutable](shots/extensions-script.webp)

## Un autre catalogue

**Catalogue…**, en bas de la boîte de dialogue, définit l'adresse du catalogue : une adresse https, ou le chemin d'un `index.json` sur cet ordinateur ou dans un dossier partagé (le catalogue propre à une entreprise). Laissez vide pour le catalogue de Lapin. La flèche à côté relit le catalogue. Un catalogue à vous n'a pas besoin d'être signé : Lapin indique alors au-dessus de la liste qu'il **n’est pas signé par Lapin Extensions**, alors n'installez depuis lui que si vous faites confiance à qui le tient. Un catalogue dont la signature n'est pas valide n'est jamais lu.

## Publier les vôtres

Le catalogue a son propre site, **[store.lapinbim.com](https://store.lapinbim.com)** : une page pour chaque extension, avec ses images, ses versions, sa licence et ses téléchargements, et une page pour chaque auteur.

Tout le monde peut publier une extension. Exportez-la depuis Lapin (Mon mobilier → Exporter, Enregistrer comme modèle…, une bibliothèque de profilés, un cartouche), puis :

- **Sur la boutique** : connectez-vous sur store.lapinbim.com avec votre compte du [forum](https://forum.lapinbim.com), ouvrez **Your extensions** (vos extensions), remplissez le formulaire et téléversez le fichier et quelques captures d'écran. La boutique vérifie le fichier comme le fera Lapin et l'envoie en revue ; votre page montre la revue et, une fois publiée, combien de fois l'extension a été téléchargée. Une nouvelle version suit le même chemin.
- **Sur GitHub** : joignez le fichier à une release d'un de vos dépôts et ajoutez son manifeste par une pull request sur [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Son README explique comment, et ce que regardent les contrôles automatiques.

Un paquet de types vient de **Gérer → Paquets de types… → Exporter des types…** ; une liste de prix de **Listes de prix…**, avec le bouton d'export d'une liste de cet ordinateur (un fichier `.lapinprices`, ses prix marqués indicatifs).
