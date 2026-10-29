import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import type { ModelState } from '../engine/model';
import { gateOpen } from './gate';
import { defaultModel, fetchTransport, loopbackUrl, transportFor, type OllamaChunk } from './ollama';
import { MAX_CHARS, measure, resolveClasses, runTool, tableCsv, TOOL_DEFS, type QuantitySet, type ToolContext } from './tools';
import { checkProposal, mergeRows, toSteps, type ChangeRow, type StandardTable } from './changeset';
import { parseCsv } from './csv';
import { cleanText, runTurn, SYSTEM_PROMPT, type TurnEvent } from './loop';

// ─── A small project: two storeys, walls, doors (one wide), a wall type ────

const node = (id: string, type: string, name: string, storeyId: string | null, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name, parentId: storeyId, px: 0, py: 0, props: props as NodeDto['props'], editable: true, module: 'architecture', storeyId });

function project(walls = 3): { model: ModelState; q: Record<string, QuantitySet[]> } {
  const nodes: NodeDto[] = [
    node('p', 'IfcProject', 'House', null),
    node('s0', 'IfcBuildingStorey', 'Ground floor', null, { elevation: 0 }),
    node('s1', 'IfcBuildingStorey', 'First floor', null, { elevation: 3 }),
    node('wt', 'IfcWallType', 'Brick 250', null),
    node('dt', 'IfcDoorType', 'Door 900×2100', null),
  ];
  const q: Record<string, QuantitySet[]> = {};
  const typed: string[] = [];
  for (let i = 0; i < walls; i++) {
    const id = `w${i}`;
    nodes.push(node(id, 'IfcWall', `Wall ${i + 1}`, i % 2 === 1 ? 's1' : 's0'));
    q[id] = [{ name: 'Qto_WallBaseQuantities', quantities: [{ name: 'Length', value: 2 + i * 0.5 }, { name: 'Height', value: 3 }] }];
    typed.push(id);
  }
  for (const [id, width, storey] of [['d1', 0.9, 's0'], ['d2', 1.2, 's0'], ['d3', 1.0, 's1']] as const) {
    nodes.push(node(id, 'IfcDoor', id.toUpperCase(), storey, id === 'd3' ? { params: [{ name: 'FireRating', kind: 'text', value: 'EI15', pset: 'Pset_DoorCommon' }] } : {}));
    q[id] = [{ name: 'Qto_DoorBaseQuantities', quantities: [{ name: 'Width', value: width }, { name: 'Height', value: 2.1 }] }];
  }
  const relations: RelationDto[] = [
    { id: 'r1', class: 'IfcRelDefinesByType', relating: 'wt', related: typed },
    { id: 'r2', class: 'IfcRelDefinesByType', relating: 'dt', related: ['d1', 'd2', 'd3'] },
  ];
  return { model: { nodes: new Map(nodes.map((n) => [n.id, n])), relations: new Map(relations.map((r) => [r.id, r])) }, q };
}

const TABLE: StandardTable = {
  own: 'Lapin_Parameters',
  reserved: ['Lapin_Intent', 'Lapin_Phase'],
  sets: [
    { name: 'Pset_DoorCommon', classes: ['IfcDoor'], properties: { FireRating: 'text', IsExternal: 'bool', ThermalTransmittance: 'number' } },
    { name: 'Pset_WallCommon', classes: ['IfcWall'], properties: { FireRating: 'text', LoadBearing: 'bool' } },
  ],
};

function context(walls = 3): ToolContext & { calls: string[][] } {
  const { model, q } = project(walls);
  const calls: string[][] = [];
  return {
    model, selection: ['d2'], calls,
    standard: async () => TABLE,
    quantities: async (ids) => { calls.push(ids); return Object.fromEntries(ids.filter((id) => q[id]).map((id) => [id, q[id]])); },
    scheduleCsv: async () => 'Mark,Size,"Note, quoted"\nD,900×2100,"say ""hi"""\n',
  };
}

const json = (s: string) => JSON.parse(s) as Record<string, unknown>;

