/**
 * Annotate → Table (issue #71, docs/plan-drafting-tables.md): a click puts a table's top-left corner on the view or the sheet's
 * paper in front, of the rows, columns and preset chosen in the command bar, and opens its editor. A table's Properties show
 * its size and open the editor again; so does a double-click on it.
 */

import { Table2 } from 'lucide-react';
import type { NodeDto } from '../../../engine/protocol';
import { execute } from '../../../engine/session';
import { useUi } from '../../../app/store';
import { isTable, openTableEditor, useNewTable } from './tableStore';
import type { ToolDef } from '../../../platform/module';
import { NumberField } from '../../../shell/cells';
import { PRESETS, type Preset, type TableJson } from './tableModel';

function TableOptions() {
  const { rows, cols, preset } = useNewTable();
  return (
    <>
      <NumberField className="cad-length" testId="table-rows" title="Rows of the new table" suffix="rows" value={rows} min={1} max={500} onCommit={(v) => { if (v) useNewTable.setState({ rows: Math.round(v) }); }} />
      <NumberField className="cad-length" testId="table-cols" title="Columns of the new table" suffix="columns" value={cols} min={1} max={100} onCommit={(v) => { if (v) useNewTable.setState({ cols: Math.round(v) }); }} />
      <select className="cad-layer" data-testid="table-preset" title="How the new table looks" value={preset} onChange={(e) => useNewTable.setState({ preset: e.target.value as Preset })}>
        {PRESETS.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
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
