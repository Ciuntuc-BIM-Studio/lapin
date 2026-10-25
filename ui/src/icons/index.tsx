/**
 * Lapin's own icons for the modelling tools: each is the element's symbol as
 * a designer reads it on a sheet (the door's swing, the column cut, the
 * section marker), not a generic object. A 24-unit grid, lines of 1.5, round
 * caps, the same optical weight as lucide, which keeps the generic interface
 * (files, undo, theme). The shapes live in icons.json so a script can lay
 * them out on a sheet (docs/mock/icons.svg) to check them at 22 px.
 */

import { createElement, forwardRef, type ComponentType, type SVGProps } from 'react';
import shapes from './icons.json';

export interface IconProps extends Omit<SVGProps<SVGSVGElement>, 'ref'> {
  size?: number | string;
  strokeWidth?: number | string;
}
export type Icon = ComponentType<IconProps>;

export type IconName = keyof typeof shapes;
type Node = [string, Record<string, string | number>];

function icon(name: IconName): Icon {
  const nodes = shapes[name] as Node[];
  const C = forwardRef<SVGSVGElement, IconProps>(({ size = 24, strokeWidth = 1.5, color = 'currentColor', className, ...rest }, ref) => (
    <svg ref={ref} xmlns="http://www.w3.org/2000/svg" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth}
      strokeLinecap="round" strokeLinejoin="round" className={className ? `lapin-icon ${className}` : 'lapin-icon'} aria-hidden="true" {...rest}>
      {nodes.map(([tag, attrs], i) => createElement(tag, { key: i, ...attrs }))}
    </svg>
  ));
  C.displayName = `Icon(${name})`;
  return C;
}

export const WallIcon = icon('wall');
export const DoorIcon = icon('door');
export const WindowIcon = icon('window');
export const SlabIcon = icon('slab');
export const RoofIcon = icon('roof');
export const ColumnIcon = icon('column');
export const BeamIcon = icon('beam');
export const StripFootingIcon = icon('stripFooting');
export const PadFootingIcon = icon('padFooting');
export const StairIcon = icon('stair');
export const RoomIcon = icon('room');
export const SectionIcon = icon('section');
export const ElevationIcon = icon('elevation');
export const LevelIcon = icon('level');
export const GridAxisIcon = icon('gridAxis');
export const DimensionIcon = icon('dimension');
export const TerrainIcon = icon('terrain');
export const PlatformIcon = icon('platform');
export const SlabOpeningIcon = icon('slabOpening');
export const RoofWindowIcon = icon('roofWindow');
export const DormerIcon = icon('dormer');
export const LintelIcon = icon('lintel');
export const WallTypesIcon = icon('wallTypes');
export const AttachRoofIcon = icon('attachRoof');
export const DetachRoofIcon = icon('detachRoof');
export const AngleIcon = icon('angle');
export const RadiusIcon = icon('radius');
export const DiameterIcon = icon('diameter');
export const ArcLengthIcon = icon('arcLength');
export const SlopeIcon = icon('slope');
export const OrdinateIcon = icon('ordinate');
