---
title: Loads
order: 51
summary: Load cases, area, line and point loads, the imposed loads of rooms, the derived loads and the EN 1990 combinations, for a structural analysis.
---

The **Loads** group of the **Structure** tab holds the loads a structural analysis takes: **Load cases…**, **Area load**, **Line load** and **Point load**. Values are in kN, kN/m and kN/m².

![The Loads group of the Structure tab](shots/ribbon-loads.webp)

> The values Lapin ships (the imposed loads of EN 1991-1-1 and the factors of EN 1990, as recommended) are **indicative**. Set your national annex's values and check them before you use the results.

## Load cases

**Load cases…** opens the Loads dialog. **Add standard cases** makes G1 (self weight), G2 (finishes and partitions), Q-A (imposed, category A), S (snow) and W (wind).

![The Cases tab of the Loads dialog](shots/loads-cases.webp)

A case has a **Code**, a **Name**, a **Type** (Permanent, Variable or Accidental), a **Source** (Self weight / dead load, Completion (finishes), Imposed, Snow, Wind, Earthquake or User defined) and, for an imposed case, a **Category**. The three fields ψ0, ψ1 and ψ2 are the combination factors; leave them empty to take the category's from the table. A value that is not a number from 0 to 1 is marked and is not kept.

**Add missing cases** makes one imposed-load case for each category the rooms use that has none.

## Placing loads

- **Area load**: click the corners, then press <kbd>Enter</kbd>.
- **Line load**: click its two ends.
- **Point load**: click where it acts.

While a tool is on, the command line's bar offers the load's **value** and the **load case** it goes in (by default **Imposed load (Q-A)**). The load goes on the active level.

![An area load of 2.5 kN/m² drawn in the living room](shots/loads-plan.webp)

## What Lapin works out for you

You do not draw these; Lapin derives them from the model each time you ask, and they follow every change:

- The **self weight** of floors, walls, columns and beams, from the densities of their materials, in the case whose source is self weight (G1).
- The **floor finishes**, from the layers of the floors, and the **non-load-bearing walls**, as line loads, in the case that lists finishes and partitions (G2).
- The **rooms**: the imposed load of a room's category over its area, in the case of that category (Q-A, Q-B…).
- The **snow** and the **wind**, once you give the site's values (see [Snow and wind](#snow-and-wind)).

A material with no density is left out, and the Summary says which.

## A room's imposed load

Select a room: its properties have an **Imposed load** section. Its **Category** is **From function** (a living room, a bedroom or a kitchen is A, an office B, a storage room E1, a garage F) unless you choose one, or **None**. Below it is the **qk** the category gives.

![A room's Imposed load in its properties](shots/loads-room.webp)

## The imposed loads table

The **Imposed loads** tab lists each category with its use and its **qk** in kN/m², as EN 1991-1-1 recommends. Type your national annex's value in the field; **Overridden** goes back to the default.

![The Imposed loads tab](shots/loads-imposed.webp)

## Snow and wind

The **Snow and wind** tab holds the site's climate. Lapin does not ship the national maps: read the values for your site from **CR 1-1-3/2012** (snow, its Figure 3.1 or Annex A) and **CR 1-1-4/2012** (wind, its Figure 2.1 or Annex A), or from the national annex you work to, and type them in.

![The Snow and wind tab](shots/loads-climate.webp)

- **Snow**: the **ground snow load s0,k** (kN/m²), the **exposure Ce** (full 0.8, partial 1.0, sheltered 1.2), the **thermal coefficient Ct** (1.0 unless the roof is glazed and loses heat) and the **importance factor γIs** (1.0 for ordinary buildings). **Apply the snow**.
- **Wind**: the **reference wind pressure qb** (kN/m²), the **terrain category** (0 to IV, from the sea to a town centre) and the **importance factor γIw**. **Apply the wind**.

Applying them makes the cases their loads go in:

- **S**, the undrifted snow, and **S+X**, **S-X**, **S+Y**, **S-Y**, the snow drifted by a wind blowing towards each side: on a roof of two slopes, the slope facing the wind takes half. A drift that changes nothing here (no slope faces that wind) is left out, and the Summary says so.
- **W+X/s**, **W+X/p**, and so on for the four directions: the wind blowing towards +X, −X, +Y, −Y, once with the roofs' **suction** (/s) and once with their **pressure** (/p).

The arrangements of the snow and the directions of the wind are alternatives: a combination takes only one of each.

What Lapin works out from them, each time you ask:

- **Snow** on each roof slope, per m² of plan: s = γIs · μ1 · Ce · Ct · s0,k, with μ1 = 0.8 up to 30°, then less, 0 at 60° and steeper; on a floor nothing covers (a terrace, a flat roof) μ1 = 0.8.
- **Wind** on the walls and roofs: the building's outline is the hull of its walls and columns; the façades take zones A to E, the flat roofs F to I, a roof of two slopes F to J, by the coefficients of EN 1991-1-4, at the pressure of the building's height for the terrain category. A façade passes its wind to the beams, rails and floor edges along it, its lowest strip to the ground; a roof to its rafters or purlins. Hipped roofs, the internal pressure and snow drifts at parapets are not derived yet.

> These values are **indicative**: the coefficients are the norms' recommended ones. Check them against your national annex.

## Summary

The **Summary** tab shows, for each case, the loads you drew and the total in kN, in all and on each level, then the derived loads one by one, with what they come from. It warns of rooms whose category has no case, and notes what was left out.

![The Summary tab](shots/loads-summary.webp)

## Combinations

The **Combinations** tab lists the combinations of the cases, after EN 1990 as recommended and indicative: for the ultimate limit state 1.35 on permanent actions and 1.5 on the leading variable one, the others with their ψ0; for the serviceability limit state the characteristic, frequent and quasi-permanent ones. A case that holds nothing takes no part.

![The Combinations tab](shots/loads-combinations.webp)

## Analysing

**Run analysis** (in the same group, and in the Loads dialog) analyses the frame under these loads: see [Structural analysis](analysis.md).

## In the IFC file

**Export IFC, Fragments or BOS…** has **Structural analysis view (analytical model and loads)**: the file then carries the analytical model of the bearing walls, floors, columns and beams, with the load cases, the loads and the combinations, as an IFC Structural Analysis View. See [Import and export](import-export.md).
