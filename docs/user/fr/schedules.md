---
title: Nomenclatures et graphiques
order: 95
summary: Listez portes, fenêtres, linteaux, pièces et tout ce qu’un module compte ; ajoutez vos propres lignes et colonnes, écrivez par-dessus les valeurs du modèle et tirez un graphique du tableau.
---

## Obtenir les quantités

Tout ce que Lapin modélise est mesuré à partir de sa géométrie : la longueur, la hauteur, la surface latérale brute et nette et le volume d’un mur, la surface et le volume de chaque couche, les surfaces et le volume d’un plancher, la surface et le périmètre d’une pièce, le nombre de portes. Il y a trois chemins :

- **Un élément.** Sélectionnez-le : la section **Métrés** du panneau des propriétés montre ce qu’il mesure, puis chacune de ses couches ou parties.
- **Tout le modèle.** **Coûts → Métré** crée (ou ouvre) le métré : tous les éléments que le modèle mesure, additionnés ligne par ligne, avec le nombre, la longueur, la surface brute et nette, le volume brut et net et le poids. Sa barre choisit ce qui est additionné ensemble : **Par catégorie et type** (par défaut : les murs de chaque type de mur, les planchers de chaque type…), **Par matériau** (chaque couche sous son matériau : la brique, l’enduit et l’isolant des murs séparément, le béton des linteaux), par niveau, ou combinés. **Tous les éléments** choisit les catégories qu’il liste (seulement les murs et les planchers, par exemple). Le métré suit le modèle comme toute nomenclature et accepte des lignes et des colonnes à vous (une provision, un prix unitaire).
- **Toute autre liste.** **Coûts → Nomenclatures…** crée toute nomenclature que l’édition propose : pièces, finitions, menuiseries, compositions de types, bois, armatures, coffrages, acier… La même liste est sous **+** à côté de **Nomenclatures** dans le navigateur du projet.

![Le métré groupé par catégorie et matériau](shots/quantity-takeoff.webp)

Par matériau, un matériau mesuré par **surface** (voir [Matériaux](materials.md)) a une ligne pour chaque épaisseur où il est posé, avec une colonne **Épaisseur (mm)** : *Laine minérale 150 mm : 84,20 m²*, *Laine minérale 200 mm : 31,00 m²*. Un matériau mesuré par volume n’a que ses m³, par masse ses kg, par pièces ses **Pièces**. Une colonne **Note** signale une couche qui n’a pas une épaisseur du catalogue du matériau. Le devis fait de même : un article sur un matériau se mesure comme le dit le matériau, une ligne par épaisseur. Un matériau sans règle se mesure comme avant.

**Coûts → Exporter XLSX** enregistre directement le métré comme classeur (téléchargé dans un navigateur).

## Les quantités sur une feuille

Une nomenclature se place sur une feuille comme une vue, et y est dessinée avec toutes ses lignes et valeurs, ses totaux et ses graphiques :

- **Placer sur une feuille…** dans la barre de la nomenclature : **Nouvelle feuille** crée une feuille A3 avec le tableau et l’ouvre, ou choisissez une feuille existante.
- Ou faites glisser la nomenclature du navigateur du projet sur une feuille ouverte : elle arrive là où vous la lâchez. Plans, coupes et façades se glissent de même.
- Ou **Placer une vue…** dans la barre de la feuille.

Sur la feuille, faites glisser le tableau pour le déplacer ; un tableau plus long que le cadre continue sur d’autres pages (la feuille le signale, et **la placer sur une nouvelle feuille** le continue). Le tableau suit le modèle : ajoutez un mur et les chiffres de la feuille changent. Il s’imprime avec **Imprimer** et va dans le PDF et le DXF de la feuille tel qu’il est dessiné.

![Le métré placé sur une nouvelle feuille](shots/takeoff-sheet.webp)

## Créer une nomenclature

Une nomenclature est un tableau que le modèle remplit : portes et fenêtres, linteaux, pièces, armatures, bois, coffrages et plus encore. Cliquez sur le **+** à côté de **Nomenclatures** dans l’arborescence et choisissez ce qu’elle liste, ou utilisez l’outil d’un module (Bois → Liste de bois, Installations de chantier → Nomenclature de coffrage…). Double-cliquez dessus dans l’arborescence pour l’ouvrir dans un onglet. Une nomenclature suit le modèle : déplacez un mur, ajoutez une fenêtre, et le tableau change avec eux.

