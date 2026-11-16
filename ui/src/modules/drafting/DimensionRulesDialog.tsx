/**
 * Annotate → Dimension rules… (forum #78): the user's settings for Exterior dimensions and Room dimensions: which strings
 * are made and in what order, how far from the wall and from each other (millimetres on paper), the dimension style, what
 * the corners are measured to, and the narrowest opening worth a point. Stored on the project (`drafting.setDimensionRules`,
 * one undo step) and carried in the drafting standards. Below the tabs, the project's automatic placement of dimension
 * values that do not fit (forum #78: `standards.options` dimAutoText, dimTextLeader), saved in the same undo step.
 */

import { NumberField } from '../../shell/cells';
import { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { query } from '../../engine/session';
import { runSteps } from '../../viewport/grips';

type ExteriorString = 'details' | 'openings' | 'axes' | 'rooms' | 'overall';
type RoomString = 'details' | 'overall';
type Measure = 'faces' | 'core' | 'axes';
interface ExteriorRules { strings: { kind: ExteriorString; on: boolean }[]; offset: number; spacing: number; dimStyle: string | null; measureTo: Measure; minOpening: number }
interface RoomRules { strings: { kind: RoomString; on: boolean }[]; offset: number; spacing: number; dimStyle: string | null; minOpening: number }
interface Rules { exterior: ExteriorRules; rooms: RoomRules; defaults: { exterior: ExteriorRules; rooms: RoomRules } }
type StyleEntry = { id: string; name: string };
/** The project's automatic text placement (forum #78): on or off, and past how many paper mm a moved value takes a leader. */
interface Placement { dimAutoText: boolean; dimTextLeader: number }
const PLACEMENT_DEFAULT: Placement = { dimAutoText: true, dimTextLeader: 5 };

const EXTERIOR_LABEL: Record<ExteriorString, [string, string]> = {
  details: ['Details', 'Every feature on the facade: corners and the openings\' jambs'],
  openings: ['Openings', 'From the facade\'s start to each door\'s and window\'s centre, and to its end'],
  axes: ['Axes', 'From the start to each grid axis crossing the facade, and to the end'],
  rooms: ['Rooms', 'The thicknesses of the walls meeting the facade from inside and the distances between them'],
  overall: ['Overall', 'The whole facade'],
};
const ROOM_LABEL: Record<RoomString, [string, string]> = {
  details: ['Details', 'The side with the openings\' jambs'],
  overall: ['Overall', 'The side\'s whole length'],
};

/** The strings of one kind of rules: a toggle each and the order, first being nearest the wall (exterior) or the side (rooms). */
function StringList<K extends string>({ items, labels, onChange }: { items: { kind: K; on: boolean }[]; labels: Record<K, [string, string]>; onChange: (v: { kind: K; on: boolean }[]) => void }) {
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= items.length) return;
    const next = items.slice();
    [next[i], next[j]] = [next[j], next[i]];
    onChange(next);
  };
  return (
    <ol className="dim-rules-strings" data-dim-rules-strings style={{ listStyle: 'none', margin: 0, padding: 0 }}>
      {items.map((s, i) => (
        <li key={s.kind} style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '2px 0' }}>
          <label title={labels[s.kind][1]} style={{ flex: 1 }}>
            <input type="checkbox" checked={s.on} onChange={(e) => onChange(items.map((x, k) => (k === i ? { ...x, on: e.target.checked } : x)))} /> {labels[s.kind][0]}
          </label>
          <button type="button" className="icon" title="Move up" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={14} /></button>
          <button type="button" className="icon" title="Move down" disabled={i === items.length - 1} onClick={() => move(i, 1)}><ArrowDown size={14} /></button>
        </li>
      ))}
    </ol>
  );
}


