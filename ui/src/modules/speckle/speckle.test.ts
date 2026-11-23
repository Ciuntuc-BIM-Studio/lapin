import { describe, expect, it } from 'vitest';
import { DEFAULT_SERVER, receiveArgs, secretKey, sendArgs, serverOf } from './speckle';

describe('the Speckle connector', () => {
  it('takes a server by its address alone, Speckle\'s own by default, as the engine does', () => {
    expect(serverOf('')).toBe(DEFAULT_SERVER);
    expect(serverOf(' speckle.firm.ro/ ')).toBe('https://speckle.firm.ro');
    expect(serverOf('http://127.0.0.1:8090/projects/abc')).toBe('http://127.0.0.1:8090');
    // The token is kept in the secure store under its server's key, the address alone.
    expect(secretKey('http://127.0.0.1:8090/')).toBe('speckle:http://127.0.0.1:8090');
  });

  it('asks the engine for only what the choice of what to send uses', () => {
    const base = { projectId: 'p', hidden: ['a'], planes: [{ normal: [0, 0, -1], constant: 3 }], ids: ['w1', 'w2'], viewId: 'v3d' };
    expect(sendArgs({ ...base, what: 'model', modelName: ' House ' })).toEqual({ projectId: 'p', what: 'model', modelName: 'House' });
    expect(sendArgs({ ...base, what: 'view', modelId: 'm', message: ' first ' })).toEqual({ projectId: 'p', what: 'view', modelId: 'm', message: 'first', viewId: 'v3d', hidden: ['a'], planes: base.planes });
    expect(sendArgs({ ...base, what: 'selection', modelId: 'm' })).toEqual({ projectId: 'p', what: 'selection', modelId: 'm', ids: ['w1', 'w2'] });
  });

  it('receives the latest version unless one is named, by the levels unless asked to put everything on the current storey', () => {
    const base = { projectId: 'p', modelId: 'm', activeStoreyId: 's1' };
    expect(receiveArgs({ ...base })).toEqual({ projectId: 'p', modelId: 'm', activeStoreyId: 's1' });
    expect(receiveArgs({ ...base, versionId: 'v2', storeys: 'active', origin: 'internal', asCopy: true }))
      .toEqual({ projectId: 'p', modelId: 'm', versionId: 'v2', storeyId: 's1', origin: 'internal', asCopy: true });
    expect(receiveArgs({ projectId: 'p', modelId: 'm', versionId: null, origin: 'base' })).toEqual({ projectId: 'p', modelId: 'm' });
  });
});
