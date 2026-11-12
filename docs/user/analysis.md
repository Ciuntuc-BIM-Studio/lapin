---
title: Structural analysis
order: 52
summary: Run a linear static analysis of the frame under your loads, read the diagrams of forces, moments and deformation in 3D, and set the end releases of the bars.
---

**Run analysis** in the **Loads** group of the **Structure** tab (and in the Loads dialog) analyses the bars of the model under the loads and combinations of [Loads](loads.md), and shows the forces in them.

![The Loads group, with Run analysis](shots/ribbon-analysis.webp)

> The results are **indicative**. Loads, combinations and the analysis are yours to check before you rely on them.

## What is analysed

- **Bars** (columns, beams, steel and timber members) are analysed with their own stiffness, from their material and section. A bar whose material has no modulus of elasticity is left out and listed.
- **Load-bearing walls** are supports: they hold the bars on them, but are not analysed as walls.
- **Floors and slabs** do not carry load as plates; they pass their loads to the bars and walls under them by the **45° rule**: the corners' bisectors divide the slab into the areas each side carries, and each becomes a trapezoidal or triangular load on the bar below.
- **Framed roofs** pass the load to each rafter by its **tributary width**, half way to its neighbours; snow is taken per horizontal projection.
- The **self weight** of the bars is taken along them, and the loads you drew and the derived ones go where they find something under them. A load that finds nothing is left out and reported.

## Running it

Choose **Run analysis**. The **Analysis results** dialog opens:

![The Analysis results dialog](shots/analysis-results.webp)

- The summary: the bars, nodes and unknowns of the calculation and how long it took.
- For each case, the **sum of its loads** against the **sum of the reactions**, and whether they **balance** (a tick) or not (a cross: do not rely on those results). Unmapped loads are listed in kN.
- What was **left out** of the analysis, and why.
- The **largest and smallest** forces, moments, the largest displacement and deflection (with its ratio to the span, L/300) of the case, combination or envelope you pick.

**Show the diagrams** closes the dialog and shows the results in the 3D view.

## The diagrams in 3D

A card over the 3D view chooses what is drawn:

![The bending moment of the ultimate envelope](shots/analysis-moments.webp)

- **What**: a load case, a combination, or the **envelope** of the ultimate (ULS) or serviceability (SLS) combinations: the largest and smallest value at each point.
- **Quantity**: **N** axial force (tension positive), **Vy** and **Vz** shear forces, **T** torsion, **My** and **Mz** bending moments (**Mz** is the strong axis of a level beam, sagging positive), or the **Deformed** shape.
- **Scale**: the size of the diagram on screen; the model itself is not changed.
- **Reactions**: arrows at the supports with their values in kN.

Each bar shows its base line, the curve, hatch lines between them and the values at its extremes; red is positive, blue is negative. Moments are drawn on the side where the bar is in tension.

![The deformed shape](shots/analysis-deformed.webp)

## End releases

Select a column, beam or member: its properties have an **Analysis** section with the **Start (i)** and **End (j)** releases.

![A beam's Analysis section](shots/analysis-bar.webp)

- **Default**: what the connection says (a cleat, a gusset or a fly brace pins the member), a timber roof member pinned about its strong axis, any other bar fixed.
- **Fixed**: the end carries moments. **Pinned**: it turns freely about both bending axes.
- **Custom**: six tick boxes, one for each freedom (displacement along the bar, along each shear axis, torsion, rotation about each bending axis); a ticked freedom is released.

A change is one undo step.

## When the model changes

Results are worked out from the model as it is, never saved. When you change the model, the diagrams go off the view and the card says so:

![The card after a change](shots/analysis-pick.webp)

**Run again** analyses the new model.

## A mechanism

If the structure can move without resistance (too many releases, or a part that nothing holds), the analysis stops and says where:

![A mechanism](shots/analysis-mechanism.webp)

It names the node, the freedom and the bars concerned; **Select the elements involved** selects them so you can fix them, usually by fixing an end or adding a support.