export function DimensionRulesDialog() {
  const [tab, setTab] = useState<'exterior' | 'rooms'>('exterior');
  const [rules, setRules] = useState<Rules | null>(null);
  const [ext, setExt] = useState<ExteriorRules | null>(null);
  const [room, setRoom] = useState<RoomRules | null>(null);
  const [styles, setStyles] = useState<StyleEntry[]>([]);
  const [placement, setPlacement] = useState<Placement | null>(null);
  const [placementWas, setPlacementWas] = useState<Placement | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void query<Rules>('drafting.dimensionRules').then((r) => { if (live && r) { setRules(r); setExt(r.exterior); setRoom(r.rooms); } });
    void query<{ dimStyles?: StyleEntry[]; dimAutoText?: boolean; dimTextLeader?: number }>('standards.get').then((b) => {
      if (!live || !b) return;
      if (b.dimStyles) setStyles(b.dimStyles);
      const p = { dimAutoText: b.dimAutoText !== false, dimTextLeader: typeof b.dimTextLeader === 'number' ? b.dimTextLeader : PLACEMENT_DEFAULT.dimTextLeader };
      setPlacement(p);
      setPlacementWas(p);
    });
    return () => { live = false; };
  }, []);
  const save = async () => {
    if (!ext || !room) return;
    const changed = placement && placementWas && (placement.dimAutoText !== placementWas.dimAutoText || placement.dimTextLeader !== placementWas.dimTextLeader);
    const r = await runSteps([
      { command: 'drafting.setDimensionRules', args: { exterior: ext, rooms: room } },
      changed ? { command: 'standards.options', args: { ...placement } } : null,
    ]);
    if (r !== null) { notify('Dimension rules saved.'); close(); }
  };
  const reset = () => { if (rules) { setExt(rules.defaults.exterior); setRoom(rules.defaults.rooms); setPlacement(PLACEMENT_DEFAULT); } };

  const common = <T extends ExteriorRules | RoomRules>(v: T, set: (v: T) => void) => (<>
    <dt title="Millimetres on paper, from the wall's face to the first string">Offset (mm on paper)</dt>
    <dd><NumberField min={0} value={v.offset} testId="dim-offset" onCommit={(n) => { if (n != null) set({ ...v, offset: n }); }} /></dd>
    <dt title="Millimetres on paper between one string and the next">Spacing (mm on paper)</dt>
    <dd><NumberField min={0} value={v.spacing} testId="dim-spacing" onCommit={(n) => { if (n != null) set({ ...v, spacing: n }); }} /></dd>
    <dt title="A dimension style of the drafting standards">Dimension style</dt>
    <dd><select data-dim-style value={v.dimStyle ?? ''} onChange={(e) => set({ ...v, dimStyle: e.target.value || null })}>
      <option value="">Default style</option>
      {styles.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
    </select></dd>
  </>);
  const minOpening = <T extends ExteriorRules | RoomRules>(v: T, set: (v: T) => void) => (<>
    <dt title="Openings narrower than this get no points; 0 keeps them all">Skip openings narrower than (m)</dt>
    <dd><NumberField min={0} value={v.minOpening} testId="dim-min-opening" onCommit={(n) => { if (n != null) set({ ...v, minOpening: n }); }} /></dd>
  </>);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Dimension rules">
      <div className="dialog" data-dimension-rules>
        <header className="dialog-head">
          <span className="panel-title">Dimension rules</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          {!ext || !room ? <p className="muted">Loading…</p> : (<>
            <div className="seg" style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
              <button type="button" className={`btn${tab === 'exterior' ? ' primary' : ''}`} data-dim-tab="exterior" onClick={() => setTab('exterior')}>Exterior dimensions</button>
              <button type="button" className={`btn${tab === 'rooms' ? ' primary' : ''}`} data-dim-tab="rooms" onClick={() => setTab('rooms')}>Room dimensions</button>
            </div>
            {tab === 'exterior' ? (<>
              <p className="muted small">Strings, from the wall outward</p>
              <StringList items={ext.strings} labels={EXTERIOR_LABEL} onChange={(strings) => setExt({ ...ext, strings })} />
              <dl className="fields">
                {common(ext, setExt)}
                <dt title="What corners and partitions are measured to">Measure to</dt>
                <dd><select data-dim-measure value={ext.measureTo} onChange={(e) => setExt({ ...ext, measureTo: e.target.value as Measure })}>
                  <option value="faces">Faces</option>
                  <option value="core">Core</option>
                  <option value="axes">Axes</option>
                </select></dd>
                {minOpening(ext, setExt)}
              </dl>
            </>) : (<>
              <p className="muted small">Strings, from the side inward</p>
              <StringList items={room.strings} labels={ROOM_LABEL} onChange={(strings) => setRoom({ ...room, strings })} />
              <dl className="fields">
                {common(room, setRoom)}
                {minOpening(room, setRoom)}
              </dl>
            </>)}
            {placement && (<>
              <p className="muted small">Text placement, for every dimension whose style says as the project</p>
              <dl className="fields" data-dim-placement>
                <dt title="A value that does not fit between its witness lines moves just outside its segment, or above and below the line in turn, clear of the other values of the view; a value you dragged keeps its place">Automatic text placement</dt>
                <dd><input type="checkbox" data-dim-auto-text checked={placement.dimAutoText} onChange={(e) => setPlacement({ ...placement, dimAutoText: e.target.checked })} /></dd>
                <dt title="A value moved further than this from its place gets a leader to its segment">Leader when moved beyond (mm on paper)</dt>
                <dd><NumberField min={0} max={100} value={placement.dimTextLeader} testId="dim-text-leader" readOnly={!placement.dimAutoText}
                  onCommit={(n) => { if (n != null) setPlacement({ ...placement, dimTextLeader: n }); }} /></dd>
              </dl>
            </>)}
          </>)}
        </div>
        <footer className="dialog-foot">
          <button type="button" data-dim-reset disabled={!rules} onClick={reset}>Reset to defaults</button>
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-dim-save disabled={!ext || !room} onClick={() => void save()}>OK</button>
        </footer>
      </div>
    </div>
  );
}
