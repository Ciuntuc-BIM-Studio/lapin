import { describe, expect, it } from 'vitest';
import { claudeCodeCommand, cleanPath, mcpServersJson, quotePath } from './mcpConnect';

const MAC = '/Applications/Lapin.app/Contents/Resources/resources/lapin-mcp.mjs';
const SUPPORT = '/Users/me/Library/Application Support/Lapin/resources/lapin-mcp.mjs';
const WIN = 'C:\\Program Files\\Lapin\\resources\\lapin-mcp.mjs';

describe('connecting an MCP client', () => {
  it('quotes a path so a space stays inside one argument', () => {
    expect(claudeCodeCommand(MAC)).toBe(`claude mcp add lapin -- node "${MAC}"`);
    expect(claudeCodeCommand(SUPPORT)).toBe(`claude mcp add lapin -- node "${SUPPORT}"`);
  });

  it('escapes what a POSIX shell reads inside double quotes', () => {
    expect(quotePath('/a/b "c"/$HOME/`x`/lapin-mcp.mjs')).toBe('"/a/b \\"c\\"/\\$HOME/\\`x\\`/lapin-mcp.mjs"');
  });

  it('keeps a Windows path as it is in the command (no escaping of backslashes) and drops the \\\\?\\ prefix', () => {
    expect(claudeCodeCommand(WIN)).toBe(`claude mcp add lapin -- node "${WIN}"`);
    expect(cleanPath(`\\\\?\\${WIN}`)).toBe(WIN);
    expect(cleanPath('\\\\?\\UNC\\server\\share\\lapin-mcp.mjs')).toBe('\\\\server\\share\\lapin-mcp.mjs');
    expect(quotePath(`\\\\?\\${WIN}`)).toBe(`"${WIN}"`);
  });

  it('writes the mcpServers JSON, backslashes escaped, that parses back to the path', () => {
    const json = mcpServersJson(WIN);
    expect(json).toContain('C:\\\\Program Files\\\\Lapin');
    expect(JSON.parse(json)).toEqual({ mcpServers: { lapin: { command: 'node', args: [WIN] } } });
    expect(JSON.parse(mcpServersJson(SUPPORT)).mcpServers.lapin.args[0]).toBe(SUPPORT);
  });
});
