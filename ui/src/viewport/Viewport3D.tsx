/**
 * The 3D view: the engine's meshes in three.js (metres, Z up), on the
 * architect's-sheet palette. A click selects; with a drawing tool on, clicks
 * on the active storey's plane give its points.
 */

import { useEffect, useRef, useState } from 'react';
import { useShallow } from 'zustand/react/shallow';
import { useLooks, type Look } from '../platform/looks';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { NodeDto, RenderItem } from '../engine/protocol';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { select, setTool, useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { completeTool } from './tools';
import { boxUv, splitByFace, textureFor, textureOf, type Face, type TextureKind } from './textures';
import { KeyPlan } from './KeyPlan';
import { poseOf, positionOf, useCamera, type CameraApi, type CameraPose } from './camera';
import { Grips3D } from './Grips3D';

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

function meshOf(item: RenderItem): THREE.Group {
  const group = new THREE.Group();
  group.userData.nodeId = item.nodeId;
  const nodes = useSession.getState().nodes;
  const add = (positions: Float32Array, normals: Float32Array, indices: Int32Array, edges: Float32Array, color: string, material?: string | null) => {
    if (positions.length) {
      const mat = material ? nodes.get(material) : undefined;
      const kinds: Record<Face, TextureKind | null> = { side: textureOf(mat, 'side'), end: textureOf(mat, 'end'), top: textureOf(mat, 'top') };
      const all = new Uint32Array(indices.buffer, indices.byteOffset, indices.length);
      // One mesh, or one per face when the ends or the top look different (a brick wall's cut ends).
      const faces = kinds.end !== kinds.side || kinds.top !== kinds.side ? splitByFace(positions, all) : { side: all };
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
        mesh.userData.nodeId = item.nodeId;
        group.add(mesh);
      }
    }
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
  const [pending, setPending] = useState<Vec2 | null>(null);
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  // What a click does, kept current for the listeners set up once.
  const clickRef = useRef<(hit: { nodeId: string | null; ground: Vec2 | null; add: boolean }) => void>(() => undefined);
  const elevationRef = useRef(0);
  useUi((s) => s.storeyId);
  useSession((s) => s.nodes);
  const storey = activeStorey();
  elevationRef.current = num(storey ?? undefined, 'elevation');

  useEffect(() => { setPending(null); }, [toolId]);

  clickRef.current = ({ nodeId, ground, add }) => {
    const g = tool?.gesture;
    if (!g) { select(nodeId, add); return; }
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
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(20, -30, 40);
    s.add(sun);
    s.add(groundGrid(useUi.getState().theme, elevationRef.current));

    let frame = 0;
    invalidate.current = () => {
      if (!frame) frame = requestAnimationFrame(() => { frame = 0; renderer.render(s, cam); });
    };
    controls.addEventListener('change', invalidate.current);
    controls.addEventListener('change', publish);
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
    let down: { x: number; y: number } | null = null;
    const onDown = (e: PointerEvent) => { down = { x: e.clientX, y: e.clientY }; };
    const onUp = (e: PointerEvent) => {
      const start = down;
      down = null;
      if (!start || e.button !== 0 || Math.hypot(e.clientX - start.x, e.clientY - start.y) > 4) return;
      const r = renderer.domElement.getBoundingClientRect();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), cam);
      const meshes: THREE.Object3D[] = [];
      for (const { object } of live.values()) meshes.push(object);
      const hit = ray.intersectObjects(meshes, true).find((h) => h.object.userData.nodeId);
      const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), -elevationRef.current);
      const g = ray.ray.intersectPlane(plane, new THREE.Vector3());
      clickRef.current({
        nodeId: (hit?.object.userData.nodeId as string | undefined) ?? null,
        ground: g ? { x: Math.round(g.x * 20) / 20, y: Math.round(g.y * 20) / 20 } : null,
        add: e.shiftKey,
      });
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { setPending(null); if (!(e.target as HTMLElement)?.closest('input, select, textarea')) setTool(null); } };
    renderer.domElement.addEventListener('pointerdown', onDown);
    renderer.domElement.addEventListener('pointerup', onUp);
    window.addEventListener('keydown', onKey);
    return () => {
      cancelAnimationFrame(frame);
      cancelAnimationFrame(flight);
      if (useCamera.getState().api === api) useCamera.setState({ api: null });
      invalidate.current = () => undefined;
      ro.disconnect();
      controls.dispose();
      renderer.domElement.removeEventListener('pointerdown', onDown);
      renderer.domElement.removeEventListener('pointerup', onUp);
      window.removeEventListener('keydown', onKey);
      // The elements, the lights, the ground grid and the grid's axes.
      dispose(s);
      s.clear();
      live.clear();
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

  const selected = useUi((st) => st.selected);
  const looks = useLooks((st) => st.looks);
  const visualStyle = useUi((st) => st.visualStyle);
  useEffect(() => {
    const s = scene.current;
    if (!s) return;
    const live = items.current;
    for (const [id, entry] of live) {
      if (render.get(id) === entry.item) continue;
      s.remove(entry.object);
      dispose(entry.object);
      live.delete(id);
    }
    for (const [id, item] of render) {
      if (live.has(id)) continue;
      const object = meshOf(item);
      s.add(object);
      live.set(id, { item, object });
    }
    for (const [id, { object }] of live) {
      highlight(object, selected.includes(id));
      look(object, looks[id]);
      // The visual style: faces, edges, or both.
      object.traverse((c) => {
        if ((c as THREE.Mesh).isMesh) c.visible = visualStyle !== 'wireframe';
        else if ((c as THREE.LineSegments).isLineSegments) c.visible = visualStyle !== 'shaded';
      });
    }
    invalidate.current();
  }, [render, selected, looks, visualStyle]);

  return (
    <>
      <div ref={host} className={`viewport${tool?.gesture ? ' picking' : ''}`} data-testid="view3d" />
      <Grips3D camera={camRef} canvas={host} />
      <KeyPlan mode="3d" />
    </>
  );
}
