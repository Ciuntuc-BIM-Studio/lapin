/**
 * The example graphs bundled with the app (ui/src/modules/graphs/examples/*.graph.json): "New from example…" in the
 * Graphs panel saves a copy into the project. The files are loaded when one is asked for (a lazy import, so the web
 * and the desktop build alike); the engine's tests read the same files, so what is offered is what is tested.
 */

import type { GraphJson } from './graph';

export interface GraphExample {
  /** The file's name: examples/<id>.graph.json. */
  id: string;
  /** The graph's name in the project, and in the list. */
  name: string;
  description: string;
  /** A name scriptIcon knows (Lapin's own, or a lucide one). */
  icon: string;
}

export const EXAMPLES: GraphExample[] = [
  { id: 'twisted-tower', name: 'Twisted tower', description: 'Floors on a stack of storeys, each turned a little more than the one below, with columns; sliders for floors, height, twist, width and taper.', icon: 'building' },
  { id: 'columns-on-a-grid', name: 'Columns on a grid', description: 'A column at each point of a grid: how many each way, and the spacing.', icon: 'grid' },
  { id: 'walls-round-a-rectangle', name: 'Walls round a rectangle', description: 'Four walls round a rectangle of the width and depth you set.', icon: 'square' },
];

const files = import.meta.glob<GraphJson>('./examples/*.graph.json', { import: 'default' });

/** An example's graph, read when asked for. */
export async function loadExample(id: string): Promise<GraphJson> {
  const load = files[`./examples/${id}.graph.json`];
  if (!load) throw new Error(`There is no example graph "${id}".`);
  return structuredClone(await load());
}

/** The ids of the files bundled (for the tests that keep the list and the files in step). */
export const exampleFiles = (): string[] => Object.keys(files).map((p) => p.replace(/^\.\/examples\//, '').replace(/\.graph\.json$/, '')).sort();

/** A name not taken yet (compared without regard to case): the name itself, else "name 2", "name 3"… */
export function uniqueName(base: string, taken: Iterable<string>): string {
  const used = new Set([...taken].map((n) => n.toLowerCase()));
  if (!used.has(base.toLowerCase())) return base;
  let i = 2;
  while (used.has(`${base} ${i}`.toLowerCase())) i++;
  return `${base} ${i}`;
}
