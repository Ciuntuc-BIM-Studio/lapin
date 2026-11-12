import { pensToSvg } from '../documents/docModel';
import { previewImage, type SymPreview } from './symbolModel';

/** A symbol drawn small, from the engine's preview. */
export const Thumb = ({ p }: { p?: SymPreview | null }) => (
  <span className="sy-thumb" data-no-i18n aria-hidden dangerouslySetInnerHTML={{ __html: p && p.pens.length ? pensToSvg(previewImage(p)) : '' }} />
);
