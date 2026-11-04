/**
 * The quantity takeoff's own settings in its toolbar (decision 287): how it groups (kind, type, storey, material) and
 * which kinds of element it lists.
 */

import { useEffect, useRef, useState } from 'react';
import { execute, query, useSession } from '../engine/session';
import { TAKEOFF_GROUPINGS, groupingOf } from '../modules/cost/schedules';

interface TakeoffClass { class: string; label: string; count: number }

export function TakeoffControls({ id, groupBy, classes }: { id: string; groupBy: unknown; classes: unknown }) {
  const version = useSession((s) => s.version);
  const [all, setAll] = useState<TakeoffClass[]>([]);
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => { void query<TakeoffClass[]>('schedule.takeoffClasses').then((r) => setAll(r ?? [])); }, [version]);
  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    window.addEventListener('pointerdown', away);
    return () => window.removeEventListener('pointerdown', away);
  }, [open]);
  const chosen = Array.isArray(classes) && classes.length ? new Set(classes.map(String)) : null;
  const toggle = (cls: string) => {
    const next = new Set(chosen ?? all.map((c) => c.class));
    if (next.has(cls)) next.delete(cls); else next.add(cls);
    // Every kind ticked is the same as none chosen: all of them, new kinds too.
    const list = all.every((c) => next.has(c.class)) ? null : [...next];
    void execute('architecture.updateSchedule', { id, classes: list && list.length ? list : null });
  };
  return <>
    <select value={groupingOf(groupBy)} title="What the takeoff adds up together" data-testid="takeoff-group"
      onChange={(e) => void execute('architecture.updateSchedule', { id, groupBy: TAKEOFF_GROUPINGS.find((g) => g.id === e.target.value)?.groupBy ?? null })}>
      {TAKEOFF_GROUPINGS.map((g) => <option key={g.id} value={g.id}>{g.label}</option>)}
    </select>
    <div className="takeoff-kinds" ref={box}>
      <button type="button" className={`chip${chosen ? ' on' : ''}`} title="The kinds of element the takeoff lists" data-testid="takeoff-kinds" onClick={() => setOpen(!open)}>
        {chosen ? `Elements (${chosen.size})` : 'All elements'}
      </button>
      {open && (
        <div className="menu takeoff-kinds-menu" role="menu">
          {all.map((c) => (
            <label key={c.class} className="field-line">
              <input type="checkbox" checked={!chosen || chosen.has(c.class)} onChange={() => toggle(c.class)} data-class={c.class} />
              <span>{c.label}</span><span className="muted small" data-no-i18n> · {c.count}</span>
            </label>
          ))}
        </div>
      )}
    </div>
  </>;
}
