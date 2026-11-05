# Piesele scrise: documente din șabloane, completate din proiect

Cerut de utilizator la 04.11.2026: „pentru a genera documentație text folosind un template și rapoarte introduse dinamic din proiect, cum ar fi mai bine să facem? integrare cu LibreOffice, un fel de bridge, sau modul dedicat in-app?”, apoi „șablonul ar fi interesant să îl facem vizual în aplicație, apoi export, sau un MCP cu LibreOffice sau Google Docs”. Răspunsul convenit: **un model de document al lui Lapin**, completat din proiect și exportat de Lapin în DOCX, ODT și PDF, fără dependențe; editorul vizual peste el; MCP doar ca drum separat, pentru asistent. Aprobat la 05.11.2026 („sunt de acord. 1 și 3 putem să începem să scriem planul și să implementăm”): etapele 1 și 3 întâi. Etapele 1 și 3 făcute (decizia 311); etapele 2, 4 și 5 făcute (decizia 321).

## De ce așa

- **Nu un bridge cu LibreOffice.** Ar cere LibreOffice instalat (300 MB, o versiune pe care n-o controlăm), UNO e fragil între versiuni și platforme, iar varianta web n-ar avea nimic. LibreOffice rămâne doar unealta cu care testele verifică, pe Mac, că fișierele scrise de Lapin se deschid (`scripts/documents-check.mjs`).
- **Nu un Word în aplicație.** Un procesor de text complet înseamnă luni, iar beneficiarii și avizatorii cer oricum .docx sau PDF. Lapin face **documentul legat de proiect**: structura, câmpurile, tabelele și imaginile din model; ce rămâne de lustruit se face în Word sau LibreOffice, pe fișierul exportat.
- **Formatele le scriem noi**, ca la IFC, USD, BCF, Fragments și PDF: DOCX (OOXML) și ODT (ODF) sunt arhive zip cu XML. Licența MIT rămâne curată și merge și în browser.
- **MCP** (`docs/mcp.md`) primește mai târziu uneltele pentru documente (câmpurile, tabelele, imaginile), ca un asistent să poată scrie, de exemplu, în Google Docs; nu e baza funcției (cont, cloud, datele clientului ieșite din firmă).

## Ce există deja

| Ce | Unde | Ce aduce |
|---|---|---|
| Extrasele: tâmplărie, buiandrugi, ale modulelor (încăperi, finisaje, lemn, oțel, armătură, cofraje, schele, MEP, profile de perete, deviz) | `ScheduleEntity` (architecture), `IScheduleSource`, `ScheduleGrid` | tabelele documentelor, aceleași ca pe planșe și în XLSX |
| Scriitorul PDF vectorial, cu literele românești | `Lapin.Drawing/Export/PdfWriter.cs` | PDF-ul documentelor (text, tabele, desene vectoriale) |
| XLSX scris și citit fără dependențe | `XlsxWriter`, `XlsxReader` | același mod de a scrie un zip OOXML |
| Vederile ca desen (creioane), puse pe planșe la scară | `Sheets.cs`, `Views` | imaginile din documente: o vedere la o scară sau o lățime |
| Emiterea: verificări, PDF-urile planșelor, IFC, borderoul, SHA-256 | `Lapin.Platform/Cde` | un document intră în emitere ca planșele |
| Indicatorul planșei: proiect, revizie, stare, cine a desenat și a verificat | `Sheets.cs` (`titleBlock`) | câmpurile de proiect, aceleași |
| Câmpurile etichetelor `{mark}`, `{Fire rating}`, Pset-uri | `TagText` | aceeași regulă de citire a parametrilor unui element |

## Modelul

### Documentul (`LapinDocument`)

O entitate a proiectului, ca `LapinIssue` și `LapinRemark`, în `Lapin.Platform/Documents`. Atribute:

- `name`, `template` (id-ul șablonului din care a pornit, pentru „Update from template”), `lang` (`ro`, `en`…: formatul numerelor și al datelor, separatorul zecimal), `page` (`{size: A4|A3|Letter, orientation, margins: [sus, dreapta, jos, stânga] mm, header, footer}`; antetul și subsolul sunt text cu câmpuri, `{{page}}` și `{{pages}}` incluse);
- `blocks`: lista de blocuri (mai jos);
- `inputs`: valorile scrise de utilizator pentru câmpurile de intrare ale șablonului (`{"structura": "Structură din cadre de beton armat…"}`);
- `revision`, `status` (ca la planșe; emiterea le scrie).

