import { describe, expect, it } from 'vitest';
import { extractPythonCode, generateCellCode, searchCatalogue } from './ai';
import type { Catalogue } from './logic';
import type { OllamaChunk, OllamaTransport } from '../../assistant/ollama';

const CAT: Catalogue = {
  commands: [
    { name: 'architecture.addWall', label: 'Wall', module: 'architecture', args: [{ name: 'storeyId', type: 'str' }, { name: 'x1', type: 'float' }, { name: 'height', type: 'float' }] },
    { name: 'structure.addColumn', label: 'Column', module: 'structure', args: [{ name: 'x', type: 'float' }, { name: 'y', type: 'float' }] },
  ],
  queries: [
    { name: 'schedule.csv', module: 'core', args: [{ name: 'id', type: 'str' }] },
  ],
};

describe('notebook AI code generator (Ollama)', () => {
  it('searches the command catalogue and returns Python signatures', () => {
    const res = JSON.parse(searchCatalogue(CAT, 'wall')) as { matches: string[] };
    expect(res.matches).toHaveLength(1);
    expect(res.matches[0]).toBe('lapin.architecture.add_wall(storey_id: str, x1: float, height: float)');
  });

  it('auto-repairs hallucinated kwargs in 1 shot and works when supportsTools=false', async () => {
    let calls = 0;
    let sawToolsField = false;
    const fakeTransport: OllamaTransport = {
      async json() { return {}; },
      async stream(_base, _path, body, onChunk) {
        calls++;
        if ((body as Record<string, unknown>).tools !== undefined) sawToolsField = true;
        if (calls === 1) {
          // Small model hallucinates wrong kwarg `pos_x`
          onChunk({ message: { role: 'assistant', content: '```python\nlapin.structure.add_column(pos_x=2, y=0)\n```' }, done: true });
          return;
        }
        // Auto-repair pass fixes it to `x=2, y=0`
        onChunk({ message: { role: 'assistant', content: '```python\nlapin.structure.add_column(x=2, y=0)\n```' }, done: true });
      },
    };

    const res = await generateCellCode({
      prompt: 'Pune un stâlp',
      catalogue: CAT,
      model: 'gemma4:12b',
      supportsTools: false,
      url: 'http://127.0.0.1:11434',
      transport: fakeTransport,
      onCode: () => undefined,
    });

    expect(sawToolsField).toBe(false);
    expect(calls).toBe(2);
    expect(res.repaired).toBe(1);
    expect(res.remainingIssues).toEqual([]);
    expect(res.code).toBe('lapin.structure.add_column(x=2, y=0)');
  });

  it('extracts clean Python code from fenced markdown responses and leaked reasoning tags', () => {
    const raw = 'thought\nplan<channel|>```python\nx = 10\nprint(x)\n```';
    expect(extractPythonCode(raw)).toBe('x = 10\nprint(x)');
  });

  it('runs a tool call round and streams the generated code into onCode', async () => {
    let round = 0;
    const fakeTransport: OllamaTransport = {
      async json() { return {}; },
      async stream(_base, _path, _body, onChunk) {
        round++;
        if (round === 1) {
          const c: OllamaChunk = {
            message: { role: 'assistant', content: '', tool_calls: [{ function: { name: 'search_catalogue', arguments: { query: 'column' } } }] },
            done: true,
          };
          onChunk(c);
          return;
        }
        onChunk({ message: { role: 'assistant', content: '```python\nfor i in range(3):\n' } });
        onChunk({ message: { role: 'assistant', content: '    lapin.structure.add_column(x=i*4, y=0)\n```' }, done: true, eval_count: 24, eval_duration: 500_000_000 });
      },
    };

    const streamed: string[] = [];
    const res = await generateCellCode({
      prompt: 'Place 3 columns spaced by 4m',
      catalogue: CAT,
      model: 'qwen3-coder:30b',
      url: 'http://127.0.0.1:11434',
      transport: fakeTransport,
      onCode: (code) => streamed.push(code),
    });

    expect(round).toBe(2);
    expect(res.code).toBe('for i in range(3):\n    lapin.structure.add_column(x=i*4, y=0)');
    expect(res.tokens).toBe(24);
    expect(streamed.length).toBeGreaterThan(0);
  });
});
