/**
 * Which view the command line works on when two panes each lend it a host
 * (a plan beside a 3D view whose work plane drafts while a command runs):
 * the active pane's, the pane a click lands in becoming the active one
 * (PaneBody focuses it before the view sees the click). Without this the last
 * view to register won, so a command started with the 3D pane beside a plan
 * picked in the 3D view's drafts only and every click on a plan's wall found
 * nothing (decision 655).
 */

import { useUi } from '../../app/store';
import { useCad, type CadHost } from './session';

/** The registered hosts, oldest first, with the pane each sits in. */
const hosts = new Map<CadHost, number>();

/** The host for the commands: the newest in the active pane, else the newest of all. */
function choose(): CadHost | null {
  const active = useUi.getState().pane;
  let inActive: CadHost | null = null, newest: CadHost | null = null;
  for (const [h, pane] of hosts) { newest = h; if (pane === active) inActive = h; }
  return inActive ?? newest;
}

function settle(): void {
  const h = choose();
  if (useCad.getState().host !== h) useCad.setState({ host: h });
}

/** A view lends its host while it is shown; the returned function takes it back. */
export function registerCadHost(host: CadHost, pane: number): () => void {
  hosts.set(host, pane);
  settle();
  return () => { hosts.delete(host); settle(); };
}

useUi.subscribe((s, prev) => { if (s.pane !== prev.pane) settle(); });
