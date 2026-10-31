/**
 * The 3D view's instances (docs/plan-instancing.md): every element the engine
 * sends as a shared mesh and a matrix is drawn by one InstancedMesh per part
 * of that mesh, so a million references are a few draw calls, not millions.
 *
 * Per instance: its colour (a look's, lighter when selected), hidden (its
 * matrix made nothing), or faded (drawn by the part's second, see-through
 * InstancedMesh, which casts no shadow). The edges are instanced lines, so
 * the renderer's clipping planes and fog apply to them as to everything else.
 * Kept incrementally: an edit's instances are added, moved or taken out, not
 * the whole set rebuilt.
 *
 * A heavy part's coarser copies (docs/plan-display-precision.md, lod.ts) are
 * InstancedMeshes of their own: each instance is written into the one its
 * distance chooses and is nothing in the others.
 */

import * as THREE from 'three';
import type { RenderItem, RenderPart, SharedMesh } from '../engine/protocol';
import type { Look } from '../platform/looks';
import { elements, placedBox } from '../engine/instances';
import { copyArrays, levelFor, pixelsPerMetre, type DisplayQuality, type LodCamera } from './lod';

const ZERO = new THREE.Matrix4().makeScale(0, 0, 0).elements;
const SELECTED = new THREE.Color(0xb5532d);
const FIRST = 64;

export type VisualStyle = 'shaded' | 'wireframe' | string;

interface PartBatch {
  part: RenderPart;
  colour: THREE.Color;
  solid: THREE.InstancedMesh;
  faded: THREE.InstancedMesh;
  edges: THREE.LineSegments | null;
  edgeMatrix: THREE.InstancedBufferAttribute | null;
  /** The part's coarser copies, finest first, and the most each strays (m). */
  copies: PartBatch[];
  errors: number[];
}

/** A part and its copies: what an instance is written into, one of them at a time. */
const variants = (p: PartBatch): PartBatch[] => (p.copies.length ? [p, ...p.copies] : [p]);

/** An instanced LineSegments: three.js draws any object it takes for an InstancedMesh with its instanceMatrix. */
function instancedLines(edges: Float32Array, material: THREE.Material, matrix: THREE.InstancedBufferAttribute): THREE.LineSegments {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(edges, 3));
  const lines = new THREE.LineSegments(g, material);
  Object.assign(lines, { isInstancedMesh: true, instanceMatrix: matrix, instanceColor: null, morphTexture: null, count: 0 });
  lines.raycast = () => undefined;
  lines.frustumCulled = false;
  return lines;
}

/** One shared mesh's instances. */
class Batch {
  readonly group = new THREE.Group();
  ids: string[] = [];
  raw: ArrayLike<number>[] = [];
  readonly index = new Map<string, number>();
  /** Each instance's state as last drawn (its look, selected), so a restyle touches only what changed. */
  states: string[] = [];
  /** Each instance's copy per part, one character each ('0' the part itself), as the last relevel chose. */
  lodKeys: string[] = [];
  parts: PartBatch[] = [];
  capacity = 0;

  constructor(public mesh: SharedMesh, readonly edgeMaterial: THREE.Material) {
    this.group.userData.batch = this;
    this.build(FIRST);
  }

