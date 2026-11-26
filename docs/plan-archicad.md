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

Not measured yet:
- slabs, roofs, columns, beams, zones, stairs, railings, curtain walls, morphs, library objects in 3D, curved or slanted walls;
- hotlinked modules;
- a large project, for timing;
- whether IFC export (`IFCFileOperation`) works in demo mode.

Stage A1 starts by measuring these on a larger model (question Q7).

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
| **A1** | **Receive.** See below. | 8–10 |
| **A2** | **Updates.** See below. | 3–4 |
| **A3** | **To Atrium without Lapin open.** See below. | 2 |
| **A4** | **More native types.** See below. | 5–8 |
| **A5** | **Send back.** See below. | 6–8 |

**A1, receive**
- First, measure the types of §2 that are missing on a larger model, and keep the recorded answers as test fixtures.
- Then the client and instance discovery; storeys; walls (straight, composite and basic, with their types); slabs (polygon, holes, thickness, level); columns and beams (`GetDetailsOfElements` sections; profiles through `GetProfiles`); doors and windows as native openings in their host walls; zones as rooms (`IfcSpace`, `GetZoneBoundaries`, with their number and name); the rest as references.
- Also properties, classifications, the fidelity report and the guide page.
- UI: File → Archicad → Receive…, with the instance, storeys, origin, "Recognise elements", the report (native and references, and why), progress and Cancel.

**A2, updates**
- Get latest from Archicad: `CaptureElementState` after each receive. The state id is kept in the project's history, never in a commit.
- Then `GetElementChangesSince` for the next one. The update happens in place by GlobalId and deletes what is gone, in one undo step.
- When the state is gone (Archicad restarted, or another project open), the update falls back to a full read compared by a hash of each element's details.
- A status item shows "Archicad model changed" (polled only while the dialog or the status is visible).

**A3, to Atrium without Lapin open**
- A script (`scripts/archicad-to-atrium.mjs`): `IFCFileOperation` saves an IFC (with the user's translator) to a temporary file.
- The file is uploaded to an Atrium project as a reference document of type `model` (R3 API), or submitted with Submit files… semantics (R7).
- The token is a personal access token from the environment, never in the script. The script also documents running it from Archicad's own Python palette.
- With Lapin open, the route is Receive, then Save to server, which already works.

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
