/**
 * Manage → Rules… (docs/plan-user-extensibility.md §5.4): the rule packs the configurators read — stair limits, reinforcement steels,
 * diameters, mandrels and hook tails, the roofs' least pitches. Lapin's own (marked indicative, read only: Duplicate makes a copy to
 * change), the project's own, the one in use; each section an editable table; .lapinrules files in and out; the project checked
 * against its rules; the rules copied into the stair types. Nothing is refused because of a rule: the tools propose and warn.
 * Every value is indicative and the user's to check against the norm in force.
 */

import { backdropClose } from '../../../shell/Dialog';
import { useCallback, useEffect, useState } from 'react';
import { Check, Copy, FileDown, FileUp, ListChecks, Plus, Stamp, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { execute, useSession } from '../../../engine/session';
import { LengthField, NumberField } from '../../../shell/cells';
import { ask, runOk as run } from '../LibraryChoices';
import { FirmFolderFiles } from '../FirmFolderFiles';
import { PendingDialog } from '../PendingDialog';
import { importLibraryText } from '../../../platform/ImportPreview';
import {
  BUILTIN_LOAD_SETS, LOAD_SET_FIELDS, addLoadSet, addSnowZone, languageFrom, loadSetProblem, removeLoadSet, removeSafetyClass, removeSnowZone, setLoadSet, setPsi,
  setSafetyClass, setSnowZone, sourcesFrom, type LoadSet, type LoadsSection,
} from './rulesModel';
import {
  BUILTIN_SETS, STAIR_FIELDS, addGrade, removeGrade, renameGrade, addStairSet, addStep, diametersText, findingsByModule, parseDiameters, removeStairSet, removeStep, removeSteel, addSteel,
  setPitch, setStairValue, setStep, stairProblem, stepsProblem, type Finding, type RulePack, type RulesList, type Section, type Step,
} from './rulesModel';

const SECTION_LABELS: Record<string, string> = { stairs: 'Stairs', rebar: 'Reinforcement', roofs: 'Roofs', bolts: 'Bolts', loads: 'Loads' };
const EDITED = ['stairs', 'rebar', 'roofs', 'bolts', 'loads'];

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
  const importPack = async (given?: { bytes: Uint8Array }) => {
    const f = given ?? await pickFile('Rule pack (.lapinrules)', 'lapinrules');
    if (!f) return;
    // Through the one import preview (audit D13): a pack of its name there kept both, replaced or skipped.
    const r = await importLibraryText(new TextDecoder().decode(f.bytes), 'name' in f && typeof f.name === 'string' ? f.name : 'rules.lapinrules') as { id?: string } | null;
    if (r?.id) setSelected(r.id);
  };
  const check = async () => { const r = await ask<{ pack: string; findings: Finding[] }>('rules.check', {}); if (r) setFindings(r); };
  const applyStairs = async () => {
    const r = await run<{ changed: number }>('architecture.applyStairRules', {});
    if (r) notify(r.changed === 0 ? 'Every stair type already follows the active rules.' : `${r.changed} stair types now follow the active rules.`);
  };
  const remove = async () => { if (item.builtin) return; if (await run('rules.delete', { id: item.id }) !== null) setSelected(null); };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
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
            <FirmFolderFiles ext="lapinrules" onPick={(f) => importPack(f)}
              save={{ get: async () => { const r = await ask<{ text: string; file: string }>('rules.export', { id: item.id }); return r ? { text: r.text, name: r.file.replace(/\.[^.]+$/, '') } : null; } }} />
          </aside>
          <main className="tpl-detail">
            <p className="muted small rules-note" data-testid="rules-note">Every value here is indicative and yours to check against the norm in force: Lapin's tools propose and warn from these rules, they never refuse.</p>
            {readOnly
              ? <h3 className="tpl-title" data-no-i18n>{item.name}</h3>
              : <label className="field-line"><span className="eyebrow">Name</span>
                <input key={item.id + item.name} data-field="rules-name" defaultValue={item.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== item.name) void execute2('rules.update', { id: item.id, name: v }); }} /></label>}
            {!readOnly && <label className="field-line"><span className="eyebrow">Description</span>
              <input key={item.id + (item.description ?? '')} data-field="rules-description" defaultValue={item.description ?? ''} onBlur={(e) => { const v = e.target.value.trim(); if (v !== (item.description ?? '')) void execute2('rules.update', { id: item.id, description: v }); }} /></label>}
            {!readOnly && <PackMetaFields item={item} setProblem={setProblem} />}
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
            {section === 'bolts' && pack.sections.bolts && <BoltsEditor values={pack.sections.bolts} readOnly={readOnly} onChange={(v) => void update('bolts', v)} setProblem={setProblem} />}
            {section === 'loads' && pack.sections.loads && <LoadsEditor values={pack.sections.loads as unknown as LoadsSection} readOnly={readOnly} onChange={(v) => void update('loads', v as unknown as Section)} setProblem={setProblem} />}
            {!EDITED.includes(section) && <p className="muted small">This section has no table here: it comes and goes with the pack's file.</p>}
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

function BoltsEditor({ values, readOnly, onChange, setProblem }: { values: Section; readOnly: boolean; onChange: (s: Section) => void; setProblem: (p: string | null) => void }) {
  const grades = (values.grades as string[]) ?? [];
  const [grade, setGrade] = useState('');
  const add = () => {
    const next = addGrade(grades, grade);
    if (next === grades) { setProblem('A grade is 1 to 20 characters and not already in the list.'); return; }
    onChange({ ...values, grades: next });
    setGrade('');
  };
  return (
    <div data-testid="rules-bolts">
      <h4 className="eyebrow">Bolt grades offered</h4>
      <ul className="plain-list">
        {grades.map((g, i) => (
          <li key={g + i} className="row">
            <input defaultValue={g} readOnly={readOnly} data-field={`rules-grade-${i}`} data-no-i18n
              onBlur={(e) => {
                const v = e.target.value.trim();
                if (v === g) return;
                const next = renameGrade(grades, i, v);
                if (next === grades) { e.target.value = g; setProblem('A grade is 1 to 20 characters and not already in the list.'); return; }
                onChange({ ...values, grades: next });
              }} />
            {!readOnly && grades.length > 1 && <button type="button" className="icon mini" title="Take this grade off the list" onClick={() => onChange({ ...values, grades: removeGrade(grades, i) })}><X size={10} /></button>}
          </li>
        ))}
      </ul>
      {!readOnly && <div className="row">
        <input placeholder="New grade" value={grade} data-field="rules-new-grade" onChange={(e) => setGrade(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && grade.trim()) add(); }} />
        <button type="button" className="icon" title="Add the grade" disabled={!grade.trim()} onClick={add}><Plus size={13} /></button>
      </div>}
      <p className="muted small">These are the grades a steel connection can be given, in its properties and in the hall's connections.</p>
    </div>
  );
}

