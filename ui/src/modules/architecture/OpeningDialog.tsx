/**
 * A wall opening's small configurator (a tester's report #114, decision 392): its size, sill, head, reveal finish and
 * lintel over a picture of it in its wall, applied as one step to the openings selected; and the opening made a door or
 * a window of a type, where it is, its GlobalId kept (fitted: a type of its size and head).
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { mm, ofClass } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { LengthField } from '../../shell/cells';
import { outline, type HeadKind, type Pt } from './openingShape';

/** The opening the dialog edits, and whether it opens on its Change to part. */
export const openingDialog: { current: { id: string; ids: string[]; change?: 'door' | 'window' } | null } = { current: null };

export function openOpeningDialog(id: string, ids: string[], change?: 'door' | 'window'): void {
  openingDialog.current = { id, ids: ids.length ? ids : [id], change };
  useUi.setState({ dialog: 'wallOpening' });
}

export type RevealKind = 'none' | 'plaster' | 'lining';
export const REVEALS: { kind: RevealKind; label: string }[] = [{ kind: 'none', label: 'None' }, { kind: 'plaster', label: 'Plaster' }, { kind: 'lining', label: 'Lining' }];
export const HEAD_KINDS: { kind: HeadKind; label: string }[] = [
  { kind: 'rect', label: 'Rectangle' }, { kind: 'segmental', label: 'Segmental arch' }, { kind: 'elliptical', label: 'Elliptical arch' },
  { kind: 'round', label: 'Semicircle' }, { kind: 'radiused', label: 'Radiused corners' },
];
/** A reveal's own thickness and material, as the engine's BareOpenings.DefaultThickness and DefaultMaterial. */
export const revealThickness = (kind: RevealKind) => (kind === 'lining' ? 0.02 : 0.015);
export const revealMaterial = (kind: RevealKind) => (kind === 'lining' ? 'Timber lining' : 'Plaster');

export interface OpeningDraft { width: number; height: number; sill: number; shape: HeadKind; rise: number | null; reveal: RevealKind; revealThickness: number | null; revealMaterial: string | null; lintel: 'wall' | 'none' }

const n = (v: unknown, d: number) => (typeof v === 'number' ? v : d);
export function draftOf(node: NodeDto): OpeningDraft {
  const k = node.props.shape;
  const r = node.props.reveal;
  return {
    width: n(node.props.width, 1), height: n(node.props.height, 2), sill: n(node.props.sill, 0),
    shape: k === 'segmental' || k === 'elliptical' || k === 'round' || k === 'radiused' || k === 'circle' ? k : 'rect',
    rise: typeof node.props.rise === 'number' ? node.props.rise : null,
    reveal: r === 'plaster' || r === 'lining' ? r : 'none',
    revealThickness: typeof node.props.revealThickness === 'number' ? node.props.revealThickness : null,
    revealMaterial: typeof node.props.revealMaterial === 'string' ? node.props.revealMaterial : null,
    lintel: node.props.lintel === 'none' ? 'none' : 'wall',
  };
}

/** What the draft changes, as updateWallOpening's arguments (only the fields that differ). */
export function changes(was: OpeningDraft, now: OpeningDraft): Record<string, unknown> {
  const o: Record<string, unknown> = {};
  for (const k of ['width', 'height', 'sill'] as const) if (Math.abs(was[k] - now[k]) > 1e-9) o[k] = now[k];
  if (was.shape !== now.shape || was.rise !== now.rise) { o.shape = now.shape; o.rise = now.rise; }
  if (was.reveal !== now.reveal) o.reveal = now.reveal;
  if (now.reveal !== 'none') {
    if (was.revealThickness !== now.revealThickness || was.reveal !== now.reveal) o.revealThickness = now.revealThickness;
    if (was.revealMaterial !== now.revealMaterial || was.reveal !== now.reveal) o.revealMaterial = now.revealMaterial;
  }
  if (was.lintel !== now.lintel) o.lintel = now.lintel;
  return o;
}

