/**
 * AI code generation for Python notebook prompt cells using local Ollama models
 * (docs/plan-python-notebook.md). Engineered specifically so small local models
 * (e.g. gemma4:12b, qwen2.5-coder:7b/14b) produce reliable `lapinbim` code:
 *
 *   1. Proactive Smart Context Pack (contextPack.ts): bilingual RO+EN pre-retrieval
 *      of top 10 exact `lapin.*` signatures, top 2–3 verified cookbook recipes, live
 *      BIM model + selection attributes, and live Python kernel variables (`script.inspect`).
 *   2. Capability-aware tool loop: uses `search_catalogue` / `model_overview` only when
 *      the selected Ollama model supports tools (`/api/show` capabilities).
 *   3. Static pre-flight validator + 1-shot auto-repair (validate.ts): catches hallucinated
 *      command names or wrong keyword arguments and fixes them automatically before Run.
 *
 * Generated code is streamed into the target code cell and NEVER auto-executed.
 */

import { cleanText } from '../../assistant/loop';
import {
  checkStatus,
  defaultCodeModel,
  fetchTransport,
  shellTransport,
  transportFor,
  useAssistantSettings,
  type OllamaChunk,
  type OllamaMessage,
  type OllamaStatus,
  type OllamaToolCall,
  type OllamaTransport,
} from '../../assistant/ollama';
import { isDesktop } from '../../engine/files';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { type Catalogue } from './logic';
import { buildContextPack, matchCatalogue, type KernelVar } from './contextPack';
import { formatRepairPrompt, validateLapinCode, type ValidationIssue } from './validate';

const MAX_TOOL_ROUNDS = 2;

export const NOTEBOOK_SYSTEM_PROMPT = `You are a Python coding assistant inside Lapin, a BIM modeller built on IFC.
You write concise, runnable Python code for a notebook cell using the pre-bound \`lapin\` package (\`lapinbim\`).
Rules:
- Output ONLY valid Python code (no prose outside comments).
- \`lapin\` is already imported and bound in the notebook namespace.
- Units are metres and degrees.
- Always pass \`storey_id\` explicitly when creating elements (pick the storey with \`elevation == 0\` for Ground floor; \`storeys[0]\` may be Foundations at -1.5m).
- Use ONLY the exact \`lapin.<module>.<command>(...)\` signatures provided in the context (or found via \`search_catalogue\`). Never invent command or keyword argument names.
- Prefer variables already defined in the live Python kernel when relevant.
- Keep key dimensions as clean variables at the top of the cell so the cell is easy to tweak or convert to a Visual Graph.`;

