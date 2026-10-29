/**
 * The site's drawings (docs/site-plans.md): a site plan (1:500) and a
 * location plan (1:2000), plan views the engine draws as the whole model from
 * above, with a north arrow, the map's coordinate grid and a scale bar. In a
 * site view's settings: what it is, the grid's step, whether the arrow and
 * the bar are drawn, and whether the project stands out in red.
 */

import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';

export type SitePurpose = 'situation' | 'location';

export const isSiteView = (n: NodeDto | undefined | null): boolean => n?.type === 'LapinView' && n.props.kind === 'plan' && typeof n.props.site === 'string';

export const SITE_PURPOSES: [SitePurpose, string][] = [['situation', 'Site plan'], ['location', 'Location plan']];

/** A new site view of that purpose (its scale and name the engine's defaults), on the site's storey; its id. */
export async function createSiteView(purpose: SitePurpose): Promise<string | null> {
  const r = (await execute('view.create', { kind: 'plan', site: purpose })) as { id?: string } | null;
  return r?.id ?? null;
}

/** The site view's own fields, in its view settings. */
export function SitePlanFields({ view }: { view: NodeDto }) {
  const set = (patch: Record<string, unknown>) => void execute('view.setSite', { id: view.id, ...patch });
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
      <dt title="Crosses where the map's eastings and northings meet, every so many metres, with their values; drawn when the project is tied to the map (Manage → Location). Empty: a tenth of the scale (100 mm on paper); 0: none">Coordinate grid (m)</dt>
      <dd>
        <input key={`${view.id}:${step}`} className="cell" type="number" min={0} defaultValue={step == null ? '' : String(step)} placeholder={String(scale / 10)} aria-label="Coordinate grid step"
          onBlur={(e) => { const v = e.target.value.trim(); if (v !== (step == null ? '' : String(step))) set({ gridStep: v === '' ? null : Number(v) }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }} />
      </dd>
      <dt title="The project's buildings filled in red and the parcel outlined in red, so the site stands out from its neighbours: on by default in a location plan">Highlight the project</dt>
      <dd><input type="checkbox" checked={typeof view.props.highlight === 'boolean' ? view.props.highlight : view.props.site === 'location'} onChange={(e) => set({ highlight: e.target.checked })} /></dd>
      <dt title="Drawn at the top right, pointing to true north (Manage → Location: the angle to true north)">North arrow</dt>
      <dd><input type="checkbox" checked={view.props.northArrow !== false} onChange={(e) => set({ northArrow: e.target.checked })} /></dd>
      <dt title="Drawn at the bottom left, about 60 mm on paper, with the scale">Scale bar</dt>
      <dd><input type="checkbox" checked={view.props.scaleBar !== false} onChange={(e) => set({ scaleBar: e.target.checked })} /></dd>
    </>
  );
}
