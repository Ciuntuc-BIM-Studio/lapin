# Level marks relative or absolute (real altitudes): plan

Asked on 25.11.2026 in in-app report #190 (a French user: real altitudes such as 650.00 m, with the project's zero tied to an altitude, for connection sections in permit filings).

**Order: 3 of 6** (angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse).
**Status: done (S1–S6), branch `feat/level-datums`, 25.11.2026.** What is left is at the end.

## What exists today

- Every level text goes through `Standards.FormatLevel(z, book, plusAtZero)` (`Lapin.Drawing/Standards.cs`). It takes the model z as it is; the settings are `StyleBook.LevelDecimals` and `LevelZeros`. About 20 call sites:
  - section and elevation level marks (`Views.cs`, `plusAtZero`); base and survey point labels (`Location.cs`); `VoidDrawings.cs`;
  - symbol tokens `elevation` and `point.elevation` (`Symbols.cs`); documents and schedules (`Documents/DocNumbers.cs` `Level`);
  - the Level dimension kind (`DimensionKinds.cs`: value, `LevelText`, its own decimals and zeros);
  - Civil (`CivilModule.cs`): contour labels, pads, held/other labels, spot levels, hardscape top labels, ditch and pipe IL; `Analysis.cs` (elevation bands); `FloorSlopes.cs`.
- The absolute height already exists as `location.base.elevation`. The IFC export writes it as `IfcMapConversion.OrthogonalHeight` and as `IfcSite.RefElevation`; the import fills the base and survey elevations from it. `IfcBuilding.ElevationOfRefHeight` is written as Null: it is the IFC attribute made for this.
- Three competing datums: the base point's elevation; the survey point's elevation (subtracted in "shared" mode by `PointCloudLinks.cs`); each terrain's own `datum` (`Terrains.cs`).
- Contour labels show the model z, so surveyed altitudes print relative: the report's complaint.
- Precedent: the spot coordinate keeps a `system` (map/project/internal, `DimensionKinds.cs`); the Level kind mirrors it.

## Design

