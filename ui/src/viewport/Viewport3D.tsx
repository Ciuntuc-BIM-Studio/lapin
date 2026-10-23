/**
 * The 3D view: the engine's meshes in three.js (metres, Z up), on the
 * architect's-sheet palette. With the wall tool on, two clicks on the
 * storey's plane give the wall's ends.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { RenderItem } from '@bbim/platform/types';
import { useSession } from '../engine/session';

export interface PlanePick { x: number; y: number }

interface Props {
  /** Called with the point on the ground plane (z = 0) under a click, while a tool wants points. */
  onPick?: (p: PlanePick) => void;
  picking: boolean;
}

const PAPER = 0xf3efe7;

function meshOf(item: RenderItem): THREE.Group {
  const group = new THREE.Group();
  group.userData.nodeId = item.nodeId;
  const add = (positions: number[], normals: number[], indices: number[], edges: number[], color: string) => {
    if (positions.length) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
      if (normals.length === positions.length) g.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
      else g.computeVertexNormals();
      g.setIndex(indices);
      group.add(new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color: new THREE.Color(color), roughness: 0.85, metalness: 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 })));
    }
    if (edges.length) {
      const e = new THREE.BufferGeometry();
      e.setAttribute('position', new THREE.Float32BufferAttribute(edges, 3));
      group.add(new THREE.LineSegments(e, new THREE.LineBasicMaterial({ color: 0x1e2a3a })));
    }
  };
  add(item.positions, item.normals, item.indices, item.edges, item.color);
  for (const p of item.parts ?? []) add(p.positions, p.normals, p.indices, p.edges, p.color);
  return group;
}

function dispose(o: THREE.Object3D) {
  o.traverse((c) => {
    const m = c as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    if (Array.isArray(mat)) mat.forEach((x) => x.dispose()); else mat?.dispose();
  });
}

export function Viewport3D({ onPick, picking }: Props) {
  const host = useRef<HTMLDivElement>(null);
  const scene = useRef<THREE.Scene | null>(null);
  const items = useRef(new Map<string, { item: RenderItem; object: THREE.Group }>());
  const pickRef = useRef(onPick);
  pickRef.current = picking ? onPick : undefined;

  useEffect(() => {
    const el = host.current!;
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setClearColor(PAPER);
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
    const grid = new THREE.GridHelper(60, 60, 0xb5532d, 0xd6cebf);
    grid.rotation.x = Math.PI / 2;
    s.add(grid);

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

    // A click (not a drag) on the ground plane z = 0, while a tool wants a point.
    const ray = new THREE.Raycaster();
    const ground = new THREE.Plane(new THREE.Vector3(0, 0, 1), 0);
    let down: { x: number; y: number } | null = null;
    const onDown = (e: PointerEvent) => { down = { x: e.clientX, y: e.clientY }; };
    const onUp = (e: PointerEvent) => {
      const start = down;
      down = null;
      if (!start || Math.hypot(e.clientX - start.x, e.clientY - start.y) > 4 || !pickRef.current) return;
      const r = renderer.domElement.getBoundingClientRect();
      ray.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), camera);
      const hit = ray.ray.intersectPlane(ground, new THREE.Vector3());
      if (hit) pickRef.current({ x: Math.round(hit.x * 20) / 20, y: Math.round(hit.y * 20) / 20 });
    };
    renderer.domElement.addEventListener('pointerdown', onDown);
    renderer.domElement.addEventListener('pointerup', onUp);
    return () => {
      cancelAnimationFrame(frame);
      ro.disconnect();
      controls.dispose();
      renderer.domElement.removeEventListener('pointerdown', onDown);
      renderer.domElement.removeEventListener('pointerup', onUp);
      for (const { object } of items.current.values()) dispose(object);
      items.current.clear();
      renderer.dispose();
      el.removeChild(renderer.domElement);
    };
  }, []);

  // Keep the scene in step with the engine's render items (rebuild only what changed).
  const render = useSession((st) => st.render);
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
  }, [render]);

  return <div ref={host} className={`viewport${picking ? ' picking' : ''}`} />;
}
