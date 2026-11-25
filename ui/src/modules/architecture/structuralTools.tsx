/**
 * The Structure tab's Wall and Floor (docs/plan-structural-usage.md, the user's answer 3): Architecture's own tools, drawing
 * the same walls and floors, marked Structural (load-bearing) as they are made, in the same step. A tick in the command
 * line's bar (on by default) turns it off; Architecture's tools leave it to the rule.
 */

import { create } from 'zustand';
import type { ToolContext, ToolDef } from '../../platform/module';

export const useStructuralDrawing = create<{ on: boolean }>(() => ({ on: true }));

/** The commands that make a wall or a floor, which take the tick. */
const MAKES = new Set(['architecture.addWall', 'architecture.addSlab', 'architecture.addFloorFromBuilding']);

/** The arguments with the tick, when on, for a command that makes a wall or a floor. */
export function withStructural(command: string, args: Record<string, unknown> | undefined, on: boolean): Record<string, unknown> | undefined {
  return on && MAKES.has(command) ? { ...(args ?? {}), loadBearing: true } : args;
}

function StructuralOption() {
  const on = useStructuralDrawing((s) => s.on);
  return (
    <label className="cad-layer" title="What is drawn is marked Structural (load-bearing); untick to leave it to the rule" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      <input type="checkbox" data-testid="draw-structural" checked={on} onChange={(e) => useStructuralDrawing.setState({ on: e.target.checked })} />
      <span>Structural</span>
    </label>
  );
}

/** One of Architecture's tools as the Structure tab's: its own id and label, its walls or floors drawn ticked. */
export function asStructural(tool: ToolDef, id: string, label: string): ToolDef {
  const Options = tool.options;
  const complete = tool.complete;
  return {
    ...tool, id, label, shortcut: undefined,
    complete: complete && ((input, ctx: ToolContext) => complete(input, { ...ctx, execute: (command, args) => ctx.execute(command, withStructural(command, args, useStructuralDrawing.getState().on)) })),
    options: () => <>{Options && <Options />}<StructuralOption /></>,
  };
}