- **One resolver**, `Lapin.Platform/LevelDatums.cs`: `LevelDatum.Of(m, view)` gives the mode (`relative | absolute | both`), the zero (the model z of ±0.00: 0, or a reference storey's elevation) and the altitude of model z = 0 (= `base.elevation`, the single source). `LevelDatum.Text(ctx, z, plusAtZero, own?)` replaces the direct `FormatLevel` calls in drawings; `FormatLevel` stays the number formatter underneath. The engine works the text out once in the view's marks, so plans, sheets, PDF and DXF print the same.
- **Settings.** Project `levelDatum = {mode, zeroStorey?}` by a command `project.levelDatum` (one undo step). View `levelDatum: project | relative | absolute | both` overrides the project's. A single Level dimension or spot level can override the view with a `datum` attribute (as `system` does for coordinates). No sheet-viewport setting: a sheet command "set every viewport's views" covers it.
- **Tie UX** in Manage → Location: "±0.00 = [650.00]". It writes `base.elevation = value − zeroStorey.elevation`, so the model never moves. The absolute height is always `base.elevation`; the survey point's elevation is informational (point clouds aligned to it later).
- **Notation (decision 864).** A core `NotationDomain "levels"`: `relative` "{level}", `absolute` "{altitude}", `both` "{level} ({altitude})", a `tie` note "±0.00 = {altitude}", symbol keys for the zero sign, plus and minus. Indicative presets: FR "NGF", RO "MN75" (Marea Neagră 1975), DE "NHN". The defaults reproduce today's output exactly, including +0.00 on section marks. New symbol tokens `level.altitude`, `level.relative`, `level.tie`; `{= …}` formulas can mix them. Absolute text carries the same signs as relative ("+650.00"); presets drop the plus where local practice does.
- **Terrain and site.** In absolute views contours fall at round absolute heights (levels shifted by the altitude's fraction of the interval; otherwise 650.37 would label every line). Pads, IL and spot levels follow the view. A terrain whose `datum` differs from `base.elevation` gets a warning and a button "Use the terrain's datum as the project's altitude". A new survey import into a project whose altitude is 0 also sets `base.elevation`, in the same undo step, and says so.
- **Schedules and documents.** `DocNumbers.Level` takes the datum; level columns get a format of project, relative or absolute; title blocks get `{project.altitude}` and a tie token.

## IFC

- Write `IfcBuilding.ElevationOfRefHeight = base.elevation` (with OrthogonalHeight and RefElevation as now). Storey `Elevation` stays model z.
- On import, read ElevationOfRefHeight (and `IfcSite.RefElevation`) when there is no map conversion.
- Mode and zero storey go into the project's `Lapin_Intent` (`ProjectIntent`). Nothing is written into an existing IFC file.

## Drawings, quantities, upgrade

One resolver for drawings, sheets and exports. No quantities change. Nothing is stored until the user changes something (default = relative to model 0 = today's output): no `IProjectUpgrade`; each command is one undo step; no GlobalId changes.

## Tests first (`LevelDatumTests`)

- With the defaults every call site writes byte-for-byte what it writes today (a golden sweep: sections, plan spot levels, contours, documents).
- Then relative to a storey, absolute, both, imperial, a preset; the Level dimension's `datum`; contours at round absolute heights.
- An IFC round trip keeps altitude, mode and zero storey; `notation.describe` lists the `levels` domain.
- A test that fails on any new direct use of `FormatLevel(` in drawings.

## Risks

Missing one of the ~20 call sites; the three datums (pick one meaning and document it); contour geometry depending on the view; `plusAtZero` differing between marks and spot levels.

## Stages

| Stage | Content | Effort |
|---|---|---|
| S1 | Resolver, settings, notation domain, every call site rerouted | M |
| S2 | Dimension and spot `datum`, symbol tokens, tie note | S |
| S3 | Terrain contours, pads, IL, datum reconciliation | M |
| S4 | Schedules, documents, title blocks | S |
| S5 | IFC `ElevationOfRefHeight` and the intent round trip | S |
| S6 | UI (Location tie, view property, notation dialog), guide (levels.md, site.md, sheets/sections, every language folder), scenes (a section with "±0.00 (650.00)", an absolute site plan), strings in all languages, catalogue | M |

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. The absolute height is always `base.elevation` (IFC OrthogonalHeight); the survey point's elevation is only informational. **Yes.**
2. Project plus view is enough; a sheet command sets every viewport's views. **Yes.**
3. The absolute text carries the same signs as relative ("+650.00"); presets drop the plus where local practice does. **Yes.**
4. In absolute views contours are drawn at round absolute heights. **Yes.**
5. A survey import into a project whose altitude is 0 takes the terrain's datum automatically, and says so. **Yes.**

## What was built, and what is left (25.11.2026)

Built as designed: `LevelDatum` (`Lapin.Platform/LevelDatums.cs`) with `project.levelDatum`, `view.levelDatum` (a view, or every view on a sheet) and a level dimension's `datum`; the `levels` notation domain with the FR, RO and DE presets; every `FormatLevel` call site rerouted, with a test that fails on a new one; contours at round altitudes in absolute views (`TerrainContoursInView`); the survey import taking the terrain's datum; the datum warning with its button in Manage → Location → Levels; IFC `ElevationOfRefHeight` and the intent round trip; the UI (Location's Levels block, the view's Levels, the sheet's Levels in its views, the level dimension's Written as); strings in every language; the guide (levels.md, site.md, EN and RO) and the scene `level-datums`.

Left:
- Schedules have no level columns yet, so there is no column format; documents have `storey.altitude` and `storey.level`.
- Civil spot levels follow the view; they have no `datum` of their own (only the level dimension has).
- The elevation analysis legend follows the project's datum, not the view's.
- The guide's other language folders (da, de, es, fr, it, lo, pl, pt) lag behind the English and were not updated.
- Point clouds aligned to the survey point's elevation, as planned for later.

