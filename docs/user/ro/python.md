---
title: Scripturi Python
order: 127
summary: Conduci Lapin cu Python din panoul Script, un extra pentru dezvoltatori: o consolă și un editor care rulează Python-ul tău, fiecare Rulare un singur pas de anulare.
---

## Pornește Extra pentru dezvoltatori

Lapin poate fi condus cu Python: fiecare unealtă e o comandă, iar un script trimite aceleași comenzi. Panoul **Script** e un *extra pentru dezvoltatori*, oprit până îl pornești, așa că altfel nu se vede nimic din el.

Deschide **Fișier → Preferințe…** și, la **General**, bifează **Extra pentru dezvoltatori**.

![Preferințe: Extra pentru dezvoltatori](shots/python-preferences.webp)

- **Python**: Python-ul pe care îl rulează panoul. Gol, Lapin caută unul pe acest calculator (`python3`, `py -3` pe Windows, cel de la python.org și cel din Homebrew). Trebuie să fie **Python 3.11 sau mai nou**; Lapin nu instalează Python. Ia-l de pe [python.org](https://www.python.org/downloads/) dacă nu ai, sau scrie calea celui pe care îl vrei (al unui mediu virtual, de exemplu).
- **Editor extern**: editorul pe care îl folosește **Deschide în editorul extern**, ca o comandă (`code` pentru Visual Studio Code). Gol, editorul de text al sistemului.

Python rulează doar în aplicația desktop: în Lapin pentru browser, panoul spune asta.

## Panoul Script

Deschide **Vedere → Script**. Panoul se deschide de-a lungul marginii de jos a ferestrei; trage de marginea lui de sus ca să-l faci mai înalt. Antetul lui spune ce Python a găsit sau de ce n-a găsit niciunul.

![Panoul Script după o rulare: patru pereți, un singur pas de anulare](shots/python-panel.webp)

- **Ale mele**, în stânga: scripturile tale, fișiere `.py` obișnuite, păstrate în folderul lui Lapin de pe acest calculator. Dă clic pe unul ca să-l deschizi; **Script nou** pornește de la un exemplu.
- **Editorul**, la mijloc: Python cu culorile și indentarea lui. Scrii `lapin.` și îți propune ce are Lapin (comenzile tuturor modulelor ediției tale și apelurile lui proprii); `lapin.architecture.` propune comenzile de arhitectură cu argumentele lor. Dă-i scriptului un nume și **salvează-l** în Ale mele; **Deschide în editorul extern** îi deschide fișierul în editorul tău, iar ce salvezi acolo revine în panou.
- **Consola**, în dreapta: ce afișează scripturile, erorile lor cu rândul la care s-au oprit (marcat în editor) și un rând în care scrii Python. **Enter** îl rulează, **Shift+Enter** începe un rând nou, **Sus** și **Jos** aduc înapoi rândurile scrise înainte. Un nume pe care îl dai în consolă rămâne pentru rândul următor și pentru scripturile pe care le rulezi.

În panou, `lapin` e pachetul `lapinbim`, deja încărcat. Același script rulează dintr-un terminal cu `import lapinbim as lapin`, cât timp Lapin e deschis.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Lungimile sunt în metri și unghiurile în grade, oricare ar fi unitatea în care arată proiectul.

## Rulează, Oprește, Repornește, Anulează

- **Rulează** (**Ctrl+Enter** în editor) rulează scriptul. Tot ce face e **un singur pas de anulare**, numit după script: un singur **Anulează** le ia pe toate înapoi.

  ![Un singur Anulează a luat înapoi cei patru pereți ai rulării](shots/python-undo.webp)

- Cât rulează un script, modelul e al scriptului: o unealtă folosită în fereastră e refuzată cu *A script is editing the model*; vederile, selecția și extrasele merg mai departe.
- Un script care eșuează nu păstrează **nimic** din ce a făcut: modelul e cum era înainte de **Rulează**, iar consola arată eroarea și rândul ei.
- **Oprește** oprește scriptul; nu se păstrează nimic din ce a făcut. Unui script care nu se oprește în două secunde i se închide Python-ul.
- **Repornește** pornește Python din nou: numele păstrate de consolă se uită.

## Înregistrează ce faci ca script

Fila **Înregistrator**, lângă consolă, scrie ce faci în fereastră ca Python, o linie pe pas.

1. Dă clic pe **Înregistrează**. Un punct roșu pe filă arată că se înregistrează.
2. Lucrează ca de obicei: desenează pereți, schimbă o proprietate, șterge ceva. Fiecare pas apare pe loc ca o linie, de exemplu `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Dă clic pe **Oprește**, apoi pe **Copiază** (ca să lipești scriptul în altă parte) sau pe **Salvează ca script** (intră în Ale mele și se deschide în editor).

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

## Forme făcute de un script

Un script, sau un program de pe calculatorul tău (un script Python în VS Code, de exemplu), poate face elemente a căror formă o calculează el: o fermă peste o încăpere, un panou de fațadă, o formă proiectată de alt instrument. Sunt elemente obișnuite: apar în planuri, secțiuni și 3D, se măsoară în extrase, se exportă în IFC și le muți, rotești, copiezi, ascunzi sau ștergi ca pe oricare altul. Proprietățile spun **Făcut de un script:** și care. Forma lor e a scriptului: ce le-ar schimba forma aici e refuzat (*Forma aceasta e făcută de un script; schimbă-o în script, sau mut-o, rotește-o ori secționeaz-o aici.*).

Un script poate urmări din ce e făcut. Rulat din afara lui Lapin, își actualizează singur elementele când schimbi încăperea sau peretele pe care îl urmărește. Un script al proiectului care urmărește elemente e rulat din nou de Lapin după schimbarea ta, doar dacă are încrederea ta pe acest calculator, ca pas de anulare propriu numit *„<script> regenerated”*: Ctrl+Z ia înapoi doar regenerarea. Un script fără încredere nu e rulat; panoul Script spune asta.

## Siguranța

Un script poate face orice poți face tu pe acest calculator: rulează în Python-ul tău, cu drepturile tale. Rulează scripturi scrise de tine sau în care ai încredere; un script al proiectului și unul instalat întreabă mai întâi.

Un invitat al unei sesiuni partajate nu poate rula scripturi pe calculatorul tău, nu poate pune unul în proiectul tău și nici nu poate șterge, redenumi sau muta un script al proiectului; un script se conectează doar la Lapin-ul de pe propriul lui calculator, iar întrebările lui se pun doar în fereastra ta.

## Pentru dezvoltatori

Ce oferă `lapin` (comenzile, interogările, loturile, modelul, evenimentele) e descris în ghidul pentru dezvoltatori, *Lapin from Python* (`docs/python.md` în depozitul lui Lapin); pachetul e pe PyPI ca `lapinbim`.
