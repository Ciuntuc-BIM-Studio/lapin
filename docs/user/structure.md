---
title: Structure
order: 50
summary: Columns, beams and footings, their types, and how they meet.
---

The **Structure** tab holds the frame and the foundations.

![The Structure tab](shots/ribbon-structure.webp)

## Frame

- **Column** (<kbd>CL</kbd>): click where it stands. A column runs from its level to the level above.
- **Beam** (<kbd>BM</kbd>): click its two ends; with **Arc** in the command line's bar (**Line** / **Arc**), click its two ends, then a point the arc passes through. **Curved beam** (<kbd>BC</kbd>) starts the Beam tool already set to **Arc**.
  A beam hangs from the level above: its top is at the top of its level (the level's **Top level**, where the level above starts). **Properties → Placement → Top offset (from the level above)** moves it from there: 0 keeps its top on the level above, −50 lowers it 50 mm; **Top elevation** shows where its top is above ±0.00, and you can type that instead. If a beam is not where you expect, check the heights of the levels first ([Levels](levels.md)).
- **Columns at grid** places a column at every crossing of the grid axes. To draw the axes, use **Annotate → Grid axis** (<kbd>GR</kbd>).

## Temporary dimensions

Select one column in a plan and Lapin draws its **temporary dimensions**, along each of its own axes, to the nearest parallel wall face, column or grid axis on each side; select one straight beam and they run across it to the parallel beams, walls and grid axes either side, and from each free end along it to the beam or wall it runs toward, as a [wall's](walls.md#temporary-dimensions) do.

Where they start and end follows your [dimension reference](walls.md#dimension-reference-core-face-to-core-face) (**Annotate → Temporary dimensions…**): with **Faces of core** a column measures from its section's face (its [finish layers](#finish-layers-on-columns) left out) to the wall's core face, with **Faces** from its plaster to the wall's finish, with **Centrelines** from its centre, with **Centre of core** from the middle of its section (its centre, unless the section is off its insertion point). A beam's core faces are its sides. A selected straight beam that meets another beam or a wall at an angle shows that angle too, typed to turn it, as a [wall's](walls.md#angular-temporary-dimensions) does. Click a value, type a distance and press <kbd>Enter</kbd>: the column moves square to the line measured, the beam square to itself or its end along it, one step to undo. The dots and the padlock work as on walls.

While you place a column, the dimensions show, read only, from the column under the cursor (its type's section and finish layers); while you draw a beam, from the line drawn. The **Temporary dimensions** list in the Column and Beam tools' bars chooses the reference.

![A column's temporary dimensions, core face to core face](shots/column-temp-dimensions.webp)

## Foundations

- **Strip footing** (<kbd>SF</kbd>) runs under walls. Its outer layers are mitred at the corners.
  Like the beam, it has **Line** / **Arc** in the bar; **Curved strip footing** (<kbd>CF</kbd>) starts it set to **Arc**.
- **Pad footing** (<kbd>PF</kbd>): click under a column. While the tool is on, it snaps to the centres of the columns of the plan's level and of the level above.
- **Pad footings under columns** puts a pad footing under each selected column that has none, at its centre and turned with it. With no column selected, it takes the columns of the plan's level (in a foundation plan, those of the level above).

Draw foundations on the **Foundations** level, or give them a plan of their own.

**A strip footing is the concrete alone** unless you ask for the wall. While **Strip footing** is on, the command line's bar offers **Footing alone** (the default: a 600 × 400 mm concrete footing, a profile type made the first time) or **With wall** (the stem of a strip type on it, with its layers), and which strip type with a wall to use. The choice is kept. A footing already drawn changes with its type in **Structure types**.

![The strip footing tool's option in the command line: footing alone or with a wall](shots/structure-strip-option.webp)

**In the plans.** A footing lies under the floor, so a plan draws it dashed in the plan of the level it stands on, as a plan of a building shows what is under it. To see it cut and seen, use a **foundation plan**: in the project browser, the **+** beside **Floor plans** makes one for the active level (cut 0.50 m under it, seeing 2.50 m down).

**Drawing in a foundation plan.** It shows the level under it too, and what you draw there with a floor, wall, column or beam tool goes on that level (the Foundations of a new project, at −1.50); the footing tools keep the plan's own level. A pad footing drawn there under a column rises to the column's base; a strip footing's bottom is its type's under the level.

**A pad footing rises to its column.** A pad footing whose point lies within a column's section (to a millimetre) has its top on the column's base: the column's level plus its **Base offset**, the steps under it. Move the column, change its level or its base offset, and the pad follows. Under columns standing on one another it follows the lowest; of two as low, the one whose centre is nearest. A pad with no column over it stands on its own level, its type's **Bottom** under it. **Pad footings under columns** puts each pad in the highest level at or under its bottom (the Foundations of a new project), else in the lowest. A pad footing imported from an IFC file stays where the file puts it.

In **Properties** a pad footing shows its **Top** and **Bottom** as elevations and the column it follows. Type a **Top** to give it its own: it no longer follows the column. **At the column's base** links it back.

![A pad footing's level in Properties: at the base of its column](shots/structure-pad-level.webp)

![Columns, beams and pad footings in 3D](shots/structure-3d.webp)

Beams and strip footings are cut and moved with the same tools as walls (see [Trimming, extending, splitting and offsetting walls](walls.md)): **Modify → Split element** cuts one where you click it into two, **Modify → Trim/Extend elements** trims it back to a boundary or carries it on to one (a curved strip footing goes on round its circle; brought to another beam's axis, the two are joined), and **Modify → Offset element** copies it parallel, by its axis or between its faces.

## Types and corners

**Structure types** sets the sections of columns, beams and footings: concrete sections and steel profiles. A column's **Section** is a **Rectangle**, a **Circle** or a **Polygon**, a regular one given by its **Sides** and its **Diameter** across the corners (an octagonal post, as an imported IFC may have it). Click a type in the list to change its size or its name, then **Save type**: every element of that type follows; **New type** goes back to making one.

**Material** is what the type's elements are made of: reinforced concrete for a new type, or the profile's own material for one from a profile library (steel, timber, aluminium), until you choose another. Pick one of the project's materials, or **New material…** to make one. Their colour, hatch, joins, quantities and IFC export follow it. A strip type's is the **Footing material** (its stem keeps its layers); a pad footing's is its steps'. **Properties** shows a column's, beam's or footing's material under its type; **Change in type…** opens the type here.

**Duplicate** beside the name copies the type as „Name 2” („IPE 300 (2)” when the name ends in a number), and **↑** **↓** move it up and down the lists of its types, as in [Wall types](walls.md#wall-types).

Beams and footings join by themselves where they meet, a corner as a mitre. A beam drawn from column axis to column axis stops at the columns' faces; its length, volume and stirrups count the clear span between them. **Mitre**, **Butt** and **Swap** (the **Joins** group, the same one that serves walls) change how two beams or two footings meet. **Match properties** (<kbd>MA</kbd>) copies one element's type and settings to others, and **Create similar** (<kbd>CS</kbd>) starts drawing another element of the same type.

Any other profile comes from the libraries: **From a profile library…** searches them all (see [Profile libraries](profiles.md)).

## Types from a table

**Import…** under the list of column and beam types reads a CSV or an Excel workbook, one row a type: its name, its kind (beam, column or lintel), its profile, or its height h and width b for a rectangle, its material and mark, and for a lintel its bearing, manufacturer, code and the lengths it is made in. Lapin guesses each column from its heading, in English, Romanian, German, French or Czech (*Název*, *Výška*, *Šířka*, *Uložení*…), and reads a producer's export with columns such as *height.value* and *height.unit*; the dialog shows what it took, and you can give a field another column or another unit. A profile is looked for in the project, in your profile libraries and in Lapin's catalogue (IPE 300, HEA 240…); a row whose profile no library has is shown as not read. Rows that say no kind are what you choose (beams, columns or precast lintels). **One type for the lengths of one section** makes the rows of one section, material and maker whose names differ only by their length one type with all their stock lengths and the code of each (*KP 7 - 100*, *KP 7 - 125*… become *KP 7* with 1000 and 1250); Lapin ticks it when the file has such rows.

The preview lists each type as **New**, **Already in the project** (left as it is) or **Differs**, with what differs (*section*, *lengths*, *material*…); for these you choose, as for the wall types, **Update the project's** (its elements and the lintels that use it follow), **Keep both** (*Name (2)*) or **Skip**. The import is one step to undo. **Export CSV** writes the types in the same columns, so a list can go to a spreadsheet and come back.

![Structure types: a precast lintel type imported from a producer's table, its mark, bearing and stock lengths](shots/structure-types-lintel.webp)

## Finish layers on columns

A column type has **Finish layers, outward** in **Structure types** (the **Columns** tab): plaster, fire boards or insulation round the core. Each row is a **material**, a **thickness** and the **faces** it covers: **+X**, **−X**, **+Y** and **−Y**, the section's own axes (X along its width, Y along its length). All ticked is all round; untick a face that stands against a wall. **Add layer** adds a row, the bin removes one, and the first row is the innermost.

![Structure types: a column type with a plaster layer all round and a fire board on one face](shots/column-layers-types.webp)

- **Steel sections.** A steel I or H section is boxed round its outline, as fire boards are.
- **Plans and sections.** Each layer is drawn round the core with its own material's hatch ([Material graphics](material-graphics.md)).
- **Visibility / Graphics.** The layers fall under the column category's subcategories (core, finishes, insulation…) and obey the view styles' layers modes as a wall's layers do. A new project's plans leave finishes out by default, as they do the walls' plaster.
- **3D.** The layers show in 3D.
- **Quantities.** They are measured per layer in the column's quantities (*Lapin_ColumnLayerQuantities*): width, side area and volume.
- **IFC.** A column with layers goes out with an *IfcMaterialConstituentSet*: *Core* and one constituent per layer.
- **Walls.** A wall that meets a column stops at its outermost layer.

![A plan with two columns cut, their layers hatched round the core](shots/column-layers-plan.webp)

## Beam types as lintels

A beam type can be the product of a precast lintel. In **Wall types** or in a door's or window's configurator, **Lintels → Precast**, then the arrow, **Product** lists the **Beam types** first: the lintel takes the type's section (its width across the wall, its height), the shortest of its lengths that covers the opening and both bearings (a type with no lengths is cut to length), and its material unless the rule names one. Its bearing is the type's own: the bearing field shows it greyed, and a value typed there is the rule's own, until you clear it. Clicked in **Structure types**, a beam type shows its **Mark**, its **Bearing** and its **Stock lengths** (millimetres, separated by spaces or |) to change; the lintels follow the type. Importing a manufacturer's lintel catalogue (the button beside **Product**) makes each product a beam type too, so lintels and beam types are one list; products imported before still work.

![A wall type's lintels: precast, the beam type KP 7 as the product](shots/lintel-beam-type.webp)

The lintels stay parts of their wall, as before; in IFC each is an *IfcBeam* LINTEL typed by its *IfcBeamType*, and the lintel schedule lists them by the type's mark and name, with the code of the length each takes.

## Strip footing graphics in a view

A strip footing's hatch is its material's. To change it in one view (the hatch's scale or angle, no hatch, a colour), or to draw a hatched elevation of it plain, select the footing and choose **Graphics in this view…** (right-click in the plan, section or elevation, or **Properties → Graphics in view**): **Cut** sets what plans and sections hatch, **Seen faces** what an elevation hatches. See [Graphics in this view](views.md#graphics-in-this-view).

## Strip footing profile

A strip footing type is either the standard one, a stem with its layers on a footing, or any profile swept along the footing's axis, as with Revit's footing profiles. In **Structure types → Strip**, choose **Shape: Profile** and pick a profile: one of the catalogue's (rectangular, stepped in 2 or 3 steps, with a sloped top, eccentric with its stem at one edge, with a stem), made with its sizes, or one of the project's (**Manage → Profiles**, where you can also type its corners or read a drafted outline). The profile's insertion point goes on the footing's axis at the type's **Bottom** level.

Profile footings run straight or curved. Two of them meeting in a corner are mitred, and one ending against another's run goes on into it and is cut by its body, so each step meets the other's step. Plans, sections and elevations cut the swept body. The quantities give its length, cross-section area and volume, and its upright sides are the faces Site works forms. In IFC a straight one is an IfcFooting STRIP_FOOTING whose body is its profile extruded (an IfcArbitraryClosedProfileDef), cut by the corners' planes; a curved one goes as its faces. Choose **Standard** to go back to the stem on a footing.

![Stepped strip footings under a house, mitred at the corners](shots/footing-profile-3d.webp)

![A strip footing type of a profile](shots/footing-profile-type.webp)

## Structural or not

Every wall, floor, roof, column, beam and footing has **Structural (load-bearing)** in its **Properties**: ticked, it carries loads; cleared, it does not; unset, the rule decides and the box shows greyed, with where its value came from. The rule: a wall is structural unless it is a partition (its IFC type PARTITIONING), floors, roofs, columns, beams and footings are; coverings and room finishes never are. The analysis keeps its own rule for an unset wall: it takes one of masonry or concrete with a core of 10 cm or more (see [Structural analysis](analysis.md)).

![The Structural tick in Properties, set on the wall](shots/structural-usage-properties.webp)

- **Type structural**, under it, sets the tick on the elements' type: every element of the type that says nothing itself takes it.
- **Structural wall** and **Structural floor**, in the Structure tab's **Walls and floors** group, draw Architecture's walls and floors already ticked; untick **Structural** in the command line's bar to leave them to the rule. The Architecture tab's Wall and Floor leave it unset.
- **Modify → Structural** and **Not structural** set it on the selection.
- The tick is IFC's **Pset_…Common.LoadBearing**: a value typed there, or given by an imported IFC file, counts too (the tick wins over it, and Properties says when they disagree). The IFC export writes the tick into the common set; **Write derived LoadBearing** in the export's options writes the rule's value on the others too (a CDE issue and an IDS check always do).
- Filters, Quick select, clash sets and schedules read the field **Structural** (yes or no). **Visibility / Graphics → Filters → Structural usage** adds a filter of what is not structural, drawn halftone in the view.
- A floor, a roof, a column or a beam whose tick is cleared (or whose LoadBearing is false) leaves the analysis, and its report says so.

![The Structure tab's Structural wall, its tick in the command line's bar](shots/structural-usage-wall-tool.webp)

## A structural view

A view's discipline decides what it shows. A **Structure** view shows the load-bearing cores only, without the finishes. You choose the discipline in the view's properties. See [Views](views.md). With **Other disciplines halftone** on in its view settings, what is not structural (a partition, a wall whose tick is cleared) is drawn halftone with the other disciplines.

![A Structure plan: the partition halftone, the load-bearing walls in full](shots/structural-usage-plan.webp)

## Loads

The **Loads** group holds what a structural analysis needs: load cases, area, line and point loads, and the combinations. See [Loads](loads.md).
