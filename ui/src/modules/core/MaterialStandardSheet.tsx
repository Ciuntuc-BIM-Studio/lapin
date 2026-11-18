/**
 * A material standard drawn on the reference details (`standards.previewSheet`): the details the engine keeps for the purpose,
 * at 1:20, 1:50 or 1:100, drawn by the views' own pen renderer, so what shows is what a sheet prints. The standard is a saved
 * one (`id`) or one not saved yet (`standard`, inline: a draft in the editor, a store pack before it is installed).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../../app/store';
import { query } from '../../engine/session';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import { drawsOnDark, type DrawingDto } from '../../viewport/drawing';
import { fitBounds } from './typePreviewState';
import { SHEET_SCALES, sheetArgs, type MaterialStandard } from './materialStandardsModel';

interface SheetDetail { name: string; title: string; views: { name: string; kind: string; drawing: DrawingDto }[] }
interface Sheet { standard: string; scale: number; details: SheetDetail[] }

export type SheetTarget = { id: string } | { standard: MaterialStandard };

export function MaterialStandardSheet({ target, width = 250, height = 170 }: { target: SheetTarget | null; width?: number; height?: number }) {
  const onDark = drawsOnDark(useUi((s) => s.theme));
  const [scale, setScale] = useState<number>(50);
  const [detail, setDetail] = useState<string>('');
  const [sheet, setSheet] = useState<Sheet | null>(null);
  const [problem, setProblem] = useState('');
  const asked = useRef(0);
  const key = JSON.stringify(target);
  useEffect(() => {
    if (!target) { setSheet(null); return; }
    const n = ++asked.current;
    const timer = setTimeout(() => {
      void query<Sheet>('standards.previewSheet', sheetArgs(JSON.parse(key) as NonNullable<SheetTarget>, scale)).then((r) => {
        if (n !== asked.current) return;
        if (r) { setSheet(r); setProblem(''); } else setProblem('The sheet could not be drawn.');
      });
    }, 350);
    return () => clearTimeout(timer);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, scale]);
  const details = sheet?.details ?? [];
  const shown = details.find((d) => d.name === detail) ?? details[0];
  return (
    <section className="standard-sheet" data-testid="standard-sheet">
      <div className="row">
        <span className="eyebrow">Preview on the reference details</span>
        <span className="grow" />
        <select data-testid="standard-sheet-detail" aria-label="Detail" value={shown?.name ?? ''} onChange={(e) => setDetail(e.target.value)}>
          {details.map((d) => <option key={d.name} value={d.name} data-no-i18n>{d.title}</option>)}
        </select>
        <select data-testid="standard-sheet-scale" aria-label="Scale" value={scale} onChange={(e) => setScale(Number(e.target.value))}>
          {SHEET_SCALES.map((s) => <option key={s} value={s}>1:{s}</option>)}
        </select>
      </div>
      {problem && <p className="muted small" role="status">{problem}</p>}
      {!problem && !sheet && <p className="muted small">Drawing…</p>}
      <div className="standard-sheet-views">
        {shown?.views.map((v) => <SheetView key={v.name} view={v} width={width} height={height} onDark={onDark} />)}
      </div>
    </section>
  );
}

function SheetView({ view, width, height, onDark }: { view: SheetDetail['views'][number]; width: number; height: number; onDark: boolean }) {
  const fitted = useMemo(() => fitBounds(view.drawing.bounds, width, height, 8), [view, width, height]);
  return (
    <figure className="standard-sheet-view">
      <svg className="drawing-svg" width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label={view.name}>
        <rect className="paper" x={0} y={0} width={width} height={height} />
        <DrawingLayer drawing={view.drawing} toScreen={fitted.toScreen} pxPerMetre={fitted.k} onDark={onDark} minText={4} />
      </svg>
      <figcaption className="muted small" data-no-i18n>{view.name}</figcaption>
    </figure>
  );
}
