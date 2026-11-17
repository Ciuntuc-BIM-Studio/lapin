import { describe, expect, it } from 'vitest';
import { conditionOf, moveIndex, ruleClasses, rulesByClass, type TagRuleDto } from './tagRules';

const r = (id: string, cls: string): TagRuleDto => ({ id, class: cls, when: '1', tagType: 'door' });
const rules = [r('a', 'IfcDoor'), r('b', 'IfcWindow'), r('c', 'IfcDoor'), r('d', 'IfcDoor')];

/** What the engine's standards.move does: the entry taken out, then put at the index. */
function moved(list: TagRuleDto[], id: string, index: number): string[] {
  const rest = list.filter((x) => x.id !== id);
  rest.splice(index, 0, list.find((x) => x.id === id)!);
  return rest.map((x) => x.id);
}

describe('tag rules (issue #72)', () => {
  it('are grouped by class in the order their first rule stands, each class keeping its order', () => {
    expect(rulesByClass(rules).map(([c, l]) => [c, l.map((x) => x.id)])).toEqual([['IfcDoor', ['a', 'c', 'd']], ['IfcWindow', ['b']]]);
  });

  it('move up and down among their class only, past the other classes\' rules', () => {
    expect(moveIndex(rules, 'c', -1)).toBe(0);
    expect(moved(rules, 'c', moveIndex(rules, 'c', -1)!)).toEqual(['c', 'a', 'b', 'd']);
    expect(moveIndex(rules, 'a', 1)).toBe(2);
    expect(moved(rules, 'a', moveIndex(rules, 'a', 1)!)).toEqual(['b', 'c', 'a', 'd']);
    expect(moveIndex(rules, 'a', -1)).toBeNull();
    expect(moveIndex(rules, 'd', 1)).toBeNull();
    expect(moveIndex(rules, 'b', 1)).toBeNull();
    expect(moveIndex(rules, 'x', 1)).toBeNull();
  });

  it('offer the usual classes, then those the tag types and the rules name, each once', () => {
    const classes = ruleClasses([{ classes: ['IfcDoor', 'IfcCovering'] }, { classes: 'IfcRoof, IfcDoor' }, {}], [r('z', 'LapinRoomFinish')]);
    expect(classes.slice(0, 2)).toEqual(['IfcDoor', 'IfcWindow']);
    expect(classes).toContain('IfcCovering');
    expect(classes).toContain('IfcRoof');
    expect(classes).toContain('LapinRoomFinish');
    expect(classes.filter((c) => c === 'IfcDoor')).toHaveLength(1);
  });

  it('read a condition bare or as a formula field, as the engine does', () => {
    expect(conditionOf('{= width > 1.2}')).toBe('width > 1.2');
    expect(conditionOf('= width > 1.2')).toBe('width > 1.2');
    expect(conditionOf('  FireRating != ""  ')).toBe('FireRating != ""');
    expect(conditionOf('{= }')).toBe('');
  });
});