// ─── What the pack says of itself ────────────────────────────────────────────

/** The country, the edition of the rules, the language and the sources of a pack of the project's (docs/plan-national-packs.md). */
function PackMetaFields({ item, setProblem }: { item: { id: string; meta?: { country?: string; edition?: string; language?: string; sources?: string[] } }; setProblem: (p: string | null) => void }) {
  const meta = item.meta ?? {};
  const put = (key: 'country' | 'edition' | 'language', had: string | undefined) => (e: React.FocusEvent<HTMLInputElement>) => {
    const v = e.target.value.trim();
    if (v === (had ?? '')) return;
    if (key === 'language' && languageFrom(v) === null) { setProblem('The language is a two-letter code (sv, ro, en).'); return; }
    void execute2('rules.update', { id: item.id, [key]: v });
  };
  const sources = (meta.sources ?? []).join('\n');
  return (
    <div className="row wrap" data-testid="rules-meta">
      <label className="field-line"><span className="eyebrow">Country</span>
        <input key={item.id + (meta.country ?? '')} data-field="rules-country" defaultValue={meta.country ?? ''} placeholder="SE" onBlur={put('country', meta.country)} /></label>
      <label className="field-line"><span className="eyebrow">Edition of the rules</span>
        <input key={item.id + (meta.edition ?? '')} data-field="rules-edition" defaultValue={meta.edition ?? ''} title="The edition the values were read from: rules change, so a pack names the one it follows" onBlur={put('edition', meta.edition)} /></label>
      <label className="field-line"><span className="eyebrow">Language</span>
        <input key={item.id + (meta.language ?? '')} data-field="rules-language" defaultValue={meta.language ?? ''} placeholder="sv" style={{ width: 60 }} onBlur={put('language', meta.language)} /></label>
      <label className="field-line grow"><span className="eyebrow">Sources, one a line</span>
        <textarea key={item.id + sources} data-field="rules-sources" rows={2} defaultValue={sources}
          onBlur={(e) => { const next = sourcesFrom(e.target.value); if (next.join('\n') !== sources) void execute2('rules.update', { id: item.id, sources: next }); }} /></label>
    </div>
  );
}

