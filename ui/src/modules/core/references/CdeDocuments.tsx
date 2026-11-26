/**
 * The CDE web's Documents tab (docs/plan-cde-references.md §5, stage R4): the project's reference documents on the server, read and
 * written directly, with no project open. The same list, folder tree and detail as Manage → Reference documents…, over the server
 * adapter. Viewers read; editors add and change their own; owners also delete and change the folders and types.
 */

import { useEffect, useState } from 'react';
import { account, getRules } from '../../../engine/atrium';
import type { Role } from '../../../engine/cdeServer';
import { useLang } from '../../../i18n';
import type { RefAdapter, RefPerms } from './adapter';
import { ReferencesWorkspace } from './ReferencesDialog';
import { folderLanguage } from './references';
import { serverAdapter, serverPerms } from './serverRefs';

export function CdeDocuments({ server, project, name }: { server: string; project: string; name?: string }) {
  const ui = useLang((s) => s.lang);
  const [who, setWho] = useState<{ role: Role; authorId: string | null } | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    void (async () => {
      try {
        const a = await account(server);
        const rules = await getRules(server, project).catch(() => null);
        const role = (rules?.role ?? a.projects.find((p) => p.id === project)?.role ?? 'viewer') as Role;
        if (live) setWho({ role, authorId: a.authorId ?? null });
      } catch (e) { if (live) setFailed(e instanceof Error ? e.message : String(e)); }
    })();
    return () => { live = false; };
  }, [server, project]);
  const [adapter, setAdapter] = useState<RefAdapter | null>(null);
  useEffect(() => { setAdapter(serverAdapter(project, folderLanguage(ui), name ?? project)); }, [project, ui, name]);

  if (failed) return <p className="muted small">{failed}</p>;
  if (!who || !adapter) return <p className="muted small">Loading…</p>;
  const perms: RefPerms = serverPerms(who.role, who.authorId);
  return <ReferencesWorkspace adapter={adapter} perms={perms} embedded />;
}
