/**
 * A door or window in the inspector: its type (and the configurator), its
 * place on its wall (along it, its sill), and Flip — the hand (left ↔ right)
 * or the side (interior ↔ exterior).
 */

import { useEffect, useState } from 'react';
import { FlipHorizontal2, FlipVertical2, Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { mm, num, ofClass, relatedOf, relatingOf, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

function Millimetres({ value, onCommit }: { value: number; onCommit: (m: number) => void }) {
  const [text, setText] = useState(mm(value));
  useEffect(() => setText(mm(value)), [value]);
  const done = () => { const v = Number(text.replace(',', '.')); if (Number.isFinite(v) && mm(value) !== text) onCommit(v / 1000); };
  return <span className="commit"><input value={text} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /><span className="muted"> mm</span></span>;
}

export function JoinerySection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const types = ofClass(model, node.type === 'IfcDoor' ? 'IfcDoorType' : 'IfcWindowType');
  const opening = relatingOf(model, node.id, 'IfcRelFillsElement');
  const wall = opening ? relatingOf(model, opening.id, 'IfcRelVoidsElement') : null;
  const spec = type?.props.spec as { width?: number; height?: number } | undefined;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={!node.editable} onChange={(e) => void execute('architecture.setJoineryType', { id: node.id, typeId: e.target.value })}>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button type="button" className="icon" title="Doors and windows…" onClick={() => useUi.setState({ dialog: 'joinery' })}><Settings2 size={14} /></button>
        </span>
      </label>
      <dl className="fields">
        <dt>Size</dt><dd className="num">{mm(spec?.width ?? 0)} × {mm(spec?.height ?? 0)}</dd>
        <dt>In wall</dt><dd>{wall?.name ?? '—'}</dd>
        {opening && node.editable && <>
          <dt>Along the wall</dt><dd><Millimetres value={num(opening, 'along')} onCommit={(v) => void execute('architecture.moveOpening', { id: node.id, along: v })} /></dd>
          <dt>Sill</dt><dd><Millimetres value={num(opening, 'sill')} onCommit={(v) => void execute('architecture.moveOpening', { id: node.id, sill: v })} /></dd>
        </>}
        <dt>Of its type</dt><dd className="num">{type ? relatedOf(model, type.id, 'IfcRelDefinesByType').length : 0}</dd>
      </dl>
      {node.editable && (
        <div className="row">
          <button type="button" className="btn" title="Hinges on the other side" onClick={() => void execute('architecture.flipJoinery', { id: node.id, axis: 'x' })}><FlipHorizontal2 size={14} />Hand</button>
          <button type="button" className="btn" title="Opens to the other side of the wall" onClick={() => void execute('architecture.flipJoinery', { id: node.id, axis: 'y' })}><FlipVertical2 size={14} />Side</button>
        </div>
      )}
    </div>
  );
}
