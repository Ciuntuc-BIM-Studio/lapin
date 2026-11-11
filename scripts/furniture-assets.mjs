#!/usr/bin/env node
// The furniture models (docs/plan-furniture.md, Configuratorul flexibil, stage 2): picks the curated models out of an
// unpacked CC0 kit, copies each GLB and its preview unchanged, and writes the register beside them
// (engine/src/Modules/Interior/Lapin.Furniture.Models/furniture-assets.json): the source, the licence, the hashes, and
// what Lapin reads to stretch each one (its stretch zones, its sizes, its material slots → Lapin's roles).
//
//   node scripts/furniture-assets.mjs <unpacked kit folder> <the kit's zip>
//
// The kit is data: nothing in it is run. The GLB is read here only for its bounding box and its corners (its JSON chunk
// and positions), to find the zones. `pnpm check:licenses` checks the register against the files shipped.
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const [kit, zip] = process.argv.slice(2);
if (!kit || !zip || !existsSync(join(kit, 'License.txt'))) {
  console.error('usage: node scripts/furniture-assets.mjs <unpacked Kenney Furniture Kit folder> <its zip>');
  process.exit(1);
}
const licence = readFileSync(join(kit, 'License.txt'), 'utf8');
if (!/Creative Commons Zero, CC0/.test(licence)) { console.error('The kit\'s License.txt does not say CC0: stopped.'); process.exit(1); }

const sha = (b) => createHash('sha256').update(b).digest('hex');
const out = join(root, 'engine/src/Modules/Interior/Lapin.Furniture.Models');
const models = join(out, 'models');
const thumbs = join(root, 'ui/public/furniture-models');

