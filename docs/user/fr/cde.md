---
title: Dossier du projet et émissions
order: 125
summary: Émettez les feuilles et le modèle comme le demande l’ISO 19650, gardés dans le fichier du projet et, si vous en avez un, dans un dossier du projet, nommés selon la convention, avec un bordereau, après les vérifications d’un bureau d’études.
---

## Le dossier du projet

**Manage → Project folder** fixe le dossier dans lequel le projet est aussi émis : un dossier de votre ordinateur, un partage réseau, ou un dossier SharePoint ou OneDrive synchronisé sur le disque. Le dossier est **facultatif** : chaque émission et ses fichiers sont gardés dans le fichier du projet lui-même, donc un projet sans dossier émet tout autant, dans l'application de bureau et dans le navigateur. Lapin ne demande aucun compte et ne parle à aucun serveur : il écrit des fichiers, et ce qui synchronise le dossier est l'affaire de votre bureau. Le chemin est gardé dans le projet, il voyage donc avec le fichier ; un dossier absent de cet ordinateur est un avertissement, pas une erreur. Choisir le dossier demande l'application de bureau.

![Dossier du projet : le dossier, les conteneurs, la nomenclature](shots/project-folder.webp)

Dans le dossier, Lapin crée à la première émission les quatre conteneurs ISO 19650 : **WIP**, **Shared**, **Published** et **Archived**. Le code de statut suggère le conteneur : S0 va dans WIP, S1–S7 dans Shared, A, B et CR dans Published.

## Nomenclature

Les fichiers sont nommés selon l'ISO 19650-2 : **Projet-Émetteur-Volume-Niveau-Type-Rôle-Numéro**, puis le statut et la révision, par exemple `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. Dans le dialogue Project folder vous fixez le code du projet, l'émetteur (votre agence), le volume, et le niveau et le rôle par défaut. Le reste vient du projet :

- le **niveau** d'une feuille est l'étage que ses plans dessinent (`00` à ±0,00, `01` au-dessus, `B1` en dessous, `ZZ` quand elle en dessine plusieurs ou aucun) ;
- son **rôle** est celui de sa discipline (architecture A, structure S, MEP M, génie civil C) ;
- son **numéro** est fait des chiffres de son numéro de feuille (`A-101` devient `0101`) ;
- le modèle est `M3`, le bordereau `IE`.

## Vérifications

**Manage → Check** exécute, sur tout le projet, les règles qu'un bureau d'études vérifie à l'œil avant une émission. Un constat **bloquant** arrête l'émission ; un **avertissement** peut être ignoré. Cliquez un constat pour aller à la feuille, à la vue ou à l'élément.

![Vérification : les constats par règle](shots/cde-check.webp)

Bloquants : un cartouche sans numéro, sans titre ou sans dessinateur ; une vue sans échelle ; une vue hors du cadre ou sur le cartouche ; une feuille modifiée depuis sa dernière émission mais toujours à cette révision ; un numéro de feuille qui ne donne aucun numéro de fichier. Avertissements : vérificateur, date et phase vides ; un plan coloré par un schéma de pièces, ou avec beaucoup de hachures, sans légende ; un plan, une coupe ou une élévation sur aucune feuille ; un plan de situation sans flèche du nord ; des éléments sans étage, sans type ou sans matériau ; des portes et fenêtres sans étiquette ; des pièces sans numéro ; les propriétés IFC requises (IsExternal, LoadBearing, FireRating sur les portes) vides.

## Émettre

**Manage → Issue…** fait une émission, gardée dans le fichier du projet et, quand le projet a un dossier sur cet ordinateur, écrite aussi dans son conteneur :

![Émission : les feuilles, la révision, le statut, les vérifications](shots/cde-issue.webp)

