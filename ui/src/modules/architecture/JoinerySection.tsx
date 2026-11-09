/**
 * A door or window in the inspector: its type (and the configurator), its
 * place on its wall (along it, its sill), and Flip — the hand (left ↔ right)
 * or the side (interior ↔ exterior).
 */

import { FlipHorizontal2, FlipVertical2, Settings2, SquareDashed } from 'lucide-react';
import { select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { mm, num, ofClass, relatedOf, relatingOf, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { LengthField } from '../../shell/cells';
import { sameKind } from '../../shell/params';
import { JoineryDataButton } from './JoineryData';
import type { Position } from './JoineryDialog';

/** Doors and windows flip together. */
const JOINERY = ['IfcDoor', 'IfcWindow'];

/** The doors and windows selected made wall openings of their holes, one step; the first opening selected. */
async function toOpening(node: NodeDto): Promise<void> {
  const r = await execute('architecture.fillToOpening', { ids: sameKind(node, JOINERY) }) as { ids?: string[] } | null;
  if (r?.ids?.[0]) select(r.ids[0]);
}

export function JoinerySection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const types = ofClass(model, node.type === 'IfcDoor' ? 'IfcDoorType' : 'IfcWindowType');
  const opening = relatingOf(model, node.id, 'IfcRelFillsElement');
  const wall = opening ? relatingOf(model, opening.id, 'IfcRelVoidsElement') : null;
  const spec = type?.props.spec as { width?: number; height?: number; position?: Position | null } | undefined;
  // The frame's place across the wall (report #76): the element's own over the type's.
  const ownPosition = node.props.position as Position | null | undefined;
  const position = ownPosition ?? spec?.position ?? null;
  const setPosition = (p: Position | null) => void execute('architecture.setJoineryPosition', p ? { ids: sameKind(node, JOINERY), ...p } : { ids: sameKind(node, JOINERY), clear: true });
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setJoineryType', { ids: sameKind(node), typeId: e.target.value })}>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Doors and windows…" onClick={() => useUi.setState({ dialog: 'joinery' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <dl className="fields">
        <dt>Size</dt><dd className="num">{mm(spec?.width ?? 0)} × {mm(spec?.height ?? 0)}</dd>
        <dt>In wall</dt><dd>{wall?.name ?? '—'}</dd>
        {opening && node.editable && <>
          <dt>Along the wall</dt><dd><LengthField metres={num(opening, 'along')} onCommit={(v) => { if (v != null) void execute('architecture.moveOpening', { id: node.id, along: v }); }} /></dd>
          <dt>Sill</dt><dd><LengthField metres={num(opening, 'sill')} onCommit={(v) => { if (v != null) void execute('architecture.moveOpening', { ids: sameKind(node, JOINERY), sill: v }); }} /></dd>
        </>}
        {node.editable && <>
          <dt>Frame in the wall</dt><dd>
            <select data-field="position-from" title="Where the frame sits across the wall; its type’s unless set here" value={ownPosition?.from ?? ''}
              onChange={(e) => setPosition(e.target.value ? { from: e.target.value as Position['from'], distance: position?.distance ?? 0 } : null)}>
              <option value="">{spec?.position ? 'As the type' : 'Centred (the type’s)'}</option><option value="exterior">From the exterior finish</option><option value="core">From the core</option><option value="centre">From the wall’s centre</option>
            </select>
          </dd>
          {ownPosition && <><dt>Set back</dt><dd><LengthField metres={ownPosition.distance} onCommit={(v) => { if (v != null) setPosition({ ...ownPosition, distance: v }); }} /></dd></>}
        </>}
        <dt>Of its type</dt><dd className="num">{type ? relatedOf(model, type.id, 'IfcRelDefinesByType').length : 0}</dd>
      </dl>
      {node.editable && (
        <div className="row">
          <button type="button" className="btn" title="Hinges on the other side" onClick={() => void execute('architecture.flipJoinery', { ids: sameKind(node, JOINERY), axis: 'x' })}><FlipHorizontal2 size={14} />Hand</button>
          <button type="button" className="btn" title="Opens to the other side of the wall" onClick={() => void execute('architecture.flipJoinery', { ids: sameKind(node, JOINERY), axis: 'y' })}><FlipVertical2 size={14} />Side</button>
          <JoineryDataButton ids={sameKind(node, JOINERY)} typeNode={type} own={node} />
          {/* The door or window goes, its opening stays in the wall with its size and head (report #114, decision 392). */}
          {wall?.type === 'IfcWall' && <button type="button" className="btn" data-action="fill-to-opening" onClick={() => void toOpening(node)}><SquareDashed size={14} />Change to opening</button>}
        </div>
      )}
    </div>
  );
}
