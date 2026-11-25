# A visible "Structural" usage on elements: plan

Asked on 25.11.2026 in forum topic 256 (Victor1: how to tell structural from architectural walls, columns and beams; in Revit there are tabs for both and a Structural Usage tick in the properties).

**Order: 2 of 6** (angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse).
**Status: done, stages U1–U3 (25.11.2026, decision 866); U4 (one unified default rule) left.** The type dialogs got no tick of their own: the properties' "Type structural" row sets it on the elements' type (and a type selected shows the tick itself).

## What exists today

Four rules that disagree:

- `Platform/Coordination/Provisions.LoadBearingOrNull` (element, then type) reads the own attribute `loadBearing` or a `params` entry `Pset_*Common.LoadBearing`. `LoadBearing()` defaults to false.
- `Platform/ViewStyles.BearsLoad` (decisions 500/502; used by `VisibilityGraphics` for the structure discipline's `coreLoadBearing` and the layers mode): an unsaid wall bears load unless it or its type is PARTITIONING; slab, roof, column, beam, footing and pile bear load; coverings and room finishes never do.
- `Analysis/AnalyticalModel` with `Architecture/Analytical.cs`: an unsaid wall counts when masonry or concrete with a core of 10 cm or more (`Unsaid`), otherwise it is a partition load; slabs, roofs and `Lapin.Structure` members always go in, even when marked LoadBearing=false (a gap).
- `Ifc/IfcExport.Properties` always writes LoadBearing=true for Column, Beam and Footing; the user's own value wins in `CommonWith`. Walls and slabs get it only when the user typed the parameter.
- Also: `CdeChecks` requires LoadBearing on walls, slabs, columns and beams; the Wood `FrameSpec.loadBearing` is a separate flag; the JG export FAQ says "set Pset_WallCommon.LoadBearing".
- Nothing in the properties panel or the ribbon writes `loadBearing`: users type a Pset parameter.

## Design

- **Storage.** The existing attribute `loadBearing` (true / false / absent) on the element and on the type. `LoadBearingOrNull` already reads it first, so absent keeps today's behaviour exactly.
- **One resolver**, `Lapin.Platform/StructuralUsage.cs`: `Of(m, e) → (bool? Said, bool Effective, string Source)`. Source: own, ownPset, type, typePset, rule:partition, rule:class, rule:thickness. Precedence: own attribute, own Pset value, type attribute, type Pset value, class rule. Each consumer keeps its default as a named rule (`ForViews` = `BearsLoad`, `ForAnalysis` = the thickness rule), so nothing changes until the user ticks. `Provisions.LoadBearing*`, `BearsLoad`, `AnalyticalModel` and the export all call it.
- **Analysis gap fixed.** A slab, roof, column or beam explicitly false leaves the analytical model, with a note in the report (as walls already do).
- **Command** `element.setStructural {ids, value: true | false | null, onType?}`: one undo step, listed in `EngineDispatcher.AllowedRemote`. No GlobalId changes. No `IProjectUpgrade` (both stores are read); if attribute and Pset differ, the attribute wins and a check reports the conflict.

## IFC

- Export writes `Pset_<Class>Common.LoadBearing` from the resolved value when said (attribute or Pset); the existing column/beam/footing true stays.
- An export option "Write derived LoadBearing" (default off; on in the CDE/BIM-check preset) also writes the rule's value.
- Import: the file's Pset value stays as the file has it; the resolver reads it and no attribute is added (faithful import).
- Round trip: an own attribute is exported as the Pset value plus `Lapin_Intent` `loadBearing`, and comes back as the attribute.
- `IfcStructural*`: unchanged; `IfcExport.Structural.cs` already writes the analysis model from `AnalyticalModel.Build`, so from the resolver.

## UI

- Properties panel: a tri-state "Structural (load-bearing)" checkbox (ticked / clear / "by rule: yes|no" greyed with its source) for the bearing classes, on the instance and in the type dialogs (wall, slab, structure types).
- The Structure tab's wall and floor tools place elements with `loadBearing: true` (an option-bar tick, default on); the Architecture tab's tools leave it absent. A tick on the Modify tab sets it on the selection.
- The Wood frame dialog's `loadBearing` stays its own setting, defaulting from the wall's value.

## Views, quantities, analysis

- A field `structural` (the effective value) in `ViewFilters`/`ElementFilter`, so Visibility / Graphics colours or hides by it; a built-in preset "Structural usage" (bearing solid, others halftone); the Structure discipline makes non-structural elements halftone (`OutsideDiscipline` uses the resolver); `coreLoadBearing` behaves as before, now through the resolver. Schedules get a "Structural" column.
- Quantities and cost can group by it. The analysis notes point at the new tick instead of Pset typing; update the FAQ, `JgExportDialog`, `notes.test.ts` and `mode.test.ts`.

## Tests first (`StructuralUsageTests`)

- The precedence table, each source reported.
- Absent equals today for views (the `BearsLoad` cases of decisions 500/502) and for analysis (the existing analytical tests unchanged).
- A column, beam or slab explicitly false leaves the analytical model.
- The command over several ids is one undo step.
- IFC: `Pset_WallCommon.LoadBearing` written from the tick, the round trip keeps it, the import adds no attribute, the derived option writes it on every bearing class.
- The view filter `structural` and the discipline halftone.

## Risks

- Changing the defaults by mistake: keep the per-consumer rules separate and test that absent equals today.
- Double storage drifting apart: the resolver reports conflicts, the UI shows the source.
- A user's typed Pset false on a column, now honoured by the analysis, can drop members from existing models: note it in the analysis report.

## Stages

| Stage | Content | Effort |
|---|---|---|
| U1 | Resolver, command, properties tick, export, tests | M |
| U2 | Ribbon presets, view filter field, preset and discipline, schedules | M |
| U3 | Analysis honours an explicit false on every class; notes, FAQ, guide (structure.md, walls.md, EN and RO), scene `structural-usage` | S |
| U4 | Optional: one unified default rule | S |

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. Store the tick as the own attribute `loadBearing` (the IFC Pset value is written on export). **Yes.**
2. An unsaid wall: keep both rules for now, the UI shows which one decided; unify later on the analysis rule. **Yes.**
3. The Structure tab's wall and floor tools tick Structural by default; Architecture leaves it unset. **Yes.**
4. The IFC export writes the derived LoadBearing only as an option, default off, on in the CDE/BIM-check preset. **Yes.**
5. An explicit false leaves the analysis for columns, beams and slabs too, with a note in the report. **Yes.**
