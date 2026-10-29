/**
 * One turn of the assistant's chat (docs/plan-assistant.md): the user's words
 * to the local model with the tools, its tool calls run (tools.ts) and their
 * results sent back, until it answers in words. The answer streams in; Stop
 * aborts it. The loop runs here, in the interface; Ollama only sees the
 * conversation and the tools' results.
 */

import type { OllamaChunk, OllamaMessage, OllamaToolCall, OllamaTransport } from './ollama';
import { runTool, TOOL_DEFS, type ToolContext, type ToolOutcome } from './tools';

/** The most rounds of tool calls one question may take. */
export const MAX_STEPS = 8;

export const SYSTEM_PROMPT = `You are the model assistant inside Lapin, a BIM modeller built on IFC. You read and analyse the open project, make short reports, and propose property values for its elements.
Rules:
- Learn about the project only through the tools. Never invent elements, ids, names, numbers or property values. If a tool cannot tell you something, say so.
- Every count, sum, length, area or average comes from the aggregate tool (or list_elements totals). Quote its numbers; never do arithmetic yourself.
- You cannot create, move, delete or reshape anything, and you write no code. To set properties, call propose_properties: the user reviews and applies them. Never say they are applied.
- Standard IFC property sets (Pset_…) only with their own property names: check property_names first. A set of the user's own never starts with Pset_.
- Lengths are in metres, areas in m², volumes in m³.
- When a result says truncated or gives next_offset, page with offset or narrow the filter.
- Answer briefly. Reply in the same language as the user's message (a question in English gets an English answer, in Romanian a Romanian one). Use a short list or table when it helps.`;

/** The system prompt with the interface's language, the fallback when a message's language is unclear. */
export const systemPrompt = (language: string) => `${SYSTEM_PROMPT}\nThe interface is in ${language}; when the user's language is unclear, use ${language}.`;

export type TurnEvent =
  | { type: 'text'; delta: string }
  | { type: 'tool'; name: string; args: unknown; outcome: ToolOutcome }
  | { type: 'stats'; tokens: number; seconds: number };

export interface TurnOptions {
  transport: OllamaTransport;
  url: string;
  model: string;
  /** The conversation so far, the system prompt first, the user's new message last. */
  messages: OllamaMessage[];
  ctx: ToolContext;
  onEvent: (e: TurnEvent) => void;
  signal?: AbortSignal;
  maxSteps?: number;
}

/**
 * An answer without what some models leak of their reasoning channel even
 * with thinking off (gemma4 writes "thought" then "<channel|>" before its answer):
 * only what follows the last channel mark.
 */
export function cleanText(text: string): string {
  const i = text.lastIndexOf('<channel|>');
  return i >= 0 ? text.slice(i + '<channel|>'.length).trimStart() : text;
}

/** The request body for one round. */
export function chatBody(model: string, messages: OllamaMessage[]) {
  return {
    model,
    messages,
    tools: TOOL_DEFS,
    stream: true,
    think: false,
    options: { temperature: 0.1, num_ctx: 16384 },
  };
}

/** Run the turn; the messages it adds to the conversation (the model's, the tools'), in order. */
export async function runTurn(o: TurnOptions): Promise<OllamaMessage[]> {
  const added: OllamaMessage[] = [];
  const steps = o.maxSteps ?? MAX_STEPS;
  for (let step = 0; step < steps; step++) {
    if (o.signal?.aborted) return added;
    let content = '';
    const calls: OllamaToolCall[] = [];
    let failed: string | null = null;
    await o.transport.stream(o.url, '/api/chat', chatBody(o.model, [...o.messages, ...added]), (c: OllamaChunk) => {
      if (c.error) { failed = c.error; return; }
      const m = c.message;
      if (m?.content) { content += m.content; o.onEvent({ type: 'text', delta: m.content }); }
      if (m?.tool_calls?.length) calls.push(...m.tool_calls);
      if (c.done && c.eval_count && c.eval_duration) o.onEvent({ type: 'stats', tokens: c.eval_count, seconds: c.eval_duration / 1e9 });
    }, o.signal);
    if (failed) throw new Error(failed);
    if (o.signal?.aborted) { if (content) added.push({ role: 'assistant', content }); return added; }
    if (!calls.length) { added.push({ role: 'assistant', content: cleanText(content) }); return added; }
    added.push({ role: 'assistant', content, tool_calls: calls });
    for (const call of calls) {
      if (o.signal?.aborted) return added;
      const outcome = await runTool(o.ctx, call.function.name, call.function.arguments);
      o.onEvent({ type: 'tool', name: call.function.name, args: call.function.arguments, outcome });
      added.push({ role: 'tool', tool_name: call.function.name, content: outcome.content });
    }
  }
  // Too many rounds: the model is asked for its answer with what it has.
  const last = 'Stop calling tools now and answer with what you have.';
  let content = '';
  await o.transport.stream(o.url, '/api/chat', { ...chatBody(o.model, [...o.messages, ...added, { role: 'user', content: last }]), tools: [] }, (c) => {
    if (c.message?.content) { content += c.message.content; o.onEvent({ type: 'text', delta: c.message.content }); }
  }, o.signal);
  added.push({ role: 'assistant', content });
  return added;
}
