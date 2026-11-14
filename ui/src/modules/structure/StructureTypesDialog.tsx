/**
 * Structure types: columns and beams (their section), strip foundations
 * (the stem's layers, the footing's size, its level) and pad footings (the
 * steps, bottom up). A strip foundation is the standard stem on a footing, or any
 * profile of the project swept along its axis (decision 297). A new type is made here, and a type clicked in the list
 * is changed here (report #42), its elements following it; the inspector
 * gives an element another one.
 */

import { useState } from 'react';
import { Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass, typeLayers } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { describeType } from './describe';
import { structureHooks } from './hooks';
import { deleteType } from '../core/deleteType';
import { SaveTypeToLibrary } from '../core/SaveTypeToLibrary';
import { useStandards } from '../../engine/unitsStore';
import { formatSteps, parseSteps, type Step } from './steps';
import { Cell, LengthField } from '../../shell/cells';
import { ProfilePicker, ProfilePreview } from '../core/ProfilesDialog';
import { ProfilePickerField } from '../core/profileLibraries/ProfilePickerField';
import { useLibraryPick } from '../core/profileLibraries/useLibraryPick';
import { SectionGlyph } from '../steel/SteelTypesDialog';
import type { ProfileJson } from '../core/profiles';
import { TypeListActions, endsOf } from '../../shell/TypeListActions';

type Kind = 'column' | 'beam' | 'strip' | 'pad';
type Shape = 'rectangle' | 'circle' | 'polygon' | 'library';
const num = (m: unknown, fallback: number) => (typeof m === 'number' ? m : fallback);

/** A labelled length of the type (the shared field). */
const Len = ({ label, value, onChange, min }: { label: string; value: number; onChange: (m: number) => void; min?: number }) => (
  <label className="field-line"><span className="eyebrow">{label}</span><LengthField metres={value} min={min} onCommit={(m) => { if (m != null) onChange(m); }} /></label>
);

