/**
 * An imported DXF in the 3D view (a tester's report #106, decision 382): the
 * drawing a DXF brought into a plan, section, elevation or reference plane,
 * laid on that view's plane in the model, as the engine batches it
 * (`drafting.underlays3d`: one batch a view's plane, import and layer). Each
 * batch is one LineSegments (a colour a vertex, thin, never picked) and its
 * hatches one faint mesh, so a drawing of tens of thousands of lines is a few
 * objects. They are asked for again only when what they are made of changes:
 * the DXF's shapes, the project (its layers, its imports' switches), a link,
 * a view or a storey. View → DXF in 3D shows or hides them all.
 */

import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import type { Vec3 } from './camera';

/** One batch as the engine sends it: the segments' ends and the fills' triangles in the model, base64 (float32, rgb bytes). */
export interface UnderlayBatch {
  key: string; viewId: string; storeyId: string | null; source: string | null; layer: string;
  normal: [number, number, number]; segments: number; triangles: number;
  lines: string; lineColors: string; fills: string; fillColors: string;
}

/** Lifted off their plane by this much (m), towards the side it is seen from, so a slab's top does not swallow them. */
export const LIFT = 0.002;

/** A drafted entity that is a DXF's flat drawing (its heights make it an element in space instead). */
export const isUnderlayNode = (n: NodeDto): boolean =>
  n.type === 'IfcAnnotation' && n.props.predefinedType === 'DRAFT' && (n.props.dxf === true || typeof n.props.dxfImport === 'string') && !Array.isArray(n.props.heights);

/** What the batches are made of: the DXF shapes, and what places or hides them (the project, links, views, storeys). */
export function underlaySources(nodes: ReadonlyMap<string, NodeDto>): { refs: NodeDto[]; any: boolean } {
  const refs: NodeDto[] = [];
  let any = false;
  for (const n of nodes.values()) {
    if (n.type === 'IfcAnnotation') { if (isUnderlayNode(n)) { refs.push(n); any = true; } }
    else if (n.type === 'IfcProject' || n.type === 'LapinLink' || n.type === 'LapinView' || n.type === 'IfcBuildingStorey') refs.push(n);
  }
  return { refs, any };
}

/** Whether two snapshots of the sources are the same nodes (an edit replaces a node, so identity tells). */
export const sameSources = (a: readonly NodeDto[], b: readonly NodeDto[]): boolean => a.length === b.length && a.every((x, i) => x === b[i]);

/** Base64 into bytes. */
export function bytesOf(b64: string): Uint8Array {
  const s = atob(b64);
  const o = new Uint8Array(s.length);
  for (let i = 0; i < s.length; i++) o[i] = s.charCodeAt(i);
  return o;
}

/** Base64 of little-endian float32s into floats. */
export function floatsOf(b64: string): Float32Array {
  const b = bytesOf(b64);
  const o = new Float32Array(Math.floor(b.length / 4));
  const v = new DataView(b.buffer, b.byteOffset, b.byteLength);
  for (let i = 0; i < o.length; i++) o[i] = v.getFloat32(i * 4, true);
  return o;
}

/**
 * The colours as the theme reads them: a CAD drawing's white (ACI 7) is black on a light background, and its black
 * light on a dark one, as a CAD program shows them.
 */
export function themed(rgb: Uint8Array, dark: boolean): Uint8Array {
  const o = new Uint8Array(rgb.length);
  for (let i = 0; i + 2 < rgb.length; i += 3) {
    const r = rgb[i], g = rgb[i + 1], b = rgb[i + 2];
    const lum = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
    const flip = dark ? lum < 0.12 : lum > 0.9;
    const v = dark ? 0xd4 : 0x27;
    o[i] = flip ? v : r; o[i + 1] = flip ? v : g; o[i + 2] = flip ? v : b;
  }
  return o;
}

const NO_RAYCAST = () => undefined;

