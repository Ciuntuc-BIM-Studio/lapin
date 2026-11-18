/**
 * The rule table editor of a material standard (docs/plan-material-graphics.md, stage 3): its details, its rules in order (the
 * first that matches wins: drag or arrows), and for the rule chosen its names per language (chips), the category it matches, the
 * graphics for the cut in a section, the cut in a plan and the elevation (the material editor's own fields, by scale too), its
 * membrane and category; the standard's patterns (a .pat file brought in) and pens. Beside it the standard drawn on the reference
 * details, live. A built-in standard opens read-only, with "Duplicate to edit". Saved by `standards.materialSet`, one undo step.
 */

import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Copy, FileUp, GripVertical, Plus, Trash2, X } from 'lucide-react';
import { notify } from '../../app/store';
import { query } from '../../engine/session';
import { pickFile } from '../../engine/files';
import { confirmAction } from '../../shell/prompts';
import { ColourField } from '../../shell/ColourField';
import { ask, run } from './LibraryChoices';
import { ColourControl, LineTypeSelect, MATERIAL_KINDS, NumberInput, OpacityInput, Row, type HatchPattern } from './MaterialGraphicsEditor';
import { MaterialScaleSteps } from './MaterialScaleSteps';
import { PatternOptions, UNITS_LABEL } from './hatchUnits';
import { MaterialStandardSheet } from './MaterialStandardSheet';
import { MaterialStandardPublish } from './MaterialStandardPublish';
import {
  ISO_WEIGHTS, fieldPatch, groupPatch, isSet, parseNumber, parseWeight, stepsOf, stepsPatch, type Field, type GroupField, type MaterialKind, type OwnGraphics, type ResolvedGraphics,
} from './materialGraphicsModel';
import {
  RULE_CATEGORIES, addPatterns, addRule, blankStandard, duplicateRule, moveRule, moveRuleTo, nameRows, parseNames, patchRuleKind, removeLanguage, removePattern, removeRuleAt,
  ruleLabel, setLanguages, setMeta, setNames, setPen, splitPatterns, standardProblem, updateRule, withPatterns,
  type MaterialStandard, type Rule, type RuleGraphics, type StandardGet,
} from './materialStandardsModel';

/** What a rule's fields show while they are not set (the editor has no material to inherit from). */
const NOTHING: ResolvedGraphics = { hatch: null, hatchScale: 1, fill: null, membrane: false, drawJoints: false, source: 'own' };

/** Names as chips: a chip each, × takes it away, typing and Enter (or a comma) adds. */
function ChipInput({ value, onChange, label }: { value: string[]; onChange: (v: string[]) => void; label: string }) {
  const [text, setText] = useState('');
  const commit = () => { const add = parseNames(text); if (add.length) onChange([...value, ...add.filter((a) => !value.some((v) => v.toLowerCase() === a.toLowerCase()))]); setText(''); };
  return (
    <span className="chips" data-testid="rule-names">
      {value.map((n) => <span key={n} className="chip" data-no-i18n>{n}<button type="button" className="icon mini" aria-label="Remove" onClick={() => onChange(value.filter((v) => v !== n))}><X size={10} /></button></span>)}
      <input aria-label={label} value={text} placeholder="name, name…" onChange={(e) => { if (/[,;]$/.test(e.target.value)) { setText(e.target.value); setTimeout(commit, 0); } else setText(e.target.value); }}
        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); commit(); } }} onBlur={commit} />
    </span>
  );
}

