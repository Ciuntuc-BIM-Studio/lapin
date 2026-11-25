/** The project's rebar drawing settings, and the choice of a view to inset where the user clicked. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { useRebarCatalog } from './RebarSection';
import { steelChoices, useRebarRules } from './rebarRules';
import { LengthField, NumberField } from '../../shell/cells';
import { NotationFields } from '../../shell/NotationFields';
import { LabelStyleFields, MARK_GROUPS, useRebarSettings, type LabelKind, type MarkGroup, type LabelStylePatch, type RebarSettings as Settings } from './LabelStyleFields';

const KINDS: { id: LabelKind; label: string }[] = [{ id: 'bars', label: 'Bars' }, { id: 'stirrups', label: 'Stirrups on the type' }, { id: 'columnBars', label: 'Column bars' }, { id: 'runs', label: 'Distributions' }, { id: 'sets', label: 'Rebar sets' }];

/** The stock length bars come in (6 m, 12 m, or any) and the lap a split bar overlaps by (50Ø, or a value), decision 227. */
function StockAndLapFields({ s, set }: { s: Settings; set: (patch: Partial<Settings>) => void }) {
  const stock = s.stockLength ?? 12;
  const standard = stock === 6 || stock === 12;
  // "Another length" is picked on purpose (a state), but a stock length that is not 6 or 12 (set, or brought back by an undo) is always shown as one.
  const [pickedCustom, setCustom] = useState(false);
  useEffect(() => setCustom(!standard), [stock]); // eslint-disable-line react-hooks/exhaustive-deps
  const stockChoice = pickedCustom || !standard ? 'custom' : String(stock);
  return (
    <div data-field="rebar-stock">
      <label className="field-line"><span className="eyebrow">Stock length</span>
        <select value={stockChoice} onChange={(e) => { if (e.target.value === 'custom') setCustom(true); else { setCustom(false); set({ stockLength: Number(e.target.value) }); } }}>
          <option value="6">6 m</option><option value="12">12 m</option><option value="custom">Another length</option>
        </select>
        {stockChoice === 'custom' && <LengthField metres={stock} min={1} max={24} onCommit={(m) => { if (m != null) set({ stockLength: m }); }} />}
      </label>
      <label className="field-line"><span className="eyebrow">Lap length</span>
        <select value={s.lapLength == null ? 'dia' : 'value'} onChange={(e) => set({ lapLength: e.target.value === 'dia' ? null : 0.8 })}>
          <option value="dia">50Ø</option><option value="value">A length</option>
        </select>
        {s.lapLength != null && <LengthField metres={s.lapLength} min={0.01} max={5} onCommit={(m) => { if (m != null) set({ lapLength: m }); }} />}
      </label>
    </div>
  );
}

const GROUP_NAMES: Record<MarkGroup, string> = { straight: 'Straight bars', L: 'L bars', U: 'U bars', stirrup: 'Stirrups', tie: 'Ties', polyline: 'Polyline bars', other: 'Other shapes' };

