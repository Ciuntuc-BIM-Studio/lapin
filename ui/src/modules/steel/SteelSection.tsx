/**
 * A steel member in the properties panel (decision 268): a member's type and its ends' heights; for a member, or a
 * straight beam or column of a steel profile, its two ends, each a set of rules — the extension (negative: set back),
 * the cut (square, by a plane turned in plan and in elevation, mitred with a member, fitted to a member's face) and a
 * cope of the flanges at the top and/or the bottom. The body follows the rules; a mitre or a fit follows the member it
 * names. A member's haunches (decision 270), each set on its own (a hall built again keeps it), and the hall a member
 * belongs to, opened in its configurator.
 */

import { Settings2, Warehouse } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { LengthField, NumberField } from '../../shell/cells';
import { sectionLabel, type SectionDto } from './sections';
import { hallOf, type HaunchDto } from './hall';
import { useHall } from './HallDialog';

export interface SteelEnd {
  cut?: 'square' | 'plane' | 'mitre' | 'fit'; extension?: number; planAngle?: number; elevAngle?: number; ref?: string;
  copeLength?: number; copeDepth?: number; copeTop?: boolean; copeBottom?: boolean;
}

/** The kinds a section of steel has (the others are concrete's rectangles and circles). */
export const STEEL_KINDS = ['I', 'U', 'T', 'L', 'RHS', 'CHS', 'C', 'Z', 'SIGMA'];
const STEEL_CLASSES = ['LapinSteelMember', 'IfcBeam', 'IfcColumn'];
const CUTS: [NonNullable<SteelEnd['cut']>, string][] = [['square', 'Square to the axis'], ['plane', 'By a plane'], ['mitre', 'Mitre'], ['fit', 'Fit to a member']];

const point = (v: unknown): number[] => (Array.isArray(v) ? v.map(Number) : [0, 0, 0]);

/** One end's rules, each change one engine command (one undo step). */
function EndRules({ node, which, end, others }: { node: NodeDto; which: 'start' | 'end'; end: SteelEnd; others: NodeDto[] }) {
  const cut = end.cut ?? 'square';
  const send = (patch: Partial<SteelEnd>) => {
    const next = { ...end, cut, ...patch };
    // A mitre or a fit names a member; the first other one until one is chosen.
    if ((next.cut === 'mitre' || next.cut === 'fit') && !next.ref) next.ref = others[0]?.id;
    if ((next.cut === 'mitre' || next.cut === 'fit') && !next.ref) return;
    void execute('steel.setEnds', { ids: [node.id], [which]: next });
  };
  const ro = !node.editable;
  return (
    <>
      <dt className="eyebrow">{which === 'start' ? 'Start' : 'End'}</dt><dd />
      <dt>Cut</dt>
      <dd><select className="cell" data-field={`steel-${which}-cut`} value={cut} disabled={ro} onChange={(e) => send({ cut: e.target.value as SteelEnd['cut'] })}>
        {CUTS.map(([k, label]) => <option key={k} value={k} disabled={(k === 'mitre' || k === 'fit') && !others.length}>{label}</option>)}
      </select></dd>
      <dt title="How far the end reaches past its point; negative: set back, a gap">Extension</dt>
      <dd><LengthField metres={end.extension ?? 0} readOnly={ro} testId={`steel-${which}-extension`} onCommit={(v) => send({ extension: v ?? 0 })} /></dd>
      {cut === 'plane' && <>
        <dt title="Turned from square about the section's up, degrees">In plan</dt>
        <dd><NumberField value={end.planAngle ?? 0} suffix="°" min={-79} max={79} readOnly={ro} testId={`steel-${which}-plan`} onCommit={(v) => send({ planAngle: v ?? 0 })} /></dd>
        <dt title="Turned from square about the section's side, degrees">In elevation</dt>
        <dd><NumberField value={end.elevAngle ?? 0} suffix="°" min={-79} max={79} readOnly={ro} testId={`steel-${which}-elev`} onCommit={(v) => send({ elevAngle: v ?? 0 })} /></dd>
      </>}
      {(cut === 'mitre' || cut === 'fit') && <>
        <dt>Member</dt>
        <dd><select className="cell" data-field={`steel-${which}-ref`} value={end.ref ?? ''} disabled={ro} onChange={(e) => send({ ref: e.target.value })}>
          {!end.ref && <option value="">—</option>}
          {others.map((o) => <option key={o.id} value={o.id} data-no-i18n>{o.name}</option>)}
        </select></dd>
      </>}
      <dt title="The flanges notched back from the end">Cope</dt>
      <dd className="row">
        <label className="small"><input type="checkbox" data-field={`steel-${which}-cope-top`} checked={Boolean(end.copeTop)} disabled={ro}
          onChange={(e) => send({ copeTop: e.target.checked, copeLength: end.copeLength || 0.1, copeDepth: end.copeDepth || 0.03 })} /> Top</label>
        <label className="small"><input type="checkbox" data-field={`steel-${which}-cope-bottom`} checked={Boolean(end.copeBottom)} disabled={ro}
          onChange={(e) => send({ copeBottom: e.target.checked, copeLength: end.copeLength || 0.1, copeDepth: end.copeDepth || 0.03 })} /> Bottom</label>
      </dd>
      {(end.copeTop || end.copeBottom) && <>
        <dt>Cope length</dt>
        <dd><LengthField metres={end.copeLength ?? 0} min={0.001} readOnly={ro} testId={`steel-${which}-cope-length`} onCommit={(v) => { if (v != null) send({ copeLength: v }); }} /></dd>
        <dt>Cope depth</dt>
        <dd><LengthField metres={end.copeDepth ?? 0} min={0.001} readOnly={ro} testId={`steel-${which}-cope-depth`} onCommit={(v) => { if (v != null) send({ copeDepth: v }); }} /></dd>
      </>}
    </>
  );
}

