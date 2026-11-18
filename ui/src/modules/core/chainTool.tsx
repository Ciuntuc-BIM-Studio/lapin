/**
 * One chain tool for a linear element with a Line / Arc option (audit 2026-11-15, S3), as a polyline's "A": the Wall, the Beam, the
 * Strip footing and the room separation line draw chained straight pieces (each end the start of the next) or, set to Arc, a start, an
 * end and a point the arc passes through. The engine takes the arc's third point (x3, y3). The old Curved … tools stay as tools
 * (`legacyTools`) that open the chain tool already in Arc mode, so a quick slot, My tools or a shortcut naming them keeps working.
 */
import type { ComponentType } from 'react';
import { create } from 'zustand';
import { useUi } from '../../app/store';
import { activate, useToolTick } from '../../platform/modules';
import type { Gesture, ToolContext, ToolDef, ToolInput, Vec2 } from '../../platform/module';

export type ChainMode = 'line' | 'arc';

/** The mode each chain tool is in, by its id (a tool not set is in Line). */
const useChain = create<Record<string, ChainMode>>(() => ({}));
export const chainMode = (id: string): ChainMode => useChain.getState()[id] ?? 'line';
export function setChainMode(id: string, mode: ChainMode): void {
  useChain.setState({ [id]: mode });
  // Views read the tool's gesture as they draw: they draw again, and a gesture half made starts over (`mode`).
  useToolTick.setState((s) => ({ n: s.n + 1 }));
}


/** The Line / Arc choice in the command line's bar. */
function ModeOption({ id }: { id: string }) {
  const mode = useChain((s) => s[id] ?? 'line');
  return (
    <select className={`cad-layer${mode === 'arc' ? ' on' : ''}`} value={mode} data-testid={`chain-mode-${id}`} title="Straight pieces, each starting where the last ended, or an arc: its start, its end, then a point it passes through"
      onChange={(e) => setChainMode(id, e.target.value as ChainMode)}>
      <option value="line">Line</option>
      <option value="arc">Arc</option>
    </select>
  );
}

interface ChainSpec extends Omit<ToolDef, 'gesture' | 'complete' | 'options' | 'mode'> {
  /** The straight piece, from a to b. */
  line: (a: Vec2, b: Vec2, ctx: ToolContext) => Promise<void> | void;
  /** The arc, from a to b through p. */
  arc: (a: Vec2, b: Vec2, p: Vec2, ctx: ToolContext) => Promise<void> | void;
  /** What else the tool lets one choose while it is on, beside the Line / Arc choice. */
  more?: ComponentType;
  /** What the path drawn would build, drawn with it before the click (a wall's body, decision 692). */
  body?: (path: { a: Vec2; b: Vec2; rise: number }) => Vec2[][];
}

/** A chain tool: its gesture and what it makes follow its mode. */
export function chainTool(spec: ChainSpec): ToolDef {
  const { line, arc, more: More, body, ...def } = spec;
  const id = def.id;
  const LINE: Gesture = { kind: 'twoPoints', repeat: true, ...(body ? { body } : {}) };
  const ARC: Gesture = { kind: 'threePoints', arc: true, ...(body ? { body } : {}) };
  const tool: ToolDef = {
    ...def,
    get gesture(): Gesture { return chainMode(id) === 'arc' ? ARC : LINE; },
    mode: () => chainMode(id),
    complete: async (input: ToolInput, ctx: ToolContext) => {
      if (chainMode(id) === 'arc') { const [a, b, p] = input.points; if (p) await arc(a, b, p, ctx); } else { const [a, b] = input.points; if (b) await line(a, b, ctx); }
    },
    options: () => <><ModeOption id={id} />{More && <More />}</>,
  };
  return tool;
}

/** An old Curved … tool: the chain tool, switched to Arc. */
export function arcAlias(def: Pick<ToolDef, 'id' | 'label' | 'icon' | 'shortcut'> & Partial<ToolDef>, chain: ToolDef): ToolDef {
  return { ...def, typed: chain.typed, properties: chain.properties, run: () => { setChainMode(chain.id, 'arc'); if (useUi.getState().tool !== chain.id) activate(chain); } };
}