Un **șablon** are aceeași formă, plus `id`, `title`, `description`, `inputs` declarate (`[{key, label, multiline, default, group}]`). Șabloanele de pornire sunt în engine (resurse JSON în `Lapin.Platform/Documents/Templates/`); cele ale firmei (etapa 4) stau în biblioteca aplicației, ca listele de prețuri.

### Blocurile

```jsonc
{ "kind": "heading", "level": 1, "text": "Memoriu tehnic de arhitectură" }
{ "kind": "paragraph", "text": "Beneficiar: **{{project.client}}**, cu sediul în {{project.clientAddress}}." }
{ "kind": "list", "ordered": false, "items": ["Aria construită: {{areas.built}}", "Aria desfășurată: {{areas.gross}}"] }
{ "kind": "fields", "rows": [["Denumirea proiectului", "{{project.name}}"], ["Faza", "{{project.phase}}"]] }  // tabel cheie–valoare
{ "kind": "table", "source": "schedule:<id>" | "kind:<kind>", "title": "…", "columns": ["Mark", "Width"]?, "total": true? }
{ "kind": "image", "view": "<viewId>", "scale": 100?, "width": 170?, "caption": "…" }  // mm pe hârtie
{ "kind": "repeat", "over": "storeys" | "rooms" | "sheets" | "wallTypes" | …, "where": {"storey": "{{storey.id}}"}?, "blocks": [ … {{room.name}} … ] }
{ "kind": "if", "field": "areas.basement", "blocks": [ … ] }            // doar când câmpul are valoare (nu e gol, nu e 0)
{ "kind": "input", "key": "structura" }                               // textul scris de utilizator (paragrafe, ** și * ca mai jos)
{ "kind": "toc" }                                                      // cuprinsul
{ "kind": "pageBreak" }
```

Textul are puțină formatare, aceeași peste tot: `**îngroșat**`, `*cursiv*`, un rând nou ca paragraf nou în `input`. Un câmp necunoscut sau gol rămâne vizibil în previzualizare (`⟨project.client⟩`, marcat), iar la export apare ca text gol, iar exportul îl raportează în lista „de completat”.

### Câmpurile (`IDocumentFields`)

Un serviciu prin platformă, ca `IScheduleSource`: fiecare modul își dă câmpurile și colecțiile. `document.fields` le listează pe toate, cu eticheta și valoarea de acum, pentru dialog (și pentru editorul din etapa 2).

```csharp
public sealed record DocField(string Key, string Label, string? Value);           // Value: deja scris, în unitățile proiectului și formatul limbii
public sealed record DocCollection(string Key, string Label, string ItemPrefix);  // "rooms" → elementele lui {{room.*}}
public interface IDocumentFields
{
    IEnumerable<DocField> Fields(IQueryContext q, DocFormat f);
    IEnumerable<DocCollection> Collections => [];
    /// <summary>Fiecare element al colecției ca un set de câmpuri (fără prefix: "name", "area"); where filtrează după câmpurile lor.</summary>
    IEnumerable<IReadOnlyDictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f) => [];
}
public sealed record DocFormat(string Lang, string Units);  // ro: 1.234,56 m²; en: 1,234.56 m²
```

- **Platforma (core):** `project.*`, datele proiectului (mai jos); `site.*` (adresa, numărul cadastral, cartea funciară, sistemul de coordonate); `date.today`; `cde.*` (codul proiectului, emitentul, ultima emitere, revizia, starea); colecțiile `storeys` (nume, cota, înălțimea) și `sheets` (număr, titlu, scara, revizia: borderoul), `documents` (ale proiectului, pentru borderou).
- **Arhitectura:** `areas.built` (aria construită: amprenta la sol a pereților exteriori și a plăcii parterului), `areas.gross` (aria desfășurată: suma pe niveluri), `areas.usable` (aria utilă: suma încăperilor), `areas.<storeyName>`, `counts.doors`, `counts.windows`, `height.max` (cota cea mai înaltă, de la ±0,00), `levels.text` („S+P+1E+M”, din numele și cotele nivelurilor); colecțiile `rooms` (nivel, număr, nume, funcțiune, aria netă, finisajele), `wallTypes` (numele, grosimea, straturile ca text: „cărămidă 25 cm + EPS 10 cm + tencuială 2 cm”), `slabTypes`, `roofs` (forma, panta, învelitoarea), `joineryTypes`.
- **Celelalte module, când există:** `cost.*` (totalurile devizului), `planning.*` (start, final, durata), `urban.*` (POT, CUT, regimul de înălțime: modulul premium), `structure.*`. Fiecare modul își adaugă câmpurile când e gata: se adaugă doar serviciul, fără nicio legătură între module.