/** One kind's graphics of a rule: the material editor's fields over a plain object; every change is a patch the model applies. */
export function RuleGraphicsFields({ kind, gfx, hatches, onPatch }: { kind: MaterialKind; gfx: RuleGraphics | undefined; hatches: HatchPattern[]; onPatch: (p: Record<string, unknown> | null) => void }) {
  const own = (gfx ?? {}) as OwnGraphics;
  const has = (f: Field) => isSet(own, f);
  const set = (f: Field, v: unknown) => onPatch(fieldPatch(f, v));
  const grp = (g: GroupField, k: string, v: unknown) => onPatch(groupPatch(own, g, k, v));
  const hatchValue = 'hatch' in own ? (own.hatch ?? '') : '*';
  const fillMode = own.fill === 'material' ? 'material' : own.fill ? 'colour' : '';
  const num = (id: string, value: number | undefined, parse: (t: string) => number | null, commit: (v: number | undefined) => void, placeholder = '') => (
    <NumberInput id={`${kind}${id}`} value={value} placeholder={placeholder} parse={parse} onCommit={commit} />
  );
  const positive = (t: string) => { const v = parseNumber(t); return v > 0 ? v : null; };
  const lineRows = (g: 'cutLine' | 'separatorLine') => {
    const l = own[g] ?? {};
    return (
      <>
        <Row label="Colour" set={l.color !== undefined} onReset={() => grp(g, 'color', undefined)}><ColourControl value={l.color} auto="#000000" onSet={(v) => grp(g, 'color', v)} /></Row>
        <Row label="Weight, mm" set={l.weight !== undefined} onReset={() => grp(g, 'weight', undefined)}>{num(`${g}w`, l.weight, parseWeight, (v) => grp(g, 'weight', v), 'Object styles')}</Row>
        <Row label="Line type" set={l.lineType !== undefined} onReset={() => grp(g, 'lineType', undefined)}><LineTypeSelect value={l.lineType} onSet={(v) => grp(g, 'lineType', v)} /></Row>
      </>
    );
  };
  return (
    <div className="rule-gfx" data-testid="rule-graphics" data-kind={kind}>
      <datalist id="gfx-iso-weights">{ISO_WEIGHTS.map((w) => <option key={w} value={w} />)}</datalist>
      <Row label="Pattern" set={hatchValue !== '*'} onReset={() => set('hatch', undefined)}>
        <select data-field={`rule-hatch-${kind}`} value={hatchValue} onChange={(e) => (e.target.value === '*' ? set('hatch', undefined) : set('hatch', e.target.value || null))}>
          <option value="*">not set by this rule</option>
          <option value="">none</option>
          <PatternOptions patterns={hatches} current={hatchValue !== '*' && hatchValue !== '' ? hatchValue : undefined} />
        </select>
      </Row>
      <Row label="Units" set={has('hatchUnits')} onReset={() => set('hatchUnits', undefined)}>
        <select value={own.hatchUnits ?? ''} onChange={(e) => set('hatchUnits', e.target.value || undefined)}>
          <option value="">pattern's own</option><option value="paper">{UNITS_LABEL.paper}</option><option value="model">{UNITS_LABEL.model}</option>
        </select>
      </Row>
      <Row label="Scale" set={has('hatchScale')} onReset={() => set('hatchScale', undefined)}>{num('s', own.hatchScale, positive, (v) => set('hatchScale', v))}</Row>
      <Row label="Angle" set={has('hatchAngle')} onReset={() => set('hatchAngle', undefined)}>{num('a', own.hatchAngle, (t) => { const v = parseNumber(t); return Number.isFinite(v) ? v : null; }, (v) => set('hatchAngle', v))}</Row>
      <Row label="Colour" set={has('hatchColor')} onReset={() => set('hatchColor', undefined)}><ColourControl value={own.hatchColor} auto="#000000" onSet={(v) => set('hatchColor', v)} /></Row>
      <Row label="Weight, mm" set={has('hatchWeight')} onReset={() => set('hatchWeight', undefined)}>{num('hw', own.hatchWeight, parseWeight, (v) => set('hatchWeight', v), 'from the pen')}</Row>
      <Row label="Line type" set={has('hatchLineType')} onReset={() => set('hatchLineType', undefined)}><LineTypeSelect value={own.hatchLineType} onSet={(v) => set('hatchLineType', v)} /></Row>
      <Row label="Opacity" set={has('hatchOpacity')} onReset={() => set('hatchOpacity', undefined)}><OpacityInput value={own.hatchOpacity} inherited={100} onDraft={() => undefined} onCommit={(v) => set('hatchOpacity', v)} /></Row>
      <Row label="Fill" set={fillMode !== ''} onReset={() => set('fill', undefined)}>
        <select data-field={`rule-fill-${kind}`} value={fillMode} onChange={(e) => (e.target.value === '' ? set('fill', undefined) : set('fill', e.target.value === 'material' ? 'material' : (own.fill && own.fill !== 'material' ? own.fill : '#ffffff')))}>
          <option value="">none</option><option value="material">material colour</option><option value="colour">colour…</option>
        </select>
        {fillMode === 'colour' && <ColourField value={own.fill ?? '#ffffff'} onChange={(v) => set('fill', v.slice(0, 7))} />}
      </Row>
      <Row label="Second pattern" set={own.backgroundHatch !== undefined} onReset={() => set('backgroundHatch', undefined)}>
        <select value={own.backgroundHatch?.hatch ?? ''} onChange={(e) => (e.target.value ? grp('backgroundHatch', 'hatch', e.target.value) : set('backgroundHatch', undefined))}>
          <option value="">none</option><PatternOptions patterns={hatches} />
        </select>
      </Row>
      <span className="eyebrow">Cut outline</span>
      {lineRows('cutLine')}
      <span className="eyebrow">Separator lines</span>
      {lineRows('separatorLine')}
      <details className="gfx-section" open={stepsOf(own).length > 0}>
        <summary>By scale</summary>
        <MaterialScaleSteps kind={kind} materialId={`rule-${kind}`} steps={stepsOf(own)} r={NOTHING} hatches={hatches} activeIndex={null} onSteps={(s) => onPatch(stepsPatch(s))} onPreviewAt={() => undefined} />
      </details>
    </div>
  );
}

