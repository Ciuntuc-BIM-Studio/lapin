/**
 * An opening alone in a wall, nothing filling it (a tester's report, decision 247): a passage, a hatch, a hole for
 * a later door. The tool cuts it where the wall is clicked, its width, height and sill set in Properties before the
 * click; selected, its size and place are changed there. An IfcOpeningElement voiding the wall, as a door's is.
 * Its head may be shaped (decision 253): a segmental or elliptical arch, a semicircle, rounded top corners.
 */

import { AppWindow, DoorOpen, Settings2, SquareDashed } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { ToolDef } from '../../platform/module';
import { useUi, type OpeningHead } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { relatedOf, relatingOf } from '../../engine/model';
import { AlongField } from './AlongField';
import { LengthField } from '../../shell/cells';
import { sameKind } from '../../shell/params';
import { draftOf, openOpeningDialog, revealMaterial, revealThickness, REVEALS } from './OpeningDialog';

const HEADS: { kind: OpeningHead; label: string }[] = [
  { kind: 'rect', label: 'Rectangle' }, { kind: 'segmental', label: 'Segmental arch' }, { kind: 'elliptical', label: 'Elliptical arch' },
  { kind: 'round', label: 'Semicircle' }, { kind: 'radiused', label: 'Radiused corners' },
];

/** The rise a head takes when none is asked (as the engine's JoineryShapes.Rise): a segment's sixth of the width, an ellipse's quarter, corners of 150 mm. */
export const defaultRise = (kind: OpeningHead, width: number): number | null =>
  kind === 'segmental' ? width / 6 : kind === 'elliptical' ? width / 4 : kind === 'radiused' ? 0.15 : null;

/** The head's kind and, when it has one, its rise (a radiused head's corner radius); an empty rise is the kind's own. */
function HeadFields({ kind, rise, width, onChange }: { kind: OpeningHead; rise: number | null; width: number; onChange: (patch: { shape?: OpeningHead; rise: number | null }) => void }) {
  const fallback = defaultRise(kind, width);
  return (
    <>
      <dt>Head</dt>
      <dd><select data-field="opening-head" value={kind} onChange={(e) => onChange({ shape: e.target.value as OpeningHead, rise: null })}>
        {HEADS.map((h) => <option key={h.kind} value={h.kind}>{h.label}</option>)}
      </select></dd>
      {fallback != null && <>
        <dt>{kind === 'radiused' ? 'Corner radius' : 'Rise of the head'}</dt>
        <dd><LengthField metres={rise ?? fallback} min={0.01} testId="opening-rise" onCommit={(m) => onChange({ rise: m })} /></dd>
      </>}
    </>
  );
}

/** The size and head the next opening takes, chosen before the click. */
function NewOpening() {
  const size = useUi((s) => s.openingSize);
  const set = (patch: Partial<typeof size>) => useUi.setState({ openingSize: { ...size, ...patch } });
  return (
    <dl className="fields pad" data-field="new-opening">
      <dt>Width</dt><dd><LengthField metres={size.width} min={0.05} onCommit={(m) => m != null && set({ width: m })} /></dd>
      <dt>Height</dt><dd><LengthField metres={size.height} min={0.05} onCommit={(m) => m != null && set({ height: m })} /></dd>
      <dt>Sill</dt><dd><LengthField metres={size.sill} min={0} onCommit={(m) => m != null && set({ sill: m })} /></dd>
      <HeadFields kind={size.shape} rise={size.rise} width={size.width} onChange={set} />
    </dl>
  );
}

export const wallOpening: ToolDef = {
  id: 'architecture.wallOpening', label: 'Wall opening', icon: SquareDashed, shortcut: 'WO', properties: NewOpening,
  gesture: { kind: 'pick', types: ['IfcWall'] },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const { width, height, sill, shape, rise } = useUi.getState().openingSize;
    await ctx.execute('architecture.addWallOpening', { wallId: entity.id, x: p.x, y: p.y, width, height, sill, shape, ...(rise != null && { rise }) });
  },
};

/** An opening in a wall with nothing filling it (a door's or window's is its fill's). */
export const isWallOpening = (n: NodeDto): boolean => {
  const model = useSession.getState();
  return relatingOf(model, n.id, 'IfcRelVoidsElement')?.type === 'IfcWall' && relatedOf(model, n.id, 'IfcRelFillsElement').length === 0;
};

