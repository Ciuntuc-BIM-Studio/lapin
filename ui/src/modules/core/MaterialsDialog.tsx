/**
 * Materials: every IfcMaterial of the project, an editor of how it renders and
 * how it draws (report #76). The list searches and filters by category, adds
 * a material by name or from the library (materialLibrary.ts), duplicates,
 * selects the elements made of one, replaces one by another everywhere (one
 * undo step) and deletes. Appearance: a live preview, the colour, the texture
 * (drawn, or the user's image: OwnImage) with its size in metres and its turn,
 * roughness, metalness, transparency, glass (transmission and the index of
 * refraction) and a glow; what is not set comes from the name. 2D graphics:
 * how it draws where a section or plan cuts it and in elevation (hatch, scale,
 * fill and pen, or a membrane line), as before. Hatch patterns are imported
 * from .pat files and travel with the project. Every 3D export names its
 * materials after these, one each, with these values. Data (MaterialDataTab): its
 * description, parameters, measure and catalogue of thicknesses.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, FileUp, Library, MousePointerClick, Plus, Replace, RotateCcw, Search, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { MaterialGraphicsTable, type HatchPattern as Hatch, type MaterialRow as Material } from './MaterialGraphicsTable';
import { OwnImage } from './OwnImage';
import { MaterialPreview } from './MaterialPreview';
import { forgetPatterns, keepPatterns, myPatterns } from './myPatterns';
import { FirmFolderFiles } from './FirmFolderFiles';
import { forgetHatchPreviews } from './hatchCache';
import { freeName, matches, PRESET_GROUPS, PRESETS, presetArgs, type Preset } from './materialLibrary';
import { lookOf, LOOK_RANGES, type Look } from '../../viewport/look';
import { LengthField } from '../../shell/cells';
import { MaterialDataTab } from './MaterialDataTab';

/** IfcMaterial.Category as the interface offers it (Materials.Categories in the engine). */
const MATERIAL_CATEGORIES = ['Structure', 'Finish', 'Insulation', 'Membrane', 'Other'];

const TEXTURES = ['none', 'brick', 'brick-end', 'concrete', 'wood', 'wood-end', 'plaster', 'stone', 'metal', 'insulation', 'earth', 'tiles'];
type LookKey = Exclude<keyof Look, 'emissive'>;

/** The look's fields the editor offers as sliders, with their labels, steps and what they mean. */
const SLIDERS: { key: LookKey; label: string; step: number; title: string }[] = [
  { key: 'roughness', label: 'Roughness', step: 0.01, title: '0: a mirror; 1: chalk. Glass and polished stone are low, plaster and concrete high.' },
  { key: 'metalness', label: 'Metalness', step: 0.01, title: '1 for bare metal (anodised, steel, chrome); 0 for everything painted, powder-coated, wood, stone.' },
  { key: 'transparency', label: 'Transparency', step: 0.01, title: 'How much shows through: 0 opaque, 1 invisible. Renderers take it as the opacity.' },
  { key: 'transmission', label: 'Glass (transmission)', step: 0.01, title: 'Light through the body, as glass and water let it: renderers refract it.' },
];
/** Every look field, to take them all back to the name's. */
const LOOK_KEYS = ['roughness', 'metalness', 'transparency', 'transmission', 'ior', 'emissive', 'emissiveStrength', 'textureRotation'];

