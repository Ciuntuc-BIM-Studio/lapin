---
title: Scripturi Python
order: 127
summary: Conduci Lapin cu Python din panoul Script, un extra pentru dezvoltatori: un notebook cu celule Python, Markdown și AI care rulează Python-ul tău, fiecare rulare un singur pas de anulare.
---

## Pornește Extra pentru dezvoltatori

Lapin poate fi condus cu Python: fiecare unealtă e o comandă, iar un script trimite aceleași comenzi. Panoul **Script** e un *extra pentru dezvoltatori*, oprit până îl pornești, așa că altfel nu se vede nimic din el.

Deschide **Fișier → Preferințe…** și, la **General**, bifează **Extra pentru dezvoltatori**.

![Preferințe: Extra pentru dezvoltatori](shots/python-preferences.webp)

- **Python**: Python-ul pe care îl rulează panoul. Gol, Lapin caută unul pe acest calculator (`python3`, `py -3` pe Windows, cel de la python.org și cel din Homebrew). Trebuie să fie **Python 3.11 sau mai nou**; Lapin nu instalează Python. Ia-l de pe [python.org](https://www.python.org/downloads/) dacă nu ai, sau scrie calea celui pe care îl vrei (al unui mediu virtual, de exemplu).
- **Editor extern**: editorul pe care îl folosește **Deschide în editorul extern**, ca o comandă (`code` pentru Visual Studio Code). Gol, editorul de text al sistemului.

Python rulează doar în aplicația desktop: în Lapin pentru browser, panoul spune asta.

## Panoul Script: un notebook

Deschide **Vedere → Script**. Notebook-ul se deschide ca o vedere lângă plan sau 3D, cu fereastra împărțită în două; poate fi mutat într-o fereastră a lui, ca orice vedere. Antetul lui spune ce Python a găsit sau de ce n-a găsit niciunul.

![Panoul Script după o rulare: patru pereți, un singur pas de anulare](shots/python-panel.webp)

- **Ale mele** și **Ale proiectului**, în stânga (primul buton din antet le arată sau le ascunde): scripturile și notebook-urile tale, fișiere `.py` obișnuite, păstrate în folderul lui Lapin de pe acest calculator, și cele ale proiectului. Dă clic pe unul ca să-l deschizi; **Notebook nou** pornește de la un exemplu.
- **Celulele**, la mijloc. Un notebook e o listă de celule, fiecare cu butoanele ei:
  - **Python**: cod, cu culorile și indentarea lui. Scrii `lapin.` și îți propune ce are Lapin (comenzile tuturor modulelor ediției tale și apelurile lui proprii); `lapin.architecture.` propune comenzile de arhitectură cu argumentele lor. Se editează doar celula pe care dai clic; celelalte își arată codul.
  - **Markdown**: notițe, titluri, liste, tabele și cod, arătate formatat; **Editează** (sau dublu clic) le scrie, **Previzualizare** sau **Shift+Enter** le arată din nou.
  - **Prompt AI → Python**: o cerere în cuvintele tale, pe care un model al Ollama-ului tău o transformă în Python în celula de sub ea (vezi *Celulele AI* mai jos).
  
  **+ Cod**, **+ Text** și **+ Prompt AI**, sub ultima celulă, adaugă una; săgețile mută o celulă în sus sau în jos, coșul o șterge.
- **Terminalul**, jos (trage de marginea lui de sus ca să-l faci mai înalt, **Arată sau ascunde terminalul** în antet): **Terminal**, ce afișează fiecare rulare, erorile cu rândul lor și un rând în care scrii Python (**Enter** îl rulează, **Shift+Enter** începe un rând nou, **Sus** și **Jos** aduc înapoi rândurile scrise înainte), și **Înregistrator**.

Un notebook se salvează ca un singur fișier `.py`, cu celulele marcate de rânduri `# %%` (`# %% [markdown]` pentru notițe, `# %% [prompt]` pentru o cerere), cum le scriu Jupytext și Visual Studio Code. Un script fără aceste rânduri se deschide ca o singură celulă Python și se salvează cum era, deci orice script scris înainte merge ca înainte; ce au afișat celulele nu se salvează în fișier. Dă-i notebook-ului un nume și **salvează-l** în Ale mele; **Deschide în editorul extern** îi deschide fișierul în editorul tău, iar ce salvezi acolo revine în panou.

În panou, `lapin` e pachetul `lapinbim`, deja încărcat, iar toate celulele, rândul terminalului și rularea următoare împart aceleași nume. Același script rulează dintr-un terminal cu `import lapinbim as lapin`, cât timp Lapin e deschis.

```python
# %%
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
len(lapin.model.walls)
```

Ultimul rând al unei celule, când e o valoare, se arată sub celulă, ca în Jupyter. Lungimile sunt în metri și unghiurile în grade, oricare ar fi unitatea în care arată proiectul.

## Rulează, Oprește, Repornește, Anulează

- **Rulează** pe o celulă (**Shift+Enter** sau **Ctrl+Enter** în ea) rulează celula. Tot ce face e **un singur pas de anulare**: un singur **Anulează** le ia pe toate înapoi. Ce afișează apare sub ea, iar numărul ei (`[1]`, `[2]`…) spune în ce ordine au rulat celulele; o celulă schimbată de la rulare e marcată cu `•`.
- **Rulează tot** rulează toate celulele Python, de sus, ca **un singur pas de anulare** numit după notebook.

  ![Un singur Anulează a luat înapoi cei patru pereți ai rulării](shots/python-undo.webp)

- Cât rulează un script, modelul e al scriptului: o unealtă folosită în fereastră e refuzată cu *A script is editing the model*; vederile, selecția și extrasele merg mai departe.
- O rulare care eșuează nu păstrează **nimic** din ce a făcut: modelul e cum era înainte, iar celula arată eroarea, cu rândul ei marcat.
- **Oprește** oprește rularea; nu se păstrează nimic din ce a făcut. Unui script care nu se oprește în două secunde i se închide Python-ul.
- **Repornește** pornește Python din nou: numele păstrate de celule și de terminal se uită. **Repornește și rulează tot** le face pe amândouă: așa verifici că notebook-ul rulează singur, de sus.

## Celulele AI

O celulă **Prompt AI** cere unui model **Ollama** de pe acest calculator Python-ul celulei de sub ea: scrii ce trebuie să facă (*șase stâlpi la 4 m unul de altul, pe X, la parter*), apoi **Generează** (**Shift+Enter**). Codul se scrie în celula de dedesubt pe măsură ce vine; **nu rulează niciodată singur**: citește-l, apoi rulează-l. **Oprește** oprește modelul.

- Modelul primește ce îi trebuie ca să scrie corect apelurile lui Lapin: comenzile ediției tale potrivite cererii (din catalogul de comenzi), nivelurile, tipurile și selecția proiectului, celulele de deasupra și numele pe care le ține Python acum. Ce scrie se verifică după catalog, iar o comandă sau un argument care nu există se trimite înapoi o dată, spre corectare.
- O celulă Python care a eșuat are **Repară cu AI**: modelul primește codul și eroarea și scrie celula corectată.
- Modelul se alege în antetul notebook-ului, dintre cele pe care le are Ollama; prima dată se ia primul model de cod instalat. Ce modele contează ca modele de cod, cel mai bun primul, e câmpul **Modele pentru cod** din setările asistentului (**Vedere → Asistent**, butonul de setări); gol, e lista proprie a lui Lapin (`qwen3-coder:30b`, `qwen2.5-coder`, `deepseek-coder-v2`, `gemma4:12b`). Adresa Ollama e tot a asistentului ([Asistentul](assistant.md)).

Nimic nu pleacă de pe acest calculator: Ollama rulează pe el. Fără Ollama, celulele AI spun asta, iar restul notebook-ului merge ca înainte.

## O celulă ca graf

**În graf**, pe o celulă Python, face din ea un graf vizual ([Grafuri vizuale](graphs.md)) și îl deschide în panoul **Grafuri**, salvat în proiect. Numerele, textele și punctele ei devin noduri de parametri, `range(…)` un interval, aritmetica o formulă, iar fiecare `lapin.<modul>.<comandă>(…)` nodul acelei comenzi, legat de ce folosește. Un graf rulează în Lapin însuși, deci merge și în browser.

O celulă cu rânduri care n-au nod (un `if`, o bibliotecă proprie) spune care sunt: **Deschide graful parțial** deschide ce s-a putut face, iar **Transformă cu Ollama** îi cere modelului tot graful, verificat de Lapin înainte să fie salvat.

## Înregistrează ce faci ca script

Fila **Înregistrator** a terminalului, lângă **Terminal**, scrie ce faci în fereastră ca Python, o linie pe pas.

1. Dă clic pe **Înregistrează**. Un punct roșu pe filă arată că se înregistrează.
2. Lucrează ca de obicei: desenează pereți, schimbă o proprietate, șterge ceva. Fiecare pas apare pe loc ca o linie, de exemplu `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Dă clic pe **Oprește**, apoi pe **Inserează ca celulă** (o celulă Python nouă sub cea pe care ești), **Copiază** (ca să lipești scriptul în altă parte) sau **Salvează ca script** (intră în Ale mele și se deschide în editor).

![Înregistratorul după desenarea a doi pereți](shots/python-recorder.webp)

Scriptul e scris așa încât să meargă și pe alt proiect:
- ce a făcut un pas se păstrează într-o variabilă (`wall1`), iar pașii următori o folosesc (`wall1.id`);
- nivelul pe care ai desenat se găsește după nume, altfel după cotă (`lapin.model.storey("Ground floor", 0)`), iar un tip după nume (`lapin.model.find("IfcWallType", "Brick 250")`);
- un alt element care era deja în proiect e numit după id (`lapin.model["…"]`), deci linia aceea merge doar în acest proiect;
- cu **Selecția ca lapin.selection()** bifată, o comandă pe ce ai selectat se scrie `lapin.selection()`: scriptul lucrează apoi pe ce e selectat când îl rulezi.

Un **Anulează** în timpul înregistrării scoate ultimul pas din script, iar **Refă** îl pune la loc, așa că scriptul cuprinde ce ai făcut în cele din urmă. Ce face un script nu se înregistrează niciodată, nici întrebările care nu schimbă nimic (un extras citit, o măsurătoare).

![Scriptul înregistrat, rulat într-un proiect nou: aceiași doi pereți](shots/python-recorder-new.webp)

## Scripturile proiectului

Un script poate fi păstrat **în proiect**, ca să meargă odată cu fișierul: deschide-l sau scrie-l în editor și dă clic pe **Salvează în proiect**. Apare la **Ale proiectului**, în stânga. Salvarea, redenumirea și ștergerea unui script al proiectului sunt fiecare un singur pas de anulare, iar versiunile proiectului păstrează scripturile lui. În fișierul `.lapin` fiecare e și un fișier text simplu, `scripts/<name>.py`, deci poate fi citit fără Lapin.

**Un script al proiectului nu rulează niciodată de la sine**, nici când se deschide proiectul, nici când se salvează. Prima dată când îl rulezi, și din nou ori de câte ori i s-a schimbat codul, Lapin întreabă:
- **Rulează o dată**: rulează de data aceasta, iar următoarea rulare întreabă din nou;
- **Am încredere**: rulează, iar acest cod e ținut minte ca de încredere pe acest calculator (o schimbare a lui întreabă din nou);
- **Renunță**: nu rulează nimic.

Un script pe care îl salvezi tu însuți în proiect e de încredere din clipa salvării. Scripturile tale (Ale mele) nu întreabă niciodată.

## Ce e selectat

`lapin.selection()` dă ce e selectat în fereastră, ca elemente:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Câmpurile unui element se citesc după nume și se schimbă prin atribuire: `wall.height = 3` schimbă peretele (un singur pas de anulare, sau parte din Rulare). `with lapin.step("Name"):` face din mai multe comenzi un singur pas de anulare, iar fiecare răspunde totuși pe loc, deci una de după poate folosi ce a făcut una de dinainte.

## O unealtă dintr-un script

Un script poate fi o **unealtă**: un buton din **Uneltele mele** care îl rulează, pentru oricine, cu Extra pentru dezvoltatori pornit sau oprit. Spune asta în primele rânduri ale scriptului, *antetul* lui:

```python
# lapin: name="Perimeter walls", version="1.0.0", tool="Perimeter walls", icon="square", run="perimeter"
# lapin: tooltip="Four walls round a rectangle, one undo step"

def perimeter():
    v = lapin.ui.ask("Perimeter walls", {
        "width": lapin.ui.number(8.0, unit="m", min=0.5),
        "depth": lapin.ui.number(6.0, unit="m", min=0.5),
        "storey": lapin.ui.storey(),
    })
    if v is None:          # Renunță
        return
    w, d = v["width"], v["depth"]
    corners = [(0, 0), (w, 0), (w, d), (0, d)]
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        lapin.architecture.add_wall(storey_id=v["storey"].id, x1=x1, y1=y1, x2=x2, y2=y2)
```

- **tool**: numele butonului; **run**: funcția pe care o cheamă (fără ea rulează tot scriptul); **icon**: una dintre iconițele Lapin (`wall`, `door`, `stair`…) sau una obișnuită (`square`, `ruler`, `layers`, `grid`, `star`…), altfel o literă (`icon="P"`, sau prima literă a numelui); **tooltip**: ce spune butonul când treci cu mausul peste el.
- Salvează scriptul la **Ale mele** (sau în proiect): unealta lui apare în **Uneltele mele**, într-un grup **Scripturi**, fără ca scriptul să fie rulat. Mut-o, fă-o mică sau scoate-o ca pe orice unealtă; o găsește și **Comenzi…** (Ctrl+K).

![Uneltele mele cu unealta unui script](shots/python-tools-tab.webp)

Un clic rulează scriptul ca o singură **Rulare**: tot ce face e **un singur pas de anulare**, numit după unealtă. `lapin.ui.ask` arată un dialog al lui Lapin, făcut din câmpurile pe care le numește scriptul: un număr cu unitatea lui, un text, o alegere, o casetă de bifat, un element (cel pe care îl selectezi între timp în model: **Folosește selecția**), un nivel, un tip. **OK** dă scriptului valorile; **Renunță** nu îi dă nimic.

![Întrebarea unealtei: dimensiunile și nivelul](shots/python-tools-ask.webp)

![Cei patru pereți ai uneltei; un singur Anulează îi ia înapoi](shots/python-tools-walls.webp)

Un script poate face unelte și în timp ce rulează: `@lapin.ui.tool("Name", icon="ruler")` deasupra unei funcții, rulat o dată din panou, pune acea unealtă în Uneltele mele.

De unde vin uneltele:
- **Ale mele**: mereu;
- **scripturile proiectului**: doar când ai dat încredere codului lor pe acest calculator, așa că un proiect de la altcineva nu pune niciodată un buton în ribbonul tău; **Retrage încrederea** din panou le scoate;
- **Lapin Extensions**: un script instalat își arată unealta; primul clic întreabă **Rulează o dată / Am încredere / Renunță**, iar **Rulează scriptul** din **Extensii → Instalate** îl rulează și el.

Un script șters, dezinstalat sau fără încredere nu mai are uneltele în Uneltele mele.

### Panouri, meniuri și palete

Un script își poate așeza singur partea lui din **Uneltele mele**, totul din cod, iar Lapin o desenează; rulează scriptul o dată din panoul Script și apare ce înregistrează:

```python
with lapin.ui.panel("Walls"):                      # un grup din Uneltele mele numit Walls
    @lapin.ui.tool("Perimeter", icon="square", size="small")
    def perimeter(): ...

    with lapin.ui.menu("Openings", icon="door"):   # un singur buton cu o listă de unelte
        lapin.ui.tool("Door here", run=door)
        lapin.ui.tool("Window here", run=window)

    p = lapin.ui.palette("Wall tools", icon="wall")
    p.heading("New wall")
    p.field("length", lapin.ui.number(5.0, unit="m", min=0.5))
    p.field("height", lapin.ui.number(2.8, unit="m"))
    p.field("storey", lapin.ui.storey())

    @p.button("Make wall", icon="hammer")
    def make(v):                                   # v are valorile din paletă
        lapin.architecture.add_wall(storey_id=v["storey"].id, x1=0, y1=0, x2=v["length"], y2=0, height=v["height"])
```

- **Un panou** e un grup din Uneltele mele, cu numele dat de script: uneltele lui ajung acolo în loc de **Scripturi**, prima dată când apar. În antet, `panel="Walls"` face același lucru pentru unealta din antet.
- **size="small"** pune unealta mică: un rând cu iconița ei, câte trei una sub alta.
- **Un meniu** e un singur buton care deschide lista uneltelor lui. Fiecare rămâne o unealtă de sine stătătoare: o găsește **Comenzi…** și o poate așeza singură **Personalizează Uneltele mele…**.
- **O paletă** e un mic panou plutitor pe care butonul ei din Uneltele mele îl deschide și îl închide: titluri, texte, separatoare, câmpuri (aceleași ca într-o întrebare) și butoane (`with p.row():` pune butoanele unul lângă altul). O muți trăgând de titlu; rămâne deschisă cât lucrezi și ține minte unde era și ce ai scris. Un buton e o singură **Rulare**, un singur pas de anulare numit după buton; funcția lui primește valorile din paletă dacă are un parametru. Cât rulează un script, butoanele așteaptă.

![Grupul propriu al unui script în Uneltele mele: un meniu și o paletă](shots/python-tools-panel.webp)

![Lista de unelte a meniului](shots/python-tools-menu.webp)

![Paleta: câmpurile și butonul ei](shots/python-tools-palette.webp)

Ca pe orice unealtă, le poți muta, face mici sau scoate; o unealtă scoasă rămâne scoasă.

## Mesaje

Jos în panoul Script, banda Mesaje păstrează un rând pe rulare: pornită, terminată cu cât a durat, oprită, sau eșuată cu ultima linie a erorii și, când traceback-ul Python le dă, numele scriptului și linia (`Pereti:12`). Un clic pe un rând eșuat deschide scriptul în editor la acea linie, dacă e cel deschis, unul dintre ale tale sau unul al proiectului. Înregistrează și rulările pornite din Uneltele mele și din butoanele paletelor, chiar cu panoul închis, deci o unealtă care a eșuat e raportată când deschizi panoul. O deschizi cu săgeata ei, filtrezi după Erori, Avertismente sau Toate, copiezi rândurile ca text simplu sau le golești. Terminalul e neschimbat: arată în continuare tot ce tipărește un script.

## Forme făcute de un script

Un script, sau un program de pe calculatorul tău (un script Python în VS Code, de exemplu), poate face elemente a căror formă o calculează el: o fermă peste o încăpere, un panou de fațadă, o formă proiectată de alt instrument. Sunt elemente obișnuite: apar în planuri, secțiuni și 3D, se măsoară în extrase, se exportă în IFC și le muți, rotești, copiezi, ascunzi sau ștergi ca pe oricare altul. Proprietățile spun **Făcut de un script:** și care. Forma lor e a scriptului: ce le-ar schimba forma aici e refuzat (*Forma aceasta e făcută de un script; schimbă-o în script, sau mut-o, rotește-o ori secționeaz-o aici.*).

Un script poate spune din ce e făcută forma lui și ce e ea în IFC. Fiecare parte își poate numi materialul (betonul unei pile, elastomerul reazemului ei); o parte care nu numește niciunul e din materialul elementului. Planurile, secțiunile și fațadele hașurează, umplu și unesc atunci tăietura după aceste materiale, ca la un perete, iar exportul IFC le scrie. **Se desenează ca** al unui material (Gestionare → Materiale) spune cum se desenează oricare ar fi limba numelui său, așa că un *Conglomerato bituminoso* italian e asfalt, nu membrană; un script îl poate da și el când face materialul. Scriptul poate da elementului și tipul predefinit și tipul de obiect IFC (o grindă care e un cap de pilă) și partea de pod IFC 4.3 în care stă (o pilă, tablierul, suprastructura); acestea, și nimic din forma lui, pot fi schimbate și după. Proprietățile arată materialul elementului și pe ale părților lui, doar pentru citire, și valorile proprii ale scriptului.

Un script poate urmări din ce e făcut. Rulat din afara lui Lapin, își actualizează singur elementele când schimbi încăperea sau peretele pe care îl urmărește. Un script al proiectului care urmărește elemente e rulat din nou de Lapin după schimbarea ta, doar dacă are încrederea ta pe acest calculator, ca pas de anulare propriu numit *„<script> regenerated”*: Ctrl+Z ia înapoi doar regenerarea. Un script fără încredere nu e rulat; panoul Script spune asta.

## Siguranța

Un script poate face orice poți face tu pe acest calculator: rulează în Python-ul tău, cu drepturile tale. Rulează scripturi scrise de tine sau în care ai încredere; un script al proiectului și unul instalat întreabă mai întâi.

Un invitat al unei sesiuni partajate nu poate rula scripturi pe calculatorul tău, nu poate pune unul în proiectul tău și nici nu poate șterge, redenumi sau muta un script al proiectului; un script se conectează doar la Lapin-ul de pe propriul lui calculator, iar întrebările lui se pun doar în fereastra ta.

## Pentru dezvoltatori

Ce oferă `lapin` (comenzile, interogările, loturile, modelul, evenimentele) e descris în ghidul pentru dezvoltatori, *Lapin from Python* (`docs/python.md` în depozitul lui Lapin); pachetul e pe PyPI ca `lapinbim`.

Un program de pe calculatorul tău găsește Lapin pe portul **14912**; un al doilea Lapin deschis în același timp ia 14913, și tot așa. Cu mai multe deschise, programul întreabă care (`lapin.connect(project="Casa")`); ca să impui unul, setează `LAPIN_PORT`.

### Notebook-uri Jupyter

Un notebook din afara lui Lapin poate comanda Lapin-ul deschis, fără panoul Script: `pip install lapinbim` în Python-ul notebook-ului (3.11 sau mai nou), apoi

```python
import lapinbim as lapin
lapin.info().product.name
with lapin.batch("Din notebook"):
    lapin.execute("architecture.addWall", {"x1": 0, "y1": 0, "x2": 3, "y2": 0})
```

Pereții apar imediat în fereastră, iar lotul e un singur pas de undo. Merge într-un kernel Jupyter ca în orice program (testat cu ipykernel). Merge doar cu aplicația desktop, nu cu Lapin în browser, iar Python-ul notebook-ului are drepturile tale, ca în nota de siguranță de mai sus.
