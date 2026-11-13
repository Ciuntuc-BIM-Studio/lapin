/**
 * Bending schedule (standard) (docs/plan-user-extensibility.md §6.4): the bars' marks as the standard writes them — mark, diameter,
 * shape code, the dimensions A, B, C…, number, length, total and mass, one row a mark — with a CSV for the spreadsheet and the
 * BVBS file for the bending machine.
 */

import { useEffect, useState } from 'react';
import { FileDown, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { safeFileName, saveFile } from '../../engine/files';
import { PendingDialog } from '../core/PendingDialog';
import { bendingSchedule, exportBvbsFile, scheduleCsv, type Schedule } from './barShapes';

export function BendingScheduleDialog() {
  const version = useSession((s) => s.version);
  const [s, setS] = useState<Schedule | null>(null);
  useEffect(() => { let live = true; void bendingSchedule().then((r) => { if (live && r) setS(r); }); return () => { live = false; }; }, [version]);
  const close = () => useUi.setState({ dialog: null });
  if (!s) return <PendingDialog title="Bending schedule" />;
  const csv = async () => {
    // A byte order mark first, so a spreadsheet reads the CSV as UTF-8.
    const saved = await saveFile(new TextEncoder().encode(`﻿${scheduleCsv(s)}`), `${safeFileName('Bending schedule', 'schedule')}.csv`, 'text/csv');
    if (saved) notify(`Exported ${saved}.`);
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Bending schedule" data-dialog="bendingSchedule" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Bending schedule (standard)</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {s.rows.length === 0 ? <p className="muted">There are no bars to schedule yet.</p> : (
            <table className="layer-table bending-table" data-testid="bending-schedule">
              <thead><tr>{s.columns.map((c, i) => <th key={i} data-no-i18n>{c}</th>)}</tr></thead>
              <tbody>{s.rows.map((r, i) => <tr key={i}>{r.map((v, j) => <td key={j} className={typeof v === 'number' ? 'num' : undefined} data-no-i18n>{v ?? ''}</td>)}</tr>)}</tbody>
            </table>
          )}
          <div className="row">
            <button type="button" className="btn" data-field="bending-csv" disabled={s.rows.length === 0} onClick={() => void csv()}><FileDown size={13} />Export CSV…</button>
            <button type="button" className="btn" data-field="bending-bvbs" disabled={s.rows.length === 0} onClick={() => void exportBvbsFile()}><FileDown size={13} />Export BVBS (.abs)…</button>
          </div>
          <p className="muted small">The shape codes and dimensions are those of the bar shapes (Bar shapes…). Bars laid by a type (stirrups, column bars) have no drawn shape and are not listed here.</p>
        </div>
      </div>
    </div>
  );
}
