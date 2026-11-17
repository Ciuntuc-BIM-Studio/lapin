/**
 * The configurator's preview: a sample the engine draws for the material's graphics (material.preview) at the chosen scale,
 * with what is edited and not saved yet (the draft), so what is shown is what a view prints. Drawn by the views' own pen
 * renderer (DrawingLayer). Asked again a moment after the last change; the last answer stays while the next is on its way.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useUi } from '../../app/store';
import { query } from '../../engine/session';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import { drawsOnDark, type DrawingDto } from '../../viewport/drawing';
import { fitBounds } from './typePreviewState';
import { GRAPHICS_SCALES, previewArgs, type MaterialKind, type SampleLayout } from './materialGraphicsModel';

type PreviewDto = DrawingDto & { layout?: string; resolved?: unknown };

export function MaterialGraphicsPreview({ id, kind, draft, version, scale, layout, onScale, onLayout, width = 300, height = 74 }: {
  id: string; kind: MaterialKind; draft: Record<string, unknown>; version?: unknown; scale: number; layout: SampleLayout;
  onScale: (s: number) => void; onLayout: (l: SampleLayout) => void; width?: number; height?: number;
}) {
  const onDark = drawsOnDark(useUi((s) => s.theme));
  const [data, setData] = useState<PreviewDto | null>(null);
  const [problem, setProblem] = useState('');
  const draftJson = JSON.stringify(draft);
  const asked = useRef(0);
  useEffect(() => {
    const n = ++asked.current;
    const timer = setTimeout(() => {
      const args = previewArgs(id, kind, scale, layout, JSON.parse(draftJson) as Record<string, unknown>, { width: 60, height: layout === 'band' ? 30 : 15 });
      void query<PreviewDto>('material.preview', args).then((r) => {
        if (n !== asked.current) return;
        if (r) { setData(r); setProblem(''); } else setProblem('The preview could not be drawn.');
      });
    }, 200);
    return () => clearTimeout(timer);
  }, [id, kind, scale, layout, draftJson, version]);
  const fitted = useMemo(() => fitBounds(data?.bounds, width, height, 6), [data, width, height]);
  return (
    <section className="material-gfx-preview" data-testid="material-graphics-preview">
      <div className="row">
        <span className="eyebrow">Preview</span>
        <span className="grow" />
        <label title="Scale of the sample"><select data-testid="material-preview-scale" value={scale} onChange={(e) => onScale(Number(e.target.value))}>
          {GRAPHICS_SCALES.map((s) => <option key={s} value={s}>1:{s}</option>)}
        </select></label>
        <span className="tabs" role="tablist">
          {(['strip', 'band'] as const).map((l) => (
            <button key={l} type="button" role="tab" aria-selected={layout === l} data-layout={l} className={`tab${layout === l ? ' on' : ''}`}
              title={l === 'strip' ? 'A strip of the material between two lines' : 'A band between two layers, with the lines that separate them'} onClick={() => onLayout(l)}>{l === 'strip' ? 'Strip' : 'Band'}</button>
          ))}
        </span>
      </div>
      {problem
        ? <p className="muted small" role="status">{problem}</p>
        : (
          <svg className="drawing-svg material-gfx-sample" width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Preview of the material graphics">
            <rect className="paper" x={0} y={0} width={width} height={height} />
            {data && <DrawingLayer drawing={data} toScreen={fitted.toScreen} pxPerMetre={fitted.k} onDark={onDark} minText={4} />}
          </svg>
        )}
    </section>
  );
}
