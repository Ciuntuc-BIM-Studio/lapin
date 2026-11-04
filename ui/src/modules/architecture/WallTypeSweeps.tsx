/**
 * The Wall types dialog's "Sweeps and reveals" (decision 298, Revit's wall type sweeps and reveals): the sweeps every
 * wall of the type carries, each a row: its profile, sweep or reveal, the face (R or L, as the legend names them: R is
 * the face of the type's first layer), the height over the wall's base or under its top, the offset from the face, the
 * material of a sweep and its returns at free ends. What a row keeps that it does not show (flip, set-backs) stays as it
 * was. Saved with the type, every wall of it follows; a wall may change one of them as its own, or switch one off.
 */

import { Plus, Trash2 } from 'lucide-react';
import { useLang } from '../../i18n';
import { faceLetters } from '../../viewport/faceMarks';
import { LengthField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { ProfilePicker } from '../core/ProfilesDialog';
import type { ProfileJson } from '../core/profiles';

export type SweepMode = 'add' | 'cut';
export type Returns = 'none' | 'start' | 'end' | 'both';

/** A type's sweep as the dialog holds it: the engine's fields, the material by name. */
export interface TypeSweepRow {
  key?: string; profileId: string | null; profile?: ProfileJson; mode: SweepMode; side: 'left' | 'right';
  height: number; fromTop: boolean; offset: number; material: string; returns: Returns | null;
  /** The fields the row does not show, kept as they were (flip, set-backs, returns at openings, gaps). */
  rest: Record<string, unknown>;
}

const SHOWN = new Set(['key', 'profileId', 'profile', 'mode', 'side', 'height', 'fromTop', 'offset', 'material', 'returns']);

/** The type's stored sweeps as rows (`materialOf` names a material id). */
export function sweepRows(stored: unknown, materialOf: (id: string) => string): TypeSweepRow[] {
  if (!Array.isArray(stored)) return [];
  return stored.map((raw) => {
    const s = raw as Record<string, unknown>;
    const rest: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(s)) if (!SHOWN.has(k)) rest[k] = v;
    return {
      key: typeof s.key === 'string' ? s.key : undefined,
      profileId: typeof s.profileId === 'string' ? s.profileId : null,
      profile: s.profile as ProfileJson | undefined,
      mode: s.mode === 'cut' ? 'cut' : 'add',
      side: s.side === 'left' ? 'left' : 'right',
      height: typeof s.height === 'number' ? s.height : 0.3,
      fromTop: s.fromTop === true,
      offset: typeof s.offset === 'number' ? s.offset : 0,
      material: typeof s.material === 'string' ? materialOf(s.material) : '',
      returns: typeof s.returns === 'string' ? (s.returns as Returns) : null,
      rest,
    };
  });
}

/** A row as the engine reads it (architecture.updateWallType's sweeps). */
export function sweepJson(r: TypeSweepRow): Record<string, unknown> {
  return {
    ...r.rest,
    ...(r.key ? { key: r.key } : {}),
    ...(r.profileId ? { profileId: r.profileId } : r.profile ? { profile: r.profile } : {}),
    mode: r.mode, side: r.side, height: r.height, fromTop: r.fromTop, offset: r.offset,
    ...(r.mode === 'add' && r.material.trim() ? { material: r.material.trim() } : {}),
    ...(r.returns ? { returns: r.returns } : {}),
  };
}

const blankRow = (): TypeSweepRow => ({ profileId: null, mode: 'add', side: 'right', height: 0.3, fromTop: false, offset: 0, material: 'Plaster', returns: null, rest: {} });

export function WallTypeSweeps({ rows, onChange }: { rows: TypeSweepRow[]; onChange: (rows: TypeSweepRow[]) => void }) {
  const letters = faceLetters(useLang((s) => s.lang));
  const set = (i: number, patch: Partial<TypeSweepRow>) => onChange(rows.map((r, k) => (k === i ? { ...r, ...patch } : r)));
  return (
    <>
      <div className="row">
        <span className="eyebrow">Sweeps and reveals</span>
        <span className="muted small">every wall of the type carries them · a wall may change or switch off its own</span>
      </div>
      {rows.length > 0 && (
        <table className="layer-edit sweeps" data-testid="type-sweeps">
          <thead><tr><th>Profile</th><th>Kind</th><th title="The face: R is the face of the first layer, L the other">Face</th><th title="Over the wall's base, or under its top">Height</th><th>From the top</th><th title="Out from the face">Offset</th><th>Material</th><th title="Closed round the wall's free ends">Returns</th><th /></tr></thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={r.key ?? `new-${i}`} data-testid="type-sweep">
                <td><ProfilePicker use={r.mode === 'cut' ? 'reveal' : 'sweep'} value={r.profileId} testId="type-sweep-profile" onPick={(profileId) => set(i, { profileId })} /></td>
                <td><select value={r.mode} data-field="type-sweep-mode" onChange={(e) => set(i, { mode: e.target.value as SweepMode, profileId: null })}>
                  <option value="add">Wall sweep</option><option value="cut">Wall reveal</option>
                </select></td>
                <td><select value={r.side} data-field="type-sweep-side" onChange={(e) => set(i, { side: e.target.value as 'left' | 'right' })}>
                  <option value="right" data-no-i18n>{letters.right}</option><option value="left" data-no-i18n>{letters.left}</option>
                </select></td>
                <td className="num"><LengthField className="layer-height" testId="type-sweep-height" metres={r.height} onCommit={(m) => { if (m != null) set(i, { height: m }); }} /></td>
                <td><input type="checkbox" checked={r.fromTop} onChange={(e) => set(i, { fromTop: e.target.checked })} /></td>
                <td className="num"><LengthField className="layer-height" metres={r.offset} onCommit={(m) => set(i, { offset: m ?? 0 })} /></td>
                <td>{r.mode === 'add' ? <MaterialPicker value={r.material} onChange={(material) => set(i, { material })} /> : <span className="muted small">the wall's</span>}</td>
                <td><select value={r.returns ?? ''} data-field="type-sweep-returns" onChange={(e) => set(i, { returns: (e.target.value || null) as Returns | null })}>
                  <option value="">{r.mode === 'cut' ? 'none' : 'both'}</option>
                  <option value="none">none</option><option value="start">at the start</option><option value="end">at the end</option><option value="both">both</option>
                </select></td>
                <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => onChange(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row">
        <button type="button" className="btn" data-testid="type-sweep-add" onClick={() => onChange([...rows, blankRow()])}><Plus size={14} />Wall sweep</button>
        <button type="button" className="btn" data-testid="type-reveal-add" onClick={() => onChange([...rows, { ...blankRow(), mode: 'cut', height: 1, material: '' }])}><Plus size={14} />Wall reveal</button>
      </div>
    </>
  );
}
