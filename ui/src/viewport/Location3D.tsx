/**
 * The project's points over the 3D view (decision 445), as plans draw them when Manage → Location (or View →
 * Coordinate points) shows them: the internal origin with its X, Y and Z axes (red, green, blue), the project base
 * point (a circle with a cross, its elevation, an arrow to true north when the project is turned from it) and the
 * survey point (a triangle, its map coordinates). Drawn on the screen at their projected places, so they keep their
 * size at any zoom and never hide behind the model.
 */

import * as THREE from 'three';
import { useCamera, type Vec3 } from './camera';
import { basePoint, surveyPoint, useLocation } from '../app/location';
import { formatLevel } from '../engine/unitsStore';
import { t } from '../i18n';

const fixed3 = (v: number) => v.toFixed(3);

export function Location3D({ camera, canvas }: { camera: React.RefObject<THREE.Camera | null>; canvas: React.RefObject<HTMLElement | null> }) {
  useCamera();
  const loc = useLocation((s) => s.loc);
  if (!loc.show) return null;
  const r = canvas.current?.getBoundingClientRect();
  const c = camera.current;
  if (!r || !c) return null;
  c.updateMatrixWorld();
  const toScreen = (p: Vec3) => {
    const v = new THREE.Vector3(p.x, p.y, p.z).project(c);
    if (v.z > 1 || v.z < -1) return null;
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  // An axis drawn 48 px long on the screen from a point, along a model direction.
  const axis = (from: Vec3, d: Vec3, px = 48) => {
    const a = toScreen(from), b = toScreen({ x: from.x + d.x, y: from.y + d.y, z: from.z + d.z });
    if (!a || !b) return null;
    const dx = b.x - a.x, dy = b.y - a.y, l = Math.hypot(dx, dy);
    if (l < 1e-6) return null;
    return { a, b: { x: a.x + (dx / l) * px, y: a.y + (dy / l) * px } };
  };
  const o = { x: 0, y: 0, z: 0 };
  const axes = ([[{ x: 1, y: 0, z: 0 }, 'loc-x', 'X'], [{ x: 0, y: 1, z: 0 }, 'loc-y', 'Y'], [{ x: 0, y: 0, z: 1 }, 'loc-z', 'Z']] as const)
    .map(([d, cls, name]) => ({ seg: axis(o, d), cls, name }));
  const oS = toScreen(o);
  const b = basePoint(loc);
  const bS = toScreen({ x: b.x, y: b.y, z: 0 });
  const th = (b.northAngle * Math.PI) / 180;
  const north = Math.abs(b.northAngle) > 1e-9 ? axis({ x: b.x, y: b.y, z: 0 }, { x: -Math.sin(th), y: Math.cos(th), z: 0 }, 40) : null;
  const s = surveyPoint(loc);
  const sS = s ? toScreen({ x: s.x, y: s.y, z: 0 }) : null;
  return (
    <svg className="grip-overlay location-3d" data-testid="location-3d" aria-label={t("The project's points")}>
      {oS && <g className="loc-origin">
        {axes.map(({ seg, cls, name }) => seg && <g key={name} className={cls}>
          <line x1={seg.a.x} y1={seg.a.y} x2={seg.b.x} y2={seg.b.y} />
          <text x={seg.b.x + 4} y={seg.b.y + 4}>{name}</text>
        </g>)}
        <text className="loc-label" x={oS.x + 6} y={oS.y + 16}>{t('Internal origin')}</text>
      </g>}
      {bS && <g className="loc-point">
        <circle cx={bS.x} cy={bS.y} r={7} />
        <line x1={bS.x - 10} y1={bS.y} x2={bS.x + 10} y2={bS.y} /><line x1={bS.x} y1={bS.y - 10} x2={bS.x} y2={bS.y + 10} />
        {north && <><line x1={north.a.x} y1={north.a.y} x2={north.b.x} y2={north.b.y} /><text x={north.b.x} y={north.b.y - 4} textAnchor="middle">N</text></>}
        <text className="loc-label" x={bS.x + 12} y={bS.y - 6}>{`${t('Base point')} ${formatLevel(b.elevation)}`}</text>
      </g>}
      {s && sS && <g className="loc-point">
        <polygon points={`${sS.x},${sS.y - 9} ${sS.x - 8},${sS.y + 5} ${sS.x + 8},${sS.y + 5}`} />
        <text className="loc-label" x={sS.x + 12} y={sS.y + 18}>{`${t('Survey point')} E ${fixed3(s.easting)} N ${fixed3(s.northing)}`}</text>
      </g>}
    </svg>
  );
}
