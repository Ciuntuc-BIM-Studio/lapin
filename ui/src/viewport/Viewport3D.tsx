/**
 * The 3D view: the engine's meshes in three.js (metres, Z up), on the
 * architect's-sheet palette. A click selects; with a drawing tool on, clicks
 * on the active storey's plane give its points.
 */

import { useEffect, useRef, useState } from 'react';
import { useShallow } from 'zustand/react/shallow';
import { useLooks, type Look } from '../platform/looks';
import { useSunlight } from '../platform/sunlight';
import { pickable } from '../platform/scope';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { LineSegments2 } from 'three/examples/jsm/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/examples/jsm/lines/LineSegmentsGeometry.js';
import { LineMaterial } from 'three/examples/jsm/lines/LineMaterial.js';
import { X } from 'lucide-react';
import type { CapItem, NodeDto, RenderItem } from '../engine/protocol';
import { activeStorey, engine, execute, useSession } from '../engine/session';
import { num, storeys as storeysOf } from '../engine/model';
import { chipText, cutZ, settingOf } from './storeys3d';
import { showAllStoreys3d } from './storeys3dActions';
import { everyLayerPatch, layers3dOf, layersChip, shownIn } from './layers3d';
import { select, setTool, useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { FaceHit, Vec2 } from '../platform/module';
import { completeTool } from './tools';
import { boxUv, boxUvSized, imageOf, imageTexture, orthophotos, orthoTexture, orthoUv, splitByFace, textureFor, textureOf, whenImagesLoad, type Face, type TextureKind } from './textures';
import { KeyPlan } from './KeyPlan';
import { poseOf, positionOf, useCamera, type CameraApi, type CameraPose } from './camera';
import { Grips3D } from './Grips3D';
import { SectionBox3D } from './SectionBox3D';
import { boxPlanes, planesKey, useSectionBox, type SectionBox } from './sectionBox';
import { alignSectionBox, commitBox, cutAtPlan, fitSectionBox } from './sectionBoxTools';
import { useDrawing, type DrawingDto } from './drawing';
import { CUT_LAYERS, cssColor, dashSegments, drawingPoint, penPixels, evenOddShapes, onCut, sectionPlanes, sectionPose, textFont, textQuad, useClip3d, useSection3d, type ClipPlane, type DrawingText, type ViewFrameDto } from './section3d';
import { clampPitch, easeEye, exitWalk, lookVector, toggleFly, useWalk, walkStep, walkWish, EYE_HEIGHT, WALK_KEYS, type WalkStart } from './walk';
import { TICK, Walker, worldOf } from './walkPhysics';
import { InstanceLayer } from './instancing';
import { inWorld } from '../engine/instances';

const CLEAR = { paper: 0xf3efe7, graphite: 0x1c1b19 } as const;
/** The ground grid per theme: its axes in the accent, its lines a shade off the paper. */
const GRID = { paper: [0xb5532d, 0xd6cebf], graphite: [0xe07a4f, 0x34312c] } as const;
const SELECTED = new THREE.Color(0xb5532d);
/** The edges' one material, shared by every element (highlights and looks change only the faces'). */
const EDGES = new THREE.LineBasicMaterial({ color: 0x1e2a3a });
EDGES.userData.shared = true;

function groundGrid(theme: keyof typeof GRID, z: number): THREE.GridHelper {
  const grid = new THREE.GridHelper(60, 60, GRID[theme][0], GRID[theme][1]);
  grid.rotation.x = Math.PI / 2;
  grid.position.z = z;
  grid.name = 'ground-grid';
  return grid;
}

/** A surface's meshes in its material (its own image, or its texture tinted by its colour, per face when they differ) added to `group`. */
function addSurface(group: THREE.Group, nodeId: string, positions: Float32Array, normals: Float32Array, indices: Int32Array, color: string, material?: string | null) {
  const nodes = useSession.getState().nodes;
  {
    if (positions.length) {
      const mat = material ? nodes.get(material) : undefined;
      // A terrain under an orthophoto wears it, laid by the photo's corners in plan.
      const owner = nodes.get(nodeId);
      const ortho = owner?.type === 'IfcGeographicElement' && owner.props.predefinedType === 'TERRAIN' ? orthophotos(nodes)[0] : undefined;
      if (ortho) {
        // The photo on the ground's top only; its sides and bottom keep the terrain's colour.
        const top: number[] = [], rest: number[] = [];
        for (let k = 0; k + 2 < indices.length; k += 3) {
          const [a, b, c] = [indices[k], indices[k + 1], indices[k + 2]];
          const ux = positions[3 * b] - positions[3 * a], uy = positions[3 * b + 1] - positions[3 * a + 1], uz = positions[3 * b + 2] - positions[3 * a + 2];
          const vx = positions[3 * c] - positions[3 * a], vy = positions[3 * c + 1] - positions[3 * a + 1], vz = positions[3 * c + 2] - positions[3 * a + 2];
          const nz = ux * vy - uy * vx, len = Math.hypot(uy * vz - uz * vy, uz * vx - ux * vz, nz);
          (len > 0 && nz / len > 0.3 ? top : rest).push(a, b, c);
        }
        indices = Int32Array.from(rest);
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        if (normals.length === positions.length) g.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
        else g.computeVertexNormals();
        g.setIndex(new THREE.BufferAttribute(Uint32Array.from(top), 1));
        const uv = new Float32Array((positions.length / 3) * 2);
        for (let i = 0; i < positions.length / 3; i++) { const [u, v] = orthoUv(ortho, positions[3 * i], positions[3 * i + 1]); uv[2 * i] = u; uv[2 * i + 1] = v; }
        g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
        const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color: 0xffffff, map: orthoTexture(ortho), roughness: 0.95, metalness: 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 }));
        mesh.userData.nodeId = nodeId;
        mesh.castShadow = mesh.receiveShadow = true;
        group.add(mesh);
      }
      // The material's own image: on every face, untinted, at its size in metres.
      const own = imageOf(mat, nodes);
      if (own) {
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        if (normals.length === positions.length) g.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
        else g.computeVertexNormals();
        g.setIndex(new THREE.BufferAttribute(new Uint32Array(indices.buffer, indices.byteOffset, indices.length), 1));
        boxUvSized(g, own.width, own.height);
        const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color: 0xffffff, map: imageTexture(own), roughness: 0.85, metalness: 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 }));
        mesh.userData.nodeId = nodeId;
        mesh.castShadow = mesh.receiveShadow = true;
        group.add(mesh);
      }
      const kinds: Record<Face, TextureKind | null> = { side: textureOf(mat, 'side'), end: textureOf(mat, 'end'), top: textureOf(mat, 'top') };
      const all = new Uint32Array(indices.buffer, indices.byteOffset, indices.length);
      // One mesh, or one per face when the ends or the top look different (a brick wall's cut ends).
      const faces = own ? {} : kinds.end !== kinds.side || kinds.top !== kinds.side ? splitByFace(positions, all) : { side: all };
      for (const [face, idx] of Object.entries(faces) as [Face, Uint32Array][]) {
        if (!idx.length) continue;
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        if (normals.length === positions.length) g.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
        else g.computeVertexNormals();
        g.setIndex(new THREE.BufferAttribute(idx, 1));
        // The material's texture, tinted by its colour, laid on in metres.
        const kind = kinds[face];
        if (kind) boxUv(g, kind);
        const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({
          color: kind ? 0xffffff : new THREE.Color(color), map: kind ? textureFor(kind, color) : null,
          roughness: kind === 'metal' ? 0.45 : 0.85, metalness: kind === 'metal' ? 0.3 : 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
        }));
        mesh.userData.nodeId = nodeId;
        mesh.castShadow = mesh.receiveShadow = true;
        group.add(mesh);
      }
    }
  }
}

