---
title: Ascenseurs
order: 43
summary: Des ascenseurs depuis un configurateur de douze types dimensionnés selon EN 81-20 et EN 81-70, avec leur gaine, leur cuvette, leur réservation haute, leur local machine, leur cabine et leurs portes palières, les trémies dans les planchers traversés, des plans à chaque niveau, une nomenclature et l’IFC.
---

## Poser un ascenseur

**Architecture → Ascenseur** (<kbd>LF</kbd>) pose un ascenseur d’un clic dans le plan. Le clic donne le centre de sa gaine, et ses portes regardent vers le bas du plan (tournez-le avec 0°, 90°, 180° ou 270° dans la barre, ou plus tard avec sa poignée). L’ascenseur va du niveau du plan au niveau le plus haut du projet et s’arrête à chaque niveau entre les deux. Son configurateur s’ouvre aussitôt.

Tant que l’outil est actif, la barre de la ligne de commande et le panneau des propriétés montrent **le style** qu’il pose : l’un des douze types du catalogue ou l’un des styles propres au projet.

## Le configurateur

![Le configurateur d’ascenseur, avec le style accessible en fauteuil roulant](shots/elevator-configurator.webp)

Les styles sont à gauche. Un style remplit tous les champs, et vous pouvez ensuite modifier n’importe lequel à la main. Le catalogue contient ces types :

| Style | Personnes | Charge nominale | Cabine (mm) | Entraînement |
|---|---|---|---|---|
| Petit résidentiel | 4 | 320 kg | 900 × 1000 | sans local machine |
| Personnes standard | 6 | 450 kg | 1000 × 1250 | sans local machine |
| Personnes moyen | 8 | 630 kg | 1100 × 1400 | à traction, local machine en haut |
| Personnes grand | 10 | 800 kg | 1350 × 1400 | à traction |
| Personnes, fort trafic | 13 | 1000 kg | 1600 × 1400 | à traction, 1,6 m/s |
| Accessible en fauteuil roulant | 8 | 630 kg | 1100 × 1400, porte 900 | EN 81-70 type 2 |
| Monte-lits d’hôpital | 17 | 1275 kg | 1200 × 2300 | à traction, cabine traversante |
| Monte-charge | 26 | 2000 kg | 1500 × 2700 | à traction, 0,5 m/s |
| Hydraulique (faible hauteur) | 8 | 630 kg | 1100 × 1400 | vérin, local de la centrale à côté de la gaine |
| Sans local machine (MRL) | 13 | 1000 kg | 1100 × 2100 | machine dans la réservation haute |
| Panoramique en verre | 8 | 630 kg | 1100 × 1400 | cabine vitrée dans une gaine vitrée |
| Élévateur domestique | 3 | 250 kg | 800 × 950 | 0,15 m/s, cuvette peu profonde |

À droite, trois onglets :

- **Cabine et charge** : l’entraînement (à traction, sans local machine, hydraulique, élévateur domestique), la charge nominale et les personnes, la vitesse, le type d’accessibilité (EN 81-70 type 1, 2 ou 3), la largeur, la profondeur et la hauteur de la cabine, des parois de cabine vitrées, un ascenseur pompiers, un ascenseur d’évacuation. **Charge et personnes d’après la cabine** fixe la plus petite charge nominale que EN 81-20 donne à la surface de la cabine, et les personnes qu’elle porte.
- **Portes et arrêts** : un accès ou deux (cabine traversante), la largeur et la hauteur de la porte, ouverture centrale ou latérale et ses vantaux (deux ou trois vantaux coulissent l’un derrière l’autre), l’arrêt le plus haut et les niveaux où il s’arrête. À un niveau qu’il passe sans s’arrêter, le mur de la gaine reste fermé et il n’y a pas de porte palière.
- **Gaine** : le côté du contrepoids (ou du vérin d’un ascenseur hydraulique), la cuvette sous l’arrêt le plus bas et la réservation haute au-dessus du plus haut (les deux suivent l’entraînement et la vitesse tant que vous ne saisissez pas vos propres valeurs), le local machine (en haut pour la traction, en bas à côté de la gaine pour un hydraulique, aucun sinon), les murs de la gaine (un type de mur du projet, ou du béton de l’épaisseur que vous donnez), une gaine vitrée, la rotation.

