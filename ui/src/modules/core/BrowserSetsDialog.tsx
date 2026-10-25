/**
 * Organize the project browser: its sets (docs/plan-views-and-joinery.md §1),
 * each a name and a filter on the views' and sheets' parameters (discipline,
 * kind, level, scale, a text in the name, the user's own parameters). Kept
 * on the project, so in the file.
 */

import { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { DISCIPLINES, KINDS, setsOf, type BrowserSet, type SetFilter } from '../../shell/browserSets';

const KIND_LABEL: Record<string, string> = { plan: 'Floor plans', section: 'Sections', elevation: 'Elevations', '3d': '3D views', sheet: 'Sheets', schedule: 'Schedules' };

/** The filter's parameters as lines "name = value". */
const paramsText = (p: Record<string, unknown> | undefined) => Object.entries(p ?? {}).map(([k, v]) => `${k} = ${String(v)}`).join('\n');
const paramsOf = (text: string): Record<string, string> => {
  const o: Record<string, string> = {};
  for (const line of text.split('\n')) {
    const at = line.indexOf('=');
    if (at <= 0) continue;
    const k = line.slice(0, at).trim(), v = line.slice(at + 1).trim();
    if (k) o[k] = v;
  }
  return o;
};
const list = (s: string) => s.split(',').map((x) => x.trim()).filter(Boolean);

export function BrowserSetsDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const sets = setsOf(nodes);
  const levels = storeys({ nodes, relations });
  const [current, setCurrent] = useState<string | null>(sets[0]?.id ?? null);
  const set = sets.find((s) => s.id === current) ?? null;
  const [name, setName] = useState('');
  const [filter, setFilter] = useState<SetFilter>({});
  const [others, setOthers] = useState('');
  const [scales, setScales] = useState('');
  const [params, setParams] = useState('');
  const close = () => useUi.setState({ dialog: null });

  // The editor takes the set chosen; what is typed is written by Apply.
  useEffect(() => {
    setName(set?.name ?? '');
    const f = set?.filter ?? {};
    setFilter(f);
    setOthers((f.disciplines ?? []).filter((d) => !DISCIPLINES.includes(d)).join(', '));
    setScales((f.scales ?? []).join(', '));
    setParams(paramsText(f.params));
  }, [set?.id, set?.name, set?.filter]);

  const patch = (p: Partial<SetFilter>) => setFilter({ ...filter, ...p });
  const toggleIn = (key: 'disciplines' | 'kinds' | 'storeys', value: string) => {
    const cur = filter[key] ?? [];
    patch({ [key]: cur.includes(value) ? cur.filter((x) => x !== value) : [...cur, value] });
  };
  const apply = () => {
    if (!set) return;
    const disciplines = [...(filter.disciplines ?? []).filter((d) => DISCIPLINES.includes(d)), ...list(others)];
    const f: SetFilter = { ...filter, disciplines, scales: list(scales), params: paramsOf(params) };
    void execute('browser.setSet', { id: set.id, name: name.trim() || set.name, filter: f });
  };
  const add = () => void execute('browser.setSet', { name: 'New set', filter: {} }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) setCurrent(id); });
  const remove = (s: BrowserSet) => void execute('browser.removeSet', { id: s.id }).then(() => { if (current === s.id) setCurrent(null); });
  const move = (s: BrowserSet, by: -1 | 1) => {
    const i = sets.findIndex((x) => x.id === s.id);
    const target = sets[i + by];
    if (!target) return;
    void execute('browser.moveSet', { id: s.id, before: by < 0 ? target.id : sets[i + 2]?.id ?? null });
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Organize the browser" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Organize the browser</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body sets">
          <div className="sets-list">
            <p className="muted small">A set takes the views and sheets its filter matches; one that matches none goes under Other.</p>
            {sets.map((s, i) => (
              <span key={s.id} className={`tree-row${s.id === current ? ' on' : ''}`}>
                <button type="button" className={`tree-item${s.id === current ? ' on' : ''}`} onClick={() => setCurrent(s.id)}>{s.name}</button>
                <button type="button" className="icon mini" title="Move up" disabled={i === 0} onClick={() => move(s, -1)}><ArrowUp size={11} /></button>
                <button type="button" className="icon mini" title="Move down" disabled={i === sets.length - 1} onClick={() => move(s, 1)}><ArrowDown size={11} /></button>
                <button type="button" className="icon mini" title="Remove the set" onClick={() => remove(s)}><Trash2 size={11} /></button>
              </span>
            ))}
            <button type="button" className="btn" onClick={add}><Plus size={13} /> New set</button>
          </div>
          {set && (
            <div className="sets-editor">
              <label className="field-line"><span className="eyebrow">Name</span><input value={name} onChange={(e) => setName(e.target.value)} /></label>
              <div className="field-line"><span className="eyebrow">Disciplines</span>
                <span className="checks">{DISCIPLINES.map((d) => <label key={d}><input type="checkbox" checked={(filter.disciplines ?? []).includes(d)} onChange={() => toggleIn('disciplines', d)} /> {d}</label>)}</span>
                <input value={others} placeholder="Other disciplines, comma-separated" onChange={(e) => setOthers(e.target.value)} />
              </div>
              <div className="field-line"><span className="eyebrow">Kinds</span>
                <span className="checks">{KINDS.map((k) => <label key={k}><input type="checkbox" checked={(filter.kinds ?? []).includes(k)} onChange={() => toggleIn('kinds', k)} /> {KIND_LABEL[k]}</label>)}</span>
              </div>
              <div className="field-line"><span className="eyebrow">Levels</span>
                <span className="checks">{levels.map((l) => <label key={l.id}><input type="checkbox" checked={(filter.storeys ?? []).includes(l.id)} onChange={() => toggleIn('storeys', l.id)} /> {l.name}</label>)}</span>
              </div>
              <label className="field-line"><span className="eyebrow">Scales (1:)</span><input value={scales} placeholder="100, 50" onChange={(e) => setScales(e.target.value)} /></label>
              <label className="field-line"><span className="eyebrow">Name contains</span><input value={filter.nameContains ?? ''} onChange={(e) => patch({ nameContains: e.target.value || undefined })} /></label>
              <label className="field-line"><span className="eyebrow">Parameters (name = value, one per line)</span><textarea rows={3} value={params} onChange={(e) => setParams(e.target.value)} /></label>
              <p className="muted small">Every field given must hold; a set with none takes everything.</p>
              <div className="row"><span className="grow" /><button type="button" className="btn primary" onClick={apply}>Apply</button></div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
