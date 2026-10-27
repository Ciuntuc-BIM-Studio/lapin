// The user guide's scenes (run.mjs): each drives Lapin as a user does, checks
// what it sees, and takes the pictures its page shows (shots/<name>.webp).
// A scene starts from the start screen; `house` gives it a small model to
// work on, built through the same commands the interface sends.

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
  await openFromBrowser(t, '{3D}', 0);
  await t.page.waitForSelector('canvas', { timeout: 20_000 });
  await t.wait(2500);
}

/** Selects an element, as a click on it does. */
const select = (t, id) => t.ui({ selection: id, selected: [id] });

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
      await t.wait(800);
      await t.shot('browser', { of: 'nav.browser', pad: 0, maxHeight: 640 });
      await openFromBrowser(t, 'A-A', 2500);
      await t.shot('section-view');
      const elevation = (await t.nodes()).find((n) => n.type === 'LapinView' && /elevation/i.test(n.name));
      t.expect(elevation, 'an elevation in the project');
      await openFromBrowser(t, elevation.name, 2500);
      await t.shot('elevation-view', { of: '[data-testid=drawing]', pad: 0 });
      await t.button('View', { exact: true });
      await t.shot('ribbon-view', { of: '.topbar', pad: 0 });
    },
  },
  {
    id: 'drafting',
    async run(t) {
      await t.fresh();
      await house(t, { roof: false });
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
    id: 'export',
    async run(t) {
      await t.fresh();
      await house(t);
      await t.button('File', { exact: true });
      await t.shot('file-menu', { of: '[role=menu]', pad: 6 });
      await t.page.getByRole('menuitem', { name: /Export 3D/ }).click();
      await t.wait(500);
      await t.shot('export-3d', { of: '[aria-label="Export 3D"]', pad: 0 });
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
      await input.fill(t.lang === 'ro' ? 'cum copiez un perete' : 'how do I copy a wall');
      await input.press('Enter');
      await t.wait(400);
      t.expect(await t.page.getByText('How do I copy a wall?').count() > 0, 'Ask Lapin answers how to copy a wall');
      await t.shot('ask-me', { of: '[aria-label="Ask Lapin"]', pad: 0 });
      await t.keys('Escape');
      await t.page.mouse.click(5, 600);
      await t.page.locator('.help-menu > button').click();
      await t.wait(300);
      await t.shot('help-menu', { clip: { x: 1100, y: 0, width: 340, height: 300 } });
    },
  },
];
