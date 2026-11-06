---
title: Planșe, PDF și DXF
order: 100
summary: Așezi vederile pe planșe cu cartuș și le scoți în PDF sau DXF.
---

<!-- video sheets -->

## Cum faci o planșă

Apasă **+** lângă **Planșe** în navigatorul proiectului. Ca să pui o vedere, trage-o din navigator pe planșă sau alege-o în **Pune o vedere…** din bara planșei. Fiecare vedere își păstrează scara și rămâne legată de model, așa că planșa se schimbă odată cu modelul. Pe planșă, trage o vedere ca s-o muți: se mută doar ea, cu titlul ei, iar chenarul, cartușul și celelalte vederi rămân pe loc; se prinde de marginile și de mijlocul chenarului și de celelalte vederi, iar săgețile o împing cu 1 mm (10 mm cu Shift).

![O planșă A3 cu un plan și o secțiune](shots/sheet.webp)

Selectează planșa ca să-i completezi **cartușul** în panoul de proprietăți: proiectul, beneficiarul, cine a desenat, cine a verificat, data și revizia. Tot acolo se setează numărul, formatul și orientarea. Formatul e unul A (A0 până la A4), unul din formatele lungi ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), unul nord-american (Letter, Tabloid, ARCH D, ARCH E) sau **personalizat**, cu **Lățimea hârtiei** și **Înălțimea hârtiei** în milimetri. Lăsat gol, câmpul proiectului arată numele proiectului: numele dat proiectului, altfel numele fișierului lui.

## Ce arată o vedere

O vedere de pe planșă arată exact ce alegi, fără s-o tragi pe ghicite:

- **Decupeaz-o pe planșă.** Clic pe o vedere: apar mânere la colțuri și pe laturi. Le tragi și chenarul cuprinde mai mult sau mai puțin din vedere, iar desenul rămâne pe loc pe hârtie. Cu **Shift**-tras înăuntru deplasezi desenul în chenar.
- **Desenează-i limita în vedere.** **Annotate → View boundary** (<kbd>VB</kbd>): desenezi un dreptunghi pe un plan, o secțiune sau o fațadă și îi dai un nume, unic în proiect. În **Place view…** vederea apare apoi cu fiecare limită a ei („Ground floor plan › Aripa est”), iar o vedere pusă așa arată exact dreptunghiul acela și îl urmează când îl muți sau îl redimensionezi în vedere. Aceeași vedere poate sta pe mai multe planșe, câte o limită pe fiecare. O vedere pusă trece de la o limită la alta, sau la decupare liberă, din lista de pe chenarul ei. Limitele se văd doar pe ecran, niciodată pe hârtie.
- **Fixeaz-o.** Pioneza de pe chenarul vederii o ține pe loc: se selectează, dar nu se mută, nu se împinge și nu se decupează din greșeală.

Chenarul planșei și cartușul sunt ale hârtiei. Nu se selectează și nu se mută, iar un clic alege cea mai mică vedere de sub cursor, așa că o vedere pusă peste cartuș se poate selecta oricând.

![O vedere selectată pe planșă: mânerele de decupare, limita ei, pioneza](shots/sheet-view-boundary.webp)

## Cartușul tău

**Gestionare → Cartușe…** arată cartușele: ale lui Lapin (**Lapin standard**, cel pe care l-au avut toate planșele până acum, **cartușul** românesc și un cartuș **ISO 7200**), ale proiectului și cele din biblioteca acestui calculator, oferite în orice proiect de pe el. Dă clic pe unul ca să-l vezi cu valorile proiectului. **Duplică în proiect** face o copie pe care o poți edita, **Nou** unul gol; **Fă-l implicit pentru proiect** îl dă tuturor planșelor care nu își aleg unul, iar o planșă își alege cartușul la **Cartuș** în proprietățile ei.

![Cartușe: ale lui Lapin, ale proiectului și ale acestui calculator, cel ales cu valorile proiectului](shots/title-blocks.webp)

**Editează…** deschide editorul. Cartușul e un dreptunghi în milimetri de hârtie, așezat într-un colț al chenarului (jos dreapta, implicit), așa că același cartuș se potrivește de la A4 la A0, vertical sau orizontal; marginile chenarului se stabilesc tot aici. Desenezi cu **Linie**, **Dreptunghi**, **Polilinie**, **Text**, **Imagine** (sigla, PNG sau JPEG) și **Tabel de revizii**; un dreptunghi sau o polilinie închisă primește o umplutură sau o hașură, un text o înălțime sau unul din stilurile de text din Standarde de desen. Clicurile se agață de capetele, colțurile și mijloacele elementelor și de grilă; **Shift** ține linia dreaptă. Tragi un element ca să-l muți, mânerele lui ca să-i schimbi forma; săgețile îl mișcă, **Delete** îl șterge, **Ctrl+Z** anulează.

Lista **Câmpuri** are textul viu: al proiectului (nume, număr, beneficiar, amplasament, autorizație, proiectant, persoane și parametrii tăi), al planșei (număr, titlu, scară, format, revizie, stare, dată, desenat, verificat și aprobat, faza, planșa x din y) și al sistemului (data tipăririi, numele fișierului). Un clic pune câmpul în textul selectat sau îl așază ca text nou; un text poate amesteca vorbe și câmpuri, `Proiect: {project.name}`. Editorul arată valoarea fiecărui câmp pe o planșă de exemplu; un câmp fără valoare apare cu numele lui, gri, iar unul pe care Lapin nu-l cunoaște, cu roșu (se tipărește ca `<câmp>` și **Gestionare → Verificare** îl semnalează). **Salvează** păstrează cartușul ca un singur pas de anulat.

![Editorul: un cartuș în stil finlandez, câmpurile cu valorile lor, un câmp adăugat sub numărul planșei](shots/title-block-designer.webp)

Planșele, PDF-ul lor, Tipărirea și emiterea îl desenează cu valorile fiecărei planșe. În DXF e un bloc cu atribute, câte unul pentru fiecare text viu (`PROJECT_NAME`, `SHEET_NUMBER`…), ca indicatoarele din AutoCAD. **Exportă…** salvează un cartuș ca fișier `.json`, cu imaginile lui, iar **Importă…** îl citește în proiect sau în bibliotecă, așa că un birou își mută cartușele de pe un calculator pe altul. Proiectele vechi păstrează Lapin standard și se desenează exact ca înainte.

![Planșa cu cartușul ei](shots/sheet-title-block.webp)

## Tipărirea

Butoanele **PDF** și **DXF** din bara planșei o exportă. Un PDF e gata de tipărit sau de trimis. Un DXF se deschide în AutoCAD și în alte programe CAD cu layerele, liniile și hașurile lui. **Tipărește** trimite planșa la imprimantă sau plotter la formatul ei, la 1:1, în linii vectoriale: alegi plotterul și hârtia în fereastra de tipărire. Pe o imprimantă mai mică, alegi încadrarea în pagină.
