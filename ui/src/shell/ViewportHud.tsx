/** Atrium's HUD over the view: the mode and level card, the hint, the numbered tool bar (1–9). */

import { useUi } from '../app/store';
import { activate, findTool, hotbarTools } from '../platform/modules';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';

const level = (e: number) => `${e >= 0 ? '+' : '−'}${Math.abs(e).toFixed(2)}`;

export function ViewportHud() {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  const storey = activeStorey();
  const tools = hotbarTools();
  const active = findTool(tool);
  const hint = active?.gesture?.kind === 'twoPoints' ? `${active.label}: click its start, then its end · Shift squares it · Esc stops` : null;
  return (
    <>
      <div className="hud-card">
        <span className="eyebrow">Mode</span><span>{active ? active.label.toUpperCase() : 'SELECT'}</span>
        <span className="eyebrow">Level</span><span>{(storey?.name ?? '—').toUpperCase()} <span className="accent">{level(num(storey ?? undefined, 'elevation'))}</span></span>
        <span className="eyebrow">View</span><span>{view === 'plan' ? 'PLAN' : '3D · SHADED'}</span>
        <span className="eyebrow">Snap</span><span>POINTS · GRID 0.05 m</span>
      </div>
      {hint && <div className="hud-hint">{hint}</div>}
      <div className="hotbar" role="toolbar" aria-label="Quick tools">
        {tools.map((t, i) => (
          <button type="button" key={t.id} className={`slot${tool === t.id ? ' on' : ''}`} onClick={() => activate(t)} title={t.shortcut ? `${t.label} · ${t.shortcut}` : t.label}>
            <span className="slot-n">{i + 1}</span>
            <t.icon size={20} strokeWidth={1.5} />
          </button>
        ))}
      </div>
    </>
  );
}
