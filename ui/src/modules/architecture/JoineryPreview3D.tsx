/**
 * The Doors and windows configurator's 3D preview (report #108): the type
 * being edited, as the draft is now, in a piece of wall of the project's
 * first wall type, built by the engine as a placed door or window is
 * (`architecture.joineryPreview3d`: the reveal, sills, lining and dressings
 * with their materials). One small renderer, drawn when something changes
 * (the meshes, a drag or the wheel), disposed when the dialog closes. Drag to
 * orbit, the wheel to zoom, right-drag to pan; it starts from outside.
 */

import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { lookOf } from '../../viewport/look';
import { reflections, standardMaterial } from '../../viewport/materialLook';

export interface Preview3DPart { element: 'wall' | 'fill'; name: string; color: string; material?: string | null; positions: number[]; indices: number[]; edges: number[] }
export interface Preview3D { problem?: string | null; parts?: Preview3DPart[]; size?: [number, number, number] }

interface Stage { renderer: THREE.WebGLRenderer; scene: THREE.Scene; camera: THREE.PerspectiveCamera; controls: OrbitControls; model: THREE.Group; framed: string }

/** The parts as three.js meshes (Z up, as the engine gives them), each with its material's look, and their edges. */
export function buildModel(parts: Preview3DPart[], materialOf: (id: string) => NodeDto | undefined): THREE.Group {
  const group = new THREE.Group();
  for (const p of parts) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(p.positions, 3));
    if (p.indices.length) g.setIndex(p.indices);
    g.computeVertexNormals();
    const node = p.material ? materialOf(p.material) : undefined;
    const look = lookOf(node);
    // Glass without a material of its own is still see-through.
    const l = !node && p.name === 'glass' ? { ...look, transparency: Math.max(look.transparency, 0.6), roughness: 0.05 } : look;
    const color = typeof node?.props.color === 'string' ? node.props.color : p.color;
    group.add(new THREE.Mesh(g, standardMaterial(color, null, l)));
    if (p.edges.length) {
      const e = new THREE.BufferGeometry();
      e.setAttribute('position', new THREE.Float32BufferAttribute(p.edges, 3));
      group.add(new THREE.LineSegments(e, new THREE.LineBasicMaterial({ color: 0x3a3d42, transparent: true, opacity: 0.55 })));
    }
  }
  return group;
}

function dispose(o: THREE.Object3D) {
  o.traverse((x) => {
    const m = x as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    for (const one of Array.isArray(mat) ? mat : mat ? [mat] : []) one.dispose();
  });
}

export function JoineryPreview3D({ spec, width = 300, height = 220 }: { spec: unknown; width?: number; height?: number }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const stage = useRef<Stage | null>(null);
  const [data, setData] = useState<Preview3D | null>(null);
  const [failed, setFailed] = useState(false);
  const nodes = useSession((s) => s.nodes);

  // The engine's meshes of the draft, a moment after the last change (typing a size asks once).
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => { void query<Preview3D>('architecture.joineryPreview3d', { spec }).then((p) => { if (live) setData(p); }).catch(() => undefined); }, 200);
    return () => { live = false; clearTimeout(t); };
  }, [spec]);

  // One renderer while the dialog is open.
  useEffect(() => {
    if (!canvas.current) return;
    let renderer: THREE.WebGLRenderer;
    try { renderer = new THREE.WebGLRenderer({ canvas: canvas.current, antialias: true, alpha: true }); } catch { setFailed(true); return; }
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(width, height, false);
    const scene = new THREE.Scene();
    scene.environment = reflections(renderer);
    const sun = new THREE.DirectionalLight(0xffffff, 1.5);
    sun.position.set(-2, -4, 5);
    const fill = new THREE.DirectionalLight(0xffffff, 0.5);
    fill.position.set(3, 4, 2);
    scene.add(sun, fill, new THREE.HemisphereLight(0xffffff, 0xb8b2a6, 0.9));
    const camera = new THREE.PerspectiveCamera(35, width / height, 0.05, 100);
    camera.up.set(0, 0, 1);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = false;
    const model = new THREE.Group();
    scene.add(model);
    const draw = () => renderer.render(scene, camera);
    controls.addEventListener('change', draw);
    stage.current = { renderer, scene, camera, controls, model, framed: '' };
    return () => {
      stage.current = null;
      controls.removeEventListener('change', draw);
      controls.dispose();
      dispose(scene);
      renderer.dispose();
    };
  }, [width, height]);

  // The meshes into the scene; the camera framed from outside the first time and when the size changes.
  useEffect(() => {
    const s = stage.current;
    if (!s || !data?.parts) return;
    for (const c of [...s.model.children]) { s.model.remove(c); dispose(c); }
    s.model.add(buildModel(data.parts, (id) => nodes.get(id)));
    const [w, t, h] = data.size ?? [2, 0.3, 2.7];
    const key = `${w.toFixed(3)} ${t.toFixed(3)} ${h.toFixed(3)}`;
    if (s.framed !== key) {
      s.framed = key;
      const r = Math.max(w, h) * 0.5;
      const target = new THREE.Vector3(0, 0, h / 2);
      s.controls.target.copy(target);
      // From outside (−Y), a little to the left and above, as one walks up to a facade.
      const d = r / Math.tan((s.camera.fov * Math.PI) / 360) * 1.7;
      s.camera.position.copy(target).add(new THREE.Vector3(-0.45, -1, 0.35).normalize().multiplyScalar(d));
      s.controls.minDistance = r * 0.3;
      s.controls.maxDistance = d * 4;
      s.controls.update();
    }
    s.renderer.render(s.scene, s.camera);
  }, [data, nodes]);

  if (failed) return null;
  return (
    <figure className="joinery-3d" data-testid="joinery-preview-3d">
      <canvas ref={canvas} width={width} height={height} style={{ width, height }} aria-label="3D preview" />
      <figcaption className="muted small">3D: drag to turn, wheel to zoom</figcaption>
    </figure>
  );
}
