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
import { circleAsTwoArcs, middle } from '../../viewport/arc';

export type ChainMode = 'line' | 'arc' | 'arcCenter' | 'arcTangent' | 'arcTanTanPt';

/** The mode each chain tool is in, by its id (a tool not set is in Line). */
const useChain = create<Record<string, ChainMode>>(() => ({}));
export const chainMode = (id: string): ChainMode => useChain.getState()[id] ?? 'line';
export function setChainMode(id: string, mode: ChainMode): void {
  useChain.setState({ [id]: mode });
  // Views read the tool's gesture as they draw: they draw again, and a gesture half made starts over (`mode`).
  useToolTick.setState((s) => ({ n: s.n + 1 }));
}


/** The Line / Arc mode choice in the command line's bar (forum topic 293). */
function ModeOption({ id }: { id: string }) {
  const mode = useChain((s) => s[id] ?? 'line');
  return (
    <select className={`cad-layer${mode !== 'line' ? ' on' : ''}`} value={mode} data-testid={`chain-mode-${id}`} title="Choose straight line or curve drawing mode (3-point, centre-start-end, tangent, or 2 tangents + point)"
      onChange={(e) => setChainMode(id, e.target.value as ChainMode)}>
      <option value="line">Line</option>
      <option value="arc">Arc (3-point)</option>
      <option value="arcCenter">Arc / Circle (Centre)</option>
      <option value="arcTangent">Arc (Tangent)</option>
      <option value="arcTanTanPt">Arc (2 Tangents + Pt)</option>
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
  const ARC_3PT: Gesture = { kind: 'threePoints', arc: true, arcMode: '3pt', ...(body ? { body } : {}) };
  const ARC_CENTER: Gesture = { kind: 'threePoints', arc: true, arcMode: 'center', hint: 'pick centre, start point, then end point (click start again for full circle)', ...(body ? { body } : {}) };
  const ARC_TANGENT: Gesture = { kind: 'threePoints', arc: true, arcMode: 'tangent', hint: 'pick start at an element end (or continue tangentially), then end point', ...(body ? { body } : {}) };
  const ARC_TTP: Gesture = { kind: 'threePoints', arc: true, arcMode: 'tanTanPt', hint: 'pick first tangent line, second tangent line, then a point on the arc', ...(body ? { body } : {}) };
  const gestureFor = (m: ChainMode): Gesture => {
    switch (m) {
      case 'arc': return ARC_3PT;
      case 'arcCenter': return ARC_CENTER;
      case 'arcTangent': return ARC_TANGENT;
      case 'arcTanTanPt': return ARC_TTP;
      default: return LINE;
    }
  };
  const tool: ToolDef = {
    ...def,
    get gesture(): Gesture { return gestureFor(chainMode(id)); },
    mode: () => chainMode(id),
    complete: async (input: ToolInput, ctx: ToolContext) => {
      const m = chainMode(id);
      if (m === 'line') {
        const [a, b] = input.points;
        if (b) await line(a, b, ctx);
        return;
      }
      if (m === 'arcCenter' && input.fullCircle && input.points.length >= 2) {
        const pair = circleAsTwoArcs(input.points[0], input.points[1]);
        if (pair) {
          await arc(pair[0].a, pair[0].b, middle(pair[0]), ctx);
          await arc(pair[1].a, pair[1].b, middle(pair[1]), ctx);
        }
        return;
      }
      const [a, b, p] = input.points;
      if (p) await arc(a, b, p, ctx);
      else if (b) await line(a, b, ctx);
    },
    options: () => <><ModeOption id={id} />{More && <More />}</>,
  };
  return tool;
}

/** An old Curved … tool: the chain tool, switched to Arc. */
export function arcAlias(def: Pick<ToolDef, 'id' | 'label' | 'icon' | 'shortcut'> & Partial<ToolDef>, chain: ToolDef): ToolDef {
  return { ...def, typed: chain.typed, properties: chain.properties, run: () => { setChainMode(chain.id, 'arc'); if (useUi.getState().tool !== chain.id) activate(chain); } };
}
