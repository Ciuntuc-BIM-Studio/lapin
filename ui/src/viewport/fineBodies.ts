/**
 * Bodies a 3D view draws otherwise than the render holds them: a member's real section at LOD 400 (decision 415), and the plants'
 * maquette bodies in the vegetation's maquette style (decision 812). The render holds the light coloured bodies for everyone; a
 * view at LOD 400 or in the maquette style asks the engine for its own (`view.fine`) and draws them in their place. Asked once
 * per element as it is built: a changed element comes back as a new render item, so only it is asked again; a view that changes
 * its level or its vegetation asks for them all again.
 */

import { useEffect, useState } from 'react';
import { engine } from '../engine/session';
import type { RenderItem, RenderPart } from '../engine/protocol';

export interface FineBodiesReply { lod: number; vegetation?: string; items: { nodeId: string; parts: RenderPart[] }[] }

/** The level from which a view draws members' real sections. */
export const FINE_LOD = 400;

/** What a reply says the view draws otherwise ("fine", "maquette", or both joined by +), or null when it draws the render as it is. */
export function modeOf(r: Pick<FineBodiesReply, 'lod' | 'vegetation'>): string | null {
  const o = [r.lod >= FINE_LOD ? 'fine' : '', r.vegetation === 'maquette' ? 'maquette' : ''].filter(Boolean).join('+');
  return o || null;
}

/** The item a body replaces, as the views draw it; the items asked about that have none, remembered as such; the mode they are for. */
let fine = new WeakMap<RenderItem, RenderItem>();
let asked = new WeakSet<RenderItem>();
let keptFor: string | null = null;

/** The item as a view draws it: its own body when `on` and it has one. */
export function fineShown(item: RenderItem, on: boolean): RenderItem {
  return on ? fine.get(item) ?? item : item;
}

/** A reply taken in: each named item gets its body, each item asked about is not asked again; a reply for another mode forgets the last one's. */
export function keepFine(render: Map<string, RenderItem>, reply: FineBodiesReply, askedOf: Iterable<RenderItem>): void {
  const mode = modeOf(reply);
  if (mode !== keptFor) { fine = new WeakMap(); asked = new WeakSet(); keptFor = mode; }
  for (const it of askedOf) asked.add(it);
  for (const b of reply.items) {
    const whole = render.get(b.nodeId);
    if (whole) fine.set(whole, { ...whole, parts: b.parts });
  }
}

/** What the render holds that has not been asked about yet (instances are stored bodies: they have no other shape). */
export function notAsked(render: Map<string, RenderItem>): RenderItem[] {
  const o: RenderItem[] = [];
  for (const it of render.values()) if (!asked.has(it) && !it.instance) o.push(it);
  return o;
}

/**
 * Whether the 3D view draws bodies of its own and they are in (`version` moves when more came), and which: its members' real
 * sections (`fine`), its plants' maquette bodies (`maquette`), for its caps. `key` stands for what may change its level or style: the nodes. Asked a moment after
 * the last change.
 */
export function useFineBodies(view3dId: string | null | undefined, render: Map<string, RenderItem>, key: unknown): { on: boolean; version: number; fine: boolean; maquette: boolean } {
  const [state, setState] = useState({ on: false, version: 0, mode: null as string | null });
  const { on, mode } = state;
  useEffect(() => {
    let stale = false;
    const id = view3dId ?? '3d';
    const off = () => setState((s) => (s.on ? { on: false, version: s.version, mode: null } : s));
    // Every such element (no ids): the first time, and whenever the view's mode changes.
    const all = () => void engine.call<FineBodiesReply>('view.fine', { id }).then((r) => {
      if (stale || !r) return;
      const now = modeOf(r);
      if (!now) { off(); return; }
      keepFine(render, r, notAsked(render));
      setState((s) => ({ on: true, version: s.version + 1, mode: now }));
    }).catch(() => undefined);
    const timer = setTimeout(() => {
      if (!on) { all(); return; }
      const todo = notAsked(render);
      // Only what is new (nothing: the engine says only the level and the style, which may have changed).
      void engine.call<FineBodiesReply>('view.fine', { id, ids: todo.map((t) => t.nodeId) }).then((r) => {
        if (stale || !r) return;
        const now = modeOf(r);
        if (!now) { off(); return; }
        if (now !== mode) { all(); return; }
        if (todo.length === 0) return;
        keepFine(render, r, todo);
        setState((s) => ({ on: true, version: s.version + 1, mode: now }));
      }).catch(() => undefined);
    }, 80);
    return () => { stale = true; clearTimeout(timer); };
  }, [view3dId, render, key, on, mode]);
  const kinds = on ? (mode ?? '').split('+') : [];
  return { on, version: state.version, fine: kinds.includes('fine'), maquette: kinds.includes('maquette') };
}
