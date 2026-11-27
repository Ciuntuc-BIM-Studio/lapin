/**
 * File → Extensions… (Lapin Extensions, docs/plan-extensions.md, stage 0): Browse the catalogue (search, categories, an
 * extension's details: author, licence, the Lapin versions it works with, what it holds, what a script asks for) and
 * install it; Installed lists what this computer has, to open where it went, update or uninstall. Content packs go into
 * Lapin's own libraries through their importers; nothing is run. The catalogue's address can be changed (a local index
 * while developing, a firm's own catalogue). Stage 1: how the catalogue is signed is said above the list (signed by Lapin
 * Extensions, or a catalogue of one's own left unsigned); the store's page of an extension is store.lapinbim.com.
 * Stage 2a (docs/plan-extensions.md §12): a paid extension is marked Paid with its price as indicative, sold by its author
 * through their shop (Buy opens the checkout, Seller's terms their terms); Install asks for the licence key once, which is
 * kept on this computer; a key its seller revoked gets no more updates, and says so.
 * Extension modules (docs/plan-extension-modules.md, S2): Installed shows each one's permission (it runs code with full access),
 * what the engine found at its start (refused, and why), an On switch, and Restart now when a change waits for the next start.
 * Revocation (§4.3): a version the catalogue withdrew says so in Installed with its reason (a module is turned off at the next
 * start; a content pack stays installed).
 */

