/**
 * Drafting standards → Tags: a door's or window's marker laid out (forum topic 205). The classic line across the wall with its
 * texts at both ends, or a bubble marker: a line from the frame's middle out to a bubble holding the mark, a text over the line
 * and one under it (the clear width and height, {clearWidth} over {clearHeight}), one at the line's origin (the fire rating)
 * and the inner one at its interior end (a window's sill). Lengths are the building's (cm at any scale), each change one
 * engine command; the plan draws what is set here.
 */

import { useEffect, useState } from 'react';
import { execute } from '../../engine/session';

/** The marker fields of a tag type entry (lengths in metres, as the engine keeps them). */
export interface MarkerEntry {
  id: string; name?: string; layout?: string; above?: string; below?: string; centre?: string;
  lineLength?: number; lineOffset?: number; lineInside?: number; bubbleSize?: number; centreOffset?: number; innerOffset?: number;
}

type LengthKey = 'lineLength' | 'lineOffset' | 'lineInside' | 'bubbleSize' | 'centreOffset' | 'innerOffset';

/** A length typed in cm (decimals with a point or a comma) as metres; '' is none (null); undefined when it does not read. */
export function parseCm(text: string, min = -1000, max = 1000): number | null | undefined {
  const t = text.trim().replace(',', '.');
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) && n >= min && n <= max ? Math.round(n * 100) / 10000 : undefined;
}

/** Metres shown in cm, to the tenth of a millimetre. */
export const cm = (m: number | undefined): string => (m == null ? '' : String(Math.round(m * 10000) / 100));

const LENGTHS: { key: LengthKey; label: string; title: string; placeholder: string; min: number }[] = [
  { key: 'lineLength', label: 'Line length cm', title: 'From the line\'s origin out to the bubble, in the building\'s centimetres (the drawing scales it)', placeholder: '60', min: 1 },
  { key: 'lineOffset', label: 'Line origin offset cm', title: 'The line\'s origin moved from the middle of the frame: out of the building (+) or into it (−)', placeholder: '0', min: -1000 },
  { key: 'lineInside', label: 'Line inside past the wall cm', title: 'The line runs on into the room, this far past the wall\'s interior face (a window\'s: 10); empty: it starts at its origin', placeholder: '', min: -1000 },
  { key: 'bubbleSize', label: 'Bubble diameter cm', title: 'The bubble round the mark at the line\'s outer end; 0: the mark alone', placeholder: '30', min: 0 },
  { key: 'centreOffset', label: 'Origin text offset cm', title: 'The text at the line\'s origin (the fire rating) moved further into the room', placeholder: '0', min: -1000 },
  { key: 'innerOffset', label: 'Inner text offset cm', title: 'The text at the line\'s interior end (a window\'s sill) moved further into the room', placeholder: '0', min: -1000 },
];

/** A door's or window's marker type: its layout and, for a bubble marker, its texts and lengths. */
export function MarkerEditor({ entry, onClose }: { entry: MarkerEntry; onClose: () => void }) {
  const [draft, setDraft] = useState<MarkerEntry>(entry);
  useEffect(() => { setDraft(entry); }, [entry]);
  const commit = (patch: Partial<MarkerEntry> | Record<string, unknown>) => {
    setDraft((d) => ({ ...d, ...patch }));
    void execute('standards.set', { kind: 'tag', entry: { id: entry.id, ...patch } });
  };
  const bubble = draft.layout === 'bubble';
  const text = (key: 'above' | 'below' | 'centre', label: string, title: string, placeholder: string) => (
    <>
      <dt title={title}>{label}</dt>
      <dd><input data-field={`marker-${key}`} key={`${key}${draft[key] ?? ''}`} defaultValue={draft[key] ?? ''} placeholder={placeholder} data-no-i18n
        onBlur={(e) => { const t = e.target.value.trim(); if (t !== (draft[key] ?? '')) commit({ [key]: t }); }} /></dd>
    </>
  );
  return (
    <section className="buildup-editor" data-marker-editor={entry.id}>
      <header className="row"><strong data-no-i18n>{draft.name}</strong><span className="grow" /><button type="button" className="btn mini" onClick={onClose}>Close</button></header>
      <dl className="fields buildup-fields">
        <dt title="Across the wall past both faces with its texts at the ends, or out of the frame's middle to a bubble with the mark, texts over and under the line">Marker layout</dt>
        <dd><select data-field="marker-layout" value={bubble ? 'bubble' : 'line'} onChange={(e) => commit({ layout: e.target.value })}>
          <option value="line">line across the wall</option><option value="bubble">line to a bubble</option>
        </select></dd>
        {bubble && <>
          {text('above', 'Over the line', 'Written along the line, over it as it reads, whichever way the door turns: {clearWidth}, {openingWidth}, {width}…', '{clearWidth}')}
          {text('below', 'Under the line', 'Written along the line, under it: {clearHeight}, {openingHeight}, {height}…', '{clearHeight}')}
          {text('centre', 'At the origin', 'Written at the line\'s origin, the middle of the frame: {FireRating}; nothing when the door has none', '{FireRating}')}
          {LENGTHS.map((l) => (
            <span key={l.key} style={{ display: 'contents' }}>
              <dt title={l.title}>{l.label}</dt>
              <dd><input className="num" data-field={`marker-${l.key}`} key={`${l.key}${draft[l.key] ?? ''}`} defaultValue={cm(draft[l.key])} placeholder={l.placeholder}
                onBlur={(e) => {
                  const v = parseCm(e.target.value, l.min);
                  if (v === undefined || v === (draft[l.key] ?? null)) return;
                  commit({ [l.key]: v });
                }} /></dd>
            </span>
          ))}
        </>}
      </dl>
      <p className="muted small">{'The mark goes in the bubble (Text (outside)), the inner text at the line\'s interior end (Inside). Sizes: {width}, {height} (the door\'s or window\'s), {openingWidth}, {openingHeight} (the wall opening), {clearWidth}, {clearHeight} (inside the frame), {sill}, {sillFinished} (over the finished floor), in the tag type\'s unit.'}</p>
    </section>
  );
}
