/**
 * The "Elements" section of a part's tree (audit S6): the native elements the part holds (walls, slabs, furniture…) with what
 * each is driven by — an attribute given by a formula of the part's parameters (`{length: "W"}`) — Select, and Let go (the
 * element stays in the placed part as a plain element, gone from the others).
 */

import { useState } from 'react';
import { Link2Off, MousePointerClick, Trash2 } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { addPickRedirect } from '../../platform/groups';
import { editPart } from './editMode';
import { driveChange, driveRows, freeAttrs, type PartMember } from './partMembersModel';

const send = async (command: string, args: Record<string, unknown>) => { await execute(command, args); const e = useSession.getState().error; if (e) notify(e); };

function Drives({ partId, m }: { partId: string; m: PartMember }) {
  const free = freeAttrs(m);
  const [attr, setAttr] = useState('');
  const [formula, setFormula] = useState('');
  const chosen = free.includes(attr) ? attr : free[0] ?? '';
  const set = (a: string, f: string | null) => send('studio.setPartMemberDrives', { partId, key: m.key, drives: driveChange(a, f) });
  return (
    <div className="part-member-drives" data-part-member-drives={m.key}>
      {driveRows(m).map((d) => (
        <div key={d.attr} className="row" data-part-drive={d.attr}>
          <span className="muted small" data-no-i18n>{d.attr}</span>
          <span className="muted small">=</span>
          <input type="text" defaultValue={d.formula} key={d.formula} style={{ width: 90 }} aria-label={d.attr} data-no-i18n data-field="member-drive-formula"
            onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
            onBlur={(e) => { const v = e.target.value.trim(); if (v !== d.formula) void set(d.attr, v); }} />
          <button type="button" className="icon mini" data-field="member-drive-clear" title="Stop driving it" onClick={() => void set(d.attr, null)}><Trash2 size={11} /></button>
        </div>
      ))}
      {free.length > 0 && (
        <div className="row">
          <select value={chosen} onChange={(e) => setAttr(e.target.value)} data-field="member-drive-attr" aria-label="Attribute">
            {free.map((a) => <option key={a} value={a} data-no-i18n>{a}</option>)}
          </select>
          <input type="text" value={formula} placeholder="W, H / 2…" style={{ width: 90 }} data-no-i18n data-field="member-drive-new" aria-label="Formula"
            onChange={(e) => setFormula(e.target.value)}
            onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter' && formula.trim() && chosen) { void set(chosen, formula).then(() => setFormula('')); } }} />
          <button type="button" className="btn small" disabled={!formula.trim() || !chosen} data-field="member-drive-set" title="Drive the attribute by a formula of the part's parameters"
            onClick={() => void set(chosen, formula).then(() => setFormula(''))}>Set</button>
        </div>
      )}
    </div>
  );
}

export function PartMembersSection({ partId, members }: { partId: string; members: PartMember[] }) {
  return (
    <div className="part-section" data-testid="part-members">
      <div className="part-title">Elements</div>
      {!members.length && <p className="muted small">None: select the elements and the part, then Studio → Add to part.</p>}
      <ul className="part-list">
        {members.map((m) => (
          <li key={m.key} data-part-member={m.key}>
            <strong data-no-i18n>{m.name}</strong><span className="muted small" data-no-i18n>{m.class.replace(/^Ifc/, '')}</span>
            <span className="grow" />
            <button type="button" className="icon mini" data-field="member-select" title="Select it" onClick={() => useUi.setState({ selection: m.id, selected: [m.id] })}><MousePointerClick size={11} /></button>
            <button type="button" className="icon mini" data-field="member-release" title="Let go: it stays in this object as a plain element, and goes from the others"
              onClick={() => void send('studio.removePartMembers', { partId, keys: [m.key] })}><Link2Off size={11} /></button>
            <Drives partId={partId} m={m} />
          </li>
        ))}
      </ul>
    </div>
  );
}

// ------------------------------------------------------------ outside the edit mode

/** The placed part that holds an element (its IfcRelAggregates whole), when the element is one of its members (`props.partKey`). */
export function holderOf(node: NodeDto, relations = useSession.getState().relations): string | undefined {
  if (typeof node.props.partKey !== 'string') return undefined;
  for (const r of relations.values()) if (r.class === 'IfcRelAggregates' && r.related.includes(node.id)) return r.relating;
  return undefined;
}

/** A click on a member picks its placed part, unless the part's edit mode (a scope) is on. */
addPickRedirect((id) => {
  const st = useSession.getState();
  const n = st.nodes.get(id);
  const to = n ? holderOf(n, st.relations) : undefined;
  return to && st.nodes.get(to)?.props.operation === 'part' ? to : undefined;
});

/** Properties of a member: the part that holds it, and the way into its edit mode. */
export function PartMemberSection({ node }: { node: NodeDto }) {
  const relations = useSession((s) => s.relations);
  const holder = holderOf(node, relations);
  const part = useSession((s) => (holder ? s.nodes.get(holder) : undefined));
  if (!part) return null;
  return (
    <dl className="fields" data-section="part-member">
      <dt>Part</dt>
      <dd className="row"><span className="muted small">Held by</span> <strong data-no-i18n>{part.name}</strong><span className="grow" />
        <button type="button" className="btn small" data-field="member-edit-part" title="Edit it in the part, for every object of the part"
          onClick={() => { useUi.setState({ view: '3d' }); editPart(part.id); }}>Edit part</button></dd>
    </dl>
  );
}