function meshOf(item: RenderItem): THREE.Group {
  const group = new THREE.Group();
  group.userData.nodeId = item.nodeId;
  const add = (positions: Float32Array, normals: Float32Array, indices: Int32Array, edges: Float32Array, color: string, material?: string | null) => {
    addSurface(group, item.nodeId, positions, normals, indices, color, material);
    if (edges.length) {
      const e = new THREE.BufferGeometry();
      e.setAttribute('position', new THREE.BufferAttribute(edges, 3));
      group.add(new THREE.LineSegments(e, EDGES));
    }
  };
  add(item.positions, item.normals, item.indices, item.edges, item.color);
  for (const p of item.parts ?? []) add(p.positions, p.normals, p.indices, p.edges, p.color, p.material);
  return group;
}

/** The caps' hatch lines, as thin as a sheet's (one material, shared). */
const HATCH = new THREE.LineBasicMaterial({ color: 0x2b2b2b });
HATCH.userData.shared = true;

/** Cut outlines on the caps: a little heavier than the model's edges, so the cut reads first. */
const CAP_EDGE_PX = 1.6;

/**
 * An element's caps where the section cuts it (view.caps): each part's in
 * its own material, as its faces are, outlined in the cut's line.
 */
function capsOf(item: CapItem): THREE.Group {
  const group = new THREE.Group();
  group.userData.nodeId = item.nodeId;
  group.userData.caps = true;
  const edges: number[] = [];
  const hatch: number[] = [];
  for (const p of item.parts) {
    if (p.fill) {
      // As a sheet draws the cut: the material's section fill, flat, and its hatch over it.
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(p.positions, 3));
      g.setAttribute('normal', new THREE.BufferAttribute(p.normals, 3));
      g.setIndex(new THREE.BufferAttribute(new Uint32Array(p.indices.buffer, p.indices.byteOffset, p.indices.length), 1));
      const fill = cssColor(p.fill);
      const mesh = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: new THREE.Color(fill?.hex ?? '#ffffff') }));
      mesh.userData.nodeId = item.nodeId;
      group.add(mesh);
      if (p.hatch) for (const v of p.hatch) hatch.push(v);
    } else addSurface(group, item.nodeId, p.positions, p.normals, p.indices, p.color, p.material);
    for (const v of p.edges) edges.push(v);
  }
  if (hatch.length) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(hatch), 3));
    group.add(new THREE.LineSegments(g, HATCH));
  }
  if (edges.length) {
    const mat = new LineMaterial({ color: 0x1e2a3a, linewidth: CAP_EDGE_PX });
    const lines = new LineSegments2(new LineSegmentsGeometry().setPositions(edges), mat);
    lines.onBeforeRender = (renderer) => { renderer.getSize(mat.resolution); };
    group.add(lines);
  }
  return group;
}

function highlight(group: THREE.Object3D, on: boolean) {
  group.traverse((c) => {
    const m = (c as THREE.Mesh).material as THREE.MeshStandardMaterial | undefined;
    if (m && 'emissive' in m) m.emissive = on ? SELECTED : new THREE.Color(0);
    if (m && 'emissiveIntensity' in m) m.emissiveIntensity = on ? 0.45 : 0;
  });
}

/** A module's look (a phase's): hidden, or its faces tinted and see-through; none: its own colours back. */
function look(group: THREE.Object3D, l: Look | undefined) {
  group.visible = !l?.hidden;
  group.traverse((c) => {
    const m = (c as THREE.Mesh).material as THREE.MeshStandardMaterial | undefined;
    if (!m || !('emissive' in m)) return;
    if (m.userData.own === undefined) m.userData.own = m.color.getHex();
    m.color.setHex(l?.color ? new THREE.Color(l.color).getHex() : m.userData.own as number);
    const opacity = l?.opacity ?? 1;
    m.transparent = opacity < 1;
    m.opacity = opacity;
    m.depthWrite = opacity >= 1;
    // What is seen through casts no shadow (a buildable volume, a phase shown faded).
    (c as THREE.Mesh).castShadow = opacity >= 1;
  });
}

/** Free an object's GPU resources: geometries, materials and their textures (not the shared ones). */
function dispose(o: THREE.Object3D) {
  const free = (m: THREE.Material) => {
    if (m.userData.shared) return;
    const map = (m as THREE.MeshStandardMaterial).map;
    if (map && !map.userData.shared) map.dispose();
    m.dispose();
  };
  o.traverse((c) => {
    const m = c as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    if (Array.isArray(mat)) mat.forEach(free); else if (mat) free(mat);
    if ((c as THREE.Light).isLight) (c as THREE.Light).dispose();
  });
}

/** The lowest storey's elevation (0 at most): where the grid's axes lie. */
const gridBase = (nodes: Map<string, NodeDto>) => {
  let base = 0;
  for (const n of nodes.values()) if (n.type === 'IfcBuildingStorey') base = Math.min(base, Number(n.props.elevation ?? 0));
  return base;
};
/** A named face as the engine gives it (core.faceAt). */
interface FaceDto { role: string; label: string; normal: number[]; outline: number[][] }

/** Where a ray hit: the point, and the hit triangle's normal in the world (an instance's turned by its matrix first). */
function faceHit(hit: THREE.Intersection): FaceHit {
  const n = hit.face ? hit.face.normal.clone() : null;
  const inst = hit.object as THREE.InstancedMesh;
  if (n && inst.isInstancedMesh && hit.instanceId !== undefined) { const m = new THREE.Matrix4(); inst.getMatrixAt(hit.instanceId, m); n.transformDirection(m); }
  n?.transformDirection(hit.object.matrixWorld);
  return { point: [hit.point.x, hit.point.y, hit.point.z], ...(n ? { normal: [n.x, n.y, n.z] } : {}) };
}

/** A face lit up: its outline filled, a hair off it towards the viewer, and drawn round. */
function faceMesh(face: FaceDto): THREE.Object3D {
  const n = new THREE.Vector3(face.normal[0], face.normal[1], face.normal[2]).normalize();
  const pts = face.outline.map((p) => new THREE.Vector3(p[0], p[1], p[2]).addScaledVector(n, 0.004));
  const seed = Math.abs(n.x) < 0.9 ? new THREE.Vector3(1, 0, 0) : new THREE.Vector3(0, 1, 0);
  const u = new THREE.Vector3().crossVectors(n, seed).normalize();
  const v = new THREE.Vector3().crossVectors(n, u);
  const flat = pts.map((p) => new THREE.Vector2(p.dot(u), p.dot(v)));
  const tris = THREE.ShapeUtils.triangulateShape(flat, []);
  const geo = new THREE.BufferGeometry().setFromPoints(pts);
  geo.setIndex(tris.flat());
  const group = new THREE.Group();
  group.add(new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color: SELECTED, transparent: true, opacity: 0.45, side: THREE.DoubleSide, depthWrite: false })));
  group.add(new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: SELECTED })));
  return group;
}

/**
 * A section's drawing laid on its cut (section3d.ts): its fills a few
 * millimetres into the kept side, so they close the cut solids, its lines
 * and texts just over them (inside the kept side, so the section's own
 * clipping plane leaves them); what is seen beyond is left to the model.
 * With `annotations`, the dimensions, levels, tags and texts too, where the
 * drawing has them: those beside the building stand in the air on the plane.
 * Without `cut` (the caps drawn in their materials), only those.
 */
