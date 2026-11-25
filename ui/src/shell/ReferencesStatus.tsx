import { Clock } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useSession } from '../engine/session';
import { expiryCounts, openReferences, today } from '../modules/core/references/expiry';

/**
 * The status bar's word on the reference documents (docs/plan-cde-references.md §5): how many expire within 30 days and how many
 * have expired, at a click the list filtered by it. Nothing while there are none. Reads the documents among the project's nodes.
 */
export function ReferencesStatus() {
  const nodes = useSession((s) => s.nodes);
  // The day changes under an open window: counted again at the turn of the hour.
  const [hour, setHour] = useState(0);
  useEffect(() => { const id = setInterval(() => setHour((h) => h + 1), 3_600_000); return () => clearInterval(id); }, []);
  void hour;
  const { expiring, expired } = expiryCounts(nodes, today());
  return (
    <>
      {expired > 0 && (
        <button type="button" className="status-link status-warn" data-testid="refs-expired" title="Reference documents past their validity: click to list them" onClick={() => openReferences('expired')}>
          <Clock size={11} /> <span>{expired}</span> <span>{expired === 1 ? 'document expired' : 'documents expired'}</span>
        </button>
      )}
      {expiring > 0 && (
        <button type="button" className="status-link status-warn" data-testid="refs-expiring" title="Reference documents whose validity ends soon: click to list them" onClick={() => openReferences('expiring')}>
          <Clock size={11} /> <span>{expiring}</span> <span>{expiring === 1 ? 'document expires within 30 days' : 'documents expire within 30 days'}</span>
        </button>
      )}
    </>
  );
}
