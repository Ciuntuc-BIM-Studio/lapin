---
title: Sketch constraints
order: 88
summary: Hold a sketch's shapes together with constraints (horizontal, tangent, equal, fixed…) and dimensions, see how free it still is, and let a changed dimension move the solid built from it.
---

## What constraints are

A sketch is the set of shapes drafted on one view: a plan, a section, an elevation or a reference plane. A **constraint** is a rule about those shapes that stays true while you edit: *this side is horizontal*, *these two are equal*, *this point sits on that circle*. A **dimension** is a constraint with a number: *this side is 4 m*. The sketch is solved by the engine every time something changes, so what the rules tie together moves together, and a solid, a sweep or a part built from the sketch follows.

Constraints belong to the sketch and are saved with the project. They are not written to IFC; the shapes, as solved, are.

## The Constraints tools

**Studio → Constraints** has one tool for each kind. Each one asks for what it needs in the view: click a point, an edge, a circle or an arc. The part under the cursor is lit and named (*vertex*, *start*, *end*, *centre*, *edge*, *curve*), and it snaps as drawing does. When the picks are complete, the constraint is made and the tool stays on for the next one; **Esc** ends it.

![A tool lights the edge under the cursor and names it](shots/sketch-constraints-pick.webp)

![The Constraints group of the Studio tab](shots/sketch-constraints-ribbon.webp)

| Tool | You pick | Result |
|---|---|---|
| Coincident | two points | one point |
| Point on object | a point, then a line, circle or arc | the point stays on it |
| Horizontal, Vertical | a line, or two points | level, plumb |
| Parallel, Perpendicular | two lines | |
| Tangent | a line and an arc, two arcs, or two ends | touching, not crossing |
| Equal | two lines, or two circles or arcs | the same length or radius |
| Symmetric | two points, then a line or a point | mirrored about it |
| Midpoint | a point, then a line | at its middle |
| Fix | a point, a line, a circle or a whole polyline | held where it is |

If the second pick can be taken more than one way (two shapes meet at a corner), the click means the first shape's part that fits; click again on the other to take it. A pick that cannot make the constraint is refused in the command line with what it takes.

Typing a command works too: `CNHORIZONTAL`, `CNFIX`, `CDIM`… are on each tool's tooltip.

## Dimensions

**Dimension** is the smart one: what you pick says what it measures. Two points, or a point and a line: the distance. One line: its length (press **Enter**), or a second line for the angle between them. A circle or an arc: its radius. Then type the value:

- a number, with its unit if you like (`4 m`, `250 mm`, `45°`);
- `=formula`, in the parameters of the part whose recipe uses the sketch (`=W / 2`);
- **Enter** to keep the size the shape has now;
- `Driven`, for a **reference**: it measures and does not drive.

Keywords after a pick change the kind: `X` and `Y` for the horizontal or vertical distance, `D` for the diameter. **Horizontal distance**, **Vertical distance**, **Radius**, **Diameter** and **Angle** are the same tool for one kind each.

A dimension is drawn like a drafting dimension, with its value. A **driven** one is in parentheses and dashed. A formula's dimension shows its value, and its formula when you point at it. Angles are signed, counter-clockwise, in degrees.

## Glyphs, selecting and editing

Each other constraint has a small glyph near what it holds: **H**, **V**, **∥**, **⊥**, a lock for fixed, and drawings for coincident, point on object, tangent and symmetric. Glyphs standing together fan out so none hides another.

- Click a glyph or a dimension to select it (Shift adds); **Delete** removes the selection, as one undo step.
- **Double-click** a dimension to edit its value in place, in the field that opens: a number or `=formula`. A driven dimension becomes driving when you give it a value, unless that would over-constrain the sketch.
- The eye on the chip below hides the constraints in this view, and shows them again. It is remembered for the view.

![A dimension edited in place](shots/sketch-constraints-edit.webp)

## How free the sketch is

A chip at the bottom of the view says what the solver found, while the view has constraints or a constraint tool is on:

- **N degrees of freedom**: the sketch can still move or change in N ways. Its shapes that can still move are blue, and their free points have a blue dot.
- **Fully constrained**: nothing moves. The shapes are drawn in ink.
- **Conflicting constraints**: two rules cannot both hold. The shapes and glyphs involved are red, and the last constraint you tried was refused with a message naming them.
- **Redundant constraints**: a rule that another already makes true; its glyph is amber. It does no harm, and can be deleted.

![The sketch after Horizontal and Vertical: four degrees of freedom are left, and its free shapes are blue](shots/sketch-constraints-free.webp)

![The chip](shots/sketch-constraints-chip.webp)

![Fully constrained: the glyphs, the two dimensions and the chip](shots/sketch-constraints-fixed.webp)

In **Studio → Part**'s tree, each sketch shows its number of constraints and the same words.

## A dimension drives the solid

Change a dimension and the sketch follows, and so does everything built from it: here the width goes from 4 m to 6 m, and the extruded block with it.

![The same sketch, 6 m wide](shots/sketch-constraints-wide.webp)

![The solid built from it](shots/sketch-constraints-3d.webp)

## Dragging

Drag a point of a constrained shape with its grip: the rest of the sketch follows the rules while you move, shown as you drag, and the result is one undo step on release. If the rules do not let that point go there (a fixed point, a dimension that would be broken), the drag shows in red with the reason and is refused. Other edits (move, trim, extend, offset, fillet) are solved the same way. A rule that cannot follow an edit (its shape was cut away) is removed with a notice saying which and why; it is never kept wrong.

## Where to go next

The Parts page tells how a sketch becomes a part with parameters, which the formulas in dimensions use.
