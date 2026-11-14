/**
 * Annotate → Grid… (report #68): a whole grid from its spacings, in the
 * project's units: the axes across (numbered), the axes along (lettered),
 * where it starts, its angle, how far the axes run past it, its first labels,
 * and the axis type it is drawn in (Drafting standards → Grid axes); the
 * grid's axes so far kept or replaced. One undo step.
 */

import { Fragment, useEffect, useState } from 'react';
import { Settings2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { lengthText, useStandards } from '../../engine/unitsStore';
import { openStandards } from '../core/standardsTab';
import { gridLines, readSpacings } from './gridSpec';
import { axesOf, spacingOf } from '../../shell/gridAxes';
import type { NodeDto } from '../../engine/protocol';

interface AxisStyle { id: string; name: string }

export function GridDialog() {
  const units = useStandards((s) => s.units);
  const nodes = useSession((s) => s.nodes);
  const grid = [...nodes.values()].find((n) => n.type === 'IfcGrid');
  const existing = axesOf(grid?.props.axes).length;
  const metric = units !== 'ft-in' && units !== 'in';
  const [across, setAcross] = useState(metric ? (units === 'mm' ? '3*6000' : units === 'cm' ? '3*600' : '3*6') : "3*20'");
  const [along, setAlong] = useState(metric ? (units === 'mm' ? '2*5000' : units === 'cm' ? '2*500' : '2*5') : "2*16'");
  const [origin, setOrigin] = useState({ x: 0, y: 0 });
  const [angle, setAngle] = useState(0);
  const [extend, setExtend] = useState(1.5);
  const [firstX, setFirstX] = useState('1');
  const [firstY, setFirstY] = useState('A');
  const [replace, setReplace] = useState(existing > 0);
  const [styles, setStyles] = useState<{ list: AxisStyle[]; current: string }>({ list: [], current: 'standard' });
  useEffect(() => {
    let live = true;
    void query<{ axisStyles?: AxisStyle[]; defaultAxisStyle?: string }>('standards.get').then((b) => { if (live && b) setStyles({ list: b.axisStyles ?? [], current: b.defaultAxisStyle ?? 'standard' }); });
    return () => { live = false; };
  }, [nodes]);
  const x = readSpacings(across), y = readSpacings(along);
  const ok = !!x && !!y && x.length + y.length > 0;
  const lines = ok ? gridLines({ x: x!, y: y!, extend, angle, origin }) : [];
  const close = () => useUi.setState({ dialog: null });
  const make = async () => {
    if (!ok) return;
    const r = await execute('drafting.makeGrid', { x: x!, y: y!, origin: [origin.x, origin.y], angle, extend, firstX, firstY, replace });
    if (r) { notify('The grid is made: select an axis to set it.'); close(); }
  };
  // The preview: the axes in a box, y up.
  const pts = lines.flatMap((l) => [l.a, l.b]);
  const [minX, maxX, minY, maxY] = pts.length ? [Math.min(...pts.map((p) => p.x)), Math.max(...pts.map((p) => p.x)), Math.min(...pts.map((p) => p.y)), Math.max(...pts.map((p) => p.y))] : [0, 1, 0, 1];
  const span = Math.max(maxX - minX, maxY - minY, 1e-3);
  const sx = (v: number) => 10 + ((v - minX) / span) * 220, sy = (v: number) => 230 - ((v - minY) / span) * 220;
  const total = (list: number[] | null) => (list && list.length ? lengthText(list.reduce((a, b) => a + b, 0)) : '—');
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Axis grid">
      <div className="dialog" data-grid-dialog-body>
        <header className="dialog-head">
          <span className="panel-title">Axis grid</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body row" style={{ alignItems: 'flex-start', gap: 16 }}>
          <dl className="fields" style={{ flex: 1 }}>
            <dt title="The spacings between the numbered axes, left to right, in the project's units: 6000 6000 4500, or 3*6000">Across</dt>
            <dd><input className="cell num" style={{ minWidth: 200 }} value={across} data-grid-across onChange={(e) => setAcross(e.target.value)} aria-invalid={!x} /></dd>
            <dt>Width</dt><dd className="num muted">{total(x)}</dd>
            <dt title="The spacings between the lettered axes, bottom to top, in the project's units">Along</dt>
            <dd><input className="cell num" style={{ minWidth: 200 }} value={along} data-grid-along onChange={(e) => setAlong(e.target.value)} aria-invalid={!y} /></dd>
            <dt>Depth</dt><dd className="num muted">{total(y)}</dd>
            <dt title="Where the first axes cross">Origin</dt>
            <dd className="pair"><LengthField noUnit metres={origin.x} onCommit={(v) => { if (v != null) setOrigin({ ...origin, x: v }); }} /><LengthField metres={origin.y} onCommit={(v) => { if (v != null) setOrigin({ ...origin, y: v }); }} /></dd>
            <dt title="The grid turned about its origin, counter-clockwise">Angle</dt>
            <dd><NumberField value={angle} suffix="°" onCommit={(v) => { if (v != null) setAngle(v); }} /></dd>
            <dt title="How far each axis runs past the outer axes across it">Past the grid</dt>
            <dd><LengthField metres={extend} min={0} onCommit={(v) => { if (v != null) setExtend(v); }} /></dd>
            <dt title="The labels the axes start from: 1, A, A1…; each next one follows">First labels</dt>
            <dd className="pair"><input className="cell" value={firstX} data-no-i18n onChange={(e) => setFirstX(e.target.value)} aria-label="Across" /><input className="cell" value={firstY} data-no-i18n onChange={(e) => setFirstY(e.target.value)} aria-label="Along" /></dd>
            <dt title="How axes are drawn: bubbles, ends, size, line type (Drafting standards → Grid axes)">Axis type</dt>
            <dd style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <select value={styles.current} onChange={(e) => void execute('standards.set', { kind: 'axis', entry: { id: e.target.value }, default: true })}>
                {styles.list.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
              <button type="button" className="icon" title="Axis styles…" onClick={() => openStandards('axis')}><Settings2 size={14} /></button>
            </dd>
            {existing > 0 && <>
              <dt />
              <dd><label><input type="checkbox" checked={replace} onChange={(e) => setReplace(e.target.checked)} /> replace the grid's axes</label></dd>
            </>}
          </dl>
          <svg width={240} height={240} className="grid-preview" aria-label="Preview">
            {lines.map((l, i) => <line key={i} x1={sx(l.a.x)} y1={sy(l.a.y)} x2={sx(l.b.x)} y2={sy(l.b.y)} stroke="currentColor" strokeWidth={1} strokeDasharray="6 2 1 2" />)}
          </svg>
        </div>
        <footer className="dialog-foot">
          {!ok && <span className="error-text">Spacings are lengths above zero.</span>}
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-grid-make disabled={!ok} onClick={() => void make()}>Make the grid</button>
        </footer>
      </div>
    </div>
  );
}

/** The whole grid in Properties (report #68): its axes, each with its spacing from the one before, and the way to its settings. */
export function GridSection({ node }: { node: NodeDto }) {
  useStandards((s) => s.units);
  const axes = axesOf(node.props.axes);
  return (
    <>
      <dl className="fields" data-grid-section>
        {axes.map((a) => {
          const gap = spacingOf(axes, a.tag);
          return <Fragment key={a.tag}><dt data-no-i18n>{a.tag}</dt><dd className="num muted">{gap ? `↔ ${gap.prev}: ${lengthText(gap.spacing)}` : '—'}</dd></Fragment>;
        })}
      </dl>
      <p className="muted small">Click an axis in a plan to set its ends, length, angle and spacing.</p>
      <div className="row">
        <button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'grid' })}>Axis grid…</button>
        <button type="button" className="btn" title="The axes renumbered in order across them" onClick={() => useUi.setState({ dialog: 'renumberAxes' })}>Renumber axes…</button>
        <button type="button" className="btn" onClick={() => openStandards('axis')}>Axis styles…</button>
      </div>
    </>
  );
}