  /** The part meshes for `capacity` instances, the instances so far copied in. */
  build(capacity: number) {
    const old = this.parts;
    this.capacity = capacity;
    const make = (part: RenderPart, prev: PartBatch | undefined): PartBatch => {
      const geometry = prev?.solid.geometry ?? geometryOf(part);
      const colour = new THREE.Color(part.color);
      const solid = new THREE.InstancedMesh(geometry, prev?.solid.material ?? material(1), capacity);
      const faded = new THREE.InstancedMesh(geometry, prev?.faded.material ?? material(0.35), capacity);
      for (const m of [solid, faded]) {
        m.userData.batch = this;
        m.frustumCulled = false;
        m.count = this.ids.length;
        m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      }
      solid.castShadow = solid.receiveShadow = true;
      faded.castShadow = false;
      const edgeMatrix = part.edges.length ? new THREE.InstancedBufferAttribute(new Float32Array(capacity * 16), 16) : null;
      const edges = edgeMatrix ? instancedLines(part.edges, this.edgeMaterial, edgeMatrix) : null;
      const pb: PartBatch = { part, colour, solid, faded, edges, edgeMatrix, copies: [], errors: [] };
      if (prev) {
        solid.instanceMatrix.array.set(prev.solid.instanceMatrix.array.subarray(0, this.ids.length * 16));
        faded.instanceMatrix.array.set(prev.faded.instanceMatrix.array.subarray(0, this.ids.length * 16));
        if (prev.solid.instanceColor) { solid.setColorAt(0, colour); solid.instanceColor!.array.set(prev.solid.instanceColor.array.subarray(0, this.ids.length * 3)); }
        if (prev.faded.instanceColor) { faded.setColorAt(0, colour); faded.instanceColor!.array.set(prev.faded.instanceColor.array.subarray(0, this.ids.length * 3)); }
        if (prev.edgeMatrix && edgeMatrix) edgeMatrix.array.set(prev.edgeMatrix.array.subarray(0, this.ids.length * 16));
      }
      return pb;
    };
    this.parts = this.mesh.parts.map((part, k) => {
      const pb = make(part, old[k]);
      pb.copies = (part.lods ?? []).map((l, j) => make({ ...part, ...copyArrays(l), lods: null }, old[k]?.copies[j]));
      pb.errors = (part.lods ?? []).map((l) => l.error);
      return pb;
    });
    for (const p of old.flatMap(variants)) { this.group.remove(p.solid, p.faded); p.solid.dispose(); p.faded.dispose(); if (p.edges) { this.group.remove(p.edges); p.edges.geometry.dispose(); } }
    for (const p of this.parts.flatMap(variants)) { this.group.add(p.solid, p.faded); if (p.edges) this.group.add(p.edges); }
    this.setCount();
  }

  /** The mesh sent again: its parts made anew, the instances kept (drawn again by the next restyle). */
  rebuild(mesh: SharedMesh) {
    for (const p of this.parts.flatMap(variants)) { this.group.remove(p.solid, p.faded); if (p.edges) this.group.remove(p.edges); }
    this.dispose();
    this.parts = [];
    this.mesh = mesh;
    this.build(this.capacity);
    this.states.fill('');
    this.lodKeys.fill('');
  }

  setCount() {
    for (const p of this.parts.flatMap(variants)) {
      p.solid.count = p.faded.count = this.ids.length;
      if (p.edges) (p.edges as unknown as { count: number }).count = this.ids.length;
    }
  }

  /** An instance placed (added at the end when new); its state drawn again. */
  put(id: string, matrix: ArrayLike<number>) {
    let i = this.index.get(id);
    if (i === undefined) {
      if (this.ids.length >= this.capacity) this.build(this.capacity * 2);
      i = this.ids.length;
      this.ids.push(id); this.raw.push(matrix); this.states.push(''); this.lodKeys.push('');
      this.index.set(id, i);
      this.setCount();
    } else { this.raw[i] = matrix; this.states[i] = ''; }
  }

  /** An instance taken out: the last one moved into its place. */
  remove(id: string) {
    const i = this.index.get(id);
    if (i === undefined) return;
    const last = this.ids.length - 1;
    if (i !== last) {
      const moved = this.ids[last];
      this.ids[i] = moved; this.raw[i] = this.raw[last]; this.states[i] = ''; this.lodKeys[i] = this.lodKeys[last];
      this.index.set(moved, i);
    }
    this.ids.pop(); this.raw.pop(); this.states.pop(); this.lodKeys.pop();
    this.index.delete(id);
    this.setCount();
    // The moved one is drawn again by the next restyle (its state was cleared).
  }

