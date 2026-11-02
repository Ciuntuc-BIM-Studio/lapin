/**
 * An opening alone in a wall, nothing filling it (a tester's report, decision 247): a passage, a hatch, a hole for
 * a later door. The tool cuts it where the wall is clicked, its width, height and sill set in Properties before the
 * click; selected, its size and place are changed there. An IfcOpeningElement voiding the wall, as a door's is.
 */

import { SquareDashed } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { ToolDef } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { relatedOf, relatingOf } from '../../engine/model';
import { LengthField } from '../../shell/cells';

/** The size the next opening takes, chosen before the click. */
function NewOpening() {
  const size = useUi((s) => s.openingSize);
  const set = (patch: Partial<typeof size>) => useUi.setState({ openingSize: { ...size, ...patch } });
  return (
    <dl className="fields pad" data-field="new-opening">
      <dt>Width</dt><dd><LengthField metres={size.width} min={0.05} onCommit={(m) => m != null && set({ width: m })} /></dd>
      <dt>Height</dt><dd><LengthField metres={size.height} min={0.05} onCommit={(m) => m != null && set({ height: m })} /></dd>
      <dt>Sill</dt><dd><LengthField metres={size.sill} min={0} onCommit={(m) => m != null && set({ sill: m })} /></dd>
    </dl>
  );
}

export const wallOpening: ToolDef = {
  id: 'architecture.wallOpening', label: 'Wall opening', icon: SquareDashed, shortcut: 'WO', properties: NewOpening,
  gesture: { kind: 'pick', types: ['IfcWall'] },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const { width, height, sill } = useUi.getState().openingSize;
    await ctx.execute('architecture.addWallOpening', { wallId: entity.id, x: p.x, y: p.y, width, height, sill });
  },
};

/** An opening in a wall with nothing filling it (a door's or window's is its fill's). */
export const isWallOpening = (n: NodeDto): boolean => {
  const model = useSession.getState();
  return relatingOf(model, n.id, 'IfcRelVoidsElement')?.type === 'IfcWall' && relatedOf(model, n.id, 'IfcRelFillsElement').length === 0;
};

const num = (n: NodeDto, k: string, d: number) => (typeof n.props[k] === 'number' ? (n.props[k] as number) : d);

/** A selected opening alone: its size, its place along the wall and its sill, each checked against the wall and its other openings. */
export function WallOpeningSection({ node }: { node: NodeDto }) {
  const set = (patch: Record<string, number>) => void execute('architecture.updateWallOpening', { id: node.id, ...patch });
  return (
    <dl className="fields pad" data-field="wall-opening">
      <dt>Width</dt><dd><LengthField metres={num(node, 'width', 1)} min={0.05} onCommit={(m) => m != null && set({ width: m })} /></dd>
      <dt>Height</dt><dd><LengthField metres={num(node, 'height', 2)} min={0.05} onCommit={(m) => m != null && set({ height: m })} /></dd>
      <dt>Along the wall</dt><dd><LengthField metres={num(node, 'along', 0)} min={0} onCommit={(m) => m != null && set({ along: m })} /></dd>
      <dt>Sill</dt><dd><LengthField metres={num(node, 'sill', 0)} min={0} onCommit={(m) => m != null && set({ sill: m })} /></dd>
    </dl>
  );
}
