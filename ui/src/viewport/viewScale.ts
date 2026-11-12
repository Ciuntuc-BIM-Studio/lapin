/**
 * A view's scale changed from the view itself (forum #30, decision 492), as Revit's view control bar does: the
 * project's standard scales (Drafting standards → Project, `scaleChoices` of its standards), the view's own among them,
 * one typed. The view takes it as its settings give it (element.update, one undo step); a level's default plan, held
 * by no view, becomes the level's plan view at that scale (view.create, one step too).
 */

/** The scales offered: the project's, with the view's own where it is not one of them, smallest first. */
export function scaleOptions(choices: readonly number[], current: number): number[] {
  return [...new Set([...choices, current])].sort((a, b) => a - b);
}

/** A scale as typed (1:75, 75, 1/75, a decimal comma), or null when it is none or outside 1:1 to 1:10000. */
export function parseScale(text: string): number | null {
  const t = text.trim().replace(',', '.').replace(/^1\s*[:/]\s*/, '');
  if (!/^\d+(\.\d+)?$/.test(t)) return null;
  const n = Number(t);
  return n >= 1 && n <= 10000 ? n : null;
}

/** The command that gives a view a scale. */
export function scaleCommand(viewId: string, scale: number): { command: string; args: Record<string, unknown> } {
  if (viewId.startsWith('plan:')) return { command: 'view.create', args: { kind: 'plan', storeyId: viewId.slice(5), scale } };
  return { command: 'element.update', args: { ids: [viewId], attrs: { scale } } };
}
