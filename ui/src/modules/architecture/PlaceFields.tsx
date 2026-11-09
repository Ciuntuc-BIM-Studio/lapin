/**
 * A profile's place on its member (report #128): turn, mirror, alignment across and along the member, offset. One set of fields
 * for a curtain wall type's mullions and transoms and for the members a curtain wall styles of its own; the section drawn
 * beside them is the member's, as the engine lays it (`placeRings`, x across the member, y out of the building).
 */

import { LengthField, NumberField } from '../../shell/cells';
import { boxRing, placeRings, type Place, type Ring } from './memberPlace';
import { fit, ringsPath } from '../core/profileLibraries/model';

export function PlaceFields({ place, onChange }: { place: Place; onChange: (p: Place) => void }) {
  const set = (patch: Place) => onChange({ ...place, ...patch });
  return (
    <div className="place-fields" data-place-fields>
      <label className="field-line"><span className="eyebrow" title="Counter-clockwise, in degrees: the profile turned on its member">Rotation</span>
        <NumberField value={place.rotate ?? 0} suffix="°" testId="place-rotate" onCommit={(n) => set({ rotate: n ?? 0 })} />
      </label>
      <label className="field-line"><span className="eyebrow">Mirror</span>
        <input type="checkbox" data-place="mirror" checked={!!place.mirror} onChange={(e) => set({ mirror: e.target.checked })} /></label>
      <label className="field-line"><span className="eyebrow" title="Out of the building (the front) or into it: which face of the profile lies on the member's plane">Align across</span>
        <select data-place="across" value={place.across ?? 'center'} onChange={(e) => set({ across: e.target.value as Place['across'] })}>
          <option value="front">front face on the plane</option><option value="center">centred</option><option value="back">back face on the plane</option>
        </select></label>
      <label className="field-line"><span className="eyebrow" title="Along the grid line (a mullion) or up the level (a transom): which edge of the profile lies on it">Align along</span>
        <select data-place="along" value={place.along ?? 'center'} onChange={(e) => set({ along: e.target.value as Place['along'] })}>
          <option value="start">start edge on the line</option><option value="center">centred</option><option value="end">end edge on the line</option>
        </select></label>
      <label className="field-line"><span className="eyebrow">Offset across</span>
        <LengthField metres={place.dx ?? 0} testId="place-dx" onCommit={(m) => set({ dx: m ?? 0 })} /></label>
      <label className="field-line"><span className="eyebrow">Offset out</span>
        <LengthField metres={place.dy ?? 0} testId="place-dy" onCommit={(m) => set({ dy: m ?? 0 })} /></label>
    </div>
  );
}

/** The member's section as placed, with its axis cross-hair: the profile's rings, or a plain box of the member's size. */
export function PlacedSection({ rings, width, depth, place, size = 120 }: { rings?: Ring[] | null; width: number; depth: number; place: Place; size?: number }) {
  const base = rings && rings.length ? rings : [boxRing(width, depth)];
  const placed = placeRings(base, place);
  const { k, x0, y0 } = fit([...placed, ...base], size, size, 14);
  const X = (x: number) => x0 + x * k, Y = (y: number) => y0 - y * k;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label="Section" data-placed-section>
      <line x1={X(0)} y1={4} x2={X(0)} y2={size - 4} stroke="var(--ink-muted)" strokeDasharray="3 3" strokeWidth={0.75} />
      <line x1={4} y1={Y(0)} x2={size - 4} y2={Y(0)} stroke="var(--ink-muted)" strokeDasharray="3 3" strokeWidth={0.75} />
      <path d={ringsPath(placed, k, x0, y0)} fill="var(--accent-soft, #d9cdb8)" fillRule="evenodd" stroke="currentColor" strokeWidth={1} />
      <text x={size - 6} y={12} fontSize={9} textAnchor="end" fill="var(--ink-muted)">out</text>
    </svg>
  );
}
