/**
 * A drafted entity in the inspector, in CAD terms: a shape's layer, colour,
 * line type and pen (by layer), and the geometry of its kind, lines by their
 * ends, circles by centre and radius, polylines by their vertices, with the
 * lengths and areas that follow; a hatch by its pattern; a text by its words,
 * style, angle and place; a dimension and a tag by what they belong to.
 * Lengths in mm, angles in degrees, areas in m².
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import type { Vec2 } from '../../platform/module';
import { fromJson, toJson, type Shape } from '../../viewport/cad/shapes';
import { ROLES, roleLabel } from '../../viewport/cad/session';

interface Layer { name: string; color: string; weight: number; lineType: string; visible: boolean; locked: boolean }

const mm = (v: number) => String(Math.round(v * 1000));
const deg = (rad: number) => String(Math.round((rad * 180) / Math.PI * 100) / 100);
const m2 = (v: number) => `${v.toFixed(3)} m²`;
const pt = (v: unknown): Vec2 | null => (Array.isArray(v) && typeof v[0] === 'number' && typeof v[1] === 'number' ? { x: v[0], y: v[1] } : null);
const polygonArea = (pts: Vec2[]) => Math.abs(pts.reduce((a, p, i) => { const q = pts[(i + 1) % pts.length]; return a + p.x * q.y - q.x * p.y; }, 0)) / 2;
const polylineLength = (pts: Vec2[], closed: boolean) => {
  let l = 0;
  for (let i = 0; i + 1 < pts.length; i++) l += Math.hypot(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y);
  if (closed && pts.length > 2) l += Math.hypot(pts[0].x - pts[pts.length - 1].x, pts[0].y - pts[pts.length - 1].y);
  return l;
};

/** A number typed in, committed on Enter or blur; empty keeps the value. */
function Num({ value, onCommit, suffix }: { value: string; onCommit: (n: number) => void; suffix?: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const done = () => { const n = Number(text.replace(',', '.')); if (text.trim() !== '' && Number.isFinite(n) && text !== value) onCommit(n); else setText(value); };
  return (
    <span className="commit">
      <input className="num" value={text} onChange={(e) => setText(e.target.value)} onBlur={done}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(value); }} />
      {suffix && <span className="muted"> {suffix}</span>}
    </span>
  );
}

function Point({ label, p, onCommit }: { label: string; p: Vec2; onCommit?: (q: Vec2) => void }) {
  return (
    <>
      <dt>{label}</dt>
      <dd className="pair">
        {onCommit
          ? <><Num value={mm(p.x)} onCommit={(x) => onCommit({ x: x / 1000, y: p.y })} /><Num value={mm(p.y)} onCommit={(y) => onCommit({ x: p.x, y: y / 1000 })} suffix="mm" /></>
          : <span className="num">{mm(p.x)} · {mm(p.y)} mm</span>}
      </dd>
    </>
  );
}

const Row = ({ label, value }: { label: string; value: string }) => <><dt>{label}</dt><dd><span className="num">{value}</span></dd></>;