// ─── Loads ───────────────────────────────────────────────────────────────────

/** A ψ0, ψ1, ψ2 triple as three fields. */
function PsiFields({ psi, readOnly, onChange, test }: { psi: Record<'0' | '1' | '2', number>; readOnly: boolean; onChange: (i: 0 | 1 | 2, v: number) => void; test: string }) {
  return (
    <span className="row">
      {([0, 1, 2] as const).map((i) => (
        <span key={i} data-rule={`${test}.${i}`}><NumberField value={psi[String(i) as '0']} readOnly={readOnly} min={0} max={1} onCommit={(n) => { if (n != null) onChange(i, n); }} /></span>
      ))}
    </span>
  );
}

/**
 * The loads of a pack (docs/plan-national-packs.md N1): its factor sets (γ, the expression and ξ, γd by safety class, ψ of snow by
 * the site's s0,k, ψ by category) and the imposed loads. A project chooses its set and safety class in Loads → Combinations.
 */
function LoadsEditor({ values, readOnly, onChange, setProblem }: { values: LoadsSection; readOnly: boolean; onChange: (s: LoadsSection) => void; setProblem: (p: string | null) => void }) {
  const names = Object.keys(values.sets ?? {});
  const [chosen, setChosen] = useState(names[0] ?? 'en');
  const [newSet, setNewSet] = useState('');
  const [newClass, setNewClass] = useState('');
  const name = names.includes(chosen) ? chosen : names[0]!;
  const set = values.sets[name];
  if (!set) return null;
  const put = (next: LoadSet | null) => {
    if (!next) { setProblem('A safety class needs a name, and its γd is between 0.5 and 1.5.'); return; }
    const p = loadSetProblem(next);
    if (p) { setProblem(`${name}: ${p}.`); return; }
    onChange(setLoadSet(values, name, next));
  };
  const categories = Object.keys(set.psi);
  return (
    <div data-testid="rules-loads">
      <div className="row wrap">
        {names.map((n) => (
          <span key={n} className={`chip${n === name ? ' on' : ''}`}>
            <button type="button" className="link-button" data-load-set={n} onClick={() => setChosen(n)} data-no-i18n>{n}</button>
            {!readOnly && !BUILTIN_LOAD_SETS.includes(n) && <button type="button" className="icon mini" title="Remove this set" onClick={() => onChange(removeLoadSet(values, n))}><X size={10} /></button>}
          </span>
        ))}
        {!readOnly && <>
          <input placeholder="New set's name" value={newSet} data-field="rules-new-load-set" onChange={(e) => setNewSet(e.target.value)} />
          <button type="button" className="btn small" disabled={!newSet.trim()} data-field="rules-add-load-set" title="A set of your own, started from the one shown"
            onClick={() => { const n = addLoadSet(values, newSet, name); if (n) { onChange(n); setChosen(newSet.trim()); setNewSet(''); } else setProblem('That set exists already, or its name is too long.'); }}><Plus size={12} />Set</button>
        </>}
      </div>
      <label className="field-line"><span className="eyebrow">Label</span>
        <input key={name + set.label} data-field="rules-load-label" defaultValue={set.label} readOnly={readOnly} data-no-i18n
          onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== set.label) put({ ...set, label: v }); }} /></label>
      <table className="layer-table rules-table" data-testid="rules-load-factors">
        <tbody>
          {LOAD_SET_FIELDS.map((f) => (
            <tr key={f.key}><td data-no-i18n>{f.label}</td>
              <td data-rule={`loads.${name}.${f.key}`}><NumberField value={set[f.key]} readOnly={readOnly} min={f.min} max={f.max} onCommit={(n) => { if (n != null) put({ ...set, [f.key]: n }); }} /></td></tr>
          ))}
          <tr><td>Expression</td>
            <td><select value={set.expression} disabled={readOnly} data-field="rules-load-expression" onChange={(e) => put({ ...set, expression: e.target.value as LoadSet['expression'] })}>
              <option value="6.10" data-no-i18n>6.10</option><option value="6.10ab">6.10a and 6.10b</option>
            </select></td></tr>
        </tbody>
      </table>

      <h4 className="eyebrow">γd by safety class</h4>
      <table className="layer-table rules-table" data-testid="rules-safety-classes">
        <thead><tr><th>Safety class</th><th data-no-i18n>γd</th><th /></tr></thead>
        <tbody>
          {Object.entries(set.safetyClasses).map(([c, g]) => (
            <tr key={c}><td data-no-i18n>{c}</td>
              <td data-rule={`loads.${name}.class.${c}`}><NumberField value={g} readOnly={readOnly} min={0.5} max={1.5} onCommit={(n) => { if (n != null) put(setSafetyClass(set, c, n)); }} /></td>
              <td>{!readOnly && <button type="button" className="icon mini" title="Remove this class" onClick={() => put(removeSafetyClass(set, c))}><X size={11} /></button>}</td></tr>
          ))}
        </tbody>
        {!readOnly && <tfoot><tr><td colSpan={3}><span className="row">
          <input placeholder="Class" value={newClass} style={{ width: 80 }} data-field="rules-new-class" onChange={(e) => setNewClass(e.target.value)} />
          <button type="button" className="btn small" disabled={!newClass.trim()} data-field="rules-add-class" onClick={() => { put(setSafetyClass(set, newClass, 1)); setNewClass(''); }}><Plus size={12} />Class</button>
        </span></td></tr></tfoot>}
      </table>
      <p className="muted small">γd multiplies the unfavourable actions of the ultimate combinations of a project of this safety class; none: γd 1.</p>

      <h4 className="eyebrow">ψ of snow by the site's s0,k</h4>
      <table className="layer-table rules-table" data-testid="rules-snow-zones">
        <thead><tr><th>From s0,k</th><th data-no-i18n>ψ0 ψ1 ψ2</th><th /></tr></thead>
        <tbody>
          {set.snowPsi.map((z, i) => (
            <tr key={i}>
              <td><NumberField value={z.from} suffix="kN/m²" readOnly={readOnly} min={0} max={20} onCommit={(n) => { if (n != null) put(setSnowZone(set, i, { from: n })); }} /></td>
              <td><PsiFields psi={z.psi} readOnly={readOnly} test={`loads.${name}.snow.${i}`} onChange={(k, v) => put(setSnowZone(set, i, { psi: { ...z.psi, [String(k)]: v } }))} /></td>
              <td>{!readOnly && <button type="button" className="icon mini" title="Remove this zone" onClick={() => put(removeSnowZone(set, i))}><X size={11} /></button>}</td>
            </tr>
          ))}
        </tbody>
        {!readOnly && <tfoot><tr><td colSpan={3}><button type="button" className="btn small" data-field="rules-add-snow-zone" onClick={() => put(addSnowZone(set))}><Plus size={12} />Zone</button></td></tr></tfoot>}
      </table>
      <p className="muted small">No zone: ψ of snow is the table's. With zones, a site's s0,k (Loads → Snow and wind) takes the last zone it reaches.</p>

      <details className="small" data-testid="rules-load-psi">
        <summary>ψ by category</summary>
        <table className="layer-table rules-table">
          <thead><tr><th>Category</th><th data-no-i18n>ψ0 ψ1 ψ2</th></tr></thead>
          <tbody>{categories.map((c) => (
            <tr key={c}><td data-no-i18n>{c}</td><td><PsiFields psi={set.psi[c]!} readOnly={readOnly} test={`loads.${name}.psi.${c}`} onChange={(i, v) => put(setPsi(set, c, i, v))} /></td></tr>
          ))}</tbody>
        </table>
      </details>

      <details className="small" data-testid="rules-imposed">
        <summary>Imposed loads, qk</summary>
        <table className="layer-table rules-table">
          <thead><tr><th>Category</th><th data-no-i18n>qk</th></tr></thead>
          <tbody>{Object.entries(values.imposed ?? {}).map(([c, q]) => (
            <tr key={c}><td data-no-i18n>{c}</td><td data-rule={`loads.imposed.${c}`}><NumberField value={q} suffix="kN/m²" readOnly={readOnly} min={0} max={1000} onCommit={(n) => { if (n != null) onChange({ ...values, imposed: { ...values.imposed, [c]: n } }); }} /></td></tr>
          ))}</tbody>
        </table>
      </details>
      <p className="muted small">A project picks its set and safety class in Loads → Combinations. The values are yours, typed from the edition in force; Lapin's are indicative.</p>
    </div>
  );
}
