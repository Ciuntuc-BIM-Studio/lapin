/**
 * Suggestions while typing (suggest.ts): a list under the input of what fits where the caret is, asked of the engine as the
 * text changes inside braces. Arrow keys move through it, Enter or Tab accepts, Esc closes it, typing filters it; a click
 * accepts too. An accepted item that leads on (`el.`, `schedule.Rooms.`) asks again at once. Used by the table editor's
 * cells and formula bar; any text input with `{fields}` can take it (`useSuggest`, then `update` on change, `onKeyDown`, `close` on blur).
 */

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
import { createPortal, flushSync } from 'react-dom';
import { accept, inBraces, keyOf, leadsOn, stepIndex, type SuggestItem, type SuggestResult } from './suggest';

type Field = HTMLTextAreaElement | HTMLInputElement;

export interface Suggest {
  /** The text or its caret changed: asks again (only inside braces). */
  update: (el: Field) => void;
  /** A key in the input: true when the open list took it (the input then does nothing more with it). */
  onKeyDown: (e: KeyboardEvent<Field>) => boolean;
  close: () => void;
  open: boolean;
  popup: JSX.Element | null;
}

/** `ask` gives the suggestions for a text and its caret; `set` puts an accepted one's text into the input (its state). */
export function useSuggest(ask: (text: string, caret: number) => Promise<SuggestResult | null>, set: (text: string) => void, delay = 80): Suggest {
  const [result, setResult] = useState<SuggestResult | null>(null);
  const [index, setIndex] = useState(0);
  const anchor = useRef<Field | null>(null);
  const seq = useRef(0);
  const timer = useRef<number | undefined>(undefined);
  const askRef = useRef(ask);
  askRef.current = ask;
  const setRef = useRef(set);
  setRef.current = set;

  const close = useCallback(() => { seq.current++; window.clearTimeout(timer.current); setResult(null); }, []);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  const update = useCallback((el: Field) => {
    const text = el.value, caret = el.selectionStart ?? text.length;
    window.clearTimeout(timer.current);
    const n = ++seq.current;
    if (!inBraces(text, caret)) { setResult(null); return; }
    anchor.current = el;
    timer.current = window.setTimeout(() => {
      void askRef.current(text, caret).then((r) => {
        if (n !== seq.current) return;
        setResult(r && (r.items.length > 0 || r.note) ? r : null);
        setIndex(0);
      });
    }, delay);
  }, [delay]);

  const take = (item: SuggestItem) => {
    const el = anchor.current;
    if (!el || !result) return;
    const next = accept(el.value, result, item);
    // The input shows the text at once (not a frame later, when the next key may have gone in), the caret after what went in;
    // an item that leads on asks again.
    flushSync(() => setRef.current(next.text));
    close();
    el.focus();
    el.setSelectionRange(next.caret, next.caret);
    if (leadsOn(item)) update(el);
  };

  const onKeyDown = (e: KeyboardEvent<Field>): boolean => {
    if (!result) return false;
    const k = keyOf(e.key, e.shiftKey);
    if (!k) return false;
    // Only a note (the cell reads no element): the keys go on to the input, the note closes.
    if (k !== 'close' && result.items.length === 0) { close(); return false; }
    e.preventDefault();
    e.stopPropagation();
    if (k === 'down') setIndex((i) => stepIndex(i, result.items.length, 1));
    else if (k === 'up') setIndex((i) => stepIndex(i, result.items.length, -1));
    else if (k === 'close') close();
    else take(result.items[Math.min(index, result.items.length - 1)]);
    return true;
  };

  // Where the list goes: under the input, or over it when there is no room below.
  const [box, setBox] = useState<{ left: number; top?: number; bottom?: number; width: number } | null>(null);
  useLayoutEffect(() => {
    if (!result || !anchor.current) { setBox(null); return; }
    const r = anchor.current.getBoundingClientRect();
    const below = window.innerHeight - r.bottom;
    const width = Math.max(260, Math.min(420, r.width));
    const left = Math.max(8, Math.min(r.left, window.innerWidth - width - 8));
    setBox(below >= 180 || below >= r.top ? { left, top: r.bottom + 2, width } : { left, bottom: window.innerHeight - r.top + 2, width });
  }, [result]);
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => { list.current?.querySelector('[data-on]')?.scrollIntoView({ block: 'nearest' }); }, [index, result]);

  const popup = result && box ? createPortal(
    <div ref={list} role="listbox" data-testid="suggest-list" className="suggest-list"
      style={{ position: 'fixed', left: box.left, top: box.top, bottom: box.bottom, width: box.width, maxHeight: 220, overflowY: 'auto', zIndex: 1000,
        background: 'var(--panel, #fff)', color: 'var(--ink, #000)', border: '1px solid var(--rule-strong, #888)', borderRadius: 3, boxShadow: '0 4px 16px rgba(0,0,0,0.2)', fontSize: 12 }}>
      {result.items.length === 0 && result.note && <div className="muted" style={{ padding: '4px 8px' }}>{result.note}</div>}
      {result.items.map((it, i) => (
        <div key={it.insert} role="option" aria-selected={i === index} data-on={i === index ? '' : undefined} data-suggest={it.insert} data-no-i18n
          onMouseDown={(e) => { e.preventDefault(); take(it); }} onMouseEnter={() => setIndex(i)}
          style={{ display: 'flex', gap: 8, padding: '2px 8px', cursor: 'pointer', background: i === index ? 'var(--accent-soft, #dde8ff)' : undefined, whiteSpace: 'nowrap' }}>
          <span style={{ fontFamily: 'monospace' }}>{it.kind === 'function' ? `${it.label}()` : it.insert.endsWith('.') ? `${it.label}.` : it.label}</span>
          {it.detail && <span className="muted" style={{ marginLeft: 'auto', overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: '60%' }}>{it.detail}</span>}
        </div>
      ))}
    </div>, document.body) : null;

  return { update, close, open: !!result, onKeyDown, popup };
}
