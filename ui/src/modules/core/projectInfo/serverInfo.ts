/**
 * The project information record of an Atrium Server project (docs/plan-archicad.md A1b; the server's side is Atrium's
 * docs/atrium-server.md, "Project information (A1b)"): read and changed directly by the CDE web's Project information tab, and by
 * File → Collaborate on the desktop (through the engine's remote.atrium, as every Atrium request there). The record has Lapin's keys
 * (`name` and the project information's fields), the project's own parameters by key, and what sources said that no field took
 * (`source`, read-only); a change is a JSON merge patch with the record's ETag as If-Match, a 412 meaning it changed meanwhile.
 * Proposals (from Archicad, IFC models, by hand) are applied in part or whole by owners, or dismissed.
 */

import { need, send } from '../../../engine/atrium';

/** Lapin's keys in the form's order, with their labels (engine ProjectInfo.Grouped, the project's name first). */
export const INFO_FIELDS: { key: string; label: string; group: string }[] = [
  { key: 'name', label: 'Project name', group: 'project' },
  { key: 'number', label: 'Project number', group: 'project' }, { key: 'phase', label: 'Phase', group: 'project' }, { key: 'phaseName', label: 'Phase name', group: 'project' },
  { key: 'contract', label: 'Contract number', group: 'project' },
  { key: 'client', label: 'Client', group: 'client' }, { key: 'clientAddress', label: "Client's address", group: 'client' },
  { key: 'address', label: 'Site address', group: 'site' }, { key: 'cadastral', label: 'Cadastral number', group: 'site' }, { key: 'landBook', label: 'Land book', group: 'site' },
  { key: 'permit', label: 'Building permit number', group: 'site' }, { key: 'permitDate', label: 'Building permit date', group: 'site' },
  { key: 'designer', label: 'Designer', group: 'designer' }, { key: 'designerAddress', label: "Designer's address", group: 'designer' },
  { key: 'designerRegistration', label: 'Registration number', group: 'designer' }, { key: 'designerPhone', label: 'Phone', group: 'designer' }, { key: 'designerEmail', label: 'E-mail', group: 'designer' },
  { key: 'projectManager', label: 'Project manager', group: 'people' }, { key: 'chiefArchitect', label: 'Chief architect', group: 'people' },
  { key: 'structuralEngineer', label: 'Structural engineer', group: 'people' }, { key: 'mepEngineer', label: 'MEP engineer', group: 'people' },
  { key: 'author', label: 'Drawn by', group: 'people' }, { key: 'checkedBy', label: 'Checked by', group: 'people' }, { key: 'approvedBy', label: 'Approved by', group: 'people' },
];

export const INFO_GROUPS: { key: string; label: string }[] = [
  { key: 'project', label: 'Project' }, { key: 'client', label: 'Client' }, { key: 'site', label: 'Site and permit' }, { key: 'designer', label: 'Designer' }, { key: 'people', label: 'People' },
];

export const SOURCE_NAMES: Record<string, string> = { archicad: 'Archicad', ifc: 'IFC', manual: 'By hand' };

export interface InfoParameter { label: string; type: 'text' | 'number' | 'date'; group: string; value?: string }
export interface InfoRecord {
  fields: Record<string, string>;
  custom: Record<string, InfoParameter>;
  source: Record<string, Record<string, { label?: string | null; value: string }>>;
  keys?: string[]; etag?: string; modifiedAt: string | null; modifiedBy: string | null; modifiedByName: string | null; openProposals: number;
}
export interface InfoChange { key: string; label?: string | null; current: string | null; proposed: string }
export type ProposalStatus = 'open' | 'applied' | 'dismissed' | 'superseded';
export interface InfoProposal {
  id: string; number: number; status: ProposalStatus; source: 'archicad' | 'ifc' | 'manual';
  sourceRef: { referenceId?: string; file?: string; submissionId?: string; commit?: string; sha256?: string } | null;
  fields: Record<string, string>; extras: Record<string, { label?: string | null; value: string }>; changes: InfoChange[];
  created: string; createdBy: string | null; createdByName: string | null; decided: string | null; appliedKeys: string[] | null; reason: string | null;
}

// ------------------------------------------------------------ pure parts

/** A field's label, a parameter's (its own label), or a source's value's (`source.<source>.<id>`: its label, else its id). */
export function changeLabel(c: Pick<InfoChange, 'key' | 'label'>, record?: InfoRecord | null): string {
  const f = INFO_FIELDS.find((x) => x.key === c.key);
  if (f) return f.label;
  if (c.key.startsWith('custom.')) return record?.custom[c.key.slice(7)]?.label ?? c.key.slice(7);
  const m = /^source\.([^.]+)\.(.+)$/.exec(c.key);
  return c.label?.trim() || (m ? m[2] : c.key);
}

