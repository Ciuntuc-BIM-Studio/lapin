/**
 * The viewer of the page Lapin exports for the browser (decision 482, docs/html-export.md), built on its own as one
 * script (vite.config.ts, virtual:lapin-viewer) that the engine inlines into the page. It reads the page's blocks — the
 * data (JSON), the 3D model (GLB, base64), each drawing (SVG) — and shows them with nothing from the network: the 3D
 * model (orbit, pan, zoom, fit; a storey alone or with those below; a click selects, isolate, hide, show all), the
 * drawings (the list, pan and zoom, print; a click selects the element), each module's table (the estimate: sortable,
 * filtered, a row selects its elements), and the selection's properties. A selection is one across the tabs: an
 * element clicked on a plan is the one lit in 3D. Lapin's look, light or dark; the exporting user's language.
 */

import {
  AmbientLight, Box3, Color, DirectionalLight, HemisphereLight, Material, Mesh, MeshStandardMaterial, Object3D, PerspectiveCamera,
  Raycaster, Scene, SRGBColorSpace, Vector2, Vector3, WebGLRenderer,
} from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  drawingTitle, filterRows, fitBox, formatNumber, fromBase64, groupDrawings, phrase, readData, sortRows, storeysShown, unscript, zoomAbout,
  type PageData, type PageDrawing, type PageTable,
} from './data';
import { VIEWER_CSS } from './style';

type Child = Node | string | null | undefined | false;
function h<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Record<string, string | boolean | undefined> = {}, ...children: Child[]): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === false) continue;
    if (k === 'class') e.className = String(v);
    else e.setAttribute(k, v === true ? '' : v);
  }
  for (const c of children) if (c != null && c !== false) e.append(c);
  return e;
}

