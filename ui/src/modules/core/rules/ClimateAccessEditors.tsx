/**
 * The rules' Climate and Accessibility tabs (docs/plan-national-packs.md N2, N3). Climate: a list of regions (for Sweden the
 * kommun) each giving sk and vb,0 (or qb), typed or pasted from a table; Loads → Snow and wind picks one. Accessibility: the least
 * door clear width, the least corridor width, the turning circle, ramps against the stairs' set ramp; Check the project lists
 * what falls short, nothing is refused. Lapin's own pack lists no regions and checks nothing of accessibility.
 */

import { useState } from 'react';
import { ClipboardPaste, Plus, X } from 'lucide-react';
import { LengthField, NumberField } from '../../../shell/cells';
import {
  ACCESS_LIMITS, addRegion, functionsFrom, mergeRegions, parseRegions, qbFromVb0, regionsProblem, removeRegion, setAccessLimit, setRegion,
  type AccessibilitySection, type ClimateSection,
} from './rulesModel';

export function ClimateEditor({ values, readOnly, onChange, setProblem }: { values: ClimateSection; readOnly: boolean; onChange: (s: ClimateSection) => void; setProblem: (p: string | null) => void }) {
  const [name, setName] = useState('');
  const [paste, setPaste] = useState<string | null>(null);
  const put = (next: ClimateSection) => {
    const p = regionsProblem(next);
    if (p) { setProblem(`${p}.`); return; }
    onChange(next);
  };
  const regions = values.regions ?? [];
  return (
    <div data-testid="rules-climate">
      <label className="field-line"><span className="eyebrow">The regions are</span>
        <input key={values.regionLabel} data-field="rules-region-label" defaultValue={values.regionLabel} readOnly={readOnly} placeholder="Kommun" style={{ width: 160 }} data-no-i18n
          title="What a region is called in the pack's country: Kommun, County, Locality"
          onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== values.regionLabel) { if (v.length > 40) { setProblem("The regions' label is 1 to 40 characters."); return; } onChange({ ...values, regionLabel: v }); } }} /></label>
      <table className="layer-table rules-table" data-testid="rules-regions">
        <thead><tr><th data-no-i18n>{values.regionLabel}</th><th data-no-i18n>sk</th><th data-no-i18n>vb,0</th><th data-no-i18n>qb</th><th /></tr></thead>
        <tbody>
          {regions.map((r, i) => (
            <tr key={r.name + i} data-region={r.name}>
              <td data-no-i18n>{r.name}</td>
              <td data-rule={`climate.${i}.sk`}><NumberField value={r.sk} suffix="kN/m²" readOnly={readOnly} min={0} max={20} onCommit={(n) => put(setRegion(values, i, { sk: n }))} /></td>
              <td data-rule={`climate.${i}.vb0`}><NumberField value={r.vb0} suffix="m/s" readOnly={readOnly} min={0} max={60} onCommit={(n) => put(setRegion(values, i, { vb0: n, ...(n != null ? { qb: null } : {}) }))} /></td>
              <td data-rule={`climate.${i}.qb`}>{r.vb0 != null
                ? <span className="muted small" title="½ · 1.25 kg/m³ · vb,0²" data-no-i18n>{qbFromVb0(r.vb0)} kN/m²</span>
                : <NumberField value={r.qb} suffix="kN/m²" readOnly={readOnly} min={0} max={10} onCommit={(n) => put(setRegion(values, i, { qb: n }))} />}</td>
              <td>{!readOnly && <button type="button" className="icon mini" title="Remove this region" onClick={() => onChange(removeRegion(values, i))}><X size={11} /></button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {regions.length === 0 && <p className="muted small">No regions: the site's snow and wind are typed in Loads → Snow and wind.</p>}
      {!readOnly && (
        <div className="row wrap">
          <input placeholder="New region's name" value={name} data-field="rules-new-region" onChange={(e) => setName(e.target.value)} />
          <button type="button" className="btn small" disabled={!name.trim()} data-field="rules-add-region"
            onClick={() => { const n = addRegion(values, name); if (n) { onChange(n); setName(''); } else setProblem('That region is listed already, or its name is too long.'); }}><Plus size={12} />Region</button>
          <button type="button" className="btn small" data-field="rules-paste-regions" title="Name, sk and vb,0 a line, the columns split by tabs or semicolons (as copied from a spreadsheet)" onClick={() => setPaste(paste == null ? '' : null)}><ClipboardPaste size={12} />Paste a list…</button>
        </div>
      )}
      {!readOnly && paste != null && (
        <div className="rules-paste">
          <textarea rows={5} data-field="rules-regions-text" value={paste} placeholder={'Kiruna\t5.5\t22\nMalmö\t1.0\t26'} onChange={(e) => setPaste(e.target.value)} data-no-i18n />
          <div className="row">
            <button type="button" className="btn small primary" data-field="rules-regions-add" disabled={!paste.trim()}
              onClick={() => { const r = parseRegions(paste); if (r.problem) { setProblem(`${r.problem}.`); return; } put(mergeRegions(values, r.regions)); setPaste(null); }}>Add the regions</button>
            <span className="muted small">A region of a name already listed takes the pasted values.</span>
          </div>
        </div>
      )}
      <p className="muted small">qb = ½ · 1.25 kg/m³ · vb,0² (cdir and cseason 1). A project picks its region in Loads → Snow and wind; its values can still be typed there. The values are yours, typed from the edition in force: indicative.</p>
    </div>
  );
}

export function AccessibilityEditor({ values, readOnly, onChange, setProblem }: { values: AccessibilitySection; readOnly: boolean; onChange: (s: AccessibilitySection) => void; setProblem: (p: string | null) => void }) {
  const list = (key: 'corridorFunctions' | 'turningFunctions', label: string, title: string) => {
    const text = (values[key] ?? []).join(', ');
    return (
      <label className="field-line" title={title}><span className="eyebrow">{label}</span>
        <input key={text} data-field={`rules-${key}`} defaultValue={text} readOnly={readOnly}
          onBlur={(e) => { const next = functionsFrom(e.target.value); if (next.join(', ') !== text) onChange({ ...values, [key]: next }); }} /></label>
    );
  };
  return (
    <div data-testid="rules-accessibility">
      <table className="layer-table rules-table">
        <tbody>
          {ACCESS_LIMITS.map((f) => (
            <tr key={f.key}><td>{f.label}</td>
              <td data-rule={`accessibility.${f.key}`}><LengthField metres={values[f.key]} readOnly={readOnly} placeholder="not checked" min={f.min} max={f.max}
                onCommit={(m) => { const next = setAccessLimit(values, f.key, m); if (!next) { setProblem(`${f.label} is between ${f.min} and ${f.max} m.`); return; } onChange(next); }} /></td></tr>
          ))}
          <tr><td>Ramps</td>
            <td><label className="row small"><input type="checkbox" data-field="rules-check-ramps" checked={!!values.checkRamps} disabled={readOnly} onChange={(e) => onChange({ ...values, checkRamps: e.target.checked })} />
              Checked against the ramp set of the Stairs tab (its steepest slope and least width)</label></td></tr>
        </tbody>
      </table>
      {list('corridorFunctions', 'Corridors are rooms of the functions', 'Their narrowest width, to the finishes, is checked against the corridor\'s least width')}
      {list('turningFunctions', 'The turning circle is asked of the functions', 'Each such room must hold a circle of the diameter given')}
      <p className="muted small">An empty value is not checked. Check the project lists each door, ramp and room that falls short; nothing is refused. The values are yours, typed from the edition in force: indicative.</p>
    </div>
  );
}
