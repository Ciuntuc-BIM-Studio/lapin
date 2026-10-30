// The user guide's scenes (run.mjs): each drives Lapin as a user does, checks
// what it sees, and takes the pictures its page shows (shots/<name>.webp).
// A scene starts from the start screen; `house` gives it a small model to
// work on, built through the same commands the interface sends.
// A scene drives the interface in English; what a user types (a question)
// is in the run's language (t.lang, one of languages.mjs), from the tables
// below: a language missing from a table types the English.
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
  },
  proposed: {
    en: (n) => `I proposed EI30 for ${n} door. Review and apply it below.`,
    ro: (n) => `Am propus EI30 pentru ${n} ușă. Verifică și aplică mai jos.`,
    es: (n) => `He propuesto EI30 para ${n} puerta. Revísalo y aplícalo abajo.`,
    de: (n) => `Ich habe EI30 für ${n} Tür vorgeschlagen. Prüfen und unten anwenden.`,
    fr: (n) => `J’ai proposé EI30 pour ${n} porte. Vérifiez et appliquez ci-dessous.`,
    da: (n) => `Jeg har foreslået EI30 til ${n} dør. Gennemse og anvend nedenfor.`,
  },
  walls: {
    en: (s, n, m) => `**${s}** has **${n} walls**, **${m} m** in all.`,
    ro: (s, n, m) => `Pe **${s}** sunt **${n} pereți**, în total **${m} m**.`,
    es: (s, n, m) => `En **${s}** hay **${n} muros**, **${m} m** en total.`,
    de: (s, n, m) => `**${s}**: **${n} Wände**, insgesamt **${m} m**.`,
    fr: (s, n, m) => `**${s}** compte **${n} murs**, **${m} m** au total.`,
    da: (s, n, m) => `**${s}** har **${n} vægge**, i alt **${m} m**.`,
  },
};

/** A small house on the ground floor: 12 × 8 m, two rooms, a door, windows, a floor and a gable roof. */
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

