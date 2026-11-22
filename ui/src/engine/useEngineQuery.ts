/**
 * Ask the engine a question for as long as `deps` stay the same (audit 22.11.2026, §3.2): the one hook for "the engine's
 * answer to this query, for this selection". An answer that arrives after the deps changed is ignored (room B never shows
 * room A's), and the value is null from the render the deps change until the new answer lands.
 */

import { useCallback, useEffect, useMemo, useState, type DependencyList } from 'react';
import { query } from './session';

/**
 * Ask, and hand the answer to `set` only while `isLive()` says the question is still the current one. Exported for tests
 * and for code that asks outside a component.
 */
export async function askWhileLive<T>(ask: () => Promise<T | null>, isLive: () => boolean, set: (value: T | null) => void): Promise<void> {
  let value: T | null = null;
  try { value = await ask(); } catch { value = null; }
  if (isLive()) set(value);
}

/**
 * The engine's answer to the query `name` with `args`, asked again whenever `deps` change (what the answer is about: the
 * selected id, a counter). Null while it is on its way, when the engine cannot answer, and when `name` is null (nothing
 * to ask). `refresh` lists what only makes the answer stale, not another one (the model's `nodes` or `version`): it asks
 * again but keeps the last answer on screen meanwhile, so a table does not blink at every edit. An answer that lands
 * after either changed is ignored. Returns the value and a setter, for a panel that shows its own edit before the
 * engine's next answer; `useEngineQuery` is the value alone.
 */
export function useEngineQueryState<T>(name: string | null, args: Record<string, unknown>, deps: DependencyList, refresh: DependencyList = []): [T | null, (value: T | null) => void] {
  // One token per set of deps: an answer belongs to the token it was asked under.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const token = useMemo(() => ({}), [name, ...deps]);
  const [held, setHeld] = useState<{ token: object; value: T | null }>({ token, value: null });
  useEffect(() => {
    if (name === null) return;
    let live = true;
    void askWhileLive(() => query<T>(name, args), () => live, (value) => setHeld({ token, value }));
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, ...refresh]);
  const set = useCallback((value: T | null) => setHeld({ token, value }), [token]);
  return [held.token === token ? held.value : null, set];
}

/** The engine's answer to a query for these deps (see `useEngineQueryState`): null until it lands, and again whenever the deps change. */
export function useEngineQuery<T>(name: string | null, args: Record<string, unknown>, deps: DependencyList, refresh: DependencyList = []): T | null {
  return useEngineQueryState<T>(name, args, deps, refresh)[0];
}
