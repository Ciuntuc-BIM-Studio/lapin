---
title: Structural analysis
order: 52
summary: Run a linear static analysis of the frame under your loads, read the diagrams of forces, moments and deformation in 3D, set the end releases of the bars, work out the modes and the earthquake's response, and check the bars, slabs and walls against the codes.
---

<!-- video structural-analysis -->

**Run analysis** in the **Loads** group of the **Structure** tab (and in the Loads dialog) analyses the bars, slabs and walls of the model under the loads and combinations of [Loads](loads.md), and shows the forces in them.

![The Loads group, with Run analysis](shots/ribbon-analysis.webp)

> The results are **indicative**. Loads, combinations and the analysis are yours to check before you rely on them.

## What is analysed

- **Bars** (columns, beams, steel and timber members) are analysed with their own stiffness, from their material and section. A bar whose material has no modulus of elasticity is left out and listed.
- **Concrete floors and slabs** are analysed as **shells**: each is divided into small elements (a mesh, 0.5 m by default) that bend and stretch, sharing their nodes with the beams, columns and walls they meet. The slab carries its loads itself to what holds it, by its stiffness and theirs. A beam under a slab keeps its own axis below it and works with the slab as a T beam.
- **Concrete load-bearing walls** are shells too: they stand on their footings (a wall with nothing modelled under it is held along its foot) and carry the floors on them, and wind or other sideways loads in their plane.
- **Masonry walls** are shells too, linear elastic and isotropic, with values after EN 1996-1-1 as CR 6-2013 takes it (E = KE·fk, G = 0.4 E; see [Seismic analysis](#seismic-analysis)). Walls of other materials are supports: they hold the slabs and bars on them, but are not analysed as walls.
- **Which walls carry**: a wall's **LoadBearing** (Pset_WallCommon, on the wall or its type) decides. Ticked, the wall carries; unticked, or a wall whose IFC type is a partition (PARTITIONING), it does not, and its weight goes onto the floor under it as a partition load (see [Loads](loads.md)). A wall that **says nothing** carries when it is of masonry or concrete with a core of 10 cm or more: a note lists those walls, so you can untick **LoadBearing** on the partitions among them. Thinner ones, and walls of other materials, stay partitions, and a note lists the thin ones.
- **Floors of other materials** (a timber floor) pass their loads to the bars and walls under them by the **45° rule**: the corners' bisectors divide the slab into the areas each side carries, and each becomes a trapezoidal or triangular load on the bar below.
- **Framed roofs** pass the load to each rafter by its **tributary width**, half way to its neighbours; snow is taken per horizontal projection.
- **Rods and ties** (a solid round bar that braces, a tie or cable) work axially: they are pinned at their ends and their weight goes to their ends, so they do not sag as beams. A bar's **Axial member** in its **Analysis** properties changes it: **By its section and use**, **Yes (a rod or tie)** or **No (a beam)**.
- **Beams of different depths** meeting a column each keep their own level; the column has one joint, and the beams are tied to it by a rigid arm.
- **Wind on the façades** goes to the beams, rails and floor edges along them; the strip at their foot goes straight to the ground and is listed apart from the bars' sums.
- The **self weight** of the bars is taken along them, and the loads you drew and the derived ones go where they find something under them. A load that finds nothing is left out and reported.

## Running it

Choose **Run analysis**. The **Analysis results** dialog opens:

![The Analysis results dialog](shots/analysis-results.webp)

- The summary: the bars, shell elements, nodes and unknowns of the calculation and how long it took.
- For each case, the **sum of its loads** against the **sum of the reactions**, and whether they **balance** (a tick) or not (a cross: do not rely on those results). Unmapped loads are listed in kN.
- What was **left out** of the analysis, and why.
- The **largest and smallest** forces, moments, the largest displacement and deflection (with its ratio to the span, L/300) of the case, combination or envelope you pick; below them, the largest and smallest values on the **slabs and walls**, and where.
- **Shells**: which slabs and walls are analysed as shells (**Slabs and walls**, **Slabs only**, **Walls only**, or **None**: the slabs by the 45° rule and the walls as supports), and the **Mesh** size in metres. A smaller mesh is more exact and slower; a narrow piece always gets at least four elements across it. A change is one undo step; **Run again** takes it.

**Run again**, **Show the diagrams** and **Close without diagrams** stay at the foot of the dialog, however far you scroll. The notes of the run (how it was made, and what to check) are folded under **Notes (n)**; those to check come first, in red.

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

## Slabs and walls

When slabs or walls are analysed as shells, the card has a second row of buttons, **Slabs and walls**, and the shells are coloured in bands by the quantity you choose (a second click on it takes the colours off):

![The floor's moment my, in bands](shots/analysis-slab.webp)

- **w**: the displacement across the slab or wall (up positive on a slab).
- **mx**, **my**: the bending moments in the slab's x and y directions (global X and Y on a slab; along and up a wall), in kN·m per metre. On a slab, **positive means its bottom face is in tension** (the span), negative its top face (over a support); on a wall, the face on the right of its direction.
- **mxy**: the twisting moment; **nx**, **ny**, **nxy**: the forces in the slab's plane (tension positive), in kN per metre; **vx**, **vy**: the shear forces across it.

The legend on the card gives each band's values and the unit:

![The legend of the contours](shots/analysis-legend.webp)

The same bands are drawn on the floor plan of the slab's storey, for the screen; sheets do not print them yet. For an envelope, each point shows the larger in size of its least and largest value.

## Seismic analysis

The **Seismic** section of the **Analysis results** dialog works out the modes of vibration of the structure, the earthquake's response by a response spectrum, and the storeys' shears and drifts, with the code's checks. It needs the site's values, and until you give them it only says so.

![The Seismic section](shots/analysis-seismic.webp)

**The parameters.** Each change is one undo step; **Turn off** forgets them all.

- **Code**: **P100-1/2013** (the default) or **EN 1998-1**, with its recommended values.
- **ag (g)**: the site's design ground acceleration as a fraction of g, and **TC (s)** (0.7, 1.0 or 1.6 s) with P100, or the **Ground** type A to E and the **Spectrum type** 1 or 2 with EN. **These are yours**: take them from the code's seismic zoning maps; Lapin ships no map.
- **Importance class** I to IV (its factor γ is shown) and **q**, the behaviour factor (1.5 by default). Below them, a table lists P100-1/2013 Table 8.10's factors for masonry, to be multiplied by αu/α1 of your building.
- **Modal combination** (CQC by default, or SRSS), **Directions** (the 30 % rule by default, or SRSS) and **Diaphragm**: a rigid diaphragm is only applied on levels without a shell floor; a floor analysed as a shell already gives it. **Modes' limit** is the most modes worked out.

**The modes.** The table gives each mode's period T and frequency f, its effective mass along X and Y and the sum up to it. More modes are worked out until 90 % of the mass is reached in both directions, or the limit stops them, and the dialog warns you when it does.

**The spectrum.** The chart draws the design spectrum Sd and the elastic spectrum Se against the period, with the modes' periods marked on the design spectrum and the corner periods TB, TC, TD.

The notes of the modal and seismic analysis are folded under the section's own **Notes (n)**.

**The storeys.** For each storey: its mass, shear force along X and Y, the elastic interstorey drift, and the serviceability (SLS) and ultimate (ULS, P100 only) drift checks against their limits, ticked or crossed (a failing one is red). The base shear is given with the seismic combination, and compared with the lateral force method as a check. A **θ** column gives the interstorey drift sensitivity coefficient θ = Ptot·dr/(Vtot·h) along X and Y (P100-1/2013 §4.6.2.2, dr = q·de): up to 0.1 second-order effects are neglected (green), up to 0.2 the seismic effects are multiplied by 1/(1 − θ) (amber, with the factor), up to 0.3 a second-order analysis is needed, and above 0.3 the structure is refused: stiffen it (red).

**A mode in 3D.** **Animate** on a mode's row (or the mode chosen beside **Animate** at the foot of the dialog) closes the dialog and shows its shape swinging in the 3D view: the slabs and walls as shaded surfaces and the bars as tubes, the model at rest see-through behind them. The shape is sized to be seen: its largest displacement is about 7 % of the model's size, and **Scale** on the card makes it smaller or larger (×0.25 to ×4). It tells how the structure moves, not by how much. Untick **Show undeformed** to hide the model at rest. **Stop** on the card ends it and brings back the diagrams' card as it was. It follows the model when you change it.

![A mode shape in 3D](shots/analysis-mode.webp)

**The seismic combination.** With the parameters given, the picker offers **Seismic combination** among the envelopes (G + ψ2 Q ± AEd, the least and largest of each value), and the ultimate envelope includes it. Changing a parameter changes the model, so the results go out of date: **Run again**.

**Masonry walls** are analysed as shells with values taken from the material's name (clay, hollow clay, aerated concrete, concrete block) and indicative. To give your own, set these parameters of the set `Lapin_Masonry` on the material: `UnitGroup` (clay1, clay2, aac, concrete1, concrete2), `fb`, `fm`, `fk` (MPa) and `KE`; or `YoungModulus` of `Pset_MaterialMechanical`. Masonry is linear in this version: no tension cut-off and no cracking.

> Every value here is indicative: the site's ag and TC, q and the code's numbers are yours to check.

## Design checks

After a run, the **Checks** section of the **Analysis results** dialog checks the bars, slabs and walls against the codes, with the forces of the combinations. Every check is **indicative**: the values are the Eurocodes' recommended ones (and **CR 6-2013** for masonry), not the national annexes', and the signed design stays the engineer's.

![The Checks section](shots/analysis-checks.webp)

**What is checked, by material**

- **Steel** bars (EN 1993-1-1): the section's class, the resistance to tension, compression, bending and shear, flexural and lateral-torsional buckling, and the interaction of axial force and bending (Annex A, method 1, as the Romanian national annex chooses; Annex B, method 2, if the project asks for it). The buckling lengths assume a braced frame: when the model has no wall or bracing in a direction, the notes say so, and the columns' own lengths are yours to give. Class 4 sections are reported as not checked.
- **Concrete** beams and columns (EN 1992-1-1): bending (the required reinforcement), shear and stirrups, the minimum and maximum reinforcement, the span/depth ratio, and for columns the second-order effects with the nominal curvature. **Slabs and walls** analysed as shells get the reinforcement they need in each layer, by the Wood–Armer method.
- **Timber** bars (EN 1995-1-1, strength class from the material's name such as C24 or GL24h): bending, tension, compression with buckling, shear and lateral-torsional buckling, with kmod for the load duration and the service class (snow is a short-term action by default; a mountain site can take it as medium-term).
- **Masonry** walls (EN 1996-1-1 as CR 6-2013 takes it): the axial resistance at the foot, middle and head, shear and bending in the plane, with γM 2.2 (1.65 where the code allows it). The seismic combination uses the same resistances with its own forces.

**The table of members.** One row for each element (a beam made of several pieces in the analysis is one row, with its worst piece): its **utilisation** (the most loaded check divided by its resistance, as a bar and a number; more than 1 fails and is red), the **governing check** with its clause and the **governing combination**. The filters choose a **Material** or a **Storey**. A click on a row selects the element in the model. **Slabs and walls** in concrete follow with the reinforcement each layer needs, in cm²/m.

**The masonry walls' table.** For each storey, each wall with its direction (X or Y), length, thickness, the forces **N**, **V** and **M** (hover a value for the combination it comes from), the resistances **NRd**, **VRd** and **MRd**, its utilisation and its governing check. Under it, for each direction, **ΣV** against **ΣVRd**: the storey's wall system as a whole.

![The masonry walls of a storey](shots/analysis-walls.webp)

**Design values.** The project's values (concrete cover, bars' and stirrups' diameters, fyk, the masonry's fvk0 and γM, the timber's service class, the **steel interaction** method and the **snow on timber** duration) are under **Design values of the project** in the Checks section; an element's own are in its properties (below). An empty field takes the default; each change is one undo step, and **Run again** checks with it.

**Colours by utilisation.** **Colour the model by utilisation** (here, or **Utilisation** on the card over the 3D view) colours each checked bar, slab and wall from green (0) through yellow and orange (1) to red (over 1), and grey where nothing was checked; the plans of a storey show the same colours on its slabs and bars. The diagrams give way to them while it is on.

![The model coloured by utilisation](shots/analysis-utilisation.webp)

![Masonry walls coloured by utilisation](shots/analysis-utilisation-walls.webp)

**Reinforcement as contours.** The card's **Reinforcement** buttons (**asxBottom**, **asyBottom**, **asxTop**, **asyTop**) draw the reinforcement a slab needs in each layer, in cm²/m, on the ultimate envelope.

**In the properties.** Select a bar, slab or wall: its **Design checks** section lists each check with its clause, utilisation and governing combination (open one for its intermediate values), the notes, and the reinforcement required. On a bar it also has the design values of the element: the **buckling length factors** ky and kz (or the lengths Lcr,y and Lcr,z in metres), the **laterally unrestrained length** LLT (a beam under a slab that carries it is taken as restrained), the timber **service class** and the concrete beam's **span system**.

![A beam's design checks and values](shots/analysis-design.webp)

> The checks are indicative. Lapin takes the Eurocodes' recommended values; the national annexes' values, the loads and the design values are yours to verify before you rely on them.

**Buckling lengths from αcr.** By default the flexural buckling and the bending-and-compression checks of steel and timber bars use the lengths you give: k·L, with the factors ky and kz (or Lcr,y and Lcr,z) of the bar. **Buckling lengths** under **Design values of the project** can take them instead **From αcr**; a bar can choose its own in its properties (empty follows the project). The length about an axis is then Lcr = π√(EI/(αcr·NEd)), with αcr of the lowest buckling mode that bends the bar about that axis (the strong axis y-y, the weak axis z-z), found for the combination that governs. It is never shorter than k·L, and with **Second order (P-Δ)** on every bar keeps its k·L: the sway is then in the forces (EN 1993-1-1 5.2.2(3)b). Concrete bars only show it (EN 1992-1-1 5.8.3.2): the check keeps its l0. The checks table has a column **Lcr,y / Lcr,z** with the lengths used and their source (k, αcr, or given); hover it for both values, αcr and the mode. A bar's properties list, for each axis, k·L, the length from αcr and the one used. Run again after changing the option.

![The table with k·L](shots/analysis-lcr-k.webp)

![The same table with the lengths from αcr: the columns' utilisation rises](shots/analysis-lcr-alpha.webp)

![A column's buckling lengths in its properties](shots/analysis-lcr-props.webp)

> The lengths from αcr can be much longer than k·L (here the columns' weak-axis length goes from 3.27 m to 4.09 m and the strong-axis one to 7.71 m, the utilisation from 0.23 to 0.27; in the steel hall of this guide the rafters' in-plane length grows to about 38 m and their utilisation doubles). For steel halls the better path is second order (P-Δ) with the imperfections, which needs no buckling length for the sway.

## Second order and buckling

Two questions decide whether a frame can be analysed to first order: how far its loads are from the critical ones, and how much its sway adds. The **Stability** section of the **Analysis results** dialog answers the first, and the **Second order (P-Δ)** tick box, beside the shell options (the analysis options), the second.

![The Stability section](shots/analysis-stability.webp)

**Second order (P-Δ).** With the box ticked, each static combination is solved on its own with the bars' geometric stiffness of their axial forces, and iterated until the forces stop changing. The table lists, for each combination, the iterations, whether it settled, and the **amplification**: the largest horizontal displacement over its first-order value. The load cases stay first order. Along a bar the moment keeps its first-order shape between its ends: a bar's own buckling is its check's (EN 1993-1-1 6.3). A combination that is unstable, or a mechanism, is left out of the envelopes and the checks, and the notes say so in red.

**Global imperfections.** The **Global imperfections (±X, ±Y)** box, beside Second order, adds the sway imperfections of EN 1993-1-1 5.3.2 to every ultimate (and accidental) combination. It works with or without second order, and ticking Second order ticks it too in a project that never said anything about imperfections (unticking it is remembered). The sway is φ = φ0·αh·αm, with φ0 = 1/200, αh = 2/√h (h the height, between 2/3 and 1) and αm = √(0.5(1 + 1/m)), m being the columns of a row that carry at least half of its mean load. A bar within 10° of the vertical that is in compression is a column. Each one gets the equivalent horizontal forces: φ·NEd at its top and −φ·NEd at its foot, NEd being its force in the first-order result. Each combination is solved four times, **+X, −X, +Y and −Y**, and its results (the diagrams, the envelopes, the checks) are the envelope of the four, shown as one combination. The table adds a **Governing** column naming the variant of the largest amplification (hover for the four), the line above it gives φ as 1/N along X and Y with m, h and αh, and the checks name the governing variant in their combination. Concrete takes the same form (EN 1992-1-1 5.2) and timber follows EN 1995-1-1 5.4.4. Where the horizontal loads are at least 0.15 of the vertical ones, 5.3.2(4) would allow leaving the imperfections out: the notes say so, and Lapin keeps them, on the safe side. The shell walls' vertical load is not tilted: give theirs as loads where they matter.

**Braced by sheeting.** A roof purlin or a wall rail that carries sheeting is held straight by it across, in the sheeting's plane, so its lowest buckling modes are often its own, with an α well below the frame's. When the first modes in the buckling table are a roof purlin's own, a button **The sheeting braces the purlins** appears under it (EN 1993-1-3 10.1); it does the same as ticking **Braced by sheeting** in the **Analysis** section of each purlin or rail, which you can also do for one bar or a selection. The run then holds each braced member straight between the members it rests on, in the plane of its sheeting (along its section's width), and the Stability section says how many members are braced. The sheeting does not tie a member to another (it is not a stressed skin), and the members' own buckling checks keep their buckling lengths. Find the critical load factors again after the run to see the modes move on.

![Purlins' own modes before the sheeting braces them](shots/analysis-sheeting-before.webp)

![After: the sheeting holds the purlins](shots/analysis-sheeting-after.webp)

**The critical load factors.** **Find the critical load factors** works out, for each ultimate combination, the factor αcr that brings its loads to the ones the frame buckles under. It can take a few seconds on a big model, so it runs when you ask. The table gives the first three (α1, α2, α3), the **global αcr** (the first mode that no single member holds more than half of: the frame's own; an α marked * is one member's own buckling, and a value written ≥ is a lower bound), the **class** and the **amplifier** 1/(1 − 1/αcr). The class follows EN 1993-1-1 5.2.1(3):

- **αcr ≥ 10** (green): the first-order analysis will do.
- **3 ≤ αcr < 10** (amber): second-order effects must be taken, by second order, or by multiplying the sway effects by the amplifier.
- **1 ≤ αcr < 3** (red): a second-order analysis is required.
- **αcr < 1** (red): the loads are past the critical ones, the structure is unstable.

The lowest αcr is highlighted. Pick a combination and a mode, then **Show buckling mode** to see the shape in 3D, swinging like the natural modes (the card has the same scale and Stop). The steel checks say when αcr is below 10 and second order is off.

> The values are indicative: αcr is of the bars alone (the shells have no geometric stiffness), concrete is at its uncracked stiffness, and the judgement stays the engineer's.

## Supports on springs, and members that work one way

Select a footing, a slab on the ground or a steel column base: its properties have an **Analysis support** section.

- **Springs**: a stiffness for each of the six freedoms (kx, ky, kz in kN/m; krx, kry, krz in kN·m/rad). Empty means as the support holds it, 0 means free.
- **Winkler's bed** (footings and slabs): the modulus of subgrade reaction **ks** (kN/m³) of the ground. A pad footing takes kz = ks·B·L and the matching rotational stiffnesses, a strip footing each node its length, a slab on the ground each node its share of the area. The list under it gives typical ks values by soil, **indicative only: ks is the geotechnical engineer's to give**.
- **Compression only (lifts)**: a support that would pull is lifted off the ground, and the combination is solved again without it. The section lists the stiffness each node got in the last analysis.

The forces in the springs are reactions. A slab takes ks only; a column base takes springs only (it has no footprint).

![A footing's Analysis support](shots/analysis-support.webp)

A bar's **Analysis** section has **Acts in**: **Both ways**, **Tension only** (a brace of a cross, a tie) or **Compression only** (a strut). A one-way bar that would work the other way is taken out of that combination, and the Stability table names the **slack members** and the **lifted supports** of each. The cases stay linear, with every member in and every support seated; so does the modal analysis. Each change is one undo step.

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