function ShapeFields({ node, shape }: { node: NodeDto; shape: Shape }) {
  const set = (s: Shape) => void execute('drafting.updateDraft', { id: node.id, shape: toJson(s) });
  switch (shape.type) {
    case 'line': {
      const d = { x: shape.b.x - shape.a.x, y: shape.b.y - shape.a.y };
      return (
        <>
          <Point label="Start" p={shape.a} onCommit={(a) => set({ ...shape, a })} />
          <Point label="End" p={shape.b} onCommit={(b) => set({ ...shape, b })} />
          <Row label="Length" value={`${mm(Math.hypot(d.x, d.y))} mm`} />
          <Row label="Angle" value={`${deg(Math.atan2(d.y, d.x))}°`} />
        </>
      );
    }
    case 'polyline':
      return (
        <>
          <Row label="Vertices" value={String(shape.pts.length)} />
          <dt>Closed</dt><dd><input type="checkbox" checked={shape.closed} onChange={(e) => set({ ...shape, closed: e.target.checked })} /></dd>
          <Row label="Length" value={`${mm(polylineLength(shape.pts, shape.closed))} mm`} />
          {shape.closed && shape.pts.length > 2 && <Row label="Area" value={m2(polygonArea(shape.pts))} />}
        </>
      );
    case 'circle':
      return (
        <>
          <Point label="Centre" p={shape.c} onCommit={(c) => set({ ...shape, c })} />
          <dt>Radius</dt><dd><Num value={mm(shape.r)} onCommit={(r) => { if (r > 0) set({ ...shape, r: r / 1000 }); }} suffix="mm" /></dd>
          <Row label="Diameter" value={`${mm(2 * shape.r)} mm`} />
          <Row label="Circumference" value={`${mm(2 * Math.PI * shape.r)} mm`} />
          <Row label="Area" value={m2(Math.PI * shape.r * shape.r)} />
        </>
      );
    case 'arc': {
      let sweep = (shape.end - shape.start) % (2 * Math.PI);
      if (sweep <= 0) sweep += 2 * Math.PI;
      return (
        <>
          <Point label="Centre" p={shape.c} onCommit={(c) => set({ ...shape, c })} />
          <dt>Radius</dt><dd><Num value={mm(shape.r)} onCommit={(r) => { if (r > 0) set({ ...shape, r: r / 1000 }); }} suffix="mm" /></dd>
          <dt>Start angle</dt><dd><Num value={deg(shape.start)} onCommit={(a) => set({ ...shape, start: (a * Math.PI) / 180 })} suffix="°" /></dd>
          <dt>End angle</dt><dd><Num value={deg(shape.end)} onCommit={(a) => set({ ...shape, end: (a * Math.PI) / 180 })} suffix="°" /></dd>
          <Row label="Length" value={`${mm(shape.r * sweep)} mm`} />
        </>
      );
    }
  }
}

function HatchFields({ hatch }: { hatch: Record<string, unknown> }) {
  const loops = Array.isArray(hatch.loops) ? (hatch.loops as unknown[][]).map((l) => l.map(pt).filter((p): p is Vec2 => !!p)) : [];
  const area = loops.length ? loops.reduce((a, l, i) => a + (i === 0 ? 1 : -1) * polygonArea(l), 0) : null;
  return (
    <>
      <Row label="Pattern" value={String(hatch.pattern ?? 'solid')} />
      <Row label="Scale" value={String(hatch.scale ?? 1)} />
      <Row label="Angle" value={`${String(hatch.angle ?? 0)}°`} />
      {area != null && <Row label="Area" value={m2(Math.abs(area))} />}
    </>
  );
}

