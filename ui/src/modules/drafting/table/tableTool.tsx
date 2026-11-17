/**
 * Annotate → Table (issue #71, docs/plan-drafting-tables.md): a click puts a table's top-left corner on the view or the sheet's
 * paper in front, of the rows, columns and preset chosen in the command bar, and opens its editor. A table's Properties show
 * its size and open the editor again; so does a double-click on it.
 */

import { Table2, TableRowsSplit } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute } from '../../../engine/session';
import { useUi } from '../../../app/store';
import { isTable, openTableEditor, useNewTable, useTablePart, useTableStyles } from './tableStore';
import type { Grip, GripView, ToolDef } from '../../../platform/module';
import { NumberField } from '../../../shell/cells';
import { PRESETS, colName, sizeAt, sizeGrips, type TableJson } from './tableModel';

function TableOptions() {
  const { rows, cols, preset } = useNewTable();
  const styles = useTableStyles();
  return (
    <>
      <NumberField className="cad-length" testId="table-rows" title="Rows of the new table" suffix="rows" value={rows} min={1} max={500} onCommit={(v) => { if (v) useNewTable.setState({ rows: Math.round(v) }); }} />
      <NumberField className="cad-length" testId="table-cols" title="Columns of the new table" suffix="columns" value={cols} min={1} max={100} onCommit={(v) => { if (v) useNewTable.setState({ cols: Math.round(v) }); }} />
      <select className="cad-layer" data-testid="table-preset" title="How the new table looks" value={preset} onChange={(e) => useNewTable.setState({ preset: e.target.value })}>
        {PRESETS.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
        {styles.map((st) => <option key={st.id} value={st.id} data-no-i18n>{st.name}</option>)}
      </select>
    </>
  );
}

/** A table: its top-left corner; then its editor opens. */
export const tableTool: ToolDef = {
  id: 'drafting.table', label: 'Table', icon: Table2, shortcut: 'TT',
  gesture: { kind: 'point' },
  options: TableOptions,
  complete: async ({ points: [p], rotation }, ctx) => {
    const { rows, cols, preset } = useNewTable.getState();
    const angle = rotation ? -rotation : 0;
    const r = await ctx.execute('drafting.addTable', { viewId: ctx.viewId, at: [p.x, p.y], rows, cols, preset, ...(angle ? { angle } : {}) }) as { id?: string } | null;
    if (r?.id) {
      useUi.setState({ selection: r.id, selected: [r.id], tool: null });
      openTableEditor(r.id);
    }
  },
};

/** A table's Properties: its size, its turn, and its editor. */
export function TableSection({ node }: { node: NodeDto }) {
  if (!isTable(node)) return null;
  const t = node.props.table as TableJson | undefined;
  const angle = typeof node.props.angle === 'number' ? node.props.angle : 0;
  return (
    <dl className="fields" data-testid="table-section">
      <dt>Size</dt>
      <dd data-no-i18n>{`${t?.rows?.length ?? 0} × ${t?.cols?.length ?? 0}`}</dd>
      <dt>Angle</dt>
      <dd><NumberField value={angle} suffix="°" onCommit={(a) => void execute('element.update', { ids: [node.id], attrs: { angle: a ?? 0 } })} /></dd>
      <dt />
      <dd><button type="button" className="btn" data-testid="table-edit" onClick={() => openTableEditor(node.id)}>Edit table…</button></dd>
    </dl>
  );
}

/** A long table's part placed on its own (stage 5): a click puts its top-left corner on the view or sheet in front. */
export const tablePartTool: ToolDef = {
  id: 'drafting.tablePart', label: 'Place table part', icon: TableRowsSplit,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const { id, part } = useTablePart.getState();
    if (!id) return;
    const r = await ctx.execute('drafting.placeTablePart', { id, part, viewId: ctx.viewId, at: [p.x, p.y] }) as { id?: string } | null;
    if (r?.id) useUi.setState({ selection: r.id, selected: [r.id], tool: null });
  },
};

/** A table's grips on the drawing (stage 5): its columns' widths and rows' heights, each dragged as one edit. */
export function tableGrips(node: NodeDto, view?: GripView): Grip[] {
  if (!isTable(node) || !view?.mark?.data) return [];
  const d = view.mark.data as { cols?: number[]; rows?: number[]; rowIndex?: number[]; mm?: number };
  const at = Array.isArray(node.props.at) ? { x: Number((node.props.at as number[])[0]), y: Number((node.props.at as number[])[1]) } : null;
  if (!at || !d.cols || !d.rows || !d.mm) return [];
  const angle = typeof node.props.angle === 'number' ? node.props.angle : 0;
  const mm = d.mm;
  return sizeGrips(at, angle, mm, d.cols, d.rows, d.rowIndex).map((g) => {
    const k = d.rowIndex ? d.rowIndex.indexOf(g.index) : g.index;
    const start = g.kind === 'col' ? d.cols!.slice(0, g.index).reduce((a, b) => a + b, 0) : d.rows!.slice(0, k).reduce((a, b) => a + b, 0);
    return {
      key: g.key, at: g.at, kind: 'edgeMid' as const,
      title: g.kind === 'col' ? 'Drag: the column\'s width' : 'Drag: the row\'s height',
      step: (to) => {
        const v = sizeAt(g, to, at, angle, mm, start);
        const cell = g.kind === 'col' ? `${colName(g.index)}1` : `A${g.index + 1}`;
        return { command: 'drafting.editTable', args: { id: node.id, ops: [g.kind === 'col' ? { op: 'colWidth', range: cell, w: v } : { op: 'rowHeight', range: cell, h: v }] } };
      },
    };
  });
}