### Datele proiectului (Manage → Project information…)

Pe `IfcProject`, `info`: `number` (nr. proiectului), `phase` (SF, DTAC, PT, DE…), `client`, `clientAddress`, `address` (amplasamentul), `cadastral` (nr. cadastral), `landBook` (CF), `designer` (firma), `designerAddress`, `chiefArchitect`, `author`, `checkedBy`, plus parametrii utilizatorului pe proiect (`{{param.Nume}}`). Indicatorul planșelor le ia de aici când planșa nu le are pe ale ei (`project`, `drawnBy`, `checkedBy`): un singur loc de scris.

### Tabelele (`IDocumentTables`)

`ScheduleEntity` (arhitectura) dă orice extras al proiectului după id (`schedule:<id>`, cu coloanele, sortarea și totalurile lui, cum apare pe planșă) sau după tip (`kind:door`, `kind:rooms`, `kind:wallSweeps`…; tâmplăria a lui, restul prin `IScheduleSource`). Fără arhitectură, platforma dă direct tabelele `IScheduleSource`. Un bloc poate alege coloanele (`columns`) și poate cere rândul de total.

### Imaginile

O vedere (plan, secțiune, fațadă, 3D salvat, planșă) desenată de engine la scara blocului, decupată la limitele ei sau la `boundary`, lățimea cel mult cât pagina. În PDF e vectorială, cu aceleași creioane ca pe planșă. În DOCX și ODT: SVG-ul ei și un PNG de 200 dpi pentru programele care nu citesc SVG. Le face interfața, care are deja desenarea creioanelor în SVG (tipărirea, decizia 236) și un canvas: `document.images` dă creioanele fiecărei imagini, interfața le întoarce ca SVG și PNG la `document.export`. Fără interfață (MCP, linia de comandă), DOCX și ODT primesc doar SVG-ul.

## Exportul

- **DOCX** (`DocxWriter`): `word/document.xml`, `styles.xml` (Titlu 1–3, Normal, Tabel, Listă, Legendă, cu numele stilurilor Word, ca să fie reluate de un șablon al firmei), `numbering.xml` (liste), antet și subsol cu câmpurile `PAGE` / `NUMPAGES`, secțiunea (formatul, orientarea, marginile), cuprinsul ca câmp `TOC` (Word îl actualizează la deschidere: `w:updateFields`), tabelele cu rândul de cap repetat pe fiecare pagină, imaginile inline (PNG + `asvg:svgBlip`), `docProps/core.xml` (titlul, autorul, revizia).
- **ODT** (`OdtWriter`): `mimetype` primul, necomprimat; `content.xml`, `styles.xml` (aceleași stiluri, cu nume ODF), `meta.xml`, `META-INF/manifest.xml`; imaginile ca `draw:frame` cu SVG și PNG ca alternativă; cuprinsul ca `text:table-of-content`; numerotarea paginilor ca `text:page-number`.
- **PDF** (`DocumentPdf`): paginarea făcută de Lapin, cu lățimile reale ale literelor Helvetica și Helvetica-Bold (tabelele AFM publice ale fonturilor standard PDF): rânduri despărțite pe cuvinte, titlurile neseparate de paragraful de după, tabelele pe mai multe pagini cu capul repetat, imaginile vectoriale, antetul și subsolul cu numerotarea paginilor, cuprinsul cu paginile reale (calculat în două treceri), marcajele PDF (outline) pe titluri.
- Toate trei pornesc din același **document completat** (`FilledDocument`: blocurile cu textul gata, tabelele cu rândurile, imaginile cu creioanele), deci spun același lucru.

## Interfața, etapa 1 (înainte de editorul vizual)

**Manage → Documents…** (și în browserul proiectului, un set **Documents** sub Sheets):

