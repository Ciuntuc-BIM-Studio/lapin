/**
 * Import IFC or Fragments: what the file holds, for the user to keep only what they need before anything is read
 * in (docs/fragments.md, Exchange). Its elements by discipline (architecture, structure, MEP, furniture and
 * equipment, site, other), each discipline by kind with how many there are; its storeys with how many elements
 * each carries (SurveyChoice, the same for both formats and for an export). For IFC also: the 2D it carries (the
 * elements' own plan symbols, footprints and texts; the elements drawn only in 2D), each left out on request; its
 * property sets, the technical ones (Navisworks' transform, ids, geometry statistics) unticked; the import's rules
 * (a body the file repeats kept once, flat bodies, far elements, duplicates, ports and openings written as objects
 * left out, meshes simplified, numbers written as text read as numbers), kept in this browser as the user's general
 * rule; and how it comes in (recognised, as references, linked). A structural engineer leaves MEP and the furniture
 * out, or brings two storeys without the 2D. What the file places comes in as one group unless told otherwise.
 */

import { useMemo, useState } from 'react';
import { ChevronDown, ChevronRight, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { choiceOf, chosenCount, rememberRules, rememberedRules, rulesOf, runIfcImport, useIfcImport, type ImportMode, type RememberedRules } from '../../engine/ifcImport';
import { nothingChosen, SurveyChoice } from './SurveyChoice';

const mb = (bytes: number) => (bytes >= 1 << 30 ? `${(bytes / (1 << 30)).toFixed(1)} GB` : `${Math.max(1, Math.round(bytes / (1 << 20)))} MB`);

const toggle = (set: ReadonlySet<string>, keys: string[], on: boolean) => {
  const next = new Set(set);
  for (const k of keys) { if (on) next.delete(k); else next.add(k); }
  return next;
};

export function ImportIfcDialog() {
  const survey = useIfcImport((s) => s.survey);
  const source = useIfcImport((s) => s.source);
  const format = useIfcImport((s) => s.format);
  const [mode, setMode] = useState<ImportMode>(useIfcImport.getState().mode);
  const [offKinds, setOffKinds] = useState<Set<string>>(new Set());
  const [offStoreys, setOffStoreys] = useState<Set<string>>(new Set());
  const [plan2d, setPlan2d] = useState(true);
  const [only2d, setOnly2d] = useState(true);
  const [rules, setRulesState] = useState<RememberedRules>(rememberedRules);
  const setRules = (patch: Partial<RememberedRules>) => setRulesState((r) => ({ ...r, ...patch }));
  // The sets left out: the technical ones when the rule says so, then as the user ticks them.
  const [offSets, setOffSets] = useState<Set<string> | null>(null);
  const sets = survey?.sets ?? [];
  const skipped = offSets ?? new Set(rules.technical ? sets.filter((x) => x.technical).map((x) => x.name) : []);
  const [setsOpen, setSetsOpen] = useState(false);
  // What the file places comes in as one group named after it (docs/groups.md); a link is a unit of its own.
  const [group, setGroup] = useState(true);
  const close = () => { useIfcImport.setState({ survey: null, source: null }); useUi.setState({ dialog: null }); };
  const count = useMemo(() => (survey ? chosenCount(survey, offKinds, offStoreys) : 0), [survey, offKinds, offStoreys]);
  if (!survey || !source) return null;
  const frag = format === 'frag';
  const nothing = count === 0 || nothingChosen(survey, offKinds, offStoreys);
  const title = frag ? 'Import Fragments' : 'Import IFC';
  const run = () => {
    if (frag) { void runIfcImport({ ...choiceOf(survey, offKinds, offStoreys), mode: 'reference', group }); return; }
    rememberRules(rules);
    void runIfcImport({ ...choiceOf(survey, offKinds, offStoreys), mode, plan2d, only2d, rules: rulesOf(rules, [...skipped]), group });
  };

  const setsList = frag || sets.length === 0 ? null : (
      <div className="import-sets">
        <div className="import-row">
          <button type="button" className="icon mini" title={setsOpen ? 'Fold' : 'Show the sets'} onClick={() => setSetsOpen(!setsOpen)}>
            {setsOpen ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
          </button>
          <strong>Property sets</strong>
          <span className="grow" /><span className="num muted">{sets.length - skipped.size} / {sets.length}</span>
        </div>
        {setsOpen && (
          <>
            <div className="row small">
              <button type="button" className="btn small" onClick={() => setOffSets(new Set())}>All</button>
              <button type="button" className="btn small" onClick={() => setOffSets(new Set(sets.filter((x) => x.technical).map((x) => x.name)))}>Without the technical ones</button>
            </div>
            <ul className="import-tree">
              {sets.map((x) => (
                <li key={x.name} className="import-row" data-set={x.name}>
                  <label>
                    <input type="checkbox" checked={!skipped.has(x.name)} onChange={(e) => setOffSets(toggle(skipped, [x.name], e.target.checked))} />
                    <span data-no-i18n>{x.name}</span>{x.technical && <span className="muted small">technical</span>}
                  </label>
                  <span className="grow" /><span className="num muted">{x.count}</span>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
  );
  const after = (
    <>
      {!frag && (
        <>
        {survey.twoD && survey.twoD.elements + survey.twoD.only > 0 && (
          <>
            <h4 className="eyebrow" style={{ marginTop: 14 }}>Drawn in 2D</h4>
            <ul className="import-tree import-2d">
              {survey.twoD.elements > 0 && (
                <li className="import-row" title="Without it, a plan draws these elements by their cut">
                  <label><input type="checkbox" className="import-plan2d" checked={plan2d} onChange={(e) => setPlan2d(e.target.checked)} /> <span>The elements' own 2D: plan symbols, footprints, texts</span></label>
                  <span className="grow" /><span className="num muted">{survey.twoD.elements}</span>
                </li>
              )}
              {survey.twoD.only > 0 && (
                <li className="import-row">
                  <label><input type="checkbox" className="import-only2d" checked={only2d} onChange={(e) => setOnly2d(e.target.checked)} /> <span>Elements drawn only in 2D: lines, texts, symbols</span></label>
                  <span className="grow" /><span className="num muted">{survey.twoD.only}</span>
                </li>
              )}
            </ul>
          </>
        )}
        <h4 className="eyebrow" style={{ marginTop: 14 }}>Clean-up and optimisation</h4>
        <ul className="import-tree import-rules">
          <li className="import-row" title="The same triangles the file repeats under other names, kept once: its elements share them, nothing lost">
            <label><input type="checkbox" data-rule="dedupe" checked={rules.dedupe} onChange={(e) => setRules({ dedupe: e.target.checked })} /> <span>Keep a repeated body once (no loss)</span></label>
          </li>
          <li className="import-row" title="Texts, lines and hatches another program turned into triangles: bodies that lie in one plane">
            <label><input type="checkbox" data-rule="flat" checked={rules.flat} onChange={(e) => setRules({ flat: e.target.checked })} /> <span>Leave out flat bodies: texts, lines, hatches</span></label>
          </li>
          <li className="import-row" title="A drawing's texts or a title block placed away from the model">
            <label><input type="checkbox" data-rule="far" checked={rules.farOn} onChange={(e) => setRules({ farOn: e.target.checked })} /> <span>Leave out elements further from the rest than (m)</span></label>
            <input className="import-rule-value" inputMode="decimal" aria-label="Distance (m)" value={rules.far} disabled={!rules.farOn} onChange={(e) => setRules({ far: e.target.value })} />
          </li>
          <li className="import-row" title="An element with the same body in the same place as one already brought">
            <label><input type="checkbox" data-rule="duplicates" checked={rules.duplicates} onChange={(e) => setRules({ duplicates: e.target.checked })} /> <span>Leave out duplicates in the same place</span></label>
          </li>
          <li className="import-row" title="Ports, openings and virtual elements another program wrote as objects (by their name or kind)">
            <label><input type="checkbox" data-rule="pseudo" checked={rules.pseudo} onChange={(e) => setRules({ pseudo: e.target.checked })} /> <span>Leave out ports and openings written as objects</span></label>
          </li>
          <li className="import-row" title="Fewer triangles where a body is a mesh, never further from it than the tolerance">
            <label><input type="checkbox" data-rule="simplify" checked={rules.simplifyOn} onChange={(e) => setRules({ simplifyOn: e.target.checked })} /> <span>Simplify meshes within (mm)</span></label>
            <input className="import-rule-value" inputMode="decimal" aria-label="Tolerance (mm)" value={rules.simplify} disabled={!rules.simplifyOn} onChange={(e) => setRules({ simplify: e.target.value })} />
          </li>
          <li className="import-row" title="DoubleLength:0,01 → 0.01 m; 1.15395 kg; +106.059. A whole number alone (a code) stays text.">
            <label><input type="checkbox" data-rule="numbers" checked={rules.numbers} onChange={(e) => setRules({ numbers: e.target.checked })} /> <span>Read numbers written as text</span></label>
          </li>
          <li className="import-row" title="Navisworks' transform, internal ids, entity handles and geometry statistics: how another program held the element, not what it is">
            <label><input type="checkbox" data-rule="technical" checked={rules.technical} onChange={(e) => { setRules({ technical: e.target.checked }); setOffSets(null); }} /> <span>Leave out technical property sets</span></label>
          </li>
        </ul>
        <h4 className="eyebrow" style={{ marginTop: 14 }}>How</h4>
        <select value={mode} onChange={(e) => setMode(e.target.value as ImportMode)} aria-label="How it comes in">
          <option value="auto">Recognised as Lapin elements where it can, the rest as references</option>
          <option value="reference">Everything as references, as the file draws it</option>
          <option value="link">Linked, read-only (Manage → Links)</option>
        </select>
        </>
      )}
      {frag && <p className="muted small" style={{ marginTop: 14 }}>A Fragments file holds triangles: its elements come in as references, drawn from their bodies, with their attributes.</p>}
      {(frag || mode !== 'link') && (
        <label className="field-line check-line" style={{ marginTop: 8 }}>
          <input type="checkbox" checked={group} onChange={(e) => setGroup(e.target.checked)} /> As one group, named after the file (double-click it to edit)
        </label>
      )}
      <p className="muted small" style={{ marginTop: 14 }}>
        <span data-no-i18n>{mb(survey.size)} · {survey.schema}{survey.application ? ` · ${survey.application}` : ''}</span><br />
        {survey.products} elements in the file; about <strong className="import-count">{count}</strong> chosen.
      </p>
    </>
  );

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog import-ifc" role="dialog" aria-label={title} onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">{title} · <span data-no-i18n>{survey.name}</span></span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <SurveyChoice survey={survey} whatLabel="What to bring" offKinds={offKinds} setOffKinds={setOffKinds} offStoreys={offStoreys} setOffStoreys={setOffStoreys}
            kindsAfter={setsList} storeysAfter={after} />
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={nothing} onClick={run}>{!frag && mode === 'link' ? 'Link' : 'Import'}</button>
        </footer>
      </div>
    </div>
  );
}
