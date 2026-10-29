/**
 * Import IFC: what the file holds, for the user to keep only what they need
 * before anything is read in. Its elements by discipline (architecture,
 * structure, MEP, furniture and equipment, site, other), each discipline by
 * kind with how many there are; its storeys with how many elements each
 * carries; and how it comes in (recognised, as references, linked). A
 * structural engineer leaves MEP and the furniture out, or brings two storeys.
 */

import { useMemo, useState } from 'react';
import { ChevronDown, ChevronRight, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { choiceOf, chosenCount, runIfcImport, useIfcImport, type ImportMode, type SurveyGroup } from '../../engine/ifcImport';

const mb = (bytes: number) => (bytes >= 1 << 30 ? `${(bytes / (1 << 30)).toFixed(1)} GB` : `${Math.max(1, Math.round(bytes / (1 << 20)))} MB`);

/** A checkbox showing all, none or some of its rows. */
function Tri({ on, some, onChange, label }: { on: boolean; some: boolean; onChange: (on: boolean) => void; label: string }) {
  return (
    <input type="checkbox" aria-label={label} checked={on} ref={(el) => { if (el) el.indeterminate = some && !on; }}
      onChange={(e) => onChange(e.target.checked)} />
  );
}

export function ImportIfcDialog() {
  const survey = useIfcImport((s) => s.survey);
  const source = useIfcImport((s) => s.source);
  const [mode, setMode] = useState<ImportMode>(useIfcImport.getState().mode);
  const [offKinds, setOffKinds] = useState<Set<string>>(new Set());
  const [offStoreys, setOffStoreys] = useState<Set<string>>(new Set());
  const [open, setOpen] = useState<Set<string>>(new Set());
  const close = () => { useIfcImport.setState({ survey: null, source: null }); useUi.setState({ dialog: null }); };
  const count = useMemo(() => (survey ? chosenCount(survey, offKinds, offStoreys) : 0), [survey, offKinds, offStoreys]);
  if (!survey || !source) return null;

  const toggle = (set: Set<string>, keys: string[], on: boolean) => {
    const next = new Set(set);
    for (const k of keys) { if (on) next.delete(k); else next.add(k); }
    return next;
  };
  const groupState = (g: SurveyGroup) => {
    const off = g.kinds.filter((k) => offKinds.has(k.key)).length;
    return { on: off === 0, some: off < g.kinds.length };
  };
  const storeyKeys = [...survey.storeys.map((s) => s.id), ...(survey.noStorey ? [''] : [])];
  const nothing = count === 0 || offStoreys.size === storeyKeys.length || survey.groups.every((g) => g.kinds.every((k) => offKinds.has(k.key)));

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog import-ifc" role="dialog" aria-label="Import IFC" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Import IFC · <span data-no-i18n>{survey.name}</span></span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <section>
            <h4 className="eyebrow">What to bring</h4>
            <div className="row small">
              <button type="button" className="btn small" onClick={() => setOffKinds(new Set())}>All</button>
              <button type="button" className="btn small" onClick={() => setOffKinds(new Set(survey.groups.flatMap((g) => g.kinds.map((k) => k.key))))}>None</button>
            </div>
            <ul className="import-tree">
              {survey.groups.map((g) => {
                const st = groupState(g);
                const expanded = open.has(g.discipline);
                return (
                  <li key={g.discipline} data-discipline={g.discipline}>
                    <div className="import-row">
                      <button type="button" className="icon mini" title={expanded ? 'Fold' : 'Show its kinds'} onClick={() => setOpen(toggle(open, [g.discipline], expanded))}>
                        {expanded ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
                      </button>
                      <label>
                        <Tri on={st.on} some={st.some} label={g.label} onChange={(on) => setOffKinds(toggle(offKinds, g.kinds.map((k) => k.key), on))} />
                        <strong>{g.label}</strong>
                      </label>
                      <span className="grow" /><span className="num muted">{g.count}</span>
                    </div>
                    {expanded && (
                      <ul>
                        {g.kinds.map((k) => (
                          <li key={k.key} className="import-row">
                            <label>
                              <input type="checkbox" checked={!offKinds.has(k.key)} onChange={(e) => setOffKinds(toggle(offKinds, [k.key], e.target.checked))} />
                              <span data-no-i18n={k.category ? true : undefined}>{k.label}</span>
                            </label>
                            <span className="grow" /><span className="num muted">{k.count}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </li>
                );
              })}
            </ul>
          </section>
          <section>
            <h4 className="eyebrow">Storeys</h4>
            <div className="row small">
              <button type="button" className="btn small" onClick={() => setOffStoreys(new Set())}>All</button>
              <button type="button" className="btn small" onClick={() => setOffStoreys(new Set(storeyKeys))}>None</button>
            </div>
            <ul className="import-tree">
              {[...survey.storeys].reverse().map((s) => (
                <li key={s.id} className="import-row">
                  <label>
                    <input type="checkbox" checked={!offStoreys.has(s.id)} onChange={(e) => setOffStoreys(toggle(offStoreys, [s.id], e.target.checked))} />
                    <span data-no-i18n>{s.name}</span> <span className="muted small">{s.elevation >= 0 ? '+' : ''}{s.elevation.toFixed(2)}</span>
                  </label>
                  <span className="grow" /><span className="num muted">{s.count}</span>
                </li>
              ))}
              {survey.noStorey > 0 && (
                <li className="import-row">
                  <label>
                    <input type="checkbox" checked={!offStoreys.has('')} onChange={(e) => setOffStoreys(toggle(offStoreys, [''], e.target.checked))} />
                    <span>On no storey</span>
                  </label>
                  <span className="grow" /><span className="num muted">{survey.noStorey}</span>
                </li>
              )}
            </ul>
            <h4 className="eyebrow" style={{ marginTop: 14 }}>How</h4>
            <select value={mode} onChange={(e) => setMode(e.target.value as ImportMode)} aria-label="How it comes in">
              <option value="auto">Recognised as Lapin elements where it can, the rest as references</option>
              <option value="reference">Everything as references, as the file draws it</option>
              <option value="link">Linked, read-only (Manage → Links)</option>
            </select>
            <p className="muted small" style={{ marginTop: 14 }}>
              <span data-no-i18n>{mb(survey.size)} · {survey.schema}{survey.application ? ` · ${survey.application}` : ''}</span><br />
              {survey.products} elements in the file; about <strong className="import-count">{count}</strong> chosen.
            </p>
          </section>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={nothing}
            onClick={() => void runIfcImport({ ...choiceOf(survey, offKinds, offStoreys), mode })}>{mode === 'link' ? 'Link' : 'Import'}</button>
        </footer>
      </div>
    </div>
  );
}