describe('the assistant\'s gate', () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); });

  it('opens only in the desktop app, for the host, in a build that has it', () => {
    expect(gateOpen({ build: true, desktop: true, guest: false })).toBe(true);
    expect(gateOpen({ build: true, desktop: false, guest: false })).toBe(false);
    expect(gateOpen({ build: true, desktop: true, guest: true })).toBe(false);
    expect(gateOpen({ build: false, desktop: true, guest: false })).toBe(false);
    expect(gateOpen({ build: false, desktop: true, guest: false, devOverride: true })).toBe(false);
    expect(gateOpen({ build: true, desktop: false, guest: false, devOverride: true })).toBe(true);
  });

  it('is not built into the web build (the engine in a worker)', async () => {
    vi.stubEnv('VITE_ENGINE', 'worker');
    vi.resetModules();
    const gate = await import('./gate');
    expect(gate.ASSISTANT_BUILD).toBe(false);
    // Even in the desktop shell, with the development override.
    expect(gate.gateOpen({ build: gate.ASSISTANT_BUILD, desktop: true, guest: false, devOverride: true })).toBe(false);
  });

  it('is built into the desktop and development builds', async () => {
    vi.stubEnv('VITE_ENGINE', '');
    vi.resetModules();
    expect((await import('./gate')).ASSISTANT_BUILD).toBe(true);
  });
});

describe('Ollama', () => {
  it('is reached only on this computer', () => {
    expect(loopbackUrl('')).toBe('http://127.0.0.1:11434');
    expect(loopbackUrl('localhost:11500')).toBe('http://127.0.0.1:11500');
    expect(loopbackUrl('http://[::1]:11434/api')).toBe('http://127.0.0.1:11434');
    expect(loopbackUrl('http://192.168.1.5:11434')).toBeNull();
    expect(loopbackUrl('https://127.0.0.1:11434')).toBeNull();
    expect(loopbackUrl('http://ollama.example.com')).toBeNull();
    expect(loopbackUrl('http://user:pw@127.0.0.1:11434')).toBeNull();
  });

  it('starts with the best model installed', () => {
    expect(defaultModel(['gemma4:12b', 'qwen3-coder:30b', 'phi3:latest'])).toBe('qwen3-coder:30b');
    expect(defaultModel(['phi3:latest', 'gemma4:12b'])).toBe('gemma4:12b');
    expect(defaultModel(['phi3:latest'])).toBe('phi3:latest');
    expect(defaultModel([])).toBeNull();
  });

  it('goes through the shell only where Ollama refuses the webview\'s origin (Windows)', () => {
    expect(transportFor('http://tauri.localhost', true)).toBe('shell');
    expect(transportFor('https://tauri.localhost', true)).toBe('shell');
    expect(transportFor('tauri://localhost', true)).toBe('fetch');
    expect(transportFor('http://localhost:5180', true)).toBe('fetch');
    expect(transportFor('http://tauri.localhost', false)).toBe('fetch');
  });
});

