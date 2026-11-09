/**
 * A member's real section in a 3D view at LOD 400 (decision 415): the render holds the light sharp bodies for everyone; a view
 * at LOD 400 asks the engine for the fine ones (`view.fine`) and draws them in their place. Asked once per element as it is
 * built: a changed element comes back as a new render item, so only it is asked again.
 */

import { useEffect, useState } from 'react';
import { engine } from '../engine/session';
import type { RenderItem, RenderPart } from '../engine/protocol';

export interface FineBodiesReply { lod: number; items: { nodeId: string; parts: RenderPart[] }[] }

/** The level from which a view draws members' real sections. */
export const FINE_LOD = 400;

/** The item a fine body replaces, as the views draw it; items asked about that have none are remembered as such. */
const fine = new WeakMap<RenderItem, RenderItem>();
const asked = new WeakSet<RenderItem>();

/** The item as a view draws it: its fine body when `on` and it has one. */
export function fineShown(item: RenderItem, on: boolean): RenderItem {
  return on ? fine.get(item) ?? item : item;
}

/** A reply taken in: each named item gets its fine body, each item asked about is not asked again. */
export function keepFine(render: Map<string, RenderItem>, reply: FineBodiesReply, askedOf: Iterable<RenderItem>): void {
  for (const it of askedOf) asked.add(it);
  for (const b of reply.items) {
    const whole = render.get(b.nodeId);
    if (whole) fine.set(whole, { ...whole, parts: b.parts });
  }
}

/** What the render holds that has not been asked about yet (instances are stored bodies: they have no finer shape). */
export function notAsked(render: Map<string, RenderItem>): RenderItem[] {
  const o: RenderItem[] = [];
  for (const it of render.values()) if (!asked.has(it) && !it.instance) o.push(it);
  return o;
}

/**
 * Whether the 3D view is at LOD 400 and its fine bodies are in (`version` moves when more came). `key` stands for what may change
 * its level: the nodes. Asked a moment after the last change.
 */
export function useFineBodies(view3dId: string | null | undefined, render: Map<string, RenderItem>, key: unknown): { on: boolean; version: number } {
  const [state, setState] = useState({ on: false, version: 0 });
  const on = state.on;
  useEffect(() => {
    let stale = false;
    const timer = setTimeout(() => {
      const todo = on ? notAsked(render) : [];
      // Off, the engine says only the level; first at LOD 400 it gives every such element (no ids), then only what changed.
      if (on && todo.length === 0) {
        void engine.call<FineBodiesReply>('view.fine', { id: view3dId ?? '3d', ids: [] }).then((r) => { if (!stale && r && r.lod < FINE_LOD) setState((s) => ({ on: false, version: s.version })); }).catch(() => undefined);
        return;
      }
      void engine.call<FineBodiesReply>('view.fine', on ? { id: view3dId ?? '3d', ids: todo.map((t) => t.nodeId) } : { id: view3dId ?? '3d' }).then((r) => {
        if (stale || !r) return;
        if (r.lod < FINE_LOD) { setState((s) => (s.on ? { on: false, version: s.version } : s)); return; }
        keepFine(render, r, on ? todo : notAsked(render));
        setState((s) => ({ on: true, version: s.version + 1 }));
      }).catch(() => undefined);
    }, 80);
    return () => { stale = true; clearTimeout(timer); };
  }, [view3dId, render, key, on]);
  return state;
}
