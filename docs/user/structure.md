---
title: Structure
order: 50
summary: Columns, beams and footings, their types, and how they meet.
---

The **Structure** tab holds the frame and the foundations.

![The Structure tab](shots/ribbon-structure.webp)

## Frame

- **Column** (<kbd>CL</kbd>): click where it stands. A column runs from its level to the level above.
- **Beam** (<kbd>BM</kbd>): click its two ends. **Curved beam** (<kbd>BC</kbd>) follows an arc.
- **Columns at grid** places a column at every crossing of the grid axes. To draw the axes, use **Annotate → Grid axis** (<kbd>GR</kbd>).

## Foundations

- **Strip footing** (<kbd>SF</kbd>) runs under walls. Its outer layers are mitred at the corners.
- **Curved strip footing** (<kbd>CF</kbd>) follows an arc.
- **Pad footing** (<kbd>PF</kbd>): click under a column. While the tool is on, it snaps to the centres of the columns of the plan's level and of the level above.
- **Pad footings under columns** puts a pad footing under each selected column that has none, at its centre and turned with it. With no column selected, it takes the columns of the plan's level (in a foundation plan, those of the level above).

Draw foundations on the **Foundations** level, or give them a plan of their own.

**A strip footing is the concrete alone** unless you ask for the wall. While **Strip footing** or **Curved strip footing** is on, the command line's bar offers **Footing alone** (the default: a 600 × 400 mm concrete footing, a profile type made the first time) or **With wall** (the stem of a strip type on it, with its layers), and which strip type with a wall to use. The choice is kept. A footing already drawn changes with its type in **Structure types**.

![The strip footing tool's option in the command line: footing alone or with a wall](shots/structure-strip-option.webp)

**In the plans.** A footing lies under the floor, so a plan draws it dashed in the plan of the level it stands on, as a plan of a building shows what is under it. To see it cut and seen, use a **foundation plan**: in the project browser, the **+** beside **Floor plans** makes one for the active level (cut 0.50 m under it, seeing 2.50 m down).

**Drawing in a foundation plan.** It shows the level under it too, and what you draw there with a floor, wall, column or beam tool goes on that level (the Foundations of a new project, at −1.50); the footing tools keep the plan's own level. A pad footing drawn there under a column rises to the column's base; a strip footing's bottom is its type's under the level.

**A pad footing rises to its column.** A pad footing whose point lies within a column's section (to a millimetre) has its top on the column's base: the column's level plus its **Base offset**, the steps under it. Move the column, change its level or its base offset, and the pad follows. Under columns standing on one another it follows the lowest; of two as low, the one whose centre is nearest. A pad with no column over it stands on its own level, its type's **Bottom** under it. **Pad footings under columns** puts each pad in the highest level at or under its bottom (the Foundations of a new project), else in the lowest. A pad footing imported from an IFC file stays where the file puts it.

In **Properties** a pad footing shows its **Top** and **Bottom** as elevations and the column it follows. Type a **Top** to give it its own: it no longer follows the column. **At the column's base** links it back.

![A pad footing's level in Properties: at the base of its column](shots/structure-pad-level.webp)

![Columns, beams and pad footings in 3D](shots/structure-3d.webp)

## Types and corners

**Structure types** sets the sections of columns, beams and footings: concrete sections and steel profiles. A column's **Section** is a **Rectangle**, a **Circle** or a **Polygon**, a regular one given by its **Sides** and its **Diameter** across the corners (an octagonal post, as an imported IFC may have it). Click a type in the list to change its size or its name, then **Save type**: every element of that type follows; **New type** goes back to making one.

Beams and footings join by themselves where they meet, a corner as a mitre. A beam drawn from column axis to column axis stops at the columns' faces; its length, volume and stirrups count the clear span between them. **Mitre**, **Butt** and **Swap** change how two beams or two footings meet. **Match properties** (<kbd>MA</kbd>) copies one element's type and settings to others, and **Create similar** (<kbd>CS</kbd>) starts drawing another element of the same type.

Any other profile comes from the libraries: **From a profile library…** searches them all (see [Profile libraries](profiles.md)).

## Strip footing profile

A strip footing type is either the standard one, a stem with its layers on a footing, or any profile swept along the footing's axis, as with Revit's footing profiles. In **Structure types → Strip**, choose **Shape: Profile** and pick a profile: one of the catalogue's (rectangular, stepped in 2 or 3 steps, with a sloped top, eccentric with its stem at one edge, with a stem), made with its sizes, or one of the project's (**Manage → Profiles**, where you can also type its corners or read a drafted outline). The profile's insertion point goes on the footing's axis at the type's **Bottom** level.

Profile footings run straight or curved. Two of them meeting in a corner are mitred, and one ending against another's run goes on into it and is cut by its body, so each step meets the other's step. Plans, sections and elevations cut the swept body. The quantities give its length, cross-section area and volume, and its upright sides are the faces Site works forms. In IFC a straight one is an IfcFooting STRIP_FOOTING whose body is its profile extruded (an IfcArbitraryClosedProfileDef), cut by the corners' planes; a curved one goes as its faces. Choose **Standard** to go back to the stem on a footing.

![Stepped strip footings under a house, mitred at the corners](shots/footing-profile-3d.webp)

![A strip footing type of a profile](shots/footing-profile-type.webp)

## A structural view

A view's discipline decides what it shows. A **Structure** view shows the load-bearing cores only, without the finishes. You choose the discipline in the view's properties. See [Views](views.md).

## Loads

The **Loads** group holds what a structural analysis needs: load cases, area, line and point loads, and the combinations. See [Loads](loads.md).
