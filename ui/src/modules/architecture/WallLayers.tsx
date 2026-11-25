/**
 * A wall in the inspector: its type (and the button to edit types), its
 * layers as its own usage lays them, its length and thickness (each wall's
 * own, report #107), and Flip.
 */

import { FlipHorizontal2, Mountain, Settings2 } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import { materialColor, materialName, ofClass, typeLayers, typeOf, usageOf, wallLength } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { lengthText } from '../../engine/unitsStore';
import { sameKind } from '../../shell/params';
import { LengthField } from '../../shell/cells';
import { WallTopFields } from './WallTop';
import { openWallProfile } from './wallProfileBoard';
import { WallReferenceField } from './WallReferenceField';
import { openTypeDialog } from '../../shell/typeToEdit';
import { WRAP_TIP, WrapEndsField, wrapOf } from './WrapEndsField';
import { WallLeanFields } from './WallLeanFields';

/** The finished floor field's tip, one phrase for the translations. */
export const FLOOR_TIP = 'The top of the finished floor over this wall\'s base (0 to 1 m), where a window or door sill measured from the finished floor starts. Empty: its wall type\'s, else 0.';

const THICKNESS_TIP = 'The wall\'s thickness comes from its type\'s layers. Type a new one and the selected walls take the type with the same build-up and that thickness (the core resized, the finishes kept): the project\'s, or a new one named after their type, e.g. Partition wall 150mm.';

/** A straight wall's end moved along its axis so the wall is `length` long. */
function setLength(node: NodeDto, length: number) {
  const [x1, y1] = node.props.start as number[];
  const [x2, y2] = node.props.end as number[];
  const k = length / Math.hypot(x2 - x1, y2 - y1);
  return execute('architecture.updateWall', { id: node.id, x2: x1 + (x2 - x1) * k, y2: y1 + (y2 - y1) * k });
}

export function WallLayers({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const types = ofClass(model, 'IfcWallType');
  const layers = type ? typeLayers(model, type.id) : [];
  const usage = usageOf(model, node.id);
  const total = layers.reduce((s, l) => s + l.thickness, 0);
  const straight = !node.props.arc && Array.isArray(node.props.start) && Array.isArray(node.props.end);
  // Layers are listed from the face right of the drawn direction; a flipped wall lays them from the left one.
  const shown = usage?.negative ? [...layers].reverse() : layers;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setWallType', { ids: sameKind(node), typeId: e.target.value })}>
            {!type && <option value="">(none)</option>}
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Wall types…" onClick={() => openTypeDialog('wallTypes', type?.id)}><Settings2 size={14} /></button>
        </span>
      </label>
      {layers.length > 0 && (
        <>
          <div className="layer-bar" title="Right face → left face">
            {/* A layer of no thickness (a membrane, paint) as a thin dark line where it lies. */}
            {shown.map((l, i) => <span key={i} style={l.thickness > 0 ? { flex: l.thickness, background: materialColor(model, l.material) } : { flex: '0 0 2px', background: 'var(--ink)' }} title={materialName(model, l.material)} />)}
          </div>
          <table>
            <tbody>
              {shown.map((l, i) => (
                <tr key={i}>
                  <td><span className="swatch" style={{ background: materialColor(model, l.material) }} />{l.name ?? materialName(model, l.material)}{l.core && <span className="chip">core</span>}</td>
                  <td className="num">{lengthText(l.thickness)}</td>
                </tr>
              ))}
              <tr className="total"><td>Total</td><td className="num">{lengthText(total)}</td></tr>
            </tbody>
          </table>
        </>
      )}
      <dl className="fields">
        {/* Each wall's own length (report #107): a straight wall's end moved along it; an arc's measured here, changed by its grips. */}
        <dt title="Its end moves along it">Length</dt>
        {straight && node.editable
          ? <dd><LengthField metres={wallLength(node)} min={1e-3} testId="wall-length" onCommit={(l) => { if (l != null) void setLength(node, l); }} /></dd>
          : <dd className="num">{lengthText(wallLength(node))}</dd>}
        {/* Its thickness (report #107): its type's build-up; a new one gives it the type with that thickness, made when the project has none. */}
        <dt title={THICKNESS_TIP}>Thickness</dt>
        {node.editable
          ? <dd><LengthField metres={layers.length ? total : typeof node.props.thickness === 'number' ? node.props.thickness : null} min={1e-3} testId="wall-thickness" title={THICKNESS_TIP}
              onCommit={(t) => { if (t != null) void execute('architecture.setWallThickness', { ids: sameKind(node), thickness: t }); }} /></dd>
          : <dd className="num">{lengthText(total)}</dd>}
        {/* Forum topic 192: the finished floor over the wall's base, which a sill measured from it starts at; empty: its type's. */}
        <dt title={FLOOR_TIP}>Finished floor above base</dt>
        <dd><LengthField metres={typeof node.props.finishedFloor === 'number' ? node.props.finishedFloor : null} min={0} max={1} blank testId="wall-finished-floor" readOnly={!node.editable}
          placeholder={typeof type?.props.finishedFloor === 'number' ? lengthText(type.props.finishedFloor) : undefined} title={FLOOR_TIP}
          onCommit={(m) => void execute('architecture.updateWall', { id: node.id, finishedFloor: m })} /></dd>
        {/* In-app report #192: which face's finish turns its free ends; empty: its type's. */}
        <dt title={WRAP_TIP}>Wrap at free ends</dt>
        <dd><WrapEndsField inherit value={wrapOf(node.props.wrapEnds)} disabled={!node.editable}
          onChange={(v) => void execute('architecture.updateWall', { id: node.id, wrapEnds: v })} /></dd>
        {/* In-app report #191: its lean from vertical (several walls at once); empty: its type's. A straight wall's only. */}
        {straight && <WallLeanFields node={node} type={type} />}
        <dt>Layers run</dt><dd>{usage ? (usage.negative ? 'flipped' : 'as drawn') : '—'}</dd>
        {/* The line it keeps besides its axis (forum topic 159, decision 685). */}
        <WallReferenceField node={node} layers={layers} name={(l) => l.name ?? materialName(model, l.material)} />
      </dl>
      {/* Where it goes up to (the forum's topic #45): the storey above, a storey, or a height of its own. */}
      <WallTopFields node={node} />
      {node.editable && (
        <span className="row" style={{ gap: 6 }}>
          <button type="button" className="btn" title="Swap the wall's faces (its ends stay)" onClick={() => void execute('architecture.flipWall', { ids: sameKind(node) })}>
            <FlipHorizontal2 size={14} />Flip
          </button>
          {!node.props.arc && <button type="button" className="btn" data-action="wall-profile" title="Its top drawn in elevation: a gable, a slope, steps (decision 372)" onClick={() => void openWallProfile(node.id)}>
            <Mountain size={14} />Profile…
          </button>}
        </span>
      )}
    </div>
  );
}
