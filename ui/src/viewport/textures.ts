/**
 * Materials' looks in 3D: textures drawn here (no image files), tinted by the
 * material's colour, laid on by box projection in metres (a brick course is
 * 65 mm wherever the wall is). A material names its texture (`texture`), or
 * it is guessed from its section hatch and its name.
 */

import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';

export type TextureKind = 'brick' | 'brick-end' | 'concrete' | 'wood' | 'wood-end' | 'plaster' | 'stone' | 'metal' | 'insulation' | 'earth' | 'tiles';
/** Which face of a part a texture is for: along it, its ends (cut across), its top and bottom. */
export type Face = 'side' | 'end' | 'top';

/** A texture's repeat on the model (m per tile). */
const TILE: Record<TextureKind, number> = { brick: 0.52, 'brick-end': 0.5, concrete: 1, wood: 0.6, 'wood-end': 0.4, plaster: 1, stone: 0.8, metal: 0.5, insulation: 0.4, earth: 2, tiles: 0.6 };

/** What a material looks like in 3D on a face (null: plain): its own texture for that face, else the faces' (the ends of brick and wood show their section). */
export function textureOf(material: NodeDto | undefined, face: Face = 'side'): TextureKind | null {
  if (!material) return null;
  if (face !== 'side') {
    const key = face === 'end' ? 'textureEnd' : 'textureTop';
    const ownFace = material.props[key] ?? (face === 'top' ? material.props.textureEnd : undefined);
    if (ownFace === 'none') return null;
    if (typeof ownFace === 'string' && ownFace in TILE) return ownFace as TextureKind;
    const side = textureOf(material, 'side');
    return side === 'brick' ? 'brick-end' : side === 'wood' ? 'wood-end' : side;
  }
  const own = material.props.texture;
  if (own === 'none') return null;
  if (typeof own === 'string' && own in TILE) return own as TextureKind;
  const hatch = String((material.props.section as { hatch?: string } | undefined)?.hatch ?? '').toUpperCase();
  const byHatch: Record<string, TextureKind> = { BRICK: 'brick', CONCRETE: 'concrete', WOOD: 'wood', WOODCROSS: 'wood', INSULATION: 'insulation', EARTH: 'earth', TILES: 'tiles', STEEL: 'metal', GRAVEL: 'earth' };
  if (byHatch[hatch]) return byHatch[hatch];
  const n = material.name.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
  if (/brick|caramid|masonry|zidari/.test(n)) return 'brick';
  if (/concrete|beton|screed|sapa/.test(n)) return 'concrete';
  if (/timber|wood|lemn|clt|osb/.test(n)) return 'wood';
  if (/insul|wool|eps|xps|vata|polistiren/.test(n)) return 'insulation';
  if (/plaster|render|tencu|gips/.test(n)) return 'plaster';
  if (/stone|piatra|granit|marmur/.test(n)) return 'stone';
  if (/steel|metal|otel|alumin|zinc/.test(n)) return 'metal';
  if (/earth|soil|pamant|ground/.test(n)) return 'earth';
  if (/tile|ceramic|gresie|faianta/.test(n)) return 'tiles';
  return null;
}

const cache = new Map<string, THREE.Texture>();

function shade(hex: string, k: number): string {
  const c = new THREE.Color(hex);
  c.multiplyScalar(k);
  return `#${c.getHexString()}`;
}

/** A seeded noise, so a texture comes out the same each time. */
function rng(seed: number) { return () => ((seed = (seed * 16807) % 2147483647) / 2147483647); }

