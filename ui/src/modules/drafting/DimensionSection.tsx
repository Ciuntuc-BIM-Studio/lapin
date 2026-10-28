/**
 * A dimension in the inspector: what it measures, a value typed to drive it,
 * and, when it lost its element, freeing it. A string shows each segment's
 * value (typed: what follows moves too) and the text written over it, takes
 * points in and out, puts values moved by hand back on the line, and splits
 * into single dimensions; several selected that go on from one another join
 * into one string.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { select, useUi } from '../../app/store';
import { startCommand } from '../../viewport/cad/session';
import type { DimensionInfo } from '../../viewport/dimensionEdit';

export function DimensionSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const [d, setD] = useState<DimensionInfo | null>(null);
  useEffect(() => { void query<DimensionInfo>('drafting.dimension', { id: node.id }).then(setD); }, [node.id, nodes]);
  if (node.props.predefinedType !== 'DIMENSION' || !d) return null;
  const drive = (v: string, segment: number, now: number | null) => {
    const n = Number(v.replace(',', '.'));
    if (n > 0 && Math.abs(n - (now ?? 0)) > 1e-6) void execute('drafting.setDimensionValue', { id: node.id, value: n, segment });
  };
  const kind = d.kind ?? (node.props.dir ? 'linear' : 'aligned');
  const unit = kind === 'angular' ? '°' : kind === 'slope' ? 'ratio' : 'm';
  const set = (attrs: Record<string, unknown>) => void execute('element.update', { id: node.id, attrs });
  const values = d.values ?? [d.value];
  const texts = Array.isArray(node.props.texts) ? (node.props.texts as unknown[]) : [];
  const textOf = (i: number) => String((values.length > 1 ? texts[i] : texts[i] ?? node.props.text) ?? '');
  const string = !d.kind && values.length > 1;
  // The segments whose value was moved by hand (dragged by its grip).
  const moved = (Array.isArray(node.props.textAt) ? (node.props.textAt as unknown[]) : []).flatMap((o, i) => (Array.isArray(o) ? [i] : []));
  // Other linear dimensions selected with this one: offered to join into one string.
  const others = selected.filter((id) => id !== node.id && nodes.get(id)?.props.predefinedType === 'DIMENSION' && !nodes.get(id)?.props.dimKind);
  const title = d.driving ? 'Type a value: the element it measures moves (and, in a string, what follows)' : 'Neither end holds on an element';
  return (
    <dl className="fields">
      <dt>Kind</dt><dd>{string ? `${kind} · ${values.length} segments` : kind}</dd>
      {kind === 'slope' && <><dt>Written as</dt><dd><select value={String(node.props.format ?? 'auto')} onChange={(e) => set({ format: e.target.value })}>
        <option value="auto">auto (% under 15 %, else degrees)</option><option value="%">%</option><option value="deg">degrees</option><option value="ratio">1:n</option>
      </select></dd></>}
      {values.map((v, i) => (
        <SegmentRow key={`${i}:${v}`} label={string ? `Segment ${i + 1} (${unit})` : `Value (${unit})`} value={v} disabled={d.orphan || !(d.drives?.[i] ?? d.driving)} title={title}
          onValue={(s) => drive(s, i, v)} text={d.kind ? null : textOf(i)}
          onText={(t) => void execute('drafting.setDimensionText', { id: node.id, segment: i, text: t })} />
      ))}
      {!d.kind && <>
        <dt />
        <dd>
          <button type="button" title="Click points to put into the string, each where it falls along its line (DIMADD)" onClick={() => { select(node.id); void startCommand('DIMADD'); }}>Add points</button>
          {string && <button type="button" title="Click near points to take them out of the string; Ctrl/⌘-click on a point's grip does the same (DIMDEL)" onClick={() => { select(node.id); void startCommand('DIMDEL'); }}>Remove points</button>}
          {moved.length > 0 && <button type="button" title="Every value moved by hand back on its line (one: Ctrl/⌘-click its grip)"
            onClick={() => void Promise.all(moved.map((i) => execute('drafting.moveDimensionText', { id: node.id, segment: i, reset: true })))}>Texts back on the line</button>}
          {string && <button type="button" title="One single dimension per segment" onClick={() => void execute('drafting.splitDimension', { id: node.id }).then((r) => {
            const ids = (r as { ids?: string[] } | null)?.ids;
            if (ids?.length) select(ids[0]);
          })}>Split into single dimensions</button>}
          {others.length > 0 && <button type="button" title="The selected dimensions, going on from one another on one dimension line, as one string"
            onClick={() => void execute('drafting.joinDimensions', { ids: [node.id, ...others] }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) select(id); })}>Join into a string</button>}
        </dd>
      </>}
      {d.orphan && <>
        <dt />
        <dd className="error-text">It lost its element. <button type="button" onClick={() => void execute('drafting.freeDimensions', { ids: [node.id] })}>Free it</button></dd>
      </>}
    </dl>
  );
}

function SegmentRow({ label, value, disabled, title, onValue, text, onText }: {
  label: string; value: number | null; disabled: boolean; title: string; onValue: (v: string) => void; text: string | null; onText: (t: string) => void;
}) {
  return <>
    <dt>{label}</dt>
    <dd>
      <input defaultValue={value == null ? '∞' : value.toFixed(3)} disabled={disabled} title={title}
        onKeyDown={(e) => { if (e.key === 'Enter') onValue((e.target as HTMLInputElement).value); }} onBlur={(e) => onValue(e.target.value)} />
      {text != null && <input defaultValue={text} placeholder="text" title="Written over the segment instead of its measure (empty: the measure)"
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} onBlur={(e) => { if (e.target.value !== text) onText(e.target.value); }} />}
    </dd>
  </>;
}
