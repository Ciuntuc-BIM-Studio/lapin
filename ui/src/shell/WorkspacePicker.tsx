/**
 * The workspaces (decision 243): the ribbon tabs a discipline works with, every tab, or the user's own,
 * chosen on the start screen and in View → Interface…; each shows the tabs it brings, in its order.
 * The project stays one: a hidden tab's tools are still in Commands… and the shortcuts.
 */

import { Building2, Compass, Layers, PenTool, Play, Route, Wrench } from 'lucide-react';
import { choosePreset, chooseWorkspace, useInterface, visibleTabIds, WORKSPACE_IDS, WORKSPACE_LABELS, type InterfacePreset, type WorkspaceId } from '../app/interface';
import { NAV_STYLE_IDS, NAV_STYLES, type NavStyleId } from '../app/navStyle';
import { setPrefs, usePrefs } from '../app/prefs';
import { launchRoleSandbox } from '../app/onboarding';
import { useSession } from '../engine/session';
import { ribbonTabs } from '../platform/modules';

const ROLES: { id: WorkspaceId; title: string; note: string; icon: typeof Building2 }[] = [
  { id: 'architecture', title: 'Architect', note: 'Walls, joinery, rooms, roofs & interior', icon: Building2 },
  { id: 'structure', title: 'Structural engineer', note: 'Grids, columns, beams, footings & rebar', icon: Layers },
  { id: 'site', title: 'Roads & site', note: 'Terrain, alignments, parcels & site works', icon: Route },
  { id: 'mep', title: 'MEP engineer', note: 'Pipes, ducts, equipment & systems', icon: Wrench },
  { id: 'drafting', title: 'Drafting 2D', note: 'CAD linework, dimensions & sheets', icon: PenTool },
  { id: 'all', title: 'All disciplines', note: 'Every tab & tool of this edition', icon: Compass },
];

const QUICK_PRESETS: { id: InterfacePreset; label: string }[] = [
  { id: 'flow', label: 'Flow' },
  { id: 'comfortable', label: 'Comfortable' },
  { id: 'compact', label: 'Compact' },
  { id: 'focus', label: 'Focus' },
];

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

/** The workspace as one labelled choice, for the start screen (the same store as the picker; View → Interface… shows each one's tabs). */
export function WorkspaceSelect() {
  const s = useInterface();
  const ids = WORKSPACE_IDS.filter((w) => w !== 'mine' || s.mine !== null || s.workspace === 'mine');
  return (
    <label className="start-workspace" data-field="workspace-select">
      <span>Workspace</span>
      <select value={s.workspace} onChange={(e) => chooseWorkspace(e.target.value as WorkspaceId)}>
        {ids.map((w) => <option key={w} value={w} data-workspace={w}>{WORKSPACE_LABELS[w]}</option>)}
      </select>
    </label>
  );
}

/** Game-like role selector for the start screen: picks discipline tabs, curated 1–9 hotbar, interface mode & mouse style. */
export function RoleSelector() {
  const s = useInterface();
  const navStyle = usePrefs((p) => p.navStyle);
  const connected = useSession((st) => st.connected);
  return (
    <section className="role-setup" aria-label="Choose your role" data-testid="role-selector">
      <div className="role-head">
        <p className="eyebrow">Your role</p>
        <div className="role-quick">
          <button type="button" className="role-sandbox-btn" disabled={!connected} data-testid="launch-sandbox"
            title="Start a ~70% pre-built mini-project with interactive missions for your role"
            onClick={() => void launchRoleSandbox(s.workspace)}>
            <Play size={12} /><span>Guided sandbox</span>
          </button>
          <div className="role-pills" role="radiogroup" aria-label="Interface mode">
            {QUICK_PRESETS.map((p) => (
              <button type="button" key={p.id} role="radio" aria-checked={s.preset === p.id} className={`role-pill${s.preset === p.id ? ' on' : ''}`}
                data-preset={p.id} onClick={() => choosePreset(p.id)}>{p.label}</button>
            ))}
          </div>
          <label className="role-nav" title="Mouse navigation style">
            <span className="muted small">Mouse</span>
            <select value={navStyle} aria-label="Mouse navigation style" onChange={(e) => setPrefs({ navStyle: e.target.value as NavStyleId })}>
              {NAV_STYLE_IDS.map((id) => <option key={id} value={id}>{NAV_STYLES[id].label}</option>)}
            </select>
          </label>
        </div>
      </div>
      <div className="role-cards" role="radiogroup" aria-label="Role">
        {ROLES.map(({ id, title, note, icon: Icon }) => {
          const on = s.workspace === id;
          return (
            <button type="button" key={id} role="radio" aria-checked={on} data-role={id} className={`role-card${on ? ' on' : ''}`}
              onClick={() => chooseWorkspace(id)}>
              <span className="role-icon"><Icon size={17} strokeWidth={1.6} /></span>
              <span className="role-body">
                <span className="role-title">{title}</span>
                <span className="muted small">{note}</span>
              </span>
            </button>
          );
        })}
      </div>
    </section>
  );
}
