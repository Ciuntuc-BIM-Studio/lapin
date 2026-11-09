/**
 * Steel types (decision 268): the project's member types, and a new type of a profile — from the catalogue (IPE, HEA,
 * HEB, UPN, L, RHS, CHS, and the generic cold-formed C, Z and Σ) or a hollow or cold-formed section of one's own
 * dimensions — made a member type, or a beam or column type of the structure module (whose tools then place steel).
 */

import { useState } from 'react';
import { Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { deleteType } from '../core/deleteType';
import { LengthField } from '../../shell/cells';
import { kgPerMetre, sectionLabel, sectionPath, type SectionDto } from './sections';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';
import { useLibraryPick } from '../core/profileLibraries/useLibraryPick';

type Made = 'member' | 'beam' | 'column';
const CUSTOM = ['RHS', 'CHS', 'C', 'Z', 'SIGMA'];

/** A section drawn in its box, for the list and the catalog's cards. */
export function SectionGlyph({ section, size = 36 }: { section: SectionDto | undefined; size?: number }) {
  if (!section) return null;
  return <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true"><path d={sectionPath(section, size)} fill="currentColor" fillRule="evenodd" opacity={0.75} /></svg>;
}

export function SteelTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const catalogue = (useSession((s) => s.info?.catalogs['steel.profiles']) ?? []) as unknown as SectionDto[];
  const model = { nodes, relations };
  const types = ofClass(model, 'LapinSteelMemberType');
  const [profile, setProfile] = useState<string>('IPE 300');
  const [made, setMade] = useState<Made>('member');
  const [name, setName] = useState('');
  // A section of one's own: its kind and dimensions.
  const [kind, setKind] = useState('RHS');
  const [d, setD] = useState(0.2);
  const [w, setW] = useState(0.1);
  const [t, setT] = useState(0.005);
  const [lip, setLip] = useState(0.02);
  const own = profile === '';
  // A profile of the libraries (docs/plan-profile-libraries.md): copied into the project with the type.
  const fromLibrary = profile === '@library';
  const lib = useLibraryPick();
  const section: SectionDto | undefined = fromLibrary ? lib.section ?? undefined
    : own ? { kind, width: w, depth: kind === 'CHS' ? w : d, web: t, flange: t, lip: ['C', 'Z', 'SIGMA'].includes(kind) ? lip : 0 } : catalogue.find((p) => p.name === profile);

  const create = async () => {
    if (fromLibrary && !lib.picked) return;
    const sec = own ? { kind, width: w, depth: kind === 'CHS' ? w : d, thickness: t, lip: ['C', 'Z', 'SIGMA'].includes(kind) ? lip : 0 } : profile;
    const args = fromLibrary ? { name: name.trim() || undefined, profile: lib.picked!.profile, library: lib.picked!.library } : { name: name.trim() || undefined, section: sec };
    const r = (await execute(made === 'member' ? 'steel.createType' : made === 'beam' ? 'structure.createBeamType' : 'structure.createColumnType', args)) as { id?: string } | null;
    if (r?.id && made === 'member') useUi.setState({ memberTypeId: r.id });
    if (r?.id) setName('');
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Steel types">
      <div className="dialog wide" data-dialog="steel-types">
        <header className="dialog-head">
          <span className="panel-title">Steel types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <p className="eyebrow">Member types</p>
            <ul className="type-list">
              {types.map((ty) => {
                const s = ty.props.section as SectionDto | undefined;
                return (
                  <li key={ty.id} className="tree-item" title="Place members with it" onClick={() => useUi.setState({ memberTypeId: ty.id })}>
                    <SectionGlyph section={s} size={20} /> <span data-no-i18n>{ty.name}</span>
                    <span className="muted small" data-no-i18n> · {s ? `${kgPerMetre(s).toFixed(1)} kg/m` : ''} · {relatedOf(model, ty.id, 'IfcRelDefinesByType').length}</span>
                    <button type="button" className="icon mini" title="Delete the type (refused while elements use it)" onClick={(e) => { e.stopPropagation(); void deleteType(ty.id); }}><Trash2 size={11} /></button>
                  </li>
                );
              })}
            </ul>
          </div>
          <div className="type-edit">
            <p className="eyebrow">New steel type</p>
            <label className="field-line"><span className="eyebrow">Profile</span>
              <select value={profile} data-field="steel-profile" onChange={(e) => setProfile(e.target.value)}>
                {catalogue.map((p) => <option key={p.name} value={p.name} data-no-i18n>{p.name}</option>)}
                <option value="">Of its own dimensions</option>
                <option value="@library">From a profile library…</option>
              </select>
            </label>
            {fromLibrary && <label className="field-line"><span className="eyebrow">Library profile</span>
              <ProfilePickerField testId="steel-library-profile" value={lib.picked?.profile.name} onPick={lib.pick} />
            </label>}
            {own && <>
              <label className="field-line"><span className="eyebrow">Shape</span>
                <select value={kind} onChange={(e) => setKind(e.target.value)}>{CUSTOM.map((k) => <option key={k} value={k} data-no-i18n>{k === 'SIGMA' ? 'Σ' : k}</option>)}</select>
              </label>
              {kind !== 'CHS' && <label className="field-line"><span className="eyebrow">Depth</span><LengthField metres={d} min={0.01} onCommit={(v) => { if (v != null) setD(v); }} /></label>}
              <label className="field-line"><span className="eyebrow">{kind === 'CHS' ? 'Diameter' : 'Width'}</span><LengthField metres={w} min={0.01} onCommit={(v) => { if (v != null) setW(v); }} /></label>
              <label className="field-line"><span className="eyebrow">Thickness</span><LengthField metres={t} min={0.0005} onCommit={(v) => { if (v != null) setT(v); }} /></label>
              {['C', 'Z', 'SIGMA'].includes(kind) && <label className="field-line"><span className="eyebrow">Lip</span><LengthField metres={lip} min={0} onCommit={(v) => { if (v != null) setLip(v); }} /></label>}
            </>}
            <div className="row" style={{ gap: 12, alignItems: 'center' }}>
              <SectionGlyph section={section} size={64} />
              <span className="small" data-no-i18n>{sectionLabel(section)}{section ? ` · ${(fromLibrary && lib.mass != null ? lib.mass : kgPerMetre(section)).toFixed(1)} kg/m` : ''}</span>
            </div>
            <label className="field-line"><span className="eyebrow">Used as</span>
              <select value={made} data-field="steel-made" onChange={(e) => setMade(e.target.value as Made)}>
                <option value="member">member type</option><option value="beam">beam type</option><option value="column">column type</option>
              </select>
            </label>
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its size)" onChange={(e) => setName(e.target.value)} /></label>
            <div className="row"><span className="grow" /><button type="button" className="btn primary" data-field="steel-create-type" onClick={() => void create()}>Create type</button></div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
