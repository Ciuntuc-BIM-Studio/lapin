import { describe, expect, it, vi } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { checkProposal, liveRows, type ChangeRow, type StandardTable } from './changeset';
import { runTurn } from './loop';
import { fetchTransport, type OllamaChunk, type OllamaTransport } from './ollama';
import { runTool, type ToolContext } from './tools';
import { newChat, send, useAssistant } from './store';
import { useAssistantSettings } from './ollama';

const node = (id: string, type: string, name = id): NodeDto =>
  ({ id, type, name, parentId: null, px: 0, py: 0, props: {}, editable: true, module: 'architecture', storeyId: 's0' });
const TABLE: StandardTable = { own: 'Lapin_Parameters', reserved: [], sets: [] };
const ctxOf = (nodes: NodeDto[], extra: Partial<ToolContext> = {}): ToolContext => ({
  model: { nodes: new Map(nodes.map((n) => [n.id, n])), relations: new Map() }, selection: [],
  standard: async () => TABLE, quantities: async () => ({}), scheduleCsv: async () => null, ...extra,
});
const stream = (chunks: OllamaChunk[]) => new Response(chunks.map((c) => JSON.stringify(c)).join('\n') + '\n', { status: 200 });

describe('Stop in the middle of a round of tool calls', () => {
  it('answers every call asked for, so no tool call is left without its result', async () => {
    const ctrl = new AbortController();
    const calls = [{ function: { name: 'model_overview', arguments: {} } }, { function: { name: 'list_schedules', arguments: {} } }];
    const fakeFetch = vi.fn(async () => stream([{ message: { role: 'assistant', content: '', tool_calls: calls } }, { done: true }]));
    const added = await runTurn({
      transport: fetchTransport(fakeFetch as unknown as typeof fetch), url: 'http://127.0.0.1:11434', model: 'm',
      messages: [{ role: 'user', content: 'hi' }], ctx: ctxOf([node('w', 'IfcWall')]), signal: ctrl.signal,
      // Stop pressed while the first tool's result comes in.
      onEvent: (e) => { if (e.type === 'tool') ctrl.abort(); },
    });
    expect(added.map((m) => m.role)).toEqual(['assistant', 'tool', 'tool']);
    expect(added[2]).toMatchObject({ tool_name: 'list_schedules' });
    expect(JSON.parse(added[2].content)).toEqual({ error: 'Stopped by the user.' });
  });

  it('a tool measuring many elements gives up between its requests', async () => {
    const ctrl = new AbortController();
    const walls = Array.from({ length: 4500 }, (_, i) => node(`w${i}`, 'IfcWall'));
    const asked: number[] = [];
    const ctx = ctxOf(walls, { signal: ctrl.signal, quantities: async (ids) => { asked.push(ids.length); ctrl.abort(); return {}; } });
    const r = await runTool(ctx, 'list_elements', { class: 'IfcWall', where: [{ field: 'Length', op: '>', value: 1 }] });
    expect(asked).toEqual([2000]);
    expect(r.error).toBe(true);
    expect(JSON.parse(r.content).error).toBe('Stopped by the user.');
  });
});

describe('the tools’ results', () => {
  it('are JSON that reads, however long: never cut in the middle', async () => {
    const many = Array.from({ length: 3000 }, (_, i) => node(`s${i}`, 'LapinSchedule', `A schedule with a rather long name ${i}`));
    const r = await runTool(ctxOf(many), 'list_schedules', {});
    const back = JSON.parse(r.content) as { schedules: unknown[]; truncated?: boolean };
    expect(back.truncated).toBe(true);
    expect(back.schedules.length).toBeGreaterThan(0);
  });
});

describe('the changeset', () => {
  it('a set that is not text is no set (the model may send anything)', () => {
    const r = checkProposal({ id: 'd', pset: 5 as unknown as string, name: 'Note', value: 'x' }, ctxOf([node('d', 'IfcDoor')]).model, TABLE);
    expect(r).toMatchObject({ ok: true, row: { pset: null } });
  });
  it('keeps the rows whose element is still there, and says which went', () => {
    const row = (id: string) => ({ key: id, id } as ChangeRow);
    const { kept, dropped } = liveRows([row('a'), row('b'), row('c')], (id) => id !== 'b');
    expect(kept.map((r) => r.id)).toEqual(['a', 'c']);
    expect(dropped.map((r) => r.id)).toEqual(['b']);
  });
});

describe('New chat while an answer streams in', () => {
  it('is not undone by the turn it left behind', async () => {
    useAssistantSettings.setState({ url: 'http://127.0.0.1:11434', model: 'm' });
    let release: () => void = () => undefined;
    const transport: OllamaTransport = {
      json: async () => ({}),
      stream: async (_base, _path, _body, onChunk, signal) => {
        onChunk({ message: { role: 'assistant', content: 'Half an ans' } });
        await new Promise<void>((r) => { release = r; signal?.addEventListener('abort', () => r()); });
        onChunk({ message: { role: 'assistant', content: 'wer' } });
        onChunk({ done: true });
      },
    };
    const turn = send('How many walls?', { transport, ctx: ctxOf([]) });
    await Promise.resolve();
    newChat();
    release();
    await turn;
    const s = useAssistant.getState();
    expect(s.entries).toEqual([]);
    expect(s.history).toEqual([]);
    expect(s.busy).toBe(false);
  });

  it('a failed turn leaves no question without an answer in the conversation', async () => {
    newChat();
    const transport: OllamaTransport = { json: async () => ({}), stream: async () => { throw new Error('Ollama went away'); } };
    await send('Hello?', { transport, ctx: ctxOf([]) });
    const s = useAssistant.getState();
    expect(s.entries.at(-1)?.error).toBe('Ollama went away');
    expect(s.history.filter((m) => m.role === 'user')).toHaveLength(0);
  });
});
