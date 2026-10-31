/**
 * Reference planes in the 3D view (docs/plan-studio-thick.md): each a
 * translucent bounded quad with its name, as large as the model projected on
 * it (or its own drawn width and height), 2 × 2 m at least, in the colour the
 * plans draw them in. The quads are picked like elements (a click selects the
 * plane, a double click opens its view), lit up under the cursor, and the
 * selected one drags along its normal by the handle at its middle
 * (RefPlanes3D). They are drawn for the screen only: no export, render or
 * sheet carries them. The plane's place is its six fields, as the engine's
 * RefPlanes.Of reads them.
 */

import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import type { Vec3 } from './camera';

export interface RefPlane { id: string; name: string; origin: Vec3; right: Vec3; up: Vec3; normal: Vec3; width?: number; height?: number }

const DEG = Math.PI / 180;
const n = (v: unknown, d = 0) => (typeof v === 'number' && Number.isFinite(v) ? v : d);

/** The plane a reference plane view lies on: its origin and axes, from its bearing, tilt and spin (RefPlanes.Of). */
export function planeOf(node: NodeDto): RefPlane {
  const p = node.props;
  const b = n(p.bearing) * DEG, t = n(p.tilt) * DEG, s = n(p.spin) * DEG;
  const normal = { x: Math.cos(t) * Math.cos(b), y: Math.cos(t) * Math.sin(b), z: Math.sin(t) };
  // Level along the plane (the bearing turned a quarter clockwise), whatever the tilt.
  const x0 = { x: -Math.sin(b), y: Math.cos(b), z: 0 };
  const nx0 = cross(normal, x0);
  const right = norm({ x: x0.x * Math.cos(s) + nx0.x * Math.sin(s), y: x0.y * Math.cos(s) + nx0.y * Math.sin(s), z: x0.z * Math.cos(s) + nx0.z * Math.sin(s) });
  const up = norm(cross(normal, right));
  const width = n(p.width), height = n(p.height);
  return { id: node.id, name: node.name, origin: { x: n(p.x), y: n(p.y), z: n(p.z) }, right, up, normal, width: width > 0 ? width : undefined, height: height > 0 ? height : undefined };
}

/** What the planes' quads depend on, as one string: their ids, names and fields (the scene rebuilds them when it changes). */
export function refPlanesKey(nodes: ReadonlyMap<string, NodeDto>): string {
  const o: unknown[] = [];
  for (const node of nodes.values())
    if (node.type === 'LapinView' && node.props.kind === 'plane') o.push([node.id, node.name, node.props.x, node.props.y, node.props.z, node.props.bearing, node.props.tilt, node.props.spin, node.props.width, node.props.height]);
  return JSON.stringify(o);
}

/** Every reference plane among the nodes. */
export function refPlanesOf(nodes: ReadonlyMap<string, NodeDto>): RefPlane[] {
  const o: RefPlane[] = [];
  for (const node of nodes.values()) if (node.type === 'LapinView' && node.props.kind === 'plane') o.push(planeOf(node));
  return o;
}

const cross = (a: Vec3, b: Vec3): Vec3 => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });
const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
const norm = (a: Vec3): Vec3 => { const l = Math.hypot(a.x, a.y, a.z) || 1; return { x: a.x / l, y: a.y / l, z: a.z / l }; };
export const planePoint = (p: RefPlane, x: number, y: number, off = 0): Vec3 => ({
  x: p.origin.x + p.right.x * x + p.up.x * y + p.normal.x * off,
  y: p.origin.y + p.right.y * x + p.up.y * y + p.normal.y * off,
  z: p.origin.z + p.right.z * x + p.up.z * y + p.normal.z * off,
});

/** The quad's extent on the plane (its x and y range): the model's bounds projected on it with a margin, 2 × 2 m at least, or its own size about its origin. */
export function planeExtent(p: RefPlane, bounds: { min: number[]; max: number[] } | null, pad = 0.5): { x0: number; x1: number; y0: number; y1: number } {
  if (p.width || p.height) {
    const w = Math.max(2, p.width ?? 2), h = Math.max(2, p.height ?? 2);
    return { x0: -w / 2, x1: w / 2, y0: -h / 2, y1: h / 2 };
  }
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  if (bounds) {
    for (const cx of [bounds.min[0], bounds.max[0]]) for (const cy of [bounds.min[1], bounds.max[1]]) for (const cz of [bounds.min[2], bounds.max[2]]) {
      const d = { x: cx - p.origin.x, y: cy - p.origin.y, z: cz - p.origin.z };
      const x = dot(d, p.right), y = dot(d, p.up);
      x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y);
    }
    x0 -= pad; x1 += pad; y0 -= pad; y1 += pad;
  }
  if (!Number.isFinite(x0)) { x0 = -1; x1 = 1; y0 = -1; y1 = 1; }
  // The origin inside, and 2 m across at least.
  x0 = Math.min(x0, -1); x1 = Math.max(x1, 1); y0 = Math.min(y0, -1); y1 = Math.max(y1, 1);
  return { x0, x1, y0, y1 };
}

