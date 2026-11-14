/**
 * The SDNF import's protocol (docs/sdnf.md, report #148), shown when an import ends: what came in, each packet's count,
 * what was left out and why, what was changed, the notes and the time; or, when the file could not be read, why and
 * where (its line and packet). Copy puts the protocol on the clipboard as text, Save writes it to a file.
 */

import { create } from 'zustand';
import { Copy, Save, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { saveFile } from '../../engine/files';
import { durationText, groupItems, PACKET_KINDS, plainNotes, reportFileName, sdnfReportText, sdnfSummary, type SdnfGroup, type SdnfImportReport } from './sdnfReport';

interface Shown { name: string; report: SdnfImportReport | null; error: string | null; totalMs: number; at: Date }

/** The import the dialog shows. */
export const useSdnfReport = create<{ shown: Shown | null }>(() => ({ shown: null }));

/** Opens the protocol of an import that ended (`report`) or failed (`error`). */
export function showSdnfReport(name: string, report: SdnfImportReport | null, error: string | null, totalMs: number): void {
  useSdnfReport.setState({ shown: { name, report, error, totalMs, at: new Date() } });
  useUi.setState({ dialog: 'sdnfReport' });
}

function failureText(s: Shown): string {
  return `SDNF import protocol — ${s.name}\nDate: ${s.at.toISOString().replace('T', ' ').slice(0, 19)} UTC\nTime: ${durationText(s.totalMs)}\n\nNot imported: ${s.error}\n`;
}

function Groups({ head, groups, kind }: { head: string; groups: SdnfGroup[]; kind: 'skips' | 'warnings' }) {
  if (!groups.length) return null;
  return (
    <>
      <p className="eyebrow">{head}</p>
      <ul className="sdnf-groups" data-field={`sdnf-${kind}`}>
        {groups.map((g) => (
          <li key={g.reason}>
            <span data-no-i18n>{g.reason}</span> <span className="num" data-no-i18n>({g.count})</span>
            <div className="muted small" data-no-i18n>{groupItems(g)}</div>
          </li>
        ))}
      </ul>
    </>
  );
}

export function SdnfReportDialog() {
  const shown = useSdnfReport((s) => s.shown);
  const close = () => useUi.setState({ dialog: null });
  if (!shown) return null;
  const r = shown.report;
  const text = r ? sdnfReportText(r, shown.totalMs, shown.at) : failureText(shown);
  const copy = async () => {
    try { await navigator.clipboard.writeText(text); notify('The protocol is copied.'); } catch { notify('Copy failed.'); }
  };
  const save = async () => { await saveFile(new TextEncoder().encode(text), reportFileName(shown.name), 'text/plain'); };
  const notes = r ? plainNotes(r) : [];
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="SDNF import">
      <div className="dialog wide" data-dialog="sdnf-report">
        <header className="dialog-head">
          <span className="panel-title">SDNF import</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          {!r && (
            <>
              <p className="error-text" role="alert" data-field="sdnf-error" data-no-i18n>{shown.error}</p>
              <p className="muted small">Nothing of the file was imported. Correct the record named above (or ask for the file again from the program that wrote it) and import it again.</p>
            </>
          )}
          {r && (
            <>
              <p data-field="sdnf-summary" data-no-i18n>{sdnfSummary(r)}</p>
              <p className="muted small"><span>Time</span>: <span className="num" data-no-i18n>{durationText(shown.totalMs)}</span>{r.lines ? <> · <span className="num" data-no-i18n>{r.lines.toLocaleString()}</span> <span>lines</span></> : null}</p>
              {!!r.packets?.length && (
                <table className="layer-table sdnf-packets" data-field="sdnf-packets">
                  <thead><tr><th>Packet</th><th>Holds</th><th>Read</th><th>Imported</th></tr></thead>
                  <tbody>
                    {r.packets.map((p) => (
                      <tr key={`${p.packet}-${p.line}`}>
                        <td className="num" data-no-i18n>{p.packet}</td>
                        <td>{PACKET_KINDS[p.packet] ?? 'not defined by SDNF (not imported)'}</td>
                        <td className="num" data-no-i18n>{p.announced !== p.read ? `${p.read} / ${p.announced}` : p.read}</td>
                        <td className="num" data-no-i18n>{p.imported}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <Groups head="Left out" groups={r.skips ?? []} kind="skips" />
              <Groups head="Changed" groups={r.warnings ?? []} kind="warnings" />
              {r.unknownProfiles.length > 0 && (
                <>
                  <p className="eyebrow">Profiles in no library, drawn as placeholders</p>
                  <p className="small" data-no-i18n>{r.unknownProfiles.join(', ')}</p>
                </>
              )}
              {notes.length > 0 && (
                <>
                  <p className="eyebrow">Notes</p>
                  <ul className="sdnf-notes">{notes.map((n, i) => <li key={i} className="small" data-no-i18n>{n}</li>)}</ul>
                </>
              )}
            </>
          )}
          <div className="row">
            <span className="grow" />
            <button type="button" className="btn" data-action="sdnf-report-copy" onClick={() => void copy()}><Copy size={14} />Copy</button>
            <button type="button" className="btn" data-action="sdnf-report-save" onClick={() => void save()}><Save size={14} />Save…</button>
            <button type="button" className="btn primary" onClick={close}>Close</button>
          </div>
        </div>
      </div>
    </div>
  );
}