export function MaterialsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [list, setList] = useState<Material[]>([]);
  const [hatches, setHatches] = useState<Hatch[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [mine, setMine] = useState(myPatterns);
  const [newName, setNewName] = useState('');
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [tab, setTab] = useState<'appearance' | 'graphics' | 'data'>('appearance');
  const [library, setLibrary] = useState(false);
  const [replaceWith, setReplaceWith] = useState('');
  /** A slider being dragged: its value shows in the preview, and goes to the engine when let go. */
  const [draft, setDraft] = useState<Partial<Record<LookKey, number>>>({});
  // Every edit comes back as a diff: read the materials again.
  useEffect(() => {
    void query<Material[]>('material.list').then((r) => { if (r) { setList(r); setSelected((cur) => cur ?? r[0]?.id ?? null); } });
    void query<Hatch[]>('hatch.list').then((r) => r && setHatches(r));
  }, [nodes]);
  useEffect(() => { setDraft({}); setReplaceWith(''); }, [selected]);
  const m = list.find((x) => x.id === selected) ?? null;
  const node = m ? nodes.get(m.id) : undefined;
  const shown = list.filter((x) => matches(x.name, search) && (!category || (category === '-' ? !x.category : x.category === category)));
  const update = (args: Record<string, unknown>) => m && void execute('material.update', { id: m.id, ...args });
  // The material as the preview shows it: with the slider being dragged.
  const previewNode: NodeDto | undefined = useMemo(() => node && { ...node, props: { ...node.props, ...draft } }, [node, draft]);
  const look = lookOf(previewNode);
  const commit = (key: LookKey) => {
    const v = draft[key];
    if (v === undefined || !m) return;
    void execute('material.update', { id: m.id, [key]: v }).then(() => setDraft((d) => { const n = { ...d }; delete n[key]; return n; }));
  };

  const create = (args: Record<string, unknown>) => void execute('material.create', args).then((r) => {
    const id = (r as { id?: string } | null)?.id;
    if (id) { setSelected(id); setNewName(''); setLibrary(false); setTab('appearance'); }
  });
  const addPreset = (p: Preset) => {
    const have = list.find((x) => x.name.toLowerCase() === p.name.toLowerCase());
    if (have) { setSelected(have.id); setLibrary(false); notify('That material is already in the project: it is selected.'); return; }
    create(presetArgs(p, freeName(p.name, list.map((x) => x.name))));
  };

  const importPatFile = async (name: string, text: string) => {
    const r = await execute('hatch.import', { text }) as { count: number; errors: string[] } | null;
    if (r) {
      forgetHatchPreviews();
      keepPatterns(name, text);
      setMine(myPatterns());
      notify(`${r.count} hatch pattern${r.count === 1 ? '' : 's'} loaded${r.errors.length ? `, ${r.errors.length} line(s) skipped` : ''}, and kept in My patterns on this computer.`);
    }
  };

  const importPat = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.pat,text/plain';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      await importPatFile(file.name, await file.text());
    };
    input.click();
  };

  const selectUses = async () => {
    if (!m) return;
    const ids = await query<string[]>('material.uses', { id: m.id }) ?? [];
    useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
    notify(ids.length ? 'The elements made of this material are selected.' : 'No element is made of this material.');
  };
  const replace = (andDelete: boolean) => {
    if (!m || !replaceWith) return;
    void execute('material.replace', { id: m.id, with: replaceWith, delete: andDelete }).then((r) => {
      if (!r) return;
      notify(andDelete ? 'The material is replaced everywhere, and deleted.' : 'The material is replaced everywhere.');
      if (andDelete) setSelected(replaceWith);
    });
  };

  const textureValue = String(node?.props.texture ?? '');
  const ownImage = typeof node?.props.textureImage === 'string';
  const size = Array.isArray(node?.props.textureSize) ? (node!.props.textureSize as number[]) : null;
  const ownLook = (key: string) => (node ? key in node.props : false);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Materials">
      <div className="dialog wide materials-dialog">
        <header className="dialog-head">
          <span className="panel-title">Materials</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div className="material-side">
            <label className="row material-search"><Search size={13} /><input data-field="material-search" value={search} placeholder="Search materials" onChange={(e) => setSearch(e.target.value)} /></label>
            <select data-field="material-category" value={category} title="Show the materials of one category" onChange={(e) => setCategory(e.target.value)}>
              <option value="">All categories</option>
              {MATERIAL_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
              <option value="-">Without a category</option>
            </select>
            <ul className="type-list material-list">
              {shown.map((x) => (
                <li key={x.id}>
                  <button type="button" className={`tree-item material-item${selected === x.id && !library ? ' on' : ''}`} data-material={x.name} onClick={() => { setSelected(x.id); setLibrary(false); }}>
                    <span className="swatch" style={{ background: x.color }} /><span data-no-i18n>{x.name}</span>{!x.inUse && <span className="muted small"> · unused</span>}
                  </button>
                </li>
              ))}
              {shown.length === 0 && <li className="muted small">No material matches.</li>}
            </ul>
            <div className="row">
              <input value={newName} placeholder="New material" onChange={(e) => setNewName(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter' && newName.trim()) create({ name: newName.trim() }); }} />
              <button type="button" className="icon" title="Add" disabled={!newName.trim()} onClick={() => create({ name: newName.trim() })}><Plus size={14} /></button>
            </div>
            <button type="button" className={`btn${library ? ' on' : ''}`} data-action="material-library" onClick={() => setLibrary((v) => !v)}><Library size={14} />From the library…</button>
            <button type="button" className="btn" onClick={importPat}><FileUp size={14} />Import .pat</button>
            <FirmFolderFiles ext="pat" onPick={(f) => importPatFile(f.name, new TextDecoder().decode(f.bytes))}
              save={{ disabled: mine.length === 0, get: async () => ({ text: mine.map((f) => f.text).join('\n'), name: 'My patterns' }) }} />
            {mine.length > 0 && (
              <div className="my-patterns">
                <span className="eyebrow" title="The .pat files you imported, kept on this computer for any project">My patterns</span>
                <ul className="plain">
                  {mine.map((f) => (
                    <li key={f.name} className="row">
                      <span className="grow small" title={f.names.join(', ')}>{f.name} <span className="muted">· {f.names.length}</span></span>
                      <button type="button" className="icon mini" title="Forget this file (projects keep what they use)" onClick={() => { forgetPatterns(f.name); setMine(myPatterns()); }}><Trash2 size={12} /></button>
                    </li>
                  ))}
                </ul>
                <button type="button" className="btn compact" onClick={() => void execute('hatch.import', { text: mine.map((f) => f.text).join('\n') }).then((r) => {
                  forgetHatchPreviews();
                  const n = (r as { count?: number } | null)?.count ?? 0;
                  notify(`${n} pattern${n === 1 ? '' : 's'} from My patterns in this project.`);
                })}>Add to this project</button>
              </div>
            )}
          </div>
          {library && (
            <div className="material-library" data-testid="material-library">
              <p className="muted small">Ready-made materials with their colour, roughness, metalness and glass. A click adds one to the project; change it as any other.</p>
              {PRESET_GROUPS.map((g) => {
                const items = PRESETS.filter((p) => p.group === g && matches(p.name, search));
                if (!items.length) return null;
                return (
                  <section key={g}>
                    <span className="eyebrow">{g}</span>
                    <div className="preset-grid">
                      {items.map((p) => (
                        <button key={p.name} type="button" className="preset" data-preset={p.name} title={p.name} onClick={() => addPreset(p)}>
                          <span className="preset-ball" style={{ background: ball(p.color, p.metalness ?? 0, p.roughness, p.transparency ?? 0) }} />
                          <span className="small" data-no-i18n>{p.name}</span>
                        </button>
                      ))}
                    </div>
                  </section>
                );
              })}
            </div>
          )}
          {m && !library && (
            <div className="type-edit">
              <div className="row">
                <label className="field-line grow"><span className="eyebrow">Name</span>
                  <input key={m.id + m.name} data-field="material-name" defaultValue={m.name} onBlur={(e) => e.target.value.trim() && e.target.value.trim() !== m.name && update({ name: e.target.value.trim() })} /></label>
                <label className="field-line" title="What the material is in a layered element (IfcMaterial.Category). A view style without finishes leaves out the layers of finish materials; without a category, plaster, render, paint, tiles, boards and floor coverings count as finishes."><span className="eyebrow">Category</span>
                  <select value={String(node?.props.category ?? '')} onChange={(e) => update({ category: e.target.value || null })}>
                    <option value="">by its name</option>
                    {MATERIAL_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                  </select></label>
              </div>
              <div className="row material-actions">
                <button type="button" className="btn compact" data-action="material-duplicate" onClick={() => void execute('material.duplicate', { id: m.id }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) setSelected(id); })}><Copy size={13} />Duplicate</button>
                <button type="button" className="btn compact" data-action="material-select" title="Select the elements with a part in this material" onClick={() => void selectUses()}><MousePointerClick size={13} />Select elements</button>
                <span className="grow" />
                <button type="button" className="btn compact" disabled={m.inUse} title={m.inUse ? 'In use: replace it with another first' : 'Delete'} onClick={() => void execute('material.delete', { id: m.id }).then(() => setSelected(null))}><Trash2 size={13} />Delete</button>
              </div>
              <div className="row material-replace">
                <span className="small">Replace with</span>
                <select data-field="material-replace" value={replaceWith} onChange={(e) => setReplaceWith(e.target.value)}>
                  <option value="">—</option>
                  {list.filter((x) => x.id !== m.id).map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}
                </select>
                <button type="button" className="btn compact" disabled={!replaceWith} title="Every layer, type, door, window and element that uses this material uses the other: one undo step" onClick={() => replace(false)}><Replace size={13} />Replace</button>
                <button type="button" className="btn compact" disabled={!replaceWith} onClick={() => replace(true)}>Replace and delete</button>
              </div>
              <div className="tabs-inline" role="tablist">
                <button type="button" role="tab" aria-selected={tab === 'appearance'} className={tab === 'appearance' ? 'on' : ''} data-tab="appearance" onClick={() => setTab('appearance')}>Appearance</button>
                <button type="button" role="tab" aria-selected={tab === 'graphics'} className={tab === 'graphics' ? 'on' : ''} data-tab="graphics" onClick={() => setTab('graphics')}>2D graphics</button>
                <button type="button" role="tab" aria-selected={tab === 'data'} className={tab === 'data' ? 'on' : ''} data-tab="data" data-field="mat-data-tab" onClick={() => setTab('data')}>Data</button>
              </div>
              {tab === 'data' && node && <MaterialDataTab node={node} />}
              {tab === 'appearance' && previewNode && (
                <div className="material-look" data-testid="material-look">
                  <MaterialPreview material={previewNode} />
                  <div className="material-look-fields">
                    <div className="row">
                      <label className="field-line"><span className="eyebrow">Colour</span>
                        <input type="color" data-field="material-color" value={m.color} onChange={(e) => update({ color: e.target.value })} /></label>
                      <label className="field-line"><span className="eyebrow">Texture</span>
                        <select data-field="material-texture" value={textureValue} onChange={(e) => update({ texture: e.target.value || null })}>
                          <option value="">guessed</option>
                          {TEXTURES.map((t) => <option key={t} value={t}>{t}</option>)}
                        </select></label>
                      {!ownImage && (
                        <>
                          <label className="small" title="How wide one tile of the texture lies on the model">W <LengthField metres={size?.[0] ?? null} min={0.01} noUnit placeholder="auto" style={{ width: 64 }} onCommit={(v) => update({ textureSize: v != null ? [v, size?.[1] ?? v] : null })} /></label>
                          <label className="small" title="How high one tile of the texture lies on the model">H <LengthField metres={size?.[1] ?? null} min={0.01} placeholder="auto" style={{ width: 64 }} onCommit={(v) => update({ textureSize: v != null ? [size?.[0] ?? v, v] : null })} /></label>
                        </>
                      )}
                      <label className="small" title="The texture's turn on the faces, degrees">Rotation <input className="num" data-field="material-rotation" key={`${m.id}r${look.textureRotation}`} defaultValue={look.textureRotation}
                        onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v) && v >= -360 && v <= 360 && v !== look.textureRotation) update({ textureRotation: v }); }} />°</label>
                    </div>
                    <OwnImage materialId={m.id} />
                    {SLIDERS.map(({ key, label, step, title }) => (
                      <label key={key} className={`slider-line${ownLook(key) ? ' own' : ''}`} title={title}>
                        <span className="small">{label}</span>
                        <input type="range" data-field={`material-${key}`} min={LOOK_RANGES[key][0]} max={LOOK_RANGES[key][1]} step={step} value={look[key]}
                          onChange={(e) => setDraft((d) => ({ ...d, [key]: Number(e.target.value) }))}
                          onPointerUp={() => commit(key)} onKeyUp={() => commit(key)} onBlur={() => commit(key)} />
                        <span className="small num-out">{look[key].toFixed(2)}</span>
                      </label>
                    ))}
                    <div className="row">
                      <label className="small" title="Index of refraction: glass 1.52, water 1.33, diamond 2.42">IOR <input className="num" data-field="material-ior" key={`${m.id}i${look.ior}`} defaultValue={look.ior}
                        onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (v >= 1 && v <= 3 && v !== look.ior) update({ ior: v }); }} /></label>
                      <label className="check" title="The light it gives off, as a lamp's diffuser"><input type="checkbox" checked={look.emissive != null} onChange={(e) => update({ emissive: e.target.checked ? '#fff4e0' : '' })} /> Glows</label>
                      {look.emissive && <>
                        <input type="color" value={look.emissive} onChange={(e) => update({ emissive: e.target.value })} />
                        <label className="small">Strength <input className="num" key={`${m.id}e${look.emissiveStrength}`} defaultValue={look.emissiveStrength}
                          onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (v >= 0 && v <= 100 && v !== look.emissiveStrength) update({ emissiveStrength: v }); }} /></label>
                      </>}
                    </div>
                    <p className="muted small">What is not set comes from the name: glass is clear, a metal metallic, a tile glossy. The 3D exports name each material as here, with these values.</p>
                    <div className="row">
                      <button type="button" className="btn compact" onClick={() => update(Object.fromEntries(LOOK_KEYS.map((k) => [k, null])))}><RotateCcw size={13} />Appearance from the name</button>
                    </div>
                  </div>
                </div>
              )}
              {tab === 'graphics' && (
                <>
                  <div className="row">
                    {([['textureEnd', 'Ends, cut across (3D)', 'as the faces (brick: its holes)'], ['textureTop', 'Top and bottom (3D)', 'as the ends']] as const).map(([key, label, dflt]) => (
                      <label key={key} className="field-line"><span className="eyebrow">{label}</span>
                        <select value={String(node?.props[key] ?? '')} onChange={(e) => update({ [key]: e.target.value || null })}>
                          <option value="">{dflt}</option>
                          {TEXTURES.map((t) => <option key={t} value={t}>{t}</option>)}
                        </select></label>
                    ))}
                  </div>
                  <div className="row">
                    <label className="check"><input type="checkbox" checked={m.section.membrane} onChange={(e) => update({ membrane: e.target.checked })} /> Membrane (a dashed line, not a band)</label>
                    <label className="check" title="Off (the default): where this material goes on from one element into another (two walls' cores, footings round a corner) no line is drawn between them. On: the joint is drawn."><input type="checkbox" checked={m.section.drawJoints} onChange={(e) => update({ drawJoints: e.target.checked })} /> Draw joints</label>
                  </div>
                  <MaterialGraphicsTable m={m} hatches={hatches} />
                  <p className="muted small">Hatches are paper millimetres at 1:1; a scale enlarges them. A fill is off unless asked.</p>
                  <div className="row">
                    <button type="button" className="btn" onClick={() => void execute('material.reset', { id: m.id })}><RotateCcw size={14} />From the name</button>
                  </div>
                </>
              )}
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/** A library swatch: a ball lit from the top left, glossier and darker-edged as it is metal and smooth, chequered through when see-through. */
function ball(color: string, metalness: number, roughness: number, transparency: number): string {
  const shine = ((1 - roughness) * (0.35 + 0.5 * metalness)).toFixed(2);
  const body = transparency > 0 ? `color-mix(in srgb, ${color} ${Math.round((1 - transparency) * 80 + 20)}%, transparent)` : color;
  const layers = `radial-gradient(circle at 32% 30%, rgba(255,255,255,${shine}) 0, rgba(255,255,255,0) 38%), radial-gradient(circle at 50% 50%, ${body} 55%, color-mix(in srgb, ${color} ${Math.round(100 - 35 * metalness)}%, black) 100%)`;
  return transparency > 0 ? `${layers}, repeating-conic-gradient(#d9d6cf 0 25%, #f4f2ec 0 50%) 50% / 10px 10px` : layers;
}
