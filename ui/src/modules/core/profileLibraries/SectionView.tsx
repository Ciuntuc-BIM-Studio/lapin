/**
 * A profile drawn to scale with its dimensions (docs/plan-profile-libraries.md): its rings as the engine works its
 * properties out from them (fillets rounded, holes open), the depth dimensioned on the right and the width under it, the
 * centroid marked; the other sizes listed beside it in millimetres.
 */

import { dimensionsOf, fit, mm, ringsPath, type ProfileJson, type Ring } from './model';

export function SectionView({ profile, rings, size = 220, centroid }: { profile: ProfileJson; rings: Ring[]; size?: number; centroid?: [number, number] | null }) {
  const pad = 34;
  const { k, x0, y0, minX, maxX, minY, maxY } = fit(rings, size, size, pad);
  const path = ringsPath(rings, k, x0, y0);
  const X = (x: number) => x0 + x * k, Y = (y: number) => y0 - y * k;
  const dims = dimensionsOf(profile);
  const h = maxY - minY, b = maxX - minX;
  const tick = (x: number, y: number) => <line x1={x - 3} y1={y + 3} x2={x + 3} y2={y - 3} stroke="currentColor" strokeWidth={1} />;
  return (
    <div className="section-view" data-testid="profile-section">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label={profile.name}>
        <path d={path} fill="var(--accent-soft, #d9cdb8)" fillRule="evenodd" stroke="currentColor" strokeWidth={1} />
        {h > 0 && <g className="dim" opacity={0.75}>
          <line x1={X(maxX) + 12} y1={Y(minY)} x2={X(maxX) + 12} y2={Y(maxY)} stroke="currentColor" strokeWidth={0.75} />
          {tick(X(maxX) + 12, Y(minY))}{tick(X(maxX) + 12, Y(maxY))}
          <text x={X(maxX) + 16} y={(Y(minY) + Y(maxY)) / 2} fontSize={10} dominantBaseline="middle" data-no-i18n>{mm(h)}</text>
        </g>}
        {b > 0 && <g className="dim" opacity={0.75}>
          <line x1={X(minX)} y1={Y(minY) + 12} x2={X(maxX)} y2={Y(minY) + 12} stroke="currentColor" strokeWidth={0.75} />
          {tick(X(minX), Y(minY) + 12)}{tick(X(maxX), Y(minY) + 12)}
          <text x={(X(minX) + X(maxX)) / 2} y={Y(minY) + 24} fontSize={10} textAnchor="middle" data-no-i18n>{mm(b)}</text>
        </g>}
        {centroid && <g stroke="var(--accent, #b5654a)" strokeWidth={1}>
          <line x1={X(centroid[0]) - 5} y1={Y(centroid[1])} x2={X(centroid[0]) + 5} y2={Y(centroid[1])} />
          <line x1={X(centroid[0])} y1={Y(centroid[1]) - 5} x2={X(centroid[0])} y2={Y(centroid[1]) + 5} />
        </g>}
      </svg>
      {dims.length > 0 && <dl className="section-dims small">
        {dims.map((d) => <div key={d.key}><dt data-no-i18n>{d.label}</dt><dd data-no-i18n>{mm(d.value)}</dd></div>)}
        <div><dt>mm</dt><dd /></div>
      </dl>}
    </div>
  );
}
