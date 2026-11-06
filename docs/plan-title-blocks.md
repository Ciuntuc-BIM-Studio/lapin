# Title blocks of the user's own (report #76)

A tester: "I want to make a custom title block. To look like the official Finnish title block. I cannot do it?" Until now a
sheet's title block had one fixed layout (`Sheets.Compose`), its fields typed per sheet (project, client, drawn by, checked,
date, revision, status, phase) or taken from Manage → Project information… (decision 311).

The user's design: a manual configurator that lets you draw and insert parametric text; a project configurator in Manage
with every piece of data a title block needs, each with a unique parameter name, inserted as live text into the template;
templates kept in a library of title blocks, the data in the project. ArchiCAD (master layouts with autotext), AutoCAD
(blocks with attributes) and Revit (title block families with labels) all have it.

## Design

### 1. The project's data (Manage → Project information…)

- `ProjectInfo.Fields` grows to what a title block commonly asks, each with its group: **Project** (number, phase, phase
  name, contract), **Client** (client, address), **Site** (address, cadastral number, land book, building permit and its
  date), **Designer** (company, address, registration / licence number, phone, e-mail), **People** (project manager, chief
  architect, structural engineer, MEP engineer, drawn by, checked by, approved by).
- **Parameters of the user's own**: `info.custom` on the IfcProject, each `{key, label, type (text, number, date), group,
  value}`; the key unique (letters, digits, `_`; not a built-in field's), read as `{project.<key>}` by title blocks and
  `{{project.<key>}}` by documents. Commands `project.setParameter` and `project.removeParameter`, one undo step each.
- IFC: written as Lapin's own property set `Lapin_ProjectInformation` on IfcProject (never `Pset_`): the fields as IfcLabel
  / IfcText, a number as IfcReal, a date as IfcDate; the import reads it back.

### 2. The template (`LapinTitleBlock`)

An entity of the project (not exported to IFC), named, holding its template:

```json
{ "w": 180, "h": 50, "anchor": "bottomRight",
  "frame": { "show": true, "left": 10, "top": 10, "right": 10, "bottom": 10 },
  "revisions": "above",
  "items": [ { "type": "rect", "x": 0, "y": 0, "w": 180, "h": 50, "stroke": 0.5 },
             { "type": "text", "at": [3, 40], "text": "Project: {project.name}", "h": 3.5, "bold": true } ] }
```

- The block is a box `w × h` paper mm; its items are in mm from the box's bottom-left, y up (as CAD draws). The box sits in
  the frame's corner named by `anchor` (bottom-right by default), so one template fits A4 to A0, portrait or landscape. The
  frame's margins are the template's (ISO 5457's 20 mm filing margin on the left, if wanted); `frame.show: false` leaves
  the frame out.
- Items: `line`, `rect`, `polyline` (closed or not), fills (a rect's or a closed polyline's `fill`, or a `hatch` pattern
  of Drafting standards), `text` (height in mm or a text style of Drafting standards, bold, italic, anchor start / middle /
  end, angle), `image` (a LapinImage of the project: a logo) and `revisions` (the revision table: a box, a row height, its
  rows newest first, as many as fit).
- **Live text**: any text may hold tokens, `Project: {project.name}`. Three sources:
  - `project.*`: the name and every field of Project information, the user's own parameters; `param.<Name>` the
    project's parameters of decision 119.
  - `sheet.*`: number, name, scale (the scales of its views), paper, size, orientation, revision, status, date, drawnBy,
    checkedBy, phase, client, discipline, index and count (sheet x of y), and any title block field the sheet sets.
  - `system.*`: printDate, printTime, file, app.
  An unknown token prints as `<key>` (a placeholder the designer and the sheet show, and the CDE check reports); a known one
  with no value prints empty.
- Composed by the platform (`TitleBlocks.Draw`, no module needed: a product without drafting still has sheets).
- **Lapin standard** is the built-in `builtin:standard`: the code that drew every title block so far, untouched, so a
  project from before draws its sheets byte for byte the same. Two other built-ins, `builtin:cartus` (the Romanian
  "cartuș": designer, client, project number, the verifier's table, roles with names and signatures, scale, date, phase,
  sheet title and number) and `builtin:iso7200` (ISO 7200's fields: legal owner, title, supplementary title, identification
  number, revision, date of issue, sheet x/y, created by, approved by, document type and status, language). Built-ins are
  read only; Duplicate makes an editable copy (Lapin standard's copy is the same layout as items).

### 3. Assigning

- The project's default (`titleBlock` on the IfcProject; none: Lapin standard) and each sheet's own (`titleBlockId` on the
  sheet; none: the project's). Sheet properties: Title block; Manage → Title blocks…: Set as project default.
- The sheet, its PDF, Print, DXF and the issue draw it; the views' warnings ("over the title block") and a schedule's page
  size use the template's box and frame. The revision table goes where the template puts it (`revisions` item), else above
  the block as before (`revisions: "above"`), or nowhere (`"none"`).
- DXF: a template is written as a block (`TB_<name>`) inserted at 0,0, its live texts as attributes (ATTDEF in the block,
  ATTRIB on the insert, the tag from the token: `PROJECT_NAME`), so AutoCAD's ATTEDIT and data extraction see them.

### 4. The designer and the library (Manage → Title blocks…)

- The list: Built-in, This project, This computer (the app's storage, like document templates of decision 321). New,
  Duplicate (into the project or the library), Rename, Delete, Export… (.json, its images inside), Import… (into the project
  or the library), Copy to the project / library, Set as project default, Edit.
- The designer (a canvas of its own: the title block lives in paper mm, not on a view of the model, and drafting is an
  optional module): tools Select, Line, Rectangle, Polyline, Text, Field, Image, Revision table; a 0.5 / 1 / 2.5 / 5 mm grid
  and snaps to the items' corners, ends and middles; Ortho with Shift; typed coordinates in the item's fields; drag to move,
  Delete, Ctrl+Z / Ctrl+Y inside the designer; the item's properties (position, size, stroke, fill, text, height or text
  style, bold, anchor); the fields panel lists every token with its value for the sheet chosen as a sample, and a click
  inserts it (into the selected text, or as a new text); live text shows the value, a token with no value shows the token
  itself, greyed. Save writes the template as one undo step.

### 5. Tests

Engine: live text resolution and mixed text, the placeholder, anchoring on A3 and A1 (portrait too), a project from before
drawing the same pens, PDF and DXF holding the values (the DXF's block and attributes), the IFC property set both ways,
the commands (create, duplicate, update, delete falling back, default, per sheet). Vitest: the token parser, the
designer's snapping and item moves, the library.

## Open

- A title block's own drawing of each sheet's issue history beyond the revision table (the transmittal stays as it is).
- Signatures as images per person.
- Item groups and alignment helpers in the designer.
