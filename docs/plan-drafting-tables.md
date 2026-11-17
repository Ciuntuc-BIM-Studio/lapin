# Drafting tables

Issue #71, from the forum (https://forum.lapinbim.com/t/119): a free table annotation beside the model's schedules, with presets
for cells, rows, columns and headers; multiline text with rotation; formulas; references to cells (`E5`) and to other tables'
cells; merge and split; widths and heights with min, max and fit-to-text; borders, text and background colours with opacity;
placeable on sheets, on views and in symbols (a room tag that grows with its text).

Stage 1 is built (decision 653). This page holds the design for all of it and what is left.

## 1. What a table is, and what it is not

A **schedule** lists the model (its rows come from elements, decision 287 and the schedule module); a **title block's revision
table** lists a sheet's issues (`Sheets.RevisionTable`). Both are computed, never typed. A **drafting table** is the user's own:
every cell typed, a formula where wanted. It lives where texts live: an `IfcAnnotation` with `PredefinedType = TABLE`, on a view
(`viewId`), drawn by the drafting module's `IViewAnnotations` like a text note. A sheet's paper is a view (`paper:<sheetId>`,
decision 620), so a table on a sheet is the same element on that view: copied with "Duplicate sheet", deleted with it, printed
by the sheet's PDF and DXF through the same marks. Nothing else stores tables, so a table never forks into a sheet variant and a
view variant.

Sizes are **paper millimetres**, as a text's height is: a table reads the same at 1:50 and 1:200. Its corner `at` is its
top-left in the view's coordinates, `angle` turns it; Move, Copy, Rotate and Mirror carry it as they carry a text
(`AnnotationPlacement`, `TEXT or TABLE`). Visibility / Graphics counts it among the texts (`VisibilityGraphics`: `TABLE →
texts`) until a category of its own is asked for.

## 2. Data model

`attrs.table`, JSON kept as typed (no derived value is stored: "what can be worked out is derived on every ask"):

```json
{
  "cols": [{ "w": 25 }, { "w": 18, "fit": true, "min": 10, "max": 60 }],
  "rows": [
    { "h": 7, "fit": true, "header": true,
      "cells": [{ "t": "Room", "bold": true, "fill": "#e6e6e6", "cs": 2 }, null] },
    { "h": 7, "fit": true, "cells": [{ "t": "{= sum(B3:B9) | 0.00}", "align": "right" }, null] }
  ],
  "style": { "size": 2.5, "pad": 1, "color": "#000000", "fill": null, "align": "left", "valign": "middle",
             "grid": { "w": 0.18, "c": "#000000" }, "outline": { "w": 0.35, "c": "#000000" } },
  "preset": "header"
}
```

- **Columns** have a width `w` (mm), or `fit` (as wide as their widest single-column text plus padding) within `min`/`max`.
- **Rows** have a height `h`; with `fit` (the default for new rows) they grow to their tallest single-row cell and never shrink
  under `h` (or `min`); `max` caps them. `header` marks the header row (presets style it; a later stage repeats it when a table
  breaks across sheets).
- **Cells** are row-major, one per column, `null` when empty. A cell has its text `t` (lines parted by `\n`), its span (`rs`,
  `cs`: it is the top-left, *master*, cell of a merged region; the cells it covers are `null` and ignored), its own style
  (`fill`, `color` as `#rrggbb` or `#rrggbbaa` with opacity, `bold`, `italic`, `size`, `align`, `valign`) and its own sides
  (`top`, `bottom`, `left`, `right`: `{w, c}`, `w = 0` for none).
- **Style**: the table's defaults; the font, weight and colour come from its `textStyle` (Drafting standards) like a text's.
- **Lines**: an edge between two cells takes the heavier of their own sides when either has one, else the table's `outline` at
  its edge and `grid` inside; none inside a merged cell. Collinear edges with the same pen are joined into one line, so a DXF
  holds long lines, not a segment per cell.

`DraftingTables.Read` turns the JSON into a `Grid` (columns, rows, a `Cell[,]` with covered cells marked, each cell's master) and
tolerates hand-made JSON (short rows filled, spans cut where they would overlap). A table's JSON size is bounded (500 rows, 100
columns, 10 000 characters a cell).

## 3. Formulas and references

Cells reuse the platform's formula language (`Lapin.Platform/Expressions.cs`, `Expr` and `LiveText`): `{= …}` anywhere in a
cell's text, a format after a bar (`{= B2 * C2 | 0.00}`), texts with `&`, `if`, `round`, `text`, `pad` and the rest.

- **A cell by its address**: `A1`, `B3`, `AA12` (case aside) is a name the table resolves: the referenced cell's value. A cell
  that is one formula and nothing else keeps its number (not its printed text); any other cell is read as `XValue.Read` reads
  a model's text (`"24.50 m²"` counts as 24.5 in arithmetic). An empty cell alone is 0.
