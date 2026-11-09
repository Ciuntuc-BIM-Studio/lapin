/**
 * Entourage (docs/plan-visualisation.md, stage 4): people and cars that give a site its scale, after BimGo's figures
 * (docs/acknowledgements.md). The Entourage tool puts one at a click, Entourage row a line of them (cars parked
 * across it, people walking along it); the kind is chosen in the command line's bar while the tool is on, and in
 * Properties afterwards with its turn, height, height above the ground and colour.
 */

import { create } from 'zustand';
import { CarFront, PersonStanding } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { NumberField } from '../../shell/cells';
import { Mm } from './SiteSections';

export const ENTOURAGE_KINDS = [
  { key: 'man', label: 'Man standing', category: 'person', height: 1.78, color: '#3e5c7a' },
  { key: 'woman', label: 'Woman walking', category: 'person', height: 1.66, color: '#b5483b' },
  { key: 'child', label: 'Child', category: 'person', height: 1.15, color: '#e0a93b' },
  { key: 'phone', label: 'Person on the phone', category: 'person', height: 1.72, color: '#6b7f5a' },
  { key: 'car', label: 'Car', category: 'car', height: 1.45, color: '#8c96a0' },
  { key: 'suv', label: 'SUV / van', category: 'car', height: 1.85, color: '#5c6e7e' },
] as const;

export type EntourageKind = (typeof ENTOURAGE_KINDS)[number]['key'];

export const kindOf = (key: unknown) => ENTOURAGE_KINDS.find((k) => k.key === key) ?? ENTOURAGE_KINDS[0];

export interface ProjectKind { id: string; name: string; height: number; width: number; category: string; color: string }

export const PROJECT_KIND_CLASS = 'LapinEntourageKind';

export function projectKinds(nodes: Map<string, NodeDto>): ProjectKind[] {
  const out: ProjectKind[] = [];
  for (const n of nodes.values()) {
    if (n.type !== PROJECT_KIND_CLASS) continue;
    const height = typeof n.props.height === 'number' ? n.props.height : 1.75;
    out.push({
      id: n.id, name: n.name, height,
      width: typeof n.props.width === 'number' ? n.props.width : 0.7,
      category: typeof n.props.category === 'string' ? n.props.category : 'person',
      color: '#c8ccd2',
    });
  }
  return out.sort((a, b) => a.name.localeCompare(b.name));
}

export const useProjectKinds = () => projectKinds(useSession((s) => s.nodes));

/** The kind the tools put down, kept for the session. */
export const useEntouragePrefs = create<{ kind: EntourageKind }>(() => ({ kind: 'man' }));

function KindSelect({ value, onChange, testId, pictures }: { value: string; onChange: (k: string) => void; testId?: string; pictures: ProjectKind[] }) {
  return (
    <select value={value} data-testid={testId} onChange={(e) => onChange(e.target.value)}>
      <optgroup label="People">{ENTOURAGE_KINDS.filter((k) => k.category === 'person').map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}</optgroup>
      <optgroup label="Cars">{ENTOURAGE_KINDS.filter((k) => k.category === 'car').map((k) => <option key={k.key} value={k.key}>{k.label}</option>)}</optgroup>
      {pictures.length > 0 && <optgroup label="Pictures">{pictures.map((k) => <option key={k.id} value={k.id}>{k.name}</option>)}</optgroup>}
    </select>
  );
}

/** The kind, in the command line's bar while an entourage tool is on. */
function KindOption() {
  const kind = useEntouragePrefs((s) => s.kind);
  const pictures = useProjectKinds();
  return <span className="cad-layer on"><KindSelect value={kind} testId="entourage-kind" pictures={pictures} onChange={(k) => useEntouragePrefs.setState({ kind: k as EntourageKind })} /></span>;
}

/** One person or car at a click, of the kind in the bar; turned afterwards (RO, or its rotation in Properties). */
export const entourage: ToolDef = {
  id: 'civil.entourage', label: 'Entourage', icon: PersonStanding, shortcut: 'EN', options: KindOption,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('civil.addEntourage', { storeyId: ctx.storeyId, kind: useEntouragePrefs.getState().kind, x: p.x, y: p.y }); },
};

/** A row along a line: cars every 2.5 m parked across it (their fronts to its left), people every 1.2 m walking along it. */
export const entourageRow: ToolDef = {
  id: 'civil.entourageRow', label: 'Entourage row', icon: CarFront, shortcut: 'ER', options: KindOption,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => {
    await ctx.execute('civil.addEntourage', { storeyId: ctx.storeyId, kind: useEntouragePrefs.getState().kind, points: [[a.x, a.y], [b.x, b.y]] });
  },
};

/** A person's or car's properties: its kind, turn, height, height above the ground and colour. */
export function EntourageSection({ node }: { node: NodeDto }) {
  const pictures = useProjectKinds();
  const picture = pictures.find((p) => p.id === node.props.kind);
  const kind = picture ?? kindOf(node.props.kind);
  const update = (attrs: Record<string, unknown>) => void execute('element.update', { ids: [node.id], attrs });
  const height = typeof node.props.height === 'number' ? node.props.height : kind.height;
  return (
    <dl className="fields">
      <dt>Kind</dt><dd><KindSelect value={kind === picture ? picture.id : kindOf(node.props.kind).key} pictures={pictures} onChange={(k) => update({ kind: k, height: null })} /></dd>
      <dt>Rotation</dt><dd><NumberField value={typeof node.props.rotation === 'number' ? node.props.rotation : 0} suffix="°" onCommit={(v) => { if (v != null) update({ rotation: v }); }} /></dd>
      <dt>Height</dt><dd><Mm value={height} min={300} onCommit={(v) => update({ height: v })} /></dd>
      <dt title="Raised above the ground under it (a deck, a balcony)">Above the ground</dt><dd><Mm value={node.props.offset} fallback={0} onCommit={(v) => update({ offset: v })} /></dd>
      <dt title={kind.category === 'car' ? 'The body' : 'The clothes'}>Colour</dt>
      <dd><input type="color" value={typeof node.props.color === 'string' ? node.props.color : kind.color} onChange={(e) => update({ color: e.target.value })} /></dd>
    </dl>
  );
}
