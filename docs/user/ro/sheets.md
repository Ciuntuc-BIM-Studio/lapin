---
title: Planșe, PDF și DXF
order: 100
summary: Așezi vederile pe planșe cu cartuș și le scoți în PDF sau DXF.
---

<!-- video sheets -->

## Cum faci o planșă

Apasă **+** lângă **Planșe** în navigatorul proiectului. Ca să pui o vedere, trage-o din navigator pe planșă sau alege-o în **Pune o vedere…** din bara planșei. Fiecare vedere își păstrează scara și rămâne legată de model, așa că planșa se schimbă odată cu modelul. Pe planșă, trage o vedere ca s-o muți: se mută doar ea, cu titlul ei, iar chenarul, cartușul și celelalte vederi rămân pe loc; se prinde de marginile și de mijlocul chenarului și de celelalte vederi, iar săgețile o împing cu 1 mm (10 mm cu Shift).

![O planșă A3 cu un plan și o secțiune](shots/sheet.webp)

**Aliniază vederile după ce desenează.** Apasă **Align** în bara planșei, apoi dă clic pe un colț sau pe o linie dreaptă dintr-o vedere (un pătrat marchează colțul, o linie se luminează pe toată lungimea ei), apoi pe colțul sau linia dintr-o altă vedere care trebuie să se alinieze cu ea. A doua vedere se mută: două colțuri se întâlnesc, două linii orizontale ajung la aceeași înălțime, două linii verticale la aceeași poziție, iar un colț față de o linie aliniază doar direcția liniei. Folosește-l pentru planul și elevația unui mobilier sau pentru un detaliu lângă vederea din care provine. O linie orizontală nu se poate alinia cu una verticală, o vedere fixată nu se mută (alege-o întâi pe ea și mută-o pe cealaltă), iar <kbd>Esc</kbd> anulează. E un singur pas de undo.
**Pune pe planșă… chiar din vedere.** Fără să deschizi întâi planșa: clic dreapta într-un plan, o secțiune, o fațadă sau o vedere 3D deschisă (sau pe tabul ei, sau pe vederea din browserul proiectului) și alegi **Pune pe planșă…**. Se deschide o listă mică a planșelor proiectului: scrii câteva litere din numărul sau numele unei planșe ca s-o restrângi, apoi dai clic pe planșă (<kbd>Enter</kbd> o ia pe prima). **Planșă nouă…** din capul listei face o planșă nouă cu numele vederii, pe aceeași hârtie ca ultima ta planșă (A3 când nu e niciuna), cu vederea pe ea, într-un singur pas de anulare. Vederea se pune unde se pune una din **Pune o vedere…**, la mijlocul hârtiei din stânga cartușului, iar planșa se deschide ca s-o muți la locul ei (vederea rămâne la un tab distanță); debifează **Deschide planșa după ce pui vederea** ca să rămâi în vedere cât pui mai multe. O vedere stă pe o singură planșă: una care e deja pe o planșă îți spune pe care și îți oferă s-o deschizi. Comanda e și în paleta de comenzi (**Comenzi…**), unde îi poți da o scurtătură.

![Pune pe planșă…: planșele restrânse după ce scrii, Planșă nouă… deasupra lor](shots/place-on-sheet.webp)

**Care vederi sunt pe planșe.** În browserul proiectului, un pătrățel albastru înaintea numelui unei vederi arată că e pe o planșă; ține mouse-ul pe el ca să citești pe care (*Pe planșa A-101 · Planuri*). Clic dreapta pe o asemenea vedere în browser îți dă **Deschide planșa A-101 · Planuri**. Clic dreapta într-o vedere sau pe tabul ei și alegi **Găsește în browserul proiectului** ca s-o găsești într-un browser lung: browserul se desface dacă e strâns, își deschide setul și grupul, aduce rândul vederii la mijloc și îl luminează o clipă, iar vederea e selectată, ca să-i vezi proprietățile. Merge și pentru o planșă (clic dreapta pe tabul ei) și din paleta de comenzi.

![Browserul proiectului cu vederile de pe planșe marcate, una găsită](shots/browser-on-sheet.webp)