function Drafted({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [layers, setLayers] = useState<Layer[]>([]);
  useEffect(() => { void query<Layer[]>('drafting.layers').then((l) => l && setLayers(l)); }, [nodes]);
  const layerName = typeof node.props.layer === 'string' ? node.props.layer : '0';
  const layer = layers.find((l) => l.name === layerName);
  const own = typeof node.props.color === 'string' ? node.props.color : null;
  const shape = node.props.shape && typeof node.props.shape === 'object' ? fromJson(node.props.shape as Record<string, unknown>) : null;
  const hatch = node.props.hatch && typeof node.props.hatch === 'object' ? (node.props.hatch as Record<string, unknown>) : null;
  const kind = hatch ? 'Hatch' : shape ? { line: 'Line', polyline: 'Polyline', circle: 'Circle', arc: 'Arc' }[shape.type] : 'Shape';
  return (
    <dl className="fields">
      <Row label="Kind" value={kind} />
      <dt>Layer</dt>
      <dd><select value={layerName} onChange={(e) => void execute('drafting.updateDraft', { id: node.id, layer: e.target.value })}>
        {(layers.length ? layers : [{ name: layerName } as Layer]).map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
      </select></dd>
      <dt>Colour</dt>
      <dd className="pair">
        <input type="color" value={own ?? layer?.color ?? '#18181b'} onChange={(e) => void execute('drafting.updateDraft', { id: node.id, color: e.target.value })} />
        {own
          ? <button type="button" className="link" onClick={() => void execute('drafting.updateDraft', { id: node.id, color: null })}>by layer</button>
          : <span className="muted small">by layer</span>}
      </dd>
      <dt title="What it is for: Studio's operations find their profile, path, rail or axis by it; construction lines are never printed">Role</dt>
      <dd><select value={typeof node.props.role === 'string' ? node.props.role : ''} onChange={(e) => void execute('drafting.updateDraft', { id: node.id, role: e.target.value || null })}>
        <option value="">none</option>
        {ROLES.map((r) => <option key={r} value={r}>{roleLabel(r)}</option>)}
      </select></dd>
      {layer && <Row label="Line type" value={layer.lineType} />}
      {layer && <Row label="Pen" value={`${layer.weight} mm`} />}
      {hatch ? <HatchFields hatch={hatch} /> : shape ? <ShapeFields node={node} shape={shape} /> : null}
    </dl>
  );
}

function TextFields({ node }: { node: NodeDto }) {
  const set = (attrs: Record<string, unknown>) => void execute('element.update', { id: node.id, attrs });
  const at = pt(node.props.at);
  const text = typeof node.props.text === 'string' ? node.props.text : '';
  const [body, setBody] = useState(text);
  useEffect(() => setBody(text), [text]);
  return (
    <dl className="fields">
      <dt>Text</dt>
      <dd><textarea rows={2} value={body} onChange={(e) => setBody(e.target.value)} onBlur={() => { if (body.trim() && body !== text) set({ text: body }); }} /></dd>
      <dt>Text style</dt><dd><input value={typeof node.props.textStyle === 'string' ? node.props.textStyle : ''} placeholder="default" onChange={(e) => set({ textStyle: e.target.value || null })} /></dd>
      <dt>Angle</dt><dd><Num value={String(typeof node.props.angle === 'number' ? node.props.angle : 0)} onCommit={(a) => set({ angle: a })} suffix="°" /></dd>
      {at && <Point label="Position" p={at} />}
    </dl>
  );
}

function DimensionFields({ node }: { node: NodeDto }) {
  const a = pt(node.props.a), b = pt(node.props.b), at = pt(node.props.at);
  return (
    <dl className="fields">
      <Row label="Kind" value={pt(node.props.dir) ? 'Linear' : 'Aligned'} />
      <dt>Dimension style</dt><dd><input value={typeof node.props.dimStyle === 'string' ? node.props.dimStyle : ''} placeholder="default" onChange={(e) => void execute('element.update', { id: node.id, attrs: { dimStyle: e.target.value || null } })} /></dd>
      {a && <Point label="Start" p={a} />}
      {b && <Point label="End" p={b} />}
      {a && b && <Row label="Length" value={`${mm(Math.hypot(b.x - a.x, b.y - a.y))} mm`} />}
      {at && <Point label="Position" p={at} />}
    </dl>
  );
}

function TagFields({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const target = typeof node.props.element === 'string' ? nodes.get(node.props.element) : undefined;
  const at = pt(node.props.at);
  return (
    <dl className="fields">
      <Row label="Tagged element" value={target ? `${target.name} (${target.type})` : '—'} />
      <Row label="Field" value={String(node.props.field ?? 'mark')} />
      {at && <Point label="Position" p={at} />}
    </dl>
  );
}

export function DraftSection({ node }: { node: NodeDto }) {
  switch (node.props.predefinedType) {
    case 'DRAFT': return <Drafted node={node} />;
    case 'TEXT': return <TextFields node={node} />;
    case 'DIMENSION': return <DimensionFields node={node} />;
    case 'TAG': return <TagFields node={node} />;
    default: return null;
  }
}
