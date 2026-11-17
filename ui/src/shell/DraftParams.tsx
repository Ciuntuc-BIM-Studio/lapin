/**
 * A new type's parameters, chosen before the type is made (issue #32): the rows of a type's Parameters section (a name, a value as
 * its kind, Instance default), held by the dialog until Create type sends them with the type in one step. Once the type exists, its
 * parameters are edited in the type's own section (ParamsSection), as in the properties panel.
 */

import { useState } from 'react';
import { Plus, X } from 'lucide-react';
import { Cell, LengthCell } from './cells';
import { parseNumber } from './cellKeys';
import { promptText } from './prompts';
import { paramKey, type Param } from './params';
import { SUGGESTED, useStandardProps } from './ParamsSection';
import { addDraft, patchDraft } from './typeParamDraft';

const KINDS = ['text', 'number', 'length', 'area', 'angle', 'bool', 'choice'] as const;

export function DraftParams({ typeClass, params, onChange }: { typeClass: string; params: Param[]; onChange: (list: Param[]) => void }) {
  const [name, setName] = useState('');
  const [kind, setKind] = useState<string>('text');
  const standard = useStandardProps(typeClass).filter(([set, n]) => !params.some((p) => p.pset === set && p.name === n));
  const set = (p: Param, patch: Partial<Param>) => onChange(patchDraft(params, paramKey(p), patch));
  const add = () => {
    if (!name.trim()) return;
    const options = kind === 'choice' ? (promptText('The choices, separated by commas') ?? '').split(',').map((x) => x.trim()).filter(Boolean) : undefined;
    onChange(addDraft(params, name, kind, undefined, options));
    setName('');
  };
  const addStandard = (key: string) => {
    const hit = standard.find(([s, n]) => `${s}.${n}` === key);
    if (hit) onChange(addDraft(params, hit[1], hit[2], hit[0]));
  };
  return (
    <div className="draft-params" data-testid="draft-params">
      {params.length > 0 && (
        <dl className="fields">
          {params.map((p) => (
            <span key={paramKey(p)} className="qrow">
              <dt title={p.pset ? `${p.pset} · ${p.kind}` : p.kind}>{p.pset && <span className="muted small" data-no-i18n>{p.pset} · </span>}<span data-no-i18n>{p.name}</span>
                <button type="button" className="icon mini row-x" title="Remove the parameter" onClick={() => onChange(params.filter((x) => x !== p))}><X size={10} /></button></dt>
              <dd>
                {p.kind === 'length' ? <LengthCell metres={p.value as number} onCommit={(m) => set(p, { value: m })} />
                  : p.kind === 'bool' ? <input type="checkbox" checked={p.value === true} data-draft-param={p.name} onChange={(e) => set(p, { value: e.target.checked })} />
                  : p.kind === 'choice' ? <select className="cell" value={String(p.value ?? '')} onChange={(e) => set(p, { value: e.target.value })}>{(p.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}</select>
                  : p.kind === 'text' ? <Cell value={String(p.value ?? '')} onCommit={(t) => set(p, { value: t })} />
                  : <Cell className="num" value={p.value == null ? '' : String(p.value)} suffix={p.kind === 'area' ? 'm²' : p.kind === 'angle' ? '°' : undefined}
                      onCommit={(t) => { const n = parseNumber(t); if (n != null) set(p, { value: n }); }} />}
              </dd>
              <label className="small muted param-instance" title="An instance default: each element given this type gets a copy it may change (Renovation status, a room's use), and the type's IFC property sets leave it out. Unticked: a property of the type itself (FireRating), shared by every element.">
                <input type="checkbox" checked={p.instance === true} onChange={(e) => set(p, { instance: e.target.checked })} /> Instance default
              </label>
            </span>
          ))}
        </dl>
      )}
      <div className="param-add">
        <input placeholder="New parameter" value={name} list="draft-param-suggestions" onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') add(); }} />
        <datalist id="draft-param-suggestions">{SUGGESTED.map((x) => <option key={x} value={x} />)}</datalist>
        <select value={kind} onChange={(e) => setKind(e.target.value)} title="Kind">{KINDS.map((k) => <option key={k} value={k}>{k}</option>)}</select>
        <button type="button" className="icon" title="Add the parameter" onClick={add} disabled={!name.trim()}><Plus size={14} /></button>
      </div>
      {standard.length > 0 && (
        <div className="param-add">
          <select value="" data-draft-param-standard title="A property of IFC's standard set for this class (FireRating, AcousticRating, ThermalTransmittance…): exported in that set, and a tag can show it" onChange={(e) => addStandard(e.target.value)}>
            <option value="">Add a standard property…</option>
            {standard.map(([s, n]) => <option key={`${s}.${n}`} value={`${s}.${n}`} data-no-i18n>{`${s} · ${n}`}</option>)}
          </select>
        </div>
      )}
    </div>
  );
}
