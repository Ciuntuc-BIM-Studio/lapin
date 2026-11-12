# Jogged, curved and developed sections

Asked on 30.10.2026 (decision 176): "systems of sections made of straight polylines with segments in different directions, or even curved sections, or through an alignment".

## One mechanism

A section's line is a polyline in plan (`SectionPaths`). Each straight piece is a planar section looking left of the piece (as before), that **sees only between its two ends**: `ViewSpec.Span = (0, length)`, applied in `HiddenLines.Draw(span)` — the edges are clipped where they cross the piece's end planes, the cut areas are clipped there too (Sutherland–Hodgman on x), nothing is drawn at the clip, and a solid wholly outside the piece is skipped. The pieces are then laid side by side at their stations (the developed x: the sum of the lengths before them), `ViewDrawing.Shifted` and `Join`, so a jogged section reads as one drawing at true lengths (`Views.Generate`, `Views.Pieces`). A plain two-point section is one piece without a span, exactly as it was.

- **Jogged**: `pts` with three or more points. Drawn **projected** by default (below); `jogged: "developed"` on the view draws it developed as described here.
- **Curved**: `curve: "arc"` with three points (start, a point on the arc, end): the arc is sampled to pieces of at most 7.5° (4 to 48), `SectionPaths.SampleArc`.
- **Along an element** (an alignment): `along: <id>`. The element's line is read live each time the section is drawn, through `ISectionPaths` (Civil: paths, retaining walls, ditches with the station their chain starts at, site feature lines; Drafting: a drafted line, polyline, arc or circle) or, for a path element (a wall, a beam, a strip footing), its axis with its arc sampled. When the element is gone, the last line it had (`pts`) stays.
- `start` and `end` are kept as the first and last point, so everything that read a section as one line still works: `Views.Look` (the first piece), the DXF import's mirroring, the Studio sketches' plane, the legacy import, the key plan's "look from".

## Projected jogged sections (report #146, 12.11.2026)

A tester: "the section cuts the model along the break as well… grid line 4 goes through the wall in plan, but appears offset in section; expected: the section to work like Revit's, where the break does not cut, only the parallel segments". A jogged line (three or more points, no arc, following nothing, not across an element) is now drawn **projected** unless the view says `jogged: "developed"` (`SectionPaths.Projects`, `SectionPaths.Cuts`, `SectionPaths.ProjectedPieces`):

