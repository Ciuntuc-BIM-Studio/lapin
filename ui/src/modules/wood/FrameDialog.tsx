/**
 * The timber frame configurator (docs/plan-wood.md, decision 239): the project's wall types on the left; on the right the
 * one being edited — built as a timber frame or not, its studs, plates and noggins, where the module starts, whether it
 * carries load, the openings' fitting gaps and their rules by width — beside a sample wall the engine frames as it would
 * (a window and a door, every member). Two presets: the Nordic practice and the Romanian. Saving is one command.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';

export type Header = { kind: string; section: string; plies: number };
export type Rule = { maxWidth: number; kings: number; jacks: number; header: Header; sill: number };
export interface Spec {
  construction: 'timberFrame'; studSection: string; spacing: number; grade: string; bottomPlates: number; topPlates: number; endStuds: number;
  noggins: number[]; layoutFrom: 'start' | 'project'; loadBearing: boolean; gapSide: number; gapTop: number; gapBottom: number; minGap: number; minPier: number; openings: Rule[];
}
export interface Framed {
  problem?: string; length: number; height: number;
  members: { role: string; section: string; opening?: string | null; face: number[] }[];
  insulation: number[][]; counts: Record<string, number>; warnings: string[];
}

export const SECTIONS = ['45x95 mm', '45x120 mm', '45x145 mm', '45x170 mm', '45x195 mm', '45x220 mm', '45x245 mm', '50x100 mm', '50x150 mm', '50x200 mm', '70x220 mm', '90x315 mm', '90x360 mm', '115x360 mm'];
export const GRADES = ['C14', 'C16', 'C18', 'C24', 'C30', 'GL24h', 'GL28h', 'GL30c'];
export const HEADERS = ['none', 'solid', 'lvl', 'glulam'];

const NORDIC: Spec = {
  construction: 'timberFrame', studSection: '45x145 mm', spacing: 0.6, grade: 'C24', bottomPlates: 1, topPlates: 2, endStuds: 2, noggins: [], layoutFrom: 'start', loadBearing: true,
  gapSide: 0.01, gapTop: 0.01, gapBottom: 0.01, minGap: 0.05, minPier: 0.2,
  openings: [
    { maxWidth: 0.9, kings: 1, jacks: 1, header: { kind: 'solid', section: '45x195 mm', plies: 2 }, sill: 1 },
    { maxWidth: 1.8, kings: 2, jacks: 2, header: { kind: 'solid', section: '45x245 mm', plies: 2 }, sill: 2 },
    { maxWidth: 99, kings: 2, jacks: 2, header: { kind: 'glulam', section: '90x360 mm', plies: 1 }, sill: 2 },
  ],
};
const ROMANIAN: Spec = {
  ...NORDIC, studSection: '50x150 mm', spacing: 0.625, noggins: [1.25], gapSide: 0.015, gapTop: 0.015, gapBottom: 0.015,
  openings: [
    { maxWidth: 0.9, kings: 1, jacks: 1, header: { kind: 'solid', section: '50x200 mm', plies: 2 }, sill: 1 },
    { maxWidth: 1.8, kings: 2, jacks: 2, header: { kind: 'solid', section: '50x200 mm', plies: 3 }, sill: 2 },
    { maxWidth: 99, kings: 2, jacks: 2, header: { kind: 'glulam', section: '115x360 mm', plies: 1 }, sill: 2 },
  ],
};

export const ROLE_FILL: Record<string, string> = {
  plate: '#b48650', end: '#c99a64', stud: '#d4a970', king: '#c08a50', jack: '#cf9a5c', header: '#a87a46', sill: '#a87a46', cripple: '#dcb27c', noggin: '#c99a64',
};

/** A length in mm, kept as text while typing. */
function Mm({ label, value, onChange, title }: { label: string; value: number; onChange: (m: number) => void; title?: string }) {
  const [text, setText] = useState(String(Math.round(value * 1000)));
  useEffect(() => setText(String(Math.round(value * 1000))), [value]);
  return (
    <label className="field-line wood-field" title={title}><span className="eyebrow">{label}</span>
      <input className="num" value={text} onChange={(e) => setText(e.target.value)} onBlur={() => { const v = Number(text.replace(',', '.')); if (Number.isFinite(v) && v >= 0) onChange(v / 1000); else setText(String(Math.round(value * 1000))); }} />
    </label>
  );
}

function Count({ label, value, onChange, min = 0, max = 4 }: { label: string; value: number; onChange: (n: number) => void; min?: number; max?: number }) {
  return (
    <label className="field-line wood-field"><span className="eyebrow">{label}</span>
      <input className="num" type="number" min={min} max={max} value={value} onChange={(e) => onChange(Math.max(min, Math.min(max, Math.round(Number(e.target.value) || 0))))} />
    </label>
  );
}

