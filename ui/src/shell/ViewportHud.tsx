/**
 * Atrium's HUD over the view: the mode and level card, the hint, the
 * numbered tool bar (1–9). View → Interface puts the card in the status line
 * or leaves it out, the hint in the command line, and the tool bar floating
 * at the bottom, docked on the view's left edge, or off.
 */

import { useUi } from '../app/store';
import { useInterface } from '../app/interface';
import { hint } from '../app/commands';
import { activate, findTool, hotbarTools } from '../platform/modules';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { ScopeBar } from './ScopeBar';

const level = (e: number) => `${e >= 0 ? '+' : '−'}${Math.abs(e).toFixed(2)}`;

/** What the active tool asks for, when it is a two-point tool; null otherwise. */
export function toolHint(tool: string | null): string | null {
  const active = findTool(tool);
  return active?.gesture?.kind === 'twoPoints' ? `${active.label}: click its start, then its end · Shift squares it · Esc stops` : null;
}

/** The HUD's facts: the mode, the level and its elevation, the view, the snap. */
export function useHudFacts() {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  const storey = activeStorey();
  const active = findTool(tool);
  return {
    mode: active ? active.label.toUpperCase() : 'SELECT',
    level: (storey?.name ?? '—').toUpperCase(), elevation: level(num(storey ?? undefined, 'elevation')),
    view: view === 'plan' ? 'PLAN' : view === 'drawing' ? 'DRAWING' : view === 'sheet' ? 'SHEET' : view === 'schedule' ? 'SCHEDULE' : '3D · SHADED',
    snap: 'POINTS · GRID 0.05 m',
  };
}

export function ViewportHud() {
  const tool = useUi((s) => s.tool);
  const parts = useInterface((s) => s.parts);
  const facts = useHudFacts();
  const tools = hotbarTools();
  const hintText = toolHint(tool);
  return (
    <>
      {parts.hud === 'over' && (
        <div className="hud-card">
          <span className="eyebrow">Mode</span><span>{facts.mode}</span>
          <span className="eyebrow">Level</span><span>{facts.level} <span className="accent">{facts.elevation}</span></span>
          <span className="eyebrow">View</span><span>{facts.view}</span>
          <span className="eyebrow">Snap</span><span>{facts.snap}</span>
        </div>
      )}
      <ScopeBar />
      {hintText && parts.prompt === 'banner' && <div className="hud-hint">{hintText}</div>}
      {parts.quickTools !== 'off' && (
        <div className={`hotbar${parts.quickTools === 'docked' ? ' docked' : ''}`} role="toolbar" aria-label="Quick tools">
          {tools.map((t, i) => (
            <button type="button" key={t.id} className={`slot${tool === t.id ? ' on' : ''}`} onClick={() => activate(t)} title={hint(t.id, t.label)}>
              <span className="slot-n">{i + 1}</span>
              <t.icon size={parts.quickTools === 'docked' ? 17 : 20} strokeWidth={1.5} />
            </button>
          ))}
        </div>
      )}
    </>
  );
}
