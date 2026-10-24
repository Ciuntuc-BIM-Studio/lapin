/**
 * The 3D view: the engine's meshes in three.js (metres, Z up), on the
 * architect's-sheet palette. A click selects; with a drawing tool on, clicks
 * on the active storey's plane give its points.
 */

import { useEffect, useRef, useState } from 'react';
import { useLooks, type Look } from '../platform/looks';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { RenderItem } from '../engine/protocol';
import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { select, setTool, useUi } from '../app/store';
import { findTool } from '../platform/modules';
import type { Vec2 } from '../platform/module';
import { completeTool } from './tools';
import { boxUv, textureFor, textureOf } from './textures';

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
  const nodes = useSession.getState().nodes;
  const add = (positions: Float32Array, normals: Float32Array, indices: Int32Array, edges: Float32Array, color: string, material?: string | null) => {
    if (positions.length) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      if (normals.length === positions.length) g.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
      else g.computeVertexNormals();
      g.setIndex(new THREE.BufferAttribute(new Uint32Array(indices.buffer, indices.byteOffset, indices.length), 1));
      // The material's texture, tinted by its colour, laid on in metres.
      const kind = material ? textureOf(nodes.get(material)) : null;
      if (kind) boxUv(g, kind);
      const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({
        color: kind ? 0xffffff : new THREE.Color(color), map: kind ? textureFor(kind, color) : null,
        roughness: kind === 'metal' ? 0.45 : 0.85, metalness: kind === 'metal' ? 0.3 : 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
      }));
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

  // The grid's axes, on the lowest storey: dash-dot lines with their labels at both ends.
  const gridNodes = useSession((st) => st.nodes);
  useEffect(() => {
    const s = scene.current;
    if (!s) return;
    const old = s.getObjectByName('grid-axes');
    if (old) { s.remove(old); dispose(old); }
    const group = new THREE.Group();
    group.name = 'grid-axes';
    const base = Math.min(0, ...[...gridNodes.values()].filter((n) => n.type === 'IfcBuildingStorey').map((n) => Number(n.props.elevation ?? 0)));
    for (const g of gridNodes.values()) {
      if (g.type !== 'IfcGrid') continue;
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
  }, [gridNodes]);

  // Keep the scene in step with the engine's render items (rebuild only what changed).
  const render = useSession((st) => st.render);
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
  }, [render, selected, looks, visualStyle]);

  return <div ref={host} className={`viewport${tool?.gesture ? ' picking' : ''}`} data-testid="view3d" />;
}
