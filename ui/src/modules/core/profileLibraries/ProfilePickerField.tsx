/**
 * A profile chosen from the libraries (docs/plan-profile-libraries.md): typed to search every library at once — the
 * project's copies first, then Lapin's, this computer's and the firm's folders — by name, family, standard or kind; a
 * kind filter where only some fit (a hall's frame wants I sections). The list is grouped under headings, the project's
 * copies first, then each standard (EN, AISC / ASTM, a firm's own; decision 457): only those the project offers, and of
 * them those the user has not hidden with the chips at its top (kept in the preferences). `onPick` gets the profile and its library's name, for
 * the engine to copy into the project with the type made of it (`profile`, `library` in the type commands).
 */

import { Fragment, useMemo, useState } from 'react';
import { useSession } from '../../../engine/session';
import { setPrefs, usePrefs } from '../../../app/prefs';
import { groupFound, massOf, naturalCompare, projectStandardsOf, search, standardsOf, type LibraryEntry, type ProfileJson } from './model';
import { useProfileLibraries } from './store';

/** The project's copies as a library entry of their own. */
export function projectEntry(nodes: Map<string, { type: string; name: string; props: Record<string, unknown> }>): LibraryEntry {
  const profiles = [...nodes.values()].filter((n) => n.type === 'LapinSectionProfile')
    .map((n) => ({ ...(n.props.profile as unknown as ProfileJson), library: n.props.library as string | undefined }))
    .filter((p) => p && typeof p.name === 'string')
    .sort((a, b) => naturalCompare(a.name, b.name));
  return { key: 'project', origin: 'project', name: 'In the project', library: { name: 'In the project', profiles } };
}

export function ProfilePickerField({ kinds, onPick, testId, placeholder, value }: {
  kinds?: readonly string[]; onPick: (p: ProfileJson, library: string | null) => void; testId?: string; placeholder?: string; value?: string | null;
}) {
  const nodes = useSession((s) => s.nodes);
  const { entries, loading } = useProfileLibraries();
  const hidden = usePrefs((s) => s.profileStandardsHidden);
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const all = useMemo(() => [projectEntry(nodes as never), ...entries], [nodes, entries]);
  // The standards the project offers (all when it says none), each shown or hidden for this user by its chip.
  const offered = useMemo(() => projectStandardsOf(nodes as never), [nodes]);
  const standards = useMemo(() => standardsOf(entries).filter((g) => !offered || offered.includes(g)), [entries, offered]);
  const groups = useMemo(() => groupFound(search(all, text, { kinds }, 2000), offered, hidden, 80), [all, text, kinds, offered, hidden]);
  const toggle = (g: string) => setPrefs({ profileStandardsHidden: hidden.includes(g) ? hidden.filter((x) => x !== g) : [...hidden, g] });
  const pick = (entry: LibraryEntry, p: ProfileJson) => {
    const library = entry.origin === 'project' ? ((p as ProfileJson & { library?: string }).library ?? null) : entry.name;
    const profile = { ...p } as ProfileJson & { library?: string };
    delete profile.library;
    onPick(profile, library);
    setText(''); setOpen(false);
  };
  return (
    <div className="profile-picker" data-field={testId ?? 'library-profile'}>
      <input type="search" value={open ? text : text || (value ?? '')} placeholder={placeholder ?? 'Search the profile libraries (IPE 300, HEB, W14, RHS 200…)'}
        onFocus={() => setOpen(true)} onBlur={() => setTimeout(() => setOpen(false), 200)} onChange={(e) => { setText(e.target.value); setOpen(true); }}
        data-testid={`${testId ?? 'library-profile'}-search`} />
      {open && (
        <ul className="profile-picker-list" role="listbox">
          {standards.length > 1 && (
            <li className="profile-standards" role="presentation" title="The standards shown: a click hides or shows one (kept for you, in every project)" data-testid="profile-picker-standards" onMouseDown={(e) => e.preventDefault()}>
              {standards.map((g) => (
                <button key={g} type="button" className={`chip${hidden.includes(g) ? '' : ' on'}`} aria-pressed={!hidden.includes(g)} data-standard={g} onClick={() => toggle(g)} data-no-i18n>{g}</button>
              ))}
            </li>
          )}
          {loading && <li className="muted small">Reading the libraries…</li>}
          {!loading && groups.length === 0 && <li className="muted small">No profile matches: import a library in Manage → Profile libraries.</li>}
          {groups.map((g) => (
            <Fragment key={g.project ? '#project' : g.group}>
              <li className="profile-group-head" role="presentation" data-group={g.project ? 'project' : g.group} onMouseDown={(e) => e.preventDefault()}>
                {g.project ? <span>In the project</span> : <span data-no-i18n>{g.group}</span>}
              </li>
              {g.items.map(({ entry, profile }) => (
                <li key={`${entry.key}/${profile.name}`} role="option" aria-selected={false} onMouseDown={(e) => { e.preventDefault(); pick(entry, profile); }} data-profile={profile.name}>
                  <span data-no-i18n>{profile.name}</span>
                  <span className="muted small" data-no-i18n>{[profile.family, massOf(profile) != null ? `${massOf(profile)!.toFixed(1)} kg/m` : null].filter(Boolean).join(' · ')}</span>
                  <span className="grow" />
                  {entry.origin !== 'project' && entry.name !== g.group && <span className="muted small" data-no-i18n>{entry.name}</span>}
                </li>
              ))}
            </Fragment>
          ))}
        </ul>
      )}
    </div>
  );
}
