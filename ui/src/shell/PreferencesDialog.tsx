/**
 * File → Preferences… (Ctrl+,; decision 445): the user's own settings, kept on this computer for every project and
 * applied at once. General: the theme (light, dark, as the system is) and the language. Grid and snaps: the 2D
 * grid adaptive or fixed (its step and every how many lines a heavier one), snapping to it, each object snap on or
 * off, polar tracking's angle. Coordinates: what the status line reads at the cursor, the project's points shown in
 * plans and 3D, the UCS. Display: the switches of View → Graphics and the 3D view's navigation, in one place.
 * Profiles: the profile standards a new project offers and those the pickers show (decision 457).
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../app/store';
import { POLAR_STEPS, SNAP_MODES, resolveTheme, setPrefs, usePrefs, type CoordSystem, type ThemeChoice } from '../app/prefs';
import { setDisplay, useDisplay, type Display } from '../app/display';
import { setLocationShown, setUcs, surveyPoint, ucsOf, useLocation } from '../app/location';
import { LANGUAGES, setLang, useLang } from '../i18n';
import { LengthField, NumberField } from './cells';
import { COORD_LABELS, SNAP_LABELS } from './CursorCoords';
import { formatLength } from '../engine/unitsStore';
import { startCommand } from '../viewport/cad/session';
import { useProfileLibraries } from '../modules/core/profileLibraries/store';
import { standardsOf } from '../modules/core/profileLibraries/model';
import { StandardChecks } from '../modules/core/profileLibraries/StandardChecks';

type Page = 'general' | 'grid' | 'coords' | 'display' | 'profiles';
const PAGES: { id: Page; label: string }[] = [
  { id: 'general', label: 'General' }, { id: 'grid', label: 'Grid and snaps' }, { id: 'coords', label: 'Coordinates' }, { id: 'display', label: 'Display' },
  { id: 'profiles', label: 'Profiles' },
];

/** The profile standards (decision 457): those a new project offers, those the pickers show. */
function ProfilesPage() {
  const p = usePrefs();
  const { entries, loading } = useProfileLibraries();
  const all = standardsOf(entries);
  if (loading) return <p className="muted small">Reading the libraries…</p>;
  return <>
    <h4>Standards a new project offers</h4>
    <StandardChecks all={all} value={p.newProjectProfileStandards} testId="prefs-new-project-standards" onChange={(v) => setPrefs({ newProjectProfileStandards: v })} />
    <p className="muted small">A project keeps only the profiles its types use, never a whole library: these are what its pickers offer. Manage → Profile libraries changes them for the project open.</p>
    <h4>Shown in the pickers</h4>
    <StandardChecks all={all} value={all.filter((g) => !p.profileStandardsHidden.includes(g))} testId="prefs-shown-standards"
      onChange={(v) => setPrefs({ profileStandardsHidden: v ? all.filter((g) => !v.includes(g)) : [] })} />
    <p className="muted small">The chips at the top of a picker's list do the same.</p>
  </>;
}
const THEMES: { id: ThemeChoice; label: string }[] = [{ id: 'paper', label: 'Light' }, { id: 'graphite', label: 'Dark' }, { id: 'system', label: 'As the system' }];
const DISPLAY: { key: keyof Display; label: string }[] = [
  { key: 'grid', label: 'Grid in plans and 3D (F7)' },
  { key: 'thinLines', label: 'Thin lines (TL)' },
  { key: 'drafts3d', label: 'Drafts in 3D' },
  { key: 'dxf3d', label: 'DXF in 3D' },
  { key: 'zoomToCursor', label: 'The wheel zooms to the cursor in 3D' },
  { key: 'orbitSelection', label: 'Orbit round the selection in 3D' },
  { key: 'dragMove3d', label: 'Drag a selected element to move it in 3D' },
  { key: 'drillSelect', label: 'Clicks drill into assemblies' },
  { key: 'sketchupKeys', label: 'SketchUp’s one-letter keys in 3D' },
];

function Radio<T extends string | number>({ name, value, options, onChange }: { name: string; value: T; options: { id: T; label: string; disabled?: boolean }[]; onChange: (v: T) => void }) {
  return (
    <div className="pref-radios" role="radiogroup" aria-label={name}>
      {options.map((o) => (
        <label key={String(o.id)} className={`row small${o.disabled ? ' muted' : ''}`}>
          <input type="radio" name={name} checked={value === o.id} disabled={o.disabled} onChange={() => onChange(o.id)} /> {o.label}
        </label>
      ))}
    </div>
  );
}

