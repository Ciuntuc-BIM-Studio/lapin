# Visibility / Graphics as in Revit

Asked by a user and approved by the user (the developer of Lapin) on 07.11.2026: "Please expand the support for Visibility/Graphics like in Revit: with tabs for Annotative Objects, File Links, Imports, even Point Cloud (if possible). We need to be able to turn off Categories from links, like architects don't want to see rebars, engineers don't want to see wall and floor finishings, etc. The V/G is the most powerful tool in any BIM software."

Decisions 391– in `docs/decisions-pending.md`, one per stage. Stage A done (decision 391), stage B done (decision 393), stage C done (decision 394); stage D (point clouds) approved by the user on 08.11.2026 in the open formats only, D1 done (decision 395), D2 done (decision 396).

## What there is today

- **The dialog** (View → Visibility VV, Graphics VG; `ui/src/modules/core/VisibilityDialog.tsx`) works on the view in front, 2D or 3D, with two tabs: *Model categories* (shown, line colour, line weight on the ISO series, fill, cut hatch) and *IFC classes* (shown). A view keeps its own graphics (`graphics` on the view, the 3D's in the project's `view3d`) or takes a named **graphics set** of the project's (`graphicsSets`), shared by every view that takes it (`GraphicsSets.cs`: `view.graphics`, `view.saveGraphicsSet`, `view.useGraphicsSet`, `view.removeGraphicsSet`, the queries `view.visibility` and `view.hidden3d`). Per element the same bucket holds line overrides (`elements`).
- **Categories** are the drawing's (`LineStyles.Categories`: walls, slabs, roofs, columns, beams, doors, windows, stairs, railings, spaces, furniture, topography, other, linework, voids, entourage), reached from an IFC class by `LineStyles.CategoryOf`. Object styles (Manage → Object styles) give each category its pens per role (cut, separation, projection, hidden, overhead, beyond); a view's category row overrides them. Rebar, formwork, scaffolds, room finishes, MEP and steel connections have no category: they can only be hidden by IFC class.
- **View styles** (`docs/view-styles.md`, decisions 137–139, 162) are Lapin's view templates: the layers drawn (all, without finishes, core only), joins, overhead, IFC 2D, voids, hatches, entourage. A view takes its style and overrides fields. The graphics sets are not tied to styles yet (view-styles.md, Rămase).
- **Layers in 3D**: every part of a layered element carries `hiddenBy` (`ViewStyles.HiddenBy`), the interface hides the parts by the view's layers mode (`viewport/layers3d.ts`). The 3D's category and class visibility comes from `view.hidden3d` as looks (`platform/looks.ts`: hidden, colour, opacity, and looks per part key).
- **Links** (`docs/links.md`, decision 132): a linked IFC or DXF is imported into the model and every entity carries `linkId`; Manage → Links shows/hides, halftones, toggles 3D (DXF), reloads, converts, opens separately, unlinks. These are project-wide: no view can hide a link alone, nor a category inside a link. A view's categories apply to linked elements as to the host's (Revit's "By host view").
- **Imported IFC** comes as groups (decision 204) of native elements or references (`StoredBodies`, shared bodies #166), with their IFC classes. **Fragments** and meshes the same.
- **DXF imports** (`docs/dxf-import.md`): native drafting on a view, on the project's drafting layers (`draftLayers`: shown, locked, colour, weight, line type), each import recorded in `dxfImports` and each shape carrying `dxfImport`; a view hides layers of its own (`hiddenLayers`); 3D underlays by import (`in3d`). PDFs and images traced over a view are `LapinViewImage` (decision 371).
- **Annotations** are marks the modules add to a view (`IViewAnnotations`, `ViewMark` with its owner): dimensions, texts, tags, grids, levels, section and elevation marks, room stamps, legends, reference planes, view boundaries. None can be hidden per view by kind; only the drafting layers and the elements they belong to.
- **Phases** (`IViewElementFilter`) and the 4D state at a date (`IViewPartFilter`) already hide or tint elements and parts per view, in the engine's drawing.
- Everything a 2D view shows is drawn by the engine (`Views.Gather`, `Views.Render`), so sheets, PDF and DXF follow; the 3D view follows `view.hidden3d`; the mesh exports follow the 3D view's graphics (`MeshScene`).

## The design

One resolver in the platform, `VisibilityGraphics` (`Lapin.Platform/VisibilityGraphics.cs`), built once per view and drawing, answers every question the drawing, the marks, the 3D and the exports ask: is this element hidden, is this part hidden, is this mark hidden, how is it drawn (colour, halftone, transparency, pens, patterns). Everything stays in the view's (or its set's) `graphics`, which only grows new keys, so an old project opens unchanged and needs no upgrade:

```json
{
  "categories": { "walls": { "hidden": false, "lineColor": "#…", "lineWeight": "0.5", "fillColor": "#…", "cutHatch": "…",
                             "halftone": true, "transparency": 50, "lines": { "cut": { "pen": 7, "pattern": "dashed" } },
                             "sub": { "finishes": { "hidden": true }, "insulation": { "halftone": true } } } },
  "classes": { "IfcWall": { "hidden": true } },
  "elements": { "<id>": { "cut": { "pen": 5 } } },
  "annotations": { "dimensions": { "hidden": true }, "tags": { "color": "#…", "halftone": true } },
  "imports": { "<dxfImport id>": { "hidden": false, "halftone": true, "layers": { "A-WALL": { "hidden": true } } },
               "<image id>": { "hidden": true } },
  "filters": [ { "id": "<filter id>", "hidden": false, "color": "#ff0000", "halftone": false, "transparency": 0, "lines": { … } } ],
  "links": { "<link id>": { "display": "host" | "custom", "hidden": false, "halftone": false,
                            "categories": { … }, "classes": { … }, "filters": [ … ] } },
  "detailLevel": "coarse" | "medium" | "fine"
}
```

- **The dialog** keeps its head (the view's own graphics or a set, Save as a set, hidden lines) and gets Revit's tabs: *Model categories*, *Annotation categories*, *Imported categories*, *Filters*, *Links*, and *IFC classes* (Lapin's own: what IFC calls the thing). Each tab is a table; the model categories unfold into their subcategories.
- **Templates**: a view style names a graphics set (`graphicsSet` on the style). A view without graphics of its own and without a set takes its style's set; so a Structural style can carry "no finishes, no furniture, rebar shown" and every structural view follows. The set stays editable in the dialog as now.

### Model categories and subcategories

The drawing's categories, plus those the modules lacked: **rebar** (`LapinRebar`, `LapinRebarRun`, `IfcReinforcingBar`, `IfcReinforcingMesh`), **coverings** (room finishes, skirtings, ceilings, `IfcCovering`), **formwork**, **scaffolds**, **mep** (pipes, ducts, cables, fittings, terminals, equipment, the exhaust fans), **steel connections** (`LapinSteelNode`, `IfcMechanicalFastener`), **transport** (elevators) and **shading** (sunshades). `IfcCovering` leaves *other* for *coverings*; a project that styled *other* keeps that style on its other elements.

Subcategories split an element by its parts, the way the view styles' layers already do:

| Subcategory | Parts |
|---|---|
| core | the layers marked core |
| finishes | layers in a finish material (`Materials.IsFinish`) |
| insulation | layers in an Insulation material |
| membranes | layers in a Membrane material |
| other layers | the remaining layers |
| reveals | door and window reveals (`reveal:*`) |

They exist for every category whose elements can have layers (walls, slabs, roofs, coverings, beams, columns, footings in *other*). Columns: Shown, Halftone (stage C), Projection/cut lines (colour, weight, pattern; stage C), Fill, Cut hatch, Transparency (3D, stage C), and *Detail level* for the whole view (stage C: coarse, medium, fine; by the scale when unset, as `PlanDetail` does now).

### Annotation categories

From a mark's owner: **dimensions** (IfcAnnotation DIMENSION), **texts** (TEXT), **tags** (TAG: door and window tags, build-up tags), **room tags** (a room's stamp; its fill stays with the *spaces* category), **grids**, **levels** (level heads in sections and elevations), **section marks**, **elevation marks**, **callouts** (insets), **reference planes**, **view boundaries**, **legends** (colour scheme legends). Shown in stage B; colour and halftone in stage C. Drafted lines stay in the model category *linework* (shown or hidden in 2D now too, as in 3D) and on the drafting layers.

### Imported categories

Per DXF import (and per linked DXF), by its file's name: shown, halftone, and each of its layers shown; per PDF or image traced over the view (`LapinViewImage`): shown, halftone. The project's drafting layers still turn a layer off everywhere (Draft → Layers); the view's `hiddenLayers` stay as they are.

### Filters

Rule-based, the project's (`viewFilters`), each a name, *all* or *any* of its rules, and rules `{field, op, value}`:

- fields: *IFC class*, *category*, *type* (the type's name), *name*, *material* (any of the element's materials), *storey*, *group*, *link* (its name; *none* for the host's), *phase created*, *predefined type*, a **parameter** (the user's or a property set's, `params`), or any stored attribute by name;
- operators: equals, does not equal, contains, does not contain, begins with, greater, less, has a value, has no value; numbers compared as numbers.

A view lists the filters it uses in order (`graphics.filters`); for each the same columns as a category (shown, colour, halftone, transparency, lines). An element a filter hides is hidden; where several filters match, the first in the list gives each field (Revit's rule). Manage → View filters edits them, the Filters tab adds, removes and orders them for the view.

### Links

The Links tab lists every link (IFC, Fragments imported as links, DXF) with:

- **Display**: *By host view* (the default: the view's categories, filters and classes reach the link's elements, as today) or *Custom*: the link has its own categories (with subcategories), classes and filters for this view, and the host's do not reach it. "Architect hides rebar in the structural link": the plan's Links tab → the structural IFC → Custom → Model categories → rebar off. "Engineer hides finishes": the architectural link → Custom → walls ▸ finishes off, coverings off.
- **Shown** and **Halftone** for this view alone, over the link's own (Manage → Links still hides a link everywhere).
- *By linked view* does not apply: a linked IFC or DXF has no views of its own. A linked Lapin project would bring them, and Lapin does not link projects (later, if ever).
- *Underlay* (a link drawn under the host's elements and not picked) and *display as references* are later.

### Where it is drawn

`Views.Gather` asks the resolver for each element and part; `Views.Render` drops the marks it hides and gives the overrides to the pens (`ElementColors`, the per-element line specs), so sheets, the PDF and DXF print what the view shows. The 3D asks `view.looks3d` for the view's looks (hidden, colour, opacity, and per part), which replace `view.hidden3d` in the interface; the mesh exports ask the same resolver.

### Analytical

Lapin has no analytical model of its own (the JG export writes one from the physical elements), so there is no Analytical tab. If an analytical model comes, it gets its categories in the same resolver.

## Stages

- **A. The dialog with tabs, model categories with subcategories, links per view.** The resolver; the new categories; subcategories by parts in plans, sections, elevations, sheets and 3D (looks per part) and the mesh exports; the Links tab (display by host / custom with the link's own categories, subcategories and classes, shown and halftone per view); a view style's graphics set. `view.graphics` takes `sub`, `link`; `view.visibility` returns subcategories and links; `view.looks3d`.
- **B. Annotation categories, imported categories, filters.** The mark filter by owner; DXF imports by file and layer, traced PDFs and images; the project's filters with their rules, Manage → View filters, the Filters tab; visibility only.
- **C. Graphic overrides.** Halftone, colour, transparency, cut and projection lines (pen, pattern) for categories, subcategories, filters, links and annotation categories, in drawings and in 3D; the view's detail level.
- **D. Point clouds** (below).

After stage D, from filming the requester's own case (approved by the user on 08.11.2026): 

- **A link's finishes** (decision 400): a linked or imported IFC's walls, slabs and roofs stay references, but their layers come from the material the file associated with them (`ReferenceLayers`: the IfcMaterialLayerSetUsage or IfcMaterialLayerSet carried in `IfcSource`, each layer's subcategory by its IFC Category, else by its material's name in English, Romanian or German). The body is split by the layers' thicknesses along the layer direction (the import keeps `layerAxis`: the Axis curve's normal for a wall, the placement's Z for a slab), fitted to the body's extent, with manifold, into parts keyed `layer:<n>` as a native element's, when the session builds it (`StoredBodies.ViewGeometry`; nothing stored, the export and recognition read the file's body whole). So Finishes, Insulation and Core hide as a native wall's in plans, sections, sheets and 3D, and the view styles' layers modes reach them. A reference of one finish material (a wall's plaster brought as an `IfcBuildingElementPart`, its wall's category kept in `partOf`; a plaster covering) goes with Finishes. A reference linked before keeps splitting where the direction can be told from its body (a slab; a wall only with a symmetric build-up) and gets the rest when reloaded. Not split: a body whose extent the layers do not fill (a wall with a footing in its body, a curved wall), a mesh part over 100 000 triangles, profile sets.
- **Rebar in 3D** (decision 402): the Rebar category is hidden by default in an architectural 3D view (`GraphicsSets.HiddenByDefault3d`, by the view style's discipline) and shown in a structural one; a linked structural IFC's IfcReinforcingBar (swept disk solids, shared by content: 11 000 bars of a test file as 3 bodies and 11 250 instances, linked in 1.5 s) follow it, and plans and sections draw them at a fine detail level only. The open rebar module's bars in space (`rebar.bodies {view}`) show under the same category (View → Bars in 3D switches it). Since decision 403 every 3D view hides the Rebar category until shown, whatever its discipline (`GraphicsSets.HiddenIn3d`).
- **The detail level** (decision 401): coarse draws a layered element as one area hatched as its core (its layers take the core's material and `MaterialDrawing.Apply(mergeOwn)` takes the lines between them out, no membranes); medium its layers; fine adds the doors' and windows' details (`PlanDetail.IsDetailPart`: reveals, sills, surrounds, thresholds, boards, hardware, shutters, grilles, boxes, canopies), the openings' linings and a link's bars. By the scale when unset (`PlanDetail.OfBuilding`: fine to 1:50, medium to 1:100, coarse from 1:200); the site's symbols keep `PlanDetail.Of`. A 3D view set to coarse or medium hides the details as parts.

What stage C left: a subcategory's own graphics (colour, pattern; a subcategory is shown or hidden only), a filter's or a category's line weight as a pen for every role, overrides inside a custom link (it hides, shows and halftones), a traced image's halftone, the 3D underlays of a DXF following a 3D view's imported categories, a link drawn as an underlay.

Each stage: engine tests (`VisibilityGraphicsTests`), interface tests, the user guide (views.md, import-export.md, in the 8 translations), the dialog's scene, the strings in every language, an Ask Lapin entry, a decision row.

## D. Point clouds

Approved by the user on 08.11.2026, **in the open formats only**: LAS (ASPRS 1.0–1.4), LAZ, E57 (ASTM E2807), PLY (ASCII and binary), text XYZ/PTS/CSV. No proprietary format (Autodesk RCP/RCS, FARO FLS, Leica LGS…). Decoders licence-clean: Lapin's own C# readers; for LAZ a port to C# of laz-rs (Apache 2.0, its notice kept in `Lapin.PointClouds/Laz/NOTICE.md`), never LASzip (LGPL). Nothing GPL or LGPL.

A point cloud is a link like any other (`LapinLink` of kind `pointcloud`), with its own row on the Links tab; its points never enter the model.

- **Formats** (`engine/src/Platform/Lapin.PointClouds`, no dependency, managed only so the browser's engine has it too): `LasReader` (point formats 0–10, the WKT or GeoTIFF keys for the EPSG code and the linear unit, LAS 1.4's extended records), `Laz/` (pointwise items Point10, GpsTime, RGB12, Byte version 2 for formats 0–3; layered items Point14 and RGB14 version 3 for formats 6–8, only the layers drawn; fixed and variable chunks), `E57Reader` (pages with their CRC-32C checked, the XML, CompressedVector data packets, bit-packed integers, scaled integers and floats, cartesian or spherical, invalid states, each scan's pose), `PlyReader`, `TextPointReader`. Not read: waveforms in LAZ (formats 4, 5, 9, 10 compressed), LASzip 1.x's old items, E57 images.
- **Index** (`Indexer`, `PointStore`): Potree Converter 2's scheme written by Lapin. A pass for the box and statistics; a cloud of at most 2 million points indexed in memory; a larger one counted on a 128³ grid, cut into chunks of at most 2 million, distributed to chunk files, each chunk built top down (a node keeps one point a cell of its 128³ grid, at most 90 000, the rest to its children, leaves of at most 30 000), the levels above built bottom up from the chunks' roots. Coordinates quantized to the file's own step (lossless for LAS/LAZ), 18 bytes a point (xyz int32, rgb, intensity, class). Kept in the host's user folder (`PointClouds/<key>`; `LAPIN_POINTCLOUD_CACHE` overrides), keyed by the file's path, length and time (or its bytes' hash in a browser), never in the project; the link holds the key.
- **The link** (`PointCloudLinks`): `pointcloud.link` (by path or bytes) reads the header, suggests the placement (`shared` by the project's survey point and north when the file names a CRS or its coordinates look like map coordinates and the project is located; `center` when far from zero; else `origin`), the unit from the file (m, ft, US survey ft) with a size check; indexing in the background (a delivery writes the summary onto the link), at once in a browser. `pointcloud.place`, `pointcloud.update` (colour mode, point size), `pointcloud.reload`; queries `pointcloud.status`, `pointcloud.hierarchy` (nodes with their boxes in the project), `pointcloud.check`; the protocol's `pointcloud.tiles` sends tiles as raw arrays (positions in the project, packed colours, intensity and class).
- **Display** (`ui/src/viewport/pointClouds3d.ts`): every frame the nodes in the frustum chosen from the root, largest on screen first, children while a node's spacing covers more than a point, within a budget by the display quality (1.5 / 3 / 6 million; half in a browser); missing tiles asked for 12 at a time, three batches in flight, a cache of twice the budget; one shader (RGB, intensity, elevation ramp, ASPRS class colours, hidden classes, halftone, point size), clipped by the view's planes.
- **Measured** (desktop, Release, M-series Mac): a synthetic LAS of 100 million points indexed in 20.5 s (4.9 M points/s), peak working set 0.9 GB, 7 335 nodes; 20 million points of LAZ in 23.5 s (decoded three times: the passes); 200 tiles (2.6 million points) read from the store in 25 ms.

### Stages

- **D1** (decision 395): the readers, the index, the link with its placement and unit check, the 3D display, Manage → Links (status, colours, size, place, reload), File → Link point cloud…, the user guide (import-export.md), the scene `point-cloud`, Ask Lapin.
- **D2** (decision 396, done): the Point clouds tab of Visibility / Graphics (per view: shown, halftone, colour mode, ASPRS classes); plans and sections drawing the slice their cut takes as dots (a raster on a pen of its own, printed on sheets and in the PDF), usable to trace over, its points snapped to; snapping to points in 3D (the inference engine of decisions 374–383); the mesh exports carrying a cloud only when ticked.

Measured with D2 (the development engine, Chrome): 20 million points indexed in 11.7 s, 100 million in 55 s; the 3D at 60 fps (16.7 ms a frame, the screen's limit) with about 3 million points drawn, 165 MB of JS heap.

Later: a developed section's pieces drawing the cloud, the slice in DXF exports, snapping to clouds in the 3D sketch's plane, the index made in fewer passes for LAS/LAZ (the header's box trusted), chunks indexed in parallel, LAZ waveforms, E57 images, an index kept beside a project to carry it to another computer.
