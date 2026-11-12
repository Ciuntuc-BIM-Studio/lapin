// The user guide's scenes (run.mjs): each drives Lapin as a user does, checks
// what it sees, and takes the pictures its page shows (shots/<name>.webp).
// A scene starts from the start screen; `house` gives it a small model to
// work on, built through the same commands the interface sends.
// A scene drives the interface in English; what a user types (a question)
// is in the run's language (t.lang, one of languages.mjs), from the tables
// below: a language missing from a table types the English.
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { say } from './languages.mjs';

/** Ask Me's question in the help scene: each language's way of asking how to copy a wall. */
const ASK_COPY = {
  en: 'how do I copy a wall',
  ro: 'cum copiez un perete',
  es: 'cómo copio un muro',
  de: 'wie kopiere ich eine Wand',
  fr: 'comment copier un mur',
  da: 'hvordan kopierer jeg en væg',
  it: 'come copio un muro',
  pl: 'jak skopiować ścianę',
  pt: 'como copio uma parede',
};

/** The assistant's scene: the two questions asked, and the answers the stand-in model gives. */
const ASSISTANT = {
  questions: {
    en: ['How many walls per storey, and how long?', 'Give the doors FireRating EI30.'],
    ro: ['Câți pereți sunt pe etaj și ce lungime au?', 'Dă ușilor FireRating EI30.'],
    es: ['¿Cuántos muros hay por planta y cuánto miden?', 'Pon FireRating EI30 a las puertas.'],
    de: ['Wie viele Wände gibt es je Geschoss, und wie lang sind sie?', 'Gib den Türen FireRating EI30.'],
    fr: ['Combien de murs par niveau, et quelle longueur ?', 'Donne aux portes FireRating EI30.'],
    da: ['Hvor mange vægge er der pr. etage, og hvor lange er de?', 'Giv dørene FireRating EI30.'],
    it: ['Quanti muri ci sono per piano, e quanto sono lunghi?', 'Dai alle porte FireRating EI30.'],
    pl: ['Ile ścian jest na każdej kondygnacji i jaka jest ich długość?', 'Nadaj drzwiom FireRating EI30.'],
    pt: ['Quantas paredes há por pavimento, e qual o comprimento?', 'Dê às portas FireRating EI30.'],
  },
  proposed: {
    en: (n) => `I proposed EI30 for ${n} door. Review and apply it below.`,
    ro: (n) => `Am propus EI30 pentru ${n} ușă. Verifică și aplică mai jos.`,
    es: (n) => `He propuesto EI30 para ${n} puerta. Revísalo y aplícalo abajo.`,
    de: (n) => `Ich habe EI30 für ${n} Tür vorgeschlagen. Prüfen und unten anwenden.`,
    fr: (n) => `J’ai proposé EI30 pour ${n} porte. Vérifiez et appliquez ci-dessous.`,
    da: (n) => `Jeg har foreslået EI30 til ${n} dør. Gennemse og anvend nedenfor.`,
    it: (n) => `Ho proposto EI30 per ${n} porta. Controlla e applica qui sotto.`,
    pl: (n) => `Zaproponowałem EI30 dla ${n} drzwi. Sprawdź i zastosuj poniżej.`,
    pt: (n) => `Propus EI30 para ${n} porta. Revise e aplique abaixo.`,
  },
  walls: {
    en: (s, n, m) => `**${s}** has **${n} walls**, **${m} m** in all.`,
    ro: (s, n, m) => `Pe **${s}** sunt **${n} pereți**, în total **${m} m**.`,
    es: (s, n, m) => `En **${s}** hay **${n} muros**, **${m} m** en total.`,
    de: (s, n, m) => `**${s}**: **${n} Wände**, insgesamt **${m} m**.`,
    fr: (s, n, m) => `**${s}** compte **${n} murs**, **${m} m** au total.`,
    da: (s, n, m) => `**${s}** har **${n} vægge**, i alt **${m} m**.`,
    it: (s, n, m) => `**${s}** ha **${n} muri**, **${m} m** in tutto.`,
    pl: (s, n, m) => `**${s}**: **${n} ścian**, łącznie **${m} m**.`,
    pt: (s, n, m) => `**${s}** tem **${n} paredes**, **${m} m** no total.`,
  },
};

/** A small house on the ground floor: 12 × 8 m, two rooms, a door, windows, a floor and a gable roof. */
/**
 * A small company logo for the guide, as a PNG made here (a blue mark and three grey bars, 240 × 80 px): the Style tab's Choose logo… takes it.
 */
async function logoPng() {
  const { deflateSync, crc32 } = await import('node:zlib');
  const w = 240, h = 80;
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    for (let x = 0; x < w; x++) {
      const mark = x >= 6 && x < 74 && y >= 6 && y < 74 && !(x >= 26 && x < 54 && y >= 26 && y < 74);
      const bar = (x >= 90 && x < 232 && y >= 14 && y < 32) || (x >= 90 && x < 200 && y >= 40 && y < 50) || (x >= 90 && x < 170 && y >= 58 && y < 66);
      const [r, g, b, a] = mark ? [31, 95, 158, 255] : bar ? [96, 104, 116, 255] : [255, 255, 255, 0];
      raw.set([r, g, b, a], y * (w * 4 + 1) + 1 + x * 4);
    }
  }
  const chunk = (type, data) => {
    const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
    const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td) >>> 0);
    return Buffer.concat([len, td, crc]);
  };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr.set([8, 6, 0, 0, 0], 8);
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]);
}

async function house(t, { roof = true } = {}) {
  const s = await t.storey();
  const walls = {};
  const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
  for (let i = 0; i < 4; i++) {
    const [a, b] = [outline[i], outline[(i + 1) % 4]];
    walls[i] = (await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] })).id;
  }
  walls.inner = (await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8 })).id;
  await t.cmd('architecture.addDoor', { wallId: walls[0], x: 3, y: 0 });
  await t.cmd('architecture.addWindow', { wallId: walls[0], x: 9.5, y: 0 });
  await t.cmd('architecture.addWindow', { wallId: walls[2], x: 3.5, y: 8 });
  await t.cmd('architecture.addWindow', { wallId: walls[2], x: 9.5, y: 8 });
  await t.cmd('architecture.addWindow', { wallId: walls[1], x: 12, y: 4 });
  await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
  if (roof) {
    const r = await t.cmd('architecture.addRoof', { storeyId: s, points: outline });
    await t.cmd('architecture.roofStyle', { id: r.id, style: 'ro_saxon' });
  }
  await t.wait(800);
  return { storey: s, walls };
}

/**
 * A Finnish-style title block (nimiö) for the title blocks' scene (decision 340, a tester's report #76): 180 × 56 mm in the
 * frame's bottom-right corner, the labels in Finnish, the values live fields of the project and the sheet.
 */
function finnishTitleBlock() {
  const items = [{ type: 'rect', x: 0, y: 0, w: 180, h: 56, stroke: 0.5 }];
  const line = (x0, y0, x1, y1, w = 0.25) => items.push({ type: 'line', a: [x0, y0], b: [x1, y1], w });
  const cell = (x, y, label, value, h = 2.5, bold = false) => {
    items.push({ type: 'text', at: [x + 1.2, y + 6.4], text: label, h: 1.6 });
    items.push({ type: 'text', at: [x + 1.2, y + 1.8], text: value, h, bold });
  };
  for (const y of [46, 38, 28, 18]) line(0, y, 180, y);
  line(120, 38, 120, 56); line(90, 28, 90, 38); line(60, 0, 60, 28); line(120, 0, 120, 28); line(150, 18, 150, 28); line(100, 0, 100, 18);
  cell(0, 46, 'Rakennuskohde', '{project.name}', 3.5, true);
  cell(120, 46, 'Rakennuslupatunnus', '{project.permit}');
  cell(0, 38, 'Rakennuskohteen osoite', '{project.address}');
  cell(120, 38, 'Kiinteistötunnus', '{project.cadastral}');
  cell(0, 28, 'Rakennuttaja', '{project.client}');
  cell(90, 28, 'Piirustuksen sisältö', '{sheet.name}', 3, true);
  cell(0, 18, 'Pääsuunnittelija', '{project.chiefArchitect}');
  cell(60, 18, 'Suunnittelija', '{sheet.drawnBy}');
  cell(120, 18, 'Suunnitteluala', '{project.suunnitteluala}');
  cell(150, 18, 'Mittakaava', '{sheet.scale}');
  items.push({ type: 'rect', x: 0, y: 0, w: 60, h: 18, stroke: 0, fill: '#eef2f5' });
  items.push({ type: 'text', at: [3, 10], text: '{project.designer}', h: 3, bold: true });
  items.push({ type: 'text', at: [3, 5.5], text: '{project.designerAddress}', h: 1.8 });
  items.push({ type: 'text', at: [3, 2], text: '{project.designerEmail}', h: 1.8 });
  line(0, 18, 60, 18);
  cell(60, 9, 'Päiväys', '{sheet.date}');
  cell(60, 0, 'Muutos', '{sheet.revision}');
  line(60, 9, 120, 9);
  cell(100, 9, 'Työnumero', '{project.number}');
  items.push({ type: 'text', at: [121.2, 14.4], text: 'Piirustusnumero', h: 1.6 });
  items.push({ type: 'text', at: [150, 6], text: '{sheet.number}', h: 6, bold: true, align: 'middle' });
  return { w: 180, h: 56, anchor: 'bottomRight', frame: { show: true, left: 20, top: 10, right: 10, bottom: 10 }, revisions: 'above', items };
}

/** Opens a view, a sheet or a level's plan from the project browser, by its name there. */
async function openFromBrowser(t, name, wait = 2000) {
  await t.page.locator('nav.browser').getByText(name, { exact: true }).first().dblclick({ timeout: 5000 });
  await t.wait(wait);
}

/** The 3D view, from its tab in the project browser. */
async function open3d(t) {
  await openFromBrowser(t, '3D', 0);
  await t.page.waitForSelector('canvas', { timeout: 20_000 });
  await t.wait(2500);
}

/** Selects an element, as a click on it does. */
const select = (t, id) => t.ui({ selection: id, selected: [id] });

/**
 * A stand-in for Ollama on this computer, for the assistant's scene: its model list, and a chat that
 * answers as a model does — a tool call for a question, words for a tool's result (from the result itself).
 */
async function fakeOllama(page, lang) {
  const line = (message) => JSON.stringify({ message: { role: 'assistant', ...message }, done: false }) + '\n' + JSON.stringify({ done: true, eval_count: 40, eval_duration: 7e8 }) + '\n';
  const call = (name, args) => line({ content: '', tool_calls: [{ function: { name, arguments: args } }] });
  await page.route('http://127.0.0.1:11434/**', async (route) => {
    const url = route.request().url();
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': '*' }, body: typeof body === 'string' ? body : JSON.stringify(body) });
    if (url.endsWith('/api/tags')) return json({ models: [{ name: 'qwen3-coder:30b' }, { name: 'gemma4:12b' }] });
    if (url.endsWith('/api/show')) return json({ capabilities: ['completion', 'tools'] });
    const { messages } = JSON.parse(route.request().postData() ?? '{}');
    const last = messages.at(-1);
    if (last.role === 'user') {
      return json(/fire/i.test(last.content)
        ? call('list_elements', { class: 'IfcDoor' })
        : call('aggregate', { class: 'IfcWall', group_by: ['storey'], metrics: [{ op: 'count' }, { op: 'sum', field: 'Length' }] }));
    }
    const result = JSON.parse(last.content);
    if (last.tool_name === 'list_elements') return json(call('propose_properties', { changes: result.rows.map((r) => ({ id: r.id, pset: 'Pset_DoorCommon', name: 'FireRating', value: 'EI30' })) }));
    if (last.tool_name === 'propose_properties') return json(line({ content: say(ASSISTANT.proposed, lang)(result.proposed) }));
    const g = result.groups[0];
    return json(line({ content: say(ASSISTANT.walls, lang)(g.storey, g.count, g['sum(Length)']) }));
  });
}

/** A brick texture, drawn on a canvas in the page: a user's own image to put on a material. */
const brickPng = (t) => t.page.evaluate(() => {
  const c = document.createElement('canvas'); c.width = 512; c.height = 512;
  const g = c.getContext('2d');
  g.fillStyle = '#d9d2c5'; g.fillRect(0, 0, 512, 512);
  for (let row = 0; row < 8; row++) for (let col = -1; col < 4; col++) {
    const x = col * 128 + (row % 2 ? 64 : 0) + 4, y = row * 64 + 4;
    const tone = 150 + ((row * 7 + col * 13) % 5) * 9;
    g.fillStyle = `rgb(${tone + 30}, ${tone - 60}, ${tone - 90})`; g.fillRect(x, y, 120, 56);
  }
  return c.toDataURL('image/png').split(',')[1];
});

/** A hatch pattern file, as a user brings one from AutoCAD. */
const PAT = `*LAPIN_HERRINGBONE, Herringbone 45°
45, 0,0, 0,0.2828, 0.2,-0.2
135, 0.1414,0.1414, 0,0.2828, 0.2,-0.2
`;

/** A small survey in Stereo 70 for the site scenes: a DXF of points on a gentle slope, a parcel as GeoJSON, a neighbour as CityJSON, an aerial photo drawn on a canvas. */
const E0 = 587900, N0 = 325820;
/** A site drawing as a CAD program writes one (mm): the parcel, the setbacks, the axes and a paved terrace, each on its layer and colour. */
function siteDxf() {
  const layer = (name, aci) => `0\nLAYER\n2\n${name}\n70\n0\n62\n${aci}\n`;
  const tables = `0\nTABLE\n2\nLAYER\n${layer('PARCEL', 1)}${layer('SETBACK', 3)}${layer('AXES', 5)}${layer('TERRACE', 8)}0\nENDTAB\n`;
  const poly = (l, pts) => `0\nLWPOLYLINE\n8\n${l}\n90\n${pts.length}\n70\n1\n${pts.map(([x, y]) => `10\n${x}\n20\n${y}\n`).join('')}`;
  const line = (l, a, b) => `0\nLINE\n8\n${l}\n10\n${a[0]}\n20\n${a[1]}\n11\n${b[0]}\n21\n${b[1]}\n`;
  let body = poly('PARCEL', [[-6000, -7000], [20000, -6000], [21000, 15000], [-5000, 14000]]);
  body += poly('SETBACK', [[-3000, -4000], [17000, -3500], [17800, 11500], [-2500, 11000]]);
  for (let x = 0; x <= 12000; x += 3500) body += line('AXES', [x, -5000], [x, 13000]);
  for (let y = 0; y <= 8000; y += 4000) body += line('AXES', [-4000, y], [16000, y]);
  body += `0\nSOLID\n8\nTERRACE\n10\n1000\n20\n-3000\n11\n6500\n21\n-3000\n12\n1000\n22\n-200\n13\n6500\n23\n-200\n`;
  return `0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nTABLES\n${tables}0\nENDSEC\n0\nSECTION\n2\nENTITIES\n${body}0\nENDSEC\n0\nEOF\n`;
}
/**
 * A scan of a house standing on the site (docs/point-clouds.md), as a terrestrial scanner would give it: LAS 1.4, point
 * format 7 (colour), in metres, its points classed ground (2), building (6) and vegetation (5): the ground round it, its
 * four walls with their openings to the eaves at 2.9 m, two gables, a tree. Written here, a few hundred thousand points.
 */
function scanLas() {
  const pts = [];
  let seed = 7;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
  const add = (x, y, z, r, g, b, c) => pts.push([x, y, z, r, g, b, c]);
  // Ground: grass, a gravel path to the door.
  for (let i = 0; i < 360000; i++) {
    const x = -8 + rnd() * 28, y = -9 + rnd() * 25, z = 0.02 * Math.sin(x / 3) + 0.015 * Math.cos(y / 4) - 0.02;
    if (x > 0 && x < 12 && y > 0 && y < 8) continue;
    const path = x > 2.3 && x < 3.7 && y < 0;
    const k = 0.85 + rnd() * 0.3;
    if (path) add(x, y, z, 170 * k, 160 * k, 140 * k, 2); else add(x, y, z, 90 * k, 130 * k, 60 * k, 2);
  }
  // The walls' outer faces, rendered, a plinth; openings left empty.
  const openings = [[0, 2.55, 3.45, 0, 2.1], [0, 8.9, 10.1, 0.9, 2.1], [2, 2.9, 4.1, 0.9, 2.1], [2, 8.9, 10.1, 0.9, 2.1], [1, 3.4, 4.6, 0.9, 2.1]];
  const faces = [[0, 0, 12, 0, 0, -1], [12, 0, 12, 8, 1, 0], [12, 8, 0, 8, 0, 1], [0, 8, 0, 0, -1, 0]];
  faces.forEach(([ax, ay, bx, by], f) => {
    const len = Math.hypot(bx - ax, by - ay);
    for (let i = 0; i < 26000 * len / 10; i++) {
      const u = rnd() * len, z = rnd() * 2.9;
      if (openings.some(([w, s0, s1, z0, z1]) => w === f && u > s0 && u < s1 && z > z0 && z < z1)) continue;
      const x = ax + (bx - ax) * u / len, y = ay + (by - ay) * u / len;
      const k = 0.9 + rnd() * 0.15;
      if (z < 0.4) add(x, y, z, 120 * k, 115 * k, 110 * k, 6); else add(x, y, z, 236 * k, 224 * k, 200 * k, 6);
    }
  });
  // Gables on the short walls, up to the ridge at 5.4 m.
  for (const x of [0, 12]) for (let i = 0; i < 18000; i++) {
    const y = rnd() * 8, z = 2.9 + rnd() * 2.5;
    if (z - 2.9 > 2.5 * (1 - Math.abs(y - 4) / 4)) continue;
    add(x, y, z, 230, 218, 196, 6);
  }
  // A tree: a trunk and a crown.
  for (let i = 0; i < 30000; i++) {
    const a = rnd() * Math.PI * 2, e = Math.acos(2 * rnd() - 1), r = 2.2 * Math.cbrt(rnd());
    const trunk = i < 3000;
    const [x, y, z] = trunk ? [16 + 0.18 * Math.cos(a), 12 + 0.18 * Math.sin(a), rnd() * 2.6] : [16 + r * Math.sin(e) * Math.cos(a), 12 + r * Math.sin(e) * Math.sin(a), 4.2 + r * Math.cos(e)];
    if (trunk) add(x, y, z, 95, 70, 50, 5); else add(x, y, z, 50 + rnd() * 40, 110 + rnd() * 50, 40, 5);
  }
  const n = pts.length, len = 36, head = 375;
  const buf = Buffer.alloc(head + n * len);
  buf.write('LASF', 0, 'ascii');
  buf[24] = 1; buf[25] = 4;
  buf.writeUInt16LE(head, 94); buf.writeUInt32LE(head, 96); buf[104] = 7; buf.writeUInt16LE(len, 105);
  for (let k = 0; k < 3; k++) { buf.writeDoubleLE(0.001, 131 + 8 * k); buf.writeDoubleLE(0, 155 + 8 * k); }
  const ext = [0, 1, 2].map((k) => pts.reduce(([lo, hi], p) => [Math.min(lo, p[k]), Math.max(hi, p[k])], [Infinity, -Infinity]));
  buf.writeDoubleLE(ext[0][1], 179); buf.writeDoubleLE(ext[0][0], 187); buf.writeDoubleLE(ext[1][1], 195); buf.writeDoubleLE(ext[1][0], 203); buf.writeDoubleLE(ext[2][1], 211); buf.writeDoubleLE(ext[2][0], 219);
  buf.writeBigUInt64LE(BigInt(n), 247);
  pts.forEach(([x, y, z, r, g, b, c], i) => {
    const o = head + i * len;
    buf.writeInt32LE(Math.round(x * 1000), o); buf.writeInt32LE(Math.round(y * 1000), o + 4); buf.writeInt32LE(Math.round(z * 1000), o + 8);
    buf.writeUInt16LE(Math.round(20000 + 30000 * (z / 6)), o + 12);
    buf[14 + o] = 0x11; buf[16 + o] = c;
    buf.writeUInt16LE(Math.min(255, Math.round(r)) * 257, o + 30); buf.writeUInt16LE(Math.min(255, Math.round(g)) * 257, o + 32); buf.writeUInt16LE(Math.min(255, Math.round(b)) * 257, o + 34);
  });
  return buf;
}
function surveyDxf() {
  let body = '';
  for (let i = 0; i <= 8; i++) for (let j = 0; j <= 8; j++) {
    const z = 80 + i * 0.25 + Math.sin(j / 2) * 0.4;
    body += `0\nPOINT\n8\nPUNCTE\n10\n${E0 - 10 + i * 6}\n20\n${N0 - 10 + j * 6}\n30\n${z.toFixed(2)}\n`;
  }
  return `0\nSECTION\n2\nENTITIES\n${body}0\nENDSEC\n0\nEOF\n`;
}
const parcelGeoJson = () => JSON.stringify({
  type: 'FeatureCollection', crs: { type: 'name', properties: { name: 'urn:ogc:def:crs:EPSG::3844' } },
  features: [{ type: 'Feature', properties: { nr_cad: '50731' }, geometry: { type: 'Polygon', coordinates: [[[E0 - 4, N0 - 6], [E0 + 26, N0 - 4], [E0 + 28, N0 + 30], [E0 - 2, N0 + 32], [E0 - 4, N0 - 6]]] } }],
});
const neighbourCityJson = () => JSON.stringify({
  type: 'CityJSON', version: '2.0', metadata: { referenceSystem: 'https://www.opengis.net/def/crs/EPSG/0/3844' },
  transform: { scale: [0.01, 0.01, 0.01], translate: [E0 + 28, N0 + 15, 80] },
  vertices: [[0, 0, 0], [1000, 0, 0], [1000, 1400, 0], [0, 1400, 0], [0, 0, 900], [1000, 0, 900], [1000, 1400, 900], [0, 1400, 900]],
  CityObjects: { n1: { type: 'Building', attributes: { name: 'Neighbour' }, geometry: [{ type: 'Solid', lod: '2', boundaries: [[[[0, 3, 2, 1]], [[4, 5, 6, 7]], [[0, 1, 5, 4]], [[1, 2, 6, 5]], [[2, 3, 7, 6]], [[3, 0, 4, 7]]]] }] } },
});
/** An aerial photo, as an orthophoto looks: fields, a road, roofs (drawn in the page, 600 × 600 px at 0.1 m). */
const aerialJpeg = (t) => t.page.evaluate(() => {
  const c = document.createElement('canvas'); c.width = 600; c.height = 600;
  const g = c.getContext('2d');
  g.fillStyle = '#8fa66a'; g.fillRect(0, 0, 600, 600);
  for (let i = 0; i < 2000; i++) { g.fillStyle = `rgba(${90 + (i * 37) % 60}, ${120 + (i * 53) % 50}, ${60 + (i * 17) % 40}, 0.35)`; g.fillRect((i * 97) % 600, (i * 151) % 600, 6, 6); }
  g.fillStyle = '#b7b1a3'; g.fillRect(0, 470, 600, 70);
  g.fillStyle = '#d9d2c2'; g.fillRect(0, 500, 600, 6);
  g.fillStyle = '#9c5b45'; g.fillRect(430, 160, 100, 140);
  g.fillStyle = '#7f4a39'; g.fillRect(430, 160, 100, 70);
  g.fillStyle = '#6f8f55'; for (const [x, y] of [[80, 90], [140, 60], [330, 400], [60, 380]]) { g.beginPath(); g.arc(x, y, 26, 0, 7); g.fill(); }
  return c.toDataURL('image/jpeg', 0.85).split(',')[1];
});

/** A small foreign IFC that carries 2D: a cabinet (a box) with its footprint, and a chair drawn only in 2D in a plan context. */
/**
 * A foreign IFC with typed walls, as another program writes it (no Lapin intent): four outer walls of type EXT-30,
 * 300 thick, round an 8 × 6 m outline, and a partition of type INT-12, 125 thick, across it.
 */
function ifcTypedWalls() {
  const lines = [];
  let n = 100;
  const add = (text) => { lines.push(`#${n}=${text};`); return `#${n++}`; };
  const gid = (key, i) => `0Guide${key}${String(i).padStart(16 - key.length, '0')}`;
  const walls = { 'EXT-30': [], 'INT-12': [] };
  const at = add('IFCLOCALPLACEMENT(#13,#2)');
  [[4, 0, 8, 0.3, 'EXT-30'], [8, 3, 0.3, 6, 'EXT-30'], [4, 6, 8, 0.3, 'EXT-30'], [0, 3, 0.3, 6, 'EXT-30'], [4, 3, 0.125, 6, 'INT-12']].forEach(([x, y, dx, dy, type], i) => {
    const centre = add(`IFCAXIS2PLACEMENT2D(${add(`IFCCARTESIANPOINT((${x.toFixed(3)},${y.toFixed(3)}))`)},$)`);
    const solid = add(`IFCEXTRUDEDAREASOLID(${add(`IFCRECTANGLEPROFILEDEF(.AREA.,$,${centre},${dx.toFixed(3)},${dy.toFixed(3)})`)},#2,#23,2.8)`);
    const shape = add(`IFCPRODUCTDEFINITIONSHAPE($,$,(${add(`IFCSHAPEREPRESENTATION(#3,'Body','SweptSolid',(${solid}))`)}))`);
    walls[type].push(add(`IFCWALL('${gid('Wall', i + 1)}',$,'Wall ${i + 1}',$,$,${at},${shape},$,.STANDARD.)`));
  });
  Object.entries(walls).forEach(([name, ids], i) => {
    const type = add(`IFCWALLTYPE('${gid('WTyp', i + 1)}',$,'${name}',$,$,$,$,$,$,.STANDARD.)`);
    add(`IFCRELDEFINESBYTYPE('${gid('DTyp', i + 1)}',$,$,$,(${ids.join(',')}),${type})`);
  });
  add(`IFCRELCONTAINEDINSPATIALSTRUCTURE('${gid('Cont', 1)}',$,$,$,(${[...walls['EXT-30'], ...walls['INT-12']].join(',')}),#14)`);
  return `ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView_V1.2]'),'2;1');
FILE_NAME('typed-walls.ifc','2026-11-01T12:00:00',(''),(''),'guide','guide','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCCARTESIANPOINT((0.,0.,0.));
#2=IFCAXIS2PLACEMENT3D(#1,$,$);
#3=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#2,$);
#6=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#7=IFCUNITASSIGNMENT((#6));
#8=IFCPROJECT('0GuideProj000000000002',$,'Typed walls',$,$,$,$,(#3),#7);
#9=IFCLOCALPLACEMENT($,#2);
#10=IFCSITE('0GuideSite000000000002',$,'Site',$,$,#9,$,$,.ELEMENT.,$,$,$,$,$);
#11=IFCLOCALPLACEMENT(#9,#2);
#12=IFCBUILDING('0GuideBldg000000000002',$,'Building',$,$,#11,$,$,.ELEMENT.,$,$,$);
#13=IFCLOCALPLACEMENT(#11,#2);
#14=IFCBUILDINGSTOREY('0GuideStry000000000002',$,'Ground',$,$,#13,$,$,.ELEMENT.,0.);
#15=IFCRELAGGREGATES('0GuideAgg1000000000002',$,$,$,#8,(#10));
#16=IFCRELAGGREGATES('0GuideAgg2000000000002',$,$,$,#10,(#12));
#17=IFCRELAGGREGATES('0GuideAgg3000000000002',$,$,$,#12,(#14));
#23=IFCDIRECTION((0.,0.,1.));
${lines.join('\n')}
ENDSEC;
END-ISO-10303-21;
`;
}

const IFC_2D = `ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView_V1.2]'),'2;1');
FILE_NAME('2d.ifc','2026-10-30T12:00:00',(''),(''),'guide','guide','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCCARTESIANPOINT((0.,0.,0.));
#2=IFCAXIS2PLACEMENT3D(#1,$,$);
#3=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#2,$);
#4=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Plan',2,1.E-05,#2,$);
#5=IFCGEOMETRICREPRESENTATIONSUBCONTEXT('Annotation','Plan',*,*,*,*,#4,$,.PLAN_VIEW.,$);
#6=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#7=IFCUNITASSIGNMENT((#6));
#8=IFCPROJECT('0GuideProj000000000001',$,'2D',$,$,$,$,(#3,#4),#7);
#9=IFCLOCALPLACEMENT($,#2);
#10=IFCSITE('0GuideSite000000000001',$,'Site',$,$,#9,$,$,.ELEMENT.,$,$,$,$,$);
#11=IFCLOCALPLACEMENT(#9,#2);
#12=IFCBUILDING('0GuideBldg000000000001',$,'Building',$,$,#11,$,$,.ELEMENT.,$,$,$);
#13=IFCLOCALPLACEMENT(#11,#2);
#14=IFCBUILDINGSTOREY('0GuideStry000000000001',$,'Ground',$,$,#13,$,$,.ELEMENT.,0.);
#15=IFCRELAGGREGATES('0GuideAgg1000000000001',$,$,$,#8,(#10));
#16=IFCRELAGGREGATES('0GuideAgg2000000000001',$,$,$,#10,(#12));
#17=IFCRELAGGREGATES('0GuideAgg3000000000001',$,$,$,#12,(#14));
#20=IFCCARTESIANPOINT((0.3,0.2));
#21=IFCAXIS2PLACEMENT2D(#20,$);
#22=IFCRECTANGLEPROFILEDEF(.AREA.,$,#21,0.6,0.4);
#23=IFCDIRECTION((0.,0.,1.));
#24=IFCEXTRUDEDAREASOLID(#22,#2,#23,0.9);
#25=IFCSHAPEREPRESENTATION(#3,'Body','SweptSolid',(#24));
#26=IFCPOLYLINE((#27,#28,#29,#30,#27));
#27=IFCCARTESIANPOINT((0.,0.));
#28=IFCCARTESIANPOINT((0.6,0.));
#29=IFCCARTESIANPOINT((0.6,0.4));
#30=IFCCARTESIANPOINT((0.,0.4));
#31=IFCSHAPEREPRESENTATION(#3,'FootPrint','Curve2D',(#26));
#32=IFCPRODUCTDEFINITIONSHAPE($,$,(#25,#31));
#35=IFCCARTESIANPOINT((2.,1.,0.));
#36=IFCAXIS2PLACEMENT3D(#35,$,$);
#33=IFCLOCALPLACEMENT(#13,#36);
#34=IFCFURNITURE('0GuideCabinet000000001',$,'Cabinet',$,$,#33,#32,$,$);
#40=IFCPOLYLINE((#41,#42,#43,#44,#41));
#41=IFCCARTESIANPOINT((0.,0.));
#42=IFCCARTESIANPOINT((0.5,0.));
#43=IFCCARTESIANPOINT((0.5,0.5));
#44=IFCCARTESIANPOINT((0.,0.5));
#45=IFCSHAPEREPRESENTATION(#5,'Annotation','Annotation2D',(#40));
#46=IFCPRODUCTDEFINITIONSHAPE($,$,(#45));
#47=IFCLOCALPLACEMENT(#13,#2);
#48=IFCFURNITURE('0GuideChair00000000001',$,'Chair',$,$,#47,#46,$,$);
#50=IFCRELCONTAINEDINSPATIALSTRUCTURE('0GuideCont000000000001',$,$,$,(#34,#48),#14);
ENDSEC;
END-ISO-10303-21;
`;

/** Whether no two controls of the schedule's toolbar overlap (report #65: words ran under the next button). */
async function toolbarClear(t) {
  return t.page.evaluate(() => {
    const r = [...document.querySelectorAll('.schedule-pane .view-toolbar > *')].map((e) => e.getBoundingClientRect()).filter((b) => b.width > 0);
    return r.every((a, i) => r.every((b, j) => j <= i || a.right <= b.left + 0.5 || b.right <= a.left + 0.5 || a.bottom <= b.top + 0.5 || b.bottom <= a.top + 0.5));
  });
}

export const scenes = [
  {
    id: 'start',
    async run(t) {
      await t.start();
      await t.shot('start-screen', { clip: { x: 0, y: 0, width: 1440, height: 640 } });
      // A template (decision 261): the apartment block starts with its six levels, in the Architecture workspace.
      await t.page.locator('[data-template=block]').click();
      await t.wait(800);
      const levels = (await t.nodes()).filter((n) => n.type === 'IfcBuildingStorey');
      t.expect(levels.length === 6, 'the apartment block starts with six levels');
      t.expect(await t.page.evaluate(async () => (await import('/src/app/interface.ts')).useInterface.getState().workspace) === 'architecture', 'a template chooses its workspace');
      await t.page.evaluate(async () => (await import('/src/app/interface.ts')).chooseWorkspace('all'));
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      await t.callouts([
        { of: '.tabs-row .tab >> nth=-1', n: 1, at: 'after' },
        { of: '.band', n: 2, at: 'bottom' },
        { of: 'nav.browser', n: 3, at: 'right' },
        { of: '[data-testid=plan]', n: 4, at: 'center' },
        { of: '.hotbar', n: 5, at: 'after' },
        { of: '[data-testid=command-line]', n: 6, at: 'center' },
        { of: 'aside.props', n: 7, at: 'right' },
        { of: '.ask-me', n: 8, at: 'before' },
      ]);
      await t.shot('workspace');
    },
  },
  {
    id: 'walls',
    async run(t) {
      await t.fresh();
      await t.button('Architecture', { exact: true });
      await t.shot('ribbon-architecture', { of: '.topbar', pad: 0 });
      // Drawn as a user draws: WA, a click at each corner, the next one following the mouse.
      await t.keys('w', 'a');
      for (const [x, y] of [[-1, -1], [8, -1], [8, 5]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(250); }
      const [mx, my] = await t.planPoint(2, 5);
      await t.page.mouse.move(mx, my, { steps: 12 });
      await t.wait(300);
      await t.shot('wall-drawing');
      // The line it is drawn on (decision 237): offered in the command line's bar while the tool is on.
      t.expect(await t.page.locator('[data-testid=wall-line]').count() > 0, 'the Wall tool offers the line it is drawn on');
      await t.keys('Escape', 'Escape');
      const walls = (await t.nodes()).filter((n) => n.type === 'IfcWall');
      t.expect(walls.length === 2, `two walls drawn, found ${walls.length}`);
      // A selected wall: its grips, and the properties panel.
      await select(t, walls[0].id);
      // Its drawn direction and faces (R, L), the marks the Wall types legend uses (report #79).
      t.expect(await t.page.locator('[data-testid=wall-faces]').count() >= 1, 'the selected wall shows its faces');
      await t.shot('wall-selected', { clip: { x: 250, y: 120, width: 1190, height: 700 } });
      // Each wall's own length and thickness (report #107).
      t.expect(await t.page.locator('[data-field=wall-length]').count() === 1 && await t.page.locator('[data-field=wall-thickness]').count() === 1, 'the wall\'s length and thickness are fields');
      await t.shot('wall-properties', { of: 'aside.props', pad: 0, maxHeight: 620 });
      await t.tool('architecture.wallTypes');
      t.expect(await t.page.locator('.dialog [data-testid=wall-legend]').count() === 1, 'the wall types show their legend');
      await t.shot('wall-types', { of: '.dialog', pad: 0 });
      // A layer's material hatches at hand (forum #30, decision 493): its swatch opens the material's 2D graphics, the same command as Materials.
      const swatch = t.page.locator('.dialog [data-testid=layer-hatch]').first();
      t.expect(await swatch.count() === 1 && await swatch.isEnabled(), 'each layer shows its material\'s hatch swatch');
      await swatch.click();
      await t.wait(500);
      const editor = t.page.locator('[data-testid=material-hatch-editor]');
      t.expect(await editor.locator('tbody tr').count() === 3, 'the swatch opens the material\'s cut and elevation hatches');
      const layerMaterial = await t.page.locator('.dialog .layer-material select').first().inputValue();
      await editor.locator('[data-field=material-hatch-section]').selectOption('CROSS');
      await t.wait(800);
      const own = await t.page.evaluate(async (name) => (await (await import('/src/engine/session.ts')).query('material.list')).find((m) => m.name === name)?.own?.section?.hatch, layerMaterial);
      t.expect(own === 'CROSS', `the hatch picked there is the material's own, found ${own}`);
      const [a, b] = [await swatch.boundingBox(), await editor.boundingBox()];
      const x0 = Math.max(0, Math.min(a.x, b.x) - 12), y0 = Math.max(0, Math.min(a.y, b.y) - 12);
      await t.shot('layer-hatch', { clip: { x: x0, y: y0, width: Math.max(a.x + a.width, b.x + b.width) + 12 - x0, height: Math.max(a.y + a.height, b.y + b.height) + 12 - y0 } });
      await t.keys('Escape');
      await t.wait(300);
      t.expect(await editor.count() === 0 && await t.page.locator('.dialog [data-testid=wall-legend]').count() === 1, 'Esc closes the hatches, not Wall types');
    },
  },
  {
    // A wall's layer through the floor's layers by priority (the forum's topic #30): the brick core ticked ↓ in Wall types goes
    // through the floor finish and the screed onto the concrete; the section shows it, the floor loses what the wall takes.
    id: 'wall-into-floor',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.cmd('architecture.addSlab', { storeyId: s, points: [[-2, -2], [4, -2], [4, 3], [-2, 3]] });
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Plaster', thickness: 0.015 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Plaster', thickness: 0.015 }] })).id;
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, typeId: type, x1: -1, y1: 0, x2: 3, y2: 0, height: 1.2 })).id;
      const core = async () => (await t.call('quantities.of', { id: wall })).find((q) => q.part === 'layer:1')?.quantities.find((x) => x.name === 'NetVolume')?.value ?? 0;
      const before = await core();
      t.expect(Math.abs(before - 4 * 0.25 * 1.2) < 1e-6, `the brick stands on the floor's top at first (${before.toFixed(4)} m³)`);
      // Ticked in Wall types, on the core's row, as a user does.
      await t.ui({ wallTypeId: type });
      await t.tool('architecture.wallTypes');
      await t.wait(600);
      const down = t.page.locator('.dialog [data-testid=layer-extend-down]');
      t.expect(await down.count() === 3, `every layer offers to go down into the floor (${await down.count()})`);
      await down.nth(1).check();
      await t.page.locator('.dialog').getByRole('button', { name: 'Save type' }).click();
      await t.wait(1200);
      const after = await core();
      t.expect(Math.abs(after - 4 * 0.25 * 1.27) < 1e-6, `the brick goes down through the finish and the screed onto the concrete (${after.toFixed(4)} m³)`);
      await t.ui({ dialog: null });
      await t.cmd('view.create', { kind: 'section', name: 'W-W', start: [1, -0.6], end: [1, 0.8], depth: 1, scale: 10 });
      await openFromBrowser(t, 'W-W', 1500);
      await t.ui({ keyPlan: false });
      await t.fit();
      t.expect(await t.page.locator('[data-testid=drawing] .pen-hatch').count() > 0, 'the section hatches the brick and the floor');
      await t.wait(800);
      await t.shot('wall-into-floor-section', { of: '[data-testid=drawing]', pad: 0 });
      await t.ui({ keyPlan: true });
    },
  },
  {
    // The partial structure display (decision 500, as ArchiCAD's; the forum's topic #30): a new project's plans without the
    // finishes, its sections the entire model, set by kind of view in Manage → View styles and per view from its corner.
    id: 'finishes-by-view',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      const s = await t.storey();
      const plan = await t.call('doc.query', { name: 'view.drawing', args: { storeyId: s } });
      t.expect(plan.structure === 'noFinishes', `a new project's plan leaves out the finishes (${plan.structure})`);
      t.expect(JSON.stringify(plan.hiddenSubs) === '["finishes"]', `the plan's snaps leave out the finish layers (${JSON.stringify(plan.hiddenSubs)})`);
      await t.ui({ dialog: 'viewStyles' });
      await t.wait(800);
      const row = t.page.locator('.dialog table.view-styles tbody tr').first();
      t.expect(await row.locator('[data-testid=style-structure-plan]').inputValue() === 'noFinishes', 'the Architectural style\'s plans: without finishes');
      t.expect(await row.locator('[data-testid=style-structure-section]').inputValue() === 'all', 'its sections: the entire model');
      await t.shot('view-styles-structure', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // From the plan's corner, beside its scale: the cores only, one undo step; the level's plan becomes its plan view.
      await t.ui({ keyPlan: false });
      await t.fit();
      const picker = t.page.locator('[data-testid=view-structure]').first();
      await t.wait(600);
      t.expect(await picker.inputValue() === 'noFinishes', 'the plan\'s corner shows its partial structure display');
      await picker.selectOption('core');
      await t.wait(1200);
      const planView = (await t.nodes()).find((n) => n.type === 'LapinView' && n.props.kind === 'plan' && n.props.storeyId === s && !n.props.site && !n.props.foundation);
      t.expect(planView, 'the level has its plan view');
      const cores = await t.call('doc.query', { name: 'view.drawing', args: { id: planView.id } });
      t.expect(cores.structure === 'core' && JSON.stringify(cores.hiddenSubs) === '["finishes","insulation","membranes"]', `the level's plan draws the cores only (${cores.structure})`);
      await t.shot('structure-picker', { of: '.view-toolbar', pad: 0 });
      await t.shot('structure-plan-core', { of: '[data-testid=plan]', pad: 0 });
      await t.page.evaluate(async () => (await import('/src/engine/session.ts')).undo());
      await t.wait(900);
      // A section: the entire model, the walls' plaster and the floor's finish drawn.
      const section = (await t.cmd('view.create', { kind: 'section', name: 'F-F', start: [5, -0.6], end: [5, 0.8], depth: 1, scale: 10 })).id;
      const drawn = await t.call('doc.query', { name: 'view.drawing', args: { id: section } });
      t.expect(drawn.structure === 'all' && drawn.hiddenSubs.length === 0, `the section draws the entire model (${drawn.structure})`);
      await openFromBrowser(t, 'F-F', 1500);
      await t.fit();
      await t.wait(800);
      await t.shot('finishes-section', { of: '[data-testid=drawing]', pad: 0 });
      // Decision 502: a room's finishes and its skirting go with the finish layers; a partition's door goes with the partition.
      const room = (await t.cmd('architecture.addRoom', { storeyId: s, x: 3, y: 4 })).id;
      const finish = (await t.cmd('architecture.roomFinishTypeFromLibrary', { name: 'Living room' })).id;
      await t.cmd('architecture.applyRoomFinish', { roomIds: [room], typeId: finish });
      const skirting = (await t.cmd('architecture.addSkirting', { storeyId: s, roomId: room })).id;
      const across = (await t.cmd('view.create', { kind: 'section', name: 'G-G', start: [3, -1], end: [3, 9], depth: 1 })).id;
      const owners = async () => new Set((await t.call('doc.query', { name: 'view.drawing', args: { id: across } })).marks.map((m) => m.id.split(/[#:]/)[0]));
      const finishes = (await t.nodes()).filter((n) => n.type === 'LapinRoomFinish' && n.props.roomId === room).map((n) => n.id);
      t.expect(finishes.length > 0, 'the room has its finishes');
      const whole = await owners();
      t.expect(whole.has(skirting) && finishes.some((id) => whole.has(id)), 'the entire model draws the room\'s finishes and its skirting');
      await t.cmd('view.setStyle', { id: across, layers: 'noFinishes' });
      const bare = await owners();
      t.expect(!bare.has(skirting) && !finishes.some((id) => bare.has(id)), 'without finishes, the room\'s finishes and its skirting are left out');
      const door = (await t.cmd('architecture.addDoor', { wallId: walls.inner, x: 7, y: 6 })).id;
      await t.cmd('element.setParam', { ids: [walls.inner], pset: 'Pset_WallCommon', name: 'LoadBearing', value: false });
      const plan2 = await t.cmd('view.create', { kind: 'plan', storeyId: s, name: 'Load-bearing cores', layers: 'coreLoadBearing' });
      const cores2 = new Set((await t.call('doc.query', { name: 'view.drawing', args: { id: plan2.id } })).marks.map((m) => m.id.split(/[#:]/)[0]));
      t.expect(!cores2.has(walls.inner) && (!door || !cores2.has(door)), 'the partition and its door are left out of the load-bearing cores');
      t.expect(cores2.has(walls[0]), 'a wall that says nothing of its load is drawn');
      await t.ui({ keyPlan: true });
    },
  },
  {
    // Placing a door or window as Revit does (a tester's wish): the cursor's side of the wall is the side it opens to, Space its other hand, shown before the click.
    id: 'fill-placing',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      for (const [a, b] of [[[0, 0], [8, 0]], [[8, 0], [8, 5]], [[8, 5], [0, 5]], [[0, 5], [0, 0]]]) await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] });
      await t.ui({ keyPlan: false });
      await t.wait(600);
      await t.fit();
      await t.tool('architecture.door');
      await t.wait(300);
      const place = async (x, y) => {
        const [px, py] = await t.planPoint(x, y);
        await t.page.mouse.move(px - 3, py - 3);
        await t.page.mouse.move(px, py);
        await t.wait(250);
        return [px, py];
      };
      const box = async () => { const [a, b] = [await t.planPoint(1, -1.6), await t.planPoint(5.4, 1.8)]; return { x: a[0], y: b[1], width: b[0] - a[0], height: a[1] - b[1] }; };
      // Inside the room: the leaf swings in, hinged towards the wall's start.
      let at = await place(2, 0.5);
      t.expect(await t.page.locator('[data-testid=pick-ghost] polyline').count() === 6, 'the door is shown before the click');
      await t.shot('fill-ghost', { clip: await box() });
      await t.page.mouse.click(at[0], at[1]);
      await t.wait(500);
      // Outside the room, in the other hand.
      at = await place(4.4, -0.5);
      await t.keys(' ');
      await t.wait(250);
      await t.shot('fill-ghost-other', { clip: await box() });
      await t.page.mouse.click(at[0], at[1]);
      await t.wait(600);
      await t.keys(' ');
      await t.keys('Escape');
      await t.wait(400);
      const doors = (await t.nodes()).filter((n) => n.type === 'IfcDoor');
      t.expect(doors.length === 2, `two doors placed (${doors.length})`);
      t.expect(!doors[0].props.flipHand && !doors[0].props.flipSide, 'inside: the door as it comes');
      t.expect(doors[1].props.flipHand && doors[1].props.flipSide, 'outside, Space pressed: the other side and the other hand');
      await t.shot('fill-placed', { clip: await box() });
    },
  },
  {
    id: 'openings',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      await t.shot('openings-plan', { of: '[data-testid=plan]', pad: 0 });
      const door = (await t.nodes()).find((n) => n.type === 'IfcDoor');
      t.expect(door, 'a door in the house');
      await select(t, door.id);
      await t.shot('door-properties', { of: 'aside.props', pad: 0, maxHeight: 560 });
      await t.tool('architecture.joineryTypes');
      // The 3D preview under the drawings (report #108), drawn once the engine's meshes come.
      t.expect(await t.page.locator('[data-testid=joinery-preview-3d] canvas').count() === 1, 'the configurator shows its 3D preview');
      await t.wait(900);
      await t.shot('joinery-configurator', { of: '.dialog', pad: 0 });
    },
  },
  {
    // A wall opening edited like a door or a window (a tester's report #114, decision 392): its reveal finished and its head
    // arched in its configurator, then made a window where it is, and an opening again.
    id: 'wall-opening',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      const id = (await t.cmd('architecture.addWallOpening', { wallId: walls[3], along: 4, width: 1.2, height: 2.1 })).id;
      await t.ui({ view: 'plan', keyPlan: false });
      await select(t, id);
      await t.wait(800);
      t.expect(await t.page.locator('[data-field=opening-reveal]').count() === 1, 'a selected opening offers its reveal finish');
      await t.page.locator('[data-field=opening-reveal]').selectOption('plaster');
      await t.wait(700);
      t.expect((await t.nodes()).find((n) => n.id === id)?.props.reveal === 'plaster', 'the reveal is plastered');
      await t.page.locator('[data-action=opening-edit]').click();
      await t.page.waitForSelector('[data-dialog=wall-opening]', { timeout: 10000 });
      await t.page.locator('[data-field=opening-dialog-head]').selectOption('round');
      await t.wait(400);
      await t.shot('wall-opening-dialog', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-action=opening-apply]').click();
      await t.wait(800);
      t.expect((await t.nodes()).find((n) => n.id === id)?.props.shape === 'round', 'the head is a semicircle');
      // Made a window where it is, of a type fitted to it; its GlobalId kept.
      await t.page.locator('[data-action=opening-to-window]').click();
      await t.page.waitForSelector('[data-dialog=wall-opening]', { timeout: 10000 });
      const windows = async () => (await t.nodes()).filter((n) => n.type === 'IfcWindow').map((n) => n.id);
      const before = await windows();
      await t.page.locator('[data-action=opening-change]').click();
      await t.wait(1000);
      const window = (await windows()).find((w) => !before.includes(w));
      t.expect(window && (await t.nodes()).some((n) => n.id === id), 'a window fills the opening, which stays');
      // And an opening again, from the window's properties.
      await select(t, window);
      await t.wait(600);
      await t.page.locator('[data-action=fill-to-opening]').click();
      await t.wait(800);
      t.expect(!(await windows()).includes(window) && (await t.nodes()).find((n) => n.id === id)?.props.width === 1.2, 'the window goes, the opening of its size stays');
    },
  },
  {
    // A wall by its profile (a tester's wish, #100, decision 372): the house's west wall made a gable in the profile editor,
    // the window kept, then seen in 3D.
    id: 'wall-profile',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.cmd('architecture.addWindow', { wallId: walls[3], x: 0, y: 4 });
      await t.ui({ view: 'plan', keyPlan: false, selection: walls[3], selected: [walls[3]] });
      await t.wait(800);
      await t.page.locator('[data-action=wall-profile]').first().click();
      await t.page.waitForSelector('[data-testid=wall-profile]', { timeout: 10000 });
      await t.page.locator('[data-field=profile-peak]').fill('5200');
      await t.page.locator('[data-action=profile-gable]').click();
      await t.wait(600);
      await t.shot('wall-profile-editor', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-action=profile-apply]').click();
      await t.wait(1200);
      const wall = (await t.nodes()).find((n) => n.id === walls[3]);
      t.expect(Array.isArray(wall.props.topProfile) && wall.props.topProfile.length === 3, 'the wall keeps its gable');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.3, pitch: -0.25, distance: 22, target: { x: 4, y: 4, z: 2 } }, false));
      await t.wait(1500);
      await t.shot('wall-profile-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Tracing over a PDF (a tester's wish, #89, decision 371): a page drawn by Lapin itself (a plan sheet as PDF) brought back
    // into an empty plan at 1:100, its lines as drafting and as one group.
    id: 'trace-pdf',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // The house's plan on an A3 sheet, printed to PDF by Lapin, then traced on the first floor.
      const sheet = await t.cmd('sheet.create', { paper: 'A3', name: 'Plan' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: `plan:${storey}`, x: 200, y: 140 });
      const data = await t.page.evaluate(async (id) => (await (await import('/src/engine/session.ts')).engine.call('export.pdf', { sheets: [id] })).data, sheet.id);
      t.expect(Boolean(data), 'the sheet as a PDF');
      const upper = (await t.nodes()).find((n) => n.type === 'IfcBuildingStorey' && n.name === 'First floor');
      const r = await t.cmd('drafting.importPdf', { viewId: `plan:${upper.id}`, name: 'plan.pdf', data, scale: 100, at: [-8, -6] });
      t.expect(r.lines > 10, 'the plan comes in as drafting');
      await t.ui({ view: 'plan', storeyId: upper.id, keyPlan: false, selected: [r.groupId], selection: r.groupId });
      await t.wait(1500);
      await t.fit();
      await t.wait(600);
      await t.shot('trace-pdf', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // A double door (decision 466): the Double door preset, its middle mullion picked in the elevation (none), the meeting section.
    id: 'joinery-double-door',
    async run(t) {
      await t.fresh();
      const { page } = t;
      const d = page.locator('.dialog');
      await t.tool('architecture.joineryTypes');
      await page.waitForSelector('.dialog [data-testid=pane-editor]', { timeout: 15000 });
      await d.locator('[data-action=new-double-door]').click();
      await d.locator('[data-testid=meeting-fields]').waitFor({ timeout: 15000 });
      await t.wait(600);
      await d.locator('[data-divider=col-0]').click();
      await t.wait(400);
      t.expect(await d.locator('[data-testid=bar-field]').count() === 1, 'the picked mullion shows its width');
      t.expect(await d.locator('input[data-field=bar-none]').isChecked(), 'the preset has no mullion between its leaves');
      await d.locator('[data-testid=meeting-fields]').evaluate((el) => el.scrollIntoView({ block: 'center' }));
      await t.wait(800);
      await t.shot('joinery-double-door', { of: '.dialog', pad: 0 });
    },
  },
  {
    // Panes, mullions and profiles (decision 370, a tester's questions): a shop door made in the pane editor (four columns, a
    // transom split off each and merged across, two leaves, one solid side panel), then a PVC window in a wall, in 3D.
    id: 'joinery-panes',
    async run(t) {
      await t.fresh();
      const { page } = t;
      const d = page.locator('.dialog');
      await t.tool('architecture.joineryTypes');
      await page.waitForSelector('.dialog [data-testid=pane-editor]', { timeout: 15000 });
      await d.getByRole('button', { name: /New door/ }).first().click();
      await t.wait(800);
      const field = async (label, value) => { const i = d.locator('.field-line', { hasText: new RegExp(`^${label}`) }).locator('input').first(); await i.fill(value); await i.press('Enter'); await t.wait(300); };
      await field('Width', '2400');
      await field('Height', '2700');
      await d.locator('input[data-field=columns]').fill('1 1.5 1.5 1');
      await d.locator('input[data-field=columns]').press('Tab');
      await t.wait(800);
      for (const c of [0, 1, 2, 3]) {
        await d.locator(`[data-pane="${c}:0"]`).click(); await t.wait(300);
        await d.locator('[data-field=split-row-at]').fill('2150'); await d.locator('[data-action=split-row]').click(); await t.wait(500);
      }
      await d.locator('[data-pane="1:0"]').click(); await t.wait(200); await d.locator('select[data-field=pane-operation]').selectOption('swing-left'); await t.wait(300);
      await d.locator('[data-pane="2:0"]').click(); await t.wait(200); await d.locator('select[data-field=pane-operation]').selectOption('swing-right'); await t.wait(300);
      await d.locator('select[data-field=pane-infill]').selectOption('glass'); await t.wait(300);
      await d.locator('[data-pane="0:0"]').click(); await t.wait(200); await d.locator('select[data-field=pane-infill]').selectOption('solid'); await t.wait(300);
      await d.locator('[data-pane="0:1"]').click(); await t.wait(300);
      for (let i = 0; i < 3; i++) { await d.locator('[data-action=merge-right]').click(); await t.wait(700); }
      t.expect(await d.locator('[data-pane]').count() === 5, 'two leaves, two side lights and a transom across');
      await d.locator('[data-testid=pane-editor]').evaluate((el) => el.scrollIntoView({ block: 'center' }));
      await t.wait(800);
      await t.shot('joinery-panes', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // A PVC window: a solid panel under a fixed light, two sashes over them.
      const s = await t.storey();
      const info = await page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().info.catalogs['architecture.joinery']);
      const sys = info.profileSystems.pvc;
      const spec = { ...info.defaultWindow, name: 'PVC 1200', frame: { ...sys.frame, offset: 0 }, sash: sys.sash, profile: sys.profile, columns: [1, 1], rows: [400, 1000],
        panels: [{ col: 0, row: 0, operation: 'fixed', infill: 'solid' }, { col: 1, row: 0, operation: 'fixed' }, { col: 0, row: 1, operation: 'tilt-turn-left' }, { col: 1, row: 1, operation: 'side-right' }] };
      const type = (await t.cmd('architecture.createJoineryType', { spec })).id;
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, x1: 0, y1: 0, x2: 4, y2: 0 })).id;
      await t.cmd('architecture.addWindow', { wallId: wall, x: 2, y: 0, typeId: type });
      await open3d(t);
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.2, pitch: -0.15, distance: 2.6, target: { x: 2, y: 0, z: 1.5 } }, false));
      await t.wait(1500);
      await t.shot('joinery-profiles-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    id: 'window-dressings',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      // The window type: three panes, then shutters, a grille in the reveal, a window box and a canopy (reports #38, #39).
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.tree-item').filter({ hasText: /^Window/ }).first().click();
      await t.wait(300);
      await dialog.locator('input[data-field=columns]').fill('1 1 1');
      for (const d of ['shutters', 'grille', 'windowBox', 'canopy']) await dialog.locator(`input[data-dressing=${d}]`).check();
      await dialog.locator('input[data-field=outside]').check();
      await t.wait(800);
      t.expect(await dialog.locator('input[data-dressing=shutters-open]').count() === 1, 'the shutters are set up in the configurator');
      // The form at its facade dressings, the preview (kept in view) drawing them from outside.
      await dialog.locator('input[data-dressing=shutters]').evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await t.wait(300);
      await t.shot('joinery-dressings', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const type = (await t.nodes()).find((n) => n.type === 'IfcWindowType' && n.props?.spec?.details?.shutters?.on);
      t.expect(type, 'the window type keeps its shutters');
      await open3d(t);
      await t.shot('window-dressings-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    id: 'window-styles',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      // A style sets the outline, the glazing bars and the ornaments at once (decision 188): a baroque window, a round arch with a sunburst.
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.tree-item').filter({ hasText: /^Window/ }).first().click();
      await t.wait(300);
      await dialog.locator('select[data-field=style]').selectOption('baroque');
      await dialog.locator('input[data-field=outside]').check();
      await t.wait(800);
      // The select keeps the style the type was given (report #111).
      t.expect(await dialog.locator('select[data-field=style]').inputValue() === 'baroque', 'the style select shows the style applied');
      t.expect(await dialog.locator('select[data-field=shape]').inputValue() === 'round', 'the style gives the window a round arch');
      t.expect(await dialog.locator('input[data-dressing=keystone]').isChecked(), 'the style puts a keystone on it');
      await dialog.locator('select[data-field=shape]').evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await t.wait(300);
      await t.shot('joinery-styles', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      // The door gets a knob (report #48).
      await dialog.locator('.tree-item').filter({ hasText: /^Door/ }).first().click();
      await t.wait(300);
      await dialog.locator('select[data-field=handle]').selectOption('knob');
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const type = (await t.nodes()).find((n) => n.type === 'IfcWindowType' && n.props?.spec?.shape?.kind === 'round');
      t.expect(type, 'the window type keeps its arch');
      t.expect(type.props.spec.style === 'baroque', 'the window type remembers its style');
      await open3d(t);
      await t.shot('window-styles-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // An arched casement (a tester's report #112): no transom at the spring line, one sash following the arch.
    id: 'window-arched-casement',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.tree-item').filter({ hasText: /^Window/ }).first().click();
      await t.wait(300);
      await dialog.locator('select[data-field=style]').selectOption('arched-casement');
      await t.wait(800);
      t.expect(await dialog.locator('select[data-field=shape]').inputValue() === 'segmental', 'the style gives the window a segmental arch');
      t.expect(!(await dialog.locator('input[data-field=shape-transom]').isChecked()), 'no transom at the springing');
      await dialog.locator('select[data-field=shape]').evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await t.wait(500);
      await t.shot('joinery-arched-casement', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const type = (await t.nodes()).find((n) => n.type === 'IfcWindowType' && n.props?.spec?.shape?.transom === false);
      t.expect(type, 'the window type keeps its sash following the arch');
    },
  },
  {
    // A frameless glass door (tester's question 7): toughened glass, no frame or sash, a sliding leaf in front of a fixed pane.
    id: 'door-frameless-glass',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.tree-item').filter({ hasText: 'New door' }).click();
      await t.wait(400);
      await dialog.locator('select[data-field=style]').selectOption('frameless-glass');
      await t.wait(600);
      t.expect(await dialog.locator('input[data-field=frameless]').isChecked(), 'the style makes the door frameless glass');
      t.expect(await dialog.locator('select[data-field=frameless-fixing]').inputValue() === 'pivot', 'on pivots with patch fittings by default');
      t.expect(await dialog.locator('select[data-field=shape]').count() === 0, 'no shape or frame fields for frameless glass');
      await dialog.locator('input[data-field=frameless]').evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await t.wait(500);
      await t.shot('joinery-frameless-door', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const type = (await t.nodes()).find((n) => n.type === 'IfcDoorType' && n.props?.spec?.frameless?.fixing === 'pivot');
      t.expect(type, 'the door type keeps its frameless glass');
    },
  },
  {
    // A frameless glass door on a floor spring (a forum user's request, topic #28): the preset, the spring's model, single or
    // double action, the pivot's offset, the bottom fitting and the hold-open angle.
    id: 'door-floor-spring',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-action=new-floor-spring-door]').click();
      await dialog.locator('[data-testid=floor-spring-fields]').waitFor({ timeout: 15000 });
      await t.wait(600);
      t.expect(await dialog.locator('input[data-field=frameless]').isChecked(), 'the preset is frameless glass');
      t.expect(await dialog.locator('select[data-field=frameless-fixing]').inputValue() === 'floor-spring', 'hung on a floor spring');
      t.expect(await dialog.locator('select[data-field=floor-spring-model]').inputValue() === 'auto', 'the spring the lightest that carries the leaf');
      t.expect(await dialog.locator('select[data-field=floor-spring-action]').inputValue() === 'double', 'double action by default');
      t.expect(await dialog.locator('select[data-field=floor-spring-hold]').inputValue() === '90', 'held open at 90°');
      t.expect(await dialog.locator('select[data-field=floor-spring-model] option').count() >= 3, 'the models offered');
      await dialog.locator('select[data-field=floor-spring-bottom]').selectOption('strap');
      await t.wait(600);
      await dialog.locator('[data-testid=floor-spring-fields]').evaluate((el) => el.scrollIntoView({ block: 'center' }));
      await t.wait(500);
      await t.shot('joinery-floor-spring', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const type = (await t.nodes()).find((n) => n.type === 'IfcDoorType' && n.props?.spec?.frameless?.fixing === 'floor-spring');
      t.expect(type, 'the door type keeps its floor spring');
      t.expect(type?.props.spec.frameless.bottom === 'strap', 'with a heel strap at the bottom');
      t.expect(type?.props.spec.panels?.[0]?.operation === 'double-acting-left', 'its leaf double acting');
    },
  },
  {
    // Bay, bow and box windows (report #60): a bay window type from the configurator (New bay window, a bow style), placed
    // with the Bay window tool and by command, in plan (the projection, the roof dashed over it) and in 3D.
    id: 'bay-windows',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] })).id);
      }
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      await t.fit();
      // The configurator: a new bay window, then a bow from Lapin's styles.
      await t.tool('architecture.joineryTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-action=new-bay-window]').click();
      await t.wait(500);
      t.expect(await dialog.locator('select[data-field=bay-kind]').inputValue() === 'bay', 'a new bay window projects');
      await dialog.locator('select[data-field=bay-style]').selectOption('bow-5');
      await t.wait(800);
      t.expect(await dialog.locator('select[data-field=bay-kind]').inputValue() === 'bow', 'the style makes it a bow');
      t.expect(await dialog.locator('select[data-field=bay-style]').inputValue() === 'bow-5', 'the bay style select shows the style applied');
      await dialog.locator('[data-testid=bay-fields]').evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await t.wait(400);
      await t.shot('bay-configurator', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').first().click();
      await t.wait(800);
      await t.keys('Escape');
      const bow = (await t.nodes()).find((n) => n.type === 'IfcWindowType' && n.props?.spec?.bay?.kind === 'bow');
      t.expect(bow, 'the bow window type is saved');
      await t.cmd('architecture.addWindow', { wallId: walls[2], x: 6, y: 8, typeId: bow.id });
      // The Bay window tool: a click on the south wall puts a 45° bay in it.
      await t.ui({ windowTypeId: null });
      await t.tool('architecture.bayWindow');
      { const [px, py] = await t.planPoint(3.5, 0); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(1200); }
      await t.keys('Escape');
      const bays = (await t.nodes()).filter((n) => n.type === 'IfcWindow');
      t.expect(bays.length === 2, `the bay tool placed a window (${bays.length})`);
      // A box window with solid sides, and an oriel on corbels, by their styles.
      const styles = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().info?.catalogs['architecture.bayStyles']);
      for (const [id, wall, x, y] of [['box-solid', 0, 9.2, 0], ['oriel', 1, 12, 4]]) {
        const type = (await t.cmd('architecture.createJoineryType', { spec: styles.find((st) => st.id === id).spec })).id;
        await t.cmd('architecture.addWindow', { wallId: walls[wall], x, y, typeId: type });
      }
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      await t.wait(1200);
      // Zoomed out a little about the middle, the quick tools and the card hidden, so what projects stands clear.
      { const [px, py] = await t.planPoint(6, 4); await t.page.mouse.move(px, py); for (let i = 0; i < 3; i++) { await t.page.mouse.wheel(0, 100); await t.wait(80); } await t.wait(600); }
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      await t.wait(300);
      await t.shot('bay-plan', { of: '[data-testid=plan]', pad: 0 });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 0.6, pitch: -0.45, distance: 19, target: { x: 6, y: 4, z: 1.2 } }, false));
      await t.wait(1800);
      await t.shot('bay-3d', { of: 'canvas', pad: 0 });
      await quiet.evaluate((el) => el.remove());
    },
  },
  {
    // Sunshades (report #56): a concrete canopy put on a window with the Sunshade tool, louvres, fins, an awning, a pergola and
    // an external blind; the noon helper on the canopy; the types dialog; in plan (overhead dashed) and in 3D.
    id: 'sunshades',
    async run(t) {
      await t.fresh();
      const { storey: s, walls } = await house(t, { roof: false });
      await t.fit();
      const windows = (await t.nodes()).filter((n) => n.type === 'IfcWindow');
      const door = (await t.nodes()).find((n) => n.type === 'IfcDoor');
      // The Sunshade tool, with a style in its bar: a click on the south window.
      await t.tool('architecture.sunshade');
      await t.wait(300);
      await t.page.locator('[data-testid=sunshade-type]').selectOption('style:concrete-canopy');
      { const [px, py] = await t.planPoint(9.5, 0); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(1000); }
      await t.keys('Escape');
      const shades = async () => (await t.nodes()).filter((n) => n.type === 'IfcShadingDevice');
      const canopy = (await shades())[0];
      t.expect(canopy && canopy.props.host, 'the canopy stands on the window');
      await t.cmd('architecture.addSunshade', { style: 'brise-soleil', hostId: windows[1].id });
      await t.cmd('architecture.addSunshade', { style: 'venetian-blind', hostId: windows[3].id });
      await t.cmd('architecture.addSunshade', { style: 'fabric-awning', hostId: door.id });
      await t.cmd('architecture.addSunshade', { style: 'vertical-fins', wallId: walls[2], x1: 7.4, y1: 8, x2: 11.6, y2: 8 });
      await t.cmd('architecture.addSunshade', { style: 'timber-pergola', storeyId: s, x1: 12.2, y1: 0.4, x2: 12.2, y2: 3.2 });
      t.expect((await shades()).length === 6, `six sunshades (${(await shades()).length})`);
      // The canopy's properties and the noon helper.
      await select(t, canopy.id);
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=sunshade-section]').count() === 1, 'a sunshade shows its properties');
      await t.page.locator('[data-action=sunshade-noon]').click();
      await t.wait(1000);
      const after = (await shades()).find((n) => n.id === canopy.id);
      t.expect(typeof after.props.projection === 'number' && after.props.projection > 0.2, `the noon helper sets the canopy's projection (${after.props.projection})`);
      await t.shot('sunshade-properties', { of: '[data-testid=sunshade-section]', pad: 8 });
      // The types dialog on the louvres' type.
      await t.page.locator('[data-action=sunshade-edit-type]').click();
      await t.wait(400);
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-sunshade-type]').filter({ hasText: 'Brise-soleil' }).first().click();
      await t.wait(1200);
      t.expect(await dialog.locator('[data-testid=sunshade-preview] svg polyline').count() > 4, 'the dialog previews the type');
      t.expect(await dialog.locator('[data-param=angle]').count() === 1, 'the louvres\' own parameters are edited there');
      await t.shot('sunshade-types', { of: '.dialog', pad: 0 });
      await dialog.locator('button[title="Close"]').first().click();
      await t.wait(300);
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      await t.wait(1200);
      // Zoomed out a little about the middle, the quick tools and the card hidden, so what projects stands clear.
      { const [px, py] = await t.planPoint(6, 4); await t.page.mouse.move(px, py); for (let i = 0; i < 3; i++) { await t.page.mouse.wheel(0, 100); await t.wait(80); } await t.wait(600); }
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      await t.wait(300);
      await t.shot('sunshades-plan', { of: '[data-testid=plan]', pad: 0 });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 0.75, pitch: -0.4, distance: 20, target: { x: 6, y: 4, z: 1.4 } }, false));
      await t.wait(1800);
      await t.shot('sunshades-3d', { of: 'canvas', pad: 0 });
      await quiet.evaluate((el) => el.remove());
    },
  },
  {
    id: 'floors-roofs',
    async run(t) {
      await t.fresh();
      await house(t);
      await open3d(t);
      await t.shot('house-3d', { of: 'canvas', pad: 0 });
      const roof = (await t.nodes()).find((n) => n.type === 'IfcRoof');
      t.expect(roof, 'a roof on the house');
      await select(t, roof.id);
      await t.wait(600);
      // The covering's material (decision 328): the covering's own, made in the project, picked in the properties.
      t.expect(roof.props?.coveringMaterial && (await t.nodes()).some((n) => n.type === 'IfcMaterial' && n.name === roof.props.coveringMaterial), `the covering is made of a project material (${roof.props?.coveringMaterial})`);
      t.expect(await t.page.locator('aside.props dt[title=coveringMaterial]').count() > 0, 'the properties show the covering material');
      // Only the fields that apply (decision 333): a framed roof shows its rafters' fields, not the deck's.
      const isFramed = !['flat', 'barrel'].includes(roof.props?.shape) && roof.props?.structure !== 'none';
      t.expect(isFramed, `the house's roof is framed (${roof.props?.shape}, ${roof.props?.structure})`);
      t.expect(await t.page.locator('aside.props dt[title=deckThickness]').count() === 0, 'a framed roof hides the deck fields');
      t.expect(await t.page.locator('aside.props dt[title=rafterSpacing]').count() > 0, 'a framed roof shows its rafters');
      await t.shot('roof-properties', { of: 'aside.props', pad: 0, maxHeight: 700 });
      t.expect((await t.nodes()).some((n) => n.type === 'IfcSlab'), 'a floor in the house');
      // The roof configurator (decision 231): the Swedish truss style picked, its framing seen in the section, applied.
      await t.tool('architecture.roofConfigurator');
      const conf = t.page.locator('[aria-label="Roof configurator"]');
      t.expect(await conf.count() > 0, 'the roof configurator opens on the roof selected');
      await conf.locator('.roof-styles .tree-item').filter({ hasText: 'Swedish truss roof' }).click();
      await conf.locator('[role=tab]').nth(2).click();
      await t.wait(800);
      t.expect(await conf.locator('svg[aria-label="Section"] polygon').count() > 6, 'the section shows a truss');
      await t.shot('roof-configurator', { of: '.dialog', pad: 0 });
      await conf.locator('.btn.primary').click();
      await t.wait(1000);
      const framed = (await t.nodes()).find((n) => n.id === roof.id);
      t.expect(framed?.props?.structure === 'trusses', `the roof takes the trusses (structure ${framed?.props?.structure})`);
      // Saved as the project's own style (decision 233): Save as style…, a name, and it is listed under Project.
      const answers = t.page.listeners('dialog');
      t.page.removeAllListeners('dialog');
      t.page.once('dialog', (d) => void d.accept('Our truss roof'));
      await conf.locator('.joinery-actions .btn').nth(1).click();
      await t.wait(1000);
      for (const l of answers) t.page.on('dialog', l);
      const project = (await t.nodes()).find((n) => n.type === 'IfcProject');
      t.expect(project?.props?.roofStyles?.[0]?.label === 'Our truss roof', `the roof saved as a project style (${project?.props?.roofStyles?.[0]?.label})`);
      t.expect(await conf.locator('.roof-own').count() === 1, 'the configurator lists the project style');
      // The downpipes made elements of their own (decision 235), each tied to its gutter.
      await t.cmd('architecture.makeDownpipes', { id: roof.id });
      await t.wait(800);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinDownpipe').length >= 2, 'the roof\'s downpipes are elements');
      await t.ui({ dialog: null });
      // A roof window (decisions 251, 280): the roof selected in plan shows its grips: the middle moves it on the slope, an edge resizes it, Delete over a grip removes it.
      const opening = await t.cmd('architecture.addRoofOpening', { kind: 'window', x: 3.5, y: 1.6 });
      t.expect(opening?.id, 'a roof window on the front slope');
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: await t.storey(), name: 'Roof plan' });
      await t.ui({ view: 'plan', planViewId: plan.id, keyPlan: false });
      await t.wait(1200);
      await t.fit();
      await select(t, roof.id);
      await t.wait(800);
      // The grips as drawn (data-grip: the grip's key): the window's middle, then its right edge.
      const gripAt = async (key) => { const r = await t.page.locator(`[data-testid=plan] [data-grip="${key}"]`).first().boundingBox(); return r ? [r.x + r.width / 2, r.y + r.height / 2] : null; };
      const [px] = await t.planPoint(3.5, 1.6);
      const [gx, gy] = (await gripAt(`opening:${opening.id}`)) ?? [0, 0];
      t.expect(gx > 0, 'the roof window shows its middle grip');
      await t.page.mouse.move(gx, gy);
      await t.page.mouse.down();
      const [hx] = await t.planPoint(5, 1.6);
      await t.page.mouse.move(gx + (hx - px), gy, { steps: 6 });
      await t.page.mouse.up();
      await t.wait(1000);
      const moved = (await t.nodes()).find((n) => n.id === roof.id)?.props?.openings?.find((o) => o.id === opening.id);
      t.expect(moved && Math.abs(moved.x - 3.5) > 0.5 && Math.abs((moved.width ?? 0.78) - 0.78) < 1e-6, `dragging its grip moves the roof window (x ${moved?.x})`);
      // Its right edge's grip (decision 280): dragged 0.3 m out along the eave, the window as much wider, its left edge kept.
      const [ex, ey] = (await gripAt(`opening:${opening.id}:1:0`)) ?? [0, 0];
      t.expect(ex > 0, 'the roof window shows its edges\' grips');
      const [r0] = await t.planPoint(0, 1.6), [r1] = await t.planPoint(0.3, 1.6);
      const ex2 = ex + (r1 - r0);
      await t.page.mouse.move(ex, ey);
      await t.page.mouse.down();
      await t.page.mouse.move(ex2, ey, { steps: 6 });
      await t.page.mouse.up();
      await t.wait(1000);
      const resized = (await t.nodes()).find((n) => n.id === roof.id)?.props?.openings?.find((o) => o.id === opening.id);
      t.expect(resized && Math.abs((resized.width ?? 0.78) - (moved.width ?? 0.78) - 0.3) < 0.05, `dragging its edge's grip resizes the roof window (width ${resized?.width})`);
      await t.page.mouse.move(ex2 + 40, ey + 40);
      await t.wait(300);
      await t.shot('roof-window-grip', { of: '[data-testid=plan]', pad: 0 });
      // A right-click on its grip offers to remove it; Delete with the cursor over the grip removes the window, not the roof.
      const [mx, my] = (await gripAt(`opening:${opening.id}`)) ?? [0, 0];
      await t.page.mouse.click(mx, my, { button: 'right' });
      await t.wait(300);
      t.expect(await t.page.locator('.context-menu .context-item').count() === 1, 'a right-click on its grip offers Remove roof window');
      await t.page.keyboard.press('Escape');
      await t.page.mouse.move(mx + 30, my + 30);
      await t.page.mouse.move(mx, my, { steps: 3 });
      await t.wait(300);
      await t.page.keyboard.press('Delete');
      await t.wait(1000);
      const after = (await t.nodes()).find((n) => n.id === roof.id);
      t.expect(after && !(after.props?.openings ?? []).some((o) => o.id === opening.id), 'Delete over its grip removes the roof window and keeps the roof');
      await t.ui({ keyPlan: true });
    },
  },
  {
    // A flat roof's deck under its covering (decision 329): two parts, two bands in a section, each hatched by its material.
    id: 'floors-roofs-deck',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const roof = await t.cmd('architecture.addRoof', { storeyId: storey, points: [[0, 0], [12, 0], [12, 8], [0, 8]], style: 'modern_minimalist' });
      await t.cmd('architecture.updateRoof', { id: roof.id, attrs: { covering: 'Green roof', coveringMaterial: 'Soil substrate' } });
      await t.wait(800);
      const node = (await t.nodes()).find((n) => n.id === roof.id);
      t.expect(node?.props?.deckMaterial === 'Reinforced concrete', `the flat roof has a deck of reinforced concrete (${node?.props?.deckMaterial})`);
      await select(t, roof.id);
      await t.wait(600);
      t.expect(await t.page.locator('aside.props dt[title=deckThickness]').count() > 0, 'a flat roof shows its deck fields');
      t.expect(await t.page.locator('aside.props dt[title=rafterSpacing]').count() === 0, 'a flat roof hides the rafters');
      await t.cmd('view.create', { kind: 'section', name: 'R-R', start: [10.5, -1], end: [10.5, 9], scale: 20 });
      await openFromBrowser(t, 'R-R', 1500);
      await t.fit();
      t.expect(await t.page.locator('[data-testid=drawing] .pen-hatch').count() > 0, 'the section hatches the deck and the covering');
      // Zoomed in on the eave over the south wall (a wheel event zooms one notch at most).
      await t.ui({ keyPlan: false });
      const box = await t.page.locator('[data-testid=drawing]').boundingBox();
      const [cx, cy] = [box.x + 0.161 * box.width, box.y + 0.25 * box.height];
      await t.page.mouse.move(cx, cy);
      for (let i = 0; i < 13; i++) { await t.page.mouse.wheel(0, -100); await t.wait(80); }
      // The corner panned to the middle (a middle-button drag).
      await t.page.mouse.down({ button: 'middle' });
      await t.page.mouse.move(box.x + 0.42 * box.width, box.y + 0.52 * box.height, { steps: 8 });
      await t.page.mouse.up({ button: 'middle' });
      await t.page.mouse.move(box.x + box.width - 20, box.y + box.height - 20);
      await t.wait(800);
      await t.shot('roof-deck-section', { of: '[data-testid=drawing]', pad: 0 });
      await t.ui({ keyPlan: true });
    },
  },
  {
    id: 'schedules',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const nodes = await t.nodes();
      const doorType = nodes.find((n) => n.type === 'IfcDoorType');
      const windowType = nodes.find((n) => n.type === 'IfcWindowType');
      // The types' parameters (decision 251): the door's fire rating in IFC's own set, a maker for the windows.
      await t.cmd('element.setParam', { ids: [doorType.id], pset: 'Pset_DoorCommon', name: 'FireRating', value: 'EI2 30' });
      await t.cmd('element.setParam', { ids: [windowType.id], name: 'Manufacturer', kind: 'text', value: 'Rehau' });
      await t.cmd('element.setParam', { ids: [doorType.id], name: 'Manufacturer', kind: 'text', value: 'Porta' });
      const schedule = await t.cmd('architecture.createSchedule', { kind: 'all' });
      await t.ui({ view: 'schedule', scheduleId: schedule.id, keyPlan: false });
      await t.wait(1500);
      t.expect(await t.page.locator('.schedule-grid input[value="EI2 30"]').count() === 1, 'the fire rating row reads the door type\'s FireRating');
      t.expect(await t.page.locator('[data-testid^=param-Manufacturer]').count() >= 2, 'the types\' Manufacturer is a row');
      t.expect(await toolbarClear(t), 'the schedule\'s toolbar lays out without its controls overlapping (report #65)');
      await t.shot('joinery-schedule', { of: '.schedule-pane', pad: 0 });
      // The configurator's parameters.
      await t.ui({ dialog: 'joinery' });
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=joinery-params]').count() === 1, 'the configurator shows the type\'s parameters');
      await t.page.locator('[data-testid=joinery-params]').scrollIntoViewIfNeeded();
      await t.wait(300);
      await t.shot('joinery-params', { of: '[data-testid=joinery-params]', pad: 8 });
      await t.ui({ dialog: null });
      // A room schedule with a column and a row of its own, and a chart of the areas by function.
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      const rooms = await t.cmd('architecture.createSchedule', { kind: 'rooms' });
      const column = await t.cmd('architecture.addScheduleColumn', { id: rooms.id, label: 'Floor finish' });
      const row = await t.cmd('architecture.addScheduleManualRow', { id: rooms.id });
      await t.cmd('architecture.scheduleCell', { id: rooms.id, entry: row.row, row: 'c:Name', text: 'Terrace' });
      await t.cmd('architecture.scheduleCell', { id: rooms.id, entry: row.row, row: 'c:Function', text: 'Outside' });
      await t.cmd('architecture.scheduleCell', { id: rooms.id, entry: row.row, row: 'c:Area (m²)', text: '12.00' });
      await t.cmd('architecture.scheduleCell', { id: rooms.id, entry: row.row, row: column.column, text: 'Decking' });
      await t.cmd('architecture.scheduleChart', { id: rooms.id, chart: { kind: 'pie', category: 'Function', value: 'Area (m²)', aggregate: 'sum' } });
      await t.ui({ view: 'schedule', scheduleId: rooms.id });
      await t.wait(1500);
      await t.page.locator('[data-testid=schedule-charts-toggle]').click();
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=schedule-grid] input[value="Terrace"]').count() === 1, 'the room schedule has the row of its own');
      t.expect(await t.page.locator('[data-testid=schedule-charts] li').count() === 1, 'the chart is listed beside the table');
      await t.page.locator('[data-testid=schedule-charts] button[title="Change the chart"]').click();
      await t.wait(400);
      await t.fit();
      await t.shot('schedule-chart', { of: '.schedule-pane', pad: 0 });
      // The type assemblies schedule (reports #65–#67): each type's build-up drawn after the table.
      const types = await t.cmd('architecture.createSchedule', { kind: 'types' });
      await t.ui({ view: 'schedule', scheduleId: types.id });
      await t.wait(1500);
      if (await t.page.locator('[data-testid=schedule-charts]').count()) await t.page.locator('[data-testid=schedule-charts-toggle]').click();
      await t.wait(300);
      t.expect(await t.page.locator('[data-testid=schedule-buildups] input:checked').count() === 1, 'a new type assemblies schedule draws the build-ups');
      t.expect(await t.page.locator('[data-testid=schedule] text', { hasText: 'Build-ups' }).count() === 1, 'the build-ups follow the table');
      t.expect(await toolbarClear(t), 'the type assemblies toolbar lays out without its controls overlapping (report #65)');
      await t.fit();
      await t.shot('type-assemblies', { of: '.schedule-pane', pad: 0 });
      // Only the result: the table editor folded into its strip, the quick tools hidden in the documents (app/bars.ts).
      await t.page.locator('[data-testid=schedule-editor-toggle]').click();
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=schedule-editor].folded').count() === 1 && await t.page.locator('[data-testid=schedule-grid]').count() === 0, 'the table editor folds into its strip');
      await t.page.locator('[data-testid=quick-tools-hide]').click();
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=quick-tools-show]').count() === 1 && await t.page.locator('.hotbar').count() === 0, 'the quick tools hide, a handle left to bring them back');
      await t.fit();
      await t.shot('schedule-result-only', { of: '.schedule-pane', pad: 0 });
      await t.ui({ view: 'plan' });
      await t.wait(800);
      t.expect(await t.page.locator('.hotbar').count() === 1, 'hidden over the documents, the quick tools stay on the plan');
      await t.ui({ view: 'schedule', scheduleId: types.id });
      await t.wait(800);
      await t.page.locator('[data-testid=quick-tools-show]').click();
      await t.page.locator('[data-testid=schedule-editor-toggle]').click();
      await t.wait(300);
      t.expect(await t.page.locator('[data-testid=schedule-grid]').count() === 1 && await t.page.locator('.hotbar').count() === 1, 'the editor and the quick tools come back');
      await t.ui({ keyPlan: true });
    },
  },
  {
    id: 'quantities',
    async run(t) {
      // Getting quantities (decision 287, the tester's report #75): Cost → Quantity takeoff, grouped by kind and
      // material, then placed on a new sheet, its table drawn there with its numbers.
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4 });
      await t.ui({ keyPlan: false });
      await t.button('Construction', { exact: true });
      await t.wait(300);
      await t.page.locator('.band button', { hasText: 'Quantity takeoff' }).first().click();
      await t.wait(1500);
      const takeoff = (await t.nodes()).find((n) => n.type === 'LapinSchedule' && n.props.kind === 'quantities');
      t.expect(takeoff, 'Quantity takeoff makes the takeoff schedule');
      t.expect(await t.page.locator('[data-testid=schedule-grid] input[value="Wall"]').count() === 1, 'the takeoff lists the walls');
      await t.page.locator('[data-testid=takeoff-group]').selectOption('kind,material');
      await t.wait(1500);
      t.expect(await t.page.locator('[data-testid=schedule-grid] th', { hasText: 'Material' }).count() === 1, 'grouped by material, the takeoff has a Material column');
      await t.fit();
      await t.shot('quantity-takeoff', { of: '.schedule-pane', pad: 0 });
      // Place on sheet… → New sheet: the sheet opens with the table on it.
      await t.page.locator('[data-testid=schedule-place]').selectOption('new');
      await t.wait(2500);
      t.expect(await t.page.locator('[data-testid=sheet]').count() === 1, 'the new sheet opens');
      await t.fit();
      const texts = await t.page.locator('[data-testid=sheet] text').allTextContents();
      t.expect(texts.includes('Quantity takeoff') && texts.includes('Wall') && texts.filter((x) => /^\d+\.\d{2,3}$/.test(x)).length > 10, 'the sheet draws the table with its numbers (report #75)');
      await t.shot('takeoff-sheet', { of: '.view-pane:has([data-testid=sheet])', pad: 0 });
      await t.ui({ keyPlan: true });
    },
  },
  {
    // The estimate (docs/plan-4d5d.md, stage 6): a price list imported from a Romanian CSV (its columns guessed from the
    // headings), Map articles suggesting the walls' article by its words and the plaster's by its material, the estimate
    // under the view with a wall line selecting its walls, the F3 form of the same estimate.
    id: 'cost-estimate',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const type = (await t.cmd('architecture.createWallType', { name: 'Zidărie cărămidă 25 tencuită', layers: [
        { material: 'Tencuială de var', thickness: 0.02 }, { material: 'Cărămidă', thickness: 0.25, core: true }, { material: 'Tencuială de var', thickness: 0.02 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
      }
      walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type })).id);
      await t.cmd('architecture.addDoor', { wallId: walls[4], x: 7, y: 4 });
      for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8], [1, 12, 4]]) await t.cmd('architecture.addWindow', { wallId: walls[w], x, y });
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      await t.ui({ keyPlan: false });
      await t.button('Construction', { exact: true });
      await t.wait(400);
      await t.shot('ribbon-cost', { of: '.topbar', pad: 0 });
      // Cost → Price lists… → Import…: a CSV as an estimating program writes it, Romanian headings, decimal commas.
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFileSync } = await import('node:fs');
      const csv = join(tmpdir(), 'Preturi-2026.csv');
      writeFileSync(csv, '\uFEFFCod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport;Normă de timp;Meserie\n'
        + 'ZID01;Zidărie din cărămidă 25 cm;mc;380,00;85,00;6,00;14,00;4;Zidar\n'
        + 'TEN01;Tencuială de var pe pereți;mp;18,00;22,00;1,00;1,00;0,5;Tencuitor\n'
        + 'BET01;Beton C25/30 în plăci;mc;410,00;95,00;22,00;18,00;6;Betonist\n'
        + 'USA01;Ușă interioară montată;buc;900,00;120,00;0;15,00;3;Tâmplar\n'
        + 'FER01;Fereastră PVC montată;mp;650,00;80,00;2,00;10,00;1,5;Tâmplar\n');
      await t.tool('cost.prices');
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.locator('[data-testid=price-import]').click();
      await (await chooser).setFiles(csv);
      await t.page.locator('[data-testid=price-matching]').waitFor({ timeout: 10_000 });
      const guessed = await t.page.locator('[data-testid^=price-column-]').evaluateAll((els) => els.map((e) => e.value));
      t.expect(guessed.join(',') === 'code,name,unit,material,labour,equipment,transport,hours,trade', `the columns guessed from the Romanian headings (${guessed.join(',')})`);
      await t.shot('price-import', { of: '[data-testid=price-lists-dialog]', pad: 0 });
      await t.page.locator('[data-testid=price-import-go]').click();
      await t.wait(800);
      t.expect((await t.nodes()).some((n) => n.type === 'LapinPriceList'), 'the list is in the project');
      await t.ui({ dialog: null });
      // Cost → Map articles…: the walls' type suggested ZID01 on its core, the plaster TEN01 by its material.
      await t.tool('cost.map');
      await t.page.locator('[data-testid=map-types]').waitFor({ timeout: 10_000 });
      await t.wait(800);
      const typeCode = await t.page.locator(`[data-testid=map-types] tr[data-type="${type}"] [data-testid=map-code]`).inputValue();
      t.expect(typeCode === 'ZID01', `the walls' article suggested by its words (${typeCode})`);
      const plaster = (await t.nodes()).find((n) => n.type === 'IfcMaterial' && n.name === 'Tencuială de var').id;
      t.expect(await t.page.locator(`[data-testid=map-materials] tr[data-material="${plaster}"] [data-testid=map-code]`).inputValue() === 'TEN01', 'the plaster suggested by its material');
      await t.page.locator(`[data-testid=map-materials] tr[data-material="${plaster}"] input[type=checkbox]`).check();
      await t.shot('map-articles', { of: '[data-testid=map-articles]', pad: 0 });
      await t.page.locator('[data-testid=map-apply]').click();
      await t.wait(800);
      // The floor, the door and the windows by their types; a line of one's own for the site fence.
      const nodes = await t.nodes();
      const typeOf = (cls) => nodes.find((n) => n.type === cls)?.id;
      await t.cmd('cost.setArticles', { type: typeOf('IfcSlabType'), articles: [{ code: 'BET01', basis: 'volume' }] });
      await t.cmd('cost.setArticles', { type: typeOf('IfcDoorType'), articles: [{ code: 'USA01' }] });
      await t.cmd('cost.setArticles', { type: typeOf('IfcWindowType'), articles: [{ code: 'FER01', basis: 'area' }] });
      // Cost → Estimate…: under the plan, a wall line clicked selecting its walls.
      await t.tool('cost.estimate');
      await t.wait(1500);
      await t.cmd('cost.addLine', { chapter: 'Site works', name: 'Împrejmuire șantier', unit: 'm', qty: 40, m: 25, l: 10 });
      await t.wait(1200);
      const est = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('cost.estimate'));
      const codes = est.chapters.flatMap((c) => c.lines.map((l) => l.code));
      t.expect(['ZID01', 'TEN01', 'BET01', 'USA01', 'FER01'].every((c) => codes.includes(c)), `every article priced (${codes.join(', ')})`);
      t.expect(est.chapters.find((c) => c.name === 'Walls')?.lines.length === 1 && est.chapters.find((c) => c.name === 'Finishes and insulation'), 'the walls priced layer by layer: masonry under Walls, plaster under Finishes');
      t.expect(est.totals.total > est.totals.net && est.totals.net > est.totals.direct, 'overheads, profit and VAT over the direct costs');
      await t.page.locator('[data-testid=estimate-line][data-code=ZID01]').click();
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=estimate-line].holds').count() >= 1, 'the line of the walls selected marked');
      await t.shot('estimate');
      // The F3 form of the same estimate.
      await t.page.locator('[data-testid=estimate-form-pick]').selectOption('f3');
      await t.wait(1200);
      const f3 = await t.page.locator('[data-testid=estimate-form] td').allTextContents();
      t.expect(f3.includes('TOTAL cu TVA') && f3.some((x) => x.startsWith('Capitolul')), 'the F3 lists the chapters and its totals');
      // The estimate alone in the window, to show the form whole.
      await t.page.evaluate(async () => { const { useUi } = await import('/src/app/store.ts'); const p = useUi.getState().panes; useUi.setState({ panes: [p[1]], pane: 0 }); });
      await t.wait(1000);
      await t.shot('estimate-f3', { of: '[data-testid=estimate]', pad: 0 });
      await t.page.locator('[data-testid=estimate-form-pick]').selectOption('estimate');
      await t.ui({ keyPlan: true });
    },
  },
  {
    // Cost over time (docs/plan-4d5d.md, stage 7): the estimate laid on the schedule with no linking of its own — each task's
    // cost in the Gantt's Cost column, the budget and the cost to date in its toolbar; the S-curve (planned, the baseline's,
    // the work done to the status date) over the months' bars and the month table; the curve placed on a sheet.
    id: 'cost-time',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const csv = 'Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\n'
        + 'ZID01;Zidărie din cărămidă 25 cm;mc;380,00;85,00;6,00;14,00\n'
        + 'TEN01;Tencuială de var pe pereți;mp;18,00;22,00;1,00;1,00\n'
        + 'BET01;Beton C25/30 în plăci;mc;410,00;95,00;22,00;18,00\n'
        + 'FER01;Fereastră PVC montată;mp;650,00;80,00;2,00;10,00\n';
      await t.cmd('cost.importPrices', { data: Buffer.from(csv, 'utf8').toString('base64'), name: 'Prețuri 2027.csv' });
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Lime plaster', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Lime plaster', thickness: 0.02 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
      }
      walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type })).id);
      for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8], [1, 12, 4]]) await t.cmd('architecture.addWindow', { wallId: walls[w], x, y });
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      const nodes = await t.nodes();
      const typeOf = (cls) => nodes.find((n) => n.type === cls)?.id;
      await t.cmd('cost.setArticles', { type, articles: [{ code: 'ZID01', part: 'core' }] });
      await t.cmd('cost.setRule', { material: nodes.find((n) => n.type === 'IfcMaterial' && n.name === 'Lime plaster').id, code: 'TEN01' });
      await t.cmd('cost.setArticles', { type: typeOf('IfcSlabType'), articles: [{ code: 'BET01', basis: 'volume' }] });
      await t.cmd('cost.setArticles', { type: typeOf('IfcWindowType'), articles: [{ code: 'FER01', basis: 'area' }] });
      await t.cmd('cost.createEstimate', { name: 'Estimate: House' });
      // The schedule, as the Planning scenes make it; the baseline, then the site at the status date.
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2027-03-01' });
      const add = async (name, duration, extra = {}) => {
        const id = (await t.cmd('planning.addTask', { name, duration })).id;
        if (Object.keys(extra).length) await t.cmd('planning.updateTask', { id, ...extra });
        return id;
      };
      const slab = await add('Floor slab', 6, { rule: { classes: ['IfcSlab'] }, wet: true });
      const masonry = await add('Masonry', 15, { rule: { classes: ['IfcWall'] }, part: 'core' });
      const joinery = await add('Windows', 4, { rule: { classes: ['IfcWindow'] } });
      const plaster = await add('Plaster', 12, { rule: { classes: ['IfcWall'] }, part: 'finishes', wet: true });
      await t.cmd('planning.link', { from: slab, to: masonry });
      await t.cmd('planning.link', { from: masonry, to: joinery });
      await t.cmd('planning.link', { from: joinery, to: plaster });
      await t.cmd('planning.saveBaseline', { date: '2027-02-26' });
      await t.cmd('planning.setProgress', { id: slab, actualStart: '2027-03-01', actualFinish: '2027-03-10' });
      await t.cmd('planning.setProgress', { id: masonry, actualStart: '2027-03-11', complete: 45 });
      await t.cmd('planning.updateSchedule', { dataDate: '2027-03-26' });
      // The Gantt: the Cost column, the budget and the cost to date.
      await open3d(t);
      await t.button('Construction', { exact: true });
      await t.tool('planning.gantt');
      await t.wait(1500);
      const sched = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      const row = (id) => sched.tasks.find((x) => x.id === id);
      t.expect(sched.cost && sched.cost.budget > 0 && Math.abs(sched.cost.scheduled - sched.cost.budget) < 0.01, 'every line of the estimate on a task');
      t.expect(row(masonry).cost > 0 && row(plaster).cost > 0 && Math.abs(row(masonry).costDone - row(masonry).cost * 0.45) < 0.02, "the masonry's cost, 45 % of it done");
      // The Gantt alone in the window, wide enough for its Cost column (a narrow pane drops it after the others).
      await t.page.evaluate(async () => { const { useUi } = await import('/src/app/store.ts'); const p = useUi.getState().panes; useUi.setState({ panes: [p[1]], pane: 0 }); });
      await t.wait(1000);
      await t.page.evaluate(async () => { const { usePanels } = await import('/src/app/panels.ts'); usePanels.setState({ browserOpen: false }); });
      await t.wait(800);
      await t.page.locator('[data-testid=gantt-fit]').click();
      await t.wait(800);
      t.expect(await t.page.locator('.gantt-th.c-cost').count() === 1, 'the Gantt has the Cost column');
      t.expect(await t.page.locator('[data-testid=gantt-cost]').count() === 1, 'the budget and the cost to date in the toolbar');
      await t.ui({ selection: masonry, selected: [masonry] });
      await t.wait(1200);
      await t.shot('planning-cost');
      await t.page.evaluate(async () => { const { usePanels } = await import('/src/app/panels.ts'); usePanels.setState({ browserOpen: true }); });
      // Cost → Cost over time: the S-curve and the month table.
      await t.ui({ selection: null, selected: [] });
      await t.button('Construction', { exact: true });
      await t.wait(300);
      await t.tool('cost.overTime');
      await t.page.locator('[data-testid=cost-chart]').waitFor({ timeout: 10_000 });
      await t.wait(800);
      const ot = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('cost.overTime'));
      t.expect(ot.planned.length > 20 && Math.abs(ot.planned[ot.planned.length - 1] - ot.budget) < 0.01, 'the planned curve ends at the budget');
      t.expect(ot.baseline && ot.done.length > 0 && Math.abs(ot.toDate - sched.cost.toDate) < 0.01, 'the baseline and the work done to the status date');
      t.expect(await t.page.locator('[data-testid=cost-curve-done]').count() === 1 && await t.page.locator('[data-testid=cost-curve-baseline]').count() === 1, 'the three curves drawn');
      t.expect(await t.page.locator('[data-testid=cost-time-table] tbody tr').count() >= 4, 'the month table by chapter');
      // The view alone in the window, to show it whole.
      await t.page.evaluate(async () => { const { useUi } = await import('/src/app/store.ts'); const p = useUi.getState().panes; const i = p.findIndex((x) => x.view === 'costTime'); useUi.setState({ panes: [p[i]], pane: 0 }); });
      await t.wait(1000);
      await t.shot('cost-time', { of: '[data-testid=cost-time]', pad: 0 });
      // The curve on a sheet, drawn by the engine.
      await t.ui({ keyPlan: false });
      await t.page.locator('[data-testid=cost-time-sheet]').click();
      await t.wait(2500);
      t.expect(await t.page.locator('[data-testid=sheet]').count() === 1, 'the sheet opens');
      await t.fit();
      const texts = await t.page.locator('[data-testid=sheet] text').allTextContents();
      t.expect(texts.some((x) => x.startsWith('Cost over time')) && texts.includes('Planned, cumulative'), 'the sheet draws the curve with its legend');
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      await t.wait(300);
      await t.shot('cost-curve-sheet', { of: '.view-pane:has([data-testid=sheet])', pad: 0 });
      await quiet.evaluate((el) => el.remove());
      await t.ui({ keyPlan: true });
    },
  },
  {
    id: 'wood',
    async run(t) {
      // The timber frame configurator (decision 239): the house's wall type made a timber frame, the sample wall framed, saved.
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.tool('wood.frame');
      const dialog = t.page.locator('[aria-label="Timber frame"]');
      t.expect(await dialog.count() > 0, 'the timber frame configurator opens');
      await dialog.locator('[data-testid=wood-on]').check();
      await t.wait(900);
      t.expect(await dialog.locator('svg[aria-label="Frame elevation"] polygon').count() > 20, 'the sample wall is framed');
      await t.shot('wood-frame', { of: '.dialog', pad: 0 });
      // Prefabricated panels (decision 245): a longer sample wall, its joints clear of the openings.
      await dialog.locator('[data-testid=wood-panels]').check();
      await t.wait(900);
      t.expect(await dialog.locator('svg[aria-label="Frame elevation"] line').count() >= 1, 'the sample wall is cut into panels');
      await t.shot('wood-panels', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-testid=wood-panels]').uncheck();
      await t.wait(600);
      await dialog.locator('.btn.primary').click();
      await t.wait(1200);
      const framed = await t.call('doc.query', { name: 'wood.wall', args: { id: walls[0] } });
      t.expect(framed?.framed && framed.counts.king > 0, `the house's walls are framed (${JSON.stringify(framed?.counts)})`);
      await t.ui({ dialog: null });
      // The frame elevation of the front wall, from its Wood section (decision 240).
      await select(t, walls[0]);
      await t.wait(600);
      await t.page.locator('[data-testid=wood-frame-view]').click();
      await t.wait(1500);
      t.expect((await t.nodes()).some((n) => n.type === 'LapinView' && n.props?.woodFrame === walls[0]), 'the front wall has its frame elevation');
      // CLT (decision 241): the configurator shows a sample wall in panels.
      await t.tool('wood.frame');
      await dialog.locator('[data-testid=wood-mode]').selectOption('clt');
      await t.wait(800);
      t.expect(await dialog.locator('svg[aria-label="CLT panels"] :is(rect, polygon)').count() > 3, 'the sample wall in CLT panels');
      // A floor type on joists (decision 244): the sample floor with its stairwell.
      await dialog.locator('button[data-class=IfcSlabType]').first().click();
      await t.wait(500);
      await dialog.locator('[data-testid=wood-mode]').selectOption('joists');
      await t.wait(900);
      t.expect(await dialog.locator('svg[aria-label="Floor joists"] polygon').count() > 10, 'the sample floor on joists');
      await t.shot('wood-joists', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').click();
      await t.wait(1000);
      t.expect((await t.nodes()).some((n) => n.type === 'IfcSlabType' && n.props?.wood?.construction === 'joists'), 'the floor type is on joists');
      await t.ui({ dialog: null });
    },
  },
  {
    // Steel, stage 1 (docs/plan-steel.md, decision 268): two outlines sketched on the plan, slanted at their ends, converted to
    // members; a column of the structure, a member fitted to its flange and coped; the members' ends in Properties; the types.
    id: 'steel',
    async run(t) {
      await t.fresh();
      const storey = await t.storey();
      const plan = `plan:${storey}`;
      await t.button('Steel', { exact: true });
      await t.shot('ribbon-steel', { of: '.topbar', pad: 0 });
      // Decision 458: the quick buttons before the view tabs (3D, Section, Measure), and the Steel tab's Column (an HEB 200 column type).
      await t.page.locator('[data-testid="quick-view.section"]').click();
      t.expect(await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().tool) === 'view.section', 'the quick Section button starts the section tool');
      await t.page.locator('[data-testid="quick-measure"]').click();
      t.expect(await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().tool) === 'measure', 'the quick Measure button starts the measure tool');
      await t.keys('Escape');
      await t.ui({ view: 'plan' });
      await t.wait(500);
      await t.tool('steel.column');
      const box = await t.page.locator('[data-testid=plan]').boundingBox();
      await t.page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
      await t.wait(800);
      const steelColumns = (await t.nodes()).filter((n) => n.type === 'IfcColumn');
      t.expect(steelColumns.length === 1 && steelColumns[0].name === 'Column HEB 200', `the Steel tab's Column places an HEB 200 column (${steelColumns.map((n) => n.name).join(', ')})`);
      await t.keys('Escape');
      await t.cmd('core.delete', { ids: steelColumns.map((n) => n.id) });
      await t.page.locator('[data-testid="quick-view.3d"]').click();
      t.expect(await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().view) === '3d', 'the quick 3D button opens the 3D view');
      await t.ui({ view: 'plan' });
      await t.wait(500);
      // The user's sketch: a band 150 mm wide (an IPE 300's flanges), both ends slanted; and a second one beside it, square.
      const slant = 0.15 * Math.tan(Math.PI / 6);
      const a = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'polyline', pts: [[0, 0], [6, 0], [6 + slant, 0.15], [slant, 0.15]], closed: true } })).ids[0];
      const b = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'polyline', pts: [[0, 2], [6, 2], [6, 2.15], [0, 2.15]], closed: true } })).ids[0];
      await t.ui({ keyPlan: false, selection: a, selected: [a, b] });
      await t.wait(600);
      await t.fit();
      await t.shot('steel-sketch', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('steel.convert');
      await t.wait(1200);
      const members = (await t.nodes()).filter((n) => n.type === 'LapinSteelMember');
      t.expect(members.length === 2, `the two outlines are members (${members.length})`);
      t.expect(!(await t.nodes()).some((n) => n.id === a || n.id === b), 'the sketches were replaced');
      const slanted = members.find((m) => m.props.steelEnds?.end?.cut === 'plane' || m.props.steelEnds?.start?.cut === 'plane');
      t.expect(slanted, 'the slanted ends are cuts by a plane');
      t.expect(members.every((m) => m.name === 'Member IPE 300'), `the outlines' width found the IPE 300 (${members.map((m) => m.name).join(', ')})`);
      // A column of HEA 200 at the far end, the square member fitted to its flange 10 mm off, its top flange coped.
      const hea = await t.cmd('structure.createColumnType', { section: 'HEA 200' });
      const column = await t.cmd('structure.addColumn', { storeyId: storey, typeId: hea.id, x: 7, y: 2.075 });
      const square = members.find((m) => m.id !== slanted?.id);
      await t.cmd('steel.setEnds', { ids: [square.id], end: { cut: 'fit', ref: column.id, extension: -0.01, copeLength: 0.12, copeDepth: 0.04, copeTop: true } });
      await t.ui({ selection: square.id, selected: [square.id] });
      await t.wait(1000);
      t.expect(await t.page.locator('[data-field=steel-ends]').count() > 0, 'the member shows its ends in Properties');
      t.expect(await t.page.locator('select[data-field=steel-end-cut]').inputValue() === 'fit', 'its end is fitted');
      await t.shot('steel-ends', { of: 'aside.props', pad: 0, maxHeight: 760 });
      // The steel types: the catalogue's profiles, hollow and cold-formed sections of one's own.
      await t.tool('steel.types');
      const dialog = t.page.locator('[data-dialog=steel-types]');
      t.expect(await dialog.count() > 0, 'the Steel types dialog opens');
      await dialog.locator('select[data-field=steel-profile]').selectOption('C 200×65×2');
      await t.wait(400);
      await t.shot('steel-types', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null, selection: null, selected: [] });
      await open3d(t);
      await t.shot('steel-3d', { of: 'canvas', pad: 0 });
      await t.ui({ keyPlan: true });
    },
  },
  {
    // A profile's real shape: steel at LOD 400 with its root fillets (decision 415); a curtain wall's mullions and a window's frame and
    // sash from an aluminium section with chambers (decision 417).
    id: 'profile-shapes',
    async run(t) {
      await t.fresh();
      const page = t.page;
      const s = await t.storey();
      const column = (await t.cmd('structure.createColumnType', { name: 'HEA 300', section: 'HEA 300', material: 'Steel' })).id;
      await t.cmd('structure.addColumn', { storeyId: s, typeId: column, x: 1, y: 1 });
      const beam = (await t.cmd('structure.createBeamType', { name: 'IPE 400', section: 'IPE 400', material: 'Steel' })).id;
      await t.cmd('structure.addBeam', { storeyId: s, typeId: beam, x1: 1, y1: 1, x2: 5, y2: 1 });
      await open3d(t);
      await t.cmd('view.setStyle', { id: '3d', detail: 400 });
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 0.4, pitch: -0.25, distance: 2.2, target: { x: 1.1, y: 1, z: 2.8 } }, false));
      await t.wait(1800);
      const fine = await page.evaluate(async () => (await import('/src/engine/session.ts')).engine.call('view.fine', { id: '3d' }));
      t.expect(fine.lod === 400 && fine.items.length === 2, 'at LOD 400 the column and the beam have their real sections');
      await t.shot('profile-real-section', { of: '[data-testid=view3d]', pad: 0 });
      // An aluminium section with chambers, 60 wide, 110 deep, read from a maker's DXF in real life.
      const alu = { name: 'ALU 60x110', kind: 'outline', material: 'aluminium', b: 0.06, h: 0.11, rings: [
        [[-0.03, -0.055], [0.03, -0.055], [0.03, 0.055], [0.008, 0.055], [0.008, 0.03], [-0.03, 0.03]],
        [[-0.02, -0.045], [-0.02, -0.01], [0.02, -0.01], [0.02, -0.045]], [[-0.02, 0.0], [-0.02, 0.02], [0.02, 0.02], [0.02, 0.0]],
      ] };
      const section = (await t.cmd('profiles.use', { profile: alu, library: 'Aluminium system' })).section;
      const curtain = await page.evaluate(async () => (await import('/src/engine/session.ts')).query('architecture.curtainWallPreview', {}));
      t.expect(curtain != null, 'the curtain wall preview answers');
      const defaults = await page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().info.catalogs['architecture.curtainWallDefault']);
      const cw = (await t.cmd('architecture.createCurtainWallType', { name: 'ALU facade', spec: { ...defaults, mullion: { width: 0.06, depth: 0.11, profile: section }, transom: { width: 0.06, depth: 0.11, profile: section } } })).id;
      await t.cmd('architecture.addCurtainWall', { storeyId: s, typeId: cw, x1: 8, y1: 0, x2: 12, y2: 0, height: 3 });
      const info = await page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().info.catalogs['architecture.joinery']);
      const type = (await t.cmd('architecture.createJoineryType', { spec: { ...info.defaultWindow, name: 'ALU 1200', frame: { ...info.defaultWindow.frame, width: 0.06, depth: 0.11 }, sash: { ...info.defaultWindow.sash, width: 0.06, depth: 0.11 }, frameSection: section, sashSection: section } })).id;
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, x1: 14, y1: 0, x2: 18, y2: 0 })).id;
      await t.cmd('architecture.addWindow', { wallId: wall, x: 16, y: 0, typeId: type });
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 0.5, pitch: -0.3, distance: 2.4, target: { x: 9.5, y: 0, z: 1.5 } }, false));
      await t.wait(1800);
      await t.shot('profile-real-mullions', { of: '[data-testid=view3d]', pad: 0 });
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 2.4, pitch: -0.2, distance: 1.8, target: { x: 16, y: 0, z: 1.6 } }, false));
      await t.wait(1500);
      await t.shot('profile-real-frame', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Profile libraries (docs/plan-profile-libraries.md): the built-in EN set drawn with its properties; a producer's table with German headings
    // matched and its wrong value flagged; an IFC profile library; a type made from the picker; a drafted outline with a hole kept as a profile.
    id: 'profiles',
    async run(t) {
      await t.fresh();
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFileSync } = await import('node:fs');
      await t.tool('manage.profileLibraries');
      const dialog = t.page.locator('[data-dialog=profile-libraries]');
      t.expect(await dialog.count() > 0, 'the Profile libraries dialog opens');
      await dialog.locator('[data-profile="HEA 200"]').waitFor({ timeout: 15000 });
      await dialog.locator('[data-profile="HEA 200"]').click();
      await dialog.locator('[data-testid=profile-props]').waitFor({ timeout: 10000 });
      const area = await dialog.locator('[data-testid=profile-props] tbody tr').first().locator('td.num').first().innerText();
      t.expect(Math.abs(Number(area.replace(',', '.')) - 53.83) < 0.5, `HEA 200's area is worked out (${area} cm²)`);
      await t.shot('profile-libraries', { of: '.dialog', pad: 0 });
      // A producer's table: German headings, one area wrong.
      const csv = join(tmpdir(), 'Stahlprofile.csv');
      writeFileSync(csv, '﻿Bezeichnung;h [mm];b [mm];s [mm];t [mm];r [mm];A [cm²];G [kg/m];Iy [cm4];iy [cm];Iz [cm4];It [cm4]\n'
        + 'IPE 300;300;150;7,1;10,7;15;53,81;42,2;8356;12,46;603,8;20,12\n'
        + 'HEA 200;190;200;6,5;10;18;53,83;42,3;3692;8,28;1336;20,98\n'
        + 'HEB 200;200;200;9;15;18;99,99;61,3;5696;8,54;2003;59,28\n');
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      let chooser = t.page.waitForEvent('filechooser', { timeout: 10000 });
      await dialog.locator('[data-testid=profile-import-table]').click();
      await (await chooser).setFiles(csv);
      await dialog.locator('[data-testid=profile-table-matching]').waitFor({ timeout: 10000 });
      const guessed = await dialog.locator('[data-testid^=profile-column-]').evaluateAll((els) => els.map((e) => e.value));
      t.expect(guessed.slice(0, 6).join(',') === 'name,h,b,tw,t,r1', `the columns guessed from the German headings (${guessed.join(',')})`);
      await t.shot('profile-table', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-testid=profile-table-go]').click();
      await dialog.locator('[data-testid=profile-report]').waitFor({ timeout: 10000 });
      t.expect((await dialog.locator('[data-testid=profile-report]').innerText()).includes('HEB 200'), 'the wrong area is on the check list');
      await dialog.locator('[data-profile="HEB 200"]').click();
      await dialog.locator('[data-testid=profile-props] tr.flagged').first().waitFor({ timeout: 10000 });
      await t.shot('profile-check', { of: '.dialog', pad: 0 });
      // An IFC profile library, as other programs write one.
      const ifc = join(tmpdir(), 'Firm profiles.ifc');
      writeFileSync(ifc, "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\nFILE_NAME('f','2026-01-01T00:00:00',(''),(''),'','','');\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n"
        + "#1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);\n#2=IFCUNITASSIGNMENT((#1));\n#3=IFCPROJECTLIBRARY('0abcdefghijklmnopqrstu',$,'Firm',$,$,$,$,$,#2);\n"
        + "#4=IFCISHAPEPROFILEDEF(.AREA.,'W14X90',$,368.,356.,11.2,18.,15.,$,$);\n#5=IFCRECTANGLEHOLLOWPROFILEDEF(.AREA.,'RHS 120x80x5',$,80.,120.,5.,5.,7.5);\n#6=IFCLSHAPEPROFILEDEF(.AREA.,'L 90x90x9',$,90.,90.,9.,11.,5.5,$);\nENDSEC;\nEND-ISO-10303-21;\n");
      chooser = t.page.waitForEvent('filechooser', { timeout: 10000 });
      await dialog.locator('[data-testid=profile-import-ifc]').click();
      await (await chooser).setFiles(ifc);
      await dialog.locator('[data-profile="W14X90"]').waitFor({ timeout: 15000 });
      t.expect(await dialog.locator('[data-profile^="L 90"]').count() > 0, 'the IFC file\'s profiles are a library');
      await dialog.locator('[data-profile="W14X90"]').click();
      await dialog.locator('[data-testid=profile-props]').waitFor({ timeout: 10000 });
      await t.wait(400);
      await t.shot('profile-ifc', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // The Steel types' picker searches them all; the type made keeps the project's copy.
      await t.button('Steel', { exact: true });
      await t.tool('steel.types');
      const types = t.page.locator('[data-dialog=steel-types]');
      await types.locator('select[data-field=steel-profile]').selectOption('@library');
      await types.locator('[data-testid=steel-library-profile-search]').fill('W14');
      await types.locator('[data-profile="W14X90"]').waitFor({ timeout: 10000 });
      await t.shot('profile-picker', { of: '.dialog', pad: 0 });
      await types.locator('[data-profile="W14X90"]').dispatchEvent('mousedown');
      await t.wait(500);
      await types.locator('[data-field=steel-create-type]').click();
      await t.wait(800);
      const nodes = await t.nodes();
      const copy = nodes.find((n) => n.type === 'LapinSectionProfile');
      t.expect(copy?.name === 'W14X90', 'the profile was copied into the project');
      t.expect(nodes.some((n) => n.type === 'LapinSteelMemberType' && n.props.section?.profileId === copy?.id), 'the member type keeps its copy');
      await t.ui({ dialog: null });
      // A drafted outline with a hole, made a profile.
      const storey = await t.storey();
      const plan = `plan:${storey}`;
      const rect = async (x0, y0, x1, y1) => (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'polyline', pts: [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], closed: true } })).ids[0];
      const ids = [await rect(0, 0, 0.1, 0.06), await rect(0.005, 0.005, 0.095, 0.055)];
      await t.ui({ selection: ids[0], selected: ids });
      await t.tool('manage.profileLibraries');
      await dialog.locator('[data-testid=profile-from-selection]').click();
      await dialog.locator('[data-testid=profile-outline]').waitFor({ timeout: 10000 });
      await dialog.locator('[data-testid=profile-outline-name]').fill('Alu 100×60×5');
      await t.wait(300);
      await t.shot('profile-outline', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-testid=profile-outline-keep]').click();
      await dialog.locator('[data-profile="Alu 100×60×5"]').waitFor({ timeout: 10000 });
      await t.ui({ dialog: null, selection: null, selected: [] });
    },
  },
  {
    // Profile standards (a tester's request, decision 457): the user's own AISC-style table (its Standard column says ASTM A6)
    // beside Lapin's EN set and a firm's library of its own; the picker grouped under the standards' headings with their chips,
    // EN hidden by its chip; the standards the project offers; those a new project offers in Preferences → Profiles.
    id: 'profile-standards',
    touches: ['core', 'steel'],
    async run(t) {
      await t.fresh();
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFileSync } = await import('node:fs');
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.tool('manage.profileLibraries');
      const dialog = t.page.locator('[data-dialog=profile-libraries]');
      await dialog.locator('[data-profile="HEA 200"]').waitFor({ timeout: 15000 });
      const importCsv = async (file, text) => {
        const path = join(tmpdir(), file);
        writeFileSync(path, text);
        const chooser = t.page.waitForEvent('filechooser', { timeout: 10000 });
        await dialog.locator('[data-testid=profile-import-table]').click();
        await (await chooser).setFiles(path);
        await dialog.locator('[data-testid=profile-table-matching]').waitFor({ timeout: 10000 });
        await dialog.locator('[data-testid=profile-table-go]').click();
        await dialog.locator('[data-testid=profile-report]').waitFor({ timeout: 10000 });
      };
      // Your own copy of an American table (Lapin ships none: its licence does not let Lapin pass it on).
      await importCsv('My AISC shapes.csv', 'Type,AISC_Manual_Label,W,A,d,bf,tw,tf,Ix,Iy,Standard\n'
        + 'W,W14X90,90,26.5,14,14.5,0.44,0.71,999,362,ASTM A6\n'
        + 'W,W14X120,120,35.3,14.5,14.7,0.59,0.94,1380,495,ASTM A6\n'
        + 'W,W12X40,40,11.7,11.9,8.01,0.295,0.515,307,44.1,ASTM A6\n');
      // A firm's own library: no standard, its own group.
      await importCsv('Firm sections.csv', 'Type;Name;h [mm];b [mm];s [mm];t [mm];r [mm]\nI;Firm I 140;140;90;6;9;8\nI;Firm I 160;160;100;6;10;8\n');
      await dialog.locator('[data-profile="Firm I 140"]').waitFor({ timeout: 10000 });
      await t.ui({ dialog: null });
      // The steel types' picker: the project's copies, then EN, AISC / ASTM, the firm's, each under its heading.
      await t.button('Steel', { exact: true });
      await t.tool('steel.types');
      const types = t.page.locator('[data-dialog=steel-types]');
      await types.locator('select[data-field=steel-profile]').selectOption('@library');
      const search = types.locator('[data-testid=steel-library-profile-search]');
      await search.fill('14');
      await types.locator('[data-profile="W14X90"]').waitFor({ timeout: 10000 });
      const heads = await types.locator('.profile-group-head').evaluateAll((els) => els.map((e) => e.getAttribute('data-group')));
      t.expect(heads.join('|') === 'EN|AISC / ASTM|Firm sections', `the list is grouped by standard (${heads.join(', ')})`);
      t.expect(await types.locator('[data-testid=profile-picker-standards] [data-standard]').count() === 3, 'a chip for each standard');
      await t.shot('profile-picker-standards', { of: '.dialog', pad: 0 });
      // EN hidden by its chip: kept for this user.
      await types.locator('[data-testid=profile-picker-standards] [data-standard="EN"]').click();
      await t.wait(300);
      t.expect(await types.locator('[data-profile="IPE 140"]').count() === 0 && await types.locator('[data-profile="W14X90"]').count() === 1, 'EN hidden, AISC shown');
      t.expect(await t.page.evaluate(() => JSON.parse(localStorage.getItem('lapin.prefs') ?? '{}').profileStandardsHidden?.includes('EN')), 'the choice is kept in the preferences');
      await types.locator('[data-testid=profile-picker-standards] [data-standard="EN"]').click();
      await search.blur();
      await t.ui({ dialog: null });
      // The standards this project offers: AISC / ASTM taken out of it.
      await t.tool('manage.profileLibraries');
      await dialog.locator('[data-testid=profile-project-standards]').waitFor({ timeout: 10000 });
      await dialog.locator('[data-testid=profile-project-standards] input[data-standard="AISC / ASTM"]').click();
      await t.until(async () => (await t.nodes()).some((n) => n.type === 'IfcProject' && Array.isArray(n.props.profileStandards) && !n.props.profileStandards.includes('AISC / ASTM')), 'the project offers EN and the firm\'s only');
      await dialog.locator('[data-testid=profile-search]').fill('14');
      await t.wait(300);
      await t.shot('profile-project-standards', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // Preferences → Profiles: a new project offers EN only.
      await t.ui({ dialog: 'preferences' });
      await t.page.waitForSelector('[data-testid=preferences]', { timeout: 10_000 });
      await t.page.locator('[data-field=prefs-profiles]').click();
      const fresh = t.page.locator('[data-testid=prefs-new-project-standards]');
      await fresh.waitFor({ timeout: 10000 });
      await fresh.locator('input[data-standard="AISC / ASTM"]').uncheck();
      await fresh.locator('input[data-standard="Firm sections"]').uncheck();
      await t.wait(200);
      t.expect(await t.page.evaluate(() => JSON.stringify(JSON.parse(localStorage.getItem('lapin.prefs') ?? '{}').newProjectProfileStandards)) === '["EN"]', 'new projects will offer EN only');
      await t.shot('prefs-profiles', { of: '.prefs-dialog', pad: 0 });
      await fresh.getByRole('button', { name: 'All' }).click();
      await t.ui({ dialog: null });
    },
  },
  {
    // Steel, stage 2 (docs/plan-steel.md, decision 270): the portal frame hall configurator — a catalogue style, the haunch
    // at the eaves welded from plates, the hall made in one step; a rafter's haunches in Properties; the hall edited again.
    id: 'steel-hall',
    async run(t) {
      await t.fresh();
      await t.button('Steel', { exact: true });
      await t.tool('steel.hall');
      const dialog = t.page.locator('[data-dialog=steel-hall]');
      t.expect(await dialog.count() > 0, 'the hall configurator opens');
      await dialog.locator('[data-field=hall-style-hall-20]').click();
      // The preview builds the whole hall, its secondary steel and connections too.
      await t.page.waitForFunction(() => document.querySelectorAll('[data-dialog=steel-hall] svg[data-field=hall-elevation] polygon').length >= 12, null, { timeout: 15000 }).catch(() => {});
      t.expect(await dialog.locator('svg[data-field=hall-elevation] polygon').count() >= 12, 'the preview draws a typical frame with its haunches');
      t.expect(await dialog.locator('[data-field=hall-summary] dt').count() >= 9, 'the summary weighs the hall');
      await t.shot('steel-hall', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-tab=hall-haunches]').click();
      await dialog.locator('select[data-field=hall-eaves-kind]').selectOption('plates');
      await t.wait(1000);
      await t.shot('steel-hall-haunches', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-tab=hall-gables]').click();
      await t.wait(800);
      t.expect(await dialog.locator('svg[data-field=hall-elevation] polygon[data-role=post]').count() === 5, 'the Gables tab draws the gable: five posts under two rafters');
      await t.shot('steel-hall-gable', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-tab=hall-placement]').click();
      await t.wait(800);
      t.expect(await dialog.locator('svg[data-field=hall-plan] line').count() === 8, 'the plan shows the hall\'s grid: six frames, two column lines');
      await dialog.locator('[data-field=hall-apply]').click();
      await t.wait(2500);
      let nodes = await t.nodes();
      const hall = nodes.find((n) => n.type === 'LapinSteelHall');
      t.expect(hall, 'the hall is made');
      const members = nodes.filter((n) => /^f\d/.test(n.props?.hallRole ?? ''));
      t.expect(members.length === 30, `its frames, gables and posts (${members.length})`);
      const rafter = members.find((n) => n.props.hallRole === 'f1:raf:a');
      t.expect(rafter?.props.haunches?.start?.kind === 'plates' && rafter?.props.haunches?.end?.kind === 'cut', 'the eaves haunch welded from plates, the apex one cut from the rafter');
      await t.ui({ dialog: null, keyPlan: false });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.3, pitch: -0.45, distance: 52, target: { x: 10, y: 15, z: 3 } }, false));
      await t.wait(1500);
      await t.shot('steel-hall-3d', { of: 'canvas', pad: 0 });
      // A rafter selected: its haunches in Properties, and its hall opened again from there.
      await select(t, rafter.id);
      await t.wait(1000);
      t.expect(await t.page.locator('select[data-field=steel-haunch-start-kind]').inputValue() === 'plates', 'the rafter shows its haunches');
      await t.shot('steel-hall-props', { of: 'aside.props', pad: 0, maxHeight: 760 });
      await t.page.locator('[data-field=steel-edit-hall]').click();
      await t.wait(1000);
      t.expect(await dialog.count() > 0, 'Edit hall opens the configurator on the hall');
      const span = dialog.locator('input[data-field=hall-span]');
      await t.until(async () => await span.inputValue() === '20000' && await dialog.locator('[data-field=hall-summary] dt').count() > 0, 'the hall\'s own spec in the configurator');
      await span.fill('24000');
      await span.press('Enter');
      await t.until(() => dialog.locator('[data-field=hall-apply]').isEnabled(), 'Apply offered for the changed span');
      await dialog.locator('[data-field=hall-apply]').click();
      await t.until(async () => {
        nodes = await t.nodes();
        const again = nodes.find((n) => n.id === rafter.id);
        return again && Math.abs((again.props.end?.[0] ?? 0) - 12) < 1e-6;
      }, 'the hall is built again over the same members, 24 m wide');
      await t.ui({ dialog: null, selection: null, selected: [], keyPlan: true });
    },
  },
  {
    // Steel, stage 3 (docs/plan-steel.md, decision 273): the frames' connections — the Nodes tab of the hall configurator
    // (fixed bases on hooked anchors), the hall made with its column bases, eaves, apexes and gable posts' fin plates; an
    // eaves and a base in 3D; a connection's parameters and parts in Properties.
    id: 'steel-nodes',
    async run(t) {
      await t.fresh();
      await t.button('Steel', { exact: true });
      await t.tool('steel.hall');
      const dialog = t.page.locator('[data-dialog=steel-hall]');
      await dialog.locator('[data-field=hall-style-hall-20]').click();
      await t.wait(1200);
      await dialog.locator('[data-tab=hall-nodes]').click();
      await dialog.locator('select[data-field=hall-base]').selectOption('fixed');
      await dialog.locator('select[data-field=hall-anchor]').selectOption('hooked');
      await t.wait(1500);
      await t.until(async () => await dialog.locator('svg[data-field=hall-elevation] polygon[data-role=node]').count() > 20, 'the preview draws the frame\'s connections');
      t.expect(await dialog.locator('[data-field=hall-summary] dt').count() >= 12, 'the summary counts the connections, their bolts and anchors');
      await t.shot('steel-nodes-hall', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-field=hall-apply]').click();
      await t.wait(3000);
      const nodes = await t.nodes();
      const conns = nodes.filter((n) => n.type === 'LapinSteelNode' && ['base', 'eaves', 'apex', 'post'].includes(n.props.kind));
      t.expect(conns.length === 42, `the hall's connections: bases, eaves, apexes, gable posts (${conns.length})`);
      const eaves = conns.find((n) => n.props.hallNode === 'eaves@f1:raf:a@start');
      const base = conns.find((n) => n.props.hallNode === 'base@f1:col:a@start');
      t.expect(eaves && base, 'an eaves and a column base, each in its place');
      await t.ui({ dialog: null, keyPlan: false });
      await open3d(t);
      const orbit = (view) => t.page.evaluate(async (v) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo(v, false), view);
      // The canvas runs under the quick tools: the target is drawn low, so it stands under what is shown.
      await orbit({ azimuth: 0.7, pitch: -0.25, distance: 4.5, target: { x: 0.1, y: 6, z: 4.7 } });
      await t.wait(1500);
      await t.shot('steel-nodes-eaves', { of: 'canvas', pad: 0 });
      await orbit({ azimuth: 0.9, pitch: -0.6, distance: 3.2, target: { x: 0, y: 6, z: -0.9 } });
      await t.wait(1500);
      await t.shot('steel-nodes-base', { of: 'canvas', pad: 0 });
      // The eaves selected: its parameters and parts in Properties.
      await select(t, eaves.id);
      await t.wait(1200);
      t.expect(await t.page.locator('[data-field=steel-node]').count() > 0, 'the connection shows in Properties');
      t.expect(await t.page.locator('[data-field=node-parts] dt').count() >= 4, 'its plates, bolts, welds and weight');
      await t.page.locator('select[data-field=node-cols]').selectOption('4');
      await t.wait(1500);
      const after = (await t.nodes()).find((n) => n.id === eaves.id);
      t.expect(after?.props.params?.cols === 4 && after?.props.own === true, 'four bolts a row, set on the connection itself');
      await t.shot('steel-nodes-props', { of: 'aside.props', pad: 0, maxHeight: 760 });
      await t.ui({ dialog: null, selection: null, selected: [], keyPlan: true });
    },
  },
  {
    // Steel, stage 4 (docs/plan-steel.md, decision 275): the hall's secondary steel — the Secondary steel tab (an eaves beam,
    // bays 1 and 5 braced with angles), the plan of the preview with the bracing; the hall in 3D with its purlins, side rails
    // and bracing; a purlin's cleat with a fly brace, and the bracing's gussets, close up.
    id: 'steel-secondary',
    async run(t) {
      await t.fresh();
      await t.button('Steel', { exact: true });
      await t.tool('steel.hall');
      const dialog = t.page.locator('[data-dialog=steel-hall]');
      await dialog.locator('[data-field=hall-style-hall-20]').click();
      await t.wait(1200);
      await dialog.locator('[data-tab=hall-secondary]').click();
      await dialog.locator('input[data-field=hall-eavesBeam]').check();
      const bracing = dialog.locator('input[data-field=hall-bracing]');
      await bracing.fill('1, 5');
      await bracing.press('Tab');
      await dialog.locator('select[data-field=hall-brace]').selectOption('L 60×60×6');
      await t.wait(2500);
      await t.until(async () => await dialog.locator('svg[data-field=hall-plan] polygon[data-role=brace]').count() >= 16, 'the plan shows the roof bracing of both braced bays');
      t.expect(await dialog.locator('svg[data-field=hall-plan] polygon[data-role=purlin]').count() >= 80, 'and the purlins');
      t.expect(await dialog.locator('[data-field=hall-summary] dt').count() >= 15, 'the summary weighs the purlins, rails, eaves beam, bracing and fly braces');
      await t.shot('steel-secondary-hall', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-field=hall-apply]').click();
      await t.wait(5000);
      const nodes = await t.nodes();
      const role = (p) => nodes.filter((n) => (n.props?.hallRole ?? '').startsWith(p));
      t.expect(role('pur:').length === 80, `80 purlins, one a bay (${role('pur:').length})`);
      t.expect(role('rbr:').length === 16 && role('wbr:').length === 8, `the bracing of bays 1 and 5 (${role('rbr:').length}, ${role('wbr:').length})`);
      t.expect(role('eb:').length === 10, 'an eaves beam on each long wall');
      const cleats = nodes.filter((n) => n.type === 'LapinSteelNode' && n.props.kind === 'cleat');
      t.expect(cleats.length > 200, `every purlin and rail on its cleats (${cleats.length})`);
      await t.ui({ dialog: null, keyPlan: false });
      await open3d(t);
      const orbit = (view) => t.page.evaluate(async (v) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo(v, false), view);
      await orbit({ azimuth: -2.3, pitch: -0.45, distance: 50, target: { x: 10, y: 15, z: 3 } });
      await t.wait(2500);
      await t.shot('steel-secondary-3d', { of: 'canvas', pad: 0 });
      // A purlin on its cleat over the rafter of frame 3, a fly brace down to the rafter's bottom flange.
      await orbit({ azimuth: 0.8, pitch: -0.35, distance: 2.4, target: { x: 4.4, y: 12, z: 6.5 } });
      await t.wait(2000);
      await t.shot('steel-secondary-cleat', { of: 'canvas', pad: 0 });
      // The roof bracing's gussets at a rafter of the first braced bay.
      await orbit({ azimuth: -0.5, pitch: 0.55, distance: 4, target: { x: 5, y: 0.8, z: 5.9 } });
      await t.wait(2000);
      await t.shot('steel-secondary-bracing', { of: 'canvas', pad: 0 });
      await t.ui({ dialog: null, selection: null, selected: [], keyPlan: true });
    },
  },
  {
    // Steel, stage 5 (docs/plan-steel.md, decision 276): a hall's anchor plan (its bases, anchors dimensioned to the grid,
    // the table of base types) and a frame's elevation, from the Steel tab; the Steel members list.
    id: 'steel-drawings',
    async run(t) {
      await t.fresh();
      const ground = (await t.nodes()).find((n) => n.type === 'IfcBuildingStorey' && Math.abs(n.props?.elevation ?? 1) < 1e-6);
      const { id: hall } = await t.cmd('steel.addHall', { storeyId: ground?.id, style: 'hall-20', attrs: {} });
      await t.button('Steel', { exact: true });
      await t.tool('steel.anchorPlan');
      await t.wait(3000);
      const views = (await t.nodes()).filter((n) => n.type === 'LapinView');
      t.expect(views.some((v) => v.props?.steelAnchors === hall), 'the hall has its anchor plan');
      t.expect(await t.page.locator('[data-testid=drawing]').count() > 0, 'the anchor plan opens as a drawing');
      // Zoomed in on the base at axes A and 2 (a wheel event zooms one notch at most).
      const zoomAt = async (fx, fy, notches) => {
        const box = await t.page.locator('[data-testid=drawing]').boundingBox();
        await t.page.mouse.move(box.x + fx * box.width, box.y + fy * box.height);
        for (let i = 0; i < notches; i++) { await t.page.mouse.wheel(0, -100); await t.wait(80); }
        await t.wait(800);
      };
      const fitDrawing = async () => { await t.page.locator('button:has(svg.lucide-maximize):visible').first().click(); await t.wait(800); };
      await fitDrawing();
      await zoomAt(0.232, 0.726, 32);
      await t.shot('steel-anchor-plan', { of: '[data-testid=drawing]', pad: 0 });
      await t.tool('steel.frameView');
      await t.wait(3000);
      t.expect((await t.nodes()).some((v) => v.type === 'LapinView' && v.props?.steelFrame === hall), 'and a frame\'s elevation');
      await fitDrawing();
      await t.shot('steel-frame-view', { of: '[data-testid=drawing]', pad: 0 });
      const schedule = await t.cmd('architecture.createSchedule', { kind: 'steelMembers' });
      await t.ui({ view: 'schedule', scheduleId: schedule.id, keyPlan: false });
      await t.wait(1500);
      t.expect(await t.page.locator('.schedule-grid').count() > 0, 'the Steel members list');
      await t.shot('steel-members', { of: '.schedule-pane', pad: 0, maxHeight: 760 });
      await t.ui({ view: 'plan', keyPlan: true });
    },
  },
  {
    // Planning (docs/plan-4d5d.md, stage 1): a house's schedule in the Gantt under the view, a task in Properties, the calendar.
    id: 'planning',
    async run(t) {
      await t.fresh();
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2026-11-16' });
      const add = async (name, duration, extra = {}) => (await t.cmd('planning.addTask', { name, duration, ...extra })).id;
      const site = await add('Site set-up', 3);
      const found = await add('Foundations', 1);
      const exc = await add('Excavation', 4, { parent: found });
      const foot = await add('Footings', 6, { parent: found });
      const struct = await add('Structure', 1);
      const walls = await add('Ground floor walls', 10, { parent: struct });
      const slab = await add('Floor slab', 6, { parent: struct });
      const roof = await add('Roof', 8, { parent: struct });
      const env = await add('Envelope and finishes', 1);
      const windows = await add('Windows and doors', 4, { parent: env });
      const plaster = await add('Plaster', 8, { parent: env });
      const paint = await add('Floors and paint', 10, { parent: env });
      const handover = (await t.cmd('planning.addTask', { name: 'Handover', milestone: true })).id;
      for (const id of [foot, slab, plaster]) await t.cmd('planning.updateTask', { id, wet: true });
      const link = (from, to, type = 'FS', lag = 0) => t.cmd('planning.link', { from, to, type, lag });
      await link(site, exc);
      await link(exc, foot, 'SS', 2);
      await link(foot, walls, 'FS', 3);
      await link(walls, slab);
      await link(slab, roof);
      await link(roof, windows, 'SS', 3);
      await link(windows, plaster);
      await link(plaster, paint, 'SS', 4);
      await link(paint, handover);
      await link(roof, handover);
      await t.button('Construction', { exact: true });
      await t.shot('ribbon-planning', { of: '.topbar', pad: 0 });
      await t.tool('planning.gantt');
      await t.wait(1500);
      t.expect(await t.page.locator('[data-testid=gantt]').count() > 0, 'the Gantt opens under the view');
      t.expect(await t.page.locator('.gantt-row').count() === 13, 'every task has its row');
      const s = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      t.expect(s.tasks.filter((x) => x.critical && !x.summary).length >= 6, 'the critical path runs through the structure');
      t.expect(s.tasks.find((x) => x.name === 'Handover').start > '2027-01-01', 'the holidays and the weekends push the handover into 2027');
      await t.ui({ selection: plaster, selected: [plaster] });
      await t.wait(800);
      await t.shot('planning-gantt');
      await t.shot('planning-task', { of: '[data-testid=task-section]', pad: 8 });
      await t.tool('planning.calendar');
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=calendar-holiday-list] li').count() >= 16, 'the calendar lists the year\'s holidays');
      await t.shot('planning-calendar', { of: '[data-testid=calendar-dialog]', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    // Planning, stage 2 (docs/plan-4d5d.md): a house's elements in the tasks by rules (the walls' core in one, their finishes in
    // another), a task selected showing its elements in 3D, the rule in Properties, the selection dragged onto a row, TA.
    id: 'planning-links',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Lime plaster', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Gypsum plaster', thickness: 0.015 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
      }
      walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type })).id);
      const door = (await t.cmd('architecture.addDoor', { wallId: walls[0], x: 3, y: 0 })).id;
      for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8], [1, 12, 4]]) await t.cmd('architecture.addWindow', { wallId: walls[w], x, y });
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2026-11-16' });
      const add = async (name, duration, extra = {}) => {
        const id = (await t.cmd('planning.addTask', { name, duration })).id;
        if (Object.keys(extra).length) await t.cmd('planning.updateTask', { id, ...extra });
        return id;
      };
      const slab = await add('Floor slab', 6, { rule: { classes: ['IfcSlab'] }, wet: true });
      const masonry = await add('Masonry', 10, { rule: { classes: ['IfcWall'], storeys: [s] }, part: 'core' });
      const joinery = await add('Windows and doors', 4, { rule: { classes: ['IfcWindow'] } });
      const plaster = await add('Plaster', 8, { rule: { classes: ['IfcWall'] }, part: 'finishes', wet: true });
      await t.cmd('planning.link', { from: slab, to: masonry });
      await t.cmd('planning.link', { from: masonry, to: joinery, type: 'SS', lag: 5 });
      await t.cmd('planning.link', { from: joinery, to: plaster });
      await open3d(t);
      await t.tool('planning.gantt');
      await t.wait(1500);
      let sched = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      const row = (id) => sched.tasks.find((x) => x.id === id);
      t.expect(row(masonry).count === 5 && row(plaster).count === 5 && row(joinery).count === 4 && row(slab).count === 1, 'the rules find the walls, windows and slab');
      t.expect(sched.elements.none === 1, 'the door has no task yet');
      // A task selected: its elements drawn selected in 3D, its rule in Properties.
      await t.ui({ selection: masonry, selected: [masonry] });
      await t.wait(1000);
      await t.shot('planning-links');
      await t.page.locator('[data-testid=task-links]').scrollIntoViewIfNeeded();
      await t.wait(300);
      await t.shot('planning-task-elements', { of: '[data-testid=task-links]', pad: 8 });
      // The door selected: the card to drag it from, the rows of its tasks marked (none yet); dragged onto Windows and doors.
      await t.ui({ selection: door, selected: [door], pane: 0 });
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=selection-to-task]').count() === 1, 'the selection card shows over the 3D');
      await t.page.locator('[data-testid=selection-drag]').dragTo(t.page.locator(`.gantt-row[data-task="${joinery}"]`));
      await t.wait(1200);
      sched = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      t.expect(row(joinery).count === 5 && row(joinery).added === 1 && sched.elements.none === 0, 'the door dropped on its row joins the task');
      await t.ui({ selection: walls[1], selected: [walls[1]], pane: 0 });
      await t.wait(1000);
      t.expect(await t.page.locator('.gantt-row.holds').count() === 2, "a wall's two tasks marked in the Gantt");
      await t.shot('planning-element-tasks');
      // TA: the same from a list.
      await t.tool('planning.assign');
      await t.wait(600);
      await t.shot('planning-assign', { of: '[data-testid=assign-dialog]', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    // Planning, stage 3 (docs/plan-4d5d.md): a two-storey concrete-frame house generated into a schedule — the configurator
    // with its templates, sequence and durations (Lapin's indicative norms), the preview and its counter of linked elements,
    // then the Gantt under the 3D, a generated task in Properties, the formwork's days on site.
    id: 'planning-generate',
    async run(t) {
      await t.fresh();
      const gf = await t.storey('Ground floor');
      const ff = await t.storey('First floor');
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Lime plaster', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Gypsum plaster', thickness: 0.015 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const outer = {};
      for (const s of [gf, ff]) {
        outer[s] = [];
        for (let i = 0; i < 4; i++) {
          const [a, b] = [outline[i], outline[(i + 1) % 4]];
          outer[s].push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
        }
        await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type });
        for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8]]) await t.cmd('architecture.addWindow', { wallId: outer[s][w], x, y });
        for (const [x, y] of outline) await t.cmd('structure.addColumn', { storeyId: s, x, y });
      }
      await t.cmd('architecture.addDoor', { wallId: outer[gf][0], x: 3, y: 0 });
      await t.cmd('architecture.addSlab', { storeyId: gf, points: outline });
      const floor = (await t.cmd('architecture.addSlab', { storeyId: ff, points: outline })).id;
      const roof = await t.cmd('architecture.addRoof', { storeyId: ff, points: outline });
      await t.cmd('architecture.roofStyle', { id: roof.id, style: 'ro_saxon' });
      for (const [x, y] of outline) await t.cmd('structure.addPadFooting', { storeyId: gf, x, y });
      await t.cmd('sitework.addFormwork', { hostIds: [floor] });
      await t.cmd('sitework.scaffoldFromWalls', { wallIds: outer[gf] });
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2027-03-01' });
      await t.cmd('planning.useIndicativeNorms', {});
      await open3d(t);
      await t.button('Construction', { exact: true });
      await t.tool('planning.generate');
      const dialog = t.page.locator('[data-dialog=planning-generate]');
      t.expect(await dialog.count() > 0, 'the generator opens');
      await t.page.waitForFunction(() => document.querySelectorAll('[data-dialog=planning-generate] .gen-bar-row').length >= 10, null, { timeout: 20000 }).catch(() => {});
      t.expect(await dialog.locator('.gen-bar-row').count() >= 10, 'the preview lists the tasks it would make');
      t.expect(await dialog.locator('[data-field=gen-template-concrete] .muted').count() === 1, 'the columns make the concrete frame the suggested template');
      const linked = (await dialog.locator('[data-field=gen-linked]').textContent()) ?? '';
      const [a, b] = linked.split('/').map((x) => Number(x.trim()));
      t.expect(a > 0 && a === b, `every element linked (${linked})`);
      await t.shot('planning-generate', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-tab=gen-sequence]').click();
      await t.wait(600);
      await t.shot('planning-generate-sequence', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-tab=gen-durations]').click();
      await t.wait(600);
      t.expect(await dialog.locator('[data-field=gen-indicative]').count() === 1, "Lapin's indicative values say so");
      await t.shot('planning-generate-durations', { of: '.dialog', pad: 0 });
      // Applied in the picture's language, so the tasks are named in it.
      const setLang = (l) => t.page.evaluate(async (l) => (await import('/src/i18n/index.ts')).setLang(l), l);
      if (t.lang !== 'en') { await setLang(t.lang); await t.wait(800); }
      await dialog.locator('[data-field=gen-apply]').click();
      await t.wait(2500);
      if (t.lang !== 'en') await setLang('en');
      const sched = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      t.expect(sched.tasks.length >= 10 && sched.elements.none === 0, 'the schedule is made, every element in a task');
      t.expect(sched.tasks.some((x) => x.durationFrom === 'norm'), 'durations from the norms');
      t.expect((sched.temporaries ?? []).length === 2, 'the formwork and the scaffold live by their hosts\' tasks');
      t.expect(await t.page.locator('[data-testid=gantt]').count() > 0, 'the Gantt opens under the 3D');
      const masonry = sched.tasks.find((x) => x.gen === `masonry/${gf}`);
      t.expect(t.lang === 'en' || masonry.name !== 'Masonry · Ground floor', 'the tasks are named in the interface\'s language');
      await t.ui({ selection: masonry.id, selected: [masonry.id] });
      await t.wait(1200);
      await t.shot('planning-generated');
      await t.shot('planning-generated-task', { of: '[data-testid=task-section]', pad: 8 });
      await t.ui({ selection: null, selected: [] });
    },
  },
  {
    // Planning, stage 4 (docs/plan-4d5d.md): the 4D simulation — a generated two-storey house shown on a day of its
    // schedule (the first floor's structure under way, its formwork up, the walls above not started), the player at the
    // Gantt's top and its settings; a plan at a date in its properties; the stages sheet; the Gantt on a sheet; the video.
    id: 'planning-simulation',
    async run(t) {
      await t.fresh();
      const gf = await t.storey('Ground floor');
      const ff = await t.storey('First floor');
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Lime plaster', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Gypsum plaster', thickness: 0.015 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const outer = {};
      for (const s of [gf, ff]) {
        outer[s] = [];
        for (let i = 0; i < 4; i++) {
          const [a, b] = [outline[i], outline[(i + 1) % 4]];
          outer[s].push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
        }
        await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type });
        for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8]]) await t.cmd('architecture.addWindow', { wallId: outer[s][w], x, y });
        for (const [x, y] of outline) await t.cmd('structure.addColumn', { storeyId: s, x, y });
      }
      await t.cmd('architecture.addDoor', { wallId: outer[gf][0], x: 3, y: 0 });
      await t.cmd('architecture.addSlab', { storeyId: gf, points: outline });
      const floor = (await t.cmd('architecture.addSlab', { storeyId: ff, points: outline })).id;
      for (const [x, y] of outline) await t.cmd('structure.addPadFooting', { storeyId: gf, x, y });
      await t.cmd('sitework.addFormwork', { hostIds: [floor] });
      await t.cmd('sitework.scaffoldFromWalls', { wallIds: outer[gf] });
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2027-03-01' });
      await t.cmd('planning.useIndicativeNorms', {});
      // Generated with the tasks named in the picture's language.
      await t.page.evaluate(async (lang) => {
        const [s, i, g] = [await import('/src/engine/session.ts'), await import('/src/i18n/index.ts'), await import('/src/modules/planning/generator.ts')];
        const o = await s.query('planning.generatorOptions', { style: 'concrete' });
        await s.execute('planning.generate', { spec: g.translateNames(o.spec, (x) => i.translate(x, lang) ?? x) });
      }, t.lang);
      await open3d(t);
      await t.button('Construction', { exact: true });
      await t.tool('planning.simulate');
      await t.wait(2500);
      t.expect(await t.page.locator('[data-testid=sim-bar]').count() === 1, 'the player opens at the Gantt\'s top');
      const tl = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.timeline'));
      const fw = tl.elements.find((e) => e.temporary === 'formwork');
      t.expect(!!fw, 'the formwork has its days on site');
      // A day in the first floor's slab: its formwork up, the walls above not started.
      const day = fw.present[0];
      const goTo = (iso) => t.page.evaluate(async (iso) => {
        const [m, g] = [await import('/src/modules/planning/SimulationView.tsx'), await import('/src/modules/planning/gantt.ts')];
        m.setDay(g.dayOf(iso));
      }, iso);
      await goTo(day);
      await t.wait(2000);
      const looks = await t.page.evaluate(async () => Object.values((await import('/src/platform/looks.ts')).useLooks.getState().bySource.planning ?? {}));
      t.expect(looks.some((l) => l.hidden) && looks.some((l) => l.color === tl.look.temporary) && looks.some((l) => l.color === tl.look.active), 'some elements hidden, the formwork and the work under way coloured');
      t.expect(await t.page.locator('[data-testid=gantt-sim-day]').count() === 1, 'the date line on the Gantt');
      await t.ui({ selection: null, selected: [], pane: 0 });
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=sim-card]').count() === 1, 'the card over the 3D says the day and the colours');
      await t.shot('planning-simulation');
      await t.shot('planning-simulation-bar', { of: '[data-testid=sim-bar]', pad: 0 });
      await t.page.locator('[data-testid=sim-settings-open]').click();
      await t.wait(500);
      await t.shot('planning-simulation-settings', { of: '[data-testid=sim-settings]', pad: 6 });
      await t.page.locator('[data-testid=sim-settings-open]').click();
      // A plan at that date: its State at date in its properties, printed so on its sheets.
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: gf, name: 'Ground floor at a date' })).id;
      await t.cmd('planning.viewDate', { id: plan, date: day });
      await t.ui({ selection: plan, selected: [plan] });
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=view-date-input]').inputValue() === day, 'the view\'s State at date in its properties');
      await t.page.locator('[data-testid=view-date]').scrollIntoViewIfNeeded();
      await t.wait(300);
      await t.shot('planning-view-date', { of: '[data-testid=view-date]', pad: 8 });
      await t.ui({ selection: null, selected: [] });
      // The stages sheet: the 3D's pictures at the end of each chapter, the 3D framed on the house first.
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.fit(false));
      await t.wait(600);
      await t.tool('planning.stages');
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=stages-dates] .chip').count() >= 3, 'the chapters\' ends as dates');
      await t.shot('planning-stages-dialog', { of: '[data-testid=stages-dialog]', pad: 0 });
      await t.page.locator('[data-testid=stages-make]').click();
      await t.page.waitForSelector('[data-testid=sheet]', { timeout: 30_000 }).catch(() => {});
      await t.wait(2500);
      t.expect(await t.page.locator('[data-testid=sheet]').count() === 1, 'the stages sheet opens');
      // The sheet alone on the screen, fitted.
      const alone = () => t.page.evaluate(async () => { const { useUi } = await import('/src/app/store.ts'); const s = useUi.getState(); useUi.setState({ panes: [s.panes[s.pane]], pane: 0 }); });
      await alone();
      await t.wait(800);
      await t.fit();
      t.expect(await t.page.locator('[data-testid=sheet] image').count() >= 3, 'with the 3D\'s pictures');
      await t.shot('planning-stages-sheet', { of: '.view-pane:has([data-testid=sheet])', pad: 0 });
      // The Gantt on a sheet.
      await t.tool('planning.ganttSheet');
      await t.wait(2500);
      await t.ui({ notice: null });
      await t.fit();
      await t.wait(600);
      const texts = await t.page.locator('[data-testid=sheet] text').allTextContents();
      t.expect(texts.length > 20, 'the Gantt drawn on the sheet');
      await t.shot('planning-gantt-sheet', { of: '.view-pane:has([data-testid=sheet])', pad: 0 });
      await t.tool('planning.video');
      await t.wait(800);
      await t.shot('planning-video', { of: '[data-testid=video-dialog]', pad: 0 });
      await t.ui({ dialog: null });
      await t.page.evaluate(async () => (await import('/src/modules/planning/SimulationView.tsx')).toggleSimulation(false));
    },
  },
  {
    // Planning, stage 5 (docs/plan-4d5d.md): progress. A house's schedule with a baseline saved; the slab done on its actual
    // dates, two walls marked done on site and one started, the status date; the Gantt with the % column, the part done in
    // each bar, the baseline under it and the variance; a task's progress in Properties; a wall's marks; the simulation's late.
    id: 'planning-progress',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick 250 plastered', layers: [
        { material: 'Lime plaster', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Gypsum plaster', thickness: 0.015 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
      }
      walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8, typeId: type })).id);
      for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8], [1, 12, 4]]) await t.cmd('architecture.addWindow', { wallId: walls[w], x, y });
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      await t.cmd('planning.updateSchedule', { name: 'House', start: '2027-03-01' });
      const add = async (name, duration, extra = {}) => {
        const id = (await t.cmd('planning.addTask', { name, duration })).id;
        if (Object.keys(extra).length) await t.cmd('planning.updateTask', { id, ...extra });
        return id;
      };
      const slab = await add('Floor slab', 6, { rule: { classes: ['IfcSlab'] }, wet: true });
      const masonry = await add('Masonry', 10, { rule: { classes: ['IfcWall'] }, part: 'core' });
      const joinery = await add('Windows and doors', 4, { rule: { classes: ['IfcWindow'] } });
      const plaster = await add('Plaster', 8, { rule: { classes: ['IfcWall'] }, part: 'finishes', wet: true });
      await t.cmd('planning.link', { from: slab, to: masonry });
      await t.cmd('planning.link', { from: masonry, to: joinery, type: 'SS', lag: 5 });
      await t.cmd('planning.link', { from: joinery, to: plaster });
      // The baseline, then the site: the slab two days late, the masonry under way, the status date Friday 19 March.
      await t.cmd('planning.saveBaseline', { date: '2027-02-26' });
      await t.cmd('planning.setProgress', { id: slab, actualStart: '2027-03-01', actualFinish: '2027-03-10' });
      await t.cmd('planning.updateSchedule', { dataDate: '2027-03-19' });
      await t.cmd('planning.markDone', { ids: [walls[0], walls[1]], date: '2027-03-17', task: masonry });
      await t.cmd('planning.markStarted', { ids: [walls[2]], date: '2027-03-18', task: masonry });
      await open3d(t);
      await t.button('Construction', { exact: true });
      await t.tool('planning.gantt');
      await t.wait(1500);
      const sched = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.schedule'));
      const row = (id) => sched.tasks.find((x) => x.id === id);
      t.expect(row(slab).status === 'done' && row(masonry).status === 'started', 'the slab done, the masonry under way');
      t.expect(Math.round(row(masonry).complete) === 40 && row(masonry).progressFrom === 'elements', "the masonry's 40 % from its walls marked on site");
      t.expect(row(plaster).finishVariance > 0 && sched.baseline.late >= 3, 'the tasks after the slab late on the baseline');
      await t.page.locator('[data-testid=gantt-fit]').click();
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=gantt-baseline-bar]').count() === 4, 'the baseline under each bar');
      t.expect(await t.page.locator('[data-testid=gantt-done-bar]').count() === 2, 'the part done in the bars');
      await t.ui({ selection: masonry, selected: [masonry] });
      await t.wait(1200);
      await t.shot('planning-progress');
      await t.page.locator('[data-testid=task-progress]').scrollIntoViewIfNeeded();
      await t.wait(300);
      await t.shot('planning-progress-task', { of: '[data-testid=task-section]', pad: 8 });
      // A wall selected: its marks on site, in each of its tasks.
      await t.ui({ selection: walls[2], selected: [walls[2]], pane: 0 });
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=element-mark][data-mark=started]').count() === 1, "the wall's mark in its properties");
      await t.page.locator('[data-testid=element-tasks]').scrollIntoViewIfNeeded();
      await t.wait(300);
      await t.shot('planning-element-marks', { of: '[data-testid=element-tasks]', pad: 8 });
      // The simulation on the status date: the masonry late on its baseline.
      await t.ui({ selection: null, selected: [] });
      await t.tool('planning.simulate');
      await t.wait(2500);
      await t.page.evaluate(async () => {
        const [m, g] = [await import('/src/modules/planning/SimulationView.tsx'), await import('/src/modules/planning/gantt.ts')];
        m.setDay(g.dayOf('2027-03-19'));
      });
      await t.wait(2000);
      const tl = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('planning.timeline'));
      const looks = await t.page.evaluate(async () => Object.values((await import('/src/platform/looks.ts')).useLooks.getState().bySource.planning ?? {}));
      t.expect(tl.baseline && looks.some((l) => l.color === tl.look.late), 'what is late on the baseline in its colour');
      t.expect(await t.page.locator('[data-testid=sim-legend] [data-state=late]').count() >= 1, 'late in the legend');
      await t.shot('planning-progress-simulation');
      await t.page.evaluate(async () => (await import('/src/modules/planning/SimulationView.tsx')).toggleSimulation(false));
    },
  },
  {
    id: 'stairs',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // The porch: a floor outside the front wall, in front of the door, its top at ±0.00.
      await t.cmd('architecture.addSlab', { storeyId: storey, points: [[1.8, -1.6], [4.2, -1.6], [4.2, 0], [1.8, 0]] });
      await t.ui({ keyPlan: false });
      await t.wait(800);
      await t.fit();
      // Zoomed out a little, about the house, so the porch and the way down stand clear of the quick tools: four notches (a wheel event zooms one at most).
      { const [px, py] = await t.planPoint(6, 4); await t.page.mouse.move(px, py); for (let i = 0; i < 4; i++) { await t.page.mouse.wheel(0, 100); await t.wait(80); } await t.wait(600); }
      // Entrance steps with the tool: the middle of the porch's edge, then the way down.
      await t.tool('architecture.entranceSteps');
      for (const [x, y] of [[3, -1.6], [3, -3]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(500); }
      await t.keys('Escape');
      const stair = (await t.nodes()).find((n) => n.type === 'IfcStair');
      t.expect(stair, 'entrance steps placed in front of the porch');
      t.expect(Math.abs((stair.props.baseOffset ?? 0) + 0.45) < 1e-6, `the steps start 450 mm below the storey (base offset ${stair.props.baseOffset})`);
      await t.wait(800);
      await t.shot('entrance-steps-plan', { of: '[data-testid=plan]', pad: 0 });
      await select(t, stair.id);
      await t.wait(800);
      await t.shot('entrance-steps-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      await t.tool('architecture.stairTypes');
      t.expect(await t.page.locator('[aria-label="Stair types"]').count() > 0, 'the Stair types dialog opens');
      await t.page.locator('.dialog .type-list').getByText('Entrance steps', { exact: false }).first().click();
      await t.wait(1500);
      await t.shot('stair-types', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null, keyPlan: true });
      await open3d(t);
      await t.shot('entrance-steps-3d', { of: 'canvas', pad: 0 });
      // A balcony railing of its own (decision 238).
      const rail = await t.cmd('architecture.addRailing', { storeyId: storey, points: [[1.8, -1.6], [4.2, -1.6]] });
      await t.wait(600);
      t.expect((await t.nodes()).some((n) => n.id === rail.id && n.type === 'LapinRailing'), 'a railing along the porch');
    },
  },
  {
    // A U stair with winders at both corners and a straight tread between them (a tester's report #109).
    id: 'stair-winder-u',
    async run(t) {
      await t.fresh();
      const storey = (await t.nodes()).filter((n) => n.type === 'IfcBuildingStorey').find((n) => Math.abs(n.props.elevation ?? 0) < 1e-6) ?? (await t.nodes()).find((n) => n.type === 'IfcBuildingStorey');
      const stair = await t.cmd('architecture.addStair', { storeyId: storey.id, library: 'winder-u', x: 0, y: 0, x2: 1, y2: 0 });
      const check = await t.page.evaluate(async (id) => (await import('/src/engine/session.ts')).query('architecture.stairCheck', { id }), stair.id);
      t.expect(check.flights.length === 3 && check.winders === 4 && check.between === 1, `two winders at each corner and a tread between (${check.flights.join(' + ')} + ${check.winders} winders)`);
      await t.tool('architecture.stairTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.type-list').getByText('U stair with winders', { exact: false }).first().click();
      await t.wait(1500);
      t.expect(await dialog.locator('[data-field]').count() > 0, 'the configurator shows the stair type');
      await t.shot('stair-winder-u', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      await t.fit();
      await select(t, stair.id);
      await t.wait(1000);
      t.expect(await t.page.locator('aside.props [data-field=stair-treads-between]').count() > 0, 'the stair\'s properties change the treads between its corners');
      await t.shot('stair-winder-u-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      await t.shot('stair-winder-u-plan', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    id: 'stair-shapes',
    async run(t) {
      await t.fresh();
      const storey = (await t.nodes()).filter((n) => n.type === 'IfcBuildingStorey').sort((a, b) => (a.props.elevation ?? 0) - (b.props.elevation ?? 0)).find((n) => Math.abs(n.props.elevation ?? 0) < 1e-6) ?? (await t.nodes()).find((n) => n.type === 'IfcBuildingStorey');
      // The shapes asked for (reports #45, #46): a spiral, a split stair and a cantilever stair, side by side.
      for (const [key, x] of [['spiral', 0], ['split', 4], ['cantilever', 10]])
        await t.cmd('architecture.addStair', { storeyId: storey.id, library: key, x, y: 0, x2: x + 1, y2: 0 });
      await t.tool('architecture.stairTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.type-list').getByText('Spiral stair', { exact: false }).first().click();
      await t.wait(800);
      await dialog.locator('input[data-field=symbols-numbers]').check();
      await t.wait(1500);
      t.expect(await dialog.locator('input[data-field=symbols-numbers]').isChecked(), 'the steps are numbered in the type');
      await t.shot('stair-spiral', { of: '.dialog', pad: 0 });
      await dialog.locator('.btn.primary').last().evaluate((el) => el.click());
      await t.wait(800);
      await t.ui({ dialog: null });
      const spiral = (await t.nodes()).find((n) => n.type === 'IfcStairType' && n.props?.spec?.shape === 'spiral');
      t.expect(spiral?.props?.spec?.symbols?.numbers, 'the spiral type numbers its steps');
      await t.fit();
      await t.wait(800);
      await t.shot('stair-numbers-plan', { of: '[data-testid=plan]', pad: 0 });
      await open3d(t);
      await t.shot('stair-shapes-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Ramps in the stair configurator (decision 252): a switchback ramp up to the floor above and an entrance ramp from the ground.
    id: 'ramps',
    async run(t) {
      await t.fresh();
      const storey = (await t.nodes()).filter((n) => n.type === 'IfcBuildingStorey').find((n) => Math.abs(n.props.elevation ?? 0) < 1e-6);
      await t.cmd('architecture.addStair', { storeyId: storey.id, library: 'switchback-ramp', x: 0, y: 0, x2: 1, y2: 0 });
      await t.cmd('architecture.addStair', { storeyId: storey.id, library: 'entrance-ramp', x: 0, y: -4, x2: 1, y2: -4 });
      const ramps = (await t.nodes()).filter((n) => n.type === 'IfcRamp');
      t.expect(ramps.length === 2, `two ramps placed (${ramps.length})`);
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.wait(1000);
      await t.shot('ramps-plan', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('architecture.stairTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('.type-list').getByText('Switchback ramp', { exact: false }).first().click();
      await t.wait(1500);
      t.expect(await dialog.locator('select[data-field=ramp-shape]').count() > 0, 'the ramp type is edited as a ramp');
      await t.shot('ramp-types', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null, keyPlan: true });
      await open3d(t);
      await t.shot('ramps-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // The space under a stair (a tester's report, #71): the configurator from the stair's properties, a reading nook made,
    // the six ready-made styles under six stairs in 3D, the nook in plan and in a section along its flight.
    id: 'under-stair',
    async run(t) {
      await t.fresh();
      const storey = await t.storey();
      // A stair along a wall: the wall on its left as one climbs, the open side towards the room.
      await t.cmd('architecture.addWall', { storeyId: storey, x1: -0.5, y1: 1.5, x2: 33.5, y2: 1.5 });
      const stair = (await t.cmd('architecture.addStair', { storeyId: storey, x: 0.6, y: 0.8, x2: 2, y2: 0.8 })).id;
      await t.ui({ keyPlan: false });
      await select(t, stair);
      await t.wait(800);
      await t.page.locator('[data-field=under-stair-open]').click();
      const dialog = t.page.locator('[data-dialog=under-stair]');
      t.expect(await dialog.count() > 0, 'the under-stair configurator opens from the stair\'s properties');
      await dialog.locator('[data-style=reading]').click();
      await t.page.waitForFunction(() => document.querySelectorAll('[data-dialog=under-stair] svg[data-field=under-stair-elevation] polygon[data-role=fabric]').length >= 2, null, { timeout: 15000 }).catch(() => {});
      t.expect(await dialog.locator('svg[data-field=under-stair-elevation] polygon[data-role=fabric]').count() >= 2, 'the preview draws the bench\'s cushions under the flight');
      await t.shot('under-stair-dialog', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-field=under-stair-apply]').click();
      await t.wait(1500);
      const nook = (await t.nodes()).find((n) => n.type === 'LapinUnderStair');
      t.expect(nook?.props?.host === stair && nook?.props?.style === 'reading', 'a reading nook is made under the stair');
      await t.ui({ dialog: null });
      await t.wait(600);
      await t.shot('under-stair-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      // The other five styles, each under a stair of its own beside the first.
      const styles = ['storage', 'office', 'mudroom', 'playhouse', 'dogbed'];
      for (let i = 0; i < styles.length; i++) {
        const x = 0.6 + (i + 1) * 5.6;
        const s = (await t.cmd('architecture.addStair', { storeyId: storey, x, y: 0.8, x2: x + 1, y2: 0.8 })).id;
        await t.cmd('architecture.addUnderStair', { stairId: s, style: styles[i] });
      }
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinUnderStair').length === 6, 'six spaces, one of each style');
      await t.fit();
      await t.wait(1200);
      await t.shot('under-stair-plan', { of: '[data-testid=plan]', pad: 0 });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.72, pitch: -0.2, distance: 33, target: { x: 18.6, y: 0.8, z: 2.4 } }, false));
      await t.wait(1800);
      await t.shot('under-stair-3d', { of: 'canvas', pad: 0 });
      await t.cmd('view.create', { kind: 'section', name: 'Nook', start: [-0.8, 0.8], end: [5.9, 0.8], depth: 1, scale: 50 });
      await t.wait(600);
      await openFromBrowser(t, 'Nook', 2500);
      await t.shot('under-stair-section', { of: '[data-testid=drawing]', pad: 0 });
    },
  },
  {
    id: 'ceilings',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // Report #44: a grid ceiling in each room, light panels every third cell and air diffusers between them.
      for (const [x, y] of [[3.5, 4], [9.5, 4]]) await t.cmd('architecture.addRoom', { storeyId: storey, x, y });
      const ids = [];
      for (const [x, y] of [[3.5, 4], [9.5, 4]]) ids.push((await t.cmd('architecture.addCeiling', { storeyId: storey, x, y })).id);
      for (const id of ids) {
        await t.cmd('architecture.ceilingLayout', { id, kind: 'light', every: 3 });
        await t.cmd('architecture.ceilingLayout', { id, kind: 'diffuser', every: 3, offset: 2 });
      }
      await t.wait(800);
      const c = (await t.nodes()).find((n) => n.type === 'LapinCeiling');
      t.expect(c && (c.props.fixtures ?? []).length > 4, 'the ceiling has its light panels and diffusers');
      await select(t, ids[0]);
      await t.wait(800);
      await t.shot('ceiling-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      await open3d(t);
      await t.shot('ceilings-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    id: 'skirtings',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // Report #61: an oak skirting round the room with the door, broken at it; the types' dialog with its profile and library.
      const room = (await t.cmd('architecture.addRoom', { storeyId: storey, x: 3.5, y: 4 })).id;
      const oak = (await t.cmd('architecture.skirtingTypeFromLibrary', { name: 'Oak 70 moulded' })).id;
      const id = (await t.cmd('architecture.addSkirting', { storeyId: storey, roomId: room, typeId: oak })).id;
      await t.wait(800);
      t.expect((await t.nodes()).some((n) => n.type === 'LapinSkirting'), 'the room has its skirting');
      await t.tool('architecture.skirtingTypes');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-library="Bullnose 70"]').click();
      await t.wait(800);
      t.expect(await dialog.locator('svg.skirting-preview').count() === 1, 'the profile is drawn');
      await t.shot('skirting-types', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      await select(t, id);
      await t.wait(800);
      await t.shot('skirting-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
    },
  },
  {
    id: 'wall-sweeps',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      // Decision PROFILES: a cornice under the top of the outer walls (mitred round the corners), a plinth band, a reveal; the Profiles dialog.
      const outer = [walls[0], walls[1], walls[2], walls[3]];
      await t.cmd('architecture.addWallSweep', { wallIds: outer, side: 'right', catalog: 'cornice', fromTop: true, height: 0.2 });
      await t.cmd('architecture.addWallSweep', { wallIds: outer, side: 'right', catalog: 'chamfered', height: 0.3 });
      await t.cmd('architecture.addWallSweep', { wallIds: [walls[1]], side: 'right', mode: 'cut', catalog: 'vGroove', height: 1.5 });
      await t.wait(800);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinWallSweep').length === 9, 'the outer walls have their cornice and band, one wall its reveal');
      await open3d(t);
      await t.shot('wall-sweeps-3d', { of: '[data-testid=view3d]', pad: 0 });
      await t.tool('manage.profiles');
      await t.wait(800);
      t.expect(await t.page.locator('.dialog svg.profile-preview').count() === 1, 'the profile is drawn');
      await t.shot('profiles', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // Decision 298: a drip band on the wall type itself, which every wall of the type then carries; its row in Wall types.
      const type = (await t.nodes()).find((n) => n.type === 'IfcWallType');
      await t.cmd('architecture.updateWallType', { id: type.id, sweeps: [{ catalog: 'drip', mode: 'add', side: 'right', height: 1.0, material: 'Plaster' }] });
      await t.wait(800);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinWallSweep' && typeof n.props.typeSweep === 'string').length === 5, 'every wall of the type has its sweep');
      await t.ui({ wallTypeId: type.id });
      await t.tool('architecture.wallTypes');
      await t.wait(600);
      t.expect(await t.page.locator('.dialog [data-testid=type-sweep]').count() === 1, 'the type lists its sweep');
      await t.shot('wall-type-sweeps', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'footing-profile',
    async run(t) {
      await t.fresh();
      const f = await t.storey('Foundations');
      // Decision PROFILES: a stepped strip footing type round a rectangle, mitred at the corners; its type in Structure types.
      const type = (await t.cmd('structure.createStripType', { name: 'Stepped 100/60', catalog: 'stepped2', bottom: -1.2 })).id;
      const pts = [[0, 0], [10, 0], [10, 7], [0, 7]];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [pts[i], pts[(i + 1) % 4]];
        await t.cmd('structure.addStripFooting', { storeyId: f, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type });
      }
      await t.cmd('structure.addStripFooting', { storeyId: f, x1: 5, y1: 0, x2: 5, y2: 7, typeId: type });
      await t.wait(800);
      await open3d(t);
      await t.shot('footing-profile-3d', { of: '[data-testid=view3d]', pad: 0 });
      await t.tool('structure.types');
      const dialog = t.page.locator('.dialog');
      await dialog.getByText('Strip', { exact: true }).click();
      await dialog.locator('li.tree-item').first().click();
      await t.wait(600);
      t.expect(await dialog.locator('select[data-field=strip-shape]').inputValue() === 'profile', 'the type is of a profile');
      await t.shot('footing-profile-type', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'room-finishes',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // Decision 265: a bathroom and a living room finished from the library; the configurator, the room's Finish field.
      const rooms = [];
      for (const [x, y] of [[3.5, 4], [9.5, 4]]) rooms.push((await t.cmd('architecture.addRoom', { storeyId: storey, x, y })).id);
      const bath = (await t.cmd('architecture.roomFinishTypeFromLibrary', { name: 'Bathroom' })).id;
      const living = (await t.cmd('architecture.roomFinishTypeFromLibrary', { name: 'Living room' })).id;
      await t.cmd('architecture.applyRoomFinish', { roomIds: [rooms[0]], typeId: bath });
      await t.cmd('architecture.applyRoomFinish', { roomIds: [rooms[1]], typeId: living });
      await t.wait(800);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinRoomFinish').length === 6, 'each room has its floor, walls and ceiling');
      await t.ui({ roomFinishTypeId: bath });
      await t.tool('architecture.roomFinishes');
      const dialog = t.page.locator('.dialog');
      await t.wait(800);
      t.expect(await dialog.locator('[data-part=walls] tr.layer-parts').count() === 1, 'the walls show their tiles up the height');
      t.expect(await dialog.locator('[data-testid=finish-floor-hatch] select[data-field=hatch-mode]').count() === 1, 'the floor has its plan hatch');
      await t.shot('room-finishes', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      await select(t, rooms[0]);
      await t.wait(800);
      t.expect(await t.page.locator('select[data-field=room-finish]').count() === 1, 'the room shows its finish');
      t.expect(await t.page.locator('input[data-field=room-finish-hatch]').count() === 1, 'the room can turn its floor hatch off');
      await t.shot('room-finish-properties', { of: 'aside.props', pad: 0, maxHeight: 560 });
      // Decision 299: the floors hatched on the plan, the bathroom's tiles and the living room's boards, under the walls.
      await t.ui({ selection: null, selected: [] });
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Finishes plan' });
      await t.ui({ planViewId: plan.id, keyPlan: false });
      await t.wait(1200);
      await t.fit();
      await t.shot('room-finish-hatch', { of: '[data-testid=plan]', pad: 0 });
      // A wall type's layer of two materials up its height: tiles to 2.10 m, the plaster over them.
      await t.ui({ selection: null, selected: [] });
      await t.tool('architecture.wallTypes');
      const types = t.page.locator('.dialog');
      await types.locator('[data-testid=layer-part-add]').last().click();
      await t.wait(400);
      t.expect(await types.locator('[data-testid=layer-part]').count() === 1, 'the layer has a piece up its height');
      await t.shot('wall-layer-parts', { of: '.dialog', pad: 0 });
    },
  },
  {
    // Apartment types (docs/plan-units.md): a flat drawn inside the building's walls made a type, mirrored into the next
    // slot: a change in one reaches the other, each stopping in the building's walls and each with its room's door in the facade.
    id: 'units',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const wall = async (a, b) => (await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] })).id;
      // The building: the facade and the walls between the flats.
      const south = await wall([0, 0], [14, 0]);
      await wall([14, 0], [14, 5]);
      await wall([14, 5], [0, 5]);
      await wall([0, 5], [0, 0]);
      await wall([7, 0], [7, 5]);
      // The flat: an inner wall from facade to facade, its door, two rooms.
      const inner = await wall([3.5, 0], [3.5, 5]);
      await t.cmd('architecture.addDoor', { wallId: inner, x: 3.5, y: 2 });
      const rooms = [(await t.cmd('architecture.addRoom', { storeyId: s, x: 1.5, y: 2.5, name: 'Living room', function: 'Living' })).id, (await t.cmd('architecture.addRoom', { storeyId: s, x: 5, y: 2.5, name: 'Bedroom', function: 'Bedroom' })).id];
      // The room carries the door in the facade, not the wall.
      const sides = (await t.page.evaluate(async (id) => (await import('/src/engine/session.ts')).query('architecture.roomOpenings', { id }), rooms[0])).sides;
      const side = sides.find((x) => x.a[1] < 1 && x.b[1] < 1).index;
      await t.cmd('architecture.setRoomOpenings', { id: rooms[0], on: true, items: [{ key: 'in', kind: 'door', side }] });
      await t.ui({ keyPlan: false, selected: [inner, ...rooms], selection: rooms[0] });
      await t.wait(400);
      await t.tool('modify.unit');
      const box = t.page.locator('.ask-box');
      await box.waitFor({ timeout: 5000 });
      await box.locator('input').first().fill('Flat A');
      await box.locator('.btn.primary').click();
      await t.wait(800);
      const group = (await t.nodes()).find((n) => n.type === 'IfcGroup');
      t.expect(!!group, 'the flat is one instance');
      const copied = await t.cmd('element.transform', { ids: [group.id], op: 'mirror', a: [7, 0], b: [7, 1], copy: true });
      t.expect((copied.ids ?? []).length > 0, 'mirrored copy made');
      await t.wait(800);
      let nodes = await t.nodes();
      t.expect(nodes.filter((n) => n.type === 'IfcGroup').length === 2, 'two instances');
      const doors = nodes.filter((n) => n.type === 'IfcDoor');
      t.expect(doors.length === 4, 'the inner doors came with the walls, each room its own door in the facade');
      t.expect(doors.filter((n) => n.props.roomOpening).length === 2, 'each room carries its door');
      t.expect(nodes.some((n) => n.id === south), 'the facade is still there, one wall');
      // The inner wall moved in the first: in the mirrored flat, the other way, still stopped in the facade.
      await t.cmd('element.transform', { ids: [inner], op: 'move', by: [-0.5, 0] });
      await t.wait(800);
      nodes = await t.nodes();
      t.expect(nodes.filter((n) => n.type === 'IfcWall').some((w) => Math.abs(w.props.start[0] - 11) < 1e-6 && Math.abs(w.props.end[0] - 11) < 1e-6), 'the mirrored flat followed, mirrored');
      await t.ui({ selected: [], selection: null });
      await t.wait(400);
      await t.fit();
      await t.wait(500);
      await t.shot('units-plan', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // A room's doors and windows chosen in its properties, by type and side (docs/rooms.md).
    id: 'room-openings',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      for (const [a, b] of [[[0, 0], [5, 0]], [[5, 0], [5, 4]], [[5, 4], [0, 4]], [[0, 4], [0, 0]]]) await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] });
      const room = (await t.cmd('architecture.addRoom', { storeyId: s, x: 2.5, y: 2 })).id;
      await t.ui({ keyPlan: false, selected: [room], selection: room });
      await t.wait(600);
      const panel = t.page.locator('[data-testid=room-openings]');
      await panel.waitFor({ timeout: 5000 });
      t.expect(!(await panel.locator('[data-field=room-openings]').isChecked()), 'off by default');
      await panel.locator('[data-field=room-openings]').check();
      await t.wait(400);
      await panel.locator('[data-add=door]').click();
      await t.wait(500);
      await panel.locator('[data-add=window]').click();
      await t.wait(800);
      const nodes = await t.nodes();
      t.expect(nodes.filter((n) => n.type === 'IfcDoor').length === 1, 'the door is in its wall');
      t.expect(nodes.filter((n) => n.type === 'IfcWindow').length === 1, 'the window is in its wall');
      await t.ui({ selected: [], selection: null });
      await t.wait(400);
      await t.fit();
      await t.wait(400);
      await t.ui({ selected: [room], selection: room });
      await t.wait(600);
      await panel.scrollIntoViewIfNeeded();
      await t.shot('room-openings', { of: 'aside.props', pad: 0, maxHeight: 520 });
      await t.shot('room-openings-plan', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    id: 'rooms',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // Two rooms by a click inside the walls, coloured by function on the plan, with a legend.
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      const rooms = (await t.nodes()).filter((n) => n.type === 'IfcSpace');
      t.expect(rooms.length === 2, `two rooms placed, found ${rooms.length}`);
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Ground floor plan' });
      await t.cmd('architecture.viewColorScheme', { viewId: plan.id, schemeId: 'by-function' });
      await t.cmd('architecture.addColorLegend', { viewId: plan.id, x: 0, y: -1.5 });
      await t.ui({ planViewId: plan.id, keyPlan: false });
      await t.wait(1200);
      await t.fit();
      await t.shot('rooms-plan', { of: '[data-testid=plan]', pad: 0 });
      await select(t, rooms[0].id);
      await t.wait(500);
      await t.shot('room-properties', { of: 'aside.props', pad: 0, maxHeight: 620 });
      await t.tool('architecture.roomColors');
      t.expect(await t.page.locator('[aria-label="Room colours"]').count() > 0, 'the Room colours dialog opens');
      await t.shot('room-colors', { of: '.dialog', pad: 0 });
      // Room tag… (report #62): the stamp's lines, here with the perimeter added.
      await t.ui({ dialog: null });
      await t.cmd('standards.set', { kind: 'tag', entry: { id: 'room', template: '{name}|{number}|{area}|{perimeter}' } });
      await t.tool('architecture.roomTag');
      t.expect(await t.page.locator('[aria-label="Room tag"]').count() > 0, 'the Room tag dialog opens');
      await t.wait(500);
      await t.shot('room-tag', { of: '.dialog', pad: 0 });
      // Nothing selected: every room of the plan.
      await t.ui({ dialog: null, selected: [] });
      // Annotate → Room dimensions (decision 249): a string inside each room along each side, the doors' and windows' jambs on it.
      await t.button('Annotate', { exact: true });
      await t.tool('drafting.roomDimensions');
      await t.wait(1200);
      const strings = (await t.nodes()).filter((n) => n.type === 'IfcAnnotation' && n.props.room);
      t.expect(strings.length >= 8, `room dimensions on every side of both rooms, found ${strings.length}`);
      await t.fit();
      await t.shot('room-dimensions', { of: '[data-testid=plan]', pad: 0 });
      await t.ui({ keyPlan: true });
    },
  },
  {
    id: 'mep',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.button('MEP', { exact: true });
      await t.shot('ribbon-mep', { of: '.topbar', pad: 0 });
      // A heating system placed whole: the boiler in the living room, its first radiator under the window across; a second radiator joins by a run of its own (a tee on the first).
      const r = await t.cmd('mep.placeSystem', { service: 'heating', storeyId: storey, x: 1, y: 1.5, to: [3.5, 7.3] });
      await t.cmd('mep.placeNode', { systemId: r.id, role: 'radiator', storeyId: storey, x: 9.5, y: 7.3, connect: true });
      const runs = (await t.nodes()).filter((n) => n.type === 'IfcPipeSegment');
      t.expect(runs.length === 2, `two runs, found ${runs.length}`);
      await t.ui({ keyPlan: false });
      await t.wait(1000);
      await t.fit();
      await t.shot('mep-plan', { of: '[data-testid=plan]', pad: 0 });
      // A detour with the tool: two clicks on the first run's longest leg, a third to the side, the span going out, along and back round what is in the way.
      const first = (await t.nodes()).find((n) => n.id === r.runId);
      const pts = first.props.points;
      let leg = 0;
      for (let i = 1; i + 1 < pts.length; i++) if (Math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) > Math.hypot(pts[leg + 1][0] - pts[leg][0], pts[leg + 1][1] - pts[leg][1])) leg = i;
      const [a, b] = [pts[leg], pts[leg + 1]];
      const at = (k) => [a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k];
      const len = Math.hypot(b[0] - a[0], b[1] - a[1]);
      const side = [(-(b[1] - a[1]) / len) * 0.7, ((b[0] - a[0]) / len) * 0.7];
      await t.tool('mep.detour');
      for (const [x, y] of [at(0.3), at(0.65), [at(0.65)[0] + side[0], at(0.65)[1] + side[1]]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(300); }
      await t.keys('Escape');
      // The run's hosts are rebuilt with it (their provision openings), so the new route comes in a moment later.
      let after = (await t.nodes()).find((n) => n.id === r.runId);
      for (let i = 0; i < 50 && after.props.points.length === pts.length; i++) { await t.wait(100); after = (await t.nodes()).find((n) => n.id === r.runId); }
      t.expect(after.props.points.length === pts.length + 4, `the detour adds four corners (${pts.length} → ${after.props.points.length})`);
      await select(t, r.runId);
      await t.wait(500);
      await t.shot('mep-detour', { of: '[data-testid=plan]', pad: 0 });
      await select(t, r.endpointId);
      await t.wait(500);
      await t.shot('mep-radiator-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      await t.tool('mep.systems');
      t.expect(await t.page.locator('[aria-label="Systems"]').count() > 0, 'the Systems dialog opens');
      await t.shot('mep-systems', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null, keyPlan: true });
    },
  },
  {
    // Gas and fuel oil (report #72): a house's gas installation from the meter box on the facade to the boiler and the cooker,
    // the regulator and the meter on the way, sleeved through the wall; the street's main 0.9 m down and the service connection
    // rising into the box; the gas system's defaults; the bill of materials.
    id: 'mep-gas',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.button('MEP', { exact: true });
      // The meter box outside the front wall, the gas boiler in the kitchen: the system placed whole.
      const r = await t.cmd('mep.placeSystem', { service: 'gas', storeyId: storey, x: 7.6, y: -0.4, to: [10.5, 6.5] });
      const runOf = async (id) => (await t.nodes()).find((n) => n.id === id);
      const first = await runOf(r.runId);
      const y = first.props.points[0][1];
      // The regulator and the meter dropped on the run, outside, by the box.
      const reg = await t.cmd('mep.placeNode', { systemId: r.id, role: 'regulator', storeyId: storey, x: 8.3, y, onRunId: r.runId });
      const tail = (await t.nodes()).find((n) => n.type === 'IfcPipeSegment' && n.props.systemId === r.id && n.id !== r.runId && n.name.includes('after'));
      t.expect(!!tail, 'the regulator cuts the run');
      await t.cmd('mep.placeNode', { systemId: r.id, role: 'gasMeter', storeyId: storey, x: 8.9, y, onRunId: tail.id });
      // The cooker in the kitchen joins by a run of its own, a tee on the boiler's.
      const cooker = await t.cmd('mep.placeNode', { systemId: r.id, role: 'cooker', storeyId: storey, x: 8.2, y: 7.4, connect: true });
      t.expect(!!cooker.runId, 'the cooker joins the gas installation');
      await t.ui({ keyPlan: false, view: 'plan' });
      await t.wait(1000);
      await t.fit();
      // The quick tools would lie over the meter box and the street.
      const quiet = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.shot('mep-gas-plan', { of: '[data-testid=plan]', pad: 0 });

      // The street: a main 6 m in front of the house, 0.9 m down; the service connection from it to the box.
      const main = await t.cmd('mep.mainAlong', { points: [[-4, -6], [16, -6]], depth: 0.9, storeyId: storey });
      const conn = await t.cmd('mep.serviceConnection', { mainId: main.id, toNodeId: r.sourceId });
      t.expect(!!conn.teeId, 'a tapping tee on the main');
      await select(t, conn.id);
      await t.wait(1000);
      await t.fit();
      await t.shot('mep-gas-street', { of: '[data-testid=plan]', pad: 0 });
      await t.shot('mep-gas-service-properties', { of: 'aside.props', pad: 0, maxHeight: 640 });
      await t.ui({ selection: null, selected: [] });
      await open3d(t);
      await t.page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [{ x: 8, y: -2.5, z: -0.3 }, Math.PI * 0.35, -0.3, 11]);
      await t.wait(1500);
      await t.shot('mep-gas-3d', { of: '[data-testid=view3d]', pad: 0 });

      // The gas system's defaults: natural gas or LPG, the pressure class.
      await t.ui({ view: 'plan' });
      await t.page.evaluate(async (id) => (await import('/src/modules/mep/prefs.ts')).setMep({ systemId: id }), r.id);
      await t.tool('mep.systems');
      t.expect(await t.page.locator('[data-gas-pressure]').count() > 0, 'the gas system shows its pressure class');
      await t.shot('mep-gas-systems', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // The bill of materials.
      const list = await t.cmd('architecture.createSchedule', { kind: 'mep-gas' });
      await t.ui({ view: 'schedule', scheduleId: list.id });
      await t.wait(1500);
      t.expect(await t.page.locator('.schedule-grid').count() > 0, 'the gas bill of materials');
      await t.shot('mep-gas-list', { of: '.schedule-pane', pad: 0, maxHeight: 760 });
      await quiet.evaluate((el) => el.remove());
      await t.ui({ view: 'plan', keyPlan: true });
    },
  },
  {
    // Fire protection (a tester's request, report #89): a fire alarm loop from the panel by the entrance through detectors in both
    // rooms, a sounder and a call point, closed back to the panel; the detectors' coverage; hydrants fed from a pump set with
    // their reach; 3D; the bill of materials.
    id: 'mep-fire',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.button('MEP', { exact: true });
      const alarm = await t.cmd('mep.placeSystem', { service: 'fireAlarm', storeyId: storey, x: 1.2, y: 0.35, to: [3.5, 4] });
      const device = (role, x, y) => t.cmd('mep.placeNode', { systemId: alarm.id, role, storeyId: storey, x, y, connect: true });
      await device('smokeDetector', 9.5, 4);
      await device('heatDetector', 9.5, 1.2);
      await device('sounder', 6.4, 1.2);
      const call = await device('callPoint', 3.9, 0.35);
      t.expect(!!call.runId, 'the call point joins the loop');
      // Close the loop from the tab, with the fire alarm system current.
      await t.page.evaluate(async (id) => (await import('/src/modules/mep/prefs.ts')).setMep({ systemId: id }), alarm.id);
      await t.tool('mep.closeLoop');
      await t.wait(800);
      const net = await t.page.evaluate(async (id) => (await import('/src/engine/session.ts')).query('mep.network', { systemId: id }), alarm.id);
      const back = net[0].nodes.find((n) => n.id === alarm.sourceId).ports.find((p) => p.key === 'return');
      t.expect(back?.taken, 'the loop comes back to the panel');
      await t.ui({ keyPlan: false, view: 'plan', selection: null, selected: [] });
      await t.wait(1000);
      await t.fit();
      const quiet = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.shot('mep-fire-plan', { of: '[data-testid=plan]', pad: 0 });

      // Hydrants: the pump set in the corner, a hydrant cabinet against the north wall of each room (its back to the wall).
      const water = await t.cmd('mep.placeSystem', { service: 'fireWater', storeyId: storey, x: 1.2, y: 7.3, to: [5.6, 7.72] });
      const second = await t.cmd('mep.placeNode', { systemId: water.id, role: 'hydrant', storeyId: storey, x: 10.8, y: 7.72, connect: true });
      t.expect(!!second.runId, 'the second hydrant tees onto the network');
      await t.wait(1000);
      await t.fit();
      await t.shot('mep-fire-hydrants', { of: '[data-testid=plan]', pad: 0 });
      await open3d(t);
      await t.page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [{ x: 6, y: 4, z: 1.2 }, -Math.PI * 0.62, -0.62, 17]);
      await t.wait(1500);
      await t.shot('mep-fire-3d', { of: '[data-testid=view3d]', pad: 0 });

      // The bill of materials.
      const list = await t.cmd('architecture.createSchedule', { kind: 'mep-fire' });
      await t.ui({ view: 'schedule', scheduleId: list.id });
      await t.wait(1500);
      t.expect(await t.page.locator('.schedule-grid').count() > 0, 'the fire protection bill of materials');
      await t.shot('mep-fire-list', { of: '.schedule-pane', pad: 0, maxHeight: 760 });
      await quiet.evaluate((el) => el.remove());
      await t.ui({ view: 'plan', keyPlan: true });
    },
  },
  {
    // Openings vs MEP (decisions 289-293): the check run on demand as a report over three heating pipes through the inner wall —
    // one through an opening it fits, one through an opening too small, one where there is none — with a floor opening nothing goes
    // through; the voids listed; the clashes marked in the plan with red clouds; fixed from the report (an opening made, one enlarged),
    // the report out of date, run again and resolved; then a pipe that cuts its own opening by the cut order, the voids in 3D and in plan.
    id: 'mep-voids',
    async run(t) {
      await t.fresh();
      const { storey, walls } = await house(t, { roof: false });
      // The report first: runs that cut their own openings (on in a new project) are switched off until the end.
      await t.cmd('cutOrder.setMep', { cut: false });
      await t.cmd('architecture.addWallOpening', { wallId: walls.inner, along: 2, width: 0.2, height: 0.2, sill: 0.9 });
      await t.cmd('architecture.addWallOpening', { wallId: walls.inner, along: 5, width: 0.15, height: 0.15, sill: 0.925 });
      await t.cmd('architecture.addSlabOpening', { storeyId: storey, points: [[2, 6], [2.4, 6], [2.4, 6.4], [2, 6.4]] });
      const system = (await t.cmd('mep.createSystem', { service: 'heating', defaults: { insulation: 0 } })).id;
      for (const [y, size] of [[2, 0.16], [5, 0.16], [6.5, 0.1]]) {
        const run = await t.cmd('mep.drawRun', { systemId: system, storeyId: storey, points: [[5, y], [9, y]], pair: false });
        await t.cmd('element.update', { ids: [run.id], attrs: { size, elevation: 1.0 } });
      }
      await t.ui({ keyPlan: false });
      await t.tool('manage.openingsMep');
      t.expect(await t.page.locator('[data-testid=coordination-dialog]').count() === 1, 'Clash check opens');
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=clash-none]').count() === 1, 'nothing has run the check: no report yet');
      await t.page.locator('[data-coordination-tab=settings]').click();
      await t.page.locator('[data-testid=void-empty]').check();
      await t.page.locator('[data-testid=clash-run]').click();
      await t.page.waitForSelector('[data-testid=clash-counts]', { timeout: 30_000 });
      await t.wait(500);
      t.expect(await t.page.locator('[data-clash-group=missing] tr[data-clash-n]').count() === 1, 'the pipe with no opening is a clash');
      t.expect(await t.page.locator('[data-clash-group=tooSmall] tr[data-clash-n]').count() === 1, 'the opening too small is a clash');
      t.expect(await t.page.locator('[data-clash-group=empty] tr[data-clash-n]').count() === 1, 'the floor opening nothing goes through is reported');
      t.expect(await t.page.locator('[data-testid=clash-stale]').count() === 0, 'the report is current');
      await t.shot('mep-voids-check', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-coordination-tab=voids]').click();
      await t.wait(400);
      t.expect(await t.page.locator('tr[data-void-mark]').count() === 8, 'eight voids: a door, four windows, two wall openings, a floor opening');
      await t.shot('mep-voids-list', { of: '.dialog', pad: 0 });
      // The clashes as remarks (what BCF sends).
      const remarks = await t.cmd('coordination.remarks', { author: 'Lapin' });
      t.expect(remarks.added === 2 && (await t.nodes()).filter((n) => n.type === 'LapinRemark').length === 2, 'two clashes, two remarks');
      // The clashes marked in the plan: red clouds with their numbers, on the screen only.
      await t.ui({ dialog: null, view: 'plan' });
      await t.wait(1000);
      await t.tool('view.clashMarks');
      await t.wait(1500);
      await t.fit();
      const clouds = await t.page.locator('[data-testid=plan] text').filter({ hasText: /^[12]$/ }).count();
      t.expect(clouds >= 2, `the plan marks the two clashes with their numbers, found ${clouds}`);
      await t.shot('mep-voids-clouds', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('view.clashMarks');
      // Fixed from the report: both clashes picked, Fix selected makes the one opening and enlarges the other; the report is then out of date.
      await t.tool('manage.openingsMep');
      await t.page.waitForSelector('[data-testid=clash-counts]', { timeout: 10_000 });
      for (const kind of ['missing', 'tooSmall']) await t.page.locator(`[data-clash-group=${kind}] tr[data-clash-n] input[type=checkbox]`).first().check();
      await t.page.locator('[data-testid=clash-fix-picked]').click();
      await t.wait(1500);
      t.expect(await t.page.locator('[data-testid=clash-stale]').count() === 1, 'the openings changed the model: the report says it is out of date');
      await t.page.locator('[data-testid=clash-run]').click();
      await t.wait(2000);
      t.expect(await t.page.locator('[data-testid=clash-stale]').count() === 0, 'run again, the report is current');
      t.expect(await t.page.locator('[data-clash-group=missing], [data-clash-group=tooSmall]').count() === 0, 'nothing left to fix but the empty void');
      await t.page.locator('.coordination-tools input[type=checkbox]').nth(1).check();
      await t.wait(300);
      t.expect(await t.page.locator('tr[data-clash-state=fixed]').count() === 2, 'the two clashes are listed resolved');
      await t.shot('mep-voids-fixed', { of: '.dialog', pad: 0 });
      // MEP in the cut order: a pipe through the inner wall cuts its own opening, round, with the clearance; a check again finds nothing.
      await t.ui({ dialog: 'cutOrder' });
      await t.wait(600);
      await t.page.locator('[data-testid=mep-cut]').check();
      await t.wait(600);
      await t.shot('mep-voids-cut-order', { of: '[data-testid=cut-order-mep]', pad: 8 });
      const extra = await t.cmd('mep.drawRun', { systemId: system, storeyId: storey, points: [[5, 3.5], [9, 3.5]], pair: false });
      await t.cmd('element.update', { ids: [extra.id], attrs: { size: 0.11, elevation: 2.2 } });
      const provisions = await t.call('doc.query', { name: 'coordination.provisions', args: {} });
      t.expect(provisions.length === 1 && provisions[0].shape === 'Round' && Math.abs(provisions[0].width - 0.15) < 1e-6, 'the new pipe cuts a round opening of 110 + 2 × 20 = 150 mm');
      const report = await t.cmd('coordination.runCheck', {});
      t.expect(report.counts.missing === 0 && report.counts.tooSmall === 0, 'run again, its opening passes the check');
      await t.ui({ dialog: null, openingVoids: true });
      await open3d(t);
      const quiet = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [{ x: 7, y: 4.2, z: 1.2 }, Math.PI * 0.05, -0.35, 6]);
      await t.wait(2000);
      await t.shot('mep-voids-3d', { of: '[data-testid=view3d]', pad: 0 });
      await quiet.evaluate((el) => el.remove());
      // The voids in plan (decision 290): View → Opening voids with the plan in front draws them there, cut through the walls,
      // dashed where they lie in the wall under the cut, the floor opening seen, each crossed with its mark and size, as sheets print them.
      await t.ui({ openingVoids: false, view: 'plan' });
      await t.wait(1000);
      await t.tool('view.openingVoids');
      await t.wait(2000);
      const marks = await t.page.locator('[data-testid=plan] text').filter({ hasText: /^V\d+$/ }).count();
      t.expect(marks === 10, `the plan draws the ten voids with their marks (the opening made and the pipe's own among them), found ${marks}`);
      await t.fit();
      // Zoomed out a little about the house, so the marks beside the outer walls stand clear of the plan's edges.
      { const [px, py] = await t.planPoint(6, 4); await t.page.mouse.move(px, py); for (let i = 0; i < 2; i++) { await t.page.mouse.wheel(0, 100); await t.wait(80); } await t.wait(600); }
      await t.shot('mep-voids-plan', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('view.openingVoids');
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=plan] text').filter({ hasText: /^V\d+$/ }).count() === 0, 'View → Opening voids again takes them off the plan');
      await t.ui({ keyPlan: true });
    },
  },
  {
    // Exhaust fans (report #55): a bathroom in the house with a wall fan on its outer wall (a round sleeve through the wall), a
    // ceiling fan with a light ducted out through the wall, a fan in a louvre panel as in the report's picture; the types with
    // their preview; a fan's properties; the fans in 3D; the rooms' extract check.
    id: 'mep-fans',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bathroom', function: 'Bath' });
      await t.button('MEP', { exact: true });
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.wait(600);
      // The wall fan by the tool: a click near the east wall's inside face.
      await t.tool('mep.fan.wallAxial');
      await t.wait(400);
      { const [px, py] = await t.planPoint(11.6, 6); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(800); }
      await t.keys('Escape');
      const fans = async () => (await t.nodes()).filter((n) => n.type === 'LapinExhaustFan');
      const wall = (await fans())[0];
      t.expect(wall && Math.abs(wall.props.rotation - 270) < 1e-6, `the wall fan faces the room (${wall?.props.rotation})`);
      const light = await t.cmd('mep.placeFan', { storeyId: storey, preset: 'ceilingLight', x: 9.5, y: 2.2 });
      await t.cmd('mep.fanDuct', { id: light.id, x: 12.5, y: 2.2, outlet: 'wall' });
      await t.cmd('mep.placeFan', { storeyId: storey, kind: 'louvrePanel', x: 5, y: 0.3 });
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      await t.wait(1200);
      await t.shot('fans-plan', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('mep.fanTypes');
      await t.wait(400);
      await t.page.locator('.dialog [data-fan-type]').filter({ hasText: 'louvre' }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=fan-preview] path').count() > 6, 'the dialog previews the fan');
      t.expect(await t.page.locator('.dialog [data-param=panelWidth]').count() === 1, 'a louvre panel shows its panel\'s size');
      await t.shot('fans-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await select(t, light.id);
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=fan-section]').count() === 1, 'a fan shows its fan properties');
      await t.shot('fans-properties', { of: 'aside.props', pad: 0, maxHeight: 560 });
      await t.ui({ selection: null, selected: [], keyPlan: true });
      const check = await t.cmd('architecture.createSchedule', { kind: 'fanRooms' });
      t.expect(!!check, 'the rooms\' extract check is made');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 2.4, pitch: -0.55, distance: 9, target: { x: 10, y: 3.5, z: 1.8 } }, false));
      await t.wait(1500);
      await t.shot('fans-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    id: 'structure',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      for (const x of [0, 6, 12]) for (const y of [0, 6]) await t.cmd('structure.addColumn', { storeyId: s, x, y });
      for (const y of [0, 6]) await t.cmd('structure.addBeam', { storeyId: s, x1: 0, y1: y, x2: 12, y2: y });
      for (const x of [0, 6, 12]) await t.cmd('structure.addBeam', { storeyId: s, x1: x, y1: 0, x2: x, y2: 6 });
      const f = await t.storey('Foundations');
      await t.button('Structure', { exact: true });
      await t.shot('ribbon-structure', { of: '.topbar', pad: 0 });
      // Decision 507: Pad footings under columns, nothing selected on the ground floor: one under each column, in the Foundations level, its top on the column's base.
      await t.ui({ storeyId: s, selection: null, selected: [] });
      await t.tool('structure.padsUnderColumns');
      await t.wait(800);
      const pads = (await t.nodes()).filter((n) => n.type === 'IfcFooting');
      t.expect(pads.length === 6, `a pad footing under each column (${pads.length})`);
      t.expect(pads.every((p) => (p.props.at?.[0] ?? -1) % 6 === 0), 'each pad at its column\'s point');
      const inFoundations = await t.page.evaluate(async ([ids, f]) => {
        const { useSession } = await import('/src/engine/session.ts');
        const { relatingOf } = await import('/src/engine/model.ts');
        return ids.every((id) => relatingOf(useSession.getState(), id, 'IfcRelContainedInSpatialStructure')?.id === f);
      }, [pads.map((p) => p.id), f]);
      t.expect(inFoundations, 'the pads are in the Foundations level');
      await open3d(t);
      await t.shot('structure-3d', { of: 'canvas', pad: 0 });
      t.expect((await t.nodes()).filter((n) => n.type === 'IfcColumn').length === 6, 'six columns');
      // A pad's level in Properties: its top and bottom, the column it follows.
      await select(t, pads[0].id);
      await t.until(async () => (await t.page.locator('[data-testid=pad-level-rule]').count()) === 1, 'a pad footing shows its level');
      t.expect(await t.page.locator('[data-testid=pad-follow]').count() === 0, 'a pad under a column follows it');
      await t.wait(400);
      await t.shot('structure-pad-level', { of: 'aside.props', pad: 0, maxHeight: 560 });
      await t.ui({ selection: null, selected: [] });
      // Decision 427 (report #116): the strip footing is the concrete alone unless the wall is asked for, in the command line's bar.
      await t.tool('structure.strip');
      t.expect(await t.page.locator('[data-testid=strip-wall]').count() === 1, 'the strip footing tool offers its wall');
      t.expect(await t.page.locator('[data-testid=strip-wall]').inputValue() === 'alone', 'a footing alone by default');
      await t.ui({ view: 'plan' });
      await t.wait(500);
      await t.shot('structure-strip-option', { of: '.cad-prompt', pad: 4 });
      await t.keys('Escape');
    },
  },
  {
    // Structural analysis, stage S0 (docs/plan-fem.md §8): the load cases, an area load by its tool, a room's imposed-load category, the summary and the combinations.
    id: 'loads',
    touches: ['core', 'architecture', 'structure', 'analysis'],
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      const rooms = (await t.nodes()).filter((n) => n.type === 'IfcSpace');
      t.expect(rooms.length === 2, `two rooms placed, found ${rooms.length}`);
      await t.button('Structure', { exact: true });
      await t.shot('ribbon-loads', { of: '.topbar', pad: 0 });
      // Load cases… → the standard set.
      await t.tool('analysis.cases');
      const dialog = t.page.locator('[aria-label="Loads"]');
      t.expect(await dialog.count() > 0, 'the Loads dialog opens');
      await dialog.getByRole('button', { name: 'Add standard cases' }).click();
      await t.until(async () => (await dialog.locator('tbody tr').count()) >= 5, 'the standard cases are listed');
      await t.wait(400);
      await t.shot('loads-cases', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // The Area load tool: a click at each corner of the room, Enter to finish; the value and the case in the command line's bar.
      await t.tool('analysis.areaLoad');
      t.expect(await t.page.locator('[data-testid=load-value]').count() === 1, 'the Area load tool offers its value');
      await t.page.locator('[data-testid=load-value]').fill('2.5');
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.wait(400);
      for (const [x, y] of [[0.5, 0.5], [6.5, 0.5], [6.5, 7.5], [0.5, 7.5]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(250); }
      await t.keys('Enter');
      await t.wait(800);
      const loads = await t.call('doc.query', { name: 'analysis.loads', args: {} });
      const qa = loads.cases.find((c) => c.code === 'Q-A');
      t.expect(qa && qa.userLoads === 1, `one area load in Q-A (${qa?.userLoads})`);
      await t.keys('Escape', 'Escape');
      await t.shot('loads-plan', { of: '[data-testid=plan]', pad: 0 });
      // A room's imposed-load category: from its function unless set; the bedroom's chosen.
      await select(t, rooms[1].id);
      await t.wait(500);
      const cat = t.page.locator('[data-field=load-category]');
      t.expect(await cat.count() === 1, 'a room has its Imposed load category');
      await cat.selectOption('B');
      await t.wait(500);
      await t.shot('loads-room', { of: 'aside.props', pad: 0, maxHeight: 620 });
      // The Imposed loads, Summary and Combinations tabs.
      await t.tool('analysis.cases');
      await dialog.getByRole('tab', { name: 'Imposed loads' }).click();
      await t.wait(400);
      await t.shot('loads-imposed', { of: '.dialog', pad: 0 });
      await dialog.getByRole('tab', { name: 'Summary' }).click();
      await t.wait(500);
      t.expect(/derived loads/i.test(await dialog.innerText()), 'the Summary lists the derived loads');
      await t.shot('loads-summary', { of: '.dialog', pad: 0 });
      await dialog.getByRole('tab', { name: 'Combinations' }).click();
      await t.until(async () => (await dialog.locator('tbody tr').count()) > 0, 'the combinations are listed');
      await t.wait(400);
      await t.shot('loads-combinations', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    // Structural analysis, stage S1b (docs/plan-fem.md §9.6): Run analysis on a floor of four beams and columns, the results, the diagrams in 3D, a bar's releases, the stale card, a mechanism.
    id: 'analysis',
    touches: ['core', 'architecture', 'structure', 'analysis'],
    async run(t) {
      await t.fresh();
      const ground = await t.storey();
      const first = (await t.cmd('storey.create', { name: 'First floor', elevation: 3, height: 3 })).id;
      const concrete = 'Concrete C25/30';
      const col = (await t.cmd('structure.createColumnType', { name: 'C30x30', section: { kind: 'rectangle', width: 0.3, depth: 0.3 }, material: concrete })).id;
      const beam = (await t.cmd('structure.createBeamType', { name: 'B30x50', section: { kind: 'rectangle', width: 0.3, depth: 0.5 }, material: concrete })).id;
      const slab = (await t.cmd('architecture.createSlabType', { name: 'Floor 20', layers: [{ material: concrete, thickness: 0.2, core: true }] })).id;
      const mat = (await t.nodes()).find((n) => n.name === concrete && n.type === 'IfcMaterial');
      if (mat) await t.cmd('material.setData', { id: mat.id, params: [{ pset: 'Pset_MaterialCommon', name: 'MassDensity', value: 2500 }] });
      const corners = [[0, 0], [6, 0], [6, 4], [0, 4]];
      const columns = [];
      for (const [x, y] of corners) columns.push((await t.cmd('structure.addColumn', { storeyId: ground, x, y, typeId: col })).id);
      const beams = [];
      for (let i = 0; i < 4; i++) { const [a, b] = [corners[i], corners[(i + 1) % 4]]; beams.push((await t.cmd('structure.addBeam', { storeyId: ground, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: beam })).id); }
      await t.cmd('architecture.addSlab', { storeyId: first, points: [[-0.15, -0.15], [6.15, -0.15], [6.15, 4.15], [-0.15, 4.15]], typeId: slab });
      for (const [x, y] of corners) await t.cmd('structure.addPadFooting', { storeyId: ground, x, y });
      await t.cmd('analysis.defaultCases', {});
      await t.cmd('analysis.addAreaLoad', { storeyId: first, points: [[0, 0], [6, 0], [6, 4], [0, 4]], value: 5 });
      await t.wait(600);
      await t.button('Structure', { exact: true });
      await t.shot('ribbon-analysis', { of: '.topbar', pad: 0 });
      // Run analysis: the results open.
      await t.tool('analysis.run');
      const dialog = t.page.locator('[aria-label="Analysis results"]');
      await t.until(async () => (await dialog.locator('[data-testid=analysis-maxima]').count()) > 0, 'the Analysis results dialog lists the largest values');
      const run = await t.call('doc.query', { name: 'analysis.run', args: {} });
      t.expect(run.ok === true, `the analysis ran (${JSON.stringify(run.mechanism ?? run.refused ?? '')})`);
      t.expect(run.cases.every((c) => c.balanced !== false), 'every case balances');
      t.expect(await dialog.locator('[data-testid=analysis-cases] tbody tr').count() >= 2, 'a row for each case');
      await t.wait(500);
      await t.shot('analysis-results', { of: '.dialog', pad: 0 });
      // Show the diagrams in 3D: the bending moment of the ultimate envelope.
      await dialog.locator('[data-testid=results-show]').click();
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 0.9, pitch: -0.45, distance: 15, target: { x: 3, y: 2, z: 2 } }, false));
      await t.wait(1500);
      await t.until(async () => (await t.page.locator('[data-testid=analysis-overlay]').count()) > 0, 'the diagram is drawn over the 3D view');
      t.expect(await t.page.locator('[data-testid=analysis-card]').count() === 1, 'the card with the picker shows');
      await t.shot('analysis-moments', { of: '[data-testid=view3d]', pad: 0 });
      await t.page.locator('[data-testid=analysis-q-N]').click();
      await t.wait(700);
      await t.page.locator('[data-testid=analysis-q-deformed]').click();
      await t.wait(700);
      await t.shot('analysis-deformed', { of: '[data-testid=view3d]', pad: 0 });
      await t.page.locator('[data-testid=analysis-q-Mz]').click();
      // A bar's releases and extremes in its properties.
      await select(t, beams[0]);
      await t.wait(600);
      t.expect(await t.page.locator('[data-field=release-i]').count() === 1, 'a beam has its releases in its properties');
      await t.shot('analysis-bar', { of: 'aside.props', pad: 0, maxHeight: 700 });
      await t.page.locator('[data-field=release-i]').selectOption('pinned');
      await t.wait(700);
      const own = (await t.nodes()).find((n) => n.id === beams[0]);
      t.expect(JSON.stringify(own?.props?.analysisReleases?.i) === JSON.stringify([false, false, false, false, true, true]), `the beam's end i is pinned (${JSON.stringify(own?.props?.analysisReleases)})`);
      // The model changed: the card says so, and Run again.
      t.expect(await t.page.locator('[data-testid=analysis-rerun]').count() === 1, 'the card offers Run again');
      await t.page.locator('[data-testid=analysis-rerun]').click();
      await t.until(async () => (await t.page.locator('[data-testid=analysis-pick]').count()) > 0, 'the picker is back after Run again');
      await t.wait(700);
      await t.page.locator('[data-testid=analysis-pick]').selectOption({ index: 0 });
      await t.wait(700);
      await t.shot('analysis-pick', { of: '[data-testid=analysis-card]', pad: 6 });
      // A mechanism: every column's top end released in all six freedoms.
      await t.cmd('analysis.setReleases', { ids: columns, j: [true, true, true, true, true, true] });
      await t.wait(600);
      await t.ui({ dialog: null });
      await t.tool('analysis.run');
      await t.until(async () => (await t.page.locator('[data-testid=analysis-mechanism]').count()) > 0 || (await t.page.locator('[data-testid=analysis-maxima]').count()) > 0, 'the run answers');
      t.expect(await t.page.locator('[data-testid=analysis-mechanism]').count() === 1, 'the mechanism is said');
      await t.wait(500);
      await t.shot('analysis-mechanism', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    // Decision 445: File → Preferences (grid and snaps), the snap menu, a UCS in a plan, the project's points in 3D.
    id: 'preferences',
    touches: ['core', 'drafting'],
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.ui({ keyPlan: false });
      // A line left 2.5 km away (decision 446): Fit frames the house and says what it left out; ZE would frame both.
      const plan = `plan:${await t.storey()}`;
      await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'line', a: [2000, 1500], b: [2001, 1500] } });
      await t.wait(600);
      await t.fit();
      await t.until(async () => (await t.page.locator('.notice-status').count()) > 0, 'a notice that something far was left out of the frame');
      const scale = await t.page.evaluate(async () => { const storey = (await import('/src/engine/session.ts')).activeStorey(); return (await import('/src/viewport/zoom.ts')).recall(`plan:${storey?.id}`)?.scale ?? 0; });
      t.expect(scale > 20, `the house fills the plan (${scale.toFixed(2)} px/m)`);
      await t.page.keyboard.press('Control+Comma');
      await t.wait(400);
      if (await t.page.locator('[data-testid=preferences]').count() === 0) await t.ui({ dialog: 'preferences' });
      await t.page.waitForSelector('[data-testid=preferences]', { timeout: 10_000 });
      await t.page.locator('[data-field=prefs-grid]').click();
      await t.page.getByRole('radio').nth(1).check();
      await t.wait(300);
      t.expect(await t.page.locator('[data-field=prefs-grid-step]').count() > 0, 'a fixed grid asks for its step');
      await t.shot('preferences', { of: '.prefs-dialog', pad: 0 });
      await t.page.getByRole('radio').nth(0).check();
      await t.ui({ dialog: null });
      await t.wait(300);
      // The snap menu: a right click on SNAP under the plan.
      await t.page.locator('[data-testid=snap-toggle]').first().click({ button: 'right' });
      await t.wait(400);
      t.expect(await t.page.getByRole('menuitem').count() >= 8, 'the snap menu lists the object snaps');
      await t.shot('snap-menu', { clip: await t.page.evaluate(() => { const m = document.querySelector('[role=menu]') ?? document.querySelector('.context-menu'); const r = (m ?? document.body).getBoundingClientRect(); return { x: Math.max(0, r.left - 240), y: Math.max(0, r.top - 40), width: Math.min(window.innerWidth, r.width + 480), height: Math.min(window.innerHeight - Math.max(0, r.top - 40), r.height + 120) }; }) });
      await t.page.keyboard.press('Escape');
      // A UCS turned 30° at a corner of the house, the grid along its axes.
      await t.cmd('project.location', { ucs: { x: 12, y: 8, angle: 30 } });
      await t.until(async () => (await t.page.locator('[data-testid=ucs-icon]').count()) > 0, 'the UCS icon in the plan');
      t.expect((await t.page.locator('[data-testid=ucs-chip]').first().textContent())?.trim() === 'UCS', 'the UCS chip says a UCS is set');
      await t.shot('ucs', { of: '[data-testid=plan]', pad: 0 });
      await t.cmd('project.location', { ucs: null });
      // The project's points in 3D: the base point turned 12.5° from true north, a survey point.
      await t.cmd('project.location', { show: true, base: { x: 4, y: -3, northAngle: 12.5 }, survey: { x: -6, y: 10, easting: 584500, northing: 452300, elevation: 118 } });
      await open3d(t);
      await t.page.locator('button[title="Frame everything"]').first().click({ timeout: 3000 }).catch(() => {});
      await t.wait(800);
      await t.until(async () => (await t.page.locator('[data-testid=location-3d]').count()) > 0, 'the project\'s points over the 3D view');
      await t.shot('location-3d');
      // Decision 447: a spot coordinate at the house's corner reads the map; the plan turned to true north; its DXF in map coordinates.
      await t.ui({ view: 'plan' });
      await t.wait(800);
      const spot = await t.cmd('drafting.addDimension', { viewId: plan, dimKind: 'coordinate', a: [0, 0], at: [-2.5, -2] });
      t.expect(!!spot?.id, 'a spot coordinate is made');
      await t.until(async () => (await t.page.locator('[data-testid=plan-true-north]').count()) > 0, 'the true north button on a turned project');
      await t.page.locator('[data-testid=plan-true-north]').first().click();
      await t.wait(400);
      t.expect(((await t.page.locator('[data-testid=plan-true-north]').first().getAttribute('class')) ?? '').includes('on'), 'the plan turns to true north');
      await t.page.locator('[data-testid=plan-export-dxf]').first().click();
      await t.wait(300);
      t.expect(await t.page.getByRole('menuitem').count() >= 3, 'the DXF button offers the plan\'s, the base point\'s and the map\'s coordinates');
      await t.page.keyboard.press('Escape');
      await t.fit();
      await t.wait(800);
      await t.shot('spot-coordinate', { of: '[data-testid=plan]', pad: 0 });
      await t.page.locator('[data-testid=plan-true-north]').first().click();
    },
  },
  {
    id: 'site',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.cmd('project.location', { crs: 'EPSG:3844' });
      // The survey points from DXF, the parcel from GeoJSON, a neighbour from CityJSON, the orthophoto with its world file: all in Stereo 70.
      const dxf = Buffer.from(surveyDxf()).toString('base64');
      const terrain = await t.cmd('civil.importDxfPoints', { data: dxf, storeyId: s, name: 'Survey' });
      t.expect(terrain?.points === 81, `81 survey points, found ${terrain?.points}`);
      const geo = await t.cmd('civil.importGeoJson', { text: parcelGeoJson(), storeyId: s });
      t.expect(!!geo?.parcel, 'a parcel from the GeoJSON');
      await t.cmd('civil.importCityJson', { text: neighbourCityJson(), storeyId: s });
      await t.cmd('civil.addOrthophoto', { data: await aerialJpeg(t), mime: 'image/jpeg', name: 'Orthophoto', worldFile: `0.1\n0\n0\n-0.1\n${E0 - 15}\n${N0 + 45}\n`, storeyId: s });
      await t.button('Site', { exact: true });
      await t.shot('ribbon-site', { of: '.topbar', pad: 0 });
      await t.ui({ keyPlan: false });
      await t.wait(1200);
      await t.fit();
      await t.shot('site-plan', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('manage.location');
      t.expect(await t.page.locator('[aria-label="Location"]').count() > 0, 'the Location dialog opens');
      await t.shot('site-location', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null, keyPlan: true });
      await open3d(t);
      await t.page.locator('button[title="Frame everything"]').first().click();
      await t.wait(800);
      await t.shot('site-3d', { of: 'canvas', pad: 0 });
      // The browser's Site: the site plan, the whole model from above with the parcel's corners numbered, the map's grid, the north arrow and the scale bar.
      await t.ui({ keyPlan: false });
      await t.page.locator('[data-testid=browser-site] button.tree-item').first().click();
      await t.wait(1500);
      await t.fit();
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=browser-site] .tree-item.on').count() === 1, 'the site plan opens from the Site');
      // The quick tools would hide the scale bar at the bottom.
      const quiet = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.shot('site-drawings', { of: '[data-testid=plan]', pad: 0 });
      await quiet.evaluate((el) => el.remove());
      await t.shot('site-browser', { of: '[data-testid=browser-site]', pad: 6 });
      // The location plan: a house on the parcel, filled in red with the parcel's outline, the neighbour left as it is.
      for (const [x1, y1, x2, y2] of [[8, 8, 18, 8], [18, 8, 18, 16], [18, 16, 8, 16], [8, 16, 8, 8]]) await t.cmd('architecture.addWall', { storeyId: await t.storey(), x1, y1, x2, y2 });
      await t.cmd('architecture.addRoof', { storeyId: await t.storey(), points: [[8, 8], [18, 8], [18, 16], [8, 16]] });
      await t.page.locator('[data-testid=browser-site] button.tree-item').nth(1).click();
      await t.wait(1500);
      await t.fit();
      // Near paper size (1:2000 as it prints), closer in on the site.
      await t.page.locator('.chip.scale').first().click();
      await t.wait(500);
      const box = await t.page.locator('[data-testid=plan]').boundingBox();
      await t.page.mouse.move(box.x + box.width * 0.55, box.y + box.height * 0.55);
      for (let k = 0; k < 4; k++) { await t.page.mouse.wheel(0, -120); await t.wait(120); }
      await t.wait(800);
      const quiet2 = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.shot('site-location-plan', { of: '[data-testid=plan]', pad: 0 });
      await quiet2.evaluate((el) => el.remove());
      await t.ui({ keyPlan: true });
    },
  },
  {
    // Gates (report #57): a fence wall along the street with a sliding gate drawn on it by the tool (its middle on the wall's
    // axis, the wall cut, the zone it parks in dashed) and a pedestrian gate beside it; a wrought iron double gate on the parcel's
    // side edge from the built-in styles; the configurator with its preview; a gate's properties; the gates in 3D.
    id: 'gates',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const fence = (await t.cmd('architecture.addWall', { storeyId: s, x1: -2, y1: 0, x2: 22, y2: 0, height: 1.4 })).id;
      await t.cmd('civil.addFlatTerrain', { storeyId: s, x1: -8, y1: -8, x2: 28, y2: 24, elevation: 0 });
      await t.cmd('civil.setParcel', { points: [[-2, 0], [22, 0], [22, 18], [-2, 18]], storeyId: s });
      await t.button('Site', { exact: true });
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.wait(600);
      // The sliding gate by the tool: two clicks near the fence, its middle put on the wall's axis.
      await t.tool('architecture.gate.sliding');
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=gate-on-line].on').count() === 1, 'the bar puts gates on lines');
      for (const [x, y] of [[4, 0.5], [9, 0.5]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(400); }
      await t.keys('Escape');
      await t.wait(800);
      const gates = async () => (await t.nodes()).filter((n) => n.type === 'LapinGate');
      const sliding = (await gates())[0];
      t.expect(sliding && Math.abs(sliding.props.at[1]) < 1e-6, `the sliding gate stands on the fence's axis (${sliding?.props.at})`);
      await t.cmd('architecture.placeGate', { storeyId: s, kind: 'pedestrian', x: 14, y: 0.3 });
      const iron = await t.cmd('architecture.placeGate', { storeyId: s, style: 'wroughtIron', x: 22.4, y: 9 });
      t.expect(!!iron?.on, 'the double gate stands on the parcel\'s edge');
      await t.ui({ selection: null, selected: [] });
      await t.fit();
      await t.wait(1200);
      await t.shot('gates-plan', { of: '[data-testid=plan]', pad: 0 });
      // The configurator: the wrought iron style, its arched top and spears, the plan with its swing and its elevation.
      await t.tool('architecture.gateTypes');
      await t.wait(400);
      await t.page.locator('.dialog [data-gate-type]').filter({ hasText: 'Wrought iron' }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=gate-preview] path').count() > 10, 'the configurator previews the gate');
      t.expect(await t.page.locator('.dialog [data-param=rise]').count() === 1, 'an arched top shows its rise');
      await t.shot('gates-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await select(t, sliding.id);
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=gate-section]').count() === 1, 'a gate shows its gate properties');
      await t.shot('gates-properties', { of: 'aside.props', pad: 0, maxHeight: 560 });
      await t.ui({ selection: null, selected: [], keyPlan: true });
      t.expect((await gates()).length === 3, 'three gates');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 4.2, pitch: -0.42, distance: 22, target: { x: 12, y: 2, z: 0.8 } }, false));
      await t.wait(1500);
      await t.shot('gates-3d', { of: 'canvas', pad: 0 });
      t.expect(!!fence, 'the fence wall');
    },
  },
  {
    // The planting library (decision 234): a small orchard with a walnut, conifers along the back and shrubs in front, drawn in plan by
    // their families' outlines (decision 341), then by the detailed symbols the view style's Entourage in 2D asks for.
    id: 'planting-symbols',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const plant = (name, x, y, seed) => t.cmd('civil.addPlanting', { storeyId: s, x, y, seed, species: [{ name }] });
      let seed = 3;
      for (const [k, name] of ['Apple', 'Pear', 'Plum', 'Sour cherry', 'Apricot', 'Quince'].entries()) await plant(name, 4 + (k % 3) * 6, 4 + Math.floor(k / 3) * 6, seed++);
      await plant('Walnut', 28, 7, seed++);
      for (const [k, name] of ['Silver fir', 'Norway spruce', 'Scots pine', 'Black pine'].entries()) await plant(name, 2 + k * 8, 20, seed++);
      for (const [k, name] of ['Hazel', 'Elder', 'Dog rose', 'Lilac (Syringa)', 'Sea buckthorn', 'Redcurrant', 'Cornelian cherry'].entries()) await plant(name, 1 + k * 4.5, -4, seed++);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinPlanting').length === 18, 'eighteen plants');
      await t.button('Site', { exact: true });
      await t.ui({ keyPlan: false });
      await t.wait(1200);
      await t.fit();
      await t.wait(600);
      // The quick tools and the HUD's card would lie over the plants.
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      await t.shot('planting-symbols', { of: '[data-testid=plan]', pad: 0 });
      const styles = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('viewStyle.list'));
      t.expect(styles.every((st) => st.entourage === 'outline'), 'the styles draw the entourage as outlines');
      await t.cmd('viewStyle.update', { id: 'architectural', entourage: 'detailed' });
      await t.wait(1200);
      await t.shot('planting-symbols-detailed', { of: '[data-testid=plan]', pad: 0 });
      await t.cmd('viewStyle.update', { id: 'architectural', entourage: 'outline' });
      await quiet.evaluate((el) => el.remove());
      await t.ui({ keyPlan: true });
    },
  },
  {
    // Entourage (stage 4 of docs/plan-visualisation.md): people at the door and a row of parked cars, in 3D, in plan and in the south
    // elevation, drawn there as simple symbols and silhouettes (decision 341); the view's Entourage in 2D among its properties.
    id: 'entourage',
    async run(t) {
      await t.fresh();
      await house(t);
      const s = await t.storey();
      for (const [kind, x, y, rotation] of [['man', 2.2, -1.6, 20], ['woman', 3.4, -2.4, 340], ['child', 4.1, -2.1, 10], ['phone', 6.2, -1.8, 30]]) await t.cmd('civil.addEntourage', { storeyId: s, kind, x, y, rotation });
      // Parked beside the house, their fronts to it: the south elevation sees them from the side.
      const row = await t.cmd('civil.addEntourage', { storeyId: s, kind: 'car', points: [[16, 0.5], [16, 8]], spacing: 2.5 });
      await t.cmd('element.update', { ids: [row.ids[1]], attrs: { kind: 'suv' } });
      await t.cmd('element.update', { ids: [row.ids[2]], attrs: { color: '#9b3b32' } });
      await t.cmd('civil.addPlanting', { storeyId: s, x: -4, y: 6, seed: 4, species: [{ name: 'Lime (Tilia)' }] });
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinEntourage').length === 8, 'four people and four cars');
      await t.button('Site', { exact: true });
      await open3d(t);
      const orbit = (view) => t.page.evaluate(async (v) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo(v, false), view);
      await orbit({ azimuth: 2.2, pitch: -0.22, distance: 22, target: { x: 9, y: 1.5, z: 1.2 } });
      await t.wait(1500);
      await t.shot('entourage-3d', { of: 'canvas', pad: 0 });
      // In plan: the shoulders and heads, the cars' outlines and windscreens.
      await t.ui({ view: 'plan', storeyId: s });
      await t.wait(1500);
      await t.fit();
      await t.wait(600);
      await t.ui({ keyPlan: false });
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      await t.shot('entourage-plan', { of: '[data-testid=plan]', pad: 0 });
      await quiet.evaluate((el) => el.remove());
      await t.ui({ keyPlan: true });
      // The south elevation draws them as silhouettes, one outline each; with nothing selected its properties offer Entourage in 2D.
      await t.cmd('view.create', { kind: 'elevation', direction: 'south', name: 'South' });
      await openFromBrowser(t, 'South', 2500);
      await t.shot('entourage-elevation', { of: '[data-testid=drawing]', pad: 0 });
      t.expect(await t.page.locator('aside.props [data-testid=view-entourage]').count() > 0, 'the elevation\'s Entourage in 2D');
      await select(t, row.ids[2]);
      await t.wait(600);
      t.expect(await t.page.locator('aside.props').getByText('Above the ground').count() > 0, 'the car\'s properties');
      await t.shot('entourage-properties', { of: 'aside.props', pad: 0, maxHeight: 700 });
      // A kind of the project's own from a picture (decision 468): a tree drawn on a see-through PNG, in Entourage kinds….
      const data = await t.page.evaluate(() => {
        const c = document.createElement('canvas');
        c.width = 300; c.height = 400;
        const g = c.getContext('2d');
        g.fillStyle = '#6b4a2b'; g.fillRect(138, 250, 24, 150);
        g.fillStyle = '#4f7f3a'; g.beginPath(); g.ellipse(150, 160, 130, 150, 0, 0, Math.PI * 2); g.fill();
        const url = c.toDataURL('image/png');
        return url.slice(url.indexOf(',') + 1);
      });
      const tree = await t.cmd('civil.addEntourageKind', { data, mime: 'image/png', name: 'Tree cut-out', height: 4, width: 3, category: 'other' });
      await t.cmd('civil.addEntourage', { storeyId: s, kind: tree.id, x: -1, y: -3 });
      await t.ui({ dialog: 'entourageKinds' });
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=entourage-kinds] input[aria-label=Name]').count() === 1, 'the picture kind in Entourage kinds');
      await t.shot('entourage-kinds', { of: '[data-testid=entourage-kinds]', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'site-works',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      const first = await t.storey('First floor');
      const ground = await t.storey();
      const slab = (await t.cmd('architecture.addSlab', { storeyId: first, points: [[0, 0], [12, 0], [12, 8], [0, 8]] })).id;
      const beam = (await t.cmd('structure.addBeam', { storeyId: ground, x1: 0, y1: 4, x2: 7, y2: 4 })).id;
      await t.button('Site', { exact: true });
      await t.shot('ribbon-site-works', { of: '.topbar', pad: 0 });
      // A scaffold round the four outer walls, formwork under the first floor and its beam.
      await t.ui({ selection: walls[0], selected: [walls[0], walls[1], walls[2], walls[3]] });
      await t.tool('sitework.scaffoldOnWalls');
      await t.ui({ selection: slab, selected: [slab, beam] });
      await t.tool('sitework.formwork');
      await t.wait(800);
      const nodes = await t.nodes();
      const scaffold = nodes.find((n) => n.type === 'LapinScaffold');
      t.expect(scaffold && scaffold.props.closed === true, 'one closed scaffold round the walls');
      t.expect(nodes.filter((n) => n.type === 'LapinFormwork').length === 2, 'formwork on the floor and on the beam');
      await t.ui({ selection: null, selected: [] });
      await open3d(t);
      await t.shot('site-works-3d', { of: 'canvas', pad: 0 });
      // The ground floor's formwork plan: the props cut, the beams over them dashed.
      await t.ui({ storeyId: ground });
      await t.tool('sitework.formworkPlan');
      await t.wait(1500);
      await t.fit();
      t.expect((await t.nodes()).some((n) => n.type === 'IfcAnnotation' || n.name?.startsWith('Formwork plan')), 'a formwork plan');
      await t.shot('formwork-plan', { of: '[data-testid=plan]', pad: 0 });
      await select(t, scaffold.id);
      await t.wait(600);
      await t.shot('scaffold-properties', { of: 'aside.props', pad: 0, maxHeight: 1300 });
      // The type's parameters are the configurator's: a lift changed here is the type's.
      const lift = t.page.locator('aside.props .qrow', { has: t.page.locator('dt', { hasText: /^Lift$/ }) }).locator('input');
      await lift.fill('1500');
      await lift.press('Enter');
      await t.wait(600);
      const type = (await t.nodes()).find((n) => n.type === 'LapinScaffoldType');
      t.expect(Math.abs((type?.props.spec?.lift ?? 0) - 1.5) < 1e-6, `the lift set in the properties is the type's (${JSON.stringify(type?.props.spec?.lift)})`);
      await t.ui({ selection: null, selected: [] });
      await t.tool('sitework.scaffoldTypes');
      t.expect(await t.page.locator('[aria-label="Scaffolds"]').count() > 0, 'the scaffold configurator opens');
      await t.shot('scaffold-configurator', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      await t.tool('sitework.formworkTypes');
      t.expect(await t.page.locator('[aria-label="Formwork"]').count() > 0, 'the formwork configurator opens');
      await t.shot('formwork-configurator', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
      // The walls' formwork (their ring beams when they have them, else both faces), and its configurator.
      await t.ui({ selection: walls[0], selected: [walls[0], walls[1], walls[2], walls[3]] });
      await t.tool('sitework.formwork');
      await t.wait(800);
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinFormwork' && n.props.kind === 'upright').length === 4, 'formwork on the four walls');
      await t.ui({ selection: null, selected: [] });
      await t.tool('sitework.formworkTypes');
      await t.button('Walls & columns', { exact: true });
      await t.shot('wall-formwork-configurator', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'sharing',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.call('share.stop');
      // The host shares from the File menu: a link for the browser, a code for the desktop.
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Share session…' }).click();
      await t.page.locator('.share-dialog input').first().fill('Ionuț');
      await t.page.locator('.share-dialog .btn.primary').click();
      await t.page.waitForSelector('.share-secret input', { timeout: 10000 });
      const link = await t.page.locator('.share-secret input').first().inputValue();
      t.expect(link.includes('#join='), 'an invitation link');
      // A guest opens the link in another browser, gives a name and joins.
      const guestContext = await t.page.context().browser().newContext({ viewport: { width: 1200, height: 800 } });
      const guest = await guestContext.newPage();
      try {
        await guest.goto(link);
        await guest.waitForSelector('.join-gate input', { timeout: 20000 });
        await guest.locator('.join-gate input').fill('Ana');
        await guest.getByRole('button', { name: 'Join', exact: true }).click();
        const welcomed = () => guest.waitForFunction(async () => (await import('/src/engine/share.ts')).useShare.getState().welcome != null && (await import('/src/engine/session.ts')).useSession.getState().nodes.size > 0, null, { timeout: 30000 });
        await welcomed();
        // A new context's page may reload once (Vite): settled again.
        await guest.waitForTimeout(2000);
        await welcomed();
        await t.wait(800);
        t.expect(await t.page.locator('.share-people li').count() >= 2, 'the guest in the host\'s list');
        await t.shot('share-dialog', { of: '.share-dialog', pad: 0 });
        await t.ui({ dialog: null });
        // She selects a wall and points at the plan: the host sees both, in her colour.
        await guest.evaluate(async (id) => (await import('/src/app/store.ts')).useUi.setState({ view: 'plan', selection: id, selected: [id] }), walls[0]);
        const box = await guest.locator('[data-testid=plan]').boundingBox();
        await guest.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await guest.mouse.move(box.x + box.width / 2 + 30, box.y + box.height / 2 + 30);
        await t.ui({ selection: null, selected: [] });
        await t.wait(1500);
        t.expect(await t.page.locator('.plan-peer-cursor').count() === 1, 'her cursor in the host\'s plan');
        await t.shot('sharing-plan', { of: '[data-testid=plan]', pad: 0 });
        await t.shot('participants', { of: '.participants', pad: 6 });
      } finally {
        await t.call('share.stop');
        await guestContext.close();
      }
      // Joining from Lapin: the link or the code, and one's name.
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Join session…' }).click();
      await t.shot('join-dialog', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'levels',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await select(t, await t.storey());
      await t.wait(500);
      await t.shot('level-properties', { of: 'aside.props', pad: 0, maxHeight: 330 });
      // The first floor's plan, the ground floor under it.
      await openFromBrowser(t, 'First floor');
      const s = await t.storey('First floor');
      for (const [a, b] of [[[0, 0], [7, 0]], [[7, 0], [7, 8]], [[7, 8], [0, 8]], [[0, 8], [0, 0]]]) await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1] });
      const underlay = t.page.locator('.view-toolbar select').first();
      const options = await underlay.locator('option').allTextContents();
      const ground = options.find((o) => /ground/i.test(o));
      t.expect(ground, `the underlay offers the ground floor (${options.join(' / ')})`);
      await underlay.selectOption({ label: ground });
      await t.wait(1200);
      await t.fit();
      // A step back, so the floor below shows round the first floor.
      const box = await t.page.locator('[data-testid=plan]').boundingBox();
      await t.page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await t.page.mouse.wheel(0, 400);
      await t.wait(600);
      await t.shot('underlay', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // Copy to storeys (a tester's wish): the ground floor's walls, doors, windows and floor onto the two levels above, in one step.
    id: 'copy-storeys',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.cmd('storey.create', { name: 'Second floor', height: 3 });
      const placed = (await t.nodes()).filter((n) => n.storeyId === storey && ['IfcWall', 'IfcSlab'].includes(n.type)).map((n) => n.id);
      t.expect(placed.length === 6, `the house's five walls and its floor (${placed.length})`);
      await t.ui({ selected: placed, selection: placed[0] });
      await t.tool('modify.copyToStoreys');
      await t.page.waitForSelector('.dialog.copy-storeys', { timeout: 5000 });
      await t.button('All above', { exact: true });
      const ticked = await t.page.locator('.storey-ticks input:checked').count();
      t.expect(ticked === 2, `All above ticks the first and second floors (${ticked})`);
      await t.shot('copy-to-storeys', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog .btn.primary').click();
      await t.wait(1500);
      const nodes = await t.nodes();
      for (const name of ['First floor', 'Second floor']) {
        const s = await t.storey(name);
        const walls = nodes.filter((n) => n.storeyId === s && n.type === 'IfcWall').length;
        const windows = nodes.filter((n) => n.storeyId === s && n.type === 'IfcWindow').length;
        t.expect(walls === 5 && windows === 4, `${name} has the five walls and four windows copied (${walls}, ${windows})`);
      }
      // Copied again: every element is already there, so nothing is added.
      const before = nodes.length;
      await t.cmd('element.copyToStoreys', { ids: placed, storeyIds: [await t.storey('First floor')] });
      t.expect((await t.nodes()).length === before, 'a second copy onto the same level is skipped');
      // The copies stay selected; the picture shows the building without the selection's grips.
      await t.ui({ selected: [], selection: null });
      await open3d(t);
      await t.shot('copy-to-storeys-3d');
    },
  },
  {
    id: 'views',
    async run(t) {
      await t.fresh();
      await house(t);
      await t.cmd('view.create', { kind: 'section', name: 'A-A', start: [6, -2], end: [6, 10], depth: 0.5, scale: 50 });
      await t.cmd('view.create', { kind: 'elevation', direction: 'south' });
      // A jogged section (decision 176): up through the south wall, across (a jog, cutting nothing: projected, report #146), up through the north wall and its window.
      await t.cmd('view.create', { kind: 'section', name: 'B-B', pts: [[2, -1], [2, 4], [9.5, 4], [9.5, 9]], scale: 50 });
      await t.wait(800);
      await t.shot('browser', { of: 'nav.browser', pad: 0, maxHeight: 640 });
      await openFromBrowser(t, 'A-A', 2500);
      await t.shot('section-view');
      // The view's scale picked in its corner (forum #30, decision 492): the project's standard scales, one undo step.
      const sectionA = (await t.nodes()).find((n) => n.type === 'LapinView' && n.name === 'A-A');
      const picker = t.page.locator('[data-testid=view-scale]').first();
      t.expect(await picker.inputValue() === '50', 'the section\'s corner shows its scale, 1:50');
      t.expect(await picker.locator('option[value=defaults]').count() === 1, 'the scale picker leads to the default scales');
      await picker.selectOption('20');
      await t.wait(900);
      t.expect((await t.nodes()).find((n) => n.id === sectionA.id)?.props.scale === 20, 'a scale picked in the corner is the view\'s');
      await t.shot('view-scale', { of: '.view-toolbar', pad: 0 });
      await t.page.evaluate(async () => (await import('/src/engine/session.ts')).undo());
      await t.wait(600);
      t.expect((await t.nodes()).find((n) => n.id === sectionA.id)?.props.scale === 50, 'one undo takes the scale back');
      await openFromBrowser(t, 'B-B', 2500);
      await t.shot('section-jogged', { of: '[data-testid=drawing]', pad: 0 });
      const elevation = (await t.nodes()).find((n) => n.type === 'LapinView' && /elevation/i.test(n.name));
      t.expect(elevation, 'an elevation in the project');
      await openFromBrowser(t, elevation.name, 2500);
      await t.shot('elevation-view', { of: '[data-testid=drawing]', pad: 0 });
      await t.button('View', { exact: true });
      await t.shot('ribbon-view', { of: '.topbar', pad: 0 });
      // View → Zoom to cursor and Orbit around selection (report #89): each turns off and on again, kept on this computer.
      const navigation = () => t.page.evaluate(async () => { const d = (await import('/src/app/display.ts')).useDisplay.getState(); return [d.zoomToCursor, d.orbitSelection]; });
      t.expect((await navigation()).every(Boolean), 'zoom to cursor and orbit around selection on by default');
      await t.tool('view.zoomToCursor');
      await t.tool('view.orbitSelection');
      t.expect((await navigation()).every((on) => !on), 'View turns zoom to cursor and orbit around selection off');
      await t.tool('view.zoomToCursor');
      await t.tool('view.orbitSelection');
      t.expect((await navigation()).every(Boolean), 'and on again');
      // View → Sections… (decision 223): the project's sections under their marker layer, what the elevation in front marks.
      await t.tool('view.sections');
      t.expect(await t.page.locator('.dialog .layer-table tbody tr').count() >= 3, 'the sections listed under their layer');
      await t.shot('sections-dialog', { of: '.dialog', pad: 0 });
      await t.keys('Escape');
      // View → Display quality (decision 203): the project's, Balanced chosen until it is changed.
      await t.tool('view.displayQuality');
      t.expect(await t.page.locator('.dialog input[type=radio]:checked').count() === 2, 'a rendering and a display quality chosen');
      await t.shot('display-quality', { of: '.dialog', pad: 0 });
    },
  },
  {
    // The X-ray visual style (report #142, decision 462): View → Visual style cycles shaded, edges, X-ray, wireframe; View → X-ray
    // switches it directly. Every surface is see-through with its edges; the selected wall stays solid.
    id: 'views-xray',
    touches: ['core'],
    async run(t) {
      await t.fresh();
      await house(t);
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.3, pitch: -0.35, distance: 22, target: { x: 6, y: 4, z: 2 } }, false));
      const style = () => t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().visualStyle);
      await t.button('View', { exact: true });
      await t.ui({ visualStyle: 'edges' });
      await t.tool('view.style');
      t.expect(await style() === 'xray', 'View → Visual style goes from shaded with edges to X-ray');
      await t.tool('view.style');
      t.expect(await style() === 'wireframe', 'and on to wireframe');
      await t.tool('view.xray');
      t.expect(await style() === 'xray', 'View → X-ray switches it on directly');
      const wall = (await t.nodes()).find((n) => n.type === 'IfcWall');
      t.expect(wall, 'a wall in the house');
      await select(t, wall.id);
      await t.ui({ notice: null });
      await t.wait(1500);
      await t.shot('view-xray', { of: '[data-testid=view3d]', pad: 0 });
      await t.tool('view.xray');
      t.expect(await style() === 'edges', 'and off again, back to shaded with edges');
      // A dialog moves by its title bar (report #140, shell/dialogDrag.ts): dragged with the mouse it moves, never out of the window; a double click recentres it.
      await t.ui({ dialog: 'displayQuality' });
      await t.wait(800);
      t.expect(await t.page.locator('.dialog').count() >= 1, `a dialog is open (dialog state: ${await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().dialog)})`);
      const dialog = t.page.locator('.dialog').first();
      const head = dialog.locator('.dialog-head');
      const box = async () => dialog.boundingBox();
      const before = await box();
      const h = await head.boundingBox();
      t.expect(before && h, 'a dialog with its title bar');
      const grab = [h.x + 24, h.y + h.height / 2];
      await t.page.mouse.move(grab[0], grab[1]);
      await t.page.mouse.down();
      await t.page.mouse.move(grab[0] + 60, grab[1] + 40, { steps: 5 });
      // Its settings: Jogged, projected by default; developed lays every piece, the jog too, side by side.
      const sectionB = (await t.nodes()).find((n) => n.type === 'LapinView' && n.name === 'B-B');
      await t.page.evaluate(async (id) => (await import('/src/app/store.ts')).useUi.setState({ selected: [], selection: id }), sectionB.id);
      await t.page.locator('nav.browser').getByText('B-B', { exact: true }).first().click();
      const jogged = t.page.locator('[data-field=section-jogged]');
      await jogged.waitFor();
      t.expect(await jogged.inputValue() === 'projected', 'a jogged section is projected by default');
      await t.shot('section-jogged-setting', { of: '.view-settings', pad: 0 });
      await jogged.selectOption('developed');
      await t.wait(1200);
      t.expect((await t.nodes()).find((n) => n.id === sectionB.id)?.props.jogged === 'developed', 'Developed is kept on the view');
      await t.shot('section-jogged-developed', { of: '[data-testid=drawing]', pad: 0 });
      await t.page.mouse.up();
      const moved = await box();
      t.expect(Math.abs(moved.x - before.x - 60) < 2 && Math.abs(moved.y - before.y - 40) < 2, `the dialog follows the drag (${before.x},${before.y} → ${moved.x},${moved.y})`);
      const h2 = await head.boundingBox();
      await t.page.mouse.move(h2.x + 24, h2.y + h2.height / 2);
      await t.page.mouse.down();
      await t.page.mouse.move(-400, -400, { steps: 5 });
      await t.page.mouse.up();
      const far = await box();
      const win = await t.page.evaluate(() => ({ w: window.innerWidth, h: window.innerHeight }));
      t.expect(far.x >= -1 && far.y >= -1 && far.x + far.width <= win.w + 1 && far.y + far.height <= win.h + 1, `and stays inside the window (${far.x},${far.y})`);
      const h3 = await head.boundingBox();
      await t.page.mouse.dblclick(h3.x + 24, h3.y + h3.height / 2);
      const again = await box();
      t.expect(Math.abs(again.x - before.x) < 2 && Math.abs(again.y - before.y) < 2, 'a double click on the title puts it back in the middle');
      await t.keys('Escape');
    },
  },
  {
    // Several elements at once and Match properties (a forum user's report and wish, topic #29; decision 490): two walls
    // selected given a phase in the properties panel, both take it; then MA from the facade wall: the inner wall lit under the
    // cursor, the settings of what is carried, a click giving it the facade's type, height, parameter and phase in one undo step.
    id: 'match-properties',
    touches: ['core', 'phases', 'architecture'],
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      const thin = (await t.cmd('architecture.createWallType', { name: 'Partition 12', layers: [{ material: 'Plasterboard', thickness: 0.12, core: true }] })).id;
      await t.cmd('architecture.setWallType', { ids: [walls.inner], typeId: thin });
      const node = async (id) => (await t.nodes()).find((n) => n.id === id);
      const typeOf = (id) => t.page.evaluate(async (id) => { const s = (await import('/src/engine/session.ts')).useSession.getState(); return (await import('/src/engine/model.ts')).typeOf(s, id)?.id ?? null; }, id);
      // Several selected: a phase chosen in the panel goes to each of them (only the first took it before).
      await t.ui({ selection: walls[1], selected: [walls[3], walls[1]] });
      await t.wait(400);
      const created = t.page.locator('aside.props [data-field=phase-created]');
      t.expect(await created.count() === 1, 'the properties panel shows the phases of the walls selected');
      await created.selectOption('existing');
      await t.wait(500);
      t.expect((await node(walls[1])).props.phaseCreated === 'existing' && (await node(walls[3])).props.phaseCreated === 'existing', 'both walls selected take the phase');
      // The source: the facade wall, made 3.2 m high with a parameter of its own, in the new construction.
      await t.cmd('element.update', { ids: [walls[0]], attrs: { height: 3.2 } });
      await t.cmd('element.setParam', { ids: [walls[0]], name: 'Fire rating', kind: 'text', value: 'EI 60' });
      await t.cmd('phases.assign', { ids: [walls[0]], created: 'new-construction' });
      const facadeType = await typeOf(walls[0]);
      await select(t, walls[0]);
      await t.fit();
      await t.keys('m', 'a');
      t.expect(await t.page.evaluate(async () => (await import('/src/viewport/cad/session.ts')).useCad.getState().command) === 'MATCHPROP', 'MA starts Match properties with the selected wall as its source');
      t.expect(await t.page.locator('[data-testid=match-bar]').count() === 1, 'the command line shows what it copies from');
      // The inner wall under the cursor lights up before the click.
      const [px, py] = await t.planPoint(7, 5);
      await t.page.mouse.move(px - 40, py - 40);
      await t.page.mouse.move(px, py, { steps: 6 });
      await t.wait(300);
      t.expect(await t.page.evaluate(async () => (await import('/src/viewport/tagTarget.ts')).useTagHover.getState().element) === walls.inner, 'the wall under the cursor is lit');
      await t.page.locator('[data-action=match-settings]').click();
      t.expect(await t.page.locator('[data-testid=match-settings] input[type=checkbox]').count() === 6, 'the settings list what is carried');
      t.expect(!(await t.page.locator('[data-testid=match-settings] input[data-group=name]').isChecked()), 'the name is not carried at first');
      await t.page.mouse.move(px, py);
      await t.wait(300);
      await t.shot('match-properties', { clip: { x: 250, y: 120, width: 1190, height: 780 } });
      await t.page.locator('[data-action=match-settings]').click();
      await t.page.mouse.click(px, py);
      await t.wait(700);
      const inner = await node(walls.inner);
      t.expect(await typeOf(walls.inner) === facadeType, 'the inner wall takes the facade wall\'s type');
      t.expect(Math.abs(inner.props.height - 3.2) < 1e-9, 'and its height');
      t.expect(inner.props.phaseCreated === 'new-construction', 'and its phase');
      t.expect((inner.props.params ?? []).some((p) => p.name === 'Fire rating' && p.value === 'EI 60'), 'and its parameter');
      t.expect(JSON.stringify(inner.props.start) === JSON.stringify([7, 0]) || JSON.stringify(inner.props.start) === JSON.stringify([7, 8]), 'but stays where it stands');
      // One click, one undo step.
      await t.keys('Escape');
      t.expect(await t.page.evaluate(async () => (await import('/src/viewport/cad/session.ts')).useCad.getState().command) === null, 'Esc ends it');
      await t.page.evaluate(async () => (await import('/src/engine/session.ts')).undo());
      await t.wait(500);
      t.expect(await typeOf(walls.inner) === thin && !(await node(walls.inner)).props.phaseCreated, 'Undo takes the whole match back at once');
    },
  },
  {
    // Section and elevation markers (decisions 455, 456, 460): the section markers' size in Drafting standards → Project
    // (a section's and an elevation's apart), the south elevation marked in the plan clear of A-A's head, its line's end
    // grip dragged so it reaches only the middle of the house, its marker dragged further out, A-A's head moved off its end.
    id: 'views-elevation-marker',
    touches: ['core'],
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      const section = await t.cmd('view.create', { kind: 'section', name: 'A-A', start: [6, -2], end: [6, 10] });
      const { id } = await t.cmd('view.create', { kind: 'elevation', direction: 'south' });
      // The section markers' size, typed in Drafting standards → Project.
      await t.tool('manage.standards');
      await t.page.locator('.dialog').getByText('Project', { exact: true }).first().click();
      const size = t.page.locator('.dialog [data-field=section-mark-size]');
      await size.fill('12');
      await size.blur();
      await t.wait(600);
      t.expect(await t.page.locator('.dialog [data-field=elevation-mark-size]').count() === 1, 'the elevation markers have a size of their own');
      const book = await t.call('doc.query', { name: 'standards.get', args: {} });
      t.expect(book?.sectionMarkSize === 12 && book?.elevationMarkSize == null, `the section markers' size set to 12 mm (${book?.sectionMarkSize}), the elevations' as they were`);
      await t.keys('Escape');
      const gripAt = async (key) => { const r = await t.page.locator(`[data-testid=plan] [data-grip="${key}"]`).first().boundingBox(); return r ? [r.x + r.width / 2, r.y + r.height / 2] : null; };
      const drag = async (from, to) => {
        await t.page.mouse.move(from[0], from[1]);
        await t.page.mouse.down();
        await t.page.mouse.move((from[0] + to[0]) / 2, (from[1] + to[1]) / 2, { steps: 4 });
        await t.page.mouse.move(to[0], to[1], { steps: 4 });
        await t.page.mouse.up();
        await t.wait(1000);
      };
      // The elevation selected in the plan: a grip on each end of its line; the right one dragged to x = 8.
      await t.ui({ selected: [id], selection: id });
      await t.fit();
      await t.wait(800);
      const end = await gripAt('extentMax');
      t.expect(end && (await gripAt('extentMin')), 'the elevation has a grip on each end of its line');
      const [toX] = await t.planPoint(8, -2.5);
      await drag(end, [toX, end[1]]);
      const max = (await t.nodes()).find((n) => n.id === id)?.props.extentMax;
      t.expect(typeof max === 'number' && Math.abs(max - 8) < 0.3, `the elevation reaches to x = 8 (${max})`);
      // Its marker dragged 1.5 m further out by the circle's grip: the elevation sees the same.
      const storeyId = await t.storey();
      const planDrawing = () => t.call('doc.query', { name: 'view.drawing', args: { id: `plan:${storeyId}` } });
      const depth0 = (await planDrawing())?.marks?.find((m) => m.id === id)?.data?.depth;
      const marker = await gripAt('marker');
      t.expect(marker, 'the elevation marker has a grip to move it');
      const [, y0] = await t.planPoint(0, 0);
      const [, y1] = await t.planPoint(0, -1.5);
      await drag(marker, [marker[0], marker[1] + (y1 - y0)]);
      const depth1 = (await t.nodes()).find((n) => n.id === id)?.props.markerDepth;
      t.expect(typeof depth0 === 'number' && typeof depth1 === 'number' && Math.abs(depth0 - depth1 - 1.5) < 0.3, `the marker moved out 1.5 m (${depth0} → ${depth1})`);
      // A-A's head at its south end moved off the line, west of it.
      await t.ui({ selected: [section.id], selection: section.id });
      await t.wait(600);
      const head = await gripAt('head-a');
      t.expect(head, "the section's head has a grip");
      const [x0] = await t.planPoint(0, 0);
      const [x1] = await t.planPoint(-1, 0);
      await drag(head, [head[0] + (x1 - x0), head[1]]);
      const headA = (await t.nodes()).find((n) => n.id === section.id)?.props.headA;
      t.expect(Array.isArray(headA) && Math.abs(headA[0] + 1) < 0.3, `the head moved 1 m west (${JSON.stringify(headA)})`);
      const start = (await t.nodes()).find((n) => n.id === section.id)?.props.start;
      t.expect(start?.[0] === 6, 'the section cuts where it did');
      await t.ui({ selected: [id, section.id], selection: id });
      await t.fit();
      await t.wait(600);
      await t.shot('elevation-marker', { of: '[data-testid=plan]', pad: 0 });
      // Moved into the house, 3.5 m north of its south wall (decision 460): the elevation starts seeing there, the walls its line crosses cut and hatched.
      await t.ui({ selected: [], selection: null });
      await t.cmd('view.moveMarker', { id, depth: 3.5, along: 0 });
      await openFromBrowser(t, 'South elevation', 2500);
      const drawing = await t.call('doc.query', { name: 'view.drawing', args: { id } });
      t.expect((drawing?.pens ?? []).some((p) => p.layer === 'HATCH'), 'the elevation draws its cut, hatched');
      await t.shot('elevation-clipped', { of: '[data-testid=drawing]', pad: 0 });
    },
  },
  {
    // Visibility / Graphics as in Revit (docs/plan-visibility-graphics.md, decision 391): a wall's finishes off as a
    // subcategory, and a linked structure displayed custom in the plan, its columns off in this view alone.
    id: 'views-vg',
    async run(t) {
      await t.fresh();
      const first = await house(t, { roof: false });
      for (const [x, y] of [[0, 0], [12, 0], [12, 8], [0, 8]]) await t.cmd('structure.addColumn', { storeyId: first.storey, x, y });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const file = join(tmpdir(), 'Structure.ifc');
      await t.call('project.exportIfc', { path: file });
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.call('project.importIfc', { path: file, mode: 'link' });
      await t.wait(800);
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Ground floor V/G' })).id;
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false });
      await t.wait(1500);
      await t.tool('view.layers');
      const dialog = t.page.locator('[data-testid=vv-dialog]');
      await dialog.waitFor({ timeout: 10_000 });
      // Walls ▸ Finishes off.
      await dialog.locator('tr[data-category=walls] button.icon').click();
      // A click (the box follows the engine's answer, so Playwright's uncheck would not see it change at once).
      await dialog.locator('tr[data-sub="walls.finishes"] input[type=checkbox]').click();
      await t.wait(800);
      const vis = () => t.call('doc.query', { name: 'view.visibility', args: { id: plan } });
      t.expect((await vis()).graphics?.categories?.walls?.sub?.finishes?.hidden === true, 'the walls\' finishes hidden as a subcategory');
      await t.shot('vg-categories', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 640 });
      // Links: the structure custom, its columns off here.
      await dialog.locator('[data-tab=links]').click();
      const row = dialog.locator('tr[data-link=Structure]');
      t.expect(await row.count() === 1, 'the linked structure listed on the Links tab');
      await row.locator('select').selectOption('custom');
      await t.wait(800);
      const custom = dialog.locator('[data-testid=vv-link-custom]');
      await custom.waitFor({ timeout: 5000 });
      await custom.locator('tr[data-category=columns] input[type=checkbox]').click();
      await t.wait(1000);
      const links = (await vis()).graphics?.links ?? {};
      const own = Object.values(links)[0];
      t.expect(own?.display === 'custom' && own?.categories?.columns?.hidden === true, 'the link displayed custom, its columns hidden in this view');
      await t.shot('vg-links', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 720 });
      await t.keys('Escape');
    },
  },
  {
    // A click takes only what the view shows (decision 498), as the snaps do (decision 491); Properties says so of an element picked by its name.
    id: 'select-shown',
    async run(t) {
      await t.fresh();
      const { storey, walls } = await house(t, { roof: false });
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Ground floor selection' })).id;
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false, dialog: null });
      await t.wait(1500);
      // The inner wall clicked, then walls hidden here and the same click passes it over; picked by its name instead,
      // Properties says it is not shown in this view.
      await t.wait(600);
      const at = await t.page.evaluate(async ([id, x, y]) => {
        const svg = document.querySelector('[data-testid=plan]');
        const view = (await import('/src/viewport/zoom.ts')).recall(id);
        if (!svg || !view) return null;
        const r = svg.getBoundingClientRect();
        return [r.left + (x - view.cx) * view.scale + r.width / 2, r.top + r.height / 2 - (y - view.cy) * view.scale];
      }, [plan, 7, 4]);
      t.expect(!!at, 'the plan on the screen');
      const clickWall = async () => {
        await t.ui({ selection: null, selected: [] });
        await t.page.mouse.move(at[0], at[1], { steps: 3 });
        await t.page.mouse.click(at[0], at[1]);
        await t.wait(600);
        return t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().selected);
      };
      t.expect((await clickWall()).includes(walls.inner), 'a click on the inner wall selects it');
      await t.cmd('view.graphics', { id: plan, category: 'walls', value: { hidden: true } });
      await t.wait(1500);
      t.expect(!(await clickWall()).includes(walls.inner), 'with walls hidden in the view, the same click passes the wall over');
      await t.ui({ selection: walls.inner, selected: [walls.inner] });
      await t.page.locator('[data-testid=not-shown-here]').waitFor({ timeout: 5000 });
      t.expect(await t.page.locator('[data-testid=not-shown-here]').count() === 1, 'selected by its name, Properties says it is not shown in this view');
    },
  },
  {
    // Visibility / Graphics, stages B and C (decisions 393, 394): annotation categories, a DXF import by its layers, a rule-based filter drawn in its colour.
    id: 'views-vg-filters',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Ground floor V/G' })).id;
      await t.cmd('drafting.addDimension', { viewId: plan, a: [0, 0], b: [12, 0], at: [0, -1.5] });
      const dxf = ['0', 'SECTION', '2', 'ENTITIES', '0', 'LINE', '8', 'SURVEY', '10', '-2000', '20', '-3000', '11', '14000', '21', '-3000',
        '0', 'LINE', '8', 'TREES', '10', '14000', '20', '-3000', '11', '14000', '21', '11000', '0', 'ENDSEC', '0', 'EOF'].join('\n');
      await t.cmd('drafting.importDxf', { viewId: plan, name: 'survey.dxf', data: Buffer.from(dxf).toString('base64'), units: 'mm' });
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false });
      await t.wait(1500);
      const vis = () => t.call('doc.query', { name: 'view.visibility', args: { id: plan } });
      const cut = async () => (await t.call('doc.query', { name: 'view.drawing', args: { id: plan } })).pens.filter((p) => p.layer === 'CUT').reduce((n, p) => n + p.segments.length, 0);
      const before = await cut();
      await t.tool('manage.viewFilters');
      const dialog = t.page.locator('[data-testid=vv-dialog]');
      await dialog.waitFor({ timeout: 10_000 });
      // Filters: a new one (walls, as it starts) hides the walls in this view; its rule says partitions only.
      await dialog.locator('[data-testid=vv-new-filter]').click();
      await t.wait(1200);
      const editor = dialog.locator('[data-testid=vv-filter-editor]');
      await editor.waitFor({ timeout: 5000 });
      await editor.locator('input[aria-label="Filter name"]').fill('Exterior walls');
      await editor.locator('input[aria-label="Filter name"]').press('Tab');
      await t.wait(800);
      let g = (await vis()).graphics;
      t.expect(g?.filters?.length === 1 && g.filters[0].hidden === true, 'the new filter used by the view, hiding what it takes');
      const after = await cut();
      t.expect(after < before, `the walls the filter takes are not cut in the plan (${after} of ${before} cut segments left)`);
      await t.shot('vg-filters', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 720 });
      // Shown again: the box beside the filter; then drawn in red and dashed (stage C, decision 394).
      await dialog.locator('.vv-filters tbody tr input[type=checkbox]').first().click();
      await t.wait(800);
      await dialog.locator('.vv-filters tbody tr input[type=color]').first().fill('#d62828');
      await t.wait(600);
      await dialog.locator('.vv-filters tbody tr select[aria-label=Pattern]').first().selectOption('dashed');
      await t.wait(800);
      g = (await vis()).graphics;
      t.expect(!g.filters[0].hidden && g.filters[0].color === '#d62828' && g.filters[0].pattern === 'dashed', 'the filter shows what it takes, red and dashed');
      const red = (await t.call('doc.query', { name: 'view.drawing', args: { id: plan } })).pens.some((p) => p.layer === 'CUT' && p.stroke === '#d62828');
      t.expect(red, 'the walls the filter takes are cut in red in the plan');
      await t.shot('vg-overrides', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 520 });
      // Annotation categories: the dimensions off.
      await dialog.locator('[data-tab=annotations]').click();
      await dialog.locator('tr[data-annotation=dimensions] input[type=checkbox]').first().click();
      await t.wait(800);
      g = (await vis()).graphics;
      t.expect(g?.annotations?.dimensions?.hidden === true, 'the dimensions hidden as an annotation category');
      await t.shot('vg-annotations', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 640 });
      // Imported categories: the survey's TREES layer off, the import halftone.
      await dialog.locator('[data-tab=imports]').click();
      const row = dialog.locator('tr[data-import="survey.dxf"]');
      t.expect(await row.count() === 1, 'the DXF import listed by its file');
      await row.locator('button.icon').click();
      await dialog.locator('tr[data-layer=TREES] input[type=checkbox]').first().click();
      await t.wait(600);
      await row.locator('input[aria-label=Halftone]').click();
      await t.wait(800);
      const imports = Object.values((await vis()).graphics?.imports ?? {});
      t.expect(imports[0]?.halftone === true && imports[0]?.layers?.TREES?.hidden === true, 'the import halftone, its TREES layer hidden');
      await t.shot('vg-imports', { of: '[data-testid=vv-dialog]', pad: 0 });
      await dialog.locator('.dialog-foot .btn.primary').click();
      await t.wait(800);
      await t.fit();
      await t.shot('vg-plan', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // A view's level of development (decisions 401, 404): the style's LOD 350, then LOD 100 (one outline hatched as the core), never by the scale.
    id: 'views-detail',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const typeId = (await t.cmd('architecture.createWallType', { name: 'Brick 250 insulated', layers: [
        { material: 'Lime plaster', thickness: 0.015 }, { material: 'Brick', thickness: 0.25, core: true },
        { material: 'Mineral wool', thickness: 0.1 }, { material: 'Silicate render', thickness: 0.01 }] })).id;
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, x1: 0, y1: 0, x2: 4, y2: 0, typeId })).id;
      await t.cmd('architecture.addWall', { storeyId: s, x1: 4, y1: 0, x2: 4, y2: 3, typeId });
      await t.cmd('architecture.addWindow', { wallId: wall, x: 2, y: 0 });
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: s, name: 'Corner 1:50', scale: 50 })).id;
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false });
      await t.wait(1500);
      await t.fit();
      const cuts = async () => (await t.call('doc.query', { name: 'view.drawing', args: { id: plan } })).pens.filter((p) => p.layer === 'CUT').reduce((n, p) => n + p.segments.length, 0);
      const fine = await cuts();
      await t.shot('detail-fine', { of: '[data-testid=plan]', pad: 0 });
      await t.cmd('view.setStyle', { id: plan, detail: 100 });
      await t.wait(1200);
      const coarse = await cuts();
      t.expect(coarse < fine, `a plan at LOD 100 draws no line between the walls' layers (${coarse} cut segments, ${fine} at LOD 350)`);
      await t.shot('detail-coarse', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // A section seen in 3D (the browser's right-click), and a walk into a room (the plan's right-click).
    id: 'views-3d',
    async run(t) {
      await t.fresh();
      const { storey, walls } = await house(t);
      // A door between the two rooms, and a stair in the bedroom, to walk through and up.
      await t.cmd('architecture.addDoor', { wallId: walls.inner, x: 7, y: 2 });
      await t.cmd('architecture.addStair', { storeyId: storey, x: 10.5, y: 1, x2: 10.5, y2: 7 });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      const bb = await t.cmd('view.create', { kind: 'section', name: 'B-B', start: [-2, 5], end: [14, 5], scale: 50 });
      // Its annotations (the drawing's x runs from the line's start: the walls at 2, 9 and 14): the walls dimensioned under the cut, the storey's height beside it, a text.
      await t.cmd('drafting.addDimension', { viewId: bb.id, pts: [[2, 0], [9, 0], [14, 0]], at: [8, -1.2], dir: [1, 0] });
      await t.cmd('drafting.addDimension', { viewId: bb.id, pts: [[2, 0], [2, 3]], at: [0.6, 1.5], dir: [0, 1] });
      await t.cmd('drafting.addText', { viewId: bb.id, at: [4.2, 1.2], text: 'Living room' });
      await t.wait(800);
      const { page } = t;
      await page.locator('nav.browser').getByText('B-B', { exact: true }).first().click({ button: 'right' });
      await t.wait(300);
      t.expect(await page.locator('.context-menu').count() === 1, 'the section\'s right-click menu opens');
      const nav = await page.locator('nav.browser').boundingBox();
      const menu = await page.locator('.context-menu').boundingBox();
      await t.shot('section-3d-menu', { clip: { x: nav.x, y: Math.max(0, menu.y - 120), width: Math.max(nav.width, menu.x + menu.width - nav.x) + 12, height: menu.height + 180 } });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Show in 3D' }).click();
      await page.waitForSelector('canvas', { timeout: 20_000 });
      await t.wait(3500);
      t.expect(await page.locator('.view3d-modes').getByText('B-B').count() === 1, 'the 3D view says which section it shows');
      await t.shot('section-3d', { of: '[data-testid=view3d]', pad: 0 });
      // Its annotations on the cut, on by default, off and on again from the chip.
      const notes = page.locator('.view3d-modes button.mode-toggle', { hasText: 'Annotations' });
      t.expect(await notes.getAttribute('aria-pressed') === 'true', 'the section\'s annotations show by default');
      await notes.click();
      await t.wait(300);
      t.expect(await notes.getAttribute('aria-pressed') === 'false', 'the chip hides the annotations');
      await notes.click();
      // The cut closed by caps in the elements' materials; Hatches lays the section's drawing on it instead.
      const hatches = page.locator('.view3d-modes button.mode-toggle', { hasText: 'Hatches' });
      t.expect(await hatches.getAttribute('aria-pressed') === 'false', 'the cut shows the materials by default');
      t.expect((await t.call('view.caps', { planes: [{ normal: [0, 1, 0], constant: -5 }] })).length > 0, 'the engine caps what the section cuts');
      await hatches.click();
      await t.wait(800);
      await t.shot('section-3d-hatches', { of: '[data-testid=view3d]', pad: 0 });
      await hatches.click();
      // Off again from its chip.
      await page.locator('.view3d-modes button.mode-close').click();
      await t.wait(400);
      t.expect(await page.locator('.view3d-modes').count() === 0, 'the section goes off from its chip');
      // An elevation: the camera turns to look its way; the same entry turns it off.
      await t.cmd('view.create', { kind: 'elevation', direction: 'north' });
      await t.wait(600);
      const elevation = (await t.nodes()).find((n) => n.type === 'LapinView' && n.props.kind === 'elevation');
      await page.locator('nav.browser').getByText(elevation.name, { exact: true }).first().click({ button: 'right' });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Show in 3D' }).click();
      await t.wait(1500);
      t.expect(await page.locator('.view3d-modes').getByText(elevation.name, { exact: true }).count() === 1, 'an elevation shows in 3D');
      await page.locator('nav.browser').getByText(elevation.name, { exact: true }).first().click({ button: 'right' });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Show in 3D' }).click();
      await t.wait(400);
      t.expect(await page.locator('.view3d-modes').count() === 0, 'Show in 3D again turns it off');

      // The section box: on from the View tab, its top at the ground floor's plan cut, its east face dragged in.
      // The View tab may be under More ▾ (decision 367): chosen as the tab strip does.
      await t.ui({ tab: 'view' });
      await t.wait(300);
      await page.locator('.band').getByRole('button', { name: 'Section box' }).click();
      await t.wait(800);
      t.expect(await page.locator('.section-box-handle:not(.section-box-turn)').count() === 6, 'the section box has a handle on each face');
      await page.locator('.view3d-modes').getByRole('button', { name: 'Cut at plan' }).click();
      await t.wait(800);
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.2, pitch: -0.6, distance: 24, target: { x: 6, y: 4, z: 0.5 } }, false));
      await t.wait(600);
      const east = await page.locator('.section-box-handle[data-face="x1"] circle').last().boundingBox();
      const west = await page.locator('.section-box-handle[data-face="x0"] circle').last().boundingBox();
      await page.mouse.move(east.x + east.width / 2, east.y + east.height / 2);
      await page.mouse.down();
      await page.mouse.move(east.x + (west.x - east.x) * 0.45, east.y + (west.y - east.y) * 0.45, { steps: 8 });
      await page.mouse.up();
      await t.wait(1500);
      const box = await page.evaluate(async () => (await import('/src/viewport/sectionBox.ts')).useSectionBox.getState().box);
      t.expect(box.max[0] < 11 && box.max[2] < 2, 'the box is cut at the plan and its face dragged in');
      await t.shot('section-box', { of: '[data-testid=view3d]', pad: 0 });
      // Its cuts drawn as a sheet draws them; turned the way a wall runs.
      await page.locator('.view3d-modes').getByRole('button', { name: 'Hatches' }).click();
      await t.wait(1500);
      t.expect((await t.call('view.caps', { planes: [{ normal: [0, 0, -1], constant: 1.2 }], style: 'drawing' })).some((c) => c.parts.some((p) => p.hatch?.length)), 'the box\'s caps are hatched as on a sheet');
      await t.shot('section-box-hatches', { of: '[data-testid=view3d]', pad: 0 });
      await page.locator('.view3d-modes').getByRole('button', { name: 'Hatches' }).click();
      t.expect(await page.locator('.section-box-turn').count() === 1, 'the box has a handle to turn it');
      await page.locator('.view3d-modes button.mode-close').click();
      await t.wait(300);

      // The ground floor's plan, a right-click in the living room: walk in 3D.
      await t.ui({ view: 'plan', storeyId: storey, planViewId: null });
      await t.wait(1200);
      await t.fit();
      const [x, y] = await t.planPoint(3, 4);
      await page.mouse.click(x, y, { button: 'right' });
      await t.wait(300);
      t.expect(await page.locator('.context-menu').getByRole('menuitem', { name: 'Walk in 3D' }).count() === 1, 'a room\'s right-click offers a walk');
      await t.shot('room-walk-menu', { clip: { x: x - 320, y: y - 200, width: 680, height: 400 } });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Walk in 3D' }).click();
      await page.waitForSelector('canvas', { timeout: 20_000 });
      await t.wait(3000);
      t.expect(await page.locator('.mode-chip.walk').count() === 1, 'the walk is on');
      await t.shot('room-walk', { of: '[data-testid=view3d]', pad: 0 });
      // A few steps forward and a look to the left: the camera moves, the walk goes on.
      const before = await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().position);
      await page.keyboard.down('w'); await t.wait(400); await page.keyboard.up('w');
      const c = await page.locator('[data-testid=view3d]').boundingBox();
      await page.mouse.move(c.x + c.width / 2, c.y + c.height / 2);
      await page.mouse.down(); await page.mouse.move(c.x + c.width / 2 - 120, c.y + c.height / 2, { steps: 6 }); await page.mouse.up();
      const after = await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().position);
      t.expect(Math.hypot(after.x - before.x, after.y - before.y) > 0.2 && Math.abs(after.z - before.z) < 0.01, 'W walks forward, level');
      // The walker has a body: the inner wall (x 7) stops it, the door in it lets it through, the stair takes it up.
      const camera = () => page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().position);
      const walkFrom = async (x, y, yaw, held, ms, pitch = 0) => {
        await page.evaluate(async ([x, y, yaw, pitch]) => (await import('/src/viewport/walk.ts')).useWalk.setState({ start: { eye: { x, y, z: 1.6 }, yaw, pitch, name: 'Living room' } }), [x, y, yaw, pitch]);
        await t.wait(400);
        if (held) { await page.keyboard.down(held); await t.wait(ms); await page.keyboard.up(held); }
        await t.wait(600);
        return camera();
      };
      const atWall = await walkFrom(5.5, 5, 0, 'w', 2500);
      t.expect(atWall.x > 6 && atWall.x < 6.9, `a wall stops the walker (x ${atWall.x.toFixed(2)})`);
      await walkFrom(3.5, 2, 0, null, 0);
      await t.shot('room-walk-door', { of: '[data-testid=view3d]', pad: 0 });
      const through = await walkFrom(3.5, 2, 0, 'w', 3500);
      t.expect(through.x > 7.6, `a door lets the walker through (x ${through.x.toFixed(2)})`);
      const onStair = await walkFrom(10.5, 0.4, Math.PI / 2, 'w', 1500, -0.3);
      t.expect(onStair.z > 1.9, `a stair takes the walker up (eye at ${onStair.z.toFixed(2)})`);
      await t.shot('room-walk-stair', { of: '[data-testid=view3d]', pad: 0 });
      await page.keyboard.down('w'); await t.wait(2500); await page.keyboard.up('w'); await t.wait(600);
      const top = await camera();
      t.expect(top.z > 3.5, `up to the stair's top (eye at ${top.z.toFixed(2)})`);
      // F flies: through the wall.
      await page.evaluate(async () => (await import('/src/viewport/walk.ts')).useWalk.setState({ start: { eye: { x: 5.5, y: 5, z: 1.6 }, yaw: 0, pitch: 0, name: 'Living room' } }));
      await t.wait(400);
      await t.keys('f');
      t.expect(await page.locator('.mode-chip.walk button[aria-pressed=true]').count() === 1, 'F flies');
      await page.keyboard.down('w'); await t.wait(2500); await page.keyboard.up('w');
      t.expect((await camera()).x > 7.5, 'flying goes through walls');
      await t.keys('f');
      await t.keys('Escape');
      t.expect(await page.locator('.mode-chip.walk').count() === 0, 'Esc ends the walk');
      const back = await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().position);
      t.expect(back.z > 5, `the camera there was comes back (z ${back.z.toFixed(2)})`);
    },
  },
  {
    // The 3D view rendered realistic (docs/plan-visualisation.md): the sky, a ground, the sun's shadows; View → Sun and shadows.
    id: 'views-realistic',
    async run(t) {
      await t.fresh();
      await house(t);
      const profile = (p) => t.page.evaluate(async (v) => (await import('/src/viewport/render3d.ts')).setRenderProfile(v), p);
      try {
        await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.setState({ view: '3d', keyPlan: false }));
        await t.page.waitForSelector('[data-testid=view3d] canvas');
        await t.page.evaluate(async () => (await import('/src/modules/core/sun.ts')).setSun({ date: '2026-06-21', hour: 17 }));
        await profile('realistic');
        await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -1.2, pitch: -0.22, distance: 24, target: { x: 6, y: 4, z: 2 } }, false));
        await t.wait(2500);
        t.expect(await t.page.evaluate(async () => !!(await import('/src/platform/sunlight.ts')).useSunlight.getState().dir), 'the realistic view has a sun');
        await t.shot('view-realistic', { of: '[data-testid=view3d]', pad: 0 });
        await t.tool('view.sun');
        await t.wait(800);
        t.expect(await t.page.locator('.dialog input[type=date]').count() === 1, "the sun's day");
        await t.shot('sun-dialog', { of: '.dialog', pad: 0 });
        await t.keys('Escape');
      } finally {
        // The scenes after draw as the drawing does.
        await profile('drawing');
      }
    },
  },
  {
    // Manage → Clash detection (docs/plan-visualisation.md, stage 2): heating runs through walls (the cut order's MEP cutting off,
    // so they clash), the starting tests run, a clash framed in 3D, a test's settings and a search set's rules.
    id: 'clash',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      await t.cmd('cutOrder.setMep', { cut: false });
      const system = (await t.cmd('mep.createSystem', { service: 'heating', defaults: { insulation: 0 } })).id;
      // Two runs across the house (through the front and the back walls) and one through the inner and the end walls.
      for (const [x1, y1, x2, y2] of [[2, -1, 2, 9], [5, -1, 5, 9], [6, 4, 13, 4]]) {
        const run = (await t.cmd('mep.drawRun', { systemId: system, storeyId: storey, points: [[x1, y1], [x2, y2]], pair: false })).id;
        await t.cmd('element.update', { id: run, attrs: { size: 0.11, elevation: 1.2 } });
      }
      t.expect(storey, 'a storey');
      await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.setState({ view: '3d', keyPlan: false }));
      await t.page.waitForSelector('[data-testid=view3d] canvas');
      await t.tool('clash.manage');
      await t.page.waitForSelector('[data-testid=clash-dialog] [data-testid=clash-test] option', { state: 'attached' });
      await t.wait(1500);
      await t.page.locator('[data-testid=clash-test]').selectOption({ label: 'Architecture × Services' });
      await t.wait(800);
      await t.page.locator('[data-testid=clash-run-test]').click();
      await t.page.waitForSelector('[data-clash-n]', { timeout: 20_000 });
      t.expect(await t.page.locator('[data-clash-n]').count() === 6, 'three runs through two walls each');
      await t.page.locator('[data-clash-n="1"]').click();
      await t.wait(2500);
      await t.shot('clash-dialog');
      // Grouped by element: each wall with its runs.
      await t.page.locator('[data-testid=clash-group-by]').selectOption('element');
      await t.page.waitForSelector('[data-clash-group]');
      await t.shot('clash-groups', { of: '[data-testid=clash-dialog]', pad: 0 });
      await t.page.locator('[data-testid=clash-group-by]').selectOption('none');
      await t.page.locator('[data-clash-tab=test]').click();
      await t.wait(400);
      await t.shot('clash-test', { of: '[data-testid=clash-dialog]', pad: 0 });
      await t.page.locator('[data-clash-tab=sets]').click();
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=clash-set-count]').count() === 1, 'a set and how many elements it takes');
      await t.shot('clash-sets', { of: '[data-testid=clash-dialog]', pad: 0 });
      // The clash as the dialog left it framed, without the dialog over it.
      await t.page.locator('[data-testid=clash-dialog] button[title="Close"]').click();
      await t.wait(600);
      await t.shot('clash-3d', { of: '[data-testid=view3d]', pad: 0 });
      await t.page.evaluate(async () => { (await import('/src/viewport/sectionBox.ts')).useSectionBox.setState({ box: null }); });
      // The open clashes as red clouds in the ground floor's plan (View → Clash marks).
      await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.setState({ view: 'plan' }));
      await t.page.waitForSelector('.plan-svg');
      await t.wait(1500);
      await t.tool('view.clashMarks');
      await t.wait(1500);
      t.expect(await t.page.locator('.plan-svg text', { hasText: /^\d+\.\d+$/ }).count() >= 6, 'six clouds labelled test.clash');
      await t.shot('clash-marks', { of: '.plan-svg', pad: 0 });
      await t.tool('view.clashMarks');
    },
  },
  {
    // Sun shadows in a drawing (docs/plan-visualisation.md): the south elevation of a house with its roof, by the drawing
    // convention (the sun over the left shoulder), and its fields in the view's properties.
    id: 'views-shadows',
    async run(t) {
      await t.fresh();
      await house(t);
      const south = (await t.cmd('view.create', { kind: 'elevation', direction: 'south', name: 'South' })).id;
      await t.cmd('view.setShadows', { id: south, mode: 'view' });
      await openFromBrowser(t, 'South', 2500);
      t.expect(await t.page.locator('[data-testid=drawing] .pen-shadows').count() > 0, 'the elevation fills its shadows');
      await t.shot('elevation-shadows', { of: '[data-testid=drawing]', pad: 0 });
      await t.page.evaluate(async (id) => (await import('/src/app/store.ts')).useUi.setState({ selected: [], selection: id }), south);
      await t.page.locator('nav.browser').getByText('South', { exact: true }).first().click();
      await t.page.waitForSelector('[data-testid=view-shadows]');
      await t.shot('view-shadows', { of: '.view-settings', pad: 0 });
    },
  },
  {
    // Surface hatches in elevations (decisions 325, 327): nothing set by hand, each material's elevation hatch as its name
    // suggests — the facing brick BRICK, the insulation and plaster none; a jogged section seeing a brick garden wall beyond
    // its pieces; the view style's switch in the view's properties.
    id: 'views-surface-hatch',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const type = (await t.cmd('architecture.createWallType', { name: 'Brick facade 50', layers: [
        { material: 'Facing brick', thickness: 0.12 }, { material: 'Mineral wool', thickness: 0.1 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Plaster', thickness: 0.015 },
      ] })).id;
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: s, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: type })).id);
      }
      await t.cmd('architecture.addWall', { storeyId: s, x1: 7, y1: 0, x2: 7, y2: 8 });
      await t.cmd('architecture.addDoor', { wallId: walls[0], x: 3, y: 0 });
      for (const [w, x, y] of [[0, 9.5, 0], [2, 3.5, 8], [2, 9.5, 8], [1, 12, 4]]) await t.cmd('architecture.addWindow', { wallId: walls[w], x, y });
      await t.cmd('architecture.addSlab', { storeyId: s, points: outline });
      const roof = await t.cmd('architecture.addRoof', { storeyId: s, points: outline });
      await t.cmd('architecture.roofStyle', { id: roof.id, style: 'ro_saxon' });
      // Its covering is made of Clay tile (decision 328): the south slope hatched in level courses as the name suggests.
      t.expect((await t.nodes()).find((n) => n.id === roof.id)?.props?.coveringMaterial === 'Clay tile', 'the roof\'s covering is made of Clay tile');
      // A garden wall of facing brick behind the house.
      const garden = (await t.cmd('architecture.createWallType', { name: 'Garden wall', layers: [{ material: 'Facing brick', thickness: 0.25, core: true }] })).id;
      await t.cmd('architecture.addWall', { storeyId: s, x1: -2, y1: 11, x2: 14, y2: 11, typeId: garden });
      await t.wait(800);
      const south = (await t.cmd('view.create', { kind: 'elevation', direction: 'south', name: 'South' })).id;
      await openFromBrowser(t, 'South', 2500);
      t.expect(await t.page.locator('[data-testid=drawing] .pen-surface-hatch').count() > 0, 'the elevation hatches the faces it sees, from their materials\' names');
      await t.shot('elevation-surface-hatch', { of: '[data-testid=drawing]', pad: 0 });
      // A jogged section: through the house along y = 4, north along x = 5, then along y = 9.5 looking at the garden wall.
      await t.cmd('view.create', { kind: 'section', name: 'J-J', pts: [[-1, 4], [5, 4], [5, 9.5], [14, 9.5]], jogged: 'developed' });
      await openFromBrowser(t, 'J-J', 1500);
      await t.fit();
      t.expect(await t.page.locator('[data-testid=drawing] .pen-surface-hatch').count() > 0, 'the developed section hatches what its pieces see');
      await t.shot('section-developed-surface-hatch', { of: '[data-testid=drawing]', pad: 0 });
      await openFromBrowser(t, 'South', 1500);
      await t.page.evaluate(async (id) => (await import('/src/app/store.ts')).useUi.setState({ selected: [], selection: id }), south);
      await t.page.locator('nav.browser').getByText('South', { exact: true }).first().click();
      await t.page.waitForSelector('[data-testid=view-surface-hatch]');
      await t.shot('view-surface-hatch', { of: '.view-settings', pad: 0 });
    },
  },
  {
    // One storey at a time in 3D (report #29): a level's right-click in the browser, the chip, and back to every storey.
    id: 'views-3d-storeys',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.cmd('architecture.addDoor', { wallId: walls.inner, x: 7, y: 2 });
      // The first floor over it: the same outline, its floor and the roof.
      const first = await t.storey('First floor');
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const upper = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        upper.push((await t.cmd('architecture.addWall', { storeyId: first, x1: a[0], y1: a[1], x2: b[0], y2: b[1] })).id);
      }
      await t.cmd('architecture.addWindow', { wallId: upper[0], x: 3, y: 0 });
      await t.cmd('architecture.addWindow', { wallId: upper[0], x: 9.5, y: 0 });
      await t.cmd('architecture.addSlab', { storeyId: first, points: outline });
      const roof = await t.cmd('architecture.addRoof', { storeyId: first, points: outline });
      await t.cmd('architecture.roofStyle', { id: roof.id, style: 'ro_saxon' });
      await t.wait(800);
      await open3d(t);
      await t.fit();
      const { page } = t;
      const hidden = () => page.evaluate(async () => Object.keys((await import('/src/platform/looks.ts')).useLooks.getState().bySource.storeys3d ?? {}));
      const level = (name) => page.locator('nav.browser .tree-item', { hasText: `${name} ·` }).first();
      await level('Ground floor').click({ button: 'right' });
      await t.wait(300);
      t.expect(await page.locator('.context-menu').getByRole('menuitem', { name: 'Show only this level in 3D' }).count() === 1, 'a level\'s right-click offers to show it alone in 3D');
      const nav = await page.locator('nav.browser').boundingBox();
      const menu = await page.locator('.context-menu').boundingBox();
      // (Wider than the menu: its Romanian is longer.)
      await t.shot('storeys-3d-menu', { clip: { x: nav.x, y: Math.max(0, menu.y - 80), width: Math.max(nav.width, menu.x + menu.width * 1.35 - nav.x) + 12, height: menu.height + 120 } });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Show only this level in 3D' }).click();
      await t.wait(1200);
      let off = await hidden();
      t.expect(upper.every((id) => off.includes(id)) && !off.includes(walls[0]), 'the first floor is hidden, the ground floor shown');
      t.expect(await page.locator('.mode-chip.storeys').getByText('Ground floor', { exact: true }).count() === 1, 'the chip says which storey is shown');
      await t.fit();
      await t.shot('storeys-3d', { of: '[data-testid=view3d]', pad: 0 });
      // The 3D follows the active level: the first floor and those below it.
      await level('First floor').click({ button: 'right' });
      await page.locator('.context-menu').getByRole('menuitem', { name: 'Show this level and below in 3D' }).click();
      await t.wait(800);
      off = await hidden();
      t.expect(!upper.some((id) => off.includes(id)) && !off.includes(walls[0]), 'the first floor and those below are shown');
      await level('Ground floor').click();
      await t.wait(800);
      off = await hidden();
      t.expect(upper.every((id) => off.includes(id)), 'picking another level, the 3D follows it');
      // The cut above the level, set in the 3D view's properties, says so on the chip.
      await t.cmd('view.setStoreys', { id: '3d', cut: 1.2 });
      await t.wait(500);
      t.expect(await page.locator('.mode-chip.storeys').getByText('cut at 1200 mm').count() === 1, 'the chip says where the model is cut');
      // The cut is closed like a section's: each cut layer capped in its material.
      const capped = await t.call('view.caps', { planes: [{ normal: [0, 0, -1], constant: 1.2 }] });
      t.expect(Array.isArray(capped) && capped.length > 0, 'the walls cut above the level are closed by caps');
      await t.wait(1000);
      await t.fit();
      await t.shot('storeys-3d-cut', { of: '[data-testid=view3d]', pad: 0 });
      // × shows every storey again.
      await page.locator('.mode-chip.storeys button.mode-close').click();
      await t.wait(800);
      t.expect(await page.locator('.mode-chip.storeys').count() === 0 && (await hidden()).length === 0, 'the chip\'s × shows every storey');
    },
  },
  {
    // Groups and clearing up in 3D (decision 204, docs/groups.md): a region selected in 3D, made a group, opened by a double-click.
    id: 'groups',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await open3d(t);
      await t.fit();
      const { page } = t;
      const view = await page.locator('[data-testid=view3d]').boundingBox();
      const state = () => page.evaluate(async () => {
        const { useUi } = await import('/src/app/store.ts');
        const { useSession } = await import('/src/engine/session.ts');
        const { useScope } = await import('/src/platform/scope.ts');
        const nodes = useSession.getState().nodes;
        return { selected: useUi.getState().selected, groups: [...nodes.values()].filter((n) => n.type === 'IfcGroup').map((n) => n.id), scope: useScope.getState().scope?.id ?? null };
      });
      // View → Select region, a crossing dragged right to left over the house (shot while it is dragged).
      await t.tool('view.selectRegion');
      const at = (fx, fy) => [view.x + view.width * fx, view.y + view.height * fy];
      const [x0, y0] = at(0.78, 0.78), [x1, y1] = at(0.22, 0.25);
      await page.mouse.move(x0, y0);
      await page.mouse.down();
      await page.mouse.move(x1, y1, { steps: 14 });
      await t.wait(300);
      await t.shot('select-region-3d', { clip: view });
      await page.mouse.up();
      await t.wait(600);
      let st = await state();
      t.expect(st.selected.length >= 5, `the crossing takes the house's elements, took ${st.selected.length}`);
      // Grouped (GP): one group, selected, its members lit.
      await t.tool('modify.group');
      await t.wait(800);
      st = await state();
      t.expect(st.groups.length === 1 && st.selected[0] === st.groups[0], 'the selection made one group, selected');
      await t.tool('view.selectRegion');
      await t.shot('group-selected', { clip: { x: view.x, y: view.y, width: view.width + 360, height: view.height } });
      // A garden wall outside the group, to be seen faded while the group is open.
      await t.cmd('architecture.addWall', { storeyId: await t.storey(), x1: -3, y1: -4, x2: 15, y2: -4 });
      await t.wait(800);
      // A double-click on a member opens the group: the rest faded, its members picked one by one.
      const [cx, cy] = at(0.5, 0.55);
      await page.mouse.dblclick(cx, cy);
      await t.wait(800);
      st = await state();
      t.expect(st.scope === st.groups[0], 'a double-click on a member opens its group');
      await t.shot('group-edit', { clip: view });
      await page.keyboard.press('Escape');
      await t.wait(400);
      // Lines drawn on the plan (as an imported DXF's are) grouped from the right-click menu (a tester's report, #98).
      const plan = `plan:${await t.storey()}`;
      const sketch = [[[16, 0], [22, 0]], [[22, 0], [22, 5]], [[22, 5], [16, 5]], [[16, 5], [16, 0]], [[16, 2.5], [22, 2.5]]];
      const drawn = (await t.cmd('drafting.draw', { viewId: plan, shapes: sketch.map(([a, b]) => ({ type: 'line', a, b })) })).ids;
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false, selection: drawn[0], selected: drawn });
      await t.wait(1200);
      await t.fit();
      await t.wait(600);
      // The sketch's left line, so the menu opens over the plan.
      const boxes = [];
      for (const m of await page.locator('path.plan-mark.selected').all()) boxes.push(await m.boundingBox());
      const line = boxes.filter(Boolean).sort((a, b) => a.x + a.width / 2 - (b.x + b.width / 2))[0];
      await page.mouse.click(line.x + line.width / 2, line.y + line.height / 2, { button: 'right' });
      await t.wait(400);
      const item = page.locator('.context-menu button, .context-menu [role=menuitem]').filter({ hasText: /^Create group$/ }).first();
      t.expect(await item.count() === 1, 'the right-click menu offers Create group for drafted lines');
      await t.shot('group-drafting', { of: '[data-testid=plan]', pad: 0 });
      await item.click();
      await t.wait(800);
      st = await state();
      const made = st.groups.find((g) => g !== st.groups[0]) ?? st.groups[st.groups.length - 1];
      t.expect(st.groups.length === 2 && st.selected.join() === made, 'the drafted lines made one group, selected');
      await page.mouse.click(line.x + line.width / 2, line.y + line.height / 2, { button: 'right' });
      await t.wait(400);
      t.expect(await page.locator('.context-menu').getByText('Ungroup', { exact: true }).count() === 1, 'on a group the menu offers Ungroup');
      await page.keyboard.press('Escape');
    },
  },
  {
    // Drill-down picking (platform/assemblies.ts), off by default: View → Drill into assemblies; a pick of a hall's rafter
    // takes the hall, the next the rafter, the breadcrumb names both, Esc steps back up to the hall.
    id: 'drill-select',
    async run(t) {
      await t.fresh();
      await t.cmd('steel.addHall', { storeyId: await t.storey(), attrs: { secondary: false } });
      await t.wait(2500);
      const nodes = await t.nodes();
      const hall = nodes.find((n) => n.type === 'LapinSteelHall');
      const rafter = nodes.find((n) => n.props?.hallRole === 'f1:raf:a');
      t.expect(hall && rafter, 'the hall and its rafters are made');
      await t.ui({ dialog: null, keyPlan: false, selection: null, selected: [] });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.3, pitch: -0.45, distance: 52, target: { x: 10, y: 15, z: 3 } }, false));
      await t.wait(1500);
      await t.tool('view.drillSelect');
      // What a click on the rafter does (the views call the same pickClick).
      const pick = (id) => t.page.evaluate(async (id) => {
        (await import('/src/platform/assemblies.ts')).pickClick(id);
        return (await import('/src/app/store.ts')).useUi.getState().selected;
      }, id);
      t.expect((await pick(rafter.id)).join() === hall.id, 'the first click on a rafter picks its hall');
      t.expect((await pick(rafter.id)).join() === rafter.id, 'the next picks the rafter');
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=drill-crumbs] button').count() === 3, 'the breadcrumb names the storey, the hall and the rafter');
      await t.shot('drill-select', { of: '[data-testid=view3d]', pad: 0 });
      await t.page.keyboard.press('Escape');
      await t.wait(400);
      const selected = await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().selected);
      t.expect(selected.join() === hall.id, 'Esc steps back up to the hall');
      // Off again, as it is by default (kept on this computer).
      await t.tool('view.drillSelect');
    },
  },
  {
    id: 'views-3d-layers',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await open3d(t);
      await t.fit();
      const { page } = t;
      // The parts the 3D draws, of all the render sends, in the mode the 3D view resolves to (layers3d.ts).
      const parts = () => page.evaluate(async () => {
        const { useSession } = await import('/src/engine/session.ts');
        const { layers3dOf, shownIn } = await import('/src/viewport/layers3d.ts');
        const st = useSession.getState();
        const mode = layers3dOf(st.nodes, null).mode;
        let all = 0, shown = 0;
        for (const it of st.render.values()) { all += it.parts?.length ?? 0; shown += shownIn(it, mode).parts?.length ?? 0; }
        return { mode, all, shown };
      });
      let p = await parts();
      t.expect(p.mode === 'all' && p.shown === p.all, 'a 3D view shows every layer at first');
      // A Structural 3D view: the cores alone, set in its properties (nothing selected, the 3D in front).
      await t.ui({ selection: null, selected: [] });
      await t.wait(400);
      const props = page.locator('aside.panel.props');
      await props.locator('dt', { hasText: 'Discipline' }).locator('xpath=following-sibling::dd[1]').locator('select').selectOption('Structure');
      await t.wait(1500);
      p = await parts();
      t.expect(p.mode === 'core' && p.shown < p.all, `a Structural 3D view shows the cores alone (${p.shown} of ${p.all} parts)`);
      t.expect(await page.locator('.mode-chip.layers').getByText('the core only').count() === 1, 'the chip says the cores alone are shown');
      const layersSelect = props.locator('[data-testid=view-structure-setting]');
      t.expect(await layersSelect.inputValue() === 'core', 'the 3D view\'s properties say the core only');
      // The section box's caps close the cores alone.
      const capped = await t.call('view.caps', { planes: [{ normal: [-1, 0, 0], constant: 6 }], layers: 'core' });
      const every = await t.call('view.caps', { planes: [{ normal: [-1, 0, 0], constant: 6 }] });
      const count = (c) => (c ?? []).reduce((n, it) => n + it.parts.length, 0);
      t.expect(count(capped) > 0 && count(capped) < count(every), 'a cut closes the cores alone');
      await t.fit();
      await t.shot('layers-3d-core', { of: '[data-testid=view3d]', pad: 0 });
      // Without finishes, chosen in the properties: the insulation is back, the plaster stays out.
      await layersSelect.selectOption('noFinishes');
      await t.wait(1200);
      p = await parts();
      t.expect(p.mode === 'noFinishes', 'the view sets its own layers over its style');
      // × shows every layer again.
      await page.locator('.mode-chip.layers button.mode-close').click();
      await t.wait(1200);
      p = await parts();
      t.expect(p.mode === 'all' && p.shown === p.all && await page.locator('.mode-chip.layers').count() === 0, 'the chip\'s × shows every layer');
    },
  },
  {
    id: 'drafting',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.fit();
      // Copy, as the command line asks for it.
      const wall = (await t.nodes()).find((n) => n.type === 'IfcWall');
      await select(t, wall.id);
      await t.page.locator('.band').getByRole('button', { name: 'Copy', exact: true }).click();
      await t.wait(300);
      const [px, py] = await t.planPoint(0, 0);
      await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py);
      const [qx, qy] = await t.planPoint(2, 2.5);
      await t.page.mouse.move(qx, qy, { steps: 10 });
      await t.wait(300);
      const asks = await t.page.locator('[data-testid=command-line]').innerText();
      t.expect(/point/i.test(asks), `the command line asks for the next point (it says: ${asks.replace(/\s+/g, ' ').slice(0, 120)})`);
      await t.shot('command-line', { clip: { x: 250, y: 120, width: 860, height: 730 } });
      await t.keys('Escape', 'Escape');
      // Move the inner wall (decision 165): from its middle (snapped), partway across the room with ortho on.
      await select(t, walls.inner);
      await t.page.locator('.band').getByRole('button', { name: 'Move', exact: true }).click();
      await t.wait(300);
      await t.page.evaluate(() => document.activeElement instanceof HTMLElement && document.activeElement.blur());
      await t.keys('F8');
      const [bx, by] = await t.planPoint(7, 4);
      await t.page.mouse.move(bx, by, { steps: 3 }); await t.page.mouse.click(bx, by);
      const [mx, my] = await t.planPoint(9.6, 4.3);
      await t.page.mouse.move(mx, my, { steps: 12 });
      await t.wait(400);
      t.expect(await t.page.locator('.move-trace .move-base').count() === 1, 'the base point keeps its marker while the wall moves');
      t.expect(await t.page.locator('.move-trace .move-vector').count() === 1, 'a line runs from the base point to the mouse');
      t.expect(await t.page.locator('.move-trace .move-ghost').count() > 0, 'a ghost of the wall stays where it was');
      t.expect(await t.page.locator('.live-box .live-label', { hasText: 'Δx' }).count() === 1, 'the readout at the mouse shows Δx');
      await t.shot('move-trace', { clip: { x: 250, y: 120, width: 920, height: 730 } });
      t.expect(await t.page.locator('.move-trace.trace-locked').count() === 1, 'ortho gives the line its locked colour');
      await t.keys('Escape', 'Escape', 'F8');
      t.expect(await t.page.locator('.move-trace').count() === 0, 'Esc takes the trace away');
      // Dragging the selected wall by its body (not a grip): the same trace from the press; Esc before letting go leaves it.
      await select(t, walls.inner);
      const [wx, wy] = await t.planPoint(7, 2.2);
      await t.page.mouse.move(wx, wy, { steps: 3 });
      await t.page.mouse.down();
      const [vx, vy] = await t.planPoint(8.4, 2.6);
      await t.page.mouse.move(vx, vy, { steps: 10 });
      await t.wait(300);
      t.expect(await t.page.locator('.move-trace .move-ghost').count() > 0, 'dragging the selected wall itself leaves its ghost where it was');
      t.expect(await t.page.locator('.move-trace .move-base').count() === 1, 'and keeps a marker where it was pressed');
      t.expect(await t.page.locator('.live-box .live-label', { hasText: 'Δx' }).count() === 1, 'and shows the offset at the mouse');
      await t.keys('Escape');
      t.expect(await t.page.locator('.move-trace').count() === 0, 'Esc ends the drag');
      await t.page.mouse.up();
      // A lasso: a window box dragged to the right, Space turns it into a lasso that goes round the left wall and the inner wall only.
      await t.ui({ selection: null, selected: [] });
      // Zoomed out, so the lasso's corners round the house fall clear of the HUD and the key plan over the view.
      await t.keys('-', '-');
      await t.wait(400);
      const lasso = [[-1, 9], [8, 9], [8, -1], [6, -1], [6, 8.5], [1, 8.5], [1, -1], [-1, -1]];
      const [lx, ly] = await t.planPoint(...lasso[0]);
      await t.page.mouse.move(lx, ly, { steps: 3 });
      await t.page.mouse.down();
      const [bx2, by2] = await t.planPoint(...lasso[1]);
      await t.page.mouse.move(bx2, by2, { steps: 8 });
      await t.wait(200);
      t.expect(await t.page.locator('rect.select-box.window').count() === 1, 'a drag to the right is a window box');
      await t.page.keyboard.press('Space');
      for (const [x, y] of lasso.slice(2)) { const [sx, sy] = await t.planPoint(x, y); await t.page.mouse.move(sx, sy, { steps: 8 }); }
      await t.wait(300);
      t.expect(await t.page.locator('polygon.select-box.lasso.window').count() === 1, 'Space turns the box into a lasso, still a window');
      await t.shot('select-lasso', { clip: { x: 250, y: 120, width: 920, height: 730 } });
      await t.page.mouse.up();
      await t.wait(300);
      const taken = await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().selected);
      t.expect(taken.length === 2 && taken.includes(walls[3]) && taken.includes(walls.inner), `the lasso takes the two walls wholly inside it (took ${taken.length})`);
      await t.ui({ selection: null, selected: [] });
      await t.fit();
      await t.button('Draft', { exact: true });
      await t.shot('ribbon-draft', { of: '.topbar', pad: 0 });
      await t.button('Annotate', { exact: true });
      await t.shot('ribbon-annotate', { of: '.topbar', pad: 0 });
    },
  },
  {
    // A tester's report (decisions 453, 454): Move, Mirror and dragging in a section or an elevation act in its plane. A beam
    // dragged up in the south elevation rises by its top offset, which Properties shows while the drag lasts; Rotate is refused.
    id: 'section-move',
    touches: ['core', 'drafting', 'structure'],
    async run(t) {
      await t.fresh();
      const page = t.page;
      const storeyId = await t.storey();
      for (const x of [0, 6]) await t.cmd('structure.addColumn', { storeyId, x, y: 0 });
      const beam = (await t.cmd('structure.addBeam', { storeyId, x1: 0, y1: 0, x2: 6, y2: 0 })).id;
      const south = (await t.cmd('view.create', { kind: 'elevation', direction: 'south', name: 'South' })).id;
      await t.wait(800);
      await openFromBrowser(t, 'South', 2500);
      await t.fit();
      await select(t, beam);
      await t.wait(800);
      // Where the beam's drawn line is on the screen: the middle of its first segment, through the view's own zoom.
      const at = async (dy = 0) => {
        const p = await page.evaluate(async ([viewId, id, dy]) => {
          const svg = document.querySelector('[data-testid=drawing]');
          const view = (await import('/src/viewport/zoom.ts')).recall(viewId);
          const d = await (await import('/src/engine/session.ts')).query('view.drawing', { id: viewId });
          const m = d.marks.find((x) => x.id === id) ?? d.marks.find((x) => x.id.startsWith(`${id}:`));
          if (!svg || !view || !m) return null;
          const s = m.segments, x = (s[0] + s[2]) / 2, y = (s[1] + s[3]) / 2 + dy;
          const r = svg.getBoundingClientRect();
          return [r.left + (x - view.cx) * view.scale + r.width / 2, r.top + r.height / 2 - (y - view.cy) * view.scale];
        }, [south, beam, dy]);
        if (!p) throw new Error('the beam is not drawn in the elevation');
        return p;
      };
      const offset = () => page.evaluate(() => {
        const dt = [...document.querySelectorAll('aside.props dt')].find((x) => x.textContent?.trim() === 'Offset from level');
        const input = dt?.nextElementSibling?.querySelector('input');
        return input ? input.value : null;
      });
      const before = await offset();
      t.expect(before != null, 'the beam\'s offset from its level is in its properties');
      const [x0, y0] = await at();
      const [x1, y1] = await at(0.5);
      await page.mouse.move(x0, y0, { steps: 3 });
      await page.mouse.down();
      await page.mouse.move(x1, y1, { steps: 12 });
      await t.wait(400);
      t.expect(await page.locator('.move-trace .move-ghost').count() > 0, 'dragging the selected beam in the elevation leaves its ghost where it was');
      t.expect(await offset() !== before, `its offset from the level follows the drag in Properties (${before} → ${await offset()})`);
      await t.shot('section-drag', { clip: { x: 250, y: 120, width: 1180, height: 730 } });
      await page.mouse.up();
      // Let go about 0.5 m up (the drag snaps, to the columns' tops perhaps): the beam is that much higher, by its top offset.
      await t.until(async () => ((await t.nodes()).find((n) => n.id === beam)?.props.topOffset ?? 0) > 0.25, 'let go, the beam is higher: its top offset');
      const moved = (await t.nodes()).find((n) => n.id === beam);
      t.expect(Math.abs(moved.props.start[1]) < 1e-6, 'and it stays on its line in plan');
      // Rotate there: refused, with what to do instead.
      const refused = await page.evaluate(async (id) => {
        const s = await import('/src/engine/session.ts');
        await s.execute('element.transform', { ids: [id], op: 'rotate', center: [0, 0], angle: 90, frame: { origin: [0, 0, 0], right: [1, 0, 0], up: [0, 0, 1] } });
        return s.useSession.getState().error;
      }, beam);
      t.expect(/tip them over/.test(refused ?? ''), `a turn of the beam in the elevation is refused (${refused})`);
    },
  },
  {
    // A tester's report (#106, decision 382): a DXF imported into a plan lies in the 3D view on its storey's level, in its
    // layers' colours, its hatch faint; one import is switched off and on, and View → DXF in 3D hides them all.
    id: 'dxf-3d',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      const s = await t.storey();
      const nodes = await t.nodes();
      const plan = nodes.find((n) => n.type === 'LapinView' && (n.props.kind ?? 'plan') === 'plan' && n.props.storeyId === s && !n.props.site)?.id ?? `plan:${s}`;
      const r = await t.cmd('drafting.importDxf', { viewId: plan, name: 'site.dxf', data: Buffer.from(siteDxf()).toString('base64') });
      t.expect(typeof r?.importId === 'string', 'the import is recorded, to be shown in 3D as one');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.2, pitch: -0.55, distance: 34, target: { x: 6, y: 4, z: 0 } }, false));
      await t.wait(1200);
      const drawn = async () => Number(await t.page.locator('[data-testid=view3d]').getAttribute('data-underlays') ?? 0);
      t.expect(await drawn() >= 4, `the DXF's layers are drawn in 3D (${await drawn()} batches)`);
      await t.shot('dxf-3d', { of: '[data-testid=view3d]', pad: 0 });
      // The import off in 3D (its shapes' properties, In 3D), then on again.
      await t.cmd('drafting.setUnderlay3d', { id: r.importId, shown: false });
      await t.wait(800);
      t.expect(await drawn() === 0, 'an import switched off is not drawn in 3D');
      await t.cmd('drafting.setUnderlay3d', { id: r.importId, shown: true });
      await t.wait(800);
      t.expect(await drawn() >= 4, 'switched on again, it is back');
      // View → DXF in 3D: every import hidden, and shown again.
      const on = () => t.page.evaluate(async () => (await import('/src/app/display.ts')).useDisplay.getState().dxf3d);
      await t.tool('view.dxf3d');
      t.expect(!(await on()), 'View → DXF in 3D hides them');
      await t.tool('view.dxf3d');
      t.expect(await on(), 'and shows them again');
    },
  },
  {
    // An imported DXF kept by the project (a tester's report #113, decision 386): imported as File → Import DXF… does (into the
    // level's own plan view, made then), grouped, saved, the project opened again: the plan shows it without importing it again.
    id: 'dxf-kept',
    async run(t) {
      await t.fresh();
      const view = await t.page.evaluate(async () => (await import('/src/viewport/frontView.ts')).frontView());
      t.expect(typeof view === 'string' && !view.startsWith('plan:'), 'the import goes into the level\'s own plan view');
      await t.cmd('drafting.importDxf', { viewId: view, name: 'plan.dxf', data: Buffer.from(siteDxf()).toString('base64') });
      const drafted = (await t.nodes()).filter((n) => n.type === 'IfcAnnotation' && n.props.viewId === view).map((n) => n.id);
      await t.cmd('group.create', { ids: drafted, name: 'Ground floor DXF' });
      const path = join(mkdtempSync(join(tmpdir(), 'lapin-guide-dxf-')), 'house.lapin');
      await t.call('project.save', { path });
      await t.page.evaluate(async (p) => (await import('/src/engine/files.ts')).openProject(p), path);
      await t.wait(1500);
      const kept = (await t.nodes()).filter((n) => n.type === 'IfcAnnotation' && n.props.viewId === view).length;
      t.expect(kept === drafted.length && (await t.nodes()).some((n) => n.type === 'IfcGroup'), `the DXF and its group come back (${kept} of ${drafted.length})`);
      // The plan in front is the level's own: its drawing holds the DXF's lines.
      const shown = await t.page.evaluate(async () => {
        const ui = (await import('/src/app/store.ts')).useUi.getState();
        const { useSession, activeStorey } = await import('/src/engine/session.ts');
        const st = activeStorey();
        return st ? (await import('/src/viewport/storeyPlan.ts')).planViewIdOf(useSession.getState().nodes, st.id, ui.planViewId) : null;
      });
      t.expect(shown === view, `the plan shown after opening is the one the DXF is on (${shown})`);
    },
  },
  {
    // Point clouds linked (docs/point-clouds.md, decision 395): a scan of the house linked as a LAS, indexed in the background,
    // streamed into the 3D view in its own colours; Manage → Links lists it with its points and how it is drawn.
    id: 'point-cloud',
    async run(t) {
      await t.fresh();
      const r = await t.cmd('pointcloud.link', { name: 'house-scan.las', data: scanLas().toString('base64') });
      t.expect(typeof r?.id === 'string' && r.placement?.mode === 'origin', 'the scan is linked, origin to origin (it is in local coordinates)');
      let st = null;
      for (let k = 0; k < 120 && st?.state !== 'ready'; k++) { st = await t.call('doc.query', { name: 'pointcloud.status', args: { id: r.id } }); await t.wait(250); }
      t.expect(st?.state === 'ready', `the scan is indexed (${st?.state})`);
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.25, pitch: -0.42, distance: 30, target: { x: 6, y: 4, z: 1.5 } }, false));
      await t.wait(2500);
      const drawn = async () => Number(await t.page.locator('[data-testid=view3d]').getAttribute('data-cloud-points') ?? 0);
      t.expect(await drawn() > 100000, `its points are drawn in 3D (${await drawn()})`);
      await t.shot('point-cloud-3d', { of: '[data-testid=view3d]', pad: 0 });
      await t.ui({ dialog: 'links' });
      await t.page.waitForSelector('[data-cloud-status=ready]', { timeout: 20_000 });
      await t.shot('point-cloud-links', { of: '.dialog', pad: 0 });
      await t.ui({ dialog: null });
    },
  },
  {
    // A point cloud in a plan and in Visibility / Graphics (decision 396): the scan's slice at the plan's cut drawn as dots under
    // the walls being traced over it, its points snapped to; the view's Point clouds tab hides its vegetation and halftones it.
    id: 'point-cloud-plan',
    async run(t) {
      await t.fresh();
      const r = await t.cmd('pointcloud.link', { name: 'house-scan.las', data: scanLas().toString('base64') });
      let st = null;
      for (let k = 0; k < 120 && st?.state !== 'ready'; k++) { st = await t.call('doc.query', { name: 'pointcloud.status', args: { id: r.id } }); await t.wait(250); }
      t.expect(st?.state === 'ready', `the scan is indexed (${st?.state})`);
      const s = await t.storey();
      // A wall traced over the scan, as a user does it: its outer face on the scan's south face; the other three still to trace.
      await t.cmd('architecture.addWall', { storeyId: s, x1: 0, y1: 0.15, x2: 12, y2: 0.15 });
      const plan = (await t.cmd('view.create', { kind: 'plan', storeyId: s, name: 'Ground floor scan' })).id;
      await t.ui({ view: 'plan', planViewId: plan, keyPlan: false });
      await t.wait(1500);
      await t.fit();
      await t.wait(1200);
      const pictures = await t.page.locator('image.plan-orthophoto').count();
      t.expect(pictures >= 1, `the scan's slice is drawn in the plan (${pictures})`);
      const drawing = await t.call('doc.query', { name: 'view.drawing', args: { id: plan } });
      const snaps = (drawing.marks ?? []).find((m) => m.id === r.id)?.snaps?.length ?? 0;
      t.expect(snaps > 100, `its points are snaps (${snaps / 2})`);
      await t.shot('point-cloud-plan', { of: '.plan-svg', pad: 0 });
      // Visibility / Graphics → Point clouds: its classes, vegetation hidden, halftone in this view.
      await t.tool('view.layers');
      const dialog = t.page.locator('[data-testid=vv-dialog]');
      await dialog.waitFor({ timeout: 10_000 });
      await dialog.locator('[data-tab=pointClouds]').click();
      await dialog.locator('button[data-cloud-classes]').click();
      await dialog.locator('tr[data-class="5"] input[type=checkbox]').click();
      await t.wait(800);
      const vis = await t.call('doc.query', { name: 'view.visibility', args: { id: plan } });
      t.expect(JSON.stringify(vis.graphics?.pointClouds?.[r.id]?.hiddenClasses) === '[5]', 'the vegetation hidden in this view');
      await t.shot('point-cloud-vg', { of: '[data-testid=vv-dialog]', pad: 0, maxHeight: 640 });
      await t.ui({ dialog: null });
    },
  },
  {
    // A tester's report (decision 312): a wall opening edited by its grips as a window is, OFFSET read in the project's
    // millimetres, then a line drafted in the 3D view on the storey's level, standing there in 3D as the plan's drafts do.
    id: 'draft-3d',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.fit();
      const page = t.page;
      // The wall opening: picked in its gap on the plan, its middle and both jambs as grips.
      const opening = (await t.cmd('architecture.addWallOpening', { wallId: walls[3], along: 4, width: 1, height: 2.1 })).id;
      await t.wait(800);
      const [ox, oy] = await t.planPoint(0, 4);
      await page.mouse.click(ox, oy);
      await t.wait(500);
      const picked = await page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().selection);
      t.expect(picked === opening, `a click in the opening's gap selects the opening (took ${picked})`);
      const grips = await page.locator('[data-grip]').count();
      t.expect(grips >= 3, `the opening shows its grips: its middle and its two jambs (${grips})`);
      await t.ui({ selection: null, selected: [] });
      // OFFSET's distance in the project's unit: 500 is half a metre in millimetres.
      await page.evaluate(() => document.activeElement instanceof HTMLElement && document.activeElement.blur());
      await t.keys('o', 'Enter');
      await t.keys('5', '0', '0', 'Enter');
      const asks = await page.locator('[data-testid=command-line]').innerText();
      t.expect(/500 mm/.test(asks), `OFFSET shows its distance in millimetres (it says: ${asks.replace(/\s+/g, ' ').slice(0, 120)})`);
      await t.keys('Escape', 'Escape');
      // A line in the 3D view, on the ground floor's level.
      await open3d(t);
      const before = (await t.nodes()).filter((n) => n.props.predefinedType === 'DRAFT').length;
      await page.evaluate(() => document.activeElement instanceof HTMLElement && document.activeElement.blur());
      await t.keys('l', 'Enter');
      await t.wait(600);
      t.expect(await page.locator('[data-draft3d=plane]').count() === 1, 'the chip says where the line is drawn');
      const box = await page.locator('[data-testid=view3d]').boundingBox();
      const at = (fx, fy) => [box.x + box.width * fx, box.y + box.height * fy];
      for (const [fx, fy] of [[0.35, 0.72], [0.62, 0.66]]) {
        const [x, y] = at(fx, fy);
        await page.mouse.move(x, y, { steps: 6 });
        await page.mouse.click(x, y);
        await t.wait(500);
      }
      const [cx, cy] = at(0.7, 0.5);
      await page.mouse.move(cx, cy, { steps: 10 });
      await t.wait(500);
      const drafts = (await t.nodes()).filter((n) => n.props.predefinedType === 'DRAFT');
      t.expect(drafts.length === before + 1, `two clicks in 3D draft a line (${drafts.length - before})`);
      t.expect(await page.locator('[data-draft3d=overlay] .cad-preview').count() > 0, 'the next segment follows the mouse in 3D');
      await t.shot('draft-3d', { of: '[data-testid=view3d]', pad: 0 });
      await t.keys('Escape', 'Escape');
      const lined = await page.evaluate(async (id) => !!(await import('/src/engine/session.ts')).useSession.getState().render.get(id)?.lines?.length, drafts.at(-1).id);
      t.expect(lined, 'the line stands in the 3D view, as the plan\'s drafts do');
    },
  },
  {
    // Studio's thick solids and reference planes (docs/plan-studio-thick.md): native walls round a circle with RING on the plan,
    // a box with its bottom and top, a wall strip, a reference plane on the south facade with a ring drawn on it.
    id: 'studio',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const plan = `plan:${storey}`;
      await t.button('Studio', { exact: true });
      await t.shot('ribbon-studio', { of: '.topbar', pad: 0 });
      // A circle drafted beside the house; RING from the ribbon picks it and, on a plan, lays native walls round it.
      const circle = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'circle', c: [15, 4], r: 1.5 } })).ids[0];
      await t.ui({ keyPlan: false });
      await t.wait(600);
      await t.fit();
      // Zoomed out about the house, so the circle beside it is on the screen.
      { const [px, py] = await t.planPoint(8, 4); await t.page.mouse.move(px, py); await t.page.mouse.wheel(0, 500); await t.wait(600); }
      await t.tool('studio.ring');
      const asks = await t.page.locator('[data-testid=command-line]').innerText();
      t.expect(/pick a circle/i.test(asks), `RING asks for a circle (it says: ${asks.replace(/\s+/g, ' ').slice(0, 120)})`);
      const [cx, cy] = await t.planPoint(16.5, 4);
      await t.page.mouse.move(cx, cy, { steps: 3 }); await t.page.mouse.click(cx, cy);
      await t.wait(400);
      await t.keys('Enter');
      await t.wait(1200);
      const curved = (await t.nodes()).filter((n) => n.type === 'IfcWall' && typeof n.props.arc === 'number' && n.props.arc < 0);
      t.expect(curved.length === 4, `four curved walls round the circle, found ${curved.length}`);
      await t.keys('Escape');
      // A box 3 × 2 m with its bottom and top, a Studio solid; a wall strip along an L.
      const rect = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'polyline', pts: [[14, -5], [18, -5], [18, -2], [14, -2]], closed: true } })).ids[0];
      const box = await t.cmd('studio.box', { profileId: rect, thickness: 0.2, height: 1.2, bottom: true, top: true, bottomThickness: 0.15, topThickness: 0.1 });
      t.expect(box?.slabs?.length === 2 && box.assembly, 'the box has its bottom and top, grouped in an assembly');
      const l = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'polyline', pts: [[0, -3], [8, -3], [8, -5]], closed: false } })).ids[0];
      await t.cmd('studio.wall', { profileId: l, thickness: 0.25, height: 0.9, justify: 'left' });
      t.expect(circle, 'the circle drawn');
      // A reference plane on the south facade, facing south; a ring drawn on it stands off the wall. The plan draws the plane
      // where it cuts it (a dashed line with its name), the 3D view as a translucent pane.
      const south = (await t.nodes()).filter((n) => n.type === 'IfcWall' && n.props.start?.[1] === 0 && n.props.end?.[1] === 0)[0];
      t.expect(south, 'the south wall');
      const face = await t.cmd('view.create', { kind: 'plane', name: 'South facade', point: [6, -0.2, 0], normal: [0, -1, 0], elementId: south.id });
      const ringPlane = (await t.cmd('drafting.draw', { viewId: face.id, shape: { type: 'circle', c: [5.3, 2.2], r: 0.45 } })).ids[0];
      await t.cmd('studio.ring', { profileId: ringPlane, thickness: 0.08, height: 0.15 });
      await t.ui({ view: 'plan', keyPlan: false, selection: null, selected: [] });
      await t.wait(800);
      await t.fit();
      const planDrawing = await t.call('doc.query', { name: 'view.drawing', args: { id: plan } });
      t.expect(planDrawing?.marks?.some((m) => m.id === face.id), 'the plan draws the reference plane where it cuts it');
      await t.shot('studio-thick-plan', { of: '[data-testid=plan]', pad: 0 });
      await select(t, box.id);
      await t.wait(800);
      await t.shot('studio-box-properties', { of: 'aside.props', pad: 0, maxHeight: 700 });
      await t.ui({ view: 'drawing', drawingViewId: face.id, selection: null, selected: [] });
      await t.wait(2000);
      await t.fit();
      await t.shot('studio-plane', { of: '[data-testid=drawing]', pad: 0 });
      t.expect(await t.page.locator('nav.browser').getByText('South facade', { exact: true }).count() > 0, 'the browser lists the reference plane');
      await select(t, face.id);
      await t.wait(800);
      await t.shot('studio-plane-properties', { of: 'aside.props', pad: 0, maxHeight: 700 });
      // In 3D the selected plane shows as a pane with the handle that drags it along its normal.
      await t.ui({ keyPlan: true });
      await open3d(t);
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=ref-plane-handle]').count() === 1, 'the selected reference plane has its handle in 3D');
      await t.shot('studio-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    id: 'materials',
    async run(t) {
      await t.fresh();
      await house(t);
      // A material with the user's own picture on it.
      const render = (await t.nodes()).find((n) => n.type === 'IfcMaterial' && n.name === 'Render');
      t.expect(render, 'the Render material');
      const image = await t.cmd('image.add', { name: 'bricks.png', mime: 'image/png', data: await brickPng(t) });
      await t.cmd('material.update', { id: render.id, textureImage: image.id, textureSize: [1.2, 1.2] });
      await t.tool('manage.materials');
      await t.page.locator('.dialog').getByText('Render', { exact: true }).first().click();
      await t.wait(500);
      // A .pat file, imported as a user picks it.
      const chooser = t.page.waitForEvent('filechooser');
      await t.button('Import .pat');
      await (await chooser).setFiles({ name: 'herringbone.pat', mimeType: 'text/plain', buffer: Buffer.from(PAT) });
      await t.wait(800);
      t.expect(await t.page.getByText('My patterns').count() > 0, 'My patterns keeps the imported file');
      await t.shot('materials-dialog', { of: '.dialog', pad: 0 });
      await t.shot('own-image', { of: '.own-image', pad: 10 });
      await t.shot('my-patterns', { of: '.my-patterns', pad: 10 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await open3d(t);
      await t.shot('own-image-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // A material's data (docs/plan-buildup-tags.md): its description, λ from the usual parameters, measured by area with its
    // catalogue of thicknesses; a layer of another thickness marked in Wall types, and the takeoff by material a row per thickness.
    id: 'materials-data',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const layers = (wool) => [{ material: 'Render', thickness: 0.01 }, { material: 'Mineral wool', thickness: wool }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Plaster', thickness: 0.015 }];
      const t150 = await t.cmd('architecture.createWallType', { name: 'ETICS 150', layers: layers(0.15) });
      const t130 = await t.cmd('architecture.createWallType', { name: 'ETICS 130', layers: layers(0.13) });
      await t.cmd('architecture.addWall', { storeyId: s, x1: 0, y1: 0, x2: 10, y2: 0, typeId: t150.id });
      await t.cmd('architecture.addWall', { storeyId: s, x1: 0, y1: 4, x2: 6, y2: 4, typeId: t130.id });
      await t.tool('manage.materials');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-material="Mineral wool"]').click();
      await t.wait(400);
      await dialog.locator('[data-field=mat-data-tab]').click();
      await t.wait(300);
      t.expect(await dialog.locator('[data-testid=material-data]').count() === 1, 'the Data tab shows the material\'s data');
      await dialog.locator('[data-field=mat-description]').fill('Mineral wool boards, Knauf FKD-S Thermal\nfixed with dowels, joints staggered');
      await dialog.locator('[data-field=mat-measure-by]').focus();
      await t.wait(600);
      // λ from the usual parameters, in IFC's own set; its value typed in its row.
      await dialog.locator('[data-field=mat-param-preset]').selectOption({ index: 1 });
      await t.wait(800);
      const lambda = dialog.locator('tr[data-param="ThermalConductivity"] input.cell');
      t.expect(await lambda.count() === 1, 'Thermal conductivity λ is added in Pset_MaterialThermal');
      await lambda.fill('0.035');
      await lambda.press('Enter');
      await t.wait(600);
      await dialog.locator('[data-field=mat-measure-by]').selectOption('area');
      await t.wait(800);
      await dialog.locator('[data-field=mat-thicknesses]').fill('50, 80, 100, 150, 200, 240');
      await dialog.locator('[data-field=mat-thicknesses]').press('Tab');
      await t.wait(800);
      const wool = (await t.nodes()).find((n) => n.type === 'IfcMaterial' && n.name === 'Mineral wool');
      t.expect(wool?.props.description?.startsWith('Mineral wool boards'), 'the description is saved');
      t.expect(wool?.props.measure?.by === 'area' && wool.props.measure.thicknesses?.join(',') === '50,80,100,150,200,240', 'measured by area, with its catalogue thicknesses');
      t.expect(wool?.props.params?.some((p) => p.pset === 'Pset_MaterialThermal' && p.name === 'ThermalConductivity' && p.value === 0.035), 'λ = 0.035 in Pset_MaterialThermal.ThermalConductivity');
      await t.shot('material-data', { of: '.dialog', pad: 0 });
      await dialog.locator('button[title="Close"]').first().click();
      await t.wait(300);
      // Wall types: the 130 mm layer marked, the catalogue offered beside the thickness.
      await t.ui({ wallTypeId: t130.id });
      await t.tool('architecture.wallTypes');
      await t.wait(600);
      t.expect(await t.page.locator('.dialog [data-testid=layer-off-catalogue]').count() === 1, 'a layer not of its material\'s catalogue thicknesses is marked');
      t.expect(await t.page.locator('.dialog [data-field=layer-catalogue]').count() === 1, 'the catalogue is offered beside the wool\'s thickness');
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      // The takeoff by material: a row per thickness, the 130 mm one with its note.
      const takeoff = await t.cmd('architecture.createSchedule', { kind: 'quantities' });
      await t.cmd('architecture.updateSchedule', { id: takeoff.id, groupBy: ['material'], classes: ['IfcWall'] });
      await t.ui({ view: 'schedule', scheduleId: takeoff.id });
      await t.wait(1500);
      t.expect(await t.page.locator('[data-testid=schedule-grid] th', { hasText: /Thickness|Grosime|Espesor|Dicke|Épaisseur|Tykkelse|Spessore|Grubość|Espessura/ }).count() === 1, 'the takeoff by material has a Thickness column');
      t.expect(await t.page.locator('[data-testid=schedule-grid] input[value="150"]').count() >= 1, 'the wool 150 mm has its own row');
    },
  },
  {
    // The build-up tag (docs/plan-buildup-tags.md, stage 2): a facade wall drawn east to west (its exterior, its first layer's
    // side, to the north), its materials described with their λ; the default build-up tag placed by TAG above it, a second
    // tag type with a leader per layer beside its end; then the tag type's editor in Drafting standards → Tags with its preview.
    id: 'buildup-tag',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const type = await t.cmd('architecture.createWallType', { name: 'ETICS 150 on clay blocks', layers: [
        { material: 'Silicone render', thickness: 0.005 }, { material: 'Mineral wool', thickness: 0.15 },
        { material: 'Clay block', thickness: 0.25, core: true }, { material: 'Gypsum plaster', thickness: 0.015 }] });
      const id = async (name) => (await t.nodes()).find((n) => n.type === 'IfcMaterial' && n.name === name)?.id;
      const lambda = (v) => [{ pset: 'Pset_MaterialThermal', name: 'ThermalConductivity', value: v }];
      await t.cmd('material.setData', { id: await id('Silicone render'), description: 'Silicone render, 1.5 mm grain, on reinforced adhesive' });
      await t.cmd('material.setData', { id: await id('Mineral wool'), description: 'Mineral wool boards, fixed with dowels', params: lambda(0.035) });
      await t.cmd('material.setData', { id: await id('Clay block'), description: 'Clay blocks 25 cm, M10 mortar', params: lambda(0.2) });
      await t.cmd('material.setData', { id: await id('Gypsum plaster'), description: 'Gypsum plaster, two coats', params: lambda(0.57) });
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, x1: 9, y1: 0, x2: 0, y2: 0, typeId: type.id })).id;
      // A second build-up type: no header or footer, a leader into each layer.
      await t.cmd('standards.set', { kind: 'tag', entry: { id: 'buildup-layers', name: 'Build-up, a leader per layer', shape: 'buildup', template: '{description}: {thickness} mm',
        classes: 'IfcWall', unit: 'mm', leaders: 'per-layer', bullet: 'number', width: 70 } });
      await t.fit();
      await t.button('Annotate', { exact: true });
      await t.tool('cad.tag');
      await t.page.locator('[data-tag-type]').selectOption('buildup');
      await t.wait(300);
      for (const [x, y] of [[4.5, 0], [1, 2.4]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(600); }
      await t.keys('Escape', 'Escape');
      const tags = (await t.nodes()).filter((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === 'TAG');
      t.expect(tags.length === 1 && tags[0].props.tagType === 'buildup', `TAG puts the build-up tag on the wall (${tags.map((x) => x.props.tagType).join(', ')})`);
      // Above the wall, its exterior side, the note's head where a drafter would put it.
      // Its knee (decision 331) at 60° above the wall's middle, the note on its shoulder to the right.
      await t.cmd('drafting.updateTag', { id: tags[0].id, at: [7.5, 5.2] });
      const second = await t.cmd('drafting.addTag', { viewId: `plan:${s}`, element: wall, tagType: 'buildup-layers', at: [10.4, 1.5] });
      await t.cmd('drafting.updateTag', { id: second.id, end: [8, 0] });
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.page.evaluate(async () => (await import('/src/viewport/zoom.ts')).zoomHost()?.zoomWindow({ x: -0.8, y: -2.8 }, { x: 17.8, y: 5.9 }));
      await t.wait(800);
      const plan = t.page.locator('[data-testid=plan]');
      t.expect(await plan.locator('text', { hasText: 'EXTERIOR' }).count() === 1, 'the tag writes its header');
      t.expect(await plan.locator('text', { hasText: /λ = 0.035 W\/mK:/ }).count() === 1, 'a layer\'s line: its material\'s description, its λ, its thickness');
      t.expect(await plan.locator('text', { hasText: /^Silicone render, 1.5 mm grain/ }).count() >= 1, 'the render has no λ: its optional part is left out');
      const [x0, y0] = await t.planPoint(-0.7, 5.8);
      const [x1, y1] = await t.planPoint(17.7, -2.7);
      await t.shot('buildup-tag', { clip: { x: x0, y: y0, width: x1 - x0, height: y1 - y0 } });
      // The tag type's editor, beside its preview on the project's wall type.
      await t.tool('manage.standards');
      const dialog = t.page.locator('.dialog');
      await dialog.getByText('Tags', { exact: true }).first().click();
      await t.wait(400);
      await dialog.locator('[data-field=tag-shape-buildup]').first().click();
      await t.wait(1200);
      t.expect(await dialog.locator('[data-buildup-editor] [data-field=tag-preview] text').count() >= 6, 'the editor shows its preview on the wall type');
      for (const f of ['tag-header', 'tag-line', 'tag-footer', 'tag-order', 'tag-zero-layers', 'tag-leaders', 'tag-leader-shape', 'tag-underline', 'tag-extension', 'tag-bullet', 'tag-width'])
        t.expect(await dialog.locator(`[data-field=${f}]`).count() === 1, `the editor has its ${f}`);
      await t.shot('buildup-tag-type', { of: '[data-buildup-editor]', pad: 6 });
    },
  },
  {
    // Materials for rendering (report #76): from the library, the appearance with its preview, every window's frame and glass at once, the house in 3D.
    id: 'materials-render',
    async run(t) {
      await t.fresh();
      await house(t);
      await t.tool('manage.materials');
      const dialog = t.page.locator('.dialog');
      await dialog.locator('[data-action=material-library]').click();
      await t.wait(400);
      t.expect(await dialog.locator('[data-preset="Glass blue tinted"]').count() === 1, 'the library offers blue glass');
      await t.shot('material-library', { of: '.dialog', pad: 0 });
      await dialog.locator('[data-preset="Aluminium RAL 7016 anthracite"]').click();
      await t.wait(600);
      await dialog.locator('[data-action=material-library]').click();
      await dialog.locator('[data-preset="Glass blue tinted"]').click();
      await t.wait(1500);
      t.expect(await dialog.locator('[data-testid=material-preview]').count() === 1, 'the appearance shows its live preview');
      t.expect(await dialog.locator('[data-material="Glass blue tinted"].on').count() === 1, 'the new glass is the one edited');
      await t.shot('material-appearance', { of: '.dialog', pad: 0 });
      await dialog.locator('button[title="Close"]').first().click();
      await t.wait(300);
      // Every window's frame one dark grey metal, every glass one blue glass: from the configurator, in one step.
      await t.tool('architecture.joineryTypes');
      const conf = t.page.locator('.dialog');
      await conf.locator('.type-list .tree-item', { hasText: 'Window' }).first().click();
      await t.wait(600);
      const parts = conf.locator('[data-testid=joinery-materials]');
      await parts.locator('[data-part=frame] select').selectOption('Aluminium RAL 7016 anthracite');
      await parts.locator('[data-part=glass] select').selectOption('Glass blue tinted');
      await t.wait(400);
      await parts.locator('[data-action=joinery-materials-all]').click();
      await t.wait(800);
      const windowType = (await t.nodes()).find((n) => n.type === 'IfcWindowType');
      t.expect(windowType?.props?.spec?.materials?.frame === 'Aluminium RAL 7016 anthracite', 'every window type takes the frame material');
      t.expect(windowType?.props?.spec?.materials?.glass === 'Glass blue tinted', 'every window type takes the glass material');
      await parts.scrollIntoViewIfNeeded();
      await t.shot('joinery-materials', { of: '[data-testid=joinery-materials]', pad: 8 });
      await conf.locator('button[title="Close"]').first().click();
      await t.wait(300);
      // The doors' frames too, as the command line or another type would.
      await t.cmd('architecture.joineryMaterials', { kind: 'door', frame: 'Aluminium RAL 7016 anthracite' });
      await open3d(t);
      await t.shot('materials-render-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // A door or window's frame position in the wall and its data as free text (a tester's report #76): in the configurator the
    // frame set back 50 mm from the exterior finish, the Data box with its U-value, fire rating and a parameter of the user's
    // own; then a window selected, with its own fire rating over the type's.
    id: 'joinery-data',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      await t.tool('architecture.joineryTypes');
      await t.wait(900);
      const dialog = t.page.locator('.dialog.wide').first();
      const windowType = (await t.nodes()).find((n) => n.type === 'IfcWindowType');
      await t.page.locator('.dialog .type-list .tree-item').filter({ hasText: windowType.name }).first().click();
      await t.wait(700);
      await t.page.locator('.dialog [data-field=position-from]').selectOption('exterior');
      const back = dialog.locator('label.field-line', { hasText: 'Set back' }).locator('input');
      await back.fill('50');
      await back.press('Enter');
      await t.wait(500);
      await t.page.locator('.dialog button', { hasText: /^(Save type|Create type)$/ }).first().click();
      await t.wait(900);
      const window = (await t.nodes()).find((n) => n.id === windowType.id);
      t.expect(window && window.props.spec.position.from === 'exterior' && Math.abs(window.props.spec.position.distance - 0.05) < 1e-6, 'the type keeps its position: 50 mm from the exterior finish');
      await t.shot('joinery-position', { of: '.dialog.wide', pad: 0 });
      // The Data box: free text, never a list.
      await t.page.locator('[data-testid=joinery-data]').click();
      await t.page.waitForSelector('[data-testid=joinery-data-form]', { timeout: 10000 });
      await t.page.locator('[data-field=uValue]').fill('0,9 W/m²K');
      await t.page.locator('[data-field=gValue]').fill('0.52');
      await t.page.locator('[data-field=safetyGlass]').fill('laminated inside, tempered outside');
      await t.page.locator('[data-field=fireRating]').fill('EI 30');
      await t.page.locator('[data-field=fittings]').fill('Roto NX');
      await t.page.locator('[data-testid=joinery-extra-add]').click();
      await t.page.locator('[data-field=extra-name]').last().fill('Sauna window');
      await t.page.locator('[data-field=extra-value]').last().fill('yes');
      await t.shot('joinery-data', { of: '[aria-label="Door and window data"] .dialog', pad: 0 });
      await t.page.locator('[data-testid=joinery-data-save]').click();
      await t.wait(900);
      const type = (await t.nodes()).find((n) => n.id === window.id);
      const names = (type.props.params ?? []).map((p) => p.name);
      t.expect(['U-value', 'g-value', 'Safety glass', 'FireRating', 'Fittings', 'Sauna window'].every((n) => names.includes(n)), `the data is the type's parameters (${names.join(', ')})`);
      t.expect((type.props.params ?? []).find((p) => p.name === 'FireRating')?.pset === 'Pset_WindowCommon', 'the fire rating is IFC\'s own');
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(400);
      // A window selected: its frame position and its own data over the type's.
      const fill = (await t.nodes()).find((n) => n.type === 'IfcWindow');
      await select(t, fill.id);
      await t.wait(900);
      t.expect(await t.page.locator('aside.props [data-field=position-from]').count() === 1, 'a window offers its own frame position');
      await t.page.locator('[data-testid=joinery-data]').click();
      await t.page.waitForSelector('[data-testid=joinery-data-form]', { timeout: 10000 });
      await t.page.locator('[data-field=fireRating]').fill('EI 60');
      await t.page.locator('[data-testid=joinery-data-save]').click();
      await t.wait(900);
      const own = (await t.nodes()).find((n) => n.id === fill.id);
      t.expect((own.props.params ?? []).some((p) => p.name === 'FireRating' && p.value === 'EI 60'), 'the window\'s own fire rating is its own');
      t.expect(!(own.props.params ?? []).some((p) => p.name === 'U-value'), 'what is the type\'s stays the type\'s');
      await t.shot('joinery-data-properties', { of: 'aside.props', pad: 0, maxHeight: 620 });
    },
  },
  {
    // The kitchen's layouts (a tester's report #124): six kitchen types placed in plan — one wall, galley, L-shaped, U-shaped,
    // island and peninsula, each with its sink, hob and tall unit — and the types dialog on the L-shaped one.
    id: 'kitchen-layouts',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.fit();
      await t.button('Interior', { exact: true });
      const layouts = ['one-wall', 'galley', 'l-shaped', 'u-shaped', 'island', 'peninsula'];
      const ids = {};
      for (const [i, layout] of layouts.entries()) {
        const type = (await t.cmd('furniture.createType', { kind: 'kitchen', layout })).id;
        ids[layout] = type;
        await t.cmd('furniture.place', { storeyId: s, typeId: type, x: (i % 3) * 5, y: Math.floor(i / 3) * 4.5, against: false });
      }
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      await t.wait(1200);
      const pieces = (await t.nodes()).filter((n) => n.type === 'IfcFurniture');
      t.expect(pieces.length === 6, `six kitchens placed (${pieces.length})`);
      await t.shot('kitchen-layouts', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('furniture.types');
      await t.wait(400);
      const lType = (await t.nodes()).find((n) => n.id === ids['l-shaped']);
      await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: lType.name }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-param=layout] select').count() === 1, 'a kitchen type has its layout');
      t.expect(await t.page.locator('.dialog [data-param=leg] input').count() === 1, 'an L has its leg');
      t.expect(await t.page.locator('.dialog [data-param=gap]').count() === 0, 'what the layout does not use is not shown');
      await t.shot('kitchen-layout-types', { of: '.dialog', pad: 0 });
    },
  },
  {
    // A kitchen's own units (a tester's report #76, decision 472): a kitchen type laid from units and appliances — a base
    // unit with one door, a 1½ bowl sink with a pull-out tap, a 450 dishwasher, a hob over an oven, drawers, a tall fridge —
    // with a wall line of its own (a glazed unit, a taller one, a chimney hood); the types dialog's base and wall lines
    // beside the engine's preview; the kitchen fitted to its units, in 3D; the kitchen units schedule.
    id: 'kitchen-units',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.button('Interior', { exact: true });
      const u = (type, width, more = {}) => ({ type, width, ...more });
      const type = (await t.cmd('furniture.createType', {
        kind: 'kitchen', layout: 'one-wall', name: 'Kitchen with its units',
        spec: {
          tall: 'none',
          units: [u('base', 0.4, { front: 'doors', hand: 'right' }), u('sink', 0.8, { sink: 'one-half', tap: 'pull-out' }), u('dishwasher', 0.45), u('hob', 0.6, { fuel: 'induction' }), u('base', 0.6, { front: 'drawers', drawers: 3 }), u('fridge', 0.6, { fridge: 'tall' })],
          upper: [u('wall', 0.4, { front: 'glass', doors: 1 }), u('wall', 0.8, { height: 0.9 }), u('space', 0.45), u('hood', 0.6), u('wall', 0.6)],
          materials: { fronts: 'Lacquered MDF sage', plinth: 'Plinth anthracite' },
        },
      })).id;
      const kitchen = (await t.cmd('furniture.place', { storeyId: s, typeId: type, x: 0, y: 0, against: false })).id;
      await select(t, kitchen);
      await t.wait(800);
      await t.page.locator('[data-action=kitchen-fit]').click();
      await t.wait(800);
      const piece = (await t.nodes()).find((n) => n.id === kitchen);
      t.expect(Math.abs(piece.props.length - 3.45) < 1e-6, `Fit to units makes the kitchen as long as its units (${piece.props.length})`);
      await t.ui({ selection: null, selected: [] });
      await t.tool('furniture.types');
      await t.wait(400);
      await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: 'Kitchen with its units' }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=kitchen-units] .kitchen-unit').count() === 6, 'the base line lists its six units');
      t.expect(await t.page.locator('.dialog [data-testid=kitchen-upper] .kitchen-unit').count() === 5, 'the wall line lists its five');
      // A drawer unit's front changed in the dialog: one step on the type, seen in the preview.
      await t.page.locator('.dialog [data-testid=kitchen-units] .kitchen-unit').nth(4).locator('[data-unit-field=drawers] input').fill('4');
      await t.page.keyboard.press('Enter');
      await t.wait(1200);
      const spec = (await t.nodes()).find((n) => n.id === type).props.spec;
      t.expect(spec.units[4].drawers === 4, `the drawers changed on the type (${spec.units[4].drawers})`);
      // The base line under the preview, the preview held at the top of the dialog's view.
      await t.page.locator('.dialog [data-testid=kitchen-units-editor]').evaluate((el) => el.scrollIntoView({ block: 'end' }));
      await t.wait(300);
      await t.shot('kitchen-units-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await t.cmd('architecture.createSchedule', { kind: 'kitchenUnits' });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.79, pitch: -0.3, distance: 6.5, target: { x: 0, y: 0, z: 1.1 } }, false));
      await t.wait(1500);
      await t.shot('kitchen-units-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // Casework of boards (decision 475): a sideboard on tapered legs from its preset, a TV unit hung on the wall with fluted
    // fronts, a sliding-door wardrobe and a desk with its knee space; the configurator with its preset, styles, carcasses
    // and zones beside the engine's preview and cut list; a style applied in one step; the pieces in 3D; the cut list.
    id: 'casework',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.button('Interior', { exact: true });
      const make = async (preset, name, spec = {}) => (await t.cmd('furniture.createType', { kind: 'casework', preset, name, spec })).id;
      const sideboard = await make('sideboard', 'Oak sideboard', { materials: { fronts: 'Lacquered MDF sage' } });
      const tv = await make('tvUnit', 'TV unit fluted', { frontStyle: 'fluted', materials: { fronts: 'Walnut veneer' } });
      const wardrobe = await make('wardrobeSliding', 'Sliding wardrobe');
      const desk = await make('desk', 'Desk with pedestal');
      await t.cmd('furniture.place', { storeyId: s, typeId: sideboard, x: 0, y: 0, against: false });
      await t.cmd('furniture.place', { storeyId: s, typeId: tv, x: 2.4, y: 0, against: false });
      await t.cmd('furniture.place', { storeyId: s, typeId: wardrobe, x: 5, y: 0, against: false });
      await t.cmd('furniture.place', { storeyId: s, typeId: desk, x: 7.6, y: 0, against: false });
      await t.tool('furniture.types');
      await t.wait(400);
      await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: 'Oak sideboard' }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=casework-editor]').count() === 1, 'a casework type shows its configurator');
      t.expect(await t.page.locator('.dialog [data-testid=casework-carcasses] .casework-carcass').count() === 3, 'the sideboard lists its three carcasses');
      // A style applied from the dialog: one step on the type.
      await t.page.locator('.dialog [data-style=handleless]').click();
      await t.wait(1200);
      const spec = (await t.nodes()).find((n) => n.id === sideboard).props.spec;
      t.expect(spec.handles === 'none', `the handleless style changed the type (${spec.handles})`);
      await t.page.locator('.dialog [data-testid=casework-cutlist] summary').click();
      await t.wait(300);
      t.expect(await t.page.locator('.dialog [data-testid=casework-cutlist] tbody tr').count() > 8, 'the preview lists the cut list');
      // The carcasses and their zones in view, the preview held at the top of the dialog's view.
      await t.page.locator('.dialog [data-testid=casework-carcasses]').evaluate((el) => el.scrollIntoView({ block: 'end' }));
      await t.wait(300);
      await t.shot('casework-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await t.cmd('architecture.createSchedule', { kind: 'cutList' });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.95, pitch: -0.32, distance: 12, target: { x: 4.2, y: 0, z: 0.8 } }, false));
      await t.wait(1500);
      await t.shot('casework-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // My furniture library (decision 476): a desk of boards saved from Furniture types → Save to my library… with its category,
    // tags, article code and price beside the thumbnail drawn from the engine's preview; five more kept the same way; Interior →
    // My furniture… with its thumbnails, an item inserted (the identical type in the project reused) and one placed (its tool
    // started). The entries are deleted at the end, so the next scenes' library stays empty.
    id: 'furniture-library',
    touches: ['core', 'furniture'],
    async run(t) {
      await t.fresh();
      await t.button('Interior', { exact: true });
      const make = async (preset, name, spec = {}) => (await t.cmd('furniture.createType', { kind: 'casework', preset, name, spec })).id;
      await make('desk', 'Walnut desk', { width: 1.4, materials: { body: 'Walnut veneer', fronts: 'Lacquered MDF sage' } });
      const others = [
        [await make('sideboard', 'Oak sideboard', { materials: { fronts: 'Lacquered MDF sage' } }), 'Oak sideboard', 'living', ['oak', 'modern']],
        [await make('wardrobeSliding', 'Sliding wardrobe'), 'Sliding wardrobe', 'bedroom', ['sliding']],
        [await make('tvUnit', 'TV unit fluted', { frontStyle: 'fluted', materials: { fronts: 'Walnut veneer' } }), 'TV unit fluted', 'living', ['walnut', 'fluted']],
        [await make('vanity', 'Bathroom vanity'), 'Bathroom vanity', 'bathroom', ['white']],
        [await make('bookcase', 'Tall bookcase'), 'Tall bookcase', 'storage', ['oak']],
      ];
      const kept = [];
      try {
        // Five kept by the page's own code, as the dialog keeps them (the type's item, the preview's thumbnail).
        for (const [typeId, name, category, tags] of others)
          kept.push(await t.page.evaluate(async ([typeId, name, category, tags]) => (await import('/src/modules/furniture/library.ts')).saveToLibrary(typeId, { name, category, tags }), [typeId, name, category, tags]));
        // The desk from the dialog.
        await t.tool('furniture.types');
        await t.wait(400);
        await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: 'Walnut desk' }).first().click();
        await t.wait(800);
        await t.page.locator('.dialog [data-action=furniture-save-library]').click();
        const save = t.page.locator('[data-dialog=furniture-save-library]');
        await save.waitFor({ timeout: 10_000 });
        await save.locator('[data-field=library-tags]').fill('walnut, home office');
        await save.locator('[data-field=library-article]').fill('WD-140');
        await save.locator('[data-field=library-price]').fill('2450');
        await save.locator('.furniture-thumb img').waitFor({ timeout: 10_000 });
        t.expect(await save.locator('[data-field=library-save-category]').inputValue() === 'office', 'a desk starts in the office category');
        await t.shot('furniture-save-library', { of: '[data-dialog=furniture-save-library]', pad: 0 });
        await save.locator('[data-action=library-save]').click();
        await save.waitFor({ state: 'detached', timeout: 10_000 });
        const entries = await t.call('furnitureLibrary.list', {});
        kept.push(...entries.filter((e) => e.name === 'Walnut desk').map((e) => e.id));
        t.expect(entries.length === 6 && entries.some((e) => e.name === 'Walnut desk' && e.article === 'WD-140' && e.price === 2450), 'the library keeps the six, the desk with its article and price');
        await t.page.locator('.dialog button[title="Close"]').first().click();
        await t.wait(300);

        // Interior → My furniture…: the thumbnails; the sideboard inserted (the project's identical one reused), the desk placed.
        await t.tool('furniture.library');
        const lib = t.page.locator('[data-dialog=furniture-library]');
        await lib.waitFor({ timeout: 10_000 });
        await lib.locator('.furniture-card').nth(5).waitFor({ timeout: 10_000 });
        await t.wait(500);
        await t.shot('furniture-library', { of: '[data-dialog=furniture-library]', pad: 0 });
        const types = async () => (await t.nodes()).filter((n) => n.type === 'IfcFurnitureType').length;
        const before = await types();
        await lib.locator('[data-library-entry="Oak sideboard"] [data-action=library-insert]').click();
        await t.wait(600);
        t.expect(await types() === before, 'inserting a type the project has reuses it');
        await lib.locator('[data-library-entry="Walnut desk"] [data-action=library-place]').click();
        await lib.waitFor({ state: 'detached', timeout: 10_000 });
        const tool = await t.page.evaluate(async () => (await import('/src/app/store.ts')).useUi.getState().tool);
        t.expect(tool === 'furniture.casework', `Place starts the casework tool (${tool})`);
        await t.page.keyboard.press('Escape');
      } finally {
        for (const id of kept) await t.call('furnitureLibrary.delete', { id }).catch(() => undefined);
      }
    },
  },
  {
    // Furniture from CC0 models stretched by zones (decision 477): a sofa stretched to 2.6 m (its arms and legs as they
    // were), two armchairs, a coffee table on a rug, a floor lamp and plants; the types dialog on the sofa with its gallery,
    // sizes, looks and colours beside the engine's preview; a look applied in one step; the room in 3D.
    id: 'furniture-models',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      await t.button('Interior', { exact: true });
      const make = async (model, name, spec = {}) => (await t.cmd('furniture.createType', { kind: 'model', name, spec: { model, ...spec } })).id;
      const sofa = await make('loungeSofa', 'Sofa 2600', { width: 2.6, materials: { upholstery: 'Linen beige', legs: 'Oak solid wood' } });
      const chair = await make('loungeChair', 'Armchair', { materials: { upholstery: 'Velvet sage' } });
      const table = await make('tableCoffee', 'Coffee table', { materials: { top: 'Oak solid wood' } });
      const rug = await make('rugRectangle', 'Rug 2400', { materials: { upholstery: 'Bouclé off-white', accent: 'Fabric charcoal' } });
      const lamp = await make('lampRoundFloor', 'Floor lamp');
      const plant = await make('pottedPlant', 'Plant');
      const place = (typeId, x, y, rotation = 0) => t.cmd('furniture.place', { storeyId: s, typeId, x, y, rotation, against: false });
      await place(rug, 0, -1.2);
      await place(sofa, 0, 0);
      await place(table, 0, -1.25);
      await place(chair, -1.75, -1.6, 90);
      await place(chair, 1.75, -1.6, 270);
      await place(lamp, 1.75, 0.1);
      await place(plant, -1.75, 0.1);
      await t.tool('furniture.types');
      await t.wait(400);
      await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: 'Sofa 2600' }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=furniture-model-editor]').count() === 1, 'a type of a model shows its configurator');
      t.expect(await t.page.locator('.dialog [data-testid=furniture-model-gallery] .model-tile').count() >= 20, 'the gallery shows the models');
      t.expect(await t.page.locator('.dialog .model-tile.on[data-model=loungeSofa]').count() === 1, 'the sofa\'s model is marked');
      // A look from the dialog: one step on the type, materials for the roles the sofa has.
      await t.page.locator('.dialog [data-look=warm]').click();
      await t.wait(1200);
      const spec = (await t.nodes()).find((n) => n.id === sofa).props.spec;
      t.expect(spec.materials.upholstery === 'Velvet terracotta' && !('shade' in spec.materials), `the warm look set the sofa's materials (${JSON.stringify(spec.materials)})`);
      t.expect(await t.page.locator('.dialog [data-testid=furniture-preview] .furniture-front polygon').count() > 20, 'the preview shows the sofa from the front');
      await t.shot('furniture-models-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 2.4, pitch: -0.45, distance: 7.5, target: { x: 0, y: -0.8, z: 0.3 } }, false));
      await t.wait(1500);
      await t.shot('furniture-models-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // Furniture (docs/plan-furniture.md): a living room and a bedroom furnished — a sofa clicked by the wall (backed onto it),
    // armchairs, a coffee table, a table with chairs round it, a bed, bedside tables, a wardrobe, a kitchen drawn along a
    // wall — in plan by their symbols and in 3D; the types dialog with its preview; a piece's properties.
    id: 'furniture',
    async run(t) {
      await t.fresh();
      const { storey: s } = await house(t, { roof: false });
      await t.fit();
      await t.button('Interior', { exact: true });
      // The sofa by a click near the south wall of the living room: its back on the wall, its front to the room.
      await t.tool('furniture.sofa');
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=furniture-against].on').count() === 1, 'the bar backs pieces onto walls');
      { const [px, py] = await t.planPoint(4.6, 0.7); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(800); }
      await t.keys('Escape');
      const pieces = async () => (await t.nodes()).filter((n) => n.type === 'IfcFurniture');
      const sofa = (await pieces())[0];
      t.expect(sofa && Math.abs((sofa.props.rotation ?? 0) - 180) < 1e-6, `the sofa clicked by the wall faces the room (${sofa?.props.rotation})`);
      const place = (kind, x, y, extra = {}) => t.cmd('furniture.place', { storeyId: s, kind, x, y, ...extra });
      await place('armchair', 2.3, 3.6, { against: false, rotation: 90 });
      await place('armchair', 6.3, 3.6, { against: false, rotation: 270 });
      await place('coffeeTable', 4.0, 2.45, { against: false });
      const table = (await place('table', 3.4, 6.0, { against: false })).id;
      await t.cmd('furniture.chairsAround', { tableId: table });
      await place('sideboard', 6.3, 7.6);
      await t.cmd('furniture.kitchen', { storeyId: s, x1: 0.4, y1: 7.6, x2: 0.4, y2: 4.6 });
      await place('bed', 9.5, 7.6);
      await place('bedside', 8.2, 7.6);
      await place('bedside', 10.8, 7.6);
      await place('wardrobe', 11.5, 2.3);
      await place('desk', 8.5, 0.5);
      await place('chair', 8.5, 1.15, { against: false, rotation: 0 });
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      await t.wait(1200);
      t.expect((await pieces()).length >= 18, `the rooms are furnished (${(await pieces()).length} pieces)`);
      await t.shot('furniture-plan', { of: '[data-testid=plan]', pad: 0 });
      // The types dialog: a sofa type with a chaise longue, seen in its preview.
      const sofaType = (await t.nodes()).find((n) => n.type === 'IfcFurnitureType' && n.props.spec?.kind === 'sofa');
      await t.cmd('furniture.updateType', { id: sofaType.id, spec: { chaise: 'right' } });
      await t.tool('furniture.types');
      await t.wait(400);
      await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: sofaType.name }).first().click();
      await t.wait(1500);
      t.expect(await t.page.locator('.dialog [data-testid=furniture-preview] path').count() > 4, 'the dialog previews the type\'s plan symbol');
      t.expect(await t.page.locator('.dialog [data-param=chaise] select').count() === 1, 'the sofa\'s own parameters are edited there');
      await t.shot('furniture-types', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      // A piece's properties.
      await select(t, sofa.id);
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=furniture-section]').count() === 1, 'a piece shows its furniture properties');
      await t.shot('furniture-properties', { of: '[data-testid=furniture-section]', pad: 8 });
      await t.ui({ selection: null, selected: [] });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.95, pitch: -1.05, distance: 16, target: { x: 6, y: 4, z: 0.2 } }, false));
      await t.wait(1500);
      await t.shot('furniture-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // Balconies (a tester's report, #96): on the first floor of the house, a Juliet balcony clicked by its door with the
    // Balcony tool, a balcony stacked on posts (and stacked again on the second floor), a corner balcony, a loggia cut into
    // the north wall, a curved one with a parapet, one hung from tie rods and one with rounded corners; the plan (the
    // second floor's balcony overhead, dashed), the configurator (its plan and section), a balcony's properties, 3D.
    id: 'balconies',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      const ff = await t.storey('First floor');
      const outline = [[0, 0], [12, 0], [12, 8], [0, 8]];
      const walls = [];
      for (let i = 0; i < 4; i++) {
        const [a, b] = [outline[i], outline[(i + 1) % 4]];
        walls.push((await t.cmd('architecture.addWall', { storeyId: ff, x1: a[0], y1: a[1], x2: b[0], y2: b[1] })).id);
      }
      await t.cmd('architecture.addSlab', { storeyId: ff, points: outline });
      await t.cmd('architecture.addDoor', { wallId: walls[0], x: 2.2, y: 0 });
      await t.cmd('architecture.addDoor', { wallId: walls[0], x: 6.5, y: 0 });
      await openFromBrowser(t, 'First floor');
      await t.fit();
      // A step back, so the south wall's face is clear of the quick tools.
      { const box = await t.page.locator('[data-testid=plan]').boundingBox(); await t.page.mouse.move(box.x + box.width / 2, box.y + box.height / 3); await t.page.mouse.wheel(0, 500); await t.wait(600); }
      // The Balcony tool: its style in the command line's bar, a click outside the south wall by the door — a Juliet balcony on it.
      await t.tool('architecture.balcony');
      t.expect(await t.page.locator('[data-testid=balcony-style]').count() === 1, 'the bar offers the balcony styles');
      await t.page.locator('[data-testid=balcony-style]').selectOption('juliet');
      await t.wait(300);
      { const [px, py] = await t.planPoint(2.5, -0.5); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(900); }
      await t.keys('Escape');
      const balconies = async () => (await t.nodes()).filter((n) => n.type === 'LapinBalcony');
      const juliet = (await balconies())[0];
      t.expect(juliet && juliet.props.kind === 'juliet' && Math.abs(juliet.props.along - 2.2) < 1e-6, `the Juliet balcony is centred on the door (${juliet?.props.along})`);
      const add = (style, x, y) => t.cmd('architecture.addBalcony', { storeyId: ff, style, x, y });
      const stacked = (await add('stacked', 6.5, -0.5)).id;
      await add('corner', 11.6, -0.5);
      const loggia = (await add('loggia', 3.5, 8.5)).id;
      await add('curved', 9, 8.5);
      await add('hung', 12.5, 4);
      await add('rounded', -0.5, 4);
      const recess = (await t.nodes()).find((n) => n.type === 'IfcOpeningElement' && n.props.balconyId === loggia);
      t.expect(recess && Math.abs(recess.props.width - 3) < 1e-6, 'the loggia cuts its recess into the north wall');
      // Stacked on the storey above, in one step.
      await t.cmd('storey.create', { name: 'Second floor', height: 3 });
      await t.cmd('architecture.stackBalcony', { ids: [stacked] });
      const all = await balconies();
      t.expect(all.length === 8, `seven balconies on the first floor and the stacked one above (${all.length})`);
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.fit();
      { const box = await t.page.locator('[data-testid=plan]').boundingBox(); await t.page.mouse.move(box.x + box.width / 2, box.y + box.height * 0.4); await t.page.mouse.wheel(0, 500); }
      await t.wait(1200);
      await t.shot('balconies-plan', { of: '[data-testid=plan]', pad: 0 });
      // The configurator on the stacked balcony: its plan, then its section with the posts.
      await select(t, stacked);
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=balcony-section]').count() === 1, 'a balcony shows its style and the configurator in its properties');
      await t.shot('balcony-properties', { of: 'aside.props', pad: 0, maxHeight: 620 });
      await t.tool('architecture.balconyConfigurator');
      await t.page.waitForSelector('[data-testid=balcony-dialog]', { timeout: 5000 });
      await t.wait(1200);
      t.expect(await t.page.locator('[data-testid=balcony-plan] path').count() >= 2, 'the configurator draws the balcony\'s plan');
      await t.shot('balcony-configurator', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog [role=tab]').nth(3).click();
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=balcony-section-plot] polygon').count() >= 3, 'the configurator draws its section');
      await t.shot('balcony-configurator-section', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog button[title="Close"]').first().click();
      await t.wait(300);
      await t.ui({ selection: null, selected: [] });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -0.75, pitch: -0.45, distance: 26, target: { x: 6, y: 4, z: 4 } }, false));
      await t.wait(1800);
      await t.shot('balconies-3d-north', { of: 'canvas', pad: 0 });
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 2.4, pitch: -0.4, distance: 24, target: { x: 6, y: 4, z: 4 } }, false));
      await t.wait(1800);
      await t.shot('balconies-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    // Elevators (docs/plan-elevators.md, testers' reports #54 and #97): the Elevator tool clicked in a three-storey house,
    // its configurator (the catalogue's kinds, the car, doors and stops, the shaft, EN 81's checks), the wheelchair-
    // accessible style applied, the plan with the shaft cut and the car's cross, its properties with the stops, and 3D.
    id: 'elevators',
    async run(t) {
      await t.fresh();
      const { storey: s } = await house(t, { roof: false });
      const first = await t.storey('First floor');
      const second = (await t.cmd('storey.create', { name: 'Second floor', height: 3 })).id;
      for (const st of [first, second]) await t.cmd('architecture.addSlab', { storeyId: st, points: [[0, 0], [12, 0], [12, 8], [0, 8]] });
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.button('Architecture', { exact: true });
      await t.tool('transport.elevator');
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=elevator-style]').count() === 1, 'the bar chooses the style it puts down');
      { const [px, py] = await t.planPoint(9.6, 5.4); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(1200); }
      const lifts = async () => (await t.nodes()).filter((n) => n.type === 'IfcTransportElement');
      const lift = (await lifts())[0];
      t.expect(lift, 'an elevator placed at the click');
      t.expect(await t.page.locator('[aria-label="Elevator configurator"]').count() === 1, 'its configurator opens');
      // The wheelchair-accessible style (EN 81-70 type 2), then a stop skipped by hand.
      await t.page.locator('.dialog [data-style=accessible]').first().click();
      await t.wait(1200);
      t.expect(await t.page.locator('.dialog .elevator-plan path').count() > 6, 'the configurator draws the plan of the draft');
      await t.shot('elevator-configurator', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog [role=tab]').nth(1).click();
      await t.wait(1000);
      t.expect(await t.page.locator('.dialog .elevator-section rect').count() > 6, 'and its section from the pit to the top');
      await t.shot('elevator-section', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog [data-action=elevator-apply]').click();
      await t.wait(1200);
      const applied = (await lifts())[0];
      t.expect(applied.props.accessible === 'type2' && applied.props.load === 630, `the style applied (${applied.props.accessible}, ${applied.props.load} kg)`);
      const openings = (await t.nodes()).filter((n) => n.type === 'IfcOpeningElement' && n.props.elevatorId === lift.id);
      t.expect(openings.length === 3, `the shaft opens the three floors (${openings.length})`);
      await t.ui({ dialog: null, selection: null, selected: [] });
      await t.fit();
      await t.wait(1200);
      await t.shot('elevator-plan', { of: '[data-testid=plan]', pad: 0 });
      await select(t, lift.id);
      await t.wait(1000);
      t.expect(await t.page.locator('[data-testid=elevator-section]').count() === 1, 'an elevator shows its stops in Properties');
      await t.shot('elevator-properties', { of: '[data-testid=elevator-section]', pad: 8 });
      await t.ui({ selection: null, selected: [], storeyId: s });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 2.3, pitch: -0.55, distance: 24, target: { x: 9.6, y: 5.4, z: 3.5 } }, false));
      await t.wait(1500);
      await t.shot('elevator-3d', { of: 'canvas', pad: 0 });
    },
  },
  {
    id: 'standards',
    async run(t) {
      await t.fresh();
      await t.tool('manage.standards');
      const tab = t.page.locator('.dialog').getByText('Project', { exact: true }).first();
      await tab.click();
      await t.wait(400);
      // Area rounding (report #76, decision 471): rooms' areas to 0.1, 0.5 or 1 m² (docs/user/rooms.md).
      t.expect(await t.page.locator('.dialog [data-field=areaRounding]').count() === 1, 'the Project tab has Area rounding');
      // The standard scales and each kind's scale for a new view (forum #30, decision 492).
      t.expect(await t.page.locator('.dialog [data-field=standard-scales]').count() === 1, 'the Project tab has the standard scales');
      t.expect(await t.page.locator('.dialog [data-field=plan-scale], .dialog [data-field=section-scale], .dialog [data-field=elevation-scale]').count() === 3, 'and the scales new plans, sections and elevations take');
      await t.shot('standards-project', { of: '.dialog', pad: 0 });
    },
  },
  {
    id: 'sheets',
    async run(t) {
      await t.fresh();
      await house(t);
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: await t.storey(), name: 'Ground floor plan', scale: 100 });
      const section = await t.cmd('view.create', { kind: 'section', name: 'A-A', start: [6, -2], end: [6, 10], depth: 0.5, scale: 100 });
      const sheet = await t.cmd('sheet.create', { name: 'Plans', number: 'A-101', paper: 'A3' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: plan.id, x: 130, y: 140 });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: section.id, x: 310, y: 140 });
      await t.wait(800);
      const name = (await t.nodes()).find((n) => n.id === sheet.id)?.name ?? 'Plans';
      const label = await t.page.locator('nav.browser').getByText(/Plans/).first().textContent();
      await openFromBrowser(t, label?.trim() || name, 3000);
      await t.fit();
      await t.shot('sheet', { clip: { x: 250, y: 120, width: 1190, height: 730 } });
      // What a view shows (decision 236): a named boundary drawn on the plan, the plan's view picked on the sheet (its crop
      // handles), bound to the boundary from the list on its box; then Print.
      const boundary = await t.cmd('view.addBoundary', { viewId: plan.id, a: [-1, -1], b: [7, 9], name: 'West wing' });
      await t.wait(800);
      const box = await t.page.locator('[data-testid=sheet] .sheet-viewport > rect:first-child').first().boundingBox();
      await t.page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
      await t.wait(400);
      t.expect(await t.page.locator('[data-testid=sheet] .sheet-handle.crop').count() === 8, 'a view picked on the sheet shows its eight crop handles');
      // Its view's scale beside the zoom (decision 492).
      t.expect(await t.page.locator('[data-testid=view-scale]').first().inputValue() === '100', 'a view picked on the sheet shows its scale in the corner');
      await t.page.locator('[data-testid=sheet] .sheet-vp-tools select').first().selectOption(boundary.id);
      await t.wait(1000);
      const bound = (await t.nodes()).find((n) => n.id === sheet.id)?.props?.viewports?.find((v) => v.viewId === plan.id);
      t.expect(bound?.boundary === boundary.id, 'the plan on the sheet shows its West wing boundary');
      { const b2 = await t.page.locator('[data-testid=sheet] .sheet-viewport.active > rect:first-child').first().boundingBox();
        await t.shot('sheet-view-boundary', { clip: { x: b2.x - 40, y: b2.y - 40, width: b2.width + 80, height: b2.height + 90 } }); }
      await t.page.evaluate(() => { window.print = () => { window.__printed = true; }; });
      await t.page.locator('[data-testid=sheet-print]').click();
      await t.wait(600);
      t.expect(await t.page.evaluate(() => window.__printed === true && document.querySelectorAll('#lapin-print svg path').length > 0), 'Print draws the sheet for the print dialog');
    },
  },
  {
    id: 'title-blocks',
    async run(t) {
      // A title block of one's own (decision 340, report #76): the project's data and a parameter of its own, a Finnish-style
      // block made in the designer with live fields, the sheet that uses it.
      await t.fresh();
      await house(t);
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: await t.storey(), name: 'Pohjapiirustus', scale: 100 });
      const sheet = await t.cmd('sheet.create', { name: 'Pohjapiirustus 1. krs', number: 'ARK-101', paper: 'A3' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: plan.id, x: 150, y: 120 });
      await t.cmd('sheet.titleBlock', { id: sheet.id, fields: { drawnBy: 'A. Virtanen', date: '06.11.2026', revision: 'A' } });
      await t.cmd('project.setInfo', { name: 'Kesämökki Virtanen', number: '2026-014', client: 'Matti Virtanen', address: 'Rantatie 12, 52100 Anttola',
        cadastral: '491-404-3-12', permit: '26-0123-R', designer: 'Arkkitehtitoimisto Lapin Oy', designerAddress: 'Aleksanterinkatu 1, Helsinki',
        designerEmail: 'toimisto@example.fi', chiefArchitect: 'Arkkitehti SAFA L. Laine' });
      // The project's own parameter, typed in Project information: the discipline's code the Finnish block asks.
      await t.tool('manage.projectInfo');
      await t.page.waitForSelector('[data-dialog=project-info]', { timeout: 10_000 });
      await t.page.locator('[data-field=param-new-key]').fill('suunnitteluala');
      await t.page.locator('[data-field=param-new-label]').fill('Suunnitteluala');
      await t.page.locator('[data-field=param-add]').click();
      await t.page.waitForSelector('[data-field=param-suunnitteluala]', { timeout: 10_000 });
      await t.page.locator('[data-field=param-suunnitteluala]').fill('ARK');
      await t.page.locator('[data-field=param-suunnitteluala]').press('Enter');
      await t.wait(500);
      t.expect((await t.nodes()).some((n) => n.type === 'IfcProject' && (n.props.info?.custom ?? []).some((c) => c.key === 'suunnitteluala' && c.value === 'ARK')), 'the parameter is kept on the project');
      await t.page.locator('[data-field=info-parameters]').scrollIntoViewIfNeeded();
      await t.shot('project-info-parameters', { of: '[data-dialog=project-info] [data-field=info-parameters]', pad: 6 });
      await t.page.locator('[data-dialog=project-info] .dialog-head button[title=Close]').click(); await t.wait(300);

      // The title block, made the project's default; Manage → Title blocks… lists it with a preview.
      const tb = await t.cmd('titleBlock.create', { name: 'Nimiö (FI)', template: finnishTitleBlock() });
      await t.cmd('titleBlock.setDefault', { id: tb.id });
      await t.tool('manage.titleBlocks');
      await t.page.waitForSelector('[data-dialog=title-blocks]', { timeout: 10_000 });
      await t.page.locator(`[data-field="tb-item-${tb.id}"]`).click();
      await t.page.waitForFunction(() => (document.querySelector('[data-field=tb-preview]')?.textContent ?? '').includes('Kesämökki Virtanen'), null, { timeout: 10_000 })
        .catch(() => { throw new Error('expected: the preview shows the project\'s name'); });
      await t.shot('title-blocks', { of: '[data-dialog=title-blocks]', pad: 0 });

      // The designer: a field from the list placed as a new text, its value shown at once.
      await t.page.locator('[data-field=tb-edit]').click();
      await t.page.waitForSelector('[data-dialog=title-block-designer] [data-testid=tb-canvas]', { timeout: 10_000 });
      await t.wait(500);
      await t.page.locator('[data-field="tb-field-system.file"]').click();
      const canvas = await t.page.locator('[data-testid=tb-canvas]').boundingBox();
      // 122 mm across, 2 mm up the block (the canvas shows 8 mm round it): under the drawing's number.
      const k = canvas.width / (180 + 16);
      await t.page.mouse.click(canvas.x + (8 + 122) * k, canvas.y + (8 + 56 - 2) * k);
      await t.wait(400);
      t.expect(await t.page.locator('[data-field=tb-text]').inputValue() === '{system.file}', 'the field went in as a new text');
      await t.page.locator('[data-dialog=title-block-designer] [data-field="tb-prop-height-mm-"]').fill('1.6');
      await t.page.locator('[data-field=tb-text]').focus();
      await t.wait(300);
      t.expect((await t.page.locator('[data-testid=tb-canvas]').textContent()).includes('Arkkitehtitoimisto Lapin Oy'), 'the designer shows the fields\' values');
      await t.shot('title-block-designer', { of: '[data-dialog=title-block-designer]', pad: 0 });
      await t.page.locator('[data-field=tb-save]').click();
      await t.wait(600);
      await t.page.locator('[data-dialog=title-block-designer] .dialog-head button[title=Close]').click(); await t.wait(300);
      await t.page.locator('[data-dialog=title-blocks] .dialog-head button[title=Close]').click(); await t.wait(300);

      // The sheet draws it, with its values; its properties say which title block it uses.
      const label = await t.page.locator('nav.browser').getByText(/Pohjapiirustus 1\. krs/).first().textContent();
      await openFromBrowser(t, label?.trim() || 'Pohjapiirustus 1. krs', 3000);
      await t.fit();
      const drawing = await t.page.locator('[data-testid=sheet]').textContent();
      t.expect(drawing.includes('Kesämökki Virtanen') && drawing.includes('ARK-101') && drawing.includes('Rakennuttaja'), 'the sheet draws the title block with its values');
      // The quick tools out of the picture, the sheet's corner framed with its title block (Z W, the corner's window).
      const quiet = await t.page.addStyleTag({ content: '.hotbar, .hud-card { display: none !important }' });
      const box = await t.page.locator('[data-testid=sheet]').boundingBox();
      await t.shot('sheet-title-block', { clip: { x: box.x + box.width * 0.4, y: box.y + box.height * 0.5, width: box.width * 0.6, height: box.height * 0.5 } });
      await quiet.evaluate((e) => e.remove());
    },
  },
  {
    id: 'import-ifc',
    async run(t) {
      // A file to import: the guide's house with a structure, exported as IFC.
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      for (const x of [0, 12]) await t.cmd('structure.addColumn', { storeyId: storey, x, y: 0 });
      await t.cmd('structure.addBeam', { storeyId: storey, x1: 0, y1: 0, x2: 12, y2: 0 });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const file = join(tmpdir(), 'lapin-guide-house.ifc');
      await t.call('project.exportIfc', { path: file });
      // Into a new project, as a user does: File → Import IFC…, the file picked, the dialog.
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.getByRole('menuitem', { name: 'Import IFC…' }).click();
      await (await chooser).setFiles(file);
      const dialog = t.page.locator('[aria-label="Import IFC"]');
      await dialog.waitFor({ timeout: 60_000 });
      const disciplines = await dialog.locator('[data-discipline]').evaluateAll((els) => els.map((e) => e.getAttribute('data-discipline')));
      t.expect(disciplines.includes('architecture') && disciplines.includes('structure'), 'the file\'s elements by discipline: architecture and structure');
      await dialog.locator('[data-discipline=structure] button').first().click();
      await t.wait(300);
      await t.shot('import-ifc', { of: '[aria-label="Import IFC"]', pad: 0 });
      t.expect(await dialog.locator('option[value=editable]').count() === 1, 'How offers everything editable (Studio solids)');
      // The structure left out: only the architecture comes in.
      await dialog.locator('[data-discipline=structure] input[type=checkbox]').first().uncheck();
      await dialog.getByRole('button', { name: 'Import', exact: true }).click();
      await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
      await t.wait(500);
      const kinds = (await t.nodes()).map((n) => n.type);
      t.expect(kinds.includes('IfcWall') || kinds.includes('IfcWallStandardCase'), 'the walls came in');
      t.expect(!kinds.includes('IfcColumn') && !kinds.includes('IfcBeam'), 'the structure left out stayed out');

      // A file that carries 2D: a cabinet with its footprint, a chair drawn only in 2D. The chair left out.
      const file2d = join(tmpdir(), 'lapin-guide-2d.ifc');
      const { writeFileSync } = await import('node:fs');
      writeFileSync(file2d, IFC_2D);
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      const chooser2d = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.getByRole('menuitem', { name: 'Import IFC…' }).click();
      await (await chooser2d).setFiles(file2d);
      await dialog.waitFor({ timeout: 60_000 });
      t.expect(await dialog.locator('.import-plan2d').count() === 1 && await dialog.locator('.import-only2d').count() === 1, 'the dialog offers the file\'s 2D');
      await dialog.locator('.import-only2d').uncheck();
      await t.shot('import-ifc-2d', { of: '.import-2d', pad: 8 });
      // The import's rules: a repeated body kept once and the technical sets left out unless unticked; the others on request.
      t.expect(await dialog.locator('[data-rule=dedupe]').isChecked() && await dialog.locator('[data-rule=technical]').isChecked(), 'a repeated body is kept once and the technical sets are left out by default');
      for (const r of ['flat', 'far', 'simplify']) await dialog.locator(`[data-rule=${r}]`).check();
      t.expect(!(await dialog.locator('.import-rule-value').first().isDisabled()), 'the distance can be typed once its rule is ticked');
      await t.shot('import-ifc-rules', { of: '.import-rules', pad: 8 });
      for (const r of ['flat', 'far', 'simplify']) await dialog.locator(`[data-rule=${r}]`).uncheck();
      await dialog.getByRole('button', { name: 'Import', exact: true }).click();
      await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
      await t.wait(400);
      const names = (await t.nodes()).map((n) => n.name);
      t.expect(names.includes('Cabinet') && !names.includes('Chair'), 'the chair drawn only in 2D stayed out, the cabinet came in');
    },
  },
  {
    // Fragments chosen as IFC is (decision 205, docs/fragments.md, Exchange): the same panel, a group made.
    id: 'import-frag',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      for (const x of [0, 12]) await t.cmd('structure.addColumn', { storeyId: storey, x, y: 0 });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const file = join(tmpdir(), 'lapin-guide-house.frag');
      await t.call('project.exportMesh', { format: 'frag', path: file });
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.getByRole('menuitem', { name: 'Import Fragments…' }).click();
      await (await chooser).setFiles(file);
      const dialog = t.page.locator('[aria-label="Import Fragments"]');
      await dialog.waitFor({ timeout: 60_000 });
      const disciplines = await dialog.locator('[data-discipline]').evaluateAll((els) => els.map((e) => e.getAttribute('data-discipline')));
      t.expect(disciplines.includes('architecture') && disciplines.includes('structure'), 'the Fragments file\'s elements by discipline');
      await t.shot('import-frag', { of: '[aria-label="Import Fragments"]', pad: 0 });
      t.expect(await dialog.locator('option[value=editable]').count() === 1 && await dialog.locator('option[value=auto]').count() === 1, 'How: recognised from their triangles, or everything editable');
      await dialog.locator('[data-discipline=structure] input[type=checkbox]').first().uncheck();
      await dialog.getByRole('button', { name: 'Import', exact: true }).click();
      await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
      await t.wait(800);
      const kinds = (await t.nodes()).map((n) => n.type);
      t.expect(kinds.includes('IfcWall') && !kinds.includes('IfcColumn'), 'the walls came in, the columns left out stayed out');
      t.expect(kinds.includes('IfcGroup'), 'the file came in as one group');
    },
  },
  {
    // BIM Open Schema (docs/bos.md): a house written as a .bos (Parquet tables in a zip) and brought back as a Fragments file is,
    // chosen in the same panel; the columns left out stay out, the file comes in as one group.
    id: 'import-bos',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: true });
      for (const x of [0, 12]) await t.cmd('structure.addColumn', { storeyId: storey, x, y: 0 });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const file = join(tmpdir(), 'lapin-guide-house.bos');
      await t.call('project.exportMesh', { format: 'bos', path: file });
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.getByRole('menuitem', { name: 'Import BIM Open Schema…' }).click();
      await (await chooser).setFiles(file);
      const dialog = t.page.locator('[aria-label="Import BIM Open Schema"]');
      await dialog.waitFor({ timeout: 60_000 });
      const disciplines = await dialog.locator('[data-discipline]').evaluateAll((els) => els.map((e) => e.getAttribute('data-discipline')));
      t.expect(disciplines.includes('architecture') && disciplines.includes('structure'), 'the BIM Open Schema file\'s elements by discipline');
      await t.shot('import-bos', { of: '[aria-label="Import BIM Open Schema"]', pad: 0 });
      await dialog.locator('[data-discipline=structure] input[type=checkbox]').first().uncheck();
      await dialog.getByRole('button', { name: 'Import', exact: true }).click();
      await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
      await t.wait(800);
      const kinds = (await t.nodes()).map((n) => n.type);
      t.expect(kinds.includes('IfcWall') && kinds.includes('IfcRoof') && !kinds.includes('IfcColumn'), 'the walls and the roof came in, the columns left out stayed out');
      t.expect(kinds.includes('IfcGroup'), 'the file came in as one group');
      await t.ui({ selected: [], selection: null });
      await open3d(t);
      await t.shot('import-bos-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // SDNF (docs/sdnf.md, decision 463, a tester's report #141): a steel hall written as an SDNF 3.0 file from the exchange
    // export's SDNF choice, then read into an empty project by File → Import SDNF… as native members, plates and grid.
    id: 'sdnf',
    touches: ['core', 'steel', 'structure'],
    async run(t) {
      await t.fresh();
      await t.cmd('steel.addHall', { storeyId: await t.storey(), attrs: { secondary: false } });
      await t.wait(2000);
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Export IFC, Fragments or BOS…' }).click();
      const exchange = t.page.locator('[aria-label="Export IFC, Fragments or BOS"]');
      await exchange.locator('[data-discipline]').first().waitFor({ timeout: 20_000 });
      await exchange.locator('[data-testid=export-sdnf]').check();
      t.expect(await exchange.locator('[data-testid=export-voids]').count() === 0, 'SDNF asks nothing of openings');
      await t.shot('export-sdnf', { of: '[aria-label="Export IFC, Fragments or BOS"]', pad: 0 });
      await exchange.getByRole('button', { name: 'Cancel' }).click();
      const out = await t.call('doc.query', { name: 'steel.sdnfExport', args: {} });
      const text = Buffer.from(out.data, 'base64').toString('latin1');
      t.expect(text.startsWith('Packet 00\n"SDNF Version 3.0"') && text.includes('\nPacket 10\n') && text.includes('\nPacket 20\n'), 'the file is SDNF 3.0, its members and plates in their packets');
      // Decision 480: the connections' bolt and anchor holes go as holes (packet 22).
      t.expect(text.includes('\nPacket 22\n'), 'the plates\' bolt and anchor holes are in packet 22');
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFile } = await import('node:fs/promises');
      const file = join(tmpdir(), 'lapin-guide-hall.sdnf');
      await writeFile(file, text, 'latin1');
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.getByRole('menuitem', { name: 'Import SDNF…' }).click();
      await (await chooser).setFiles(file);
      await t.wait(2500);
      const kinds = (await t.nodes()).map((n) => n.type);
      t.expect(kinds.includes('LapinSteelMember') && kinds.includes('IfcColumn') && kinds.includes('IfcPlate') && kinds.includes('IfcGrid'), 'the hall came back as members, columns, plates and its grid');
      await t.ui({ selected: [], selection: null });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -2.3, pitch: -0.45, distance: 52, target: { x: 10, y: 15, z: 3 } }, false));
      await t.wait(1500);
      await t.shot('import-sdnf-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Components (a tester's wish, docs/components.md): a floor and a solid made a component, copied and turned; an edit in one instance followed by all.
    id: 'components',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const floor = (await t.cmd('architecture.addSlab', { storeyId: s, points: [[0, 0], [3, 0], [3, 2], [0, 2]] })).id;
      const block = (await t.cmd('studio.primitive', { shape: 'box', at: [1, 0.6], storeyId: s, width: 1, length: 0.8, height: 0.9 })).id;
      await t.ui({ keyPlan: false, selected: [floor, block], selection: block });
      await t.wait(400);
      // Modify → Make component, named in its box.
      await t.tool('modify.component');
      await t.page.locator('.ask-box input').first().fill('Planter bay');
      await t.page.locator('.ask-box .btn.primary').click();
      await t.wait(800);
      const group = (await t.nodes()).find((n) => n.type === 'IfcGroup');
      t.expect(group?.props.component, 'the selection is an instance of a component');
      const instances = [group.id];
      for (const by of [[5, 0], [10, 0], [15, 0]]) {
        const r = await t.cmd('element.transform', { ids: [group.id], op: 'copy', by });
        instances.push(r.ids.find((id) => id !== group.id));
      }
      const made = (await t.nodes()).filter((n) => n.type === 'IfcGroup');
      t.expect(made.length === 4 && made.every((g) => g.props.component === group.props.component), `four instances of it (${made.length})`);
      const last = made.find((g) => g.props.componentXf[0] > 15);
      await t.cmd('element.transform', { ids: [last.id], op: 'rotate', center: [last.props.componentXf[0], last.props.componentXf[1]], angle: 40 });
      // The first instance's floor made longer: all follow.
      await t.cmd('architecture.updateSlab', { id: floor, points: [[0, 0], [4, 0], [4, 2], [2, 3.2], [0, 2]] });
      await t.wait(600);
      const floors = (await t.nodes()).filter((n) => n.type === 'IfcSlab');
      t.expect(floors.length === 4 && floors.every((f) => f.props.contour.length === 5), 'every instance takes the new outline');
      await t.ui({ selected: [last.id], selection: last.id });
      await t.wait(500);
      await t.page.locator('[data-testid=group-component]').waitFor({ timeout: 5000 });
      await t.shot('component-properties', { of: 'aside.props', pad: 0, maxHeight: 520 });
      await t.ui({ selected: [], selection: null });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: 1.1, pitch: -0.55, distance: 26, target: { x: 9.5, y: 1.2, z: 0.3 } }, false));
      await t.wait(1500);
      await t.shot('components-3d', { of: '[data-testid=view3d]', pad: 0 });
      // Manage → Components.
      await t.tool('manage.components');
      await t.page.locator('[data-testid=components] tbody tr').first().waitFor({ timeout: 5000 });
      t.expect((await t.page.locator('[data-testid=components] tbody tr td.num').first().textContent()) === '4', 'the list says four instances');
      await t.ui({ dialog: null });
    },
  },
  {
    // Another Lapin project linked in, as ArchiCAD's hotlink (a tester's wish, docs/links.md): a small flat saved, then placed twice in a new project, the second turned.
    id: 'hotlink',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      const saved = await t.call('project.saveBytes', { name: 'flat.lapin' });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFileSync } = await import('node:fs');
      const file = join(tmpdir(), 'lapin-guide-flat.lapin');
      writeFileSync(file, Buffer.from(saved.data, 'base64'));
      await t.fresh();
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      for (const [x, turned] of [['0', '0'], ['24000', '90']]) {
        await t.button('File', { exact: true });
        const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
        await t.page.getByRole('menuitem', { name: 'Link Lapin project…' }).click();
        await (await chooser).setFiles(file);
        const box = t.page.locator('.ask-box');
        await box.waitFor({ timeout: 5000 });
        await box.locator('input').nth(0).fill(x);
        await box.locator('input').nth(2).fill(turned);
        await box.locator('.btn.primary').click();
        await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
        await t.wait(800);
      }
      const nodes = await t.nodes();
      const links = nodes.filter((n) => n.type === 'LapinLink');
      const walls = nodes.filter((n) => n.type === 'IfcWall');
      t.expect(links.length === 2 && links.every((l) => l.props.kind === 'lapin'), `two links of the project (${links.length})`);
      t.expect(walls.length === 10 && walls.every((w) => w.props.linkId), `its five walls, twice, each the link's (${walls.length})`);
      t.expect(new Set(walls.map((w) => w.id)).size === 10, 'each placement with ids of its own');
      await t.ui({ keyPlan: false, selected: [], selection: null });
      await t.wait(800);
      await t.fit();
      await t.shot('hotlink-plan', { of: '[data-testid=plan]', pad: 0 });
      await t.tool('manage.links');
      await t.wait(500);
      await t.shot('hotlink-links', { of: '.dialog', pad: 0 });
      // Placed elsewhere from the dialog, without linking it again: the same ids (in the browser its file is asked for).
      const before = new Set((await t.nodes()).filter((n) => n.type === 'IfcWall').map((w) => w.id));
      await t.page.locator('[data-link=place-project]').first().click();
      const place = t.page.locator('.ask-box');
      await place.waitFor({ timeout: 5000 });
      await place.locator('input').nth(1).fill('30000');
      await place.locator('select').selectOption('yes');
      const again = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await place.locator('.btn.primary').click();
      await (await again).setFiles(file);
      await t.wait(2500);
      const after = (await t.nodes()).filter((n) => n.type === 'IfcWall');
      t.expect(after.length === 10 && after.every((w) => before.has(w.id)), `placed elsewhere under the same ids (${after.length})`);
      const moved = (await t.nodes()).find((n) => n.type === 'LapinLink' && n.props.options?.mirror === true);
      t.expect(!!moved, 'the link says it is mirrored');
      await t.ui({ dialog: null });
    },
  },
  {
    id: 'export',
    async run(t) {
      await t.fresh();
      await house(t);
      // The 3D view in front shows the ground floor alone, cut at 1.20 m: the export takes it as the view shows it.
      const ground = await t.storey('Ground floor');
      await t.cmd('view.setStoreys', { id: '3d', mode: 'some', ids: [ground], cut: 1.2 });
      await open3d(t);
      await t.button('File', { exact: true });
      await t.shot('file-menu', { of: '[role=menu]', pad: 6 });
      // IFC and Fragments exported alike, chosen as an import is.
      await t.page.getByRole('menuitem', { name: 'Export IFC, Fragments or BOS…' }).click();
      const exchange = t.page.locator('[aria-label="Export IFC, Fragments or BOS"]');
      await exchange.locator('[data-discipline]').first().waitFor({ timeout: 20_000 });
      await t.shot('export-exchange', { of: '[aria-label="Export IFC, Fragments or BOS"]', pad: 0 });
      await exchange.getByRole('button', { name: 'Cancel' }).click();
      await t.wait(300);
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: /Export 3D/ }).click();
      await t.wait(500);
      const dialog = t.page.locator('[aria-label="Export 3D"]');
      t.expect(await dialog.locator('select.export-what').inputValue() === 'view', 'a 3D view that leaves something out is exported as it shows it');
      t.expect(await dialog.locator('input.export-cuts').isChecked(), 'the export is cut as the view is cut');
      await t.shot('export-3d', { of: '[aria-label="Export 3D"]', pad: 0 });
      // The engine cuts the model at the view's planes and closes the cuts.
      const out = await t.call('project.exportMesh', { format: 'glb', viewId: '3d', planes: [{ normal: [0, 0, -1], constant: 1.2 }] });
      t.expect((out?.notes ?? []).some((n) => n.startsWith('Cut as the 3D view cuts it')), 'the exported model is cut as the view is');
      await dialog.getByRole('button', { name: 'Cancel' }).click();
      // Export IFC: face sets, simplified within 2 mm.
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Export IFC, Fragments or BOS…' }).click();
      const ifc = t.page.locator('[aria-label="Export IFC, Fragments or BOS"]');
      await ifc.locator('[data-discipline]').first().waitFor({ timeout: 20_000 });
      t.expect(await ifc.locator('select.export-geometry').inputValue() === 'brep', 'the IFC export writes B-reps unless asked otherwise');
      await ifc.locator('select.export-geometry').selectOption('faceset');
      await ifc.locator('input.export-simplify').check();
      t.expect(await ifc.locator('input.export-tolerance').inputValue() === '2', 'the tolerance starts at 2 mm');
      await t.shot('export-ifc', { of: '[aria-label="Export IFC, Fragments or BOS"]', pad: 0 });
      const written = await t.call('project.exportIfc', { geometry: 'faceset', tolerance: 0.002 });
      t.expect(typeof written?.data === 'string' && atob(written.data).includes('IFCPOLYGONALFACESET'), 'the IFC export writes face sets when asked');
      await ifc.getByRole('button', { name: 'Cancel' }).click();
    },
  },
  {
    // Lapin for the browser (decision 482, docs/html-export.md): the project as one .html file, opened from its file in the
    // browser with no server (the 3D model, a plan, the estimate, a click selecting across them), then opened again in Lapin.
    id: 'html-export',
    touches: ['core', 'cost'],
    async run(t) {
      await t.fresh();
      const { storey } = await house(t);
      await t.cmd('view.create', { kind: 'section', name: 'A-A', start: [5, -2], end: [5, 10], scale: 50 });
      const sheet = await t.cmd('sheet.create', { paper: 'A3', name: 'Ground floor plan' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: `plan:${storey}`, x: 200, y: 140 });
      // An estimate from Lapin's own indicative values: the page marks it as the app does.
      await t.cmd('cost.addPriceList', { template: true });
      const nodes = await t.nodes();
      const typeOf = (cls) => nodes.find((n) => n.type === cls)?.id;
      if (typeOf('IfcWallType')) await t.cmd('cost.setArticles', { type: typeOf('IfcWallType'), articles: [{ code: 'LAP-MAS' }] });
      if (typeOf('IfcSlabType')) await t.cmd('cost.setArticles', { type: typeOf('IfcSlabType'), articles: [{ code: 'LAP-RC', basis: 'volume' }] });
      if (typeOf('IfcDoorType')) await t.cmd('cost.setArticles', { type: typeOf('IfcDoorType'), articles: [{ code: 'LAP-DOOR' }] });
      if (typeOf('IfcWindowType')) await t.cmd('cost.setArticles', { type: typeOf('IfcWindowType'), articles: [{ code: 'LAP-WIN', basis: 'area' }] });
      await t.cmd('cost.createEstimate', { name: 'House' });
      await t.wait(600);

      // File → Lapin for the browser (.html)…: the drawings ticked, the 3D model, the estimate and the project in it.
      await t.button('File', { exact: true });
      await t.page.locator('[data-menu=export-html]').click();
      const dialog = t.page.locator('[aria-label="Lapin for the browser"]');
      await dialog.locator('[data-testid=html-export-size]').waitFor({ timeout: 60_000 });
      const ticked = await dialog.locator('[data-testid=html-export-drawings] input[type=checkbox]').evaluateAll((els) => els.filter((e) => e.checked).length);
      t.expect(ticked >= 3, `the plan, the section and the sheet are ticked (${ticked})`);
      // A level's plan with no element on it (only grids or marks) is listed, unticked.
      const emptyTicked = await dialog.locator('[data-testid=html-export-drawings] [data-empty=true] input[type=checkbox]').evaluateAll((els) => els.filter((e) => e.checked).length);
      t.expect(emptyTicked === 0, `a drawing that shows no element is not ticked (${emptyTicked})`);
      t.expect(await dialog.locator('[data-testid=html-export-tables]').isChecked(), 'the estimate goes with the page');
      t.expect(await dialog.locator('[data-testid=html-export-project]').isChecked(), 'the project goes inside the page, so Lapin opens it again');
      await t.shot('html-export-dialog', { of: '[aria-label="Lapin for the browser"]', pad: 0 });
      const download = t.page.waitForEvent('download', { timeout: 120_000 });
      await dialog.getByRole('button', { name: 'Export', exact: true }).click();
      const page = await download;
      t.expect(page.suggestedFilename().endsWith('.html'), `the browser downloads one .html (${page.suggestedFilename()})`);

      // The same page in the guide's language, written where the scene opens it from.
      const file = join(mkdtempSync(join(tmpdir(), 'lapin-guide-html-')), 'House.html');
      const args = await t.page.evaluate(async ([lang]) => {
        await (await import('/src/i18n/index.ts')).loadLanguage(lang);
        return (await import('/src/engine/htmlExport.ts')).htmlExportArgs({ viewId: '3d' }, lang);
      }, [t.lang]);
      const written = await t.call('project.exportHtml', { ...args, path: file });
      t.expect(written?.sizes?.project > 0 && written?.sizes?.model3d > 0, 'the page carries the project and the 3D model');

      // Opened from its file, as a double-click opens it: no server, no network.
      const viewerContext = await t.page.context().browser().newContext({ viewport: { width: 1280, height: 800 }, deviceScaleFactor: 2 });
      const viewer = await viewerContext.newPage();
      const offline = [];
      viewer.on('request', (r) => { if (!r.url().startsWith('file:') && !r.url().startsWith('data:') && !r.url().startsWith('blob:')) offline.push(r.url()); });
      await viewer.goto(`file://${file}`);
      await viewer.locator('[data-testid=lp-3d]').waitFor({ timeout: 60_000 });
      await viewer.locator('[data-loaded=true]').waitFor({ timeout: 60_000 });
      t.expect(await viewer.locator('plaintext').evaluate((e) => getComputedStyle(e).display === 'none'), 'the project after the page is hidden text');
      await viewer.waitForTimeout(800);
      await t.shot('html-export-3d', { page: viewer });
      // The drawings: the first plan, an element clicked on it shown in the properties.
      await viewer.locator('.lp-tab[data-key=drawings]').click();
      await viewer.locator('.lp-list button[title="Ground floor"]').click();
      await viewer.locator('.lp-paper[data-drawing^="plan:"] svg').waitFor({ timeout: 20_000 });
      await viewer.waitForTimeout(400);
      const pick = viewer.locator('.lp-paper [data-gid]').first();
      t.expect(await pick.count() === 1, 'the plan names its elements by their GlobalIds');
      const box = await pick.boundingBox();
      if (box) await viewer.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
      await viewer.waitForTimeout(400);
      t.expect(await viewer.locator('.lp-side dd', { hasText: /^Ifc/ }).count() > 0, 'a click on the plan shows the element\'s properties');
      await t.shot('html-export-drawing', { page: viewer });
      // The estimate: its lines, marked indicative, a row selecting its elements.
      await viewer.locator('.lp-tab[data-key="table:cost"]').click();
      await viewer.locator('.lp-table tbody tr').first().waitFor({ timeout: 10_000 });
      t.expect(await viewer.locator('.lp-tablebox .lp-tag').count() === 1, 'the estimate is marked indicative, as the app marks it');
      await viewer.locator('.lp-table tbody tr:not(.lp-heading)').first().click();
      await viewer.waitForTimeout(300);
      await t.shot('html-export-costs', { page: viewer });
      t.expect(offline.length === 0, `the page asks the network for nothing (${offline.join(', ')})`);
      await viewerContext.close();

      // Opened again in Lapin: the project inside the page, unsaved, the same walls.
      const walls = nodes.filter((n) => n.type === 'IfcWall').length;
      const opened = await t.call('project.open', { path: file });
      t.expect(opened?.page === 'House.html', 'Lapin reads the project from the page');
      await t.wait(1000);
      t.expect((await t.nodes()).filter((n) => n.type === 'IfcWall').length === walls, 'the same walls come back');
      t.expect(!(await t.page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().doc?.path)), 'it opens without a file of its own: Save asks where, as a .lapin');
    },
  },
  {
    // The analytical model for JG Structures (docs/jg-structures.md): a small concrete frame on footings, its export checked.
    id: 'export-jg',
    async run(t) {
      await t.fresh();
      const gf = await t.storey('Ground floor');
      const ff = await t.storey('First floor');
      const col = await t.cmd('structure.createColumnType', { name: 'Column 30×30', section: { kind: 'rectangle', width: 0.3, depth: 0.3 }, material: 'Reinforced concrete C25/30' });
      const beam = await t.cmd('structure.createBeamType', { name: 'Beam 30×50', section: { kind: 'rectangle', width: 0.3, depth: 0.5 }, material: 'Reinforced concrete C25/30' });
      const slab = await t.cmd('architecture.createSlabType', { name: 'Slab 20', layers: [{ material: 'Reinforced concrete C25/30', thickness: 0.2, core: true }] });
      const grid = [[0, 0], [6, 0], [6, 5], [0, 5]];
      for (const [x, y] of grid) await t.cmd('structure.addColumn', { storeyId: gf, x, y, typeId: col.id });
      for (let i = 0; i < 4; i++) {
        const [a, b] = [grid[i], grid[(i + 1) % 4]];
        await t.cmd('structure.addBeam', { storeyId: gf, x1: a[0], y1: a[1], x2: b[0], y2: b[1], typeId: beam.id });
      }
      await t.cmd('architecture.addSlab', { storeyId: ff, points: [[-0.15, -0.15], [6.15, -0.15], [6.15, 5.15], [-0.15, 5.15]], typeId: slab.id });
      for (const [x, y] of grid.slice(0, 3)) await t.cmd('structure.addPadFooting', { storeyId: gf, x, y });
      // A load-bearing wall under the north beam, on a strip footing; a steel beam across the middle, of a steel with no grade.
      const rc = await t.cmd('architecture.createWallType', { name: 'RC wall 25', layers: [{ material: 'Reinforced concrete C25/30', thickness: 0.25, core: true }] });
      const wall = await t.cmd('architecture.addWall', { storeyId: gf, x1: 0, y1: 5, x2: 6, y2: 5, typeId: rc.id });
      await t.cmd('element.setParam', { id: wall.id, pset: 'Pset_WallCommon', name: 'LoadBearing', value: true });
      await t.cmd('structure.addStripFooting', { storeyId: gf, x1: 0, y1: 5, x2: 6, y2: 5 });
      const ipe = await t.cmd('structure.createBeamType', { section: 'IPE 300', material: 'Steel' });
      await t.cmd('structure.addBeam', { storeyId: gf, x1: 0, y1: 2.5, x2: 6, y2: 2.5, typeId: ipe.id });
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Export JG Structures (JSON)…' }).click();
      const dialog = t.page.locator('[aria-label="Export JG Structures"]');
      await dialog.locator('.jg-counts').waitFor({ timeout: 20_000 });
      const report = await t.call('doc.query', { name: 'jg.preview', args: {} });
      t.expect(report.errors.length === 0, `the export passes the contract's checks (${report.errors[0] ?? ''})`);
      // The steel beam splits the edge beams it frames into and the floor it crosses; the wall is one more shell.
      t.expect(report.counts.frames === 11 && report.counts.shells === 3, `four columns, four beams (two split), the steel beam, the floor in two and the wall (${report.counts.frames} frames, ${report.counts.shells} shells)`);
      t.expect(report.counts.supports === 4, `the column and wall feet stand on footings (${report.counts.supports})`);
      t.expect(report.missing.some((m) => m.name === 'Steel' && m.fields.includes('elastic_modulus')), 'a steel without a grade has no E: said, not guessed');
      t.expect(await dialog.locator('.jg-warn').count() === 1, 'the report says the file is valid but analysis still needs values');
      t.expect(await dialog.locator('[data-testid=jg-footings]').inputValue() === 'fixed', 'feet on footings are fixed by default');
      const pinned = await t.call('doc.query', { name: 'jg.preview', args: { footings: 'pinned' } });
      t.expect(pinned.errors.length === 0 && pinned.counts.supports === 4, 'pinned feet are still four supports');
      await t.shot('export-jg', { of: '[aria-label="Export JG Structures"]', pad: 0 });
      await dialog.getByRole('button', { name: 'Cancel' }).click();
    },
  },
  {
    // An IFC file cleaned and lightened into a copy (docs/ifc-optimize.md): no project needed, the original never written.
    id: 'optimize-ifc',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      for (const x of [0, 4, 8, 12]) await t.cmd('structure.addColumn', { storeyId: storey, x, y: 0 });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const file = join(tmpdir(), 'lapin-guide-house.ifc');
      await t.call('project.exportIfc', { geometry: 'faceset', path: file });
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      await t.button('File', { exact: true });
      await t.page.locator('[data-menu=optimize-ifc]').click();
      const dialog = t.page.locator('[data-dialog=optimize-ifc]');
      await dialog.waitFor({ timeout: 10_000 });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await dialog.locator('[data-optimize=add]').click();
      await (await chooser).setFiles(file);
      await dialog.locator('[data-optimize-file=waiting]').waitFor({ timeout: 10_000 });
      t.expect(await dialog.locator('input[data-optimize=simplify]').isChecked() && await dialog.locator('input[data-optimize=share]').isChecked(), 'simplify and share are on by default');
      t.expect(!(await dialog.locator('input[data-optimize=zip]').isChecked()) && !(await dialog.locator('input[data-optimize=breps]').isChecked()), 'zip and face sets are off by default');
      t.expect(await dialog.locator('input[data-optimize=suffix]').inputValue() === '.optimized', 'the ending starts as .optimized');
      await t.shot('optimize-ifc', { of: '[data-dialog=optimize-ifc]', pad: 0 });
      const download = t.page.waitForEvent('download', { timeout: 60_000 });
      await dialog.locator('[data-optimize=run]').click();
      const copy = await download;
      t.expect(copy.suggestedFilename() === 'lapin-guide-house.optimized.ifc', "the copy is the original's name with its ending");
      await dialog.locator('[data-optimize=report]').waitFor({ timeout: 60_000 });
      t.expect(await dialog.locator('[data-optimize-file=done]').count() === 1, 'the file was optimised');
      await t.shot('optimize-ifc-report', { of: '[data-dialog=optimize-ifc]', pad: 0 });
      // The copy keeps the file's GlobalIds: the same products come in from it.
      const report = await t.call('ifc.optimize', { name: 'house.ifc', data: (await import('node:fs')).readFileSync(file).toString('base64'), suffix: '.optimized' });
      t.expect(report.entitiesAfter <= report.entitiesBefore && report.deviationMm <= 1, 'the copy is no bigger and stays within 1 mm');
    },
  },
  {
    // Imported types mapped onto the project's own (docs/spec-ifc.md, Î9): Manage → Map imported types after a foreign file came in.
    id: 'import-types',
    async run(t) {
      await t.fresh();
      const layers = (core, finish) => [{ material: 'Render', thickness: 0.02 }, { material: 'Brick', thickness: core, core: true }, { material: 'Plaster', thickness: finish }];
      const own = (await t.cmd('architecture.createWallType', { name: 'Brick 300 rendered', layers: layers(0.25, 0.03) })).id;
      await t.cmd('architecture.createWallType', { name: 'Partition 125', layers: [{ material: 'Brick', thickness: 0.115, core: true }, { material: 'Plaster', thickness: 0.01 }] });
      const { tmpdir } = await import('node:os');
      const { join } = await import('node:path');
      const { writeFileSync } = await import('node:fs');
      const file = join(tmpdir(), 'lapin-guide-typed-walls.ifc');
      writeFileSync(file, ifcTypedWalls());
      await t.call('project.importIfc', { path: file });
      await t.wait(800);
      await t.tool('manage.mapTypes');
      const dialog = t.page.locator('[aria-label="Map imported types"]');
      await dialog.waitFor({ timeout: 20_000 });
      const row = dialog.locator('tr', { hasText: 'EXT-30' });
      t.expect(await row.count() === 1, 'the file\'s wall type EXT-30 is listed');
      await row.locator('select').selectOption(own);
      await t.wait(300);
      await t.shot('map-types', { of: '[aria-label="Map imported types"]', pad: 0 });
      await dialog.getByRole('button', { name: 'Map', exact: true }).click();
      await t.wait(800);
      t.expect(await dialog.locator('tr', { hasText: 'EXT-30' }).count() === 1, 'the choice is remembered (listed under Remembered)');
      const after = await t.call('doc.query', { name: 'types.imported', args: {} });
      t.expect(!after.types.some((x) => x.sourceName === 'EXT-30') && after.remembered.some((r) => r.name === 'EXT-30' && r.to === own), 'EXT-30 became the project\'s type, the choice remembered');
      await dialog.locator('.dialog-foot').getByRole('button', { name: 'Close', exact: true }).click();
    },
  },
  {
    id: 'versions',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.tool('manage.versions');
      await t.shot('versions', { of: '.dialog', pad: 0 });
    },
  },
  {
    // Project templates (issues #9 and #20, decision 470): File → Save as template…, then File → New from template… starts a project
    // with the house's types and no element. The template is deleted at the end, so the next scenes' start screen and New stay Lapin's.
    id: 'project-templates',
    touches: ['core', 'architecture'],
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.button('File', { exact: true });
      await t.page.getByRole('menuitem', { name: 'Save as template…' }).click();
      const save = t.page.locator('[data-testid=save-template]');
      await save.waitFor({ timeout: 10_000 });
      await save.locator('[data-field=template-name]').fill('Office houses');
      await save.locator('[data-field=template-save]').click();
      await save.waitFor({ state: 'detached', timeout: 30_000 });
      try {
        const kept = await t.call('templates.list', {});
        t.expect(kept.some((x) => x.id === 'Office houses'), 'the template is kept on this computer');
        await t.button('File', { exact: true });
        await t.page.getByRole('menuitem', { name: 'New from template…' }).click();
        const dialog = t.page.locator('[data-testid=project-templates]');
        await dialog.waitFor({ timeout: 10_000 });
        await dialog.locator('[data-template-row="Office houses"]').waitFor({ timeout: 10_000 });
        await t.shot('project-templates', { of: '[data-testid=project-templates]', pad: 0 });
        // The house is not saved: the question is answered yes (the runner accepts every dialog), as a user who starts over would.
        await dialog.locator('[data-template-row="Office houses"]').getByRole('button', { name: 'New project', exact: true }).click();
        await dialog.waitFor({ state: 'detached', timeout: 30_000 });
        await t.page.waitForSelector('.plan-svg', { timeout: 30_000 });
        const kinds = (await t.nodes()).map((n) => n.type);
        t.expect(kinds.includes('IfcWallType') && !kinds.includes('IfcWall') && !kinds.includes('IfcWallStandardCase'), 'the new project has the house\'s wall types and no wall');
      } finally {
        await t.call('templates.delete', { id: 'Office houses' }).catch(() => undefined);
      }
    },
  },
  {
    // Lapin Extensions, stage 0 (docs/plan-extensions.md): a catalogue on this computer (an index and its three packages,
    // made here as the catalogue's tools make them) read from File → Extensions… → Catalogue…; Browse shows the furniture
    // pack's details and the script's (installable, not runnable yet); the furniture pack installed (its SHA-256 checked)
    // lands in My furniture; Installed lists it; Uninstall takes its items away again. Everything is cleaned at the end.
    // Stage 1: the catalogue is unsigned and not Lapin's own, so the dialog warns above the list.
    id: 'extensions',
    touches: ['core', 'furniture'],
    async run(t) {
      await t.fresh();
      const dir = mkdtempSync(join(tmpdir(), 'lapin-guide-extensions-'));
      const sha = (b) => createHash('sha256').update(b).digest('hex');
      const author = { name: 'Ciuntuc BIM Studio', github: 'Ciuntuc-BIM-Studio', url: 'https://ciuntucbimstudio.ro' };
      const pkg = (file, bytes) => ({ url: file, sha256: sha(bytes), size: bytes.length });
      // The furniture pack: four types made with the configurator, packed as My furniture → Export packs them.
      const made = [
        ['casework', 'sideboard', 'Comodă stejar', 'living', ['stejar']],
        ['casework', 'tvUnit', 'Comodă TV nuc', 'living', ['nuc', 'tv']],
        ['casework', 'bookcase', 'Bibliotecă înaltă', 'storage', ['stejar']],
        ['sofa', null, 'Canapea 3 locuri', 'living', ['canapea']],
      ];
      const entries = [];
      for (const [kind, preset, name, category, tags] of made) {
        const { id } = await t.cmd('furniture.createType', { kind, name, ...(preset ? { preset } : {}) });
        const type = await t.call('doc.query', { name: 'furniture.libraryItem', args: { typeId: id } });
        const thumbnail = await t.page.evaluate(async (spec) => (await import('/src/modules/furniture/library.ts')).thumbnailOf(spec), type.spec);
        entries.push({ id: `guide${entries.length + 1}`, name, category, tags, thumbnail, at: '2026-11-12T00:00:00Z', type });
      }
      const lib = Buffer.from((await t.call('furnitureLibrary.pack', { entries })).data, 'base64');
      writeFileSync(join(dir, 'mobilier-living.lapinlib'), lib);
      const tpl = Buffer.from((await t.call('template.make', {})).data, 'base64');
      writeFileSync(join(dir, 'sablon-romania.lapin'), tpl);
      const py = Buffer.from('# lapin: name="Perimeter walls", version="1.0.0", requires="scripting"\nimport lapinbim as lapin\n\nwith lapin.batch("Perimeter walls"):\n    for (x1, y1, x2, y2) in [(0, 0, 8, 0), (8, 0, 8, 6), (8, 6, 0, 6), (0, 6, 0, 0)]:\n        lapin.architecture.add_wall(x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)\n');
      writeFileSync(join(dir, 'perimeter_walls.py'), py);
      const base = { schema: 1, version: '1.0.0', author, lapin: '*', homepage: 'https://github.com/Ciuntuc-BIM-Studio/lapin-extensions', forum: 'https://forum.lapinbim.com/c/lapin' };
      const index = { format: 'lapin-extensions-index', version: 1, extensions: [
        { ...base, id: 'ro.ciuntucbimstudio.mobilier-living', name: 'Mobilier living', kind: 'furniture', license: 'CC-BY-4.0', languages: ['ro'], tags: ['living', 'example'],
          summary: 'Living-room furniture types for My furniture: sideboard, TV unit, bookcase and sofa.', contents: { file: 'mobilier-living.lapinlib', items: made.map((m) => m[2]) }, package: pkg('mobilier-living.lapinlib', lib) },
        { ...base, id: 'ro.ciuntucbimstudio.sablon-romania', name: 'Șablon de proiect România', kind: 'template', license: 'CC0-1.0', languages: ['ro'], tags: ['romania'],
          summary: 'A project template with the storeys named the Romanian way.', contents: { file: 'sablon-romania.lapin' }, package: pkg('sablon-romania.lapin', tpl) },
        { ...base, id: 'ro.ciuntucbimstudio.pereti-perimetrali', name: 'Perimeter walls', kind: 'script', license: 'MIT', permissions: ['model.read', 'model.write'], requires: ['scripting'], tags: ['walls', 'python'],
          summary: 'A Python script: four walls round a rectangle, as one undo step.', contents: { file: 'perimeter_walls.py' }, package: pkg('perimeter_walls.py', py) },
      ] };
      writeFileSync(join(dir, 'index.json'), JSON.stringify(index, null, 2));
      const before = (await t.call('furnitureLibrary.list', {})).map((e) => e.id);
      try {
        await t.button('File', { exact: true });
        await t.page.locator('[data-menu=extensions]').click();
        const dialog = t.page.locator('[data-dialog=extensions]');
        await dialog.waitFor({ timeout: 10_000 });
        // The catalogue on this computer: its index's path typed in Catalogue….
        await dialog.locator('[data-action=extensions-catalogue]').click();
        await dialog.locator('[data-field=extensions-index]').fill(join(dir, 'index.json'));
        await dialog.locator('[data-action=extensions-index-set]').click();
        await dialog.locator('[data-extension]').nth(2).waitFor({ timeout: 15_000 });
        t.expect(await dialog.locator('[data-extension]').count() === 3, 'Browse lists the three extensions of the catalogue');
        // Stage 1: a catalogue of one's own, unsigned, is read with a warning above the list.
        t.expect(await dialog.locator('[data-testid=extensions-unsigned]').isVisible(), 'an unsigned catalogue of one\'s own is said so above the list');
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.mobilier-living"]').click();
        await dialog.locator('[data-extension-details="ro.ciuntucbimstudio.mobilier-living"]').waitFor({ timeout: 5000 });
        await t.wait(300);
        await t.shot('extensions-browse', { of: '[data-dialog=extensions]', pad: 0 });
        // A script: what it asks for, and that it runs only once the Python console is in Lapin.
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.pereti-perimetrali"]').click();
        await dialog.locator('[data-extension-details="ro.ciuntucbimstudio.pereti-perimetrali"] .ext-code').waitFor({ timeout: 5000 });
        await t.shot('extensions-script', { of: '[data-extension-details="ro.ciuntucbimstudio.pereti-perimetrali"]', pad: 8 });
        // Install: downloaded, its SHA-256 checked, into My furniture.
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.mobilier-living"]').click();
        await dialog.locator('[data-action=extension-install]').click();
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.mobilier-living"] .ext-row-mark').waitFor({ timeout: 20_000 });
        const added = (await t.call('furnitureLibrary.list', {})).filter((e) => !before.includes(e.id));
        t.expect(added.length === 4 && added.some((e) => e.name === 'Canapea 3 locuri'), `the pack's four types are in My furniture (${added.length})`);
        await dialog.locator('[data-tab-extensions=installed]').click();
        await dialog.locator('[data-installed-extension="ro.ciuntucbimstudio.mobilier-living"]').waitFor({ timeout: 5000 });
        await t.shot('extensions-installed', { of: '[data-dialog=extensions]', pad: 0 });
        // Where it went: My furniture….
        await dialog.locator('[data-installed-extension="ro.ciuntucbimstudio.mobilier-living"]').getByRole('button', { name: 'My furniture…' }).click();
        const library = t.page.locator('[data-dialog=furniture-library]');
        await library.waitFor({ timeout: 10_000 });
        await library.locator('[data-library-entry="Comodă TV nuc"]').waitFor({ timeout: 10_000 });
        await t.wait(500);
        await t.shot('extensions-my-furniture', { of: '[data-dialog=furniture-library]', pad: 0 });
        await t.page.keyboard.press('Escape');
        await t.ui({ dialog: 'extensions' });
        await dialog.waitFor({ timeout: 10_000 });
        await dialog.locator('[data-tab-extensions=installed]').click();
        // Uninstall (its question answered yes): the pack's items leave My furniture, nothing else does.
        await dialog.locator('[data-installed-extension="ro.ciuntucbimstudio.mobilier-living"] [data-action=extension-uninstall]').click();
        await dialog.locator('[data-installed-extension]').waitFor({ state: 'detached', timeout: 10_000 });
        const left = (await t.call('furnitureLibrary.list', {})).map((e) => e.id);
        t.expect(left.length === before.length && left.every((id) => before.includes(id)), 'uninstalling takes the pack\'s items out of My furniture');
      } finally {
        for (const e of await t.call('furnitureLibrary.list', {}).catch(() => [])) if (!before.includes(e.id)) await t.call('furnitureLibrary.delete', { id: e.id }).catch(() => undefined);
        await t.page.evaluate(() => { localStorage.removeItem('lapin.extensions'); localStorage.removeItem('lapin.extensions.index'); }).catch(() => undefined);
        await t.ui({ dialog: null });
      }
    },
  },
  {
    // A tester's report (type libraries, nothing lost in silence): Wall types → Import… of a JSON whose wall type and brick
    // differ from the project's of the same name lists both with what differs and a choice each; the brick updated from the
    // file in place, the type updated (its wall follows), what the import did counted. Then the type's parameter made an
    // instance default (Type parameters → Instance default): a wall drawn with the type gets its copy.
    id: 'type-import-choices',
    touches: ['architecture', 'core'],
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const layers = [{ material: 'Render', thickness: 0.02 }, { material: 'Brick', thickness: 0.25, core: true }, { material: 'Plaster', thickness: 0.015 }];
      const { id: typeId } = await t.cmd('architecture.createWallType', { name: 'WT1 Exterior', mark: 'WT1', layers });
      await t.cmd('element.setParam', { id: typeId, pset: 'Pset_WallCommon', name: 'FireRating', value: 'EI 60' });
      // The library's version: the brick hatched otherwise, the core 300 mm thick, a renovation status of its own.
      const { text } = await t.call('doc.query', { name: 'architecture.exportTypes', args: { kinds: ['wall'], format: 'json' } });
      const doc = JSON.parse(text);
      const lib = doc.types.find((x) => x.name === 'WT1 Exterior');
      lib.layers[1].thickness = 0.3;
      lib.params = [...(lib.params ?? []), { name: 'Renovation', kind: 'choice', options: ['new', 'existing', 'demolish'], value: 'new', pset: 'RAVA_Wall', instance: true }];
      doc.types = [lib];
      for (const m of doc.materials) if (m.name === 'Brick') m.attrs = { ...m.attrs, section: { ...(m.attrs.section ?? {}), hatchScale: 2, hatchAngle: 30 } };
      const dir = mkdtempSync(join(tmpdir(), 'lapin-guide-type-import-'));
      const file = join(dir, 'Wall library.json');
      writeFileSync(file, JSON.stringify(doc, null, 2));
      const wall = (await t.cmd('architecture.addWall', { storeyId: s, typeId, x1: 0, y1: 0, x2: 6, y2: 0 })).id;

      await t.tool('architecture.wallTypes');
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.locator('[data-action=types-import]').click();
      await (await chooser).setFiles(file);
      const choices = t.page.locator('[data-dialog=type-conflicts]');
      await choices.waitFor({ timeout: 10_000 });
      t.expect(await choices.locator('[data-choice-material=Brick]').count() === 1, 'the brick, defined otherwise, is listed');
      t.expect((await choices.innerText()).includes('section hatch'), 'what differs of the brick is said');
      t.expect(await choices.locator('[data-choice-type="WT1 Exterior"]').count() === 1, 'the wall type of the same name is listed');
      await choices.locator('[data-choice-material=Brick]').selectOption('update');
      await t.wait(300);
      await t.shot('type-import-choices', { of: '[data-dialog=type-conflicts]', pad: 0 });
      await choices.locator('[data-action=types-import-go]').click();
      await t.page.locator('[data-testid=import-summary]').waitFor({ timeout: 10_000 });
      const brick = (await t.nodes()).find((n) => n.type === 'IfcMaterial' && n.name === 'Brick');
      t.expect(brick?.props.section?.hatchScale === 2, 'the brick was updated from the file in place');
      const q = await t.call('quantities.of', { id: wall });
      t.expect(q.length > 0, 'the wall of the updated type is still measured');
      await t.ui({ dialog: null });

      // The renovation status is an instance default: a wall drawn with the type now gets its own copy.
      const typed = (await t.nodes()).find((n) => n.id === typeId);
      t.expect((typed?.props.params ?? []).some((p) => p.name === 'Renovation' && p.instance === true), 'the instance default came with the type');
      const second = (await t.cmd('architecture.addWall', { storeyId: s, typeId, x1: 0, y1: 4, x2: 6, y2: 4 })).id;
      const own = (await t.nodes()).find((n) => n.id === second);
      t.expect((own?.props.params ?? []).some((p) => p.name === 'Renovation' && p.value === 'new'), 'a wall drawn with the type got the instance default');
    },
  },
  {
    // Types as an extension (docs/plan-extensions.md, §9.3): a wall type made in one project and chosen in Manage → Type
    // packs… (exported as its .lapintypes, the file the dialog's Export types… saves); in a new project, a catalogue on this
    // computer lists it as a types extension under a non-commercial licence (marked so); installed, its type comes into the
    // project (types.importFrom, its materials made) and a wall of it is drawn. Everything is cleaned at the end.
    id: 'extensions-types',
    touches: ['core', 'architecture'],
    async run(t) {
      await t.fresh();
      const dir = mkdtempSync(join(tmpdir(), 'lapin-guide-extension-types-'));
      const sha = (b) => createHash('sha256').update(b).digest('hex');
      const layers = [{ material: 'Tencuială exterioară', thickness: 0.02 }, { material: 'Polistiren 10 cm', thickness: 0.1 }, { material: 'BCA', thickness: 0.3, core: true }, { material: 'Tencuială interioară', thickness: 0.015 }];
      const { id: typeId } = await t.cmd('architecture.createWallType', { name: 'WT1 BCA 30 termoizolat', mark: 'WT1', layers });
      try {
        // Manage → Type packs…: the project's types, the wall type chosen.
        await t.ui({ dialog: 'typePacks' });
        const packs = t.page.locator('[data-dialog=type-packs]');
        await packs.waitFor({ timeout: 10_000 });
        await packs.locator('[data-project-type="WT1 BCA 30 termoizolat"]').check();
        await packs.locator('[data-field=type-pack-name]').fill('Pereți RO');
        await t.wait(300);
        await t.shot('type-packs-export', { of: '[data-dialog=type-packs]', pad: 0 });
        // What Export types… saves.
        const { text } = await t.call('doc.query', { name: 'types.export', args: { ids: [typeId], name: 'Pereți RO' } });
        const bytes = Buffer.from(text);
        writeFileSync(join(dir, 'pereti-ro.lapintypes'), bytes);
        await t.ui({ dialog: null });
        const index = { format: 'lapin-extensions-index', version: 1, extensions: [{
          schema: 1, id: 'ro.ciuntucbimstudio.pereti-ro', name: 'Pereți RO', version: '1.0.0', kind: 'types', license: 'CC-BY-NC-4.0', lapin: '*', languages: ['ro'], tags: ['walls', 'romania'],
          author: { name: 'Ciuntuc BIM Studio', github: 'Ciuntuc-BIM-Studio', url: 'https://ciuntucbimstudio.ro' },
          summary: 'A thermally insulated autoclaved aerated concrete wall type, with its layers and materials.',
          contents: { file: 'pereti-ro.lapintypes', items: ['WT1 BCA 30 termoizolat'] }, package: { url: 'pereti-ro.lapintypes', sha256: sha(bytes), size: bytes.length },
        }] };
        writeFileSync(join(dir, 'index.json'), JSON.stringify(index, null, 2));

        // A new project, without the type: File → Extensions…, the catalogue on this computer.
        await t.fresh();
        const s = await t.storey();
        const has = async () => (await t.call('doc.query', { name: 'types.list', args: {} })).types.find((x) => x.name === 'WT1 BCA 30 termoizolat');
        t.expect(!(await has()), 'the new project has no wall type of that name');
        await t.button('File', { exact: true });
        await t.page.locator('[data-menu=extensions]').click();
        const dialog = t.page.locator('[data-dialog=extensions]');
        await dialog.waitFor({ timeout: 10_000 });
        await dialog.locator('[data-action=extensions-catalogue]').click();
        await dialog.locator('[data-field=extensions-index]').fill(join(dir, 'index.json'));
        await dialog.locator('[data-action=extensions-index-set]').click();
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.pereti-ro"]').click({ timeout: 15_000 });
        const details = dialog.locator('[data-extension-details="ro.ciuntucbimstudio.pereti-ro"]');
        await details.locator('[data-testid=extension-non-commercial]').waitFor({ timeout: 5000 });
        await t.wait(300);
        await t.shot('extensions-types', { of: '[data-dialog=extensions]', pad: 0 });
        // Install: the pack checked and kept, its type brought into the open project in one step to undo.
        await details.locator('[data-action=extension-install]').click();
        await dialog.locator('[data-extension="ro.ciuntucbimstudio.pereti-ro"] .ext-row-mark').waitFor({ timeout: 20_000 });
        let type = null;
        for (let i = 0; i < 20 && !type; i++) { type = await has(); if (!type) await t.wait(250); }
        t.expect(!!type, 'installing the types extension brought its wall type into the project');
        await t.ui({ dialog: null });
        const wall = (await t.cmd('architecture.addWall', { storeyId: s, typeId: type.id, x1: 0, y1: 0, x2: 6, y2: 0 })).id;
        const q = await t.call('quantities.of', { id: wall });
        t.expect(q.some((x) => x.part === 'layer:2'), 'a wall of the installed type is drawn with its layers');
      } finally {
        await t.page.evaluate(() => { localStorage.removeItem('lapin.extensions'); localStorage.removeItem('lapin.extensions.index'); try { indexedDB.deleteDatabase('lapin-type-packs'); } catch { /* none */ } }).catch(() => undefined);
        await t.ui({ dialog: null });
      }
    },
  },
  {
    // Decision 489, stage 2: Python in Lapin. Preferences → Developer extras switched on; View → Script opens the panel; the
    // editor's script (four walls round a room), named and saved in Mine, runs as one undo step named after it; the console
    // reads the model; one Undo takes the four walls away. Python is this computer's own (3.11 or newer): with none, the
    // panel's message is photographed and the run is left out, saying so.
    id: 'python-console',
    touches: ['scripting', 'architecture'],
    async run(t) {
      await t.fresh();
      const walls = async () => (await t.nodes()).filter((n) => n.type === 'IfcWall').length;
      try {
        await t.ui({ dialog: 'preferences' });
        await t.page.waitForSelector('[data-testid=preferences]', { timeout: 10_000 });
        await t.page.locator('[data-field=prefs-developer-extras]').check();
        await t.page.locator('[data-field=prefs-python]').waitFor({ timeout: 5000 });
        // The section's end in view: the switch, the Python and the editor it names.
        await t.page.locator('.prefs-page').evaluate((e) => { e.scrollTop = e.scrollHeight; });
        await t.wait(300);
        await t.shot('python-preferences', { of: '.prefs-dialog', pad: 0 });
        await t.ui({ dialog: null });
        await t.button('View', { exact: true });
        await t.page.locator('.band button.tool', { hasText: /^Script$/ }).first().click();
        const panel = t.page.locator('[data-panel=script]');
        await panel.waitFor({ timeout: 20_000 });
        await t.until(async () => (await panel.locator('[data-field=script-status]').count()) > 0, 'the panel says which Python it found');
        const status = await t.call('script.status', {});
        if (!status.ok) {
          console.log(`  ! python-console: ${status.message} The panel is photographed without a run.`);
          await t.shot('python-panel', { of: '[data-panel=script]', pad: 0 });
          return;
        }
        // The editor's script named, saved in Mine, then run: one undo step named after it.
        const name = say({ en: 'Room walls', ro: 'Pereții camerei' }, t.lang);
        await panel.locator('[data-field=script-name]').fill(name);
        await panel.locator('[data-action=script-save]').click();
        await panel.locator(`[data-script="${name}.py"]`).waitFor({ timeout: 10_000 });
        await panel.locator('[data-action=script-run]').click();
        await t.until(async () => (await panel.locator('.script-line.ok').count()) > 0, 'the run ends well', 60_000);
        t.expect(await walls() === 4, `the script made four walls (${await walls()})`);
        const doc = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().doc);
        t.expect(doc?.undoLabel === name, `the run is one undo step named after the script (${doc?.undoLabel})`);
        // The console reads the model.
        const input = panel.locator('[data-field=script-console-input]');
        await input.fill('len(lapin.model.walls)');
        await input.press('Enter');
        await t.until(async () => (await panel.locator('.script-line.out').last().innerText()).trim() === '4', 'the console says how many walls there are');
        await t.fit();
        await t.wait(600);
        await t.shot('python-panel');
        // One Undo takes the whole run away.
        await t.page.locator('.undo-btn').first().click();
        await t.until(async () => (await walls()) === 0, 'one Undo takes the four walls away');
        await t.shot('python-undo', { of: '[data-panel=script]', pad: 0 });
        await t.page.evaluate(async () => (await import('/src/app/prefs.ts')).setPrefs({ developerExtras: false }));
      } finally {
        await t.call('script.delete', { name: `${say({ en: 'Room walls', ro: 'Pereții camerei' }, t.lang)}.py` }).catch(() => undefined);
      }
    },
  },
  {
    // Decision 489, stage 3: the macro recorder. Record, two walls drawn in the plan with the wall tool, Stop: the recorder's tab
    // shows them as two lines of Python (the storey found by its name, the second wall's start the first's end); Copy puts the
    // script on the clipboard; in a new project the copied script, run from the editor, draws the same two walls as one undo step.
    id: 'python-recorder',
    touches: ['scripting', 'architecture'],
    async run(t) {
      await t.fresh();
      const page = t.page;
      const walls = async () => (await t.nodes()).filter((n) => n.type === 'IfcWall');
      try {
        await page.evaluate(async () => (await import('/src/app/prefs.ts')).setPrefs({ developerExtras: true }));
        await t.button('View', { exact: true });
        await page.locator('.band button.tool', { hasText: /^Script$/ }).first().click();
        const panel = page.locator('[data-panel=script]');
        await panel.waitFor({ timeout: 20_000 });
        await panel.locator('[data-action=script-tab-recorder]').click();
        await panel.locator('[data-action=script-record]').click();
        await t.until(async () => (await panel.locator('[data-action=script-record-stop]').count()) > 0, 'the recorder records');
        // The panel closed while drawing (it would cover the plan): the recorder goes on.
        await page.evaluate(async () => (await import('/src/modules/scripting/store.ts')).closePanel());
        await t.wait(300);
        // Two walls drawn in the plan, as a user draws them: start, a corner, the end.
        await t.tool('architecture.wall');
        for (const [x, y] of [[0, 0], [5, 0], [5, 4]]) {
          const [px, py] = await t.planPoint(x, y);
          await page.mouse.move(px, py, { steps: 3 });
          await page.mouse.click(px, py);
          await t.wait(300);
        }
        await t.keys('Escape', 'Escape');
        await t.until(async () => (await walls()).length === 2, 'two walls are drawn');
        await page.evaluate(async () => (await import('/src/modules/scripting/store.ts')).openPanel());
        await panel.waitFor({ timeout: 20_000 });
        const recorder = panel.locator('[data-field=script-recorder]');
        await t.until(async () => ((await recorder.innerText()).match(/lapin\.architecture\.add_wall\(/g) ?? []).length === 2, 'the recorder shows the two walls as two lines');
        const text = await recorder.innerText();
        t.expect(/storey1 = lapin\.model\.storey\(/.test(text), 'the storey is found by its name, so the script works in another project');
        await t.fit();
        await t.wait(500);
        await t.shot('python-recorder');
        await panel.locator('[data-action=script-record-stop]').click();
        const script = await page.evaluate(async () => (await import('/src/modules/scripting/store.ts')).recordedText());
        await panel.locator('[data-action=script-record-copy]').click().catch(() => undefined);

        // A new project: the copied script pasted into the editor and run.
        const status = await t.call('script.status', {});
        if (!status.ok) { console.log(`  ! python-recorder: ${status.message} The script is not run.`); return; }
        await page.evaluate(async () => (await import('/src/engine/session.ts')).engine.call('project.new'));
        await t.until(async () => (await walls()).length === 0, 'a new project, with no walls');
        await page.evaluate(async (code) => { const s = await import('/src/modules/scripting/store.ts'); s.newScript(code); }, script);
        await panel.locator('[data-field=script-name]').fill(say({ en: 'Two walls', ro: 'Doi pereți' }, t.lang));
        await panel.locator('[data-action=script-save]').click();
        await panel.locator(`[data-script="${say({ en: 'Two walls', ro: 'Doi pereți' }, t.lang)}.py"]`).waitFor({ timeout: 10_000 });
        await panel.locator('[data-action=script-tab-console]').click();
        await panel.locator('[data-action=script-run]').click();
        await t.until(async () => (await panel.locator('.script-line.ok').count()) > 0, 'the recorded script runs well', 60_000);
        t.expect((await walls()).length === 2, `the script draws the same two walls in the new project (${(await walls()).length})`);
        const doc = await page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().doc);
        t.expect(doc?.undoLabel === say({ en: 'Two walls', ro: 'Doi pereți' }, t.lang), `one undo step named after the script (${doc?.undoLabel})`);
        await t.fit();
        await t.wait(600);
        await t.shot('python-recorder-new');
      } finally {
        await page.evaluate(async () => (await import('/src/app/prefs.ts')).setPrefs({ developerExtras: false })).catch(() => undefined);
        await t.call('script.delete', { name: `${say({ en: 'Two walls', ro: 'Doi pereți' }, t.lang)}.py` }).catch(() => undefined);
      }
    },
  },
  {
    // Decision 489, stage 4: a script's tool. A script of Mine with a header (# lapin: tool=…, icon=…, run=…) puts its tool in
    // My tools (group Scripts) without being run; a click runs it: its lapin.ui.ask dialog (sizes in metres, the storey) is the
    // window's own; OK makes four walls as one undo step named after the tool; one Undo takes them away. Developer extras stay
    // off: a tool is for anyone. Python is this computer's own: with none, the tool is photographed and the run left out.
    id: 'python-tools',
    touches: ['scripting', 'architecture', 'core'],
    async run(t) {
      await t.fresh();
      const page = t.page;
      const walls = async () => (await t.nodes()).filter((n) => n.type === 'IfcWall').length;
      const file = `${say({ en: 'Perimeter walls', ro: 'Pereți perimetrali' }, t.lang)}.py`;
      const tool = say({ en: 'Perimeter walls', ro: 'Pereți perimetrali' }, t.lang);
      const code = [
        `# lapin: name="${tool}", version="1.0.0", tool="${tool}", icon="square", run="perimeter"`,
        `# lapin: tooltip="${say({ en: 'Four walls round a rectangle, one undo step', ro: 'Patru pereți în jurul unui dreptunghi, un singur pas de anulare' }, t.lang)}"`,
        '',
        'def perimeter():',
        `    v = lapin.ui.ask("${tool}", {`,
        `        "${say({ en: 'width', ro: 'lățime' }, t.lang)}": lapin.ui.number(8.0, unit="m", min=0.5),`,
        `        "${say({ en: 'depth', ro: 'adâncime' }, t.lang)}": lapin.ui.number(6.0, unit="m", min=0.5),`,
        `        "${say({ en: 'height', ro: 'înălțime' }, t.lang)}": lapin.ui.number(2.8, unit="m", min=0.5),`,
        `        "${say({ en: 'storey', ro: 'nivel' }, t.lang)}": lapin.ui.storey(),`,
        '    })',
        '    if v is None:',
        '        return',
        `    w, d, h = v["${say({ en: 'width', ro: 'lățime' }, t.lang)}"], v["${say({ en: 'depth', ro: 'adâncime' }, t.lang)}"], v["${say({ en: 'height', ro: 'înălțime' }, t.lang)}"]`,
        '    corners = [(0, 0), (w, 0), (w, d), (0, d)]',
        '    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):',
        `        lapin.architecture.add_wall(storey_id=v["${say({ en: 'storey', ro: 'nivel' }, t.lang)}"].id, x1=x1, y1=y1, x2=x2, y2=y2, height=h)`,
        '',
      ].join('\n');
      try {
        await page.evaluate(async () => { const m = await import('/src/app/myTools.ts'); m.useMyTools.setState({ tabs: [], past: [] }); localStorage.removeItem('lapin.scriptTools.added'); });
        await t.call('script.save', { name: file, code });
        // Mine read again (the panel's own refresh): the header's tool appears in My tools, without the script being run.
        await page.evaluate(async () => { await (await import('/src/modules/scripting/store.ts')).refreshFiles(); await (await import('/src/modules/scripting/scriptTools.ts')).refreshTools(); });
        const id = `script:mine:${file}#${tool}`;
        await t.until(async () => page.evaluate(async (x) => (await import('/src/app/myTools.ts')).myTab().groups.some((g) => g.label === 'Scripts' && g.items.some((i) => i.tool === x)), id), 'the header\'s tool is put in My tools, group Scripts');
        await page.locator('[data-tab=my-tools]').click();
        await t.wait(400);
        const button = page.locator(`.my-band [data-my-tool="${id}"]`);
        await button.waitFor({ timeout: 10_000 });
        await t.shot('python-tools-tab', { of: '.topbar', pad: 0 });
        const status = await t.call('script.status', {});
        if (!status.ok) { console.log(`  ! python-tools: ${status.message} The tool is not run.`); return; }
        // A click: the script asks for the sizes in a dialog of Lapin's own.
        await button.click();
        const dialog = page.locator('[data-dialog=script-ask]');
        await dialog.waitFor({ timeout: 60_000 });
        await dialog.locator(`[data-ask-field="${say({ en: 'width', ro: 'lățime' }, t.lang)}"] input`).fill('10');
        await t.wait(300);
        await t.shot('python-tools-ask', { of: '[data-dialog=script-ask]', pad: 16 });
        await dialog.locator('[data-action=ask-ok]').click();
        await t.until(async () => (await walls()) === 4, 'OK: the tool makes four walls', 60_000);
        await t.until(async () => (await page.evaluate(async () => (await import('/src/modules/scripting/store.ts')).useScripting.getState().running)) == null, 'the run ends');
        const doc = await page.evaluate(async () => (await import('/src/engine/session.ts')).useSession.getState().doc);
        t.expect(doc?.undoLabel === tool, `the tool's run is one undo step named after it (${doc?.undoLabel})`);
        await t.fit();
        await t.wait(600);
        await t.shot('python-tools-walls');
        // One Undo takes the four walls away.
        await page.locator('.undo-btn').first().click();
        await t.until(async () => (await walls()) === 0, 'one Undo takes the four walls away');
      } finally {
        await t.call('script.delete', { name: file }).catch(() => undefined);
      }
    },
  },
  {
    id: 'help',
    async run(t) {
      await t.fresh();
      await t.button('Ask Me', { exact: true });
      const input = t.page.getByRole('textbox', { name: 'Your question' });
      await input.fill(say(ASK_COPY, t.lang));
      await input.press('Enter');
      await t.wait(400);
      t.expect(await t.page.getByText('How do I copy a wall?').count() > 0, 'Ask Lapin answers how to copy a wall');
      await t.shot('ask-me', { of: '[aria-label="Ask Lapin"]', pad: 0 });
      await t.keys('Escape');
      await t.page.mouse.click(5, 600);
      await t.page.locator('.help-menu > button').click();
      await t.wait(300);
      await t.shot('help-menu', { clip: { x: 1100, y: 0, width: 340, height: 400 } });
      t.expect(await t.page.getByRole('menuitem', { name: 'Forum', exact: true }).count() === 1 && await t.page.getByRole('menuitem', { name: 'Ask on the forum…' }).count() === 1, 'the help menu opens the forum and a new topic on it');
      t.expect((await t.page.locator('.menu-langs button').allTextContents()).join(' ') === 'English Română Español Deutsch Français Dansk Italiano Polski Português ພາສາລາວ 简体中文', 'the help menu offers the interface in English, Romanian, Spanish, German, French, Danish, Italian, Polish, Portuguese, Lao and Simplified Chinese');
      await t.page.getByRole('menuitem', { name: 'About Lapin…' }).click();
      await t.wait(200);
      t.expect(await t.page.getByText('Helpware', { exact: true }).count() > 0, 'About Lapin says what helpware means');
      await t.shot('about', { of: '.dialog.about', pad: 0 });
      // The report form with a reply asked for (decision 177): the nickname and what is kept. Nothing is sent: the form is cancelled.
      await t.keys('Escape');
      await t.page.locator('.help-menu > button').click();
      await t.wait(200);
      await t.page.getByRole('menuitem', { name: 'Report an issue…' }).click();
      await t.wait(200);
      await t.page.getByLabel("I'd like a reply").check();
      await t.page.getByLabel('Nickname (optional)').fill('Ana');
      t.expect(await t.page.locator('.reply-note').count() === 1, 'the report form says what is kept when a reply is asked for');
      await t.shot('report-reply', { of: '[aria-label="Report an issue"] .dialog', pad: 0 });
      await t.button('Cancel', { exact: true });
    },
  },
  {
    // The model assistant (docs/plan-assistant.md) is the desktop app's: the guide's browser run opens it with the
    // development switch, and a stand-in for Ollama answers with the tool calls a model makes (the tools themselves are Lapin's).
    id: 'assistant',
    async run(t) {
      await t.page.addInitScript(() => { localStorage.setItem('lapin.assistant.dev', '1'); localStorage.setItem('lapin.assistant', JSON.stringify({ url: 'http://127.0.0.1:11434', model: 'qwen3-coder:30b' })); });
      await fakeOllama(t.page, t.lang);
      await t.fresh();
      await house(t);
      await t.page.locator('.side-tabs').getByRole('tab', { name: 'Assistant' }).click();
      const input = t.page.locator('[data-testid=assistant-panel] textarea');
      await input.waitFor({ state: 'visible' });
      await t.page.waitForSelector('[data-testid=assistant-panel] textarea:not([disabled])', { timeout: 10_000 });
      for (const q of say(ASSISTANT.questions, t.lang)) {
        await input.fill(q);
        await input.press('Enter');
        await t.page.waitForSelector('[data-testid=assistant-panel] button[title=Send]', { timeout: 20_000 });
      }
      t.expect(await t.page.locator('[data-testid=assistant-changes] tbody tr').count() === 1, 'the assistant proposes a value for the door, waiting for approval');
      await t.shot('assistant', { of: '.props-side', pad: 0 });
      await t.page.locator('[data-testid=assistant-changes]').getByRole('button', { name: 'Apply' }).click();
      await t.wait(400);
      const door = (await t.nodes()).find((n) => n.type === 'IfcDoor');
      t.expect(door?.props.params?.[0]?.pset === 'Pset_DoorCommon' && door.props.params[0].value === 'EI30', 'Apply writes Pset_DoorCommon.FireRating on the door');
    },
  },
  {
    // The interface's modes (View → Interface…), the panels folding, the keyboard shortcuts (KS) and their table for the page.
    id: 'interface',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.fit();
      // The table of the default shortcuts, from the registry, in the page's language.
      await t.data('shortcuts', await t.page.evaluate(async (lang) => {
        const { commands, current } = await import('/src/app/commands.ts');
        const { formatBinding } = await import('/src/app/shortcuts.ts');
        const { translate } = await import('/src/i18n/index.ts');
        const say = (s) => (lang === 'en' ? s : translate(s, lang) ?? s);
        const { bindings } = current();
        return commands().filter((c) => !c.id.startsWith('hotbar.') && (bindings[c.id] ?? []).length)
          .map((c) => ({ category: say(c.category), command: say(c.label), keys: bindings[c.id].map((b) => formatBinding(b, false)) }));
      }, t.lang));
      await t.tool('app.interface');
      t.expect(await t.page.locator('.interface-dialog').count() === 1, 'View → Interface… opens its dialog');
      await t.shot('interface-dialog', { of: '.interface-dialog', pad: 0 });
      // The workspaces (decision 243): MEP shows its tabs in its order; back to All for the rest of the scene.
      await t.page.locator('.interface-dialog [data-workspace=mep]').click();
      await t.wait(300);
      t.expect((await t.page.locator('.tabs-row .tab').count()) <= 8, 'the MEP workspace shows only its tabs (and My tools)');
      await t.shot('workspaces', { of: '.interface-dialog [data-field=workspace]', pad: 6 });
      await t.page.locator('.interface-dialog [data-workspace=all]').click();
      await t.wait(300);
      // Compact: an icon band, one status line, the quick tools on the edge, the browser on a rail.
      await t.page.locator('.seg.presets .seg-item').nth(1).click();
      await t.wait(300);
      await t.page.locator('.interface-dialog .btn.primary').click();
      await t.wait(600);
      t.expect(await t.page.locator('.band.icons').count() === 1 && await t.page.locator('.status.line').count() === 1, 'Compact shows the icon band and the status line');
      t.expect(await t.page.locator('.hotbar.docked').count() === 1 && await t.page.locator('.panel.rail.browser').count() === 1, 'Compact docks the quick tools and folds the browser');
      await t.fit();
      await t.callouts([
        { of: '.band.icons', n: 1, at: 'bottom' },
        { of: '.panel.rail.browser .rail-icon >> nth=0', n: 2, at: 'after' },
        { of: '.hotbar.docked', n: 3, at: 'after' },
        { of: '.key-plan-button', n: 4, at: 'before' },
        { of: '.status.line .cad-prompt', n: 5, at: 'center' },
      ]);
      await t.shot('interface-compact');
      await t.clearCallouts();
      // A folded browser over the view, from its rail.
      await t.page.locator('.panel.rail.browser .rail-icon').nth(0).click();
      await t.wait(400);
      t.expect(await t.page.locator('.panel-overlay nav.browser').count() === 1, 'the rail opens the browser over the view');
      await t.shot('browser-overlay', { clip: { x: 0, y: 0, width: 640, height: 560 } });
      await t.keys('Escape');
      // Focus: the tabs and a command line; a selected wall's card.
      await t.keys('Control+.');
      await t.page.locator('.seg.presets .seg-item').nth(2).click();
      await t.page.locator('.interface-dialog .btn.primary').click();
      await t.wait(400);
      await select(t, walls[0]);
      await t.wait(500);
      t.expect(await t.page.locator('.sel-card').count() === 1, 'Focus shows the selection in a card');
      await t.shot('interface-focus');
      await t.ui({ selection: null, selected: [] });
      // Back to Comfortable, and the shortcuts by their own letters.
      await t.keys('Control+.');
      await t.page.locator('.seg.presets .seg-item').nth(0).click();
      await t.page.locator('.interface-dialog .btn.primary').click();
      await t.wait(400);
      t.expect(await t.page.locator('.band .group').count() > 0, 'Comfortable brings the large ribbon back');
      // The panels on either side (decision 427, report #123): swapped, then stacked on the left; back to the default.
      await t.tool('view.panels.swapped');
      await t.wait(300);
      t.expect(await t.page.locator('.side-col.on-left .props-side').count() === 1 && await t.page.locator('.side-col.on-right .browser-side').count() === 1, 'View → Panels swaps the browser and the properties');
      await t.tool('view.panels.bothLeft');
      await t.wait(400);
      t.expect(await t.page.locator('.side-col.on-left.stacked .browser-side').count() === 1 && await t.page.locator('.side-col.on-left.stacked .props-side').count() === 1 && await t.page.locator('.stack-sep').count() === 1, 'both panels stack on the left with a divider');
      await t.fit();
      await t.shot('panel-layouts', { clip: { x: 0, y: 0, width: 760, height: 620 } });
      await t.tool('view.panels.default');
      await t.wait(300);
      t.expect(await t.page.locator('.side-col.on-left .browser-side').count() === 1 && await t.page.locator('.side-col.on-right .props-side').count() === 1, 'the default layout is back');
      // The quick tools the user chooses (decision 259): slot 7 given the stair from View → Quick tools…, slot 2 emptied.
      await t.tool('app.quickTools');
      t.expect(await t.page.locator('.quick-tools-dialog').count() === 1, 'View → Quick tools… opens its dialog');
      await t.page.locator('.quick-tools-dialog [data-slot="7"]').selectOption('architecture.stair');
      await t.page.locator('.quick-tools-dialog [data-slot="2"]').selectOption('');
      // The wall's slot with a type of its own (decision 260): the project's first wall type, by its name.
      await t.page.locator('.quick-tools-dialog [data-slot-type="3"]').selectOption({ index: 1 });
      await t.wait(300);
      await t.shot('quick-tools-dialog', { of: '.quick-tools-dialog', pad: 0 });
      await t.page.locator('.quick-tools-dialog .btn.primary').click();
      await t.wait(300);
      t.expect(await t.page.locator('.hotbar .slot.empty').count() >= 1, 'an emptied slot waits for a tool');
      t.expect(await t.page.locator('.hotbar .slot').nth(6).getAttribute('data-tool') === 'architecture.stair', 'the stair stands in slot 7');
      const typed = t.page.locator('.hotbar .slot.typed').first();
      t.expect(await typed.count() === 1 && !!(await typed.getAttribute('data-type')), 'the wall\'s slot shows its type\'s name');
      await t.shot('quick-tools', { of: '.hotbar', pad: 8 });
      await typed.click();
      await t.wait(200);
      t.expect(await t.page.evaluate(async () => { const { useUi } = await import('/src/app/store.ts'); const s = useUi.getState(); return s.tool === 'architecture.wall' && !!s.wallTypeId; }), 'the typed slot starts the wall tool with its type');
      await t.keys('Escape');
      await t.page.evaluate(async () => { (await import('/src/platform/modules.ts')).resetSlots(); });
      await t.page.mouse.click(700, 500);
      await t.keys('k', 's');
      await t.wait(400);
      t.expect(await t.page.locator('.shortcuts-dialog').count() === 1, 'KS opens the keyboard shortcuts');
      await t.page.locator('.shortcuts-dialog .props-filter input').fill('wall');
      await t.wait(300);
      await t.shot('shortcuts-dialog', { of: '.shortcuts-dialog', pad: 0 });
      // The Command aliases page (decision 473): an alias of the user's own, with what it shadows.
      await t.page.locator('[data-field=shortcuts-tab-aliases]').click();
      await t.page.locator('[data-field=alias-name]').fill('wa');
      await t.page.locator('[data-field=alias-find]').fill('line');
      await t.wait(200);
      await t.page.locator('[data-field=alias-target]').selectOption({ index: 0 });
      await t.wait(300);
      t.expect(await t.page.locator('.aliases-panel .alias-note').count() >= 1, 'an alias with the letters of a shortcut shows what it shadows');
      await t.shot('shortcuts-aliases', { of: '.shortcuts-dialog', pad: 0 });
      await t.page.locator('[data-field=alias-add]').click();
      await t.wait(200);
      t.expect(await t.page.locator('[data-alias=WA]').count() === 1, 'the alias is added');
      await t.page.locator('[data-alias=WA] button[title="Remove this alias"]').click();
    },
  },
  {
    // My tools (decision 479): a ribbon tab of the user's own, filled by right-click and by dragging, ordered in the ribbon and in Customize My tools….
    id: 'my-tools',
    touches: ['core', 'architecture'],
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      const store = () => t.page.evaluate(async () => {
        const m = await import('/src/app/myTools.ts');
        return m.myTab().groups.map((g) => ({ label: g.label, tools: g.items.map((i) => i.tool) }));
      });
      await t.page.evaluate(async () => { const m = await import('/src/app/myTools.ts'); m.useMyTools.setState({ tabs: [], past: [] }); });
      // Empty: the tab stands first, with its hint.
      await t.page.locator('[data-tab=my-tools]').click();
      await t.wait(300);
      t.expect(await t.page.locator('.my-empty').count() === 1, 'an empty My tools tab says how to fill it');
      await t.shot('my-tools-empty', { of: '.topbar', pad: 0 });
      // Right-click a tool of another tab → Add to My tools.
      await t.page.locator('[data-tab=architecture]').click();
      await t.wait(300);
      await t.page.locator('.band .tool', { hasText: 'Wall' }).first().click({ button: 'right' });
      await t.wait(200);
      await t.shot('my-tools-menu', { of: '.context-menu', pad: 12 });
      await t.page.locator('.context-menu .context-item', { hasText: 'Add to My tools' }).first().click();
      await t.wait(200);
      // A second one in a group of its own, a third dragged onto the tab's header.
      await t.page.locator('.band .tool', { hasText: 'Door' }).first().click({ button: 'right' });
      await t.page.locator('.context-menu .context-item', { hasText: 'Add to a new group in My tools' }).click();
      await t.wait(200);
      await t.page.locator('.band .tool', { hasText: 'Window' }).first().dragTo(t.page.locator('[data-tab=my-tools]'));
      await t.wait(300);
      let groups = await store();
      t.expect(groups.length === 2 && groups[0].tools.length === 1 && groups[1].tools.length === 2, 'right-click and dragging onto the tab header fill My tools: ' + JSON.stringify(groups));
      // Reordered in the ribbon itself: the window dragged in front of the door.
      await t.page.locator('[data-tab=my-tools]').click();
      await t.wait(400);
      const mine = t.page.locator('.my-band');
      const door = groups[1].tools[0], win = groups[1].tools[1];
      await mine.locator(`[data-my-tool="${win}"]`).dragTo(mine.locator(`[data-my-tool="${door}"]`), { targetPosition: { x: 4, y: 20 } });
      await t.wait(400);
      groups = await store();
      t.expect(groups[1].tools[0] === win && groups[1].tools[1] === door, 'dragging a tool in the ribbon reorders its group: ' + JSON.stringify(groups));
      // And between groups: the wall dragged into the second group.
      await mine.locator(`[data-my-tool="${groups[0].tools[0]}"]`).dragTo(mine.locator(`[data-my-tool="${door}"]`), { targetPosition: { x: 40, y: 20 } });
      await t.wait(400);
      groups = await store();
      t.expect(groups[0].tools.length === 0 && groups[1].tools.length === 3, 'a tool moves between groups by dragging: ' + JSON.stringify(groups));
      await t.page.evaluate(async () => { (await import('/src/app/myTools.ts')).undoMyTools(); });
      await t.wait(300);
      // A group renamed in place.
      await mine.locator('.my-group-label').nth(0).dblclick();
      await t.page.locator('.my-rename').fill('Walls');
      await t.keys('Enter');
      await mine.locator('.my-group-label').nth(1).dblclick();
      await t.page.locator('.my-rename').fill('Openings');
      await t.keys('Enter');
      await t.wait(300);
      groups = await store();
      t.expect(groups[0].label === 'Walls' && groups[1].label === 'Openings', 'double-clicking a group\'s name renames it');
      await t.page.locator('.band .group-label', { hasText: 'Walls' }).hover();
      await t.shot('my-tools-tab', { of: '.topbar', pad: 0 });
      // Customize My tools…: the whole catalogue on the left, the tab on the right.
      await t.page.locator('[data-testid=my-tools-customize]').click();
      await t.wait(500);
      t.expect(await t.page.locator('.my-tools-dialog').count() === 1, 'the customize button opens Customize My tools…');
      await t.page.locator('.my-tools-dialog .my-search input').fill('stair');
      await t.wait(200);
      await t.page.locator('.my-tools-dialog .my-cat-row .icon').first().click();
      await t.wait(300);
      await t.page.locator('.my-tools-dialog .my-search input').fill('');
      groups = await store();
      t.expect(groups.flatMap((g) => g.tools).some((x) => x.includes('stair')), 'a tool found by search is added with its + button');
      // By keys: Alt+↑ moves the focused tool up, Ctrl+Z takes the move back.
      const row = t.page.locator('.my-tools-dialog .my-row', { hasText: 'Stair' });
      await row.focus();
      const before = JSON.stringify(await store());
      await t.keys('Alt+ArrowUp');
      t.expect(JSON.stringify(await store()) !== before, 'Alt+↑ moves the tool focused');
      await t.keys('Control+z');
      t.expect(JSON.stringify(await store()) === before, 'Ctrl+Z takes the move back');
      await t.shot('my-tools-dialog', { of: '.my-tools-dialog', pad: 0 });
      await t.page.locator('.my-tools-dialog .btn.primary').click();
      await t.wait(300);
      await t.page.evaluate(async () => { const m = await import('/src/app/myTools.ts'); m.useMyTools.setState({ tabs: [], past: [] }); localStorage.removeItem('lapin.myTools'); });
    },
  },
  {
    // The catalog (decision 263): the browser's Catalog tab, the project's types drawn small; a click places with the type, a card dragged onto a quick slot keeps it there.
    id: 'catalog',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      await t.page.locator('nav.browser [data-tab=catalog]').click();
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=catalog] .catalog-card').count() >= 1, 'the catalog shows the project\'s wall types');
      await t.shot('catalog', { of: 'nav.browser', pad: 0 });
      await t.page.locator('[data-testid=catalog] [data-kind="architecture.window"]').click();
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=catalog] .catalog-card svg polyline').count() > 0, 'a window type is drawn by its elevation');
      await t.shot('catalog-windows', { of: 'nav.browser', pad: 0 });
      // A window card dragged onto the empty quick slot after the last tool.
      const card = t.page.locator('[data-testid=catalog] .catalog-card').first();
      const name = await card.getAttribute('data-type');
      const empty = t.page.locator('.hotbar .slot.empty').last();
      if (await empty.count() === 0) await t.page.evaluate(async () => (await import('/src/platform/modules.ts')).putInSlot(8, null));
      await card.dragTo(t.page.locator('.hotbar .slot.empty').last());
      await t.wait(300);
      t.expect(await t.page.locator(`.hotbar .slot[data-type="${name}"]`).count() === 1, 'the dragged card stands in a quick slot with its type');
      // A click on a wall card places walls of that type.
      await t.page.locator('[data-testid=catalog] [data-kind="architecture.wall"]').click();
      await t.page.locator('[data-testid=catalog] .catalog-card').first().click();
      await t.wait(200);
      t.expect(await t.page.evaluate(async () => { const s = (await import('/src/app/store.ts')).useUi.getState(); return s.tool === 'architecture.wall' && !!s.wallTypeId; }), 'a card\'s click starts the tool with its type');
      await t.keys('Escape');
      await t.page.evaluate(async () => { (await import('/src/platform/modules.ts')).resetSlots(); });
      await t.page.locator('nav.browser .browser-tab').first().click();
    },
  },
  {
    // The CDE (docs/plan-cde.md, decision 185): the project file holds the issues; the folder is the desktop app's, so the guide's
    // browser run sets it with the development switch (the folder typed in) in a temporary folder. The project is saved as House.lapin
    // (an issue is a version in its file), issued for real, and the dialog shows the issue as issued, the project saved after it.
    id: 'cde',
    async run(t) {
      await t.page.addInitScript(() => { localStorage.setItem('lapin.cde.dev', '1'); });
      await t.fresh();
      await house(t);
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: await t.storey(), name: 'Ground floor plan', scale: 100 });
      const sheet = await t.cmd('sheet.create', { name: 'Ground floor plan', number: 'A-101', paper: 'A3' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: plan.id, x: 150, y: 120 });
      await t.cmd('sheet.titleBlock', { id: sheet.id, fields: { drawnBy: 'IC', date: '30.10.2026' } });
      await t.cmd('cde.setNaming', { project: 'HOUSE', originator: 'CBS' });
      const dir = mkdtempSync(join(tmpdir(), 'lapin-guide-cde-'));
      mkdirSync(join(dir, 'CDE'));
      await t.cmd('cde.setFolder', { path: join(dir, 'CDE') });
      await t.call('project.save', { path: join(dir, 'House.lapin') });
      await t.wait(400);
      await t.tool('manage.projectFolder');
      t.expect((await t.page.locator('.dialog input[aria-label=Folder]').inputValue()).includes('lapin-guide-cde'), 'the project folder dialog shows the folder kept in the project');
      await t.shot('project-folder', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog-head button[title=Close]').click(); await t.wait(300);
      await t.tool('manage.check');
      await t.page.waitForSelector('[data-testid=cde-findings]', { timeout: 10_000 });
      t.expect(await t.page.locator('[data-testid=cde-findings] li').count() > 0, 'the check lists what the house still lacks (tags, properties)');
      await t.shot('cde-check', { of: '.dialog', pad: 0 });
      await t.page.locator('.dialog-head button[title=Close]').click(); await t.wait(300);
      await t.tool('manage.issue');
      await t.page.waitForSelector('[data-testid=cde-findings]', { timeout: 10_000 });
      t.expect((await t.page.locator('.dialog input[aria-label=Revision]').inputValue()) === 'P01', 'the first issue is proposed as P01');
      t.expect(await t.page.locator('.dialog .btn.primary').isDisabled(), 'Issue waits while the warnings are not accepted');
      await t.shot('cde-issue', { of: '.dialog', pad: 0 });
      await t.page.getByText('Issue anyway, with the warnings').click();
      await t.page.locator('.dialog .btn.primary').click();
      await t.page.waitForSelector('[data-testid=cde-issued]', { timeout: 30_000 });
      await t.wait(800);
      const issued = await t.page.locator('[data-testid=cde-issued]').innerText();
      t.expect(issued.includes('Issue 1 · P01 S2 · Shared') && issued.includes('HOUSE-CBS-ZZ-00-DR-A-0101_S2_P01.pdf'), 'the dialog shows the issue as issued, its own names');
      t.expect(!issued.includes('0002'), 'not the next issue\'s names');
      t.expect(!(await t.page.locator('.panel-title', { hasText: 'House' }).first().innerText()).includes('•'), 'the project is saved after the issue');
      await t.shot('cde-issued', { of: '.dialog', pad: 0 });
      await t.button('Done', { exact: true });
      await t.tool('manage.projectFolder');
      await t.page.waitForSelector('[data-testid=cde-issues]', { timeout: 10_000 });
      await t.page.locator('[data-testid=cde-issues] tbody tr button.link').first().click();
      await t.wait(400);
      t.expect((await t.page.locator('[data-testid=cde-storage]').innerText()).match(/\d/) != null, 'the size the issues take in the project file is shown');
      await t.shot('cde-issues', { of: '.dialog', pad: 0 });
      await t.button('Done', { exact: true });
    },
  },
  {
    // The written documents (docs/plan-documents.md): the house with two rooms and a sheet, the project's information written
    // in part, Manage → Documents…, a technical report from its template, the client typed in Project information (its chip
    // leaves To complete), an input written, then Manage → Project information… alone and the Issue dialog's documents.
    id: 'documents',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t);
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: storey, name: 'Ground floor plan', scale: 100 });
      const sheet = await t.cmd('sheet.create', { name: 'Ground floor plan', number: 'A-101', paper: 'A3' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: plan.id, x: 150, y: 120 });
      await t.cmd('cde.setNaming', { project: 'HOUSE', originator: 'CBS' });
      await t.cmd('project.setInfo', {
        name: 'Locuință unifamilială P', number: '12/2026', phase: 'DTAC', address: 'Str. Corneliu Codreanu nr. 14, Huși, jud. Vaslui',
        designer: 'Ciuntuc BIM Studio SRL', designerAddress: 'Huși, jud. Vaslui', chiefArchitect: 'arh. Ionuț Ciuntuc', author: 'arh. Ionuț Ciuntuc',
      });
      await t.tool('manage.documents');
      await t.page.waitForSelector('[data-dialog=documents]', { timeout: 10_000 });
      await t.page.locator('[data-field="doc-template-memoriu-arhitectura"]').click();
      await t.page.waitForSelector('[data-dialog=documents] .doc-sheet:not(.doc-measure)', { timeout: 20_000 });
      await t.wait(800);
      t.expect((await t.page.locator('[data-field=doc-preview]').innerText()).includes('Memoriu tehnic de arhitectură'), 'the preview shows the technical report filled from the project');
      t.expect((await t.page.locator('[data-field=doc-preview]').innerText()).includes('12/2026'), 'the preview reads the project number from Project information');
      t.expect(await t.page.locator('[data-field="doc-missing-project.client"]').count() === 1, 'To complete lists the client, not written yet');
      await t.shot('documents', { of: '[data-dialog=documents]', pad: 0 });
      await t.shot('documents-missing', { of: '[data-field=doc-missing]', pad: 6 });
      // The client written where To complete sends it: the Project information tab, the field focused.
      await t.page.locator('[data-field="doc-missing-project.client"]').click();
      await t.wait(400);
      await t.page.locator('[data-dialog=documents] [data-field=info-client]').fill('Popescu Ion și Maria');
      await t.page.locator('[data-dialog=documents] [data-field=info-client]').press('Enter');
      await t.page.locator('[data-dialog=documents] [data-field=info-clientAddress]').fill('Str. Mihail Kogălniceanu nr. 3, Huși, jud. Vaslui');
      await t.page.locator('[data-dialog=documents] [data-field=info-clientAddress]').blur();
      await t.page.waitForFunction(() => !document.querySelector('[data-field="doc-missing-project.client"]'), null, { timeout: 10_000 });
      t.expect((await t.page.locator('[data-field=doc-preview]').innerText()).includes('Popescu Ion și Maria'), 'the client typed shows in the preview');
      // An input: the structure, written over its [De completat] text.
      await t.page.locator('[data-field=doc-inputs-tab]').click();
      await t.wait(300);
      const structure = t.page.locator('[data-dialog=documents] [data-field=input-structura]');
      await structure.fill('Fundații continue din beton armat sub pereți, pereți portanți din zidărie de cărămidă cu stâlpișori și centuri din beton armat, planșeu din beton armat peste parter, șarpantă din lemn.');
      await structure.blur();
      await t.page.waitForFunction(() => (document.querySelector('[data-field=doc-preview]')?.textContent ?? '').includes('Fundații continue din beton armat'), null, { timeout: 10_000 })
        .catch(() => { throw new Error('expected: the input written shows in the preview'); });
      await t.wait(400);
      t.expect((await structure.inputValue()).startsWith('Fundații continue'), 'the input keeps what was written');
      await structure.scrollIntoViewIfNeeded();
      await t.shot('documents-inputs', { of: '[data-dialog=documents] .doc-side.right', pad: 0 });
      await t.page.locator('[data-dialog=documents] .dialog-head button[title=Close]').click(); await t.wait(300);
      t.expect(await t.page.locator('[data-testid=browser-documents]').getByText('Memoriu tehnic de arhitectură').count() > 0, 'the project browser lists the document under Documents');
      await t.tool('manage.projectInfo');
      await t.page.waitForSelector('[data-dialog=project-info]', { timeout: 10_000 });
      t.expect((await t.page.locator('[data-dialog=project-info] [data-field=info-client]').inputValue()) === 'Popescu Ion și Maria', 'Project information shows what the Documents dialog wrote');
      await t.shot('project-info', { of: '[data-dialog=project-info]', pad: 0 });
      await t.page.locator('[data-dialog=project-info] .dialog-head button[title=Close]').click(); await t.wait(300);
      await t.tool('manage.issue');
      await t.page.waitForSelector('[data-field=issue-documents]', { timeout: 10_000 });
      await t.page.waitForFunction(() => /-RP-/.test(document.querySelector('[data-field=issue-documents]')?.textContent ?? ''), null, { timeout: 10_000 });
      await t.page.locator('[data-field=issue-documents-docx]').check();
      t.expect(await t.page.locator('[data-field=issue-documents] input[type=checkbox]').first().isChecked(), 'the document goes with the issue, ticked at first');
      await t.shot('issue-documents', { of: '.dialog .cde-sheets', pad: 6 });
      await t.page.locator('.dialog-head button[title=Close]').click(); await t.wait(300);
    },
  },
  {
    // The document editor, templates and the company style (docs/plan-documents.md, stages 2 and 4): the technical report of the
    // house edited — a line with the client's field (Insert → Field…, searched), a table of the project, the storeys' repeat with
    // its item stepper —, then the Style tab (the headings' colour, a small logo made here), Save as template… into the project,
    // and Manage → Document templates… on it.
    id: 'documents-editor',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t);
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 3, y: 4, name: 'Living room', function: 'Living' });
      await t.cmd('architecture.addRoom', { storeyId: storey, x: 9.5, y: 4, name: 'Bedroom', function: 'Bedroom' });
      await t.cmd('project.setInfo', {
        name: 'Locuință unifamilială P', number: '12/2026', phase: 'DTAC', address: 'Str. Corneliu Codreanu nr. 14, Huși, jud. Vaslui',
        client: 'Popescu Ion și Maria', designer: 'Ciuntuc BIM Studio SRL', chiefArchitect: 'arh. Ionuț Ciuntuc', author: 'arh. Ionuț Ciuntuc',
      });
      await t.cmd('document.create', { template: 'memoriu-arhitectura' });
      await t.tool('manage.documents');
      await t.page.waitForSelector('[data-dialog=documents] .doc-sheet:not(.doc-measure)', { timeout: 20_000 });
      await t.wait(500);
      // Edit: the document's own blocks on a page, its fields as chips with their values.
      await t.page.locator('[data-field=doc-edit]').click();
      await t.page.waitForSelector('[data-field=doc-editor] .doc-chip', { timeout: 20_000 });
      await t.wait(800);
      t.expect((await t.page.locator('[data-field=doc-editor] .doc-chip[data-chip="project.number"]').first().innerText()).includes('12/2026'), 'a field shows as a chip with its value now');
      // A line of its own after the phase's: "Beneficiar: " and the client's field, found by searching.
      // The caret at the end of the phase's paragraph, as a click there puts it (the line wraps, so no key reaches its end alike everywhere).
      await t.page.locator('[data-field=doc-editor] .doc-editor-text p', { hasText: 'Faza:' }).first().click();
      t.expect(await t.page.evaluate(async () => {
        const { useDocEditor } = await import('/src/modules/core/documents/editor/editorStore.ts');
        const ed = useDocEditor.getState().editor;
        let end = -1;
        ed.state.doc.descendants((n, pos) => { if (end < 0 && n.type.name === 'paragraph' && n.textContent.startsWith('Faza:')) end = pos + n.nodeSize - 1; return end < 0; });
        if (end < 0) return false;
        ed.chain().focus().setTextSelection(end).run();
        return true;
      }), 'the phase\'s paragraph is in the editor');
      await t.page.keyboard.press('Enter');
      await t.page.keyboard.type('Beneficiar: ');
      await t.page.locator('[data-field=doc-insert]').click();
      await t.page.locator('[data-field=doc-insert-field]').click();
      await t.page.locator('[data-field=doc-field-search]').fill('client');
      await t.wait(300);
      t.expect(await t.page.locator('[data-field="doc-field-project.client"]').count() === 1, 'searching "client" finds the client\'s field');
      t.expect((await t.page.locator('[data-field="doc-field-project.client"]').innerText()).includes('Popescu'), 'the picker shows the field\'s value');
      await t.shot('documents-insert-field', { of: '[data-field=doc-editor] .doc-insert .doc-menu', pad: 8 });
      await t.page.locator('[data-field="doc-field-project.client"]').click();
      await t.wait(300);
      t.expect((await t.page.locator('[data-field=doc-editor] p', { hasText: 'Beneficiar:' }).first().locator('.doc-chip[data-chip="project.client"]').count()) === 1, 'the client\'s chip stands in the line typed');
      // A table of the project after it: the block's settings on the right.
      await t.page.keyboard.press('Enter');
      await t.page.locator('[data-field=doc-insert]').click();
      await t.page.locator('[data-field=doc-insert-table]').click();
      const table = t.page.locator('[data-field^=doc-table-]').first();
      await table.waitFor({ timeout: 10_000 });
      await table.click();
      await t.wait(800);
      // The table stands after the client's line; a click on it selects it.
      const placed = await t.page.evaluate(() => {
        const line = [...document.querySelectorAll('[data-field=doc-editor] .doc-editor-text p')].find((p) => p.textContent.includes('Beneficiar:'));
        let n = line?.nextElementSibling;
        const atom = n && (n.matches('.doc-atom') ? n : n.querySelector('.doc-atom'));
        if (!atom || !atom.matches('.doc-atom-table')) return false;
        atom.setAttribute('data-user-docs-new', '');
        return true;
      });
      t.expect(placed, 'the table is inserted after the client\'s line');
      await t.page.locator('[data-user-docs-new] .doc-atom-tag').click();
      await t.wait(600);
      t.expect(await t.page.locator('[data-field=doc-block-settings] [data-field=doc-set-source]').count() === 1, 'the table inserted is selected, its settings on the right');
      await t.page.waitForFunction(() => document.querySelectorAll('[data-field=doc-set-columns] input').length > 0, null, { timeout: 10_000 }).catch(() => {});
      await t.shot('documents-editor', { of: '[data-dialog=documents]', pad: 0 });
      // The storeys' repeat: its band, and the item shown while editing stepped with › .
      const band = t.page.locator('[data-field=doc-editor] .doc-repeat[data-repeat=storeys] > .doc-band').first();
      await band.scrollIntoViewIfNeeded();
      t.expect((await band.innerText()).match(/\d+ items/) != null, 'the repeat\'s band counts the storeys');
      await band.locator('[data-field=doc-repeat-next]').click();
      await t.wait(400);
      t.expect((await band.innerText()).includes('2/'), 'the band\'s › shows the next storey');
      await band.click();
      await t.wait(400);
      t.expect(await t.page.locator('[data-field=doc-block-settings] [data-field=doc-set-over]').count() === 1, 'a click on the band shows the repeat\'s settings');
      await t.shot('documents-repeat', { of: t.page.locator('[data-field=doc-editor] .doc-repeat[data-repeat=storeys]').first(), pad: 6, maxHeight: 360 });
      // The company style: the Style tab in preview, the headings' colour and a logo.
      await t.page.locator('[data-field=doc-edit]').click();
      await t.wait(800);
      await t.page.locator('[data-field=doc-style-tab]').click();
      await t.page.waitForSelector('[data-field=tpl-style-color]', { timeout: 10_000 });
      await t.page.locator('[data-field=tpl-style-color]').fill('#1f5f9e');
      await t.wait(900);
      const { writeFileSync } = await import('node:fs');
      const logo = join(mkdtempSync(join(tmpdir(), 'lapin-guide-logo-')), 'logo.png');
      writeFileSync(logo, await logoPng());
      await t.page.evaluate(() => { delete window.showOpenFilePicker; });
      const chooser = t.page.waitForEvent('filechooser', { timeout: 10_000 });
      await t.page.locator('[data-field=tpl-style-logo]').click();
      await (await chooser).setFiles(logo);
      await t.page.waitForSelector('[data-field=tpl-style-logo-width]', { timeout: 10_000 });
      await t.page.locator('[data-field=tpl-style-logo-width]').fill('35');
      await t.page.locator('[data-field=tpl-style-logo-width]').press('Enter');
      await t.page.locator('[data-field=tpl-style-logo-width]').blur();
      await t.wait(1200);
      const style = await t.call('doc.query', { name: 'document.style', args: { id: (await t.nodes()).find((n) => n.type === 'LapinDocument')?.id } });
      t.expect(/1f5f9e/i.test(JSON.stringify(style ?? {})), 'the headings\' colour is kept on the document');
      t.expect(JSON.stringify(style ?? {}).includes('"w":35'), 'the logo is kept with its width');
      await t.shot('documents-style', { of: '[data-dialog=documents] .doc-side.right', pad: 0 });
      // Save as template… into this project.
      await t.page.locator('[data-field=doc-save-template]').click();
      await t.page.waitForSelector('[data-dialog=save-template]', { timeout: 10_000 });
      await t.page.locator('[data-field=tpl-save-title]').fill('Memoriu CBS');
      await t.page.locator('[data-field=tpl-save-description]').fill('The technical report with the company\'s logo and colours');
      await t.page.locator('[data-field=tpl-save-where]').selectOption('project');
      await t.shot('documents-save-template', { of: '[data-dialog=save-template]', pad: 0 });
      await t.page.locator('[data-field=tpl-save-go]').click();
      await t.page.waitForSelector('[data-dialog=save-template]', { state: 'detached', timeout: 10_000 });
      await t.wait(500);
      t.expect((await t.page.locator('[data-dialog=documents] [data-field^=doc-template-]').allInnerTexts()).some((s) => s.includes('Memoriu CBS')), 'the template is offered under New from template');
      await t.page.locator('[data-dialog=documents] .dialog-head button[title=Close]').click(); await t.wait(300);
      // Manage → Document templates…: the three groups, the project's template with its style.
      await t.tool('manage.documentTemplates');
      await t.page.waitForSelector('[data-dialog=document-templates]', { timeout: 10_000 });
      const own = t.page.locator('[data-dialog=document-templates] [data-origin=project]').first();
      await own.waitFor({ timeout: 10_000 });
      await own.click();
      await t.page.waitForSelector('[data-dialog=document-templates] [data-field=tpl-style-sample] img', { timeout: 10_000 });
      t.expect(await t.page.locator('[data-dialog=document-templates] [data-field=tpl-rename]').count() === 1, 'a project template can be renamed');
      t.expect(await t.page.locator('[data-dialog=document-templates] [data-origin=builtin]').count() === 3, 'the three built-in templates are listed');
      await t.shot('document-templates', { of: '[data-dialog=document-templates]', pad: 0 });
      await t.page.locator('[data-dialog=document-templates] .dialog-head button[title=Close]').click(); await t.wait(300);
    },
  },
  {
    // Remarks on the model as BCF (docs/plan-cde.md, stage 2): a tiny BCF 2.1 file made here (a topic on a wall, a comment, a viewpoint
    // looking at the house) is imported, the list shown, and the remark opened at its viewpoint in the 3D view.
    id: 'remarks',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t);
      const guid = '3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91', vp = '9a9b8c7d-aaaa-4bbb-8ccc-dddd00001111';
      const markup = `<?xml version="1.0" encoding="utf-8"?>
<Markup>
  <Topic Guid="${guid}" TopicType="Issue" TopicStatus="Open">
    <Title>Window too close to the corner</Title>
    <Priority>High</Priority>
    <Labels>facade</Labels>
    <CreationDate>2026-10-30T09:00:00Z</CreationDate>
    <CreationAuthor>reviewer</CreationAuthor>
    <AssignedTo>architect</AssignedTo>
    <Description>Keep at least 600 mm between the window and the corner.</Description>
  </Topic>
  <Comment Guid="7c7d2a10-1111-4222-8333-444455556666"><Date>2026-10-30T09:30:00Z</Date><Author>reviewer</Author><Comment>See the viewpoint.</Comment><Viewpoint Guid="${vp}" /></Comment>
  <Viewpoints Guid="${vp}"><Viewpoint>viewpoint.bcfv</Viewpoint></Viewpoints>
</Markup>`;
      const viewpoint = `<?xml version="1.0" encoding="utf-8"?>
<VisualizationInfo Guid="${vp}">
  <Components><Selection><Component IfcGuid="${walls[0]}" /></Selection><Visibility DefaultVisibility="true" /></Components>
  <PerspectiveCamera>
    <CameraViewPoint><X>20</X><Y>-14</Y><Z>8</Z></CameraViewPoint>
    <CameraDirection><X>-0.62</X><Y>0.72</Y><Z>-0.31</Z></CameraDirection>
    <CameraUpVector><X>-0.2</X><Y>0.23</Y><Z>0.95</Z></CameraUpVector>
    <FieldOfView>45</FieldOfView>
  </PerspectiveCamera>
</VisualizationInfo>`;
      const data = bcfZip([
        ['bcf.version', '<?xml version="1.0" encoding="utf-8"?><Version VersionId="2.1"><DetailedVersion>2.1</DetailedVersion></Version>'],
        [`${guid}/markup.bcf`, markup],
        [`${guid}/viewpoint.bcfv`, viewpoint],
      ]).toString('base64');
      const r = await t.call('project.importBcf', { name: 'review.bcfzip', data });
      t.expect(r.topics === 1 && r.added === 1, 'the BCF file brings one remark');
      await t.wait(400);
      await t.tool('manage.remarks');
      await t.page.waitForSelector('[data-testid=remarks-list] li button', { timeout: 10_000 });
      await t.page.locator('[data-testid=remarks-list] li button').first().click();
      await t.wait(300);
      t.expect((await t.page.locator('[data-testid=remarks-detail] input[aria-label=Title]').inputValue()) === 'Window too close to the corner', 'the remark opens with its title');
      await t.shot('remarks', { of: '.dialog', pad: 0 });
      await t.button('Show in 3D', { exact: true, wait: 1200 });
      t.expect((await t.page.locator('[data-testid=view3d]').count()) === 1, 'the remark opens the 3D view at its viewpoint');
      await t.shot('remark-viewpoint', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Remarks on sheets (docs/plan-cde.md, stage 3): the sheet's PDF as the engine prints it, annotated here the way a
    // reviewer's program saves it (an incremental update carrying the annotations), read back and shown over the sheet.
    id: 'cde-remarks',
    async run(t) {
      await t.fresh();
      await house(t);
      const plan = await t.cmd('view.create', { kind: 'plan', storeyId: await t.storey(), name: 'Ground floor plan', scale: 100 });
      const sheet = await t.cmd('sheet.create', { name: 'Ground floor plan', number: 'A-101', paper: 'A3' });
      await t.cmd('sheet.addViewport', { id: sheet.id, viewId: plan.id, x: 150, y: 120 });
      await t.cmd('cde.setNaming', { project: 'HOUSE', originator: 'CBS' });
      const printed = await t.call('export.pdf', { sheets: [sheet.id] });
      const reviewed = annotatePdf(printed.data, REVIEW_MARKS);
      // The file named by the convention (as an issue would write it), so the sheet is found by its name alone.
      const read = await t.call('cde.readPdf', { data: reviewed, name: 'HOUSE-CBS-ZZ-00-DR-A-0101_S2_P01.pdf' });
      t.expect(read.by === 'name' && read.remarks.length === REVIEW_MARKS.length, 'the PDF is matched to the sheet by its name and every mark becomes a remark');
      await t.cmd('cde.importRemarks', { remarks: read.remarks });
      await t.wait(400);
      await t.tool('manage.sheetRemarks');
      await t.page.waitForSelector('[data-testid=remarks-list] .remark-row', { timeout: 10_000 });
      t.expect(await t.page.locator('[data-testid=remarks-list] .remark-row').count() === REVIEW_MARKS.length, 'the remarks are listed under their sheet');
      await t.shot('cde-remarks', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-testid=remarks-list] .remark-row .link').first().click();
      await t.page.waitForSelector('[data-testid=sheet-remarks] .remark', { timeout: 10_000 });
      await t.wait(600);
      t.expect(await t.page.locator('[data-testid=remark-card]').count() === 1, 'the remark clicked is in front, with its card');
      t.expect(await t.page.locator('[data-testid=sheet-remark-count]').count() === 1, 'the project browser counts the open remarks beside the sheet');
      await t.shot('cde-remarks-sheet');
    },
  },
  {
    id: 'rebar',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const beam = (await t.cmd('structure.addBeam', { storeyId: s, x1: 0, y1: 0, x2: 6, y2: 0 })).id;
      for (const x of [0.3, 5.7]) await t.cmd('structure.addColumn', { storeyId: s, x, y: 0 });
      // The beam type's stirrups: Ø8 in three zones, closer at the ends; drawn in every section as it lies to the axis.
      const beamType = (await t.nodes()).find((n) => n.type === 'IfcBeamType').id;
      await t.cmd('rebar.setTypeStirrups', { id: beamType, enabled: true, mode: 'zones', zones: [{ share: 1 / 3, step: 0.1 }, { share: 1 / 3, step: 0.2 }, { share: 1 / 3, step: 0.1 }] });
      // A longitudinal section in front of the beam, looking at it; its x is the model's x + 1, its y the level.
      const host = (await t.cmd('view.create', { kind: 'section', start: [-1, -1.5], end: [7, -1.5], scale: 50, name: 'Beam B1' })).id;
      await t.ui({ view: 'drawing', drawingViewId: host, selection: null, selected: [], keyPlan: false });
      await t.button('Simple rebar', { exact: true });
      await t.shot('ribbon-rebar', { of: '.topbar', pad: 0 });
      const top = (await t.nodes()).find((n) => n.id === s)?.props.elevation ?? 0;
      const z = (dz) => Number(top) + 3 + dz;
      // Section here: the cross-section at mid-span, inset to the right, with the stirrups and bars drawn on the longitudinal one.
      const r = await t.cmd('rebar.sectionHere', { viewId: host, id: beam, point: [4, z(-0.2)], at: [10.6, z(-0.3)] });
      t.expect(!!r?.insetId, 'the section is inset');
      // The longitudinal bars drawn in the long section, each laid across the beam by the cover: three at the bottom with hooks, two on top; the cross-section shows them as dots.
      await t.cmd('rebar.add', { viewId: host, shape: 'straight', at: [1.05, z(-0.36)], params: { length: 5.9 }, diameter: 0.016, mark: 2, hooks: { start: { angle: 90, length: 0.25 }, end: { angle: 90, length: 0.25 } }, across: { count: 3, mode: 'cover' }, label: { at: [2.2, z(-0.9)] } });
      await t.cmd('rebar.add', { viewId: host, shape: 'straight', at: [1.05, z(-0.04)], params: { length: 5.9 }, diameter: 0.012, mark: 3, across: { count: 2, mode: 'cover' }, label: { at: [6.2, z(0.4)] } });
      await t.wait(1500);
      await t.fit();
      await t.wait(800);
      await t.shot('rebar-section', { of: '[data-testid=drawing]', pad: 0 });
      await t.ui({ keyPlan: true });
      t.expect((await t.nodes()).filter((n) => n.type === 'LapinRebar' && n.props.host).length === 2, 'two bars in the beam');
    },
  },
  {
    // Rebar partitions (decision 483): bars in two partitions, each numbered from 1, the bar schedule listing them apart.
    id: 'rebar-partitions',
    async run(t) {
      const { page } = t;
      await t.fresh();
      const view = (await t.cmd('view.create', { kind: 'section', start: [0, -1], end: [6, -1], scale: 50, name: 'Beam B1' })).id;
      await t.cmd('rebar.add', { viewId: view, shape: 'straight', at: [0.5, 0.4], params: { length: 5 }, diameter: 0.016, partition: 'Beam bars' });
      await t.cmd('rebar.add', { viewId: view, shape: 'straight', at: [0.5, 0.1], params: { length: 5 }, diameter: 0.012, partition: 'Beam bars' });
      await t.cmd('rebar.addRun', { viewId: view, start: [0.5, 0.25], end: [5.5, 0.25], partition: 'Beam stirrups' });
      const schedule = (await t.cmd('architecture.createSchedule', { kind: 'rebar' })).id;
      await t.ui({ view: 'schedule', scheduleId: schedule, keyPlan: false });
      await t.wait(1200);
      const pane = page.locator('.schedule-pane');
      const text = await pane.innerText();
      t.expect(text.includes('Beam stirrups') && text.includes('Beam bars'), 'the schedule lists both partitions');
      t.expect(await page.locator('[data-testid=rebar-schedule-partition]').count() === 1, 'the schedule chooses its partitions');
      await t.shot('rebar-partitions', { of: '.schedule-pane', pad: 0 });
      await page.locator('[data-testid=rebar-schedule-partition]').selectOption('Beam stirrups');
      await t.wait(800);
      t.expect(!(await pane.locator('table').first().innerText()).includes('Beam bars'), 'one partition alone');
      await t.ui({ keyPlan: true });
    },
  },
  {
    // An imported mesh's chair placed like any element (decision 448): six of them round a table with Modify → Array, their
    // triangles shared.
    id: 'reference-array',
    touches: ['studio'],
    async run(t) {
      const { page } = t;
      await t.fresh();
      const storey = await t.storey();
      // A chair as another program would give it: a seat, a back and four legs, its back to the south.
      let obj = 'o chair\n';
      let n = 0;
      const box = (x0, y0, z0, x1, y1, z1) => {
        for (const [x, y, z] of [[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0], [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]]) obj += `v ${x} ${y} ${z}\n`;
        for (const f of [[1, 4, 3, 2], [5, 6, 7, 8], [1, 2, 6, 5], [3, 4, 8, 7], [1, 5, 8, 4], [2, 3, 7, 6]]) obj += `f ${f.map((i) => i + n).join(' ')}\n`;
        n += 8;
      };
      const [cx, cy] = [0, -1.05];
      box(cx - 0.22, cy - 0.22, 0.42, cx + 0.22, cy + 0.22, 0.47);
      box(cx - 0.22, cy - 0.27, 0.42, cx + 0.22, cy - 0.22, 0.9);
      for (const [x, y] of [[-0.2, -0.2], [0.17, -0.2], [-0.2, 0.17], [0.17, 0.17]]) box(cx + x, cy + y, 0, cx + x + 0.03, cy + y + 0.03, 0.42);
      await page.evaluate(async ([obj, storeyId]) => (await import('/src/engine/session.ts')).engine.call('project.importMesh',
        { name: 'chair.obj', data: btoa(obj), up: 'z', oneElement: true, storeyId }), [obj, storey]);
      await t.wait(800);
      await t.cmd('studio.primitive', { shape: 'cylinder', at: [0, 0], radius: 0.6, height: 0.74, storeyId: storey });
      const chairs = async () => (await t.nodes()).filter((x) => x.type === 'IfcBuildingElementProxy' && x.name === 'chair');
      const chair = (await chairs())[0];
      t.expect(chair, 'the chair came in as a reference');
      await t.ui({ view: '3d', selection: chair.id, selected: [chair.id] });
      await page.waitForSelector('[data-testid=view3d] canvas', { timeout: 20_000 });
      await t.wait(1200);

      // Modify → Array round the table's middle: six chairs in a full turn.
      await t.tool('modify.array');
      t.expect(await page.locator('.ask-box').count() === 1, 'Array asks how');
      await page.locator('.ask-box select').first().selectOption('polar');
      const inputs = page.locator('.ask-box input');
      await inputs.nth(0).fill('6');
      await inputs.nth(4).fill('360');
      await page.locator('.ask-box button.primary').click();
      await t.until(async () => (await chairs()).length === 6, 'six chairs round the table');
      await t.ui({ selection: null, selected: [] });
      await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target: { x: 0, y: 0, z: 0.4 }, azimuth: Math.PI / 4, pitch: -0.6, distance: 5 }, false));
      await t.wait(1200);
      await t.shot('reference-array', { of: '[data-testid=view3d]', pad: 0 });

      // One chair pinned from its properties (decision 449): it cannot be deleted until Modify → Unpin lets it go.
      const first = (await chairs())[0];
      await t.ui({ selection: first.id, selected: [first.id] });
      await t.wait(800);
      await page.locator('aside.props input[data-field=pinned]').click();
      await t.until(async () => (await t.nodes()).find((x) => x.id === first.id)?.props.pinned === true, 'the chair is pinned');
      t.expect(await page.locator('aside.props .sect.actions button').first().isDisabled(), 'a pinned chair cannot be deleted');
      await t.tool('modify.unpin');
      await t.until(async () => (await t.nodes()).find((x) => x.id === first.id)?.props.pinned !== true, 'Modify → Unpin lets it go');
    },
  },
  {
    // An array that keeps its arrangement (decision 481): columns along a drafted arc, then their count changed in the Array section.
    id: 'array-path',
    touches: ['structure', 'drafting'],
    async run(t) {
      const { page } = t;
      await t.fresh();
      const storey = await t.storey();
      const plan = 'plan:' + storey;
      // A quarter circle drawn on the plan, and a column where it starts.
      const arc = (await t.cmd('drafting.draw', { viewId: plan, shape: { type: 'arc', c: [0, 0], r: 6, start: 0, end: Math.PI / 2 } })).ids[0];
      const type = await t.cmd('structure.createColumnType', { section: 'HEA 200' });
      const column = (await t.cmd('structure.addColumn', { storeyId: storey, typeId: type.id, x: 6, y: 0 })).id;
      await t.ui({ view: 'plan', keyPlan: false, selection: column, selected: [column] });
      await t.wait(1000);
      const columns = async () => (await t.nodes()).filter((x) => x.type === 'IfcColumn');
      // Modify → Array → Along a path: five columns, then a click on the arc and Enter (the selection stays where it is).
      await t.tool('modify.array');
      t.expect(await page.locator('.ask-box').count() === 1, 'Array asks how');
      await page.locator('.ask-box select').first().selectOption('path');
      await page.locator('.ask-box input').nth(0).fill('5');
      await page.locator('.ask-box button.primary').click();
      await t.wait(600);
      const [px, py] = await t.planPoint(6 * Math.cos(Math.PI / 4), 6 * Math.sin(Math.PI / 4));
      await page.mouse.move(px, py, { steps: 4 });
      await t.wait(600);
      await page.mouse.click(px, py);
      await t.wait(600);
      await page.keyboard.press('Enter');
      await t.until(async () => (await columns()).length === 5, 'five columns along the arc');
      // A copy selected shows the arrangement: change the count and the columns follow.
      const copy = (await columns()).find((x) => x.id !== column);
      await t.ui({ selection: copy.id, selected: [copy.id] });
      await t.wait(800);
      t.expect(await page.locator('[data-testid=array-title]').count() === 1, 'a copy shows its Array section');
      await page.locator('aside.props dl.fields dt:text-is("Copies") + dd input').fill('8');
      await page.keyboard.press('Enter');
      await t.until(async () => (await columns()).length === 8, 'eight columns after the count changed');
      await t.ui({ selection: copy.id, selected: [copy.id] });
      await t.wait(800);
      await t.shot('array-path', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // Studio as a geometry editor (docs/plan-studio-editor.md): push/pull and a fillet in 3D, a pipe's steps, a rising handrail.
    id: 'studio-editor',
    async run(t) {
      const { page } = t;
      await t.fresh();
      const storey = await t.storey();
      const plan = 'plan:' + storey;
      const rect = (x0, y0, x1, y1) => ({ type: 'polyline', pts: [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], closed: true });
      const draw = async (viewId, shape) => (await t.cmd('drafting.draw', { viewId, shape })).ids[0];
      // A 3 × 2 m block, 1 m high, extruded from a rectangle on the plan.
      const block = (await t.cmd('studio.extrude', { profileId: await draw(plan, rect(0, 0, 3, 2)), depth: 1 })).id;
      const solid = async (id) => (await t.nodes()).find((n) => n.id === id);
      const length = (m) => page.evaluate(async (m) => (await import('/src/engine/unitsStore.ts')).formatLength(m), m);
      /** The 3D camera looking at a point from a side (azimuth: the way it looks; pitch below 0 looks down), the point at the view's middle. */
      const look = async (target, azimuth, pitch, distance = 9) => {
        await page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [target, azimuth, pitch, distance]);
        await t.wait(700);
      };
      const middle = async () => { const b = await page.locator('[data-testid=view3d]').boundingBox(); return [b.x + b.width / 2, b.y + b.height / 2]; };
      /** A face tool used where the camera looks: the face lights up under the mouse, a click asks for its value. */
      const pickMiddle = async () => {
        const [x, y] = await middle();
        await page.mouse.move(x - 40, y - 30); await page.mouse.move(x, y, { steps: 6 });
        await t.wait(900);
        await page.mouse.click(x, y);
        await t.wait(600);
      };
      await t.ui({ view: '3d', selection: null, selected: [] });
      await page.waitForSelector('[data-testid=view3d] canvas', { timeout: 20_000 });
      await t.wait(1500);

      // Push/pull: the top picked, an arrow stands on it and the mouse pulls it up, what it adds drawn in green; half a metre typed
      // beside the arrow holds it, Enter applies it, and the extrusion's depth takes it.
      await look({ x: 1.5, y: 1, z: 1 }, Math.PI / 4, -0.55);
      await t.tool('studio.pushPull');
      await pickMiddle();
      t.expect(await page.locator('.axis-drag-value input').count() === 1, 'push/pull puts its arrow and its distance on the face');
      const [mx, my] = await middle();
      await page.mouse.move(mx, my - 60, { steps: 8 });
      await t.wait(300);
      await page.locator('.axis-drag-value input').fill(await length(0.5));
      await t.shot('studio-push-pull', { of: '[data-testid=view3d]', pad: 0 });
      await t.keys('Enter');
      await t.wait(800);
      t.expect(Math.abs(Number((await solid(block)).props.depth) - 1.5) < 1e-6, 'the top pulled 0.5 m makes the extrusion 1.5 m deep');

      // A fillet on the top's east edge, picked on the top near it: a step of the solid.
      await look({ x: 2.92, y: 1, z: 1.5 }, Math.PI * 0.85, -0.6, 6);
      await t.tool('studio.fillet');
      await pickMiddle();
      t.expect(await page.locator('.ask-box').count() === 1, 'a fillet asks for its radius');
      await page.locator('.ask-box input').first().fill(await length(0.3));
      await t.keys('Enter');
      await t.wait(1000);
      t.expect(((await solid(block)).props.features ?? []).length === 1, 'the fillet is a step of the solid');
      await t.keys('Escape');
      await look({ x: 1.5, y: 1, z: 0.7 }, Math.PI * 0.8, -0.45, 8);
      await t.shot('studio-fillet', { of: '[data-testid=view3d]', pad: 0 });

      // Drawing on a face: a rectangle on the block's north side (the camera at the north-west) follows the mouse, its size beside it; pulled 0.3 m, it is a
      // solid of its own standing on the face, sketched on a plane lying on the face that the 3D view does not draw.
      await look({ x: 1.3, y: 2, z: 0.5 }, -Math.PI / 3, -0.3, 7);
      await t.tool('studio.rectOnFace');
      await pickMiddle();
      t.expect(await page.locator('.face-sketch').count() === 1, 'a click on a face begins the rectangle on it');
      {
        const [fx, fy] = await middle();
        await page.mouse.move(fx + 110, fy - 70, { steps: 8 });
        await t.wait(300);
        t.expect(await page.locator('.face-sketch-shape').count() === 1, 'the rectangle follows the mouse on the face');
        await t.shot('studio-on-face', { of: '[data-testid=view3d]', pad: 0 });
        await page.mouse.click(fx + 110, fy - 70);
        await t.wait(500);
        t.expect(await page.locator('.axis-drag-value input').count() === 1, 'the rectangle closed, push/pull stands on it');
        await page.locator('.axis-drag-value input').fill(await length(0.3));
        await t.keys('Enter');
        await t.wait(1000);
        const all = await t.nodes();
        const boss = all.find((n) => n.props.operation === 'extrude' && n.id !== block);
        t.expect(boss && Math.abs(Number(boss.props.depth) - 0.3) < 1e-6, 'the rectangle pulled 0.3 m is a solid on the face');
        t.expect(all.some((n) => n.props.kind === 'plane' && n.props.onFace === block), 'its sketch lies on a plane on the block\'s face');
        await t.keys('Escape');
      }

      // A cylinder placed from the ribbon, hollowed into a pipe: its steps in its properties.
      const pipe = (await t.cmd('studio.primitive', { shape: 'cylinder', at: [6, 1], radius: 0.6, height: 1.2, storeyId: storey })).id;
      await t.cmd('studio.addFeature', { id: pipe, feature: { op: 'shell', thickness: 0.08, open: 'end' } });
      await t.cmd('studio.addFeature', { id: pipe, feature: { op: 'array', count: 3, step: [0, 2, 0] } });
      await t.ui({ view: '3d', selection: pipe, selected: [pipe] });
      await t.wait(1500);
      t.expect(await page.locator('.studio-steps li').count() === 2, 'the pipe lists its two steps');
      await t.shot('studio-steps', { of: 'aside.props', pad: 0 });

      // A handrail that rises round a circle: a sweep whose path climbs 3 m in two turns.
      const tube = await draw(plan, { type: 'circle', c: [20, 0], r: 0.04 });
      const round = await draw(plan, { type: 'circle', c: [10, 6], r: 1.2 });
      await t.cmd('studio.sweep', { profileId: tube, pathId: round, rise: 3, turns: 2, name: 'Handrail' });
      await t.ui({ selection: null, selected: [] });
      await look({ x: 6, y: 3, z: 1.2 }, Math.PI * 0.3, -0.35, 15);
      await t.shot('studio-helix', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Sketching in 3D (docs/plan-studio-sketch.md, decision 374): SketchUp's way (Line on the face under the cursor, the red axis
    // inferred, a typed length; a rectangle typed and pulled at once) and CAD's (Sketch on face, RECTANGLE on it, Finish sketch).
    id: 'studio-sketch-3d',
    async run(t) {
      const { page } = t;
      await t.fresh();
      const storey = await t.storey();
      const plan = 'plan:' + storey;
      const rect = (x0, y0, x1, y1) => ({ type: 'polyline', pts: [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], closed: true });
      const draw = async (viewId, shape) => (await t.cmd('drafting.draw', { viewId, shape })).ids[0];
      const block = (await t.cmd('studio.extrude', { profileId: await draw(plan, rect(0, 0, 3, 2)), depth: 1.2 })).id;
      const length = (m) => page.evaluate(async (m) => (await import('/src/engine/unitsStore.ts')).formatLength(m), m);
      const look = async (target, azimuth, pitch, distance) => {
        await page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [target, azimuth, pitch, distance]);
        await t.wait(700);
      };
      const middle = async () => { const b = await page.locator('[data-testid=view3d]').boundingBox(); return [b.x + b.width / 2, b.y + b.height / 2]; };
      const solids = async () => (await t.nodes()).filter((n) => n.props.operation === 'extrude' && n.id !== block);
      // Typed as SketchUp's Measurements, no box clicked (decision 378): the keys go to the value, never to a quick tool.
      const vcb = async (text) => {
        await page.evaluate(() => document.activeElement?.blur?.());
        await page.keyboard.type(text);
        t.expect(await page.locator('[data-sketch3d=vcb]').inputValue() === text, `typed ${text} goes to the value`);
        await page.keyboard.press('Enter');
        await t.wait(500);
      };
      await t.ui({ view: '3d', selection: null, selected: [] });
      await page.waitForSelector('[data-testid=view3d] canvas', { timeout: 20_000 });
      await t.wait(1500);
      // The block's north side, seen from the north (looking south).
      await look({ x: 1.5, y: 2, z: 0.6 }, -Math.PI / 2, -0.25, 6);
      const [x, y] = await middle();

      // The direct way: Line on the face under the cursor; the first click fixes the face, the cursor then runs along red.
      await t.tool('studio.sketch.line');
      await page.mouse.move(x - 100, y + 30, { steps: 4 });
      await t.wait(900);
      t.expect(await page.locator('[data-sketch3d=overlay] .face-sketch-face').count() === 1, 'the face under the cursor lights up');
      await page.mouse.click(x - 100, y + 30);
      await t.wait(400);
      await page.mouse.move(x + 60, y + 32, { steps: 8 });
      await t.wait(400);
      t.expect(await page.locator('.face-sketch-snap-name.red').count() === 1, 'from the first point the cursor infers the red axis');
      await t.shot('studio-sketch-3d', { of: '[data-testid=view3d]', pad: 0 });
      await vcb(await length(1));
      await page.mouse.move(x + 60, y - 60, { steps: 6 });
      await t.wait(300);
      await vcb(await length(0.4));
      await t.keys('Escape');
      await t.wait(800);
      const open = (await t.nodes()).find((n) => n.type === 'IfcAnnotation' && n.props.shape?.type === 'polyline' && n.props.shape.closed === false);
      t.expect(!!open, 'the line left open stays a sketch on the face');

      // A rectangle typed as its size: closed, the push/pull arrow at once; 0.2 m pulled is a solid on the face.
      await t.tool('studio.sketch.rect');
      await page.mouse.move(x + 20, y + 20, { steps: 4 });
      await t.wait(900);
      await page.mouse.click(x + 20, y + 20);
      await t.wait(300);
      await page.mouse.move(x + 80, y - 20, { steps: 4 });
      await vcb(`${await length(0.6)},${await length(0.3)}`);
      t.expect(await page.locator('.axis-drag-value input').count() === 1, 'the rectangle closed, push/pull stands on it');
      await page.locator('.axis-drag-value input').fill(await length(0.2));
      await t.keys('Enter');
      await t.wait(1000);
      t.expect((await solids()).length === 1, 'the rectangle pulled out is a solid on the face');
      await t.keys('Escape');
      await t.wait(300);

      // The CAD way: Sketch on face makes the top the work plane; RECTANGLE on it, then Finish sketch pushes it in.
      await look({ x: 1.5, y: 1, z: 1.2 }, Math.PI / 4, -0.9, 7);
      const [cx, cy] = await middle();
      await t.tool('studio.sketchOnFace');
      await page.mouse.move(cx, cy, { steps: 4 });
      await t.wait(900);
      await page.mouse.click(cx, cy);
      await t.wait(800);
      t.expect(await page.locator('[data-draft3d=sketch]').count() === 1, 'Sketch on face: the chip says the face sketched on');
      await t.tool('studio.sketch.rect');
      await page.mouse.move(cx - 40, cy - 20, { steps: 4 });
      await page.mouse.click(cx - 40, cy - 20);
      await t.wait(400);
      await page.mouse.move(cx + 40, cy + 20, { steps: 4 });
      await t.wait(400);
      await t.shot('studio-sketch-plane', { of: '[data-testid=view3d]', pad: 0 });
      await page.mouse.click(cx + 40, cy + 20);
      await t.wait(600);
      await t.tool('studio.finishSketch');
      await t.wait(600);
      t.expect(await page.locator('.axis-drag-value input').count() === 1, 'Finish sketch puts the push/pull arrow on the rectangle');
      await page.locator('.axis-drag-value input').fill(await length(-0.1));
      await t.keys('Enter');
      await t.wait(1000);
      t.expect((await t.nodes()).some((n) => n.props.operation === 'extrude' && Array.isArray(n.props.cut) && n.props.cut.includes(block)) || (await solids()).length === 2, 'pushed in, a cut into the block');

      // Stage B (decision 375): a line across the block's west side splits it; the region below it, pulled on its own.
      await t.keys('Escape');
      await t.cmd('studio.onFace', { id: block, point: [0, 1, 0.6], normal: [-1, 0, 0], open: true, outline: [[0, 0, 0.4], [0, 2, 0.4]] });
      await look({ x: 0, y: 1, z: 0.4 }, 0, -0.3, 6);
      const before = (await solids()).length;
      await t.tool('studio.pushPull');
      const [wx, wy] = await middle();
      await page.mouse.move(wx, wy + 40, { steps: 6 });
      await t.wait(900);
      await page.mouse.click(wx, wy + 40);
      await t.wait(600);
      t.expect(await page.locator('.axis-drag-value input').count() === 1, 'push/pull stands on the region below the line');
      await page.mouse.move(wx - 60, wy + 30, { steps: 6 });
      await page.locator('.axis-drag-value input').fill(await length(0.3));
      await t.shot('studio-sketch-region', { of: '[data-testid=view3d]', pad: 0 });
      await t.keys('Enter');
      await t.wait(1000);
      t.expect((await solids()).length === before + 1, 'the region pulled out is a solid of its own');
      await t.keys('Escape');

      // Stage C (decision 376): the tape measure from a point on the east side, 500 typed: a guide point; a guide line along the top's
      // east edge; Follow Me sweeps the east side's top strip along a path drafted on the plan, out and round to the north.
      await look({ x: 3, y: 1, z: 0.6 }, Math.PI, -0.35, 7);
      const [ex, ey] = await middle();
      await t.tool('studio.sketch.tape');
      await page.mouse.move(ex, ey, { steps: 4 });
      await t.wait(900);
      await page.mouse.click(ex, ey);
      await t.wait(300);
      await page.mouse.move(ex + 80, ey, { steps: 6 });
      await vcb(await length(0.5));
      const guides = async () => (await t.nodes()).filter((n) => n.type === 'LapinGuide').length;
      t.expect(await guides() === 1, 'the tape measure typed 500 leaves a guide point');
      await t.keys('Escape');
      await t.cmd('studio.addGuide', { a: [3, 0, 1.0], b: [3, 2, 1.0] });
      await t.cmd('studio.onFace', { id: block, point: [3, 1, 0.6], normal: [1, 0, 0], open: true, outline: [[3, 0, 1.0], [3, 2, 1.0]] });
      const route = await draw(plan, { type: 'polyline', pts: [[3, 1], [4.2, 1], [4.2, 3.5]], closed: false });
      const swept = await t.cmd('studio.followMe', { profile: { id: block, point: [3, 1, 1.1], normal: [1, 0, 0] }, path: { id: route } });
      t.expect(!!swept?.id, 'Follow Me sweeps the strip along the path');
      await t.ui({ selection: null, selected: [] });
      await look({ x: 3.6, y: 1.8, z: 0.8 }, Math.PI * 1.2, -0.5, 9);
      await t.shot('studio-sketch-follow', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Modelling in 3D (docs/plan-3d-modelling.md), stage A (testers' report #104): a small stair beside a floor, moved in the
    // 3D view by its first step's corner onto the floor's corner, both snapped as endpoints; then dragged by its body.
    id: 'model-3d',
    async run(t) {
      const { page } = t;
      await t.fresh();
      const storey = await t.storey();
      await t.cmd('architecture.addSlab', { storeyId: storey, points: [[0, 0], [4, 0], [4, 3], [0, 3]] });
      const stair = (await t.cmd('architecture.addStair', { storeyId: storey, library: 'entrance', x: 5.6, y: 1.4, x2: 6.6, y2: 1.4 })).id;
      const look = async (target, azimuth, pitch, distance) => {
        await page.evaluate(async ([target, azimuth, pitch, distance]) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ target, azimuth, pitch, distance }, false), [target, azimuth, pitch, distance]);
        await t.wait(700);
      };
      const screen = (p) => page.evaluate(async (p) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.screenOf?.(p) ?? null, p);
      const box = () => page.evaluate(async (ids) => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.boxOf?.(ids) ?? null, [stair]);
      await t.ui({ view: '3d', selection: stair, selected: [stair] });
      await page.waitForSelector('[data-testid=view3d] canvas', { timeout: 20_000 });
      await t.wait(1500);
      await look({ x: 4.6, y: 1.2, z: 0.3 }, -Math.PI * 0.62, -0.55, 9);
      const b0 = await box();
      t.expect(!!b0, 'the stair is drawn in 3D');
      // MOVE in the 3D view, typed (M, Space) with nothing selected (decision 399): the stair clicked in 3D, Enter; then the
      // base point on the stair's corner and the floor's corner, both inferred as endpoints.
      await t.ui({ selection: null, selected: [] });
      await page.evaluate(() => document.activeElement?.blur?.());
      await t.keys('m', ' ');
      await t.wait(300);
      const body = await screen({ x: (b0.min.x + b0.max.x) / 2, y: (b0.min.y + b0.max.y) / 2, z: b0.max.z * 0.4 });
      await page.mouse.click(body.x, body.y);
      await t.wait(300);
      await t.keys('Enter');
      await t.wait(500);
      t.expect(await page.locator('[data-move3d=chip]').count() === 1, 'MOVE in 3D is the 3D move');
      // The top step's corner nearest the camera, and the floor's top corner nearest it on the screen.
      const cam = await page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().position);
      const tops = [[b0.min.x, b0.min.y], [b0.max.x, b0.min.y], [b0.max.x, b0.max.y], [b0.min.x, b0.max.y]].map(([x, y]) => ({ x, y, z: b0.max.z }));
      const corner = tops.sort((a, b) => Math.hypot(a.x - cam.x, a.y - cam.y, a.z - cam.z) - Math.hypot(b.x - cam.x, b.y - cam.y, b.z - cam.z))[0];
      const s0 = await screen(corner);
      const floor = [];
      for (const [x, y] of [[0, 0], [4, 0], [4, 3], [0, 3]]) floor.push({ p: { x, y, z: 0 }, s: await screen({ x, y, z: 0 }) });
      const target = floor.sort((a, b) => Math.hypot(a.s.x - s0.x, a.s.y - s0.y) - Math.hypot(b.s.x - s0.x, b.s.y - s0.y))[0];
      await page.mouse.move(s0.x + 3, s0.y - 2, { steps: 4 });
      await t.wait(500);
      t.expect((await page.locator('[data-move3d=inference]').innerText()) === 'Endpoint', 'the stair\'s corner snaps as an endpoint');
      await page.mouse.click(s0.x + 3, s0.y - 2);
      await t.wait(300);
      await page.mouse.move(target.s.x + 3, target.s.y - 2, { steps: 12 });
      await t.wait(600);
      t.expect((await page.locator('[data-move3d=inference]').innerText()) === 'Endpoint', 'the floor\'s corner snaps as an endpoint');
      await t.shot('model-3d-move', { of: '[data-testid=view3d]', pad: 0 });
      await page.mouse.click(target.s.x + 3, target.s.y - 2);
      await t.wait(1200);
      const b1 = await box();
      const dx = target.p.x - corner.x, dy = target.p.y - corner.y;
      t.expect(Math.abs(b1.min.x - b0.min.x - dx) < 0.01 && Math.abs(b1.min.y - b0.min.y - dy) < 0.01, `the stair's corner is on the floor's corner (moved ${(b1.min.x - b0.min.x).toFixed(3)}, ${(b1.min.y - b0.min.y).toFixed(3)}; asked ${dx.toFixed(3)}, ${dy.toFixed(3)})`);
      // Decision 399: snapped from its top corner to the floor's, the stair keeps its level (it went there in plan only).
      t.expect(Math.abs(b1.min.z - b0.min.z) < 0.005, `the stair keeps its level (its foot ${b0.min.z.toFixed(3)} → ${b1.min.z.toFixed(3)})`);

      // Dragged by its body: pressed on the selected stair and held briefly (a plain drag orbits), then moved, it follows; letting go puts it there.
      const mid = await screen({ x: (b1.min.x + b1.max.x) / 2, y: (b1.min.y + b1.max.y) / 2, z: b1.max.z * 0.5 });
      await page.mouse.move(mid.x, mid.y, { steps: 2 });
      await page.mouse.down();
      await t.wait(600); // past the viewport's hold (HOLD_MS 350 in Move3D.tsx)
      const away = await screen({ x: (b1.min.x + b1.max.x) / 2 + 0.6, y: (b1.min.y + b1.max.y) / 2 + 1, z: b1.max.z * 0.5 });
      await page.mouse.move(away.x, away.y, { steps: 12 });
      await t.wait(400);
      t.expect(await page.locator('[data-move3d=chip]').count() === 1, 'dragging the selected stair moves it');
      await page.mouse.up();
      await t.wait(1200);
      const b2 = await box();
      t.expect(Math.hypot(b2.min.x - b1.min.x, b2.min.y - b1.min.y) > 0.3, 'let go, the stair stays where it was dragged');

      // Stage B (report #105): a wall drawn in 3D from the floor's corner, along the green axis, its length typed; a door clicked on its face.
      await t.ui({ selection: null, selected: [] });
      const length = (m) => page.evaluate(async (m) => (await import('/src/engine/unitsStore.ts')).formatLength(m), m);
      await t.tool('architecture.wall');
      t.expect(await page.locator('[data-place3d=chip]').count() === 1, 'the wall tool works in 3D');
      await look({ x: 1.5, y: 1.5, z: 0.5 }, -Math.PI * 0.7, -0.6, 10);
      const c0 = await screen({ x: 0, y: 0, z: 0 });
      await page.mouse.move(c0.x + 3, c0.y - 2, { steps: 6 });
      await t.wait(500);
      t.expect((await page.locator('[data-place3d=inference]').innerText()) === 'Endpoint', 'the wall starts on the floor\'s corner');
      await page.mouse.click(c0.x + 3, c0.y - 2);
      await t.wait(300);
      const c1 = await screen({ x: 0, y: 2.6, z: 0 });
      await page.mouse.move(c1.x, c1.y, { steps: 10 });
      await t.wait(500);
      t.expect((await page.locator('[data-place3d=inference]').innerText()) === 'On green axis', 'from its start the cursor infers the green axis');
      await t.shot('model-3d-wall', { of: '[data-testid=view3d]', pad: 0 });
      await page.evaluate(() => document.activeElement?.blur?.());
      await page.keyboard.type(await length(3));
      await page.keyboard.press('Enter');
      await t.wait(1200);
      await t.keys('Escape', 'Escape');
      const wall = (await t.nodes()).find((n) => n.type === 'IfcWall');
      t.expect(!!wall && Math.hypot(wall.props.start[0], wall.props.start[1]) < 0.01 && Math.abs(wall.props.end[1] - 3) < 0.01 && Math.abs(wall.props.end[0]) < 0.01, `the wall runs 3 m from the corner (${JSON.stringify(wall?.props.start)} → ${JSON.stringify(wall?.props.end)})`);
      await t.tool('architecture.door');
      const d = await screen({ x: 0, y: 1.5, z: 1.4 });
      await page.mouse.move(d.x, d.y, { steps: 4 });
      await page.mouse.click(d.x, d.y);
      await t.wait(1500);
      t.expect((await t.nodes()).some((n) => n.type === 'IfcDoor'), 'a door clicked on the wall\'s face in 3D goes into it');
      await t.keys('Escape');

      // Stage C: the wall's end grip dragged in 3D onto the floor's far corner, snapped; a column dropped from the ribbon onto the
      // first floor's slab goes on the first floor.
      await t.ui({ selection: wall.id, selected: [wall.id] });
      await t.wait(600);
      const g0 = await screen({ x: 0, y: 3, z: 0 }), g1 = await screen({ x: 4, y: 3, z: 0 });
      await page.mouse.move(g0.x, g0.y, { steps: 2 });
      await page.mouse.down();
      await page.mouse.move(g1.x + 4, g1.y + 3, { steps: 14 });
      await t.wait(300);
      // Decision 399: the grip infers as Move does, the snap named at the cursor.
      const named = await page.locator('[data-grip3d=inference]').allInnerTexts();
      t.expect(named[0] === 'Endpoint', `the grip names its snap at the cursor (${named.join(', ') || 'none'})`);
      await page.mouse.up();
      await t.wait(1200);
      const moved = (await t.nodes()).find((n) => n.id === wall.id);
      t.expect(Math.abs(moved.props.end[0] - 4) < 1e-6 && Math.abs(moved.props.end[1] - 3) < 1e-6, `the wall's end grip snaps onto the floor's corner (${JSON.stringify(moved.props.end)})`);
      await t.ui({ selection: null, selected: [] });
      const first = await t.storey('First floor');
      await t.cmd('architecture.addSlab', { storeyId: first, points: [[0, 0], [4, 0], [4, 3], [0, 3]] });
      await t.wait(1000);
      const top = await screen({ x: 2.5, y: 1.2, z: 3 });
      await page.evaluate(([x, y]) => {
        const el = document.querySelector('[data-testid=view3d]');
        const dt = new DataTransfer();
        dt.setData('application/x-lapin-tool', 'structure.column');
        for (const type of ['dragover', 'drop']) el.dispatchEvent(new DragEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, dataTransfer: dt }));
      }, [top.x, top.y]);
      await t.wait(1500);
      const column = (await t.nodes()).find((n) => n.type === 'IfcColumn');
      t.expect(!!column && column.storeyId === first, `a column dropped on the first floor's slab goes on the first floor (${column?.storeyId} vs ${first})`);
      await t.keys('Escape');
    },
  },
  {
    // Tags (report #64): what a tag shows chosen in the command line's bar (here the walls' fire rating, a parameter of the
    // user's own), its elbow leader (decision 331: an oblique line to the knee at 60°, the shoulder under the text) with
    // its grips, and the tag's properties.
    id: 'tags',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      await t.cmd('element.setParam', { ids: [walls[0]], name: 'Fire rating', kind: 'text', value: 'REI 120' });
      await t.fit();
      await t.button('Annotate', { exact: true });
      await t.tool('cad.tag');
      t.expect(await t.page.locator('[data-tag-field]').count() > 0, 'TAG offers what it shows in the command line\'s bar');
      await t.page.locator('[data-tag-field]').selectOption('FireRating');
      await t.wait(300);
      for (const [x, y] of [[5, 0], [5, 2]]) { const [px, py] = await t.planPoint(x, y); await t.page.mouse.move(px, py, { steps: 3 }); await t.page.mouse.click(px, py); await t.wait(500); }
      await t.shot('tag-options', { of: '[data-testid=command-line]', pad: 0 });
      await t.keys('Escape', 'Escape');
      const tag = (await t.nodes()).find((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === 'TAG');
      t.expect(tag && tag.props.field === 'FireRating', `a tag of the fire rating (${tag?.props.field})`);
      await t.cmd('drafting.updateTag', { id: tag.id, at: [6.2, 2.1] });
      await t.wait(800);
      t.expect(await t.page.locator('[data-testid=plan] text', { hasText: 'REI 120' }).count() > 0, 'the tag shows the wall\'s fire rating');
      await select(t, tag.id);
      await t.wait(600);
      t.expect(await t.page.locator('[data-grip=knee]').count() === 1, 'an elbow leader has its knee\'s grip');
      const [cx, cy] = await t.planPoint(4, 1.2);
      await t.shot('tag-leader', { clip: { x: cx - 260, y: cy - 170, width: 520, height: 300 } });
      t.expect(await t.page.locator('[data-tag-fields]').count() > 0, 'a tag\'s properties say what it shows and its leader');
      await t.shot('tag-properties', { of: 'aside.props', pad: 0, maxHeight: 520 });
    },
  },
  {
    // A tag turned and aligned (report #129, decision 429) and what it tags lit (#130): a tag on the east wall aligned to it, one
    // typed to 30°; the aligned tag's rotation grip; the element it tags highlighted while it is selected.
    id: 'tag-rotate',
    async run(t) {
      await t.fresh();
      const { walls, storey } = await house(t, { roof: false });
      await t.fit();
      const plan = `plan:${storey}`;
      const a = (await t.cmd('drafting.addTag', { viewId: plan, element: walls[1], field: 'name', at: [12.8, 3], leaderShape: 'straight' })).id;
      const b = (await t.cmd('drafting.addTag', { viewId: plan, element: walls[2], field: 'name', at: [6, 7.4], leaderShape: 'straight', rotation: 30 })).id;
      await t.cmd('drafting.updateTag', { id: a, align: true });
      await t.ui({ view: 'plan', keyPlan: false, selection: a, selected: [a] });
      await t.wait(900);
      const tag = (await t.nodes()).find((n) => n.id === a);
      t.expect(tag?.props.align === true, 'the tag follows its wall');
      t.expect(await t.page.locator('[data-grip=turn]').count() === 1, 'a selected tag has its turning grip');
      t.expect(await t.page.locator('[data-tag-rotation]').count() === 1 && await t.page.locator('[data-tag-align]').count() === 1, 'its properties have Rotation and Align to element');
      t.expect(await t.page.locator('.plan-item.tag-target, .plan-mark.tag-target').count() > 0, 'the wall it tags is highlighted while its tag is selected');
      const [cx, cy] = await t.planPoint(12, 3.5);
      await t.shot('tag-rotate', { clip: { x: cx - 470, y: cy - 190, width: 560, height: 330 } });
      await select(t, b);
      await t.wait(500);
      t.expect((await t.nodes()).find((n) => n.id === b)?.props.rotation === 30, 'the other is turned 30° by hand');
    },
  },
  {
    // A dimension on a grid axis stays where it measured when the axis's end is dragged along itself (report #126).
    id: 'dimension-grid-line',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const g = await t.cmd('drafting.addAxis', { x1: 0, y1: -2, x2: 0, y2: 10, tag: '1' });
      await t.cmd('drafting.addAxis', { x1: 12, y1: -2, x2: 12, y2: 10, tag: '2' });
      const d = (await t.cmd('drafting.addDimension', { viewId: `plan:${storey}`, a: [0, 8], b: [12, 8], at: [6, 9] })).id;
      const ends = async () => t.page.evaluate(async (id) => {
        const s = (await import('/src/engine/session.ts')).useSession.getState();
        const q = await (await import('/src/engine/session.ts')).query('view.drawing', { id: s.nodes.get(id)?.props.viewId });
        return q.marks.find((m) => m.id === id)?.data?.points;
      }, d);
      const before = await ends();
      await t.cmd('drafting.updateAxis', { gridId: g.gridId, tag: '1', y1: -4 });
      await t.cmd('drafting.updateAxis', { gridId: g.gridId, tag: '1', y2: 12 });
      await t.wait(800);
      const after = await ends();
      t.expect(JSON.stringify(before) === JSON.stringify(after), `the dimension stays where it measured (${JSON.stringify(before)} → ${JSON.stringify(after)})`);
      await t.ui({ view: 'plan', keyPlan: false, selection: null, selected: [] });
      await t.page.evaluate(async () => (await import('/src/viewport/zoom.ts')).zoomHost()?.zoomWindow({ x: -2, y: -5 }, { x: 14, y: 13 }));
      await t.wait(900);
      await t.shot('dimension-grid-line', { of: '[data-testid=plan]', pad: 0 });
    },
  },
  {
    // A curtain wall by its boundary (report #127): a gable drawn in the profile dialog, the grid, the panels and the members
    // following it, in 3D.
    id: 'curtain-boundary',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const cw = (await t.cmd('architecture.addCurtainWall', { storeyId: storey, x1: 0, y1: -4, x2: 7, y2: -4, height: 3 })).id;
      await t.ui({ view: 'plan', keyPlan: false, selection: cw, selected: [cw] });
      await t.wait(800);
      await t.page.locator('[data-action=curtain-profile]').first().click();
      await t.page.waitForSelector('[data-testid=wall-profile]', { timeout: 10000 });
      await t.page.locator('[data-field=profile-peak]').fill('4800');
      await t.page.locator('[data-action=profile-gable]').click();
      await t.wait(600);
      await t.shot('curtain-boundary-editor', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-action=profile-apply]').click();
      await t.wait(1400);
      const wall = (await t.nodes()).find((n) => n.id === cw);
      t.expect(Array.isArray(wall.props.topProfile) && wall.props.topProfile.length === 3, 'the curtain wall keeps its gable');
      t.expect(Math.abs(Number(wall.props.height) - 4.8) < 1e-6, 'its height is its highest point');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -0.7, pitch: -0.2, distance: 16, target: { x: 3.5, y: -4, z: 2 } }, false));
      await t.wait(1500);
      await t.shot('curtain-boundary-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Custom profiles for chosen curtain wall members (report #128): a section drawn in the editor, kept, given to two mullions of
    // a wall, turned and aligned; the members dialog and the result in 3D.
    id: 'curtain-members',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const cw = (await t.cmd('architecture.addCurtainWall', { storeyId: storey, x1: 0, y1: -4, x2: 7, y2: -4, height: 3 })).id;
      // The editor makes the profile: a hollow 50 × 100 box.
      const profile = await t.page.evaluate(async () => (await import('/src/engine/session.ts')).query('profiles.fromRings', {
        name: 'Alu 50x100', material: 'aluminium', unit: 0.001,
        rings: [[[0, 0], [50, 0], [50, 100], [0, 100]], [[3, 3], [3, 97], [47, 97], [47, 3]]],
      }));
      t.expect(profile?.kind === 'outline', 'the editor\'s rings make an outline profile');
      const used = await t.cmd('profiles.use', { profile, library: 'My profiles' });
      await t.cmd('architecture.setCurtainMembers', { id: cw, keys: ['mullion:1', 'mullion:2'], profile: used.section, place: { rotate: 90, across: 'front' } });
      await t.ui({ view: 'plan', keyPlan: false, selection: cw, selected: [cw] });
      await t.wait(800);
      await t.page.locator('[data-action=curtain-members]').first().click();
      await t.page.waitForSelector('[data-testid=curtain-members]', { timeout: 10000 });
      await t.page.locator('[data-member="mullion:1"]').click();
      await t.wait(500);
      t.expect(await t.page.locator('[data-placed-section]').count() === 1, 'the section is drawn as it sits on its member');
      await t.shot('curtain-members-dialog', { of: '.dialog', pad: 0 });
      await t.page.keyboard.press('Escape');
      await t.ui({ dialog: null });
      const wall = (await t.nodes()).find((n) => n.id === cw);
      t.expect(wall.props.memberStyles && Object.keys(wall.props.memberStyles).length === 2, 'two members have a style of their own');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -0.7, pitch: -0.2, distance: 8, target: { x: 3.5, y: -4, z: 1.5 } }, false));
      await t.wait(1500);
      await t.shot('curtain-members-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // Curtain panel types and curtain wall doors in chosen cells (report #133): a framed spandrel given to the upper row, a door
    // smaller than its cell placed in the bottom row with its transom and mullions round it; the dialog and the result in 3D.
    id: 'curtain-cells',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const typeId = (await t.cmd('architecture.createCurtainWallType', { name: 'Shopfront', spec: { u: { mode: 'max', spacing: 1.5 }, v: { mode: 'offsets', offsets: [2.6] } } })).id;
      const cw = (await t.cmd('architecture.addCurtainWall', { storeyId: storey, x1: 0, y1: -4, x2: 7, y2: -4, height: 3, typeId })).id;
      await t.cmd('architecture.createCurtainPanelType', { name: 'Framed spandrel', panel: { kind: 'framed', thickness: 0.03, offset: 0.02, frameWidth: 0.06, frameDepth: 0.09, inset: 0.015, infill: 'solid' } });
      await t.ui({ view: 'plan', keyPlan: false, selection: cw, selected: [cw] });
      await t.wait(800);
      await t.page.locator('[data-action=curtain-cells]').first().click();
      await t.page.waitForSelector('[data-testid=curtain-cells]', { timeout: 10000 });
      const d = t.page.locator('[data-testid=curtain-cells]');
      // The upper row: the spandrel.
      await d.locator('[data-cell="panel:0:1"]').click();
      for (const k of ['panel:1:1', 'panel:3:1', 'panel:4:1']) await d.locator(`[data-cell="${k}"]`).click({ modifiers: ['Shift'] });
      const spandrel = await d.locator('select[data-field=cell-panel-type] option').nth(1).getAttribute('value');
      await d.locator('select[data-field=cell-panel-type]').selectOption(spandrel);
      await d.locator('[data-action=cells-panel-apply]').click();
      await t.wait(800);
      // The middle bottom cell: a glazed door of its own size, centred, with glass round it.
      await d.locator('[data-cell="panel:2:0"]').click();
      await d.locator('input[data-field=door-fit]').uncheck();
      await t.wait(300);
      await d.locator('[data-action=cells-door-place]').click();
      await t.wait(1200);
      t.expect(await d.locator('[data-door="panel:2:0"]').count() === 1, 'the door is drawn in its cell');
      await t.shot('curtain-cells-dialog', { of: '.dialog', pad: 0 });
      await t.page.keyboard.press('Escape');
      await t.ui({ dialog: null });
      const nodes = await t.nodes();
      const wall = nodes.find((n) => n.id === cw);
      t.expect(wall.props.panelTypes && Object.keys(wall.props.panelTypes).length === 4, 'four cells have the spandrel');
      const door = nodes.find((n) => n.type === 'IfcDoor' && n.props.cell === 'panel:2:0');
      t.expect(door != null, 'the door is a door, in its cell');
      await t.ui({ selection: null, selected: [] });
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -0.6, pitch: -0.15, distance: 11, target: { x: 3.5, y: -4, z: 1.5 } }, false));
      await t.wait(1500);
      await t.shot('curtain-cells-3d', { of: '[data-testid=view3d]', pad: 0 });
    },
  },
  {
    // A curtain wall door under a sloped boundary (report #133, the user's answers): its mullions rising to the boundary, a transom
    // over its head and the glass over it following the slope, framed panels framed along their trimmed outline; and the door's
    // opening symbol in the south elevation.
    id: 'curtain-cells-boundary',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      const typeId = (await t.cmd('architecture.createCurtainWallType', { name: 'Gable front', spec: { u: { mode: 'max', spacing: 1.5 }, v: { mode: 'count', count: 1 } } })).id;
      const cw = (await t.cmd('architecture.addCurtainWall', { storeyId: storey, x1: 0, y1: -4, x2: 6, y2: -4, height: 3, typeId })).id;
      await t.cmd('architecture.setCurtainWallProfile', { ids: [cw], top: [[0, 3.6], [3, 4.4], [6, 3.6]] });
      const framed = (await t.cmd('architecture.createCurtainPanelType', { name: 'Framed glass', panel: { kind: 'framed', thickness: 0.024, frameWidth: 0.06, frameDepth: 0.08, inset: 0.02, infill: 'glass' } })).id;
      await t.cmd('architecture.setCurtainPanels', { id: cw, keys: ['panel:0:top', 'panel:2:top', 'panel:3:top'], typeId: framed });
      await t.ui({ view: 'plan', keyPlan: false, selection: cw, selected: [cw] });
      await t.wait(800);
      await t.page.locator('[data-action=curtain-cells]').first().click();
      await t.page.waitForSelector('[data-testid=curtain-cells]', { timeout: 10000 });
      const d = t.page.locator('[data-testid=curtain-cells]');
      await d.locator('[data-cell="panel:1:top"]').click();
      await d.locator('input[data-field=door-fit]').uncheck();
      await t.wait(300);
      await d.locator('[data-action=cells-door-place]').click();
      await t.wait(1200);
      t.expect(await d.locator('[data-door="panel:1:top"]').count() === 1, 'the door stands in the trimmed cell');
      await t.page.keyboard.press('Escape');
      await t.ui({ dialog: null, selection: null, selected: [] });
      const parts = await t.page.evaluate(async (id) => (await import('/src/engine/session.ts')).query('architecture.curtainCells', { id }), cw);
      t.expect(parts.pieces.some((p) => p.key === 'mullion:1:top:left' && p.outline), 'its mullion rises to the boundary');
      await open3d(t);
      await t.page.evaluate(async () => (await import('/src/viewport/camera.ts')).useCamera.getState().api?.orbitTo({ azimuth: -0.5, pitch: -0.12, distance: 10, target: { x: 3, y: -4, z: 2 } }, false));
      await t.wait(1500);
      await t.shot('curtain-cells-gable', { of: '[data-testid=view3d]', pad: 0 });
      await t.cmd('view.create', { kind: 'elevation', direction: 'south', name: 'South' });
      await openFromBrowser(t, 'South', 2500);
      await t.shot('curtain-cells-elevation', { of: '[data-testid=drawing]', pad: 0 });
    },
  },
  {
    // The section editor (report #128): Manage → Profile libraries → Draw a profile…, a hollow box with a chamber added.
    id: 'profile-editor',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.ui({ dialog: 'profileLibraries' });
      await t.page.waitForSelector('[data-dialog=profile-libraries]', { timeout: 10000 });
      await t.page.locator('[data-testid=profile-draw-new]').click();
      await t.page.waitForSelector('[data-testid=profile-editor]', { timeout: 10000 });
      await t.page.locator('[data-action=editor-T]').click();
      await t.page.locator('[data-action=editor-box]').click();
      await t.wait(500);
      t.expect(await t.page.locator('[data-editor-point]').count() === 8, 'a hollow box has its two rings\' eight points');
      await t.shot('profile-editor', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-testid=profile-editor-make]').click();
      await t.page.waitForSelector('[data-testid=profile-outline]', { timeout: 10000 });
      t.expect(await t.page.locator('[data-testid=profile-outline-keep]').count() === 1, 'the profile made goes on to be kept in a library');
    },
  },
  {
    // An opening's height under a dimension line (report #92, decision 338): a string along the south wall, its segments
    // across the door and the window writing the width over the line and the height under it, read from the model; then
    // Drafting standards → Dimensions with its Opening height column.
    id: 'dimension-opening-height',
    async run(t) {
      await t.fresh();
      const { storey } = await house(t, { roof: false });
      // The door 0.90 × 2.10 at 3 m, the window 1.20 × 1.50 (sill 0.90) at 9.5 m: the string through their jambs on the wall's axis.
      await t.cmd('drafting.addDimension', { viewId: `plan:${storey}`, pts: [[0, 0], [2.55, 0], [3.45, 0], [8.9, 0], [10.1, 0], [12, 0]], at: [6, -1.4], dir: [1, 0] });
      await t.ui({ selection: null, selected: [], keyPlan: false });
      await t.page.evaluate(async () => (await import('/src/viewport/zoom.ts')).zoomHost()?.zoomWindow({ x: -0.5, y: -2.4 }, { x: 12.5, y: 1.2 }));
      await t.wait(800);
      const plan = t.page.locator('[data-testid=plan]');
      t.expect(await plan.locator('text', { hasText: /^1500$/ }).count() === 1, 'the window\'s height under its segment');
      t.expect(await plan.locator('text', { hasText: /^2100$/ }).count() === 1, 'the door\'s height under its segment');
      const [x0, y0] = await t.planPoint(-0.5, 1.0);
      const [x1, y1] = await t.planPoint(12.5, -2.3);
      await t.shot('dimension-opening-height', { clip: { x: x0, y: y0, width: x1 - x0, height: y1 - y0 } });
      // The rule in the dimension style.
      await t.tool('manage.standards');
      const dialog = t.page.locator('.dialog');
      await dialog.getByText('Dimensions', { exact: true }).first().click();
      await t.wait(400);
      t.expect(await dialog.locator('th', { hasText: 'Opening height' }).count() === 1, 'the dimension styles have an Opening height column');
      t.expect(await dialog.locator('select').filter({ has: t.page.locator('option[value=hp]') }).first().inputValue() === 'number', 'a new project writes the height alone');
      await t.shot('dimension-opening-height-style', { of: '.standards-table', pad: 6 });
      await t.keys('Escape');
    },
  },
  {
    // Draw order (report #44, decision 339): a text over a grey fill, the fill selected and right-clicked, Bring to front
    // from the menu: the fill now covers the text, in the view and on paper.
    id: 'draw-order',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      const plan = `plan:${s}`;
      await t.cmd('drafting.draw', { viewId: plan, shapes: [{ type: 'polyline', pts: [[0, 0], [4, 0], [4, 2], [0, 2]], closed: true }] });
      const fill = await t.cmd('drafting.hatch', { viewId: plan, at: [1, 1], pattern: 'SOLID', color: '#c9c9c9' });
      // A note half over the fill: drawn after it, it lies on top.
      await t.cmd('drafting.addText', { viewId: plan, at: [2.2, 0.9], text: 'A note across the fill' });
      await t.ui({ keyPlan: false });
      await t.page.evaluate(async () => (await import('/src/viewport/zoom.ts')).zoomHost()?.zoomWindow({ x: -1, y: -1.5 }, { x: 9, y: 3.5 }));
      await t.wait(800);
      await select(t, fill.id);
      await t.wait(500);
      const [mx, my] = await t.planPoint(4.6, 2.6);
      await t.page.mouse.click(mx, my, { button: 'right' });
      await t.wait(400);
      const menu = t.page.locator('.context-menu');
      for (const label of ['Bring to front', 'Bring forward', 'Send backward', 'Send to back', 'Send behind the model'])
        t.expect(await menu.getByRole('menuitem', { name: label }).count() === 1, `the menu has ${label}`);
      const box = await menu.boundingBox();
      const [x0, y0] = await t.planPoint(-0.6, 2.9);
      const [, y1] = await t.planPoint(0, -0.6);
      await t.shot('draw-order-menu', { clip: { x: x0, y: Math.min(y0, box.y - 10), width: box.x + box.width + 110 - x0, height: Math.max(y1, box.y + box.height + 10) - Math.min(y0, box.y - 10) } });
      await menu.getByRole('menuitem', { name: 'Bring to front' }).click();
      await t.wait(800);
      const nodes = await t.nodes();
      const text = nodes.find((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === 'TEXT');
      const order = (id) => Number(nodes.find((n) => n.id === id)?.props.drawOrder ?? 0);
      t.expect(order(fill.id) > order(text.id), `the fill is drawn over the text (${order(fill.id)} over ${order(text.id)})`);
      await t.ui({ selection: null, selected: [] });
      await t.wait(400);
      const [x2, y2] = await t.planPoint(8.2, -0.6);
      await t.shot('draw-order-front', { clip: { x: x0, y: y0, width: x2 - x0, height: y2 - y0 } });
    },
  },
  {
    // Labels edited part by part (decision 292): a door's marker selected shows a grip for each text (move, turn) and each
    // end of its line; the mark dragged and turned, the line drawn out by their grips; the texts along the tag line as the
    // tag type says; an opening void's label placed clear of the door's swing, then moved by its grip.
    id: 'label-edit',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
      await t.ui({ keyPlan: false });
      await t.fit();
      await t.tool('tagOpenings');
      await t.wait(1000);
      const nodes = await t.nodes();
      const door = nodes.find((n) => n.type === 'IfcDoor');
      const tagOf = async (id) => (await t.nodes()).find((n) => n.type === 'IfcAnnotation' && n.props.predefinedType === 'TAG' && n.props.element === id);
      const tag = await tagOf(door.id);
      t.expect(!!tag, 'Door & window tags gives the door its marker');
      // Zoomed in about the door, the quick tools out of the way.
      const quiet = await t.page.addStyleTag({ content: '.hotbar { display: none !important }' });
      await t.page.evaluate(async () => (await import('/src/viewport/zoom.ts')).zoomHost()?.zoomWindow({ x: 0.5, y: -2.2 }, { x: 5.5, y: 2.2 }));
      await t.wait(800);
      await select(t, tag.id);
      await t.wait(700);
      for (const g of ['o0', 'o0:turn', 'i0', 'a', 'b'])
        t.expect(await t.page.locator(`[data-grip="${g}"]`).count() === 1, `the selected marker has its grip ${g}`);
      // The pictures about the door: the middle of its marker's line, from its two end grips (the zoom stays as it is).
      const ends = await Promise.all(['a', 'b'].map((k) => t.page.locator(`[data-grip="${k}"]`).boundingBox()));
      const [cx, cy] = [(ends[0].x + ends[1].x + ends[0].width) / 2, (ends[0].y + ends[1].y + ends[0].height) / 2];
      const clip = async () => ({ x: cx - 260, y: cy - 220, width: 660, height: 440 });
      await t.shot('marker-grips', { clip: await clip() });
      // Each drag one step: the mark moved along the wall, turned, the line's outer end drawn out.
      const drag = async (key, dx, dy) => {
        const b = await t.page.locator(`[data-grip="${key}"]`).boundingBox();
        const [x, y] = [b.x + b.width / 2, b.y + b.height / 2];
        await t.page.mouse.move(x, y);
        await t.page.mouse.down();
        await t.page.mouse.move(x + dx / 2, y + dy / 2, { steps: 4 });
        await t.page.mouse.move(x + dx, y + dy, { steps: 4 });
        await t.page.mouse.up();
        await t.wait(900);
      };
      await drag('o0', 45, 0);
      await drag('b', 0, 30);
      await drag('o0:turn', 0, -40);
      const edited = (await tagOf(door.id)).props.parts ?? {};
      t.expect(!!edited.o0?.d && edited.o0?.rot != null && edited.b?.ext > 0, `the mark moved and turned, the line longer (${JSON.stringify(edited)})`);
      await t.shot('marker-edited', { clip: await clip() });
      t.expect(await t.page.locator('[data-marker-reset-all]').count() === 1, 'the marker\'s properties list its parts');
      await t.shot('marker-properties', { of: 'aside.props', pad: 0, maxHeight: 520 });
      // Back as by default, then the texts along the tag line for every door marker (Drafting standards → Tags).
      await t.page.locator('[data-marker-reset-all]').click();
      await t.wait(800);
      t.expect(!(await tagOf(door.id)).props.parts, 'Put the label back takes every edit off');
      await t.cmd('standards.set', { kind: 'tag', entry: { id: 'door', textOrientation: 'along' } });
      await t.ui({ selection: null, selected: [] });
      await t.wait(900);
      await t.shot('marker-along', { clip: await clip() });
      await t.cmd('standards.set', { kind: 'tag', entry: { id: 'door', textOrientation: 'across' } });
      // The voids: the door's label stands beside the wall clear of the swing and the marker; dragged by its grip, it stays there.
      await t.tool('view.openingVoids');
      await t.wait(1500);
      await t.shot('void-label', { clip: await clip() });
      const [dx, dy] = [cx, cy];
      const labels = t.page.locator('[data-testid=plan] text').filter({ hasText: /^V\d+$/ });
      let nearest = null, best = Infinity;
      for (let i = 0; i < await labels.count(); i++) {
        const b = await labels.nth(i).boundingBox();
        const d = b ? Math.hypot(b.x + b.width / 2 - dx, b.y + b.height / 2 - dy) : Infinity;
        if (d < best) { best = d; nearest = labels.nth(i); }
      }
      { const b = await nearest.boundingBox(); await t.page.mouse.click(b.x + b.width / 2, b.y + b.height / 2); }
      await t.wait(700);
      const grip = t.page.locator('[data-grip^="void:"][data-grip$=":label"]');
      t.expect(await grip.count() === 1, 'a click on the void selects its opening, whose label has its grips');
      await drag(await grip.getAttribute('data-grip'), -40, -25);
      const plan = (await t.nodes()).find((n) => n.props.voidLabels);
      t.expect(!!plan, 'the moved label is kept on the view');
      await t.shot('void-label-moved', { clip: await clip() });
      await t.tool('view.openingVoids');
      await quiet.evaluate((el) => el.remove());
      await t.ui({ keyPlan: true, selection: null, selected: [] });
    },
  },
  {
    // The axis grid (report #68): Annotate → Axis grid… lays a grid from its spacings; a selected axis shows its ends, length,
    // angle and spacing in the project's units, here in feet and inches after the units are changed.
    id: 'grid',
    async run(t) {
      await t.fresh();
      await t.button('Annotate', { exact: true });
      await t.tool('drafting.grid');
      t.expect(await t.page.locator('[data-grid-dialog-body]').count() > 0, 'the Axis grid dialog opens');
      await t.page.locator('[data-grid-across]').fill('6000 6000 4500');
      await t.page.locator('[data-grid-along]').fill('2*5000');
      await t.wait(300);
      await t.shot('grid-dialog', { of: '.dialog', pad: 0 });
      await t.page.locator('[data-grid-make]').click();
      await t.wait(1000);
      const grid = (await t.nodes()).find((n) => n.type === 'IfcGrid');
      t.expect(grid && grid.props.axes.length === 7, `a grid of 4 + 3 axes (${grid?.props.axes?.length})`);
      await t.fit();
      await t.ui({ selection: `${grid.id}#2`, selected: [`${grid.id}#2`] });
      await t.wait(600);
      t.expect(await t.page.locator('[data-field=axis-spacing]').count() > 0, 'a selected axis shows its spacing');
      await t.shot('axis-properties', { of: 'aside.props', pad: 0, maxHeight: 520 });
      // Feet and inches: the axis's spacing and the levels follow.
      await t.cmd('standards.options', { units: 'ft-in' });
      await t.wait(800);
      const spacing = await t.page.locator('[data-field=axis-spacing]').first().inputValue();
      t.expect(/'/.test(spacing), `the spacing in feet and inches (${spacing})`);
      await t.shot('axis-properties-imperial', { of: 'aside.props', pad: 0, maxHeight: 520 });
      await t.cmd('standards.options', { units: 'mm' });
    },
  },
  {
    // The ribbon's buttons that open a dialog really open it, clicked as a user does (the band is wider than the window, so the button
    // is brought into view first): Site → Entourage kinds…, Interior → Furniture types and My furniture…, at two window sizes; and the
    // Furniture types dialog's headers never run over one another, in a long language too.
    id: 'ribbon-dialogs',
    async run(t) {
      await t.fresh();
      await t.cmd('furniture.createType', { kind: 'casework', preset: 'sideboard', name: 'Oak sideboard' });
      const opens = async (tab, label, dialog) => {
        await t.page.locator(`[data-tab=${tab}]`).click();
        await t.wait(300);
        const b = t.page.locator('.band .tool', { hasText: label }).first();
        await b.scrollIntoViewIfNeeded();
        await t.wait(200);
        const box = await b.boundingBox();
        const vw = t.page.viewportSize().width;
        t.expect(box && box.x >= 0 && box.x + box.width <= vw, `${label} is on the screen once brought into view (${JSON.stringify(box)})`);
        await t.page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
        await t.page.waitForSelector(dialog, { timeout: 10_000 });
        t.expect(true, `${label} opened its dialog`);
      };
      const close = async () => { await t.ui({ dialog: null }); await t.wait(300); };
      for (const [w, h] of [[1536, 864], [1920, 1080]]) {
        await t.page.setViewportSize({ width: w, height: h });
        await t.wait(300);
        await opens('site', /Entourage kinds/, '[data-testid=entourage-kinds]');
        await close();
        await opens('interior', /My furniture/, '[data-dialog=furniture-library]');
        await close();
        await opens('interior', /Furniture types/, '[data-dialog=furniture-types]');
        await t.page.locator('.dialog [data-furniture-type]').filter({ hasText: 'Oak sideboard' }).first().click();
        await t.wait(800);
        for (const lang of ['en', 'de', 'pl', 'ro', 'fr']) {
          for (const width of [w, 1100]) {
            await t.page.setViewportSize({ width, height: h });
            await t.page.evaluate(async (l) => (await import('/src/i18n/index.ts')).setLang(l), lang);
            await t.wait(300);
            const bad = await t.page.evaluate(() => {
              const dlg = document.querySelector('.dialog[data-dialog=furniture-types]');
              const out = [];
              for (const head of dlg.querySelectorAll('.furniture-new, .furniture-edit-head')) {
                const kids = [...head.children].filter((e) => e.getBoundingClientRect().width > 0);
                const hr = head.parentElement.getBoundingClientRect();
                for (const k of kids) { const r = k.getBoundingClientRect(); if (r.right > hr.right + 1) out.push(`${head.className}: ${k.textContent.trim().slice(0, 20)} runs past its column`); }
                for (let i = 0; i < kids.length; i++) for (let j = i + 1; j < kids.length; j++) {
                  const a = kids[i].getBoundingClientRect(), b = kids[j].getBoundingClientRect();
                  if (a.left < b.right - 1 && b.left < a.right - 1 && a.top < b.bottom - 1 && b.top < a.bottom - 1) out.push(`${head.className}: "${kids[i].textContent.trim().slice(0, 20)}" overlaps "${kids[j].textContent.trim().slice(0, 20)}"`);
                }
              }
              return out;
            });
            t.expect(bad.length === 0, `the Furniture types headers in ${lang} at ${width}: ${bad.join('; ')}`);
          }
        }
        await t.page.setViewportSize({ width: w, height: h });
        await t.page.evaluate(async () => (await import('/src/i18n/index.ts')).setLang('en'));
        await close();
      }
    },
  },
];

/** A zip of stored entries (name, text), enough for a BCF file: the local headers, the central directory and its end, with CRC-32. */
export function bcfZip(entries) {
  const crcTable = new Uint32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
  const crc32 = (buf) => { let c = 0xffffffff; for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const u16 = (v) => { const b = Buffer.alloc(2); b.writeUInt16LE(v); return b; };
  const u32 = (v) => { const b = Buffer.alloc(4); b.writeUInt32LE(v >>> 0); return b; };
  const locals = [], centrals = [];
  let offset = 0;
  for (const [name, text] of entries) {
    const nameBytes = Buffer.from(name, 'utf8'), data = Buffer.from(text, 'utf8'), crc = crc32(data);
    const local = Buffer.concat([u32(0x04034b50), u16(20), u16(0), u16(0), u16(0), u16(0x21), u32(crc), u32(data.length), u32(data.length), u16(nameBytes.length), u16(0), nameBytes, data]);
    centrals.push(Buffer.concat([u32(0x02014b50), u16(20), u16(20), u16(0), u16(0), u16(0), u16(0x21), u32(crc), u32(data.length), u32(data.length), u16(nameBytes.length), u16(0), u16(0), u16(0), u16(0), u32(0), u32(offset), nameBytes]));
    locals.push(local);
    offset += local.length;
  }
  const directory = Buffer.concat(centrals);
  const end = Buffer.concat([u32(0x06054b50), u16(0), u16(0), u16(entries.length), u16(entries.length), u32(directory.length), u32(offset), u16(0)]);
  return Buffer.concat([...locals, directory, end]);
}

/** The marks a reviewer leaves on the A3 sheet of the remarks scene: PDF annotation dictionaries, in points, y up. */
const PT = 72 / 25.4;
const ptX = (mm) => (mm * PT).toFixed(3);
const ptY = (mm) => ((297 - mm) * PT).toFixed(3);
const REVIEW_MARKS = [
  `<< /Type /Annot /Subtype /Square /Rect [${ptX(150)} ${ptY(170)} ${ptX(215)} ${ptY(120)}] /Contents (Check the kitchen door swing) /T (Ana Pop) /M (D:20261030101500+02'00') /C [0.85 0.2 0.1] /NM (review-1) >>`,
  `<< /Type /Annot /Subtype /Text /Rect [${ptX(240)} ${ptY(66)} ${ptX(246)} ${ptY(60)}] /Contents (Add the section mark here) /T (Ana Pop) /M (D:20261030102000+02'00') /NM (review-2) >>`,
  `<< /Type /Annot /Subtype /Ink /Rect [${ptX(60)} ${ptY(230)} ${ptX(120)} ${ptY(190)}] /InkList [[${ptX(60)} ${ptY(190)} ${ptX(80)} ${ptY(215)} ${ptX(100)} ${ptY(200)} ${ptX(120)} ${ptY(230)}]] /Contents (Move the window 300 mm) /T (Mihai) /M (D:20261030104500+02'00') /NM (review-3) >>`,
  `<< /Type /Annot /Subtype /FreeText /Rect [${ptX(260)} ${ptY(210)} ${ptX(360)} ${ptY(196)}] /Contents (12.40 m) /T (Mihai) /Subj (Length Measurement) /M (D:20261030105000+02'00') /NM (review-4) >>`,
];

/**
 * A reviewer's annotations appended to a PDF (base64 in, base64 out) as an incremental update, the way Acrobat saves
 * them: the first page written again with its /Annots, the annotations as new objects, a new xref section with /Prev.
 */
function annotatePdf(base64, annots) {
  const text = Buffer.from(base64, 'base64').toString('latin1');
  const prev = Number(text.slice(text.lastIndexOf('startxref') + 9).trim().split('\n')[0]);
  const size = Number(/\/Size (\d+)/.exec(text)[1]);
  const page = /(\d+) 0 obj\n(<< \/Type \/Page .*?>>)\nendobj/s.exec(text);
  const first = size;
  const objects = [
    [Number(page[1]), `${page[2].slice(0, -2)} /Annots [${annots.map((_, i) => `${first + i} 0 R`).join(' ')}] >>`],
    ...annots.map((a, i) => [first + i, a]),
  ];
  let out = text.endsWith('\n') ? text : `${text}\n`;
  const offsets = [];
  for (const [num, body] of objects) {
    offsets.push([num, Buffer.byteLength(out, 'latin1')]);
    out += `${num} 0 obj\n${body}\nendobj\n`;
  }
  const xref = Buffer.byteLength(out, 'latin1');
  out += 'xref\n';
  for (const [num, off] of offsets.sort((a, b) => a[0] - b[0])) out += `${num} 1\n${String(off).padStart(10, '0')} 00000 n \n`;
  out += `trailer\n<< /Size ${first + annots.length} /Root 1 0 R /Prev ${prev} >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(out, 'latin1').toString('base64');
}
