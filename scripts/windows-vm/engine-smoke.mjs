// The engine of a Windows build, driven from the Mac through an SSH tunnel (test-build.sh): a new project, a
// wall, a window in it (its opening is a boolean: manifold's Windows library), a slab, the plan drawn, then
// the project saved and the IFC and GLB exports. Fails on the first thing that does not work.
//   node engine-smoke.mjs <port> [<where to save, on the engine's machine>]
const port = Number(process.argv[2] ?? 47899);
const savePath = process.argv[3] ?? 'C:\\Users\\lapin\\lapin-smoke.lapin';
const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`); ws.binaryType = 'arraybuffer';
let id = 0; const pending = new Map(); const dec = new TextDecoder();
ws.onmessage = (ev) => { const buf = new Uint8Array(ev.data); const n = new DataView(buf.buffer).getUint32(4, true); const h = JSON.parse(dec.decode(buf.subarray(8, 8 + n)).trim()); if (h.id != null && pending.has(h.id)) { const { res, rej } = pending.get(h.id); pending.delete(h.id); h.error ? rej(new Error(h.error)) : res(h.result); } };
const call = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pending.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const ex = (command, args) => call('doc.execute', { command, args });
await new Promise((r, j) => { ws.onopen = r; ws.onerror = () => j(new Error(`no engine on port ${port}`)); });
const step = async (label, f) => { const t = Date.now(); const r = await f(); console.log(`▸ ${label} (${Date.now() - t} ms)${r ? `: ${r}` : ''}`); };
const info = await call('session.info');
console.log(`▸ engine ${info.engineVersion ?? ''} ${info.product?.name ?? ''}, modules ${info.modules.map((m) => m.id).join(', ')}`);
await call('project.new');
const gf = (await call('doc.snapshot')).nodes.find((n) => n.type === 'IfcBuildingStorey' && n.name === 'Ground floor').id;
let wall;
await step('a wall', async () => { wall = (await ex('architecture.addWall', { storeyId: gf, x1: 0, y1: 0, x2: 6, y2: 0 })).id; });
await step('a window in it (an opening cut by manifold)', async () => {
  await ex('architecture.addWindow', { wallId: wall, along: 3 });
  const snap = await call('doc.snapshot');
  const item = snap.render.find((r) => r.nodeId === wall);
  const problems = item?.diagnostics ?? [];
  if (problems.length) throw new Error(`the wall reports: ${problems.join('; ')}`);
  // Meshes come as binary buffers: each array names its length (n).
  return `${item.parts.length} parts, ${item.parts.reduce((s, p) => s + (p.indices?.n ?? 0) / 3, 0)} triangles`;
});
await step('a slab', async () => { await ex('architecture.addSlab', { storeyId: gf, points: [[0, 0], [6, 0], [6, 4], [0, 4]] }); });
await step('the plan drawn', async () => { const d = await call('doc.query', { name: 'view.drawing', args: { id: `plan:${gf}` } }); return `${d.pens.length} pens`; });
await step('saved and opened', async () => { await call('project.save', { path: savePath }); await call('project.open', { path: savePath }); });
await step('IFC export', async () => { const r = await call('project.exportIfc', {}); return `${Buffer.from(r.data, 'base64').length} bytes`; });
await step('GLB export', async () => { const r = await call('project.exportMesh', { format: 'glb' }); return `${r.elements} elements, ${r.triangles} triangles`; });
console.log('✓ the Windows engine works');
ws.close();
