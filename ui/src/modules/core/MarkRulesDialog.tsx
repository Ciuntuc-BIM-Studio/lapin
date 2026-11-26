/**
 * Manage → Mark rules… (decision 737): the marks the user builds, one rule per category (walls, doors, windows, their types,
 * floors, columns, beams, footings, stairs). A rule is a prefix, a suffix and a pattern of live text, `{prefix}{mark}{suffix}`
 * by default, where `{mark}` is the mark Lapin gives by itself, `{n}` the element's number (kept on it), `{= n | 00}` a formula
 * and any field a tag reads (`{storey.code}`, `{type.mark}`, a parameter), `{variant}` one number per size of a type (issue #148,
 * told by the rule's Variant by, else the category's own fields). Apply sets every changed rule in one step;
 * Renumber numbers a category again from 1, by where its elements stand, in one step. An element's own mark still wins.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { ListOrdered, RotateCcw, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query } from '../../engine/session';
import { backdropClose } from '../../shell/Dialog';
import { FieldPicker } from '../../shell/FieldPicker';

/** A rule as the dialog edits it; `variantBy` (issue #148): the fields that tell one variant of a type from another, empty: the category's own. */
export interface MarkRuleDto { prefix: string; suffix: string; pattern: string; scope: 'project' | 'storey'; variantBy?: string }
interface Sample { id: string; name: string; mark: string; own: boolean }
interface CategoryDto extends MarkRuleDto { id: string; label: string; isType: boolean; count: number; isDefault: boolean; samples: Sample[]; defaultVariantBy?: string }

export const DEFAULT_MARK_PATTERN = '{prefix}{mark}{suffix}';

/** Whether a rule is the built-in one: today's marks, nothing before or after them. */
export const isDefaultRule = (r: MarkRuleDto): boolean => !r.prefix && !r.suffix && (r.pattern.trim() || DEFAULT_MARK_PATTERN) === DEFAULT_MARK_PATTERN && r.scope === 'project' && !r.variantBy?.trim();

/** Whether two rules are the same as the engine keeps them. */
export const sameRule = (a: MarkRuleDto, b: MarkRuleDto): boolean =>
  a.prefix === b.prefix && a.suffix === b.suffix && (a.pattern.trim() || DEFAULT_MARK_PATTERN) === (b.pattern.trim() || DEFAULT_MARK_PATTERN) && a.scope === b.scope
  && (a.variantBy?.trim() ?? '') === (b.variantBy?.trim() ?? '');

