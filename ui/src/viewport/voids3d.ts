/**
 * The openings' voids in 3D (decision 289): while View → Opening voids is
 * on, the 3D view asks the engine for `coordination.voids` (each void's
 * triangles, the project's kinds and offset) 300 ms after the model last
 * changed and draws them in a group of their own, `opening-voids`: a
 * translucent violet body each with its edges and its mark. A void picks as
 * its opening (its host for a roof window), so a click shows the opening's
 * properties. For the screen only: sheets and drawings never carry them; the
 * IFC and mesh exports write them only when asked.
 */

import { useEffect, type MutableRefObject } from 'react';
import * as THREE from 'three';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';
import { loadVoids, type VoidDto } from '../modules/core/coordination/coordination';

export const VOIDS_GROUP = 'opening-voids';
export const VOID_COLOR = 0xb05cff;
/** Above this many voids their marks are left out (a sprite each costs a texture). */
export const MAX_MARKS = 300;

/** The mark as a sprite, violet on a faint card. */
function mark(text: string): THREE.Sprite {
  const cv = document.createElement('canvas');
  const c = cv.getContext('2d');
  cv.width = 96; cv.height = 40;
  if (c) {
    c.fillStyle = 'rgba(255,255,255,0.8)';
    c.fillRect(0, 0, cv.width, cv.height);
    c.fillStyle = '#7a2fd0'; c.font = 'bold 26px sans-serif'; c.textAlign = 'center'; c.textBaseline = 'middle';
    c.fillText(text, cv.width / 2, cv.height / 2 + 1);
  }
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), depthTest: false, transparent: true }));
  sprite.scale.set(0.36, 0.15, 1);
  sprite.renderOrder = 4;
  sprite.raycast = () => undefined;
  return sprite;
}

/** The voids as a group: a translucent mesh each (`userData.nodeId` what it selects, `userData.openingVoid` its key), its edges, its mark over it. */
export function voidsGroup(voids: VoidDto[], selected: ReadonlySet<string>): THREE.Group {
  const group = new THREE.Group();
  group.name = VOIDS_GROUP;
  for (const v of voids) {
    if (!v.positions || !v.indices || v.positions.length < 9) continue;
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(v.positions, 3));
    geo.setIndex(v.indices);
    geo.computeVertexNormals();
    const on = selected.has(v.selectId);
    const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color: VOID_COLOR, transparent: true, opacity: on ? 0.55 : 0.3, depthWrite: false, side: THREE.DoubleSide }));
    mesh.userData.nodeId = v.selectId;
    mesh.userData.openingVoid = v.key;
    mesh.renderOrder = 3;
    group.add(mesh);
    const edges = new THREE.LineSegments(new THREE.EdgesGeometry(geo, 20), new THREE.LineBasicMaterial({ color: VOID_COLOR, transparent: true, opacity: 0.9 }));
    edges.raycast = () => undefined;
    group.add(edges);
    if (voids.length <= MAX_MARKS) {
      geo.computeBoundingBox();
      const b = geo.boundingBox!;
      const s = mark(v.mark);
      s.position.set((b.min.x + b.max.x) / 2, (b.min.y + b.max.y) / 2, b.max.z + 0.12);
      group.add(s);
    }
  }
  return group;
}

export function disposeVoids(group: THREE.Object3D): void {
  group.traverse((c) => {
    const m = c as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    for (const x of Array.isArray(mat) ? mat : mat ? [mat] : []) {
      (x as THREE.SpriteMaterial).map?.dispose();
      x.dispose();
    }
  });
}

export function removeVoids(scene: THREE.Scene): void {
  const old = scene.getObjectByName(VOIDS_GROUP);
  if (old) { scene.remove(old); disposeVoids(old); }
}

/** The 3D view's side: while the setting is on, the voids asked for after the model changes and swapped in; off, taken out. */
export function useVoids3d(scene: MutableRefObject<THREE.Scene | null>, invalidate: MutableRefObject<() => void>): void {
  const on = useUi((s) => s.openingVoids);
  const render = useSession((s) => (on ? s.render : null));
  const selected = useUi((s) => s.selected);
  useEffect(() => {
    if (!on) {
      const s = scene.current;
      if (s) { removeVoids(s); invalidate.current(); }
      return;
    }
    let cancelled = false;
    const timer = setTimeout(() => {
      void loadVoids(true).then((voids) => {
        const s = scene.current;
        if (cancelled || !s) return;
        removeVoids(s);
        s.add(voidsGroup(voids, new Set(useUi.getState().selected)));
        invalidate.current();
      });
    }, 300);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [on, render, scene, invalidate]);
  // The selected ones drawn stronger, without asking the engine again.
  useEffect(() => {
    const g = scene.current?.getObjectByName(VOIDS_GROUP);
    if (!g) return;
    const on = new Set(selected);
    for (const c of g.children) if (c.userData.openingVoid) ((c as THREE.Mesh).material as THREE.MeshBasicMaterial).opacity = on.has(c.userData.nodeId as string) ? 0.55 : 0.3;
    invalidate.current();
  }, [selected, scene, invalidate]);
  useEffect(() => () => { const s = scene.current; if (s) removeVoids(s); }, [scene]);
}
