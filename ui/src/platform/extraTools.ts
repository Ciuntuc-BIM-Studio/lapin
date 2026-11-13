/**
 * Tools a module adds while the app runs, beside the ones its tabs declare (decision 489, stage 4: a Python script's
 * tools, read from its header or registered with lapin.ui.tool). Kept by owner (a module's own key: one script each), they
 * are part of every tool (`allTools`): My tools shows them, Customize My tools… and the quick tools offer them under
 * "Scripts", the command palette finds them. `rev` grows with each change, so what caches the tools reads them again.
 */

import { create } from 'zustand';
import type { ToolDef } from './module';

interface ExtraToolsState { byOwner: Readonly<Record<string, readonly ToolDef[]>>; rev: number }

export const useExtraTools = create<ExtraToolsState>(() => ({ byOwner: {}, rev: 0 }));

/** The same tools as drawn (a tool's `run` looks its script up when pressed, so a new closure is no change). */
const sameTools = (a: readonly ToolDef[] | undefined, b: readonly ToolDef[]) =>
  !!a && a.length === b.length && a.every((t, i) => t.id === b[i].id && t.label === b[i].label && t.icon === b[i].icon && t.tooltip === b[i].tooltip
    && (t.items ?? []).map((x) => x.id).join('\n') === (b[i].items ?? []).map((x) => x.id).join('\n'));

/** An owner's tools now (none: its tools go). */
export function setExtraTools(owner: string, tools: readonly ToolDef[]): void {
  const { byOwner, rev } = useExtraTools.getState();
  if (!tools.length && !(owner in byOwner)) return;
  if (sameTools(byOwner[owner], tools)) return;
  const next = { ...byOwner };
  if (tools.length) next[owner] = tools; else delete next[owner];
  useExtraTools.setState({ byOwner: next, rev: rev + 1 });
}

/** Every owner's tools whose ids start with `prefix` replaced at once (a module's whole set). */
export function replaceExtraTools(prefix: string, owners: Record<string, readonly ToolDef[]>): void {
  const { byOwner, rev } = useExtraTools.getState();
  const next: Record<string, readonly ToolDef[]> = {};
  for (const [k, v] of Object.entries(byOwner)) if (!k.startsWith(prefix)) next[k] = v;
  for (const [k, v] of Object.entries(owners)) if (v.length) next[k] = v;
  const keys = new Set([...Object.keys(next), ...Object.keys(byOwner)]);
  if ([...keys].every((k) => (next[k] && byOwner[k] ? sameTools(byOwner[k], next[k]) : !next[k] && !byOwner[k]))) return;
  useExtraTools.setState({ byOwner: next, rev: rev + 1 });
}

/** The tools added so far, in the order their owners came. */
export const extraTools = (): ToolDef[] => Object.values(useExtraTools.getState().byOwner).flat();

/** What the extra tools are now, for a cache to compare. */
export const extraToolsRev = (): number => useExtraTools.getState().rev;
