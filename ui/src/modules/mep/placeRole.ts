/** The role the Place tool puts down now (S8): set by its picker, by an old role's tool (Radiator, Valve…) and by "+ Radiator" in a system's properties. */
import { create } from 'zustand';

export const usePlaceRole = create<{ role: string }>(() => ({ role: 'radiator' }));

/** The tool that places the role: the Place tool, set to it. */
export function toolForRole(role: string): string {
  usePlaceRole.setState({ role });
  return 'mep.place';
}
