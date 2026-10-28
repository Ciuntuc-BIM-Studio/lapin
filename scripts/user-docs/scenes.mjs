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
      t.expect(await page.locator('.section-box-handle').count() === 6, 'the section box has a handle on each face');
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
      await t.shot('help-menu', { clip: { x: 1100, y: 0, width: 340, height: 340 } });
      await t.page.getByRole('menuitem', { name: 'About Lapin…' }).click();
      await t.wait(200);
      t.expect(await t.page.getByText('Helpware', { exact: true }).count() > 0, 'About Lapin says what helpware means');
      await t.shot('about', { of: '.dialog.about', pad: 0 });
    },
  },
];
