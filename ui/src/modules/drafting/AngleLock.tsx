/**
 * The padlock of an angle held on two elements (decision 739, Revit's padlock on an angular dimension): with such a dimension
 * selected in plan, a padlock beside its arc, closed when the angle is locked (the engine then keeps it whatever turns either
 * element, the other turned about where they meet in the same undo step), open when not. A click, or its right-click menu,
 * locks or unlocks it (element.update's `locked`, one undo step); a command that cannot keep a lock fails with the engine's
 * reason in the error toast, as any command does.
 */

import { useEffect, useState } from 'react';
import type { PlanOverlayProps } from '../../platform/module';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { openContextMenu } from '../../shell/ContextMenu';
import { t } from '../../i18n';
import { isHeldAngle, lockCommand } from './dimensionMenu';

interface HeldAngle { locked?: boolean; vertex?: number[]; at?: number[]; orphan?: boolean }

const toggle = (id: string, locked: boolean) => { const [c, a] = lockCommand(id, locked); return execute(c, a); };

/** The padlock glyph at (x, y): its shackle shut, or lifted and open. */
export function Padlock({ x, y, locked, onClick, onMenu }: { x: number; y: number; locked: boolean; onClick?: () => void; onMenu?: (at: { x: number; y: number }) => void }) {
  return (
    <g className={`angle-lock${locked ? ' locked' : ''}`} data-angle-lock={locked ? 'locked' : 'open'} data-overlay-hit transform={`translate(${x} ${y})`} style={{ pointerEvents: 'all', cursor: 'pointer' }}
      onPointerDown={(e) => e.stopPropagation()} onClick={onClick}
      onContextMenu={onMenu ? (e) => { e.preventDefault(); e.stopPropagation(); onMenu({ x: e.clientX, y: e.clientY }); } : undefined}>
      <rect x={-8} y={-8} width={16} height={16} className="temp-dim-hit" />
      <path className="temp-dim-lock" d={locked ? 'M-3.5 -0.5V-3a3.5 3.5 0 0 1 7 0V-0.5M-4.5 -0.5H4.5V5H-4.5Z' : 'M-3.5 -0.5V-4a3.5 3.5 0 0 1 7 0M-4.5 -0.5H4.5V5H-4.5Z'} />
      <title>{locked ? t('Locked: the angle is kept when either element turns (click to unlock)') : t('Click to lock the angle: it is kept when either element turns')}</title>
    </g>
  );
}

export function AngleLockMark({ toScreen, scale }: PlanOverlayProps) {
  const tool = useUi((s) => s.tool);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const node = !tool && selected.length === 1 ? nodes.get(selected[0]) : undefined;
  const held = isHeldAngle(node) ? node : undefined;
  const [info, setInfo] = useState<HeldAngle | null>(null);
  useEffect(() => {
    if (!held) { setInfo(null); return; }
    let live = true;
    void query<HeldAngle>('drafting.dimension', { id: held.id }).then((r) => { if (live) setInfo(r); });
    return () => { live = false; };
  }, [held?.id, nodes]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!held || !info?.vertex || !info.at || info.orphan || !(scale > 0)) return null;
  const c = toScreen({ x: info.vertex[0], y: info.vertex[1] });
  const p = toScreen({ x: info.at[0], y: info.at[1] });
  const l = Math.hypot(p.x - c.x, p.y - c.y) || 1;
  // Out beyond the arc's middle, clear of its value.
  const at = { x: p.x + ((p.x - c.x) / l) * 34, y: p.y + ((p.y - c.y) / l) * 34 };
  const locked = !!info.locked;
  return (
    <g className="angle-lock-mark" data-testid="angle-lock">
      <Padlock x={at.x} y={at.y} locked={locked} onClick={() => void toggle(held.id, !locked)}
        onMenu={(m) => openContextMenu(m.x, m.y, [{ label: locked ? 'Unlock the angle' : 'Lock the angle', onSelect: () => void toggle(held.id, !locked) }])} />
    </g>
  );
}
