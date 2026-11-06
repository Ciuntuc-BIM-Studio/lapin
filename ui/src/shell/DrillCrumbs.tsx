/**
 * Drill-down picking's breadcrumb over the view (platform/assemblies.ts): the storey, then each level from the top
 * whole to the selection, each a button selecting its level (the storey: nothing). While drilled into a whole, the 3D
 * dims what is outside it (more lightly than an edit scope does), and Esc steps one level up.
 */

import { Fragment, useEffect } from 'react';
import { select, useUi } from '../app/store';
import { useDisplay } from '../app/display';
import { useSession } from '../engine/session';
import { setLooks, type Look } from '../platform/looks';
import { drillEscape, drillInside, drillLevel, useDrill } from '../platform/assemblies';
import { useScope } from '../platform/scope';
import { useCad } from '../viewport/cad/session';

/** Esc read once for every pane's HUD, before the views' own (capture), so a drilled selection steps up instead of being let go. */
let listening = 0;
const onKey = (e: KeyboardEvent) => {
  if (e.key !== 'Escape' || (e.target as HTMLElement)?.closest?.('input, select, textarea')) return;
  if (useUi.getState().tool || useUi.getState().dialog || useCad.getState().step) return;
  if (drillEscape()) { e.preventDefault(); e.stopImmediatePropagation(); }
};

export function DrillCrumbs() {
  const on = useDisplay((s) => s.drillSelect);
  const path = useDrill((s) => s.path);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const scope = useScope((s) => s.scope);
  useEffect(() => {
    if (listening++ === 0) window.addEventListener('keydown', onKey, true);
    return () => { if (--listening === 0) window.removeEventListener('keydown', onKey, true); };
  }, []);
  useEffect(() => {
    const inside = on && !scope ? drillInside(path, selected, nodes, relations) : null;
    const looks: Record<string, Look> = {};
    if (inside) for (const n of nodes.values()) if (!inside.has(n.id)) looks[n.id] = { opacity: 0.4 };
    setLooks('drill', looks);
  }, [on, scope, path, selected, nodes, relations]);
  const level = on ? drillLevel(path, selected) : -1;
  if (level < 0 || !path) return null;
  const leaf = nodes.get(path[path.length - 1]);
  const storey = leaf?.storeyId ? nodes.get(leaf.storeyId) : undefined;
  return (
    <nav className="drill-crumbs" aria-label="Assembly levels" data-testid="drill-crumbs">
      <button type="button" title="Select nothing" onClick={() => { useDrill.setState({ path: null }); select(null); }}>
        {storey ? <span data-no-i18n>{storey.name}</span> : 'Model'}
      </button>
      {path.slice(0, Math.max(level + 1, 1)).map((id, i) => (
        <Fragment key={id}>
          <span className="drill-sep" aria-hidden>›</span>
          <button type="button" className={i === level ? 'current' : undefined} title="Select this level" onClick={() => select(id)}>
            <span data-no-i18n>{nodes.get(id)?.name || nodes.get(id)?.type || id}</span>
          </button>
        </Fragment>
      ))}
    </nav>
  );
}
