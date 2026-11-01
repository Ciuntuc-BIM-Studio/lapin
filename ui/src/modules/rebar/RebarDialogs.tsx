/** The project's rebar drawing settings, and the choice of a view to inset where the user clicked. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { useRebarCatalog } from './RebarSection';
import { LabelStyleFields, useRebarSettings, type LabelKind, type LabelStylePatch, type RebarSettings as Settings } from './LabelStyleFields';

const KINDS: { id: LabelKind; label: string }[] = [{ id: 'bars', label: 'Bars' }, { id: 'stirrups', label: 'Stirrups on the type' }, { id: 'columnBars', label: 'Column bars' }, { id: 'runs', label: 'Distributions' }];

/** The stock length bars come in (6 m, 12 m, or any) and the lap a split bar overlaps by (50Ø, or a value), decision 227. */
function StockAndLapFields({ s, set }: { s: Settings; set: (patch: Partial<Settings>) => void }) {
  const stock = s.stockLength ?? 12;
  const standard = stock === 6 || stock === 12;
  const [custom, setCustom] = useState(!standard);
  const stockChoice = custom || !standard ? 'custom' : String(stock);
  return (
    <div data-field="rebar-stock">
      <label className="field-line"><span className="eyebrow">Stock length</span>
        <select value={stockChoice} onChange={(e) => { if (e.target.value === 'custom') setCustom(true); else { setCustom(false); set({ stockLength: Number(e.target.value) }); } }}>
          <option value="6">6 m</option><option value="12">12 m</option><option value="custom">Another length</option>
        </select>
        {stockChoice === 'custom' && <><input className="num" type="number" step={0.5} min={1} max={24} value={stock} onChange={(e) => { const v = Number(e.target.value); if (v >= 1 && v <= 24) set({ stockLength: v }); }} /><span className="muted small"> m</span></>}
      </label>
      <label className="field-line"><span className="eyebrow">Lap length</span>
        <select value={s.lapLength == null ? 'dia' : 'value'} onChange={(e) => set({ lapLength: e.target.value === 'dia' ? null : 0.8 })}>
          <option value="dia">50Ø</option><option value="value">A length</option>
        </select>
        {s.lapLength != null && <><input className="num" type="number" step={10} min={10} max={5000} value={Math.round(s.lapLength * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 10 && v <= 5000) set({ lapLength: v / 1000 }); }} /><span className="muted small"> mm</span></>}
      </label>
    </div>
  );
}

export function RebarSettingsDialog() {
  const catalog = useRebarCatalog();
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
          <label className="field-line"><span className="eyebrow">Text height</span><input className="num" type="number" step={0.5} min={1} max={10} value={s.textHeight} onChange={(e) => { const v = Number(e.target.value); if (v >= 1 && v <= 10) set({ textHeight: v }); }} /><span className="muted small"> mm on paper</span></label>
          <label className="field-line"><span className="eyebrow">Mark balloon</span><input className="num" type="number" step={0.5} min={3} max={20} value={s.balloon} onChange={(e) => { const v = Number(e.target.value); if (v >= 3 && v <= 20) set({ balloon: v }); }} /><span className="muted small"> mm on paper</span></label>
          <label className="field-line"><span className="eyebrow">Steel</span>
            <select value={s.steel} onChange={(e) => set({ steel: e.target.value })}>{catalog.steels.map((x) => <option key={x} value={x}>{x}</option>)}</select>
          </label>
          <label className="field-line"><span className="eyebrow">Cover</span><input className="num" type="number" step={5} min={0} max={200} value={Math.round(s.cover * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 0 && v <= 200) set({ cover: v / 1000 }); }} /><span className="muted small"> mm</span></label>
          <p className="muted small">Bars are drawn as thick as they are; bend radii follow SR EN 1992-1-1 (a mandrel of 4Ø up to Ø16, 7Ø over). The schedule's lengths are the nominal ones, hooks included.</p>
          <StockAndLapFields s={s} set={set} />
          <p className="muted small">A bar longer than the stock length is cut in two with Split bar, the lap where you put it (from one station to another along the bar); this lap length is taken when only one point is given. Each piece has its own mark in the schedule.</p>
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

/** The views that can be inset where the user clicked (the host itself left out); a click on one makes the inset. */
export function InsetPickDialog() {
  const nodes = useSession((s) => s.nodes);
  const at = useUi((s) => s.insetAt);
  const close = () => useUi.setState({ dialog: null, insetAt: null });
  if (!at) return null;
  const views = [...nodes.values()].filter((n) => n.type === 'LapinView' && ['section', 'elevation', 'plan', 'plane'].includes(String(n.props.kind)) && n.id !== at.viewId);
  const choose = async (id: string) => {
    const r = await execute('view.inset', { viewId: at.viewId, source: id, at: [at.x, at.y] }) as { id?: string } | null;
    close();
    if (r?.id) useUi.setState({ selected: [r.id] });
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Inset view">
      <div className="dialog narrow">
        <header className="dialog-head">
          <span className="panel-title">Inset view</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">The view drawn where you clicked, at real size, so bars drawn over it fit.</p>
          {views.length === 0 && <p className="muted small">There is no other view to show. Make a section first.</p>}
          <ul className="type-list">
            {views.map((v) => <li key={v.id}><button type="button" className="tree-item" onClick={() => void choose(v.id)}><span data-no-i18n>{v.name}</span><span className="muted small"> · {String(v.props.kind)}</span></button></li>)}
          </ul>
        </div>
      </div>
    </div>
  );
}
