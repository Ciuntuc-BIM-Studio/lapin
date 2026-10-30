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
- **Pad footing** (<kbd>PF</kbd>) goes under a column.

Draw foundations on the **Foundations** level, or give them a plan of their own.

![Columns, beams and pad footings in 3D](shots/structure-3d.webp)

## Types and corners

**Structure types** sets the sections of columns, beams and footings: concrete sections and steel profiles.

Beams and footings join by themselves where they meet, a corner as a mitre. **Mitre**, **Butt** and **Swap** change how two beams or two footings meet. **Match type** (<kbd>MA</kbd>) copies one element's type to others, and **Create similar** (<kbd>CS</kbd>) starts drawing another element of the same type.

## A structural view

A view's discipline decides what it shows. A **Structure** view shows the load-bearing cores only, without the finishes. You choose the discipline in the view's properties. See [Views](views.md).