1. Donnez un nom à l'émission, sa **révision** (`P01`, `P02`… tant qu'elle est préliminaire, `C01`… une fois contractuelle ; la suivante est proposée d'après la dernière émission), son **statut** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) et le **conteneur**.
2. Cochez les feuilles qui partent, le modèle en IFC, chaque feuille aussi en DXF, ou toutes les feuilles aussi dans un seul PDF. Le nom de chaque fichier s'affiche tel qu'il sera écrit. L'IFC est gardé dans le fichier du projet seulement si vous cochez **Garder l'IFC dans le fichier du projet** (un gros modèle pèse des dizaines de Mo par émission) ; décoché, il va dans le dossier du projet et le fichier du projet garde son nom et son hash. **Aussi dans le dossier du projet** écrit en plus les fichiers dans le conteneur (coché par défaut quand le projet a un dossier).
3. Les vérifications s'exécutent à mesure que vous choisissez. Avec un constat bloquant, l'émission ne peut pas partir ; avec des avertissements seuls, cochez **Émettre quand même**.
4. **Émettre** fait chaque feuille en PDF, l'IFC et le reste, nommés selon la convention, calcule le hash (SHA-256) de chaque fichier, fait le **bordereau** (un PDF et un CSV : projet, émission, date, révision, statut, les fichiers) et un **manifeste** (JSON, avec tous les hashes), les garde tous dans le fichier du projet et enregistre l'émission dans le projet. Chaque feuille imprimée porte dans son cartouche la révision et le statut de l'émission, et dans son tableau des révisions la ligne de l'émission, telle qu'émise.
5. Dans la même étape le projet est enregistré comme une **version** au nom de l'émission (voir [Fichiers, versions et récupération](files.md)), pour qu'une émission puisse être rouverte, restaurée ou comparée au projet tel qu'il est maintenant, et le projet reste enregistré, sans modifications en attente. Le dialogue montre alors l'émission telle qu'elle est partie — son nom, sa révision et ses fichiers, chacun à ouvrir — jusqu'à ce que vous le fermiez ou commenciez une **Nouvelle émission**.

![Le dialogue Émettre après une émission : l’émission telle qu’elle est partie, ses fichiers](shots/cde-issued.webp)

## Les émissions dans le fichier du projet

Les émissions sont listées au bas du dialogue Project folder, avec ce qu'elles occupent dans le fichier du projet (les fichiers identiques sont gardés une seule fois, quel que soit le nombre d'émissions qui les partagent). Cliquez sur une émission pour voir ses fichiers :

![Les émissions dans le fichier du projet : ce qu’elles occupent, une émission ouverte avec ses fichiers](shots/cde-issues.webp)

