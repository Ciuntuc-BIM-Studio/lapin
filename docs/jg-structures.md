# JG Structures: the analytical model exchanged as JSON

Decision 357. JG Structures is a structural analysis (FEA) program in development, ETABS-like. The collaboration is by export: Lapin writes the program's exchange file, a JSON document after its contract **`JG_Structures_v2_exchange`, `schema_version` 1.0.0**. Lapin exports only; results are not read back yet.

- **Where:** File → **Export JG Structures (JSON)…** (the IFC and Fragments section of the menu; a guest of a shared session may export too). The engine's queries are `jg.survey` (storeys and how many members of each kind), `jg.preview` (the report, no file) and `jg.export` (`{name, data, report}`, the file as base64 UTF-8), all three with `{units?: "contract"|"si", storeys?: [ids], kinds?: [...], allWalls?: bool}`. The MCP bridge has `lapin_export_jg {path, units?, storeys?, kinds?, allWalls?}` (docs/mcp.md).
- **The code:** `engine/src/Platform/Lapin.Platform/Analysis/` — `Analytical.cs` (what modules give: `IAnalyticalElements`), `AnalyticalModel.cs` (the assembly: snapping, nodes, splits, supports), `SectionProperties.cs`, `StructuralMaterials.cs` (with `Units`), `JgExchange.cs` (the document and the queries), `JgValidator.cs` (the contract's rules). The modules' contributions: `Lapin.Structure/Analytical.cs` (columns, beams, footings), `Lapin.Architecture/Analytical.cs` (walls, floors), `Lapin.Steel/Analytical.cs` (steel members, column bases). The platform service is meant for IFC's Structural Analysis View later too.
- **The schema:** `docs/jg-structures/schema-1.0.0.json`, a JSON Schema (draft 2020-12) written from the contract's logical schema; the tests validate every export against it (`JgExportTests`, with a small validator in the test project, no production dependency).
- **Tests:** `engine/tests/Lapin.Engine.Tests/JgExportTests.cs`.

## What is exported

| Contract entity | From Lapin |
|---|---|
| `project` | The IfcProject's GlobalId and name, `source_application: "Lapin"`, the project's file name. |
| `units` | Declared once: `cm`, `kgf`, `kg`, `C`, `kgf/cm2`, `kgf/cm3`, and `cm2`, `cm4`, `cm3`, `cm6` for sections (the contract's units). SI on request (below). |
| `coordinate_system` | Right-handed, x east, y north, z up (Lapin's own axes), `frame_local_axes` stated (below). |
| `stories` | Every storey: its GlobalId, name, elevation (its base level) and height. |
| `grids` | Each IfcGrid as a cartesian grid: its axes parallel to Y in `x` (their X), parallel to X in `y`; an oblique axis is left out with a warning. |
| `materials` | Every material a member uses (below); `lapin-unknown-material` when a type names none, all its values null. |
| `sections` | A frame section per column, beam or member type; a shell section per wall or floor type's core (its thickness; a wall of its own thickness gets its own). |
| `nodes` | The analytical model's nodes (below), `N1…` in order of height, then y, then x. |
| `frames` | Columns, beams (curved ones as chords, at most 1 mm off the arc) and steel members. |
| `shells` | Load-bearing walls and floors; `pier_label` and `spandrel_label` null (Lapin has no piers or spandrels yet). |
| `documentation` | The rules of this file: null policy, units and their conversion, the analytical model's rules, the local axes, the catalogue policy, what is missing for analysis, what was left out and why, the warnings. |

Ids are the elements' GlobalIds (`frames[].id` = the column's GlobalId), a piece of one `GlobalId#n` when the element is split, so a later results file maps back. The contract does not forbid extra fields: Lapin's own data goes under a namespaced **`lapin`** object on each item — `{globalId, piece, pieces, name, ifcClass}`, a frame's `role` (a hall member's), `offset_z` (a beam moved into its slab's mid-plane: its centroid's height above the analytical line), `mirrored`, a support's `{support, source}` on its node, a section's `centroid_offset`. An importer may ignore it.

## The analytical model

Built from the physical model each time (`AnalyticalModel.Build`), in metres, then converted:

1. **Members.** A column from its foot up its height on its point; a beam on its whole axis (column centre to column centre, not the clear span), its centroid half its depth under its top; a steel member on its axis through its section's box centre. A wall on its core's centre line (its axis moved across to the core's middle; an arc as planar panels on chords) from its base to its height; a floor at its core's mid-plane over its outline. Finishes are left out. Walls count when they are load-bearing (`Pset_WallCommon.LoadBearing` on the wall or its type, or its own `loadBearing`), every wall on request.
2. **Levels.** A horizontal beam lies in the mid-plane of the floor it carries (a floor within its depth, over it), its eccentricity in `lapin.offset_z`. A column's or a wall's head and foot go to the highest beam, rafter or floor at that point within 1 m below the end, preferring one whose axis passes through it (5 cm).
3. **Plan.** Beam ends move onto column axes within the column's half diagonal; wall ends onto columns and onto another wall's centre line in a T (within its half thickness); a floor's corners onto the nearest support node at its level (column ends, wall corners, beam ends) within 0.5 m. Two bar ends that nearly meet (a portal's rafters at the apex, set back by their connection) move to where their axes cross.
4. **Nodes.** Points within **1 cm** are one node.
5. **Splits.** A convex floor is split along the beams and wall heads crossing it from edge to edge; a non-convex one stays one shell (a note says so). A wall is split where another wall ends on it or a column stands in it. Every node on a shell's edge becomes one of its nodes; every node on a bar's span splits it.
6. **Supports.** The lowest column and wall feet over a footing (a pad's footprint, a strip's axis and width, standing at most 2 m above it) are **fixed** (a concrete footing cast with what stands on it); a steel column base is **pinned** or **fixed** as its connection says, over the footing. A foot that stands on nothing and meets nothing else keeps `restraints: null` and is listed in the warnings; nothing is assumed.
7. **Storeys of nodes.** A node takes the storey whose level it lies on (the nearest within 0.5 m: a floor's mid-plane and the beams in it belong to the level they carry), else the storey it stands in.

What it does not do yet (and says so in the warnings): eccentric connections — purlins on rafters, rails on columns, secondary steel crossing over a member — are not linked (no rigid links in 1.0): their ends are listed as meeting nothing. Openings in walls and floors are not in the file. Non-convex floors are not split at inner supports. Walls attached to roofs keep their own height.

## Frames' local axes

Stated in `coordinate_system.frame_local_axes`:

- **Local 1** runs from `node_i` to `node_j` (a column from its foot up; a beam as it was drawn).
- **Before rotation**, local 2 is global **+X** for a vertical frame (local 1 within 0.001 rad of Z), else the **upward** unit vector perpendicular to local 1 in the vertical plane containing it. Local 3 = local 1 × local 2 (right-handed).
- **`local_axis_rotation_deg`** turns local 2 and 3 about local 1 by the right-hand rule.
- **The section's depth h lies along local 2, its width b along local 3.** A beam at 0° has its depth vertical, so `I33` is its strong axis; a column at rotation θ in Lapin (its section's depth along its local y turned by θ) has `local_axis_rotation_deg = θ + 90`. A steel member's angle is its own rotation about its axis.

## Section properties

`SectionProperties.Of`, about the centroid, in the local axes: `area`; `I33 = ∫ y₂² dA` (bending about local 3), `I22 = ∫ y₃² dA`; `S = I / farthest fibre`; `Z` the plastic modulus about the equal-area axis; `J` the torsion constant. `property_status` is `derived_user_geometry` when computed, `pending` when the geometry cannot give them (then null).

| Shape (`geometry.shape`) | Method |
|---|---|
| `rectangle` | Closed form; J = a·b³·(1/3 − 0.21·(b/a)·(1 − b⁴/(12a⁴))) (Roark). |
| `circle`, `pipe` (CHS) | Closed form (exact). |
| `i_wide_flange`, `channel`, `tee`, `angle` | The outline's exact polygon integrals (Green's theorem), a catalogue profile's root fillets rounded in as arcs; the plastic axis by halving the area (clipping). J: rolled I formula It = 2/3·(b − 0.63·tf)·tf³ + 1/3·(h − 2tf)·tw³ + 2·α·D⁴ (with its root radius); Σ b·t³/3 for U, T, L. |
| `box` (RHS) | Polygon integrals with EN 10210-2 corners for a catalogue profile (ro = 1.5t, ri = t); J by EN 10210-2 Annex B. |
| `cold_formed_c`, `_z`, `_sigma` | The sheet as a polygon (sharp bends); J = L·t³/3 along the centreline. |

Checked in the tests: a 30 × 50 rectangle (A 1500 cm², I33 312 500 cm⁴, I22 112 500 cm⁴, S33 12 500, Z33 18 750, J 281 737 cm⁴); an IPE 300 within 1% of its published values (A 53.81 cm², Iy 8356 cm⁴, Wel,y 557.1 cm³, Wpl,y 628.4 cm³, Iz 603.8 cm⁴, Wpl,z 125.2 cm³) and It within 2% (20.12 cm⁴); a CHS 114.3×5, an RHS 150×100×6, a C 200×65×2 and an L 100×100×10. L and other unsymmetric sections are given about their geometric axes, not their principal ones (the contract has no I23 or principal angle).

**Catalogue profiles.** A profile of Lapin's catalogue with its dimensions unchanged (IPE, HEA, HEB, UPN, L, RHS, CHS) is `kind: "catalog"` with `catalog {standard, family, designation, source}` (EN 10365, EN 10056-1, EN 10210-2). Lapin holds no tabulated profile data: only nominal h, b, tw, tf and the root radius, so its properties are derived (`derived_user_geometry`, a note says why), **never `verified_tabulated`**. A section is never taken from a type's name: only from the profile the type carries. Cold-formed C, Z, Σ are generic in Lapin (no manufacturer), so `user_defined`.

## Materials

`StructuralMaterials.Of`, first the project's own data, then the strength class in the name:

- **The project's data** (Manage → Materials, decision 326): `Pset_MaterialMechanical.YoungModulus` and `PoissonRatio`, `Pset_MaterialSteel.YieldStress`, `Pset_MaterialConcrete.CompressiveStrength` in IFC's SI units (Pa), the density (`Pset_MaterialCommon.MassDensity`, kg/m³). A value no building material has in that unit (a modulus of 30 000 typed in MPa) is left out with a note, never reinterpreted. `source.kind: "project_data"`.
- **Concrete C12/15 … C90/105** in the name: fck and Ecm by EN 1992-1-1:2004 Table 3.1 (C25/30: fck 25 MPa, Ecm 31 GPa), ν = 0.2 (§3.1.3(4)). `compressive_strength` is fck (the characteristic cylinder strength). `source.kind: "standard"`, each value's reference in `source.values`.
- **Steel S235 … S460** in the name: fy for t ≤ 40 mm by EN 1993-1-1:2005 Table 3.1, E = 210 000 MPa and ν = 0.3 (§3.2.6), ρ = 7850 kg/m³ (§3.2.6).
- **Weight density:** a reinforced concrete (the name says reinforced/armat) 25 kN/m³ by EN 1991-1-1:2002 Table A.1; steel by EN 1993-1-1 §3.2.6.
- Anything else stays **null** (a material "Concrete" without a class has no E), and is listed in `documentation.missing` and the report. `kind` is steel, concrete, timber, masonry or other, from the class, the category or the name.

## Units

Lapin works in SI. The contract's units by default; **SI (m, kN, kN/m², kN/m³; m², m⁴, m³)** on request, declared in the same `units` block — Lapin's **proposal for 1.1** (the contract declares its units once, and does not forbid others, but 1.0 shows only cm/kgf).

- 1 kgf = 9.80665 N exactly (standard gravity).
- Lengths: m × 100 → cm. Areas, inertias, moduli by the same factor squared, to the fourth, cubed.
- Stresses: Pa / 98 066.5 → kgf/cm² (C25/30's Ecm 31 GPa = 316 112.0 kgf/cm²); SI: Pa / 1000 → kN/m².
- Weight density: ρ kg/m³ weighs ρ kgf/m³, so × 10⁻⁶ → kgf/cm³ (steel 0.00785); SI: ρ·g / 1000 → kN/m³.
- Numbers are written to nine significant digits. No NaN or Infinity is ever written; `null` is never 0.

## Validation

`JgValidator.Validate` checks every document before it is written (the export refuses a document that fails, which would be a Lapin bug): the required fields of each entity, ids unique within their entity, every `node_i`, `node_j`, `node_ids`, `story_id`, `section_id`, `material_id` resolving, `node_j` ≠ `node_i`, at least three distinct nodes to a shell, coordinates exactly `[X, Y, Z]`, restraints six booleans or null, every number finite, the units declared, `property_status` one of three, a catalogue section with its standard, family, designation and source, `verified_tabulated` only with a table or a page in its source. The report adds what analysis needs: materials without E, ν, fy/f'c or density, sections without properties, feet without supports, bar ends meeting nothing — "the model can be imported with pending properties, but analysis that needs them must stay disabled" (the contract's analysis policy).

## Suggested for 1.1

What the collaboration would gain next, to agree with JG Structures:

1. **SI units** (as Lapin already writes them) as an equal option of the contract.
2. **A local axis vector** per frame (local 2 as a vector) besides the angle, so no convention needs explaining.
3. **End releases** per frame end (6 booleans), and **end offsets** (rigid zones: Lapin knows each beam's clear span between column faces) with their schema.
4. **Openings** in walls and floors (polygons on the shell).
5. **Line and area supports**, and springs (soil), besides node restraints.
6. **Diaphragms** per storey (rigid / semi-rigid), and **rigid links** for eccentric connections (purlins on rafters).
7. **Loads**: load patterns and cases (dead, live, snow, wind, seismic), area loads on shells from Lapin's rooms and finishes, line loads from walls not exported as shells.
8. **The GlobalId per element** as a field of the contract (Lapin puts it under `lapin.globalId` today).
9. **Principal axes** (I23 or the principal angle) for unsymmetric sections.
10. **A results file** (displacements, member forces, reactions, by the same ids), for Lapin to read back and show.