/** Whether a pattern reads the variant ({variant}, or variant in a formula): its Variant by is then shown. */
export const usesVariant = (p: string): boolean => p.includes('{variant}') || /\{=[^{}]*(?<![\w.])variant(?![\w.(])[^{}]*\}/.test(p);

/** A pattern's braces each closed: what the engine accepts. */
export const patternOk = (p: string): boolean => {
  let open = 0;
  for (const ch of p) {
    open += ch === '{' ? 1 : ch === '}' ? -1 : 0;
    if (open < 0 || open > 1) return false;
  }
  return open === 0;
};

/** The tokens offered beside the picker, in the order a mark is usually built. */
const QUICK: [string, string][] = [
  ['{n}', 'Its number in the category'],
  ['{= n | 00}', 'Its number on two digits (01, 02…)'],
  ['{mark}', "The mark Lapin gives by itself: its type's mark, else its type's name"],
  ['{storey.code}', "Its storey's code (a parameter of the storey)"],
  ['{type.mark}', "Its type's mark"],
  ['{variant}', 'Its variant: one number per size of its type (1, 2…), the same size the same number'],
];

/** What the dialog is for, one phrase for the translations. */
const INTRO = "Build each category's mark: a prefix, a pattern and a suffix. The pattern is live text: {mark} is the mark Lapin gives by itself, {n} the element's number (kept on it, new elements take the next), {= n | 00} a formula, and any field a tag reads. Tags, schedules and IFC write the result; a mark typed on an element still wins.";
const CLOSE_FIELDS = 'Close each {field} you open.';

const rulePart = (c: MarkRuleDto): MarkRuleDto => ({ prefix: c.prefix, suffix: c.suffix, pattern: c.pattern, scope: c.scope, variantBy: c.variantBy ?? '' });

export function MarkRulesDialog() {
  const [cats, setCats] = useState<CategoryDto[]>([]);
  const [rows, setRows] = useState<Record<string, MarkRuleDto>>({});
  const [samples, setSamples] = useState<Record<string, Sample[]>>({});
  const [order, setOrder] = useState<'rows' | 'columns' | 'current'>('rows');
  const [byStorey, setByStorey] = useState(true);
  const timers = useRef<Record<string, ReturnType<typeof setTimeout>>>({});
  const close = () => useUi.setState({ dialog: null });
  const load = useCallback(() => {
    void query<{ categories: CategoryDto[] }>('markRules.list').then((r) => {
      if (!r) return;
      setCats(r.categories);
      setRows(Object.fromEntries(r.categories.map((c) => [c.id, rulePart(c)])));
      setSamples(Object.fromEntries(r.categories.map((c) => [c.id, c.samples])));
    });
  }, []);
  useEffect(load, [load]);
  // The marks a changed rule would give, asked a moment after the typing stops.
  const preview = (id: string, rule: MarkRuleDto) => {
    clearTimeout(timers.current[id]);
    if (!patternOk(rule.pattern) || !patternOk(rule.variantBy ?? '')) return;
    timers.current[id] = setTimeout(() => {
      void query<{ samples: Sample[] }>('markRules.preview', { category: id, rule }).then((r) => { if (r) setSamples((s) => ({ ...s, [id]: r.samples })); });
    }, 250);
  };
  const set = (id: string, patch: Partial<MarkRuleDto>) => {
    const next = { ...rows[id], ...patch };
    setRows({ ...rows, [id]: next });
    preview(id, next);
  };
  const changed = cats.filter((c) => rows[c.id] && !sameRule(rows[c.id], rulePart(c)));
  const ok = cats.every((c) => !rows[c.id] || (patternOk(rows[c.id].pattern) && patternOk(rows[c.id].variantBy ?? '')));
  const apply = async () => {
    const rules = Object.fromEntries(changed.map((c) => [c.id, isDefaultRule(rows[c.id]) ? null : rows[c.id]]));
    if (changed.length === 0 || await execute('markRules.set', { rules }) !== null) close();
  };
  const renumber = async (c: CategoryDto) => {
    const r = await execute('markRules.renumber', { category: c.id, order, byStorey }) as { changed?: number } | null;
    if (r) { notify(`${r.changed ?? 0} marks renumbered.`); load(); }
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide mark-rules-dialog" role="dialog" aria-label="Mark rules" data-testid="mark-rules-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Mark rules</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">{INTRO}</p>
          <table className="standards-table mark-rules-table" data-testid="mark-rules-table">
            <thead><tr><th>Category</th><th>Prefix</th><th>Pattern</th><th>Suffix</th><th title="Where its numbers are counted">Numbered</th><th>Marks</th><th /></tr></thead>
            <tbody>
              {cats.map((c) => {
                const r = rows[c.id];
                if (!r) return null;
                const bad = !patternOk(r.pattern) || !patternOk(r.variantBy ?? '');
                return (
                  <tr key={c.id} data-mark-category={c.id} className={bad ? 'bad' : undefined}>
                    <td><span>{c.label}</span> <span className="muted small" data-no-i18n>{c.count}</span></td>
                    <td><input className="cell" value={r.prefix} maxLength={16} aria-label="Prefix" data-field={`mark-prefix-${c.id}`} onChange={(e) => set(c.id, { prefix: e.target.value })} /></td>
                    <td>
                      <div className="row tight">
                        <input className="cell" value={r.pattern} maxLength={120} aria-label="Pattern" placeholder={DEFAULT_MARK_PATTERN} data-field={`mark-pattern-${c.id}`} data-no-i18n
                          onChange={(e) => set(c.id, { pattern: e.target.value })} />
                        <FieldPicker value="" placeholder="+ Field…" testId={`mark-field-${c.id}`} extra={[{ id: 'mark', label: 'Mark', fields: QUICK.map(([token, label]) => ({ token, label, kind: 'text' as const, count: 0, examples: [], description: label })) }]}
                          onPick={(t) => set(c.id, { pattern: `${r.pattern.trim() || DEFAULT_MARK_PATTERN}${t.startsWith('{') ? t : `{${t}}`}` })} />
                      </div>
                      {!c.isType && usesVariant(r.pattern) && (
                        <label className="row tight small" title="What tells one variant of its type from another: fields as a tag reads them ({openingWidth}x{openingHeight}, {height}, a parameter); empty: a door's or window's opening, else one variant per type. Numbered in the order the sizes first appear, in the whole project">
                          <span className="muted">Variant by</span>
                          <input className="cell" value={r.variantBy ?? ''} maxLength={120} aria-label="Variant by" placeholder={c.defaultVariantBy ?? ''} data-field={`mark-variant-${c.id}`} data-no-i18n
                            onChange={(e) => set(c.id, { variantBy: e.target.value })} />
                        </label>
                      )}
                    </td>
                    <td><input className="cell" value={r.suffix} maxLength={16} aria-label="Suffix" data-field={`mark-suffix-${c.id}`} onChange={(e) => set(c.id, { suffix: e.target.value })} /></td>
                    <td>{c.isType ? <span className="muted small">In the project</span> : (
                      <select className="cell" value={r.scope} data-field={`mark-scope-${c.id}`} onChange={(e) => set(c.id, { scope: e.target.value as MarkRuleDto['scope'] })}>
                        <option value="project">In the project</option>
                        <option value="storey">On each storey</option>
                      </select>
                    )}</td>
                    <td className="mark-samples" data-mark-samples={c.id}>
                      {bad ? <span className="muted small">{CLOSE_FIELDS}</span>
                        : (samples[c.id] ?? []).map((s) => <span key={s.id} className={`chip${s.own ? ' muted' : ''}`} style={{ textTransform: 'none' }} title={s.own ? 'Its own mark' : undefined} data-no-i18n>{s.mark}</span>)}
                    </td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" title="Back to the built-in rule" disabled={isDefaultRule(r)} onClick={() => set(c.id, { prefix: '', suffix: '', pattern: DEFAULT_MARK_PATTERN, scope: 'project', variantBy: '' })}><RotateCcw size={12} /></button>
                      <button type="button" className="icon mini" title="Renumber from 1 (one undo step)" data-field={`mark-renumber-${c.id}`} disabled={c.count === 0} onClick={() => void renumber(c)}><ListOrdered size={12} /></button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div className="row">
            <span className="muted small">Renumber</span>
            <select value={order} data-field="mark-renumber-order" onChange={(e) => setOrder(e.target.value as typeof order)}>
              <option value="rows">Top to bottom, each row left to right</option>
              <option value="columns">Left to right, each column top to bottom</option>
              <option value="current">In their present order</option>
            </select>
            <label className="check"><input type="checkbox" checked={byStorey} onChange={(e) => setByStorey(e.target.checked)} />Storey by storey, lowest first</label>
          </div>
          <p className="muted small">Renumbering uses the rules as applied. Elements brought by an IFC file keep the file's marks.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" /><button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!ok} data-testid="mark-rules-apply" onClick={() => void apply()}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