function draw(kind: TextureKind, color: string): HTMLCanvasElement {
  const S = 256;
  const cv = document.createElement('canvas');
  cv.width = cv.height = S;
  const g = cv.getContext('2d')!;
  const r = rng(kind.length * 977 + 13);
  g.fillStyle = color;
  g.fillRect(0, 0, S, S);
  const speckle = (n: number, dark: number, light: number, size = 2) => {
    for (let i = 0; i < n; i++) { g.fillStyle = r() < 0.5 ? shade(color, dark) : shade(color, light); g.fillRect(r() * S, r() * S, size, size); }
  };
  switch (kind) {
    case 'brick': {
      // 250 × 65 bricks, 10 mm joints: 2 bricks a tile across (0.52 m), 8 courses up (75 mm each, 0.6 m → drawn on the square).
      const rows = 8, cols = 2, jh = S / rows, jw = S / cols;
      for (let y = 0; y < rows; y++)
        for (let x = -1; x < cols; x++) {
          const off = y % 2 ? jw / 2 : 0;
          g.fillStyle = shade(color, 0.85 + r() * 0.3);
          g.fillRect(x * jw + off + 2, y * jh + 2, jw - 4, jh - 4);
        }
      g.fillStyle = shade(color, 1.45);
      for (let y = 0; y <= rows; y++) g.fillRect(0, y * jh - 1, S, 2);
      break;
    }
    case 'brick-end': {
      // A hollow block seen across: 2 blocks a tile, each with three rows of holes in its web.
      const cols = 2, rows = 4, bw = S / cols, bh = S / rows;
      for (let y = 0; y < rows; y++)
        for (let x = 0; x < cols; x++) {
          const off = y % 2 ? bw / 2 : 0;
          const bx = x * bw + off, by = y * bh;
          g.fillStyle = shade(color, 0.9 + r() * 0.2);
          g.fillRect(bx + 2, by + 2, bw - 4, bh - 4);
          g.fillStyle = shade(color, 0.45);
          for (let hy = 0; hy < 3; hy++) for (let hx = 0; hx < 4; hx++) g.fillRect(bx + 8 + hx * (bw - 16) / 4 + 2, by + 6 + hy * (bh - 12) / 3 + 2, (bw - 16) / 4 - 4, (bh - 12) / 3 - 4);
          if (off) { g.fillStyle = shade(color, 0.9); g.fillRect(bx - bw + 2, by + 2, bw - 4, bh - 4); }
        }
      g.fillStyle = shade(color, 1.45);
      for (let y = 0; y <= rows; y++) g.fillRect(0, y * bh - 1, S, 2);
      break;
    }
    case 'wood-end': {
      // End grain: rings about a point off the tile, every few millimetres.
      g.strokeStyle = shade(color, 0.72); g.lineWidth = 1.5;
      for (let i = 0; i < 40; i++) { g.beginPath(); g.arc(S * 0.15, S * 1.1, 10 + i * 9 + r() * 3, 0, 7); g.stroke(); }
      speckle(600, 0.9, 1.08);
      break;
    }
    case 'concrete': speckle(2600, 0.85, 1.12); for (let i = 0; i < 40; i++) { g.fillStyle = shade(color, 0.75); g.beginPath(); g.arc(r() * S, r() * S, 1 + r() * 2, 0, 7); g.fill(); } break;
    case 'plaster': speckle(1800, 0.93, 1.06, 3); break;
    case 'stone': {
      for (let i = 0; i < 26; i++) { g.fillStyle = shade(color, 0.8 + r() * 0.4); g.fillRect(r() * S, r() * S, 30 + r() * 60, 20 + r() * 40); }
      speckle(1200, 0.85, 1.1);
      break;
    }
    case 'wood': {
      for (let x = 0; x < S; x += 2) { g.fillStyle = shade(color, 0.85 + 0.25 * Math.sin(x * 0.11 + Math.sin(x * 0.023) * 3)); g.fillRect(x, 0, 2, S); }
      g.strokeStyle = shade(color, 0.7); g.lineWidth = 1;
      for (let i = 0; i < 6; i++) { const x = r() * S; g.beginPath(); g.moveTo(x, 0); for (let y = 0; y <= S; y += 16) g.lineTo(x + Math.sin(y * 0.05 + i) * 4, y); g.stroke(); }
      break;
    }
    case 'metal': for (let x = 0; x < S; x += S / 8) { g.fillStyle = shade(color, 0.82); g.fillRect(x, 0, 3, S); g.fillStyle = shade(color, 1.15); g.fillRect(x + 3, 0, 2, S); } break;
    case 'insulation': {
      g.strokeStyle = shade(color, 0.75); g.lineWidth = 2;
      for (let y = 8; y < S; y += 16) { g.beginPath(); for (let x = 0; x <= S; x += 8) g.lineTo(x, y + Math.sin(x * 0.2) * 5); g.stroke(); }
      break;
    }
    case 'earth': speckle(4200, 0.75, 1.2, 3); break;
    case 'tiles': g.fillStyle = shade(color, 0.7); for (let i = 0; i <= 4; i++) { g.fillRect(i * S / 4 - 1, 0, 2, S); g.fillRect(0, i * S / 4 - 1, S, 2); } break;
  }
  return cv;
}

