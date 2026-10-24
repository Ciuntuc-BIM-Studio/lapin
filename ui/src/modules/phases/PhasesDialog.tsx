/** Phases: the project's phases in order, the active one (new work, the 3D) and the filter the 3D applies. */

import { useEffect, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

export interface PhaseSettingsDto {
  phases: { id: string; name: string; kind: string }[];
  filters: { id: string; name: string; states: Record<string, string> }[];
  overrides: Record<string, { color: string; opacity: number }>;
  active?: string; activeFilter?: string;
}

export function usePhaseSettings(): PhaseSettingsDto | null {
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<PhaseSettingsDto | null>(null);
  useEffect(() => { void query<PhaseSettingsDto>('phases.settings').then((r) => r && setS(r)); }, [nodes]);
  return s;
}

export function PhasesDialog() {
  const s = usePhaseSettings();
  const error = useSession((st) => st.error);
  const [name, setName] = useState('');
  if (!s) return null;
  const active = s.active ?? s.phases[s.phases.length - 1]?.id;
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Phases">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Phases</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <table className="standards-table">
            <thead><tr><th>Active</th><th>Name</th><th>Kind</th><th /></tr></thead>
            <tbody>
              {s.phases.map((p) => (
                <tr key={p.id}>
                  <td><input type="radio" name="active-phase" checked={active === p.id} onChange={() => void execute('phases.setActive', { phase: p.id })} /></td>
                  <td><input key={p.id + p.name} defaultValue={p.name} onBlur={(e) => e.target.value.trim() && e.target.value !== p.name && void execute('phases.updatePhase', { id: p.id, name: e.target.value })} /></td>
                  <td><select value={p.kind} onChange={(e) => void execute('phases.updatePhase', { id: p.id, kind: e.target.value })}>{['existing', 'construction', 'execution'].map((k) => <option key={k}>{k}</option>)}</select></td>
                  <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => void execute('phases.removePhase', { id: p.id })}><Trash2 size={12} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row">
            <input value={name} placeholder="New phase" onChange={(e) => setName(e.target.value)} />
            <button type="button" className="btn" disabled={!name.trim()} onClick={() => void execute('phases.addPhase', { name: name.trim(), after: active }).then(() => setName(''))}><Plus size={14} />Add after the active one</button>
          </div>
          <label className="field-line"><span className="eyebrow">3D shows</span>
            <select value={s.activeFilter ?? ''} onChange={(e) => void execute('phases.setActive', { filter: e.target.value || null })}>
              <option value="">everything as it is</option>
              {s.filters.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </label>
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}
