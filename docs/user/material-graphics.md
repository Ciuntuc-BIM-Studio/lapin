---
title: Material graphics
order: 91
summary: Set how each material is hatched, filled and outlined in cuts, plans and elevations, at real size and by scale; choose, edit and share material standards; graphics sets per view style; hatch patterns you draw yourself.
---

A drawing is only as good as the way its materials look. Each material says how it is drawn, and a **material standard** says it for all the materials that say nothing: the hatch of brick, the speckle of concrete, the loops of insulation. You can set all of it by hand, pick a standard, or do both.

## The three kinds of view

Every material has its graphics three times, in **Manage → Materials → 2D graphics**:

- **Cut (section)**: what a section, and a wall's or floor's cut layers anywhere, show.
- **Cut (plan)**: the same in a plan. It starts from the section's and changes only the fields you set.
- **Elevation**: the faces an elevation or a section sees beyond the cut.

![The graphics of a material: the three kinds of view, the preview and the hatch](shots/material-graphics-hatch.webp)

What you leave unset comes from the material's standard, then from its name, as before. A field you set has a bold label and an arrow to take it back to the default; the default shows as the field's grey text.

![A wall type cut in a section: brick hatched at 0.35 mm, a second pattern under it](shots/material-graphics-section.webp)

## Hatch

- **Pattern**, **Scale** and **Angle**. **Align**: *With the element* (in a plan the hatch turns with each wall: a wall at 30° has its 45° lines at 75°, its courses along it, and the layers' hatches run on from one layer to the next), *Fixed to the view* (the hatch keeps its angle on the sheet whatever the wall's, and runs on from one element to the next) or *Each layer* (it starts at each band, along its longest edge). The **Angle** turns it further. A project made since November 2026 aligns with the element unless a material says otherwise; a project saved before keeps its hatches fixed to the view, as it drew them, until you set a material to *With the element*. Slabs, columns and everything in a section have no axis in the view and draw *With the element* as *Fixed to the view*; a curved wall's hatch runs along its chord.
- **Insulation** (the BATT pattern) is the looped line of mineral wool: a loop on one face of the layer, a stroke across, a loop on the other face, again and again along the layer. In a plan it runs along each wall's own axis, at any angle and round a curved wall, between the layer's faces whatever its ends are cut to. In IFC it is written as the nearest line hatching, named BATT, which Lapin reads back.
- **Colour**: automatic is the category's colour.
- **Weight, mm**: the hatch lines' width on paper, from the ISO series (0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0) or any value from 0.05 to 5. It wins over **Pen**.
- **Line type**: dashed, dotted or one of your own.
- **Opacity** and **Halftone**: the hatch as a lighter tone, as a background.

![The background: fill and a second pattern](shots/material-graphics-background.webp)

## Background, outline and separators

- **Fill**: *none*, the *material colour* or a colour of your own, with its **opacity**. A fill with no hatch is a plain tone.
- **Second pattern**: a hatch under the first, with its own scale, angle, colour and weight, like Revit's background and foreground patterns.
- **Cut outline** and **Separator lines**: the colour, weight and line type of the line round a cut element, and of the line between two layers. Where two materials meet and both set a weight, the heavier wins.

![The cut outline and separator lines](shots/material-graphics-lines.webp)

The **preview** at the top draws the material between two plain neighbours (**Strip**) or as a band between layers (**Band**), at the scale you pick, by the same code as a view.

![The preview, at 1:50](shots/material-graphics-preview.webp)

## Several materials, and copying

Tick the boxes beside several materials and **Changes go to all** of them, in one undo step. **Copy graphics from…** takes another material's own graphics (section, plan and elevation) onto the material or the ticked ones.

![Two materials ticked: a change goes to both](shots/material-multi.webp)

![Copy graphics from another material](shots/material-copy-graphics.webp)

The same editor opens beside a layer's hatch swatch in **Wall types** and **Floor types**, so you can change a material where you choose it.

