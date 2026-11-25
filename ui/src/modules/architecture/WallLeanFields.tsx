/**
 * A wall's lean (docs/plan-wall-lean-taper.md, stage L1; in-app report #191): degrees from vertical, + towards its left face,
 * the level that stays put and how its doors and windows stand. On a wall (its own; empty: its type's; several selected walls at
 * once, through element.update) and on a wall type (its walls' default). The engine builds it (`WallLean`): the layers keep
 * their thickness across the faces, the height stays vertical, the joins follow the faces' planes.
 */

import { create } from 'zustand';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { LengthField, NumberField } from '../../shell/cells';
import { TAPER_TIP } from './WallTaperFields';
import { sameKind } from '../../shell/params';

export const LEAN_TIP = 'Degrees from vertical: + leans towards the left face (L), − towards the right face (R); empty: its type\'s. The layers keep their thickness across the faces and the height stays vertical. Not yet for curved walls, walls rising to a roof or with a top profile, wrapped ends or layer extents: those stand plumb and say so.';
export const LEAN_FROM_TIP = 'The level that stays put when the wall leans: its base (on its footing or slab edge), its top, or its mid-height.';
export const LEAN_CUT_TIP = 'Doors and windows in a leaning wall: leaning with it (their jambs in its plane), or plumb. Their sill and head stay level either way.';

export type LeanFrom = 'base' | 'top' | 'axis';
export type LeanCut = 'wall' | 'vertical';

/** A lean as a batter for civil users: n:1 (n up for 1 across); empty for a plumb wall. */
export function batter(deg: number | null | undefined): string {
  if (typeof deg !== 'number' || Math.abs(deg) < 1e-9) return '';
  const n = 1 / Math.tan((Math.abs(deg) * Math.PI) / 180);
  return `${n >= 10 ? n.toFixed(0) : n.toFixed(1)}:1`;
}

const fromOf = (v: unknown): LeanFrom | null => (v === 'base' || v === 'top' || v === 'axis' ? v : null);
const cutOf = (v: unknown): LeanCut | null => (v === 'wall' || v === 'vertical' ? v : null);

/** The three fields as a type's dialog keeps them: its values and their setters. */
export interface LeanValues { lean: number | null; leanFrom: LeanFrom | null; openingCut: LeanCut | null }

export const NO_LEAN: LeanValues = { lean: null, leanFrom: null, openingCut: null };

export const leanValues = (props: Record<string, unknown>): LeanValues => ({
  lean: typeof props.lean === 'number' ? props.lean : null,
  leanFrom: fromOf(props.leanFrom),
  openingCut: cutOf(props.openingCut),
});

function FromSelect({ value, onChange, inherit, disabled, varies }: { value: LeanFrom | null; onChange: (v: LeanFrom | null) => void; inherit?: boolean; disabled?: boolean; varies?: boolean }) {
  return (
    <select className="cell" data-testid="wall-lean-from" title={LEAN_FROM_TIP} value={varies ? '~' : value ?? (inherit ? '' : 'base')} disabled={disabled}
      onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as LeanFrom))}>
      {varies && <option value="~" disabled>varies</option>}
      {inherit && <option value="">As its type</option>}
      <option value="base">Base</option>
      <option value="top">Top</option>
      <option value="axis">Mid-height</option>
    </select>
  );
}

function CutSelect({ value, onChange, inherit, disabled, varies }: { value: LeanCut | null; onChange: (v: LeanCut | null) => void; inherit?: boolean; disabled?: boolean; varies?: boolean }) {
  return (
    <select className="cell" data-testid="wall-lean-cut" title={LEAN_CUT_TIP} value={varies ? '~' : value ?? (inherit ? '' : 'wall')} disabled={disabled}
      onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as LeanCut))}>
      {varies && <option value="~" disabled>varies</option>}
      {inherit && <option value="">As its type</option>}
      <option value="wall">Leaning with the wall</option>
      <option value="vertical">Plumb</option>
    </select>
  );
}

/** The wall type dialog's fields: its walls' lean, the level that stays put, how their openings stand. */
export function TypeLeanFields({ value, onChange }: { value: LeanValues; onChange: (v: LeanValues) => void }) {
  return (
    <>
      <label className="field-line"><span className="eyebrow" title={LEAN_TIP}>Lean</span>
        <NumberField value={value.lean} suffix="°" min={-60} max={60} blank testId="type-lean" title={LEAN_TIP} placeholder="0"
          onCommit={(n) => onChange({ ...value, lean: n == null || Math.abs(n) < 1e-9 ? null : n })} />
        {value.lean != null && <span className="muted">{batter(value.lean)}</span>}
      </label>
      {value.lean != null && (
        <>
          <label className="field-line"><span className="eyebrow" title={LEAN_FROM_TIP}>Lean from</span><FromSelect value={value.leanFrom} onChange={(leanFrom) => onChange({ ...value, leanFrom })} /></label>
          <label className="field-line"><span className="eyebrow" title={LEAN_CUT_TIP}>Openings in a leaning wall</span><CutSelect value={value.openingCut} onChange={(openingCut) => onChange({ ...value, openingCut })} /></label>
        </>
      )}
    </>
  );
}

