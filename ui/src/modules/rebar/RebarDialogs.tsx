/** The project's rebar drawing settings, and the choice of a view to inset where the user clicked. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { useRebarCatalog } from './RebarSection';

interface Settings { colorByDiameter: boolean; textHeight: number; balloon: number; steel: string; segmentLengths: boolean; cover: number }

export function RebarSettingsDialog() {
  const catalog = useRebarCatalog();
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<Settings | null>(null);
  useEffect(() => { let live = true; void query<Settings>('rebar.settings', {}).then((r) => { if (live && r) setS(r); }); return () => { live = false; }; }, [nodes]);
  const close = () => useUi.setState({ dialog: null });
  if (!s || !catalog) return null;
  const set = (patch: Partial<Settings>) => { setS({ ...s, ...patch }); void execute('rebar.setSettings', patch); };
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