/** The opening in its wall, as the outside sees it: the hole, its reveal's finish, the lintel over it. */
function Picture({ d }: { d: OpeningDraft }) {
  const t = d.reveal === 'none' ? 0 : d.revealThickness ?? revealThickness(d.reveal);
  const margin = 0.4;
  const [w, h] = [d.width + 2 * margin, d.sill + d.height + 0.6];
  const s = 220 / Math.max(w, h);
  const pt = (p: Pt) => `${((p.x + w / 2) * s).toFixed(1)},${((h - d.sill - p.y) * s).toFixed(1)}`;
  const poly = (ps: Pt[]) => ps.map(pt).join(' ');
  const hole = outline(d.shape, d.width, d.height, d.rise);
  const clear = t > 0 && 2 * t < d.width && t < d.height ? outline(d.shape, d.width, d.height, d.rise, t, d.sill > 1e-6 ? t : 0) : null;
  const lintelY = d.height + 0.02;
  return (
    <svg className="opening-picture" width={w * s} height={h * s} viewBox={`0 0 ${w * s} ${h * s}`} role="img" aria-label="Opening">
      <rect x={0} y={0} width={w * s} height={h * s} fill="var(--surface-2, #e9e6df)" stroke="var(--line, #888)" />
      <polygon points={poly(hole)} fill={clear ? '#d9cbb3' : 'var(--bg, #fff)'} stroke="var(--text, #333)" strokeWidth={1} />
      {clear && <polygon points={poly(clear)} fill="var(--bg, #fff)" stroke="var(--text, #333)" strokeWidth={0.6} />}
      {d.lintel === 'wall' && <rect x={(w / 2 - d.width / 2 - 0.25) * s} y={(h - d.sill - lintelY - 0.2) * s} width={(d.width + 0.5) * s} height={0.2 * s} fill="none" stroke="var(--text, #333)" strokeDasharray="4 3" />}
      <line x1={0} y1={h * s - 0.5} x2={w * s} y2={h * s - 0.5} stroke="var(--text, #333)" />
    </svg>
  );
}

