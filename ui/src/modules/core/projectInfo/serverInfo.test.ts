import { describe, expect, it } from 'vitest';
import { archicadProposal, changeLabel, fieldPatch, infoPerms, INFO_FIELDS, isFieldChange, proposalMade, sourceRows, type InfoRecord } from './serverInfo';
import { takenFields, type ArchicadProjectInfo } from '../../archicad/archicad';

const record = (o: Partial<InfoRecord> = {}): InfoRecord => ({ fields: {}, custom: {}, source: {}, modifiedAt: null, modifiedBy: null, modifiedByName: null, openProposals: 0, ...o });

describe('the project information on the server (A1b)', () => {
  it('has Lapin\'s keys in the form\'s order, the name first', () => {
    expect(INFO_FIELDS.map((f) => f.key)).toEqual([
      'name', 'number', 'phase', 'phaseName', 'contract', 'client', 'clientAddress', 'address', 'cadastral', 'landBook', 'permit', 'permitDate',
      'designer', 'designerAddress', 'designerRegistration', 'designerPhone', 'designerEmail',
      'projectManager', 'chiefArchitect', 'structuralEngineer', 'mepEngineer', 'author', 'checkedBy', 'approvedBy',
    ]);
  });

  it('names a change by its field, its parameter or what the source called it', () => {
    const r = record({ custom: { suprafata: { label: 'Suprafața terenului', type: 'number', group: 'site', value: '450.5' } } });
    expect(changeLabel({ key: 'number' })).toBe('Project number');
    expect(changeLabel({ key: 'custom.suprafata' }, r)).toBe('Suprafața terenului');
    expect(changeLabel({ key: 'source.archicad.KEYWORDS', label: 'Keywords' })).toBe('Keywords');
    expect(changeLabel({ key: 'source.archicad.KEYWORDS' })).toBe('KEYWORDS');
    expect(isFieldChange({ key: 'author' })).toBe(true);
    expect(isFieldChange({ key: 'source.ifc.IfcSite.Name' })).toBe(false);
  });

  it('sends only the fields edited, a cleared one as null', () => {
    expect(fieldPatch({ number: '104', client: 'Ion' }, { number: ' 104 ', client: '', phase: 'DTAC', nope: 'x' })).toEqual({ client: null, phase: 'DTAC' });
    expect(fieldPatch({}, {})).toEqual({});
  });

  it('lists what the models said, by source then label', () => {
    const r = record({ source: { ifc: { 'IfcSite.Name': { value: 'Teren' } }, archicad: { KEYWORDS: { label: 'Keywords', value: 'casă' }, NOTES: { label: 'Notes', value: '' } } } });
    expect(sourceRows(r)).toEqual([
      { source: 'archicad', id: 'KEYWORDS', label: 'Keywords', value: 'casă' },
      { source: 'ifc', id: 'IfcSite.Name', label: 'IfcSite.Name', value: 'Teren' },
    ]);
    expect(sourceRows(null)).toEqual([]);
  });

  it('lets viewers read, editors change and dismiss their own, owners apply', () => {
    expect(infoPerms('viewer', 'a')).toMatchObject({ edit: false, apply: false });
    const editor = infoPerms('editor', 'atrium:me');
    expect([editor.edit, editor.apply, editor.dismiss({ createdBy: 'atrium:me' }), editor.dismiss({ createdBy: 'atrium:x' })]).toEqual([true, false, true, false]);
    expect(infoPerms('owner', null).dismiss({ createdBy: 'atrium:x' })).toBe(true);
  });

  it('proposes Archicad\'s answer as it is, naming the file and the document', () => {
    const raw = [{ projectInfoId: 'PROJECTNAME', projectInfoName: 'Project Name', projectInfoValue: 'Casa' }];
    expect(archicadProposal(raw, 'casa.pln', 'g')).toEqual({ source: 'archicad', raw, sourceRef: { file: 'casa.pln', referenceId: 'g' } });
    expect(archicadProposal(raw)).toEqual({ source: 'archicad', raw });
    expect(proposalMade({ proposal: null })).toBe(false);
    expect(proposalMade({ id: 'x', number: 1 } as never)).toBe(true);
  });

  it('takes the ticked fields that differ, with Archicad\'s values', () => {
    const info: ArchicadProjectInfo = { source: 'archicad', filled: [], same: [], kept: [], differs: [{ key: 'number', lapin: '77', archicad: '104' }, { key: 'client', lapin: 'A', archicad: 'B' }] };
    expect(takenFields(info, ['number', 'phase'])).toEqual({ number: '104' });
    expect(takenFields(info, [])).toEqual({});
  });
});
