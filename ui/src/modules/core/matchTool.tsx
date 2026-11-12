/**
 * Match properties on the command line (MATCHPROP, MA; decision 490): the source, the element selected or the one clicked
 * first, then each element clicked takes what it carries (matchProperties.ts), one undo step each, until Enter or Esc. Its
 * bar: the source, Apply to the selection, and the groups it carries (remembered on this computer).
 */

import { useEffect, useRef, useState } from 'react';
import { Settings2 } from 'lucide-react';
import { create } from 'zustand';
import { useUi } from '../../app/store';
import { execute, stepsOf, useSession } from '../../engine/session';
import { num, relatingOf, typeOf } from '../../engine/model';
import { modules, setTypeOf } from '../../platform/modules';
import { cadHost, cadLog, submit, useCad, type Step } from '../../viewport/cad/session';
import { useTagHover } from '../../viewport/tagTarget';
import { MATCH_GROUPS, matchSteps, readGroups, type MatchContext, type MatchGroup } from './matchProperties';

const KEY = 'lapin.matchProperties';
const remembered = (): Set<MatchGroup> => { try { return readGroups(localStorage.getItem(KEY)); } catch { return readGroups(null); } };

interface MatchState { source: string | null; groups: Set<MatchGroup>; settings: boolean }
export const useMatch = create<MatchState>(() => ({ source: null, groups: remembered(), settings: false }));

export function setMatchGroup(id: MatchGroup, on: boolean): void {
  const groups = new Set(useMatch.getState().groups);
  if (on) groups.add(id); else groups.delete(id);
  useMatch.setState({ groups });
  try { localStorage.setItem(KEY, JSON.stringify([...groups])); } catch { /* remembered for this session only */ }
}

/** What the steps read of the model and the product, now. */
function context(): MatchContext {
  const s = useSession.getState();
  return {
    fieldsOf: (cls) => s.info?.nodeTypes.find((t) => t.type === cls)?.fields ?? [],
    typeOf: (id) => typeOf(s, id),
    typeSteps: (cls, ids, typeId) => { const set = setTypeOf(cls); return set ? stepsOf(() => set(ids, typeId)) : null; },
    sillOf: (id) => { const o = relatingOf(s, id, 'IfcRelFillsElement'); return o ? num(o, 'sill') : null; },
    phases: modules.some((m) => m.id === 'phases'),
  };
}

/** The source's properties given to `ids` (one undo step); whether any took something. */
export async function matchTo(ids: readonly string[]): Promise<boolean> {
  const { source, groups } = useMatch.getState();
  const nodes = useSession.getState().nodes;
  const src = source ? nodes.get(source) : undefined;
  if (!src) return false;
  const targets = ids.map((id) => nodes.get(id)).filter((n): n is NonNullable<typeof n> => !!n && n.id !== src.id);
  const steps = matchSteps(src, targets, groups, context());
  if (!steps.length) { cadLog('Nothing to change there.'); return false; }
  const r = await execute('edit.batch', { steps });
  if (r === null && useSession.getState().error) return false;
  cadLog('Properties matched.');
  return true;
}

const known = (id: string | null | undefined) => (id && !id.includes('#') ? useSession.getState().nodes.get(id) ?? null : null);
const done = () => { useTagHover.setState({ element: null }); useMatch.setState({ settings: false }); };

function sourceStep(): Step {
  const take = (id: string | null | undefined): Step => {
    const n = known(id);
    if (!n) { cadLog('Pick an element.'); return sourceStep(); }
    useMatch.setState({ source: n.id });
    return targetStep();
  };
  return {
    prompt: 'Click the element whose properties to carry', want: 'select', hover: true,
    point: (p) => take(cadHost()?.pickAny(p)),
    element: (id) => take(id),
    enter: () => { done(); return null; },
    escape: done,
  };
}

function targetStep(): Step {
  const take = async (id: string | null | undefined): Promise<Step> => {
    const n = known(id);
    if (!n) cadLog('Pick an element.');
    else if (n.id === useMatch.getState().source) cadLog('That is the source: click another element.');
    else await matchTo([n.id]);
    return targetStep();
  };
  return {
    prompt: 'Click the elements to match [Source/Selection]', want: 'select', hover: true,
    point: (p) => take(cadHost()?.pickAny(p)),
    element: (id) => take(id),
    keywords: {
      SOURCE: () => { useMatch.setState({ source: null }); return sourceStep(); },
      SELECTION: async () => { await matchTo(useUi.getState().selected); return targetStep(); },
    },
    enter: () => { done(); return null; },
    escape: done,
  };
}

/** MATCHPROP: the selected element (the one last picked of several) is the source, else the first one clicked. */
export function matchProperties(): Step {
  useMatch.setState({ source: null, settings: false });
  const sel = known(useUi.getState().selection);
  if (!sel) return sourceStep();
  useMatch.setState({ source: sel.id });
  return targetStep();
}

/** The command line's bar while it runs: the source, Apply to the selection, and what it carries. */
export function MatchOptions() {
  const { source, groups, settings } = useMatch();
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const step = useCad((s) => s.step);
  const box = useRef<HTMLSpanElement>(null);
  const [busy, setBusy] = useState(false);
  // A click outside the settings closes them.
  useEffect(() => {
    if (!settings) return;
    const away = (e: PointerEvent) => { if (!box.current?.contains(e.target as Node)) useMatch.setState({ settings: false }); };
    window.addEventListener('pointerdown', away, true);
    return () => window.removeEventListener('pointerdown', away, true);
  }, [settings]);
  const src = source ? nodes.get(source) : undefined;
  const others = selected.filter((id) => id !== source && nodes.has(id));
  return (
    <span className="match-bar" ref={box} data-testid="match-bar">
      {src && <span className="muted small match-source" title="The element whose properties are carried">From <b data-no-i18n>{src.name}</b></span>}
      {src && <button type="button" className="cad-toggle" data-action="match-source" onClick={() => void submit('SOURCE')}>Another source</button>}
      {src && others.length > 0 && step && <button type="button" className="cad-toggle on" data-action="match-selection" disabled={busy}
        onClick={() => { setBusy(true); void matchTo(others).finally(() => setBusy(false)); }}>{`Apply to the selection (${others.length})`}</button>}
      <button type="button" className={`cad-toggle${settings ? ' on' : ''}`} data-action="match-settings" title="What to carry" aria-expanded={settings} onClick={() => useMatch.setState({ settings: !settings })}><Settings2 size={11} /></button>
      {settings && (
        <span className="match-settings" role="dialog" aria-label="What to carry" data-testid="match-settings">
          <span className="eyebrow">What to carry</span>
          {MATCH_GROUPS.map((g) => (
            <label key={g.id} className="check"><input type="checkbox" data-group={g.id} checked={groups.has(g.id)} onChange={(e) => setMatchGroup(g.id, e.target.checked)} /> {g.label}</label>
          ))}
          <span className="muted small">Never where an element stands, its shape or its GlobalId.</span>
        </span>
      )}
    </span>
  );
}
