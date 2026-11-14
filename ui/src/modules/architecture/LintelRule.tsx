/**
 * A lintel rule, as the wall types dialog and the configurator edit it:
 * none, precast (a beam type of Structure types, report #135: its section,
 * lengths, material and bearing; or a product of the older catalogue; a
 * stock length centred over the bearings) or cast in place, with the bearing past each
 * side and the height; then, opened, the other side's bearing, the
 * narrowest opening that gets one, its width across the wall and where it
 * sits, pieces side by side, its material, and a cast lintel's
 * reinforcement. The configurator adds "as the wall" (no rule of its own).
 */

import { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, FileUp } from 'lucide-react';
import { mm } from '../../engine/model';
import { execute, query, useSession } from '../../engine/session';
import { notify } from '../../app/store';
import { LengthField, NumberField } from '../../shell/cells';
import { DEFAULT_RULE, effectiveBearing, legacyProducts, withBeamType, type LintelType, type Product, type Rule } from './lintels';
import { MaterialPicker } from '../core/MaterialPicker';

/** A length of the rule (the shared field); with `empty`, clearing it means that default (null). */
function MmInput({ value, onChange, empty }: { value: number | null | undefined; onChange: (m: number | null) => void; empty?: string }) {
  return <LengthField metres={value} placeholder={empty} blank={empty != null} min={0} style={{ width: 64 }} onCommit={onChange} />;
}

/** A count of the rule (the shared field): empty means none. */
function NumInput({ value, onChange }: { value: number | null | undefined; onChange: (n: number | null) => void }) {
  return <NumberField value={value} blank integer min={0} style={{ width: 48 }} onCommit={onChange} />;
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
  const [types, setTypes] = useState<LintelType[]>([]);
  useEffect(() => {
    let live = true;
    void query<Product[]>('architecture.lintelProducts').then((p) => { if (live && p) setProducts(p); });
    void query<LintelType[]>('architecture.lintelTypes').then((t) => { if (live && t) setTypes(t); });
    return () => { live = false; };
  }, [nodes]);
  const beam = rule?.beamType ? types.find((t) => t.id === rule.beamType) ?? null : null;
  const older = legacyProducts(products, types);
  /** The product chosen: `type:<id>` a beam type, `product:<code>` the older catalogue's, '' none. */
  const pickProduct = (v: string) => {
    if (!rule) return;
    if (v.startsWith('type:')) onChange(withBeamType(rule, types.find((t) => `type:${t.id}` === v) ?? null));
    else onChange({ ...withBeamType(rule, null), product: products.find((p) => `product:${p.code}` === v) ?? null });
  };
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
          {beam && beam.bearing != null
            ? <><span className="muted small" title="The beam type's bearing unless one is typed here; cleared, the type's again">bearing</span>
              <MmInput value={rule.ownBearing ? rule.bearing : null} empty={`${mm(effectiveBearing(rule, types))}`} onChange={(v) => set(v == null ? { ownBearing: null } : { bearing: v, ownBearing: true })} /></>
            : <><span className="muted small">bearing</span><MmInput value={rule.bearing} onChange={(v) => set({ bearing: v ?? 0, ...(rule.beamType ? { ownBearing: true } : {}) })} /></>}
          {!(rule.mode === 'precast' && rule.product) && <><span className="muted small">height</span><MmInput value={rule.height} onChange={(v) => set({ height: v ?? 0 })} /></>}
          <button type="button" className="icon mini" title="More: product, width, pieces, reinforcement" onClick={() => setOpen(!open)}>{open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}</button>
        </>}
      </div>
      {rule && rule.mode !== 'none' && open && (
        <div className="lintel-more">
          {rule.mode === 'precast' && (
            <label className="row"><span className="muted small">Product</span>
              <select data-field="lintel-product" value={rule.beamType ? `type:${rule.beamType}` : rule.product ? `product:${rule.product.code}` : ''} onChange={(e) => pickProduct(e.target.value)}>
                <option value="">none (the height above)</option>
                {types.length > 0 && <optgroup label="Beam types">
                  {types.map((t) => <option key={t.id} value={`type:${t.id}`} data-no-i18n>{t.name}{t.mark ? ` (${t.mark})` : ''} · {mm(t.width)}×{mm(t.height)}{t.manufacturer ? ` · ${t.manufacturer}` : ''}</option>)}
                </optgroup>}
                {rule.beamType && !beam && <option value={`type:${rule.beamType}`} data-no-i18n>{rule.product?.code ?? '?'}</option>}
                {older.length > 0 && <optgroup label="Older catalogue">
                  {older.map((p) => <option key={p.code} value={`product:${p.code}`} data-no-i18n>{p.code} · {mm(p.width)}×{mm(p.height)}{p.manufacturer ? ` · ${p.manufacturer}` : ''}</option>)}
                </optgroup>}
                {rule.product && !rule.beamType && !older.some((p) => p.code === rule.product!.code) && <option value={`product:${rule.product.code}`} data-no-i18n>{rule.product.code}</option>}
              </select>
              <button type="button" className="icon mini" title="Import a manufacturer's CSV: code; width; height; lengths (mm); manufacturer. Each product becomes a beam type (Structure types) too." onClick={() => void importCsv()}><FileUp size={12} /></button>
            </label>
          )}
          {rule.mode === 'precast' && beam && (
            <p className="muted small" data-testid="lintel-beam-type" data-no-i18n>{`${mm(beam.width)} × ${mm(beam.height)}`}{beam.lengths.length > 0 ? ` · ${beam.lengths.map((l) => mm(l)).join(', ')}` : ''}{beam.material ? ` · ${beam.material}` : ''}</p>
          )}
          <label className="row"><span className="muted small">Bearing, other side</span><MmInput value={rule.bearingEnd} empty="same" onChange={(v) => set({ bearingEnd: v })} /></label>
          <label className="row"><span className="muted small">Only openings from</span><MmInput value={rule.minWidth} empty="any" onChange={(v) => set({ minWidth: v })} /><span className="muted small">wide</span></label>
          {!(rule.mode === 'precast' && rule.product) && <label className="row"><span className="muted small">Width across</span><MmInput value={rule.width} empty="the core" onChange={(v) => set({ width: v })} /></label>}
          <label className="row"><span className="muted small">Sits</span>
            <select value={rule.placement ?? ''} onChange={(e) => set({ placement: e.target.value || null })}><option value="">centred</option><option value="outer">at the outer face</option><option value="inner">at the inner face</option></select>
          </label>
          <label className="row"><span className="muted small">Pieces side by side</span><NumInput value={rule.pieces} onChange={(n) => set({ pieces: n && n >= 1 ? Math.round(n) : null })} /></label>
          <label className="row"><span className="muted small">Material</span><MaterialPicker value={rule.material ?? ''} allowEmpty placeholder={beam?.material ?? "the wall's core"} onChange={(material) => set({ material: material || null })} /></label>
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
