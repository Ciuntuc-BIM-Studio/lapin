/** A 3D view picks only what it shows (decision 498). */

import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { clippedAway, seenHit, shownObject } from './seen3d';

const box = () => new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial());

/** What a ray down from above the box at (x, y) meets first that the view shows. */
function firstSeen(scene: THREE.Object3D, x: number, y: number, planes: THREE.Plane[] = []) {
  scene.updateMatrixWorld(true);
  const ray = new THREE.Raycaster(new THREE.Vector3(x, y, 10), new THREE.Vector3(0, 0, -1));
  return ray.intersectObject(scene, true).find((h) => seenHit(h, planes));
}

describe('what a 3D view picks', () => {
  it('passes through what is hidden to what is shown under it', () => {
    const scene = new THREE.Group();
    const top = box(); top.position.z = 2; top.userData.nodeId = 'top';
    const low = box(); low.userData.nodeId = 'low';
    const element = new THREE.Group(); element.add(top);
    scene.add(element, low);
    expect(firstSeen(scene, 0, 0)?.object.userData.nodeId).toBe('top');
    // The element hidden (a look: Hide, a category, a phase, a storey put away): three.js still meets it, the view does not.
    element.visible = false;
    expect(firstSeen(scene, 0, 0)?.object.userData.nodeId).toBe('low');
    // A part hidden on its own (a layer), or a wireframe's faces.
    element.visible = true; top.visible = false;
    expect(firstSeen(scene, 0, 0)?.object.userData.nodeId).toBe('low');
  });

  it('picks a far part by its finest copy while a coarser one is drawn', () => {
    const level = new THREE.Group(); level.visible = false; level.userData.lodLevel = true;
    const m = box(); level.add(m);
    expect(shownObject(m)).toBe(true);
    const plain = new THREE.Group(); plain.visible = false; const n = box(); plain.add(n);
    expect(shownObject(n)).toBe(false);
  });

  it('never picks what a section box or a cut takes away', () => {
    const scene = new THREE.Group();
    const top = box(); top.position.z = 2; top.userData.nodeId = 'top';
    const low = box(); low.userData.nodeId = 'low';
    scene.add(top, low);
    // Kept under z = 1 (the cut above the storeys shown, a section box's top).
    const cut = new THREE.Plane(new THREE.Vector3(0, 0, -1), 1);
    expect(firstSeen(scene, 0, 0, [cut])?.object.userData.nodeId).toBe('low');
    expect(clippedAway([-0.5, -0.5, 1.5, 0.5, 0.5, 2.5], [cut])).toBe(true);
    expect(clippedAway([-0.5, -0.5, -0.5, 0.5, 0.5, 0.5], [cut])).toBe(false);
    // Partly cut: still seen.
    expect(clippedAway([-0.5, -0.5, 0.5, 0.5, 0.5, 1.5], [cut])).toBe(false);
  });
});
