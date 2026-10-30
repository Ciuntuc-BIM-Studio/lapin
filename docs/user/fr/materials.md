---
title: Matériaux, textures et hachures
order: 90
summary: Réglez la couleur, les hachures et les joints de chaque matériau, appliquez-lui votre propre image, et apportez vos propres motifs de hachure .pat.
---

**Gérer → Matériaux** liste les matériaux du projet. Choisissez-en un pour le modifier.

![La boîte de dialogue Matériaux](shots/materials-dialog.webp)

## Ce que règle un matériau

- **Couleur (3D)** et les faces dessinées dessus en 3D. Lapin les devine à partir du nom, par exemple les assises d’une brique ou la grille d’un carrelage. Les **Extrémités** et le **Dessus et dessous** peuvent avoir un aspect différent des faces.
- **Hachure** pour chaque endroit où il est dessiné : coupé dans une coupe, coupé dans un plan, et vu dans une élévation. Chacune a son échelle, son angle, son alignement, son remplissage et sa plume.
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
