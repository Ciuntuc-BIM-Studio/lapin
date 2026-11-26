# The Archicad connector, through Tapir: plan

Asked by the user on 26.11.2026 (in Romanian). The question was whether we could take Archicad models into Atrium through a Speckle connector, without depending on Speckle's platform. Speckle's Archicad connector runs on Windows only, and its Blender connector did not work on the user's Mac either. The user then asked whether Tapir, which is installed in their Archicad, could make a connector, possibly through a Speckle of our own. The answer, measured the same day (§2): yes, through Tapir and Archicad's JSON API directly, with no Speckle. Written at the user's request ("da, scrie planul"). Decision 882.

Open, MIT, in lapin-pro. No premium part is planned.

## 1. Why this way

- **Tapir** (`ENZYME-APD/tapir-archicad-automation`) is an Archicad add-on under the MIT licence.
  - It has Mac and Windows builds for Archicad 25–30, the latest being 2.0.1 (23.11.2026).
  - It adds about 600 commands to Archicad's own JSON API, which listens on `127.0.0.1:19723`; more instances take the next ports.
  - The user installs it as an add-on. Lapin neither ships nor links it: Lapin speaks JSON over HTTP to the Archicad that is running. The licence question is therefore the same as for any program reading a documented API.
- **Parametric, not meshes.** Speckle and IFC give Lapin bodies, from which it recognises walls and slabs (`MeshRecognition`). Tapir gives the elements' own parameters: a wall's ends, thickness, height and composite, an opening's host wall, offset, size and sill. Native elements are built from those, and the 3D geometry Archicad gives (§2) checks them. The checking uses the same `RecognitionFidelity` gate as IFC and Speckle, so the rule stays one rule.
- **Stable ids.** For every building element, Tapir's `ifcId` is the Archicad GUID compressed as an IFC GlobalId (§2). So an element received through Tapir has the GlobalId that Archicad's own IFC export gives it, and the two routes agree element by element.
- **Incremental.** `CaptureElementState` and `GetElementChangesSince` give the new, modified and deleted elements since a state, including what moved with an edit (§2).
- **No Speckle in between.** An Atrium that answers as a Speckle server stays a separate idea, worth it only for Speckle's Windows connectors (Revit, Tekla); it is not needed here.
- **The exchange rule holds.** It says exchange formats are written after their public specifications with no dependency. Here the specifications are Graphisoft's JSON interface documentation and Tapir's command schemas (`docs/archicad-addon/command_definitions.js` in its repository, MIT).

## 2. Measured (26.11.2026)

The setup was Archicad 29 on the user's Mac, in demo mode (reading works, saving does not), with Tapir updated from 1.5.8 to 2.0.1. The model was an untitled test model: 3 walls, a door, a window, 16 markers (story and elevation, 2D only) and 4 elevations. The client was a 20-line Python script; every command was read-only, except a captured state that was released afterwards.

| What | Result |
|---|---|
| Elements by type (`GetDetailsOfElements` with `fields: [elementId, type, floorIndex]`) | 25 elements in 0.04 s. |
| Full details | 0.06 s, 29 KB. **Wall:** `begCoordinate`, `endCoordinate`, `begThickness`/`endThickness`, `height`, `bottomOffset`, `topOffset`, `relativeTopStory`, `referenceLineLocation` (`CoreOutside`…), `flipped`, `geometryType` (`Straight`…), `structureType` (`Composite`) with `compositeId`, `profileType`, `slantAlpha`/`slantBeta`. **Door/window:** `ownerElementId` (the host wall), `centerOffset`, `width`, `height`, `sillHeight`, `reflected`, `oSide`, `libPart`. |
| 3D geometry (`Get3DGeometryOfElements`) | 0.11 s, 41 KB. Each body is vertices plus planar polygons with their holes and a `surfaceId`. Walls have 10–23 polygons with their openings already cut; the window has 74 and the door 116. Markers have no 3D, which is expected. |
| Composite (`GetComposites`) | Skins with type (`Finish`, `Core`), thickness (0.05 + 0.25 m) and building material, plus the separators' pens. |
| Classifications | Archicad Classification v2.0, one item per element. |
| Properties (`GetAllProperties`) | 1812 definitions in 0.19 s. |
| IFC ids (`GetIFCIdsOfElements`) | 0.03 s. For all 5 building elements, `ifcId` is the compressed GUID. Markers and elevations have none. |
| Changes (`CaptureElementState`, then `GetElementChangesSince`) | The user moved a wall and deleted the door. 0.47 s: **modified** were the moved wall, the wall joined to it and the window in the moved wall; **deleted** was the door; nothing new. |