  /** Each instance drawn as its look and the selection say, where that changed; true if anything did. */
  restyle(looks: Record<string, Look | undefined>, selected: ReadonlySet<string>): boolean {
    let changed = false;
    const at = new THREE.Matrix4();
    const c = new THREE.Color();
    let fadedOpacity: number | null = null;
    for (let i = 0; i < this.ids.length; i++) {
      const id = this.ids[i];
      const l = looks[id];
      const sel = selected.has(id);
      const opacity = l?.opacity ?? 1;
      if (opacity < 1 && fadedOpacity === null) fadedOpacity = opacity;
      const key = this.lodKeys[i];
      const state = `${l?.hidden ? 'h' : opacity < 1 ? 'f' : 's'}|${l?.color ?? ''}|${sel ? 1 : 0}|${key}`;
      if (this.states[i] === state) continue;
      this.states[i] = state;
      changed = true;
      at.fromArray(elements(this.raw[i]));
      const shown = l?.hidden ? ZERO : at.elements;
      this.parts.forEach((part, k) => {
        // The copy this instance is drawn with; nothing in the others.
        const chosen = key ? Math.min(key.charCodeAt(k) - 48, part.copies.length) : 0;
        variants(part).forEach((p, v) => {
          const mine = v === chosen;
          p.solid.instanceMatrix.array.set(mine && !l?.hidden && opacity >= 1 ? shown : ZERO, i * 16);
          p.faded.instanceMatrix.array.set(mine && !l?.hidden && opacity < 1 ? shown : ZERO, i * 16);
          p.edgeMatrix?.array.set(mine ? shown : ZERO, i * 16);
          c.set(l?.color ?? p.colour);
          if (sel) c.lerp(SELECTED, 0.55);
          p.solid.setColorAt(i, c);
          p.faded.setColorAt(i, c);
        });
      });
    }
    if (changed)
      for (const p of this.parts.flatMap(variants)) {
        p.solid.instanceMatrix.needsUpdate = p.faded.instanceMatrix.needsUpdate = true;
        if (p.solid.instanceColor) p.solid.instanceColor.needsUpdate = true;
        if (p.faded.instanceColor) p.faded.instanceColor.needsUpdate = true;
        if (p.edgeMatrix) p.edgeMatrix.needsUpdate = true;
        // Raycasts test the instances' sphere first: computed again from the matrices now.
        p.solid.boundingSphere = p.faded.boundingSphere = null;
        p.solid.boundingBox = p.faded.boundingBox = null;
        if (fadedOpacity !== null) (p.faded.material as THREE.MeshStandardMaterial).opacity = fadedOpacity;
      }
    return changed;
  }

  /** Faces, edges or both; the parts a layers mode leaves out, hidden. */
  show(style: VisualStyle, layers: string) {
    for (const p of this.parts.flatMap(variants)) {
      const kept = !p.part.hiddenBy?.includes(layers as 'noFinishes' | 'core');
      p.solid.visible = p.faded.visible = kept && style !== 'wireframe';
      if (p.edges) p.edges.visible = kept && style !== 'shaded';
    }
  }

  /** Each instance's copies chosen again for the camera; true if any changed (drawn by the next restyle). */
  relevel(cam: LodCamera, quality: DisplayQuality): boolean {
    if (!this.parts.some((p) => p.copies.length)) return false;
    let changed = false;
    for (let i = 0; i < this.ids.length; i++) {
      const q = placedBox(this.mesh.box, this.raw[i]);
      const ppm = pixelsPerMetre(cam, [(q[0] + q[3]) / 2, (q[1] + q[4]) / 2, (q[2] + q[5]) / 2], Math.hypot(q[3] - q[0], q[4] - q[1], q[5] - q[2]) / 2);
      let key = '';
      for (const p of this.parts) key += String.fromCharCode(48 + (p.copies.length ? levelFor(p.errors, ppm, quality) : 0));
      if (this.lodKeys[i] !== key) { this.lodKeys[i] = key; this.states[i] = ''; changed = true; }
    }
    return changed;
  }

  dispose() {
    for (const p of this.parts.flatMap(variants)) {
      p.solid.geometry.dispose();
      (p.solid.material as THREE.Material).dispose();
      (p.faded.material as THREE.Material).dispose();
      p.solid.dispose(); p.faded.dispose();
      p.edges?.geometry.dispose();
    }
  }
}

function geometryOf(part: RenderPart): THREE.BufferGeometry {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(part.positions, 3));
  if (part.normals.length === part.positions.length) g.setAttribute('normal', new THREE.BufferAttribute(part.normals, 3));
  else g.computeVertexNormals();
  g.setIndex(new THREE.BufferAttribute(new Uint32Array(part.indices.buffer, part.indices.byteOffset, part.indices.length), 1));
  return g;
}

function material(opacity: number): THREE.MeshStandardMaterial {
  // White, so each instance's colour is its own (instanceColor multiplies it).
  return new THREE.MeshStandardMaterial({
    color: 0xffffff, roughness: 0.85, metalness: 0, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
    transparent: opacity < 1, opacity, depthWrite: opacity >= 1,
  });
}

/** Every instance in the 3D view, by shared mesh. */
export class InstanceLayer {
  readonly group = new THREE.Group();
  readonly batches = new Map<string, Batch>();
  /** Each instance's item as last placed, and its batch. */
  readonly known = new Map<string, { item: RenderItem; batch: Batch }>();

  constructor(readonly edgeMaterial: THREE.Material) {
    this.group.name = 'instances';
  }