- în stânga, documentele proiectului și **New from template** (șabloanele cu descrierea lor);
- în mijloc, previzualizarea documentului completat (HTML: pagini A4 una sub alta, tabelele și imaginile reale), câmpurile goale marcate;
- în dreapta, două taburi: **Inputs** (textele de scris ale șablonului, cu grupurile lor) și **Project information** (datele proiectului, aceleași ca în Manage → Project information…), plus lista **To complete** (câmpurile goale, un clic duce la locul lor);
- jos: **Export DOCX**, **Export ODT**, **Export PDF**, **Update from template**, **Rename**, **Duplicate**, **Delete**.

**Manage → Issue…** primește lista documentelor: fiecare bifat iese ca PDF (și DOCX, la cerere) în emitere, cu numele după ISO 19650 (tipul `RP` pentru rapoarte și memorii, `SP` pentru caiete de sarcini), SHA-256 și borderou; documentul primește revizia și starea emiterii.

## Șabloanele de pornire (etapa 3), în română

1. **Memoriu tehnic de arhitectură**, după conținutul-cadru al legii 50/1991 (anexa 1) pentru DTAC și practica pentru PT:
   - date generale (denumirea, amplasamentul, beneficiarul, proiectantul, numărul și faza);
   - situația existentă (intrare);
   - descrierea construcției: funcțiunea, regimul de înălțime, suprafețele (construită, desfășurată, utilă, pe niveluri), înălțimea maximă;
   - spațiile pe niveluri (tabel din încăperi, repetat pe nivel);
   - structura (intrare);
   - închiderile și compartimentările (din tipurile de perete, cu straturile);
   - planșeele și acoperișul (din tipurile de placă și din acoperișuri);
   - tâmplăria (extrasul de tâmplărie);
   - finisajele (pe încăperi);
   - instalațiile, siguranța la foc, accesibilitatea, eficiența energetică (intrări);
   - organizarea de șantier (intrare);
   - semnăturile.
2. **Caiet de sarcini: tâmplărie și finisaje**:
   - generalități (intrare);
   - materialele folosite (din materialele proiectului, pe categorii);
   - tâmplăria (extrasul, cu dimensiunile și numărul);
   - finisajele pe încăperi (pardoseli, pereți, tavane);
   - execuția și recepția (intrări cu texte implicite).
3. **Borderou de piese scrise și desenate**: documentele proiectului (piesele scrise) și planșele (număr, titlu, scara, revizia), din colecțiile `documents` și `sheets`.

Textele implicite ale intrărilor sunt scrise de noi, generale, de completat de proiectant. Lapin nu dă texte normative pe care nu le poate verifica.

## Etape

1. **Modelul, câmpurile, completarea și exportul** (engine): `LapinDocument`, blocurile, `IDocumentFields` (platforma și arhitectura), datele proiectului (și indicatorul care le citește), `IDocumentTables`, `FilledDocument`, DOCX, ODT, PDF, comenzile și interogările (`document.create {template, name?}`, `document.update {id, …}`, `document.delete {id}`, `document.duplicate {id}`, `document.refresh {id}` (din șablon, cu intrările păstrate), `project.setInfo {…}`; interogările `document.templates`, `document.fields {id?}`, `document.preview {id}`, `document.images {id}`, `document.export {id, format, images?}` → base64); emiterea. Interfața din secțiunea de mai sus. Testele: completarea pe o casă de test, fiecare format citit înapoi (XML valid, textul, tabelele, imaginile), paginarea PDF; `scripts/documents-check.mjs` convertește cu LibreOffice, când e instalat, și compară numărul de pagini și textul.
2. **Editorul vizual** (ProseMirror sau TipTap, MIT): documentul editat pe pagină, câmpurile ca etichete cu valoarea reală, tabelele și imaginile din proiect inserate dintr-un meniu, blocurile `repeat` și `if` ca zone marcate, stilurile; salvarea ca șablon al proiectului sau al firmei.
3. **Șabloanele de pornire** (de mai sus), în română; apoi în engleză.
4. **Biblioteca firmei**: șabloane salvate în aplicație și refolosite, importul unui DOCX al firmei ca stiluri (antet, logo, fonturi), exportul unui șablon.
5. **MCP**: `document.fields`, tabelele și imaginile ca unelte pentru asistent.

## Etapele 2, 4 și 5, în detaliu

