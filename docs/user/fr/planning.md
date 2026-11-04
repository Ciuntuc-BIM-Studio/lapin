---
title: Planification
order: 97
summary: Le planning des travaux dans un diagramme de Gantt : tâches dans une WBS, jalons, liens avec décalages, chemin critique calculé sur un calendrier avec les jours fériés légaux de la Roumanie ; vers et depuis MS Project, IFC et Excel.
---

## L'onglet Planification

![L'onglet Planification](shots/ribbon-planning.webp)

- **Gantt** : ouvre le planning sous la vue. L'écran se divise en deux lignes : le modèle en haut, le planning en bas.
- **Calendrier…** : les jours et heures ouvrés, les jours fériés et la pause hivernale (ci-dessous).
- **Tâche** et **Jalon** : une nouvelle tâche ou un nouveau jalon avant la tâche sélectionnée dans le Gantt (ou à la fin).
- **Abaisser**, **Hausser**, **Lier les tâches** et **Délier les tâches** : la WBS et les liens des tâches sélectionnées.
- **Importer MS Project…**, **Exporter MS Project…**, **Exporter XLSX** et **Exporter IFC…** : le planning vers et depuis d'autres programmes (ci-dessous).

Un projet a un seul planning. Il est créé la première fois que vous ajoutez une tâche et commence le lundi suivant. Changez son début dans la barre du Gantt.

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

Lier les éléments du modèle aux tâches, un planning généré à partir du modèle, la simulation 4D du bâtiment qui grandit en 3D, l'avancement et les références, et l'estimation des coûts viendront dans les prochaines étapes.