// The curated set: id (the kit's name), the name Lapin shows, the group, IFC's type, the size a new type starts with
// (W × D × H, metres; the model stretched there by its zones), and what is particular to it.
const PICK = [
  // Sofas and armchairs: arms and legs keep their size, the seat stretches.
  ['loungeSofa', 'Sofa', 'Sofas', 'SOFA', [2.0, 0.9, 0.82], { seats: true }],
  ['loungeDesignSofa', 'Modern sofa', 'Sofas', 'SOFA', [2.2, 0.85, 0.75], { seats: true }],
  ['loungeSofaLong', 'Sofa with chaise', 'Sofas', 'SOFA', [2.4, 1.6, 0.82], { seats: true }],
  ['loungeSofaCorner', 'Corner sofa section', 'Sofas', 'SOFA', [2.0, 2.0, 0.82], {}],
  ['loungeDesignSofaCorner', 'Modern corner sofa section', 'Sofas', 'SOFA', [2.2, 2.2, 0.75], {}],
  ['loungeSofaOttoman', 'Ottoman', 'Sofas', 'USERDEFINED', [0.8, 0.8, 0.42], { objectType: 'Ottoman', free: true }],
  ['loungeChair', 'Armchair', 'Armchairs', 'CHAIR', [0.95, 0.85, 0.82], {}],
  ['loungeChairRelax', 'Recliner', 'Armchairs', 'CHAIR', [0.9, 1.25, 1.0], {}],
  ['loungeDesignChair', 'Modern armchair', 'Armchairs', 'CHAIR', [1.2, 0.85, 0.75], {}],
  // Chairs and stools.
  ['chair', 'Wooden chair', 'Chairs', 'CHAIR', [0.44, 0.48, 0.9], { free: true }],
  ['chairCushion', 'Chair with cushion', 'Chairs', 'CHAIR', [0.44, 0.48, 0.88], { free: true }],
  ['chairRounded', 'Rounded chair', 'Chairs', 'CHAIR', [0.44, 0.48, 0.88], { free: true }],
  ['chairModernCushion', 'Modern chair', 'Chairs', 'CHAIR', [0.46, 0.5, 0.88], { free: true }],
  ['chairModernFrameCushion', 'Modern frame chair', 'Chairs', 'CHAIR', [0.46, 0.5, 0.88], { free: true }],
  ['chairDesk', 'Office chair', 'Chairs', 'CHAIR', [0.62, 0.62, 1.05], { free: true }],
  ['stoolBar', 'Bar stool', 'Chairs', 'CHAIR', [0.45, 0.42, 0.8], { free: true, objectType: 'Stool' }],
  ['stoolBarSquare', 'Square bar stool', 'Chairs', 'CHAIR', [0.38, 0.38, 0.75], { free: true, objectType: 'Stool' }],
  ['bench', 'Bench with back', 'Chairs', 'USERDEFINED', [1.4, 0.45, 0.88], { free: true, objectType: 'Bench' }],
  ['benchCushion', 'Bench with back and cushion', 'Chairs', 'USERDEFINED', [1.4, 0.45, 0.88], { free: true, objectType: 'Bench' }],
  // Tables: the top and the legs keep their thickness; the legs stretch with the height.
  ['table', 'Dining table', 'Tables', 'TABLE', [1.6, 0.9, 0.75], { free: true }],
  ['tableCross', 'Trestle table', 'Tables', 'TABLE', [1.8, 0.9, 0.75], { free: true }],
  ['tableGlass', 'Glass dining table', 'Tables', 'TABLE', [1.6, 0.9, 0.75], { free: true }],
  ['tableRound', 'Round table', 'Tables', 'TABLE', [1.2, 1.2, 0.75], { axes: 'z', free: true }],
  ['tableCoffee', 'Coffee table', 'Tables', 'TABLE', [1.2, 0.6, 0.42], { free: true }],
  ['tableCoffeeGlass', 'Glass coffee table', 'Tables', 'TABLE', [1.2, 0.6, 0.42], { free: true }],
  ['tableCoffeeSquare', 'Square coffee table', 'Tables', 'TABLE', [0.8, 0.8, 0.42], { free: true }],
  ['sideTable', 'Side table', 'Tables', 'TABLE', [0.6, 0.45, 0.55], {}],
  // Lamps (decorative: lighting design is MEP's fixtures), plants, rugs.
  ['lampRoundFloor', 'Floor lamp', 'Lamps', 'USERDEFINED', [0.35, 0.35, 1.65], { axes: 'z', objectType: 'Floor lamp', free: true }],
  ['lampSquareFloor', 'Square floor lamp', 'Lamps', 'USERDEFINED', [0.3, 0.3, 1.6], { axes: 'z', objectType: 'Floor lamp', free: true }],
  ['lampRoundTable', 'Table lamp', 'Lamps', 'USERDEFINED', [0.3, 0.3, 0.55], { axes: 'z', objectType: 'Table lamp', free: true, mount: 0.75 }],
  ['lampSquareCeiling', 'Pendant lamp', 'Lamps', 'USERDEFINED', [0.3, 0.3, 0.5], { axes: 'z', objectType: 'Pendant lamp', free: true, mount: 1.9 }],
  ['lampWall', 'Wall lamp', 'Lamps', 'USERDEFINED', [0.3, 0.15, 0.2], { axes: 'z', objectType: 'Wall lamp', mount: 1.8 }],
  ['pottedPlant', 'Potted plant', 'Plants', 'USERDEFINED', [0.45, 0.45, 1.3], { objectType: 'Plant', free: true, uniform: true }],
  ['plantSmall1', 'Small plant', 'Plants', 'USERDEFINED', [0.2, 0.2, 0.3], { objectType: 'Plant', free: true, uniform: true }],
  ['plantSmall2', 'Small plant, leafy', 'Plants', 'USERDEFINED', [0.2, 0.2, 0.3], { objectType: 'Plant', free: true, uniform: true }],
  ['rugRectangle', 'Rug', 'Rugs', 'USERDEFINED', [2.4, 1.7, 0.01], { objectType: 'Rug', free: true, flat: true }],
  ['rugRounded', 'Rug, rounded corners', 'Rugs', 'USERDEFINED', [2.4, 1.7, 0.01], { objectType: 'Rug', free: true, flat: true }],
  ['rugSquare', 'Square rug', 'Rugs', 'USERDEFINED', [2.0, 2.0, 0.01], { objectType: 'Rug', free: true, flat: true }],
  ['rugRound', 'Round rug', 'Rugs', 'USERDEFINED', [2.0, 2.0, 0.01], { objectType: 'Rug', free: true, flat: true }],
];