describe('the tools', () => {
  it('are described to the model with a schema each', () => {
    const names = TOOL_DEFS.map((t) => t.function.name);
    expect(names).toEqual(['model_overview', 'list_elements', 'get_element', 'aggregate', 'property_names', 'list_schedules', 'read_schedule', 'propose_properties']);
    for (const t of TOOL_DEFS) expect(t.function.parameters.type).toBe('object');
    // Nothing that edits geometry, deletes or runs a command.
    expect(names.some((n) => /delete|execute|command|move|create/.test(n))).toBe(false);
  });

  it('read a class however the model names it', () => {
    const { model } = project();
    expect(resolveClasses(model, 'doors')).toEqual(['IfcDoor']);
    expect(resolveClasses(model, 'IfcWall, door')).toEqual(['IfcWall', 'IfcDoor']);
    expect(resolveClasses(model, '')).toBeNull();
    expect(() => resolveClasses(model, 'IfcStair')).toThrow(/Classes present: IfcDoor, IfcWall/);
  });

  it('count and sum per storey themselves, so the model only quotes them', async () => {
    const ctx = context(5);
    const r = await runTool(ctx, 'aggregate', { class: 'IfcWall', group_by: ['storey'], metrics: [{ op: 'count' }, { op: 'sum', field: 'Length' }] });
    const out = json(r.content);
    // Walls 1, 3, 5 (2 + 3 + 4 m) below, 2 and 4 (2.5 + 3.5 m) above; the ground floor first.
    expect(out.groups).toEqual([
      { storey: 'Ground floor', count: 3, 'sum(Length)': 9 },
      { storey: 'First floor', count: 2, 'sum(Length)': 6 },
    ]);
    expect(out.total).toEqual({ count: 5, 'sum(Length)': 15 });
    expect(r.table?.rows.at(-1)).toEqual(['Total', 5, 15]);
    expect(r.summary).toBe('Computed 2 group(s) over 5 element(s)');
  });

  it('filter on quantities and on properties, and say which elements lack a field', async () => {
    const ctx = context();
    const wide = json((await runTool(ctx, 'list_elements', { class: 'doors', where: [{ field: 'Width', op: '>', value: 1 }] })).content);
    expect((wide.rows as { id: string }[]).map((r) => r.id)).toEqual(['d2']);
    const rated = json((await runTool(ctx, 'list_elements', { class: 'IfcDoor', where: [{ field: 'Pset_DoorCommon.FireRating', op: 'missing' }] })).content);
    expect((rated.rows as { id: string }[]).map((r) => r.id)).toEqual(['d1', 'd2']);
    const avg = json((await runTool(ctx, 'aggregate', { metrics: [{ op: 'avg', field: 'Width' }] })).content);
    expect(avg.total).toEqual({ 'avg(Width)': 1.033 });
    expect(avg.elements_without_the_field).toEqual({ 'avg(Width)': 3 });
    const selected = json((await runTool(ctx, 'list_elements', { selected_only: true })).content);
    expect(selected.total).toBe(1);
  });

  it('read comparisons and measures as models write them, and name a field nobody has', async () => {
    expect([measure('1 m'), measure('1,2'), measure('900 mm'), measure('120cm'), measure('EI30')]).toEqual([1, 1.2, 0.9, 1.2, null]);
    const ctx = context();
    const r = json((await runTool(ctx, 'list_elements', { class: 'IfcDoor', where: { field: 'width', op: 'gt', value: '1000 mm' } })).content);
    expect((r.rows as { id: string }[]).map((x) => x.id)).toEqual(['d2']);
    const none = json((await runTool(ctx, 'aggregate', { class: 'IfcDoor', where: [{ field: 'Lățime', op: '>', value: 1 }] })).content);
    expect(none.elements).toBe(0);
    expect(none.note).toMatch(/No element here has Lățime\. Fields they have: Width, Height/);
  });

  it('keep a result within the model\'s budget and say where the next page starts', async () => {
    const ctx = context(400);
    const r = await runTool(ctx, 'list_elements', { class: 'IfcWall', limit: 50, fields: ['Length', 'Height', 'type', 'storey'] });
    expect(r.content.length).toBeLessThanOrEqual(MAX_CHARS);
    const out = json(r.content);
    expect(out.total).toBe(400);
    expect((out.rows as unknown[]).length).toBeGreaterThan(0);
    expect(out.next_offset).toBe((out.rows as unknown[]).length);
    // The chat's table has them all, for the user to export.
    expect(r.table?.rows.length).toBe(400);
    const second = json((await runTool(ctx, 'list_elements', { class: 'IfcWall', offset: 390, limit: 50 })).content);
    expect((second.rows as unknown[]).length).toBe(10);
    expect(second.next_offset).toBeNull();
    const overview = await runTool(ctx, 'model_overview', {});
    expect(overview.content.length).toBeLessThanOrEqual(MAX_CHARS);
  });

  it('give one element with its sets and quantities, and a failure as a result', async () => {
    const ctx = context();
    const d3 = json((await runTool(ctx, 'get_element', { id: 'd3' })).content);
    expect(d3).toMatchObject({ class: 'IfcDoor', storey: 'First floor', type: { name: 'Door 900×2100' }, properties: [{ set: 'Pset_DoorCommon', name: 'FireRating', value: 'EI15' }] });
    const bad = await runTool(ctx, 'get_element', { id: 'nope' });
    expect(bad.error).toBe(true);
    expect(json(bad.content).error).toMatch(/No element/);
    expect((await runTool(ctx, 'delete_everything', {})).error).toBe(true);
    expect((await runTool(ctx, 'aggregate', '{not json')).error).toBe(true);
  });

  it('read a schedule and write a table as CSV', async () => {
    const ctx = context();
    ctx.model.nodes.set('sch', node('sch', 'LapinSchedule', 'Doors', null, { kind: 'door' }));
    const r = await runTool(ctx, 'read_schedule', { id: 'sch' });
    expect(json(r.content)).toMatchObject({ columns: ['Mark', 'Size', 'Note, quoted'], rows: [['D', '900×2100', 'say "hi"']] });
    expect(parseCsv(tableCsv(r.table!))).toEqual([['Mark', 'Size', 'Note, quoted'], ['D', '900×2100', 'say "hi"']]);
  });
});