/** The texture of a kind in a colour (made once). */
export function textureFor(kind: TextureKind, color: string): THREE.Texture {
  const key = `${kind}:${color}`;
  let t = cache.get(key);
  if (!t) {
    t = new THREE.CanvasTexture(draw(kind, color));
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 4;
    // Shared by every mesh in that material: a mesh that goes leaves it.
    t.userData.shared = true;
    cache.set(key, t);
  }
  return t;
}

/** Box-projected texture coordinates (metres / the texture's tile) for a geometry with normals. */
export function boxUv(geometry: THREE.BufferGeometry, kind: TextureKind): void {
  const pos = geometry.getAttribute('position');
  const nrm = geometry.getAttribute('normal');
  if (!pos || !nrm) return;
  const tile = TILE[kind];
  const uv = new Float32Array(pos.count * 2);
  for (let i = 0; i < pos.count; i++) {
    const [x, y, z] = [pos.getX(i), pos.getY(i), pos.getZ(i)];
    const [ax, ay, az] = [Math.abs(nrm.getX(i)), Math.abs(nrm.getY(i)), Math.abs(nrm.getZ(i))];
    // The face's dominant axis drops out (the scene is z-up): on a wall, u runs along it and v up.
    let u: number, v: number;
    if (az >= ax && az >= ay) { u = x; v = y; } else if (ax >= ay) { u = y; v = z; } else { u = x; v = z; }
    uv[2 * i] = u / tile;
    uv[2 * i + 1] = v / tile;
  }
  geometry.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
}

/**
 * A part's triangles by the face they lie on: top and bottom (horizontal),
 * its sides (the vertical faces of the direction with the most area: a wall's
 * faces along it) and its ends (the vertical faces across that direction).
 */
export function splitByFace(positions: Float32Array, indices: Uint32Array): Record<Face, Uint32Array> {
  const n = indices.length / 3;
  const nx = new Float64Array(n), ny = new Float64Array(n), nz = new Float64Array(n), area = new Float64Array(n);
  for (let t = 0; t < n; t++) {
    const [a, b, c] = [indices[3 * t] * 3, indices[3 * t + 1] * 3, indices[3 * t + 2] * 3];
    const ux = positions[b] - positions[a], uy = positions[b + 1] - positions[a + 1], uz = positions[b + 2] - positions[a + 2];
    const vx = positions[c] - positions[a], vy = positions[c + 1] - positions[a + 1], vz = positions[c + 2] - positions[a + 2];
    const x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
    const l = Math.hypot(x, y, z) || 1;
    nx[t] = x / l; ny[t] = y / l; nz[t] = z / l; area[t] = l / 2;
  }
  // The sides' direction: the horizontal normal direction (folded over 180°) carrying the most area, in 5° bins.
  const bins = new Float64Array(36);
  for (let t = 0; t < n; t++) {
    if (Math.abs(nz[t]) > 0.7) continue;
    const ang = (Math.atan2(ny[t], nx[t]) * 180) / Math.PI;
    bins[Math.floor((((ang % 180) + 180) % 180) / 5) % 36] += area[t];
  }
  let best = 0;
  for (let i = 1; i < 36; i++) if (bins[i] > bins[best]) best = i;
  const side = ((best + 0.5) * 5 * Math.PI) / 180;
  const sx = Math.cos(side), sy = Math.sin(side);
  const out: Record<Face, number[]> = { side: [], end: [], top: [] };
  for (let t = 0; t < n; t++) {
    const face: Face = Math.abs(nz[t]) > 0.7 ? 'top' : Math.abs(nx[t] * sx + ny[t] * sy) < 0.5 ? 'end' : 'side';
    out[face].push(indices[3 * t], indices[3 * t + 1], indices[3 * t + 2]);
  }
  return { side: Uint32Array.from(out.side), end: Uint32Array.from(out.end), top: Uint32Array.from(out.top) };
}