function start(data: PageData): void {
  const t = (en: string) => phrase(data.strings, en);
  const style = document.createElement('style');
  style.textContent = VIEWER_CSS;
  document.head.append(style);
  const root = document.documentElement;
  const dark = (() => { try { return window.matchMedia('(prefers-color-scheme: dark)').matches; } catch { return false; } })();
  root.dataset.theme = data.theme?.name === 'graphite' || data.theme?.name === 'paper' ? data.theme.name : dark ? 'graphite' : 'paper';
  root.lang = data.lang || 'en';

  // ─── The selection, one across the tabs ───────────────────────────────
  let selection = new Set<string>();
  const onSelect: (() => void)[] = [];
  const select = (ids: Iterable<string>, add = false) => {
    const next = add ? new Set(selection) : new Set<string>();
    for (const id of ids) { if (add && next.has(id)) next.delete(id); else next.add(id); }
    selection = next;
    for (const f of onSelect) f();
  };

  // ─── The header ───────────────────────────────────────────────────────
  let date = data.date;
  try { date = new Intl.DateTimeFormat(data.lang || 'en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(data.date)); } catch { /* as written */ }
  const tabs = h('nav', { class: 'lp-tabs', role: 'tablist' });
  const themeBtn = h('button', { class: 'lp-btn', type: 'button' });
  const paintTheme = () => { themeBtn.textContent = root.dataset.theme === 'graphite' ? t('Light theme') : t('Dark theme'); };
  themeBtn.onclick = () => { root.dataset.theme = root.dataset.theme === 'graphite' ? 'paper' : 'graphite'; paintTheme(); render3d?.(); };
  paintTheme();
  const head = h('header', { class: 'lp-head' },
    h('span', { class: 'lp-mark', 'aria-hidden': 'true' }, (data.edition || 'Lapin').trim().charAt(0).toUpperCase() || 'L'),
    h('div', { class: 'lp-title' }, h('b', {}, data.project), h('span', {}, `${data.app && data.app !== data.edition ? `${data.edition} · ${data.app}` : data.edition} · ${t('Exported')} ${date}`)),
    tabs,
    h('span', { class: 'lp-grow' }),
    data.hasProject ? h('span', { class: 'lp-open', title: t('A Lapin project. To work on it, open this file in Lapin: File → Open.') }, h('b', {}, t('Open in Lapin')), ': File → Open') : null,
    themeBtn);

  const pane = h('section', { class: 'lp-pane' });
  const side = h('aside', { class: 'lp-side', 'aria-label': t('Properties') });
  const app = h('div', { class: 'lp' }, head, h('div', { class: 'lp-main' }, pane, side));
  document.getElementById('lapin-start')?.remove();
  document.body.append(app);

  // ─── The properties of what is selected ───────────────────────────────
  const props = () => {
    side.replaceChildren();
    const ids = [...selection].filter((id) => data.elements[id]);
    if (ids.length === 0) {
      side.append(h('div', { class: 'lp-eyebrow' }, t('Properties')), h('p', { class: 'lp-muted' }, t('Click an element in 3D, on a drawing or in a table to see its properties.')));
      return;
    }
    if (ids.length > 1) {
      const clear = h('button', { class: 'lp-btn', type: 'button' }, t('Clear'));
      clear.onclick = () => select([]);
      const list = h('div');
      for (const id of ids.slice(0, 200)) {
        const e = data.elements[id];
        const b = h('button', { class: 'lp-btn', type: 'button', style: 'display:flex;width:100%;margin:2px 0;justify-content:flex-start' }, `${e.name || e.class}`);
        b.onclick = () => select([id]);
        list.append(b);
      }
      side.append(h('div', { class: 'lp-eyebrow' }, `${ids.length} ${t('elements selected')}`), clear, list);
      return;
    }
    const e = data.elements[ids[0]];
    const dl = (rows: [string, string | undefined][]) => {
      const d = h('dl', { class: 'lp-props' });
      for (const [k, v] of rows) if (v) d.append(h('dt', {}, t(k)), h('dd', {}, v === 'Yes' || v === 'No' ? t(v) : v));
      return d;
    };
    side.append(h('div', { class: 'lp-eyebrow' }, t('Properties')),
      dl([['Name', e.name], ['Class', e.class], ['Type', e.type], ['Storey', e.storey], ['Material', e.material], ['GlobalId', ids[0]]]));
    for (const s of e.sets) side.append(h('div', { class: 'lp-eyebrow' }, t(s.name)), dl(s.props));
    for (const s of e.quantities) side.append(h('div', { class: 'lp-eyebrow' }, `${t('Quantities')} · ${t(s.name)}`), dl(s.props));
  };
  onSelect.push(props);
  props();

  // ─── The tabs ─────────────────────────────────────────────────────────
  const panes: { key: string; label: string; show: () => HTMLElement }[] = [];
  let render3d: (() => void) | null = null;
  if (data.model3d) panes.push({ key: '3d', label: t('3D'), show: () => model3d(data, t, select, onSelect, () => selection, (f) => { render3d = f; }) });
  if (data.drawings.length) panes.push({ key: 'drawings', label: t('Drawings'), show: () => drawings(data, t, select, onSelect, () => selection) });
  for (const table of data.tables) panes.push({ key: 'table:' + table.key, label: t(table.title), show: () => tableView(data, table, t, select) });
  if (panes.length === 0) pane.append(h('div', { class: 'lp-empty' }, t('No drawing was exported with this page.')));
  const shown = new Map<string, HTMLElement>();
  const open = (key: string) => {
    for (const b of tabs.querySelectorAll('button')) b.setAttribute('aria-selected', String(b.dataset.key === key));
    for (const [k, el] of shown) el.style.display = k === key ? '' : 'none';
    if (!shown.has(key)) {
      const p = panes.find((x) => x.key === key);
      if (!p) return;
      const el = h('div', { style: 'position:absolute;inset:0' });
      pane.append(el);
      shown.set(key, el);
      el.append(p.show());
    }
    window.dispatchEvent(new Event('resize'));
  };
  for (const p of panes) {
    const b = h('button', { class: 'lp-tab', type: 'button', role: 'tab', 'data-key': p.key }, p.label);
    b.onclick = () => open(p.key);
    tabs.append(b);
  }
  if (panes[0]) open(panes[0].key);
}

// ─── 3D ────────────────────────────────────────────────────────────────────

/** An element's node: a node of the GLB with the element's GlobalId and storey in its extras (GltfWriter), not a storey's or the building's. */
const isElementNode = (o: Object3D) => typeof o.userData?.globalId === 'string' && typeof o.userData.storey === 'string';
/** A storey's node (or the site's, for what stands on none): its GlobalId and elevation in its extras. */
const isStoreyNode = (o: Object3D) => typeof o.userData?.globalId === 'string' && typeof o.userData.elevation === 'number';

function model3d(data: PageData, t: (s: string) => string, select: (ids: Iterable<string>, add?: boolean) => void, onSelect: (() => void)[], selection: () => Set<string>, setRender: (f: () => void) => void): HTMLElement {
  const box = h('div', { style: 'position:absolute;inset:0' });
  const block = document.getElementById('lapin-glb');
  if (!block?.textContent) { box.append(h('div', { class: 'lp-empty' }, t('This page was exported without the 3D model.'))); return box; }
  let renderer: WebGLRenderer;
  try {
    renderer = new WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  } catch {
    box.append(h('div', { class: 'lp-empty' }, t('Your browser cannot show 3D here (WebGL is off).')));
    return box;
  }
  renderer.outputColorSpace = SRGBColorSpace;
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  const canvas = renderer.domElement;
  canvas.className = 'lp-canvas';
  canvas.dataset.testid = 'lp-3d';
  box.append(canvas);
  const scene = new Scene();
  const camera = new PerspectiveCamera(45, 1, 0.05, 5000);
  camera.up.set(0, 1, 0);
  scene.add(new HemisphereLight(0xffffff, 0x8a8174, 1.1));
  scene.add(new AmbientLight(0xffffff, 0.35));
  const sun = new DirectionalLight(0xffffff, 1.4);
  sun.position.set(30, 60, 40);
  scene.add(sun);
  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = false;
  controls.screenSpacePanning = true;

  let pending = false;
  const render = () => {
    if (pending) return;
    pending = true;
    requestAnimationFrame(() => {
      pending = false;
      const bg = getComputedStyle(document.documentElement).getPropertyValue('--view').trim() || '#eef0f2';
      scene.background = new Color(bg);
      renderer.render(scene, camera);
    });
  };
  setRender(render);
  controls.addEventListener('change', render);
  const resize = () => {
    const w = box.clientWidth || 1;
    const hh = box.clientHeight || 1;
    renderer.setSize(w, hh, false);
    camera.aspect = w / hh;
    camera.updateProjectionMatrix();
    render();
  };
  window.addEventListener('resize', resize);
  new ResizeObserver(resize).observe(box);

  // Each mesh with the elements it stands for (an assembly's part: the part and the assembly) and its storey.
  const meshes: { mesh: Mesh; ids: string[]; storey: string | null; material: Material | Material[] }[] = [];
  const byId = new Map<string, number[]>();
  const highlight = new MeshStandardMaterial({ color: 0xb5532d, emissive: 0x5a2410, roughness: 0.6, metalness: 0 });
  const hidden = new Set<string>();
  let isolated: Set<string> | null = null;
  let storeyChoice: string | null = null;
  let below = true;

  const apply = () => {
    const shownStoreys = storeysShown(data.storeys, storeyChoice, below);
    const sel = selection();
    for (const m of meshes) {
      const inStorey = !m.storey || !data.storeys.some((s) => s.id === m.storey) || shownStoreys.has(m.storey);
      m.mesh.visible = inStorey && !m.ids.some((id) => hidden.has(id)) && (!isolated || m.ids.some((id) => isolated!.has(id)));
      m.mesh.material = m.ids.some((id) => sel.has(id)) ? highlight : m.material;
    }
    render();
  };
  onSelect.push(apply);

  const fit = () => {
    const b = new Box3();
    for (const m of meshes) if (m.mesh.visible) b.expandByObject(m.mesh);
    if (b.isEmpty()) return;
    const c = b.getCenter(new Vector3());
    const size = b.getSize(new Vector3()).length() || 10;
    const dir = new Vector3(1, 0.8, 1.2).normalize();
    camera.position.copy(c).addScaledVector(dir, size * 1.45);
    camera.near = Math.max(0.01, size / 500);
    camera.far = size * 50;
    camera.updateProjectionMatrix();
    controls.target.copy(c);
    controls.update();
    render();
  };

  // The tools over the view.
  const storeySel = h('select', { 'aria-label': t('Storey') }, h('option', { value: '' }, t('All storeys')));
  for (const s of [...data.storeys].reverse()) storeySel.append(h('option', { value: s.id }, s.name));
  storeySel.onchange = () => { storeyChoice = storeySel.value || null; apply(); };
  const belowBox = h('input', { type: 'checkbox', checked: true });
  belowBox.onchange = () => { below = belowBox.checked; apply(); };
  const btn = (label: string, f: () => void, testid?: string) => { const b = h('button', { class: 'lp-btn', type: 'button', 'data-testid': testid }, label); b.onclick = f; return b; };
  box.append(h('div', { class: 'lp-hud' },
    data.storeys.length > 1 ? storeySel : null,
    data.storeys.length > 1 ? h('label', {}, belowBox, t('and below')) : null,
    btn(t('Fit'), fit, 'lp-fit'),
    btn(t('Isolate'), () => { if (selection().size) { isolated = new Set(selection()); apply(); fit(); } }),
    btn(t('Hide'), () => { for (const id of selection()) hidden.add(id); select([]); }),
    btn(t('Show all'), () => { hidden.clear(); isolated = null; storeyChoice = null; storeySel.value = ''; apply(); })));

  // A click (not a drag) picks the element under it; Shift or Ctrl adds to the selection.
  const ray = new Raycaster();
  let down: { x: number; y: number } | null = null;
  canvas.addEventListener('pointerdown', (e) => { down = { x: e.clientX, y: e.clientY }; });
  canvas.addEventListener('pointerup', (e) => {
    if (!down || Math.hypot(e.clientX - down.x, e.clientY - down.y) > 4) { down = null; return; }
    down = null;
    const r = canvas.getBoundingClientRect();
    ray.setFromCamera(new Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), camera);
    const hit = ray.intersectObjects(meshes.filter((m) => m.mesh.visible).map((m) => m.mesh), false)[0];
    const entry = hit ? meshes.find((m) => m.mesh === hit.object) : undefined;
    const add = e.shiftKey || e.ctrlKey || e.metaKey;
    if (!entry) { if (!add) select([]); return; }
    // The innermost element: an assembly's part before the assembly.
    select([entry.ids[0]], add);
  });

  const bytes = fromBase64(block.textContent);
  new GLTFLoader().parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer, '', (gltf) => {
    scene.add(gltf.scene);
    gltf.scene.updateMatrixWorld(true);
    gltf.scene.traverse((o) => {
      o.matrixAutoUpdate = false;
      if (!(o as Mesh).isMesh) return;
      const mesh = o as Mesh;
      const ids: string[] = [];
      let storey: string | null = null;
      for (let p: Object3D | null = mesh; p; p = p.parent) {
        if (isElementNode(p)) ids.push(p.userData.globalId as string);
        else if (isStoreyNode(p) && !storey) storey = (p.userData.globalId as string) ?? null;
      }
      if (ids.length === 0) return;
      const at = meshes.length;
      meshes.push({ mesh, ids, storey, material: mesh.material });
      for (const id of ids) { const l = byId.get(id); if (l) l.push(at); else byId.set(id, [at]); }
    });
    resize();
    apply();
    fit();
    box.dataset.loaded = 'true';
  }, () => { box.append(h('div', { class: 'lp-empty' }, t('Your browser cannot show 3D here (WebGL is off).'))); });
  return box;
}

