import { execute } from '../engine/session';

export interface Step { command: string; args: Record<string, unknown> }

/**
 * One user action as one undo step: a single command goes as it is, several as one `edit.batch`. Answers the step results
 * (null when it failed; the session then holds the error). Steps cannot use an id an earlier step makes.
 */
export async function executeSteps(steps: readonly Step[]): Promise<unknown[] | null> {
  if (!steps.length) return [];
  if (steps.length === 1) { const r = await execute(steps[0].command, steps[0].args); return r === null ? null : [r]; }
  const r = await execute('edit.batch', { steps });
  return Array.isArray(r) ? r : null;
}
