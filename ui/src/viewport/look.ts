/**
 * How a material renders (report #76), as the engine resolves it
 * (engine/src/Platform/Lapin.Platform/MaterialLook.cs): roughness, metalness,
 * transparency, glass's transmission and index of refraction, a light's
 * emissive colour, and how its texture turns. What a material does not set
 * comes from its name, by the same rules as the engine's, so the 3D view and
 * the exports agree.
 */

import type { NodeDto } from '../engine/protocol';

export interface Look {
  roughness: number;
  metalness: number;
  transparency: number;
  transmission: number;
  ior: number;
  emissive: string | null;
  emissiveStrength: number;
  textureRotation: number;
  /** How strongly its normal map and its bump map show (1 as the image says, 0 flat). */
  normalStrength: number;
  bumpStrength: number;
}

export const PLAIN: Look = { roughness: 0.85, metalness: 0, transparency: 0, transmission: 0, ior: 1.5, emissive: null, emissiveStrength: 1, textureRotation: 0, normalStrength: 1, bumpStrength: 1 };

/** The look's numeric fields and their ranges, as the engine checks them. */
export const LOOK_RANGES: Record<Exclude<keyof Look, 'emissive'>, [number, number]> = {
  roughness: [0, 1], metalness: [0, 1], transparency: [0, 1], transmission: [0, 1], ior: [1, 3], emissiveStrength: [0, 100], textureRotation: [-360, 360],
  normalStrength: [0, 10], bumpStrength: [0, 10],
};

/** Lower case without diacritics, padded with spaces so a word's end can be matched. */
export function fold(name: string | undefined | null): string {
  return ` ${(name ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/ł/g, 'l').replace(/ø/g, 'o').replace(/æ/g, 'a').replace(/ß/g, 's')} `;
}

const has = (n: string, ...words: string[]) => words.some((w) => n.includes(w));

/** What a material's name says about its look (MaterialLook.Guess). */
export function guessLook(name: string | undefined | null): Look {
  const n = fold(name);
  if (has(n, 'insul', 'wool', 'vata', ' eps', ' xps', 'polistiren', 'mineral')) return { ...PLAIN, roughness: 0.97 };
  if (has(n, 'glass', 'glazing', 'sticla', 'geam', 'vitra', 'verre', 'vidrio', 'glas', 'szklo', 'vetro', 'vidro')) {
    const frosted = has(n, 'frosted', 'satin', 'sablat');
    return { ...PLAIN, roughness: frosted || has(n, 'matt', ' mat ') ? 0.45 : 0.05, transparency: frosted ? 0.35 : 0.6, transmission: 0.9, ior: 1.52 };
  }
  if (has(n, 'ral ', 'powder', 'painted', 'vopsit', 'lacuit', 'coated')) return { ...PLAIN, roughness: 0.45, metalness: has(n, 'metallic', 'metalizat') ? 0.6 : 0.1 };
  if (has(n, 'chrome', 'crom', 'mirror', 'oglinda', 'polished', 'lustruit')) return { ...PLAIN, roughness: 0.08, metalness: 1 };
  if (has(n, 'steel', 'otel', 'inox', 'alumin', 'metal', 'zinc', 'copper', 'cupru', 'brass', 'alama', 'iron', 'fier', 'titan', 'anodi', 'hardware', 'feronerie'))
    return { ...PLAIN, roughness: has(n, 'brushed', 'periat', 'anodi') ? 0.3 : has(n, 'galvan', 'zinc', 'rust', 'corten') ? 0.55 : 0.35, metalness: 1 };
  if (has(n, 'tile', 'ceramic', 'gresie', 'faianta', 'porcelain', 'portelan', 'marble', 'marmur', 'granite', 'granit')) return { ...PLAIN, roughness: has(n, 'matt', ' mat ') ? 0.55 : 0.2 };
  if (has(n, 'water', ' apa ', 'pool', 'piscina')) return { ...PLAIN, roughness: 0.02, transparency: 0.5, transmission: 0.8, ior: 1.33 };
  if (has(n, 'plastic', 'pvc', 'acrylic', 'acril', 'polycarb', 'policarb')) return { ...PLAIN, roughness: 0.4 };
  if (has(n, 'parquet', 'parchet', 'laminat', 'lacquer', 'varnish', 'lac ')) return { ...PLAIN, roughness: 0.45 };
  if (has(n, 'timber', 'wood', 'lemn', 'oak', 'stejar', 'pine', 'pin ', 'brad', 'beech', 'fag', 'walnut', 'nuc', 'plank', 'deck', 'osb', 'clt', 'plywood', 'placaj')) return { ...PLAIN, roughness: 0.7 };
  if (has(n, 'paint', 'vopsea', 'lavabil', 'enamel', 'email')) return { ...PLAIN, roughness: 0.6 };
  if (has(n, 'fabric', 'textil', 'carpet', 'mocheta', 'cushion', 'perna', 'felt', 'earth', 'soil', 'pamant', 'gravel', 'pietris')) return { ...PLAIN, roughness: 0.97 };
  if (has(n, 'concrete', 'beton', 'brick', 'caramid', 'plaster', 'render', 'tencu', 'stone', 'piatra', 'screed', 'sapa', 'gips', 'gypsum', 'mortar')) return { ...PLAIN, roughness: 0.9 };
  if (has(n, 'lamp', ' led ', 'diffuser', 'difuzor', 'lumina', 'luminaire')) return { ...PLAIN, roughness: 0.5, emissive: '#fff4e0', emissiveStrength: 1 };
  return PLAIN;
}

/** A material's look: its own values, the rest from its name (MaterialLook.Of). */
export function lookOf(material: NodeDto | undefined): Look {
  if (!material) return PLAIN;
  const g = guessLook(material.name);
  const p = material.props;
  const num = (k: keyof typeof LOOK_RANGES): number => (typeof p[k] === 'number' && Number.isFinite(p[k]) ? (p[k] as number) : g[k]);
  const emissive = 'emissive' in p ? (typeof p.emissive === 'string' && /^#[0-9a-f]{6}$/i.test(p.emissive) ? p.emissive : null) : g.emissive;
  return {
    roughness: num('roughness'), metalness: num('metalness'), transparency: num('transparency'), transmission: num('transmission'), ior: num('ior'),
    emissive, emissiveStrength: num('emissiveStrength'), textureRotation: num('textureRotation'),
    normalStrength: num('normalStrength'), bumpStrength: num('bumpStrength'),
  };
}

/** Box-projected coordinates turned by a rotation (degrees), as the engine's exports turn them (MeshScene.BoxUv). */
export function rotateUv(u: number, v: number, degrees: number): [number, number] {
  if (!degrees) return [u, v];
  const a = (degrees * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  return [u * c + v * s, -u * s + v * c];
}