const num = (n: NodeDto, k: string, d: number) => (typeof n.props[k] === 'number' ? (n.props[k] as number) : d);

const headOf = (n: NodeDto): OpeningHead => {
  const k = n.props.shape;
  return k === 'segmental' || k === 'elliptical' || k === 'round' || k === 'radiused' ? k : 'rect';
};

/** The wall openings selected with this one (they change together, as doors and windows of a type do). */
const openingsWith = (node: NodeDto): string[] => {
  const nodes = useSession.getState().nodes;
  const ids = sameKind(node).filter((id) => { const n = nodes.get(id); return !!n && isWallOpening(n); });
  return ids.length ? ids : [node.id];
};

/**
 * A selected opening alone: its size, its place along the wall, its sill and its head, each checked against the wall and its
 * other openings; its reveal's finish and its lintel (decision 392); the configurator, and Change to door or window (report #114).
 */
export function WallOpeningSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (patch: Record<string, number | string | null>) => void execute('architecture.updateWallOpening', { ids: openingsWith(node), ...patch });
  const d = draftOf(node);
  const t = d.reveal === 'none' ? 0 : d.revealThickness ?? revealThickness(d.reveal);
  const materials = [...nodes.values()].filter((m) => m.type === 'IfcMaterial').map((m) => m.name).sort((a, b) => a.localeCompare(b));
  const material = d.revealMaterial ?? revealMaterial(d.reveal);
  return (
    <>
      <dl className="fields pad" data-field="wall-opening">
        <dt>Width</dt><dd><LengthField metres={num(node, 'width', 1)} min={0.05} onCommit={(m) => m != null && set({ width: m })} /></dd>
        <dt>Height</dt><dd><LengthField metres={num(node, 'height', 2)} min={0.05} onCommit={(m) => m != null && set({ height: m })} /></dd>
        <dt>Along the wall</dt><dd><AlongField id={node.id} along={num(node, 'along', 0)} width={num(node, 'width', 1)} /></dd>
        <dt>Sill</dt><dd><LengthField metres={num(node, 'sill', 0)} min={0} onCommit={(m) => m != null && set({ sill: m })} /></dd>
        <HeadFields kind={headOf(node)} rise={typeof node.props.rise === 'number' ? node.props.rise : null} width={num(node, 'width', 1)} onChange={set} />
        <dt>Reveal finish</dt>
        <dd><select data-field="opening-reveal" value={d.reveal} onChange={(e) => set({ reveal: e.target.value })}>
          {REVEALS.map((r) => <option key={r.kind} value={r.kind}>{r.label}</option>)}
        </select></dd>
        {d.reveal !== 'none' && <>
          <dt>Finish thickness</dt><dd><LengthField metres={t} min={0.001} testId="opening-reveal-thickness" onCommit={(m) => set({ revealThickness: m })} /></dd>
          <dt>Finish material</dt>
          <dd><select value={material} onChange={(e) => set({ revealMaterial: e.target.value })}>
            {[...new Set([material, ...materials])].map((m) => <option key={m} value={m}>{m}</option>)}
          </select></dd>
        </>}
        <dt>Lintel</dt>
        <dd><select data-field="opening-lintel" value={d.lintel} onChange={(e) => set({ lintel: e.target.value })}>
          <option value="wall">The wall’s rule</option><option value="none">None</option>
        </select></dd>
        {/* Through every wall along its own it passes (forum topic 64). */}
        <dt>Cut also the walls it passes through</dt><dd><input type="checkbox" data-field="cut-through" checked={node.props.cutThrough !== false} disabled={node.editable === false}
          title="Cuts each wall along its own that it reaches into (one stacked on it, a lining against it), not only the wall it is in"
          onChange={(e) => void execute('architecture.setCutThrough', { ids: openingsWith(node), on: e.target.checked })} /></dd>
      </dl>
      {node.editable !== false && (
        <div className="row pad">
          <button type="button" className="btn" data-action="opening-edit" onClick={() => openOpeningDialog(node.id, openingsWith(node))}><Settings2 size={14} />Edit the opening…</button>
          <button type="button" className="btn" data-action="opening-to-door" onClick={() => openOpeningDialog(node.id, [node.id], 'door')}><DoorOpen size={14} />Change to door…</button>
          <button type="button" className="btn" data-action="opening-to-window" onClick={() => openOpeningDialog(node.id, [node.id], 'window')}><AppWindow size={14} />Change to window…</button>
        </div>
      )}
    </>
  );
}