export function StructureTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = { nodes, relations };
  const [kind, setKind] = useState<Kind>('column');
  // The type being changed; null: a new one is made.
  const [editing, setEditing] = useState<string | null>(null);
  const [name, setName] = useState('');
  // A section's shape; a regular polygon (an octagonal post, as recognised from an IFC) has its sides, its diameter across the corners.
  const [shape, setShape] = useState<Shape>('rectangle');
  const [sides, setSides] = useState(8);
  // A profile of the libraries (docs/plan-profile-libraries.md): steel, timber, aluminium, precast, copied into the project with the type.
  const lib = useLibraryPick();
  // Lengths in metres; the steps as typed.
  const units = useStandards((s) => s.units);
  const [w, setW] = useState(0.3);
  const [d, setD] = useState(0.3);
  const [stem, setStem] = useState(1.4);
  const [fw, setFw] = useState(0.6);
  const [fh, setFh] = useState(0.4);
  const [bottom, setBottom] = useState(-1.8);
  const [core, setCore] = useState(0.3);
  // A strip foundation's shape: the standard stem on a footing, or a profile of the project.
  const [stripShape, setStripShape] = useState<'standard' | 'profile'>('standard');
  const [profileId, setProfileId] = useState<string | null>(null);
  const [steps, setSteps] = useState<Step[]>([{ length: 1.8, width: 1.8, height: 0.5 }, { length: 1, width: 1, height: 0.4 }]);
  const list = kind === 'column' ? ofClass(model, 'IfcColumnType') : kind === 'beam' ? ofClass(model, 'IfcBeamType')
    : ofClass(model, 'IfcFootingType').filter((t) => t.props.kind === kind);

  const edited = editing ? nodes.get(editing) ?? null : null;

  /** A type clicked: its values in the fields, to be changed. */
  const pick = (t: NodeDto) => {
    setEditing(t.id);
    setName(t.name);
    const sec = t.props.section as { kind?: string; width?: number; depth?: number; sides?: number } | undefined;
    if (sec) {
      setShape(sec.kind === 'circle' || sec.kind === 'polygon' ? sec.kind : sec.kind && sec.kind !== 'rectangle' ? 'library' : 'rectangle'); setW(num(sec.width, 0.3)); setD(num(sec.depth, 0.3));
      if (sec.kind === 'polygon') setSides(num(sec.sides, 8));
    }
    if (t.props.kind === 'strip') {
      setStripShape(t.props.profile ? 'profile' : 'standard');
      setProfileId(typeof t.props.profileId === 'string' ? t.props.profileId : null);
      setStem(num(t.props.stemHeight, 1.4)); setFw(num(t.props.footingWidth, 0.6)); setFh(num(t.props.footingHeight, 0.4)); setBottom(num(t.props.bottom, -1.8));
      const layers = typeLayers(model, t.id);
      const core = layers.find((l) => l.core) ?? layers.reduce<(typeof layers)[number] | undefined>((a, l) => (!a || l.thickness > a.thickness ? l : a), undefined);
      if (core) setCore(core.thickness);
    }
    if (t.props.kind === 'pad') {
      setBottom(num(t.props.bottom, -1.8));
      setSteps((t.props.steps as Step[] | undefined) ?? []);
    }
  };
  const fresh = () => { setEditing(null); setName(''); };
  const switchKind = (k: Kind) => { setKind(k); fresh(); };

  const sectionOf = () => shape === 'polygon' ? { kind: shape, width: w, depth: w, sides } : { kind: shape, width: w, depth: shape === 'circle' ? w : d };

  const save = async () => {
    if (!edited) return;
    const section = sectionOf();
    const args: Record<string, unknown> = { id: edited.id, name };
    if ((kind === 'column' || kind === 'beam') && shape === 'library') { if (lib.picked) Object.assign(args, { profile: lib.picked.profile, library: lib.picked.library }); }
    else if (kind === 'column' || kind === 'beam') args.section = section;
    else if (kind === 'strip' && stripShape === 'profile') Object.assign(args, { bottom, ...(profileId ? { profileId } : {}) });
    else if (kind === 'strip') Object.assign(args, { stemHeight: stem, footingWidth: fw, footingHeight: fh, bottom, ...(edited.props.profile ? { profile: null } : { core }) });
    else Object.assign(args, { bottom, steps });
    // The name as it was: left to the engine, which renames a type still named after its size.
    if (name.trim() === edited.name) delete args.name;
    await execute('structure.updateType', args);
  };

  const create = async () => {
    const section = sectionOf();
    if ((kind === 'column' || kind === 'beam') && shape === 'library') {
      if (!lib.picked) return;
      await execute(kind === 'column' ? 'structure.createColumnType' : 'structure.createBeamType', { name, profile: lib.picked.profile, library: lib.picked.library });
    } else if (kind === 'column') await execute('structure.createColumnType', { name, section });
    else if (kind === 'beam') await execute('structure.createBeamType', { name, section });
    else if (kind === 'strip' && stripShape === 'profile') {
      if (!profileId) return;
      await execute('structure.createStripType', { name, bottom, profileId });
    } else if (kind === 'strip') {
      await execute('structure.createStripType', {
        name, stemHeight: stem, footingWidth: fw, footingHeight: fh, bottom,
        layers: [
          { material: 'Protection board', thickness: 0.02 }, { material: 'XPS insulation', thickness: 0.1 },
          { material: 'Waterproofing', thickness: 0.005 }, { material: 'Reinforced concrete', thickness: core, core: true },
        ],
      });
    } else {
      await execute('structure.createPadType', { name, bottom, steps });
    }
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Structure types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Structure types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <div className="row tabs-mini">
              {(['column', 'beam', 'strip', 'pad'] as const).map((k) => (
                <button type="button" key={k} className={`tree-item${kind === k ? ' on' : ''}`} onClick={() => switchKind(k)}>{{ column: 'Columns', beam: 'Beams', strip: 'Strip', pad: 'Pad' }[k]}</button>
              ))}
            </div>
            <ul className="type-list">
              {list.map((t) => (
                <li key={t.id} className={`tree-item${editing === t.id ? ' on' : ''}`} title="Click to change the type; its elements follow." onClick={() => pick(t)}>{t.name}<span className="muted small"> · {describeType(t)}</span>
                  <button type="button" className="icon mini" title="Delete the type (refused while elements use it)" onClick={(e) => { e.stopPropagation(); void deleteType(t.id).then((ok) => { if (ok && editing === t.id) fresh(); }); }}><Trash2 size={11} /></button>
                </li>
              ))}
            </ul>
          </div>
          <div className="type-edit">
            <div className="row">
              <p className="eyebrow grow">{edited ? <span data-no-i18n>{edited.name}</span> : `New ${kind} type`}</p>
              {edited && <SaveTypeToLibrary ids={[edited.id]} />}
              {edited && <button type="button" className="btn small" onClick={fresh}>New type</button>}
            </div>
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its size)" onChange={(e) => setName(e.target.value)} /></label>
            {/* Duplicate, and the type's place in every list of its types (forum #71). */}
            <TypeListActions id={edited?.id ?? null} onDuplicated={(id) => { const t = useSession.getState().nodes.get(id); if (t) pick(t); }} {...endsOf(list, edited?.id ?? null)} />
            {(kind === 'column' || kind === 'beam') && <>
              <label className="field-line"><span className="eyebrow">Section</span>
                <select value={shape} onChange={(e) => setShape(e.target.value as Shape)}><option value="rectangle">Rectangle</option><option value="circle">Circle</option>
                  {(kind === 'column' || shape === 'polygon') && <option value="polygon">Polygon</option>}<option value="library">From a profile library…</option></select>
              </label>
              {shape === 'library' && <>
                <label className="field-line"><span className="eyebrow">Library profile</span>
                  <ProfilePickerField testId="structure-library-profile" value={lib.picked?.profile.name ?? (edited?.props.section as { name?: string } | undefined)?.name} onPick={lib.pick} /></label>
                {lib.section && <div className="row" style={{ gap: 10, alignItems: 'center' }}><SectionGlyph section={lib.section} size={56} />
                  <span className="small" data-no-i18n>{`${lib.picked?.profile.name ?? ''}${lib.mass != null ? ` · ${lib.mass.toFixed(1)} kg/m` : ''}`}</span></div>}
              </>}
              {shape !== 'library' && <Len label={shape === 'rectangle' ? 'Width' : 'Diameter'} value={w} min={0.01} onChange={setW} />}
              {shape === 'polygon' && <label className="field-line"><span className="eyebrow">Sides</span>
                <input type="number" min={3} max={64} step={1} value={sides} onChange={(e) => setSides(Math.max(3, Math.min(64, Math.round(Number(e.target.value) || 3))))} /></label>}
              {shape === 'rectangle' && <Len label={kind === 'beam' ? 'Depth' : 'Length'} value={d} min={0.01} onChange={setD} />}
            </>}
            {edited && (kind === 'column' || kind === 'beam') && structureHooks.typeExtras.map((Extra, i) => <Extra key={i} type={edited} kind={kind} />)}
            {kind === 'strip' && <label className="field-line"><span className="eyebrow">Shape</span>
              <select data-field="strip-shape" value={stripShape} onChange={(e) => setStripShape(e.target.value as 'standard' | 'profile')}>
                <option value="standard">Standard (stem on a footing)</option>
                <option value="profile">Profile</option>
              </select></label>}
            {kind === 'strip' && stripShape === 'profile' && <div className="row" style={{ gap: 10, alignItems: 'flex-start' }}>
              <ProfilePreview shape={(profileId ? nodes.get(profileId)?.props.profile : edited?.props.profile) as unknown as ProfileJson | undefined} size={96} />
              <div className="grow">
                <label className="field-line"><span className="eyebrow">Profile</span><ProfilePicker use="footing" value={profileId} testId="strip-profile" onPick={setProfileId} /></label>
                <p className="muted small">Swept along the axis, its insertion point on the axis at the bottom level; corners mitred, a footing ending against another cut by it.</p>
              </div>
            </div>}
            {kind === 'strip' && stripShape === 'standard' && <>
              <Len label="Core" value={core} min={0.01} onChange={setCore} />
              <Len label="Stem height" value={stem} min={0} onChange={setStem} />
              <Len label="Footing width" value={fw} min={0.01} onChange={setFw} />
              <Len label="Footing height" value={fh} min={0.01} onChange={setFh} />
            </>}
            {kind === 'pad' && <label className="field-line"><span className="eyebrow">Steps L × W × H</span>
              <Cell value={formatSteps(steps, units)} title="Each step length × width × height, bottom up; a semicolon between steps"
                onCommit={(t) => { const s = parseSteps(t, units); if (!s?.length) return false; setSteps(s); }} /></label>}
            {(kind === 'strip' || kind === 'pad') && <Len label="Bottom" value={bottom} onChange={setBottom} />}
            <div className="row"><span className="grow" />{edited
              ? <button type="button" className="btn primary" title="Its elements follow the type" onClick={() => void save()}>Save type</button>
              : <button type="button" className="btn primary" onClick={() => void create()}>Create type</button>}</div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
