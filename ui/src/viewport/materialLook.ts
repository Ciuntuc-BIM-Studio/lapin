/**
 * A material's look as three.js draws it (report #76): roughness, metalness,
 * see-through and glass, a light's glow, from look.ts. Metals and glossy faces
 * reflect a soft room (three's RoomEnvironment, made once per renderer), since
 * without something to reflect a metal shows black; the rest keep the scene's
 * own lights. Glass is see-through (its opacity) in the drawing and medium
 * views, and lets light through (its transmission and index of refraction)
 * in the realistic one (render3d.ts), switched in place on its materials.
 */

import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import type { Look } from './look';

let room: THREE.Texture | null = null;

/** The room metals reflect, made from the renderer once (null until a renderer has made it). */
export function reflections(renderer?: THREE.WebGLRenderer): THREE.Texture | null {
  if (!room && renderer) {
    const pmrem = new THREE.PMREMGenerator(renderer);
    room = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    room.userData.shared = true;
    pmrem.dispose();
  }
  return room;
}

/** Whether a look reflects its surroundings enough to want them. */
export const reflects = (l: Look): boolean => l.metalness > 0.3 || l.roughness < 0.3;

/** The opacity a look draws at (1 opaque). */
export const opacityOf = (l: Look): number => Math.max(0.05, Math.min(1, 1 - l.transparency));

/**
 * A face's material in a look: its colour or texture, how rough and metallic it is, its opacity (kept in userData so a
 * phase's fading multiplies it), its glow (kept so a selection's highlight gives it back).
 */
export function standardMaterial(color: THREE.ColorRepresentation, map: THREE.Texture | null, l: Look): THREE.MeshStandardMaterial {
  const opacity = opacityOf(l);
  const params: THREE.MeshStandardMaterialParameters = {
    color, map, roughness: l.roughness, metalness: l.metalness,
    transparent: opacity < 1, opacity, depthWrite: opacity >= 1, side: opacity < 1 ? THREE.DoubleSide : THREE.FrontSide,
    polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
  };
  // Glass: physically based, so the realistic view can let light through it (glassThrough).
  const m = l.transmission > 0 ? new THREE.MeshPhysicalMaterial({ ...params, ior: l.ior, thickness: 0 }) : new THREE.MeshStandardMaterial(params);
  if (l.transmission > 0) { m.userData.glass = { transmission: l.transmission }; lightThrough(m as THREE.MeshPhysicalMaterial, glassOn); }
  const env = reflects(l) ? reflections() : null;
  if (env) { m.envMap = env; m.envMapIntensity = 0.8; }
  m.userData.opacity = opacity;
  if (l.emissive) {
    m.emissive = new THREE.Color(l.emissive);
    m.emissiveIntensity = l.emissiveStrength;
    m.userData.emissive = { hex: m.emissive.getHex(), intensity: l.emissiveStrength };
  }
  return m;
}

/** Whether glass lets light through now (the realistic view), for the materials made from here on. */
let glassOn = false;

/** A glass material switched between see-through (its opacity) and letting light through (its transmission). */
function lightThrough(m: THREE.MeshPhysicalMaterial, on: boolean): void {
  const g = m.userData.glass as { transmission: number } | undefined;
  if (!g) return;
  const opacity = (m.userData.opacity as number | undefined) ?? m.opacity;
  m.transmission = on ? g.transmission : 0;
  m.transparent = !on && opacity < 1;
  m.opacity = on ? 1 : opacity;
  m.depthWrite = on || opacity >= 1;
  m.needsUpdate = true;
}

/** Glass through the scene (and the materials made later) letting light through, or see-through again. */
export function glassThrough(root: THREE.Object3D, on: boolean): void {
  glassOn = on;
  root.traverse((o) => {
    const mat = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    for (const m of Array.isArray(mat) ? mat : mat ? [mat] : []) if (m.userData.glass) lightThrough(m as THREE.MeshPhysicalMaterial, on);
  });
}

/** The preview's material: physically based, glass refracting through its transmission. */
export function previewMaterial(color: THREE.ColorRepresentation, map: THREE.Texture | null, l: Look, env: THREE.Texture | null): THREE.MeshPhysicalMaterial {
  const glass = l.transmission > 0;
  const m = new THREE.MeshPhysicalMaterial({
    color, map, roughness: l.roughness, metalness: l.metalness, envMap: env, envMapIntensity: 1,
    transmission: l.transmission, ior: l.ior, thickness: glass ? 0.4 : 0,
    transparent: !glass && l.transparency > 0, opacity: glass ? 1 : opacityOf(l),
  });
  if (l.emissive) { m.emissive = new THREE.Color(l.emissive); m.emissiveIntensity = l.emissiveStrength; }
  return m;
}