Les nomenclatures des **menuiseries**, des **portes**, des **fenêtres** et des **linteaux** montrent une fiche par type, avec son dessin (la vue intérieure par défaut) et ses valeurs ligne par ligne. Les autres nomenclatures sont un tableau simple, une ligne par élément et une ligne de totaux à la fin.

Une porte ou une fenêtre retournée sur l’autre main de son type (**Main**, ou une seule flèche de retournement ; retournée dans les deux sens, elle est seulement tournée) est l’image en miroir de son type et reçoit sa propre fiche : son repère se termine par **-M** (D-M), son nom par « (mirrored) », et sa ligne **Opening**, son dessin et ses quantités sont ceux de l’autre main. Retournez-la à nouveau et elle revient sur la fiche de son type. La ligne **Opening** d’une porte nomme son sens d’ouverture comme le plan le montre : en regardant de la poignée vers les paumelles, un vantail qui s’ouvre à gauche est *swing left*, un qui s’ouvre à droite *swing right* ; une paire de vantaux est *double swing*. La gauche et la droite d’une fenêtre se voient de l’intérieur, du côté vers lequel s’ouvrent ses ouvrants : *Battant à droite (paumelles à gauche)* dans le configurateur, dont la grille est vue de l’extérieur. IFC écrit la même fenêtre comme gauche. Les dessins montrent chaque porte telle qu’elle est dans le modèle, vue de l’intérieur, du côté vers lequel elle s’ouvre. Une note sous les fiches le rappelle.

![Une nomenclature des menuiseries avec ses lignes de résistance au feu et de fabricant](shots/joinery-schedule.webp)

## Résistance au feu, fabricants et autres paramètres

Les paramètres d’une porte ou d’une fenêtre entrent d’eux-mêmes dans la nomenclature des menuiseries :

- **Résistance au feu.** La ligne **Résistance au feu** lit un paramètre nommé Fire rating, Fire Rated, FireRating ou Fire resistance. Mettez-le sur le type, pour que chaque porte du type l’ait. Dans **Architecture → Portes et fenêtres**, **Paramètres · dans les nomenclatures** → **+ Résistance au feu** ajoute le FireRating du type à Pset_DoorCommon (Pset_WindowCommon pour une fenêtre), là où IFC le range. Un paramètre que vous ajoutez vous-même à une seule porte, dans ses propriétés, s’affiche aussi.
- **Tout autre paramètre.** **+ Fabricant**, **+ Code produit**, **+ Site web**, ou un paramètre à vous, devient une ligne à part dans chaque nomenclature des menuiseries. La valeur propre d’une porte s’affiche à côté de celle de son type, quand les portes d’un type diffèrent.
- **Saisi dans la nomenclature.** Une valeur saisie dans la ligne d’un paramètre (en italique) va dans le type, et chaque porte du type la suit. Videz-la pour retirer le paramètre du type.

![Les paramètres du type dans le configurateur des portes et fenêtres](shots/joinery-params.webp)

**Types inutilisés.** Un type qu’aucune porte ni fenêtre du modèle n’utilise est laissé hors de la nomenclature. **Types inutilisés** dans la barre d’outils de la nomenclature les montre à nouveau, avec leur nombre.

## Vos propres lignes et colonnes

Toute nomenclature peut être remplie à la main, comme un tableur :

- **Ligne** ajoute une ligne à vous. Dans une nomenclature des menuiseries, elle a un libellé, et vous saisissez sa valeur pour chaque type (Fournisseur, Date de livraison…). Dans toute autre nomenclature, c’est une ligne entière, saisie cellule par cellule (une terrasse non modélisée, une provision) ; elle se place au-dessus de la ligne de totaux.
- **Colonne** ajoute une colonne à vous. Dans une nomenclature des menuiseries, c’est une fiche sans type derrière, chaque valeur saisie (une porte de garage commandée à part). Dans toute autre nomenclature, c’est une colonne après celles du modèle (Revêtement de sol, Notes…).
- **Toute cellule** du tableau sous le dessin peut être écrasée. Une valeur saisie par-dessus celle du modèle est marquée ; **↺** rend la valeur du modèle. Ce que vous saisissez reste avec sa ligne : les notes d’une pièce la suivent quand la liste est triée ou la pièce renommée. Dans un tableau **Rooms**, un numéro, un nom, une fonction, un département, un revêtement de sol ou une hauteur saisis sur la ligne d’une pièce vont dans la pièce elle-même, comme saisis dans ses propriétés ; l’étiquette du plan et tous les autres tableaux suivent.
- **×** à côté d’une ligne ou d’une colonne à vous la retire, avec ce qui y était saisi.
- Les flèches de l’en-tête d’un tableau **trient** ses lignes selon cette colonne : une fois en ordre croissant, encore une fois en décroissant, une troisième fois dans l’ordre du modèle. La ligne de totaux reste la dernière.

