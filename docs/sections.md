# Developed sections: jogged, curved, along an element

Asked on 30.10.2026 (decision 176): "systems of sections made of straight polylines with segments in different directions, or even curved sections, or through an alignment".

## One mechanism

A section's line is a polyline in plan (`SectionPaths`). Each straight piece is a planar section looking left of the piece (as before), that **sees only between its two ends**: `ViewSpec.Span = (0, length)`, applied in `HiddenLines.Draw(span)` — the edges are clipped where they cross the piece's end planes, the cut areas are clipped there too (Sutherland–Hodgman on x), nothing is drawn at the clip, and a solid wholly outside the piece is skipped. The pieces are then laid side by side at their stations (the developed x: the sum of the lengths before them), `ViewDrawing.Shifted` and `Join`, so a jogged section reads as one drawing at true lengths (`Views.Generate`, `Views.Pieces`). A plain two-point section is one piece without a span, exactly as it was.

- **Jogged**: `pts` with three or more points.
- **Curved**: `curve: "arc"` with three points (start, a point on the arc, end): the arc is sampled to pieces of at most 7.5° (4 to 48), `SectionPaths.SampleArc`.
- **Along an element** (an alignment): `along: <id>`. The element's line is read live each time the section is drawn, through `ISectionPaths` (Civil: paths, retaining walls, ditches with the station their chain starts at, site feature lines; Drafting: a drafted line, polyline, arc or circle) or, for a path element (a wall, a beam, a strip footing), its axis with its arc sampled. When the element is gone, the last line it had (`pts`) stays.
- `start` and `end` are kept as the first and last point, so everything that read a section as one line still works: `Views.Look` (the first piece), the DXF import's mirroring, the Studio sketches' plane, the legacy import, the key plan's "look from".

## Over the drawing

`Views.SectionMarks`: a fine grey dashed line down each jog, over the drawing's height; with **stations** (`stations` m on the view; by default a tenth of the scale when the section follows an element, else none), a base line along the bottom with a tick and the chainage ("0+020", `SectionPaths.Station`) at every round station from the line's start station, and at both ends.

In a plan, a section's mark (`Views.Marks`) is its whole line, the strokes and arrows at its two ends each looking left of its end piece, its name at both ends; every point is a snap.

## The interface

- View → **Jogged section** (SJ: a polyline gesture, Enter finishes), **Curved section** (SC: three points, as a curved wall), **Section along** (SA: pick a road, a ditch, a retaining wall, a site line, a wall, a beam, a footing or a drafted shape). `ui/src/modules/core/sections.tsx`.
- Grips in plan (`sectionGrips`, on `LapinView`): the points (dragged; Ctrl/⌘-click takes one out when more than two remain), the middle of each piece (dragged or Ctrl/⌘-clicked adds a jog); a curved section its three points; a following section none. All through `view.setLine {id, pts}`.
- The view's settings: **Line** (straight, jogged, arc, along an element, with the length), **Follows** (Stop following keeps the line as it is), **Stations (m)**.
- The drawing's JSON carries `frames: [{origin, right, up, dir, x0, x1}]` for a developed section: a drawing point (x, y) lies on the piece whose stretch holds x, at x − x0. In 3D (`sectionPieces`, `Viewport3D.sectionOverlay`) the drawing is laid piece by piece on its planes; the model's cut stays on the first piece's plane, since three.js clips with intersections of half-spaces only, and a jogged cut is a union.

## Decided by default (to confirm)

1. The pieces see only between their ends (Revit's jogged sections do the same); an element in the corner between two pieces may show in both or in neither, as the corner falls.
2. Stations are written only when asked, or when the section follows an element.
3. A developed section shown in 3D cuts along its first piece.
4. The key plan draws a curved section by its three points (its chord), not the arc.
