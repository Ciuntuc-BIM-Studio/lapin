/**
 * A steel connection in the properties panel (decision 273): its kind and the members it joins, its parameters (each
 * shown as it is, given or chosen by the profiles; each change one engine command, set on the connection itself so its
 * hall keeps it), and what it is made of: its plates, bolts and anchors counted by size and grade, its welds, its weight,
 * the footing its anchors go into.
 */

import { useEffect, useState } from 'react';
import { Warehouse } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { LengthField, NumberField } from '../../shell/cells';
import { t } from '../../i18n';
import { hallOf } from './hall';
import { useHall } from './HallDialog';
import { anchorCounts, BOLT_SIZES, boltLabel, GRADES, nodeFields, partsRows, weldsText, type NodeField, type NodeInfo, type NodeParamsDto } from './nodes';

const LENGTHS: Partial<Record<NodeField, [string, number]>> = {
  plate: ['Plate thickness', 0.004], edge: ['Edge distance', 0.012], pitch: ['Bolt pitch', 0.025], gauge: ['Bolt gauge', 0.03],
  extension: ['Above the rafter', 0], anchorLength: ['Anchor length', 0.1], grout: ['Grout', 0], gap: ['Gap at the top', 0],
};

export function NodeSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const [info, setInfo] = useState<NodeInfo | null>(null);
  useEffect(() => { let live = true; void query<NodeInfo>('steel.node', { id: node.id }).then((r) => { if (live) setInfo(r); }); return () => { live = false; }; }, [node.id, nodes]);
  if (!info) return null;
  const hall = hallOf({ nodes, relations }, node.id);
  const ro = !node.editable;
  const p = info.params;
  const s = info.spec;
  const send = (k: NodeField, v: unknown) => void execute('steel.updateNode', { id: node.id, params: { [k]: v ?? null } });
  const base = info.kind === 'base';
  // The default shown as its value alone (the column is narrow), said by its title.
  const byProfile = (text: string) => text;
  const field = (k: NodeField) => {
    const len = LENGTHS[k];
    if (len) return (
      <div key={k} className="contents"><dt>{len[0]}</dt>
        <dd><LengthField metres={(p[k] as number | undefined) ?? (s[k] as number)} min={len[1]} blank readOnly={ro} testId={`node-${k}`} onCommit={(v) => send(k, v)} /></dd></div>
    );
    switch (k) {
      case 'base': return (
        <div key={k} className="contents"><dt>Column base</dt>
          <dd><select className="cell" data-field="node-base" value={s.base} disabled={ro} onChange={(e) => send('base', e.target.value)}>
            <option value="pinned">pinned</option><option value="fixed">fixed</option>
          </select></dd></div>
      );
      case 'anchor': return (
        <div key={k} className="contents"><dt>Anchors</dt>
          <dd><select className="cell" data-field="node-anchor" value={s.anchor} disabled={ro} onChange={(e) => send('anchor', e.target.value)}>
            <option value="straight">straight</option><option value="hooked">hooked</option><option value="plate">with an anchor plate</option>
          </select></dd></div>
      );
      case 'anchors': return (
        <div key={k} className="contents"><dt>Number of anchors</dt>
          <dd><select className="cell" data-field="node-anchors" title={t('by the profile')} value={p.anchors ?? ''} disabled={ro} onChange={(e) => send('anchors', e.target.value ? Number(e.target.value) : null)}>
            <option value="">{byProfile(String(s.anchors))}</option>
            {anchorCounts(s.base).map((n) => <option key={n} value={n} data-no-i18n>{n}</option>)}
          </select></dd></div>
      );
      case 'bolt': return (
        <div key={k} className="contents"><dt>{base ? 'Anchor diameter' : 'Bolt diameter'}</dt>
          <dd><select className="cell" data-field="node-bolt" title={t('by the profile')} value={p.bolt ?? ''} disabled={ro} onChange={(e) => send('bolt', e.target.value ? Number(e.target.value) : null)}>
            <option value="">{byProfile(boltLabel(s.bolt))}</option>
            {BOLT_SIZES.map((d) => <option key={d} value={d} data-no-i18n>{boltLabel(d)}</option>)}
          </select></dd></div>
      );
      case 'grade': return (
        <div key={k} className="contents"><dt>{base ? 'Anchor grade' : 'Bolt grade'}</dt>
          <dd><select className="cell" data-field="node-grade" title={t('by the profile')} value={p.grade ?? ''} disabled={ro} onChange={(e) => send('grade', e.target.value || null)}>
            <option value="">{byProfile(s.grade)}</option>
            {GRADES.map((g) => <option key={g} value={g} data-no-i18n>{g}</option>)}
          </select></dd></div>
      );
      case 'rows': return (
        <div key={k} className="contents"><dt>Bolt rows</dt>
          <dd><NumberField value={p.rows ?? s.rows} min={1} max={16} blank readOnly={ro} testId="node-rows" onCommit={(v) => send('rows', v == null ? null : Math.round(v))} /></dd></div>
      );
      case 'cols': return (
        <div key={k} className="contents"><dt>Bolts in a row</dt>
          <dd><select className="cell" data-field="node-cols" value={s.cols} disabled={ro} onChange={(e) => send('cols', Number(e.target.value))}>
            <option value={2} data-no-i18n>2</option><option value={4} data-no-i18n>4</option>
          </select></dd></div>
      );
      case 'stiffeners': return (
        <div key={k} className="contents"><dt>{base ? 'Stiffeners' : 'Column stiffeners'}</dt>
          <dd><input type="checkbox" data-field="node-stiffeners" checked={s.stiffeners} disabled={ro} onChange={(e) => send('stiffeners', e.target.checked)} /></dd></div>
      );
      default: return null;
    }
  };
  const fields = nodeFields(info.kind, s.base) as (keyof NodeParamsDto)[];
  return (
    <div className="layers" data-field="steel-node">
      <p className="row small">
        <span className="grow" title={info.own ? t('Set on its own: the hall keeps it') : undefined}>{info.label}</span>
        {hall && <button type="button" className="btn" data-field="node-edit-hall" onClick={() => { useHall.setState({ id: hall.id }); useUi.setState({ dialog: 'steelHall' }); }}><Warehouse size={13} />Edit hall…</button>}
      </p>
      <dl className="fields">
        <dt>Members</dt><dd data-no-i18n>{info.members.map((m) => m.name ?? m.id).join(' · ')}</dd>
        {fields.map(field)}
      </dl>
      <p className="eyebrow">Parts</p>
      <dl className="fields" data-field="node-parts">
        {partsRows(info.parts).map((r) => <div key={r.part + r.text} className="contents"><dt>{r.part}</dt><dd className="num" data-no-i18n>{`${r.count} × ${r.text}`}</dd></div>)}
        {info.welds.length > 0 && <><dt>Welds</dt><dd className="num" data-no-i18n>{weldsText(info.welds)}</dd></>}
        <dt>Weight</dt><dd className="num" data-no-i18n>{`${info.weight.toFixed(1)} kg`}</dd>
        {info.footing && <><dt>Footing</dt><dd data-no-i18n>{info.footing.name}</dd></>}
      </dl>
      {info.problems.map((x) => <p key={x} className="error-text small">{x}</p>)}
    </div>
  );
}
