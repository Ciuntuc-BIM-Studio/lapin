# Lapin for the browser (.html)

Decision 482. **File → Lapin for the browser (.html)…** writes the project as one web page: a viewer that any
browser opens by double-click, from `file://`, with no network, and — unless left out — the project's `.lapin`
package itself, which **File → Open** in Lapin takes back. One file to send to a client, a site, a colleague
without Lapin.

## The file

```
<!DOCTYPE html>
<html lang="ro">
<head>
<meta charset="utf-8">
<meta name="lapin-package" content="lapin-project 3">      ← Lapin's marker, first in the head
…
</head>
<body>
<div id="lapin-start">…</div>                               ← what shows without scripts (or before the viewer runs)
<script type="application/json" id="lapin-data">…</script>   ← the data (below)
<script type="application/octet-stream" id="lapin-glb">…</script>   ← the 3D model, GLB in base64
<script type="image/svg+xml" data-drawing="<id>">…</script>  ← one per drawing
<script id="lapin-viewer">…</script>                        ← the viewer (three.js inside)
</body>
</html>
<plaintext hidden>
[the .lapin package, format 3, as Save writes it]
```

- **`<plaintext>`** makes the browser read everything after it as text (HTML's tokenizer never leaves that state),
  and `hidden` (with a `display:none` rule) keeps that text off the page: no garbage, no layout. Checked in Chrome
  by the guide's scene `html-export`.
- **The package is a zip as it stands.** A zip is read from its end (its central directory), and `ZipArchive`
  counts each entry's offset from the start of the stream it writes into, so the package written after the page
  has its offsets counted from the file's start: Lapin (`LapinPackage.Read`), `unzip -l` and Python's `zipfile`
  all read the `.html` as the zip (tested in `HtmlExportTests`; checked by hand with unzip and Python). Nothing is
  rewritten after: the package is byte for byte what `ProjectPackage.Write` writes, with the project's versions,
  issues' files and other tools' entries.
- **Escaping.** No block ends early: JSON has every `<` written `<`; the SVG and the viewer have `</script`
  written `<\/script` and `<!--` written `<\!--` (`HtmlExport.Script`), which JavaScript reads the same and the
  viewer undoes for a drawing (`unscript`). SVG text is XML-escaped and holds no script and no comment.

## The data (`lapin-data`)

`{ format: "lapin-html", version: 1, project, edition, app, date, lang, strings, theme, hasProject, model3d,
storeys: [{id, name, elevation}], drawings: [{id, name, kind, number?, w, h}], tables: [PageTable], elements: {gid: …} }`

- **strings**: the exporting user's language's phrase book (`phraseBook`, ui/src/i18n), so the viewer and the
  engine's labels (a class's fields, the estimate's columns) read in that language; English where it has none.
- **elements**: every element the page shows (in 3D, on a drawing, in a table): name, IFC class, type, storey,
  materials (the layers' or its own), its fields as the class describes them, the property sets a file brought,
  its quantity sets (`HtmlExport.Properties`).
- **tables**: what the edition's modules give through `IPageTables` (Lapin.Platform): the cost module's estimate
  (`CostPageTable`) — chapters heading their lines, the totals, the elements each line counts, marked indicative
  when a list it prices by is Lapin's own. An edition without the cost module has none: the export lives in the
  platform, the module fills the hook.

## What the engine draws

- **3D**: `MeshScene` + `GltfWriter.Glb`, as the 3D view (the one in front, else `3d`) shows it: its hidden
  elements and categories left out. Each element a node with its GlobalId, class, type and storey in `extras`.
- **Drawings**: the plans, sections, elevations (`Views.Render(print: true)`, laid on a page of their own at their
  scale) and sheets (`Sheets.Compose`, as the PDF prints them), written by `SvgWriter` from the same pens as
  `PdfWriter`: fills even-odd, lines per pen with colour, paper width and dashes, texts (cap height as in the PDF),
  pictures by their corners. After the drawing, each element's pick mark (its cut areas, its seen lines) as an
  invisible path with `data-gid`: a click selects it, in 3D too. The list (`HtmlExport.DefaultDrawings`) is
  the project browser's: each level's plan (its own floor plan view, else its default plan), the other plan views,
  sections, elevations, then the sheets by number. A drawing that draws nothing is left out.
- **Which are taken by default**: those that show a model element (`HtmlExport.TakenByDefault`), told by what the
  drawing draws, not by storey membership: an element counts when it has a pick mark on the drawing, so the view's
  Visibility / Graphics, its range and its crop decide, as for the PDF. A plan with only grids, section marks and
  texts (the test house's Foundations and First floor) shows none. A sheet shows one when one of its viewports does,
  or when it holds a module's content (a schedule). Those that show none stay in the dialog's list, unticked and
  marked *(no element)*; a page written without a `drawings` list leaves them out, one written with them named takes
  them. A drawing whose rendering fails is not left out on a doubt.

## The viewer

`ui/src/standalone/` (Lapin's own code, three.js MIT): built on its own as one IIFE by the Vite plugin
`virtual:lapin-viewer` (ui/vite.config.ts), which the export dialog imports lazily, so the app carries it in one
lazy chunk (~630 kB). The interface hands its code to the engine with the call (`ui/src/engine/htmlExport.ts`), the
same for the desktop app, the browser against the local engine and the web build's engine in its worker; the
engine embeds nothing. A call without a viewer (an MCP client) writes the page with its hint only.

Tabs: **3D** (orbit, pan, zoom, Fit; a storey alone or with those below; click, Shift-click; Isolate, Hide, Show
all), **Drawings** (the list; drag, wheel; Fit the drawing; Print), one per table (sortable by a heading, a filter,
a row selects its elements), and the properties of the selection beside. One selection across them. Lapin's paper
and graphite themes (the exporting user's, switchable), the exporting user's language.

## Commands

- `project.exportHtml {drawings?, model3d?, tables?, project?, viewId?, viewer?, strings?, lang?, edition?, app?, theme?}`:
  delivered as every export (`path`: written there; else `{name, data}` to download), with `sizes` (page,
  drawings, model3d, project, viewer, total). Changes nothing: no undo step. Allowed to a guest of a shared session,
  as the other exports.
- `project.htmlPlan {viewId?, model3d?}`: every drawing, each with `default` (false, and `empty: true`, for one that
  shows no element; it renders each drawing to tell), whether a module has a table, and the sizes for the
  dialog's estimate (the project and the GLB measured, the drawings guessed).

## Opening it in Lapin

`project.open` (a path ending in `.html`/`.htm`, or any file that starts as a page) and `project.openBytes` (the
browser) read a page through `HtmlPackage.Check`: a page without Lapin's marker is refused ("not written by
Lapin"), Lapin's without its package too ("exported without its project"). The package is read from the whole
file as a zip. The project opens **unsaved, with no file of its own**: Save asks where (suggesting
`<page>.lapin`), never over the page. Its versions and other tools' entries are kept for that first save
(`ProjectFiles._page`), its issues' files in memory as a new project's are.

## Limits

- A snapshot: nothing a reader does on the page changes the project; the page is not a collaborative copy.
- Size: the project, the GLB (flat-shaded, as Export 3D writes it) and the drawings; ~0.6 MB of viewer. A large
  model makes a large page; each element is its own mesh in three.js (no instancing or merging yet).
- Point clouds in 3D, the documents (`Documents`), schedules on their own (a schedule placed on a sheet comes with the sheet) and the 4D schedule are not in the page.
- The browser edition's engine (Lapin.Browser) runs the same code; it was not rebuilt here (no wasm-tools on the
  machine that wrote this).