Tout s’imprime : sur une feuille, dans le PDF et le DXF, et dans les exports CSV et XLSX.

## Graphiques

**Graphiques** dans la barre d’outils de la nomenclature ouvre les graphiques à côté du tableau. **Nouveau graphique** en crée un à partir du tableau :

- **Colonnes**, **Barres**, **Secteurs** ou **Courbe**.
- **Catégories selon** : la colonne qui nomme chaque barre ou secteur (Fonction, Nom, Repère, Niveau…).
- **Comment** : **Somme**, **Moyenne**, **Minimum** ou **Maximum** d’une colonne (Surface, Total, Volume…), ou **Nombre de lignes**.
- **Au plus** : ne garde que les plus grandes catégories et additionne le reste en Autres.
- **Titre** : un titre à lui, ou celui qu’il forme (Somme de Surface par Fonction).

Le moteur dessine chaque graphique après les tableaux, il s’affiche donc dans la nomenclature, sur une feuille qui place la nomenclature et dans le PDF, exactement pareil. Il suit le modèle et ce que vous avez saisi : changez la surface d’une pièce ou ajoutez une ligne à vous, et le graphique change avec eux. **✎** modifie un graphique, **×** le retire.

![Une nomenclature des pièces avec une ligne à elle et un graphique en secteurs des surfaces par fonction](shots/schedule-chart.webp)

## Compositions des types

La nomenclature **Compositions des types** (**+** → **Compositions des types**) liste chaque type de mur et de plancher : son repère, son épaisseur totale, le nombre d'éléments qui l'utilisent, puis ses couches dans l'ordre avec leurs matériaux et leurs épaisseurs.

**Dessins des compositions**, dans sa barre, dessine chaque type après le tableau, coupé à travers ses couches à l'échelle de la nomenclature (1:50 si vous n'en choisissez pas une autre) : un mur comme le coupe un plan, un plancher comme le coupe une coupe, la couche 1 en haut. Chaque couche est remplie et hachurée comme son matériau en coupe, les faces du noyau sont dessinées plus épaisses, l'épaisseur totale est cotée et chaque couche est nommée à côté comme le tableau la nomme. Les dessins s'impriment avec le tableau, sur une feuille et dans le PDF. Une nouvelle nomenclature des compositions les dessine ; décochez **Dessins des compositions** pour garder le tableau seul.

Les dessins **Sur chaque fiche** (vue intérieure, vue extérieure, plan, coupe), dans les **Paramètres** d'une nomenclature, appartiennent aux fiches des nomenclatures de menuiseries ; un tableau simple n'a pas de fiches, ils n'y sont donc pas proposés.

![La nomenclature des compositions des types avec la composition de chaque type](shots/type-assemblies.webp)

Quand seul le résultat compte, cliquez sur **Éditeur du tableau** au-dessus du tableau d’édition sous la nomenclature : il se replie dans une barre en bas de la vue, et la nomenclature occupe toute la vue. Un clic sur la barre le rouvre. Lapin retient ce choix sur cet ordinateur. La flèche au bout des outils rapides les masque aussi sur les feuilles et les nomenclatures (voir [L’interface](interface.md)).

![La nomenclature seule : l’éditeur du tableau replié dans sa barre, les outils rapides masqués](shots/schedule-result-only.webp)

## Moulures de mur

La nomenclature **Moulures de mur** (**Architecture → Nomenclature des moulures de mur**, ou **Coûts → Nomenclatures…** → **Moulures de mur**) additionne les moulures et engravures de mur du projet par profil, genre et matériau : combien il y en a, leurs pièces (une moulure coupée par des ouvertures en fait plusieurs), leur longueur le long des faces et leur volume ; pour une engravure, le volume qu'elle retire au mur. Un total pour les moulures et un pour les engravures terminent le tableau. Sa barre les additionne d'abord **par profil**, **par niveau** ou **par type de mur**. Les retours comptent dans le volume, pas dans la longueur.

## Exporter

**CSV** et **XLSX** dans la barre exportent la nomenclature en tableur (un XLSX a une feuille par tableau). Dans l'application de bureau, Lapin demande où enregistrer le fichier et propose le nom de la nomenclature ; dans un navigateur, le fichier est téléchargé. Les types de murs et de planchers exportés s'enregistrent de la même façon.
