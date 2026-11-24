/**
 * Open-web joists (decision 827): the tool's properties (the joist type to place with, a new one from a preset) and a
 * joist's section in the properties panel (its type and a line about it, the type opened to edit, the webs regenerated
 * when the file's own were kept). The type's rules are its fields, edited by the generic panel.
 */

import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { sameKind } from '../../shell/params';

export const JOIST_TYPE = 'LapinSteelJoistType';

interface Preset { name: string; spec?: Record<string, unknown> }

const num = (v: unknown): number => (typeof v === 'number' ? v : Number(v) || 0);
const mmText = (m: unknown) => String(Math.round(num(m) * 1000 * 10) / 10);

/** The chords and webs of a joist type in a few words, as the plan and the guide name them. */
export function joistAbout(type: NodeDto | null | undefined): { depth: string; chords: string; webs: string } | null {
  if (!type) return null;
  const p = type.props;
  const chord = (leg: unknown, h: unknown, t: unknown) => `2L ${mmText(leg)}×${mmText(h)}×${mmText(t)}`;
  return {
    depth: `${mmText(p.depth)} mm`,
    chords: `${chord(p.topLeg, p.topHeight, p.topThickness)} / ${chord(p.bottomLeg, p.bottomHeight, p.bottomThickness)}`,
    webs: p.webs === 'angle' ? 'Angle webs' : p.webs === 'lines' ? 'Lines only' : 'Round bar webs',
  };
}

const Summary = ({ type }: { type: NodeDto | null | undefined }) => {
  const a = joistAbout(type);
  if (!a) return null;
  return (
    <dl className="fields" data-field="joist-summary">
      <dt>Joist depth</dt><dd data-no-i18n>{a.depth}</dd>
      <dt>Chords</dt><dd data-no-i18n>{a.chords}</dd>
      <dt>Webs</dt><dd>{a.webs}</dd>
    </dl>
  );
};

/** The Open-web joist tool's properties: the type placed with, and a new type made from a preset (indicative sizes). */
export function JoistToolSection() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const presets = useSession((s) => (s.info?.catalogs['steel.joistPresets'] as unknown as Preset[] | undefined) ?? []);
  const chosen = useUi((s) => s.joistTypeId);
  const types = ofClass({ nodes, relations }, JOIST_TYPE);
  const current = types.find((t) => t.id === chosen) ?? types[0];
  const make = async (preset: string) => {
    if (!preset) return;
    const r = (await execute('steel.createJoistType', { preset })) as { id?: string } | null;
    if (r?.id) useUi.setState({ joistTypeId: r.id });
  };
  return (
    <div className="pad new-element" data-field="new-element">
      <label className="field-line"><span className="eyebrow">Type</span>
        {types.length
          ? <select value={current?.id ?? ''} data-field="joist-type" onChange={(e) => useUi.setState({ joistTypeId: e.target.value })}>
              {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
            </select>
          : <span className="muted small">The first one is made as you place it.</span>}
      </label>
      <Summary type={current} />
      {presets.length > 0 && (
        <label className="field-line"><span className="eyebrow">New type from a preset</span>
          <select value="" data-field="joist-preset" onChange={(e) => void make(e.target.value)}>
            <option value="">—</option>
            {presets.map((p) => <option key={p.name} value={p.name} data-no-i18n>{p.name}</option>)}
          </select>
        </label>
      )}
      <p className="muted small">The preset sizes are indicative: your own norms and sizes decide.</p>
    </div>
  );
}

/** A joist in the properties panel. */
export function JoistSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const model = { nodes, relations };
  const type = typeOf(model, node.id);
  const types = ofClass(model, JOIST_TYPE);
  const ro = !node.editable;
  return (
    <div className="layers" data-field="joist-section">
      <label className="type-pick">
        <span className="eyebrow">Type</span>
        <span className="row">
          <select value={type?.id ?? ''} disabled={ro} data-field="joist-type" onChange={(e) => void execute('steel.setJoistType', { ids: sameKind(node), typeId: e.target.value })}>
            {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
          {type && <button type="button" className="btn" data-field="joist-edit-type" onClick={() => useUi.setState({ selection: type.id, selected: [type.id] })}>Edit the type</button>}
        </span>
      </label>
      <Summary type={type} />
      {Array.isArray(node.props.webAxes) && (
        <div className="small" data-field="joist-web-axes">
          <p className="muted">Webs as the imported file drew them</p>
          <button type="button" className="btn" data-field="joist-reset-webs" disabled={ro} onClick={() => void execute('steel.updateJoist', { id: node.id, resetWebs: true })}>Regenerate webs</button>
        </div>
      )}
    </div>
  );
}
