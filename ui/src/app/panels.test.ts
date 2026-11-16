import { describe, expect, it } from 'vitest';
import { nextPropsTabForTool } from './panels';

describe('nextPropsTabForTool', () => {
  it('switches from properties to tool when a tool activates and restores properties on end', () => {
    const active = nextPropsTabForTool('properties', undefined, true);
    expect(active).toEqual({ propsTab: 'tool', prevPropsTab: 'properties' });

    const ended = nextPropsTabForTool(active.propsTab, active.prevPropsTab, false);
    expect(ended).toEqual({ propsTab: 'properties', prevPropsTab: undefined });
  });

  it('switches from assistant to tool and restores assistant when tool ends', () => {
    const active = nextPropsTabForTool('assistant', undefined, true);
    expect(active).toEqual({ propsTab: 'tool', prevPropsTab: 'assistant' });

    const ended = nextPropsTabForTool(active.propsTab, active.prevPropsTab, false);
    expect(ended).toEqual({ propsTab: 'assistant', prevPropsTab: undefined });
  });

  it('preserves prevPropsTab when switching directly from one tool to another', () => {
    const next = nextPropsTabForTool('tool', 'assistant', true);
    expect(next).toEqual({ propsTab: 'tool', prevPropsTab: 'assistant' });
  });

  it('keeps current non-tool tab when no tool is active', () => {
    expect(nextPropsTabForTool('assistant', undefined, false)).toEqual({
      propsTab: 'assistant',
      prevPropsTab: undefined,
    });
    expect(nextPropsTabForTool('properties', undefined, false)).toEqual({
      propsTab: 'properties',
      prevPropsTab: undefined,
    });
  });

  it('falls back to properties if tool ends without prevPropsTab', () => {
    expect(nextPropsTabForTool('tool', undefined, false)).toEqual({
      propsTab: 'properties',
      prevPropsTab: undefined,
    });
  });
});