// ─── Drawings ──────────────────────────────────────────────────────────────

/** Paper millimetres as CSS pixels at 100 %. */
const PX = 96 / 25.4;

function drawings(data: PageData, t: (s: string) => string, select: (ids: Iterable<string>, add?: boolean) => void, onSelect: (() => void)[], selection: () => Set<string>): HTMLElement {
  const list = h('nav', { class: 'lp-list', 'aria-label': t('Drawings') });
  const paperBox = h('div', { class: 'lp-paperbox' });
  const paper = h('div', { class: 'lp-paper' });
  paperBox.append(paper);
  const fitBtn = h('button', { class: 'lp-btn', type: 'button' }, t('Fit the drawing'));
  const printBtn = h('button', { class: 'lp-btn', type: 'button' }, t('Print'));
  printBtn.onclick = () => window.print();
  paperBox.append(h('div', { class: 'lp-hud' }, fitBtn, printBtn));
  let view = { scale: 1, x: 0, y: 0 };
  let current: PageDrawing | null = null;
  const place = () => { paper.style.transform = `translate(${view.x}px, ${view.y}px) scale(${view.scale})`; };
  const fit = () => {
    if (!current) return;
    view = fitBox(current.w * PX, current.h * PX, paperBox.clientWidth || 800, paperBox.clientHeight || 600);
    place();
  };
  fitBtn.onclick = fit;
  const lit = () => {
    const sel = selection();
    for (const p of paper.querySelectorAll<SVGElement>('[data-gid]')) p.classList.toggle('lp-on', sel.has(p.getAttribute('data-gid') ?? ''));
  };
  onSelect.push(lit);
  const show = (d: PageDrawing) => {
    current = d;
    for (const b of list.querySelectorAll('button')) b.setAttribute('aria-current', String(b.dataset.id === d.id));
    const block = [...document.querySelectorAll<HTMLScriptElement>('script[data-drawing]')].find((s) => s.dataset.drawing === d.id);
    paper.innerHTML = block?.textContent ? unscript(block.textContent) : '';
    paper.style.width = `${d.w * PX}px`;
    paper.style.height = `${d.h * PX}px`;
    paper.dataset.drawing = d.id;
    lit();
    requestAnimationFrame(fit);
  };
  for (const g of groupDrawings(data.drawings)) {
    list.append(h('div', { class: 'lp-eyebrow' }, t(g.label)));
    for (const d of g.items) {
      const b = h('button', { type: 'button', 'data-id': d.id, title: drawingTitle(d) }, drawingTitle(d));
      b.onclick = () => show(d);
      list.append(b);
    }
  }
  // Pan by dragging, zoom by the wheel about the cursor; a click (no drag) on an element selects it.
  let drag: { x: number; y: number; vx: number; vy: number; moved: boolean } | null = null;
  paperBox.addEventListener('pointerdown', (e) => {
    if ((e.target as Element).closest('.lp-hud')) return;
    drag = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y, moved: false };
    paperBox.setPointerCapture(e.pointerId);
  });
  paperBox.addEventListener('pointermove', (e) => {
    if (!drag) return;
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (!drag.moved && Math.hypot(dx, dy) < 4) return;
    drag.moved = true;
    paperBox.classList.add('lp-dragging');
    view = { ...view, x: drag.vx + dx, y: drag.vy + dy };
    place();
  });
  paperBox.addEventListener('pointerup', (e) => {
    const was = drag;
    drag = null;
    paperBox.classList.remove('lp-dragging');
    if (!was || was.moved) return;
    const under = document.elementsFromPoint(e.clientX, e.clientY).find((x) => x instanceof SVGElement && x.hasAttribute('data-gid'));
    const id = under?.getAttribute('data-gid');
    const add = e.shiftKey || e.ctrlKey || e.metaKey;
    if (id) select([id], add); else if (!add) select([]);
  });
  paperBox.addEventListener('wheel', (e) => {
    e.preventDefault();
    const r = paperBox.getBoundingClientRect();
    view = zoomAbout(view, { x: e.clientX - r.left, y: e.clientY - r.top }, Math.exp(-e.deltaY * 0.0015));
    place();
  }, { passive: false });
  window.addEventListener('resize', () => { if (current && paperBox.clientWidth) fit(); });
  const box = h('div', { class: 'lp-draw' }, list, paperBox);
  if (data.drawings[0]) requestAnimationFrame(() => show(groupDrawings(data.drawings)[0].items[0]));
  return box;
}

