/**
 * Manage → Profiles… (decision 297): the project's 2D profiles, what wall sweeps, reveals and strip footings are swept
 * with. A new one from the catalogue with its sizes, typed corner by corner, or read from a closed outline drawn with
 * Drafting (selected in a view); each with its preview and insertion point. A profile changed: everything using it follows.
 * ProfilePicker and ProfilePreview are what the sweep tool and the structure types dialog choose and show one with.
 */

import { useEffect, useMemo, useState } from 'react';
import { Library, PenLine, Plus, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { Cell, LengthField } from '../../shell/cells';
import { PendingDialog } from './PendingDialog';
import { ProfilePickerField } from './profileLibraries/ProfilePickerField';
import { asSweepShape, type ProfileJson as LibraryProfile, type Ring } from './profileLibraries/model';
import { formatPoints, parsePoints, profileArea, profileSize, profileSvg, type CatalogueProfile, type ProfileJson } from './profiles';

export type ProfileUse = 'sweep' | 'reveal' | 'footing';
const USES: { id: ProfileUse; label: string }[] = [
  { id: 'sweep', label: 'Wall sweep' }, { id: 'reveal', label: 'Wall reveal' }, { id: 'footing', label: 'Strip footing' },
];

export const profileCatalogue = (): CatalogueProfile[] =>
  (useSession.getState().info?.catalogs['core.profileCatalogue'] as unknown as CatalogueProfile[] | undefined) ?? [];

/** A profile's outline drawn to scale, its insertion point marked. */
export function ProfilePreview({ shape, size = 120 }: { shape: ProfileJson | null | undefined; size?: number }) {
  const svg = shape ? profileSvg(shape, size, size) : null;
  return (
    <svg className="profile-preview" width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label="Profile">
      <rect x={0.5} y={0.5} width={size - 1} height={size - 1} fill="none" stroke="currentColor" strokeOpacity={0.15} />
      {svg && <path d={svg.path} fill="var(--accent-soft, #d9cdb8)" stroke="currentColor" strokeWidth={1} />}
      {svg && <circle cx={svg.ix} cy={svg.iy} r={3} fill="none" stroke="var(--accent, #b5654a)" strokeWidth={1.5} />}
    </svg>
  );
}

/**
 * A profile chosen for a use: the project's own (those for that use first), or a ready-made one of the catalogue, made a
 * project profile when picked. `onPick` gets the project profile's id.
 */
export function ProfilePicker({ use, value, onPick, testId }: { use: ProfileUse; value: string | null | undefined; onPick: (id: string) => void; testId?: string }) {
  const nodes = useSession((s) => s.nodes);
  const own = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinProfile')
    .sort((a, b) => Number(b.props.use === use) - Number(a.props.use === use) || a.name.localeCompare(b.name)), [nodes, use]);
  const catalogue = profileCatalogue().filter((c) => c.use === use);
  const [searching, setSearching] = useState(false);
  // A profile of the libraries (docs/plan-profile-libraries.md): its outside made a project profile for this use.
  const fromLibrary = async (p: LibraryProfile) => {
    setSearching(false);
    const r = await query<{ rings: Ring[] }>('profiles.properties', { profile: p });
    const shape = r ? asSweepShape(r.rings, use, p.name) : null;
    if (!shape) return;
    const made = await execute('profile.create', { profile: shape, name: p.name, use }) as { id?: string } | null;
    if (made?.id) onPick(made.id);
  };
  if (searching) return <ProfilePickerField testId={`${testId ?? 'profile'}-library`} onPick={(p) => void fromLibrary(p)} />;
  const pick = async (v: string) => {
    if (v.startsWith('catalog:')) {
      const r = await execute('profile.create', { catalog: v.slice(8) }) as { id?: string } | null;
      if (r?.id) onPick(r.id);
    } else if (v === 'library') setSearching(true);
    else if (v === 'manage') useUi.setState({ dialog: 'profiles' });
    else if (v) onPick(v);
  };
  return (
    <select data-field={testId ?? 'profile'} value={value ?? ''} onChange={(e) => void pick(e.target.value)}>
      {!value && <option value="">(choose a profile)</option>}
      {own.length > 0 && <optgroup label="The project's">
        {own.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.name}</option>)}
      </optgroup>}
      <optgroup label="From the catalogue">
        {catalogue.map((c) => <option key={c.id} value={`catalog:${c.id}`}>{c.label}</option>)}
      </optgroup>
      <option value="library">From a profile library…</option>
      <option value="manage">Profiles…</option>
    </select>
  );
}

