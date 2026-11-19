/** The rebar set tools' bar: layer, direction, diameter and spacing a new set is made with (decision 730). */

import { useRebarCatalog } from './RebarSection';
import { diameterChoices, useRebarRules } from './rebarRules';
import { LengthField } from '../../shell/cells';
import { PartitionOptions } from './partitions';
import { SET_LAYERS, useSetOptions, type SetOptions } from './setOptions';

export function SetToolOptions() {
  const o = useSetOptions();
  const catalog = useRebarCatalog();
  const rules = useRebarRules();
  const put = (patch: Partial<SetOptions>) => useSetOptions.setState(patch);
  return (
    <>
      <span className="cad-layer on">
        <span className="small muted">Layer</span>
        <select value={o.layer} data-field="rebar-set-tool-layer" onChange={(e) => put({ layer: e.target.value })}>
          <option value="">Default</option>
          {SET_LAYERS.map((l) => <option key={l} value={l} data-no-i18n>{l}</option>)}
        </select>
      </span>
      <span className="cad-layer on">
        <span className="small muted">Direction</span>
        <select value={o.direction} data-field="rebar-set-tool-direction" onChange={(e) => put({ direction: e.target.value as SetOptions['direction'] })}>
          <option value="">Default</option><option value="x" data-no-i18n>x</option><option value="y" data-no-i18n>y</option><option value="horizontal">Horizontal</option><option value="vertical">Vertical</option>
        </select>
      </span>
      <span className="cad-layer on">
        <span className="small muted">Diameter</span>
        <select value={o.diameter} data-field="rebar-set-tool-diameter" onChange={(e) => put({ diameter: Number(e.target.value) })}>
          <option value={0}>Default</option>
          {diameterChoices(rules, catalog?.diameters ?? [], o.diameter || null).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
        </select>
      </span>
      <span className="cad-layer on">
        <span className="small muted">Spacing</span>
        <LengthField style={{ width: 64 }} metres={o.step || null} min={0.03} max={2} blank placeholder="default" onCommit={(m) => put({ step: m ?? 0 })} />
      </span>
      <PartitionOptions />
    </>
  );
}
