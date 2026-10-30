---
title: Dossier du projet et émissions
order: 125
summary: Émettez les feuilles et le modèle dans un dossier du projet comme le demande l'ISO 19650, nommés selon la convention, avec un bordereau de transmission, après les vérifications qu'un bureau d'études fait.
---

## Le dossier du projet

**Manage → Project folder** fixe le dossier dans lequel le projet est émis : un dossier de votre ordinateur, un partage réseau, ou un dossier SharePoint ou OneDrive synchronisé sur le disque. Lapin ne demande aucun compte et ne parle à aucun serveur : il écrit des fichiers, et ce qui synchronise le dossier regarde votre agence. Le chemin est conservé dans le projet, il voyage donc avec le fichier ; un dossier qui n'est pas sur cet ordinateur est un avertissement, pas une erreur.

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

**Manage → Issue…** écrit une émission dans un conteneur (dans l'application de bureau seulement, puisqu'elle écrit des fichiers) :

![Émission : les feuilles, la révision, le statut, les vérifications](shots/cde-issue.webp)

1. Donnez un nom à l'émission, sa **révision** (`P01`, `P02`… tant qu'elle est préliminaire, `C01`… une fois contractuelle ; la suivante est proposée d'après la dernière émission), son **statut** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) et le **conteneur**.
2. Cochez les feuilles qui partent, le modèle en IFC, chaque feuille aussi en DXF, ou toutes les feuilles aussi dans un seul PDF. Le nom de chaque fichier est montré tel qu'il sera écrit.
3. Les vérifications s'exécutent à mesure que vous choisissez. Avec un constat bloquant, l'émission ne peut pas partir ; avec des avertissements seuls, cochez **Émettre quand même**.
4. **Émettre** écrit chaque feuille en PDF, l'IFC et le reste dans le conteneur, nommés selon la convention, calcule le hachage (SHA-256) de chaque fichier, écrit le **bordereau de transmission** (un PDF et un CSV : projet, émission, date, révision, statut, les fichiers) et un **manifeste** (JSON, avec tous les hachages) à côté, et enregistre l'émission dans le projet. Chaque feuille imprimée prend la révision et le statut de l'émission dans son cartouche, et sa table des révisions liste les émissions dans lesquelles elle est partie.
5. Le projet est ensuite enregistré comme une **version** au nom de l'émission (voir [Fichiers, versions et récupération](files.md)), pour qu'une émission puisse être rouverte, restaurée ou comparée au projet tel qu'il est maintenant.

Les émissions sont listées au bas du dialogue Project folder, chacune avec ses fichiers et **Ouvrir le dossier**.

## Remarques (BCF)

**Manage → Remarks…** garde les remarques faites sur le modèle — les vôtres et celles d’un relecteur — comme sujets BCF (le BIM Collaboration Format qu’échangent BIMcollab, Solibri, Navisworks et Revit), dans le projet, jamais dans l’IFC. Une remarque a un titre, une description, un type, un statut (Open, In progress, Resolved, Closed), une priorité, un responsable, une échéance et des étiquettes ; ses commentaires ; et ses points de vue — la caméra 3D, les éléments sélectionnés, la boîte de coupe en plans de coupe, et une capture.

![Remarques : la liste avec ses filtres, une remarque avec sa capture et ses commentaires](shots/remarks.webp)

- **Nouvelle remarque** en crée une depuis la vue 3D affichée : la caméra telle qu’elle est, les éléments sélectionnés, la boîte de coupe et une image prise de la vue. Remplissez les champs et **Créer**.
- La liste se filtre par un texte, un statut, une priorité, le responsable et **Ouvertes seulement**. Choisissez une remarque pour voir et changer ses champs, lire ses commentaires et répondre dans la case (**Commenter** ; cochez **Avec la vue 3D** pour joindre la vue telle qu’elle est à la réponse).
- **Afficher en 3D** ouvre le point de vue de la remarque : la vue 3D va à sa caméra, ses éléments sont sélectionnés (ceux que le projet a, par leur GlobalId IFC), et ses plans de coupe deviennent la boîte de coupe. Un commentaire avec sa propre vue a **Afficher** à côté.
- **Reprendre depuis la vue 3D** remplace le point de vue de la remarque par la vue telle qu’elle est maintenant.
- **Importer BCF…** lit un fichier `.bcfzip` (BCF 2.1) ou `.bcf` (BCF 3.0) : un sujet que le projet a déjà (le même GUID) est mis à jour — les champs du fichier l’emportent, et les commentaires faits ici que le fichier n’a pas restent —, les autres sont ajoutés. **Exporter tout**, **Exporter les ouvertes** ou **Exporter la sélection** écrivent un fichier BCF, 2.1 sauf si vous choisissez 3.0, à remettre au relecteur ou à déposer dans le conteneur Shared du dossier du projet. Les sujets gardent leurs GUID, donc un aller-retour par un autre outil revient aux mêmes remarques.

![Une remarque ouverte à son point de vue dans la vue 3D](shots/remark-viewpoint.webp)

Votre nom sur ce que vous écrivez se règle dans la case **Votre nom** du dialogue (il reste sur cet ordinateur).
