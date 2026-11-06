---
title: Matériaux, textures et hachures
order: 90
summary: Réglez la couleur, les hachures et les joints de chaque matériau, appliquez-lui votre propre image, et apportez vos propres motifs de hachure .pat.
---

**Gérer → Matériaux** liste les matériaux du projet. Choisissez-en un pour le modifier.

![La boîte de dialogue Matériaux](shots/materials-dialog.webp)

## Apparence : comment un matériau est rendu

L’onglet **Apparence** règle l’aspect du matériau en 3D et dans les logiciels de rendu vers lesquels vous exportez. L’onglet **Graphisme 2D** garde ses hachures et ses plumes, comme avant.

![L’apparence d’un matériau, avec son aperçu en direct](shots/material-appearance.webp)

- **Couleur** et **Texture** : une texture dessinée (brique, lames de bois, carrelage…) ou votre propre image. **W** et **H** fixent la taille d’un motif sur le modèle, en mètres ; **Rotation** le fait tourner.
- **Rugosité** : 0 est un miroir, 1 de la craie. **Aspect métallique** : 1 pour le métal nu (aluminium anodisé, acier, chrome), 0 pour tout ce qui est peint ou thermolaqué, le bois et la pierre.
- **Transparence** : ce qu’on voit au travers. **Verre (transmission)** et **IOR** (1,52 pour le verre) font réfracter le verre dans un logiciel de rendu.
- **Lumineux** émet de la lumière, comme le diffuseur d’une lampe.

La sphère montre le résultat aussitôt. Ce que vous ne réglez pas vient du nom : un matériau appelé *verre* ou *glass* est clair, *acier* ou *steel* métallique, *parquet* et *stratifié* sont des lames de bois. **Apparence d’après le nom** retire vos valeurs.

## Depuis la bibliothèque

**Depuis la bibliothèque…** propose des matériaux prêts : métaux (aluminium RAL 7016 anthracite, anodisé, acier, cuivre…), verre (clair, bleu, vert, gris, dépoli, réfléchissant), bois en lames, béton, brique, enduit et peinture, carrelage et pierre, plastique et tissu. Un clic en ajoute un au projet ; modifiez-le comme un autre.

![La bibliothèque de matériaux](shots/material-library.webp)

## Données : description, paramètres, comment il se mesure

L’onglet **Données** contient ce que le matériau dit de lui-même. Vous l’écrivez une fois ; chaque type où se trouve le matériau le lit : l’étiquette de composition, le métré, le devis, l’export IFC.

![Les données d’un matériau : sa description, ses paramètres, comment il se mesure et ses épaisseurs du catalogue](shots/material-data.webp)

- **Description** : le texte du descriptif, sur plusieurs lignes (*Panneaux de laine minérale, λ = 0,035 W/mK, fixés par chevilles*). Une étiquette de composition l’écrit à la place du nom.
- **Paramètres** : un nom, un genre et une valeur, comme sur les éléments. **Ajouter un paramètre courant…** propose **Conductivité thermique λ**, **Résistance à la vapeur μ** et **Masse volumique**, qui vont dans l’IFC sous leurs noms standard (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, en unités SI), ainsi que **Réaction au feu** et **Résistance à la compression**, qui restent les vôtres. Vos paramètres vont dans l’IFC comme *Lapin_Material*.
- **Mesuré par** dit comment **Exécution → Métré** (par matériau) et le devis le comptent : **surface** (m², une ligne par épaisseur : panneaux, isolants, membranes), **volume** (m³ : béton, grave, chape), **masse** (kg, d’après sa masse volumique) ou **pièces** (des panneaux de la taille indiquée). Vide, il se mesure comme ses éléments.
- **Épaisseurs du catalogue (mm)** : les épaisseurs dans lesquelles le fabricant le produit, par exemple *50, 80, 100, 150, 200, 240*. Les tableaux de couches de **Types de murs** et **Types de dalles** les proposent à côté de l’épaisseur (vous pouvez en taper une autre) et une couche d’une autre épaisseur est marquée d’un signe d’avertissement.

L’export IFC écrit la description comme Description du matériau et les paramètres comme ses jeux de propriétés ; l’import les relit, y compris depuis le fichier d’un autre logiciel.

## Un matériau pour tous les dormants et tout le verre

Les nouvelles portes et fenêtres sont faites de matériaux partagés, partie par partie : **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** et **Hardware**. Modifiez **Window frame** une fois et tous les dormants de fenêtre changent.

Pour d’autres matériaux, ouvrez **Architecture → Portes et fenêtres**, choisissez pour un type le **Matériau du dormant**, le **Matériau du verre** et les autres, puis cliquez sur **Utiliser pour toutes les fenêtres** (ou **Utiliser pour toutes les portes**). Chaque type de ce genre les prend, en une seule étape d’annulation. Les murs-rideaux ont aussi un matériau de cadre et un de verre.

