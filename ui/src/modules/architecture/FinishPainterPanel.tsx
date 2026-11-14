/**
 * The finish painter's panel (forum topic 83): its tools, Whole face, and the palette — the project's surface finishes, its
 * room finishes split into their walls, floor and ceiling, the library's not yet brought in, and a layer of one material
 * with a thickness. Shown over the view while the painter's edit mode is on.
 */

import { useEffect, useState } from 'react';
import { CircleCheckBig } from 'lucide-react';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { useScope } from '../../platform/scope';
import { activate } from '../../platform/modules';
import { leavePainter, paintBrush, paintClear, paintEyedropper, paletteQuery, PAINTER, splitLine, splitRect, usePainter, type PaletteEntry, type PaintFinish } from './finishPainter';

const mm = (layers: { thickness: number }[]) => `${Math.round(layers.reduce((s, l) => s + l.thickness, 0) * 1000 * 10) / 10} mm`;

const sameFinish = (a: PaintFinish | null, e: PaletteEntry) =>
  !!a && (e.library ? a.library && a.name === e.name : !!a.typeId && a.typeId === e.typeId && (a.part ?? null) === (e.part ?? null));

export function FinishPainterPanel() {
  const scope = useScope((s) => s.scope);
  const on = scope?.kind === PAINTER;
  const nodes = useSession((s) => s.nodes);
  const tool = useUi((s) => s.tool);
  const { finish, whole } = usePainter();
  const [palette, setPalette] = useState<PaletteEntry[] | null>(null);
  const [material, setMaterial] = useState('Paint');
  const [thickness, setThickness] = useState('1');
  useEffect(() => {
    if (!on) return;
    let live = true;
    void paletteQuery().then((r) => { if (live) setPalette(r ?? []); });
    return () => { live = false; };
  }, [on, nodes]);
  if (!on) return null;
  const materials = ofClass(useSession.getState(), 'IfcMaterial').map((n) => n.name).sort();
  const groups: [string, PaletteEntry[]][] = [
    ['Surface finishes', (palette ?? []).filter((e) => e.kind === 'surface' && !e.library)],
    ['Room finishes', (palette ?? []).filter((e) => e.kind === 'room')],
    ['Library', (palette ?? []).filter((e) => e.library)],
  ];
  const own = () => {
    const t = Number(thickness.replace(',', '.'));
    if (!material.trim() || !Number.isFinite(t) || t < 0) return;
    usePainter.setState({ finish: { name: `${material.trim()} ${t} mm`, layers: [{ material: material.trim(), thickness: t / 1000 }] } });
    activate(paintBrush);
  };
  const tools = [paintBrush, splitRect, splitLine, paintEyedropper, paintClear];
  return (
    <div className="sel-card finish-painter" role="region" aria-label="Finish painter" data-testid="finish-painter">
      <div className="row between">
        <b>Finish painter</b>
        <button type="button" className="btn small" data-testid="finish-painter-done" title="Leave the finish painter (Esc)" onClick={leavePainter}><CircleCheckBig size={12} />Done</button>
      </div>
      <div className="row wrap finish-painter-tools">
        {tools.map((t) => {
          const Icon = t.icon;
          return (
            <button key={t.id} type="button" className={`icon${tool === t.id ? ' on' : ''}`} data-testid={t.id} title={t.label} aria-label={t.label} onClick={() => activate(t)}>
              <Icon size={16} />
            </button>
          );
        })}
      </div>
      <label className="row small" title="The brush and Clear take the whole face, not the region under the cursor">
        <input type="checkbox" checked={whole} onChange={(e) => usePainter.setState({ whole: e.target.checked })} />Whole face
      </label>
      <p className="muted small">{finish ? <>Painting with <b data-no-i18n>{finish.name}</b></> : 'Choose a finish, then click a face'}</p>
      {!palette && <p className="muted small">Reading the palette…</p>}
      {groups.filter(([, list]) => list.length > 0).map(([title, list]) => (
        <div key={title} className="part-section">
          <div className="part-title">{title}</div>
          <ul className="part-list">
            {list.map((e) => (
              <li key={e.key} className={sameFinish(finish, e) ? 'on' : ''}>
                <button type="button" className="link" data-palette={e.name}
                  onClick={() => { usePainter.setState({ finish: { name: e.name, typeId: e.typeId, part: e.part, library: e.library } }); if (tool !== paintBrush.id) activate(paintBrush); }}>
                  <span data-no-i18n>{e.name}</span>
                </button>
                <span className="muted small" title={e.layers.map((l) => `${l.material} ${Math.round(l.thickness * 10000) / 10} mm`).join(' + ')}>{mm(e.layers)}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}
      <div className="part-section">
        <div className="part-title">A material</div>
        <div className="row finish-painter-own">
          <input className="input small" list="finish-painter-materials" value={material} onChange={(e) => setMaterial(e.target.value)} aria-label="Material" />
          <input className="input small num" value={thickness} onChange={(e) => setThickness(e.target.value)} aria-label="Thickness (mm)" title="Thickness (mm)" style={{ width: 56 }} />
          <button type="button" className="btn small" onClick={own}>Paint with it</button>
        </div>
        <datalist id="finish-painter-materials">{materials.map((m) => <option key={m} value={m} />)}</datalist>
      </div>
    </div>
  );
}
