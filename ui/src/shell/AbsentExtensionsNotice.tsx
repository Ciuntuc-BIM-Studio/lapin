/**
 * The notice of a project that holds data of extensions this Lapin does not run (docs/plan-extension-modules.md §4.4): what it
 * says, a button to the Extensions dialog (Installed when the extension is installed but off, else Browse), and a close that
 * is the only way it goes. State and wording: platform/extensions/absent.ts.
 */

import { X } from 'lucide-react';
import { absentNotice, dismissAbsentExtensions, requestExtensionsTab, useAbsentExtensions, type AbsentExtension } from '../platform/extensions/absent';
import { useExtensions } from '../platform/extensions/runtime';
import { useUi } from '../app/store';

/** Opens Extensions where the extension is to be found, and closes the notice. */
export function openAbsentExtensions(list: readonly AbsentExtension[], installedIds: readonly string[]): void {
  requestExtensionsTab(list.some((a) => installedIds.includes(a.id)) ? 'installed' : 'browse');
  useUi.setState({ dialog: 'extensions' });
  dismissAbsentExtensions();
}

/** The notice itself: what it says of `names`, and its two buttons. */
export function AbsentExtensionsBar({ names, onOpen, onClose }: { names: readonly string[]; onOpen: () => void; onClose: () => void }) {
  return (
    <div className="notice notice-status notice-problem notice-absent" role="alert" data-testid="absent-extensions">
      <span data-no-i18n>{absentNotice(names)}</span>
      <button type="button" className="btn small" data-action="absent-extensions-open" onClick={onOpen}>Open Extensions</button>
      <button type="button" className="icon" title="Close" data-action="absent-extensions-close" onClick={onClose}><X size={13} /></button>
    </div>
  );
}

export function AbsentExtensionsNotice() {
  const list = useAbsentExtensions((s) => s.list);
  const catalogue = useExtensions((s) => s.list);
  const installed = useExtensions((s) => s.installed);
  if (!list.length) return null;
  const nameOf = (id: string) => catalogue.find((m) => m.id === id)?.name ?? installed.find((x) => x.id === id)?.name ?? id;
  return <AbsentExtensionsBar names={list.map((a) => nameOf(a.id))} onOpen={() => openAbsentExtensions(list, installed.map((x) => x.id))} onClose={dismissAbsentExtensions} />;
}
