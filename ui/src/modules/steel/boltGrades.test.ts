import { describe, expect, it } from 'vitest';
import { withCurrent } from './boltGrades';

describe('bolt grades offered', () => {
  it('keep the node\'s current value in the list', () => {
    expect(withCurrent(['4.6', '8.8'], '10.9')).toEqual(['4.6', '8.8', '10.9']);
    expect(withCurrent(['4.6', '8.8'], '8.8')).toEqual(['4.6', '8.8']);
    expect(withCurrent(['4.6'], null)).toEqual(['4.6']);
  });
});
