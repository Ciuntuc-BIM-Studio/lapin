import { describe, expect, it } from 'vitest';
import { LANGUAGES, loadAllLanguages, translate } from '../../i18n';
import { gettable, ifcName, linkOf, NEW_FILE, secretKey, sendable, sendArgs, sendTarget, serverOf, type ConvergeFile } from './convergeifc';

const file = (id: string, fileName: string): ConvergeFile => ({ id, fileName, versionNo: 1, revisionCode: 'P01.1', status: 'WIP', sha256: '', sizeBytes: 1 });

// The languages are loaded on demand in the app.
await loadAllLanguages();

describe('the Converge IFC connector', () => {
  // The i18n tests compare the languages with each other; a phrase outside the table a language is read from passes them.
  it('has its menu entries, titles and notices in every language', () => {
    const said = ['Converge IFC: Send…', 'Converge IFC: Get…', 'Send to Converge IFC', 'Get from Converge IFC', 'a new file…', 'Get…', 'Downloading from the server', 'Uploading to the server',
      'Make a token on the server, in the account menu under API tokens. Lapin never writes it into the project, its settings or a report.',
      'Sent: Hall.ifc, now at P01.2.', 'Nothing sent: Hall.ifc is already like this on the server, at P01.2.'];
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en'))
      expect(said.filter((s) => translate(s, id) == null), id).toEqual([]);
  });

  it('takes a server by its address alone, with no default, as the engine does', () => {
    expect(serverOf('')).toBe('');
    expect(serverOf(' cde.firm.ro/ ')).toBe('https://cde.firm.ro');
    expect(serverOf('http://127.0.0.1:8090/api/v1')).toBe('http://127.0.0.1:8090');
    expect(secretKey('cde.firm.ro/')).toBe('convergeifc:https://cde.firm.ro');
  });

  it('sends only to .ifc files and opens .ifc and .ifczip ones', () => {
    const all = [file('a', 'A.ifc'), file('b', 'B.IFCZIP'), file('c', 'C.ifcx')];
    expect(sendable(all).map((f) => f.id)).toEqual(['a']);
    expect(gettable(all).map((f) => f.id)).toEqual(['a', 'b']);
  });

  it('names a new file .ifc', () => {
    expect(ifcName(' Hall ')).toBe('Hall.ifc');
    expect(ifcName('Hall.IFC')).toBe('Hall.IFC');
    expect(ifcName('Hall.ifczip')).toBe('Hall.ifc');
    expect(ifcName('Hall v1.2')).toBe('Hall v1.2.ifc');
    expect(ifcName('  ')).toBe('');
  });

  it('starts a send on the file sent to last, else on the one holding the name, else on a new one', () => {
    const files = [file('a', 'Other.ifc'), file('b', 'Hall.ifc')];
    expect(sendTarget(files, 'a', 'Hall.ifc')).toBe('a');
    expect(sendTarget(files, 'gone', 'hall')).toBe('b');
    expect(sendTarget(files, null, 'House.ifc')).toBe(NEW_FILE);
  });

  it('finds the link that reads the file a version is saved over', () => {
    const links = [{ id: 'l0', name: 'Scan', path: null }, { id: 'l1', name: 'Hall', path: 'C:\\Models\\Hall.ifc' }, { id: 'l2', name: 'Site', path: '/home/a/Site.ifc' }];
    expect(linkOf(links, 'c:/models/HALL.ifc')?.id).toBe('l1');
    expect(linkOf(links, '/home/a/Site.ifc')?.id).toBe('l2');
    expect(linkOf(links, '/home/a/site.ifc')).toBeUndefined();
    expect(linkOf(links, 'C:\\Models\\House.ifc')).toBeUndefined();
  });

  it('asks the engine for a new file by its name, or a new version of a file, and has the made IFC removed', () => {
    const a = { projectId: 'p', name: ' Hall ', schema: 'IFC4' };
    expect(sendArgs({ ...a, fileId: NEW_FILE }, 'x.ifc')).toEqual({ projectId: 'p', path: 'x.ifc', discard: true, fileName: 'Hall.ifc' });
    expect(sendArgs({ ...a, fileId: 'c1' }, 'x.ifc')).toEqual({ projectId: 'p', path: 'x.ifc', discard: true, containerId: 'c1' });
  });
});