- **Ranges**: `Expr` now takes an optional range reader, so `A1:A4` is read among a function's values only:
  `sum(A1:A4)`, `average(B2:B9)`, `count(C2:C20)`, `max(A1:C1; 10)`. `sum`, `average` and `count` are new functions of the
  language (they work on plain values elsewhere: `sum(1; 2)`); over a range they take the numbers and leave texts and empty
  cells out. With no range reader (every other formula in Lapin) a range is an error, so nothing else changes.
- **Cycles**: a cell reading itself, directly or through others, does not read: it prints `<formula>` (as every formula that
  does not read) and the table's errors say `A1 reads itself`. Evaluation is lazy and memoised per drawing (`DraftingTables.Values`).
- **Rows and columns put in or taken out** rewrite every cell's formulas, as a spreadsheet does (`TableEdits.ShiftRefs`):
  references after the change move, a range narrows when some of its rows go, a reference to a cell taken out becomes `REF`
  (which reads as an unknown name). Quoted texts, function names and dotted names are left alone.
- **Tokens**: on a sheet's paper a cell reads the sheet's tokens as a text there does (`{sheet.number}`, `{project.name}`,
  and as formula names, `{= sheet.count + 1}`), through `PaperViews.Tokens`. Elsewhere tokens print as typed.

### Other tables' cells (stage 2, done)

A table's **name** is its `Name`; a formula reads a table whose name is one word (letters, digits, `_`, not starting with a
digit: `TableScope.IsName`). A reference is a dotted name, which `Expr` reads as one word: `table.Areas.B4` (case aside), a
token `{table.Areas.B4}` showing the cell as it reads, ranges `table.Areas.B2:B9` or `table.Areas.B2:table.Areas.B9` among a
function's values. `TableScope` (Platform) holds the project's tables for one drawing: each worked out once (`Of`), with its
own sheet's tokens (`outerFor`), and the set of cells being worked out kept across tables, so a circle through two tables does
not read and both say so.

