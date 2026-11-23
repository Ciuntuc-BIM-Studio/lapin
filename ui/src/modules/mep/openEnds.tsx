/**
 * What is left open, always in sight: while a system is edited (and in any
 * plan of the MEP discipline) a small red mark on every free port and every
 * run end on nothing, on the screen only (a plan overlay: drawings, sheets
 * and the PDF are the engine's and never show it); the edit's bar counts them
 * ("3 open ends") and selects them on a click, as Open ports does.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import type { PlanOverlayProps } from '../../platform/module';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { useScope } from '../../platform/scope';
import { openPortsQuery, type OpenItem } from './network';

/** The open items last asked for: for what (a system, or "*": all) and at which version of the model. */
const useOpenCache = create<{ key: string | null; of: string | null; items: OpenItem[] }>(() => ({ key: null, of: null, items: [] }));
let asking: string | null = null;

/** The open ends of a system (null: of every system), asked again as the model changes (the last answer shown meanwhile); [] while off. */
export function useOpenItems(systemId: string | null, on: boolean): OpenItem[] {
  const version = useSession((s) => s.version);
  const of = systemId ?? '*';
  const key = on ? `${of}@${version}` : null;
  const items = useOpenCache((s) => (on && s.of === of ? s.items : null));
  useEffect(() => {
    if (!key || useOpenCache.getState().key === key || asking === key) return;
    asking = key;
    void openPortsQuery(systemId ?? undefined).then((got) => {
      if (asking === key) asking = null;
      useOpenCache.setState({ key, of, items: got ?? [] });
    });
  }, [key, of, systemId]);
  return items ?? NONE;
}
const NONE: OpenItem[] = [];

/** The system being edited, if any (the MEP edit scope). */
const useEdited = (): string | null => useScope((s) => (s.scope?.kind === 'System' ? s.scope.id : null));

/** The elements the open items are on, each once, for the selection. */
export const openIds = (items: OpenItem[]): string[] => [...new Set(items.map((i) => i.id))];

/** Where an open item is in plan. */
const atOf = (i: OpenItem): { x: number; y: number } => ({ x: i.at[0], y: i.at[1] });

/** The marks over the plan: the edited system's open ends, or every system's in a plan of the MEP discipline. */
export function OpenEndsOverlay({ toScreen, view }: PlanOverlayProps) {
  const edited = useEdited();
  const discipline = useSession((s) => (view.id ? s.nodes.get(view.id)?.props.discipline : undefined));
  const nodes = useSession((s) => s.nodes);
  const on = !!edited || String(discipline ?? '').toLowerCase() === 'mep';
  const items = useOpenItems(edited, on);
  if (!on || !items.length) return null;
  const here = items.filter((i) => !view.storeyId || !nodes.get(i.id)?.storeyId || nodes.get(i.id)?.storeyId === view.storeyId);
  return (
    <g className="mep-open-ends" data-testid="mep-open-ends">
      {here.map((i, k) => {
        const p = toScreen(atOf(i));
        return <circle key={`${i.id}:${k}`} cx={p.x} cy={p.y} r={5} fill="rgba(224, 49, 49, 0.25)" stroke="#e03131" strokeWidth={1.5} data-open-end={i.kind} />;
      })}
    </g>
  );
}

/** In the edit's bar: how many ends are open, selected on a click. */
export function OpenEndsButton() {
  const edited = useEdited();
  const items = useOpenItems(edited, !!edited);
  if (!edited || !items.length) return null;
  const select = () => { const ids = openIds(items); useUi.setState({ selected: ids, selection: ids[0] ?? null }); };
  return (
    <button type="button" className="scope-extra" data-testid="mep-open-ends-count" onClick={select} title="Select the open ends">
      <span data-no-i18n>{items.length}</span> <span>{items.length === 1 ? 'open end' : 'open ends'}</span>
    </button>
  );
}