let lineMaterial: THREE.LineBasicMaterial | null = null;
let fillMaterial: THREE.MeshBasicMaterial | null = null;
const materials = () => {
  // Shared: thin lines in their own colours, drawn over what lies on their plane, never writing depth; fills faint.
  lineMaterial ??= Object.assign(new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.9, depthWrite: false }), { userData: { shared: true } });
  fillMaterial ??= Object.assign(new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.25, depthWrite: false, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 }), { userData: { shared: true } });
  return { line: lineMaterial, fill: fillMaterial };
};

/** The batches as objects: a group holding, a batch each, its lines and its fill, lifted along the plane's normal. */
export function underlayGroup(batches: readonly UnderlayBatch[], dark: boolean): THREE.Group {
  const group = new THREE.Group();
  group.name = 'cad-underlays';
  const { line, fill } = materials();
  for (const b of batches) {
    const lift = new THREE.Vector3(...b.normal).multiplyScalar(LIFT);
    if (b.segments > 0) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(floatsOf(b.lines), 3));
      g.setAttribute('color', new THREE.BufferAttribute(themed(bytesOf(b.lineColors), dark), 3, true));
      const o = new THREE.LineSegments(g, line);
      o.position.copy(lift);
      o.raycast = NO_RAYCAST;
      o.userData = { underlay: b.key, viewId: b.viewId, storeyId: b.storeyId, layer: b.layer };
      group.add(o);
    }
    if (b.triangles > 0) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(floatsOf(b.fills), 3));
      g.setAttribute('color', new THREE.BufferAttribute(themed(bytesOf(b.fillColors), dark), 3, true));
      const o = new THREE.Mesh(g, fill);
      o.position.copy(lift);
      o.raycast = NO_RAYCAST;
      o.renderOrder = -1;
      o.userData = { underlayFill: b.key };
      group.add(o);
    }
  }
  return group;
}

/** The group's geometries freed (the materials are shared). */
export function disposeUnderlays(group: THREE.Group): void {
  group.traverse((o) => { const g = (o as THREE.Mesh).geometry; if (g) g.dispose(); });
}

/**
 * The underlays' segments lying in a plane (both ends within tol of it, as their geometry has them, before the lift), for
 * the 3D sketch and drafting to snap to (decisions 374–375): at most `max`, only of batches whose bounds reach the plane.
 */
export function underlaySegmentsInPlane(scene: THREE.Object3D | null, plane: { origin: Vec3; normal: Vec3 }, max = 20000, tol = 1e-3): [Vec3, Vec3][] {
  const out: [Vec3, Vec3][] = [];
  const group = scene?.getObjectByName('cad-underlays');
  if (!group || !group.visible) return out;
  const n = plane.normal, o = plane.origin;
  const off = (x: number, y: number, z: number) => (x - o.x) * n.x + (y - o.y) * n.y + (z - o.z) * n.z;
  const th = new THREE.Plane().setFromNormalAndCoplanarPoint(new THREE.Vector3(n.x, n.y, n.z), new THREE.Vector3(o.x, o.y, o.z));
  for (const c of group.children) {
    if (out.length >= max) break;
    const ls = c as THREE.LineSegments;
    if (!ls.isLineSegments || !ls.visible) continue;
    const g = ls.geometry;
    if (!g.boundingBox) g.computeBoundingBox();
    if (!g.boundingBox!.clone().expandByScalar(tol).intersectsPlane(th)) continue;
    const p = g.getAttribute('position').array as Float32Array;
    for (let k = 0; k + 5 < p.length && out.length < max; k += 6) {
      if (Math.abs(off(p[k], p[k + 1], p[k + 2])) > tol || Math.abs(off(p[k + 3], p[k + 4], p[k + 5])) > tol) continue;
      out.push([{ x: p[k], y: p[k + 1], z: p[k + 2] }, { x: p[k + 3], y: p[k + 4], z: p[k + 5] }]);
    }
  }
  return out;
}