![La coupe dans la cabine, de la cuvette au local machine](shots/elevator-section.webp)

L’aperçu montre le plan à l’arrêt le plus bas et, dans les autres onglets, une coupe dans la cabine avec tous les arrêts (les arrêts desservis en rouge, ceux qu’il passe en pointillés). En dessous, ce que dit EN 81 : une cabine plus grande que ne le permet sa charge nominale, plus de personnes que la charge ou la surface n’en portent, une cabine ou une porte accessible trop petite, un ascenseur hydraulique de plus d’environ 18 m de course. Plus bas, les dimensions intérieures et extérieures de la gaine, la surface de la cabine face à celle que permet sa charge, les arrêts, la course et les planchers que la gaine ouvre.

**Appliquer** modifie l’ascenseur en une seule étape d’annulation. **Enregistrer comme style…** le garde comme style du projet ; **Mettre à jour le style** l’enregistre de nouveau et tous les ascenseurs de ce style suivent. Un style du projet peut être renommé ou supprimé (ses ascenseurs restent tels qu’ils sont).

## Dans les dessins

![Un ascenseur en plan : la gaine coupée, la cabine avec sa croix, le contrepoids et les portes palières](shots/elevator-plan.webp)

La gaine ouvre chaque plancher qu’elle traverse, avec une trémie propre dans chacun. Quand vous déplacez, tournez ou copiez l’ascenseur, ou changez ses niveaux, les trémies suivent. Pour un plancher dessiné après l’ascenseur, **Architecture → Trémies de gaine** l’ouvre.

Chaque plan d’un niveau que l’ascenseur atteint le dessine : les murs de la gaine coupés, la cabine avec sa croix diagonale, le contrepoids avec sa diagonale (le vérin d’un hydraulique sous forme de cercle) et, aux niveaux où il s’arrête, les vantaux de la porte palière et de la porte de cabine, en pointillés là où ils coulissent à l’ouverture. Les coupes et les élévations dessinent toute la gaine, la cuvette, la dalle haute, le local machine, la cabine à l’arrêt le plus bas et les portes palières. En 3D, la cabine se tient à l’arrêt le plus bas, portes fermées.

![Un ascenseur en 3D, son local machine au-dessus de la maison](shots/elevator-3d.webp)

## Ses propriétés, sa nomenclature et l’IFC

![Les propriétés d’un ascenseur](shots/elevator-properties.webp)

Sélectionnez un ascenseur pour changer son arrêt le plus bas et le plus haut, cocher les niveaux où il s’arrête, choisir le type de mur de sa gaine, lire les avertissements d’EN 81 et ouvrir le **Configurateur…**. Ses champs (entraînement, charge, cabine, portes, cuvette, réservation haute…) sont en dessous ; ceux qui ne s’appliquent pas sont masqués (pas de local machine pour un ascenseur sans local machine, pas de côté du contrepoids pour un hydraulique). Faites glisser sa poignée centrale pour le déplacer et celle devant ses portes pour le tourner. **Déplacer**, **Copier**, **Faire pivoter** et **Symétrie** fonctionnent comme pour tout élément ; un ascenseur mis en symétrie passe son contrepoids de l’autre côté.

**Architecture → Nomenclature des ascenseurs** liste chaque ascenseur avec son type, son entraînement, sa charge nominale, ses personnes, sa cabine, ses portes, les arrêts qu’il dessert, les niveaux entre lesquels il circule, sa course, sa vitesse et sa gaine.

Chaque ascenseur a un type créé d’après son genre, sa charge et sa cabine (Personnes moyen 630 kg, 1100 × 1400) : les ascenseurs identiques le partagent, si bien qu’un article de prix posé sur le type (**Coûts → Affecter les articles…**) les chiffre tous, et la planification les retrouve par leur classe (IfcTransportElement).

En IFC, un ascenseur est un **IfcTransportElement** de type prédéfini **ELEVATOR**, d’un **IfcTransportElementType**, avec les jeux propres à l’IFC **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) et **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin relit son propre fichier comme un ascenseur natif, avec ses arrêts et les trémies des planchers.
