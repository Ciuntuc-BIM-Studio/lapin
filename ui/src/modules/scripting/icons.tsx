/**
 * A script tool's icon (stage 4; decision 489), by the name its header or lapin.ui.tool gives: one of Lapin's own
 * (`wall`, `door`, `stair`…: ui/src/icons/icons.json), one of a set of lucide's (`square`, `ruler`, `layers`…), else a
 * letter (the name's first, or the one given: `icon="P"`) drawn in a rounded square. Nothing of the script is drawn.
 */

import { forwardRef } from 'react';
import {
  Blocks, Bolt, Box, Boxes, BrickWall, Brush, Building2, Calculator, Camera, Circle, Code, Columns3, Compass, Copy, Cuboid, Download, Eraser, Eye, FileText,
  Filter, Flag, Frame, Grid3x3, Hammer, Hash, Hexagon, House, Image, Layers, LayoutGrid, Lightbulb, Link, ListChecks, Map as MapIcon, Mountain, Move, Package, PaintBucket,
  Pencil, PenTool, Play, Printer, Rows3, Ruler, Scaling, Scissors, Search, Settings, Shapes, Sigma, Sofa, Sparkles, Spline, Square, SquareDashed, Stamp, Star, Sun,
  Table, Tag, Terminal, Trash2, TreePine, Trees, Triangle, Type, Upload, WandSparkles, Warehouse, Wrench, Zap,
} from 'lucide-react';
import { lapinIcon, type Icon, type IconProps } from '../../icons';

/** The lucide icons a script may name, by their name in lowercase without dashes (`grid-3x3`, `grid3x3`, `Grid3x3` alike). */
const LUCIDE: Record<string, Icon> = {
  blocks: Blocks, bolt: Bolt, box: Box, boxes: Boxes, brickwall: BrickWall, brush: Brush, building: Building2, building2: Building2, calculator: Calculator,
  camera: Camera, circle: Circle, code: Code, columns: Columns3, compass: Compass, copy: Copy, cuboid: Cuboid, download: Download, eraser: Eraser, eye: Eye,
  filetext: FileText, file: FileText, filter: Filter, flag: Flag, frame: Frame, grid: Grid3x3, grid3x3: Grid3x3, hammer: Hammer, hash: Hash, hexagon: Hexagon,
  house: House, home: House, image: Image, layers: Layers, layoutgrid: LayoutGrid, lightbulb: Lightbulb, link: Link, listchecks: ListChecks, map: MapIcon,
  mountain: Mountain, move: Move, package: Package, paintbucket: PaintBucket, pencil: Pencil, pentool: PenTool, play: Play, printer: Printer, rows: Rows3,
  ruler: Ruler, scaling: Scaling, scissors: Scissors, search: Search, settings: Settings, shapes: Shapes, sigma: Sigma, sofa: Sofa, sparkles: Sparkles,
  spline: Spline, square: Square, squaredashed: SquareDashed, stamp: Stamp, star: Star, sun: Sun, table: Table, tag: Tag, terminal: Terminal, trash: Trash2,
  trash2: Trash2, tree: TreePine, trees: Trees, triangle: Triangle, type: Type, upload: Upload, wand: WandSparkles, wandsparkles: WandSparkles,
  warehouse: Warehouse, wrench: Wrench, zap: Zap,
};

/** The names a script may give, for the guide and the panel. */
export const ICON_NAMES = Object.keys(LUCIDE);

const letters = new Map<string, Icon>();

/** A letter in a rounded square, at the weight of the other icons. */
function letterIcon(letter: string): Icon {
  let i = letters.get(letter);
  if (i) return i;
  i = forwardRef<SVGSVGElement, IconProps>(({ size = 24, strokeWidth = 1.5, color = 'currentColor', className, ...rest }, ref) => (
    <svg ref={ref} xmlns="http://www.w3.org/2000/svg" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth}
      strokeLinecap="round" strokeLinejoin="round" className={className ? `lapin-icon ${className}` : 'lapin-icon'} aria-hidden="true" {...rest}>
      <rect x="3" y="3" width="18" height="18" rx="4" />
      <text x="12" y="16.5" textAnchor="middle" fontSize="12" fontWeight="600" fill={color} stroke="none" fontFamily="system-ui, sans-serif">{letter}</text>
    </svg>
  ));
  (i as { displayName?: string }).displayName = `Letter(${letter})`;
  letters.set(letter, i);
  return i;
}

/** The icon a script names (`wall`, `square`, `P`), else the first letter of its tool's name. */
export function scriptIcon(name: string | undefined, label: string): Icon {
  const n = (name ?? '').trim();
  if (n) {
    const own = lapinIcon(n);
    if (own) return own;
    const lucide = LUCIDE[n.toLowerCase().replace(/[-_\s]/g, '')];
    if (lucide) return lucide;
    if ([...n].length === 1) return letterIcon(n.toLocaleUpperCase());
  }
  const first = [...label.trim()][0] ?? '?';
  return letterIcon(first.toLocaleUpperCase());
}
