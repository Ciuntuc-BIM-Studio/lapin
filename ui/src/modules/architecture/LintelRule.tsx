/**
 * A lintel rule, as the wall types dialog and the configurator edit it:
 * none, precast (a product of the project's catalogue, a catalogue length
 * centred over the bearings) or cast in place, with the bearing past each
 * side and the height (mm); then, opened, the other side's bearing, the
 * narrowest opening that gets one, its width across the wall and where it
 * sits, pieces side by side, its material, and a cast lintel's
 * reinforcement. The configurator adds "as the wall" (no rule of its own).
 */

import { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, FileUp } from 'lucide-react';
import { mm } from '../../engine/model';
import { execute, query, useSession } from '../../engine/session';
import { notify } from '../../app/store';

interface Product { code: string; width: number; height: number; lengths: number[]; manufacturer?: string }
interface Bars { count: number; diameter: number }
export interface Rule {
  mode: 'none' | 'precast' | 'cast'; bearing: number; height: number;
  bearingEnd?: number | null; minWidth?: number | null; width?: number | null; placement?: string | null; pieces?: number | null; material?: string | null;
  product?: Product | null;
  reinforcement?: { top?: Bars | null; bottom?: Bars | null; stirrups?: { diameter: number; spacing: number } | null; cover?: number | null } | null;
  [k: string]: unknown;
}

export const DEFAULT_RULE: Rule = { mode: 'precast', bearing: 0.25, height: 0.25 };

function MmInput({ value, onChange, empty }: { value: number | null | undefined; onChange: (m: number | null) => void; empty?: string }) {
  const [text, setText] = useState(value == null ? '' : mm(value));
  useEffect(() => setText(value == null ? '' : mm(value)), [value]);
  return <input className="num" placeholder={empty} value={text} onChange={(e) => {
    setText(e.target.value);
    if (e.target.value.trim() === '' && empty) { onChange(null); return; }
    const v = Number(e.target.value.replace(',', '.'));
    if (Number.isFinite(v) && v >= 0) onChange(v / 1000);
  }} />;
}

function NumInput({ value, onChange, step = 1 }: { value: number | null | undefined; onChange: (n: number | null) => void; step?: number }) {
  return <input className="num" type="number" step={step} min={0} value={value ?? ''} onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))} />;
}

/** A CSV the user picks: its text. */
function pickCsv(): Promise<string | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.csv,.txt';
    input.onchange = () => { const f = input.files?.[0]; if (f) void f.text().then(resolve); else resolve(null); };
    input.click();
  });
}

