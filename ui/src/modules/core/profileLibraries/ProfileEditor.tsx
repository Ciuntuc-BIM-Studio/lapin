/**
 * Draw or edit a section (report #128, a tester's request for custom aluminium profiles): the outside and its chambers as rings of
 * points in millimetres, dragged on the drawing (1 mm; Shift: 5 mm) or typed in the table; a click on the outline adds a point, a
 * right-click takes one out; the shapes to start from (a rectangle, a hollow box, a T, an angle, a channel) with their sizes; a
 * chamber added in the middle. Made into a profile by the engine (which refuses a ring that crosses itself or another, and
 * centres the section), it goes to the libraries as any other: copied into the project, assigned to members, updated from here.
 */

import { useMemo, useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { notify } from '../../../app/store';
import { query } from '../../../engine/session';
import { fit, MATERIALS, MATERIAL_LABELS, type ProfileJson, type Ring } from './model';
import { holeIn, insertPoint, measure, movePoint, nearestEdge, removePoint, starter, STARTERS, type MmRing, type Pt, type Sizes, type Starter } from './sectionEditor';

export interface EditorStart { name: string; material: string; rings: MmRing[]; library: string }

export function ProfileEditor({ start, onCancel, onDone }: { start: EditorStart; onCancel: () => void; onDone: (profile: ProfileJson, library: string) => void }) {
  const [rings, setRings] = useState<MmRing[]>(start.rings.length ? start.rings : starter('box', { width: 50, depth: 100, wall: 3 }));
  const [ring, setRing] = useState(0);
  const [name, setName] = useState(start.name);
  const [material, setMaterial] = useState(start.material);
  const [library, setLibrary] = useState(start.library);
  const [sizes, setSizes] = useState<Sizes>({ width: 50, depth: 100, wall: 3 });
  const [hole, setHole] = useState({ w: 30, h: 60 });
  const [drag, setDrag] = useState<{ ring: number; i: number } | null>(null);
  const [busy, setBusy] = useState(false);
  const svg = useRef<SVGSVGElement>(null);

  const W = 380, H = 320;
  const m = useMemo(() => measure(rings), [rings]);
  // The rings fitted in the box, in millimetres (y up); the drawing keeps a margin for the points.
  const { k, x0, y0 } = useMemo(() => fit(rings as Ring[], W, H, 28), [rings]);
  const X = (x: number) => x0 + x * k, Y = (y: number) => y0 - y * k;
  const toMmPt = (e: { clientX: number; clientY: number }): Pt => {
    const r = svg.current!.getBoundingClientRect();
    const px = ((e.clientX - r.left) / r.width) * W, py = ((e.clientY - r.top) / r.height) * H;
    return [(px - x0) / k, (y0 - py) / k];
  };
  const path = rings.map((r) => `M${r.map(([x, y]) => `${X(x).toFixed(1)},${Y(y).toFixed(1)}`).join('L')}Z`).join('');
  const current = rings[ring] ?? rings[0];

  const make = async () => {
    setBusy(true);
    try {
      const profile = await query<ProfileJson>('profiles.fromRings', { rings, unit: 0.001, name: name.trim() || 'Drawn profile', material });
      if (profile) onDone(profile, library.trim() || 'My profiles');
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  const num = (v: string) => { const n = Number(v.trim().replace(',', '.')); return Number.isFinite(n) ? n : null; };
  const size = (key: keyof Sizes, label: string) => (
    <label className="field-line small"><span className="eyebrow">{label}</span>
      <input className="num mini" style={{ width: 60 }} defaultValue={sizes[key]} key={`${key}${sizes[key]}`} data-field={`editor-${key}`}
        onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setSizes({ ...sizes, [key]: n }); }} /> <span className="muted">mm</span></label>
  );
  return (
    <div className="profile-editor" data-testid="profile-editor">
      <div className="row wrap" style={{ alignItems: 'flex-start', gap: 14 }}>
        <div>
          <svg ref={svg} viewBox={`0 0 ${W} ${H}`} width={W} height={H} className="wall-profile-svg" aria-label="Section"
            onPointerMove={(e) => { if (drag) setRings((cur) => movePoint(cur, drag.ring, drag.i, toMmPt(e), e.shiftKey ? 5 : 1)); }}
            onPointerUp={() => setDrag(null)} onPointerLeave={() => setDrag(null)}>
            <line x1={X(0)} y1={0} x2={X(0)} y2={H} stroke="var(--rule-strong)" strokeDasharray="2 3" strokeWidth={0.6} />
            <line x1={0} y1={Y(0)} x2={W} y2={Y(0)} stroke="var(--rule-strong)" strokeDasharray="2 3" strokeWidth={0.6} />
            <path d={path} fill="var(--accent-soft, #d9cdb8)" fillRule="evenodd" stroke="currentColor" strokeWidth={1.2} />
            {/* A click on a ring's outline adds a point there (a wide, transparent line over each edge). */}
            {rings.map((r, a) => r.map((p, i) => {
              const q = r[(i + 1) % r.length];
              return <line key={`e${a}:${i}`} x1={X(p[0])} y1={Y(p[1])} x2={X(q[0])} y2={Y(q[1])} stroke="transparent" strokeWidth={10} style={{ cursor: 'copy' }}
                onClick={(e) => { const hit = nearestEdge(r, toMmPt(e)); setRing(a); setRings((cur) => insertPoint(cur, a, hit.i, hit.foot)); }} />;
            }))}
            {rings.map((r, a) => r.map((p, i) => (
              <circle key={`p${a}:${i}`} cx={X(p[0])} cy={Y(p[1])} r={a === ring ? 5 : 3.5} fill={a === ring ? 'var(--accent)' : 'var(--ink-muted)'} stroke="#fff" strokeWidth={1} style={{ cursor: 'grab' }}
                data-editor-point={`${a}:${i}`}
                onPointerDown={(e) => { (e.target as Element).setPointerCapture?.(e.pointerId); setRing(a); setDrag({ ring: a, i }); }}
                onContextMenu={(e) => { e.preventDefault(); setRings((cur) => removePoint(cur, a, i)); }} />
            )))}
          </svg>
          <p className="muted small">Drag a point (Shift: 5 mm steps); click an outline to add one, right-click a point to take it out.</p>
        </div>
        <div className="grow" style={{ minWidth: 260 }}>
          <label className="field-line"><span className="eyebrow">Name</span><input value={name} data-testid="profile-editor-name" onChange={(e) => setName(e.target.value)} /></label>
          <label className="field-line"><span className="eyebrow">Material</span>
            <select value={material} onChange={(e) => setMaterial(e.target.value)}>{MATERIALS.map((x) => <option key={x} value={x}>{MATERIAL_LABELS[x]}</option>)}</select></label>
          <label className="field-line"><span className="eyebrow">Library</span><input value={library} onChange={(e) => setLibrary(e.target.value)} /></label>
          <div className="row wrap" style={{ gap: 6, margin: '6px 0' }} data-i18n-ctx="profileEditor">
            <span className="eyebrow">Start from</span>
            {STARTERS.map(([kind, label]) => <button key={kind} type="button" className="btn small" data-action={`editor-${kind}`} onClick={() => { setRings(starter(kind as Starter, sizes)); setRing(0); }}>{label}</button>)}
          </div>
          <div className="row wrap" style={{ gap: 8 }}>{size('width', 'Width')}{size('depth', 'Depth')}{size('wall', 'Wall thickness')}</div>
          <div className="row wrap" style={{ gap: 6, margin: '6px 0' }}>
            <span className="eyebrow">Chambers</span>
            {rings.map((_, a) => <button key={a} type="button" className={`btn small${a === ring ? ' on' : ''}`} onClick={() => setRing(a)}>{a === 0 ? 'Outside' : <><span>Chamber</span> <span data-no-i18n>{a}</span></>}</button>)}
            <input className="num mini" style={{ width: 48 }} defaultValue={hole.w} key={`hw${hole.w}`} title="Chamber width (mm)" onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setHole({ ...hole, w: n }); }} />×
            <input className="num mini" style={{ width: 48 }} defaultValue={hole.h} key={`hh${hole.h}`} title="Chamber depth (mm)" onBlur={(e) => { const n = num(e.target.value); if (n && n > 0) setHole({ ...hole, h: n }); }} />
            <button type="button" className="btn small" data-action="editor-add-hole" title="A rectangular chamber in the middle" onClick={() => { const h = holeIn(rings, hole.w, hole.h); if (!h) { notify('The chamber must be smaller than the section.'); return; } setRings([...rings, h]); setRing(rings.length); }}><Plus size={12} />Chamber</button>
            {ring > 0 && <button type="button" className="icon mini" title="Take this chamber out" onClick={() => { setRings(rings.filter((_, a) => a !== ring)); setRing(0); }}><Trash2 size={12} /></button>}
          </div>
          <table className="grid small">
            <thead><tr><th>#</th><th data-no-i18n>x (mm)</th><th data-no-i18n>y (mm)</th></tr></thead>
            <tbody>
              {current.map(([x, y], i) => (
                <tr key={`${ring}:${i}:${x}:${y}`}>
                  <td data-no-i18n>{i + 1}</td>
                  <td><input className="num mini" defaultValue={x} onBlur={(e) => { const n = num(e.target.value); if (n != null) setRings((cur) => movePoint(cur, ring, i, [n, y], 0.001)); }} /></td>
                  <td><input className="num mini" defaultValue={y} onBlur={(e) => { const n = num(e.target.value); if (n != null) setRings((cur) => movePoint(cur, ring, i, [x, n], 0.001)); }} /></td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="small"><span className="eyebrow">Size</span> <span data-no-i18n>{`${m.width} × ${m.depth} mm · ${m.area} mm²`}</span></p>
        </div>
      </div>
      <div className="row"><span className="grow" /><button type="button" className="btn" onClick={onCancel}>Back</button><button type="button" className="btn primary" disabled={busy || !name.trim()} data-testid="profile-editor-make" onClick={() => void make()}>Make the profile</button></div>
    </div>
  );
}
