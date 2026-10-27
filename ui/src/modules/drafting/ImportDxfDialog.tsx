/**
 * A DXF into the view in front, as Lapin's own drafting: the file's units (or
 * a guess from its extents), where its origin lands, a turn, whether the view
 * mirrors it (a section or elevation looking towards −X or −Y does, so what
 * the file drew from the left still reads from the left), an extra factor,
 * which layers, and whether texts, hatches and dimensions come along.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { dxfFile } from '../../engine/files';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';

interface Info {
  version: string;
  units: { factor: number; name: string; guessed: boolean };
  extents: { min: number[]; max: number[] } | null;
  counts: Record<string, number>;
  unsupported: Record<string, number>;
  layers: { name: string; color: string; visible: boolean; locked: boolean; count: number; lineType: string }[];
  blocks: number;
  view: { kind: string; scale: number; flip: boolean } | null;
}

interface Result { file: string; entities: number; shapes: number; hatches: number; texts: number; dimensions: number; layers: string[]; patterns: string[]; skipped: Record<string, number>; units: string; unitsGuessed: boolean; flipped: boolean }

const UNITS = [['auto', 'as the file says'], ['mm', 'millimetres'], ['cm', 'centimetres'], ['m', 'metres'], ['in', 'inches'], ['ft', 'feet']];
const fmt = (v: number) => (Math.abs(v) >= 100 ? Math.round(v).toString() : v.toFixed(2));

export function ImportDxfDialog() {
  const file = dxfFile.current;
  const nodes = useSession((s) => s.nodes);
  const [viewId, setViewId] = useState<string | null>(null);
  const [info, setInfo] = useState<Info | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [units, setUnits] = useState('auto');
  const [at, setAt] = useState({ x: '0', y: '0' });
  const [rotation, setRotation] = useState('0');
  const [flip, setFlip] = useState<'auto' | 'yes' | 'no'>('auto');
  const [scale, setScale] = useState('1');
  const [texts, setTexts] = useState(true);
  const [hatches, setHatches] = useState(true);
  const [dimensions, setDimensions] = useState('native');
  const [joinDimensions, setJoinDimensions] = useState(true);
  const [link, setLink] = useState(false);
  const [off, setOff] = useState<Set<string>>(new Set());
  const [busy, setBusy] = useState(false);
  const close = () => { dxfFile.current = null; useUi.setState({ dialog: null }); };

  useEffect(() => {
    if (!file) return;
    let alive = true;
    void (async () => {
      const id = await frontView();
      if (!alive) return;
      if (!id || id === '3d') { setProblem('Open a plan, a section or an elevation to import into.'); return; }
      setViewId(id);
      try {
        const r = await query<Info>('drafting.dxfInfo', { ...file, viewId: id });
        if (alive && r) setInfo(r);
      } catch (e) {
        if (alive) setProblem(e instanceof Error ? e.message : String(e));
      }
    })();
    return () => { alive = false; };
  }, [file]);

  if (!file) return null;
  const view = viewId ? nodes.get(viewId) : undefined;
  const willFlip = flip === 'auto' ? Boolean(info?.view?.flip) : flip === 'yes';
  const extent = info?.extents ? `${fmt(info.extents.max[0] - info.extents.min[0])} × ${fmt(info.extents.max[1] - info.extents.min[1])} units` : null;
  const run = async () => {
    if (!viewId || busy) return;
    setBusy(true);
    try {
      const layers = info ? info.layers.map((l) => l.name).filter((n) => !off.has(n)) : undefined;
      const r = await execute('drafting.importDxf', {
        ...file, viewId,
        units: units === 'auto' ? 'auto' : units,
        at: [Number(at.x.replace(',', '.')) / 1000 || 0, Number(at.y.replace(',', '.')) / 1000 || 0],
        rotation: Number(rotation.replace(',', '.')) || 0,
        ...(flip === 'auto' ? {} : { flip: flip === 'yes' }),
        scale: Number(scale.replace(',', '.')) || 1,
        ...(layers && off.size ? { layers } : {}),
        texts, hatches, dimensions, joinDimensions, link,
      }) as Result | null;
      if (r) {
        const parts = [r.shapes ? `${r.shapes} shapes` : '', r.hatches ? `${r.hatches} hatches` : '', r.texts ? `${r.texts} texts` : '', r.dimensions ? `${r.dimensions} dimensions` : ''].filter(Boolean);
        const skipped = Object.entries(r.skipped ?? {});
        notify(`Imported ${r.file} (${r.units}${r.flipped ? ', mirrored' : ''}): ${parts.join(', ') || 'nothing'}${r.layers.length ? `; ${r.layers.length} new layers` : ''}${r.patterns.length ? `; patterns ${r.patterns.join(', ')}` : ''}.${skipped.length ? ` Left out: ${skipped.map(([k, n]) => `${n} ${k}`).join(', ')}.` : ''}`);
      }
      close();
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Import DXF" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Import DXF · {file.name}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {problem && <p className="muted">{problem}</p>}
          {!problem && !info && <p className="muted">Reading the file…</p>}
          {info && (
            <>
              <dl className="fields">
                <dt>Into</dt><dd>{view ? `${view.name} (${info.view?.kind ?? 'view'}, 1:${info.view?.scale ?? 100})` : '…'}</dd>
                <dt>File</dt><dd className="muted small">{info.version || 'DXF'}{extent ? ` · ${extent}` : ''}{info.blocks ? ` · ${info.blocks} blocks` : ''}</dd>
                <dt>Units</dt>
                <dd>
                  <select value={units} onChange={(e) => setUnits(e.target.value)}>
                    {UNITS.map(([v, l]) => <option key={v} value={v}>{v === 'auto' ? `${info.units.guessed ? 'guessed' : 'the file says'}: ${info.units.name}` : l}</option>)}
                  </select>
                </dd>
                <dt title="Where the drawing's origin lands, in the view (mm)">Origin at</dt>
                <dd className="pair"><input className="num" value={at.x} onChange={(e) => setAt({ ...at, x: e.target.value })} /><input className="num" value={at.y} onChange={(e) => setAt({ ...at, y: e.target.value })} /><span className="muted"> mm</span></dd>
                <dt>Rotation</dt><dd className="pair"><input className="num" value={rotation} onChange={(e) => setRotation(e.target.value)} /><span className="muted"> °</span></dd>
                <dt>Scale factor</dt><dd className="pair"><input className="num" value={scale} onChange={(e) => setScale(e.target.value)} /></dd>
                {info.view && info.view.kind !== 'plan' && (
                  <>
                    <dt title="A section or elevation looking towards −X or −Y mirrors the drawing about its y axis, so what the file drew from the left still reads from the left">Mirror</dt>
                    <dd>
                      <select value={flip} onChange={(e) => setFlip(e.target.value as 'auto' | 'yes' | 'no')}>
                        <option value="auto">by the view: {info.view.flip ? 'mirrored' : 'as drawn'}</option>
                        <option value="yes">mirrored</option>
                        <option value="no">as drawn</option>
                      </select>
                    </dd>
                  </>
                )}
                <dt>Bring</dt>
                <dd className="pair">
                  <label><input type="checkbox" checked={texts} onChange={(e) => setTexts(e.target.checked)} /> texts</label>
                  <label><input type="checkbox" checked={hatches} onChange={(e) => setHatches(e.target.checked)} /> hatches</label>
                </dd>
                <dt title="Read-only, managed under Manage → Links: shown or hidden, halftone, unlinked, converted to native, read again from its file">As a link</dt>
                <dd><label><input type="checkbox" checked={link} onChange={(e) => setLink(e.target.checked)} /> read-only, kept with the file</label></dd>
                <dt>Dimensions</dt>
                <dd>
                  <select value={dimensions} onChange={(e) => setDimensions(e.target.value)}>
                    <option value="native">as Lapin dimensions (linear, aligned); the rest as lines</option>
                    <option value="geometry">as the lines and texts drawn</option>
                    <option value="skip">left out</option>
                  </select>
                </dd>
                {dimensions === 'native' && <>
                  <dt title="AutoCAD's continued dimensions (one per segment) on one dimension line, each starting where another ends, become one string of points, edited as one object">Continued</dt>
                  <dd><label><input type="checkbox" checked={joinDimensions} onChange={(e) => setJoinDimensions(e.target.checked)} /> joined into strings</label></dd>
                </>}
              </dl>
              <p className="muted small">{willFlip ? 'The drawing will be mirrored for this view.' : 'The drawing keeps its orientation.'} {Object.keys(info.unsupported).length ? `Not brought in: ${Object.entries(info.unsupported).map(([k, n]) => `${n} ${k.toLowerCase()}`).join(', ')}.` : ''}</p>
              <table className="layer-table dxf-layers">
                <thead><tr><th>Import</th><th>Layer</th><th>Colour</th><th>Line</th><th>Shown</th><th>Entities</th></tr></thead>
                <tbody>
                  {info.layers.map((l) => (
                    <tr key={l.name}>
                      <td><input type="checkbox" checked={!off.has(l.name)} onChange={(e) => { const next = new Set(off); if (e.target.checked) next.delete(l.name); else next.add(l.name); setOff(next); }} /></td>
                      <td>{l.name}</td>
                      <td><span className="swatch" style={{ background: l.color }} /> {l.color}</td>
                      <td>{l.lineType}</td>
                      <td>{l.visible ? 'yes' : 'hidden'}</td>
                      <td className="num">{l.count}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!info || !viewId || busy} onClick={() => void run()}>{busy ? 'Importing…' : 'Import'}</button>
        </footer>
      </div>
    </div>
  );
}
