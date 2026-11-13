/**
 * One field of a script's question or palette, or of a graph's palette (decisions 489, 528, 536), drawn by the window from
 * its description (askFields.ts), never from anything to run: a number with its unit (and a slider when it has a least
 * and a most), a text, a choice, a checkbox, elements of the model (Use selection), a storey, a type of a class.
 */

import { useMemo } from 'react';
import { MousePointerClick } from 'lucide-react';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';
import { elementsOf, fromSelection, readNumber, storeysOf, typesOf, type AskField, type FieldState, type ModelFacts } from './askFields';

/** What the fields know of the model (also a palette's, PaletteWindow.tsx). */
export function useFacts(): ModelFacts {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const selection = useUi((s) => s.selection);
  const storey = useUi((s) => s.storeyId);
  return useMemo(() => ({
    nodes: nodes as ModelFacts['nodes'],
    selection: selected.length ? selected : selection ? [selection] : [],
    storey,
  }), [nodes, selected, selection, storey]);
}

/** One field drawn by the window (a question's, a palette's: `prefix` keeps their ids apart). */
export function Field({ f, value, set, facts, wrong, prefix = 'ask', onCommit }: {
  f: AskField; value: FieldState; set: (v: FieldState) => void; facts: ModelFacts; wrong: boolean; prefix?: string;
  /** A number or a text let go of (its slider released, Enter, the field left): a palette that writes its values on it. */
  onCommit?: () => void;
}) {
  const id = `${prefix}-${f.key}`;
  const label = <label htmlFor={id} className="script-ask-label" data-no-i18n>{f.label}</label>;
  const cls = wrong ? ' wrong' : '';
  switch (f.kind) {
    case 'number':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <span className="script-ask-number">
            {f.min !== undefined && f.max !== undefined && f.max > f.min && (
              <input type="range" className="script-ask-slider" min={f.min} max={f.max} step={f.step ?? (f.integer ? 1 : 'any')} aria-label={f.label}
                value={readNumber(String(value)) ?? f.min} onChange={(e) => set(e.target.value)} onPointerUp={onCommit} onKeyUp={onCommit} />
            )}
            <input id={id} type="text" inputMode="decimal" value={String(value)} onChange={(e) => set(e.target.value)} spellCheck={false}
              onBlur={onCommit} onKeyDown={(e) => { if (e.key === 'Enter') onCommit?.(); }} />
            {f.unit && <span className="muted" data-no-i18n>{f.unit}</span>}
          </span>
        </div>
      );
    case 'text':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          {f.multiline
            ? <textarea id={id} rows={3} value={String(value)} onChange={(e) => set(e.target.value)} />
            : <input id={id} type="text" value={String(value)} onChange={(e) => set(e.target.value)} onBlur={onCommit} onKeyDown={(e) => { if (e.key === 'Enter') onCommit?.(); }} />}
        </div>
      );
    case 'bool':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>
          <span />
          <label className="check"><input id={id} type="checkbox" checked={value === true} onChange={(e) => set(e.target.checked)} /> <span data-no-i18n>{f.label}</span></label>
        </div>
      );
    case 'choice':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <select id={id} value={String(value)} onChange={(e) => set(Number(e.target.value))} data-no-i18n>
            {f.choices!.map((c, i) => <option key={i} value={i}>{c.label}</option>)}
          </select>
        </div>
      );
    case 'storey':
    case 'type': {
      const list = f.kind === 'storey' ? storeysOf(facts) : typesOf(f.cls, facts);
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <select id={id} value={String(value)} onChange={(e) => set(e.target.value)}>
            {f.kind === 'storey' && f.required === false ? <option value="">The view's storey</option>
              : (f.kind === 'type' && f.required === false || !list.length) && <option value="">—</option>}
            {list.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name || x.id}</option>)}
          </select>
        </div>
      );
    }
    case 'element': {
      const ids = Array.isArray(value) ? value : [];
      const list = elementsOf(f, facts);
      const take = () => set(fromSelection(f, facts));
      const named = ids.map((i) => facts.nodes.get(i)?.name || i);
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <span className="script-ask-element">
            {!f.multiple && list.length > 0
              ? (
                <select id={id} value={ids[0] ?? ''} onChange={(e) => set(e.target.value ? [e.target.value] : [])}>
                  <option value="">—</option>
                  {list.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}
                </select>
              )
              : <span id={id} className="script-ask-picked" title={named.join(', ')}>
                  {ids.length === 0 ? <span className="muted">Nothing selected</span>
                    : ids.length === 1 ? <span data-no-i18n>{named[0]}</span>
                      : <span>{`${ids.length} elements`}</span>}
                </span>}
            <button type="button" className="btn" onClick={take} data-action="ask-use-selection" title="Select in the model, then take the selection">
              <MousePointerClick size={13} /> Use selection</button>
          </span>
        </div>
      );
    }
  }
}