function sectionOverlay(d: DrawingDto, annotations: boolean, anisotropy: number, cut = true): THREE.Group {
  const f = d.frame!;
  const group = new THREE.Group();
  group.name = 'section-overlay';
  const basis = (lift: number) => {
    const o = drawingPoint(f, 0, 0, lift);
    return new THREE.Matrix4().makeBasis(new THREE.Vector3(...f.right), new THREE.Vector3(...f.up), new THREE.Vector3(...f.dir)).setPosition(o.x, o.y, o.z);
  };
  const fillAt = basis(0.004), lineAt = basis(0.002);
  for (const pen of d.pens) {
    if (!onCut(pen.layer, annotations)) continue;
    // With the caps in their materials, the drawing's own cut (fills, hatches, outlines) is not laid over them.
    if (!cut && CUT_LAYERS.has(pen.layer)) continue;
    if (pen.texts?.length) {
      for (const t of pen.texts) { const m = textMesh(t, d.scale, f, pen.stroke, anisotropy); if (m) group.add(m); }
      continue;
    }
    const fill = cssColor(pen.fill);
    if (fill && pen.loops?.length) {
      const shapes = evenOddShapes(pen.loops).map(({ outer, holes }) => {
        const s = new THREE.Shape();
        const path = (p: THREE.Path, l: number[]) => { p.moveTo(l[0], l[1]); for (let k = 2; k + 1 < l.length; k += 2) p.lineTo(l[k], l[k + 1]); p.closePath(); };
        path(s, outer);
        for (const h of holes) { const hp = new THREE.Path(); path(hp, h); s.holes.push(hp); }
        return s;
      });
      const geo = new THREE.ShapeGeometry(shapes);
      geo.applyMatrix4(fillAt);
      group.add(new THREE.Mesh(geo, new THREE.MeshBasicMaterial({
        color: new THREE.Color(fill.hex), transparent: fill.opacity < 1, opacity: fill.opacity, side: THREE.DoubleSide,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2,
      })));
    }
    const stroke = cssColor(pen.stroke);
    // Dashed as on paper, the whole pattern in the model's metres at the view's scale; as wide as the pen on a sheet at 100 %.
    const segs = pen.dash && pen.dash.length >= 2 ? dashSegments(pen.segments ?? [], pen.dash, d.scale) : pen.segments ?? [];
    if (stroke && segs.length >= 4) {
      const pos = new Float32Array((segs.length / 2) * 3);
      for (let k = 0; k + 1 < segs.length; k += 2) {
        const v = new THREE.Vector3(segs[k], segs[k + 1], 0).applyMatrix4(lineAt);
        pos.set([v.x, v.y, v.z], (k / 2) * 3);
      }
      const geo = new LineSegmentsGeometry().setPositions(pos);
      const mat = new LineMaterial({ color: new THREE.Color(stroke.hex).getHex(), linewidth: penPixels(pen.width), transparent: stroke.opacity < 1, opacity: stroke.opacity });
      const lines = new LineSegments2(geo, mat);
      // Its width is in the screen's pixels: the material learns the screen's size as it is drawn.
      lines.onBeforeRender = (renderer) => { renderer.getSize(mat.resolution); };
      group.add(lines);
    }
  }
  return group;
}

/** Pixels to a text's em on its canvas: sharp up close, mipmapped further off. */
const TEXT_PX = 64;

/**
 * A drawing's text on the section's plane: a quad as large as the 2D drawing
 * sets it (section3d.textQuad, at the view's scale), its words drawn on a
 * canvas in the same font; facing the viewer and reading left to right from
 * the side the section looks from (from behind, it is not drawn).
 */
function textMesh(t: DrawingText, scale: number | undefined, f: ViewFrameDto, color: string | null, anisotropy: number): THREE.Mesh | null {
  if (!t.text.trim() || !(t.height > 0)) return null;
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  const middle = t.baseline === 'middle';
  const setup = () => { ctx.font = textFont(t, TEXT_PX); ctx.textBaseline = middle ? 'middle' : 'alphabetic'; ctx.textAlign = 'left'; };
  setup();
  const m = ctx.measureText(t.text);
  // The font's box over and under the line the text stands on (or is centred on).
  const ascent = m.fontBoundingBoxAscent ?? TEXT_PX * (middle ? 0.6 : 0.95);
  const descent = m.fontBoundingBoxDescent ?? TEXT_PX * (middle ? 0.6 : 0.25);
  const pad = 2;
  canvas.width = Math.ceil(m.width + 2 * pad);
  canvas.height = Math.ceil(ascent + descent + 2 * pad);
  setup();
  ctx.fillStyle = cssColor(color)?.hex ?? '#000000';
  ctx.fillText(t.text, pad, pad + ascent);
  // The quad as large as the canvas (its rounding up, under a pixel, taken in), the anchor where the text stands.
  const q = textQuad(t, scale, (canvas.width - 2 * pad) / TEXT_PX, ascent / TEXT_PX, (canvas.height - 2 * pad - ascent) / TEXT_PX, pad / TEXT_PX);
  const pos = new Float32Array(12);
  q.forEach((p, i) => { const w = drawingPoint(f, p.x, p.y, 0.001); pos.set([w.x, w.y, w.z], i * 3); });
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  geo.setAttribute('uv', new THREE.BufferAttribute(new Float32Array([0, 0, 1, 0, 1, 1, 0, 1]), 2));
  geo.setIndex([0, 1, 2, 0, 2, 3]);
  const map = new THREE.CanvasTexture(canvas);
  map.colorSpace = THREE.SRGBColorSpace;
  map.anisotropy = anisotropy;
  const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({
    map, transparent: true, depthWrite: false, side: THREE.FrontSide,
    polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -4,
  }));
  mesh.renderOrder = 2;
  return mesh;
}

const gridsOf = (nodes: Map<string, NodeDto>) => [...nodes.values()].filter((n) => n.type === 'IfcGrid');