describe('the proposals', () => {
  const { model } = project();

  it('take a standard set only with its own names, on its classes', () => {
    const ok = checkProposal({ id: 'd1', pset: 'Pset_DoorCommon', name: 'firerating', value: 'EI30' }, model, TABLE);
    expect(ok).toMatchObject({ ok: true, row: { pset: 'Pset_DoorCommon', name: 'FireRating', kind: 'text', old: null, value: 'EI30' } });
    expect(checkProposal({ id: 'd1', pset: 'Pset_DoorCommon', name: 'FireResistance', value: 'EI30' }, model, TABLE)).toMatchObject({ ok: false, why: expect.stringMatching(/has no property/) });
    expect(checkProposal({ id: 'w0', pset: 'Pset_DoorCommon', name: 'FireRating', value: 'EI30' }, model, TABLE)).toMatchObject({ ok: false, why: expect.stringMatching(/not for IfcWall/) });
    expect(checkProposal({ id: 'd1', pset: 'Pset_MyChecks', name: 'Checked', value: 'yes' }, model, TABLE)).toMatchObject({ ok: false, why: expect.stringMatching(/must not start with Pset_/) });
    expect(checkProposal({ id: 'd1', pset: 'Qto_DoorBaseQuantities', name: 'Width', value: 2 }, model, TABLE).ok).toBe(false);
    expect(checkProposal({ id: 'd1', pset: 'Lapin_Intent', name: 'x', value: 2 }, model, TABLE).ok).toBe(false);
    expect(checkProposal({ id: 'ghost', name: 'x', value: 2 }, model, TABLE).ok).toBe(false);
  });

  it('hold a value as its property\'s kind', () => {
    expect(checkProposal({ id: 'd1', pset: 'Pset_DoorCommon', name: 'IsExternal', value: 'yes' }, model, TABLE)).toMatchObject({ ok: true, row: { kind: 'bool', value: true } });
    expect(checkProposal({ id: 'd1', pset: 'Pset_DoorCommon', name: 'ThermalTransmittance', value: '1,4' }, model, TABLE)).toMatchObject({ ok: true, row: { kind: 'number', value: 1.4 } });
    expect(checkProposal({ id: 'd1', pset: 'Pset_DoorCommon', name: 'ThermalTransmittance', value: 'low' }, model, TABLE).ok).toBe(false);
    // The user's own set (or none): the value's kind.
    expect(checkProposal({ id: 'd1', pset: 'Acme_Checks', name: 'Checked', value: true }, model, TABLE)).toMatchObject({ ok: true, row: { pset: 'Acme_Checks', kind: 'bool' } });
    expect(checkProposal({ id: 'd1', name: 'Note', value: 'see detail 4' }, model, TABLE)).toMatchObject({ ok: true, row: { pset: null, kind: 'text' } });
  });

  it('gather into one changeset: a later value replaces an earlier one, an unchanged one is left out', () => {
    const row = (id: string, value: string) => (checkProposal({ id, pset: 'Pset_DoorCommon', name: 'FireRating', value }, model, TABLE) as unknown as { row: ChangeRow }).row;
    let rows = mergeRows([], [row('d1', 'EI30'), row('d2', 'EI30')]);
    rows = mergeRows(rows, [row('d1', 'EI60'), row('d3', 'EI15')]);
    expect(rows.map((r) => [r.id, r.value, r.old])).toEqual([['d1', 'EI60', null], ['d2', 'EI30', null]]);
    rows[1] = { ...rows[1], accepted: false };
    expect(toSteps(rows)).toEqual([{ command: 'element.setParam', args: { ids: ['d1'], name: 'FireRating', value: 'EI60', pset: 'Pset_DoorCommon' } }]);
  });
});

// ─── The chat loop against a fake Ollama ────────────────────────────────────

function streamOf(chunks: OllamaChunk[]): Response {
  const body = chunks.map((c) => JSON.stringify(c)).join('\n') + '\n';
  // Split mid-line, as a network would.
  const bytes = new TextEncoder().encode(body);
  const half = Math.floor(bytes.length / 2);
  return new Response(new ReadableStream({ start(c) { c.enqueue(bytes.slice(0, half)); c.enqueue(bytes.slice(half)); c.close(); } }), { status: 200 });
}