**Measured again (26.11.2026, A1).** The same Archicad, the model grown by the user: 64 elements (3 walls, a window, 3 columns, a beam on the story above, a slab, a multi-plane roof, a stair, 4 elevations, and 49 subelements: the stair's 20 risers, 20 treads and 5 structures, 3 column segments, a beam segment); 3 stories with their `ifcId`s. The answers of one read are the tests' fixtures (`engine/tests/Lapin.Engine.Tests/Archicad/fixtures/demo/`, 21 commands, 0.8 MB, recorded by `ArchicadLiveTests` through `RecordingTransport`).

| What | Result |
|---|---|
| A full read, every command once (Python, 22 commands) | 0.95 s in all: details 0.07 s (79 KB), 3D geometry 0.24–0.27 s (310 KB), IFC properties 0.07 s, the rest 0.02–0.05 s each. Lapin's own read (`archicad.survey`, every element's custom properties included) 3.3 s the first time, 1 s after. |
| **Column** | `origin`, `coreAnchor` (3×3 grid), `axisRotationAngle`, `width`/`depth` or `circleBased`, `height`, `bottomOffset`, `buildingMaterialId`, `veneer`, `segments` (each `structureType`, `taper`). 6 polygons a box. Its 3D body is given twice: by the column and by its segment. |
| **Beam** | `begCoordinate`/`endCoordinate`, `zCoordinate` (absolute: the story's level plus its own), `anchorPoint` (3×3), `offset`, `width`/`height`, `profileAngle`, `isSlanted`, `segments`. Its body runs to its ends through the columns (not stopped at their faces). |
| **Slab** | `polygonOutline` (first point again at the end), `holes`, `thickness`, `level`, `offsetFromTop` (the top is the story + `level` + `offsetFromTop` whatever `referencePlaneLocation`), composite skins top down. |
| **Wall** | `offsetFromOutside`: the reference line's distance from the outside face, which Archicad works out; `floorPlanPolygons`: each skin's plan polygon, cut at the junctions. The outside face is on the left of the reference line unflipped, on the right flipped; a composite's skins are listed from the outside face. |
| **Window** | `centerOffset` is along the reference line from the wall's start; `sillHeight` from the `verticalLink` anchor (`SillToStory`: the story); the hole in the wall's body is `width` × `height` exactly. |
| **Roof** | `MultiPlane`, `pivotPolygonOutline`, `levels`; `GetRoofPlanes` gives each plane's details (a reference in A1). |
| **Stair** | 45 bodies, 715 polygons with its parts; a reference in A1. |
| **Subelements** | `Get3DGeometryOfElements` gives a hierarchical element's 3D with its parts (a stair, a column, a beam): the owner is received, the parts are not. |
| Custom properties | 168 `Custom` properties in 26 groups (the template's); values `<Undefined>` until set. |
| Classification items | `API.GetAllClassificationsInSystem` gives the tree (code as `id`); Tapir gives each element's item by guid. |
| Surfaces | `GetSurfaces`: name, colour (0–1), transparency (%). |
| **IFC export in demo mode** | **Works**: `IFCFileOperation` save with the preview translator wrote 273 KB in 0.4 s, and all 11 GlobalIds Lapin received are in it. 16 translators. |
| Hotlinks, geolocation | No hotlinks. Survey point at the project origin; a default geolocation (Budapest) that is not the model's. |

Still not measured: zones, doors, curtain walls, railings, morphs, objects, curved or slanted walls, hotlinked modules, a large project (the user's model had none of them). The tests cover zones, reference lines and changes with synthetic elements beside the recorded ones.

## 3. Decisions

- **An open module `archicad`** (`engine/src/Modules/Exchange/Lapin.Archicad`, `ui/src/modules/archicad`). It is `desktopOnly`, like `speckle`, because the browser cannot reach a local Archicad. It requires `core`; its native elements come from the modules that own them (architecture, structure), offered through the same `IIfcRecognizer` services as IFC, so a product without structure keeps columns as references.
- **The client** (`ArchicadClient`): `HttpClient` to `127.0.0.1`, with no dependency.
  - It finds the instance by probing ports 19723–19744 with `API.IsAlive` and `GetProjectInfo`. With several instances, it offers them by project name.
  - Batches are sized by the command's input; timeouts are generous for `Get3DGeometryOfElements`.
  - The minimum Tapir version is checked (`GetAddOnVersion`). Below 2.0, the dialog says what to update and where.
- **Receiving is one command, `archicad.receive`**, one undo step, with options in the shape of `speckle.receive`:
  - `{port?, storeys: "create"|"existing", recognize: true, origin?, asCopy?}`;
  - queries `archicad.instances`, `archicad.survey` (what a receive would do, with counts by type) and `archicad.status` (changed since the last receive?).
- **Fidelity.** Native elements are built from the details and measured against Archicad's own body for that element (its 3D geometry, openings cut). The gate is `RecognitionFidelity`, enforce mode: what differs by more than 1 mm or 0.5 % comes in as the reference that body would have been. An import reproduces the source and nothing of Lapin's own is added (no lintels, reveals or automatic joins on received elements: CLAUDE.md, decision on faithful imports).
- **Ids.** An element's GlobalId is its `ifcId` (or `externalIFCId` when the element came into Archicad from an IFC file). The Archicad GUID is kept as an attribute (`archicad.guid`) for updates.
- **Storeys** come from `GetStories`, matched like Speckle's levels (`SpeckleLevels.Resolve`): the GlobalId sent by Lapin, a storey made by an earlier receive, the level (5 cm, the same name first), the name.
- **Types and materials:**
  - A composite becomes a wall or slab type with its `IfcMaterialLayerSet` (the skins' order, thicknesses and building materials as Lapin materials, matched by name, else made). Types are matched across receives by the composite's GUID.
  - The reference line (`referenceLineLocation`, `offset`, `flipped`) maps onto Lapin's axis-on-the-core layer rules (`Lapin.Model` layer rules). Each Archicad location has a test with its expected axis.
- **Properties.**
  - Archicad's property groups become Lapin property sets named as in Archicad.
  - Lapin's own set is `Archicad` (never `Pset_`).
  - The IFC property sets Archicad maps (`GetIFCPropertiesOfElements`) keep their IFC names, standard sets included, because they are the file's, not Lapin's.
  - Classifications become `IfcClassificationReference`s in their systems.
- **The rest stays a reference.** Anything not offered natively, or that fails the gate, comes in as a stored body (`StoredBodies`), with its surfaces as materials:
  - morphs, library objects, stairs and railings until A4;
  - curtain walls;
  - anything with an unknown type.
- **Coordinates.** Metres as Archicad gives them. The project origin is Archicad's by default, or the survey point (`GetSurveyPointTransformation`) on request. Georeferencing comes from `GetGeoLocation` when Lapin's project has none.
- **2D** (dimensions, texts, fills, lines, the markers) is not received in A1.
- **Writing back to Archicad is a separate, explicit action (A5)**, never part of a receive. It changes the user's Archicad model, so it shows what it will do first.

## 4. Stages

| Stage | What | Days |
|---|---|---|
| **A0** | Measurement (§2). | done |
| **A1** | **Receive.** See below. | done (26.11.2026) |
| **A1b** | **Project information.** See below. | done (26.11.2026) |
| **A2** | **Updates.** See below. | done (26.11.2026) |
| **A3** | **To Atrium without Lapin open.** See below. | done (26.11.2026) |
| **A4** | **More native types.** See below. | 5–8 |
| **A5** | **Send back.** See below. | 6–8 |

**A1, receive**
- First, measure the types of §2 that are missing on a larger model, and keep the recorded answers as test fixtures.
- Then the client and instance discovery; storeys; walls (straight, composite and basic, with their types); slabs (polygon, holes, thickness, level); columns and beams (`GetDetailsOfElements` sections; profiles through `GetProfiles`); doors and windows as native openings in their host walls; zones as rooms (`IfcSpace`, `GetZoneBoundaries`, with their number and name); the rest as references.
- Also properties, classifications, the fidelity report and the guide page.
- UI: File → Archicad → Receive…, with the instance, storeys, origin, "Recognise elements", the report (native and references, and why), progress and Cancel.

**A1b, project information**
- Archicad's Project Info (`GetProjectInfoFields`) into Lapin's project information, and Lapin's project information synced with Atrium Server's project information record (Atrium `docs/atrium-server.md`, "Project information (A1b)").
- **Done (26.11.2026).** One mapping table for both sides: `engine/src/Platform/Lapin.Platform/Documents/project-info-mapping.json` is Atrium Server's `docs/project-info-mapping.json` byte for byte (a test compares them when the server's repository is beside this one), read by `ProjectInfoMapping` (first id with a value, parts joined, phase codes routed to `phase` or `phaseName`, the rest kept as extras).
  - **Receive** (`archicad.receive`; `archicad.survey` says it beforehand): the project's empty fields are filled in the receive's undo step; a field set with another value is never overwritten and is listed in the report (`projectInfo: {filled, differs: [{key, lapin, archicad}], same, kept}`); the Receive dialog takes the ticked ones with `project.setInfo` (one undo step: no command of its own was needed). What no field takes is kept read-only as `info.source.archicad` (an attribute, not the user's parameters: it is not editable, not exported, not synced; Project information shows it as "From Archicad"), replaced at each receive. Archicad busy with a dialog: the model is received, a note says the Project Info was not read.
  - **Sync** (`remote.syncProjectInfo`, `ProjectInfoSync`), with the issues and the documents (Save to server, Get latest, Sync issues and documents), skipped when the server lacks `project.info`: field by field against the values last seen (state in `history/remotes.json`, member `projectInfo`), only the changed members in a merge patch with If-Match, a 412 read again and sent again; the parameters list ↔ map by key, Lapin's order kept; `source` never sent. A first sync keeps the server's values and sends what the server lacks. Once synced apart, a pull keeps the project's name and information, and they are compared as last committed for "uncommitted" (so a sync is not a change to commit).
  - **Proposals**: Send to Atrium posts `POST …/info/proposals {source: "archicad", raw, sourceRef: {file, referenceId}}` after the upload (a tick, on by default); the A3 script calls `cde_project_info_propose` (`--no-project-info` skips it).
  - **CDE web and desktop**: Publishing → Project information (when the server offers `project.info`): the record (edited by editors and owners with If-Match), the open proposals with their changes (owners tick and apply; owners and their author dismiss), and "From the models".
  - Not done: the georeference from `GetGeoLocation` (Archicad gives an untouched project a default location, Budapest, that cannot be told from a real one: left for A2 with the survey point's rotation); the IFC import does not fill the project information from a foreign file's IfcProject, site and actors (it reads Lapin's own `Lapin_ProjectInformation`; the server proposes from uploaded IFC models with the same table); the project's own parameters are read-only in the CDE tab.

**A2, updates**
- Get latest from Archicad: `CaptureElementState` after each receive. The state id is kept in the project's history, never in a commit.
- Then `GetElementChangesSince` for the next one. The update happens in place by GlobalId and deletes what is gone, in one undo step.
- When the state is gone (Archicad restarted, or another project open), the update falls back to a full read compared by a hash of each element's details.
- A status item shows "Archicad model changed" (polled only while the dialog or the status is visible).
- **Done (26.11.2026, branch `feat/archicad-a2`).**
  - **The link.** `archicad.receive` captures Archicad's state (`CaptureElementState`, modification stamps) *before* its read, so what changes while it reads shows next time, and keeps it as the model's link: `{stateId, port, project, untitled, stories (their IFC ids), at, stamp, options}` under the model's key. It lives in the copy state (`Lapin.Platform.ICopyState`, new: a session keeps it in memory, a host's dispatcher in the history's `remotes.json`, member `connectors`, through `ProjectFiles.CopyStateOf`), never in the model, a commit or an undo step. The stamp hashes what the project holds of the model (each entity's id, guid and hash). The old state is released in Archicad when a new one replaces it.
  - **Get latest** (`archicad.update`, one undo step; File → Collaborate → Get latest from Archicad): `GetElementChangesSince` from the link's state; only the new and modified elements are read (`ArchicadRead.Read` with an `ArchicadReadScope`: a wall's doors and windows, from the `host` each now keeps in its `archicad` attribute, and a door's or window's wall, from its `ownerElementId`, are read with it until nothing is missing; plus every element of a composite that changed); the deleted ones the project holds are removed; `ArchicadReceive.Apply` takes the partial read (`ArchicadModel.Scope`, `ArchicadScope{Deleted, Force}`) and leaves every element outside it as it is, the rest going the receive's way (in place by GlobalId, recognition, the gate). Then a new state is captured and the old released. Nothing changed: nothing is read, the state stays (a renamed building material still renames its material).
  - **Fallbacks** to the whole read compared by hash (A1's): no link (a model received before A2), the project's stamp differs (an undo, elements deleted here), or `GetElementChangesSince` fails (Archicad restarted, the project closed: any refusal but a busy one). A whole read sharing no element with what the project received is another Archicad project: refused, with the way out (open that project, or receive as a copy). A model received untitled and saved under a name since is found by its stories' IFC ids. Archicad busy with a dialog: measured, even `API.IsAlive` answers 4001 then; the instances list, the receive, the update and the status say "Archicad is busy: a dialog is open in it (…)" instead of "Tapir is missing".
  - **Status** (`archicad.status`, cheap; `{full: true}` keeps A1's whole compare): no link → `{linked: false}` without asking Archicad; else `GetElementChangesSince` counted (deleted and modified the project holds; new or unknown ones told apart by their type alone, `GetDetailsOfElements` with `fields: [elementId, type]`, 2D, views and parts never counted), `local` when the project's stamp differs, `lost`, `busy`, `reachable`; never thrown. The status bar item (`ArchicadStatus.tsx`, a new `statusItems` hook of the UI module contract, lazy-loaded) asks once when the project opens and after each receive or update, then every 10 s only while linked and the window is visible, never while a receive or an update runs; "Archicad model changed (N)", a click runs Get latest.
  - **Types by composite GUID** (`ArchicadTypes`): a wall or slab type the recognisers made from a composite (or an A1 type named as the composite whose occurrences all came from Archicad) is tagged `archicadAttribute: {guid, hash, name}`; at the next receive or update a changed composite (its JSON's hash) rewrites the type's layers in place (`LayerSets.Rewrite`, its name the composite's) and its elements (and the doors and windows in its walls) come in again whatever their hash, so they are rebuilt on it and gated; untagged, the recognisers' match by name and layers holds. Building materials: the material named as one is tagged with its GUID, and renamed when Archicad renames it (unless another material has the name).
  - **Georeferencing** (`ArchicadGeo`, from `GetGeoLocation`), into a project with none (no survey point, no base latitude/longitude), option `georeference` (on; a tick in the Receive dialog, which says beforehand what it would do): a survey point set (map coordinates or a CRS named) ties the plan as an IFC map conversion does (the survey point at the plan's origin, `crs` when Lapin knows it, the X axis's turn as the base point's angle to true north, the elevation of ±0.00); else a place that is not Archicad's untouched default sets the base point's latitude and longitude; the default (longitude 19.05477, latitude 47.5597, survey point at the origin, no CRS, no map coordinates) is never taken. A turned survey point is supported as a georeference (Lapin's location has the angle to true north); receiving *from* a turned survey point (moving the geometry) is still refused, with that way out.
  - **Tests**: `ArchicadUpdateTests` (11, on `FakeArchicad`, the demo fixtures answered element by element with Tapir's states simulated: an incremental update of a moved wall with its window and the wall joined to it touched, a deleted column, a new wall, untouched elements the same entities, only the changed ones' 3D read, one undo step and the full read after it; nothing changed; a lost state; another project refused; nothing received and a busy Archicad; a changed composite; a renamed building material; the survey point turned 30° in Stereo 70; the default location skipped and a place taken; the link in `remotes.json` round-tripped), the live test `A_running_Archicad_is_received_then_updated_with_nothing_changed` (gated on `LAPIN_ARCHICAD_PORT`), UI tests for the status text and the summary.
  - **Not done / left**: a live run of the incremental path (Archicad had its Project Info dialog open during the work, so every call answered 4001; the logic rests on the fixtures and the measurement of §2); Archicad's project north (`projectLocation.north`) is not taken without a survey point (its sign convention is not measured); surfaces are still matched by name; a change of a composite is found only for composites whose type is tagged.

**A3, to Atrium without Lapin open**
- A script (`scripts/archicad-to-atrium.mjs`): `IFCFileOperation` saves an IFC (with the user's translator) to a temporary file.
- The file is uploaded to an Atrium project as a reference document of type `model` (R3 API), or submitted with Submit files… semantics (R7).
- The token is a personal access token from the environment, never in the script. The script also documents running it from Archicad's own Python palette.
- With Lapin open, the route is Receive, then Save to server, which already works.
- **Done (26.11.2026).** `scripts/archicad-to-atrium.mjs` (Node 22, no dependency; tests in `scripts/archicad-to-atrium.test.mjs`; guide `docs/user/archicad-to-atrium.md`). It signs in with a personal access token from `ATRIUM_TOKEN` and calls the server's MCP tool `cde_reference_add` (a file over 12 MiB goes through the upload session the tool opens). `--ifczip` uses Archicad's own `ifczip` save, so no zip writer. Measured: in demo mode Tapir's default IFC save works (a 3-wall model, 270 KB, 55 KB as ifczip); a named translator is refused with "Failed to save the project as IFC". Run end to end against a local Atrium Server; the Python palette route is documented but untried.

**A4, more native types**
- Roofs (`GetRoofPlanes`, Lapin's roofs) and slanted or curved walls.
- Stairs (Lapin's configurator when it reproduces the body, else a reference, as with doors).
- Railings, and curtain walls as Lapin's when they exist.
- The 2D of each storey (drafting) if the user wants it (Q3).

**A5, send back**
- Lapin elements that came from Archicad and were edited become `ModifyWalls` / `ModifySlabs` / `SetDetailsOfElements`; new Lapin walls, slabs and openings become `CreateWalls` / `CreateSlabs` / `PlaceOpenings`.
- The user sees a preview of what changes in Archicad, and a conflict is reported when Archicad changed the same element since the last receive (A2's state).
- It is verified by receiving again with no difference.
- It is explicit and never automatic.

**Tests.** Unit tests replay recorded Tapir answers (`engine/tests/.../Archicad/fixtures/*.json`), so CI needs no Archicad. An integration test runs only when `LAPIN_ARCHICAD_PORT` is set. The guide has no scene, because a scene would need Archicad; its pictures are taken by hand, as for the CDE pages.

## 5. Open questions (with what is done meanwhile)

- **Q1** Should the module be open (MIT) and in the default `lapin` edition? *Meanwhile: yes.*
- **Q2** Recognition on by default? Speckle's is off by default, but Archicad gives parameters, not meshes. *Meanwhile: on, with the gate enforced.*
- **Q3** Should Archicad's 2D (dimensions, texts, fills, lines) come in, per storey, as drafting? *Meanwhile: no, until A4.*
- **Q4** Should Archicad's layers become Lapin visibility categories, or stay an attribute? *Meanwhile: an attribute (`archicad.layer`), visible in Properties.*
- **Q5** Which elements does A5 send back first? *Meanwhile: walls, slabs and openings.*
- **Q6** Should hotlinked modules come in as Lapin groups or links (docs/groups.md, docs/links.md), or flattened? *Meanwhile: flattened, with the module's name as an attribute.*
- **Q7** A larger test model (slabs, roof, columns, beams, zones, stairs), on which to measure A1's missing types and time a full read. *Meanwhile: A1 starts with the types measured.*
