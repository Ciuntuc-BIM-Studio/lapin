/** Manage → Components (docs/components.md): the project's components, how many instances each has, renamed, their instances selected. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { select, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

interface Row { id: string; name: string; instances: string[]; members: number }

export function ComponentsDialog() {
  const version = useSession((s) => s.nodes);
  const [rows, setRows] = useState<Row[]>([]);
  useEffect(() => { void query<Row[]>('component.list').then((r) => setRows(r ?? [])); }, [version]);
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Components" data-testid="components" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Components</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {rows.length === 0 ? <p className="muted small">No components yet. Select Studio solids, floors or rooms, then Modify → Make component.</p> : (
            <table className="grid small">
              <thead><tr><th>Component</th><th>Instances</th><th>Elements</th><th /></tr></thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} data-component={r.id}>
                    <td><input key={r.name} defaultValue={r.name} aria-label="Rename" data-no-i18n onBlur={(e) => { const name = e.target.value.trim(); if (name && name !== r.name) void execute('component.rename', { id: r.id, name }); }} /></td>
                    <td className="num">{r.instances.length}</td>
                    <td className="num">{r.members}</td>
                    <td><button type="button" className="btn small" onClick={() => { select(r.instances[0] ?? null); useUi.setState({ selected: r.instances, dialog: null }); }}>Select</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
