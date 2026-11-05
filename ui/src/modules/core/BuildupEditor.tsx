/**
 * Drafting standards → Tags: a build-up tag type's editor (docs/plan-buildup-tags.md, stage 2). Its header, the line
 * each layer writes (fields picked from the layer's, the materials' parameters found in the project and the element's;
 * [optional parts] dropped where a field in them is empty), its footer, the layers' order, layers of no thickness, its
 * leaders, bullets, the width its lines wrap at and its unit; beside them the engine's own layout of it on a sample
 * build-up (a wall or floor type of the project), so what is seen is what views and sheets draw.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { execute, query, useSession } from '../../engine/session';
import {
  balanced, BUILDUP_DEFAULTS, ELEMENT_FIELDS, insertField, LAYER_FIELDS, materialFields, orderLabels, parseWidth, wrapOptional, type BuildupEntry,
} from './buildupTag';

interface Preview {
  sample?: { id: string; name: string; class: string };
  stack?: boolean; width: number; height: number; textHeight?: number; bold?: boolean;
  texts: { x: number; y: number; text: string; role: string; entry: number }[];
  mids?: number[]; layers?: { index: number; thickness: number; color: string; name: string }[]; entries?: number[];
}

const BULLETS: [string, string][] = [['bullet', '• bullet'], ['dash', '– dash'], ['number', '1. number'], ['none', 'nothing']];

/** The sample's layers and the tag laid out as the engine lays it, with its leaders sketched to a strip of the layers. */
function PreviewSvg({ p, perLayer }: { p: Preview; perLayer: boolean }) {
  const h = p.textHeight ?? 2.5;
  const w = Math.max(p.width, 20), H = Math.max(p.height, h * 2);
  const layers = p.layers ?? [];
  const total = layers.reduce((s, l) => s + l.thickness, 0) || 1;
  const stripX = w + 14, stripW = 9;
  // Each layer's band down the strip, as thick as it is (a layer of no thickness a line).
  let at = 0;
  const bands = layers.map((l) => { const y0 = at; at += (l.thickness / total) * H; return { ...l, y0, y1: at }; });
  const mids = p.mids ?? [];
  const leaders = perLayer
    ? (p.entries ?? []).map((layer, i) => { const b = bands.find((x) => x.index === layer); return b && mids[i] != null ? { y: mids[i], to: (b.y0 + b.y1) / 2 } : null; }).filter((x): x is { y: number; to: number } => !!x)
    : [{ y: H / 2, to: H / 2 }];
  const fs = h / 0.718;
  return (
    <svg className="buildup-preview-svg" viewBox={`-3 -3 ${stripX + stripW + 6} ${H + 6}`} data-field="tag-preview" role="img" aria-label="Preview">
      <rect x={-3} y={-3} width={stripX + stripW + 6} height={H + 6} className="paper" />
      {bands.map((b) => b.y1 - b.y0 > 0.05
        ? <rect key={b.index} x={stripX} y={b.y0} width={stripW} height={b.y1 - b.y0} fill={b.color} stroke="#222" strokeWidth={0.15} />
        : <line key={b.index} x1={stripX} x2={stripX + stripW} y1={b.y0} y2={b.y0} stroke="#222" strokeWidth={0.35} />)}
      {leaders.map((l, i) => (
        <g key={i} stroke="#111" strokeWidth={0.18} fill="none">
          <polyline points={`${w + h * 0.8},${l.y} ${w + h * 0.8 + 2.5},${l.y} ${stripX + stripW / 2},${l.to}`} />
          <circle cx={stripX + stripW / 2} cy={l.to} r={h / 5} fill="#111" />
        </g>
      ))}
      {p.texts.map((t, i) => (
        <text key={i} x={t.x} y={t.y} fontSize={fs} fontFamily="Helvetica, Arial, sans-serif" fontWeight={p.bold ? 700 : 400} fill="#111" data-no-i18n>{t.text}</text>
      ))}
    </svg>
  );
}

