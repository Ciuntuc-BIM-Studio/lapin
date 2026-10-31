/** The Rebar 2D tab's own icons: bar shapes, a stirrup, stirrups along a beam, a bar seen end on. */

import type { Icon, IconProps } from '../../icons';

const svg = (paths: string[], fill = false): Icon => {
  const C = ({ size = 16, strokeWidth = 1.6, ...rest }: IconProps) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill={fill ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...rest}>
      {paths.map((d, i) => <path key={i} d={d} />)}
    </svg>
  );
  return C;
};

export const LBarIcon = svg(['M4 6h13a2 2 0 0 1 2 2v10']);
export const UBarIcon = svg(['M5 5v11a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V5']);
export const StirrupIcon = svg(['M7 9l-2 -2', 'M9 7l-2 -2', 'M6 6h11a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V8']);
export const TieIcon = svg(['M12 4v16', 'M9 6l3-2 3 2', 'M9 18l3 2 3-2']);
export const PolylineBarIcon = svg(['M3 17h6a2 2 0 0 0 2-2V9a2 2 0 0 1 2-2h8']);
export const StirrupsIcon = svg(['M3 7h18', 'M3 17h18', 'M7 7v10', 'M11 7v10', 'M15 7v10', 'M19 7v10']);
export const BarDotIcon = svg(['M12 12m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0', 'M4 4h16v16H4z']);
