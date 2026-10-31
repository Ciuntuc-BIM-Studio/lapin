---
title: Rebar 2D
order: 52
summary: Reinforcement drawn on sections and plans, cross-sections cut where you click, and the bar bending schedule.
---

The **Rebar 2D** tab draws reinforcement the way a bar list is made: bars as shapes with their dimensions, stirrups spread along a beam, marks in balloons, and a schedule that sums them up. It works on any view: a section, a plan, an elevation.

![The Rebar 2D tab](shots/ribbon-rebar.webp)

## A section where you work

Reinforcement needs the cross-section beside the longitudinal one. **Section here** does both: pick a beam, a column or a wall in the view you are in, and where you click a section is cut *across* it, perpendicular to its axis, and drawn beside your click, in the same view, with half a metre of what surrounds it (the slab, the column). A column is cut horizontally at the height you clicked.

The inset view follows the element: move the beam and the section moves with it. Drag the inset by its grip; its properties give it a scale factor and a frame. **Section across** makes the same section as a view of its own, and **Inset view** puts any existing view into the one you are in, at real size and the host's scale, so the bars you draw over it fit its concrete.

![A beam's longitudinal section with its cross-section inset and its reinforcement](shots/rebar-section.webp)

## Bars

**Straight bar**, **L bar**, **U bar**, **Stirrup**, **Tie** and **Polyline bar** are placed with a click (the polyline by its vertices). Each is a shape with parameters: drag a grip and the dimension it stands for follows; type it in the properties panel in millimetres. The bars are drawn as thick as they are, in a colour for each diameter (Rebar settings), and bent at the radius SR EN 1992-1-1 asks: a mandrel of 4Ø up to Ø16, 7Ø over.

Hooks are set on each end: 90°, 135° or 180°, their straight length by practice (12Ø, 10Ø, 5Ø, never under 50 mm), on the other side when asked. A stirrup is born with two 135° hooks turned inside.

**Bar in section** is a bar seen end on: a dot of its diameter, with the length it has along the bar. **Same bar in section** puts the selected bar as a dot elsewhere, without counting it twice. An **Array** in the properties repeats a bar every so many millimetres, as the bars of a row in a cross-section.

The mark's balloon shows the mark number and what the bar is: `4Ø12 L=2450`. Drag the balloon where it reads best. Lengths are the nominal ones, measured on the bar's axis between its corners, hooks included; the developed length round the bends is told in the properties panel.

## Stirrups on the beam's or column's type

Stirrups are not drawn one by one: they belong to the **type**. In Structure → Structure types, a rectangular beam or column type takes **Stirrups on this type**: their diameter, the concrete cover, and how they are laid — one step throughout, or **three zones**, each a share of the length (a third by default) at its own step, closer at the ends. Every element of the type then shows them in every section as the view lies to the axis: the rectangle inside the cover in a cross-section, a line for each stirrup in a longitudinal one, lines over the beam in a structural plan; with the label `41Ø8/150` and the mark's balloon. They are counted in the bar schedule under the type's mark. There is no 3D of them in this edition; the longitudinal bars are drawn by hand, as below.

## Stirrups along a beam

**Stirrups along** takes the two ends of a path; the stirrups are spread so one stands at each end, at the step you set (150 mm by default). **Zones** change the step along the way: **Thirds** puts a step half as close at each end, as a seismic beam asks; the label says `25Ø8/100-200`. Show all of them, only the ends and the middle with a dashed line between, or one.

## The schedule

**Bar schedule** makes a schedule of every bar by mark: diameter, shape, number, length of one, total length and mass, with the totals by diameter. A mark whose bars differ in diameter or length is noted. It is a schedule like the others: on sheets, in CSV and XLSX.

**Rebar settings** holds the project's choices: a colour for each diameter, the text and balloon sizes on paper, the steel, the cover, and the segment lengths written along the bars.