export const scenes = [
  {
    id: 'start',
    async run(t) {
      await t.start();
      await t.shot('start-screen', { clip: { x: 0, y: 0, width: 760, height: 430 } });
      await t.fresh();
      await house(t, { roof: false });
      await t.fit();
      await t.callouts([
        { of: '.tabs-row .tab >> nth=-1', n: 1, at: 'after' },
        { of: '.band', n: 2, at: 'bottom' },
        { of: 'nav.browser', n: 3, at: 'right' },
        { of: '[data-testid=plan]', n: 4, at: 'center' },
        { of: '.hotbar', n: 5, at: 'before' },
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
      await t.keys('Escape', 'Escape');
      const walls = (await t.nodes()).filter((n) => n.type === 'IfcWall');
      t.expect(walls.length === 2, `two walls drawn, found ${walls.length}`);
      // A selected wall: its grips, and the properties panel.
      await select(t, walls[0].id);
      await t.shot('wall-selected', { clip: { x: 250, y: 120, width: 1190, height: 700 } });
      await t.shot('wall-properties', { of: 'aside.props', pad: 0, maxHeight: 620 });
      await t.tool('architecture.wallTypes');
      await t.shot('wall-types', { of: '.dialog', pad: 0 });
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
      await t.shot('joinery-configurator', { of: '.dialog', pad: 0 });
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
      await t.shot('roof-properties', { of: 'aside.props', pad: 0, maxHeight: 700 });
      t.expect((await t.nodes()).some((n) => n.type === 'IfcSlab'), 'a floor in the house');
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
      // Zoomed out a little, about the house, so the porch and the way down stand clear of the quick tools.
      { const [px, py] = await t.planPoint(6, 4); await t.page.mouse.move(px, py); await t.page.mouse.wheel(0, 400); await t.wait(600); }
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
      await t.ui({ dialog: null, keyPlan: true });
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
      const after = (await t.nodes()).find((n) => n.id === r.runId);
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
    id: 'structure',
    async run(t) {
      await t.fresh();
      const s = await t.storey();
      for (const x of [0, 6, 12]) for (const y of [0, 6]) await t.cmd('structure.addColumn', { storeyId: s, x, y });
      for (const y of [0, 6]) await t.cmd('structure.addBeam', { storeyId: s, x1: 0, y1: y, x2: 12, y2: y });
      for (const x of [0, 6, 12]) await t.cmd('structure.addBeam', { storeyId: s, x1: x, y1: 0, x2: x, y2: 6 });
      const f = await t.storey('Foundations');
      for (const x of [0, 6, 12]) for (const y of [0, 6]) await t.cmd('structure.addPadFooting', { storeyId: f, x, y });
      await t.button('Structure', { exact: true });
      await t.shot('ribbon-structure', { of: '.topbar', pad: 0 });
      await open3d(t);
      await t.shot('structure-3d', { of: 'canvas', pad: 0 });
      t.expect((await t.nodes()).filter((n) => n.type === 'IfcColumn').length === 6, 'six columns');
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
    id: 'site-works',
    async run(t) {
      await t.fresh();
      const { walls } = await house(t, { roof: false });
      const first = await t.storey('First floor');
      const ground = await t.storey();
      const slab = (await t.cmd('architecture.addSlab', { storeyId: first, points: [[0, 0], [12, 0], [12, 8], [0, 8]] })).id;
      const beam = (await t.cmd('structure.addBeam', { storeyId: ground, x1: 0, y1: 4, x2: 7, y2: 4 })).id;
      await t.button('Site works', { exact: true });
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
    id: 'views',
    async run(t) {
      await t.fresh();
      await house(t);
      await t.cmd('view.create', { kind: 'section', name: 'A-A', start: [6, -2], end: [6, 10], depth: 0.5, scale: 50 });
      await t.cmd('view.create', { kind: 'elevation', direction: 'south' });
      // A jogged section (decision 176): up through the south wall, across through the inner wall, up through the north wall and its window.
      await t.cmd('view.create', { kind: 'section', name: 'B-B', pts: [[2, -1], [2, 4], [9.5, 4], [9.5, 9]], scale: 50 });
      await t.wait(800);
      await t.shot('browser', { of: 'nav.browser', pad: 0, maxHeight: 640 });
      await openFromBrowser(t, 'A-A', 2500);
      await t.shot('section-view');
      await openFromBrowser(t, 'B-B', 2500);
      await t.shot('section-jogged', { of: '[data-testid=drawing]', pad: 0 });
      const elevation = (await t.nodes()).find((n) => n.type === 'LapinView' && /elevation/i.test(n.name));
      t.expect(elevation, 'an elevation in the project');
      await openFromBrowser(t, elevation.name, 2500);
      await t.shot('elevation-view', { of: '[data-testid=drawing]', pad: 0 });
      await t.button('View', { exact: true });
      await t.shot('ribbon-view', { of: '.topbar', pad: 0 });
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
      await page.locator('.tabs-row button.tab').getByText('View', { exact: true }).click();
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
      t.expect(await page.locator('.mode-chip.storeys').getByText('cut at 1.20 m').count() === 1, 'the chip says where the model is cut');
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
      const layersSelect = props.locator('dt', { hasText: /^Layers$/ }).locator('xpath=following-sibling::dd[1]').locator('select');
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
      await t.button('Draft', { exact: true });
      await t.shot('ribbon-draft', { of: '.topbar', pad: 0 });
      await t.button('Annotate', { exact: true });
      await t.shot('ribbon-annotate', { of: '.topbar', pad: 0 });
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
    id: 'standards',
    async run(t) {
      await t.fresh();
      await t.tool('manage.standards');
      const tab = t.page.locator('.dialog').getByText('Project', { exact: true }).first();
      await tab.click();
      await t.wait(400);
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
      // The structure left out: only the architecture comes in.
      await dialog.locator('[data-discipline=structure] input[type=checkbox]').first().uncheck();
      await dialog.getByRole('button', { name: 'Import', exact: true }).click();
      await t.page.waitForSelector('[data-testid=busy]', { state: 'detached', timeout: 120_000 });
      await t.wait(500);
      const kinds = (await t.nodes()).map((n) => n.type);
      t.expect(kinds.includes('IfcWall') || kinds.includes('IfcWallStandardCase'), 'the walls came in');
      t.expect(!kinds.includes('IfcColumn') && !kinds.includes('IfcBeam'), 'the structure left out stayed out');
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
      await t.shot('help-menu', { clip: { x: 1100, y: 0, width: 340, height: 340 } });
      t.expect((await t.page.locator('.menu-langs button').allTextContents()).join(' ') === 'English Română Español Deutsch Français Dansk', 'the help menu offers the interface in English, Romanian, Spanish, German, French and Danish');
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
      await t.page.mouse.click(700, 500);
      await t.keys('k', 's');
      await t.wait(400);
      t.expect(await t.page.locator('.shortcuts-dialog').count() === 1, 'KS opens the keyboard shortcuts');
      await t.page.locator('.shortcuts-dialog input').first().fill('wall');
      await t.wait(300);
      await t.shot('shortcuts-dialog', { of: '.shortcuts-dialog', pad: 0 });
    },
  },
  {
    // The project folder as a CDE (docs/plan-cde.md): issuing is the desktop app's, so the guide's browser run opens the
    // dialogs with the development switch (the folder typed in) against a temporary folder; nothing is issued here.
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
      await t.cmd('cde.setFolder', { path: join(tmpdir(), 'lapin-guide-cde') });
      await t.wait(400);
      await t.tool('manage.projectFolder');
      t.expect((await t.page.locator('.dialog input[aria-label=Folder]').inputValue()).includes('lapin-guide-cde'), 'the project folder dialog shows the folder kept in the project');
      await t.shot('project-folder', { of: '.dialog', pad: 0 });
      await t.button('Close', { exact: true });
      await t.tool('manage.check');
      await t.page.waitForSelector('[data-testid=cde-findings]', { timeout: 10_000 });
      t.expect(await t.page.locator('[data-testid=cde-findings] li').count() > 0, 'the check lists what the house still lacks (tags, properties)');
      await t.shot('cde-check', { of: '.dialog', pad: 0 });
      await t.button('Close', { exact: true });
      await t.tool('manage.issue');
      await t.page.waitForSelector('[data-testid=cde-findings]', { timeout: 10_000 });
      t.expect((await t.page.locator('.dialog input[aria-label=Revision]').inputValue()) === 'P01', 'the first issue is proposed as P01');
      await t.shot('cde-issue', { of: '.dialog', pad: 0 });
      await t.button('Cancel', { exact: true });
    },
  },
];