const CODE_TOOLS = [
  {
    type: 'function',
    function: {
      name: 'search_catalogue',
      description: 'Search Lapin commands and queries to get exact Python function names and keyword arguments.',
      parameters: {
        type: 'object',
        properties: {
          query: { type: 'string', description: 'English or Romanian BIM keywords (e.g. "wall", "column", "slab", "door", "roof").' },
        },
        required: ['query'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'model_overview',
      description: 'Get the open BIM model summary: storeys (id, name, elevation), element counts by class, and current selection.',
      parameters: { type: 'object', properties: {} },
    },
  },
];

/** Search the engine's command catalogue (bilingual RO+EN) and format matching calls as Python signatures. */
export function searchCatalogue(catalogue: Catalogue | null, query: string, limit = 15): string {
  if (!catalogue) return JSON.stringify({ error: 'Command catalogue is not loaded yet.' });
  const matches = matchCatalogue(catalogue, query, limit);
  return JSON.stringify({ matches, total: matches.length });
}

/** Compact summary of the open BIM model for code generation. */
export function modelOverview(): string {
  const nodes = useSession.getState().nodes;
  const storeys: { id: string; name: string; elevation: unknown }[] = [];
  const counts: Record<string, number> = {};
  for (const n of nodes.values()) {
    counts[n.type] = (counts[n.type] ?? 0) + 1;
    if (n.type === 'IfcBuildingStorey') {
      storeys.push({ id: n.id, name: n.name, elevation: n.props.elevation ?? 0 });
    }
  }
  const ui = useUi.getState();
  const selection = ui.selected.length ? ui.selected.slice(0, 20) : ui.selection ? [ui.selection] : [];
  return JSON.stringify({
    storeys,
    classes: counts,
    selectionCount: ui.selected.length || (ui.selection ? 1 : 0),
    selectionSample: selection,
  });
}

/** Strip markdown code fences from an LLM response to extract clean Python code. */
export function extractPythonCode(raw: string): string {
  const cleaned = cleanText(raw).trim();
  const fenced = /```(?:python|py)?\s*\n([\s\S]*?)(?:\n```|$)/i.exec(cleaned);
  if (fenced) return fenced[1].replace(/\n+$/, '');
  return cleaned.replace(/^```(?:python|py)?\s*/i, '').replace(/\s*```$/, '');
}

/** Pick the active Ollama transport for the current window. */
export function ollamaTransport(): OllamaTransport {
  const kind = transportFor(typeof window !== 'undefined' ? window.location.origin : '', isDesktop());
  return kind === 'shell' ? shellTransport() : fetchTransport();
}

/** Probe Ollama status and installed models for the notebook toolbar. */
export async function probeOllama(): Promise<OllamaStatus> {
  const s = useAssistantSettings.getState();
  const model = s.codeModel ?? s.model;
  return checkStatus(ollamaTransport(), s.url, model);
}

export interface GenerateCodeOptions {
  prompt: string;
  catalogue: Catalogue | null;
  contextAbove?: string;
  kernelVars?: readonly KernelVar[];
  currentCode?: string;
  errorText?: string;
  model?: string | null;
  /** Whether the chosen model supports tool calling (null/undefined: unknown, assume true). */
  supportsTools?: boolean | null;
  url?: string;
  transport?: OllamaTransport;
  signal?: AbortSignal;
  onCode: (code: string) => void;
  onStatus?: (status: string) => void;
}

export interface GenerateCodeResult {
  code: string;
  model: string;
  tokens?: number;
  seconds?: number;
  repaired?: number;
  remainingIssues?: ValidationIssue[];
}

/**
 * Generate (or fix) Python code for a notebook cell using Ollama.
 * Injects the Smart Context Pack, runs optional tool lookup, validates against `session.catalogue`,
 * and performs 1-shot deterministic auto-repair if any hallucinated command or kwarg is found.
 */
export async function generateCellCode(opts: GenerateCodeOptions): Promise<GenerateCodeResult> {
  const settings = useAssistantSettings.getState();
  const url = opts.url ?? settings.url;
  const transport = opts.transport ?? ollamaTransport();
  let model = opts.model ?? settings.codeModel ?? settings.model;
  let supportsTools = opts.supportsTools;

  if (!model) {
    const status = await checkStatus(transport, url, null);
    if (!status.running) throw new Error(status.error || 'Ollama is not running.');
    model = defaultCodeModel(status.models);
    if (!model) throw new Error('No Ollama models installed. Run `ollama pull qwen2.5-coder` or `ollama pull gemma4:12b`.');
    if (supportsTools === undefined) supportsTools = status.tools;
  }

  const effectivePrompt = opts.errorText
    ? `${opts.prompt} ${opts.currentCode ?? ''} ${opts.errorText}`
    : opts.prompt;

  const pack = buildContextPack({
    prompt: effectivePrompt,
    catalogue: opts.catalogue,
    contextAbove: opts.contextAbove,
    kernelVars: opts.kernelVars,
  });

  const userParts: string[] = [];
  if (pack) userParts.push(pack);

  if (opts.errorText?.trim()) {
    userParts.push(`Current cell code:\n\`\`\`python\n${opts.currentCode ?? ''}\n\`\`\`\n\nError to fix:\n\`\`\`\n${opts.errorText.trim()}\n\`\`\``);
    userParts.push(`Task: Fix the error above. ${opts.prompt ? `Additional instruction: ${opts.prompt}` : ''}`);
  } else {
    if (opts.currentCode?.trim()) {
      userParts.push(`Existing code in target cell (update or replace as needed):\n\`\`\`python\n${opts.currentCode.trim()}\n\`\`\``);
    }
    userParts.push(`Task: ${opts.prompt.trim()}`);
  }

  const messages: OllamaMessage[] = [
    { role: 'system', content: NOTEBOOK_SYSTEM_PROMPT },
    { role: 'user', content: userParts.join('\n\n') },
  ];

  const useTools = supportsTools !== false;
  let finalCode = '';
  let tokens: number | undefined;
  let seconds: number | undefined;

  const maxRounds = useTools ? MAX_TOOL_ROUNDS : 1;
  for (let round = 0; round < maxRounds; round++) {
    if (opts.signal?.aborted) break;
    opts.onStatus?.(round === 0 ? `Generating with ${model}…` : `Refining with ${model}…`);
    let content = '';
    const calls: OllamaToolCall[] = [];
    let failed: string | null = null;

    const body: Record<string, unknown> = {
      model,
      messages,
      stream: true,
      think: false,
      options: { temperature: 0.15, num_ctx: 16384 },
    };
    if (useTools) body.tools = CODE_TOOLS;

    await transport.stream(
      url,
      '/api/chat',
      body,
      (c: OllamaChunk) => {
        if (c.error) { failed = c.error; return; }
        const m = c.message;
        if (m?.content) {
          content += m.content;
          finalCode = extractPythonCode(content);
          opts.onCode(finalCode);
        }
        if (m?.tool_calls?.length) calls.push(...m.tool_calls);
        if (c.done && c.eval_count && c.eval_duration) {
          tokens = c.eval_count;
          seconds = c.eval_duration / 1e9;
        }
      },
      opts.signal,
    );

    if (failed) throw new Error(failed);
    if (opts.signal?.aborted) break;
    if (!calls.length) {
      finalCode = extractPythonCode(content);
      opts.onCode(finalCode);
      break;
    }

    messages.push({ role: 'assistant', content, tool_calls: calls });
    for (const call of calls) {
      const fn = call.function.name;
      const args = typeof call.function.arguments === 'string'
        ? (() => { try { return JSON.parse(call.function.arguments) as Record<string, unknown>; } catch { return {}; } })()
        : (call.function.arguments ?? {});
      opts.onStatus?.(`Looking up ${fn}…`);
      let result = '{}';
      if (fn === 'search_catalogue') result = searchCatalogue(opts.catalogue, String(args.query ?? ''));
      else if (fn === 'model_overview') result = modelOverview();
      else result = JSON.stringify({ error: `Unknown tool: ${fn}` });
      messages.push({ role: 'tool', tool_name: fn, content: result });
    }
  }

  // If tool rounds exhausted without final code, ask once more with no tools.
  if (!opts.signal?.aborted && !finalCode) {
    let content = '';
    messages.push({ role: 'user', content: 'Output only the final Python code now, with no tool calls.' });
    await transport.stream(
      url,
      '/api/chat',
      { model, messages, stream: true, think: false, options: { temperature: 0.15, num_ctx: 16384 } },
      (c: OllamaChunk) => {
        if (c.message?.content) {
          content += c.message.content;
          finalCode = extractPythonCode(content);
          opts.onCode(finalCode);
        }
      },
      opts.signal,
    );
  }

  // Layer 4: Static pre-flight validation + 1-shot deterministic auto-repair.
  let repaired = 0;
  let remainingIssues: ValidationIssue[] = [];
  if (!opts.signal?.aborted && finalCode && opts.catalogue) {
    const issues = validateLapinCode(finalCode, opts.catalogue);
    if (issues.length > 0) {
      opts.onStatus?.(`Auto-repairing ${issues.length} API issue(s)…`);
      let repairedContent = '';
      const repairMessages: OllamaMessage[] = [
        { role: 'system', content: NOTEBOOK_SYSTEM_PROMPT },
        { role: 'user', content: formatRepairPrompt(finalCode, issues) },
      ];
      await transport.stream(
        url,
        '/api/chat',
        { model, messages: repairMessages, stream: true, think: false, options: { temperature: 0.1, num_ctx: 16384 } },
        (c: OllamaChunk) => {
          if (c.message?.content) {
            repairedContent += c.message.content;
            const candidate = extractPythonCode(repairedContent);
            if (candidate) opts.onCode(candidate);
          }
        },
        opts.signal,
      );
      if (repairedContent.trim()) {
        finalCode = extractPythonCode(repairedContent);
        opts.onCode(finalCode);
        repaired = issues.length;
        remainingIssues = validateLapinCode(finalCode, opts.catalogue);
      }
    }
  }

  return { code: finalCode, model, tokens, seconds, repaired, remainingIssues };
}
