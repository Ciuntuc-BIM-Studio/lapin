/**
 * Drafting standards → Tags: a tag type's leader (decision 331), as ArchiCAD's labels. Its shape (straight from a box,
 * or an elbow: an oblique line to a knee, then a shoulder along the text), what the shoulder underlines (the first row,
 * each, the last, none) and how much further it reaches; beside them the engine's own drawing of the type
 * (drafting.tagPreview), so what is seen is what views, sheets and the PDF draw. A build-up's editor shows them too.
 */

import { useEffect, useState } from 'react';
import { execute, query, useSession } from '../../engine/session';

/** The leader fields of a tag type entry. */
export interface LeaderEntry { id: string; name?: string; leaderShape?: string; underline?: string; extension?: number }

interface TagPreview {
  shape: string; textHeight: number; bold?: boolean; bounds: number[];
  segments: number[][]; texts: { x: number; y: number; text: string; anchor: 'start' | 'middle' | 'end' }[];
  strip?: { x: number; width: number; y0: number; y1: number; color: string }[];
}

const UNDERLINES: [string, string][] = [['first', 'the first row'], ['each', 'each row'], ['last', 'the last row'], ['none', 'nothing (it stops at the text)']];

/** A paper length typed (mm, 0–100); null when it is not one. */
export function parseExtension(text: string): number | null {
  const t = text.trim().replace(',', '.');
  if (t === '') return 0;
  const n = Number(t);
  return Number.isFinite(n) && n >= 0 && n <= 100 ? Math.round(n * 10) / 10 : null;
}

/** The leader's rows of a type's fields: shape, underline, extension (each change one engine command through `commit`). */
export function LeaderFields({ draft, commit }: { draft: LeaderEntry; commit: (patch: Partial<LeaderEntry>) => void }) {
  const elbow = draft.leaderShape !== 'straight';
  return (
    <>
      <dt title="Straight from a box round the text, or an elbow: an oblique line to a knee, then along under the text, no box">Leader shape</dt>
      <dd><select data-field="tag-leader-shape" value={elbow ? 'elbow' : 'straight'} onChange={(e) => commit({ leaderShape: e.target.value })}>
        <option value="elbow">elbow, under the text</option><option value="straight">straight, from a box</option>
      </select></dd>
      {elbow && <>
        <dt title="What the leader's horizontal part runs under; with leaders per layer each ends under its own row (nothing: at its middle)">Underline</dt>
        <dd><select data-field="tag-underline" value={draft.underline ?? 'first'} onChange={(e) => commit({ underline: e.target.value })}>
          {UNDERLINES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
        </select></dd>
        <dt title="How much further the shoulder reaches from the text towards the knee, on paper">Extension mm</dt>
        <dd><input className="num" data-field="tag-extension" key={`x${draft.extension ?? ''}`} defaultValue={String(draft.extension ?? 0)}
          onBlur={(e) => { const n = parseExtension(e.target.value); if (n != null && n !== (draft.extension ?? 0)) commit({ extension: n }); }} /></dd>
      </>}
    </>
  );
}

/** The type as the engine draws it, on paper; `entry` is the draft, read again as it changes; `sample` a build-up's layered type. */
export function LeaderPreview({ entry, sample }: { entry: LeaderEntry; sample?: string }) {
  const nodes = useSession((s) => s.nodes);
  const [p, setP] = useState<TagPreview | null>(null);
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => {
      void query<TagPreview>('drafting.tagPreview', { entry, ...(sample ? { type: sample } : {}) }).then((r) => { if (live && r) setP(r); });
    }, 120);
    return () => { live = false; clearTimeout(t); };
  }, [entry, sample, nodes]);
  if (!p) return null;
  const [x0, y0, x1, y1] = p.bounds;
  const m = 3;
  const fs = p.textHeight / 0.718;
  return (
    <svg className="buildup-preview-svg" viewBox={`${x0 - m} ${y0 - m} ${x1 - x0 + 2 * m} ${y1 - y0 + 2 * m}`} data-field="tag-preview" role="img" aria-label="Preview">
      <rect x={x0 - m} y={y0 - m} width={x1 - x0 + 2 * m} height={y1 - y0 + 2 * m} className="paper" />
      {(p.strip ?? []).map((b, i) => (b.y1 - b.y0 > 0.05
        ? <rect key={i} x={b.x} y={b.y0} width={b.width} height={b.y1 - b.y0} fill={b.color} stroke="#222" strokeWidth={0.15} />
        : <line key={i} x1={b.x} x2={b.x + b.width} y1={b.y0} y2={b.y0} stroke="#222" strokeWidth={0.35} />))}
      <path d={p.segments.map((s) => `M${s[0]} ${s[1]}L${s[2]} ${s[3]}`).join('')} stroke="#111" strokeWidth={0.18} fill="none" />
      {p.texts.map((t, i) => (
        <text key={i} x={t.x} y={t.y} fontSize={fs} textAnchor={t.anchor} dominantBaseline={t.anchor === 'middle' ? 'middle' : undefined}
          fontFamily="Helvetica, Arial, sans-serif" fontWeight={p.bold ? 700 : 400} fill="#111" data-no-i18n>{t.text}</text>
      ))}
    </svg>
  );
}

/** A plain tag type's leader: its fields beside its preview. */
export function LeaderEditor({ entry, onClose }: { entry: LeaderEntry; onClose: () => void }) {
  const [draft, setDraft] = useState<LeaderEntry>(entry);
  useEffect(() => { setDraft(entry); }, [entry]);
  const commit = (patch: Partial<LeaderEntry>) => {
    setDraft((d) => ({ ...d, ...patch }));
    void execute('standards.set', { kind: 'tag', entry: { id: entry.id, ...patch } });
  };
  return (
    <section className="buildup-editor" data-leader-editor={entry.id}>
      <header className="row"><strong data-no-i18n>{draft.name}</strong><span className="grow" /><button type="button" className="btn mini" onClick={onClose}>Close</button></header>
      <div className="buildup-editor-body">
        <dl className="fields buildup-fields"><LeaderFields draft={draft} commit={commit} /></dl>
        <figure className="buildup-preview"><LeaderPreview entry={draft} /></figure>
      </div>
    </section>
  );
}
