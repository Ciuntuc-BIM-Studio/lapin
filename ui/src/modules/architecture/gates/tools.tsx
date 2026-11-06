/**
 * The gate tools (report #57) on the Site tab: a tool per kind — swing, double swing, sliding, cantilever, pedestrian,
 * with a pedestrian door, folding — each putting its chosen type (a project style or a built-in one) on the wall axis or
 * drawn line (a parcel's edge) nearest the click, or between two clicks; the configurator and the schedule.
 */

import { DoorOpen, Fence, Layers3, ListOrdered, MoveHorizontal, PanelsLeftRight, Columns3, ArrowRightLeft, FoldHorizontal } from 'lucide-react';
import type { Icon } from '../../../icons';
import type { RibbonGroupDef, ToolDef } from '../../../platform/module';
import { notify } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { chooseGateType, gateTypeArgs, useGatePrefs, TYPE_CLASS, kindOf } from './gates';
import { editGateType, useGateCatalogs } from './GateTypesDialog';

/** The bar of a kind's tool: the type it puts down (the project's styles or Lapin's), and whether a click finds a line. */
function kindOptions(kind: string) {
  return function GateOptions() {
    const nodes = useSession((s) => s.nodes);
    const prefs = useGatePrefs();
    const { styles } = useGateCatalogs();
    const types = [...nodes.values()].filter((n) => n.type === TYPE_CLASS && kindOf(n) === kind).sort((a, b) => a.name.localeCompare(b.name));
    const builtIn = styles.filter((s) => s.kind === kind);
    const current = prefs.type[kind] ?? types[0]?.id ?? '';
    return (
      <>
        <span className="cad-layer on">
          <select value={current} data-testid={`gate-type-${kind}`} title="The type it puts down" onChange={(e) => chooseGateType(kind, e.target.value)}>
            {!types.length && <option value="">(its first type is made)</option>}
            {types.length > 0 && <optgroup label="Project styles">{types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}</optgroup>}
            {builtIn.length > 0 && <optgroup label="Built-in styles">{builtIn.map((s) => <option key={s.key} value={`style:${s.key}`}>{s.name}</option>)}</optgroup>}
          </select>
        </span>
        <button type="button" className={`cad-toggle${prefs.onLine ? ' on' : ''}`} data-testid="gate-on-line" title="On the nearest wall's axis or drawn line (a fence, a parcel's edge); off: across the two points clicked"
          onClick={() => useGatePrefs.setState({ onLine: !prefs.onLine })}>ON LINES</button>
      </>
    );
  };
}

/** A kind's tool: two clicks, the gate's ends: it stands between them, or with its middle on the nearest line (ON LINES). */
function gateTool(kind: string, label: string, icon: Icon, shortcut?: string): ToolDef {
  return {
    id: `architecture.gate.${kind}`, label, icon, shortcut,
    gesture: { kind: 'twoPoints' },
    options: kindOptions(kind),
    complete: async ({ points: [a, b] }, ctx) => {
      const nodes = useSession.getState().nodes;
      const { onLine } = useGatePrefs.getState();
      const args: Record<string, unknown> = { storeyId: ctx.storeyId, ...gateTypeArgs(kind, nodes), x: a.x, y: a.y, onLine };
      if (b) Object.assign(args, { x2: b.x, y2: b.y });
      await ctx.execute('architecture.placeGate', args);
    },
  };
}

export const swingGate = gateTool('swing', 'Swing gate', DoorOpen);
export const doubleGate = gateTool('doubleSwing', 'Double swing gate', PanelsLeftRight, 'GT');
export const slidingGate = gateTool('sliding', 'Sliding gate', MoveHorizontal);
export const cantileverGate = gateTool('cantilever', 'Cantilever sliding gate', ArrowRightLeft);
export const pedestrianGate = gateTool('pedestrian', 'Pedestrian gate', Fence);
export const wicketGate = gateTool('swingWicket', 'Gate with side pedestrian door', Columns3);
export const foldingGate = gateTool('folding', 'Folding gate', FoldHorizontal);

export const gateTypes: ToolDef = { id: 'architecture.gateTypes', label: 'Gate types', icon: Layers3, run: () => editGateType(undefined) };
export const gateSchedule: ToolDef = {
  id: 'architecture.gateSchedule', label: 'Gate schedule', icon: ListOrdered,
  run: () => void execute('architecture.createSchedule', { kind: 'gates' }).then((r) => { if (r) notify('Gate schedule made: it is in the project browser, under Schedules.'); }),
};

/** Into the Site tab, after the landscape. */
export const gateGroup: RibbonGroupDef = {
  label: 'Gates', after: 'Landscape', tools: [doubleGate, slidingGate], small: [swingGate, cantileverGate, pedestrianGate, wicketGate, foldingGate, gateTypes, gateSchedule],
};
