/**
 * The workspaces (decision 243): the ribbon tabs a discipline works with, every tab, or the user's own,
 * chosen on the start screen and in View → Interface…; each shows the tabs it brings, in its order.
 * The project stays one: a hidden tab's tools are still in Commands… and the shortcuts.
 */

import { chooseWorkspace, useInterface, visibleTabIds, WORKSPACE_IDS, WORKSPACE_LABELS } from '../app/interface';
import { ribbonTabs } from '../platform/modules';

/** The workspaces as one choice, each with the tabs it shows here; the user's own only once it is made. */
export function WorkspacePicker({ compact = false }: { compact?: boolean }) {
  const s = useInterface();
  const all = ribbonTabs();
  const label = (id: string) => all.find((t) => t.id === id)?.label ?? id;
  const ids = WORKSPACE_IDS.filter((w) => w !== 'mine' || s.mine !== null || s.workspace === 'mine');
  return (
    <div className={`seg workspaces${compact ? ' small' : ''}`} role="radiogroup" aria-label="Workspace" data-field="workspace">
      {ids.map((w) => {
        const tabs = visibleTabIds({ ...s, workspace: w }, all);
        return (
          <button type="button" key={w} role="radio" data-workspace={w} aria-checked={s.workspace === w} className={`seg-item${s.workspace === w ? ' on' : ''}`} onClick={() => chooseWorkspace(w)}>
            <b>{WORKSPACE_LABELS[w]}</b>
            {!compact && <span className="muted small">{w === 'all' ? 'Every tab of this edition' : tabs.filter((t) => t !== 'view').map(label).join(' · ')}</span>}
          </button>
        );
      })}
    </div>
  );
}
