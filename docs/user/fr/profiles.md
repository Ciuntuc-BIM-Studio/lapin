---
title: Bibliothèques de profilés
order: 57
summary: Les profilés dont votre travail a besoin, quelle que soit leur norme — le petit jeu EN de Lapin, vos tableaux et fichiers IFC, les dossiers de l'entreprise ; dessinés, avec leurs propriétés calculées et les valeurs du fichier vérifiées, copiés dans le projet à mesure de leur usage.
---

Lapin ne peut pas fournir tous les profilés d'acier du monde : il apporte un petit jeu européen et lit le reste dans ce que vous avez déjà : un catalogue en tableur (AISC Shapes Database, le tableau d'un fabricant, celui de votre entreprise), une bibliothèque IFC de profilés (celle de Bonsai, par exemple) ou tout modèle IFC, un contour fermé dessiné dans Lapin ou dans un DXF. **Gérer → Bibliothèques de profilés** les réunit tous.

![Bibliothèques de profilés](shots/profile-libraries.webp)

## Les bibliothèques

- **Intégrée** : *EN steel (Lapin)* : IPE, HEA, HEB, HEM, UPN, UPE, cornières égales et inégales, SHS, RHS et CHS finis à chaud en tailles courantes, et C, Z et Σ génériques formés à froid. Seulement les dimensions normalisées ; Lapin calcule toutes les propriétés.
- **Sur cet ordinateur** : les bibliothèques que vous importez, pour tout projet. La version navigateur les garde aussi, dans ce navigateur.
- **Dossiers de l'entreprise** : dans l'application de bureau, autant de dossiers locaux, réseau ou synchronisés de fichiers `.lapinprofiles` que vous voulez (**Ajouter un dossier…**). Un dossier est en lecture seule tant que vous ne cochez pas **écriture** ; le bouton d'enregistrement à côté y écrit alors la bibliothèque choisie, pour vos collègues.
- **Dans le projet** : seuls les profilés utilisés sont copiés, avec le nom de leur bibliothèque, pour que le fichier s'ouvre partout.

Tapez dans la recherche pour parcourir toutes les bibliothèques par nom, famille, norme ou type ; choisissez un profilé pour le voir dessiné avec ses dimensions et ses propriétés.

## Propriétés

À partir des dimensions, Lapin calcule l'aire, Iy, Iz, les modules élastiques et plastiques, les rayons de giration, les axes principaux et leur angle (cornières, Z, tout contour dissymétrique), la constante de torsion It et de gauchissement Iw là où une formule existe, la masse par mètre selon la densité du matériau et la surface à peindre par mètre. Elles concordent avec les tableaux publiés à environ 1 % (It et Iw des I laminés à 3 % ; Iw d'un U à ailes inclinées seulement à 20 %). Un contour quelconque n'a ni It ni Iw sauf si son fichier les donne.

Les valeurs d'un fichier sont gardées et montrées à côté de celles de Lapin. Un écart de plus de 2 % (aire, masse), 3 % (inerties, modules) ou 10 % (It, Iw) est marqué et va sur la liste de contrôle de l'import ; pour une colonne entière décalée d'une puissance de dix, Lapin signale une autre unité.

![Une valeur du fichier qui diffère de celle de Lapin](shots/profile-check.webp)

## Importer un tableau

**Tableau…** lit un CSV ou un XLSX, une ligne par profilé. Le sens et l'unité de chaque colonne sont devinés d'après son titre en anglais, roumain, allemand ou français (h, d, Höhe, hauteur, înălțime ; tw, s, Stegdicke ; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), unités comprises (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Un tableau avec Ix sans Iz est lu à l'américaine : x l'axe fort, en pouces. Corrigez dans la boîte de dialogue ce qui est faux ; Lapin retient votre choix pour le prochain fichier de même disposition.

![Les colonnes d'un tableau associées](shots/profile-table.webp)

Le type d'une ligne vient de sa colonne de type (W, HP, UPN, HSS, PIPE…) ou de son nom ; pour les autres, choisissez-le dans la boîte de dialogue.

## IFC

**IFC…** lit toutes les définitions de profilé d'un fichier IFC quelconque : une bibliothèque, ou un modèle de Tekla, Revit ou ArchiCAD, où les profilés utilisés par les éléments prennent le nom de leurs types. I, I dissymétrique, U, T, L, C, Z, tubes, rectangle, cercle et ellipse gardent leur type et leurs dimensions ; les profils quelconques, composés et dérivés deviennent des contours. Les propriétés mécaniques de `Pset_ProfileMechanical` (et celles d'IFC2x3) suivent ; les exportateurs divergent sur leurs unités et sur l'axe Y, alors Lapin tranche les deux d'après la géométrie. Les profils de ligne médiane et trapèze sont écartés, et Lapin le dit.

![Profilés lus dans un fichier IFC](shots/profile-ifc.webp)

**IFC** et **CSV** sous la liste écrivent la bibliothèque choisie (ou les profilés du projet) en bibliothèque de profilés IFC4 ou en tableau que Lapin sait relire.

## Un contour comme profilé

Dessinez la section avec Dessin en polylignes fermées et cercles (ceux à l'intérieur d'un autre sont ses trous), sélectionnez-les et cliquez sur **Depuis la sélection**, ou lisez le dessin d'un fabricant avec **DXF…**. Nommez-le, choisissez son matériau et sa bibliothèque, gardez-le. C'est ainsi qu'entrent les profilés aluminium et PVC, les sections préfabriquées et les formes en bois : le même stock, avec leur matériau et leur densité.

![Un contour dessiné devenu profilé](shots/profile-outline.webp)

**Dessiner son contour** fait l'inverse : le contour du profilé choisi en polylignes fermées dans la vue active, pour que Studio l'extrude ou le balaie.

## Utiliser un profilé

Dans **Acier → Types acier**, **Structure → Types structurels** (poteaux et poutres) et les profilés de la halle à portiques, choisissez **D'une bibliothèque de profilés…** et tapez pour chercher.

![Le sélecteur cherche dans toutes les bibliothèques](shots/profile-picker.webp)

Le profilé est copié dans le projet et le type le garde. Les corps sont modélisés à angles vifs, comme avant ; les congés et les ailes inclinées comptent dans les propriétés et dans l'export de structure.

Ailleurs, le profilé donne ce que l'outil peut utiliser : moulures, engravures et semelles filantes prennent son extérieur comme profil 2D (**D'une bibliothèque de profilés…** dans leur liste), montants et traverses de murs-rideaux et dormants de portes et fenêtres prennent sa largeur et sa profondeur.

Une bibliothèque peut changer après usage. Un profilé du projet différent de celui de même nom dans sa bibliothèque est marqué **plus récent en bibliothèque** ; **Mettre à jour depuis la bibliothèque** le remplace et tous les types faits avec lui suivent, en une seule étape d'annulation. Rien ne change sans votre demande. Les projets antérieurs gardent leurs types.
