/**
 * The site's drawings (docs/site-plans.md): a site plan (1:500) and a
 * location plan (1:2000), plan views the engine draws as the whole model from
 * above, with a north arrow, the map's coordinate grid and a scale bar. In a
 * site view's settings: what it is, the grid's step, whether the arrow and
 * the bar are there (annotations of the view, decision 722: select one to
 * move it by its grip, size it or choose its symbol), and whether the project
 * stands out in red.
 */

import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { NumberField } from '../../shell/cells';

export type SitePurpose = 'situation' | 'location';

export const SITE_PURPOSES: [SitePurpose, string][] = [['situation', 'Site plan'], ['location', 'Location plan']];

/** A new site view of that purpose (its scale and name the engine's defaults), on the site's storey; its id. */
export async function createSiteView(purpose: SitePurpose): Promise<string | null> {
  const r = (await execute('view.create', { kind: 'plan', site: purpose })) as { id?: string } | null;
  return r?.id ?? null;
}

/** The site view's own fields, in its view settings. */
export function SitePlanFields({ view }: { view: NodeDto }) {
  const set = (patch: Record<string, unknown>) => void execute('view.setSite', { id: view.id, ...patch });
  // Its north arrow and scale bar are annotations of its own (decision 722): there when one is.
  const nodes = useSession((s) => s.nodes);
  const has = (kind: string) => [...nodes.values()].some((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === kind && n.props.viewId === view.id);
  const scale = Number(view.props.scale ?? 500);
  const step = typeof view.props.gridStep === 'number' ? view.props.gridStep : null;
  return (
    <>
      <dt title="A site plan (Romanian: plan de situație) numbers the parcel's corners; a location plan (plan de încadrare în zonă) shows the site in its area">Drawing</dt>
      <dd>
        <select value={String(view.props.site)} onChange={(e) => set({ site: e.target.value })}>
          {SITE_PURPOSES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
        </select>
      </dd>
      <dt title="Crosses where the map's eastings and northings meet, every so many metres, with their values; drawn when the project is tied to the map (Manage → Location). Empty: a tenth of the scale (100 mm on paper); 0: none">Coordinate grid</dt>
      <dd>
        {/* The map's metres, whatever the project's unit: the grid is the map's. */}
        <NumberField value={step} min={0} blank suffix="m" placeholder={String(scale / 10)} onCommit={(v) => set({ gridStep: v })} />
      </dd>
      <dt title="The project's buildings filled in red and the parcel outlined in red, so the site stands out from its neighbours: on by default in a location plan">Highlight the project</dt>
      <dd><input type="checkbox" checked={typeof view.props.highlight === 'boolean' ? view.props.highlight : view.props.site === 'location'} onChange={(e) => set({ highlight: e.target.checked })} /></dd>
      <dt title="At the top right by itself, pointing to true north (Manage → Location: the angle to true north); select it to move it by its grip, size it or choose its symbol">North arrow</dt>
      <dd><input type="checkbox" data-site-north checked={has('NORTHARROW')} onChange={(e) => set({ northArrow: e.target.checked })} /></dd>
      <dt title="At the bottom left by itself, about 60 mm on paper, with the scale; select it to move it by its grip or change its length">Scale bar</dt>
      <dd><input type="checkbox" data-site-scalebar checked={has('SCALEBAR')} onChange={(e) => set({ scaleBar: e.target.checked })} /></dd>
    </>
  );
}
