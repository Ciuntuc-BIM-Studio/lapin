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

A material with no density is left out, and the Summary says which.

## A room's imposed load

Select a room: its properties have an **Imposed load** section. Its **Category** is **From function** (a living room, a bedroom or a kitchen is A, an office B, a storage room E1, a garage F) unless you choose one, or **None**. Below it is the **qk** the category gives.

![A room's Imposed load in its properties](shots/loads-room.webp)

## The imposed loads table

The **Imposed loads** tab lists each category with its use and its **qk** in kN/m², as EN 1991-1-1 recommends. Type your national annex's value in the field; **Overridden** goes back to the default.

![The Imposed loads tab](shots/loads-imposed.webp)

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
