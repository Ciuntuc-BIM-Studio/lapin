/**
 * The user's own parameters of an element or a type (a name, a kind, a value; exported to IFC as Lapin_Parameters or the
 * standard set named): in the properties panel, and in a type's configurator (the doors and windows', decision 251).
 */

import { useEffect, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { Cell, LengthCell } from './cells';
import { parseNumber } from './cellKeys';
import { execute, query } from '../engine/session';
import { paramKey, sameKind, type Param } from './params';
import type { NodeDto } from '../engine/protocol';

// ─── The user's own parameters ──────────────────────────────────────────────

const KINDS = ['text', 'number', 'length', 'area', 'angle', 'bool', 'choice'] as const;

/** Parameters of one's own a project often wants (report #64): offered as the name is typed. */
export const SUGGESTED = ['Fire rating', 'Sound rating', 'Moisture resistance', 'Thermal resistance'];

interface StandardTable { sets: { name: string; classes: string[]; properties: Record<string, string> }[] }
let table: Promise<StandardTable | null> | null = null;
/** The standard sets' properties for an entity of `cls` (an element's, or its type's: IfcWallType takes Pset_WallCommon), as [set, name, kind]. */
export function useStandardProps(cls: string): [string, string, string][] {
  const [list, setList] = useState<[string, string, string][]>([]);
  useEffect(() => {
    let live = true;
    void (table ??= query<StandardTable>('params.standardSets')).then((t) => {
      if (!live || !t) return;
      setList(t.sets.filter((s) => s.classes.some((c) => c === cls || `${c}Type` === cls))
        .flatMap((s) => Object.entries(s.properties).map(([n, k]) => [s.name, n, k] as [string, string, string])));
    });
    return () => { live = false; };
  }, [cls]);
  return list;
}

/**
 * The user's parameters of `node` (an element, or the type it shows the
 * parameters of: `ids` then names who is written). Parameters the element
 * has of its own shadow its type's of the same name.
 */
export function ParamsSection({ node, ids: who, shadowed }: { node: NodeDto; ids?: () => string[]; shadowed?: Set<string> }) {
  const params = (Array.isArray(node.props.params) ? node.props.params : []) as unknown as Param[];
  const [name, setName] = useState('');
  const [kind, setKind] = useState<string>('text');
  const ids = who ?? (() => sameKind(node));
  const set = (p: Param, value: unknown) => void execute('element.setParam', { ids: ids(), name: p.name, value, ...(p.pset ? { pset: p.pset } : {}) });
  const add = () => {
    const n = name.trim();
    if (!n) return;
    const options = kind === 'choice' ? (window.prompt('The choices, separated by commas') ?? '').split(',').map((x) => x.trim()).filter(Boolean) : undefined;
    void execute('element.setParam', { ids: ids(), name: n, kind, value: kind === 'bool' ? false : kind === 'text' || kind === 'choice' ? (options?.[0] ?? '') : 0, ...(options ? { options } : {}) }).then(() => setName(''));
  };
  const standard = useStandardProps(node.type).filter(([set, n]) => !params.some((p) => p.pset === set && p.name === n));
  // A standard property: added empty in its own set, as its kind (text, yes/no, a number, a length…); filled in its row.
  const addStandard = (key: string) => {
    const hit = standard.find(([set, n]) => `${set}.${n}` === key);
    if (!hit) return;
    const [set, n, k] = hit;
    void execute('element.setParam', { ids: ids(), pset: set, name: n, value: k === 'bool' ? false : k === 'text' ? '' : 0 });
  };
  return (
    <>
      {params.length > 0 && (
        <dl className="fields">
          {params.map((p) => (
            <span key={paramKey(p)} className="qrow">
              <dt title={`${p.pset ? `${p.pset} · ` : ''}${shadowed?.has(paramKey(p)) ? `${p.kind} · this element has its own value` : p.kind}`} className={shadowed?.has(paramKey(p)) ? 'shadowed' : undefined}>{p.pset && <span className="muted small" data-no-i18n>{p.pset} · </span>}{p.name}<button type="button" className="icon mini row-x" title="Remove the parameter" onClick={() => void execute('element.removeParam', { ids: ids(), name: p.name, ...(p.pset ? { pset: p.pset } : {}) })}><X size={10} /></button></dt>
              <dd>
                {p.kind === 'length' ? <LengthCell metres={p.value as number} onCommit={(m) => set(p, m)} />
                  : p.kind === 'bool' ? <input type="checkbox" checked={p.value === true} onChange={(e) => set(p, e.target.checked)} />
                  : p.kind === 'choice' ? <select className="cell" value={String(p.value ?? '')} onChange={(e) => set(p, e.target.value)}>{(p.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}</select>
                  : p.kind === 'text' ? <Cell value={String(p.value ?? '')} onCommit={(t) => set(p, t)} />
                  : <Cell className="num" value={p.value == null ? '' : String(p.value)} suffix={p.kind === 'area' ? 'm²' : p.kind === 'angle' ? '°' : undefined}
                      onCommit={(t) => { const n = parseNumber(t); if (n != null) set(p, n); }} />}
              </dd>
            </span>
          ))}
        </dl>
      )}
      <div className="param-add">
        <input placeholder="New parameter" value={name} list="param-suggestions" onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') add(); }} />
        <datalist id="param-suggestions">{SUGGESTED.map((x) => <option key={x} value={x} />)}</datalist>
        <select value={kind} onChange={(e) => setKind(e.target.value)} title="Kind">{KINDS.map((k) => <option key={k} value={k}>{k}</option>)}</select>
        <button type="button" className="icon" title="Add the parameter" onClick={add} disabled={!name.trim()}><Plus size={14} /></button>
      </div>
      {standard.length > 0 && (
        <div className="param-add">
          <select value="" data-param-standard title="A property of IFC's standard set for this class (FireRating, AcousticRating, ThermalTransmittance…): exported in that set, and a tag can show it" onChange={(e) => addStandard(e.target.value)}>
            <option value="">Add a standard property…</option>
            {standard.map(([set, n]) => <option key={`${set}.${n}`} value={`${set}.${n}`} data-no-i18n>{`${set} · ${n}`}</option>)}
          </select>
        </div>
      )}
    </>
  );
}
