/**
 * The 3D view: the engine's meshes in three.js (metres, Z up), on the
 * architect's-sheet palette. A click selects; with a drawing tool on, clicks
 * on the active storey's plane give its points.
 */

import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { RenderItem } from '../engine/protocol';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { select, setTool, useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { completeTool } from './tools';

const CLEAR = { paper: 0xf3efe7, graphite: 0x1c1b19 } as const;
/** The ground grid per theme: its axes in the accent, its lines a shade off the paper. */
const GRID = { paper: [0xb5532d, 0xd6cebf], graphite: [0xe07a4f, 0x34312c] } as const;
const SELECTED = new THREE.Color(0xb5532d);

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
  const add = (positions: Float32Array, normals: Float32Array, indices: Int32Array, edges: Float32Array, color: string) => {
    if (positions.length) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      if (normals.length === positions.length) g.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
      else g.computeVertexNormals();
      g.setIndex(new THREE.BufferAttribute(new Uint32Array(indices.buffer, indices.byteOffset, indices.length), 1));
      const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color: new THREE.Color(color), roughness: 0.85, metalness: 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 }));
      mesh.userData.nodeId = item.nodeId;
      group.add(mesh);
    }
    if (edges.length) {
      const e = new THREE.BufferGeometry();
      e.setAttribute('position', new THREE.BufferAttribute(edges, 3));
      group.add(new THREE.LineSegments(e, new THREE.LineBasicMaterial({ color: 0x1e2a3a })));
    }
  };
  add(item.positions, item.normals, item.indices, item.edges, item.color);
  for (const p of item.parts ?? []) add(p.positions, p.normals, p.indices, p.edges, p.color);
  return group;
}

function highlight(group: THREE.Object3D, on: boolean) {
  group.traverse((c) => {
    const m = (c as THREE.Mesh).material as THREE.MeshStandardMaterial | undefined;
    if (m && 'emissive' in m) m.emissive = on ? SELECTED : new THREE.Color(0);
    if (m && 'emissiveIntensity' in m) m.emissiveIntensity = on ? 0.45 : 0;
  });
}

function dispose(o: THREE.Object3D) {
  o.traverse((c) => {
    const m = c as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    if (Array.isArray(mat)) mat.forEach((x) => x.dispose()); else mat?.dispose();
  });
}

export function Viewport3D() {
  const host = useRef<HTMLDivElement>(null);
  const scene = useRef<THREE.Scene | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
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
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 5000);
    camera.up.set(0, 0, 1);
    camera.position.set(18, -22, 16);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(4, 3, 0);
    controls.update();
    s.add(new THREE.HemisphereLight(0xffffff, 0xd6cebf, 2.2));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(20, -30, 40);
    s.add(sun);
    s.add(groundGrid(useUi.getState().theme, elevationRef.current));

    const resize = () => {
      const { clientWidth: w, clientHeight: h } = el;
      renderer.setSize(w, h);
      camera.aspect = w / Math.max(h, 1);
      camera.updateProjectionMatrix();
    };
    const ro = new ResizeObserver(resize);
    ro.observe(el);
    resize();
    let frame = 0;
    const loop = () => { frame = requestAnimationFrame(loop); renderer.render(s, camera); };
    loop();

    // A click (not a drag): the element under it, and the point on the active storey's plane.
    const ray = new THREE.Raycaster();
    let down: { x: number; y: number } | null = null;
    const onDown = (e: PointerEvent) => { down = { x: e.clientX, y: e.clientY }; };
    const onUp = (e: PointerEvent) => {
      const start = down;
      down = null;
      if (!start || e.button !== 0 || Math.hypot(e.clientX - start.x, e.clientY - start.y) > 4) return;
      const r = renderer.domElement.getBoundingClientRect();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), camera);
      const meshes: THREE.Object3D[] = [];
      for (const { object } of items.current.values()) meshes.push(object);
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
      ro.disconnect();
      controls.dispose();
      renderer.domElement.removeEventListener('pointerdown', onDown);
      renderer.domElement.removeEventListener('pointerup', onUp);
      window.removeEventListener('keydown', onKey);
      for (const { object } of items.current.values()) dispose(object);
      items.current.clear();
      renderer.dispose();
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
  }, [theme, elevation]);

  // Keep the scene in step with the engine's render items (rebuild only what changed).
  const render = useSession((st) => st.render);
  const selected = useUi((st) => st.selected);
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
    for (const [id, { object }] of live) highlight(object, selected.includes(id));
  }, [render, selected]);

  return <div ref={host} className={`viewport${tool?.gesture ? ' picking' : ''}`} data-testid="view3d" />;
}
