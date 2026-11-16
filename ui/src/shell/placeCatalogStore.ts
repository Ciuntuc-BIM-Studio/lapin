/** The Place catalogue's state and its opener (S15), apart from the dialog so a module's tool opens it without loading the platform. */
import { create } from 'zustand';
import { useUi } from '../app/store';

/** The source shown ('all': every one) and the text searched, kept while the app runs. */
export const usePlaceCatalog = create<{ source: string; query: string }>(() => ({ source: 'all', query: '' }));

/** Opens the catalogue, on one source when asked (an old entry point's). */
export function openPlaceCatalog(source = 'all'): void {
  usePlaceCatalog.setState({ source });
  useUi.setState({ dialog: 'placeCatalog' });
}
