/**
 * Manage → Rules… (docs/plan-user-extensibility.md §5.4): the rule packs the configurators read — stair limits, reinforcement steels,
 * diameters, mandrels and hook tails, the roofs' least pitches. Lapin's own (marked indicative, read only: Duplicate makes a copy to
 * change), the project's own, the one in use; each section an editable table; .lapinrules files in and out; the project checked
 * against its rules; the rules copied into the stair types. Nothing is refused because of a rule: the tools propose and warn.
 * Every value is indicative and the user's to check against the norm in force.
 */

import { useCallback, useEffect, useState } from 'react';
import { Check, Copy, FileDown, FileUp, ListChecks, Plus, Stamp, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { execute, useSession } from '../../../engine/session';
import { LengthField, NumberField } from '../../../shell/cells';
import { ask, runOk as run } from '../LibraryChoices';
import { PendingDialog } from '../PendingDialog';
import {
  BUILTIN_SETS, STAIR_FIELDS, addStairSet, addStep, diametersText, findingsByModule, parseDiameters, removeStairSet, removeStep, removeSteel, addSteel,
  setPitch, setStairValue, setStep, stairProblem, stepsProblem, type Finding, type RulePack, type RulesList, type Section, type Step,
} from './rulesModel';

const SECTION_LABELS: Record<string, string> = { stairs: 'Stairs', rebar: 'Reinforcement', roofs: 'Roofs' };

export function RulesDialog() {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<RulesList | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [pack, setPack] = useState<RulePack | null>(null);
  const [section, setSection] = useState<string>('stairs');
  const [findings, setFindings] = useState<{ pack: string; findings: Finding[] } | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    let live = true;
    void ask<RulesList>('rules.list', {}).then((r) => { if (live && r) { setList(r); setSelected((s) => (s && r.items.some((i) => i.id === s) ? s : r.active)); } });
    return () => { live = false; };
  }, [version]);
  useEffect(() => {
    if (!selected) return;
    let live = true;
    void ask<RulePack>('rules.get', { id: selected }).then((r) => { if (live && r) setPack(r); });
    return () => { live = false; };
  }, [selected, version]);

  const item = list?.items.find((i) => i.id === selected) ?? null;
  const sections = list?.sections ?? [];
  useEffect(() => { if (sections.length && !sections.some((s) => s.key === section)) setSection(sections[0].key); }, [sections.map((s) => s.key).join(',')]);

  const update = useCallback(async (key: string, values: Section) => {
    if (!item || item.builtin) return;
    setProblem(null);
    const r = await execute2('rules.update', { id: item.id, section: key, values });
    if (r === false) setProblem(useSession.getState().error);
  }, [item?.id, item?.builtin]);

  if (!list || !pack || !item) return <PendingDialog title="Rules" />;
  const readOnly = item.builtin;

  const duplicate = async () => {
    const r = await run<{ id: string }>('rules.create', { from: item.id, name: item.builtin ? `${item.name} copy` : `${item.name} copy` });
    if (r) setSelected(r.id);
  };
  const exportPack = async () => {
    const r = await ask<{ text: string; file: string }>('rules.export', { id: item.id });
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'rules.lapinrules'), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importPack = async () => {
    const f = await pickFile('Rule pack (.lapinrules)', 'lapinrules');
    if (!f) return;
    const r = await run<{ id: string }>('rules.import', { text: new TextDecoder().decode(f.bytes) });
    if (r) setSelected(r.id);
  };
  const check = async () => { const r = await ask<{ pack: string; findings: Finding[] }>('rules.check', {}); if (r) setFindings(r); };
  const applyStairs = async () => {
    const r = await run<{ changed: number }>('architecture.applyStairRules', {});
    if (r) notify(r.changed === 0 ? 'Every stair type already follows the active rules.' : `${r.changed} stair types now follow the active rules.`);
  };
  const remove = async () => { if (item.builtin) return; if (await run('rules.delete', { id: item.id }) !== null) setSelected(null); };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates rules-dialog" role="dialog" aria-label="Rules" data-dialog="rules" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Rules</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" data-field="rules-import" onClick={() => void importPack()} title="A .lapinrules file: it comes in as a pack of the project's"><FileUp size={13} />Import…</button>
            </div>
            <ul className="doc-templates">
              {list.items.map((i) => (
                <li key={i.id} className={i.id === selected ? 'active' : undefined}>
                  <button type="button" data-rules-pack={i.id} onClick={() => { setSelected(i.id); setFindings(null); setProblem(null); }}>
                    <span className="sy-item-text"><strong data-no-i18n>{i.name}</strong>
                      <span className="muted small">{i.builtin ? 'Lapin, indicative' : 'This project'}{i.active ? ' · in use' : ''}</span></span>
                  </button>
                </li>
              ))}
            </ul>
          </aside>
          <main className="tpl-detail">
            <p className="muted small rules-note" data-testid="rules-note">Every value here is indicative and yours to check against the norm in force: Lapin's tools propose and warn from these rules, they never refuse.</p>
            {readOnly
              ? <h3 className="tpl-title" data-no-i18n>{item.name}</h3>
              : <label className="field-line"><span className="eyebrow">Name</span>
                <input key={item.id + item.name} data-field="rules-name" defaultValue={item.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== item.name) void execute2('rules.update', { id: item.id, name: v }); }} /></label>}
            {!readOnly && <label className="field-line"><span className="eyebrow">Description</span>
              <input key={item.id + (item.description ?? '')} data-field="rules-description" defaultValue={item.description ?? ''} onBlur={(e) => { const v = e.target.value.trim(); if (v !== (item.description ?? '')) void execute2('rules.update', { id: item.id, description: v }); }} /></label>}
            <div className="row wrap">
              {!item.active && <button type="button" className="btn primary" data-field="rules-use" title="The project's configurators read this pack" onClick={() => void run('rules.setActive', { id: item.id })}><Check size={13} />Use in this project</button>}
              {item.active && <span className="chip">In use</span>}
              <button type="button" className="btn" data-field="rules-duplicate" title={readOnly ? "A copy in the project, to change" : 'A copy of this pack'} onClick={() => void duplicate()}><Copy size={13} />Duplicate</button>
              <button type="button" className="btn" data-field="rules-export" onClick={() => void exportPack()}><FileDown size={13} />Export…</button>
              {!readOnly && <button type="button" className="btn danger" data-field="rules-delete" onClick={() => void remove()}><Trash2 size={13} />Delete</button>}
              <span className="grow" />
              <button type="button" className="btn" data-field="rules-check" title="Lists what the project has that differs from the rules in use" onClick={() => void check()}><ListChecks size={13} />Check the project</button>
              <button type="button" className="btn" data-field="rules-apply-stairs" title="Copies the rules in use into the stair types (each keeps its set); one undo step" onClick={() => void applyStairs()}><Stamp size={13} />Apply to stair types</button>
            </div>
            {readOnly && <p className="muted small">Lapin's rules stay as they are: Duplicate them to change the values.</p>}
            {findings && <FindingsList f={findings} />}
            <div className="tabs-inline">
              {sections.map((s) => <button key={s.key} type="button" className={`tab-chip${section === s.key ? ' on' : ''}`} data-rules-section={s.key} onClick={() => setSection(s.key)}>{SECTION_LABELS[s.key] ?? s.label}</button>)}
            </div>
            {problem && <p className="error-text" role="alert">{problem}</p>}
            {section === 'stairs' && pack.sections.stairs && <StairsEditor values={pack.sections.stairs} readOnly={readOnly} onChange={(v) => void update('stairs', v)} setProblem={setProblem} />}
            {section === 'rebar' && pack.sections.rebar && <RebarEditor values={pack.sections.rebar} readOnly={readOnly} onChange={(v) => void update('rebar', v)} setProblem={setProblem} />}
            {section === 'roofs' && pack.sections.roofs && <RoofsEditor values={pack.sections.roofs} readOnly={readOnly} onChange={(v) => void update('roofs', v)} />}
            {!['stairs', 'rebar', 'roofs'].includes(section) && <p className="muted small">This section has no table here: it comes and goes with the pack's file.</p>}
          </main>
        </div>
      </div>
    </div>
  );
}

