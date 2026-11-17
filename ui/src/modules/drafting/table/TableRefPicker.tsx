/**
 * The table editor's reference picker (docs/plan-drafting-tables.md, stage 2): another table of the project, its cells as they
 * read; a click picks a cell, shift-click a range, and Insert puts `table.<name>.B4` (or the range) into the selected cell's
 * text where its caret was: the reference alone inside a formula, a token `{table.Areas.B4}` or a total `{= sum(…)}` elsewhere.
 * Only tables with a name a formula can read (one word) are offered; the others are listed with how to make them readable.
 */

import { useEffect, useMemo, useState } from 'react';
import { query, useSession } from '../../../engine/session';
import { address, grow, inSel, isTableName, norm, tableRef, type Sel, type TableInfo } from './tableModel';

export function TableRefPicker({ self, onInsert }: { self: string; onInsert: (ref: string) => void }) {
  const nodes = useSession((s) => s.nodes);
  const tables = useMemo(() => [...nodes.values()].filter((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === 'TABLE' && n.id !== self)
    .sort((a, b) => a.name.localeCompare(b.name)), [nodes, self]);
  const [id, setId] = useState<string>('');
  const [info, setInfo] = useState<TableInfo | null>(null);
  const [sel, setSel] = useState<Sel>({ r0: 0, c0: 0, r1: 0, c1: 0 });
  const node = id ? nodes.get(id) : undefined;

  useEffect(() => {
    if (!id || !node) { setInfo(null); return; }
    let live = true;
    void query<TableInfo>('drafting.table', { id }).then((r) => { if (live && r) setInfo(r); });
    return () => { live = false; };
  }, [id, node]);

  const readable = node ? isTableName(node.name) : false;
  const n = norm(sel);
  return (
    <div data-testid="table-ref-picker" style={{ marginTop: 6, borderTop: '1px solid var(--line, #8884)', paddingTop: 6 }}>
      <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
        <span title="A cell or a range of another table, put into the selected cell's text">Another table</span>
        <select data-testid="table-ref-table" value={id} onChange={(e) => { setId(e.target.value); setSel({ r0: 0, c0: 0, r1: 0, c1: 0 }); }}>
          <option value="">—</option>
          {tables.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
        </select>
        {node && readable && <span className="muted" data-no-i18n>{tableRef(node.name, n)}</span>}
        <button type="button" className="btn" data-testid="table-ref-insert" disabled={!node || !readable} onClick={() => node && onInsert(tableRef(node.name, n))}>Insert reference</button>
        {node && !readable && <span className="muted">Give that table a name of one word (letters, digits, _) to read it.</span>}
      </div>
      {info && readable && (
        <div style={{ overflow: 'auto', maxHeight: 160, marginTop: 4 }}>
          <table style={{ borderCollapse: 'collapse', fontSize: 11, background: 'var(--paper, #fff)', color: '#000' }}>
            <tbody>
              {info.cells.map((row, r) => (
                <tr key={r}>
                  {row.map((cell, c) => cell.covered ? null : (
                    <td key={c} rowSpan={cell.rs ?? 1} colSpan={cell.cs ?? 1} data-ref-cell={address(r, c)} data-no-i18n
                      onMouseDown={(e) => { e.preventDefault(); setSel(grow(e.shiftKey ? { ...sel, r1: r, c1: c } : { r0: r, c0: c, r1: r, c1: c }, info)); }}
                      onDoubleClick={() => node && onInsert(tableRef(node.name, n))}
                      style={{ border: '1px solid #bbb', padding: '0 3px', minWidth: 24, whiteSpace: 'pre', cursor: 'pointer', outline: inSel(n, r, c) ? '2px solid var(--accent, #2a6df4)' : undefined, outlineOffset: -2 }}>
                      {cell.shown}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
