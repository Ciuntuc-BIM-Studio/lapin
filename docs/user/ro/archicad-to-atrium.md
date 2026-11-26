---
title: Din Archicad în CDE
order: 128
summary: Trimiți modelul deschis în Archicad într-un proiect de pe serverul CDE, ca document de referință, cu o singură comandă și fără Lapin deschis, folosind Tapir și un token de acces personal.
---

Dacă biroul tău desenează în Archicad și ține documentele pe un server CDE (Atrium Server, cum e cde.lapinbim.com), un script mic poate salva modelul deschis ca fișier IFC și îl adaugă într-un proiect ca **document de referință de tip Model**, din terminal, cu Lapin închis. Scriptul este `scripts/archicad-to-atrium.mjs` din depozitul Lapin. Are nevoie de Node 22 și de nimic altceva (niciun pachet).

Cu Lapin deschis, cealaltă cale este **Fișier → Archicad → Primește…**, apoi **Salvează pe server**; pagina aceasta e pentru cazul în care vrei doar fișierul în CDE.

## Pregătirea, o singură dată

1. **Instalează Tapir** în Archicad: descarcă add-on-ul pentru versiunea ta de Archicad din [versiunile Tapir](https://github.com/ENZYME-APD/tapir-archicad-automation/releases) (licență MIT), pune-l în folderul de add-on-uri al Archicad și repornește Archicad. Scriptul cere **Tapir 2.0 sau mai nou** și spune asta când e mai vechi.
2. **Fă un token de acces** în CDE: te autentifici pe server, deschizi **Tokenuri de acces**, **Token nou**, îi dai un nume, domeniul **citire+scriere** și o expirare, și îl copiezi când apare (apare o singură dată).
3. **Pune tokenul în mediul tău** (variabilă de mediu), niciodată în linia de comandă, într-un proiect sau într-o conversație:

   ```sh
   export ATRIUM_TOKEN=atrium_pat_…
   ```

   În Windows PowerShell: `$env:ATRIUM_TOKEN = "atrium_pat_…"`. Tokenul expiră la data aleasă și îl poți revoca oricând din același panou.

## Rularea

Deschide proiectul în Archicad, apoi, din folderul Lapin:

```sh
node scripts/archicad-to-atrium.mjs --project <id-ul proiectului>
```

Scriptul găsește Archicad-ul care rulează (porturile 19723–19744), salvează IFC-ul într-un fișier temporar cu translatorul de export pe care l-ar folosi fereastra de salvare a Archicad, îl trimite serverului și scrie ce a adăugat: mărimea fișierului și SHA-256, id-ul documentului și dosarul lui. Fișierul temporar se șterge după aceea. Fără `--project` folosește singurul tău proiect sau le listează.

| Opțiune | Ce face |
|---|---|
| `--server <url>` | Adresa CDE-ului; implicit `https://cde.lapinbim.com`. Pentru un server pe calculatorul tău, `http://127.0.0.1:5280`. |
| `--project <id>` | Proiectul; dacă lipsește și ai mai multe, scriptul le listează. |
| `--title`, `--discipline`, `--phase`, `--folder` | Câmpurile documentului. Dosarul implicit este cel al tipului Model. |
| `--port <n>` | Care Archicad, când sunt mai multe deschise; `--list-instances` le arată cu numele proiectelor. |
| `--translator <nume>` | Un translator de export IFC după nume; `--list-translators` îi arată. `--elements` alege atunci ce se exportă. |
| `--ifczip` | Salvează un `.ifczip` comprimat (îl scrie Archicad), mult mai mic. |
| `--out <fișier>` | Păstrează IFC-ul la această cale. |
| `--dry-run` | Exportă fișierul și se oprește; nimic nu ajunge pe server. |

Coduri de ieșire: 0 făcut, 1 eșuat, 2 folosire greșită, 3 Archicad sau Tapir, 4 serverul (token, drepturi, refuz).

## Bine de știut

- Scriptul doar **citește** din Archicad și salvează un fișier în folderul temporar. Nu salvează, nu schimbă și nu închide proiectul tău.
- Archicad răspunde „ongoing user input” cât o comandă sau o fereastră e deschisă; scriptul așteaptă să se elibereze.
- **Modul demo** al Archicad exportă modelul normal. Un translator numit poate fi refuzat (exportul spune atunci „Failed to save the project as IFC”); lasă `--translator` deoparte ca să folosești translatorul implicit al Archicad.
- Un fișier de până la 12 MiB merge în apel; unul mai mare merge prin încărcarea reluabilă a serverului, pe bucăți, și continuă de unde s-a oprit când rulezi din nou.
- Tokenul ajunge doar la adresa MCP a serverului; scriptul nu îl scrie niciodată pe disc.

## Din paleta Python a Archicad

Scriptul este un program din linia de comandă. Dacă paleta ta Python rulează un Python obișnuit, îl poate porni cu `subprocess.run(["node", "scripts/archicad-to-atrium.mjs", "--project", "<id>"], env={**os.environ, "ATRIUM_TOKEN": token})`, luând tokenul din mediu. Nu am încercat asta din paletă, așa că rulează-l dintr-un terminal dacă acolo nu merge.
