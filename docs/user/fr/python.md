---
title: Scripts Python
order: 127
summary: Piloter Lapin avec Python depuis le panneau Script, un extra pour développeurs : une console et un éditeur qui exécutent votre propre Python, chaque Exécution une seule étape d’annulation.
---

## Activer les Extras pour développeurs

Lapin se pilote avec Python : chaque outil est une commande, et un script envoie les mêmes commandes. Le panneau **Script** est un *extra pour développeurs*, désactivé tant que vous ne l’activez pas ; sinon, rien n’en apparaît.

Ouvrez **Fichier → Préférences…** et, dans **Général**, cochez **Extras pour développeurs**.

![Préférences : Extras pour développeurs](shots/python-preferences.webp)

- **Python** : le Python que le panneau exécute. Vide, Lapin en cherche un sur cet ordinateur (`python3`, `py -3` sous Windows, celui de python.org et celui de Homebrew). Il doit s’agir de **Python 3.11 ou plus récent** ; Lapin n’installe pas Python. Téléchargez-le sur [python.org](https://www.python.org/downloads/) si vous n’en avez pas, ou saisissez le chemin de celui que vous voulez (celui d’un environnement virtuel, par exemple).
- **Éditeur externe** : l’éditeur qu’utilise **Ouvrir dans l’éditeur externe**, sous forme de commande (`code` pour Visual Studio Code). Vide, l’éditeur de texte du système.

Python ne s’exécute que dans l’application de bureau : dans Lapin pour le navigateur, le panneau le dit.

## Le panneau Script

Ouvrez **Vue → Script**. Le panneau s’ouvre le long du bord inférieur de la fenêtre ; faites glisser son bord supérieur pour l’agrandir. Son en-tête indique quel Python il a trouvé, ou pourquoi il n’en a trouvé aucun.

![Le panneau Script après une exécution : quatre murs, une seule étape d’annulation](shots/python-panel.webp)

- **Les miens**, à gauche : vos propres scripts, de simples fichiers `.py` gardés dans le dossier de Lapin sur cet ordinateur. Cliquez sur l’un pour l’ouvrir ; **Nouveau script** part d’un exemple.
- **L’éditeur**, au centre : Python avec ses couleurs et son indentation. Tapez `lapin.` et il propose ce que Lapin possède (les commandes de tous les modules de votre édition et ses propres appels) ; `lapin.architecture.` propose les commandes d’architecture avec leurs arguments. Donnez un nom au script et **enregistrez-le** dans Les miens ; **Ouvrir dans l’éditeur externe** ouvre son fichier dans votre éditeur, et ce que vous y enregistrez revient dans le panneau.
- **La console**, à droite : ce qu’affichent les scripts, leurs erreurs avec la ligne où ils se sont arrêtés (marquée dans l’éditeur), et une ligne où taper du Python. **Entrée** l’exécute, **Maj+Entrée** commence une nouvelle ligne, **Haut** et **Bas** rappellent les lignes tapées avant. Un nom défini dans la console reste pour la ligne suivante et pour les scripts que vous exécutez.

Dans le panneau, `lapin` est le paquet `lapinbim`, déjà chargé. Le même script s’exécute depuis un terminal avec `import lapinbim as lapin`, tant que Lapin est ouvert.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Les longueurs sont en mètres et les angles en degrés, quelle que soit l’unité qu’affiche le projet.

## Exécuter, Arrêter, Redémarrer, Annuler

- **Exécuter** (**Ctrl+Entrée** dans l’éditeur) exécute le script. Tout ce qu’il fait est **une seule étape d’annulation**, au nom du script : un seul **Annuler** retire tout.

  ![Un seul Annuler a retiré les quatre murs de l’exécution](shots/python-undo.webp)

- Pendant qu’un script s’exécute, le modèle est au script : un outil utilisé dans la fenêtre est refusé avec *A script is editing the model* ; les vues, la sélection et les nomenclatures continuent.
- Un script qui échoue ne garde **rien** de ce qu’il a fait : le modèle est tel qu’avant **Exécuter**, et la console montre l’erreur et sa ligne.
- **Arrêter** arrête le script ; rien de ce qu’il a fait n’est conservé. Un script qui ne s’arrête pas en deux secondes voit son Python fermé.
- **Redémarrer** relance Python : les noms que gardait la console sont oubliés.

## Enregistrer ce que vous faites comme script

L'onglet **Enregistreur**, à côté de la console, écrit ce que vous faites dans la fenêtre en Python, une ligne par étape.

1. Cliquez sur **Enregistrer**. Un point rouge sur l'onglet indique que l'enregistrement est en cours.
2. Travaillez comme d'habitude : dessinez des murs, changez une propriété, supprimez quelque chose. Chaque étape apparaît aussitôt comme une ligne, par exemple `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Cliquez sur **Arrêter**, puis sur **Copier** (pour coller le script ailleurs) ou sur **Enregistrer comme script** (il va dans Les miens et s'ouvre dans l'éditeur).

![L'enregistreur après le dessin de deux murs](shots/python-recorder.webp)

Le script est écrit pour fonctionner aussi dans un autre projet :
- ce qu'une étape a créé est gardé dans une variable (`wall1`), que les étapes suivantes utilisent (`wall1.id`) ;
- le niveau sur lequel vous avez dessiné est retrouvé par son nom, sinon par son altitude (`lapin.model.storey("Ground floor", 0)`), et un type par son nom (`lapin.model.find("IfcWallType", "Brick 250")`) ;
- un autre élément qui se trouvait déjà dans le projet est désigné par son id (`lapin.model["…"]`), cette ligne ne fonctionne donc que dans ce projet ;
- avec **Sélection comme lapin.selection()** cochée, une commande sur ce que vous aviez sélectionné s'écrit `lapin.selection()` : le script travaille alors sur ce qui est sélectionné quand vous l'exécutez.

Un **Annuler** pendant l'enregistrement retire la dernière étape du script, et **Rétablir** la remet, si bien que le script contient ce que vous avez fait au final. Ce qu'un script fait n'est jamais enregistré, ni les questions qui ne changent rien (la lecture d'un tableau, une mesure).

![Le script enregistré, exécuté dans un nouveau projet : les deux mêmes murs](shots/python-recorder-new.webp)

## Les scripts du projet

Un script peut être gardé **dans le projet**, pour voyager avec le fichier : ouvrez-le ou écrivez-le dans l'éditeur et cliquez sur **Enregistrer dans le projet**. Il apparaît sous **Du projet**, à gauche. Enregistrer, renommer et supprimer un script du projet sont chacun une seule étape d'annulation, et les versions du projet gardent ses scripts. Dans le fichier `.lapin`, chacun est aussi un simple fichier texte, `scripts/<name>.py`, qui se lit donc sans Lapin.

**Un script du projet ne s'exécute jamais de lui-même**, ni à l'ouverture du projet ni à son enregistrement. La première fois que vous l'exécutez, puis chaque fois que son code a changé, Lapin demande :
- **Exécuter une fois** : il s'exécute cette fois, et l'exécution suivante demande de nouveau ;
- **Faire confiance** : il s'exécute, et ce code est retenu comme fiable sur cet ordinateur (un changement apporté à ce code redemande) ;
- **Annuler** : rien ne s'exécute.

Un script que vous enregistrez vous-même dans le projet est fiable dès son enregistrement. Vos propres scripts (Les miens) ne demandent jamais.

## Ce qui est sélectionné

`lapin.selection()` donne ce qui est sélectionné dans la fenêtre, sous forme d'éléments :

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Les champs d'un élément se lisent par leur nom et se modifient par affectation : `wall.height = 3` modifie le mur (une seule étape d'annulation, ou une partie de l'Exécution). `with lapin.step("Name"):` fait de plusieurs commandes une seule étape d'annulation, chacune répondant toujours aussitôt, si bien qu'une commande suivante peut utiliser ce qu'une précédente a créé.

## Sécurité

Un script peut faire tout ce que vous pouvez faire sur cet ordinateur : il s'exécute dans votre propre Python, avec vos droits. Exécutez des scripts que vous avez écrits ou en qui vous avez confiance ; un script du projet demande d'abord.

Un invité d'une session partagée ne peut ni exécuter de scripts sur votre ordinateur ni en mettre un dans votre projet, et un script ne se connecte qu'au Lapin de son propre ordinateur.

## Pour les développeurs

Ce qu’offre `lapin` (les commandes, les requêtes, les lots, le modèle, les événements) est décrit dans le guide pour développeurs, *Lapin from Python* (`docs/python.md` dans le dépôt de Lapin) ; le paquet est sur PyPI sous le nom `lapinbim`.
