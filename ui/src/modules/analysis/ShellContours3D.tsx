/**
 * The shells' contours in the 3D view (docs/plan-fem.md §10.10 step 6): each slab's and wall's mesh, as the engine gave it
 * (analysis.contours), laid just off both its faces in the scene, coloured in bands by the quantity at its nodes (a texture
 * of the bands' colours read by the value, so a band's edge falls where the value crosses it, between the nodes). It takes
 * no ray: picking still finds the model under it.
 */

import { useEffect, useState } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps } from '../../platform/module';
import { useResults } from './useResults';
import { bandColours, bandEdges, shownRange, shownValues, triangles, type ContoursDto } from './shells';

export const BANDS = 10;

/** The bands' colours as a texture one texel a band, read without blending so each band stays one colour. */
function bandTexture(bands: number): THREE.DataTexture {
  const data = new Uint8Array(bands * 4);
  bandColours(bands).forEach((c, i) => {
    const [r, g, b] = c.match(/\d+/g)!.map(Number);
    data.set([r!, g!, b!, 255], i * 4);
  });
  const t = new THREE.DataTexture(data, bands, 1, THREE.RGBAFormat);
  t.magFilter = THREE.NearestFilter;
  t.minFilter = THREE.NearestFilter;
  t.colorSpace = THREE.SRGBColorSpace;
  t.needsUpdate = true;
  return t;
}

/** The contours as objects of the scene: a mesh a surface, a hair off each of its faces. */
export function contourGroup(c: ContoursDto, bands = BANDS): THREE.Group {
  const group = new THREE.Group();
  group.name = 'analysis-contours';
  const { min, max } = shownRange(c);
  const edges = bandEdges(min, max, bands);
  const span = edges[edges.length - 1]! - edges[0]!;
  const texture = bandTexture(edges.length - 1);
  const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2, toneMapped: false });
  for (const s of c.surfaces) {
    const values = shownValues(s, c.quantity);
    const n = values.length;
    const off = (s.thickness ?? 0.2) / 2 + 0.005;
    const [nx, ny, nz] = s.normal as [number, number, number];
    const pos = new Float32Array(n * 2 * 3);
    const uv = new Float32Array(n * 2 * 2);
    for (let i = 0; i < n; i++) {
      for (const [side, k] of [[1, i], [-1, i + n]] as const) {
        pos[k * 3] = s.positions[i * 3]! + nx * off * side;
        pos[k * 3 + 1] = s.positions[i * 3 + 1]! + ny * off * side;
        pos[k * 3 + 2] = s.positions[i * 3 + 2]! + nz * off * side;
        // Inside the texture's first and last texels at the ends, so the extremes take their bands' colours.
        uv[k * 2] = span > 0 ? Math.min(0.9999, Math.max(0.0001, (values[i]! - edges[0]!) / span)) : 0.5;
        uv[k * 2 + 1] = 0.5;
      }
    }
    const index: number[] = [];
    for (const [a, b, d] of triangles(s.faces)) index.push(a, b, d, a + n, d + n, b + n);
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
    g.setIndex(index);
    const mesh = new THREE.Mesh(g, material);
    mesh.raycast = () => undefined;
    mesh.renderOrder = 2;
    mesh.userData.contourSurface = s.sourceId;
    group.add(mesh);
  }
  group.userData.dispose = () => {
    group.traverse((o) => { if (o instanceof THREE.Mesh) o.geometry.dispose(); });
    material.dispose();
    texture.dispose();
  };
  return group;
}

export function ShellContours3D({ canvas, scene, invalidate }: Overlay3DProps) {
  const show = useResults((s) => s.show);
  const stale = useResults((s) => s.stale);
  const contours = useResults((s) => s.contours);
  const shellQuantity = useResults((s) => s.shellQuantity);
  // The view makes its scene after its overlays first mount (a parent's effects run after its children's): wait for it.
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (ready) return;
    let frame = 0;
    const look = () => { if (scene?.current) setReady(true); else frame = requestAnimationFrame(look); };
    look();
    return () => cancelAnimationFrame(frame);
  }, [scene, ready]);
  useEffect(() => {
    const s = scene?.current;
    if (!ready || !s || !show || stale || !contours || !shellQuantity || contours.surfaces.length === 0) return;
    const group = contourGroup(contours);
    s.add(group);
    // What the view holds, for the guide's scenes to see: the surfaces contoured.
    const host = canvas.current;
    if (host) host.dataset.contours = String(group.children.length);
    invalidate?.current?.();
    return () => {
      s.remove(group);
      if (host) delete host.dataset.contours;
      (group.userData.dispose as () => void)();
      invalidate?.current?.();
    };
  }, [ready, canvas, scene, invalidate, show, stale, contours, shellQuantity]);
  return null;
}
