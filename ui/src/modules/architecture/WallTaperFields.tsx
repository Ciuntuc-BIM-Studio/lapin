/**
 * A wall's taper (docs/plan-wall-lean-taper.md, stage L2; in-app report #191): its thickness at the top, the face that stays
 * plumb and the layer that takes the change. On a wall (its own; empty: its type's; several selected walls at once, through
 * element.update) and on a wall type (its walls' default). The engine builds it (`WallTaper`): the other layers keep their
 * thickness and follow their face, the joins follow the faces' planes.
 */

import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { LengthField } from '../../shell/cells';
import { sameKind } from '../../shell/params';
import { lengthText } from '../../engine/unitsStore';

export const TAPER_TIP = 'The wall\'s thickness at its top: it tapers from its base to it, one face plumb and the other leaning (a retaining or foundation wall). Empty: its type\'s, else the same as at the base. Not yet for curved walls, walls rising to a roof or with a top profile, wrapped ends or layer extents; a tapered wall does not lean as well.';
export const PLUMB_TIP = 'The face that stays vertical when the wall tapers: the first layer\'s (its right as drawn, usually the exterior) or the last layer\'s. The other face leans.';
export const TAPER_LAYER_TIP = 'The layer that takes the whole change of thickness: the core unless you choose another. Every other layer keeps its thickness and follows its face.';

export type PlumbFace = 'first' | 'last';

/** The three fields as a type's dialog keeps them. */
export interface TaperValues { thicknessTop: number | null; plumbFace: PlumbFace | null; taperLayer: number | null }

export const NO_TAPER: TaperValues = { thicknessTop: null, plumbFace: null, taperLayer: null };

const faceOf = (v: unknown): PlumbFace | null => (v === 'first' || v === 'last' ? v : null);

export const taperValues = (props: Record<string, unknown>): TaperValues => ({
  thicknessTop: typeof props.thicknessTop === 'number' ? props.thicknessTop : null,
  plumbFace: faceOf(props.plumbFace),
  taperLayer: typeof props.taperLayer === 'number' ? props.taperLayer : null,
});

function FaceSelect({ value, onChange, inherit, disabled, varies }: { value: PlumbFace | null; onChange: (v: PlumbFace | null) => void; inherit?: boolean; disabled?: boolean; varies?: boolean }) {
  return (
    <select className="cell" data-testid="wall-plumb-face" title={PLUMB_TIP} value={varies ? '~' : value ?? (inherit ? '' : 'first')} disabled={disabled}
      onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as PlumbFace))}>
      {varies && <option value="~" disabled>varies</option>}
      {inherit && <option value="">As its type</option>}
      <option value="first">Layer 1's face (R)</option>
      <option value="last">Last layer's face (L)</option>
    </select>
  );
}

/** The layer that tapers: the core (empty), or one of the layers by its place from the first. */
function LayerSelect({ value, onChange, layers, inherit, disabled, varies }: { value: number | null; onChange: (v: number | null) => void; layers: string[]; inherit?: boolean; disabled?: boolean; varies?: boolean }) {
  return (
    <select className="cell" data-testid="wall-taper-layer" title={TAPER_LAYER_TIP} value={varies ? '~' : value == null ? '' : String(value)} disabled={disabled}
      onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}>
      {varies && <option value="~" disabled>varies</option>}
      <option value="">{inherit ? 'As its type' : 'Core'}</option>
      {layers.map((name, i) => <option key={i} value={String(i)}>{`${i + 1}. ${name}`}</option>)}
      {value != null && value >= layers.length && <option value={String(value)}>{`${value + 1}`}</option>}
    </select>
  );
}

/** The wall type dialog's fields: its walls' thickness at the top, the face that stays plumb, the layer that tapers. */
export function TypeTaperFields({ value, onChange, layers }: { value: TaperValues; onChange: (v: TaperValues) => void; layers: string[] }) {
  return (
    <>
      <label className="field-line"><span className="eyebrow" title={TAPER_TIP}>Thickness at top</span>
        <LengthField metres={value.thicknessTop} min={0.01} max={10} blank testId="type-thickness-top" title={TAPER_TIP}
          onCommit={(m) => onChange({ ...value, thicknessTop: m })} />
      </label>
      {value.thicknessTop != null && (
        <>
          <label className="field-line"><span className="eyebrow" title={PLUMB_TIP}>Face that stays plumb</span><FaceSelect value={value.plumbFace} onChange={(plumbFace) => onChange({ ...value, plumbFace })} /></label>
          <label className="field-line"><span className="eyebrow" title={TAPER_LAYER_TIP}>Layer that tapers</span><LayerSelect value={value.taperLayer} layers={layers} onChange={(taperLayer) => onChange({ ...value, taperLayer })} /></label>
        </>
      )}
    </>
  );
}

/** A wall's own taper in its inspector: several selected walls take the same value (one step); empty gives each its type's. */
export function WallTaperFields({ node, type, layers }: { node: NodeDto; type: NodeDto | null | undefined; layers: string[] }) {
  const nodes = useSession((s) => s.nodes);
  const ids = sameKind(node);
  const all = ids.map((id) => nodes.get(id)?.props ?? {});
  const shared = (key: string) => all.every((p) => JSON.stringify(p[key] ?? null) === JSON.stringify(node.props[key] ?? null));
  const set = (attrs: Record<string, unknown>) => void execute('element.update', { ids, attrs });
  const own = taperValues(node.props);
  const typed = taperValues(type?.props ?? {});
  const effective = own.thicknessTop ?? typed.thicknessTop;
  const disabled = !node.editable;
  return (
    <>
      <dt title={TAPER_TIP}>Thickness at top</dt>
      <dd>
        <LengthField metres={shared('thicknessTop') ? own.thicknessTop : null} min={0.01} max={10} blank testId="wall-thickness-top" readOnly={disabled} title={TAPER_TIP}
          placeholder={!shared('thicknessTop') ? 'varies' : typed.thicknessTop != null ? lengthText(typed.thicknessTop) : undefined}
          onCommit={(m) => set({ thicknessTop: m })} />
      </dd>
      {(effective != null || !shared('thicknessTop')) && (
        <>
          <dt title={PLUMB_TIP}>Face that stays plumb</dt>
          <dd><FaceSelect inherit value={own.plumbFace} varies={!shared('plumbFace')} disabled={disabled} onChange={(v) => set({ plumbFace: v })} /></dd>
          <dt title={TAPER_LAYER_TIP}>Layer that tapers</dt>
          <dd><LayerSelect inherit value={own.taperLayer} layers={layers} varies={!shared('taperLayer')} disabled={disabled} onChange={(v) => set({ taperLayer: v })} /></dd>
        </>
      )}
    </>
  );
}