import { confirmAction } from './prompts';
import { useEffect, useMemo, useState } from 'react';
import { Check, Download, ExternalLink, KeyRound, RefreshCw, Search, ShieldAlert, ShieldCheck, ShoppingCart, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { engine, engineInBrowser, useSession } from '../engine/session';
import { CATEGORY_ORDER, countByKind, filterCatalogue, sizeText, updatesOf, type InstalledExtension } from '../platform/extensions/install';
import { compareVersions, compatibility, isNonCommercial, isPaid, isPermissive, KIND_INFO, PERMISSION_LABELS, PROVIDERS, runnable, type ExtensionKind, type Manifest } from '../platform/extensions/manifest';
import { cleanKey, isRevoked, LICENCE_MESSAGES } from '../platform/extensions/licence';
import { FULL_ACCESS, isModuleKind, moduleStatus, refreshEngineModules, restartNow, setModuleEnabled, useEngineModules } from '../platform/extensions/moduleKind';
import { takeExtensionsTab } from '../platform/extensions/absent';
import { usePluginReport } from '../platform/extensions/plugins';
import { revocationOf } from '../platform/extensions/revoked';
import { saveProject } from '../engine/files';
import { appFacts, contentInstallers, DEFAULT_INDEX, forgetLicence, install, loadCatalogue, setCatalogueAddress, uninstall, useExtensions } from '../platform/extensions/runtime';

const close = () => useUi.setState({ dialog: null });
const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const when = (iso: string) => new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

/** An extension's page (its homepage, its forum topic), in the browser. */
function openPage(url: string) {
  if (engineInBrowser) { window.open(url, '_blank', 'noopener'); return; }
  void engine.call('support.open', { link: 'extensionPage', url }).catch(() => notify('The link could not be opened.'));
}

/** The updates offered: none for a paid extension whose key its seller revoked. */
const updatesHere = (installed: InstalledExtension[], list: Manifest[]) => {
  const licences = useExtensions.getState().licences;
  return updatesOf(installed, list, appFacts(), (id) => isRevoked(licences, id));
};

const REVOKED_NOTE = 'Its licence is no longer valid (refunded or disabled by its seller): it gets no more updates. What is installed stays.';

async function installIt(m: Manifest, update: boolean, key?: string) {
  try {
    const x = await install(m, key);
    const items = (x.receipt.ids as unknown[] | undefined)?.length;
    notify(update ? `${m.name} updated to ${m.version}.` : items != null ? `${m.name} installed: ${items} item(s) added.` : `${m.name} installed.`);
  } catch (e) { notify(message(e)); }
}

function Details({ m, installed }: { m: Manifest; installed?: InstalledExtension }) {
  const app = appFacts();
  const can = compatibility(m, app);
  const busy = useExtensions((s) => s.busy) === m.id;
  const info = KIND_INFO[m.kind];
  const licences = useExtensions((s) => s.licences);
  const newer = installed && updatesOf([installed], [m], app, (id) => isRevoked(licences, id)).length > 0;
  const permissive = isPermissive(m.license);
  const nonCommercial = isNonCommercial(m.license);
  const paid = isPaid(m);
  const rec = licences[m.id];
  const revoked = rec?.status === 'revoked';
  // A key is asked for a paid one without a good key kept, when there is something to install (not installed, or newer).
  const wantsInstall = !installed || compareVersions(m.version, installed.version) > 0;
  const asksKey = paid && (!rec || revoked) && wantsInstall;
  const [key, setKey] = useState('');
  const installWithKey = () => {
    const k = cleanKey(key);
    if (!k) { notify(LICENCE_MESSAGES.missing); return; }
    void installIt(m, !!installed, k);
  };
  return (
    <div className="ext-details" data-extension-details={m.id}>
      <div className="ext-title">
        <span className="ext-name" data-no-i18n>{m.name}</span>
        <span className="muted small" data-no-i18n>{m.version}</span>
        {paid && <span className="chip" data-testid="extension-paid" title="Sold by its author through their own shop; Lapin Extensions takes no commission">Paid</span>}
      </div>
      <p className="small" data-no-i18n>{m.summary}</p>
      {m.description && <p className="small muted ext-description" data-no-i18n>{m.description}</p>}
      <dl className="ext-facts">
        <dt>Kind</dt><dd>{info.label}</dd>
        <dt>Author</dt><dd data-no-i18n>{m.author.name}{m.author.github ? ` (@${m.author.github})` : ''}</dd>
        <dt>Licence</dt><dd><span data-no-i18n>{m.license}</span> {permissive ? <span className="chip">permissive</span> : <span className="chip warn" title="Read its licence before you use what it holds">read its terms</span>}{nonCommercial && <> <span className="chip warn" data-testid="extension-non-commercial" title="Its licence forbids commercial use: not for paid work">Non-commercial</span></>}</dd>
        {paid && <><dt>Price</dt><dd>{m.purchase.price ? <span data-no-i18n>{m.purchase.price}</span> : '–'} <span className="chip" title="The price is the seller's, shown at the checkout">indicative</span></dd></>}
        {paid && <><dt>Sold by</dt><dd data-no-i18n>{m.author.name} · {PROVIDERS[m.purchase.provider]}</dd></>}
        <dt>Works with</dt><dd><span data-no-i18n>Lapin {m.lapin}</span></dd>
        <dt>Size</dt><dd data-no-i18n>{sizeText(m.package.size)} · {m.contents.file}</dd>
        {!!m.languages?.length && <><dt>Languages</dt><dd data-no-i18n>{m.languages.join(', ')}</dd></>}
        <dt>SHA-256</dt><dd className="ext-sha" data-no-i18n title={m.package.sha256}>{m.package.sha256.slice(0, 16)}…</dd>
      </dl>
      {!!m.contents.items?.length && (
        <div className="ext-items"><span className="eyebrow">What it holds</span>
          <ul className="small" data-no-i18n>{m.contents.items.slice(0, 12).map((i) => <li key={i}>{i}</li>)}{m.contents.items.length > 12 && <li>…</li>}</ul>
        </div>
      )}
      {info.code && (
        <div className="ext-code small">
          {m.kind === 'module'
            ? <p><ShieldAlert size={13} /> An extension module runs code with everything you can do on this computer. Lapin Extensions reviewed its source and signed it; it is desktop only.</p>
            : <p><ShieldAlert size={13} /> A script can do on this computer what you can. Installing it runs nothing: it runs only when you press its tool in My tools, or Run script here, and asks you to trust it first.</p>}
          {!!m.permissions?.length && <><span className="eyebrow">It asks to</span><ul>{m.permissions.map((p) => <li key={p}>{PERMISSION_LABELS[p]}</li>)}</ul></>}
          {m.kind === 'script' && !runnable(m, app) && <p className="muted">Not runnable yet: Lapin has no Python console in this edition.</p>}
        </div>
      )}
      {paid && (
        <div className="ext-sale small" data-extension-sale={m.id}>
          <p>Sold by its author through their own shop: the seller takes the payment, issues the invoice and handles refunds. Lapin Extensions takes no commission.</p>
          {revoked && <p className="ext-revoked" role="note" data-testid="extension-revoked">{REVOKED_NOTE}</p>}
          <div className="row ext-links">
            <button type="button" className="btn small primary" data-action="extension-buy" onClick={() => openPage(m.purchase.checkoutUrl)}><ShoppingCart size={12} />Buy</button>
            <button type="button" className="btn small" data-action="extension-seller-terms" onClick={() => openPage(m.purchase.terms)}><ExternalLink size={12} />Seller&apos;s terms</button>
          </div>
          {asksKey && can.ok && (
            <>
              <label className="ext-key"><KeyRound size={13} /><span>Licence key</span>
                <input value={key} onChange={(e) => setKey(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') installWithKey(); }} placeholder="The licence key you received from the seller" data-field="extension-licence-key" autoComplete="off" spellCheck={false} />
              </label>
              <p className="muted">Lapin sends the key to the store, which checks it with the seller&apos;s shop; the key stays on this computer.</p>
            </>
          )}
        </div>
      )}
      {m.kind === 'priceList' && <p className="small muted">Its prices are the author's and indicative: check them against your own before you use them.</p>}
      {m.kind === 'types' && <p className="small muted">Its types go into the project open now (one step to undo): materials matched by name or made, a type already there the same reused. Installed → Add its types to this project brings them into another.</p>}
      <div className="row ext-links">
        {m.homepage && <button type="button" className="btn small" onClick={() => openPage(m.homepage!)}><ExternalLink size={12} />Homepage</button>}
        {m.forum && <button type="button" className="btn small" onClick={() => openPage(m.forum!)}><ExternalLink size={12} />Forum topic</button>}
        <span className="grow" />
        {!can.ok && <span className="small error-text" data-testid="extension-incompatible">{can.reason}</span>}
        {can.ok && installed && !newer && !asksKey && <span className="small muted"><Check size={12} /> Installed</span>}
        {can.ok && (asksKey ? (
          <button type="button" className="btn primary" data-action="extension-install" disabled={busy || !key.trim()} onClick={installWithKey}>
            <Download size={13} />{busy ? 'Installing…' : installed ? `Update to ${m.version}` : 'Install'}
          </button>
        ) : (!installed || newer) && (
          <button type="button" className="btn primary" data-action="extension-install" disabled={busy} onClick={() => void installIt(m, !!installed)}>
            <Download size={13} />{busy ? 'Installing…' : newer ? `Update to ${m.version}` : 'Install'}
          </button>
        ))}
      </div>
    </div>
  );
}

/** How the catalogue is signed (stage 1): by Lapin Extensions' key, or a catalogue of one's own left unsigned. */
function Trust() {
  const { trust, from, loading, error } = useExtensions();
  if (loading || error || !trust) return null;
  if (trust === 'signed') return <p className="small muted ext-trust" data-testid="extensions-signed"><ShieldCheck size={13} /> Signed by Lapin Extensions: Lapin checks the catalogue&apos;s signature and each file&apos;s SHA-256.</p>;
  if (from === DEFAULT_INDEX) return null;
  return <p className="small ext-trust warn" role="note" data-testid="extensions-unsigned"><ShieldAlert size={13} /> This catalogue is not signed by Lapin Extensions: install from it only if you trust who keeps it.</p>;
}

function Browse() {
  const { list, loading, error, installed } = useExtensions();
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState<ExtensionKind | null>(null);
  const [picked, setPicked] = useState<string | null>(null);
  const shown = useMemo(() => filterCatalogue(list, { search, kind }), [list, search, kind]);
  const counts = countByKind(list);
  const current = shown.find((m) => m.id === picked) ?? shown[0];
  return (
    <div className="ext-browse">
      <div className="ext-search"><Search size={14} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search extensions" data-field="extensions-search" /></div>
      <div className="tabs-inline ext-kinds">
        <button type="button" className={`tab-chip${kind === null ? ' on' : ''}`} onClick={() => setKind(null)}>All</button>
        {CATEGORY_ORDER.filter((k) => counts[k]).map((k) => (
          <button key={k} type="button" className={`tab-chip${kind === k ? ' on' : ''}`} data-extension-kind={k} onClick={() => setKind(k)}>{KIND_INFO[k].label} <span className="muted" data-no-i18n>{counts[k]}</span></button>
        ))}
      </div>
      <Trust />
      {loading && <p className="muted small">Reading the catalogue…</p>}
      {error && <p className="error-text small" role="alert">The catalogue could not be read: {error}</p>}
      {!loading && !error && list.length === 0 && <p className="muted small">The catalogue lists no extension yet.</p>}
      <div className="ext-split">
        <div className="ext-list" role="listbox" aria-label="Extensions">
          {shown.map((m) => (
            <button key={m.id} type="button" role="option" aria-selected={current?.id === m.id} className={`ext-row${current?.id === m.id ? ' on' : ''}`} data-extension={m.id} onClick={() => setPicked(m.id)}>
              <span className="ext-row-name" data-no-i18n>{m.name}</span>
              <span className="muted small"><span>{KIND_INFO[m.kind].label}</span><span data-no-i18n> · {m.author.name}</span>{isNonCommercial(m.license) && <> · <span className="ext-nc" title="Its licence forbids commercial use: not for paid work">Non-commercial</span></>}{isPaid(m) && <> · <span className="ext-paid" data-paid title="Sold by its author through their own shop; Lapin Extensions takes no commission">Paid</span>{m.purchase.price && <span data-no-i18n> {m.purchase.price}</span>}</>}</span>
              {installed.some((x) => x.id === m.id) && <Check size={13} className="ext-row-mark" />}
            </button>
          ))}
        </div>
        {current && <Details key={current.id} m={current} installed={installed.find((x) => x.id === current.id)} />}
      </div>
    </div>
  );
}

/** The work open before Lapin restarts: saved, or let go when the user says so. */
async function letWorkGo(): Promise<boolean> {
  if (!useSession.getState().doc?.dirty) return true;
  if (confirmAction('Save the changes to the project before Lapin restarts?')) return saveProject();
  return confirmAction('Restart without saving? The changes will be lost.');
}

/** An extension module's line in Installed: its permission, what this start made of it, and its On switch. */
function ModuleState({ x }: { x: InstalledExtension }) {
  const engineList = useEngineModules();
  const left = usePluginReport((s) => s.left.find((l) => l.id === x.id));
  const e = engineList.installed.find((m) => m.id === x.id);
  const status = moduleStatus(e, left?.reason);
  const toggle = async (on: boolean) => {
    try { await setModuleEnabled(x.id, on); } catch (err) { notify(message(err)); }
  };
  return (
    <>
      <span className="small ext-full-access" role="note" data-testid="module-full-access"><ShieldAlert size={12} /> {FULL_ACCESS}</span>
      {status && <span className={`small${status.problem ? ' ext-revoked' : ' muted'}`} role="note" data-testid="module-status">{status.text}{status.detail && <span data-no-i18n> {status.detail}</span>}</span>}
      {e && !e.pendingRemoval && (
        <label className="small ext-module-switch" data-action="extension-module-enabled">
          <input type="checkbox" checked={e.enabled} onChange={(ev) => void toggle(ev.target.checked)} /> On
        </label>
      )}
    </>
  );
}

function Installed() {
  const { installed, list, busy, licences, revoked } = useExtensions();
  const restart = useEngineModules((s) => s.restart);
  const hasModules = installed.some((x) => isModuleKind(x.kind));
  useEffect(() => { if (hasModules && !engineInBrowser) void refreshEngineModules(); }, [hasModules]);
  const updates = new Set(updatesHere(installed, list).map((m) => m.id));
  const app = appFacts();
  const forget = (x: InstalledExtension) => {
    if (!confirmAction('Forget the licence key kept for {name}? What is installed stays; an update will ask for the key again.', { name: x.name })) return;
    forgetLicence(x.id);
  };
  const remove = async (x: InstalledExtension) => {
    if (!confirmAction('Uninstall {name}? What it added to your libraries is taken away; what you made with it in your projects stays.', { name: x.name })) return;
    try { await uninstall(x.id); notify(`${x.name} uninstalled.`); } catch (e) { notify(message(e)); }
  };
  if (!installed.length) return <p className="muted small">No extension is installed on this computer yet: Browse lists what the catalogue has.</p>;
  const restartLapin = async () => { try { await restartNow(letWorkGo); } catch (e) { notify(message(e)); } };
  return (
    <div className="ext-installed">
      {restart && (
        <div className="ext-restart" role="note" data-testid="modules-restart">
          <span className="small">Changes to extension modules take effect when Lapin restarts.</span>
          <button type="button" className="btn small primary" data-action="extensions-restart" onClick={() => void restartLapin()}><RefreshCw size={12} />Restart now</button>
        </div>
      )}
      {[...installed].sort((a, b) => a.name.localeCompare(b.name)).map((x) => {
        const open = contentInstallers().get(x.kind)?.open;
        const next = list.find((m) => m.id === x.id);
        return (
          <div key={x.id} className="recent ext-installed-row" data-installed-extension={x.id}>
            <span className="recent-body">
              <span className="recent-name" data-no-i18n>{x.name}</span>
              <span className="muted small"><span>{KIND_INFO[x.kind]?.label ?? x.kind}</span><span data-no-i18n> · {x.version} · {x.manifest.license} · {when(x.at)}</span></span>
              {isModuleKind(x.kind) && <ModuleState x={x} />}
              {KIND_INFO[x.kind]?.code && !isModuleKind(x.kind) && !runnable(x.manifest, app) && <span className="muted small">Runs once the Python console is in Lapin.</span>}
              {!isModuleKind(x.kind) && (() => {
                const r = revocationOf(revoked, { id: x.id, version: x.version, package: x.manifest.package });
                return r && <span className="small ext-revoked" role="note" data-testid="installed-withdrawn">Withdrawn by Lapin Extensions; what is installed stays:<span data-no-i18n> {r.reason}</span></span>;
              })()}
              {isRevoked(licences, x.id) && <span className="small ext-revoked" role="note" data-testid="installed-revoked">{REVOKED_NOTE}</span>}
            </span>
            {licences[x.id] && <button type="button" className="btn small" data-action="extension-forget-key" onClick={() => forget(x)}><KeyRound size={12} />Forget licence key</button>}
            {open && <button type="button" className="btn small" data-action="extension-open" onClick={() => open.run(x)}>{open.label}</button>}
            {updates.has(x.id) && next && <button type="button" className="btn small primary" disabled={busy === x.id} onClick={() => void installIt(next, true)}>Update to <span data-no-i18n>{next.version}</span></button>}
            <button type="button" className="icon" title="Uninstall" data-action="extension-uninstall" disabled={busy === x.id} onClick={() => void remove(x)}><Trash2 size={14} /></button>
          </div>
        );
      })}
    </div>
  );
}

export function ExtensionsDialog() {
  const [tab, setTab] = useState<'browse' | 'installed'>(takeExtensionsTab);
  const { installed, list, chosen, from } = useExtensions();
  const connected = useSession((s) => s.connected);
  const [address, setAddress] = useState(chosen ?? '');
  const [editing, setEditing] = useState(false);
  useEffect(() => { if (connected) void loadCatalogue(); }, [connected, chosen]);
  useExtensions((s) => s.licences);
  const updates = updatesHere(installed, list).length;
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Extensions" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog extensions-dialog" data-dialog="extensions" style={{ width: 860 }}>
        <header className="dialog-head">
          <span className="panel-title">Extensions</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="tabs-inline">
            <button type="button" className={`tab-chip${tab === 'browse' ? ' on' : ''}`} data-tab-extensions="browse" onClick={() => setTab('browse')}>Browse</button>
            <button type="button" className={`tab-chip${tab === 'installed' ? ' on' : ''}`} data-tab-extensions="installed" onClick={() => setTab('installed')}>Installed <span className="muted" data-no-i18n>{installed.length}</span>{updates > 0 && <span className="badge" data-no-i18n>{updates}</span>}</button>
          </div>
          {tab === 'browse' ? <Browse /> : <Installed />}
        </div>
        <footer className="dialog-foot">
          {editing ? (
            <>
              <input className="grow ext-address" value={address} onChange={(e) => setAddress(e.target.value)} placeholder={DEFAULT_INDEX} data-field="extensions-index" aria-label="Catalogue address" />
              <button type="button" className="btn" data-action="extensions-index-set" onClick={() => { setCatalogueAddress(address); setEditing(false); }}>Apply</button>
            </>
          ) : (
            <>
              <button type="button" className="btn" title={from ?? undefined} onClick={() => setEditing(true)} data-action="extensions-catalogue">Catalogue…</button>
              <button type="button" className="icon" title="Read the catalogue again" onClick={() => void loadCatalogue()}><RefreshCw size={14} /></button>
              <span className="muted small grow">Made by their authors and checked by the catalogue&apos;s review; Lapin checks each file&apos;s SHA-256 and installs content only.</span>
            </>
          )}
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
