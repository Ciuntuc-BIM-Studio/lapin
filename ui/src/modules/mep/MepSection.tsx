/** A node or a run in the inspector: its system, its role and ports (open or connected), a run's ends. */

import { useEffect, useState } from 'react';
import { Plus, Settings2, SquarePen, Unlink } from 'lucide-react';
import { setTool, useUi } from '../../app/store';
import { useScope } from '../../platform/scope';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { describeEnd, isRun, networkQuery, roleOf, serviceLabel, type NetworkSystem } from './network';
import { mepHooks } from './hooks';
import { setMep } from './prefs';
import { editSystem } from './edit';
import { roles, templates } from './network';

export function MepSection({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  const nodes = useSession((s) => s.nodes);
  const systemId = typeof node.props.systemId === 'string' ? node.props.systemId : null;
  const system = systemId ? nodes.get(systemId) : undefined;
  const [net, setNet] = useState<NetworkSystem | null>(null);
  useEffect(() => {
    let live = true;
    if (!systemId) { setNet(null); return; }
    void networkQuery(systemId).then((r) => { if (live) setNet(r?.[0] ?? null); });
    return () => { live = false; };
  }, [systemId, version]);
  const run = isRun(node);
  const scope = useScope((s) => s.scope);
  const editing = !!systemId && scope?.id === systemId;
  const service = system ? String(system.props.service) : null;
  const adds = service ? (templates().find((t) => t.service === service)?.endpoints ?? []) : [];
  // "+ Radiator": the system opens for editing and the tool places one, joined to the network where it is clicked.
  const add = (roleId: string) => { if (!systemId) return; if (!editing) editSystem(systemId); setMep({ systemId }); setTool(`mep.node.${roleId}`); };
  const role = run ? null : roleOf(node);
  const me = run ? net?.runs.find((r) => r.id === node.id) : null;
  const ports = run ? [] : net?.nodes.find((n) => n.id === node.id)?.ports ?? [];
  return (
    <div className="layers">
      <dl className="fields">
        <dt>System</dt>
        <dd>
          <span className="row">
            <span>{system ? `${system.name} · ${serviceLabel(String(system.props.service))}` : '—'}</span>
            <button type="button" className="icon" title="Systems…" onClick={() => { if (systemId) setMep({ systemId }); useUi.setState({ dialog: 'mepSystems' }); }}><Settings2 size={14} /></button>
          </span>
        </dd>
        {role && <><dt>Role</dt><dd>{role.label}{role.duty != null && role.dutyUnit ? <span className="muted"> · {Number(node.props.duty ?? role.duty)} {role.dutyUnit}</span> : null}</dd></>}
        {!run && ports.length > 0 && (
          <>
            <dt>Ports</dt>
            <dd>
              <ul className="plain">
                {ports.map((p) => <li key={p.key}>{p.key} <span className="muted">· {serviceLabel(p.service)} · {p.flow} · </span>{p.taken ? 'connected' : 'open'}</li>)}
              </ul>
            </dd>
          </>
        )}
        {run && me && (['start', 'end'] as const).map((end) => (
          <EndRow key={end} label={end === 'start' ? 'Start' : 'End'} text={describeEnd(me[end], nodes)} open={!me[end]} disabled={!node.editable}
            onDisconnect={() => void execute('mep.disconnect', { runId: node.id, end })} />
        ))}
      </dl>
      {systemId && (
        <div className="row" style={{ flexWrap: 'wrap', gap: 4, marginTop: 6 }}>
          {!editing && <button type="button" className="btn small" disabled={!node.editable} onClick={() => editSystem(systemId)}><SquarePen size={13} /> Edit system</button>}
          {run && mepHooks.runActions && <mepHooks.runActions node={node} />}
          {adds.map((r) => <button type="button" key={r} className="btn small" disabled={!node.editable} title={`Place a ${(roles().find((x) => x.id === r)?.label ?? r).toLowerCase()}, joined to the system where you click`} onClick={() => add(r)}><Plus size={13} /> {roles().find((x) => x.id === r)?.label ?? r}</button>)}
        </div>
      )}
      <p className="muted small">Nodes and runs take the system's defaults unless a value is set on them.</p>
    </div>
  );
}

function EndRow({ label, text, open, disabled, onDisconnect }: { label: string; text: string; open: boolean; disabled: boolean; onDisconnect: () => void }) {
  return (
    <>
      <dt>{label}</dt>
      <dd>
        <span className="row">
          <span className={open ? 'muted' : ''}>{text}</span>
          {!open && <button type="button" className="icon" title="Disconnect" disabled={disabled} onClick={onDisconnect}><Unlink size={13} /></button>}
        </span>
      </dd>
    </>
  );
}