![Les matériaux des parties dans le configurateur de portes et fenêtres](shots/joinery-materials.webp)

## Dupliquer, remplacer, sélectionner

- **Dupliquer** copie un matériau sous un nouveau nom.
- **Remplacer par** un matériau, puis **Remplacer** : chaque couche, type, porte, fenêtre et élément qui utilisait le premier utilise maintenant le second, en une seule étape d’annulation. **Remplacer et supprimer** retire aussi le premier. Sinon, un matériau utilisé ne peut pas être supprimé.
- **Sélectionner les éléments** sélectionne tous les éléments dont une partie est dans ce matériau.
- **Rechercher des matériaux** et la liste des catégories filtrent la liste.

## Matériaux pour le rendu

Twinmotion, Lumion, Enscape, Blender et d’autres logiciels de rendu remplacent les matériaux par leur nom. Lapin écrit chaque matériau **une seule fois, sous son propre nom**, avec sa couleur, sa texture, sa rugosité, son aspect métallique et son verre, et découpe le maillage de chaque élément par matériau.

1. Nommez vos matériaux comme vous les voulez dans le logiciel de rendu, par exemple *Aluminium RAL 7016 anthracite* pour tous les dormants et *Glass blue tinted* pour tout le verre.
2. **Fichier → Export 3D (glTF, USD, DAE)…** et choisissez **GLB** (recommandé : un fichier avec les textures, les noms exacts et le verre), **USDZ** ou **OBJ + MTL** (un .zip avec les textures ; les espaces des noms deviennent des tirets bas).
3. Importez le fichier dans le logiciel de rendu. Tous les dormants y partagent un matériau ; remplacez-le une fois par un métal du logiciel et tous changent.

![Une maison aux dormants gris foncé et au verre bleu](shots/materials-render-3d.webp)

L’export IFC nomme aussi le style de surface de chaque matériau d’après lui, avec sa transparence et sa façon de réfléchir.

## Ce que règle un matériau

- **Couleur (3D)** et les faces dessinées dessus en 3D. Lapin les devine à partir du nom, par exemple les assises d’une brique ou la grille d’un carrelage. Les **Extrémités** et le **Dessus et dessous** peuvent avoir un aspect différent des faces.
- **Hachure** pour chaque endroit où il est dessiné : coupé dans une coupe, coupé dans un plan, et vu dans une élévation. Chacune a son échelle, son angle, son alignement, son remplissage et sa plume. Une élévation ou une coupe hachure les faces qu’elle voit avec la hachure d’élévation, là où rien de plus proche ne les cache (Vues, Hachures de surface) ; tant que vous ne lui en donnez pas, son nom en propose une (brique BRICK, pierre STONE, bois BOARDS, bardage métallique VERTICAL), jamais le BATT de l’isolant.
- **Catégorie**, par exemple finition, isolation ou structure. Les styles de vue l’utilisent pour exclure les finitions d’une vue structurelle.
- **Membrane** dessine une couche mince comme une ligne pointillée au lieu d’une bande.
- **Dessiner les joints.** Par défaut, un matériau continue d’un élément à l’autre sans ligne, comme deux murs alignés ou un enduit sur deux niveaux. Cochez-la pour dessiner le joint.

## Votre propre image sur un matériau

Un matériau peut porter une image à vous : une photo de la vraie brique, du bois ou de la pierre.

1. Choisissez le matériau et cliquez sur **Image personnelle → Importer une image…**. Choisissez un fichier PNG ou JPEG.
2. Réglez **W** et **H** : la largeur et la hauteur, en mètres, d’une copie de l’image sur le modèle. L’image se répète sur les faces.

![L’image personnelle d’un matériau, appliquée à 1.2 × 1.2 m](shots/own-image.webp)

L’image s’affiche dans la vue 3D et est enregistrée dans le fichier du projet. Elle passe aussi dans les exports 3D : **GLB**, **glTF** et **USDZ** la transportent. **DAE** et **USDA** sont des fichiers uniques sans place pour les images ; ils gardent donc la couleur du matériau.

![La même maison avec l’image personnelle du matériau en 3D](shots/own-image-3d.webp)

Les images de plus de 2048 pixels sont réduites à cette taille à l’import. Le bouton corbeille retire l’image du matériau.

## Vos propres motifs de hachure (.pat)

**Importer .pat** lit un fichier de motifs de hachure AutoCAD. Chaque motif qu’il contient est ajouté au projet, prêt à être choisi comme hachure d’un matériau.

Lapin conserve aussi le fichier dans **Mes motifs** sur votre ordinateur. Dans n’importe quel autre projet, **Ajouter à ce projet** réimporte ces motifs. Un projet garde les motifs qu’il utilise, il s’ouvre donc de la même façon sur un autre ordinateur.

![Mes motifs : les fichiers .pat que vous avez importés, conservés pour chaque projet](shots/my-patterns.webp)