describe('the chat loop', () => {
  it('runs two rounds of tool calls, then streams the answer', async () => {
    const sent: { messages: { role: string; content: string; tool_name?: string }[]; tools: unknown[]; think: boolean; stream: boolean }[] = [];
    const answers = [
      [{ message: { role: 'assistant', content: '', tool_calls: [{ function: { name: 'list_elements', arguments: { class: 'IfcDoor', where: [{ field: 'Width', op: '>', value: 1 }] } } }] } }, { done: true }],
      [{ message: { role: 'assistant', content: '', tool_calls: [{ function: { name: 'propose_properties', arguments: { changes: [{ id: 'd2', pset: 'Pset_DoorCommon', name: 'FireRating', value: 'EI30' }] } } }] } }, { done: true }],
      [{ message: { role: 'assistant', content: 'One door is wider than 1 m; ' } }, { message: { role: 'assistant', content: 'EI30 is proposed for it.' } }, { done: true, eval_count: 20, eval_duration: 5e8 }],
    ];
    const fakeFetch = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
      expect(String(url)).toBe('http://127.0.0.1:11434/api/chat');
      sent.push(JSON.parse(String(init?.body)));
      return streamOf(answers[sent.length - 1] as OllamaChunk[]);
    });
    const events: TurnEvent[] = [];
    const added = await runTurn({
      transport: fetchTransport(fakeFetch as unknown as typeof fetch), url: 'http://127.0.0.1:11434', model: 'qwen3-coder:30b',
      messages: [{ role: 'system', content: SYSTEM_PROMPT }, { role: 'user', content: 'Doors wider than 1 m: give them FireRating EI30.' }],
      ctx: context(), onEvent: (e) => events.push(e),
    });
    expect(fakeFetch).toHaveBeenCalledTimes(3);
    expect(sent[0]).toMatchObject({ stream: true, think: false });
    expect(sent[0].tools).toHaveLength(TOOL_DEFS.length);
    // The second round carries the first tool's result; the third, the proposal's.
    const toolMsg = sent[1].messages.at(-1)!;
    expect(toolMsg).toMatchObject({ role: 'tool', tool_name: 'list_elements' });
    expect(json(toolMsg.content).total).toBe(1);
    expect(json(sent[2].messages.at(-1)!.content)).toMatchObject({ proposed: 1 });
    const tools = events.filter((e): e is Extract<TurnEvent, { type: 'tool' }> => e.type === 'tool');
    expect(tools.map((e) => e.outcome.summary)).toEqual(['Listed 1 of 1 · IfcDoor', 'Proposed 1 change(s)']);
    expect(tools[1].outcome.proposals?.[0]).toMatchObject({ id: 'd2', pset: 'Pset_DoorCommon', name: 'FireRating', value: 'EI30', accepted: true });
    expect(events.filter((e) => e.type === 'text').map((e) => (e as { delta: string }).delta).join('')).toBe('One door is wider than 1 m; EI30 is proposed for it.');
    expect(events.find((e) => e.type === 'stats')).toEqual({ type: 'stats', tokens: 20, seconds: 0.5 });
    expect(added.map((m) => m.role)).toEqual(['assistant', 'tool', 'assistant', 'tool', 'assistant']);
  });

  it('stops when asked, keeping what came', async () => {
    const ctrl = new AbortController();
    const fakeFetch = vi.fn(async () => { ctrl.abort(); return streamOf([{ message: { content: 'Partial' } }]); });
    const added = await runTurn({
      transport: fetchTransport(fakeFetch as unknown as typeof fetch), url: 'http://127.0.0.1:11434', model: 'm',
      messages: [{ role: 'user', content: 'hi' }], ctx: context(), onEvent: () => undefined, signal: ctrl.signal,
    });
    expect(fakeFetch).toHaveBeenCalledTimes(1);
    expect(added.length).toBeLessThanOrEqual(1);
  });

  it('leaves out a reasoning channel a model leaks into its answer', () => {
    expect(cleanText('Am găsit 3 uși.\n\nthought\n<channel|>Am identificat 3 uși.')).toBe('Am identificat 3 uși.');
    expect(cleanText('Plain answer')).toBe('Plain answer');
  });

  it('says Ollama\'s own error', async () => {
    const fakeFetch = vi.fn(async () => new Response(JSON.stringify({ error: 'model "x" not found' }), { status: 404 }));
    await expect(runTurn({
      transport: fetchTransport(fakeFetch as unknown as typeof fetch), url: 'http://127.0.0.1:11434', model: 'x',
      messages: [{ role: 'user', content: 'hi' }], ctx: context(), onEvent: () => undefined,
    })).rejects.toThrow('model "x" not found');
  });
});
