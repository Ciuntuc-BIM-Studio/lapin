/**
 * Make drawing… (docs/plan-shop-drawings.md §6, decision 581): the shop drawing of one element. On the left the element
 * itself, the engine's iso of it with its frame's axes (X red, Y green, Z blue) and a check box on each of its six faces
 * (and an Iso one) for the views to put on the sheet, under it the sections parallel to its faces; on the right the rule,
 * scale, paper, projection, dimensions, and the sheet as the engine composes it, asked again a moment after each choice.
 * Create makes the drawing in one undo step (made again, it updates the same one) and opens its sheet.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import { drawsOnDark, type DrawingDto } from '../../viewport/drawing';
import { fitBounds } from './typePreviewState';
import {
  FACES, PAPERS, SCALES, SECTION_ROLES, axisRange, clampStation, defaultChoices, drawableElement, drawingArgs, mm, newSection, nothingChosen,
  type Choices, type Face, type RulesDto, type SectionRole,
} from './shopDrawing';

type Pt = [number, number];
interface IsoDto extends DrawingDto { axes?: { origin: Pt; x: Pt; y: Pt; z: Pt }; faces?: Record<Face, Pt> }
interface PageDto extends DrawingDto { w: number; h: number; paper?: string; scale?: number; fits?: boolean; problem?: string }
type Created = { id: string; sheet: string; paper: string; scale: number; fits: boolean };

const ISO_W = 380, ISO_H = 290, PAGE_W = 460, PAGE_H = 330;
const FACE_LABEL: Record<Face, string> = { front: 'Front', back: 'Back', top: 'Top', bottom: 'Bottom', left: 'Left', right: 'Right' };
const AXIS_COLOUR = { x: '#d6372b', y: '#2f9e44', z: '#1c6ed5' };
const AXIS_LABEL: Record<SectionRole, string> = { 'section:x': 'X · along the part', 'section:y': 'Y · across', 'section:z': 'Z · up' };

export function ShopDrawingDialog() {
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const element = useMemo(() => drawableElement(selected, nodes), [selected, nodes]);
  const close = () => useUi.setState({ dialog: null });
  const [rules, setRules] = useState<RulesDto | null>(null);
  const [iso, setIso] = useState<IsoDto | null>(null);
  const [c, setC] = useState<Choices | null>(null);
  const [page, setPage] = useState<PageDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The element's rules, its frame and its iso, once.
  useEffect(() => {
    if (!element) return;
    let live = true;
    void query<RulesDto>('drawing.rules', { id: element }).then((r) => { if (live && r) { setRules(r); setC(defaultChoices(r)); } });
    void query<IsoDto>('elementView.iso', { element }).then((r) => { if (live && r) setIso(r); });
    return () => { live = false; };
  }, [element]);

  // The sheet as the engine composes it, a moment after the last choice; an answer to an older question is dropped.
  const asked = useRef(0);
  const choicesJson = JSON.stringify(c);
  useEffect(() => {
    if (!element || !c || !rules) return;
    const n = ++asked.current;
    setBusy(true);
    const timer = setTimeout(() => {
      void query<PageDto>('drawing.preview', drawingArgs(element, c, rules.drawing)).then((r) => {
        if (n !== asked.current) return;
        setPage(r); setBusy(false);
      });
    }, 250);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [element, choicesJson, rules]);

  if (!element) {
    return (
      <div className="dialog-backdrop" onClick={close}>
        <div className="dialog" role="dialog" aria-label="Make drawing" onClick={(e) => e.stopPropagation()}>
          <header className="dialog-head"><span className="panel-title">Make drawing</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
          <div className="dialog-body"><p className="muted">Select one element of the model to make its drawing.</p></div>
        </div>
      </div>
    );
  }

  const frame = rules?.frame ?? null;
  const set = (patch: Partial<Choices>) => setC((old) => (old ? { ...old, ...patch } : old));
  const toggleFace = (f: Face) => c && set({ views: c.views.includes(f) ? c.views.filter((x) => x !== f) : [...c.views, f] });
  const setSection = (i: number, station: number) => c && set({ sections: c.sections.map((s, k) => (k === i ? { ...s, station: clampStation(frame, s.role, station) } : s)) });
  const create = async () => {
    if (!c || !rules) return;
    setError(null);
    const r = await execute('drawing.create', drawingArgs(element, c)) as Created | null;
    if (!r) { setError('The drawing could not be made.'); return; }
    useUi.setState({ dialog: null, view: 'sheet', sheetId: r.sheet, selection: r.sheet, selected: [r.sheet] });
    notify(r.fits ? `Drawing made: ${r.paper}, 1:${r.scale}.` : `Drawing made: ${r.paper}, 1:${r.scale}, but it does not fit; choose a larger paper or a smaller scale.`);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide shop-drawing" data-i18n-ctx="shop" role="dialog" aria-label="Make drawing" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Make drawing</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body shop-body">
          <div className="shop-left">
            <span className="eyebrow">Views</span>
            <IsoPicture iso={iso} c={c} toggleFace={toggleFace} setIso={(v) => set({ iso: v })} />
            <span className="eyebrow">Sections</span>
            <label className="check shop-at-features"><input type="checkbox" data-testid="shop-at-features" checked={!!c?.atFeatures} disabled={!c} onChange={(e) => set({ atFeatures: e.target.checked })} />Sections at the notches</label>
            {SECTION_ROLES.map((role) => {
              const [lo, hi] = axisRange(frame, role);
              return (
                <div key={role} className="shop-sections" data-role={role}>
                  <div className="shop-axis"><span className="grow">{AXIS_LABEL[role]}</span>
                    <button type="button" className="btn small" disabled={!c || !frame} onClick={() => c && set({ sections: [...c.sections, newSection(frame, role)] })}><Plus size={12} /> Add</button></div>
                  {c?.sections.map((s, i) => s.role !== role ? null : (
                    <div key={i} className="shop-section">
                      <input type="range" min={lo} max={hi} step={0.001} value={s.station} onChange={(e) => setSection(i, Number(e.target.value))} aria-label="Section station" />
                      <span className="shop-station">{mm(s.station)}</span>
                      <button type="button" className="icon" title="Remove" onClick={() => set({ sections: c.sections.filter((_, k) => k !== i) })}><X size={13} /></button>
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
          <div className="shop-right">
            {c && rules && (
              <div className="shop-fields">
                <label>Rule<select value={c.rule} onChange={(e) => setC(defaultChoices(rules, e.target.value, c))}>
                  {rules.rules.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                </select></label>
                <label>Scale<select data-testid="shop-scale" value={c.scale ?? ''} onChange={(e) => set({ scale: e.target.value ? Number(e.target.value) : null })}>
                  <option value="">Auto</option>
                  {SCALES.map((s) => <option key={s} value={s}>1:{s}</option>)}
                </select></label>
                <label>Paper<select data-testid="shop-paper" value={c.paper ?? ''} onChange={(e) => set({ paper: e.target.value || null })}>
                  <option value="">Auto</option>
                  {PAPERS.map((p) => <option key={p} value={p}>{p}</option>)}
                </select></label>
                <label>Projection<select value={c.projection} onChange={(e) => set({ projection: e.target.value as Choices['projection'] })}>
                  <option value="iso-e">ISO E (first angle)</option>
                  <option value="third-angle">Third angle</option>
                </select></label>
                <label>Dimensions<select value={c.dims} onChange={(e) => set({ dims: e.target.value as Choices['dims'] })}>
                  <option value="overall">Overall</option>
                  <option value="features">Features (holes, notches)</option>
                  <option value="none">None</option>
                </select></label>
                <label>Prefer<select value={c.prefer} onChange={(e) => set({ prefer: e.target.value as Choices['prefer'] })}>
                  <option value="paper">The paper</option>
                  <option value="scale">The scale</option>
                </select></label>
              </div>
            )}
            <PagePreview page={page} busy={busy} />
            {page && !page.problem && <p className="muted small shop-note" data-testid="shop-note">{`${page.paper ?? ''} · 1:${page.scale ?? ''}`}</p>}
            {page && page.fits === false && <p className="error-text shop-warn" role="alert">The views do not fit on this paper at this scale: choose a larger paper or a smaller scale.</p>}
            {c && nothingChosen(c) && <p className="muted small">Tick at least one view or add a section.</p>}
          </div>
        </div>
        <footer className="dialog-foot">
          {error && <p className="error-text" role="alert">{error}</p>}
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-testid="shop-create" disabled={!c || nothingChosen(c)} onClick={() => void create()}>{rules?.drawing ? 'Update' : 'Create'}</button>
        </footer>
      </div>
    </div>
  );
}

function IsoPicture({ iso, c, toggleFace, setIso }: { iso: IsoDto | null; c: Choices | null; toggleFace: (f: Face) => void; setIso: (v: boolean) => void }) {
  const onDark = drawsOnDark(useUi((s) => s.theme));
  const fitted = useMemo(() => {
    if (!iso) return null;
    const pts: Pt[] = [...(iso.axes ? [iso.axes.origin, iso.axes.x, iso.axes.y, iso.axes.z] : []), ...Object.values(iso.faces ?? {})];
    const b = iso.bounds ?? [Infinity, Infinity, -Infinity, -Infinity];
    const box: [number, number, number, number] = [Math.min(b[0], ...pts.map((p) => p[0])), Math.min(b[1], ...pts.map((p) => p[1])), Math.max(b[2], ...pts.map((p) => p[0])), Math.max(b[3], ...pts.map((p) => p[1]))];
    return fitBounds(Number.isFinite(box[0]) ? box : undefined, ISO_W, ISO_H, 34);
  }, [iso]);
  const at = (p: Pt) => fitted!.toScreen({ x: p[0], y: p[1] });
  return (
    <div className="shop-iso" style={{ width: ISO_W, height: ISO_H }} data-testid="shop-iso">
      <svg className="drawing-svg" width={ISO_W} height={ISO_H} viewBox={`0 0 ${ISO_W} ${ISO_H}`} role="img" aria-label="The element">
        <rect className="paper" x={0} y={0} width={ISO_W} height={ISO_H} />
        {iso && fitted && <DrawingLayer drawing={iso} toScreen={fitted.toScreen} pxPerMetre={fitted.k} onDark={onDark} minText={4} />}
        {iso?.axes && fitted && (['x', 'y', 'z'] as const).map((k) => {
          const o = at(iso.axes!.origin), e = at(iso.axes![k]);
          const a = Math.atan2(e.y - o.y, e.x - o.x);
          const head = (da: number) => `${e.x - 8 * Math.cos(a + da)},${e.y - 8 * Math.sin(a + da)}`;
          return (
            <g key={k} stroke={AXIS_COLOUR[k]} fill={AXIS_COLOUR[k]} strokeWidth={1.6}>
              <line x1={o.x} y1={o.y} x2={e.x} y2={e.y} />
              <polygon points={`${e.x},${e.y} ${head(0.4)} ${head(-0.4)}`} stroke="none" />
              <text x={e.x + 7 * Math.cos(a)} y={e.y + 7 * Math.sin(a) + 4} fontSize={11} stroke="none" textAnchor="middle">{k.toUpperCase()}</text>
            </g>
          );
        })}
      </svg>
      {iso?.faces && fitted && FACES.map((f) => {
        const p = at(iso.faces![f]);
        return (
          <label key={f} className="shop-face" data-face={f} style={{ left: p.x, top: p.y }}>
            <input type="checkbox" checked={!!c?.views.includes(f)} onChange={() => toggleFace(f)} /> <span>{FACE_LABEL[f]}</span>
          </label>
        );
      })}
      <label className="shop-face shop-iso-box" data-face="iso"><input type="checkbox" checked={!!c?.iso} onChange={(e) => setIso(e.target.checked)} /> <span>Iso</span></label>
    </div>
  );
}

function PagePreview({ page, busy }: { page: PageDto | null; busy: boolean }) {
  const onDark = false;
  const k = page ? Math.min((PAGE_W - 12) / page.w, (PAGE_H - 12) / page.h) : 1;
  const ox = page ? (PAGE_W - page.w * k) / 2 : 0, oy = page ? (PAGE_H - page.h * k) / 2 : 0;
  const toScreen = (p: { x: number; y: number }) => ({ x: p.x * k + ox, y: p.y * k + oy });
  return (
    <svg className="shop-page" width={PAGE_W} height={PAGE_H} viewBox={`0 0 ${PAGE_W} ${PAGE_H}`} role="img" aria-label="The sheet" aria-busy={busy} data-testid="shop-page">
      {page && !page.problem && (
        <>
          <rect x={ox} y={oy} width={page.w * k} height={page.h * k} className="paper-sheet" />
          <DrawingLayer drawing={{ pens: page.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={k} onDark={onDark} minText={1} />
        </>
      )}
    </svg>
  );
}
