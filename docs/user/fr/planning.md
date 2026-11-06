---
title: Planification
order: 97
summary: Le planning des travaux dans un diagramme de Gantt : tâches dans une WBS, jalons, liens avec décalages, chemin critique calculé sur un calendrier avec les jours fériés légaux de la Roumanie ; la simulation 4D du bâtiment qui grandit jour après jour, sur les vues, les feuilles et en vidéo ; l’avancement marqué sur le chantier, depuis un téléphone aussi, face à une planification initiale; vers et depuis MS Project, IFC et Excel.
---

## L'onglet Planification

![L'onglet Planification](shots/ribbon-planning.webp)

- **Générer le planning…** : un planning entier fait à partir du modèle, en une seule étape (ci-dessous).
- **Gantt** : ouvre le planning sous la vue. L'écran se divise en deux lignes : le modèle en haut, le planning en bas.
- **Calendrier…** : les jours et heures ouvrés, les jours fériés et la pause hivernale (ci-dessous).
- **Tâche** et **Jalon** : une nouvelle tâche ou un nouveau jalon avant la tâche sélectionnée dans le Gantt (ou à la fin).
- **Abaisser**, **Hausser**, **Lier les tâches** et **Délier les tâches** : la WBS et les liens des tâches sélectionnées.
- **Ajouter à la tâche…** (TA) et **Éléments sans tâche** : les éléments du modèle dans les tâches (plus bas).
- **Simuler**, **Feuille des étapes…**, **Gantt sur feuille** et **Exporter la vidéo…** : la simulation 4D, le modèle tel que le planning l'a un jour donné (plus bas).
- **Marquer terminé** (MD), **Marquer commencé** (MS), **Marquer non commencé**, **Enregistrer la planification initiale**, **Effacer la planification initiale** et **Importer l’avancement…** : l’avancement et la planification initiale (plus bas).
- **Importer MS Project…**, **Exporter MS Project…**, **Exporter XLSX** et **Exporter IFC…** : le planning vers et depuis d'autres programmes (ci-dessous).

Un projet a un seul planning. Il est créé la première fois que vous ajoutez une tâche et commence le lundi suivant. Changez son début dans la barre du Gantt.

## Générer le planning à partir du modèle

![Générer le planning : les modèles, l'onglet Début et l'aperçu](shots/planning-generate.webp)

**Planification → Générer le planning…** (aussi dans la barre du Gantt) fait un planning complet à partir du modèle : une tâche par étape des travaux et, là où l'étape avance niveau par niveau, une par niveau, chaque tâche ayant déjà ses éléments. À gauche, les **modèles** : **Ossature en béton armé**, **Maçonnerie porteuse**, **Hall en acier** et **Ossature bois**. Lapin marque *suggéré* celui que demande le modèle : le hall en acier s'il en a un, l'ossature bois si ses murs sont à ossature, la maçonnerie porteuse s'il n'a pas de poteaux, sinon l'ossature en béton armé. Au-dessus, les styles du projet (**Enregistrer comme style…** garde ce que vous avez réglé).

À droite, l'**aperçu** : les tâches que ferait le générateur, en barres, chacune avec la quantité qui a donné sa durée, et dessous les comptes :

- **Éléments liés** : les éléments pris par les tâches, sur tous ceux qui ont besoin d'une tâche. C'est le chiffre le plus important. S'il en reste dehors, **Montrer les éléments sans tâche** les sélectionne dans la vue et **Une étape pour eux** ajoute une étape avec leurs classes.
- **Durées par les cadences** et **Durées par défaut (sans cadence)** : combien de tâches une cadence a minutées, et combien ont pris les jours de leur étape.
- **Coffrages et échafaudages sur les tâches de leurs supports** (ci-dessous).

**Appliquer** écrit le planning en une seule étape à annuler. L'écran se divise alors : le modèle en haut, le Gantt en bas.

### L'onglet Début

- **Début** : le premier jour du planning.
- **Découper par niveau** : une tâche par niveau pour les étapes qui avancent niveau par niveau (gros œuvre, maçonnerie, finitions) ; décoché, une tâche par étape.
- **Démolitions d'abord** : les éléments que les phases démolissent vont dans une tâche **Démolition** avant toutes les autres (type IFC DEMOLITION). Les éléments existants qui restent n'ont pas de tâche.
- **Coffrage** et **Échafaudage** : si ces éléments provisoires vivent selon les tâches de leurs supports (ci-dessous).
- **Garder les tâches faites à la main** : quand le planning est généré de nouveau (ci-dessous).

### L'onglet Séquence

![L'onglet Séquence : les étapes dans l'ordre](shots/planning-generate-sequence.webp)

Les étapes du modèle, dans l'ordre. Chacune peut être désactivée, renommée et montée ou descendue avec ses flèches. Elle dit si elle se fait **une fois** pour le bâtiment ou **par niveau**, et comment elle suit l'étape précédente au même niveau : fin à début (FS), ou début à début (SS) avec un décalage, pour les finitions qui montent derrière le gros œuvre. Le crayon ouvre ce que construit l'étape : les classes d'éléments, choisies parmi celles du modèle, et leur **Partie** (entiers, leur âme ou leurs finitions).

Les liens viennent de l'ordre : chaque tâche suit l'étape précédente au même niveau et la même étape au niveau inférieur. En maçonnerie porteuse et en ossature bois, les murs d'un niveau attendent aussi le plancher du niveau inférieur.

Chaque tâche est faite avec une **règle** (ses classes, son niveau) et garde donc ses éléments : un mur dessiné après la génération entre seul dans sa tâche.

### L'onglet Durées

![L'onglet Durées : les cadences et, par étape, cadence, équipe et jours](shots/planning-generate-durations.webp)

La durée d'une tâche vient de la quantité de ses éléments (du métré : le volume de l'âme, la surface des finitions, les portes et fenêtres à la pièce, l'acier au poids) et d'une **cadence de production** :

*durée = quantité × heures de main-d'œuvre par unité ÷ (équipe × heures par jour)*, en jours ouvrés entiers. Par exemple 4,5 m³ de maçonnerie à 4 heures par mètre cube, avec une équipe de 2 qui travaille 8 heures : 18 ÷ 16 = 1,125, donc 2 jours.

Les cadences sont les vôtres : Lapin ne livre aucun recueil. **Importer des cadences…** lit votre tableau d'un fichier CSV ou XLSX : une colonne pour le code, une pour le texte, une pour l'unité (m3, m2, m, u, kg, t…), une pour les heures par unité (ou une productivité en unités par heure) et, si vous voulez, une pour l'équipe. **Modèle (CSV)** enregistre les colonnes avec quelques lignes de valeurs approximatives écrites par Lapin, chacune marquée *indicative*. **Utiliser les valeurs indicatives de Lapin** les met dans le projet pour essayer le générateur ; la fenêtre avertit alors que les durées viennent de valeurs indicatives.

Dessous, chaque étape choisit sa **Cadence**, son **Équipe** (0 : celle de la cadence) et ses **Jours**, pris quand aucune cadence ne donne la durée. L'onglet Calendrier montre le calendrier dans lequel les durées sont comptées.

### Généré de nouveau

Rouvrez le générateur et cliquez sur **Appliquer** : le planning est refait sur les mêmes tâches, qui gardent leurs identifiants. Ce que vous avez changé à la main reste :

- une tâche générée que vous avez modifiée (nom, durée, dates, règle, partie) est **Gardée telle que modifiée**, coché dans ses Propriétés ; décochez pour la refaire depuis son étape ;
- les éléments ajoutés ou retirés à la main restent ses exceptions ;
- vos propres tâches et liens restent ; décochez **Garder les tâches faites à la main** pour ne garder que les tâches générées.

![Le planning généré : le modèle en haut, le Gantt en bas](shots/planning-generated.webp)

Dans les Propriétés, une tâche générée dit d'où vient sa durée : la quantité, la cadence et l'équipe, ou les jours de l'étape.

![Une tâche générée dans les Propriétés](shots/planning-generated-task.webp)

### Coffrages et échafaudages

Un coffrage ou un échafaudage des **Installations de chantier** n'a pas de tâche à lui : il vit selon les tâches de ce qu'il sert. Le coffrage est posé quand commence sa dalle, sa poutre ou son mur, et décoffré quand les jours de son type sont passés après leur fin (**Décoffré après**, en jours ouvrés : 21 pour les dalles et poutres, 7 pour les murs, poteaux et semelles, sauf si le type dit autre chose). L'échafaudage reste de la première tâche sur ses murs à la dernière, finitions comprises. Sélectionnez-en un : ses Propriétés disent quand il est **Sur le chantier** et selon quelles tâches il **Vit selon**.

## Le Gantt

![Le Gantt sous la vue 3D, une tâche sélectionnée](shots/planning-gantt.webp)

À gauche se trouve le tableau des tâches : le numéro de ligne, le code **WBS**, le nom, la **Durée** en jours ouvrés, le **Début** et la **Fin**, les **Prédécesseurs** et la **Marge** totale. À droite, chaque tâche est une barre sur l'échelle de temps :

- une **tâche** est une barre bleue, rouge lorsqu'elle est sur le chemin critique ;
- une **récapitulative** (une tâche avec des tâches en dessous) est une accolade sombre du début de sa première tâche à la fin de sa dernière ;
- un **jalon** est un losange.

Les jours non ouvrés sont grisés et aujourd'hui est une ligne en pointillés. **Jours**, **Semaines**, **Mois** et **Trimestres** changent l'échelle, tout comme <kbd>Ctrl</kbd> + la molette de la souris. Le bouton aux quatre coins montre tout le planning et **Aujourd'hui** défile jusqu'à aujourd'hui. **Chemin critique** active et désactive le rouge. La flèche à côté du nom d'une récapitulative replie et déplie les tâches en dessous.

Le Gantt est une vue comme les autres : il a un onglet et, sur le bureau, peut s'ouvrir dans une fenêtre à part pour un second écran.

### Modifier dans le tableau

Double-cliquez sur une cellule pour y écrire. <kbd>Entrée</kbd> garde la valeur et <kbd>Échap</kbd> y renonce.

- **Durée** : jours ouvrés ; 0 fait de la tâche un jalon.
- **Début** : la tâche commence au plus tôt ce jour-là (une contrainte, voir ci-dessous).
- **Fin** : la durée devient le nombre de jours ouvrés du début à ce jour.
- **Prédécesseurs** : comme dans MS Project, des numéros de ligne séparés par des virgules, chacun avec un type et un décalage : `3`, `5SS`, `7FF+2`, `2FS-1d`.

Le clavier fonctionne comme dans un tableau : <kbd>↑</kbd> et <kbd>↓</kbd> déplacent la sélection, <kbd>Insert</kbd> ajoute une tâche avant celle qui est sélectionnée (<kbd>Maj</kbd>+<kbd>Insert</kbd> un jalon), <kbd>Suppr</kbd> supprime les tâches sélectionnées avec ce qui se trouve en dessous, <kbd>Tab</kbd> abaisse et <kbd>Maj</kbd>+<kbd>Tab</kbd> hausse, <kbd>F2</kbd> renomme.

### Modifier sur le diagramme

- **Faites glisser une barre** pour déplacer la tâche. Elle commence alors au plus tôt le jour où vous la déposez. La date s'affiche pendant le glissement.
- **Faites glisser l'extrémité d'une barre** pour changer sa durée. Les jours ouvrés s'affichent pendant le glissement.
- **Lier deux tâches** : survolez une barre, puis faites glisser depuis le petit cercle à l'une de ses extrémités vers une autre barre. Les extrémités que vous joignez donnent le type : de la fin au début de l'autre, c'est fin à début (FS), début à début SS, fin à fin FF, début à fin SF.
- **Cliquez sur une flèche** pour changer le type et le décalage du lien (en jours ouvrés ; un décalage négatif est un chevauchement) ou pour le supprimer.

Chaque modification est une seule étape d'annulation.

## Une tâche dans Propriétés

![Une tâche dans Propriétés](shots/planning-task.webp)

Sélectionnez une tâche et Propriétés l'affiche : son nom, son genre IFC, sa durée, si c'est un jalon, ses dates et :

- **Contrainte** : **Dès que possible** (par défaut), **Début au plus tôt le** une date, ou **Doit commencer le** une date. Une tâche qui doit commencer avant la fin de ses prédécesseurs leur retire de la marge. Leur marge devient alors négative et ils deviennent critiques.
- **Travaux humides** : béton, chapes, enduits. Une tâche humide ne travaille pas pendant la pause hivernale du calendrier, lorsque la pause est activée.
- **Marge totale** et **Marge libre** : de combien de jours ouvrés la tâche peut glisser sans décaler la fin, ou sans décaler ce qui la suit. **Début au plus tard** et **Fin au plus tard** sont les dernières dates qui gardent la fin.
- **Prédécesseurs** : chaque lien entrant, avec son type et son décalage à modifier, ou à supprimer. **Successeurs** liste ce qui suit la tâche.

## Le chemin critique

Lapin calcule le planning après chaque modification. Un passage aller trouve les dates au plus tôt et un passage retour les dates au plus tard. Les tâches sans marge forment le chemin critique, et un retard sur l'une d'elles décale la fin. Un lien vers une récapitulative retient toutes les tâches en dessous. Un lien depuis une récapitulative part du début de sa première tâche et finit à la fin de sa dernière. Lapin refuse un lien qui formerait une boucle.

## Les éléments du modèle dans les tâches

![La tâche Maçonnerie sélectionnée : ses murs dessinés sélectionnés en 3D](shots/planning-links.webp)

Une tâche prend des éléments du modèle : le planning sait ainsi ce qui est construit et quand. Sélectionnez une tâche et ses éléments sont dessinés sélectionnés en 3D et dans les plans ; un double-clic sur sa barre les sélectionne et les cadre. Sélectionnez des éléments dans une vue et les lignes de leurs tâches sont marquées dans le Gantt. La colonne **Éléments** compte les éléments de chaque tâche (ceux d'une tâche récapitulative avec ceux des tâches en dessous).

### Une règle

![Les éléments d'une tâche dans Propriétés](shots/planning-task-elements.webp)

Sous **Éléments** dans Propriétés, cochez **Les trouver par une règle** et choisissez ce que prend la tâche, comme dans le filtre d'une nomenclature : **Classes**, **Niveaux**, **Types**, **Matériaux**, **Groupes** et **Phases**, chaque valeur avec le nombre d'éléments qui l'ont. Chaque condition posée doit être remplie ; une condition vide laisse tout passer. C'est la règle qui est gardée, pas les éléments : un mur dessiné demain au rez-de-chaussée entre de lui-même dans la tâche « Maçonnerie RDC ». **Règle d'après la sélection** fait une règle des classes et niveaux des éléments sélectionnés.

### Une partie des éléments

**Partie** dit ce que la tâche prend de chaque élément : les **éléments entiers**, leur **âme** (la couche porteuse), leurs **finitions** (toutes les couches sauf l'âme), les couches d'un matériau ou celles d'une catégorie de matériau (Finition, Isolant…). Ainsi un mur multicouche est monté dans une tâche Maçonnerie (son âme) et enduit dans une tâche Enduits (ses finitions). Un élément sans couches est sa propre âme et n'a pas de finitions.

Un toit se construit aussi en couches : sa structure (le support d’un toit plat, d’une voûte ou d’un toit sans charpente ; les chevrons ou fermettes d’un toit avec charpente) est son noyau, sa couverture (avec les toits des lucarnes) et ses liteaux ses finitions. Une tâche **Structure de toiture** (noyau) et une tâche **Couverture** (finitions) prennent chacune leur partie, une règle par matériau trouve le support par son béton, et la simulation et une vue à une date ne montrent la couverture qu’une fois posée. Les modèles du générateur construisent un toit en ces deux étapes, chacune minutée par l’aire de sa partie, et les descentes dans une étape à part (**Gouttières et descentes**) ; le bardage de toiture d’un hall en acier reste une seule étape.

### À la main

![La carte de la sélection sur la 3D, les deux tâches d'un mur marquées dans le Gantt](shots/planning-element-tasks.webp)

Tant que le Gantt est ouvert, une carte sur la vue du modèle dit combien d'éléments sont sélectionnés et dans quelles tâches ils sont. Glissez-la par sa poignée sur la ligne d'une tâche : les éléments entrent dans cette tâche. Maintenez <kbd>Maj</kbd> en lâchant pour les en retirer. **Ajouter à la tâche…** (<kbd>T</kbd> <kbd>A</kbd>, aussi dans l'onglet Planification) choisit la tâche dans une liste.

![Ajouter à la tâche](shots/planning-assign.webp)

Ce que vous ajoutez ou retirez à la main, ce sont les **exceptions** de la tâche : la règle reste telle quelle. Propriétés les compte, et le × à côté de chacune les oublie. Les Propriétés d'un élément listent ses tâches sous **Planning des travaux**, chacune avec un × pour l'en retirer. Chaque modification est un pas d'annulation.

### Éléments sans tâche

Dès qu'une tâche prend des éléments, la barre du Gantt dit combien d'éléments n'ont pas de tâche, ou ont des couches qu'aucune tâche ne prend ; un clic les sélectionne, prêts à être glissés sur une tâche. **Planification → Éléments sans tâche** fait de même. **Gérer → Vérification** les liste aussi, sous *Elements with a task* : un avertissement par élément, un clic le sélectionne. Les éléments existants qui restent n'ont pas besoin de tâche.

### En IFC et MS Project

L'export IFC écrit ce que chaque tâche construit comme IfcRelAssignsToProduct (l'élément, avec les tâches qui le construisent), et ce sur quoi agit une tâche de démolition, d'enlèvement ou de démontage comme IfcRelAssignsToProcess. La règle, la partie et les exceptions voyagent dans `Lapin_Task`, Lapin relit donc la règle ; un fichier d'un autre programme donne des tâches tenant les éléments qu'il leur affecte. MS Project XML les porte dans le champ Text1, nommé *Lapin elements*. Le classeur a les colonnes **Elements** et **Rule**.

## La simulation 4D

![La maison un jour de son planning : la dalle de l'étage en cours dans son coffrage, les murs au-dessus non commencés](shots/planning-simulation.webp)

**Planification → Simuler** (aussi **Simuler** dans la barre du Gantt) montre le modèle tel que le planning l'a un jour donné. Le Gantt s'ouvre sous la vue s'il n'est pas ouvert, avec le lecteur en haut et le jour comme une bande sur le diagramme :

- ce qui n'est **pas commencé** est masqué (ou montré comme un fantôme pâle, dans les réglages) ;
- ce qui est **en cours** est dessiné dans sa couleur (ambre) ;
- ce qui est **construit** est dessiné tel qu'il est ;
- ce qui est **en démolition** est rouge tant que sa tâche dure, puis disparaît ;
- les **coffrages et échafaudages** sont là, dans leur couleur (bleu), seulement entre les jours que leur donnent les tâches de leurs supports : le coffrage d'une dalle du début de son coulage à son décoffrage, un échafaudage de la première tâche sur ses murs à la dernière.

Un mur multicouche maçonné dans une tâche et enduit dans une autre grandit en deux temps : son noyau d'abord, son enduit ensuite. Les éléments qu'aucune tâche ne prend restent tels quels. Une carte sur la vue du modèle donne le jour, ce que veut dire chaque couleur et combien d'éléments sont dans chaque état.

### La lecture

![Le lecteur en haut du Gantt](shots/planning-simulation-bar.webp)

- **Lecture** (▶) fait défiler les jours à la vitesse choisie (de 1 à 60 jours par seconde) jusqu'à la fin ; **Pause** l'arrête.
- Les flèches de chaque côté reculent ou avancent d'**un jour** ou d'**une semaine**.
- Fais glisser le curseur, tape une date, ou clique et glisse sur l'échelle de temps du Gantt : la bande sur le diagramme et le modèle suivent.
- À côté de la date : les tâches en cours ce jour-là.

D'un jour à l'autre, seules les couleurs et la visibilité des éléments changent : rien n'est reconstruit, donc un grand modèle défile aussi bien qu'un petit.

### Ses couleurs

![Les réglages de la simulation](shots/planning-simulation-settings.webp)

L'engrenage ouvre les réglages de la simulation, gardés dans le projet : les couleurs de ce qui est en cours, en démolition, des coffrages et échafaudages et des fantômes ; si ce qui n'est **pas commencé** est masqué ou montré **en fantômes** ; si les **éléments sans tâche** sont montrés ou masqués ; et si les **couches construites à part** se montrent à part. **Couleurs par défaut** remet celles de Lapin.

### État à la date sur les vues et les feuilles

![L'État à la date d'un plan dans ses propriétés](shots/planning-view-date.webp)

Un plan, une coupe, une élévation ou une vue 3D enregistrée a dans ses propriétés **État à la date** : la vue dessine alors le modèle tel que le planning l'a ce jour-là, dans les mêmes couleurs, et ses feuilles l'impriment ainsi (le PDF montre ce que montre l'écran). Quand la simulation tourne, **la date du curseur** lui donne son jour. Une vue à une date dessine les états du planning, pas ceux de sa phase. Le × à côté de la date remet le modèle tel qu'il est. Une vue 3D avec une date montre ce jour même simulation arrêtée.

### La feuille des étapes

![Feuille des étapes : les dates des fins de chapitre](shots/planning-stages-dialog.webp)

**Planification → Feuille des étapes…** (aussi dans le lecteur) met le modèle à plusieurs dates côte à côte sur une nouvelle feuille. Il est dessiné depuis **la vue 3D, telle qu'elle est maintenant** (une image à chaque date) ou depuis un plan, une coupe ou une élévation, que le moteur dessine à chaque date. Les dates sont **la fin de chaque chapitre** (chaque tâche en haut de la WBS), **la fin de chaque mois**, ou des dates **choisies**. Chaque cadre porte sa date et les tâches en cours.

![Une feuille des étapes](shots/planning-stages-sheet.webp)

### Le Gantt sur une feuille

![Le Gantt sur une feuille](shots/planning-gantt-sheet.webp)

**Planification → Gantt sur feuille** met le planning sur une nouvelle feuille : le tableau (ligne, WBS et tâche, jours, début, fin) et les barres sur une échelle de temps, les tâches critiques en rouge, les récapitulatives en accolades, les jalons en losanges, les liens. Un planning plus haut que la feuille va sur autant de feuilles qu'il faut, son en-tête sur chacune. Il est dessiné en vecteurs, comme les vues, et s'imprime dans le PDF.

### Exporter la vidéo

![Exporter la vidéo](shots/planning-video.webp)

**Planification → Exporter la vidéo…** (aussi dans le lecteur) enregistre la vue 3D, d'où elle regarde maintenant, d'un jour à un autre, une image par **un jour** ou par **une semaine**, avec la date et les tâches en cours écrites dessous. Le fichier est une vidéo WebM, ou MP4 là où le système n'offre que cela (l'application macOS). Un navigateur qui n'enregistre pas de vidéo enregistre à la place les images en fichiers PNG dans un ZIP.

## L’avancement

![Le Gantt avec l’avancement : la colonne %, la part faite plus sombre dans chaque barre, la planification initiale dessous et l’écart](shots/planning-progress.webp)

À mesure que les travaux avancent, le planning dit où ils en sont et déplace ce qui reste :

- La **date d’état**, dans la barre du Gantt, est le jour jusqu’auquel l’avancement est déclaré. Le travail restant commence le jour ouvré suivant : une tâche non commencée ne peut pas commencer plus tôt, et une tâche en cours fait ce qui lui reste à partir de là. Le diagramme la dessine en ligne bleue.
- Le **% achevé** d’une tâche se saisit dans sa colonne ou dans les Propriétés. Sans saisie, il vient de ses éléments marqués sur le chantier : la quantité faite sur la quantité de la tâche (dans son unité, quand le générateur lui en a donné une), sinon leur nombre. Celui d’une tâche récapitulative vient de ses tâches, pondéré par leurs durées.
- Le **début réel** et la **fin réelle** se saisissent dans les Propriétés. Laissés vides, ils viennent des marques : le premier jour où un élément a été marqué, et le jour du dernier élément une fois tous terminés. Une tâche terminée reste sur ses dates réelles, hors du chemin critique.
- Une tâche en cours garde son début réel et fait la part de sa durée pas encore faite (arrondie au-dessus) après la date d’état ; les tâches qui la suivent se déplacent avec elle.

Les barres montrent la part faite plus sombre. Les Propriétés disent d’où vient un pourcentage et combien de jours ouvrés restent.

![L’avancement d’une tâche dans les Propriétés](shots/planning-progress-task.webp)

### Marquer les éléments sur le chantier

**Planification → Marquer terminé** (MD) et **Marquer commencé** (MS) marquent les éléments sélectionnés en 3D ou dans un plan, à la date d’état (aujourd’hui s’il n’y en a pas), dans chaque tâche qui les contient ; **Marquer non commencé** retire les marques. Les propriétés d’un élément sélectionné montrent son état sur le chantier avec les mêmes trois boutons et, dans chacune de ses tâches, le jour où il a été terminé ou commencé, avec un bouton qui le marque terminé dans cette tâche seule. Un mur multicouche dans une tâche de maçonnerie et une d’enduit est marqué dans les deux, ou dans une depuis sa ligne.

![Les marques d’un mur dans ses propriétés](shots/planning-element-marks.webp)

Sur le chantier, depuis un téléphone : rejoignez la session partagée du projet depuis le navigateur (**Fichier → Partager la session…** sur l’ordinateur qui l’a ouvert). Un invité venu seulement pour regarder peut tout de même marquer des éléments et saisir l’avancement d’une tâche : le marquage est la seule modification ouverte à tout invité. Les photos et les rapports sur les tâches sont pour l’édition premium.

### La planification initiale

**Planification → Enregistrer la planification initiale** garde les dates et la durée de chaque tâche telles qu’elles sont maintenant ; enregistrée de nouveau, elle est remplacée (une seule planification initiale : plusieurs et des scénarios sont pour l’édition premium). Le Gantt la dessine alors en barre grise fine sous la barre de chaque tâche (**Planification initiale** dans la barre d’outils la cache), ajoute la colonne **Écart** (les jours ouvrés dont la fin a bougé : positif est un retard, la ligne marquée en rouge) et dit dans sa barre combien de tâches sont en retard et de combien la fin a bougé. Les Propriétés donnent les dates initiales et les écarts de début et de fin. **Effacer la planification initiale** la retire.

### En retard et en avance dans la simulation

![La simulation à la date d’état : les fenêtres en retard sur la planification initiale](shots/planning-progress-simulation.webp)

Avec une planification initiale, la simulation compare : un élément non construit (ou non commencé) un jour où la planification initiale l’avait ainsi est **en retard** (cramoisi), et un élément en cours ou construit avant que la planification initiale ne le commence est **en avance** (vert). Jusqu’à la date d’état, les éléments restent sur leurs jours réels : un élément marqué terminé est construit le jour où il a été marqué. **Comparé à la planification initiale**, dans les réglages de la simulation, la désactive ; ses deux couleurs s’y choisissent aussi. Les vues et les planches à une date montrent la même chose.

### L’avancement dans les fichiers

- **Exporter XLSX** ajoute les colonnes **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** et **GlobalId** (le classeur est en anglais). Remplissez-les sur le chantier et relisez-les avec **Planification → Importer l’avancement…** : les lignes sont retrouvées par GlobalId, code WBS ou ID, et une *Status date* écrite au-dessus du tableau fixe la date d’état. Un CSV marche aussi.
- **MS Project XML** porte la date d’état, le pourcentage de chaque tâche, le début et la fin réels, la durée restante et la planification initiale (Baseline 0) ; les marques des éléments voyagent avec les éléments, dans *Lapin elements*.
- **IFC** les écrit dans l’IfcTaskTime de chaque tâche (l’heure d’état, le début et la fin réels, le temps restant, le degré d’achèvement), et la planification initiale comme un second IfcWorkSchedule de type BASELINE ; les champs propres à Lapin (le pourcentage saisi, les marques) vont dans `Lapin_Task`. Les deux reviennent aux mêmes dates.
- **Gérer → Versions → Comparer** dit, à côté des éléments, ce qui a changé dans le planning : la fin, les jours ouvrés, la date d’état, et les tâches ajoutées, supprimées, déplacées (de combien de jours ouvrés), dont la durée ou l’avancement a changé.

## Le coût des tâches

![Le Gantt avec la colonne Coût](shots/planning-cost.webp)

Avec un devis (**Coûts → Devis…**), le Gantt reçoit une colonne **Coût** : la part du devis de chaque tâche, d'après les lignes qui chiffrent ses éléments (celle d'un récapitulatif est celle de ses tâches). La barre d'outils montre le **budget** (les coûts directs du devis) et le coût **à date** (le coût de chaque tâche fois son % achevé) ; Propriétés montre le coût d'une tâche et la part réalisée. **Coûts → Coût dans le temps** dessine la courbe en S et le tableau mensuel (voir [Devis](cost.md)).

## Le calendrier

![Le calendrier de travail](shots/planning-calendar.webp)

**Planification → Calendrier…** définit :

- les **jours ouvrés** de la semaine (du lundi au vendredi par défaut) et les **heures** d'une journée (8 à partir de 08:00) ;
- les **jours fériés** : les jours fériés légaux de la Roumanie, calculés pour chaque année (y compris le Vendredi saint, Pâques et la Pentecôte orthodoxes), ou aucun. La liste en dessous montre les jours fériés d'une année pour les vérifier ;
- la **pause hivernale** pour les travaux humides, désactivée par défaut : du 15 décembre au 1er mars, sauf si vous fixez d'autres jours ;
- les **exceptions** : des jours chômés pour le projet (un chantier fermé), ou des jours travaillés qui ne le seraient pas (un samedi).

Les durées et les décalages comptent les jours ouvrés de ce calendrier.

## Échanger le planning

- **Exporter MS Project…** écrit un fichier XML MS Project. MS Project, ProjectLibre et GanttProject l'ouvrent, et Primavera P6 l'importe. Il contient les tâches avec leur WBS, durées, dates, contraintes et marges, les liens avec leurs types et décalages, et le calendrier avec chaque jour férié comme exception. **Importer MS Project…** lit un tel fichier à la place du planning du projet. Un planning écrit par Lapin puis relu garde les identifiants et les dates de ses tâches.
- **Exporter IFC…** (aussi sous **Fichier**) écrit le planning dans le fichier IFC avec le modèle : un IfcWorkSchedule, ses IfcTask avec leurs temps, les liens IfcRelSequence et l'IfcWorkCalendar. Importer un fichier IFC qui contient un planning l'importe aussi.
- **Exporter XLSX** écrit un classeur : le tableau du Gantt et les jours chômés du calendrier.

Les champs propres à Lapin pour lesquels les autres programmes n'ont pas de place (la contrainte d'une tâche, si elle est humide, la pause hivernale) voyagent dans l'IFC sous forme des jeux de propriétés `Lapin_Task` et `Lapin_Calendar`. MS Project n'a pas de tâches humides, la pause hivernale reste donc dans Lapin.

## La suite

Le coût dans le temps est dans [Devis](cost.md). Le nivellement des ressources, la valeur acquise, les situations de travaux mensuelles et plusieurs références et scénarios sont pour l'édition premium.
