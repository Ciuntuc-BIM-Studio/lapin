/**
 * The exhaust fan tools (report #55) on the MEP tab: a tool per kind — a wall fan, a ceiling fan with its duct, an inline
 * duct fan, a window fan, a ceiling fan with a light, a fan in a louvre panel — each putting its chosen type (a project
 * type or a built-in one) on the wall face nearest the click or under the ceiling where clicked; a duct from a fan to its
 * outlet; the types dialog; the fan schedule and the rooms' extract check.
 */

import { AirVent, ClipboardCheck, Fan, Lamp, Layers3, ListOrdered, PanelTop, Route, Square, Wind } from 'lucide-react';
import type { Icon } from '../../../icons';
import type { ToolDef } from '../../../platform/module';
import { notify } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { CLASS, chooseFanType, typeArgs, typesOfKind, useFanDuct, useFanPrefs } from './fans';
import { editFanType, useFanCatalogs } from './FanTypesDialog';

/** The bar of a kind's tool: the type it puts down, the project's or a built-in one. */
function kindOptions(kind: string) {
  return function FanOptions() {
    const nodes = useSession((s) => s.nodes);
    const prefs = useFanPrefs();
    const { presets } = useFanCatalogs();
    const types = typesOfKind(nodes, kind);
    const builtIn = presets.filter((p) => p.kind === kind);
    const current = prefs.types[kind] ?? types[0]?.id ?? (builtIn[0] ? `preset:${builtIn[0].key}` : '');
    return (
      <span className="cad-layer on">
        <select value={current} data-testid={`fan-type-${kind}`} title="The type it puts down" onChange={(e) => chooseFanType(kind, e.target.value)}>
          {!types.length && !builtIn.length && <option value="">(its first type is made)</option>}
          {types.length > 0 && <optgroup label="Project types">{types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}</optgroup>}
          {builtIn.length > 0 && <optgroup label="Built-in types">{builtIn.map((p) => <option key={p.key} value={`preset:${p.key}`}>{p.name}</option>)}</optgroup>}
        </select>
      </span>
    );
  };
}

function fanTool(kind: string, label: string, icon: Icon, hint: string, shortcut?: string): ToolDef {
  return {
    id: `mep.fan.${kind}`, label, icon, shortcut,
    gesture: { kind: 'point' },
    options: kindOptions(kind),
    complete: async ({ points: [p] }, ctx) => {
      const nodes = useSession.getState().nodes;
      const r = (await ctx.execute('mep.placeFan', { storeyId: ctx.storeyId, ...typeArgs(kind, nodes), x: p.x, y: p.y })) as { id?: string } | null;
      if (!r?.id) notify(hint);
    },
  };
}

export const wallFan = fanTool('wallAxial', 'Wall fan', Fan, 'Click near the face of a wall.', 'VF');
export const ceilingFan = fanTool('ceilingDuct', 'Ceiling fan', AirVent, 'Click in the room, under its ceiling.');
export const inlineFan = fanTool('inline', 'Inline duct fan', Wind, 'Click where the fan goes in the ceiling void.');
export const windowFan = fanTool('window', 'Window fan', Square, 'Click near the face of a wall, at the window.');
export const lightFan = fanTool('ceilingLight', 'Ceiling fan with light', Lamp, 'Click in the room, under its ceiling.');
export const panelFan = fanTool('louvrePanel', 'Fan with louvre panel', PanelTop, 'Click near the face of a wall.');

/** A duct from a ceiling or inline fan to its outlet: the fan picked, then where it goes out (a hood on a wall's outside, or up through the roof as the bar says). */
export const fanDuct: ToolDef = {
  id: 'mep.fanDuct', label: 'Duct to outlet', icon: Route,
  gesture: { kind: 'pickThenPoint', types: [CLASS], hint: 'click a ceiling or inline fan, then where its duct goes out' },
  options: function DuctOptions() {
    const roof = useFanDuct((s) => s.roof);
    return (
      <button type="button" className={`cad-toggle${roof ? ' on' : ''}`} data-testid="fan-outlet-roof" title="The duct rises through the roof to a cowl, instead of a hood on the wall"
        onClick={() => useFanDuct.setState({ roof: !roof })}>THROUGH THE ROOF</button>
    );
  },
  complete: async ({ points: [, q], entity }, ctx) => {
    if (!entity || !q) return;
    const { roof } = useFanDuct.getState();
    await ctx.execute('mep.fanDuct', { id: entity.id, x: q.x, y: q.y, outlet: roof ? 'roof' : 'wall' });
  },
};

export const fanTypes: ToolDef = { id: 'mep.fanTypes', label: 'Fan types', icon: Layers3, run: () => editFanType(undefined) };

const schedule = (id: string, label: string, kind: string, icon: Icon): ToolDef => ({
  id, label, icon,
  run: () => void execute('architecture.createSchedule', { kind }).then((r) => { if (r) notify(`${label} made: it is in the project browser, under Schedules.`); }),
});
export const fanSchedule = schedule('mep.fanSchedule', 'Fan schedule', 'fans', ListOrdered);
export const fanRoomCheck = schedule('mep.fanRooms', 'Room extract check', 'fanRooms', ClipboardCheck);

export const fanGroup = { label: 'Fans', tools: [wallFan, ceilingFan], small: [inlineFan, windowFan, lightFan, panelFan, fanDuct, fanTypes, fanSchedule, fanRoomCheck] };
