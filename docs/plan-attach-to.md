# Attach top / bottom to a body (`element.attachTo`): feasibility and plan

Asked on 18.11.2026, after forum topic 161 and the finish-painter fix: *can the way walls attach to a roof be generalised, so the top or the bottom of columns, beams, slabs and other shapes is attached to a target body?*

This is a study and a plan, not an implementation. Nothing in `engine/` or `ui/` was changed. Decision row 688 in `docs/decisions-pending.md` points here. File:line references are to `origin/main` at 04d746c5.

**The short answer.** Yes for **elements that are a prism along a vertical axis**: walls (done today), columns (concrete, timber, steel), the top of stairs, and Studio solids. Their "end" is one number along the axis (or a profile along a wall), and the target's surface either cuts that end or extends it. It does not make sense, or not in the same mechanism, for doors and windows, MEP runs, railings, roofs, imported references and component members. Slabs, ceilings, curtain walls and slabs on terrain are possible later, but each needs its own body builder for a surface that is not flat. That is real work and not a generalisation.

The mechanism is small. It needs:
- one platform sampler, moved out of `WallTops`;
- one service that modules implement;
- two commands;
- the rule that **attaching is a body-stage operation**: it never changes what other elements read of the attached element. That rule is what prevents cycles (§3.6).

Effort: about **14 days** for the first version (stages A0–A7), plus about 7.5 days for the later classes.

---

## 1. What exists today (verified)

### 1.1 Walls: two separate notions of "top"