// ─── A module's table (the estimate) ───────────────────────────────────────

function tableView(data: PageData, table: PageTable, t: (s: string) => string, select: (ids: Iterable<string>, add?: boolean) => void): HTMLElement {
  const box = h('div', { class: 'lp-tablebox', 'data-table': table.key });
  const filter = h('input', { class: 'lp-filter', type: 'search', placeholder: t('Filter'), 'aria-label': t('Filter') });
  box.append(h('div', { class: 'lp-tablehead' },
    h('h2', {}, t(table.title)),
    table.name ? h('span', {}, table.name) : null,
    table.indicative ? h('span', { class: 'lp-tag', title: t('Indicative: priced by Lapin\'s own rough values, not yours.') }, t('Indicative: priced by Lapin\'s own rough values, not yours.')) : null,
    h('span', { class: 'lp-grow' }),
    filter));
  if (table.note) box.append(h('p', { class: 'lp-muted', style: 'margin:0 0 10px' }, `${t(table.note)} ${t('Click a row to see its elements.')}`));
  const el = h('table', { class: 'lp-table' });
  const thead = h('thead');
  const tbody = h('tbody');
  el.append(thead, tbody);
  box.append(el);
  let sortCol: number | null = null;
  let desc = false;
  let on: HTMLTableRowElement | null = null;
  const numeric = (k: string) => k === 'number' || k === 'money';
  const draw = () => {
    thead.replaceChildren();
    const tr = h('tr');
    table.columns.forEach((c, i) => {
      const th = h('th', { class: numeric(c.kind) ? 'lp-num' : undefined, 'aria-sort': sortCol === i ? (desc ? 'descending' : 'ascending') : undefined }, `${t(c.label)}${sortCol === i ? (desc ? ' ↓' : ' ↑') : ''}`);
      // Sorted by the column, then the other way, then as written (by chapter).
      th.onclick = () => { if (sortCol !== i) { sortCol = i; desc = false; } else if (!desc) desc = true; else sortCol = null; draw(); };
      tr.append(th);
    });
    thead.append(tr);
    tbody.replaceChildren();
    for (const r of sortRows(filterRows(table.rows, filter.value), sortCol, desc)) {
      const row = h('tr', { class: r.heading ? 'lp-heading' : undefined });
      table.columns.forEach((c, i) => {
        const v = r.cells[i];
        row.append(h('td', { class: numeric(c.kind) ? 'lp-num' : undefined }, typeof v === 'number' ? formatNumber(v, c.kind, data.lang) : r.heading && i === 0 && typeof v === 'string' ? t(v) : v ?? ''));
      });
      row.onclick = () => {
        on?.classList.remove('lp-on');
        on = row;
        row.classList.add('lp-on');
        select(r.elements);
      };
      tbody.append(row);
    }
  };
  filter.oninput = draw;
  draw();
  if (table.totals.length) {
    const totals = h('table', { class: 'lp-totals' });
    for (const x of table.totals)
      totals.append(h('tr', {}, h('td', {}, `${t(x.label)}${x.percent != null ? ` (${formatNumber(x.percent, 'number', data.lang)} %)` : ''}`),
        h('td', {}, `${formatNumber(x.value, 'money', data.lang)}${table.currency ? ` ${table.currency}` : ''}`)));
    box.append(totals);
  }
  return box;
}

const data = readData(document);
if (data) start(data);