  /** The instances `render` holds now: new ones added, moved ones placed again, gone ones taken out; a mesh changed, its batch made again. */
  sync(render: ReadonlyMap<string, RenderItem>, meshes: ReadonlyMap<string, SharedMesh>): boolean {
    let changed = false;
    for (const [id, k] of this.known) {
      const now = render.get(id);
      if (now === k.item) continue;
      if (!now?.instance || now.instance.mesh !== k.batch.mesh.id) { k.batch.remove(id); this.known.delete(id); changed = true; }
    }
    // A mesh sent again: its batch made from it, its instances kept.
    for (const [mid, b] of this.batches) {
      const m = meshes.get(mid);
      if (m && m !== b.mesh) { b.rebuild(m); changed = true; }
    }
    for (const [id, item] of render) {
      const inst = item.instance;
      if (!inst) continue;
      const k = this.known.get(id);
      if (k?.item === item) continue;
      const mesh = meshes.get(inst.mesh);
      if (!mesh) continue;
      let b = this.batches.get(inst.mesh);
      if (!b) { b = new Batch(mesh, this.edgeMaterial); this.batches.set(inst.mesh, b); this.group.add(b.group); }
      b.put(id, inst.matrix);
      this.known.set(id, { item, batch: b });
      changed = true;
    }
    for (const [mid, b] of this.batches)
      if (!b.ids.length || !meshes.has(mid)) {
        for (const id of b.ids) this.known.delete(id);
        this.group.remove(b.group); b.dispose(); this.batches.delete(mid); changed = true;
      }
    return changed;
  }

  /** What the last restyle drew with, for a relevel's own. */
  private last: { looks: Record<string, Look | undefined>; selected: readonly string[]; style: VisualStyle; layers: string } = { looks: {}, selected: [], style: 'edges', layers: 'all' };

  restyle(looks: Record<string, Look | undefined>, selected: readonly string[], style: VisualStyle, layers: string): boolean {
    this.last = { looks, selected, style, layers };
    const sel = new Set(selected);
    let changed = false;
    for (const b of this.batches.values()) { if (b.restyle(looks, sel)) changed = true; b.show(style, layers); }
    return changed;
  }

  /** Every batch's copies chosen again for the camera, and drawn where they changed; true if any did. */
  relevel(cam: LodCamera, quality: DisplayQuality): boolean {
    let changed = false;
    for (const b of this.batches.values()) if (b.relevel(cam, quality)) changed = true;
    if (changed) { const l = this.last; this.restyle(l.looks, l.selected, l.style, l.layers); }
    return changed;
  }

  /** Whether any batch has coarser copies (a relevel has something to do). */
  hasCopies(): boolean {
    for (const b of this.batches.values()) if (b.parts.some((p) => p.copies.length)) return true;
    return false;
  }

  /** The element a ray hit on one of the instances' meshes, if it did. */
  nodeOf(hit: THREE.Intersection): string | undefined {
    const b = hit.object.userData.batch as Batch | undefined;
    return b && hit.instanceId !== undefined ? b.ids[hit.instanceId] : undefined;
  }

  /** The meshes a ray may hit (the faces shown). */
  pickables(): THREE.Object3D[] {
    const o: THREE.Object3D[] = [];
    for (const b of this.batches.values()) for (const p of b.parts.flatMap(variants)) { if (p.solid.visible) o.push(p.solid, p.faded); }
    return o;
  }

  /** The box of the instances shown (not those hidden), added to `box`. */
  expand(box: THREE.Box3, looks: Record<string, Look | undefined>): THREE.Box3 {
    const v = new THREE.Vector3();
    for (const b of this.batches.values())
      for (let i = 0; i < b.ids.length; i++) {
        if (looks[b.ids[i]]?.hidden) continue;
        const q = placedBox(b.mesh.box, b.raw[i]);
        box.expandByPoint(v.set(q[0], q[1], q[2])).expandByPoint(v.set(q[3], q[4], q[5]));
      }
    return box;
  }

  /** The instances' items with where each stands: what the walker takes near it. */
  *placed(): Iterable<{ item: RenderItem; at: [number, number, number] }> {
    for (const { item } of this.known.values()) {
      const m = item.instance!.matrix;
      yield { item, at: [m[9], m[10], m[11]] };
    }
  }

  /** The instance's matrix in three.js terms, for a hit's normal turned into the world. */
  matrixOf(id: string): THREE.Matrix4 | null {
    const k = this.known.get(id);
    return k ? new THREE.Matrix4().fromArray(elements(k.item.instance!.matrix)) : null;
  }

  count(): number { return this.known.size; }

  dispose() { for (const b of this.batches.values()) b.dispose(); this.batches.clear(); this.known.clear(); }
}