// The kit's material slots → Lapin's roles (FurnitureKinds.RoleDefaults); a group may read a slot its own way.
/** The kit's unit in metres: its pieces at their own proportions (a sofa 0.98 units wide is 1.96 m). */
const KIT_SCALE = 2;

const SLOT = { carpet: 'upholstery', carpetBlue: 'upholstery', carpetWhite: 'upholstery', carpetDarker: 'accent', wood: 'body', woodDark: 'accent', metal: 'metal', metalMedium: 'metal', glass: 'glass', lamp: 'shade', plant: 'foliage', _defaultMat: 'body' };
const SLOT_BY_GROUP = { Sofas: { wood: 'legs' }, Armchairs: { wood: 'legs' }, Tables: { wood: 'top' }, Plants: { wood: 'pot' } };

// ---- A GLB read as data: the JSON chunk, the positions through the node tree (glTF: metres, Y up, front +Z). ----
function glb(bytes) {
  if (bytes.readUInt32LE(0) !== 0x46546c67) throw new Error('not a GLB');
  let at = 12, json = null, bin = null;
  while (at + 8 <= bytes.length) {
    const len = bytes.readUInt32LE(at), type = bytes.readUInt32LE(at + 4);
    if (type === 0x4e4f534a) json = JSON.parse(bytes.subarray(at + 8, at + 8 + len).toString('utf8'));
    else if (type === 0x004e4942) bin = bytes.subarray(at + 8, at + 8 + len);
    at += 8 + len;
  }
  return { json, bin };
}
const mul = (a, b) => { const o = new Array(16).fill(0); for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) for (let k = 0; k < 4; k++) o[c * 4 + r] += a[k * 4 + r] * b[c * 4 + k]; return o; };
function matrix(n) {
  if (n.matrix) return n.matrix;
  const [tx, ty, tz] = n.translation ?? [0, 0, 0], [x, y, z, w] = n.rotation ?? [0, 0, 0, 1], [sx, sy, sz] = n.scale ?? [1, 1, 1];
  return [
    (1 - 2 * (y * y + z * z)) * sx, 2 * (x * y + z * w) * sx, 2 * (x * z - y * w) * sx, 0,
    2 * (x * y - z * w) * sy, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z + x * w) * sy, 0,
    2 * (x * z + y * w) * sz, 2 * (y * z - x * w) * sz, (1 - 2 * (x * x + y * y)) * sz, 0,
    tx, ty, tz, 1,
  ];
}
/** The corners in Lapin's frame (x across, y to the back, z up) and the material slots used. */
function corners(bytes) {
  const { json: j, bin } = glb(bytes);
  const pts = [], slots = new Set();
  const walk = (i, parent) => {
    const n = j.nodes[i], m = mul(parent, matrix(n));
    if (n.mesh != null) for (const p of j.meshes[n.mesh].primitives) {
      const a = j.accessors[p.attributes.POSITION], v = j.bufferViews[a.bufferView];
      const o = (v.byteOffset ?? 0) + (a.byteOffset ?? 0), st = v.byteStride ?? 12;
      slots.add(j.materials?.[p.material]?.name ?? '_defaultMat');
      for (let k = 0; k < a.count; k++) {
        const x = bin.readFloatLE(o + k * st), y = bin.readFloatLE(o + k * st + 4), z = bin.readFloatLE(o + k * st + 8);
        const X = m[0] * x + m[4] * y + m[8] * z + m[12], Y = m[1] * x + m[5] * y + m[9] * z + m[13], Z = m[2] * x + m[6] * y + m[10] * z + m[14];
        pts.push([X, -Z, Y]);
      }
    }
    for (const c of n.children ?? []) walk(c, m);
  };
  for (const r of j.scenes[j.scene ?? 0].nodes) walk(r, [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
  return { pts, slots: [...slots].sort() };
}

/**
 * An axis's stretch zone: the widest band with no corner in it, in the middle 70 % of the extent, as fractions of it
 * (what lies before stays, what lies after moves, the band between stretches); none (the axis scales whole) when the
 * widest such band is under a fifth of the extent — a round top, a plant.
 */
function zone(values, ext) {
  const xs = [...new Set(values.map((v) => Math.round(v * 1e5) / 1e5))].sort((a, b) => a - b);
  let best = null;
  for (let i = 0; i + 1 < xs.length; i++) {
    const [a, b] = [xs[i], xs[i + 1]], mid = (a + b) / 2;
    if (mid < 0.15 * ext || mid > 0.85 * ext) continue;
    if (!best || b - a > best[1] - best[0]) best = [a, b];
  }
  if (!best || best[1] - best[0] < 0.2 * ext) return null;
  // Rounded into the band, so the corners at its ends stay outside it.
  return [Math.ceil((best[0] / ext) * 1e5) / 1e5, Math.floor((best[1] / ext) * 1e5) / 1e5];
}

rmSync(models, { recursive: true, force: true });
rmSync(thumbs, { recursive: true, force: true });
mkdirSync(models, { recursive: true });
mkdirSync(thumbs, { recursive: true });
const today = new Date().toISOString().slice(0, 10);
const list = [];
for (const [id, name, group, predefined, size, opt] of PICK) {
  const srcRel = `Models/GLTF format/${id}.glb`, thumbRel = `Isometric/${id}_SW.png`;
  const bytes = readFileSync(join(kit, srcRel)), png = readFileSync(join(kit, thumbRel));
  copyFileSync(join(kit, srcRel), join(models, `${id}.glb`));
  copyFileSync(join(kit, thumbRel), join(thumbs, `${id}.png`));
  const { pts, slots } = corners(bytes);
  const lo = [0, 1, 2].map((k) => Math.min(...pts.map((p) => p[k]))), hi = [0, 1, 2].map((k) => Math.max(...pts.map((p) => p[k])));
  const ext = hi.map((h, k) => h - lo[k]);
  const zones = {};
  ['x', 'y', 'z'].forEach((axis, k) => {
    // A round top or a lamp's shade keeps its shape across (axes: the only axes that may have a zone).
    const z = opt.uniform || (opt.flat && k === 2) || (opt.axes && !opt.axes.includes(axis)) ? null : zone(pts.map((p) => p[k] - lo[k]), ext[k]);
    if (z) zones[axis] = z;
  });
  // The model at its own proportions is the kit's units × KIT_SCALE metres (a sofa 1.96 m wide); the start size is
  // reached from there by the zones. An axis with a zone shrinks until a tenth of its band is left (its fixed ends
  // never shrink), an axis without one to half the start size.
  const natural = ext.map((e) => Math.round(e * KIT_SCALE * 1e5) / 1e5);
  size.forEach((s, k) => {
    const z = zones['xyz'[k]];
    if (z && s < natural[k] * (1 - (z[1] - z[0]) * 0.9)) throw new Error(`${id}: its start size on ${'xyz'[k]}, ${s} m, is less than its parts that keep their size.`);
  });
  const min = size.map((s, k) => {
    const z = zones['xyz'[k]];
    const v = z ? natural[k] * (1 - (z[1] - z[0]) * 0.9) : s * 0.5;
    return Math.max(0.005, Math.ceil(Math.min(v, s) * 1000) / 1000);
  });
  const max = size.map((s, k) => Math.round(Math.max(s * (k === 0 ? 2.5 : 2), k === 0 && zones.x ? 4 : 0) * 1000) / 1000);
  const roles = Object.fromEntries(slots.map((s) => [s, SLOT_BY_GROUP[group]?.[s] ?? SLOT[s] ?? 'body']));
  list.push({
    id, name, group, predefined, ...(opt.objectType ? { objectType: opt.objectType } : {}),
    size, natural, min, max, zones, slots: roles,
    ...(opt.mount ? { mount: opt.mount } : {}), ...(opt.free ? { against: false } : {}), ...(opt.seats ? { seats: true } : {}), ...(opt.uniform ? { uniform: true } : {}),
    file: `models/${id}.glb`, sha256: sha(bytes), bytes: bytes.length,
    thumbnail: { file: `ui/public/furniture-models/${id}.png`, sha256: sha(png), original: thumbRel },
    source: 'kenney-furniture-kit', original: srcRel, licence: 'CC0-1.0', author: 'Kenney (www.kenney.nl)',
    changed: 'Nothing: the GLB and its preview are the kit\'s files as published; Lapin stretches the model by its zones when it builds a piece.',
  });
}
// Every file shipped is one picked (a model dropped from the list leaves no file behind).
for (const f of readdirSync(models)) if (!list.some((m) => m.file === `models/${f}`)) throw new Error(`stray ${f}`);

const register = {
  about: 'The furniture models Lapin ships (docs/plan-furniture.md, Configuratorul flexibil, stage 2; decision 477), each from a source with a licence verified on its site. Written by scripts/furniture-assets.mjs and checked by pnpm check:licenses: every model and preview listed, its hash as shipped, its licence in the allowed list. Zones are fractions of the model\'s extent in Lapin\'s frame (x across from its left, y from its front to its back, z up); natural is that extent in metres (the kit\'s units × 2), size the size a new type starts with, min and max what a type may take.',
  sources: {
    'kenney-furniture-kit': {
      name: 'Furniture Kit 2.0', url: 'https://kenney.nl/assets/furniture-kit', author: 'Kenney (www.kenney.nl)', licence: 'CC0-1.0',
      licenceUrl: 'http://creativecommons.org/publicdomain/zero/1.0/',
      verified: `${today}: the asset page states "Creative Commons CC0"; the kit's License.txt reads "License: (Creative Commons Zero, CC0)" and "This content is free to use in personal, educational and commercial projects. Support us by crediting Kenney or www.kenney.nl (this is not mandatory)".`,
      fetched: today, archive: 'kenney_furniture-kit.zip', archiveSha256: sha(readFileSync(zip)),
    },
    'poly-haven': {
      name: 'Poly Haven models', url: 'https://polyhaven.com/models', author: 'Poly Haven contributors', licence: 'CC0-1.0', licenceUrl: 'https://polyhaven.com/license',
      verified: `${today}: polyhaven.com/license states "Our assets are all licensed as CC0" and that this covers "All assets (HDRIs, textures and 3D models)"; no credit required. Allowed, none shipped yet: its models are photo-scanned with textures (megabytes each), not the flat-coloured low-poly bodies Lapin's materials colour.`,
    },
  },
  models: list,
};
// Two-space JSON with the arrays of numbers on one line.
const text = JSON.stringify(register, null, 2).replace(/\[\s*(-?[\d.e-]+(?:,\s*-?[\d.e-]+)*)\s*\]/g, (_, xs) => `[${xs.split(/,\s*/).join(', ')}]`);
writeFileSync(join(out, 'furniture-assets.json'), text + '\n');
const total = list.reduce((a, m) => a + m.bytes, 0);
console.log(`${list.length} models, ${(total / 1024).toFixed(0)} KB of GLB, previews in ui/public/furniture-models.`);
for (const m of list) console.log(m.id.padEnd(26), JSON.stringify(m.zones), m.min.join('/'), m.max.join('/'), JSON.stringify(m.slots));