/** A command whose refusal the caller reads: false when the engine refused. */
async function execute2(command: string, args: Record<string, unknown>): Promise<boolean> {
  const r = await execute(command, args);
  return !(r == null && useSession.getState().error);
}

function FindingsList({ f }: { f: { pack: string; findings: Finding[] } }) {
  const nodes = useSession((s) => s.nodes);
  return (
    <div className="rules-findings" data-testid="rules-findings">
      <h4 className="eyebrow">The project against <span data-no-i18n>{f.pack}</span></h4>
      {f.findings.length === 0 ? <p className="small">Nothing differs from the rules in use.</p> : findingsByModule(f.findings).map(([module, list]) => (
        <div key={module}>
          <h5 className="muted small" data-no-i18n>{module}</h5>
          <ul className="rules-finding-list">
            {list.map((x, i) => (
              <li key={i} className={`finding ${x.severity}`}>
                {x.element && nodes.has(x.element)
                  ? <button type="button" className="link-button" data-finding={x.element} title="Select it" onClick={() => useUi.setState({ selected: [x.element!] })} data-no-i18n>{x.message}</button>
                  : <span data-no-i18n>{x.message}</span>}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

// ─── Stairs ──────────────────────────────────────────────────────────────────

function StairsEditor({ values, readOnly, onChange, setProblem }: { values: Section; readOnly: boolean; onChange: (s: Section) => void; setProblem: (p: string | null) => void }) {
  const sets = Object.keys(values);
  const [name, setName] = useState('');
  const put = (set: string, key: string, v: number) => {
    const next = setStairValue(values, set, key, v);
    const p = stairProblem(next[set] as Record<string, unknown>);
    if (p) { setProblem(`${set}: ${p}.`); return; }
    onChange(next);
  };
  return (
    <div data-testid="rules-stairs">
      <table className="layer-table rules-table">
        <thead><tr><th>Stair set</th>{sets.map((s) => <th key={s}><span data-no-i18n>{s}</span>{!readOnly && !BUILTIN_SETS.includes(s) && <button type="button" className="icon mini" title="Remove this set" onClick={() => onChange(removeStairSet(values, s))}><X size={10} /></button>}</th>)}</tr></thead>
        <tbody>
          {STAIR_FIELDS.map((f) => (
            <tr key={f.key}>
              <td>{f.label}</td>
              {sets.map((s) => {
                const v = Number((values[s] as Record<string, unknown>)[f.key]);
                return (
                  <td key={s} data-rule={`${s}.${f.key}`}>
                    {f.kind === 'length' ? <LengthField metres={v} readOnly={readOnly} onCommit={(m) => { if (m != null) put(s, f.key, m); }} />
                      : f.kind === 'slope' ? <NumberField value={Math.round(v * 1000) / 10} suffix="%" readOnly={readOnly} min={0} max={50} onCommit={(n) => { if (n != null) put(s, f.key, n / 100); }} />
                      : <NumberField value={v} integer readOnly={readOnly} min={1} max={60} onCommit={(n) => { if (n != null) put(s, f.key, n); }} />}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
      {!readOnly && (
        <div className="row">
          <input placeholder="New set's name" value={name} data-field="rules-new-set" onChange={(e) => setName(e.target.value)} />
          <button type="button" className="btn small" disabled={!name.trim()} data-field="rules-add-set" title="A set of your own, started from the residential one" onClick={() => { const n = addStairSet(values, name, 'residential'); if (n) { onChange(n); setName(''); } else setProblem('That set exists already, or its name is too long.'); }}><Plus size={12} />Set</button>
        </div>
      )}
      <p className="muted small">The set a stair type takes is chosen in its Rules tab; a new type starts from these values. A stair that differs is warned of, never refused.</p>
    </div>
  );
}

// ─── Reinforcement ───────────────────────────────────────────────────────────

function StepsTable({ steps, what, upLabel, unit, by, readOnly, onChange }: { steps: Step[]; what: string; upLabel: string; unit: string; by: number; readOnly: boolean; onChange: (s: Step[]) => void }) {
  return (
    <table className="layer-table rules-table" data-testid={`rules-${what}`}>
      <thead><tr><th>{upLabel}</th><th>Factor of Ø</th><th /></tr></thead>
      <tbody>
        {steps.map((s, i) => (
          <tr key={i}>
            <td>{i < steps.length - 1 ? <NumberField value={s.upTo} suffix={unit} readOnly={readOnly} min={1} onCommit={(n) => { if (n != null) onChange(setStep(steps, i, { upTo: n })); }} /> : <span className="muted small">beyond</span>}</td>
            <td><NumberField value={s.factor} readOnly={readOnly} min={0.5} max={50} onCommit={(n) => { if (n != null) onChange(setStep(steps, i, { factor: n })); }} /></td>
            <td>{!readOnly && steps.length > 1 && <button type="button" className="icon mini" title="Remove this step" onClick={() => onChange(removeStep(steps, i))}><X size={11} /></button>}</td>
          </tr>
        ))}
      </tbody>
      {!readOnly && <tfoot><tr><td colSpan={3}><button type="button" className="btn small" onClick={() => onChange(addStep(steps, by))}><Plus size={12} />Step</button></td></tr></tfoot>}
    </table>
  );
}

function RebarEditor({ values, readOnly, onChange, setProblem }: { values: Section; readOnly: boolean; onChange: (s: Section) => void; setProblem: (p: string | null) => void }) {
  const steels = (values.steels as string[]) ?? [];
  const mandrel = (values.mandrel as Step[]) ?? [];
  const hooks = (values.hooks as Step[]) ?? [];
  const [steel, setSteel] = useState('');
  const steps = (key: 'mandrel' | 'hooks', next: Step[]) => {
    const p = stepsProblem(next, key === 'mandrel' ? 'mandrel' : "hooks' tails");
    if (p) { setProblem(`${p}.`); return; }
    onChange({ ...values, [key]: next });
  };
  return (
    <div data-testid="rules-rebar">
      <h4 className="eyebrow">Steels offered</h4>
      <div className="row wrap">
        {steels.map((s) => <span key={s} className="chip"><span data-no-i18n>{s}</span>{!readOnly && steels.length > 1 && <button type="button" className="icon mini" title="Take this steel off the list" onClick={() => onChange(removeSteel(values, s))}><X size={10} /></button>}</span>)}
        {!readOnly && <>
          <input placeholder="New steel" value={steel} data-field="rules-new-steel" onChange={(e) => setSteel(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && steel.trim()) { onChange({ ...values, steels: addSteel(steels, steel) }); setSteel(''); } }} />
          <button type="button" className="icon" title="Add the steel" disabled={!steel.trim()} onClick={() => { onChange({ ...values, steels: addSteel(steels, steel) }); setSteel(''); }}><Plus size={13} /></button>
        </>}
      </div>
      <label className="field-line"><span className="eyebrow">Steel of a new bar</span>
        <select value={String(values.defaultSteel ?? '')} disabled={readOnly} data-field="rules-default-steel" onChange={(e) => onChange({ ...values, defaultSteel: e.target.value })}>{steels.map((s) => <option key={s} value={s}>{s}</option>)}</select>
      </label>
      <label className="field-line"><span className="eyebrow">Diameters offered, mm</span>
        <input key={diametersText((values.diameters as number[]) ?? [])} defaultValue={diametersText((values.diameters as number[]) ?? [])} readOnly={readOnly} data-field="rules-diameters"
          onBlur={(e) => { const d = parseDiameters(e.target.value); if (!d) { setProblem('Each diameter offered is 4 to 50 mm, separated by commas.'); return; } if (d.join() !== ((values.diameters as number[]) ?? []).join()) onChange({ ...values, diameters: d }); }} />
      </label>
      <h4 className="eyebrow">Mandrel (bend diameter, a factor of Ø, by the bar's diameter)</h4>
      <StepsTable steps={mandrel} what="mandrel" upLabel="Up to Ø, mm" unit="mm" by={4} readOnly={readOnly} onChange={(s) => steps('mandrel', s)} />
      <h4 className="eyebrow">Hooks (the straight tail, a factor of Ø, by the hook's angle)</h4>
      <StepsTable steps={hooks} what="hooks" upLabel="Up to angle" unit="°" by={45} readOnly={readOnly} onChange={(s) => steps('hooks', s)} />
      <label className="field-line"><span className="eyebrow">Least hook tail</span>
        <NumberField value={Number(values.minHook ?? 0)} suffix="mm" readOnly={readOnly} min={0} max={500} onCommit={(n) => { if (n != null) onChange({ ...values, minHook: n }); }} />
      </label>
      <p className="muted small">New bars take the steel and the hooks' tails from here, and the bend radius when the mandrel differs from Lapin's. Bars of a steel or a diameter not offered are listed by the check, not refused.</p>
    </div>
  );
}

// ─── Roofs ───────────────────────────────────────────────────────────────────

function RoofsEditor({ values, readOnly, onChange }: { values: Section; readOnly: boolean; onChange: (s: Section) => void }) {
  const pitches = (values.minPitch ?? {}) as Record<string, number>;
  return (
    <div data-testid="rules-roofs">
      <table className="layer-table rules-table">
        <thead><tr><th>Covering</th><th>Least pitch</th></tr></thead>
        <tbody>
          {Object.entries(pitches).map(([c, v]) => (
            <tr key={c}><td data-no-i18n>{c}</td><td data-rule={`roofs.${c}`}><NumberField value={v} suffix="°" readOnly={readOnly} min={0} max={89} onCommit={(n) => { if (n != null) onChange(setPitch(values, c, n)); }} /></td></tr>
          ))}
        </tbody>
      </table>
      <p className="muted small">A roof pitched under its covering's least pitch is warned of, as it may not shed water.</p>
    </div>
  );
}
