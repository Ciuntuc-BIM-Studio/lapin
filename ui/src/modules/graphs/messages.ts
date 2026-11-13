/**
 * What the graph editor's messages console says (decision 538): the rows one evaluation makes (each node's error or warning
 * with its label and the reason, nodes left without an input, limits reached, a line for the timing), and the Bake's.
 * Pure: the store feeds them to the log (platform/messages.ts). One evaluation's rows are one group: the next evaluation
 * replaces them (a row that stays keeps its time and is not told again), so evaluating on every change never floods the log.
 */

import type { MessageInput } from '../../platform/messages';
import type { Evaluation } from './store';
import type { GraphJson, GraphNode } from './graph';

/** The group of an evaluation's rows. */
export const EVALUATION = 'evaluation';
/** The calls of commands one evaluation may make (engine GraphLimits.MaxCalls). */
export const MAX_CALLS = 2000;

/** A node's engine path (`group/node`) as the labels it has in the graph, joined by " / "; the id itself when it is not found. */
export function pathLabel(graph: GraphJson, path: string, kindLabel: (kind: string) => string): string {
  const names: string[] = [];
  let nodes: GraphNode[] | undefined = graph.nodes;
  for (const id of path.split('/')) {
    const n: GraphNode | undefined = nodes?.find((x) => x.id === id);
    if (!n) { names.push(id); nodes = undefined; continue; }
    names.push(n.label || kindLabel(n.kind));
    nodes = n.graph?.nodes;
  }
  return names.join(' / ');
}

/** A node which failed because an input it is wired to has nothing (the engine's message), not because of its own. */
export const isMissingInput = (message: string): boolean => /^The input .* has nothing/.test(message) || /^Not reached/.test(message);

const rank = (m: MessageInput) => (m.kind === 'error' ? 0 : 1);

/** The rows of one evaluation: the nodes' errors (the root causes first), warnings and the nodes with nothing to work on, the limits, and the timing. */
export function evaluationMessages(result: Evaluation | null, graph: GraphJson, kindLabel: (kind: string) => string): MessageInput[] {
  if (!result) return [];
  const rows: MessageInput[] = [];
  for (const [path, r] of Object.entries(result.nodes)) {
    const source = pathLabel(graph, path, kindLabel);
    const target = { node: path.split('/')[0] };
    if (r.error) rows.push({ kind: isMissingInput(r.error) ? 'warning' : 'error', source, text: r.error, target, sourcePlain: true, group: EVALUATION });
    else if (r.warning) rows.push({ kind: 'warning', source, text: r.warning, target, sourcePlain: true, group: EVALUATION });
  }
  rows.sort((a, b) => rank(a) - rank(b));
  if (result.ghost?.cut) rows.push({ kind: 'warning', source: 'Preview', text: 'The preview was cut: the ghost holds too many coordinates.', group: EVALUATION });
  if (result.calls >= MAX_CALLS) rows.push({ kind: 'warning', source: 'Preview', text: 'One evaluation reached its limit of command calls.', group: EVALUATION });
  if (typeof result.ms === 'number' && typeof result.calls === 'number') rows.push({ kind: 'info', source: 'Preview', text: `Commands: ${result.calls}, ${Math.round(result.ms)} ms`, group: EVALUATION });
  return rows;
}

/** The Bake's row: what it made, or why it was refused. */
export function bakeMessage(made: number | null, refusal: string | null): MessageInput {
  return made !== null ? { kind: 'info', source: 'Bake', text: `Elements made: ${made}, one undo step` } : { kind: 'error', source: 'Bake', text: refusal || 'The graph was not baked.' };
}
