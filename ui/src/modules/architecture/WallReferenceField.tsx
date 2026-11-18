/**
 * A wall's reference line in Properties (forum topic 159, GitHub #63, decision 685): the line the wall keeps besides its axis, a
 * face of it (its core's, its finishes', a layer's) or an offset from its core's centre. Changing it moves the line, not the wall
 * (architecture.setWallReference, one undo step); the wall is then snapped and dragged by it and grows round it when its thickness
 * or type changes.
 */

import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Layer } from '../../engine/model';
import { LengthField } from '../../shell/cells';
import { sameKind } from '../../shell/params';
import { keptReference, type ReferenceKind } from '../../viewport/wallReference';

const TIP = 'The line the wall is snapped, moved and dragged by, and grows round when its thickness or type changes. Changing it moves the line, not the wall.';

/** The value of the field's choice: a kind, or `layer:i` for layer face i (0 the exterior face). */
export function choiceOf(props: Record<string, unknown>): string {
  const r = keptReference(props);
  if (!r) return 'coreCentre';
  return r.kind === 'layer' ? `layer:${r.layer ?? 0}` : r.kind;
}

/** What setWallReference is told for a choice (an offset kept as it was, else 0). */
export function argsOf(choice: string, offset: number): Record<string, unknown> {
  if (choice.startsWith('layer:')) return { kind: 'layer', layer: Number(choice.slice(6)) };
  return choice === 'offset' ? { kind: 'offset', offset } : { kind: choice as ReferenceKind };
}

export function WallReferenceField({ node, layers, name }: { node: NodeDto; layers: Layer[]; name: (l: Layer) => string }) {
  const r = keptReference(node.props);
  const choice = choiceOf(node.props);
  const offset = r?.kind === 'offset' ? r.offset ?? 0 : 0;
  const set = (args: Record<string, unknown>) => void execute('architecture.setWallReference', { ids: sameKind(node), ...args });
  // The faces between layers, counted from the exterior (the type's first layer): 0 its outer face, n its inner one.
  const faces = layers.length > 1 ? layers.slice(0, -1).map((l, i) => ({ i: i + 1, label: `${name(l)} | ${name(layers[i + 1])}` })) : [];
  return (
    <>
      <dt title={TIP}>Reference line</dt>
      <dd>
        <select value={choice} disabled={!node.editable} data-testid="wall-reference" title={TIP} onChange={(e) => set(argsOf(e.target.value, offset))}>
          <option value="coreCentre">Core centre (the axis)</option>
          <option value="coreExterior">Core, exterior face</option>
          <option value="coreInterior">Core, interior face</option>
          <option value="exteriorFace">Exterior face</option>
          <option value="interiorFace">Interior face</option>
          {faces.map((f) => <option key={f.i} value={`layer:${f.i}`}>{`Layer face: ${f.label}`}</option>)}
          <option value="offset">Offset from the core centre</option>
        </select>
      </dd>
      {choice === 'offset' && (
        <>
          <dt title="Positive to the left of the wall's direction, negative to the right (its exterior)">Offset</dt>
          <dd><LengthField metres={offset} testId="wall-reference-offset" readOnly={!node.editable} onCommit={(m) => { if (m != null) set({ kind: 'offset', offset: m }); }} /></dd>
        </>
      )}
    </>
  );
}
