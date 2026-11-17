/**
 * The table editor (issue #71, docs/plan-drafting-tables.md §5): the table's cells as a grid, as wide and tall as the engine
 * lays them, each showing what it reads (formulas worked out); a click selects a cell, shift-click a range, a double-click,
 * F2 or typing edits its text (Enter keeps it, Shift+Enter a new line, Esc leaves it), the bar above edits the first
 * selected cell's text as typed. Its buttons put rows and columns in or take them out, merge and split, style the selection
 * (bold, italic, alignment, height, text and background colours with opacity), draw its borders, set widths and heights or
 * fit them to the text, and set the table's preset. Each change is one call, one undo step; a pasted block of a spreadsheet's
 * cells (tab-separated) is one too.
 */

import { useCallback, useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react';
import { AlignCenter, AlignLeft, AlignRight, Bold, Italic, TableCellsMerge, TableCellsSplit, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { execute, query, useSession } from '../../../engine/session';
import { NumberField } from '../../../shell/cells';
import { ColourField } from '../../../shell/ColourField';
import { BORDERS, PRESETS, address, colsOp, grow, inSel, insertAt, isTableName, norm, opacityOf, ownStyle, pasteBlock, rangeOf, refInsert, rowsOp, withOpacity, type Sel, type TableInfo, type TableJson, type TableOp } from './tableModel';
import { TableRefPicker } from './TableRefPicker';
import { useTableEditor, useTableStyles } from './tableStore';
import { promptText, confirmAction } from '../../../shell/prompts';

const HELP = "Type in a cell, or double-click it. Formulas read the table's cells: {= B2 * C2}, {= sum(D2:D9)}, {= average(B2:B5) | 0.0}; on a sheet, its fields too: {sheet.number}.";
const HELP_TABLES = 'Another table is read by its name: {= table.Areas.B4 * 2}, {= sum(table.Areas.B2:B9)}, {table.Areas.A1}.';

/** A number field in the editor's bars: a short one, beside its label. */
const NUM = { width: 64 } as const;

/** Screen pixels per paper millimetre in the editor's grid. */
const PX = 4;

export function TableEditor() {
  const id = useTableEditor((s) => s.id);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const [info, setInfo] = useState<TableInfo | null>(null);
  const [sel, setSel] = useState<Sel>({ r0: 0, c0: 0, r1: 0, c1: 0 });
  const [editing, setEditing] = useState<{ r: number; c: number; text: string } | null>(null);
  const [bar, setBar] = useState('');
  const [border, setBorder] = useState<{ edges: string; w: number; c: string }>({ edges: 'all', w: 0.25, c: '#000000' });
  const [name, setName] = useState('');
  const styles = useTableStyles();

  const grid = useRef<HTMLDivElement>(null);
  const barCaret = useRef<{ from: number; to: number } | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => setName(node?.name ?? ''), [node?.name]);

  // What the table reads now, again after every change of it (an undo too).
  useEffect(() => {
    if (!id || !node) return;
    let live = true;
    void query<TableInfo>('drafting.table', { id }).then((r) => { if (live && r) setInfo(r); });
    return () => { live = false; };
  }, [id, node]);

  const n = norm(sel);
  const first = info?.cells[n.r0]?.[n.c0];
  useEffect(() => setBar(first?.t ?? ''), [first?.t, n.r0, n.c0]);

  const edit = useCallback(async (...ops: TableOp[]) => {
    if (!id || ops.length === 0) return;
    await execute('drafting.editTable', { id, ops });
  }, [id]);

  if (!id || !node || node.props.predefinedType !== 'TABLE') return null;
  const table = node.props.table as TableJson | undefined;
  const range = rangeOf(n);

  const commitCell = async (r: number, c: number, text: string) => {
    setEditing(null);
    if ((info?.cells[r]?.[c]?.t ?? '') !== text) await edit({ op: 'set', cell: address(r, c), text });
    grid.current?.focus();
  };
  const select = (r: number, c: number, extend: boolean) => {
    if (!info) return;
    setSel(grow(extend ? { ...sel, r1: r, c1: c } : { r0: r, c0: c, r1: r, c1: c }, info));
  };
  const move = (dr: number, dc: number) => {
    if (!info) return;
    const r = Math.max(0, Math.min(info.rows - 1, n.r0 + dr)), c = Math.max(0, Math.min(info.cols - 1, n.c0 + dc));
    select(r, c, false);
  };
  const onGridKey = (e: KeyboardEvent<HTMLDivElement>) => {
    if (editing || !info) return;
    const k = e.key;
    if (k === 'ArrowUp') { e.preventDefault(); move(-1, 0); }
    else if (k === 'ArrowDown') { e.preventDefault(); move(1, 0); }
    else if (k === 'ArrowLeft') { e.preventDefault(); move(0, -1); }
    else if (k === 'ArrowRight' || k === 'Tab') { e.preventDefault(); move(0, 1); }
    else if (k === 'Enter' || k === 'F2') { e.preventDefault(); setEditing({ r: n.r0, c: n.c0, text: first?.t ?? '' }); }
    else if (k === 'Delete' || k === 'Backspace') {
      e.preventDefault();
      const ops: TableOp[] = [];
      for (let r = n.r0; r <= n.r1; r++) for (let c = n.c0; c <= n.c1; c++) if (!info.cells[r][c].covered && info.cells[r][c].t) ops.push({ op: 'set', cell: address(r, c), text: '' });
      void edit(...ops);
    } else if (k.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); setEditing({ r: n.r0, c: n.c0, text: k }); }
  };
  const onPaste = (e: ClipboardEvent<HTMLDivElement>) => {
    if (editing) return;
    const text = e.clipboardData.getData('text/plain');
    if (!text) return;
    e.preventDefault();
    void edit({ op: 'set', cell: address(n.r0, n.c0), values: pasteBlock(text) });
  };
  const style = (s: Record<string, unknown>) => void edit({ op: 'style', range, style: s });
  /** Another table's cell or range put into the selected cell's text where the bar's caret was, as one edit. */
  const insertRef = (ref: string) => {
    if (!first || first.covered) return;
    const text = bar;
    const at = barCaret.current ?? { from: text.length, to: text.length };
    const next = insertAt(text, at.from, at.to, refInsert(text, at.from, ref));
    setBar(next.text);
    barCaret.current = { from: next.caret, to: next.caret };
    void edit({ op: 'set', cell: address(n.r0, n.c0), text: next.text });
  };
  const saveStyle = () => {
    const styleName = promptText('The table style\'s name', styles.find((st) => st.id === table?.preset)?.name ?? '');
    if (!styleName?.trim()) return;
    const cells = confirmAction('Keep its cells too, so a new table in this style starts as a copy of it?');
    void execute('drafting.saveTableStyle', { id: node.id, name: styleName.trim(), cells });
  };
  const rename = () => {
    const v = name.trim();
    if (v && v !== node.name) void execute('drafting.renameTable', { id: node.id, name: v }).then((r) => { if (!r) setName(node.name); });
  };
  const own = (k: string) => ownStyle(table, n.r0, n.c0, k);
  const fill = typeof own('fill') === 'string' ? own('fill') as string : null;
  const colour = typeof own('color') === 'string' ? own('color') as string : '#000000';
  const colW = info?.colW[n.c0] ?? null, rowH = info?.rowH[n.r0] ?? null;
  const fitCols = !!table?.cols?.[n.c0]?.fit, fitRows = !!table?.rows?.[n.r0]?.fit;
  const headerRows = !!table?.rows?.[n.r0]?.header;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Table">
      <div className="dialog" data-table-editor style={{ maxWidth: '96vw', width: 980 }}>
        <header className="dialog-head">
          <span className="panel-title">Table</span>
          <input data-testid="table-name" data-no-i18n title="The table's name: one word, so other tables' formulas can read it (table.Areas.B4)" value={name} style={{ width: 160, marginLeft: 8 }}
            onChange={(e) => setName(e.target.value)} onBlur={rename} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
          {!isTableName(name) && <span className="muted" style={{ marginLeft: 6 }}>Other tables read it once its name is one word.</span>}
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="table-tools" style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center', marginBottom: 6 }}>
            <button type="button" className="btn" data-testid="table-row-above" title="A row above the selection" onClick={() => void edit(rowsOp('above', n))}>Row above</button>
            <button type="button" className="btn" data-testid="table-row-below" title="A row below the selection" onClick={() => void edit(rowsOp('below', n))}>Row below</button>
            <button type="button" className="btn" data-testid="table-row-delete" title="The selected rows taken out" onClick={() => void edit(rowsOp('delete', n))}>Delete rows</button>
            <button type="button" className="btn" data-testid="table-col-left" title="A column left of the selection" onClick={() => void edit(colsOp('left', n))}>Column left</button>
            <button type="button" className="btn" data-testid="table-col-right" title="A column right of the selection" onClick={() => void edit(colsOp('right', n))}>Column right</button>
            <button type="button" className="btn" data-testid="table-col-delete" title="The selected columns taken out" onClick={() => void edit(colsOp('delete', n))}>Delete columns</button>
            <button type="button" className="icon" data-testid="table-merge" title="Merge the selected cells: the first one's text stays" onClick={() => void edit({ op: 'merge', range })}><TableCellsMerge size={15} /></button>
            <button type="button" className="icon" data-testid="table-split" title="Split the merged cells selected" onClick={() => void edit({ op: 'split', range })}><TableCellsSplit size={15} /></button>
            <select data-testid="table-preset-edit" title="The table's preset: its lines and header row" value={table?.preset ?? 'header'} onChange={(e) => void edit({ op: 'preset', preset: e.target.value })}>
              {PRESETS.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
              {styles.map((st) => <option key={st.id} value={st.id} data-no-i18n>{st.name}</option>)}
            </select>
            <button type="button" className="btn" data-testid="table-save-style" title="This table's look, sizes and header rows kept in the drafting standards, for new tables and to restyle others" onClick={saveStyle}>Save as style…</button>
            <label title="The selected rows are header rows: a style's header, repeated when a long table breaks"><input type="checkbox" data-testid="table-header-rows" checked={headerRows} onChange={(e) => void edit({ op: 'header', range, on: e.target.checked })} /> header row</label>
          </div>
          <div className="table-tools" style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center', marginBottom: 6 }}>
            <button type="button" className={`icon${own('bold') ? ' on' : ''}`} data-testid="table-bold" title="Bold" onClick={() => style({ bold: !own('bold') })}><Bold size={15} /></button>
            <button type="button" className={`icon${own('italic') ? ' on' : ''}`} data-testid="table-italic" title="Italic" onClick={() => style({ italic: !own('italic') })}><Italic size={15} /></button>
            <button type="button" className="icon" title="Align left" onClick={() => style({ align: 'left' })}><AlignLeft size={15} /></button>
            <button type="button" className="icon" data-testid="table-center" title="Centre" onClick={() => style({ align: 'center' })}><AlignCenter size={15} /></button>
            <button type="button" className="icon" title="Align right" onClick={() => style({ align: 'right' })}><AlignRight size={15} /></button>
            <select title="Where the text stands in its cell" value={typeof own('valign') === 'string' ? own('valign') as string : ''} onChange={(e) => style({ valign: e.target.value || null })}>
              <option value="">middle (the table's)</option>
              <option value="top">top</option>
              <option value="middle">middle</option>
              <option value="bottom">bottom</option>
            </select>
            <span title="Text height on paper; empty: the table's">Text</span>
            <NumberField style={NUM} value={typeof own('size') === 'number' ? own('size') as number : null} placeholder="2.5" suffix="mm" min={0.5} max={50} onCommit={(v) => style({ size: v })} />
            <select data-testid="table-rot" title="The text along the row, or turned to read up or down it (the row fits its length)" value={String(own('rot') ?? 0)} onChange={(e) => style({ rot: Number(e.target.value) })}>
              <option value="0">text along</option>
              <option value="90">text up (90°)</option>
              <option value="270">text down (270°)</option>
            </select>
            <span title="The space round the text in the cell; empty: the table's">Padding</span>
            <NumberField style={NUM} testId="table-pad" value={typeof own('pad') === 'number' ? own('pad') as number : null} placeholder="1" suffix="mm" min={0} max={50} onCommit={(v) => style({ pad: v })} />
            <span>Colour</span>
            <ColourField value={colour} onChange={(c) => style({ color: c })} />
            <span title="The cells' background">Background</span>
            <ColourField value={fill ?? '#ffffff'} onChange={(c) => style({ fill: withOpacity(c, opacityOf(fill)) })} />
<NumberField style={NUM} testId="table-opacity" title="The background's opacity" value={fill ? Math.round(opacityOf(fill) * 100) : null} placeholder="100" suffix="%" min={0} max={100}
              onCommit={(v) => style({ fill: withOpacity(fill ?? '#ffffff', (v ?? 100) / 100) })} />
            <button type="button" className="btn" title="No background" onClick={() => style({ fill: null })}>No background</button>
          </div>
          <div className="table-tools" style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center', marginBottom: 6 }}>
            <span>Borders</span>
            <select data-testid="table-border-kind" value={border.edges} onChange={(e) => setBorder({ ...border, edges: e.target.value })}>
              {BORDERS.map((b) => <option key={b.id} value={b.id}>{b.label}</option>)}
            </select>
            <NumberField style={NUM} title="Line width on paper" value={border.w} suffix="mm" min={0} max={5} onCommit={(v) => setBorder({ ...border, w: v ?? 0 })} />
            <ColourField value={border.c} onChange={(c) => setBorder({ ...border, c })} />
            <button type="button" className="btn" data-testid="table-border-apply" onClick={() => void edit({ op: 'borders', range, edges: border.edges, w: border.w, c: border.c })}>Apply</button>
            <span title="The selected columns' width on paper">Width</span>
            <NumberField style={NUM} testId="table-col-width" value={colW != null ? Math.round(colW * 100) / 100 : null} suffix="mm" min={1} max={1000} onCommit={(v) => { if (v) void edit({ op: 'colWidth', range, w: v }); }} />
            <label title="The selected columns as wide as their widest text"><input type="checkbox" data-testid="table-fit-cols" checked={fitCols} onChange={(e) => void edit({ op: 'fit', range, axis: 'cols', on: e.target.checked })} /> fit to text</label>
            <span title="The selected rows' height on paper">Height</span>
            <NumberField style={NUM} testId="table-row-height" value={rowH != null ? Math.round(rowH * 100) / 100 : null} suffix="mm" min={1} max={1000} onCommit={(v) => { if (v) void edit({ op: 'rowHeight', range, h: v }); }} />
            <label title="The selected rows as tall as their text, at least their height"><input type="checkbox" data-testid="table-fit-rows" checked={fitRows} onChange={(e) => void edit({ op: 'fit', range, axis: 'rows', on: e.target.checked })} /> fit to text</label>
          </div>
          <div style={{ display: 'flex', gap: 6, alignItems: 'flex-start', marginBottom: 6 }}>
            <span className="muted" data-testid="table-address" data-no-i18n style={{ minWidth: 56 }}>{range}</span>
            <textarea data-testid="table-bar" rows={2} style={{ flex: 1, fontFamily: 'monospace' }} value={bar}
              placeholder="Text; {= A1 + B1} for a formula, {= sum(A1:A4) | 0.00} with a format"
              onChange={(e) => setBar(e.target.value)}
              onSelect={(e) => { const t = e.target as HTMLTextAreaElement; barCaret.current = { from: t.selectionStart, to: t.selectionEnd }; }}
              onBlur={() => { if (first && !first.covered && bar !== first.t) void edit({ op: 'set', cell: address(n.r0, n.c0), text: bar }); }}
              onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); (e.target as HTMLTextAreaElement).blur(); } }} />
          </div>
          <div ref={grid} tabIndex={0} data-testid="table-grid" onKeyDown={onGridKey} onPaste={onPaste}
            style={{ overflow: 'auto', maxHeight: '55vh', outline: 'none', border: '1px solid var(--line, #8884)' }}>
            {info && (
              <table style={{ borderCollapse: 'collapse', tableLayout: 'fixed', background: 'var(--paper, #fff)', color: '#000' }}>
                <colgroup><col style={{ width: 28 }} />{info.colW.map((w, c) => <col key={c} style={{ width: Math.max(24, w * PX) }} />)}</colgroup>
                <thead>
                  <tr><th />{info.colW.map((_, c) => <th key={c} data-no-i18n style={{ fontWeight: 400, fontSize: 11, color: '#666', background: c >= n.c0 && c <= n.c1 ? '#dde8ff' : '#f2f2f2' }}>{address(0, c).replace(/\d+$/, '')}</th>)}</tr>
                </thead>
                <tbody>
                  {info.cells.map((row, r) => (
                    <tr key={r} style={{ height: Math.max(18, info.rowH[r] * PX) }}>
                      <th data-no-i18n style={{ fontWeight: 400, fontSize: 11, color: '#666', background: r >= n.r0 && r <= n.r1 ? '#dde8ff' : '#f2f2f2' }}>{r + 1}</th>
                      {row.map((cell, c) => {
                        if (cell.covered) return null;
                        const o = (k: string) => ownStyle(table, r, c, k);
                        const isEditing = editing?.r === r && editing.c === c;
                        const on = inSel(n, r, c);
                        return (
                          <td key={c} rowSpan={cell.rs ?? 1} colSpan={cell.cs ?? 1} data-cell={address(r, c)} data-no-i18n
                            onMouseDown={(e) => { if (!isEditing) { e.preventDefault(); grid.current?.focus(); select(r, c, e.shiftKey); } }}
                            onDoubleClick={() => setEditing({ r, c, text: cell.t })}
                            style={{
                              border: '1px solid #bbb', padding: '1px 3px', verticalAlign: (o('valign') as string) ?? 'middle', whiteSpace: 'pre-wrap', overflow: 'hidden',
                              textAlign: (o('align') as 'left' | 'center' | 'right') ?? 'left', fontWeight: o('bold') ? 700 : 400, fontStyle: o('italic') ? 'italic' : 'normal',
                              color: (o('color') as string) ?? '#000', background: (o('fill') as string) ?? undefined,
                              outline: on ? '2px solid var(--accent, #2a6df4)' : undefined, outlineOffset: -2, fontSize: 12,
                              // A turned cell (stage 3) reads up (90°) or down (270°) its row, as the drawing has it.
                              ...(o('rot') === 90 ? { writingMode: 'vertical-rl' as const, transform: 'rotate(180deg)' } : o('rot') === 270 ? { writingMode: 'vertical-rl' as const } : {}),
                            }}>
                            {isEditing ? (
                              <textarea autoFocus data-testid="table-cell-input" value={editing.text} rows={Math.max(1, editing.text.split('\n').length)}
                                style={{ width: '100%', boxSizing: 'border-box', font: 'inherit', resize: 'none' }}
                                onChange={(e) => setEditing({ r, c, text: e.target.value })}
                                onBlur={() => void commitCell(r, c, editing.text)}
                                onKeyDown={(e) => {
                                  if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); setEditing(null); grid.current?.focus(); }
                                  else if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); void commitCell(r, c, editing.text).then(() => move(1, 0)); }
                                  else if (e.key === 'Tab') { e.preventDefault(); void commitCell(r, c, editing.text).then(() => move(0, 1)); }
                                }} />
                            ) : cell.shown}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
          {info && info.errors.length > 0 && (
            <ul className="muted" data-testid="table-errors" style={{ marginTop: 6, color: 'var(--danger, #c33)' }}>
              {info.errors.map((e) => <li key={e} data-no-i18n>{e}</li>)}
            </ul>
          )}
          <TableRefPicker self={node.id} onInsert={insertRef} />
          <p className="muted" style={{ marginTop: 6 }}>{HELP}</p>
          <p className="muted">{HELP_TABLES}</p>
        </div>
      </div>
    </div>
  );
}
