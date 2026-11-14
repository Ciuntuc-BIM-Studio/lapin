/**
 * The cable carrier tools on the MEP tab (report #147): Cable tray draws a carrier of the chosen type (a tray, a ladder,
 * trunking or conduit; a project type or a built-in one, indicative) along a polyline at the bar's elevation, its legs at
 * 45° steps, an end on another carrier joining it there (a tee, or a reducer end to end); Lay in carrier puts the
 * selected cable runs in a carrier; the carriers schedule.
 */

import { Cable, Rows3, Waypoints } from 'lucide-react';
import type { ToolDef } from '../../../platform/module';
import { notify, useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { LengthField } from '../../../shell/cells';
import { schedule } from '../tools';
import { CLASS, carrierEnd, carrierKinds, carrierPresets, carrierTypes, isCarrier, step45, typeArg, useCarrierPrefs } from './carriers';

/** The bar: the type the tool puts down and the elevation it goes at. */
function CarrierOptions() {
  const nodes = useSession((s) => s.nodes);
  const { type, elevation } = useCarrierPrefs();
  const types = carrierTypes(nodes);
  const presets = carrierPresets();
  const kinds = carrierKinds();
  return (
    <span className="row" data-carrier-options>
      <select value={type} data-testid="carrier-type" title="The type it puts down" onChange={(e) => useCarrierPrefs.setState({ type: e.target.value })}>
        {types.length > 0 && <optgroup label="Project types">{types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}</optgroup>}
        {kinds.map((k) => (
          <optgroup key={k.key} label={`${k.label} (indicative)`}>
            {presets.filter((p) => p.kind === k.key).map((p) => <option key={p.key} value={`preset:${p.key}`} data-no-i18n>{p.name}</option>)}
          </optgroup>
        ))}
      </select>
      <span className="muted small">Elevation</span>
      <LengthField metres={elevation} onCommit={(v) => { if (v != null) useCarrierPrefs.setState({ elevation: v }); }} />
    </span>
  );
}

const carriersOf = () => [...useSession.getState().nodes.values()].filter(isCarrier);

/** A carrier along a polyline (Enter or a click on the last point finishes it); an end on another carrier joins it. */
export const cableTray: ToolDef = {
  id: 'mep.cableTray', label: 'Cable tray', icon: Rows3, shortcut: 'CT',
  gesture: { kind: 'polyline', constrain: (placed, aimed) => step45(placed, aimed) },
  options: CarrierOptions,
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const { type, elevation } = useCarrierPrefs.getState();
    const others = carriersOf();
    const start = carrierEnd(others, points[0], 0.3, ctx.storeyId);
    const end = carrierEnd(others, points[points.length - 1], 0.3, ctx.storeyId);
    const r = await ctx.execute('mep.drawCarrier', {
      storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), elevation, ...typeArg(type),
      ...(start ? { start } : {}), ...(end ? { end } : {}),
    }) as { id?: string } | null;
    if (r?.id) useUi.setState({ selected: [r.id], selection: r.id });
  },
};

/** The selected cable runs laid in a carrier: pick it. */
export const layInCarrier: ToolDef = {
  id: 'mep.layInCarrier', label: 'Lay in carrier', icon: Cable,
  gesture: { kind: 'pick', types: [CLASS] },
  complete: async ({ entity }) => {
    if (!entity) return;
    const nodes = useSession.getState().nodes;
    const cables = useUi.getState().selected.filter((id) => nodes.get(id)?.type === 'IfcCableSegment');
    if (!cables.length) { notify('Select the cable runs first, then pick the carrier they lie in.'); return; }
    await execute('mep.setCableCarrier', { ids: cables, carrierId: entity.id });
    useUi.setState({ selected: [entity.id], selection: entity.id });
  },
};

export const carrierSchedule: ToolDef = { ...schedule('mep.scheduleCarriers', 'Cable carriers schedule', 'mep-carriers'), icon: Waypoints };

export const carrierGroup = { label: 'Cable carriers', tools: [cableTray], small: [layInCarrier, carrierSchedule] };
