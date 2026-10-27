---
title: Materials, textures and hatches
order: 90
summary: Set each material's colour, hatches and joints, put your own image on it, and bring your own .pat hatch patterns.
---

**Manage → Materials** lists the project's materials. Pick one to change it.

![The Materials dialog](shots/materials-dialog.webp)

## What a material sets

- **Colour (3D)** and the faces drawn on it in 3D. Lapin guesses them from the name, for example a brick's courses or a tile's grid. **Ends** and **Top and bottom** can look different from the faces.
- **Hatch** for each place it is drawn: cut in a section, cut in a plan, and seen in an elevation. Each has its scale, angle, alignment, fill and pen.
- **Category**, for example finish, insulation or structure. The view styles use it to leave the finishes out of a structural view.
- **Membrane** draws a thin layer as a dashed line instead of a band.
- **Draw joints.** By default, a material runs on from one element into the next without a line, like two walls in line or render over two storeys. Tick it to draw the joint.

## Your own image on a material

A material can carry a picture of your own: a photo of the real brick, timber or stone.

1. Pick the material and click **Own image → Import image…**. Choose a PNG or JPEG file.
2. Set **W** and **H**: how wide and how high, in metres, one copy of the picture is on the model. The picture repeats across the faces.

![A material's own image, laid on at 1.2 × 1.2 m](shots/own-image.webp)

The picture shows in the 3D view and is saved inside the project file. It also goes into the 3D exports: **GLB**, **glTF** and **USDZ** carry it. **DAE** and **USDA** are single files with no room for pictures, so they keep the material's colour.

![The same house with the material's own image in 3D](shots/own-image-3d.webp)

Pictures larger than 2048 pixels are reduced to that size when you import them. The trash button removes the picture from the material.

## Your own hatch patterns (.pat)

**Import .pat** reads an AutoCAD hatch pattern file. Each pattern in it is added to the project, ready to be chosen as a material's hatch.

Lapin also keeps the file in **My patterns** on your computer. In any other project, **Add to this project** brings those patterns in again. A project keeps the patterns it uses, so it opens the same on another computer.

![My patterns: the .pat files you imported, kept for every project](shots/my-patterns.webp)
