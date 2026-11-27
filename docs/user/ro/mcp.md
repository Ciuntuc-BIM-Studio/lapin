---
title: Conectează un agent AI
order: 129
summary: Lasă un agent AI (Claude Code, Claude Desktop, Gemini CLI sau orice client MCP) să lucreze în Lapin-ul pe care îl ai deschis: ? → Conectează un agent AI îți dă comenzile de copiat; modificările lui apar imediat și fiecare este un singur pas de anulare.
---

Un agent AI poate conduce Lapin-ul pe care îl ai deschis, prin aceleași comenzi ca interfața. Citește modelul, adaugă și schimbă elemente, cere cantități și desene, scrie documente și salvează. Ce face apare imediat în fereastră, iar **fiecare modificare a lui este un singur pas de anulare**: Ctrl+Z o desface ca pe orice ai făcut tu.

Legătura folosește Model Context Protocol (MCP), felul standard în care clienții AI vorbesc cu programele, printr-un script mic care vine cu Lapin. Merge **doar cu aplicația desktop**: Lapin în browser nu are un motor pe calculatorul tău pe care agentul să-l poată ajunge.

## Ce îți trebuie

- Aplicația desktop Lapin, **deschisă** cât lucrează agentul. Dacă ai mai multe Lapin deschise, agentul spune care sunt deschise, iar tu o alegi pe una (vezi mai jos).
- **Node.js 22 sau mai nou** pe calculator (nodejs.org). Lapin nu îl instalează. Într-un terminal, `node --version` arată ce ai.
- Un client AI care rulează servere MCP pe calculatorul tău: Claude Code, Claude Desktop, Gemini CLI sau altul.

## Conectare din aplicația instalată

1. Deschide **? → Conectează un agent AI…**.
2. Fereastra arată unde este scriptul în Lapin-ul tău instalat și câte o comandă pentru fiecare fel de client, cu un buton **Copiază**.
3. **Claude Code**: copiază comanda și rulează-o într-un terminal. Arată așa

   ```sh
   claude mcp add lapin -- node "/Applications/Lapin.app/Contents/Resources/resources/lapin-mcp.mjs"
   ```

   cu calea ta (are ghilimele pentru că o cale poate conține spații).
4. **Claude Desktop, Gemini CLI și alți clienți cu setarea `mcpServers`**: copiază JSON-ul în fișierul de setări al clientului, apoi repornește clientul.

   ```json
   {
     "mcpServers": {
       "lapin": { "command": "node", "args": ["<calea arătată în fereastră>"] }
     }
   }
   ```

   Claude Desktop citește `claude_desktop_config.json` (Settings → Developer → Edit Config îl deschide); Gemini CLI citește `settings.json` din folderul `.gemini`. Dacă fișierul are deja `mcpServers`, adaugă `lapin` lângă celelalte.
5. Orice alt client care pornește un server MCP pe stdio primește aceleași două părți: comanda `node` și calea scriptului ca argument.

Apoi întreabă agentul despre modelul tău, de exemplu „listează pereții de la parter”. Primul lucru pe care îl face este să cheme `lapin_info`, care îi spune produsul, modulele lui și fiecare comandă cu argumentele ei.

## Conectare din folderul sursă

Dacă rulezi Lapin din sursă (`pnpm dev`), folosește în schimb scriptul din depozit, același pe care îl poartă aplicația:

```sh
claude mcp add lapin -- node /calea/către/lapin-pro/scripts/lapin-mcp.mjs
```

Scriptul aplicației este o copie a acestuia, făcută când se construiește aplicația, deci cele două se comportă la fel.

## Ce primește agentul

| Unealta | Ce face |
|---|---|
| `lapin_info` | Produsul, modulele lui, fiecare comandă cu argumentele pe care le citește, interogările și clasele cu câmpurile lor. Agentul o cheamă prima. |
| `lapin_model` | Elementele (id, clasă, nume, nivel, atribute), eventual doar unele clase. |
| `lapin_execute` | O comandă, de exemplu `architecture.addWall`. Lungimile sunt în metri. |
| `lapin_query` | O întrebare care nu schimbă nimic: un desen, un tabel, cota terenului într-un punct, din ce este făcut un element. |
| `lapin_quantities` | Cantitățile unui element. |
| `lapin_documents`, `lapin_document_export` | Documentele scrise ale proiectului și unul scris ca DOCX, ODT sau PDF la o cale de pe calculatorul tău. |
| `lapin_export_jg` | Modelul analitic ca JSON de schimb JG Structures, cu raportul verificărilor lui. |
| `lapin_undo`, `lapin_redo` | Un pas înapoi sau înainte. |
| `lapin_save` | Salvează proiectul. |

Lista completă, cu detaliile fiecăreia, este în `docs/mcp.md` din sursă.

## Mai multe Lapin deschise

Cu un singur Lapin deschis, scriptul îl găsește singur. Cu mai multe, răspunsul agentului le listează; ca să alegi unul, setează o variabilă de mediu pentru script: `LAPIN_PROJECT` (o parte din numele sau calea proiectului) sau `LAPIN_PORT`. În Claude Code:

```sh
claude mcp add lapin -e LAPIN_PROJECT=Casa -- node "<calea>"
```

Într-o setare `mcpServers`, adaugă `"env": { "LAPIN_PROJECT": "Casa" }` lângă `command` și `args`.

## Bine de știut

- Agentul ajunge doar la Lapin pe **propriul tău calculator**: motorul ascultă pe 127.0.0.1 și scriptul nu trimite nimic nicăieri. Ce trimite clientul AI însuși furnizorului lui ține de acel client, așa că citește-i condițiile înainte să-l lași să vadă un proiect confidențial.
- Un agent poate greși. Uită-te la ce a făcut și anulează orice pas nu îl vrei.
- Este separat de **adresa MCP a serverului CDE**, care lasă un client AI să ajungă la documentele și problemele unui proiect de pe Atrium Server, fără Lapin deschis. Aceea se configurează în [Lucrul pe un server CDE](cde-server.md).
