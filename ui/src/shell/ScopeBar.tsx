/** The edit scope's bar over the view: what is being edited, and Done (Esc, with no tool or command running, does the same). The 3D is faded outside the scope. */

import { useEffect } from 'react';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { setLooks, type Look } from '../platform/looks';
import { exitScope, useScope } from '../platform/scope';
import { useCad } from '../viewport/cad/session';

export function ScopeBar() {
  const scope = useScope((s) => s.scope);
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    const looks: Record<string, Look> = {};
    if (scope) for (const n of nodes.values()) if (!scope.includes(n)) looks[n.id] = { opacity: 0.18 };
    setLooks('scope', looks);
  }, [scope, nodes]);
  useEffect(() => {
    if (!scope) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || (e.target as HTMLElement)?.closest('input, select, textarea')) return;
      if (useUi.getState().tool || useUi.getState().dialog || useCad.getState().step) return;
      void exitScope();
    };
    // Read before the views' own Esc (capture), so an Esc that stops a tool does not also leave the scope.
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [scope]);
  if (!scope) return null;
  return (
    <div className="scope-bar" role="status">
      <span>EDITING {scope.kind ? `${scope.kind.toUpperCase()} · ` : ''}{scope.label.toUpperCase()}</span>
      <button type="button" onClick={() => void exitScope()} title="Leave the edit mode (Esc)">Done</button>
    </div>
  );
}