const LANGS = ['en', 'ro', 'de', 'fr', 'es', 'it', 'pl', 'pt', 'da', 'hu', 'cs', 'bg', 'nl', 'sv', 'fi', 'no'];

function RuleEditor({ rule, hatches, onChange }: { rule: Rule; hatches: HatchPattern[]; onChange: (f: (r: Rule) => Rule) => void }) {
  const [kind, setKind] = useState<MaterialKind>('section');
  const [lang, setLang] = useState('');
  const rows = nameRows(rule.match);
  return (
    <div className="rule-editor" data-testid="rule-editor">
      <span className="eyebrow">Matches the material’s name</span>
      {rows.map((r) => (
        <div key={r.lang} className="row rule-names-row">
          <span className="small rule-lang" data-no-i18n>{r.lang || '*'}</span>
          <ChipInput value={r.names} label="Names" onChange={(names) => onChange((x) => setNames(x, r.lang, names))} />
          {r.lang !== '' && <button type="button" className="icon mini" title="Remove this language" aria-label="Remove this language" onClick={() => onChange((x) => removeLanguage(x, r.lang))}><X size={11} /></button>}
        </div>
      ))}
      <div className="row">
        <input className="num" list="rule-langs" placeholder="language" aria-label="Add a language" value={lang} onChange={(e) => setLang(e.target.value.toLowerCase().slice(0, 3))} />
        <datalist id="rule-langs">{LANGS.map((l) => <option key={l} value={l} />)}</datalist>
        <button type="button" className="btn compact" disabled={!/^[a-z]{2,3}$/.test(lang) || rows.some((r) => r.lang === lang)} data-testid="rule-add-language"
          title="Names in another language: a Polish pack also recognises the materials named in English" onClick={() => { onChange((x) => setNames(x, lang, [])); setLang(''); }}><Plus size={12} />Add a language</button>
      </div>
      <Row label="Matches the category" set={!!rule.match.category} onReset={() => onChange((x) => { const { category: _c, ...m } = x.match; return { ...x, match: m }; })}>
        <select value={rule.match.category ?? ''} onChange={(e) => onChange((x) => ({ ...x, match: { ...x.match, ...(e.target.value ? { category: e.target.value } : { category: undefined }) } }))}>
          <option value="">any</option>{RULE_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
      </Row>
      <span className="eyebrow">Gives the material</span>
      <Row label="Category" set={!!rule.category} onReset={() => onChange((x) => { const { category: _c, ...r } = x; return r; })}>
        <select value={rule.category ?? ''} onChange={(e) => onChange((x) => { const { category: _c, ...r } = x; return e.target.value ? { ...r, category: e.target.value } : r; })}>
          <option value="">not set by this rule</option>{RULE_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
      </Row>
      <Row label="Membrane" title="Drawn as a single line in the cut, as a membrane is" set={rule.membrane !== undefined} onReset={() => onChange((x) => { const { membrane: _m, ...r } = x; return r; })}>
        <input type="checkbox" checked={rule.membrane ?? false} onChange={(e) => onChange((x) => ({ ...x, membrane: e.target.checked }))} />
      </Row>
      <div className="gfx-kinds" role="tablist">
        {MATERIAL_KINDS.map(({ kind: k, label }) => (
          <button key={k} type="button" role="tab" aria-selected={kind === k} data-kind={k} className={`tab gfx-kind${kind === k ? ' on' : ''}`} onClick={() => setKind(k)}>
            <span>{label}</span>{rule[k] && <span className="muted small" aria-hidden> •</span>}
          </button>
        ))}
        {rule[kind] && <button type="button" className="icon mini" title="This rule says nothing of it" aria-label="This rule says nothing of it" onClick={() => onChange((x) => patchRuleKind(x, kind, null))}><X size={11} /></button>}
      </div>
      <RuleGraphicsFields kind={kind} gfx={rule[kind]} hatches={hatches} onPatch={(p) => onChange((x) => patchRuleKind(x, kind, p))} />
    </div>
  );
}

const META: { key: 'name' | 'author' | 'country' | 'norm' | 'version' | 'license'; label: string; title?: string }[] = [
  { key: 'name', label: 'Name' }, { key: 'author', label: 'Author' },
  { key: 'country', label: 'Country', title: 'Two letters: RO, DE, PL' }, { key: 'norm', label: 'Norm', title: 'The norm it follows, for example PN-B-01030' },
  { key: 'version', label: 'Version' }, { key: 'license', label: 'Licence', title: 'An SPDX expression: MIT, CC-BY-4.0' },
];

export function MaterialStandardEditor({ id, initial, onClose, onSaved, onDuplicate }: {
  id?: string; initial?: MaterialStandard; onClose: () => void; onSaved: (id: string) => void; onDuplicate: (id: string) => void;
}) {
  const [loaded, setLoaded] = useState<StandardGet | null>(null);
  const [draft, setDraft] = useState<MaterialStandard | null>(initial ?? null);
  const [original, setOriginal] = useState<string>(() => JSON.stringify(initial ?? null));
  const [sel, setSel] = useState(0);
  const [hatches, setHatches] = useState<HatchPattern[]>([]);
  const [publishing, setPublishing] = useState(false);
  const [dragFrom, setDragFrom] = useState<number | null>(null);
  useEffect(() => {
    if (!id) return;
    let live = true;
    void ask<StandardGet>('standards.materialGet', { id }).then((r) => { if (live && r) { setLoaded(r); setDraft(r.standard); setOriginal(JSON.stringify(r.standard)); } });
    return () => { live = false; };
  }, [id]);
  useEffect(() => { void query<HatchPattern[]>('hatch.list').then((r) => r && setHatches(r)); }, []);
  const readOnly = loaded?.builtin ?? false;
  const dirty = draft !== null && JSON.stringify(draft) !== original;
  const problem = draft ? standardProblem(draft) : null;
  const target = useMemo(() => (draft && !problem ? { standard: draft } : null), [draft, problem]);
  if (!draft) return <p className="muted">Reading…</p>;
  const edit = (f: (s: MaterialStandard) => MaterialStandard) => setDraft((d) => (d ? f(d) : d));
  const rule = draft.rules[Math.min(sel, draft.rules.length - 1)];
  const at = Math.min(sel, draft.rules.length - 1);
  const save = async () => {
    const r = await run<{ id: string }>('standards.materialSet', { standard: draft });
    if (r) { notify(`Saved „${draft.name}”.`); setOriginal(JSON.stringify(draft)); onSaved(r.id); }
  };
  const close = () => { if (!dirty || confirmAction('Close without saving the changes?')) onClose(); };
  const importPat = async () => {
    const f = await pickFile('Hatch patterns (.pat)', 'pat');
    if (f) edit((s) => withPatterns(s, addPatterns(s.patterns, new TextDecoder().decode(f.bytes))));
  };
  const duplicate = async () => {
    if (!id) return;
    const r = await run<{ id: string }>('standards.materialDuplicate', { id, name: `${draft.name} (copy)` });
    if (r) onDuplicate(r.id);
  };
  return (
    <div className="standard-editor" data-testid="standard-editor">
      <div className="row">
        <span className="panel-title" data-no-i18n>{draft.name}</span>
        {readOnly && <span className="badge" data-testid="standard-builtin">Built in</span>}
        <span className="grow" />
        {readOnly && <button type="button" className="btn primary" data-action="standard-duplicate-to-edit" onClick={() => void duplicate()}><Copy size={13} />Duplicate to edit</button>}
        {!readOnly && <button type="button" className="btn" data-action="standard-publish" disabled={!id || dirty} title={!id || dirty ? 'Save first' : 'Write the manifest and files for the store'} onClick={() => setPublishing((p) => !p)}>Publish to store…</button>}
        {!readOnly && <button type="button" className="btn primary" data-action="standard-save" disabled={!dirty || !!problem} title={problem ?? undefined} onClick={() => void save()}>Save</button>}
        <button type="button" className="btn" onClick={close}>Close</button>
      </div>
      {readOnly && <p className="muted small">A built-in standard is the base to come back to: duplicate it, then change the copy.</p>}
      {problem && !readOnly && <p className="small error-text" role="alert">{problem}</p>}
      {publishing && id && <MaterialStandardPublish id={id} standard={draft} onClose={() => setPublishing(false)} />}
      <div className="standard-fields">
        <fieldset disabled={readOnly} className="standard-fieldset"><details className="gfx-section" open={!id}>
          <summary>Details</summary>
          <div className="standard-meta">
            {META.map((m) => (
              <label key={m.key} className="field-line" title={m.title}><span className="small">{m.label}</span>
                <input data-field={`standard-${m.key}`} value={draft[m.key] ?? ''} onChange={(e) => edit((s) => setMeta(s, m.key, e.target.value))} />
              </label>
            ))}
            <label className="field-line"><span className="small">Languages</span><input data-field="standard-languages" value={(draft.languages ?? []).join(', ')} placeholder="en, ro" onChange={(e) => edit((s) => setLanguages(s, e.target.value))} /></label>
            <label className="field-line"><span className="small">Description</span><textarea rows={2} value={draft.description ?? ''} onChange={(e) => edit((s) => setMeta(s, 'description', e.target.value))} /></label>
            <label className="field-line check"><input type="checkbox" data-field="standard-indicative" checked={draft.indicative ?? false} onChange={(e) => edit((s) => ({ ...s, indicative: e.target.checked }))} /><span>Indicative: drawn from general principles, check it against the norm</span></label>
          </div>
        </details></fieldset>
        <div className="standard-split">
          <div className="standard-rules">
            <div className="row"><span className="eyebrow">Rules, the first that matches wins</span><span className="grow" />
              <button type="button" className="btn compact" data-action="rule-add" disabled={readOnly} onClick={() => { edit((s) => addRule(s)); setSel(draft.rules.length); }}><Plus size={12} />Add a rule</button></div>
            <ol className="rule-list" data-testid="rule-list">
              {draft.rules.map((r, i) => (
                <li key={i} className={`rule-row${i === at ? ' on' : ''}`} data-rule={i} draggable={!readOnly}
                  onDragStart={() => setDragFrom(i)} onDragOver={(e) => e.preventDefault()} onDrop={() => { if (dragFrom !== null) { edit((s) => moveRuleTo(s, dragFrom, i)); setSel(i); } setDragFrom(null); }}>
                  <GripVertical size={12} className="muted" aria-hidden />
                  <button type="button" className="tree-item" onClick={() => setSel(i)}><span className="muted small">{i + 1}</span> <span data-no-i18n>{ruleLabel(r) || '…'}</span></button>
                  <button type="button" className="icon mini" aria-label="Move up" title="Earlier" disabled={readOnly || i === 0} onClick={() => { edit((s) => moveRule(s, i, -1)); setSel(i - 1); }}><ArrowUp size={11} /></button>
                  <button type="button" className="icon mini" aria-label="Move down" title="Later" disabled={readOnly || i === draft.rules.length - 1} onClick={() => { edit((s) => moveRule(s, i, 1)); setSel(i + 1); }}><ArrowDown size={11} /></button>
                  <button type="button" className="icon mini" aria-label="Duplicate the rule" title="Duplicate the rule" disabled={readOnly} onClick={() => { edit((s) => duplicateRule(s, i)); setSel(i + 1); }}><Copy size={11} /></button>
                  <button type="button" className="icon mini" aria-label="Remove the rule" title="Remove the rule" disabled={readOnly || draft.rules.length < 2} onClick={() => { edit((s) => removeRuleAt(s, i)); setSel(Math.max(0, i - 1)); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ol>
          </div>
          {rule && <fieldset disabled={readOnly} className="standard-fieldset"><RuleEditor key={at} rule={rule} hatches={hatches} onChange={(f) => edit((s) => updateRule(s, at, f))} /></fieldset>}
        </div>
        <fieldset disabled={readOnly} className="standard-fieldset"><details className="gfx-section">
          <summary>Patterns and pens</summary>
          <div className="row"><button type="button" className="btn compact" data-action="standard-import-pat" onClick={() => void importPat()}><FileUp size={12} />Import a .pat file…</button></div>
          <ul className="small" data-testid="standard-patterns">
            {splitPatterns(draft.patterns).map((p) => <li key={p.name}><span data-no-i18n>{p.name}</span> <button type="button" className="icon mini" aria-label="Remove the pattern" onClick={() => edit((s) => withPatterns(s, removePattern(s.patterns, p.name)))}><X size={10} /></button></li>)}
          </ul>
          {(['hatch', 'cut'] as const).map((w) => {
            const pen = draft.pens?.[w] ?? {};
            return (
              <div key={w} className="standard-pen">
                <span className="eyebrow">{w === 'hatch' ? 'Hatch pen' : 'Cut pen'}</span>
                <Row label="Colour" set={pen.color !== undefined} onReset={() => edit((s) => setPen(s, w, 'color', undefined))}><ColourControl value={pen.color} auto="#000000" onSet={(v) => edit((s) => setPen(s, w, 'color', v))} /></Row>
                <Row label="Weight, mm" set={pen.weight !== undefined} onReset={() => edit((s) => setPen(s, w, 'weight', undefined))}>
                  <NumberInput id={`pen${w}`} value={pen.weight} placeholder="" parse={parseWeight} list="gfx-iso-weights" onCommit={(v) => edit((s) => setPen(s, w, 'weight', v))} /></Row>
                <Row label="Line type" set={pen.lineType !== undefined} onReset={() => edit((s) => setPen(s, w, 'lineType', undefined))}><LineTypeSelect value={pen.lineType} onSet={(v) => edit((s) => setPen(s, w, 'lineType', v))} /></Row>
              </div>
            );
          })}
        </details></fieldset>
      </div>
      <MaterialStandardSheet target={id && !dirty ? { id } : target} />
    </div>
  );
}

/** A new standard to edit: empty, with one rule. */
export const newStandardDraft = (author?: string): MaterialStandard => blankStandard('My standard', author);