- **Finding a name**: the table of that name on the same view (a sheet's paper) first, else the only one in the project; two
  or more elsewhere is an error naming how many ("rename one"). So a sheet duplicated with its tables reads its own, and an
  ambiguous name never reads the wrong one silently. Names are checked unique when given (`drafting.addTable name`,
  `drafting.renameTable`, `element.rename`, which goes through the same rename by `IDrivenElements`); copies keep the name.
- **Rename** rewrites every reference that reads the renamed table (as each table finds that name, before the change), in
  formulas and tokens, in one undo step (`TableCommands.Rewrite`, `TableEdits.MapRefs`).
- **Delete** (`IDeletionFollowers`): what read a deleted table reads `REF`, so a table later given its name does not take its
  place; undo brings both back. A table deleted with its sheet (not through `element.delete`) leaves its readers unknown
  (`<table.X.A1>`), the same printed result.
- **Rows and columns** put into a table or taken out move the references other tables make to it, as a spreadsheet does
  (`TableEdits.ApplyTracked` reports the shifts; `TableEdits.Shift` is the one rule for local and qualified references).
- **Picker**: under the editor's grid, *Another table* shows another table's cells; a click (shift-click: a range) and
  *Insert reference* put it into the selected cell where the bar's caret was: as it is inside a formula, else as a token
  `{table.Areas.B4}` or, for a range, its total `{= sum(…)}` (`tableModel.refInsert`).

## 4. Commands (one undo step each)

| Command | Arguments | Does |
|---|---|---|
| `drafting.addTable` | `viewId, at, rows, cols, preset?, colWidth?, rowHeight?, angle?, textStyle?, cells?, name?` | places a table; `cells` fills it from A1 (Python, MCP) |
| `drafting.editTable` | `id, ops: [{op, …}]` | applies the ops in order; one that does not read refuses them all |
| `drafting.renameTable` | `id, name` | renames a table, the references to it following |
| `drafting.saveTableStyle` | `id, name, cells?, styleId?` | keeps the table as a table style of the drafting standards |
| `drafting.table` (query) | `id` | the name, the cells as typed and as shown, spans, the laid sizes, the errors |

Ops (`TableEdits.Ops`): `set` (`cell, text` or `values` from that cell: a pasted block), `style` (`range, style`), `borders`
(`range, edges: all|outer|inner|top|bottom|left|right|none|default, w, c`), `merge`, `split`, `insertRows`/`insertCols`
(`at, count`), `deleteRows`/`deleteCols`, `colWidth` (`range, w`), `rowHeight` (`range, h`), `fit` (`range, axis:
rows|cols|both, on, min?, max?`), `tableStyle` (`style`), `preset` (`preset`: built in, or a table style's id or name),
`header` (`range, on`); `style` takes `pad` and `rot` too. Both commands are in the command catalogue;
guests who may edit run them like any edit (no entry needed in `AllowedRemote`).

## 5. Interface

- **Annotate → Text → Table** (TT): the command bar holds rows, columns and the preset; a click puts the top-left corner; the
  editor opens on the new table.
- **The table editor** (`TableEditor.tsx`): the cells as a grid scaled from the engine's laid sizes, each showing what it reads;
  click, shift-click (a selection grows to take merged cells in whole), arrows, Tab, Enter/F2 or typing to edit, Shift+Enter a
  new line, Esc, Delete clears, paste from a spreadsheet (tab-separated). A bar edits the first cell's text as typed. Buttons:
  rows above/below, columns left/right, delete, merge, split, preset; bold, italic, alignment, vertical alignment, text height,
  colour, background with opacity; borders (kind, width, colour); width and height of the selection with fit to text. Each
  button is one `editTable` call. The formula errors are listed under the grid.
- **Properties**: size, angle, Edit table…; a double-click on the table opens the editor too.

## 6. Presets

Built in: `header` (grid lines, outline 0.35, first row bold and shaded), `grid`, `minimal` (no grid, a rule under the header),
`none`. A preset sets the table's lines and styles its first row; set again it restyles them.

**User presets (stage 3, done)** follow the standing rule of extensibility: a table saved as a *table style*
(`drafting.saveTableStyle {id, name, cells?}`) is kept in the drafting standards (`StyleBook.TableStyles`, JSON
`tableStyles: [{id, name, table, cells}]`), so `.lapinstandards` files carry it (`StandardsLibrary.Lists`, kind `table`) and
`standards.set` / `standards.remove` (kind `table`) rename or remove it (Manage → Drafting standards → Tables). Its `table` is
the table's JSON: its style, columns' and rows' sizes, header rows and cells' own looks; its texts and spans only when saved
with `cells` (a template). A style is applied as a preset is, a copy of its look, never a live link (the built-in presets work
the same way, and a table never changes because a style elsewhere was edited): `preset` in `drafting.addTable` and in the
`preset` op takes a style's id or name (`DraftingTables.StyleOf`). `DraftingTables.ApplyStyle`: the style's `style`; its leading
header rows on the table's first rows, the table's other rows taking the style's body rows in turn (two body rows band them);
each cell the look (`LookKeys`: fill, colour, bold, italic, size, alignments, padding, turn, sides) of the style's cell in its
column (the last one for columns past it), texts and spans kept; a new table also takes the widths and heights. A style with
its cells gives a new table its copy whole (its rows and columns, not the bar's). Saved again under the same name, a style is
replaced. The built-in presets keep their output.

**Header rows** are marked by the `header` op (`range, on`); a style's header is its leading header rows (stage 5 repeats them).

**Turned text and padding (stage 3, done).** A cell's `rot` (0, 90: read bottom to top, 270: top to bottom) lays its text along
the row's height: the row fits the longest line, the column the lines' stack; in a row of a fixed height the text wraps at it
(`DraftingTables.Lay`). `pad` is a cell's own padding over the table's. `DraftingTables.Placements` places every line of every
cell (in the text's own frame, then turned into the cell) for the view's marks and the symbols alike.

## 7. Stages

1. **Done (this work).** The element on views and sheets, cells with multiline text, formulas with same-table references and
   ranges (`sum`, `average`, `count`), reference rewriting on insert/delete, merge/split, widths, heights, fit with min/max,
   borders, text and background colours with opacity, presets, the engine's drawing (views, sheets, PDF, DXF), the editor,
   undo per edit, saving (plain attributes: no project upgrade). Tests: `DraftingTableTests` (16), `tableModel.test.ts` (6).
2. **Done.** Other tables' cells: table names, `table.<name>.A1` references and ranges across tables, rename and delete
   rewriting, rows and columns moving other tables' references, the cross-table cycle guard; the editor's name field and
   reference picker. Tests: `DraftingTableTests` (6 more), `tableModel.test.ts` (3 more).
3. **Done.** User presets and cell rotation: table styles in the drafting standards and their files, saved from a table,
   applied as presets, with their cells as templates; header rows; cell text turned 90°/270° (`rot`) with the row fitting its
   length; per-cell padding. Tests: `DraftingTableTests` (4 more).
4. **Tables in symbols and from the model**: a table inside a symbol definition (the room tag that grows with its text), its
   cells reading the symbol's live tokens (`{attr.…}`, `{= attr.area | 0.00}`) through the symbol's resolver, so a placed copy
   lays its own sizes; a cell reading a schedule's total or an element's parameter.
5. **Long tables and exchange**: a table split across columns or sheets with its header repeated; CSV/XLSX import and export
   of the cells (written after the public specifications, no dependency); grips on the drawing for column widths and row
   heights (the marks already carry the laid sizes in `Data`).

## 8. Files

Engine: `engine/src/Modules/Drafting/Lapin.Drafting/DraftingTables.cs` (model, values, layout, lines, presets),
`TableEdits.cs` (ops, reference rewriting), `TableCommands.cs` (commands, query, marks); `Expressions.cs` (ranges, `sum`,
`average`, `count`). UI: `ui/src/modules/drafting/table/` (`tableModel.ts`, `tableStore.ts`, `tableTool.tsx`,
`TableEditor.tsx`). Guide: docs/user/drafting.md → Tables, its scene `drafting-table`.
