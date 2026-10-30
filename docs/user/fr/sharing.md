---
title: Travailler ensemble
order: 125
summary: Partagez le projet ouvert et travaillez-y en direct avec d’autres, depuis Lapin ou depuis un navigateur.
---

<!-- video sharing -->

Plusieurs personnes peuvent travailler dans le même projet en même temps, comme des joueurs qui rejoignent le serveur d’un jeu. L’un de vous partage le projet depuis Lapin sur son ordinateur : c’est l’**hôte**. Les autres le rejoignent en tant qu’**invités**, depuis Lapin sur leur propre ordinateur ou depuis un navigateur, avec le lien que l’hôte leur envoie.

Le projet reste sur l’ordinateur de l’hôte, et c’est le Lapin de l’hôte qui décide de chaque modification. Les invités voient le modèle évoluer, dessinent avec les mêmes outils et voient où se trouvent les autres.

## Partager le projet

**Fichier → Partager la session…**, indiquez votre nom, choisissez comment les invités rejoignent, puis appuyez sur **Partager** :

- les **éditeurs** modélisent comme vous ;
- les **observateurs** regardent et se déplacent, et ne modifient rien.

Lapin affiche un **lien** et un **code**. Envoyez le lien à qui rejoint depuis un navigateur, et le code à qui rejoint depuis Lapin. Quiconque les possède peut rejoindre jusqu’à ce que vous appuyiez sur **Arrêter le partage**. À chaque partage, le lien et le code sont nouveaux.

![Le partage : le lien, le code et les participants à la session](shots/share-dialog.webp)

Dans la même boîte de dialogue, vous pouvez rendre un invité éditeur ou observateur, ou **lui demander de partir**. Fermez-la, et la session continue. L’icône des personnes à côté des participants, dans la barre du haut, la rouvre.

Tant que le projet est partagé et qu’il a un fichier, Lapin l’enregistre toutes les 5 minutes. Seul l’hôte enregistre ; les invités peuvent exporter des fichiers IFC et 3D pour eux-mêmes.

## Rejoindre une session

- **Depuis un navigateur** : ouvrez le lien, indiquez votre nom et appuyez sur **Joindre**. Rien à installer.
- **Depuis Lapin** : sur l’écran d’accueil, ou dans **Fichier**, choisissez **Rejoindre une session…**, collez le lien ou le code et indiquez votre nom.

![Rejoindre depuis Lapin](shots/join-dialog.webp)

Une barre en bas indique dans quelle session vous êtes. **Quitter** vous ramène à votre propre Lapin. Si l’hôte arrête le partage ou ferme Lapin, Lapin vous le signale, et vous revenez avec **Retour à Lapin**.

## Qui est où

Les participants sont dans la barre du haut, chacun avec sa couleur. Cliquez sur l’un d’eux pour aller à la vue qu’il regarde.

![Les participants](shots/participants.webp)

En plan, vous voyez les curseurs des autres avec leurs noms, et ce qu’ils ont sélectionné, entouré de leur couleur.

![Le curseur et la sélection d’un invité dans le plan de l’hôte](shots/sharing-plan.webp)

## Chacun annule ses propres modifications

<kbd>Ctrl Z</kbd> annule votre dernière modification, pas celle de quelqu’un d’autre. Si quelqu’un l’a modifiée après vous, Lapin la laisse telle quelle et vous dit qui l’a fait. Votre annulation ne remonte alors pas plus loin que cette modification.

## Sûr par conception

- Tout ce qui passe entre l’hôte et les invités transite par le relais de Lapin, **chiffré de bout en bout** avec la clé de l’invitation. Le relais le transmet et ne peut pas le lire.
- Un invité ne peut pas ouvrir, enregistrer ni lire de fichiers sur l’ordinateur de l’hôte, ni y ouvrir un autre projet.
- Lapin n’ouvre rien sur le réseau tant que vous ne partagez pas, et s’arrête quand vous arrêtez.
