/** A bar shape drawn from the engine's corners (metres) fitted into a box, each leg named by its length (a parameter's key) at its middle. */

import { fitCorners, type ShapeLeg } from './shapeModel';

export function ShapePreview({ corners, legs, width = 280, height = 180 }: { corners: number[][]; legs?: ShapeLeg[]; width?: number; height?: number }) {
  const fit = fitCorners(corners, width, height, 22);
  if (!fit) return <svg width={width} height={height} className="shape-preview" aria-label="Preview" />;
  const d = fit.points.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`).join('');
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} className="shape-preview" aria-label="Preview" data-testid="shape-preview" data-no-i18n>
      <path d={d} fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinejoin="round" strokeLinecap="round" />
      {fit.points.map(([x, y], i) => <circle key={i} cx={x} cy={y} r={2.2} fill="currentColor" />)}
      {legs?.map((l, i) => {
        const a = fit.points[i], b = fit.points[i + 1];
        if (!a || !b) return null;
        return <text key={i} x={(a[0] + b[0]) / 2 + 5} y={(a[1] + b[1]) / 2 - 5} fontSize={11} fill="currentColor" opacity={0.7}>{l.length}</text>;
      })}
    </svg>
  );
}