export function OpeningDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const target = openingDialog.current;
  const node = target ? nodes.get(target.id) : undefined;
  const [draft, setDraft] = useState<OpeningDraft | null>(() => (node ? draftOf(node) : null));
  const types = [...ofClass({ nodes, relations }, 'IfcDoorType'), ...ofClass({ nodes, relations }, 'IfcWindowType')];
  // A kind with no type yet in the project: its first type is made for it (kind:door, kind:window).
  const [typeId, setTypeId] = useState<string>(() => (target?.change === 'window' ? ofClass({ nodes, relations }, 'IfcWindowType')[0]?.id ?? 'kind:window' : ofClass({ nodes, relations }, 'IfcDoorType')[0]?.id ?? 'kind:door'));
  const [fit, setFit] = useState(true);
  const close = () => { openingDialog.current = null; useUi.setState({ dialog: null }); };
  if (!target || !node || !draft) return null;
  const set = (patch: Partial<OpeningDraft>) => setDraft({ ...draft, ...patch });
  const was = draftOf(node);
  const diff = changes(was, draft);
  const dirty = Object.keys(diff).length > 0;
  const t = draft.reveal === 'none' ? 0 : draft.revealThickness ?? revealThickness(draft.reveal);
  const materials = [...nodes.values()].filter((m) => m.type === 'IfcMaterial').map((m) => m.name).sort((a, b) => a.localeCompare(b));
  const material = draft.revealMaterial ?? revealMaterial(draft.reveal);
  // The engine's refusal (it would not fit) shows as the session's error, and the dialog stays.
  const apply = async () => { if (!dirty) return; await execute('architecture.updateWallOpening', { ids: target.ids, ...diff }); if (!useSession.getState().error) close(); };
  const change = async () => {
    if (!typeId) return;
    if (dirty) { await execute('architecture.updateWallOpening', { ids: target.ids, ...diff }); if (useSession.getState().error) return; }
    const r = await execute('architecture.openingToFill', { id: target.id, ...(typeId.startsWith('kind:') ? { kind: typeId.slice(5) } : { typeId }), fit }) as { id?: string } | null | undefined;
    if (r?.id) { select(r.id); close(); }
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Wall opening" data-dialog="wall-opening" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Wall opening</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body row" style={{ alignItems: 'flex-start', gap: 16 }}>
          <dl className="fields">
            <dt>Width</dt><dd><LengthField metres={draft.width} min={0.05} onCommit={(m) => m != null && set({ width: m })} /></dd>
            <dt>Height</dt><dd><LengthField metres={draft.height} min={0.05} onCommit={(m) => m != null && set({ height: m })} /></dd>
            <dt>Sill</dt><dd><LengthField metres={draft.sill} min={0} onCommit={(m) => m != null && set({ sill: m })} /></dd>
            <dt>Head</dt>
            <dd><select data-field="opening-dialog-head" value={draft.shape} onChange={(e) => set({ shape: e.target.value as HeadKind, rise: null })}>
              {HEAD_KINDS.map((h) => <option key={h.kind} value={h.kind}>{h.label}</option>)}
            </select></dd>
            <dt>Reveal finish</dt>
            <dd><select data-field="opening-dialog-reveal" value={draft.reveal} onChange={(e) => set({ reveal: e.target.value as RevealKind, revealThickness: null, revealMaterial: null })}>
              {REVEALS.map((r) => <option key={r.kind} value={r.kind}>{r.label}</option>)}
            </select></dd>
            {draft.reveal !== 'none' && <>
              <dt>Finish thickness</dt><dd><LengthField metres={t} min={0.001} onCommit={(m) => set({ revealThickness: m })} /></dd>
              <dt>Finish material</dt>
              <dd><select value={material} onChange={(e) => set({ revealMaterial: e.target.value })}>
                {[...new Set([material, ...materials])].map((m) => <option key={m} value={m}>{m}</option>)}
              </select></dd>
              <dt>Clear opening</dt><dd className="num">{mm(Math.max(0, draft.width - 2 * t))} × {mm(Math.max(0, draft.height - t - (draft.sill > 1e-6 ? t : 0)))}</dd>
            </>}
            <dt>Lintel</dt>
            <dd><select data-field="opening-dialog-lintel" value={draft.lintel} onChange={(e) => set({ lintel: e.target.value === 'none' ? 'none' : 'wall' })}>
              <option value="wall">The wall’s rule</option><option value="none">None</option>
            </select></dd>
          </dl>
          <Picture d={draft} />
        </div>
        <div className="dialog-body">
          <p className="eyebrow">Change to</p>
          <p className="muted small">The door or window of the type chosen fills the opening where it is; its place, sill and GlobalId stay.</p>
          <div className="row">
            <select data-field="opening-dialog-type" value={typeId} onChange={(e) => setTypeId(e.target.value)}>
              <optgroup label="Door">{types.some((ty) => ty.type === 'IfcDoorType') ? types.filter((ty) => ty.type === 'IfcDoorType').map((ty) => <option key={ty.id} value={ty.id}>{ty.name}</option>) : <option value="kind:door">Door</option>}</optgroup>
              <optgroup label="Window">{types.some((ty) => ty.type === 'IfcWindowType') ? types.filter((ty) => ty.type === 'IfcWindowType').map((ty) => <option key={ty.id} value={ty.id}>{ty.name}</option>) : <option value="kind:window">Window</option>}</optgroup>
            </select>
            <label><input type="checkbox" checked={fit} onChange={(e) => setFit(e.target.checked)} /> Fit the type to the opening (a type of its size)</label>
            <button type="button" className="btn" data-action="opening-change" onClick={() => void change()}>Change</button>
          </div>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-action="opening-apply" disabled={!dirty} onClick={() => void apply()}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
