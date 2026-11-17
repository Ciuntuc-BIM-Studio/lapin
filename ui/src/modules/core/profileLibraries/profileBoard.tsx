/**
 * A section profile drawn on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4; report #128): Draw a
 * profile… and Edit its outline… open a blank sheet of its own (`studio.editProfile`: a reference plane seeing nothing of the model,
 * its x and y the section's), each ring drawn as a closed polyline and the section's axes as construction lines. Draft's tools,
 * snaps, grips, constraints and dimensions work on it; the side form keeps the editor's name, material and library, the shapes to
 * start from with their sizes, a chamber in the middle, the size and area, and a ring's points in millimetres. Finish sketch makes
 * the profile (`studio.finishProfile`: crossing rings refused, saying why, the board kept open; centred) and goes back to the
 * libraries to keep it, as the editor did; Cancel goes back to them. A product without Studio keeps the editor in the dialog.
 */

import { useEffect, useState } from 'react';
import { Plus } from 'lucide-react';
import { create } from 'zustand';
import { notify, useUi } from '../../../app/store';
import { execute, query, useSession } from '../../../engine/session';
import type { NodeDto } from '../../../engine/protocol';
import { hasModule } from '../../../platform/brand';
import { frameBoard, registerBoard, rememberReturn, SKETCH_TOOLS, takeReturn, type Board } from '../../../viewport/sketch/board';
import { MATERIALS, MATERIAL_LABELS, type ProfileJson } from './model';
import { holeIn, starter, STARTERS, type MmRing, type Pt, type Sizes, type Starter } from './sectionEditor';

interface ProfileOf { name: string; material: string; library: string; rings: Pt[][]; profile?: ProfileJson }

interface BoardRings { kind: 'profile'; rings?: Pt[][]; width?: number; depth?: number; area?: number; problem?: string }

/** A profile finished on its board, waiting for the libraries dialog to name and keep it. */
export const useBoardProfile = create<{ done: { profile: ProfileJson; library: string } | null }>(() => ({ done: null }));

/** Whether profiles are drawn on the canvas: a product with Studio. */
export const profilesOnBoard = (): boolean => hasModule('studio');

/** The side form's fields, per board (kept while the board is open, given to Finish). */
const fields = new Map<string, { name: string; material: string; library: string }>();

/** Opens a section on its own board: rings in metres, or a profile whose outline is drawn. */
export async function openProfileBoard(start: { name: string; material: string; library: string; rings?: Pt[][]; profile?: ProfileJson }): Promise<boolean> {
  const u = useUi.getState();
  const r = await execute('studio.editProfile', { ...start }) as { viewId?: string } | null;
  if (!r?.viewId) { const e = useSession.getState().error; if (e) notify(e); return false; }
  rememberReturn(r.viewId, { view: u.view, planViewId: u.planViewId, drawingViewId: u.drawingViewId });
  useUi.setState({ view: 'drawing', drawingViewId: r.viewId, tool: null, selected: [], selection: null, dialog: null });
  const viewId = r.viewId;
  frameBoard(() => {
    const pts = ((useSession.getState().nodes.get(viewId)?.props.profileOf as ProfileOf | undefined)?.rings ?? []).flat();
    return pts.length ? [Math.min(...pts.map((p) => p[0])), Math.min(...pts.map((p) => p[1])), Math.max(...pts.map((p) => p[0])), Math.max(...pts.map((p) => p[1]))] : null;
  }, { panel: true });
  return true;
}

/** Back to where the board was opened from, the libraries dialog open again. */
function leave(viewId: string) {
  fields.delete(viewId);
  const back = takeReturn(viewId) ?? { view: 'plan', drawingViewId: null };
  useUi.setState({ ...back, dialog: 'profileLibraries' });
}

async function finish(viewId: string): Promise<void> {
  const f = fields.get(viewId);
  const r = await execute('studio.finishProfile', { viewId, ...(f ?? {}) }) as { profile?: ProfileJson; library?: string } | null;
  if (!r?.profile) { const e = useSession.getState().error; if (e) notify(e); return; }
  useBoardProfile.setState({ done: { profile: r.profile, library: r.library ?? 'My profiles' } });
  leave(viewId);
}

async function cancel(viewId: string): Promise<void> {
  await execute('studio.cancelProfile', { viewId });
  if (!useSession.getState().error) leave(viewId);
}

const toMm = (rings: Pt[][]): MmRing[] => rings.map((r) => r.map(([x, y]): Pt => [Math.round(x * 1e6) / 1e3, Math.round(y * 1e6) / 1e3]));
const toM = (rings: MmRing[]): Pt[][] => rings.map((r) => r.map(([x, y]): Pt => [x / 1000, y / 1000]));
const num = (v: string) => { const n = Number(v.trim().replace(',', '.')); return Number.isFinite(n) ? n : null; };

