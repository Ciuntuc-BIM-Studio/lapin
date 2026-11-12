/**
 * A material's 2D graphics, one row per place it is drawn: cut in a section, cut in a plan, seen in an elevation; each
 * its hatch, scale, angle, alignment, fill and pen (report #76). The Materials dialog's 2D graphics tab and a layer
 * table's hatch button (forum #30, decision 493) both edit it here, with the same command (material.update, one undo
 * step each change).
 */

import { RotateCcw } from 'lucide-react';
import { execute } from '../../engine/session';
import { HatchSwatch } from './HatchSwatch';

export type MaterialKind = 'section' | 'plan' | 'elevation';
export type ResolvedGraphics = { hatch: string | null; hatchScale: number; fill: string | null; hatchPen?: number; hatchAngle?: number; hatchAlign?: 'view' | 'layer'; membrane: boolean; drawJoints: boolean; source: 'own' | 'name' };
export type OwnGraphics = { hatch?: string | null; hatchScale?: number; fill?: string | null; hatchPen?: number; hatchAngle?: number; hatchAlign?: 'view' | 'layer' };
/** A material as material.list gives it. */
export type MaterialRow = { id: string; name: string; color: string; inUse: boolean; category?: string | null; section: ResolvedGraphics; plan: ResolvedGraphics; elevation: ResolvedGraphics; own: Partial<Record<MaterialKind, OwnGraphics>> };
/** A hatch pattern as hatch.list gives it. */
export type HatchPattern = { name: string; description: string; solid: boolean; project: boolean };

export const MATERIAL_KINDS: { kind: MaterialKind; label: string }[] = [{ kind: 'section', label: 'Cut (section)' }, { kind: 'plan', label: 'Cut (plan)' }, { kind: 'elevation', label: 'Elevation' }];

export function MaterialGraphicsTable({ m, hatches }: { m: MaterialRow; hatches: HatchPattern[] }) {
  const update = (args: Record<string, unknown>) => void execute('material.update', { id: m.id, ...args });
  const setView = (kind: MaterialKind, patch: OwnGraphics | null) => update({ [kind]: patch });
  /** One field back to what the name (or, for a plan, the section) gives. */
  const clearField = (kind: MaterialKind, field: keyof OwnGraphics) => {
    const rest = { ...(m.own[kind] ?? {}) };
    delete rest[field];
    update({ [kind]: Object.keys(rest).length ? rest : null, replace: true });
  };
  return (
    <table className="material-views" data-testid="material-graphics">
      <thead><tr><th /><th>Look</th><th>Hatch</th><th>Scale</th><th>Angle</th><th title="Where the hatch starts and turns: from the view's origin, or from each band it fills (along the band's longest edge)">Align</th><th>Fill</th><th>Pen</th></tr></thead>
      <tbody>
        {MATERIAL_KINDS.map(({ kind, label }) => {
          const r = m[kind];
          const own = m.own[kind];
          const hatchValue = own && 'hatch' in own ? (own.hatch ?? '') : '*';
          // The hatch shown came from the name: none of its own (a plan's, none of the section's either). An elevation's guess is the facade's (brick, stone, boards; never BATT).
          const guessed = hatchValue === '*' && !(kind === 'plan' && m.own.section && 'hatch' in m.own.section);
          const fillMode = own?.fill === 'material' ? 'material' : own?.fill ? 'colour' : '';
          return (
            <tr key={kind} data-kind={kind}>
              <th>{label}{kind !== 'section' && own && <button type="button" className="icon mini" title="As the section" onClick={() => setView(kind, null)}><RotateCcw size={11} /></button>}</th>
              <td><HatchSwatch hatch={r.hatch} scale={r.hatchScale} fill={r.fill} membrane={kind !== 'elevation' && r.membrane} /></td>
              <td>
                <select data-field={`material-hatch-${kind}`} value={hatchValue} onChange={(e) => (e.target.value === '*' ? clearField(kind, 'hatch') : setView(kind, { hatch: e.target.value || null }))}>
                  <option value="*">{`from the name${guessed && r.hatch ? ` (${r.hatch})` : ''}`}</option>
                  <option value="">none</option>
                  {hatches.map((h) => <option key={h.name} value={h.name}>{h.name}{h.project ? ' (project)' : ''}</option>)}
                </select>
              </td>
              <td><input className="num" key={`${m.id}${kind}s${r.hatchScale}`} defaultValue={r.hatchScale}
                onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (v > 0 && v !== r.hatchScale) setView(kind, { hatchScale: v }); }} /></td>
              <td><input className="num" key={`${m.id}${kind}a${r.hatchAngle ?? 0}`} defaultValue={r.hatchAngle ?? 0}
                onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v) && v !== (r.hatchAngle ?? 0)) setView(kind, { hatchAngle: v }); }} /></td>
              <td><select value={r.hatchAlign ?? 'view'} onChange={(e) => setView(kind, { hatchAlign: e.target.value as 'view' | 'layer' })}><option value="view">view</option><option value="layer">layer</option></select></td>
              <td>
                <select data-field={`material-fill-${kind}`} value={fillMode} onChange={(e) => (e.target.value === '' ? clearField(kind, 'fill') : setView(kind, { fill: e.target.value === 'material' ? 'material' : (r.fill ?? '#ffffff') }))}>
                  <option value="">none</option><option value="material">material colour</option><option value="colour">colour…</option>
                </select>
                {fillMode === 'colour' && <input type="color" value={own?.fill ?? '#ffffff'} onChange={(e) => setView(kind, { fill: e.target.value })} />}
              </td>
              <td><input className="num" key={`${m.id}${kind}p${r.hatchPen}`} defaultValue={r.hatchPen ?? ''} placeholder="—"
                onBlur={(e) => { const v = Number(e.target.value); if (e.target.value && v >= 1 && v <= 16 && v !== r.hatchPen) setView(kind, { hatchPen: v }); }} /></td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