- x in the drawing is the plan point's projection on the first piece's direction, from its start: one frame for the whole drawing, so `ViewContext.Map` (the first piece's frame) puts every annotation where it is in plan.
- The pieces parallel to the first (same sense, within `ParallelDegrees` = 1°) cut, each on a plane of the first piece's direction through the piece's middle (a near-parallel piece is straightened, at most 1° off), seeing only its stretch of x.
- The other pieces are jogs: they cut nothing and add no width. Where the cut jumps from one parallel piece to the next is the middle of the jogs between them in x: a perpendicular jog's own x, an oblique one's middle, so the stretches meet with no gap.
- A line that turns back over itself (a parallel piece going the other way, a stretch running backwards) cannot be projected: it is drawn developed.
- Each cut is a `SectionPaths.Piece` from the start of its stretch to its end, laid at its station (x0), so everything that worked piece by piece (`Views.Pieces`, `Generate`'s laying, `frames`, `PlaneOf(atX)`, `ViewPlanes`, the traces in other views, the surface hatches and shadows, the voids and clash marks, the CAD underlays in 3D) takes it unchanged. A projected line with a single cut (an L) is that one piece, still seeing only its stretch.
- Grids in a section (`Grids.Marks`) stand where the axis crosses the cut whose stretch holds the crossing (`Views.CrossingX`), in both modes; an axis that crosses only a projected section's jog is not drawn. (Before, they took the first piece's infinite plane, which put them wrong on a developed section's later pieces.)
- No dashed jog line in a projected drawing; the stations, when asked, run along the projected x.
- The interface: the view's settings show **Jogged: Projected | Developed** for a jogged line (`view.setLine {id, jogged: "projected" | "developed"}`; projected, the default, stores nothing). Curved sections and sections along an element stay developed.
- Existing projects: no upgrade. A jogged section saved before has no `jogged`, so it opens projected, which is what the report asks for (the developed one was reported as wrong); its drawing gets narrower by the jogs' lengths, so a sheet's viewport of it shows the same model narrower. Set it back to Developed to keep the old drawing.

## Over the drawing

`Views.SectionMarks`: a fine grey dashed line down each jog of a developed section, over the drawing's height; with **stations** (`stations` m on the view; by default a tenth of the scale when the section follows an element, else none), a base line along the bottom with a tick and the chainage ("0+020", `SectionPaths.Station`) at every round station from the line's start station, and at both ends.

In a plan, a section's mark (`Views.Marks`) is its whole line, the strokes and arrows at its two ends each looking left of its end piece, its name at both ends; every point is a snap.

## The interface

- View → **Jogged section** (SJ: a polyline gesture, Enter finishes), **Curved section** (SC: three points, as a curved wall), **Section along** (SA: pick a road, a ditch, a retaining wall, a site line, a wall, a beam, a footing or a drafted shape). `ui/src/modules/core/sections.tsx`.
- Grips in plan (`sectionGrips`, on `LapinView`): the points (dragged; Ctrl/⌘-click takes one out when more than two remain), the middle of each piece (dragged or Ctrl/⌘-clicked adds a jog); a curved section its three points; a following section none. All through `view.setLine {id, pts}`.
- The view's settings: **Line** (straight, jogged, arc, along an element, with the length), **Jogged** (projected or developed, for a jogged line), **Follows** (Stop following keeps the line as it is), **Stations (m)**.
- The drawing's JSON carries `frames: [{origin, right, up, dir, x0, x1}]` for a developed or projected jogged section (a projected one's frames all share the first piece's right and dir): a drawing point (x, y) lies on the piece whose stretch holds x, at x − x0. In 3D (`sectionPieces`, `Viewport3D.sectionOverlay`) the drawing is laid piece by piece on its planes; the model's cut stays on the first piece's plane, since three.js clips with intersections of half-spaces only, and a jogged cut is a union.

## Decided by default (to confirm)

1. The pieces see only between their ends (Revit's jogged sections do the same); an element in the corner between two pieces may show in both or in neither, as the corner falls.
2. Stations are written only when asked, or when the section follows an element.
3. A developed section shown in 3D cuts along its first piece.
4. The key plan draws a curved section by its three points (its chord), not the arc.

## Sections from sections, the markers' ends, and where the markers show (decision 223)

Asked by the user (31.10.2026):

1. **Section here from a section.** `rebar.sectionHere` already places a click from a section or an elevation through its frame; what fails is the pick: `DrawingView.pickAny` tests only the distance to the marks' segments, so a click *inside* a cut beam or column (its loop) finds nothing, while a plan picks inside the cut. A section's pick takes the point inside a cut loop or a fill of the element too (the drawing's `cuts`/`fills` by element, as `planPick` does), nearest edge first, then the smallest loop that holds the point.
2. **The marker's ends.** A section across an element (`across`, `station`, `margin`) has no grips: its line is derived. It gets them: the two ends set `marginA`/`marginB` (one per side; `margin` stays the default for both), the middle grip moves the `station` along the element, and the ends of an ordinary section are what they are (`view.setLine`). The ends change the view's window (`Span`) as before; the user accepts that for now. `view.updateAcross {id, station?, marginA?, marginB?}`.
3. **Where the markers show.** A section's marker is drawn in every plan (as now), and in every other section and elevation whose window its plane crosses: a fine dashed line where the plane cuts the view, with the name and its arrow at the top, the same size on paper as in plans. Each section carries a `markerLayer` (free text; the default `Sections`), and each view says which markers it shows: `sectionMarkers: {hidden: [ids], hiddenLayers: [names]}` on the view (all shown when absent); `view.showSection {viewId, sectionId, shown}` and `view.showSectionLayer {viewId, layer, shown}`; the view's own marker is never drawn in itself. **View → Sections…** (`SectionsDialog`, for the view in front): the sections grouped by layer, a checkbox per layer and per section for *this view*, the layer editable per section (a select with the existing layers and a new one), a count of how many views hide each; a sheet shows what its viewports' views show. The 3D is left out for now.

Done (31.10.2026): `SectionMarkers` in the platform draws the traces (one per piece of each other section, cut against the view's planes as the reference planes are, a jogged section's piece only between its ends) and keeps `markerLayer` (a LapinView field, shown for sections), `sectionMarkers` on the view, `view.showSection`, `view.showSectionLayer` and the query `view.sectionMarkers` (`{sections: [{id, name, layer, shown, hiddenIn}], layers: [{name, shown}]}`); the plan's markers obey the same hiding. `AcrossSections` takes `marginA`/`marginB` in its line, span and window, and `view.updateAcross {id, station?, marginA?, marginB?}` holds the station within the element and moves an engine-made name with it; the across section's grips in plan are in `sections.tsx` (`acrossGrips`). A section's or elevation's pick marks carry their cut and filled loops (`data.loops`), and `ui/src/viewport/drawingPick.ts` takes a line first, then the smallest loop holding the point. **View → Sections…** is `SectionsDialog.tsx` over `sectionMarkers.ts`. Tests: `SectionMarkerTests`, `drawingPick.test.ts`, `sectionMarkers.test.ts`.

Surface hatches and sun shadows (decision 327, 05.11.2026): a developed section hatches and shadows each piece on its own plane (`SurfaceHatches.Piece`: the piece's spec, its station and its faces), the faces seen only between the piece's ends (`ViewFaces` with `span`; the parts' faces gathered once for all pieces, a face outside the span still casting), the piece's own cut areas hiding what lies behind them, then laid at its station; a hatch aligned to the view runs from the developed drawing's origin, so courses go on across the pieces; a sun of the view's own turns with each piece. A curved section is its sampled chords, so it is the same work. Tests: `SurfaceHatchTests` (jogged, its shadows, curved).