/** `rule` null: as the wall (only when `inherit` is offered). */
export function LintelRuleEditor({ rule, onChange, inherit }: { rule: Rule | null; onChange: (r: Rule | null) => void; inherit?: boolean }) {
  const mode = rule?.mode ?? 'wall';
  const [open, setOpen] = useState(false);
  const nodes = useSession((s) => s.nodes);
  const [products, setProducts] = useState<Product[]>([]);
  useEffect(() => { void query<Product[]>('architecture.lintelProducts').then((p) => p && setProducts(p)); }, [nodes]);
  const set = (patch: Partial<Rule>) => rule && onChange({ ...rule, ...patch });
  const reinf = rule?.reinforcement ?? {};
  const importCsv = async () => {
    const text = await pickCsv();
    if (!text) return;
    const r = await execute('architecture.importLintelProducts', { text }) as { added: number; skipped: string[] } | null;
    if (r) notify(`${r.added} lintel product(s) added${r.skipped.length ? `; skipped ${r.skipped.join(', ')}` : ''}.`);
  };
  return (
    <div className="lintel-rule">
      <div className="row">
        <span className="eyebrow">Lintels</span>
        <select value={mode} onChange={(e) => onChange(e.target.value === 'wall' ? null : { ...(rule ?? DEFAULT_RULE), mode: e.target.value as Rule['mode'] })}>
          {inherit && <option value="wall">As the wall</option>}
          <option value="precast">Precast</option>
          <option value="cast">Cast in place</option>
          <option value="none">None</option>
        </select>
        {rule && rule.mode !== 'none' && <>
          <span className="muted small">bearing</span><MmInput value={rule.bearing} onChange={(v) => set({ bearing: v ?? 0 })} />
          {!(rule.mode === 'precast' && rule.product) && <><span className="muted small">height</span><MmInput value={rule.height} onChange={(v) => set({ height: v ?? 0 })} /></>}
          <span className="muted small">mm</span>
          <button type="button" className="icon mini" title="More: product, width, pieces, reinforcement" onClick={() => setOpen(!open)}>{open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}</button>
        </>}
      </div>
      {rule && rule.mode !== 'none' && open && (
        <div className="lintel-more">
          {rule.mode === 'precast' && (
            <label className="row"><span className="muted small">Product</span>
              <select value={rule.product?.code ?? ''} onChange={(e) => set({ product: products.find((p) => p.code === e.target.value) ?? null })}>
                <option value="">none (the height above)</option>
                {products.map((p) => <option key={p.code} value={p.code}>{p.code} · {mm(p.width)}×{mm(p.height)}{p.manufacturer ? ` · ${p.manufacturer}` : ''}</option>)}
              </select>
              <button type="button" className="icon mini" title="Import a manufacturer's CSV: code; width; height; lengths (mm); manufacturer" onClick={() => void importCsv()}><FileUp size={12} /></button>
            </label>
          )}
          <label className="row"><span className="muted small">Bearing, other side</span><MmInput value={rule.bearingEnd} empty="same" onChange={(v) => set({ bearingEnd: v })} /></label>
          <label className="row"><span className="muted small">Only openings from</span><MmInput value={rule.minWidth} empty="any" onChange={(v) => set({ minWidth: v })} /><span className="muted small">mm wide</span></label>
          {!(rule.mode === 'precast' && rule.product) && <label className="row"><span className="muted small">Width across</span><MmInput value={rule.width} empty="the core" onChange={(v) => set({ width: v })} /></label>}
          <label className="row"><span className="muted small">Sits</span>
            <select value={rule.placement ?? ''} onChange={(e) => set({ placement: e.target.value || null })}><option value="">centred</option><option value="outer">at the outer face</option><option value="inner">at the inner face</option></select>
          </label>
          <label className="row"><span className="muted small">Pieces side by side</span><NumInput value={rule.pieces} onChange={(n) => set({ pieces: n && n >= 1 ? Math.round(n) : null })} /></label>
          <label className="row"><span className="muted small">Material</span><input list="lapin-materials" value={rule.material ?? ''} placeholder="the wall's core" onChange={(e) => set({ material: e.target.value || null })} /></label>
          {rule.mode === 'cast' && <>
            <span className="eyebrow">Reinforcement</span>
            {(['top', 'bottom'] as const).map((k) => (
              <label key={k} className="row"><span className="muted small">{k === 'top' ? 'Top bars' : 'Bottom bars'}</span>
                <NumInput value={reinf[k]?.count} onChange={(n) => set({ reinforcement: { ...reinf, [k]: n ? { count: n, diameter: reinf[k]?.diameter ?? 0.012 } : null } })} />
                <span className="muted small">Ø</span><MmInput value={reinf[k]?.diameter} onChange={(v) => set({ reinforcement: { ...reinf, [k]: { count: reinf[k]?.count ?? 2, diameter: v ?? 0.012 } } })} />
              </label>
            ))}
            <label className="row"><span className="muted small">Stirrups Ø</span><MmInput value={reinf.stirrups?.diameter} empty="none" onChange={(v) => set({ reinforcement: { ...reinf, stirrups: v ? { diameter: v, spacing: reinf.stirrups?.spacing ?? 0.2 } : null } })} />
              <span className="muted small">every</span><MmInput value={reinf.stirrups?.spacing} onChange={(v) => set({ reinforcement: { ...reinf, stirrups: { diameter: reinf.stirrups?.diameter ?? 0.006, spacing: v ?? 0.2 } } })} /></label>
            <label className="row"><span className="muted small">Cover</span><MmInput value={reinf.cover} empty="25" onChange={(v) => set({ reinforcement: { ...reinf, cover: v } })} /></label>
          </>}
        </div>
      )}
    </div>
  );
}
