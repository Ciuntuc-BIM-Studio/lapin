/**
 * The model configurator (docs/plan-furniture.md, Configuratorul flexibil, stage 2; decision 477): a gallery of the CC0
 * models by group (their previews fetched when shown), the size within the model's range — the engine's preview beside
 * it shows the stretch as it is typed —, the height it hangs at, the looks and a colour per role. Every change is one
 * step on the type (furniture.updateType); a new model brings its own size and roles.
 */

import { useSession } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import { ROLE_LABELS, type FurnitureSpec } from './furniture';
import { byGroup, isLook, lookPatch, thumbnailUrl, zonedAxes, type ModelsCatalog } from './models';

const mm = (m: number) => Math.round(m * 1000);
/** The register's sources (furniture-assets.json), as the dialog credits them. */
const SOURCES: Record<string, string> = { 'kenney-furniture-kit': 'Kenney, Furniture Kit', 'poly-haven': 'Poly Haven' };

export function useModelsCatalog(): ModelsCatalog | undefined {
  return useSession((s) => s.info?.catalogs['furniture.models']) as unknown as ModelsCatalog | undefined;
}

/** A gallery of the models, the one chosen marked; picking one calls <code>onPick</code>. */
export function ModelGallery({ value, onPick }: { value: string; onPick: (id: string) => void }) {
  const cat = useModelsCatalog();
  if (!cat?.models) return null;
  return (
    <div className="model-gallery" data-testid="furniture-model-gallery">
      {byGroup(cat.models).map(([group, models]) => (
        <div key={group} className="model-gallery-group">
          <p className="eyebrow">{group}</p>
          <div className="model-gallery-items">
            {models.map((m) => (
              <button key={m.id} type="button" className={`model-tile${m.id === value ? ' on' : ''}`} data-model={m.id} title={m.name} onClick={() => onPick(m.id)}>
                <img src={thumbnailUrl(m)} alt="" loading="lazy" width={56} height={56} />
                <span className="small">{m.name}</span>
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

/** A type of a model's configurator; <code>update</code> sends the spec's patch. */
export function ModelEditor({ spec, update }: { spec: FurnitureSpec; update: (patch: Record<string, unknown>) => void }) {
  const cat = useModelsCatalog();
  const nodes = useSession((s) => s.nodes);
  if (!cat?.models?.length) return null;
  const model = cat.models.find((m) => m.id === spec.model) ?? cat.models[0];
  const colorOf = (name: string, fallback?: string | null) =>
    String([...nodes.values()].find((n) => n.type === 'IfcMaterial' && n.name === name)?.props.color ?? fallback ?? '#999999');
  const axes = zonedAxes(model);
  const keys = ['width', 'depth', 'height'] as const;
  return (
    <div className="model-editor" data-testid="furniture-model-editor">
      <ModelGallery value={model.id} onPick={(id) => { if (id !== model.id) update({ model: id }); }} />
      <dl className="fields">
        <dt>Model</dt><dd data-no-i18n>{model.name}</dd>
        {keys.map((k, i) => (
          <div key={k} style={{ display: 'contents' }}>
            <dt>{['Width', 'Depth', 'Height'][i]}</dt>
            <dd title={`${mm(model.min[i])}–${mm(model.max[i])} mm`}>
              <LengthField testId={`furniture-model-${k}`} metres={Number(spec[k] ?? model.size[i])} min={model.min[i]} max={model.max[i]} onCommit={(v) => { if (v != null) update({ [k]: v }); }} />
            </dd>
          </div>
        ))}
        {(model.mount > 0 || Number(spec.mount ?? 0) > 0) && <>
          <dt>Hung at (its underside)</dt>
          <dd><LengthField testId="furniture-model-mount" metres={Number(spec.mount ?? model.mount)} min={0} max={3} onCommit={(v) => { if (v != null) update({ mount: v }); }} /></dd>
        </>}
      </dl>
      <p className="muted small">
        {axes.length
          ? <><span>Stretched by zones: the ends keep their size, the middle stretches.</span>{axes.map((a) => <span key={a}> · <span>{a}</span></span>)}</>
          : <span>Scaled whole.</span>}
      </p>
      {!!cat.looks?.length && (
        <div className="model-looks">
          <span className="eyebrow">Looks</span>
          {cat.looks.map((l) => (
            <button key={l.key} type="button" className={`btn small${isLook(l, model.roles, spec.materials) ? ' on' : ''}`} data-look={l.key}
              onClick={() => update({ materials: lookPatch(l, model.roles) })}>{l.label}</button>
          ))}
        </div>
      )}
      <div className="model-swatches">
        {model.roles.map((role) => (
          <div key={role} className="model-swatch-row" data-swatch-role={role}>
            <span className="muted small">{ROLE_LABELS[role] ?? role}</span>
            {(cat.swatches?.[role] ?? []).map((s) => (
              <button key={s.name} type="button" className={`model-swatch${spec.materials?.[role] === s.name ? ' on' : ''}`} title={s.name} data-swatch={s.name}
                style={{ background: colorOf(s.name, s.color) }} onClick={() => update({ materials: { [role]: s.name } })} />
            ))}
          </div>
        ))}
      </div>
      <p className="muted small" data-no-i18n>{model.licence} · {SOURCES[model.source] ?? model.source}</p>
    </div>
  );
}