**Level top (`WallTop`, forum #45).** Stored as `top` (`"above"`, a storey id, or missing) plus `topOffset`. The height is derived on every ask (`engine/src/Modules/Architecture/Lapin.Architecture/WallTop.cs:7-17, 45-64`). Problems are reported by `WallTop.Problem` (`WallTop.cs:66-75`, added to the geometry's diagnostics at `Walls.cs:268`).

**Body top (`WallTops.Profile`).** A profile `(u along the axis, v above the base)` (`WallTops.cs:24-31`), made from three sources:
- **Lapin roofs, read by their solved planes** (`Roofs/RoofTop.cs:62-121`). Roof shapes are cached per revision (`RoofTop.cs:33-39`). There are gable and eave rules: a wall under an eave stops under the wall plate of a framed roof (`RoofTop.cs:76-81`).
- **Any other body, read as triangles.** Lapin's own roofs are excluded, except the deck of a flat roof when attached (decision 329, `WallTops.cs:91`). This covers Studio solids, slabs, beams, imported bodies and anything with a geometry producer or stored body (`WallTops.cs:74-115`):
  - the triangles are cached per entity and revision (`RevisionCache`, `WallTops.cs:71, 97`; `Platform/Lapin.Platform/RevisionCache.cs:16-36`);
  - the cache replays the target's reads into the caller's tracking view, which is why the wall rebuilds live when the target changes;
  - vertical triangles are dropped, and each triangle is flagged `Down` (`WallTops.cs:109-111`).
- **The wall's own `topProfile`**, as a file gives it (`WallTops.cs:38-51`). A curved wall has none.

The modes are held in `roofTop`: `auto` (the storey's roofs), `attach` (`roofIds`, else every roof), `none`. The field is declared at `ArchitectureModule.cs:137`.

**Commands.** `architecture.attachToRoof {ids, roofIds}` and `detachFromRoof {ids, mode: none|auto}` (`ArchitectureModule.cs:1156-1192`). Each is one undo step. A wall can never be a target (`:1165`). A target needs a body (`:1166`).

**How the sampler reads the underside** (`WallTops.Underside`, `WallTops.cs:137-181`):
- It samples **only along the axis line**, every `clamp(L/200, 0.02, 0.1)` m (`:156`).
- It takes the **lowest down-facing triangle above the base** at each sample (`:145-155`).
- It finds a jump to 1 mm by bisection and keeps it as a step (`:161-178`).
- It simplifies to 0.5 mm (`:226-236`).
- It merges with the roof profile by taking the lower of the two (`:198-223`).
- An imported (recognised) wall is capped at its file height (`:30, 54-65`).

**How the body uses it** (`Walls.cs:308-309, 414-421`):
- The wall is built to `max(own height, profile max)`, so it extends.
- The region above the profile is then cut off (`Walls.AboveRoof`, `Walls.cs:1100-1115`). That region is extruded **across the whole thickness**, so the top is level across the wall (see §2.3).
- The side faces are sampled each on its own line (`Faces.cs:71`). The finish painter therefore follows the slope on each face (decision 686).
- `Walls.Envelope`, the body a wall cuts other elements with, **ignores the attachment** and uses the level height (`Walls.cs:1118-1130`). This is the rule §3.6 generalises.

**UI.** The tool `architecture.attachRoof` (AR) is a pick gesture with a hard-coded class list (`ui/src/modules/architecture/index.tsx:378-388`). It always sends **one** target, so the command's list replaces the previous one. "Detach from roof" is beside it (`:390-397`), in the ribbon under Joins (`:656`). The guide covers it in `docs/user/walls.md:98, 267` and `docs/user/floors-roofs.md:118, 130`. No guide scene drives it.

**Copies.** `roofIds` is a relation key for storey copies (`StoreyCopies.cs:24`). Splitting a roof adds the new piece to the attached walls' `roofIds` (`Roofs/RoofPieces.cs:104-107`).

**Problem found while reading.** When the named target of an attached wall is deleted, nothing removes it from `roofIds`:
- `WallTops.Bodies` then finds no named entity and falls back to **every roof in the project** (`WallTops.cs:81-82`; the same in `RoofTop.AttachedRoofs`, `RoofTop.cs:47-48`).
- So a wall attached to a Studio solid that is later deleted rises to any roof over it, on any storey.
- No Problem is reported.

The fix is small and belongs in stage A0.

### 1.2 Other classes

| Class | Top / bottom today | What "attach" would mean | Makes sense? |
|---|---|---|---|
| **Walls** (straight) | `WallTop` (level) + `WallTops.Profile` (bodies) | Done for the top. Bottom: the base follows a body's top face (a wall on a stepped footing, on a stair, on sloping ground) | Top: yes, exists. Bottom: yes, later (A8) |
| **Curved walls** | The same; the profile is sampled per chord step (`Walls.cs:548`); no own `topProfile` (`WallTops.cs:45`) | The same as straight walls | Yes, exists for the top |
| **Curtain walls** (`IfcCurtainWall`, `CurtainWalls.cs:228`) | Only the stored `topProfile` (`CurtainWalls.cs:556-568`, used at `:369, 404, 479, 488`); height `height`; **never follows a roof live** | The grid's top follows the profile; panels and mullions are cut along it, as with a file's gable | Yes, later (A9): the layout already takes a profile, so it only needs to read `WallTops.Profile` |
| **Columns** (structure, any material) | `baseOffset` + `height` (own, then type, then storey height) (`Structure/Lapin.Structure/Members.cs:28-40`) | Top: the axis end goes to where the axis meets the target; the end face takes the target's surface over the section. Bottom: the same, downwards | **Yes, v1** |
| **Steel columns** (structure column with a steel section, body rebuilt by `SteelBody` when `steelEnds` is set; `Steel/Lapin.Steel/SteelModule.cs:17-31, 378-390`) | `SteelEnd`: extension, `square`, `plane` (two angles), `mitre`, `fit` to another member's section box (`SteelEnd.cs:3-22`, `SteelBody.cs:84-98`) | Attaching the top is a `plane` cut whose plane comes from the target at the axis hit, kept live | **Yes, v1** (it reuses `CutPlane` and the IFC clip) |
| **Timber columns** | Structure columns with a timber material; there is no separate post class (the wood module has frames, joists, CLT) | The same as columns | Yes, through columns |
| **Beams** (structure) | Top = storey level + storey height + `topOffset` (`Members.cs:255`); flat; ends trimmed at column faces (`BeamSpan`, decision 225, `Members.cs:96-170`); steel beams fitted by `SteelEnd.fit` | "Top to a slab soffit" is a derived `topOffset` (flat). A sloped top is no longer a beam of constant section. Ends to a body are trim/extend, which exists in plan (`element.trimExtend`, `Platform/Lapin.Platform/PathEdits.cs:56-81`), at column faces (`BeamSpan`) and for steel (`fit`) | **Not in v1.** Possibly "top to a soffit, flat" later |
| **Slabs / floors** | Flat: `offset` from the storey (`Slabs.cs:336`); layers laid down from the top; no slope anywhere in `Slabs.cs` | The top follows a surface: that is a roof or a ramp, not a floor. Bottom on terrain: the lowest layer is thickened down to the ground (lean fill) | Top: no. Bottom on terrain or a body: later (A10); needs a non-prismatic body |
| **Ceilings** | Flat at `height` above the storey (`Ceilings.cs:298`) | A ceiling that follows a roof's underside (an attic): the board becomes a surface draped over the outline | Later (A11); grid ceilings stay flat |
| **Roofs** | `eaveOffset` / wall plate | The eave to a wall's top: the inverse of today's relation, and a cycle with every wall attached to the roof | **Never** in this mechanism |
| **Stairs / ramps** | `baseOffset`, `topStoreyId` + `topOffset`, else `height` (`Stairs/StairBuilder.cs:17-25, 83-108`) | The top landing stops at a body's top face (the finished floor of a slab): the rise is derived from the slab's top at the stair's top point | **Yes, v1** (one point; the risers come from the derived rise) |
| **Railings** | `baseOffset`, `height`; follow their stair or host | They follow their host | Never (the host decides) |
| **Strip footings** | `bottom` from the type (`Foundations.cs:208-231`) | The bottom is a design depth (frost, bearing stratum), not a surface | No |
| **Pad footings** | Top = the base of the column over it when `follows` (not for imported ones) (`Foundations.cs:584-586, 636-645`) | The relation already exists, in the opposite direction | No (it would loop with the pad following the column) |
| **Piles** | There is no pile class or tool (only names in clash and cost) | — | No subject |
| **Doors / windows** | Hosted in a wall | — | Never |
| **MEP segments** | 3D ends, by the network | — | Never (ports and routing decide) |
| **Studio solids and parts** | Their features (extrude depth, steps); a live boolean against any element (`studio.boolean`, `IfcRelInterferesElements`, `Studio/Lapin.Studio/StudioModule.cs:227-251`) | Top/bottom = keep what lies below the target's underside (or above its top face), and extend the extrusion to reach it | **Yes, v1**: a generic clip filter |
| **Imported references** | The file's stored body (`StoredBodies`) | — | **Never** as the attached element (an existing IFC is not changed). Always allowed as a **target** (today too) |
| **Recognised (native) elements from an import** | Native, flagged `imported` | Allowed only by an explicit user command later. The import itself never attaches (`ForeignElements.cs:374` sets `roofTop: none` for flat-topped recognised walls) | Yes, by the user only |
| **Component members** (`docs/components.md`: Studio solids, slabs, spaces) | Kept identical across instances | Attaching one instance's member would break "instances stay the same" | **Never** in v1 (refused with a message) |

**Scope of v1:** walls (through the new contract, unchanged behaviour), columns of every material (top and bottom), the top of stairs and ramps, and Studio solids (top and bottom).

**Later:** wall bottoms, curtain walls, slab bottoms on terrain or bodies, ceilings under a roof.

**Never:** doors, windows, MEP, railings, roofs, footings, imported references as the attached element, component members.

---

## 2. Targets

### 2.1 What a target can be

| Target | How it is read | v1 |
|---|---|---|
| Any element with a body (Lapin roof, slab, beam, column, Studio solid, an imported element) | Its triangles (§2.2); a Lapin roof by its planes, for walls (unchanged) | Yes |
| A level (storey + offset) | A number. Walls have it already (`WallTop`). For columns it is the same keys (`top`, `topOffset`), so the two read alike | Yes (columns, stairs) |
| A plane given by numbers (point + normal, or level + slope + direction) | A `CutPlane` (`SteelBody.cs:7-11`) | No: there are no reference planes in Lapin (no class, nothing found). A Studio solid used as a "reference body" serves meanwhile. Decide when issue #37 brings reference planes |
| A sloped plane | The same as above, or a Studio solid | Through a body |
| A Studio solid | Triangles | Yes |
| Terrain (`IfcGeographicElement` TERRAIN, `Civil/Lapin.Civil/Terrains.cs:33`) | Better **not** by triangles: `Tin.ElevationAt` (`Tin.cs:231`) and the designed ground `Analysis.Proposed` (platforms, ditches; `Analysis.cs:54-62`) are exact and cheap | Later (A10): a platform `ISurfaceSamplers` service that the civil module implements |

### 2.2 What the `WallTops` sampler can and cannot do

**It can:**
- read **any body**: it takes no notice of the target's class, only of its producer or stored body;
- read **planes and curved targets**: curves come through the target's triangles, which follow `Chords` because the target's body does;
- read **several targets**, taking the lower of them (one triangle list);
- handle **steps and ends**, found to 1 mm;
- handle **holes** in a target: a sample under a hole finds no triangle, or a higher one, and the bisection makes the step;
- run **live**, through the tracking view;
- run **fast enough**: one bounding-box filter per wall, then a linear scan per sample. For a 10 m wall that is about 100 samples × the triangles near the wall. Cached per target and revision, so twenty walls under one roof read its triangles once (`RoofTests.Every_wall_attached_to_a_roof_follows_what_only_the_roof_reads`, `engine/tests/Lapin.Engine.Tests/RoofTests.cs:357-371`).

I did not time it. The tests that exist run it on bodies of a few hundred triangles. A model the size of Bloc-P-4 with hundreds of attached columns is the thing to measure in A2.

**It cannot:**
1. **Read across the thickness or across a section.** It samples one line, and the body's top is then level across the wall (`Walls.AboveRoof` extrudes across). Under a slope that falls across the wall (an eave wall attached with `attach`), half the thickness pokes through by `t/2 · tan(pitch)`: 0.15 m × tan 30° = 87 mm for a 30 cm wall. Columns need the target **over the section area**, not along a line. This is the main new piece (§3.3).
2. **Read a target's top face.** Only `Down` triangles count (`WallTops.cs:143`). "Attach bottom" needs the **highest up-facing face below** the element. The flag is already in `Tri`, so this is a small extension.
3. **Handle concave targets and targets with openings, beyond the first hit.** It takes the lowest down-facing face above the base. For a target whose underside has a pocket (a coffered slab, a beam grid seen from below), it takes the lowest one, which is right for "stop under it". For a target with a void the element passes through (a slab with an opening over a column), it passes through to the next face above, which is right too. What it cannot express is "the second face", for example "stop at the top of a slab I pass through". Nobody has asked for that.
4. **Handle a target below the element's base.** Faces below the base are ignored (`z > w.Base`, `:152`), so a wall is never cut by a body under its foot. That is right for the top. Bottom attach needs the mirror rule (faces below the element's top).
5. **Report.** It returns null whether nothing is over the wall, the target is gone, or the target is somewhere else. There is no Problem for any of these.

---

## 3. The mechanism

### 3.1 Commands

```
element.attachTo { ids, end: "top" | "bottom",
                   targets: [ids]            // bodies; or
                   level: { storey: id | "above" | "below", offset },   // a level
                   offset?: number,          // a gap from the target's face (m, + away from it)
                   mode?: "both" | "extend" | "trim" }                  // default "both"
element.detach   { ids, end?: "top" | "bottom" }                        // both ends when not given
element.attachments { id | ids }   (query: per element and end, what it is attached to, the result, the problems)
element.attachPreview { ids, end, targets | level, offset, mode }      (query: the tool's ghost, §4.3)
```

- **One command, one undo step**, for any number of elements. Elements of different classes may be mixed. Each goes to its class's implementation (§3.2). A class without one refuses the whole command, naming the element (*"„Door 3” has no top to attach"*). It does not skip silently.
- **`mode`.** It says what the target may do to the element's own end:
  - `extend` only lengthens the element to reach the target;
  - `trim` only cuts it back;
  - `both` does either, which is what walls do today (`Walls.cs:309`).
  - The "own end" is the element's height as drawn: its level top, its own height, or its type's.
- **Validation.**
  - The target exists and has a body.
  - The target is not the element itself.
  - No cycle (§3.6).
  - Not a component member, not a reference.
  - The class rules, for example walls never to walls (kept: `ArchitectureModule.cs:1165`), and a pad never attached to the column it follows.
- **Additive or replacing.** `targets` replaces the end's list, as today. The tool adds with Shift (§4.1).
- **`architecture.attachToRoof` / `detachFromRoof` stay** as they are (extend, never break). Scripts, the MCP bridge, recorded macros and old graphs keep working. For walls, `element.attachTo { end: "top", targets }` writes exactly what `attachToRoof` writes.

### 3.2 Where the capability sits

**In the platform** (`Lapin.Platform/Attach/`):
- **`BodySampler`**, moved from `WallTops`, without its wall-specific parts:
  - the `Tri` list per target, cached by `RevisionCache` as today;
  - `ZAt`;
  - along a line: today's `Underside`, plus its mirror `Overside` for bottoms;
  - **over an area**: the section footprint, new (§3.3);
  - `Merge`, `Simplify`.
  - `WallTops` keeps the roof rules (planes, gables, eaves, the plate, the imported cap) and calls the sampler.
- **`IAttachable`**, a service a module registers once per class:

  ```csharp
  public interface IAttachable
  {
      bool Handles(Entity e);
      IReadOnlyList<string> Ends(Entity e);                  // "top", "bottom" (what this element has)
      void Attach(ICommandContext ctx, Entity e, AttachSpec spec);   // writes what it stores
      void Detach(ICommandContext ctx, Entity e, string end);
      AttachState? StateOf(IModelView m, Entity e, string end);       // targets, offset, mode (for Properties and the query)
  }
  ```

  The body builders read the attachment themselves, as walls do now. There is no extra pass and no filter for the classes that implement it properly.
- **`AttachFilter`**, an `IGeometryFilter` for Studio solids only: the generic clip (§5a).

**The implementations:**

| Implementation | Where | Notes |
|---|---|---|
| Walls | `Lapin.Architecture` | Maps `AttachSpec` to `roofTop` / `roofIds` / `top` / `topOffset` |
| Columns | `Lapin.Structure` | — |
| Stairs and ramps | `Lapin.Architecture/Stairs` | — |
| Steel columns | `Lapin.Steel` | Through the columns' attachment, turned into a `plane` end |
| Studio solids | `Lapin.Studio` | — |

### 3.3 Columns: how the end is built

1. **The axis hit.** One ray along the column's axis from its base. The lowest target face above, within the target list, gives `zTop`. The **axis length** `zTop − base` becomes the column's *built height*. The quantities (`Length`, `GrossVolume`), the rebar (`Rebar/ColumnBars.cs:82`), the analytical model and the drawings read it.
2. **The end surface over the section.** Rays at the section's corners, plus the centre. Three cases:
   - All hits lie on **one plane** (within 0.5 mm, the sampler's simplification tolerance): the end is **one cut plane**. That is exact, and IFC writes it as an `IfcBooleanClippingResult` with an `IfcHalfSpaceSolid`, the same path steel ends take (`IfcSolids.cs:17-31`, `FrameIfcBodies`, `Members.cs:360-381`).
   - The hits lie on **two or more planes whose region below is convex** (under a ridge or a hip: the underside is the *minimum* of its planes): **one clip per plane**. Still exact, still written as clips.
   - Anything else (under a valley, a step from a beam's edge, a curved soffit, a hole's edge over the section): the column is built to the highest hit plus one cm, and the **region above the target's underside** over the section's box is taken off with the booleans (`ISolidBooleans`, manifold). The region is made from the down-facing triangles, each extruded up to the box top, as one solid. IFC writes the result as the faceted B-rep it is (the existing fallback, `IfcExport.cs:1082-1100`).
3. **`extend` / `trim` / `offset`** are applied to `zTop` and to the planes before building.
4. **The envelope stays as drawn** (`Columns.Envelope`, `Members.cs:35-40`, takes no services and must not): see §3.6.

A **steel column** uses the same hit. For a single plane, the column's top end becomes a `SteelEnd` with `Cut = "plane"` whose plane is derived on every ask, not stored, so `SteelBody` builds it with no boolean. Several planes go through the boolean path, as above.

A **stair's** top is one point: `TopPoint` (`StairBuilder.cs:647`). The target's top face there, plus `offset`, gives the stair's top level. The risers come from that rise exactly as `topOffset` gives them today (one formula, `StairBuilder.cs:107-108`).

### 3.4 What is stored

Lapin is IFC-native and prefers objectified relationships (booleans are `IfcRelInterferesElements`, joins `IfcRelConnectsPathElements`). For the new classes I propose **a relation**:

```
IfcRelConnectsElements  Relating = target, Related = attached element
  attrs { attach: "top" | "bottom", offset, mode }, name "Lapin attach top"
```

Why a relation:
- Deleting the target deletes the relation, so the dangling-id problem of §1.1 cannot happen for the new classes.
- `core.dependents` and the tracking view see relations without any new code.
- The IFC export has a direct counterpart: the exporter already writes `IFCRELCONNECTSELEMENTS` for formwork, `IfcExport.cs:2548`.
- Copies follow the existing rule for relations: kept only when relations are copied (`StoreyCopy`).

A **level** target stays an attribute (`top`, `topOffset`, as `WallTop`), because a storey is not an element.

**Walls keep `roofTop` / `roofIds`** (attributes). There is no `IProjectUpgrade` and nothing changes in old projects or in files written before. `element.attachments` reads both storages, so Properties, Python and MCP see one model. Moving walls onto relations later would need an upgrade and the reverse mapping in IFC import (Lapin_Intent of older files). There is no benefit to users today, so this is decision D2.

**Nothing derived is stored.** The built height, the planes and the profile are worked out on every ask, as for walls.

### 3.5 Live re-evaluation and dependencies

- **Rebuilding is automatic.** The attached element reads its targets' bodies through `RevisionCache`, which records the target's reads in the caller's tracking view (`RevisionCache.cs:21-35`). The session therefore rebuilds the element whenever anything its target was built from changes (`docs/dependencies.md`). This is exactly why an attached wall follows a sketch moved under a Studio body today (`RoofTests.cs:536-547`).
- **Properties → Dependencies** (`ui/src/shell/DependenciesSection.tsx`, `core.dependents`, `Platform/Lapin.Platform/Dependencies.cs`) shows "Used by" and "Built from" with nothing new: the target appears under "Built from".

### 3.6 Cycles: the one real risk

The engine builds geometry recursively and has **no re-entrancy guard**. I found none in `Session.cs`. `studio.boolean` refuses only the direct pair (`StudioModule.cs:245`). In .NET a stack overflow ends the process: the engine dies, and the desktop app with it.

**Cycles that would happen with a naive implementation:**
1. **Column top to the slab above, and the column cuts that slab.** Columns cut slabs: priority 90 > 60 (`StructureModule.cs:45`, `ArchitectureModule.cs:334`). The slab's body asks for the column's envelope (`ICutters.For`, `Module.cs:170-182`). If the column's envelope read the slab's body, the build would loop. This is the most common case of all.
2. **Column top to a beam's soffit.** The beam's span reads every nearby column's height (`Columns.Spec`, `Members.cs:150-170`) to decide where the beam stops at column faces. If the column's height came from the beam's body, the build would loop.
3. **Explicit loops.** A Studio solid's bottom attached to a column whose top is attached to that solid. A pad that follows a column whose bottom is attached to the pad.

**The rule that removes cycles 1 and 2 by construction** (walls already follow it, `Walls.cs:1118-1130`):

> Attaching changes **only the element's own body and what is measured from it** (its quantities, rebar, drawings, IFC). What other elements read of it does not change: its envelope, its drawn height (`Columns.Spec`), its span, its joins. And a target is read through what is attach-free of it.

For example, the beam keeps stopping at the column's face as drawn. The slab is cut by the column's drawn envelope, which under an attached-to-soffit column never reaches into the slab anyway. Two signatures follow:
- `Columns.Spec(m, column)` stays as it is, the drawn height.
- A new `Columns.Built(m, column, services)` gives the attached height. Its callers are switched one by one in A2: 7 calls of `Columns.Spec` today, in quantities, rebar, analytical, faces and steel.

**Explicit loops (case 3) are refused by the command.** It walks the graph of explicit attachments (relations, `roofIds`, pad `follows`) from each target back to the element, and the message names the path (*"„C1” is attached to „S2”, which is attached to „C1”"*).

**A runtime guard covers anything the command could not see** (a project edited by a script, an old file). It is a `[ThreadStatic]` set of the targets being sampled in `BodySampler`. A target already on the stack is skipped, and the element reports a Problem (*"Its top and „S2” depend on each other: it stands at its own height"*). It never overflows.

### 3.7 Problems

The Problems panel gets these entries, in the geometry's diagnostics, as `WallTop.Problem` does:

- *"Its top is attached to „X”, which is not over it: it stands at its own height."* The target does not cross the element's axis or section.
- *"Its top is attached to „X”, which is under its base."* With `trim`, the element would vanish. It is refused at the command, and reported when it happens later.
- *"Its bottom is attached to „X”, which is not under it."* The mirror case.
- *"„X”, its top went to, is gone."* For walls' `roofIds` only. A relation cannot dangle.
- The cycle message of §3.6.
- *"Part of its section is not under „X”."* The section straddles the target's edge. It is built with the step (the boolean path), but this is said, because it is usually a mistake.

### 3.8 Undo, shared sessions, scripting

- **Undo.** Each command is one undo step (`ctx.Replace` / `AddRelation` inside the command).
- **Shared sessions.** An editor guest runs any `doc.execute` (`EngineDispatcher.cs:735-741`). `AllowedRemote` lists only host-only commands and the on-site commands a viewer may run, so **nothing is added there**. Viewers are refused as for every edit.
- **Python, MCP, Visual Graphs.** They see the commands through the catalogue (`scripts/command-catalogue.mjs` → `engine/src/Platform/Lapin.Protocol/catalogue.json`, where `architecture.attachToRoof` is at line 47). Run `node scripts/command-catalogue.mjs --check` after A1. Graph nodes come from the catalogue on their own. `lapinbim` gets `element.attach_to` from the generated catalogue, and `docs/python.md` gets one example.

### 3.9 IFC

**Export:**
- **The body is the element's own representation, as built.** A column under one plane is an `IfcExtrudedAreaSolid` clipped by `IfcHalfSpaceSolid`s, so Revit, Tekla and Archicad keep a parametric column. A column under a step or a curve is a B-rep. Walls are unchanged.
- **The intent goes in `Lapin_Intent`.** Attributes are written there already, so `roofTop` / `roofIds` reach the file today. For the relation, the attached element's set gets `AttachTop` / `AttachBottom` holding the targets' GlobalIds plus offset and mode. Lapin reads it back natively.
- **Optionally, a standard relationship as well** (decision D5): `IfcRelConnectsElements` (Relating = target, Related = element, Name `Attach top`). IFC4 has no relationship that means "this element's end is shaped by that one". Of the candidates:
  - `IfcRelConnectsElements` is the general one;
  - `IfcRelConnectsPathElements` is for path elements' ends in plan (joins), and would be misread;
  - `IfcRelConnectsWithRealizingElements` needs a realizing element (a bracket, a plate), and there is none.

  I could not determine whether Revit or Archicad import anything from such a relation. I expect both ignore it, which is harmless. The benefit is that a checker (Solibri) and BubbleBIM can see the link.

**Import.** A Lapin file's `Lapin_Intent` brings the attachment back: GlobalIds are stable, so the target ids resolve. A **foreign** file's `IfcRelConnectsElements` is **never** turned into an attachment. The import reproduces the file, and the fidelity gate (`RecognitionFidelity`) measures the recognised element against the file's body. An attachment would change that body, so the import adds none, just as recognised walls get `roofTop: none` (`ForeignElements.cs:374`) and recognised pads do not follow columns (`Foundations.cs:584-586`).

---

## 4. The interface

### 4.1 The tool

- **One tool, "Attach top/bottom", shortcut AR.** It replaces "Attach to roof" in the ribbon. The old tool id `architecture.attachRoof` stays as an alias, so shortcuts and filmed tutorials still work.
- It moves from Architecture → Joins to **Modify**, since it serves every module. It stays reachable from Architecture as well.
- **Options bar:**
  - **Top / Bottom**, a toggle; Tab switches;
  - **Mode**: Both / Extend only / Trim only;
  - **Offset**: a typed value;
  - **Add (Shift)**: adds the clicked body to the end's targets instead of replacing them.
- **Gesture.**
  - Select the elements, start AR, click the target (any element with a body, the selection filter asked of the engine), or click a storey's level line in a section or elevation for a level.
  - Started with nothing selected, it first asks for the elements. Only classes that `element.attachments` says are attachable are accepted: the engine answers `attachable` per class.
- **Detach.** "Detach top/bottom" (the old "Detach from roof" as an alias) acts on the ends set in the options bar.

### 4.2 Selection filter

- **Elements:** the classes with an `IAttachable`, given by the engine. The UI hard-codes none (today's list at `index.tsx:380` goes).
- **Targets:** any element whose geometry has parts, a reference included. Not the element itself, not a wall for walls. The engine refuses the rest with its message.

### 4.3 Preview

`element.attachPreview` (a query, like `array.preview`, `Platform/Lapin.Platform/Arrays.cs:174-200`) returns, for each element, the built end:
- the axis length;
- the planes or profile;
- the outline of the new end face, in world coordinates, which the viewport draws as a ghost;
- and the Problems it would have.

This is the engine's own computation (the rule *a configurator's preview is the engine's*). The UI only draws it.

### 4.4 Properties

Each attachable element gets a line per end in its class section:

> **Top:** attached to „Roof 1”, „Beam B3” · offset 0 · both [Change] [Detach]

- Change starts AR for that end.
- Clicking a name selects the target.
- Detached: *Top: own height 3.00 m* (or *Top: Storey 1 + 0.10*).

The wall's "Top under a roof" field (`ArchitectureModule.cs:137`) stays, showing the same state.

### 4.5 Right-click menu

On a selection of attachable elements: **Attach top…**, **Attach bottom…**, **Detach top**, **Detach bottom** (`ui/src/shell/selectionMenu.ts`). On a single element: **Select what its top is attached to**.

### 4.6 Grips and edits on an attached end

- **Height grip.** An attached end has no height grip in sections or 3D. Columns have none in plan today anyway. The 3D move's vertical part moves the base (`ElementRise`, `Platform/Lapin.Platform/Placement.cs:96-126`) and the top stays attached.
- **Typing a height detaches.** Typing a height or top offset for an attached end detaches that end and says so, as typing a wall's height makes it unconnected today (`ArchitectureModule.cs:1204-1211`). Decision D4.
- **Moving.** Moving or copying in plan keeps the attachment, which is re-read at the new place. A copy to another storey copies the relation only when relations are copied (the `StoreyCopy` rule).

### 4.7 Guide and translations

- `docs/user/walls.md` "Walls under a roof" is rewritten for the general tool. Pages are added or extended in `docs/user/structure.md` (columns), the stairs page and `docs/user/studio*.md`, each in `docs/user/ro` too.
- One new scene in `scripts/user-docs/scenes.mjs`: a column attached under a sloped Studio body, before and after. None drives AR today, so nothing old breaks.
- The FAQ (`ui/src/help/faq.ts:349-350`) is reworded.
- Every new string goes into all languages in `ui/src/i18n`.

---

## 5. Alternatives considered

**(a) The generic live boolean subtract as the fallback.**

*What it does.* Every element would get its end by subtracting "the region above the target" from its body, with no per-class code. It is already possible by hand: `studio.boolean` subtracts any tools from any element (`StudioModule.cs:227-251`).

*Why it is rejected as the general mechanism:*
- It cuts, but it **never extends**.
- The element's own measures stay those of its drawn height: a column's `Length` quantity, its rebar, its analytical line.
- In IFC it is always a B-rep, never a clipped sweep.
- Exports to analysis (SAF, the FEM module) see the wrong length.
- Booleans cost far more than one ray for a 300×300 column under a plane.

*Kept* as the implementation for **Studio solids** (where the body *is* the shape and there is no axis to lengthen; extend is done by adding to the extrusion depth first) and as the **non-planar path inside the column builder** (§3.3, case 3).

**(b) A constraint system between elements (issue #37: link a locked element to a grid axis or reference plane, so it moves or stretches when the axis moves).**

*How it differs.* #37 **moves stored placement**. When the axis moves, the element's coordinates must be written anew, which needs propagation after each command (`ICommandFollowUp`, `Module.cs:159-168`), an order, and conflict rules.

Attaching **stores nothing derived**. The body is worked out from the target on every ask, and the existing dependency tracking does the rest.

*Conclusion.* Not the same mechanism, and attaching should not wait for a constraint solver. They can share:
- the target picker;
- the Problems wording;
- a future **reference plane** class, which would be a valid attach target the day #37 brings one (§2.1).

Recommend keeping #37 separate.

**(c) Only extending each class's own tools** (a "Top to body" field on columns, another on stairs, another on Studio).

It is the cheapest per class, but it repeats the sampler, the cycle guard, the Problems and the UI three or four times. The user already sees AR as one gesture and expects it on columns. *Rejected*, but the design keeps its good part: each class builds its own end in its own body builder (`IAttachable`), and only the sampler, the commands, the guard and the UI are shared.

---

## 6. Stages

Each stage ports or writes its tests first, keeps `pnpm test:engine` green, and ends with a paragraph in `docs/history.md` and its row in `docs/decisions-pending.md`.

**Models** (CLAUDE.md): every engine stage on **Opus**; UI, guide, scene and translations on **Sonnet**; the caller inventory (who reads `Columns.Spec` and `Beams.Top`) on **Haiku**.

| Stage | Content | Days | Model |
|---|---|---|---|
| **A0** | Fix the dangling `roofIds` (§1.1): a deleted target is taken out of every wall's `roofIds` (`IDeletionFollowers`); a wall whose named targets are all gone goes to `none` with a Problem, not to "every roof" | 0.5 | Opus |
| **A1** | `BodySampler` in the platform (moved from `WallTops`, `Up` faces, area sampling, the re-entrancy guard); `IAttachable`, `element.attachTo` / `detach` / `attachments` / `attachPreview`; the walls' implementation over `roofTop` / `roofIds` / `top`; the cycle walk; the catalogue | 2.5 | Opus |
| **A2** | Columns, top and bottom: axis hit, one or several clip planes, boolean fallback; `Columns.Built` and its callers (quantities, rebar, analytical, faces); the envelope as drawn; IFC clips | 3 | Opus |
| **A3** | Steel columns through `SteelEnd` plane ends derived from the attachment; IFC half-space clips | 1 | Opus |
| **A4** | Stairs and ramps: the top to a body's top face or a level | 1 | Opus |
| **A5** | Studio solids: `AttachFilter` (extend the extrusion, then subtract or intersect live) | 2 | Opus |
| **A6** | IFC: `Lapin_Intent` round trip for the relation, the optional `IfcRelConnectsElements`, foreign files untouched, the fidelity gate | 1 | Opus |
| **A7** | UI: the AR tool generalised, options bar, ghost from `attachPreview`, Properties lines, right-click menu, i18n, guide pages (EN + RO), one scene | 3 | Sonnet |
| | **First version** | **14** | |
| A8 | Wall bottoms (base on a footing's or a stair's top, on terrain) | 2 | Opus |
| A9 | Curtain walls following `WallTops.Profile` live | 1.5 | Opus |
| A10 | Slab bottom to terrain or a body: the lowest layer thickened (a non-prismatic slab body); the terrain sampler service in civil | 2.5 | Opus |
| A11 | Ceilings under a roof's underside (a draped board; grid ceilings stay flat) | 1.5 | Opus |

**Order.**
1. A0, then A1. A1 is the contract, and walls keep every number in `RoofTests`.
2. A2 next. Columns are what the user asked for first, and they test the cycle rule hardest.
3. A3, A4 and A5 are independent of one another.
4. A6 after A2. A7 can start after A1 against the commands, with the ghost wired once A2 lands.

### Tests, with expected numbers

Each test uses the engine fixture (`Fixture`, `f.Run`, `f.Bounds`) as `RoofTests.cs` does. Unless noted, the ground storey is at ±0.00 with a height of 3.00. The "sloped body" is a Studio extruded box whose underside is the plane **z = 3.00 + 0.20·x** over x ∈ [0, 6], y ∈ [−1, 1] (a 20 % slope rising toward +x).

**A0**
- A wall attached to a Studio body. The body is deleted, and a roof on another storey covers the wall. The wall stays at its own 3.00. It does **not** rise to the roof, as it does today. Problems name the gone target, and `roofIds` is removed.
- Splitting a roof still adds the piece to the attached walls (`RoofPieces.cs:104`): regression.

**A1**
- Every existing test that uses `attachToRoof` passes unchanged. That is 20 calls in 8 files, among them `RoofTests.cs:318-330` (rise by tan 28° a metre, follows `eaveOffset` +0.5) and `:526-552` (the slope's triangle, volume ratio 51/39).
- `element.attachTo {ids: [wall], end: "top", targets: [body]}` writes the same attributes as `architecture.attachToRoof`, and gives identical geometry and an identical volume.
- `element.attachTo` on a door is refused, naming it. On a wall with a wall target it is refused, with today's message.
- Cycle: S (Studio) bottom attached to C (column) after C's top is attached to S is refused, naming both.
- A cycle forced by hand-editing the model (a relation added directly) leaves no stack overflow: C stands at 3.00 and Problems has the cycle message.
- One undo restores the previous attachment exactly, and redo reapplies it.

**A2: columns**
- **The basic case: a 0.30 × 0.30 column at (2, 0), drawn height 3.00, top attached to the sloped body, mode both.**
  - Axis length 3.400. Top corners at x = 1.85 → 3.370 and x = 2.15 → 3.430. `Bounds.Max.Z` = 3.430.
  - Volume = 0.09 × 3.40 = **0.3060 m³**. `Qto_ColumnBaseQuantities.Length` = 3.400.
  - IFC: one `IfcBooleanClippingResult` with one `IfcHalfSpaceSolid` whose normal is (−0.196, 0, 0.981).
  - Raise the body 0.50: Max.Z = 3.930 (live). Detach: 3.000 and 0.2700 m³.
- **Trim under a slab soffit.** Storey 1 at +3.00 with a default floor (`Slabs.DefaultLayers`: 0.02 + 0.05 + 0.15, soffit at **2.78**), and a column with its own height 4.00.
  - `trim`: top 2.780, volume 0.2502 m³.
  - `extend` with its own 4.00: it stays 4.000. The same column at 2.50 with `extend`: 2.780.
  - `trim` with its own 2.50: it stays 2.500.
  - The slab keeps the hole the column's drawn envelope cuts, where it overlaps. With `trim` it does not overlap, so the slab's volume is unchanged by the attachment (this checks the cycle rule's first case).
- **Under a ridge.** A Studio gable body: planes z = 3 + 0.2·x for x ≤ 2 and z = 3.8 − 0.2·x for x ≥ 2. A column at x = 2: two clips, apex 3.40 at the axis, corners at 3.370. Volume = 0.09 × 3.40 − 2 × (0.15 × 0.30 × 0.03 / 2) = **0.30465 m³**. IFC: two half-spaces.
- **A step.** Half the section under a beam soffit at 2.60, half under the slab soffit at 2.78. It goes through the boolean path. Volume = 0.045 × 2.60 + 0.045 × 2.78 = **0.2421 m³**, written as a B-rep, with the "part of its section" Problem.
- **A beam over an attached column.** The beam's clear span is unchanged by the attachment (still axis − 0.30 at the column's face). This checks the cycle rule's second case.
- **Bottom.** The column's base is attached to a Studio ramp whose top is z = 0.10·x, with the top at the storey's 3.00. Base 0.200 at the axis, corners 0.185 and 0.215, axis length 2.800, volume 0.2520 m³.
- **Rebar.** `ColumnBars`' longitudinal bars reach 3.40 − cover (instead of 3.00 − cover) for the first test.
- **Problems.** A target 5 m away gives the "not over it" message and the own height 3.00.
- **Speed.** 400 columns attached to one slab. The first build is timed, and a rebuild after moving the slab reads the slab's triangles once (`RevisionCache` hit count). The limit is set from the first measurement: I could not measure it here.

**A3: steel columns**
- An HEA 200 column (any steel section from the catalogue), top attached to the sloped body. The body equals that of the same column with a manual `SteelEnd { cut: plane, elevAngle: 11.3099° }` at the same axis length, within 1e-6 m³.
- IFC: the same half-space as the manual cut.
- Regression: a steel beam `fit` to a column face (`SteelBody.Fit`) is unchanged.

**A4: stairs**
- A stair from storey 0 to storey 1 (+3.00), top attached to a floor on storey 1 with offset +0.05 (top at 3.05). The riser count and riser height equal those of the same stair with `topOffset` 0.05 by hand.
- Moving the floor to offset +0.10 changes the stair live.
- A target not under the top point: Problem, and the level top is kept.

**A5: Studio solids**
- A 1 × 1 × 4 extrusion at x ∈ [0, 1], top attached to the sloped body, mode both. Volume = ∫₀¹ (3 + 0.2x) dx = **3.100 m³**, Max.Z = 3.200.
- With mode extend on a 2.00 extrusion: the same 3.100 (it is extended, then cut to the underside).
- A component member is refused.

**A6: IFC**
- The A2 basic column exported and imported back. It is native, attached to the body's GlobalId, with the same volume.
- A foreign file with an `IfcRelConnectsElements` between a column and a slab imports with no attachment, and the fidelity gate passes on that column.

**A7: UI** (vitest and the guide scene)
- The tool accepts exactly the classes the engine names.
- The ghost's top outline matches `attachPreview`.
- Typing a height detaches with a notice.
- AR still attaches walls to a roof in one click (regression).

### Risks

1. **Cycles and the stack** (§3.6). This is the most serious risk. A missed path kills the engine. The rule plus the guard plus tests of both cycle cases are the answer. Review every new reader of `Columns.Built` for whether it can be read *by a target*.
2. **Readers of the drawn height.** There are 7 calls of `Columns.Spec`, 7 of `Beams.Top` and more of `Slabs.Top`. Switching the wrong one to `Built` brings back a cycle. Leaving one on `Spec` gives a wrong quantity or rebar length. A2 lists each caller and its choice in the history paragraph.
3. **The boolean path's robustness** on many columns (manifold with thin slivers under a nearly vertical face). Mitigation: the plane paths cover the usual cases, and the boolean path is measured on Bloc-P-4-sized models before A2 closes.
4. **Performance** with hundreds of attached elements under one large imported body (thousands of triangles). Today's cost is linear in the triangles near each element. If A2's measurement says so, add a plan grid of the triangles (bucketed per cell) in `BodySampler`.
5. **Two storages** (walls' attributes, others' relations) until D2 is decided. It is hidden behind `element.attachments`.
6. **Parallel sessions.** `WallTops.cs` and `Walls.cs` are busy files. Branch with `--no-track` (memory: branch-no-track), and check after merging that recent commits survive (memory: parallel push reverts).

### What I could not determine

- The sampler's real cost on large models (no profiling was run; §6, A2 "speed").
- Whether other tools read an `IfcRelConnectsElements` named "Attach top" (§3.9).
- The stair rule's exact riser count for a 3.05 m rise. That is why the A4 test compares against the hand-set `topOffset` instead of a number.
- Whether the terrain's built body is a closed solid or a surface (A10 avoids the question by reading the TIN).

---

## 7. Decisions for the user

1. **Scope of the first version.** Walls (unchanged), columns of every material (top and bottom), the tops of stairs and ramps, Studio solids. Slabs, ceilings, curtain walls and wall bottoms come later; doors, windows, MEP, railings, roofs, footings, references and component members never. *Recommended: yes.*
2. **Storage.** New classes use a relation (`IfcRelConnectsElements`, §3.4); walls keep `roofTop` / `roofIds`, with no upgrade. *Recommended: yes. Move walls to the relation only if a later need shows.*
3. **Default mode.** `both` (lengthen or cut, as walls do now), with `extend` and `trim` as options. *Recommended: both.*
4. **Typing a height on an attached end.** It detaches that end, with a notice (as walls' level top does now). The alternative is refusing the edit. *Recommended: detach.*
5. **IFC.** Besides `Lapin_Intent`, also write `IfcRelConnectsElements` "Attach top/bottom" for other tools. Foreign files' relations are never read as attachments. *Recommended: yes.*
6. **The tool.** One "Attach top/bottom" (AR) moved to Modify, with the old "Attach to roof" / "Detach from roof" kept as aliases. *Recommended: yes.*
7. **Beams.** Not attachable in v1. Their ends are trim/extend, column faces (`BeamSpan`) and steel `fit`. A flat "top to a soffit" could come later. *Recommended: leave beams out for now.*
8. **The dangling `roofIds` fix (A0).** Do it now, on its own, even if the rest waits. Today a wall whose target was deleted rises to any roof. *Recommended: yes.*