export const PLANE_COLOR = 0x2b7a9e;

/** The name as a sprite: the plane's colour on a faint card, readable from either side. */
function label(text: string): THREE.Sprite {
  const cv = document.createElement('canvas');
  const c = cv.getContext('2d')!;
  c.font = 'bold 28px sans-serif';
  const w = Math.ceil(c.measureText(text).width) + 24;
  cv.width = Math.max(64, w); cv.height = 44;
  const g = cv.getContext('2d')!;
  g.fillStyle = 'rgba(255,255,255,0.75)';
  g.fillRect(0, 0, cv.width, cv.height);
  g.fillStyle = '#2b7a9e'; g.font = 'bold 28px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.fillText(text, cv.width / 2, cv.height / 2 + 1);
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), depthTest: false, transparent: true }));
  sprite.scale.set(0.012 * cv.width, 0.012 * cv.height, 1);
  sprite.renderOrder = 3;
  return sprite;
}

/**
 * The planes as a group for the scene: a quad each (`userData.nodeId` the plane's id, so a pick reads it as an element's),
 * its outline, and its name near its top left corner. The selected planes and the one under the cursor are drawn stronger.
 */
export function refPlanesGroup(planes: RefPlane[], bounds: { min: number[]; max: number[] } | null, selected: ReadonlySet<string>, hover: string | null): THREE.Group {
  const group = new THREE.Group();
  group.name = 'ref-planes';
  for (const p of planes) {
    const e = planeExtent(p, bounds);
    const corners = [planePoint(p, e.x0, e.y0), planePoint(p, e.x1, e.y0), planePoint(p, e.x1, e.y1), planePoint(p, e.x0, e.y1)];
    const flat = corners.flatMap((c) => [c.x, c.y, c.z]);
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(flat, 3));
    geo.setIndex([0, 1, 2, 0, 2, 3]);
    geo.computeVertexNormals();
    const on = selected.has(p.id), lit = hover === p.id;
    const mat = new THREE.MeshBasicMaterial({ color: PLANE_COLOR, transparent: true, opacity: on ? 0.28 : lit ? 0.2 : 0.1, side: THREE.DoubleSide, depthWrite: false });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.userData.nodeId = p.id;
    mesh.userData.refPlane = true;
    mesh.renderOrder = 2;
    group.add(mesh);
    const outline = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(corners.map((c) => new THREE.Vector3(c.x, c.y, c.z))),
      new THREE.LineDashedMaterial({ color: PLANE_COLOR, dashSize: 0.4, gapSize: 0.2, transparent: true, opacity: on || lit ? 1 : 0.7 }));
    outline.computeLineDistances();
    outline.userData.nodeId = p.id;
    group.add(outline);
    // A cross at the origin, where its drawing's (0, 0) is.
    const arm = 0.3;
    const v3 = (q: Vec3) => new THREE.Vector3(q.x, q.y, q.z);
    const crossGeo = new THREE.BufferGeometry().setFromPoints([v3(planePoint(p, -arm, 0)), v3(planePoint(p, arm, 0)), v3(planePoint(p, 0, -arm)), v3(planePoint(p, 0, arm))]);
    group.add(new THREE.LineSegments(crossGeo, new THREE.LineBasicMaterial({ color: PLANE_COLOR, transparent: true, opacity: 0.8 })));
    const tag = label(p.name);
    const at = planePoint(p, e.x0 + 0.2, e.y1 - 0.2, 0.02);
    tag.position.set(at.x, at.y, at.z);
    tag.center.set(0, 1);
    group.add(tag);
  }
  return group;
}

/** The plane's middle (of its drawn quad), where its handle stands. */
export function planeCentre(p: RefPlane, bounds: { min: number[]; max: number[] } | null): Vec3 {
  const e = planeExtent(p, bounds);
  return planePoint(p, (e.x0 + e.x1) / 2, (e.y0 + e.y1) / 2);
}

/**
 * How far along the plane's normal the pointer's ray points, from `at`: the point of the normal's line nearest the ray
 * (from `origin` along `dir`); null when the ray runs along the normal.
 */
export function alongNormal(p: RefPlane, at: Vec3, origin: Vec3, dir: Vec3): number | null {
  const u = p.normal;
  const w = { x: at.x - origin.x, y: at.y - origin.y, z: at.z - origin.z };
  const b = dot(u, dir), c = dot(dir, dir), d = dot(u, w), e = dot(dir, w);
  const den = c - b * b;
  if (Math.abs(den) < 1e-9) return null;
  return (b * e - c * d) / den;
}