**O vedere de pe planșă nu se șterge.** Ștergerea unei vederi care e pe o planșă (din browser, cu <kbd>Delete</kbd>, dintr-un script) e refuzată, iar mesajul numește planșa: scoate întâi vederea de pe planșă (vezi mai jos), apoi șterge-o. Când se șterg mai multe lucruri odată și unul e o asemenea vedere, nu se șterge nimic. Planșa însăși se poate șterge oricând; vederile ei rămân în proiect.

O **vedere 3D** salvată se pune pe planșă la fel. Se desenează în linii, ca o fațadă, din camera pe care o păstrează vederea (butonul cu cameră de lângă ea din navigatorul proiectului, **Păstrează camera așa cum e acum**, salvează camera așa cum e). O cameră ortografică arată tot modelul văzut din acea parte, o axonometrie la scara vederii (1:100 până alegi alta). O cameră în perspectivă arată ce îi cuprinde câmpul vizual, la mărimea reală acolo unde privește camera. Desenul ține cont de Vizibilitate / Grafică a vederii, de elementele ascunse în ea, de nivelurile pe care le arată și de cutia de secțiune, și iese la fel în PDF. O captură de Gaussian splats pe care vederea o desenează ca splat-uri se tipărește ca imagine sub liniile ei, ascunsă în spatele modelului acolo unde o ascunde vederea 3D (vezi [Gaussian splats](import-export.md#splat-uri-pe-planșe)). O vedere 3D salvată fără cameră se vede dinspre sud-vest, de sus. Dă dublu clic pe ea pe planșă sau apasă ✎ ca s-o deschizi în 3D, cu camera ei.

Selectează planșa ca să-i completezi **cartușul** în panoul de proprietăți: proiectul, beneficiarul, cine a desenat, cine a verificat, data și revizia. Tot acolo se setează numărul, formatul și orientarea. Formatul e unul A (A0 până la A4), unul din formatele lungi ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), unul nord-american (Letter, Tabloid, ARCH D, ARCH E) sau **personalizat**, cu **Lățimea hârtiei** și **Înălțimea hârtiei** în milimetri. Lăsat gol, câmpul proiectului arată numele proiectului: numele dat proiectului, altfel numele fișierului lui.

## Ce arată o vedere

O vedere de pe planșă arată exact ce alegi, fără s-o tragi pe ghicite:

- **Decupeaz-o pe planșă.** Clic pe o vedere: apar mânere la colțuri și pe laturi. Le tragi și chenarul cuprinde mai mult sau mai puțin din vedere, iar desenul rămâne pe loc pe hârtie. Cu **Shift**-tras înăuntru deplasezi desenul în chenar.
- **Desenează-i limita în vedere.** **Annotate → View boundary** (<kbd>VB</kbd>): desenezi un dreptunghi pe un plan, o secțiune sau o fațadă și îi dai un nume, unic în proiect. În **Place view…** vederea apare apoi cu fiecare limită a ei („Ground floor plan › Aripa est”), iar o vedere pusă așa arată exact dreptunghiul acela și îl urmează când îl muți sau îl redimensionezi în vedere. Aceeași vedere poate sta pe mai multe planșe, câte o limită pe fiecare. O vedere pusă trece de la o limită la alta, sau la decupare liberă, din lista de pe chenarul ei. Limitele se văd doar pe ecran, niciodată pe hârtie.
- **Fixeaz-o.** Pioneza de pe chenarul vederii o ține pe loc: se selectează, dar nu se mută, nu se împinge și nu se decupează din greșeală.
- **Pune-o din nou la altă scară.** Clic dreapta pe o vedere de pe planșă și alegi **Plasează din nou la 1:25…** (sunt oferite celelalte scări standard ale proiectului), apoi tragi un dreptunghi pe vedere în jurul părții de arătat, sau doar dai clic ca să iei vederea așa cum e decupată. Lapin duplică vederea cu adnotările ei, dă copiei scara aceea, o decupează la parte și o pune pe aceeași planșă lângă original, la dreapta dacă e loc (altfel la stânga, dedesubt sau deasupra), într-un singur pas de anulare: un plan la 1:100 și colțul lui la 1:25 pe aceeași planșă. Copia poartă numele vederii și scara („Parter 1:25”). În browserul proiectului, clic dreapta pe o vedere care e pe o planșă face la fel, cu vederea așa cum e decupată; planșa ei se deschide.
- **Pune un detaliu.** O vedere de detaliu făcută de o chemare (Vedere → Chemare de detaliu, vezi Vederi) se pune pe planșă ca orice vedere. Capătul chemării din vederea din care a fost chemată scrie atunci numărul detaliului pe planșă și numărul planșei („3 / A-501”), pe ecran și pe hârtie; înainte să fie pus arată „–”. Numărul lui e locul lui între vederile planșei; mutat pe altă planșă, capătul îl urmează.
- **Scoate o vedere de pe planșă.** Dă clic pe o vedere și apasă <kbd>Delete</kbd> (sau <kbd>Backspace</kbd>): vederea iese de pe planșă, doar ea; modelul, vederea însăși și planșa rămân. Cu <kbd>Ctrl</kbd>-clic (<kbd>⌘</kbd>-clic pe Mac) pe alte vederi le scoți pe mai multe odată, într-un singur pas de anulare. × de pe caseta unei vederi face același lucru pentru ea. Planșa se șterge din browserul de proiect: butonul ei cu coș, lângă numele planșei.

![O secțiune pusă din nou la 1:50 lângă ea pe planșă](shots/sheet-place-again.webp)

Un simbol pus cu **Așază simbol…** (vezi Desenare) se alege ca o vedere: se luminează sub cursor, un clic îl selectează și îi arată proprietățile, tragerea îl mută (un singur pas de anulare), iar <kbd>Delete</kbd> îl șterge.

![O săgeată a nordului pusă pe planșă, selectată, cu chenarul ei](shots/sheet-symbol-selected.webp)

Chenarul planșei și cartușul sunt ale hârtiei. Nu se selectează și nu se mută, iar un clic alege cea mai mică vedere de sub cursor, așa că o vedere pusă peste cartuș se poate selecta oricând.

![O vedere selectată pe planșă: mânerele de decupare, limita ei, pioneza](shots/sheet-view-boundary.webp)

## Cartușul tău

**Gestionare → Cartușe…** arată cartușele: ale lui Lapin (**Lapin standard**, cel pe care l-au avut toate planșele până acum, **cartușul** românesc și un cartuș **ISO 7200**), ale proiectului și cele din biblioteca acestui calculator, oferite în orice proiect de pe el. Dă clic pe unul ca să-l vezi cu valorile proiectului. **Duplică în proiect** face o copie pe care o poți edita, **Nou** unul gol; **Fă-l implicit pentru proiect** îl dă tuturor planșelor care nu își aleg unul, iar o planșă își alege cartușul la **Cartuș** în proprietățile ei.

![Cartușe: ale lui Lapin, ale proiectului și ale acestui calculator, cel ales cu valorile proiectului](shots/title-blocks.webp)

**Editează…** deschide cartușul pe o planșetă: o vedere de desen proprie, în milimetri de hârtie, cu dreptunghiul cartușului punctat. Cartușul e un dreptunghi într-un colț al chenarului, așa că același cartuș se potrivește de la A4 la A0, vertical sau orizontal. Îl desenezi cu aceleași unelte **Desen** ca pe orice vedere (linie, polilinie, dreptunghi, cerc, arc, text, hașură și uneltele de editare); prinderile și mânerele merg ca de obicei. Textele își păstrează câmpurile vii așa cum le-ai scris, `{project.name}`, `{sheet.number}`. Sigla e o imagine: **Logo sau imagine…** în panou sau **Desen → Importă imagine**. Tabelul de revizii apare ca dreptunghiul lui; mută-l sau redimensionează-l. Un panou alături are **Nume**, lățimea și înălțimea dreptunghiului (mm), colțul planșei, **Chenar în jurul colii**, locul tabelului de revizii și câmpurile de copiat. **Termină schița** scrie cartușul într-un singur pas de anulare, iar planșele lui îl urmează; neschimbat, rămâne exact cum era. **Renunță** abandonează editarea.

Lista **Câmpuri** are textul viu: al proiectului (nume, număr, beneficiar, amplasament, autorizație, proiectant, persoane și parametrii tăi), al planșei (număr, titlu, scară, format, revizie, stare, dată, desenat, verificat și aprobat, faza, planșa x din y) și al sistemului (data tipăririi, numele fișierului). Un clic copiază un câmp, ca să-l lipești într-un text scris cu **Text**; un text poate amesteca vorbe și câmpuri, `Proiect: {project.name}`. Un câmp pe care Lapin nu-l cunoaște se tipărește ca `<câmp>` și **Gestionare → Verificare** îl semnalează.

Un câmp al planșei, al tău, nu cere nicio listă: scrie-l într-un text ca `{sheet.` și un nume, `{sheet.tipDesen}` sau `{sheet.muutos}`, iar fiecare planșă cu acest cartuș îl arată în proprietățile ei, sub câmpurile obișnuite, ca să-și tasteze valoarea; până atunci se tipărește gol. Numit ca un câmp din **Datele proiectului** sau ca unul dintre parametrii tăi (`{sheet.designer}`, `{sheet.address}`), tipărește valoarea proiectului până când o planșă o tastează pe a ei. În proprietățile planșei, un câmp gol arată estompat ce se tipărește în locul lui: desenat, verificat, beneficiarul și faza proiectului, numele lui, data de azi.

![Cartușul pe planșetă: dreptunghiul lui punctat, desenul și panoul cu câmpurile de copiat](shots/title-block-designer.webp)

Planșele, PDF-ul lor, Tipărirea și emiterea îl desenează cu valorile fiecărei planșe. În DXF e un bloc cu atribute, câte unul pentru fiecare text viu (`PROJECT_NAME`, `SHEET_NUMBER`…), ca indicatoarele din AutoCAD. **Exportă…** salvează un cartuș ca fișier `.json`, cu imaginile lui, iar **Importă…** îl citește în proiect sau în bibliotecă, așa că un birou își mută cartușele de pe un calculator pe altul. Proiectele vechi păstrează Lapin standard și se desenează exact ca înainte.

![Planșa cu cartușul ei](shots/sheet-title-block.webp)

În aplicația desktop, **Cartușe** are și un bloc **Folderele firmei**: fișierele `.lapintitleblock` din folderele firmei, un clic adăugând un cartuș în proiect, iar **Salvează în folderul firmei…** scriind cartușul ales într-un folder care poate fi scris, ca toate proiectele biroului să pornească din aceleași cartușe.

![Cartușe: fișierul din folderul firmei](shots/title-blocks-firm-folder.webp)

## Desenul pe planșă

Notele, semnele și cheile care țin de planșă, nu de una dintre vederile ei, se desenează pe planșa însăși. Apasă **Adnotări** în bara planșei sau dă dublu clic pe hârtie în afara vederilor (**Adnotări → Adnotează planșa** face la fel). Planșa se deschide în panoul de desen ca hârtia ei, la 1:1, cu vederile, chenarul și cartușul dedesubt, iar bara spune *desen pe planșă*. Fiecare unealtă din **Desen** și **Adnotări** merge acolo ca pe o vedere, în milimetri de hârtie: scrii 50 și linia are 50 mm pe hârtie, iar înălțimea unui text e înălțimea lui pe hârtie. Punctele se prind de chenar, de cartuș, de casetele vederilor (albastru întrerupt) și de ce desenează vederile. **Planșă** din bară te duce înapoi la aranjarea vederilor.

- **Textele** pot avea câmpuri vii, ca ale unui cartuș: `Planșa {sheet.number} din {sheet.count}`, `{project.name}`, `{sheet.date}`. Pe o vedere, un text rămâne cum l-ai scris.
- **Notă cu indicator** (Adnotări → Text): dă clic unde arată săgeata, apoi unde stă textul, și scrie-l. Mutată, nota își ia săgeata cu ea; **Linie de indicație → scoate** din proprietăți scoate săgeata. Selectată, vârful săgeții și fiecare cot au mânerele lor: trage unul ca să-l muți singur.
- **Liniile, poliliniile, dreptunghiurile, cercurile, arcele, hașurile, simbolurile și imaginile** sunt ale uneltelor din Desen, cu mânerele, straturile și uneltele lor de modificare. Săgeata nordului e unul dintre simbolurile lui Lapin (**Adnotări → Simbol**). Simbolurile puse cu **Așază simbol…** din bara planșei se aleg cu un clic și aici și se mută în milimetri de hârtie.
- **Nor de revizie** (Desen → Desenează, Adnotări → Planșe sau REVCLOUD în linia de comandă): două colțuri, sau **P** pentru un poligon închis cu Enter. **Nor de revizie** din proprietățile oricărei polilinii închise sau ale unui cerc o face nor sau o readuce la loc și îi stabilește arcul pe hârtie.
- **Legendă** (Adnotări → Planșe): o listă vie a ce desenează vederile planșei: materialele cu care sunt hașurate și umplute tăieturile lor sau, cu **Liste** din proprietăți, liniile lor după rol, tip și grosime. Se schimbă odată cu vederile și cu modelul, iar titlul ei primește și el câmpuri. Cuvintele ei (titlul, rolurile și tipurile liniilor) sunt scrise în limba pe care o avea interfața când a fost pusă; **Limba** din proprietăți le schimbă. Cu instalațiile, **Liste** oferă și **simboluri și linii de instalații**: liniile instalațiilor și simbolurile aparatelor pe care le desenează vederile ([Legenda instalațiilor](mep.md#legenda-instalatiilor)).
  La o legendă de materiale, **Materiale** spune care: **ale zonelor secționate** (ca până acum), **ce arată vederile** (zonele lor secționate și fețele pe care le văd, așa că tencuiala, cărămida și placajul unei fațade sunt listate) sau **cele etichetate cu o cheie** (doar materialele citite de [etichetele de cheie de material](drafting.md#eticheta-de-cheie-de-material) ale vederilor). Ultimele două își cheiază rândurile: fiecare rând începe cu cheia materialului într-un cerc, apoi mostra și `nume — descriere` (primul rând al descrierii materialului), în ordinea cheilor, ca un tabel de finisaje al fațadei. **Chei** pornește sau oprește asta la oricare dintre cele trei, iar **Rând** scrie un rând al tău: `{key}`, `{name}`, `{description}`, `{category}` sau orice parametru al materialului (`{name} — {Finish}`); o parte dintre liniuțe care iese goală e lăsată afară. Un material listat de o legendă cu chei primește numărul definitiv când legenda e pusă sau schimbată, așa că cheia lui rămâne.

  ![O legendă cu chei a finisajelor fațadei: fiecare cheie, cu numele și descrierea materialului ei](shots/material-key-legend.webp)
- **Scara grafică** (Adnotări → Planșe): la scara primei vederi a planșei sau la scara dată în proprietăți, o lungime rotundă de circa 80 mm sau una aleasă de tine. Pe un plan, o secțiune sau o fațadă, scara grafică ia scara vederii. **Săgeata nordului** (același grup): dă clic pe mijlocul ei pe planșă; arată cum așază fereastra vederii planul.

Ce e desenat pe planșă se tipărește pe ea, peste vederi, exact cum se vede: în PDF, la Tipărire și în DXF. Ține de planșă: **Duplică planșa** (butonul ei din browserul de proiect) îl copiază, cu simbolurile puse pe planșă, iar ștergerea planșei îl șterge. Fiecare desen e un singur pas de anulare. Planșele făcute înainte nu au nimic desenat pe ele și se tipăresc ca până acum. O emitere îl numără și pe el: o planșă schimbată doar prin ce e desenat pe hârtia ei sau prin simbolurile ei e semnalată ca schimbată de la ultima emitere (Gestionare → Verificare, CDE).

![Desen pe o planșă: o notă cu indicator, un nor de revizie, un text viu, o legendă a materialelor planului și o scară grafică](shots/sheet-annotations.webp)

![Aceeași planșă, cum se tipărește](shots/sheet-annotated.webp)

## Tipărirea

Butoanele **PDF** și **DXF** din bara planșei o exportă. Un PDF e gata de tipărit sau de trimis. Un DXF se deschide în AutoCAD și în alte programe CAD cu layerele, liniile și hașurile lui; cercurile, arcele și elipsele (desenate, sau laturile curbe ale planșeelor și ale altor contururi pe un plan) sunt scrise ca CIRCLE, ARC și ELLIPSE adevărate, nu ca linii scurte, și revin ca curbe când DXF-ul e importat din nou. **Tipărește** trimite planșa la imprimantă sau plotter la formatul ei, la 1:1, în linii vectoriale: alegi plotterul și hârtia în fereastra de tipărire. Pe o imprimantă mai mică, alegi încadrarea în pagină.
