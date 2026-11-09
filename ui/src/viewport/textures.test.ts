import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { pictureUv } from './textures';

describe('an entourage picture', () => {
  it.each([
    { x: 0, y: 0, z: 0, angle: 0, width: 2.625, height: 1.75 },
    { x: 4.15, y: 1.9, z: 0, angle: 0, width: 2.625, height: 1.75 },
    { x: -12, y: 18, z: 3.4, angle: 90, width: 2.625, height: 2.4 },
    { x: 23, y: -8, z: -1.2, angle: 37, width: 0.8, height: 1.9 },
  ])('keeps the full image after moving, turning or resizing: $angle degrees at $x, $y, $z', ({ x, y, z, angle, width, height }) => {
    const corners = [[-width / 2, 0], [width / 2, 0], [width / 2, height], [-width / 2, height]];
    const c = Math.cos(angle * Math.PI / 180), s = Math.sin(angle * Math.PI / 180);
    const positions = corners.flatMap(([u, v]) => [x + u * c, y + u * s, z + v]);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    pictureUv(geometry, angle);
    const uv = geometry.getAttribute('uv');
    const expected = [[0, 0], [1, 0], [1, 1], [0, 1]];
    expected.forEach(([u, v], i) => {
      expect(uv.getX(i)).toBeCloseTo(u, 5);
      expect(uv.getY(i)).toBeCloseTo(v, 5);
    });
    expect(Array.from(geometry.getAttribute('position').array)).toEqual(Array.from(new Float32Array(positions)));
    geometry.dispose();
  });
});