/** A text applied when left or on Enter (a pattern half typed is not sent). */
function CommitText({ value, onCommit, placeholder, width, testId }: { value: string; onCommit: (v: string) => void; placeholder?: string; width?: number; testId?: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return <input className="cell" type="text" value={text} placeholder={placeholder} spellCheck={false} data-no-i18n data-field={testId} style={{ width: width ?? 110 }}
    onChange={(e) => setText(e.target.value)} onBlur={() => { if (text !== value) onCommit(text); }}
    onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') setText(value); e.stopPropagation(); }} />;
}

/** How bars are marked (decision 729): the pattern, a prefix for each shape group, renumbering. */
function MarksBlock({ s }: { s: Settings }) {
  const marks = s.marks;
  const [note, setNote] = useState('');
  if (!marks) return null;
  const renumber = () => void execute('rebar.renumber', {}).then((r) => { const n = (r as { changed?: number } | null)?.changed; if (typeof n === 'number') setNote(`${n}`); });
  return (
    <div data-field="rebar-marks">
      <p className="eyebrow">Marks</p>
      <label className="field-line"><span className="eyebrow">Mark pattern</span>
        <CommitText value={marks.pattern} placeholder="{prefix}{n}" width={150} testId="rebar-mark-pattern" onCommit={(v) => void execute('rebar.setSettings', { marks: { pattern: v.trim() || null } })} />
      </label>
      <p className="muted small">{'Tokens: {prefix} {n} {partition} {d} {shape}; a formula such as {= n | 00} pads the number.'}</p>
      {MARK_GROUPS.map((g) => (
        <label key={g} className="field-line"><span className="eyebrow">{GROUP_NAMES[g]}</span>
          <CommitText value={marks.prefixes?.[g] ?? ''} width={80} testId={`rebar-mark-prefix-${g}`} onCommit={(v) => void execute('rebar.setSettings', { marks: { prefixes: { [g]: v.trim() || null } } })} />
        </label>
      ))}
      <p className="muted small">Marks are numbered apart in each prefix and partition: R1, R2… beside U1, U2…. Numbers already given never change by themselves.</p>
      <div className="row small"><button type="button" className="btn" data-action="rebar-renumber" onClick={renumber}>Renumber marks</button>{note && <span className="muted small num">{note}</span>}</div>
    </div>
  );
}

export function RebarSettingsDialog() {
  const catalog = useRebarCatalog();
  const rules = useRebarRules();
  const live = useRebarSettings();
  const [edited, setEdited] = useState<Settings | null>(null);
  const [kind, setKind] = useState<LabelKind>('bars');
  // What was typed shows at once; the engine's answer replaces it as soon as it comes.
  useEffect(() => setEdited(null), [live]);
  const close = () => useUi.setState({ dialog: null });
  const s = edited ?? live;
  if (!s || !catalog) return null;
  const set = (patch: Partial<Settings>) => { setEdited({ ...s, ...patch }); void execute('rebar.setSettings', patch); };
  const setLabel = (patch: LabelStylePatch) => { setEdited(null); void execute('rebar.setSettings', { labels: { [kind]: patch } }); };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Rebar settings">
      <div className="dialog narrow">
        <header className="dialog-head">
          <span className="panel-title">Rebar settings</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <label className="field-line check"><input type="checkbox" checked={s.colorByDiameter} onChange={(e) => set({ colorByDiameter: e.target.checked })} /><span>A colour for each diameter</span></label>
          <label className="field-line check"><input type="checkbox" checked={s.segmentLengths} onChange={(e) => set({ segmentLengths: e.target.checked })} /><span>Segment lengths written on the bars</span></label>
          <label className="field-line"><span className="eyebrow">Text height</span><NumberField value={s.textHeight} min={1} max={10} onCommit={(n) => { if (n != null) set({ textHeight: n }); }} /><span className="muted small"> mm on paper</span></label>
          <label className="field-line"><span className="eyebrow">Mark balloon</span><NumberField value={s.balloon} min={3} max={20} onCommit={(n) => { if (n != null) set({ balloon: n }); }} /><span className="muted small"> mm on paper</span></label>
          <label className="field-line"><span className="eyebrow">Steel</span>
            <select value={s.steel} onChange={(e) => set({ steel: e.target.value })}>{steelChoices(rules, catalog.steels, s.steel).map((x) => <option key={x} value={x}>{x}</option>)}</select>
          </label>
          <label className="field-line"><span className="eyebrow">Cover</span><LengthField metres={s.cover} min={0} max={0.2} onCommit={(m) => { if (m != null) set({ cover: m }); }} /></label>
          <p className="muted small">Bars are drawn as thick as they are; bend radii follow SR EN 1992-1-1 (a mandrel of 4Ø up to Ø16, 7Ø over). The schedule's lengths are the nominal ones, hooks included.</p>
          <StockAndLapFields s={s} set={set} />
          <p className="muted small">A bar longer than the stock length is cut in two with Split bar, the lap where you put it (from one station to another along the bar); this lap length is taken when only one point is given. Each piece has its own mark in the schedule.</p>
          <MarksBlock s={s} />
          <NotationFields domain="rebar" />
          <p className="eyebrow">Labels</p>
          <label className="field-line"><span className="eyebrow">For</span>
            <select value={kind} onChange={(e) => setKind(e.target.value as LabelKind)}>{KINDS.map((k) => <option key={k.id} value={k.id}>{k.label}</option>)}</select>
          </label>
          <LabelStyleFields key={kind} style={s.labels[kind]} onChange={setLabel} testId="rebar-settings-label" withLine={kind === 'stirrups' || kind === 'runs'} />
          <p className="muted small">A type and a bar may change any field of theirs; a mark's balloon is never drawn empty. Drag a label by its grip; a dragged one keeps its place on that view. Drag the leader's middle to bend it, a string's middle to move it off the bars.</p>
        </div>
      </div>
    </div>
  );
}

/** The inset view's picker is the core's now, for every view (decision 576): re-exported for what imported it from here. */
export { InsetPickDialog } from '../core/InsetPickDialog';
