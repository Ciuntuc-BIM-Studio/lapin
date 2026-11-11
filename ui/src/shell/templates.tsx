/**
 * The start screen's templates (decision 261): what a new project is for —
 * a single-storey house, a two-storey house, an apartment block, a hall, a
 * drawing only, or an IFC to start from — each its levels, the workspace
 * that work uses, and what comes after (the IFC import). A template is a new
 * project like any: nothing to undo, nothing saved until the user saves.
 * An edition with levels of its own keeps them: only the IFC start is offered.
 */

import type { ReactNode } from 'react';
import { hasModule, productInfo } from '../platform/brand';
import { chooseWorkspace, type WorkspaceId } from '../app/interface';
import { importIfc, newProject, type NewStorey } from '../engine/files';

export interface ProjectTemplate {
  id: string;
  label: string;
  note: string;
  /** The workspace the work uses; null keeps the user's. */
  workspace: WorkspaceId | null;
  /** The levels, bottom up; null: the edition's. */
  storeys: NewStorey[] | null;
  /** What follows the new project (the IFC file to start from). */
  after?: () => Promise<void>;
  /** Shown only when the edition has these modules. */
  needs: string[];
  picture: ReactNode;
}

const FOUNDATIONS: NewStorey = { name: 'Foundations', elevation: -1.5, height: 1.5 };
const ground = (height = 3): NewStorey => ({ name: 'Ground floor', elevation: 0, height });
const floor = (n: number): NewStorey => ({ name: n === 1 ? 'First floor' : `Floor ${n}`, elevation: 3 * n, height: 3 });

/** The pictures: a few lines in the interface's colours, no file. */
const Pic = ({ children }: { children: ReactNode }) => (
  <svg className="template-pic" viewBox="0 -10 260 145" aria-hidden="true">{children}</svg>
);
const wall = 'var(--panel)', shade = 'var(--rule)', ink = 'var(--ink)', roof = 'var(--accent)';

export const TEMPLATES: ProjectTemplate[] = [
  {
    id: 'house', label: 'Single-storey house', note: 'Foundations and a ground floor, for walls, a roof and rooms',
    workspace: 'architecture', storeys: [FOUNDATIONS, ground()], needs: ['architecture'],
    picture: <Pic>
      <polygon points="70,96 160,114 160,76 70,58" fill={wall} stroke={ink} />
      <polygon points="160,114 200,94 200,56 160,76" fill={shade} stroke={ink} />
      <polygon points="160,76 200,56 180,28" fill={shade} stroke={ink} />
      <polygon points="64,60 164,80 182,26 82,6" fill={roof} stroke={ink} />
    </Pic>,
  },
  {
    id: 'twoStorey', label: 'Two-storey house', note: 'A ground floor and a first floor, with the stair between them',
    workspace: 'architecture', storeys: [FOUNDATIONS, ground(), floor(1)], needs: ['architecture'],
    picture: <Pic>
      <polygon points="80,110 160,124 160,60 80,46" fill={wall} stroke={ink} />
      <path d="M80 78l80 14" stroke={ink} />
      <polygon points="160,124 196,106 196,42 160,60" fill={shade} stroke={ink} />
      <polygon points="160,60 196,42 178,18" fill={shade} stroke={ink} />
      <polygon points="74,48 164,64 180,16 90,2" fill={roof} stroke={ink} />
    </Pic>,
  },
  {
    id: 'block', label: 'Apartment block', note: 'A ground floor and four floors over the foundations',
    workspace: 'architecture', storeys: [FOUNDATIONS, ground(), floor(1), floor(2), floor(3), floor(4)], needs: ['architecture'],
    picture: <Pic>
      <polygon points="76,116 170,124 170,18 76,10" fill={wall} stroke={ink} />
      <path d="M76 31l94 8M76 52l94 8M76 73l94 8M76 94l94 8" stroke={ink} />
      <polygon points="170,124 204,108 204,2 170,18" fill={shade} stroke={ink} />
    </Pic>,
  },
  {
    id: 'hall', label: 'Hall or frame', note: 'One tall storey for grids, columns, beams and footings',
    workspace: 'structure', storeys: [FOUNDATIONS, ground(6)], needs: ['structure'],
    picture: <Pic>
      <path d="M50 108V68M90 88V48M130 108V28M170 88V48M210 108V68" stroke={ink} strokeWidth="3" />
      <path d="M50 68L130 28L210 68" fill="none" stroke={roof} strokeWidth="3" />
      <path d="M40 112h180" stroke={ink} />
    </Pic>,
  },
  {
    id: 'drafting', label: 'Drafting only', note: 'Lines, layers and dimensions, as in a CAD program',
    workspace: 'drafting', storeys: [ground()], needs: ['drafting'],
    picture: <Pic>
      <rect x="70" y="16" width="120" height="78" fill="none" stroke={ink} strokeWidth="2" />
      <path d="M130 16v78M70 56h60" stroke={ink} strokeWidth="2" />
      <path d="M70 108h120M70 103v10M190 103v10" stroke={roof} strokeWidth="1.5" />
    </Pic>,
  },
  {
    id: 'ifc', label: 'Start from an IFC', note: 'Another program’s model, recognised into elements you can edit',
    workspace: null, storeys: null, after: () => importIfc('auto', true), needs: [],
    picture: <Pic>
      <path d="M100 14h48l22 22v70h-70z" fill={wall} stroke={ink} strokeWidth="2" />
      <path d="M148 14v22h22" fill="none" stroke={ink} strokeWidth="2" />
      <text x="135" y="82" textAnchor="middle" fontFamily="var(--mono, monospace)" fontSize="18" fontWeight="700" fill={roof}>IFC</text>
    </Pic>,
  },
];

/** A project template of the user's (decision 470): a stack of sheets with a plan on the top one. */
export const savedTemplatePicture = <Pic>
  <rect x="96" y="22" width="92" height="96" fill={shade} stroke={ink} />
  <rect x="84" y="12" width="92" height="96" fill={wall} stroke={ink} />
  <path d="M100 32h60v40h-60zM130 32v40M100 52h30" fill="none" stroke={ink} strokeWidth="2" />
  <path d="M100 88h60" stroke={roof} strokeWidth="2" />
</Pic>;

/** The templates this edition offers: those whose modules it has; an edition with its own levels keeps them (the IFC start only). */
export function templates(): ProjectTemplate[] {
  const own = (productInfo.storeys?.length ?? 0) > 0;
  return TEMPLATES.filter((t) => t.needs.every(hasModule) && (!own || t.storeys === null));
}

/** A template started: its workspace, a new project with its levels, then what follows. */
export async function startFrom(t: ProjectTemplate): Promise<void> {
  if (!(await newProject(t.storeys ?? undefined))) return;
  if (t.workspace) chooseWorkspace(t.workspace);
  await t.after?.();
}
