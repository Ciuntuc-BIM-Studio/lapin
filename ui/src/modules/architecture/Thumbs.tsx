/**
 * Architecture's types drawn small, for the catalog (decision 263): a wall
 * type's layers as its plan cut, a floor's or a ceiling's as its section, a
 * door's or window's elevation from the symbol engine (the configurator's own
 * preview, asked once per type as it is), a skirting's profile to scale.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { query, useSession } from '../../engine/session';
import { materialColor, typeLayers } from '../../engine/model';
import { Strokes, type Preview2 } from './JoineryDialog';
import { profilePath, type SkirtingSpec } from './skirtingProfile';

const W = 96, H = 64;

/** A type's layers as bands, across (a wall's plan cut) or stacked (a floor's section), each in its material's colour. */
function Layers({ type, across }: { type: NodeDto; across: boolean }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const layers = typeLayers({ nodes, relations }, type.id);
  const total = layers.reduce((s, l) => s + l.thickness, 0);
  if (!layers.length || total <= 0) return <svg className="thumb-svg" viewBox={`0 0 ${W} ${H}`} aria-hidden="true" />;
  const span = across ? W - 24 : H - 20;
  let at = across ? 12 : 10;
  return (
    <svg className="thumb-svg" viewBox={`0 0 ${W} ${H}`} aria-hidden="true">
      {layers.map((l, i) => {
        const d = (l.thickness / total) * span;
        // A layer of no thickness (a membrane, paint): a dashed line where it lies, as the drawings show it.
        const r = d <= 0
          ? (across
            ? <line key={i} x1={at} y1={8} x2={at} y2={H - 8} stroke="var(--ink)" strokeWidth={1.2} strokeDasharray="3 1.5" />
            : <line key={i} x1={10} y1={at} x2={W - 10} y2={at} stroke="var(--ink)" strokeWidth={1.2} strokeDasharray="3 1.5" />)
          : across
          ? <rect key={i} x={at} y={8} width={d} height={H - 16} fill={materialColor({ nodes, relations }, l.material)} stroke="var(--ink)" strokeWidth={0.6} />
          : <rect key={i} x={10} y={at} width={W - 20} height={d} fill={materialColor({ nodes, relations }, l.material)} stroke="var(--ink)" strokeWidth={0.6} />;
        at += d;
        return r;
      })}
    </svg>
  );
}

export const WallThumb = ({ type }: { type: NodeDto }) => <Layers type={type} across />;
export const SlabThumb = ({ type }: { type: NodeDto }) => <Layers type={type} across={false} />;

const previews = new Map<string, Promise<Preview2 | null>>();

/** A door's or window's elevation from inside, as the configurator draws it. */
export function JoineryThumb({ type }: { type: NodeDto }) {
  const spec = type.props.spec;
  const key = `${type.id}:${JSON.stringify(spec ?? null)}`;
  const [preview, setPreview] = useState<Preview2 | null>(null);
  useEffect(() => {
    if (!spec) return;
    let live = true;
    if (!previews.has(key)) previews.set(key, query<Preview2>('architecture.joineryPreview', { spec }).catch(() => null));
    void previews.get(key)!.then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [key]);
  return <span className="thumb-svg"><Strokes drawing={preview?.drawings?.elevation} w={W} h={H} flipX label={type.name ?? ''} /></span>;
}

/** A skirting's profile to scale, the wall on its left, in its colour. */
export function SkirtingThumb({ type }: { type: NodeDto }) {
  const spec = type.props.spec as Partial<SkirtingSpec> | undefined;
  if (!spec?.profile || !spec.height || !spec.thickness) return <svg className="thumb-svg" viewBox={`0 0 ${W} ${H}`} aria-hidden="true" />;
  return (
    <svg className="thumb-svg" viewBox={`0 0 ${W} ${H}`} aria-hidden="true">
      <path d={`M8 4V${H - 6}H${W - 8}`} fill="none" stroke="var(--rule-strong)" />
      <path d={profilePath(spec as SkirtingSpec, W, H, 8)} fill={spec.color ?? '#efe9dc'} stroke="var(--ink)" strokeWidth={0.8} />
    </svg>
  );
}
