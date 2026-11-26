---
title: Archicad to the CDE
order: 128
summary: Send the model open in Archicad to your CDE project as a reference document with one command, without Lapin open, using Tapir and a personal access token.
---

If your office draws in Archicad and keeps its documents on a CDE server (Atrium Server, such as cde.lapinbim.com), a small script can save the open model as an IFC file and add it to a project as a **reference document of type Model**, from the terminal, with Lapin closed. The script is `scripts/archicad-to-atrium.mjs` in Lapin's repository. It needs Node 22 and nothing else (no packages).

With Lapin open, the other route is **File → Archicad → Receive…**, then **Save to server**; this page is for the case where you only want the file in the CDE.

## Set up, once

1. **Install Tapir** in Archicad: download the add-on for your Archicad version from [Tapir's releases](https://github.com/ENZYME-APD/tapir-archicad-automation/releases) (MIT licence), put it in Archicad's add-ons folder, and restart Archicad. The script needs **Tapir 2.0 or newer**; it says so when it is older.
2. **Make an access token** in the CDE: sign in to the server, open **Access tokens**, **New token**, give it a name, the scope **read+write** and an expiry, and copy it when it is shown (it is shown once).
3. **Put the token in your environment**, never on the command line, in a project or in a chat:

   ```sh
   export ATRIUM_TOKEN=atrium_pat_…
   ```

   On Windows PowerShell: `$env:ATRIUM_TOKEN = "atrium_pat_…"`. A token ends with its expiry, and you can revoke it at any time from the same panel.

## Run it

Open the project in Archicad, then, from Lapin's folder:

```sh
node scripts/archicad-to-atrium.mjs --project <the project's id>
```

It finds the running Archicad (ports 19723–19744), saves the IFC to a temporary file with the export translator Archicad's own Save dialog would use, sends it to the server and prints what it added: the file's size and SHA-256, the document's id and its folder. The temporary file is removed afterwards. Without `--project` it uses your only project, or lists them.

| Option | What it does |
|---|---|
| `--server <url>` | The CDE's address; `https://cde.lapinbim.com` by default. For a server on your own computer, `http://127.0.0.1:5280`. |
| `--project <id>` | The project; when left out and you have several, the script lists them. |
| `--title`, `--discipline`, `--phase`, `--folder` | The document's fields. The folder is the Model type's folder by default. |
| `--port <n>` | Which Archicad, when several are open; `--list-instances` shows them with their project names. |
| `--translator <name>` | An IFC export translator by name; `--list-translators` shows them. `--elements` then chooses what is exported. |
| `--ifczip` | Saves a compressed `.ifczip` (Archicad writes it), much smaller. |
| `--out <file>` | Keeps the IFC at this path. |
| `--no-project-info` | Does not propose Archicad's Project Info (see below). |
| `--dry-run` | Exports the file and stops; nothing goes to the server. |

Exit codes: 0 done, 1 failed, 2 wrong use, 3 Archicad or Tapir, 4 the server (token, rights, refusal).

After the model, the script reads Archicad's **Project Info** and proposes it to the project's information on the server (the MCP tool `cde_project_info_propose`, source Archicad, naming the document just added). It prints the proposal's number and how many changes it holds, or that nothing differs. An owner applies it in the CDE's **Publishing → Project information** tab; nothing changes on the server until then. If Archicad has a dialog open, or the server is older than this feature, the script says so and the model stays added. `--no-project-info` skips it.

## Good to know

- The script only **reads** from Archicad and saves one file to the temporary folder. It does not save, change or close your project.
- Archicad answers "ongoing user input" while a command or dialog is open; the script waits for it to be free.
- Archicad's **demo mode** exports the model as normal. A named translator may be refused (the export then says "Failed to save the project as IFC"); leave `--translator` out to use Archicad's default.
- A file of up to 12 MiB goes inside the call; a larger one goes through the server's resumable upload, in chunks, and continues where it stopped when run again.
- The token reaches only the server's MCP address; it is never written to disk by the script.

## From Archicad's Python palette

The script is a command-line program. If your Python palette runs a normal Python, it can start it with `subprocess.run(["node", "scripts/archicad-to-atrium.mjs", "--project", "<id>"], env={**os.environ, "ATRIUM_TOKEN": token})`, taking the token from the environment. We have not tested this from the palette, so run it from a terminal if it does not work there.