/** A wall's own lean in its inspector: several selected walls take the same value (one step); empty gives each its type's. */
export function WallLeanFields({ node, type }: { node: NodeDto; type: NodeDto | null | undefined }) {
  const nodes = useSession((s) => s.nodes);
  const ids = sameKind(node);
  const all = ids.map((id) => nodes.get(id)?.props ?? {});
  const shared = (key: string) => all.every((p) => JSON.stringify(p[key] ?? null) === JSON.stringify(node.props[key] ?? null));
  const set = (attrs: Record<string, unknown>) => void execute('element.update', { ids, attrs });
  const own = leanValues(node.props);
  const typed = leanValues(type?.props ?? {});
  const effective = own.lean ?? typed.lean;
  const disabled = !node.editable;
  return (
    <>
      <dt title={LEAN_TIP}>Lean</dt>
      <dd className="pair">
        <NumberField value={shared('lean') ? own.lean : null} suffix="°" min={-60} max={60} blank testId="wall-lean" readOnly={disabled} title={LEAN_TIP}
          placeholder={!shared('lean') ? 'varies' : typed.lean != null ? `${typed.lean}` : '0'}
          onCommit={(n) => set({ lean: n })} />
        {effective != null && shared('lean') && <span className="muted">{batter(effective)}</span>}
      </dd>
      {(effective != null || !shared('lean')) && (
        <>
          <dt title={LEAN_FROM_TIP}>Lean from</dt>
          <dd><FromSelect inherit value={own.leanFrom} varies={!shared('leanFrom')} disabled={disabled} onChange={(v) => set({ leanFrom: v })} /></dd>
          <dt title={LEAN_CUT_TIP}>Openings in a leaning wall</dt>
          <dd><CutSelect inherit value={own.openingCut} varies={!shared('openingCut')} disabled={disabled} onChange={(v) => set({ openingCut: v })} /></dd>
        </>
      )}
    </>
  );
}

/** The wall tool's lean and taper for the walls drawn next, for this session: empty means the wall type's own. */
export interface WallSlantPrefs { lean: number | null; leanFrom: LeanFrom | null; openingCut: LeanCut | null; thicknessTop: number | null }
export const useWallSlantPrefs = create<WallSlantPrefs>(() => ({ lean: null, leanFrom: null, openingCut: null, thicknessTop: null }));

/** The addWall arguments for the wall tool's lean and thickness at top (none: as the type says). */
export function wallSlantArgs(p: WallSlantPrefs = useWallSlantPrefs.getState()): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  if (p.lean != null) {
    out.lean = p.lean;
    if (p.leanFrom) out.leanFrom = p.leanFrom;
    if (p.openingCut) out.openingCut = p.openingCut;
  }
  if (p.thicknessTop != null) out.thicknessTop = p.thicknessTop;
  return out;
}

/** The wall tool's Lean (with its n:1 display), Lean from and Thickness at top in the command line's bar. */
export function WallSlantOption() {
  const p = useWallSlantPrefs();
  const set = (v: Partial<WallSlantPrefs>) => useWallSlantPrefs.setState(v);
  return (
    <>
      <span className="eyebrow" title={LEAN_TIP}>Lean</span>
      <NumberField className="cad-length" value={p.lean} suffix="°" min={-60} max={60} blank testId="wall-tool-lean" title={LEAN_TIP} placeholder="0"
        onCommit={(n) => set({ lean: n == null || Math.abs(n) < 1e-9 ? null : n })} />
      {p.lean != null && (
        <>
          <span className="muted" data-testid="wall-tool-batter">{batter(p.lean)}</span>
          <select className="cad-layer" data-testid="wall-tool-lean-from" title={LEAN_FROM_TIP} value={p.leanFrom ?? ''}
            onChange={(e) => set({ leanFrom: e.target.value === '' ? null : (e.target.value as LeanFrom) })}>
            <option value="">Lean from: as the type</option>
            <option value="base">Lean from: base</option>
            <option value="top">Lean from: top</option>
            <option value="axis">Lean from: mid-height</option>
          </select>
          <select className="cad-layer" data-testid="wall-tool-lean-cut" title={LEAN_CUT_TIP} value={p.openingCut ?? ''}
            onChange={(e) => set({ openingCut: e.target.value === '' ? null : (e.target.value as LeanCut) })}>
            <option value="">Openings: as the type</option>
            <option value="wall">Openings: leaning with the wall</option>
            <option value="vertical">Openings: plumb</option>
          </select>
        </>
      )}
      <span className="eyebrow" title={TAPER_TIP}>Thickness at top</span>
      <LengthField className="cad-length" metres={p.thicknessTop} min={0.01} max={10} blank testId="wall-tool-thickness-top" title={TAPER_TIP}
        onCommit={(m) => set({ thicknessTop: m })} />
    </>
  );
}