/** The side form: name, material, library; the shapes to start from; a chamber; the size; a ring's points (mm). */
function ProfilePanel({ board }: { board: Board }) {
  const version = useSession((s) => s.version);
  const of = useSession((s) => s.nodes.get(board.viewId)?.props.profileOf) as ProfileOf | undefined;
  const [f, setF] = useState(() => fields.get(board.viewId) ?? { name: of?.name ?? 'Drawn profile', material: of?.material ?? 'aluminium', library: of?.library ?? 'My profiles' });
  const [sizes, setSizes] = useState<Sizes>({ width: 50, depth: 100, wall: 3 });
  const [hole, setHole] = useState({ w: 30, h: 60 });
  const [ring, setRing] = useState(0);
  const [now, setNow] = useState<BoardRings | null>(null);
  useEffect(() => { fields.set(board.viewId, f); }, [board.viewId, f]);
  useEffect(() => {
    let live = true;
    void query<BoardRings>('studio.profileBoard', { viewId: board.viewId }).then((r) => { if (live) setNow(r); });
    return () => { live = false; };
  }, [board.viewId, version]);
  const rings = now?.rings ? toMm(now.rings) : null;
  const put = (mm: MmRing[]) => void execute('studio.setBoardShapes', { viewId: board.viewId, shapes: toM(mm).map((pts) => ({ type: 'polyline', pts, closed: true })) });
  const current = rings?.[ring] ?? rings?.[0] ?? null;
  const setPoint = (i: number, p: Pt) => { if (rings) put(rings.map((r, a) => (a === Math.min(ring, rings.length - 1) ? r.map((q, b) => (b === i ? p : q)) : r))); };
  const size = (key: keyof Sizes, label: string) => (
    <label className="small"><span className="eyebrow">{label}</span>{' '}
      <input className="num mini" style={{ width: 52 }} defaultValue={sizes[key]} key={`${key}${sizes[key]}`} data-field={`editor-${key}`}
        onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setSizes({ ...sizes, [key]: n }); }} /> <span className="muted">mm</span></label>
  );
  const r1 = (v: number) => Math.round(v * 1e4) / 10;
  return (
    <div className="sketch-board-panel" data-testid="profile-board-panel" data-i18n-ctx="profileEditor">
      <label className="field-line"><span className="eyebrow">Name</span><input value={f.name} data-testid="profile-editor-name" onChange={(e) => setF({ ...f, name: e.target.value })} /></label>
      <label className="field-line"><span className="eyebrow">Material</span>
        <select value={f.material} onChange={(e) => setF({ ...f, material: e.target.value })}>{MATERIALS.map((x) => <option key={x} value={x}>{MATERIAL_LABELS[x]}</option>)}</select></label>
      <label className="field-line"><span className="eyebrow">Library</span><input value={f.library} onChange={(e) => setF({ ...f, library: e.target.value })} /></label>
      <div className="row wrap" style={{ gap: 4 }}>
        <span className="eyebrow">Start from</span>
        {STARTERS.map(([kind, label]) => <button key={kind} type="button" className="btn small" data-action={`editor-${kind}`} onClick={() => { put(starter(kind as Starter, sizes)); setRing(0); }}>{label}</button>)}
      </div>
      <div className="row wrap" style={{ gap: 6 }}>{size('width', 'Width')}{size('depth', 'Depth')}{size('wall', 'Wall thickness')}</div>
      <div className="row wrap" style={{ gap: 4, alignItems: 'center' }}>
        <span className="eyebrow">Chamber</span>
        <input className="num mini" style={{ width: 44 }} defaultValue={hole.w} key={`hw${hole.w}`} title="Chamber width (mm)" onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setHole({ ...hole, w: n }); }} />×
        <input className="num mini" style={{ width: 44 }} defaultValue={hole.h} key={`hh${hole.h}`} title="Chamber depth (mm)" onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setHole({ ...hole, h: n }); }} />
        <button type="button" className="btn small" data-action="editor-add-hole" title="A rectangular chamber in the middle" onClick={() => {
          const h = rings ? holeIn(rings, hole.w, hole.h) : null;
          if (!h) { notify(rings ? 'The chamber must be smaller than the section.' : now?.problem ?? 'Draw the section first.'); return; }
          void execute('drafting.draw', { viewId: board.viewId, shapes: [{ type: 'polyline', pts: toM([h])[0], closed: true }] });
        }}><Plus size={12} />Chamber</button>
      </div>
      {now?.problem && <p className="small problem" data-testid="board-problem">{now.problem}</p>}
      {rings && now?.width != null && (
        <p className="small" data-testid="profile-board-size"><span className="eyebrow">Size</span> <span data-no-i18n>{`${r1(now.width)} × ${r1(now.depth ?? 0)} mm · ${Math.round((now.area ?? 0) * 1e8) / 100} mm²`}</span></p>
      )}
      {rings && rings.length > 1 && (
        <div className="row wrap" style={{ gap: 4 }}>
          {rings.map((_, a) => <button key={a} type="button" className={`btn small${a === ring ? ' on' : ''}`} onClick={() => setRing(a)}>{a === 0 ? 'Outside' : <><span>Chamber</span> <span data-no-i18n>{a}</span></>}</button>)}
        </div>
      )}
      {current && current.length <= 40 && (
        <table className="grid small">
          <thead><tr><th>#</th><th data-no-i18n>x (mm)</th><th data-no-i18n>y (mm)</th></tr></thead>
          <tbody>
            {current.map(([x, y], i) => (
              <tr key={`${ring}:${i}:${x}:${y}`}>
                <td data-no-i18n>{i + 1}</td>
                <td><input className="num mini" defaultValue={x} onBlur={(e) => { const n = num(e.target.value); if (n != null && n !== x) setPoint(i, [n, y]); }} /></td>
                <td><input className="num mini" defaultValue={y} onBlur={(e) => { const n = num(e.target.value); if (n != null && n !== y) setPoint(i, [x, n]); }} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

/** A section profile's board: Finish makes the profile, for the libraries to keep. */
function profileBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.profileOf as ProfileOf | undefined;
  if (!of || typeof of !== 'object' || !Array.isArray(of.rings)) return null;
  return {
    kind: 'profile', viewId, title: fields.get(viewId)?.name ?? of.name, hint: 'Draw the outside and its chambers as closed shapes',
    tools: SKETCH_TOOLS, constraints: true, finish: () => finish(viewId), cancel: () => cancel(viewId), panel: ProfilePanel,
  };
}

registerBoard(profileBoard);
