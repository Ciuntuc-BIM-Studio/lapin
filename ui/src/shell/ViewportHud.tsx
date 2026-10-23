/** Atrium's HUD over the view: the mode and level card, the plan map, the numbered tool bar. */

import { useEffect } from 'react';
import { useUi } from '../app/store';
import { hotbarTools } from '../platform/modules';

export function ViewportHud({ hint }: { hint: string | null }) {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  const tools = hotbarTools();
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) return;
      const n = Number(e.key);
      if (n >= 1 && n <= tools.length) tools[n - 1].run?.();
      if (e.key === 'Escape') useUi.setState({ tool: null });
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [tools]);
  const active = tools.find((t) => t.id === tool);
  return (
    <>
      <div className="hud-card">
        <span className="eyebrow">Mode</span><span>{active ? active.label.toUpperCase() : 'SELECT'}</span>
        <span className="eyebrow">Level</span><span>GROUND FLOOR <span className="accent">±0.00</span></span>
        <span className="eyebrow">View</span><span>{view === 'plan' ? 'PLAN' : '3D'} · SHADED</span>
        <span className="eyebrow">Snap</span><span>ON · GRID 0.05 m</span>
      </div>
      <div className="hud-map" aria-hidden="true">
        <span className="eyebrow">Plan · Ground floor</span>
        <svg viewBox="0 0 120 80"><rect x="18" y="14" width="84" height="52" fill="none" stroke="currentColor" strokeWidth="2.5" /><line x1="60" y1="14" x2="60" y2="66" stroke="currentColor" strokeWidth="1.5" /><circle cx="44" cy="48" r="3" className="you" /></svg>
      </div>
      {hint && <div className="hud-hint">{hint}</div>}
      <div className="hotbar" role="toolbar" aria-label="Quick tools">
        {tools.map((t, i) => (
          <button type="button" key={t.id} className={`slot${tool === t.id ? ' on' : ''}`} onClick={t.run} title={t.shortcut ? `${t.label} · ${t.shortcut}` : t.label}>
            <span className="slot-n">{i + 1}</span>
            <t.icon size={20} strokeWidth={1.5} />
          </button>
        ))}
      </div>
    </>
  );
}
