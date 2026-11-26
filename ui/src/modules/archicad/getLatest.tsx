/**
 * Get latest from Archicad (docs/plan-archicad.md A2), from File → Collaborate and the status bar: the engine's `archicad.update`, one
 * undo step (only what changed in Archicad since the last receive, else the whole model compared), its outcome as a notice.
 */

import { useEffect } from 'react';
import { notify, notifyProblem, useUi } from '../../app/store';
import { begin, end } from '../../engine/ifcImport';
import { engine } from '../../engine/session';
import { lastPort, update, updatedSummary, type ArchicadReceived } from './archicad';

export async function getLatest(): Promise<ArchicadReceived | null> {
  begin('Getting the latest from Archicad', 'Asking Archicad what changed', () => { void engine.call('cancel.long', {}).catch(() => undefined); });
  try {
    const r = await update(lastPort());
    notify(updatedSummary(r));
    return r;
  } catch (e) {
    notifyProblem(e instanceof Error ? e.message : String(e));
    return null;
  } finally { end(); }
}

/** File → Collaborate → Get latest from Archicad: opened as a dialog, it closes at once and runs the update (its progress in the busy overlay). */
export function GetLatestRun() {
  useEffect(() => { useUi.setState({ dialog: null }); void getLatest(); }, []);
  return null;
}
