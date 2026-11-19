/**
 * Thermal resistance and U-value of a wall, floor or roof (and of its type), worked out by the engine from the layers
 * (EN ISO 6946; `thermal.of`): U and R, the case the heat flows in, each layer's λ and R, the surface resistances.
 */

import { useEffect, useState } from 'react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { sameKind } from '../../shell/params';
import { caseChoice, caseLabel, fmtLambda, fmtMm, fmtR, fmtU, isIndicative, type Thermal, type ThermalSettings } from './thermal';

const INDICATIVE_TITLE = 'An indicative value of Lapin’s: check it.';

export function ThermalSection({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  const [t, setT] = useState<Thermal | null>(null);
  const [settings, setSettings] = useState<ThermalSettings | null>(null);
  useEffect(() => {
    let live = true;
    void query<Thermal>('thermal.of', { id: node.id }).then((r) => { if (live) setT(r ?? null); });
    void query<ThermalSettings>('thermal.settings').then((r) => { if (live) setSettings(r ?? null); });
    return () => { live = false; };
  }, [node.id, version]);
  if (!t) return null;
  const cases = settings?.cases ?? [];
  const byKind = cases.find((c) => c.id === t.case && caseChoice(t) === '');
  return (
    <section className="sect" data-testid="thermal-section">
      <dl className="fields">
        <span className="qrow"><dt>U-value</dt>
          {t.u != null
            ? <dd className="num" data-testid="thermal-u">{fmtU(t.u)} <span className="muted small">W/m²K</span></dd>
            : <dd data-testid="thermal-incomplete">Incomplete</dd>}
        </span>
        <span className="qrow"><dt>R total</dt><dd className="num" data-testid="thermal-r">{fmtR(t.rTotal)} <span className="muted small">m²K/W</span></dd></span>
        {t.typed != null && (
          <span className="qrow" data-testid="thermal-typed"><dt>Typed</dt><dd className="num">{fmtU(t.typed)} <span className="muted small">W/m²K</span></dd></span>
        )}
      </dl>
      {t.typed != null && <p className="muted small">The typed value is used in IFC and tags.</p>}
      {!t.complete && t.missing.length > 0 && (
        <p className="small" data-testid="thermal-missing"><span>No λ for:</span> <strong>{t.missing.join(', ')}</strong> <span className="muted">Set it in Manage → Materials → Data.</span></p>
      )}
      <label className="type-pick">
        <span className="eyebrow">Heat-flow case</span>
        <select value={caseChoice(t)} disabled={!node.editable} data-field="thermal-case"
          onChange={(e) => void execute('thermal.setCase', { ids: sameKind(node), case: e.target.value || null })}>
          <option value="">By its kind</option>
          {cases.map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}
        </select>
        {caseChoice(t) === '' && <span className="muted small">{byKind?.label ?? caseLabel(cases, t.case)}</span>}
      </label>
      <table className="thermal-layers" data-testid="thermal-layers">
        <thead><tr><th>Layer</th><th className="num">d (mm)</th><th className="num">λ</th><th className="num">R</th></tr></thead>
        <tbody>
          <tr data-row="rsi"><td>Inner surface</td><td /><td /><td className="num">{fmtR(t.rsi)}</td></tr>
          {t.layers.map((l) => (
            <tr key={l.index} data-source={l.source}>
              <td>
                {l.name}
                {l.source === 'indicative' && l.as && <span className="muted small"> <span>taken as</span> <em>{l.as}</em></span>}
                {l.source === 'air' && <span className="muted small"> <span>air layer</span></span>}
              </td>
              <td className="num">{fmtMm(l.thickness)}</td>
              <td className="num">
                {l.lambda != null ? fmtLambda(l.lambda) : '–'}
                {isIndicative(l) && <span className="muted" title={INDICATIVE_TITLE} data-testid="thermal-indicative">*</span>}
              </td>
              <td className="num">{l.r != null ? fmtR(l.r) : '–'}</td>
            </tr>
          ))}
          <tr data-row="rse"><td>Outer surface</td><td /><td /><td className="num">{fmtR(t.rse)}</td></tr>
          <tr className="total"><td>R total</td><td /><td /><td className="num">{fmtR(t.rTotal)}</td></tr>
        </tbody>
      </table>
      {t.indicative && <p className="muted small"><span>*</span> <span>Includes values that are Lapin’s indicative ones: check them.</span></p>}
      {t.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
      <button type="button" className="btn" data-field="thermal-settings" onClick={() => useUi.setState({ dialog: 'thermalSettings' })}>Surface resistances…</button>
    </section>
  );
}
