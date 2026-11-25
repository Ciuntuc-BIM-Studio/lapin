import { Clock } from 'lucide-react';

/** A document's status, and beside it how many days its validity has left (or has been past): red for expired, amber for expiring. */
export function StatusBadge({ label, tone, expiry }: { label: string; tone: string; expiry: { kind: string; days: number } | null }) {
  return (
    <span className="refs-badges">
      <span className={`refs-badge ${tone}`}>{label}</span>
      {expiry && (
        <span className={`refs-badge ${expiry.kind === 'expired' ? 'bad' : 'warn'}`} title={expiry.kind === 'expired' ? 'Past its validity' : 'Its validity ends soon'}>
          <Clock size={10} /> {expiry.kind === 'expired' ? '−' : ''}{expiry.days} d
        </span>
      )}
    </span>
  );
}
