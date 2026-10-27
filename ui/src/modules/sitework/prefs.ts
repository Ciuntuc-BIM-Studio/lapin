/** The site works' choices kept for the session: the types new scaffolds and formwork take, the configurator's kind. */

import { create } from 'zustand';

export type FormworkKind = 'slab' | 'beam' | 'upright';

interface SiteWorksPrefs { scaffoldTypeId: string | null; formworkTypeId: string | null; formworkKind: FormworkKind }

export const useSiteWorks = create<SiteWorksPrefs>(() => ({ scaffoldTypeId: null, formworkTypeId: null, formworkKind: 'slab' }));

export const setSiteWorks = (patch: Partial<SiteWorksPrefs>): void => useSiteWorks.setState(patch);