export function Viewport3D() {
  const host = useRef<HTMLDivElement>(null);
  const scene = useRef<THREE.Scene | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  /** The camera in use (perspective or orthographic), for the grips drawn over the scene. */
  const camRef = useRef<THREE.Camera | null>(null);
  // Frames are drawn on demand: when the camera moves, the view resizes or the scene changes.
  const invalidate = useRef<() => void>(() => undefined);
  const items = useRef(new Map<string, { item: RenderItem; object: THREE.Group }>());
  /** The elements drawn as instances of shared meshes (instancing.ts). */
  const instances = useRef<InstanceLayer | null>(null);
  /** The cut elements' caps, by element (a section or the section box shown). */
  const caps = useRef(new Map<string, THREE.Group>());
  /** The caps' meshes as the engine sent them, for the walker to collide with. */
  const capItems = useRef<CapItem[]>([]);
  const [capsShown, setCapsShown] = useState(0);
  const [pending, setPending] = useState<Vec2 | null>(null);
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  // What a click does, kept current for the listeners set up once.
  const clickRef = useRef<(hit: { nodeId: string | null; ground: Vec2 | null; add: boolean; face?: FaceHit }) => void>(() => undefined);
  /** The classes a face tool works on (null: no face tool on), for the hover that lights up the face under the cursor. */
  const faceTypes = useRef<string[] | null>(null);
  const clearFace = useRef<() => void>(() => undefined);
  const [faceLabel, setFaceLabel] = useState<{ text: string; x: number; y: number } | null>(null);
  /** Walking: started and stopped by the walk's store (set up with the scene). */
  const walkApi = useRef<{ start(w: WalkStart): void; stop(): void; changed(): void }>({ start: () => undefined, stop: () => undefined, changed: () => undefined });
  /** The section the camera was last turned to, so it turns once per showing, not on every edit. */
  const framedSection = useRef<string | null>(null);
  const elevationRef = useRef(0);
  /** The clipping planes: a section's shown in 3D, and the cut above the storeys shown; the renderer takes both. */
  const sectionClip = useRef<THREE.Plane[]>([]);
  const storeyClip = useRef<THREE.Plane[]>([]);
  /** Where the storeys shown are cut (a height), so a click above it picks nothing cut away. */
  const cutRef = useRef<number | null>(null);
  const applyClip = () => {
    const all = [...sectionClip.current, ...storeyClip.current];
    if (rendererRef.current) rendererRef.current.clippingPlanes = all;
    useClip3d.setState({ planes: all.map((p) => ({ normal: [p.normal.x, p.normal.y, p.normal.z], constant: p.constant })) });
  };
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  const storey = activeStorey();
  elevationRef.current = num(storey ?? undefined, 'elevation');

  useEffect(() => { setPending(null); clearFace.current(); setFaceLabel(null); }, [toolId]);
  faceTypes.current = tool?.gesture?.kind === 'face' ? tool.gesture.types : null;

  clickRef.current = ({ nodeId, ground, add, face }) => {
    const g = tool?.gesture;
    if (!g) { select(nodeId && pickable(useSession.getState().nodes.get(nodeId)) ? nodeId : null, add); return; }
    if (g.kind === 'face' || g.kind === 'pick') {
      const node = nodeId ? useSession.getState().nodes.get(nodeId) : undefined;
      if (!node || !g.types.includes(node.type)) { useSession.setState({ error: 'Pick an element this tool works on.' }); return; }
      void completeTool(tool!, { points: ground ? [ground] : [], entity: node, face });
      return;
    }
    if (!ground) return;
    if (g.kind === 'point') { void completeTool(tool!, { points: [ground] }); return; }
    if (g.kind === 'twoPoints' || g.kind === 'rectangle') {
      if (!pending) { setPending(ground); return; }
      void completeTool(tool!, { points: [pending, ground] });
      setPending(g.kind === 'twoPoints' && g.repeat ? ground : null);
    }
  };

  useEffect(() => {
    const el = host.current!;
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setClearColor(CLEAR[useUi.getState().theme]);
    rendererRef.current = renderer;
    el.appendChild(renderer.domElement);
    THREE.Object3D.DEFAULT_UP.set(0, 0, 1);
    const s = new THREE.Scene();
    scene.current = s;
    const persp = new THREE.PerspectiveCamera(45, 1, 0.1, 5000);
    persp.up.set(0, 0, 1);
    persp.position.set(18, -22, 16);
    const orthoCam = new THREE.OrthographicCamera(-1, 1, 1, -1, -5000, 5000);
    orthoCam.up.set(0, 0, 1);
    let cam: THREE.PerspectiveCamera | THREE.OrthographicCamera = persp;
    camRef.current = cam;
    const controls = new OrbitControls(persp, renderer.domElement);
    controls.target.set(4, 3, 0);
    controls.update();

    // The camera as the key plan sees and steers it.
    const pose = (): CameraPose => poseOf(cam.position, controls.target);
    const orthoFrustum = (distance: number) => {
      const halfH = distance * Math.tan((persp.fov * Math.PI) / 360);
      orthoCam.left = -halfH * persp.aspect; orthoCam.right = halfH * persp.aspect; orthoCam.top = halfH; orthoCam.bottom = -halfH;
      orthoCam.updateProjectionMatrix();
    };
    const applyPose = (p: CameraPose) => {
      const at = positionOf(p);
      cam.position.set(at.x, at.y, at.z);
      controls.target.set(p.target.x, p.target.y, p.target.z);
      if (cam === orthoCam) orthoFrustum(p.distance);
      controls.update();
    };
    const publish = () => {
      const p = pose();
      useCamera.setState({ ...p, position: { x: cam.position.x, y: cam.position.y, z: cam.position.z }, fov: (persp.fov * Math.PI) / 180, ortho: cam === orthoCam });
    };
    let flight = 0;
    const flyTo = (goal: CameraPose, animate = true) => {
      cancelAnimationFrame(flight);
      if (!animate) { applyPose(goal); publish(); return; }
      const from = pose();
      const dAz = Math.atan2(Math.sin(goal.azimuth - from.azimuth), Math.cos(goal.azimuth - from.azimuth));
      const t0 = performance.now();
      const step = (now: number) => {
        const t = Math.min(1, (now - t0) / 250), e = t * (2 - t);
        const mix = (a: number, b: number) => a + (b - a) * e;
        applyPose({ azimuth: from.azimuth + dAz * e, pitch: mix(from.pitch, goal.pitch), distance: mix(from.distance, goal.distance), target: { x: mix(from.target.x, goal.target.x), y: mix(from.target.y, goal.target.y), z: mix(from.target.z, goal.target.z) } });
        publish();
        if (t < 1) flight = requestAnimationFrame(step);
      };
      flight = requestAnimationFrame(step);
    };
    const api: CameraApi = {
      orbitTo: (part, animate = true) => flyTo({ ...pose(), ...part }, animate),
      lookAt: (p) => { const at = cam.position; flyTo(poseOf({ x: at.x, y: at.y, z: at.z }, p), false); },
      moveTo: (p) => { const cur = pose(); const dx = p.x - cam.position.x, dy = p.y - cam.position.y; flyTo({ ...cur, target: { x: cur.target.x + dx, y: cur.target.y + dy, z: cur.target.z } }, false); },
      setHeight: (z) => { const cur = pose(); flyTo({ ...cur, target: { ...cur.target, z: cur.target.z + (z - cam.position.z) } }, false); },
      fit: (animate = true) => {
        const box = new THREE.Box3();
        for (const { object } of live.values()) if (object.visible) box.expandByObject(object);
        layer.expand(box, useLooks.getState().looks);
        if (box.isEmpty()) box.set(new THREE.Vector3(-5, -5, 0), new THREE.Vector3(5, 5, 3));
        const centre = box.getCenter(new THREE.Vector3()), size = box.getSize(new THREE.Vector3());
        const radius = Math.max(size.length() / 2, 1);
        const distance = (radius / Math.sin((persp.fov * Math.PI) / 360)) * 1.05;
        flyTo({ ...pose(), target: { x: centre.x, y: centre.y, z: centre.z }, distance }, animate);
      },
      setOrtho: (on) => {
        const p = pose();
        if (cam === orthoCam && !on) { p.distance /= orthoCam.zoom; orthoCam.zoom = 1; }
        cam = on ? orthoCam : persp;
        camRef.current = cam;
        controls.object = cam;
        applyPose(p);
        publish();
        invalidate.current();
      },
    };
    useCamera.setState({ api });
    s.add(new THREE.HemisphereLight(0xffffff, 0xd6cebf, 2.2));
    const hemi = s.children[s.children.length - 1] as THREE.HemisphereLight;
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(20, -30, 40);
    s.add(sun);
    s.add(sun.target);
    // A module's sun (the sun study): aimed from its direction over the model, casting shadows; none: the fixed light back.
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    // The ground the shadows fall on where there is no terrain: invisible but for them.
    const shadowGround = new THREE.Mesh(new THREE.PlaneGeometry(4000, 4000), new THREE.ShadowMaterial({ opacity: 0.28 }));
    shadowGround.receiveShadow = true;
    shadowGround.visible = false;
    s.add(shadowGround);
    const aimSun = () => {
      const { dir } = useSunlight.getState();
      shadowGround.visible = !!dir && dir[2] > 0.005;
      shadowGround.position.z = elevationRef.current - 0.002;
      if (!dir) {
        sun.castShadow = false; sun.intensity = 1.6; hemi.intensity = 2.2;
        sun.position.set(20, -30, 40); sun.target.position.set(0, 0, 0);
        invalidate.current();
        return;
      }
      const box = new THREE.Box3();
      for (const { object } of items.current.values()) if (object.visible) box.expandByObject(object);
      instances.current?.expand(box, useLooks.getState().looks);
      if (box.isEmpty()) box.set(new THREE.Vector3(-10, -10, 0), new THREE.Vector3(10, 10, 10));
      const centre = box.getCenter(new THREE.Vector3());
      const radius = Math.max(box.getSize(new THREE.Vector3()).length() / 2, 5);
      const up = dir[2] > 0.005;
      sun.target.position.copy(centre);
      sun.position.set(centre.x + dir[0] * radius * 2, centre.y + dir[1] * radius * 2, centre.z + dir[2] * radius * 2);
      sun.intensity = up ? 2.6 : 0;
      hemi.intensity = up ? 1.1 : 0.6;
      sun.castShadow = up;
      const c = sun.shadow.camera;
      c.left = -radius; c.right = radius; c.top = radius; c.bottom = -radius; c.near = 0.1; c.far = radius * 4;
      c.updateProjectionMatrix();
      sun.shadow.mapSize.set(2048, 2048);
      sun.shadow.bias = -0.0005;
      sun.shadow.normalBias = 0.02;
      invalidate.current();
    };
    const unsunlight = useSunlight.subscribe(aimSun);
    s.add(groundGrid(useUi.getState().theme, elevationRef.current));
    const layer = new InstanceLayer(EDGES);
    s.add(layer.group);
    instances.current = layer;

    let frame = 0;
    invalidate.current = () => {
      if (!frame) frame = requestAnimationFrame(() => { frame = 0; renderer.render(s, cam); });
    };
    controls.addEventListener('change', invalidate.current);
    controls.addEventListener('change', publish);
    // A material's image arrives after its mesh: draw again then.
    const unlisten = whenImagesLoad(() => invalidate.current());
    const resize = () => {
      const { clientWidth: w, clientHeight: h } = el;
      renderer.setSize(w, h);
      persp.aspect = w / Math.max(h, 1);
      persp.updateProjectionMatrix();
      orthoFrustum(pose().distance);
      invalidate.current();
    };
    const ro = new ResizeObserver(resize);
    ro.observe(el);
    resize();
    publish();
    const live = items.current;

    // A click (not a drag): the element under it, and the point on the active storey's plane.
    const ray = new THREE.Raycaster();
    // What the ray meets first among the elements shown (not those hidden, nor what the storeys' cut takes away).
    const pickAt = () => {
      const meshes: THREE.Object3D[] = [];
      for (const { object } of live.values()) if (object.visible) meshes.push(object);
      meshes.push(...layer.pickables());
      const cut = cutRef.current;
      return ray.intersectObjects(meshes, true).find((h) => nodeOfHit(h) && (cut === null || h.point.z <= cut + 1e-3));
    };
    /** The element a hit is on: its own mesh's, or the instance's. */
    const nodeOfHit = (h: THREE.Intersection | undefined) => (h ? (h.object.userData.nodeId as string | undefined) ?? layer.nodeOf(h) : undefined);
    let down: { x: number; y: number } | null = null;
    const onDown = (e: PointerEvent) => { down = { x: e.clientX, y: e.clientY }; };
    const onUp = (e: PointerEvent) => {
      const start = down;
      down = null;
      if (!start || e.button !== 0 || Math.hypot(e.clientX - start.x, e.clientY - start.y) > 4) return;
      const r = renderer.domElement.getBoundingClientRect();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), cam);
      const hit = pickAt();
      const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), -elevationRef.current);
      const g = ray.ray.intersectPlane(plane, new THREE.Vector3());
      clickRef.current({
        nodeId: nodeOfHit(hit) ?? null,
        ground: g ? { x: Math.round(g.x * 20) / 20, y: Math.round(g.y * 20) / 20 } : null,
        add: e.shiftKey,
        face: hit ? faceHit(hit) : undefined,
      });
    };
    // A face tool: the face under the cursor, as the engine names it, lit up with its name beside the cursor.
    const faceGroup = new THREE.Group();
    faceGroup.name = 'face-hover';
    s.add(faceGroup);
    let hoverFrame = 0;
    let hoverKey = '';
    const showFace = (face: FaceDto | null) => {
      for (const c of [...faceGroup.children]) { faceGroup.remove(c); dispose(c); }
      if (face) faceGroup.add(faceMesh(face));
      invalidate.current();
    };
    clearFace.current = () => { hoverKey = ''; showFace(null); };
    const onMove = (e: PointerEvent) => {
      const types = faceTypes.current;
      if (!types || down || hoverFrame) return;
      const at = { x: e.clientX, y: e.clientY };
      hoverFrame = requestAnimationFrame(() => {
        hoverFrame = 0;
        const r = renderer.domElement.getBoundingClientRect();
        ray.setFromCamera(new THREE.Vector2(((at.x - r.left) / r.width) * 2 - 1, -((at.y - r.top) / r.height) * 2 + 1), cam);
        const hit = pickAt();
        const id = nodeOfHit(hit);
        const node = id ? useSession.getState().nodes.get(id) : undefined;
        if (!hit || !node || !types.includes(node.type)) { if (hoverKey) { hoverKey = ''; showFace(null); setFaceLabel(null); } return; }
        const f = faceHit(hit);
        void engine.call<FaceDto | null>('doc.query', { name: 'core.faceAt', args: { id: node.id, point: f.point, normal: f.normal } }).then((face) => {
          if (faceTypes.current !== types) return;
          const key = face ? `${node.id}/${face.role}` : '';
          if (key !== hoverKey) { hoverKey = key; showFace(face); }
          setFaceLabel(face ? { text: `${node.name} · ${face.label}`, x: at.x, y: at.y } : null);
        }).catch(() => undefined);
      });
    };
    renderer.domElement.addEventListener('pointermove', onMove);
    // Walking (walk.ts): the orbit controls off, the perspective camera at the eye, turned by a drag, moved by keys and the wheel;
    // a body (walkPhysics.ts) against the shown elements' triangles, built on the walk's first step and again when the model changes;
    // clipped by the planes the renderer cuts by (the storeys' cut, a section, the section box) and closed by their caps, so what is
    // cut away stops nobody.
    let walk: {
      pos: THREE.Vector3; yaw: number; pitch: number; back: CameraPose; wasOrtho: boolean; keys: Set<string>; run: boolean; jump: boolean; loop: number; last: number; drag: { x: number; y: number } | null;
      body: Walker | null; stale: boolean; acc: number;
    } | null = null;
    /** The walker's body, its world (re)built when stale; null while the model has nothing to stand on yet. */
    const bodyOf = (w: NonNullable<typeof walk>): Walker | null => {
      if (!w.stale) return w.body;
      w.stale = false;
      const planes = (renderer.clippingPlanes ?? []).map((p) => ({ normal: [p.normal.x, p.normal.y, p.normal.z] as const, constant: p.constant }));
      // The instances too, placed: all of them in a small model, those within 60 m of the walker in a big one.
      const looks = useLooks.getState().looks, meshes = useSession.getState().meshes;
      const few = layer.count() <= 5000;
      const near = [...layer.placed()]
        .filter(({ item, at }) => !looks[item.nodeId]?.hidden && (few || Math.hypot(at[0] - w.pos.x, at[1] - w.pos.y) < 60))
        .map(({ item }) => inWorld(item, meshes));
      const world = worldOf([...Array.from(items.current.values()).filter((e) => e.object.visible).map((e) => e.item), ...near], undefined, {
        planes, extra: capItems.current.flatMap((c) => c.parts),
      });
      if (w.body) { w.body.world = world; return w.body; }
      if (!world.count) return null;
      // Dropped onto the floor under the eye; the ground (where the model has none) at ±0.00, or lower under a basement.
      const feet = { x: w.pos.x, y: w.pos.y, z: w.pos.z - EYE_HEIGHT + 0.3 };
      w.body = new Walker(world, feet, Math.min(0, feet.z - 0.3));
      w.body.drop();
      w.pos.set(w.body.feet.x, w.body.feet.y, w.body.feet.z + EYE_HEIGHT);
      return w.body;
    };
    const placeWalk = () => {
      if (!walk) return;
      const l = lookVector(walk.yaw, walk.pitch);
      const t = new THREE.Vector3(walk.pos.x + l.x, walk.pos.y + l.y, walk.pos.z + l.z);
      cam.position.copy(walk.pos);
      controls.target.copy(t);
      cam.lookAt(t);
      publish();
      invalidate.current();
    };
    const stepWalk = (now: number) => {
      if (!walk) return;
      const dt = Math.min(0.1, (now - walk.last) / 1000);
      walk.last = now;
      const body = useWalk.getState().fly ? null : bodyOf(walk);
      if (body) {
        const wish = walkWish(walk.yaw, walk.keys, walk.run);
        walk.acc = Math.min(walk.acc + dt, 0.1);
        while (walk.acc >= TICK) { body.step(TICK, wish, walk.jump); walk.jump = false; walk.acc -= TICK; }
        walk.pos.set(body.feet.x, body.feet.y, easeEye(walk.pos.z, body.feet.z + EYE_HEIGHT, dt, body.grounded));
      } else {
        const p = walkStep(walk.pos, walk.yaw, walk.keys, dt, walk.run);
        walk.pos.set(p.x, p.y, p.z);
      }
      placeWalk();
      // On while a key is held, or the body still falls, slows down or eases up a step.
      const settling = body != null && (!body.still || Math.abs(walk.pos.z - body.feet.z - EYE_HEIGHT) > 1e-3);
      walk.loop = walk.keys.size || settling ? requestAnimationFrame(stepWalk) : 0;
    };
    const kickWalk = () => { if (walk && !walk.loop) { walk.last = performance.now(); walk.loop = requestAnimationFrame(stepWalk); } };
    // Leaving a flight: the body falls from where the eye is.
    const unFly = useWalk.subscribe((st, prev) => {
      if (!walk || st.fly === prev.fly) return;
      if (!st.fly && walk.body) { walk.body.feet = { x: walk.pos.x, y: walk.pos.y, z: walk.pos.z - EYE_HEIGHT }; walk.body.vel = { x: 0, y: 0, z: 0 }; walk.body.grounded = false; }
      kickWalk();
    });
    let rebuild = 0;
    walkApi.current = {
      start: (w) => {
        cancelAnimationFrame(flight);
        // A walk into another room keeps the camera there was before the first.
        const back = walk ? walk.back : pose();
        const wasOrtho = walk ? walk.wasOrtho : cam === orthoCam;
        if (walk) cancelAnimationFrame(walk.loop);
        if (cam === orthoCam) api.setOrtho(false);
        controls.enabled = false;
        // A wider view inside a room, and nothing near the eye cut off.
        persp.near = 0.05;
        persp.fov = 65;
        persp.updateProjectionMatrix();
        walk = { pos: new THREE.Vector3(w.eye.x, w.eye.y, w.eye.z), yaw: w.yaw, pitch: clampPitch(w.pitch), back, wasOrtho, keys: new Set(), run: false, jump: false, loop: 0, last: 0, drag: null, body: null, stale: true, acc: 0 };
        bodyOf(walk);
        placeWalk();
      },
      // Rebuilt a moment after the last change (planes moved, caps come in after them), not on every one.
      changed: () => {
        clearTimeout(rebuild);
        rebuild = window.setTimeout(() => { if (walk) { walk.stale = true; kickWalk(); } }, 120);
      },
      stop: () => {
        if (!walk) return;
        cancelAnimationFrame(walk.loop);
        clearTimeout(rebuild);
        const { back, wasOrtho } = walk;
        walk = null;
        controls.enabled = true;
        persp.near = 0.1;
        persp.fov = 45;
        persp.updateProjectionMatrix();
        applyPose(back);
        if (wasOrtho) api.setOrtho(true);
        publish();
        invalidate.current();
      },
    };
    const onWalkDown = (e: PointerEvent) => {
      if (!walk || e.button !== 0) return;
      walk.drag = { x: e.clientX, y: e.clientY };
      renderer.domElement.setPointerCapture?.(e.pointerId);
    };
    const onWalkMove = (e: PointerEvent) => {
      if (!walk?.drag) return;
      walk.yaw -= (e.clientX - walk.drag.x) * 0.004;
      walk.pitch = clampPitch(walk.pitch - (e.clientY - walk.drag.y) * 0.004);
      walk.drag = { x: e.clientX, y: e.clientY };
      placeWalk();
    };
    const onWalkUp = () => { if (walk) walk.drag = null; };
    const onWalkWheel = (e: WheelEvent) => {
      if (!walk) return;
      e.preventDefault();
      const l = lookVector(walk.yaw, 0);
      const by = -Math.sign(e.deltaY) * (e.shiftKey ? 1.5 : 0.5);
      const body = useWalk.getState().fly ? null : bodyOf(walk);
      if (body) {
        body.shove({ x: l.x * by, y: l.y * by, z: 0 });
        walk.pos.set(body.feet.x, body.feet.y, walk.pos.z);
        kickWalk();
      } else {
        walk.pos.x += l.x * by;
        walk.pos.y += l.y * by;
      }
      placeWalk();
    };
    // Before the shortcuts (capture on the window): while walking, the letters walk instead of starting tools.
    const onWalkKey = (e: KeyboardEvent) => {
      if (!walk || e.ctrlKey || e.metaKey || (e.target as HTMLElement | null)?.closest?.('input, select, textarea, [role="dialog"]')) return;
      e.stopImmediatePropagation();
      e.preventDefault();
      if (e.type === 'keydown' && e.key === 'Escape') { exitWalk(); return; }
      walk.run = e.shiftKey;
      if (e.type === 'keydown' && e.code === 'KeyF' && !e.repeat) { toggleFly(); return; }
      if (e.code === 'Space') { if (e.type === 'keydown' && !e.repeat) { walk.jump = true; kickWalk(); } return; }
      if (!WALK_KEYS.has(e.code)) return;
      if (e.type === 'keyup') { walk.keys.delete(e.code); return; }
      walk.keys.add(e.code);
      kickWalk();
    };
    const onWalkBlur = () => walk?.keys.clear();
    renderer.domElement.addEventListener('pointerdown', onWalkDown);
    renderer.domElement.addEventListener('pointermove', onWalkMove);
    renderer.domElement.addEventListener('pointerup', onWalkUp);
    renderer.domElement.addEventListener('wheel', onWalkWheel, { passive: false });
    window.addEventListener('keydown', onWalkKey, true);
    window.addEventListener('keyup', onWalkKey, true);
    window.addEventListener('blur', onWalkBlur);

    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { setPending(null); if (!(e.target as HTMLElement)?.closest('input, select, textarea')) setTool(null); } };
    renderer.domElement.addEventListener('pointerdown', onDown);
    renderer.domElement.addEventListener('pointerup', onUp);
    window.addEventListener('keydown', onKey);
    return () => {
      unsunlight();
      cancelAnimationFrame(frame);
      cancelAnimationFrame(flight);
      if (useCamera.getState().api === api) useCamera.setState({ api: null });
      unlisten();
      invalidate.current = () => undefined;
      ro.disconnect();
      controls.dispose();
      renderer.domElement.removeEventListener('pointerdown', onDown);
      renderer.domElement.removeEventListener('pointerup', onUp);
      renderer.domElement.removeEventListener('pointermove', onMove);
      cancelAnimationFrame(hoverFrame);
      clearFace.current = () => undefined;
      window.removeEventListener('keydown', onKey);
      if (walk) cancelAnimationFrame(walk.loop);
      clearTimeout(rebuild);
      unFly();
      useClip3d.setState({ planes: [] });
      walkApi.current = { start: () => undefined, stop: () => undefined, changed: () => undefined };
      renderer.domElement.removeEventListener('pointerdown', onWalkDown);
      renderer.domElement.removeEventListener('pointermove', onWalkMove);
      renderer.domElement.removeEventListener('pointerup', onWalkUp);
      renderer.domElement.removeEventListener('wheel', onWalkWheel);
      window.removeEventListener('keydown', onWalkKey, true);
      window.removeEventListener('keyup', onWalkKey, true);
      window.removeEventListener('blur', onWalkBlur);
      // The elements, the lights, the ground grid and the grid's axes.
      dispose(s);
      s.clear();
      live.clear();
      layer.batches.clear();
      layer.known.clear();
      instances.current = null;
      scene.current = null;
      rendererRef.current = null;
      renderer.dispose();
      renderer.forceContextLoss();
      el.removeChild(renderer.domElement);
    };
  }, []);

  // The view clears to the theme's paper or graphite; the grid lies on the active storey.
  const theme = useUi((st) => st.theme);
  const elevation = elevationRef.current;
  useEffect(() => {
    rendererRef.current?.setClearColor(CLEAR[theme]);
    const s = scene.current;
    const old = s?.getObjectByName('ground-grid');
    if (!s) return;
    if (old) { s.remove(old); dispose(old); }
    s.add(groundGrid(theme, elevation));
    invalidate.current();
  }, [theme, elevation]);

  // The grid's axes, on the lowest storey: dash-dot lines with their labels at both ends.
  // Keyed on the grids and the lowest storey only, not on every diff.
  const grids = useSession(useShallow((st) => gridsOf(st.nodes)));
  const base = useSession((st) => gridBase(st.nodes));
  useEffect(() => {
    const s = scene.current;
    if (!s) return;
    const old = s.getObjectByName('grid-axes');
    if (old) { s.remove(old); dispose(old); }
    const group = new THREE.Group();
    group.name = 'grid-axes';
    for (const g of grids) {
      for (const ax of (g.props.axes as { tag: string; a: number[]; b: number[] }[] | undefined) ?? []) {
        const geo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(ax.a[0], ax.a[1], base + 0.01), new THREE.Vector3(ax.b[0], ax.b[1], base + 0.01)]);
        const line = new THREE.Line(geo, new THREE.LineDashedMaterial({ color: 0x8a5a44, dashSize: 0.6, gapSize: 0.25 }));
        line.computeLineDistances();
        group.add(line);
        for (const end of [ax.a, ax.b]) {
          const cv = document.createElement('canvas');
          cv.width = cv.height = 64;
          const c = cv.getContext('2d')!;
          c.strokeStyle = '#8a5a44'; c.lineWidth = 4; c.beginPath(); c.arc(32, 32, 28, 0, Math.PI * 2); c.stroke();
          c.fillStyle = '#8a5a44'; c.font = 'bold 30px sans-serif'; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(ax.tag, 32, 34);
          const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), depthTest: false }));
          sprite.position.set(end[0], end[1], base + 0.3);
          sprite.scale.set(0.7, 0.7, 1);
          group.add(sprite);
        }
      }
    }
    s.add(group);
    invalidate.current();
  }, [grids, base]);

  // Keep the scene in step with the engine's render items (rebuild only what changed).
  const render = useSession((st) => st.render);
  const meshes = useSession((st) => st.meshes);
  // A saved 3D view opened: its camera (where it looks from, perspective or orthographic).
  const view3dId = useUi((st) => st.view3dId);
  useEffect(() => {
    if (!view3dId) return;
    const cam = useSession.getState().nodes.get(view3dId)?.props.camera as { azimuth?: number; pitch?: number; distance?: number; target?: number[]; ortho?: boolean } | undefined;
    const api = useCamera.getState().api;
    if (!cam || !api) return;
    api.setOrtho(Boolean(cam.ortho));
    api.orbitTo({ azimuth: cam.azimuth ?? 0, pitch: cam.pitch ?? -0.5, distance: cam.distance ?? 30, target: { x: cam.target?.[0] ?? 0, y: cam.target?.[1] ?? 0, z: cam.target?.[2] ?? 0 } }, false);
  }, [view3dId]);

  // A saved 3D view's section box: shown when the view opens, and again when an undo brings it back; {3D} starts without one.
  const keptBox = useSession((st) => (view3dId && view3dId !== '3d' ? JSON.stringify(st.nodes.get(view3dId)?.props.sectionBox ?? null) : 'none'));
  const lastView3d = useRef<string | null | undefined>(undefined);
  useEffect(() => {
    const changed = lastView3d.current !== view3dId;
    lastView3d.current = view3dId;
    if (keptBox === 'none') { if (changed) useSectionBox.setState({ box: null }); return; }
    const kept = JSON.parse(keptBox) as SectionBox | null;
    if (JSON.stringify(useSectionBox.getState().box) !== keptBox) useSectionBox.setState({ box: kept });
  }, [view3dId, keptBox]);

  const selected = useUi((st) => st.selected);
  const walkStart = useWalk((st) => st.start);
  const walking = walkStart != null;
  const flying = useWalk((st) => st.fly);
  const looks = useLooks((st) => st.looks);
  const visualStyle = useUi((st) => st.visualStyle);
  const fog = useUi((st) => st.fog);
  // The layers this 3D view shows (its style's or its own): the parts the others are drawn without, picked through, walked through.
  const layers = useSession((st) => layers3dOf(st.nodes, view3dId).mode);
  // Fog: the model fades into the background with distance (a display rule for linked context, big sites).
  useEffect(() => { const s = scene.current; if (s) s.fog = fog ? new THREE.FogExp2(CLEAR[theme], 0.012) : null; }, [fog, theme]);
  useEffect(() => {
    const s = scene.current;
    if (!s) return;
    const live = items.current;
    for (const [id, entry] of live) {
      const now = render.get(id);
      if (now && !now.instance && shownIn(now, layers) === entry.item) continue;
      s.remove(entry.object);
      dispose(entry.object);
      live.delete(id);
    }
    for (const [id, whole] of render) {
      if (live.has(id) || whole.instance) continue;
      const item = shownIn(whole, layers);
      const object = meshOf(item);
      s.add(object);
      live.set(id, { item, object });
    }
    for (const [id, object] of caps.current) {
      highlight(object, selected.includes(id));
      look(object, looks[id]);
      object.visible = object.visible && visualStyle !== 'wireframe' && !(walking && live.get(id)?.item.type === 'IfcSpace');
    }
    for (const [id, { item, object }] of live) {
      highlight(object, selected.includes(id));
      look(object, looks[id]);
      // Inside a room, its own volume is not drawn round the eye.
      if (walking && item.type === 'IfcSpace') object.visible = false;
      // The visual style: faces, edges, or both.
      object.traverse((c) => {
        if ((c as THREE.Mesh).isMesh) c.visible = visualStyle !== 'wireframe';
        else if ((c as THREE.LineSegments).isLineSegments) c.visible = visualStyle !== 'shaded';
      });
    }
    // The instances of shared meshes: added, moved, taken out; drawn as their looks and the selection say.
    const layer = instances.current;
    if (layer) { layer.sync(render, meshes); layer.restyle(looks, selected, visualStyle, layers); }
    invalidate.current();
  }, [render, meshes, layers, selected, looks, visualStyle, walking, capsShown]);

  // A walk asked for (a plan's right-click on a room), or ended; the model changed or shown otherwise under a walker.
  useEffect(() => { if (walkStart) walkApi.current.start(walkStart); else walkApi.current.stop(); }, [walkStart]);
  useEffect(() => walkApi.current.changed(), [render, looks, layers]);

  // The storeys the 3D view shows: where they are cut (capped with the section's caps below).
  const nodes = useSession((st) => st.nodes);
  const storeySetting = settingOf(nodes, view3dId);
  const storeyList = storeysOf({ nodes, relations: useSession.getState().relations });
  const activeId = storey?.id ?? null;
  const cut = cutZ(storeySetting, storeyList, activeId);
  const storeysChip = chipText(storeySetting, storeyList, activeId);
  const layerSetting = layers3dOf(nodes, view3dId);
  const layersText = layersChip(layerSetting);

  // A section shown in 3D: the model cut by its plane (and its depth), its drawing on the cut; an elevation only turns the camera.
  const sectionId = useSection3d((st) => st.viewId);
  const sectionNode = useSession((st) => (sectionId ? st.nodes.get(sectionId) : undefined));
  const sectionDrawing = useDrawing(sectionId);
  const sectionNotes = useSection3d((st) => st.annotations);
  const capStyle = useSection3d((st) => st.caps);
  const box = useSectionBox((st) => st.box);
  const shown = sectionId && sectionDrawing?.frame && sectionDrawing.kind === 'section' ? sectionDrawing : null;
  // What cuts the model: the section shown (its plane, and its depth) and the section box's faces.
  const planes: ClipPlane[] = [...(shown ? sectionPlanes(shown.frame!, shown.depth) : []), ...(box ? boxPlanes(box) : [])];
  const clipKey = planesKey(planes);
  // Closed by caps in their materials; with the drawing's hatches instead, the section's own drawing closes its cut
  // and the box's faces are capped as a sheet draws a cut, at the section's scale (1:50 without one).
  // The cut above the storeys shown is capped too, so a storey seen alone reads as its plan cuts it.
  const storeyCap: ClipPlane[] = cut === null ? [] : [{ normal: [0, 0, -1], constant: cut }];
  const capPlanes = [...(capStyle === 'drawing' ? (box ? boxPlanes(box) : []) : planes), ...storeyCap];
  const capScale = shown?.scale ?? 50;
  const capKey = `${planesKey(capPlanes)}|${capStyle}|${capScale}|${layers}`;
  useEffect(() => {
    // The view deleted: nothing to show any more.
    if (sectionId && !sectionNode) useSection3d.setState({ viewId: null });
  }, [sectionId, sectionNode]);
  useEffect(() => {
    const s = scene.current;
    if (!s) return;
    let stale = false;
    const put = (items: CapItem[]) => {
      for (const g of caps.current.values()) { s.remove(g); dispose(g); }
      caps.current.clear();
      capItems.current = items;
      walkApi.current.changed();
      for (const it of items) { const g = capsOf(it); s.add(g); caps.current.set(it.nodeId, g); }
      setCapsShown((v) => v + 1);
      invalidate.current();
    };
    if (!capPlanes.length) { put([]); return; }
    // Asked a moment after the last change (a face dragged asks once it rests), the newest answer kept.
    const timer = setTimeout(() => {
      void engine.call<CapItem[]>('view.caps', { planes: capPlanes, style: capStyle, scale: capScale, layers }).then((items) => { if (!stale) put(items ?? []); }).catch(() => undefined);
    }, 60);
    return () => { stale = true; clearTimeout(timer); };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- capKey stands for capPlanes
  }, [capKey, render]);
  useEffect(() => {
    const s = scene.current, renderer = rendererRef.current;
    if (!s || !renderer) return;
    const old = s.getObjectByName('section-overlay');
    if (old) { s.remove(old); dispose(old); }
    sectionClip.current = planes.map((p) => new THREE.Plane(new THREE.Vector3(...p.normal), p.constant));
    const d = sectionId ? sectionDrawing : null;
    if (d?.frame && d.kind === 'section') s.add(sectionOverlay(d, sectionNotes, Math.min(8, renderer.capabilities.getMaxAnisotropy()), capStyle === 'drawing'));
    applyClip();
    walkApi.current.changed();
    if (!sectionId) framedSection.current = null;
    else if (d?.frame && framedSection.current !== sectionId && !useWalk.getState().start) {
      framedSection.current = sectionId;
      const api = useCamera.getState().api;
      if (api && d.kind === 'section') { api.setOrtho(false); api.orbitTo(sectionPose(d.frame, d.bounds, useCamera.getState().fov)); }
      // An elevation: looking its way, level, the whole model framed.
      else if (api) { api.setOrtho(false); api.orbitTo({ azimuth: Math.atan2(d.frame.dir[1], d.frame.dir[0]), pitch: -0.05 }, false); api.fit(); }
    }
    invalidate.current();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- clipKey stands for planes
  }, [sectionId, sectionDrawing, sectionNotes, capStyle, clipKey]);

  // The storeys the 3D view shows (report #29): the others hidden by their looks (Storeys3DLooks); here the cut above them, and the chip.
  useEffect(() => {
    cutRef.current = cut;
    // Keeps what lies under the height: −z + cut ≥ 0.
    storeyClip.current = cut === null ? [] : [new THREE.Plane(new THREE.Vector3(0, 0, -1), cut)];
    applyClip();
    walkApi.current.changed();
    invalidate.current();
  }, [cut]);

  return (
    <>
      <div ref={host} className={`viewport${tool?.gesture ? ' picking' : ''}`} data-testid="view3d" />
      {(sectionId || walkStart || box || storeysChip || layersText) && (
        <div className="view3d-modes">
          {sectionId && (
            <span className="mode-chip section">
              <span>{sectionNode?.props.kind === 'elevation' ? 'Elevation' : 'Section'}</span><span data-no-i18n>{sectionNode?.name ?? ''}</span>
              {sectionNode?.props.kind !== 'elevation' && (
                <button type="button" className="mode-toggle" aria-pressed={sectionNotes} title="Show the section's dimensions, levels, tags and texts on the cut"
                  onClick={() => useSection3d.setState({ annotations: !sectionNotes })}>Annotations</button>
              )}
              {sectionNode?.props.kind !== 'elevation' && (
                <button type="button" className="mode-toggle" aria-pressed={capStyle === 'drawing'} title="Close the cut with the section's drawing (its cut fills and hatches, as sheets print it) instead of the materials"
                  onClick={() => useSection3d.setState({ caps: capStyle === 'drawing' ? 'material' : 'drawing' })}>Hatches</button>
              )}
              <button type="button" className="mode-close" title="Stop showing the section in 3D" aria-label="Stop showing the section in 3D" onClick={() => useSection3d.setState({ viewId: null })}><X size={12} /></button>
            </span>
          )}
          {box && !walkStart && (
            <span className="mode-chip section">
              <span>Section box</span>
              <span className="mode-hint">Drag a face's arrow to move it</span>
              <button type="button" onClick={fitSectionBox} title="The box round the whole model">Fit</button>
              <button type="button" onClick={() => void cutAtPlan()} title="The box's top at the active storey's plan cut">Cut at plan</button>
              <button type="button" onClick={alignSectionBox} title="Turn the box the way the selected element runs (a wall's axis)">Align</button>
              <button type="button" className="mode-toggle" aria-pressed={capStyle === 'drawing'} title="Draw the box's cuts as a sheet does (each material's cut fill and hatch) instead of the materials"
                onClick={() => useSection3d.setState({ caps: capStyle === 'drawing' ? 'material' : 'drawing' })}>Hatches</button>
              <button type="button" className="mode-close" title="Section box off" aria-label="Section box off" onClick={() => { useSectionBox.setState({ box: null }); commitBox(); }}><X size={12} /></button>
            </span>
          )}
          {storeysChip && (
            <span className="mode-chip storeys" title="Which storeys this 3D view shows: View → Storeys shown, or its properties">
              <span>Storeys</span>
              {storeysChip.name && <span data-no-i18n>{storeysChip.name}</span>}
              <span>{storeysChip.text}</span>
              {storeysChip.cut && <span className="mode-hint">{storeysChip.cut}</span>}
              <button type="button" className="mode-close" title="Show every storey" aria-label="Show every storey" onClick={() => void showAllStoreys3d(view3dId ?? '3d')}><X size={12} /></button>
            </span>
          )}
          {layersText && (
            <span className="mode-chip layers" title="Which layers of walls, floors and footings this 3D view shows: View → View settings, or its properties">
              <span>Layers shown</span>
              <span>{layersText}</span>
              <button type="button" className="mode-close" title="Show every layer" aria-label="Show every layer"
                onClick={() => void execute('view.setStyle', { id: view3dId ?? '3d', ...everyLayerPatch(layerSetting) })}><X size={12} /></button>
            </span>
          )}
          {walkStart && (
            <span className="mode-chip walk">
              <span>{flying ? 'Flying' : 'Walking'}</span><span data-no-i18n>{walkStart.name}</span>
              <span className="mode-hint">{flying ? 'Drag to look · W A S D to fly · E/Q up and down · Shift faster' : 'Drag to look · W A S D to walk · Space jumps · Shift runs'}</span>
              <button type="button" className={`toggle${flying ? ' active' : ''}`} aria-pressed={flying} onClick={toggleFly} title="Fly through walls and floors, without gravity (F)">Fly</button>
              <button type="button" onClick={exitWalk} title="Back to the camera there was (Esc)">Exit walk</button>
            </span>
          )}
        </div>
      )}
      {faceLabel && <div className="face-label" style={{ left: faceLabel.x + 14, top: faceLabel.y + 14 }}>{faceLabel.text}</div>}
      <Grips3D camera={camRef} canvas={host} />
      {!walkStart && <SectionBox3D camera={camRef} canvas={host} />}
      <KeyPlan mode="3d" />
    </>
  );
}
