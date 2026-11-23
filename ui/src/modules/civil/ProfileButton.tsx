import { ChartSpline } from 'lucide-react';
import { openProfile } from './profileStore';

/** The "Profile…" row of a line's properties: opens the profile editor on it (site grading T3). */
export function ProfileButton({ id }: { id: string }) {
  return <><dt /><dd><button type="button" className="btn compact" data-testid="open-profile" title="The line's heights along it, as a graph to edit; cross sections across it" onClick={() => openProfile(id)}><ChartSpline size={12} /> Profile…</button></dd></>;
}