/** A framed wall's elevation: its members by role, the insulation, cotes of the rough openings. */
export function FrameElevation({ f, width = 560, height = 260 }: { f: Framed; width?: number; height?: number }) {
  const pad = 14;
  const k = Math.min((width - 2 * pad) / Math.max(0.1, f.length), (height - 2 * pad) / Math.max(0.1, f.height));
  const X = (u: number) => pad + u * k;
  const Z = (z: number) => height - pad - z * k;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="Frame elevation">
      {f.insulation.map(([u0, u1, z0, z1l, z1r], i) => <polygon key={`i${i}`} points={`${X(u0)},${Z(z0)} ${X(u1)},${Z(z0)} ${X(u1)},${Z(z1r)} ${X(u0)},${Z(z1l)}`} fill="#f3e6a8" stroke="none" />)}
      {f.members.map((m, i) => {
        const pts: string[] = [];
        for (let j = 0; j + 1 < m.face.length; j += 2) pts.push(`${X(m.face[j])},${Z(m.face[j + 1])}`);
        return <polygon key={i} points={pts.join(' ')} fill={ROLE_FILL[m.role] ?? '#c99a64'} stroke="#4a3418" strokeWidth={0.5}><title>{`${m.role} ${m.section}`}</title></polygon>;
      })}
    </svg>
  );
}