![The graphics editor beside a layer's swatch](shots/layer-swatch-graphics.webp)

## Real size and drafting patterns

A pattern is in **paper** millimetres (*Drafting*: the same spacing at every scale, as for a section's cut) or in **real** millimetres (*Real size*: a brick course is 63 mm tall at 1:20, 1:50 and 1:100, so it gets finer as the scale shrinks). **Units** in the material's graphics chooses; *pattern's own* follows the pattern. In real size, **Scale** 1 is the real object's size, 2 twice as large.

So a hatch that should keep its proportion to the drawing when the view's scale changes (set at 1:50, the same pattern on the object at 1:100 and 1:10) is a **Real size** one; a **Drafting** one keeps its look on paper instead, the same spacing in millimetres at every scale. A hatch you draft yourself (**Draft → Hatch**) has the same **Units** in its properties: choosing another keeps it looking as it does at its view's scale (scale 1 in drafting units at 1:50 becomes 50 in real size), and from then on it follows the scale as you chose.

Lapin ships real-size patterns: facing brick in three formats (240 × 63, 240 × 71 and 215 × 65), blocks, tiles 300 and 600 mm, boards, stone, roof tiles and a standing-seam metal roof. A new project's facades and floor finishes take them by the materials' names; a project made before keeps the drafting hatches it had.

**Min. hatch spacing** (**View settings**, **Manage → View styles**) stops a real-size hatch from turning into ink: lines closer than this on paper (default 0.7 mm) are not drawn, and the area shows as an outline with a plain fill. 0 draws every line.

### Hatch origin

A real-size pattern starts its courses at the element's base, so the bricks of one wall run on into the next. To start them elsewhere, select the element: **Hatch origin** in the properties panel takes two lengths, along the element and up from its base, and **automatic** is the base.

![The hatch origin row in the properties panel](shots/hatch-origin-panel.webp)

In a plan, an elevation or a section, a selected element also shows a **hatch origin grip**: drag it to where a course should start.

![The hatch origin grip on a selected wall](shots/hatch-origin-grip.webp)

## By scale

A drawing at 1:200 should not look like one at 1:20. **By scale** at the bottom of the graphics holds steps; each is a range (*from 1:*, *to 1:*) and the fields it changes. The first step that covers the view's scale wins and changes only the fields it sets. **Simplify at small scales** adds two steps (plain grey fill from 1:100, solid from 1:200) to edit. The eye on a step shows the preview at its scale.

![Steps by scale](shots/material-graphics-steps.webp)

### Small-scale cut

For a whole view, **Small-scale cut** in **View settings** (and each style in **View styles**) chooses what cut elements draw from a scale on (**from 1:**, default 1:200):

- **As material**: nothing changes.
- **Solid**: each element a single area in one colour.
- **Outline only**: no hatch, no fill, the element's outline.
- **Core only**: the core layer filled, the other layers empty, every line kept.

![The view settings: minimum hatch spacing, small-scale cut and material graphics set](shots/view-material-graphics.webp)

![The same in View styles](shots/view-styles-material-graphics.webp)

## Material standards

A standard is a list of rules, each matching material names (in several languages, or a category) and giving graphics. **Manage → Material standards…** (also **Materials → Material standards…**) lists them.

![The standards: Lapin's own and four norms](shots/material-standards.webp)

- **Built in:** *Lapin* (1.1, the default look; 1.0 is what projects made before 18.11.2026 keep), *ISO 128-50*, *DIN 1356*, *România (SR ISO 128-50)* and *BS 8888 / BS 1192*. The norms are drawn by Lapin from their general principles and marked **Indicative**: check them against the standard your drawings must follow. A standard is never chosen for you by your language or country.
- **Choose at a new project:** **Material standard** on the start screen, and in **Preferences → General → New projects** for every new project. A project made before draws as it did.

![Choosing the standard on the start screen](shots/material-standard-start.webp)

![In Preferences](shots/preferences-material-standard.webp)

- **Apply…** shows a preview: for each material the cut, plan and elevation looks before and after. Without **Overwrite graphics set by hand** a material with graphics of its own keeps them and the standard gives only the materials that have none. **Show only what changes** hides the rest. One undo step undoes it all.

![Applying a standard, with its preview](shots/material-standard-apply.webp)

- **Duplicate** a built-in standard to edit it (built-in ones are read-only). The editor lists the rules (the first that matches wins; drag, move up or down, duplicate, remove), each with its names per language, a category filter and the graphics it gives, with the same fields as a material. A standard also carries its own **.pat** patterns.

![A standard's rules](shots/material-standard-editor.webp)

- **Save the project's graphics as a standard…** makes a standard from what you set by hand on the project's materials.

![Saving the project's graphics as a standard](shots/material-standard-from-project.webp)

- **Export** and **Import…** move a standard as a file; the **.lapinstandards** file carries it with the office's other standards.
- **From the store**: search the catalogue by country and norm; **Preview** draws its reference sheet, **Install** keeps it on this computer, **Update** takes a newer version (the project does not change until you use it), **Use it** brings it into the project and opens the preview. **Publish to the store…** in a standard's editor saves the files and opens the author page.

![The store tab](shots/material-standard-store.webp)

## Graphics sets per view style

A **material graphics set** says how materials draw in the views that take it, over what the materials say themselves: concrete solid grey in a structural view, finishes without a hatch, insulation in halftone. **Manage → Material graphics sets** lists them. *Structure* and *MEP* are built in; make your own with **New set**, **Import…**, **Rename**, duplicate, export or delete.

![The material graphics sets](shots/material-graphics-sets.webp)

Each entry matches a material, a name expression or a category and gives graphics for the three kinds of view; the most particular entry wins. A view style names its set (**Material graphics** in **View styles**), and a view can name another or *none* (**View settings**). New projects' Structural and MEP styles use *Structure* and *MEP*.

## Subcategories and filters in Visibility / Graphics

In **Visibility / Graphics**, unfold a category (a wall) to see its **subcategories**: core, finishes, insulation, membranes and other layers. Each has **Line colour**, **Line weight**, **Fill**, **Cut hatch** and **Halftone**, so a view can draw the finishes without a hatch or the insulation lighter.

![A wall's subcategories with their graphics](shots/vg-subcategory-graphics.webp)

On the **Filters** tab a filter also sets **Cut hatch**, **Cut fill**, **Hatch colour**, **Hatch halftone**, **Cut line** and, for elevations, **Surface hatch** and **Surface fill**. The first filter in the list that takes an element gives each field.

![A filter that changes hatch and fill](shots/vg-filter-hatch.webp)

The strongest wins: an element's own, the view's filters, the subcategory, the view's graphics set, the small-scale cut, the material's step, the material, then the standard.

To change one element's hatch in one view only (a strip foundation's hatch scale, an elevation's hatching removed), select it and use **Graphics in this view…** (right-click, or Properties → **Graphics in view**): it is the strongest level of all. See [Graphics in this view](views.md#graphics-in-this-view).

## Hatch patterns

**Manage → Hatch patterns** lists the built-in patterns, **This project**'s own and **My patterns**. Besides **Import .pat** and **Export .pat**, **New pattern** makes one from its numbers: a family of lines is an **Angle**, an **Origin** (x, y), a **Delta** (x, y: the offset of the next line) and **Dashes** (**+ Dash**, **+ Gap**, **+ Dot**); **Add a family** crosses another. The preview and the real spacing show as you type, and **Units** and **Scale** say how it is laid. **Make pattern** saves it; renaming a pattern follows into every material, standard, set and filter that names it. A pattern in use is not deleted without asking.

![A pattern made from its numbers](shots/hatch-patterns-family.webp)

### Save as hatch pattern

Draw the pattern with lines, polylines, arcs and circles in any view (a brick bond, a tile joint), select them and type **SAVEHATCH**. Click two corners for the cell (or <kbd>Enter</kbd> for the drawing's bounds), then name it and choose **Drafting** or **Real size** units.

![Saving drawn lines as a pattern](shots/save-hatch.webp)

Straight lines and dots that repeat along the cell become exact line families, which a **.pat** file for another program carries. Arcs and circles make a tile pattern, which Lapin draws, DXF carries as lines and IFC as the nearest hatching.

![The pattern is in the project](shots/save-hatch-result.webp)

**Open in the editor** shows it, with its families.

![The saved pattern in the list](shots/hatch-patterns-saved.webp)

See also [Materials, textures and hatches](materials.md), [Views and sections](views.md) and [Drafting](drafting.md).
