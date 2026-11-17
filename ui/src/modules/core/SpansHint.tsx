/** The ? beside a spans field (issue #83): what the field reads, on hover. Axis grid and New levels share it. */

import { CircleHelp } from 'lucide-react';

/** The Axis grid's spans fields: the grammar and which labels count. */
export const SPANS_HINT = 'Spans separated by spaces make the axes at those distances, in the project\'s units (6000 6000 4500; a comma or a dot for the decimals). N*span makes N equal spans: 3*6000 is 6000 6000 6000, and both mix: 100 2*300 200. Labels count on from the first one: a number at its end counts up (1, 2… 10; A1, A2), letters at its end run through the alphabet (A… Z, AA; a, b), any other label takes a 1 after it (A- → A-1); the prefix and the suffix stay around the count (X-1, X-2; A\', B\').';

/** New levels' storey heights field. */
export const HEIGHTS_HINT = 'Storey heights separated by spaces, bottom up, in the project\'s units (3000 3000 2800; a comma or a dot for the decimals). N*height makes N storeys that high: 3*3000 2800 is four storeys. The first one stands on the base level, each next one on the one below.';

export const SpansHint = ({ text = SPANS_HINT }: { text?: string }) => (
  <span className="icon mini" role="img" aria-label="Help" title={text} data-spans-hint style={{ cursor: 'help', display: 'inline-flex', alignItems: 'center', marginLeft: 4 }}><CircleHelp size={14} /></span>
);