export function PreferencesDialog() {
  const [page, setPage] = useState<Page>('general');
  const p = usePrefs();
  const display = useDisplay();
  const lang = useLang((s) => s.lang);
  const loc = useLocation((s) => s.loc);
  const ucs = ucsOf(loc);
  const close = () => useUi.setState({ dialog: null });
  const theme = (choice: ThemeChoice) => { setPrefs({ theme: choice }); useUi.setState({ theme: resolveTheme(choice) }); };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Preferences" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog prefs-dialog" data-testid="preferences">
        <header className="dialog-head">
          <span className="panel-title">Preferences</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body prefs-body">
          <nav className="prefs-nav" aria-label="Pages">
            {PAGES.map((x) => <button key={x.id} type="button" className={`prefs-tab${page === x.id ? ' on' : ''}`} data-field={`prefs-${x.id}`} onClick={() => setPage(x.id)}>{x.label}</button>)}
          </nav>
          <section className="prefs-page">
            {page === 'general' && <>
              <h4>Theme</h4>
              <div data-i18n-ctx="theme"><Radio name="Theme" value={p.theme} options={THEMES} onChange={theme} /></div>
              <p className="muted small">Kept for the next start. The moon and sun at the right of the ribbon switch it too.</p>
              <h4>Language</h4>
              <select value={lang} data-no-i18n onChange={(e) => void setLang(e.target.value as typeof lang)}>
                {LANGUAGES.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
              </select>
              <h4>Interface</h4>
              <p className="row small"><button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'interface' })}>Interface…</button>
                <button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'shortcuts' })}>Keyboard shortcuts…</button></p>
            </>}
            {page === 'grid' && <>
              <h4 data-i18n-ctx="prefs">Grid</h4>
              <Radio name="Grid" value={p.gridMode} onChange={(v) => setPrefs({ gridMode: v })} options={[
                { id: 'adaptive', label: 'Adaptive: its step follows the zoom' },
                { id: 'fixed', label: 'Fixed: one step at every zoom' },
              ]} />
              {p.gridMode === 'fixed' && (
                <dl className="fields">
                  <dt>Step</dt><dd><LengthField metres={p.gridStep} min={0.001} testId="prefs-grid-step" onCommit={(v) => { if (v != null && v > 0) setPrefs({ gridStep: v }); }} /></dd>
                  <dt title="Every how many lines one is drawn heavier">Heavier line every</dt><dd><NumberField value={p.gridMajor} min={1} max={100} testId="prefs-grid-major" onCommit={(v) => { if (v != null) setPrefs({ gridMajor: Math.round(v) }); }} /></dd>
                </dl>
              )}
              <label className="row small"><input type="checkbox" data-field="prefs-grid-snap" checked={p.gridSnap} onChange={(e) => setPrefs({ gridSnap: e.target.checked })} /> Snap to the grid where no object snap takes the point</label>
              <p className="muted small">The grid is for the screen only: sheets, PDF and DXF never show it. In a UCS it turns with its axes.</p>
              <h4>Object snaps</h4>
              <div className="pref-checks">
                {SNAP_MODES.map((m) => (
                  <label key={m} className="row small"><input type="checkbox" data-field={`prefs-snap-${m}`} checked={p.snapModes.includes(m)}
                    onChange={(e) => setPrefs({ snapModes: e.target.checked ? [...p.snapModes, m] : p.snapModes.filter((k) => k !== m) })} /> {SNAP_LABELS[m]}</label>
                ))}
              </div>
              <p className="row small">
                <button type="button" className="btn" onClick={() => setPrefs({ snapModes: [...SNAP_MODES] })}>All</button>
                <button type="button" className="btn" onClick={() => setPrefs({ snapModes: [] })}>None</button>
              </p>
              <p className="muted small">F3 turns snapping on and off; a right click on SNAP under the view opens this list.</p>
              <h4>Polar tracking</h4>
              <label className="row small">Every <select value={p.polarStep} data-field="prefs-polar" onChange={(e) => setPrefs({ polarStep: Number(e.target.value) })}>
                {POLAR_STEPS.map((a) => <option key={a} value={a} data-no-i18n>{a}°</option>)}
              </select></label>
            </>}
            {page === 'coords' && <>
              <h4>Coordinates at the cursor</h4>
              <Radio name="Coordinates" value={p.coords} onChange={(v: CoordSystem) => setPrefs({ coords: v })} options={(['internal', 'project', 'survey', 'ucs'] as CoordSystem[]).map((c) => ({
                id: c, label: COORD_LABELS[c], disabled: (c === 'survey' && !surveyPoint(loc)) || (c === 'ucs' && !ucs),
              }))} />
              <p className="muted small">Shown in the status line while the cursor is in a plan; a click there chooses too.</p>
              <h4>The project's points</h4>
              <label className="row small"><input type="checkbox" data-field="prefs-location-shown" checked={!!loc.show} onChange={(e) => void setLocationShown(e.target.checked)} /> Show the internal origin, the base point and the survey point in plans and 3D</label>
              <p className="row small"><button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'location' })}>Location…</button></p>
              <h4>UCS</h4>
              <p className="small" data-no-i18n={ucs ? true : undefined}>{ucs ? `${formatLength(ucs.origin.x)}, ${formatLength(ucs.origin.y)} · ${Math.round(ucs.angle * 100) / 100}°` : 'World: the plan’s own axes.'}</p>
              <p className="row small">
                <button type="button" className="btn" onClick={() => { close(); void startCommand('UCS'); }}>Set in the view…</button>
                <button type="button" className="btn" disabled={!ucs} onClick={() => void setUcs(null)}>World</button>
              </p>
            </>}
            {page === 'display' && <>
              <h4>Display</h4>
              <div className="pref-checks">
                {DISPLAY.map((d) => (
                  <label key={d.key} className="row small"><input type="checkbox" checked={display[d.key]} onChange={(e) => setDisplay({ [d.key]: e.target.checked })} /> {d.label}</label>
                ))}
              </div>
              <p className="row small"><button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'displayQuality' })}>Display quality…</button></p>
            </>}
            {page === 'profiles' && <ProfilesPage />}
          </section>
        </div>
        <footer className="dialog-foot"><span className="muted small">Kept on this computer, for every project.</span><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
