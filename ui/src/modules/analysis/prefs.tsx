/** What a load tool puts down: its value and the case it goes in; the tool's option in the command line's bar, kept on this computer. */

import { create } from 'zustand';
import { useSession } from '../../engine/session';

const KEY = 'lapin.analysis.load';
type Kind = 'area' | 'line' | 'point';
interface Prefs { values: Record<Kind, string>; caseId: string | null }
const DEFAULT: Prefs = { values: { area: '2', line: '5', point: '10' }, caseId: null };

function read(): Prefs {
  try {
    const v = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Partial<Prefs> | null;
    if (v && typeof v === 'object') return { values: { ...DEFAULT.values, ...(v.values ?? {}) }, caseId: typeof v.caseId === 'string' ? v.caseId : null };
  } catch { /* the default */ }
  return DEFAULT;
}
export const useLoadPrefs = create<Prefs>(() => read());
const remember = (p: Prefs) => { try { localStorage.setItem(KEY, JSON.stringify(p)); } catch { /* this session only */ } };

/** The project's load cases (resources of class IfcStructuralLoadCase). */
export const useLoadCases = () => {
  const nodes = useSession((s) => s.nodes);
  return [...nodes.values()].filter((n) => n.type === 'IfcStructuralLoadCase').map((n) => ({ id: n.id, code: String(n.props.code ?? ''), name: n.name }));
};

const UNIT: Record<Kind, string> = { area: 'kN/m²', line: 'kN/m', point: 'kN' };

/** The value and the case, in the bar while a load tool is on. */
export function LoadOption({ kind }: { kind: Kind }) {
  const prefs = useLoadPrefs();
  const cases = useLoadCases();
  const set = (p: Prefs) => { remember(p); useLoadPrefs.setState(p); };
  const caseId = prefs.caseId && cases.some((c) => c.id === prefs.caseId) ? prefs.caseId : '';
  return (
    <span className="cad-layer on">
      <input aria-label="Load value" data-testid="load-value" title="The value of the load" style={{ width: 56 }}
        value={prefs.values[kind]} onChange={(e) => set({ ...prefs, values: { ...prefs.values, [kind]: e.target.value } })} />
      <span className="muted small">{UNIT[kind]}</span>
      <select aria-label="Load case" data-testid="load-case" title="The load case the load goes in" value={caseId} onChange={(e) => set({ ...prefs, caseId: e.target.value || null })}>
        <option value="">Imposed load (Q-A)</option>
        {cases.map((c) => <option key={c.id} value={c.id}>{c.code ? `${c.code} · ${c.name}` : c.name}</option>)}
      </select>
    </span>
  );
}

/** The value typed and the case chosen, or null (with a message) when the value does not read. */
export function currentLoad(kind: Kind): { value: number | null; caseId: string | null } {
  const { values, caseId } = useLoadPrefs.getState();
  const n = Number(values[kind].trim().replace(',', '.'));
  const cases = [...useSession.getState().nodes.values()];
  return { value: Number.isFinite(n) && n > 0 ? n : null, caseId: caseId && cases.some((c) => c.id === caseId) ? caseId : null };
}