export function BuildupEditor({ entry, onClose }: { entry: BuildupEntry; onClose: () => void }) {
  const nodes = useSession((s) => s.nodes);
  const [draft, setDraft] = useState<BuildupEntry>(entry);
  const [sample, setSample] = useState('');
  const [preview, setPreview] = useState<Preview | null>(null);
  const line = useRef<HTMLInputElement>(null);
  useEffect(() => { setDraft(entry); }, [entry]);
  const types = useMemo(() => [...nodes.values()].filter((n) => n.type === 'IfcWallType' || n.type === 'IfcSlabType'), [nodes]);
  const params = useMemo(() => materialFields([...nodes.values()].filter((n) => n.type === 'IfcMaterial').map((n) => ({ params: n.props.params }))), [nodes]);
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => {
      void query<Preview>('drafting.buildupPreview', { entry: draft, ...(sample ? { type: sample } : {}) }).then((r) => { if (live && r) setPreview(r); });
    }, 120);
    return () => { live = false; clearTimeout(t); };
  }, [draft, sample, nodes]);

  const commit = (patch: Partial<BuildupEntry>) => {
    setDraft((d) => ({ ...d, ...patch }));
    void execute('standards.set', { kind: 'tag', entry: { id: entry.id, ...patch } });
  };
  const template = draft.template ?? BUILDUP_DEFAULTS.template;
  const edit = (f: (t: string, s: number, e: number) => { text: string; caret: number }) => {
    const el = line.current;
    const { text, caret } = f(template, el?.selectionStart ?? template.length, el?.selectionEnd ?? template.length);
    commit({ template: text });
    requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(caret, caret); });
  };
  const stack = preview?.stack ?? false;
  const ok = balanced(template);
  return (
    <section className="buildup-editor" data-buildup-editor={entry.id}>
      <header className="row"><strong data-no-i18n>{draft.name}</strong><span className="grow" /><button type="button" className="btn mini" onClick={onClose}>Close</button></header>
      <div className="buildup-editor-body">
        <dl className="fields buildup-fields">
          <dt title="Written above the layers, | starts a new line (empty: none)">Header text</dt>
          <dd><input data-field="tag-header" value={draft.header ?? ''} onChange={(e) => setDraft({ ...draft, header: e.target.value })} onBlur={(e) => commit({ header: e.target.value })} /></dd>
          <dt title="Written once for each layer: {fields} read the layer, its material and the element; what is in [square brackets] goes when a field in it is empty; \[ writes a bracket">Each layer</dt>
          <dd>
            <input ref={line} data-field="tag-line" className={ok ? undefined : 'invalid'} value={template} onChange={(e) => setDraft({ ...draft, template: e.target.value })} onBlur={(e) => commit({ template: e.target.value })} />
            <div className="row buildup-picker">
              <select data-field="tag-line-field" value="" title="Put a field where the cursor is" onChange={(e) => { const f = e.target.value; if (f) edit((t, s, x) => insertField(t, s, x, f)); }}>
                <option value="">Insert a field…</option>
                <optgroup label="Layer">{LAYER_FIELDS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</optgroup>
                {params.length > 0 && <optgroup label="Material parameters">{params.map((k) => <option key={k} value={k} data-no-i18n>{k}</option>)}</optgroup>}
                <optgroup label="Element">{ELEMENT_FIELDS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</optgroup>
              </select>
              <button type="button" className="btn mini" data-field="tag-line-optional" title="Make the selection an optional part: dropped where a field in it is empty" onClick={() => edit((t, s, x) => wrapOptional(t, s, x))}>[ ]</button>
            </div>
            {!ok && <span className="error-text small">A bracket is not closed.</span>}
          </dd>
          <dt title="Written under the layers (empty: none)">Footer</dt>
          <dd><input data-field="tag-footer" value={draft.footer ?? ''} onChange={(e) => setDraft({ ...draft, footer: e.target.value })} onBlur={(e) => commit({ footer: e.target.value })} /></dd>
          <dt title="As the element stands in the model: a wall from its exterior (its first layer's side), a floor or a roof from its top">Order</dt>
          <dd><select data-field="tag-order" value={draft.order ?? 'out-in'} onChange={(e) => commit({ order: e.target.value })}>
            {orderLabels(stack).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select></dd>
          <dt title="Membranes, foils, paint: layers modelled without a thickness">Layers of no thickness</dt>
          <dd><label className="check"><input type="checkbox" data-field="tag-zero-layers" checked={draft.zeroLayers !== false} onChange={(e) => commit({ zeroLayers: e.target.checked })} />shown</label></dd>
          <dt title="One leader to the element, or one per layer, each ending inside its own layer where the views can tell them apart (walls in plans and sections, floors in sections)">Leaders</dt>
          <dd><select data-field="tag-leaders" value={draft.leaders ?? 'one'} onChange={(e) => commit({ leaders: e.target.value })}>
            <option value="one">one, to the element</option><option value="per-layer">one per layer</option>
          </select></dd>
          <dt>Lines start with</dt>
          <dd><select data-field="tag-bullet" value={draft.bullet ?? 'bullet'} onChange={(e) => commit({ bullet: e.target.value })}>
            {BULLETS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select></dd>
          <dt title="The lines wrap at this width on paper, the text hanging after its bullet">Width mm</dt>
          <dd><input className="num" data-field="tag-width" key={`w${draft.width ?? ''}`} defaultValue={String(draft.width ?? BUILDUP_DEFAULTS.width)}
            onBlur={(e) => { const n = parseWidth(e.target.value); if (n != null && n !== draft.width) commit({ width: n }); }} /></dd>
          <dt title="The unit {thickness} is written in">Thickness in</dt>
          <dd><select data-field="tag-unit" value={draft.unit === 'cm' ? 'cm' : draft.unit === 'm' ? 'm' : 'mm'} onChange={(e) => commit({ unit: e.target.value })}>
            <option value="mm">mm</option><option value="cm">cm</option><option value="m">m</option>
          </select></dd>
          <dt>Preview on</dt>
          <dd><select data-field="tag-sample" value={sample || preview?.sample?.id || ''} onChange={(e) => setSample(e.target.value)}>
            {types.length === 0 && <option value="">no layered type yet</option>}
            {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select></dd>
        </dl>
        <figure className="buildup-preview">
          {preview && preview.texts.length > 0 ? <PreviewSvg p={preview} perLayer={draft.leaders === 'per-layer'} /> : <p className="muted small">The project has no wall or floor type with layers yet.</p>}
        </figure>
      </div>
    </section>
  );
}
