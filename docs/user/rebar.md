---
title: Simple rebar
order: 52
summary: Reinforcement drawn on sections and plans, cross-sections cut where you click, and the bar bending schedule.
---

The **Simple rebar** tab draws reinforcement the way a bar list is made: bars as shapes with their dimensions, stirrups spread along a beam, marks in balloons, and a schedule that sums them up. It works on any view: a section, a plan, an elevation.

![The Simple rebar tab](shots/ribbon-rebar.webp)

## A section where you work

Reinforcement needs the cross-section beside the longitudinal one. **Section here** does both: pick a beam, a column or a wall in the view you are in, and where you click a section is cut *across* it, perpendicular to its axis, and drawn beside your click, in the same view, with half a metre of what surrounds it (the slab, the column). A column is cut horizontally at the height you clicked. Click inside the element's cut, not only on its edge: in a section or an elevation, as in a plan, the click takes the element whose cut holds it.

The inset view follows the element: move the beam and the section moves with it. Drag the inset by its grip; its properties give it a scale factor and a frame. **Section across** makes the same section as a view of its own, and **Inset view** puts any existing view into the one you are in, at real size and the host's scale, so the bars you draw over it fit its concrete.

![A beam's longitudinal section with its cross-section inset and its reinforcement](shots/rebar-section.webp)

## Bars

**Straight bar**, **L bar**, **U bar**, **Stirrup**, **Tie** and **Polyline bar** are placed with a click (the polyline by its vertices). Each is a shape with parameters: drag a grip and the dimension it stands for follows; type it in the properties panel in millimetres. The bars are drawn as thick as they are, in a colour for each diameter (Rebar settings), and bent at the radius SR EN 1992-1-1 asks: a mandrel of 4Ø up to Ø16, 7Ø over.

Hooks are set on each end: 90°, 135° or 180°, their straight length by practice (12Ø, 10Ø, 5Ø, never under 50 mm), on the other side when asked. A stirrup is born with two 135° hooks turned inside.

**Anchor grips.** A selected bar seen from the front shows, beside its grips, orange diamonds standing *outside* it by the **cover**: one at its origin and one at each free end (an end with a hook has none). They stand for the formwork face: drag one onto the element's cut — its corner, its face; the drawing snaps there — and the bar stops the cover inside it. The end's anchor sets the bar's length, the origin's moves the whole bar. The cover is the bar's own, typed in the **Cover** field of its properties (clear it to go back), else the stirrup cover of its host's type plus the stirrup, else the one in Rebar settings; the field says which as a hint.

**Bar in section** is a bar seen end on: a dot of its diameter, with the length it has along the bar. **Same bar in section** puts the selected bar as a dot elsewhere, without counting it twice. An **Array** in the properties repeats a bar every so many millimetres, as the bars of a row in a cross-section.

The mark's balloon shows the mark number and what the bar is: `4Ø12 L=2450`. Drag the balloon where it reads best.

The **label**'s style says what it writes and where it stands: its parts in order (count, diameter, step, length, mark, steel, free text), the balloon, whether the text runs horizontal, along the bars or at an angle, the side of the bars it stands on and how far off them, the leader. It is set in **Rebar settings** for each kind (bars, stirrups on the type, column bars, distributions), on a type beside its stirrups, and on a bar in its properties, the most specific winning. A balloon is never drawn without a mark. The labels of a type's stirrups, of a column's bars and of a bar seen in another view have grips too: drag one and it keeps that place on that view (**Put the label back** in the properties undoes it); until then, labels that would meet stack under one another in the order of their marks.

A **distribution** — stirrups on a type, stirrups along a path — is marked as a **dimension string** without values: a line parallel to the bars outside them, a short tick where each bar crosses it and at both ends, the label above the line along it, the balloon touching its start. Drag the balloon where it reads best; drag the string's middle to move it off the bars. **Marked by** in the label's style turns the string off and leaves the text and balloon beside the bars. The **leader** may be none, one line, or a line to each bar seen as a dot (the default for bars in a cross-section: one bar, one line; over sixteen, only the ends and the middle one). Drag the middle of a leader to bend it there; the elbow then has its own grip, and Ctrl/⌘-click on it takes it away.

Lengths are the nominal ones, measured on the bar's axis between its corners, hooks included; the developed length round the bends is told in the properties panel.

**Split bar** cuts a longitudinal bar that is longer than the stock it is delivered in (12 m, or 6 m, in **Rebar settings**). Where the two pieces overlap is your decision, not Lapin's: a bottom bar laps near the supports, a top bar away from them. So you give the lap zone yourself: click the bar at one end of the lap, then at the other; or click where the first bar ends and type the lap length (800 for 800 mm), measured along the bar; Enter or Escape after the first click ends the settings' **lap length** (50Ø unless set) at that click. In the bar's properties the same zone is typed as **Lap from … to …** in millimetres from the bar's start, with the lap length beside them (changing it keeps *to* and moves *from*); the first bar ends at *to*, the second starts at *from*, and the two lengths are shown while you type, before **Split the bar**. The first keeps the bar's mark and start hook, the second gets the next mark and the end hook, and the schedule counts them as two marks with their own lengths. The second piece is drawn a hair beside the first so the lap reads. A bar over the stock length is warned of in its properties, with **Cut at the stock length** ending the lap at the stock length, so the first piece is exactly that long.

## Stirrups on the beam's or column's type

Stirrups are not drawn one by one: they belong to the **type**. In Structure → Structure types, a rectangular beam or column type takes **Stirrups on this type**: their diameter, the concrete cover, and how they are laid — one step throughout, or **three zones**, each a share of the length (a third by default) at its own step, closer at the ends. Every element of the type then shows them in every section as the view lies to the axis: the rectangle inside the cover in a cross-section, a line for each stirrup in a longitudinal one, lines over the beam in a structural plan; with the label `41Ø8/150` and the mark's balloon. They are counted in the bar schedule under the type's mark. A **column's** stirrups may be the perimeter's alone, **with a diamond inside** (its corners at the sides' middles) or **with two inside, one each way**, each kind a mark of its own. A column is reinforced storey by storey, so its **longitudinal bars** are on its type too: so many on each face with the corners shared, their diameter, and the **starter bars** running past the top (1 m by default) for the column above; dots in its horizontal cut and in a structural plan, lines in a vertical section. **At the top** says how they end: **By the column above** (the default) runs them on as starter bars where a column stands on this one and, where none does (the top storey), stops them under the top, inside the cover, with a 90° hook (12Ø) bent inwards; **Starter bars past the top** and **90° hooks inside the top** choose one whatever stands above. A hooked bar is an L bar in the schedule, its hook counted in its length.

A beam's **longitudinal bars** are not on the type (they run over several spans): draw them in the longitudinal section or in the plan, with their hooks. A bar drawn inside a beam's cut belongs to that beam and stands in space: **Across the drawing** lays it so many times along the view's direction, **by the cover** (the first and the last at the faces, inside the cover and the stirrup, the rest spread evenly) or **at a step**. The cross-section then shows the bars as dots where they are, a structural plan as lines, and the schedule counts them all: `3Ø16 L=6400`. The same holds for a bar drawn in the cut of a column, a wall, a slab, a footing or a stair. In a stair the bars are laid between the concrete faces of its flights and landings, never its railings or finish.

**Bars in 3D** (View → Bars in 3D, or the button in this tab) shows or hides the 3D view's **Rebar** category, the same as its row in Visibility / Graphics (<kbd>VV</kbd>): every bar in space as a tube of its diameter swept along its bent shape, in its colour, the type's stirrups at their stations, the column's bars, the bars you drew with their copies. Every 3D view leaves the bars out until you show them, whatever its style; a hidden beam takes its stirrups with it. A linked or imported structural IFC's bars (IfcReinforcingBar) are in the same category: they show and hide with these, shared where they repeat so thousands turn smoothly, and plans and sections draw them only at LOD 400 (a structural style's). The bars of this tab are a picture for the screen, asked of the engine while they show: they are not picked, not exported, and past two million triangles only the first are drawn. Bars of their own in the model, generated from the types, with optimisers and configurators, are for the premium Rebar BIM.

## Stirrups along a beam

**Stirrups along** takes the two ends of a path; the stirrups are spread so one stands at each end, at the step you set (150 mm by default). **Zones** change the step along the way: **Thirds** puts a step half as close at each end, as a seismic beam asks; the label says `25Ø8/100-200`. Show all of them, only the ends and the middle with a dashed line between, or one.

## The schedule

**Bar schedule** makes a schedule of every bar by mark: diameter, shape, number, length of one, total length and mass, with the totals by diameter. A mark whose bars differ in diameter or length is noted. It is a schedule like the others: on sheets, in CSV and XLSX.

**Rebar settings** holds the project's choices: a colour for each diameter, the text and balloon sizes on paper, the steel, the cover, the segment lengths written along the bars, and the labels' style for each kind.