Cerut la 05.11.2026: „continuă cu etapele următoare ce intră în free”. Toate trei sunt **gratuite**, în ediția `lapin`: editorul, șabloanele proiectului și ale calculatorului, stilul firmei și uneltele MCP. Premium ar putea fi mai târziu doar o bibliotecă de firmă pe un server, comună tuturor calculatoarelor ei, ca legătura SharePoint de la CDE.

### Etapa 2: editorul

- **Butonul Edit** din Manage → Documents… face pagina din mijloc editabilă (TipTap peste ProseMirror, MIT). Se editează blocurile documentului, adică copia lui proprie a șablonului; **Save as template** le duce mai departe (etapa 4).
- **Textul**: titluri 1–3, paragrafe, liste cu buline și numerotate, îngroșat, cursiv; bara de sus are stilurile, iar scurtăturile sunt cele obișnuite (Ctrl+B, Ctrl+I, Ctrl+Z).
- **Câmpurile** sunt etichete în text: `{{project.client}}` apare ca „Beneficiar: Ionescu Maria”, cu valoarea de acum. **Insert → Field** caută în `document.fields`, cu valorile lor. Într-un bloc repetat, sunt oferite și câmpurile elementului (`room.name`).
- **Blocurile din proiect** sunt noduri atomice, desenate cu valorile reale prin `document.block {id, block, scope?}`, completarea unui singur bloc; un clic deschide setările lor într-un panou:
  - tabelul unui extras (sursa, coloanele, titlul, totalul);
  - imaginea unei vederi (vederea, scara sau lățimea, legenda);
  - tabelul cheie–valoare;
  - cuprinsul;
  - saltul de pagină;
  - textul de completat (`input`, cu cheia, eticheta, grupul și textul implicit; definițiile stau în `inputDefs`).
- **Repetările și condițiile** sunt zone marcate, cu o bandă deasupra („Pentru fiecare nivel”, „Doar dacă există subsol”), editabile în ele; ramura `else` e a doua zonă a condiției.
- **Salvarea**: după o pauză în scriere, ca un pas de undo în motor (`document.update {blocks, inputDefs}`); previzualizarea paginată rămâne butonul **Preview**.

### Etapa 4: șabloanele și stilul firmei

- **Save as template…** dintr-un document: în **proiect** (`documentTemplates` pe `IfcProject`, merg cu fișierul) sau în **biblioteca acestui calculator** (ca listele de prețuri: le ia orice proiect de pe calculator). `document.templates` le dă pe toate trei, fiecare cu originea lui (`builtin`, `project`, `library`).
- **Manage → Document templates…**: lista, redenumirea, ștergerea, duplicarea unui șablon încorporat ca să fie schimbat, **Export** ca fișier `.json` și **Import**, ca o firmă să-și dea șabloanele de la un calculator la altul.
- **Stilul** pe șablon și pe document (`page.style`):
  - fontul textului (în DOCX și ODT: Arial, Calibri, Times New Roman, Georgia, Verdana, Liberation Sans; în PDF: Helvetica sau Times, fonturile standard ale PDF-ului, cu lățimile lor AFM);
  - mărimea textului;
  - culoarea titlurilor;
  - **logo-ul** firmei în antet (PNG sau JPEG, cu lățimea în mm), scris în toate trei formatele.
- Importul stilurilor dintr-un DOCX al firmei rămâne pentru mai târziu: logo-ul, fontul și culorile alese în dialog acoperă ce cer cele mai multe firme.

### Etapa 5: MCP

- Bridge-ul ajunge deja la toate comenzile prin `lapin_query` și `lapin_execute`. Se adaugă `lapin_document_export {id, format, path}`, care scrie fișierul pe disc în loc să-l întoarcă în base64, și o secțiune în `docs/mcp.md`: cum face un asistent un document, cum îi scrie intrările și cum îl exportă.

### Ce rămăsese din etapa 1

- Imaginile ajung și în DOCX-ul emis: Manage → Issue… le trimite, făcute de interfață, cu `documentImages`.
- O imagine poate fi decupată la o limită de vedere (`boundary`, `LapinViewBoundary`).

## Răspunsuri

Deschise pentru utilizator, cu ce se face până atunci:

1. Ce alte documente vrei ca șabloane de pornire? (fișa spațiilor, antemăsurătoarea ca piesă scrisă, memoriile pe specialități). Până atunci, cele trei de mai sus.
2. Fonturile: Helvetica în PDF; în DOCX și ODT, Arial (cu Liberation Sans ca înlocuitor). Un font al firmei vine în etapa 4.
