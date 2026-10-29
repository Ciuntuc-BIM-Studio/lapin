/**
 * The assistant's conversation and its changeset (docs/plan-assistant.md), kept
 * while the panel is closed or switched away from; New chat starts over.
 * The tools read the session (the model as the engine last sent it) and ask
 * the engine for quantities and schedules; Apply writes the accepted values
 * as one engine edit (one undo step).
 */

import { create } from 'zustand';
import { engine, execute, query, useSession } from '../engine/session';
import { useUi } from '../app/store';
import { isDesktop } from '../engine/files';
import { mergeRows, toSteps, type ChangeRow, type StandardTable } from './changeset';
import { runTurn, systemPrompt } from './loop';
import { useLang } from '../i18n';
import { fetchTransport, shellTransport, transportFor, useAssistantSettings, type OllamaMessage, type OllamaTransport } from './ollama';
import type { QuantitySet, Table, ToolContext } from './tools';

/** A step the assistant took, shown in one line ("Listed 12 of 12 · IfcDoor"). */
export interface ToolTrace { name: string; summary: string; error?: boolean; /** The arguments as the model sent them, shown on hover. */ args?: string }

export interface ChatEntry {
  role: 'user' | 'assistant';
  text: string;
  tools: ToolTrace[];
  tables: Table[];
  /** Proposals this answer made (the changeset holds them). */
  proposals?: number;
  error?: string;
  /** Tokens a second, the last round's. */
  speed?: number;
  /** The whole turn, in seconds. */
  seconds?: number;
}

export interface AssistantState {
  entries: ChatEntry[];
  /** What Ollama is sent: the system prompt, then the conversation with the tools' results. */
  history: OllamaMessage[];
  busy: boolean;
  changes: ChangeRow[];
}

export const useAssistant = create<AssistantState>(() => ({ entries: [], history: [], busy: false, changes: [] }));

let controller: AbortController | null = null;
let standardTable: Promise<StandardTable> | null = null;

const transport = (): OllamaTransport => (transportFor(location.origin, isDesktop()) === 'shell' ? shellTransport() : fetchTransport());

/** The tools' view of this session. */
export function sessionContext(): ToolContext {
  const s = useSession.getState();
  return {
    model: { nodes: s.nodes, relations: s.relations },
    selection: useUi.getState().selected,
    standard: () => (standardTable ??= query<StandardTable>('params.standardSets').then((t) => {
      if (t) return t;
      standardTable = null; // asked again next time
      return { own: 'Lapin_Parameters', reserved: [], sets: [] };
    })),
    quantities: (ids) => engine.call<Record<string, QuantitySet[]>>('quantities.many', { ids }),
    scheduleCsv: (id) => query<string>('schedule.csv', { id }),
  };
}

function patchLast(f: (e: ChatEntry) => ChatEntry): void {
  const entries = [...useAssistant.getState().entries];
  entries[entries.length - 1] = f(entries[entries.length - 1]);
  useAssistant.setState({ entries });
}

/** Ask: the question goes to the model with the tools; its answer streams into the last entry. */
export async function send(text: string, deps: { transport?: OllamaTransport; ctx?: ToolContext } = {}): Promise<void> {
  const question = text.trim();
  const { url, model } = useAssistantSettings.getState();
  if (!question || useAssistant.getState().busy || !model) return;
  const s = useAssistant.getState();
  const language = ({ en: 'English', ro: 'Romanian', es: 'Spanish' } as const)[useLang.getState().lang];
  const history: OllamaMessage[] = s.history.length ? s.history : [{ role: 'system', content: systemPrompt(language) }];
  const asked: OllamaMessage = { role: 'user', content: question };
  useAssistant.setState({
    busy: true,
    entries: [...s.entries, { role: 'user', text: question, tools: [], tables: [] }, { role: 'assistant', text: '', tools: [], tables: [] }],
  });
  controller = new AbortController();
  const started = performance.now();
  // What the model says after a tool call starts a paragraph of its own.
  let afterTool = false;
  try {
    const added = await runTurn({
      transport: deps.transport ?? transport(), url, model, messages: [...history, asked], ctx: deps.ctx ?? sessionContext(), signal: controller.signal,
      onEvent: (e) => {
        if (e.type === 'text') { const gap = afterTool && useAssistant.getState().entries.at(-1)!.text ? '\n\n' : ''; afterTool = false; patchLast((x) => ({ ...x, text: x.text + gap + e.delta })); }
        else if (e.type === 'stats') patchLast((x) => ({ ...x, speed: e.seconds > 0 ? e.tokens / e.seconds : undefined }));
        else {
          const o = e.outcome;
          afterTool = true;
          patchLast((x) => ({
            ...x,
            tools: [...x.tools, { name: e.name, summary: o.summary, error: o.error, args: typeof e.args === 'string' ? e.args : JSON.stringify(e.args) }],
            tables: o.table && o.table.rows.length ? [...x.tables, o.table] : x.tables,
            proposals: o.proposals?.length ? (x.proposals ?? 0) + o.proposals.length : x.proposals,
          }));
          if (o.proposals?.length) useAssistant.setState((st) => ({ changes: mergeRows(st.changes, o.proposals!) }));
        }
      },
    });
    useAssistant.setState({ history: [...history, asked, ...added] });
  } catch (e) {
    patchLast((x) => ({ ...x, error: e instanceof Error ? e.message : String(e) }));
    useAssistant.setState({ history: [...history, asked] });
  } finally {
    patchLast((x) => ({ ...x, seconds: (performance.now() - started) / 1000 }));
    controller = null;
    useAssistant.setState({ busy: false });
  }
}

/** Stop the answer coming in (what came so far stays). */
export function stop(): void { controller?.abort(); }

/** A new conversation; the changeset stays until it is applied or rejected. */
export function newChat(): void {
  stop();
  useAssistant.setState({ entries: [], history: [] });
}

export function toggleChange(key: string, accepted?: boolean): void {
  useAssistant.setState((s) => ({ changes: s.changes.map((c) => (c.key === key ? { ...c, accepted: accepted ?? !c.accepted } : c)) }));
}

export function toggleAll(accepted: boolean): void {
  useAssistant.setState((s) => ({ changes: s.changes.map((c) => ({ ...c, accepted })) }));
}

/** Write the accepted values: one engine edit, one undo step. The changeset empties when it went through. */
export async function applyChanges(): Promise<boolean> {
  const steps = toSteps(useAssistant.getState().changes);
  if (!steps.length) return false;
  await execute('edit.batch', { steps });
  if (useSession.getState().error) return false;
  useAssistant.setState({ changes: [] });
  return true;
}

export function rejectChanges(): void { useAssistant.setState({ changes: [] }); }