const shapeOf = (n: NodeDto | undefined): ProfileJson | null => (n?.props.profile as unknown as ProfileJson | undefined) ?? null;

export function ProfilesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const selected = useUi((s) => s.selected);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const profiles = ofClass(model, 'LapinProfile');
  const catalogue = (info?.catalogs['core.profileCatalogue'] as unknown as CatalogueProfile[] | undefined) ?? null;
  const [editing, setEditing] = useState<string | null>(profiles[0]?.id ?? null);
  // A new profile: from the catalogue (its id) or typed.
  const [catalogId, setCatalogId] = useState<string>('rectangle');
  const [name, setName] = useState('');
  const [use, setUse] = useState<ProfileUse>('sweep');
  const [values, setValues] = useState<Record<string, number>>({});
  const [typed, setTyped] = useState<ProfileJson | null>(null);
  const node = editing ? nodes.get(editing) : undefined;
  const nodeCatalog = (node?.props.catalog as { id?: string; params?: Record<string, number> } | undefined) ?? null;

  useEffect(() => {
    if (!node) return;
    setName(node.name);
    setUse((node.props.use as ProfileUse | undefined) ?? 'sweep');
    setValues(nodeCatalog?.params ?? {});
    setTyped(shapeOf(node));
    if (nodeCatalog?.id) setCatalogId(nodeCatalog.id);
    // Only when another profile is picked: the fields keep what is being typed meanwhile.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing]);

  const entryId = node ? nodeCatalog?.id : catalogId;
  const fromCatalogue = node ? !!nodeCatalog?.id : catalogId !== 'typed';
  // A ready-made one's shape with the sizes typed, as the engine draws it.
  const [drawn, setDrawn] = useState<ProfileJson | null>(null);
  const valuesKey = JSON.stringify(values);
  useEffect(() => {
    if (!fromCatalogue || !entryId) { setDrawn(null); return; }
    let live = true;
    void query<{ profile?: ProfileJson }>('profile.outline', { catalog: entryId, params: values }).then((r) => { if (live) setDrawn(r?.profile ?? null); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fromCatalogue, entryId, valuesKey]);

  if (!catalogue) return <PendingDialog title="Profiles" />;
  const entry = catalogue.find((c) => c.id === entryId) ?? null;
  const close = () => useUi.setState({ dialog: null });
  const preview: ProfileJson | null = fromCatalogue ? drawn ?? entry?.shape ?? null : typed;
  const size = preview ? profileSize(preview) : null;

  const save = async () => {
    if (editing) {
      const args: Record<string, unknown> = { id: editing, name, use };
      if (fromCatalogue) args.params = values;
      else if (typed) args.profile = typed;
      await execute('profile.update', args);
      return;
    }
    const r = (fromCatalogue
      ? await execute('profile.create', { catalog: catalogId, params: values, name: name.trim() || undefined, use })
      : typed ? await execute('profile.create', { profile: typed, name: name.trim() || undefined, use }) : null) as { id?: string } | null;
    if (r?.id) setEditing(r.id);
  };
  const fromDrafting = async () => {
    const shape = selected.find((id) => nodes.get(id)?.type === 'IfcAnnotation');
    if (!shape) { notify('Select a closed outline drawn with Drafting first (a polyline or a rectangle), then come back here.'); return; }
    const r = await execute('profile.create', { annotationId: shape, use, name: name.trim() || undefined }) as { id?: string } | null;
    if (r?.id) setEditing(r.id);
  };
  const remove = async (id: string) => {
    await execute('element.delete', { id });
    if (editing === id) setEditing(null);
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Profiles">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Profiles</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {profiles.map((p) => (
              <li key={p.id}>
                <button type="button" className={`tree-item${editing === p.id ? ' on' : ''}`} onClick={() => setEditing(p.id)}>
                  <span data-no-i18n>{p.name}</span><span className="muted small"> · {USES.find((u) => u.id === p.props.use)?.label ?? ''}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className="tree-item" onClick={() => { setEditing(null); setName(''); setValues({}); setTyped(null); }}><Plus size={12} /> New profile</button></li>
            <li><button type="button" className="tree-item" title="A closed polyline or rectangle drawn with Drafting, selected in its view" onClick={() => void fromDrafting()}><PenLine size={12} /> From a drafted outline</button></li>
          </ul>
          <div className="type-edit">
            <div className="row" style={{ alignItems: 'flex-start', gap: 12 }}>
              <ProfilePreview shape={preview} />
              <div className="grow">
                <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its sizes)" onChange={(e) => setName(e.target.value)} /></label>
                <label className="field-line"><span className="eyebrow">Use</span>
                  <select data-field="profile-use" value={use} onChange={(e) => setUse(e.target.value as ProfileUse)}>
                    {USES.map((u) => <option key={u.id} value={u.id}>{u.label}</option>)}
                  </select></label>
                {!editing && <label className="field-line"><span className="eyebrow"><Library size={11} /> Shape</span>
                  <select data-field="profile-catalog" value={catalogId} onChange={(e) => { setCatalogId(e.target.value); setValues({}); const c = catalogue.find((x) => x.id === e.target.value); if (c) setUse(c.use); }}>
                    {USES.map((u) => <optgroup key={u.id} label={u.label}>{catalogue.filter((c) => c.use === u.id).map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}</optgroup>)}
                    <option value="typed">Typed corners</option>
                  </select></label>}
              </div>
            </div>
            {fromCatalogue && entry && entry.params.map((p) => (
              <label key={p.key} className="field-line"><span className="eyebrow">{p.label}</span>
                <LengthField testId={`profile-${p.key}`} metres={values[p.key] ?? p.default} min={0.001} onCommit={(m) => { if (m != null) setValues({ ...values, [p.key]: m }); }} /></label>
            ))}
            {!fromCatalogue && <>
              <label className="field-line"><span className="eyebrow">Corners (mm)</span>
                <Cell testId="profile-points" value={typed ? formatPoints(typed.points) : ''} placeholder="0 0; 300 0; 300 400; 0 400"
                  title="Each corner u v in millimetres (u across, v up), a semicolon between; a third number makes the side from that corner an arc (its bulge: 1 a half circle, negative the other way)"
                  onCommit={(t) => { const pts = parsePoints(t); if (!pts) return false; setTyped({ points: pts, insertion: typed?.insertion ?? [0, 0] }); }} /></label>
              <label className="field-line"><span className="eyebrow">Insertion point u</span>
                <LengthField metres={typed?.insertion?.[0] ?? 0} onCommit={(m) => { if (m != null && typed) setTyped({ ...typed, insertion: [m, typed.insertion?.[1] ?? 0] }); }} /></label>
              <label className="field-line"><span className="eyebrow">Insertion point v</span>
                <LengthField metres={typed?.insertion?.[1] ?? 0} onCommit={(m) => { if (m != null && typed) setTyped({ ...typed, insertion: [typed.insertion?.[0] ?? 0, m] }); }} /></label>
            </>}
            {size && preview && <p className="muted small" data-no-i18n>{`${Math.round(size.width * 1000)} × ${Math.round(size.height * 1000)} mm · ${(profileArea(preview) * 1e4).toFixed(1)} cm²`}</p>}
            <p className="muted small">The circle is the insertion point: on the wall's face at the sweep's height (u out of the face, into the wall for a reveal), or on a footing's axis at its bottom (u across, to the axis's left).</p>
            <div className="row joinery-actions">
              {editing && <button type="button" className="btn" title="What uses it keeps its copy of it" onClick={() => void remove(editing)}>Delete</button>}
              <span className="grow" />
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save profile' : 'Create profile'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