- un clic sur un fichier l'**ouvre** depuis le fichier du projet (le bureau l'ouvre avec son programme ; le navigateur le télécharge), ou **Enregistrer une copie…** ;
- **Télécharger en zip** donne tous les fichiers gardés d'une émission dans un zip, au nom de son bordereau : ce que vous envoyez quand il n'y a pas de dossier partagé ;
- **Publier dans un dossier…** (l'application de bureau) écrit une émission faite plus tôt dans le conteneur du dossier du projet, ou dans n'importe quel dossier choisi : émettre d'abord, choisir le dossier ensuite ;
- **Supprimer les fichiers gardés** fait sortir les fichiers d'une ancienne émission du fichier du projet au prochain enregistrement, pour garder le fichier léger ; l'enregistrement de l'émission, les noms de ses fichiers et leurs hashes restent ;
- **Ouvrir le dossier** montre le conteneur dans lequel l'émission a été écrite.

## Dans le navigateur

Le CDE fonctionne aussi dans le navigateur : Vérification, Émission, les fichiers des émissions (ouvrir, Télécharger en zip) et les deux sortes de remarques. Le fichier du projet contient tout et vous revient comme le fichier que vous enregistrez. Un dossier du projet, y publier et le parcourir demandent l'application de bureau.

## Remarques (BCF)

**Manage → Remarques sur le modèle…** garde les remarques faites sur le modèle — les vôtres et celles d’un relecteur — comme sujets BCF (le BIM Collaboration Format qu’échangent BIMcollab, Solibri, Navisworks et Revit), dans le projet, jamais dans l’IFC. Une remarque a un titre, une description, un type, un statut (Open, In progress, Resolved, Closed), une priorité, un responsable, une échéance et des étiquettes ; ses commentaires ; et ses points de vue — la caméra 3D, les éléments sélectionnés, la boîte de coupe en plans de coupe, et une capture.

![Remarques : la liste avec ses filtres, une remarque avec sa capture et ses commentaires](shots/remarks.webp)

- **Nouvelle remarque** en crée une depuis la vue 3D affichée : la caméra telle qu’elle est, les éléments sélectionnés, la boîte de coupe et une image prise de la vue. Remplissez les champs et **Créer**.
- La liste se filtre par un texte, un statut, une priorité, le responsable et **Ouvertes seulement**. Choisissez une remarque pour voir et changer ses champs, lire ses commentaires et répondre dans la case (**Commenter** ; cochez **Avec la vue 3D** pour joindre la vue telle qu’elle est à la réponse).
- **Afficher en 3D** ouvre le point de vue de la remarque : la vue 3D va à sa caméra, ses éléments sont sélectionnés (ceux que le projet a, par leur GlobalId IFC), et ses plans de coupe deviennent la boîte de coupe. Un commentaire avec sa propre vue a **Afficher** à côté.
- **Reprendre depuis la vue 3D** remplace le point de vue de la remarque par la vue telle qu’elle est maintenant.
- **Importer BCF…** lit un fichier `.bcfzip` (BCF 2.1) ou `.bcf` (BCF 3.0) : un sujet que le projet a déjà (le même GUID) est mis à jour — les champs du fichier l’emportent, et les commentaires faits ici que le fichier n’a pas restent —, les autres sont ajoutés. **Exporter tout**, **Exporter les ouvertes** ou **Exporter la sélection** écrivent un fichier BCF, 2.1 sauf si vous choisissez 3.0, à remettre au relecteur ou à déposer dans le conteneur Shared du dossier du projet. Les sujets gardent leurs GUID, donc un aller-retour par un autre outil revient aux mêmes remarques.

![Une remarque ouverte à son point de vue dans la vue 3D](shots/remark-viewpoint.webp)

Votre nom sur ce que vous écrivez se règle dans la case **Votre nom** du dialogue (il reste sur cet ordinateur).
## Remarques sur les feuilles

Un vérificateur ouvre un PDF émis dans Acrobat, Bluebeam, Aperçu ou Drawboard et l’annote : notes, texte, surlignages et barrés, traits au crayon, rectangles, cercles, flèches, nuages, tampons, mesures, réponses. **Manage → Remarques sur les planches…** relit ces marques dans le projet comme remarques sur la feuille :

![Remarques : la liste par feuille, un PDF lu](shots/cde-remarks.webp)

- **Lire les remarques du PDF…** choisit le fichier. La feuille à laquelle il appartient vient de l’émission (le manifeste à côté du fichier, ou le registre des émissions du projet), sinon de la convention de nom du fichier ; quand rien ne correspond, vous choisissez la feuille. Les pages d’un PDF de jeu vont à ses feuilles dans l’ordre. Ce qui a été trouvé est affiché — pages, annotations, comment la feuille a été associée, les avertissements — et **Importer** fait entrer les remarques. Relire le même fichier met à jour ses remarques au lieu de les doubler ; une remarque que vous avez résolue reste résolue. Un fichier renommé en route est tout de même associé : les octets émis sont ceux par lesquels commence le fichier du relecteur.
- **Parcourir le dossier du projet** (l’application de bureau) liste les PDF émis dans Shared et Published, lesquels ont changé après leur émission et si leurs remarques ont été lues depuis.
- La liste est groupée par feuille et filtrée par état : **ouvertes**, **résolues**, **fermées**. Un clic ouvre la feuille avec la remarque devant. Chaque remarque peut être résolue, fermée, rouverte ou supprimée ; l’état « Completed » ou « Accepted » du vérificateur arrive comme résolue.

![Une feuille avec ses remarques par-dessus](shots/cde-remarks-sheet.webp)

Sur la feuille, les remarques sont dessinées sur le papier à leur place : le contour dans la couleur de révision, un repère avec les initiales de l’auteur, le texte dans une infobulle ; celle de devant a sa fiche avec le texte, qui et quand, les réponses et l’état. Le bouton de la barre de la feuille les masque ou les affiche, et le navigateur du projet compte les ouvertes à côté de la feuille. C’est une superposition pour l’écran seulement : une feuille s’imprime et s’émet sans ses remarques.

Lapin lit le PDF lui-même, sans autre programme : fichiers classiques et fichiers enregistrés avec des flux de références croisées, enregistrements incrémentaux (ce que le programme du vérificateur ajoute), texte dans tout encodage. Un fichier qu’il ne peut pas lire en entier donne ce qu’il peut, avec un avertissement. Lapin n’écrit jamais dans le PDF.