/** Whether a change is one of a field (else what the source said that no field takes). */
export const isFieldChange = (c: Pick<InfoChange, 'key'>) => INFO_FIELDS.some((f) => f.key === c.key);

/** The merge patch of the fields edited: only those that changed, a cleared one as null. */
export function fieldPatch(before: Record<string, string>, edited: Record<string, string>): Record<string, string | null> {
  const o: Record<string, string | null> = {};
  for (const { key } of INFO_FIELDS) {
    if (!(key in edited)) continue;
    const now = edited[key].trim();
    if (now === (before[key] ?? '').trim()) continue;
    o[key] = now === '' ? null : now;
  }
  return o;
}

/** What sources said that no field took, flat for the read-only list "From the models": source, id, label, value. */
export function sourceRows(record: InfoRecord | null | undefined): { source: string; id: string; label: string; value: string }[] {
  const rows: { source: string; id: string; label: string; value: string }[] = [];
  for (const [source, values] of Object.entries(record?.source ?? {}))
    for (const [id, v] of Object.entries(values ?? {})) if (v?.value) rows.push({ source, id, label: v.label?.trim() || id, value: v.value });
  return rows.sort((a, b) => a.source.localeCompare(b.source) || a.label.localeCompare(b.label));
}

/** What a viewer, an editor and an owner may do here; an editor dismisses the proposals they made. */
export function infoPerms(role: 'viewer' | 'editor' | 'owner', authorId: string | null) {
  return {
    edit: role !== 'viewer',
    apply: role === 'owner',
    dismiss: (p: Pick<InfoProposal, 'createdBy'>) => role === 'owner' || (role === 'editor' && !!authorId && p.createdBy === authorId),
  };
}

/** The body of a proposal from Archicad: Tapir's GetProjectInfoFields answer as it is, with the project file's name when there is one. */
export function archicadProposal(raw: unknown[], file?: string | null, referenceId?: string | null): Record<string, unknown> {
  const sourceRef: Record<string, string> = {};
  if (file) sourceRef.file = file;
  if (referenceId) sourceRef.referenceId = referenceId;
  return { source: 'archicad', raw, ...(Object.keys(sourceRef).length > 0 ? { sourceRef } : {}) };
}

// ------------------------------------------------------------ the server

const enc = encodeURIComponent;
const I = (project: string) => `atrium/1.0/projects/${enc(project)}/info`;

export async function getInfo(server: string, project: string): Promise<InfoRecord> {
  const a = await send<InfoRecord>(server, 'GET', I(project));
  const r = a.body ?? ({ fields: {}, custom: {}, source: {}, modifiedAt: null, modifiedBy: null, modifiedByName: null, openProposals: 0 } as InfoRecord);
  return { ...r, fields: r.fields ?? {}, custom: r.custom ?? {}, source: r.source ?? {}, etag: a.etag ?? r.etag };
}

/** A merge patch with the record's ETag: the record after it, or `changed` with the record as it is now (412). */
export async function patchInfo(server: string, project: string, patch: Record<string, unknown>, etag: string | undefined): Promise<{ record: InfoRecord; changed: boolean }> {
  const a = await send<InfoRecord>(server, 'PATCH', I(project), patch, etag ?? null);
  if (a.status === 412) return { record: await getInfo(server, project), changed: true };
  const r = a.body!;
  return { record: { ...r, fields: r.fields ?? {}, custom: r.custom ?? {}, source: r.source ?? {}, etag: a.etag ?? r.etag }, changed: false };
}

export const listProposals = async (server: string, project: string, status: ProposalStatus = 'open') =>
  (await need<{ proposals: InfoProposal[] }>(server, 'GET', `${I(project)}/proposals?status=${status}`)).proposals ?? [];

export const applyProposal = (server: string, project: string, id: string, keys?: string[]) =>
  need<{ proposal: InfoProposal; info: InfoRecord }>(server, 'POST', `${I(project)}/proposals/${enc(id)}/apply`, keys ? { keys } : {});

export const dismissProposal = (server: string, project: string, id: string, reason?: string) =>
  need<InfoProposal>(server, 'POST', `${I(project)}/proposals/${enc(id)}/dismiss`, reason?.trim() ? { reason: reason.trim() } : {});

/** A proposal: the proposal made (201), or none when nothing differs (`{proposal: null, changes: [], reason}`). */
export const propose = (server: string, project: string, body: Record<string, unknown>) =>
  need<InfoProposal | { proposal: null; changes: []; reason: string }>(server, 'POST', `${I(project)}/proposals`, body);

/** Whether an answer of propose() made a proposal (else nothing differed). */
export const proposalMade = (a: InfoProposal | { proposal: null }): a is InfoProposal => (a as { proposal?: unknown }).proposal !== null && 'id' in a;
