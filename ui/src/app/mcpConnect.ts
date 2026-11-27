/**
 * Help → Connect an AI agent: the lines that connect an MCP client (Claude Code, Claude Desktop, Gemini CLI, any stdio
 * client) to the open Lapin through the bridge script bundled in the installed app (scripts/lapin-mcp.mjs, docs/mcp.md).
 * Pure text builders: the path of the script comes from the desktop shell (`mcp_script_path`).
 */

/** The path as the user's shell takes it: Windows' `\\?\` prefix (what Tauri's resource folder can carry) dropped. */
export const cleanPath = (path: string): string => path.replace(/^\\\\\?\\(?=[A-Za-z]:\\)/, '').replace(/^\\\\\?\\UNC\\/, '\\\\');

/** Whether a path is a Windows one (a drive letter or a share). */
export const isWindowsPath = (path: string): boolean => /^[A-Za-z]:[\\/]/.test(path) || path.startsWith('\\\\');

/**
 * The path as one shell argument, in double quotes: on Windows (cmd and PowerShell take `"…"` as it is, a path has no
 * quote in it); elsewhere with `\`, `"`, `$` and the backtick escaped, so a folder such as `Application Support` or one
 * with a quote in its name stays one argument.
 */
export function quotePath(path: string): string {
  const p = cleanPath(path);
  if (isWindowsPath(p)) return `"${p}"`;
  return `"${p.replace(/[\\"$`]/g, '\\$&')}"`;
}

/** Claude Code: `claude mcp add lapin -- node "<path>"`. */
export const claudeCodeCommand = (path: string): string => `claude mcp add lapin -- node ${quotePath(path)}`;

/** The `mcpServers` setting for Claude Desktop (claude_desktop_config.json), Gemini CLI (settings.json) and the clients that read the same shape; backslashes are escaped by JSON. */
export const mcpServersJson = (path: string): string =>
  JSON.stringify({ mcpServers: { lapin: { command: 'node', args: [cleanPath(path)] } } }, null, 2);