/** One haunch of a member: its kind, length and depth, each change setting it on its own (one engine command). */
function HaunchRules({ node, which, h }: { node: NodeDto; which: 'start' | 'end'; h: HaunchDto }) {
  const send = (patch: Partial<HaunchDto>) => void execute('steel.setHaunch', { id: node.id, end: which, haunch: { ...h, ...patch } });
  const ro = !node.editable;
  return (
    <>
      {/* The heading spans the row: "Vută la început" and its kin are longer than the label column (decision 273). */}
      <dt className="eyebrow wide" title={h.own ? 'Set on its own: the hall keeps it' : undefined}>
        <span>{which === 'start' ? 'Haunch at the start' : 'Haunch at the end'}</span>
        <button type="button" className="icon mini" title="No haunch here" disabled={ro} onClick={() => void execute('steel.setHaunch', { id: node.id, end: which, haunch: null })}>×</button>
      </dt>
      <dt>Made</dt>
      <dd><select className="cell" data-field={`steel-haunch-${which}-kind`} value={h.kind ?? 'cut'} disabled={ro} onChange={(e) => send({ kind: e.target.value as HaunchDto['kind'] })}>
        <option value="cut">cut from the rafter's section</option><option value="plates">welded from plates</option>
      </select></dd>
      <dt>Length</dt>
      <dd><LengthField metres={h.length} min={0.05} readOnly={ro} testId={`steel-haunch-${which}-length`} onCommit={(v) => { if (v != null) send({ length: v }); }} /></dd>
      <dt data-i18n-ctx="hall">Depth</dt>
      <dd><LengthField metres={h.depth} min={0.02} readOnly={ro} testId={`steel-haunch-${which}-depth`} onCommit={(v) => { if (v != null) send({ depth: v }); }} /></dd>
      {h.kind === 'plates' && <>
        <dt>Web plate</dt>
        <dd><LengthField metres={h.web ?? 0.01} min={0.002} readOnly={ro} onCommit={(v) => { if (v != null) send({ web: v }); }} /></dd>
        <dt>Flange plate</dt>
        <dd><LengthField metres={h.flange ?? 0.012} min={0.002} readOnly={ro} onCommit={(v) => { if (v != null) send({ flange: v }); }} /></dd>
      </>}
    </>
  );
}

export function SteelSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const section = type?.props.section as SectionDto | undefined;
  const member = node.type === 'LapinSteelMember';
  if (!member && !STEEL_KINDS.includes(section?.kind ?? '')) return <p className="muted small pad">Not a steel profile: its type's section is concrete's. A beam or column of a steel profile takes steel ends.</p>;
  const ends = (node.props.steelEnds ?? {}) as { start?: SteelEnd; end?: SteelEnd };
  const others = STEEL_CLASSES.flatMap((c) => ofClass(model, c)).filter((o) => o.id !== node.id);
  const [s, e] = [point(node.props.start), point(node.props.end)];
  const types = ofClass(model, 'LapinSteelMemberType');
  const hall = hallOf(model, node.id);
  const haunches = (node.props.haunches ?? {}) as { start?: HaunchDto; end?: HaunchDto };
  return (
    <div className="layers" data-field="steel-ends">
      {hall && (
        <p className="row small">
          <span className="grow" data-no-i18n>{hall.name}</span>
          <button type="button" className="btn" data-field="steel-edit-hall" onClick={() => { useHall.setState({ id: hall.id }); useUi.setState({ dialog: 'steelHall' }); }}><Warehouse size={13} />Edit hall…</button>
        </p>
      )}
      {member && (
        <label className="type-pick">
          <span className="eyebrow">Type</span>
          <span className="row">
            <select value={type?.id ?? ''} disabled={!node.editable} data-field="steel-type" onChange={(ev) => void execute('steel.setType', { ids: [node.id], typeId: ev.target.value })}>
              {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
            </select>
            <button type="button" className="icon" title="Steel types…" onClick={() => useUi.setState({ dialog: 'steelTypes' })}><Settings2 size={14} /></button>
          </span>
        </label>
      )}
      {!member && <p className="muted small" data-no-i18n>{sectionLabel(section)}</p>}
      <dl className="fields">
        {member && <>
          <dt title="Over the storey's level">Start height</dt>
          <dd><LengthField metres={s[2] ?? 0} readOnly={!node.editable} testId="steel-z1" onCommit={(v) => { if (v != null) void execute('steel.updateMember', { id: node.id, z1: v }); }} /></dd>
          <dt title="Over the storey's level">End height</dt>
          <dd><LengthField metres={e[2] ?? 0} readOnly={!node.editable} testId="steel-z2" onCommit={(v) => { if (v != null) void execute('steel.updateMember', { id: node.id, z2: v }); }} /></dd>
        </>}
        <EndRules node={node} which="start" end={ends.start ?? {}} others={others} />
        <EndRules node={node} which="end" end={ends.end ?? {}} others={others} />
        {member && haunches.start && <HaunchRules node={node} which="start" h={haunches.start} />}
        {member && haunches.end && <HaunchRules node={node} which="end" h={haunches.end} />}
      </dl>
      {member && node.editable && (!haunches.start || !haunches.end) && (
        <p className="row small">
          {(['start', 'end'] as const).filter((w) => !haunches[w]).map((w) => (
            <button key={w} type="button" className="btn" data-field={`steel-add-haunch-${w}`} onClick={() => void execute('steel.setHaunch', { id: node.id, end: w, haunch: { length: 1, depth: 0.3, kind: 'cut' } })}>
              {w === 'start' ? 'Haunch at the start' : 'Haunch at the end'}
            </button>
          ))}
        </p>
      )}
    </div>
  );
}
