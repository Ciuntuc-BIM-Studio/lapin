/**
 * A PDF or a picture into the view in front, to trace over (a tester's wish, #89, decision 371). A PDF: which page, the scale
 * its drawing was made to (1:N), whether its lines come in as drafting and its pictures as images, where its corner lands;
 * the whole as one group named after the file. A picture: how wide it lies (its height by its pixels), where; it is scaled
 * true afterwards with Scale image (two points and the distance between them).
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { traceFile } from '../../engine/files';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { zoomHost } from '../../viewport/zoom';

export function ImportTraceDialog() {
  const file = traceFile.current;
  const nodes = useSession((s) => s.nodes);
  const isPdf = /\.pdf$/i.test(file?.name ?? '');
  const [viewId, setViewId] = useState<string | null>(null);
  const [pages, setPages] = useState<number[][] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [scale, setScale] = useState('100');
  const [lines, setLines] = useState(true);
  const [pictures, setPictures] = useState(true);
  const [width, setWidth] = useState('10');
  const [opacity, setOpacity] = useState('0.5');
  const [at, setAt] = useState({ x: '0', y: '0' });
  const [busy, setBusy] = useState(false);
  const close = () => { traceFile.current = null; useUi.setState({ dialog: null }); };

  useEffect(() => {
    if (!file) return;
    let alive = true;
    void (async () => {
      const id = await frontView();
      if (!alive) return;
      if (!id || id === '3d') { setProblem('Open a plan, a section or an elevation to import into.'); return; }
      setViewId(id);
      const view = useSession.getState().nodes.get(id);
      if (typeof view?.props.scale === 'number') setScale(String(view.props.scale));
      if (!isPdf) return;
      try {
        const r = await query<{ pages: number[][] }>('drafting.pdfInfo', { ...file });
        if (alive && r) setPages(r.pages);
      } catch (e) { if (alive) setProblem(e instanceof Error ? e.message : String(e)); }
    })();
    return () => { alive = false; };
  }, [file, isPdf]);

  if (!file) return null;
  const view = viewId ? nodes.get(viewId) : undefined;
  const num = (s: string, d: number) => { const n = Number(s.replace(',', '.')); return Number.isFinite(n) ? n : d; };
  const go = async () => {
    if (!viewId) return;
    setBusy(true);
    try {
      const where = [num(at.x, 0), num(at.y, 0)];
      if (isPdf) {
        const r = await execute('drafting.importPdf', { ...file, viewId, page, scale: num(scale, 100), lines, pictures, at: where, opacity: num(opacity, 0.6) }) as { groupId?: string; lines: number; pictures: number; warnings: string[] } | null;
        if (!r) return;
        notify(`${r.lines} line(s) and ${r.pictures} picture(s) from page ${page}, as one group.${r.warnings.length ? ` ${r.warnings[0]}` : ''}`);
        if (r.groupId) useUi.setState({ selected: [r.groupId], selection: r.groupId });
      } else {
        const r = await execute('drafting.placeImage', { ...file, viewId, width: num(width, 10), at: where, opacity: num(opacity, 0.5) }) as { id?: string } | null;
        if (!r?.id) return;
        useUi.setState({ selected: [r.id], selection: r.id });
        notify('Placed. Scale image (two points and their true distance) sets its size exactly.');
      }
      close();
      setTimeout(() => { if (!zoomHost()?.fitSelection()) zoomHost()?.fitAll(); }, 300);
    } finally { setBusy(false); }
  };
  const size = pages?.[page - 1];
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Import PDF or image" data-testid="import-trace" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">{isPdf ? 'Import PDF' : 'Import image'} · <span data-no-i18n>{file.name}</span></span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {problem && <p className="error-text">{problem}</p>}
          <p className="muted small">Into <b data-no-i18n>{view?.name ?? '—'}</b>, under its drawing, to trace over.</p>
          {isPdf && <>
            <label className="field-line"><span className="eyebrow">Page</span>
              <select data-field="trace-page" value={page} onChange={(e) => setPage(Number(e.target.value))}>
                {(pages ?? [[0, 0]]).map((p, i) => <option key={i} value={i + 1}>{i + 1}{p[0] ? ` · ${Math.round(p[0])} × ${Math.round(p[1])} mm` : ''}</option>)}
              </select></label>
            <label className="field-line"><span className="eyebrow">Drawn at 1:</span><input className="num" data-field="trace-scale" value={scale} onChange={(e) => setScale(e.target.value)} title="The scale the PDF's drawing was made to: a paper millimetre is that many millimetres here" /></label>
            {size && <p className="muted small">The page covers {Math.round(size[0] * num(scale, 100)) / 1000} × {Math.round(size[1] * num(scale, 100)) / 1000} m here.</p>}
            <label className="field-line check"><input type="checkbox" checked={lines} onChange={(e) => setLines(e.target.checked)} /><span>Its lines, as drafting (vector PDFs)</span></label>
            <label className="field-line check"><input type="checkbox" checked={pictures} onChange={(e) => setPictures(e.target.checked)} /><span>Its pictures, as images (scans)</span></label>
          </>}
          {!isPdf && <label className="field-line"><span className="eyebrow">Width (m)</span><input className="num" data-field="trace-width" value={width} onChange={(e) => setWidth(e.target.value)} title="How wide it lies; its height follows its pixels. Scale image sets it exactly afterwards." /></label>}
          <label className="field-line"><span className="eyebrow">Opacity</span><input className="num" value={opacity} onChange={(e) => setOpacity(e.target.value)} title="0.05 (faint) to 1 (solid)" /></label>
          <div className="field-line"><span className="eyebrow">Bottom-left corner at</span>
            <span className="row" style={{ gap: 4 }}>
              <input className="num" style={{ width: 70 }} value={at.x} onChange={(e) => setAt({ ...at, x: e.target.value })} aria-label="X" />
              <input className="num" style={{ width: 70 }} value={at.y} onChange={(e) => setAt({ ...at, y: e.target.value })} aria-label="Y" />
              <span className="muted small">m</span>
            </span></div>
          {isPdf && <p className="muted small">It comes in as one group: Move, Rotate and Scale take it whole; double-click it to work on its parts.</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-action="import-trace" disabled={!viewId || busy || Boolean(problem)} onClick={() => void go()}>Import</button>
        </footer>
      </div>
    </div>
  );
}
