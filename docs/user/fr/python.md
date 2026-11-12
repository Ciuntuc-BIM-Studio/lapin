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

## Sécurité

Un script peut faire tout ce que vous pouvez faire sur cet ordinateur : il s’exécute dans votre propre Python, avec vos droits. Exécutez des scripts que vous avez écrits ou en qui vous avez confiance.

Un invité d’une session partagée ne peut pas exécuter de scripts sur votre ordinateur, et un script ne se connecte qu’au Lapin de son propre ordinateur.

## Pour les développeurs

Ce qu’offre `lapin` (les commandes, les requêtes, les lots, le modèle, les événements) est décrit dans le guide pour développeurs, *Lapin from Python* (`docs/python.md` dans le dépôt de Lapin) ; le paquet est sur PyPI sous le nom `lapinbim`.
