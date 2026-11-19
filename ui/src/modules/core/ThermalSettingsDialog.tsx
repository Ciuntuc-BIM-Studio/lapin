/**
 * Manage → Thermal settings: the surface resistances Rsi and Rse of each case (EN ISO 6946), and whether Lapin's indicative λ
 * by a material's name stands in for a material without its own. The project's, one undo step each.
 */

import { useEffect, useState } from 'react';
import { RotateCcw, X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { fmtR, parseResistance, type ThermalCase, type ThermalSettings } from './thermal';

const FLOW = { horizontal: 'Horizontal', upward: 'Upward', downward: 'Downward' } as const;

function ResistanceBox({ value, field, onCommit }: { value: number; field: string; onCommit: (v: number) => void }) {
  const [text, setText] = useState(fmtR(value));
  useEffect(() => setText(fmtR(value)), [value]);
  const commit = () => {
    const v = parseResistance(text);
    if (v == null) { setText(fmtR(value)); return; }
    if (Math.abs(v - value) > 1e-9) onCommit(v);
    else setText(fmtR(value));
  };
  return (
    <input className="cell num" inputMode="decimal" data-field={field} value={text} onChange={(e) => setText(e.target.value)}
      onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
  );
}

export function ThermalSettingsDialog() {
  const version = useSession((s) => s.version);
  const [settings, setSettings] = useState<ThermalSettings | null>(null);
  useEffect(() => {
    let live = true;
    void query<ThermalSettings>('thermal.settings').then((r) => { if (live) setSettings(r ?? null); });
    return () => { live = false; };
  }, [version]);
  const close = () => useUi.setState({ dialog: null });
  const set = (cases: Record<string, { rsi: number; rse: number } | null>) => void execute('thermal.setSettings', { cases });
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog thermal-dialog" role="dialog" aria-label="Thermal settings" data-testid="thermal-settings" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Thermal settings</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <h4 className="eyebrow">Surface resistances</h4>
          <table>
            <thead><tr><th>Case</th><th>Heat flow</th><th className="num">Rsi (m²K/W)</th><th className="num">Rse (m²K/W)</th><th /></tr></thead>
            <tbody>
              {(settings?.cases ?? []).map((c: ThermalCase) => (
                <tr key={c.id} data-case={c.id} data-own={c.own ? 'true' : 'false'}>
                  <td>{c.label}</td>
                  <td>{FLOW[c.flow]}</td>
                  <td><ResistanceBox value={c.rsi} field="thermal-rsi" onCommit={(v) => set({ [c.id]: { rsi: v, rse: c.rse } })} /></td>
                  <td><ResistanceBox value={c.rse} field="thermal-rse" onCommit={(v) => set({ [c.id]: { rsi: c.rsi, rse: v } })} /></td>
                  <td>{c.own && <button type="button" className="icon" data-field="thermal-reset" title="Back to Lapin’s values" onClick={() => set({ [c.id]: null })}><RotateCcw size={14} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <h4 className="eyebrow" style={{ marginTop: 12 }}>Conductivity λ</h4>
          <label className="quality-choice">
            <input type="checkbox" data-field="thermal-indicative" checked={settings?.indicative ?? true} disabled={!settings}
              onChange={(e) => void execute('thermal.setSettings', { indicative: e.target.checked })} />
            <span>Use Lapin’s indicative λ for materials without their own</span>
          </label>
          <p className="muted small">Lapin’s values follow EN ISO 6946 and are indicative: they are yours to check against the norm and your project’s requirements.</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