export function FrameDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcWallType');
  const [editing, setEditing] = useState<string | null>(useUi.getState().wallTypeId ?? types[0]?.id ?? null);
  const type = editing ? nodes.get(editing) : undefined;
  const stored = (type?.props.wood as Spec | undefined) ?? null;
  const [spec, setSpec] = useState<Spec>(stored ?? NORDIC);
  const [on, setOn] = useState(Boolean(stored));
  const [preview, setPreview] = useState<Framed | null>(null);
  // Loaded when the type picked changes, not on every diff (that would throw away what is being typed).
  useEffect(() => { const s = (editing ? useSession.getState().nodes.get(editing)?.props.wood : null) as Spec | undefined | null; setSpec(s ? structuredClone(s) : structuredClone(NORDIC)); setOn(Boolean(s)); }, [editing]);
  useEffect(() => {
    let live = true;
    void query<Framed>('wood.preview', { spec }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [spec]);

  const set = (patch: Partial<Spec>) => setSpec((s) => ({ ...s, ...patch }));
  const setRule = (i: number, patch: Partial<Rule>) => set({ openings: spec.openings.map((r, k) => (k === i ? { ...r, ...patch } : r)) });
  const setHeader = (i: number, patch: Partial<Header>) => setRule(i, { header: { ...spec.openings[i].header, ...patch } });
  const close = () => useUi.setState({ dialog: null });
  const save = () => { if (editing) void execute('wood.setWallFrame', { id: editing, spec: on ? spec : null }); };
  const [nogginText, setNogginText] = useState(spec.noggins.map((n) => Math.round(n * 1000)).join(' '));
  useEffect(() => setNogginText(spec.noggins.map((n) => Math.round(n * 1000)).join(' ')), [spec.noggins]);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Timber frame">
      <div className="dialog wide wood-dialog">
        <header className="dialog-head">
          <span className="panel-title">Timber frame</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {t.props.wood ? 'timber frame' : 'layers'} · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                </button>
              </li>
            ))}
          </ul>
          {type && (
            <div className="joinery-edit">
              <div className="joinery-form">
                <label className="field-line wood-field"><span className="eyebrow">Timber frame</span>
                  <input type="checkbox" checked={on} data-testid="wood-on" onChange={(e) => setOn(e.target.checked)} />
                </label>
                {on && (
                  <>
                    <div className="row">
                      <span className="eyebrow">Presets</span>
                      <button type="button" className="btn compact" onClick={() => setSpec(structuredClone(NORDIC))}>Nordic 45×145 / 600</button>
                      <button type="button" className="btn compact" onClick={() => setSpec(structuredClone(ROMANIAN))}>Romanian 50×150 / 625</button>
                    </div>
                    <label className="field-line wood-field"><span className="eyebrow">Studs</span>
                      <select value={spec.studSection} onChange={(e) => set({ studSection: e.target.value })}>{SECTIONS.map((s) => <option key={s}>{s}</option>)}</select>
                    </label>
                    <Mm label="Spacing" value={spec.spacing} onChange={(v) => set({ spacing: v })} />
                    <label className="field-line wood-field"><span className="eyebrow">Grade</span>
                      <select value={spec.grade} onChange={(e) => set({ grade: e.target.value })}>{GRADES.map((g) => <option key={g}>{g}</option>)}</select>
                    </label>
                    <Count label="Bottom plates" value={spec.bottomPlates} min={1} max={3} onChange={(n) => set({ bottomPlates: n })} />
                    <Count label="Top plates" value={spec.topPlates} min={1} max={3} onChange={(n) => set({ topPlates: n })} />
                    <Count label="End studs" value={spec.endStuds} min={1} max={3} onChange={(n) => set({ endStuds: n })} />
                    <label className="field-line wood-field" title="Heights over the wall's base, in mm, e.g. 1250"><span className="eyebrow">Noggin rows (mm)</span>
                      <input value={nogginText} onChange={(e) => setNogginText(e.target.value)} onBlur={() => set({ noggins: nogginText.split(/[ ,;]+/).map(Number).filter((x) => x > 0).map((x) => x / 1000) })} />
                    </label>
                    <label className="field-line wood-field"><span className="eyebrow">Module from</span>
                      <select value={spec.layoutFrom} onChange={(e) => set({ layoutFrom: e.target.value as Spec['layoutFrom'] })}>
                        <option value="start">the wall's start</option><option value="project">the project's origin</option>
                      </select>
                    </label>
                    <label className="field-line wood-field" title="A partition carries nothing: a flat member over its openings"><span className="eyebrow">Load-bearing</span>
                      <input type="checkbox" checked={spec.loadBearing} onChange={(e) => set({ loadBearing: e.target.checked })} />
                    </label>
                    <p className="eyebrow">Openings</p>
                    <Mm label="Gap at the sides" value={spec.gapSide} onChange={(v) => set({ gapSide: v })} title="The rough opening is the door or window and these gaps" />
                    <Mm label="Gap on top" value={spec.gapTop} onChange={(v) => set({ gapTop: v })} />
                    <Mm label="Gap under a window" value={spec.gapBottom} onChange={(v) => set({ gapBottom: v })} />
                    <Mm label="Narrowest pier" value={spec.minPier} onChange={(v) => set({ minPier: v })} title="Two openings closer than this share one header" />
                    <table className="wood-rules">
                      <thead><tr><th title="The rough opening's width, up to">≤ mm</th><th title="King studs a side">K</th><th title="Jack studs a side">J</th><th>Header</th><th>Section</th><th title="Plies">×</th><th title="Sill plies">S</th><th /></tr></thead>
                      <tbody>
                        {spec.openings.map((r, i) => (
                          <tr key={i}>
                            <td><input className="num" defaultValue={r.maxWidth >= 98 ? '' : Math.round(r.maxWidth * 1000)} key={`w${i}${r.maxWidth}`} placeholder="any"
                              onBlur={(e) => setRule(i, { maxWidth: e.target.value.trim() === '' ? 99 : Number(e.target.value) / 1000 })} /></td>
                            <td><input className="num" type="number" min={0} max={4} value={r.kings} onChange={(e) => setRule(i, { kings: Number(e.target.value) })} /></td>
                            <td><input className="num" type="number" min={0} max={4} value={r.jacks} onChange={(e) => setRule(i, { jacks: Number(e.target.value) })} /></td>
                            <td><select value={r.header.kind} onChange={(e) => setHeader(i, { kind: e.target.value })}>{HEADERS.map((h) => <option key={h}>{h}</option>)}</select></td>
                            <td><select value={r.header.section} onChange={(e) => setHeader(i, { section: e.target.value })}>{[...new Set([...SECTIONS, r.header.section])].map((s) => <option key={s}>{s}</option>)}</select></td>
                            <td><input className="num" type="number" min={1} max={4} value={r.header.plies} onChange={(e) => setHeader(i, { plies: Number(e.target.value) })} /></td>
                            <td><input className="num" type="number" min={0} max={3} value={r.sill} onChange={(e) => setRule(i, { sill: Number(e.target.value) })} /></td>
                            <td>{spec.openings.length > 1 && <button type="button" className="icon mini" title="Remove the band" onClick={() => set({ openings: spec.openings.filter((_, k) => k !== i) })}><Trash2 size={11} /></button>}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    <button type="button" className="btn compact" onClick={() => set({ openings: [...spec.openings, { ...spec.openings[spec.openings.length - 1], maxWidth: 99 }].sort((a, b) => a.maxWidth - b.maxWidth) })}><Plus size={12} /> Band</button>
                  </>
                )}
              </div>
              <div className="joinery-preview">
                {on && preview && !preview.problem && <FrameElevation f={preview} />}
                {on && preview?.problem && <p className="error-text">{preview.problem}</p>}
                {on && preview && !preview.problem && (
                  <>
                    <p className="muted small">A sample wall, 6 m × 2.7 m: a window 1200 × 1300, a door 900 × 2100.</p>
                    <p className="muted small">{Object.entries(preview.counts).map(([k, v]) => `${v} ${k}`).join(' · ')}</p>
                    {preview.warnings.map((w, i) => <p key={i} className="error-text">{w}</p>)}
                  </>
                )}
                {!on && <p className="muted small">Built of its layers. Tick Timber frame to build its core as studs, plates and the openings' framing.</p>}
              </div>
              <div className="row joinery-actions">
                <span className="grow" />
                <button type="button" className="btn primary" disabled={Boolean(on && preview?.problem)} onClick={save}>Save type</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
