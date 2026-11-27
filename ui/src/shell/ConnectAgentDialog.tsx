/**
 * Help → Connect an AI agent… (desktop only): the commands that connect an MCP client to the open Lapin through the bridge
 * script bundled in the installed app (docs/mcp.md, docs/user/mcp.md). The path is the app's own resource folder.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify } from '../app/store';
import { claudeCodeCommand, cleanPath, mcpServersJson } from '../app/mcpConnect';
import { useDialogFocus, useEscapeClose } from './dialogFocus';
import { openLink } from './support';

const copy = (text: string) => void navigator.clipboard.writeText(text).then(() => notify('Copied to the clipboard.'), () => notify('Copy failed.'));

function Snippet({ text }: { text: string }) {
  return (
    <div className="agent-snippet">
      <pre data-no-i18n tabIndex={0}>{text}</pre>
      <button type="button" className="btn" onClick={() => copy(text)}>Copy</button>
    </div>
  );
}

export function ConnectAgentDialog({ onClose }: { onClose: () => void }) {
  const focus = useDialogFocus<HTMLDivElement>();
  useEscapeClose(onClose);
  const [path, setPath] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  useEffect(() => {
    void (async () => {
      try {
        const { invoke } = await import('@tauri-apps/api/core');
        setPath(cleanPath(await invoke<string>('mcp_script_path')));
      } catch (e) { setProblem(e instanceof Error ? e.message : String(e)); }
    })();
  }, []);
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Connect an AI agent" tabIndex={-1} ref={focus}>
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Connect an AI agent</span>
          <button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p>Connect an AI agent (Claude Code, Claude Desktop, Gemini CLI or any other MCP client) to the Lapin that is open now. The agent drives the model through the same commands as the interface: its edits show at once, and each one is one undo step.</p>
          <p className="muted small">It needs Node.js 22 or newer on this computer (nodejs.org), and Lapin open while the agent works.</p>
          {problem && <><p className="error-text" role="alert">{problem}</p><p className="muted small">Use scripts/lapin-mcp.mjs from the source folder instead.</p></>}
          {path && <>
            <h4>The bridge script</h4>
            <Snippet text={path} />
            <h4>Claude Code</h4>
            <Snippet text={claudeCodeCommand(path)} />
            <h4>Claude Desktop, Gemini CLI and other clients with an mcpServers setting</h4>
            <p className="muted small">Add this to the client's settings file (Claude Desktop: claude_desktop_config.json; Gemini CLI: settings.json), then restart the client.</p>
            <Snippet text={mcpServersJson(path)} />
          </>}
          <div className="row">
            <button type="button" className="link" onClick={() => openLink('guide')}>User guide: Connect an AI agent</button>
            <span className="grow" />
            <button type="button" className="btn" onClick={onClose}>Close</button>
          </div>
        </div>
      </div>
    </div>
  );
}
