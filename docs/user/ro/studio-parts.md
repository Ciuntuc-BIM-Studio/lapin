---
title: Piese
order: 87
summary: Editează pe loc un solid Studio sau o piesă plasată cu Tab — restul modelului se estompează, iar arborele piesei arată originea, schițele, pașii și parametrii.
---

## Ce este o piesă

O **piesă** este un solid Studio cu o definiție în spate: un solid schițat cu pașii lui, sau un obiect propriu ale cărui corpuri urmează **parametri** (ceea ce era înainte o familie). Piesa se plasează de câte ori ai nevoie, iar fiecare obiect își păstrează propriile valori ale parametrilor.

**Familiile sunt acum piese.** Un proiect salvat cu familii se deschide cu aceleași obiecte ca piese, cu aceleași nume, corpuri, valori și identificatori, iar pagina Familii descrie în continuare cum sunt făcute. **Studio → Familii…** și uneltele ei funcționează ca până acum.

## Editarea unei piese

Selectează în 3D un solid Studio sau o piesă plasată, apoi fă una dintre acestea:

- apasă **Tab**;
- dă **dublu clic** pe piesă în 3D;
- apasă **Editează piesa** în **Studio → Piesă**, sau în secțiunea **Piesă** din Proprietăți.

Piesa intră atunci în **modul de editare**. Restul modelului se estompează și nu poate fi selectat, dar rămâne acolo ca să te prinzi de el. Piesa se desenează ca de obicei, iar bara de deasupra vederii spune ce editezi.

![Masa în model, selectată](shots/studio-part-model.webp)

![Masa în modul de editare: modelul estompat, planul din față al originii afișat, arborele piesei în stânga](shots/studio-part-edit.webp)

Ca să ieși, apasă din nou **Tab** sau **Esc**, ori **Termină piesa** în arbore. Ieșirea nu este un pas de anulare; fiecare modificare făcută înăuntru este un pas de anulare, ca peste tot în Lapin.

Tab nu face nimic special cât timp un câmp de text are focusul, cât timp e deschis un dialog sau cât timp rulează o unealtă: atunci strânge panourile ca înainte.

## Arborele piesei

Panoul din stânga vederii arată din ce e făcută piesa și se citește din nou după fiecare modificare.

![Arborele piesei: origine, schițe, pași și parametri](shots/studio-part-tree.webp)

- **Origine** are cele trei planuri ale piesei: sus (xy), față (xz) și dreapta (yz). Du cursorul pe unul ca să-l vezi în 3D, sau dă clic pe el ca să rămână afișat; încă un clic îl ascunde.
- **Schițe** listează schițele piesei. O schiță a unui solid (extrudare, baleiere, revoluție, loft) se deschide pe vederea ei, ca la **Editează schița**; fiindcă schița trăiește pe vederea ei, modul de editare se încheie când o deschizi. O piesă făcută din corpuri se deschide în editorul ei, cu piesa aleasă.
- **Pași** listează din ce e făcută piesa, în ordine: operația ei sau corpurile, apoi pașii făcuți asupra ei (împinge/trage, racordare, golire, împărțire…). Un pas oprit apare cenușiu; un corp făcut doar când se îndeplinește o condiție o arată (*oprit când nu HasShelf*). La pașii făcuți asupra piesei, butoanele pornesc sau opresc pasul, îl mută mai devreme sau mai târziu ori îl scot. Un clic pe numele unui pas arată piesa în Proprietăți, unde se editează numerele lui.
- **Parametri** listează fiecare parametru cu valoarea, unitatea și valoarea implicită, și dacă ține de fiecare obiect (*instanță*) și este *publicat*. Scrie o valoare nouă și apasă Enter: obiectul piesei o urmează. Un parametru diferit de cel implicit apare îngroșat; unul derivat își arată formula. **Parametri…** deschide tabelul întreg (secțiunea următoare).

## Tabelul de parametri

**Parametri…** în arbore deschide lângă el un tabel mai lat, unde parametrii se fac și se pun la punct. Rândurile sunt grupate după *grup*, și fiecare are:

- **Nume**: eticheta și, lângă ea, cheia folosită în formule.
- **Valoare sau formulă**: valoarea proprie a piesei, de la care pornește fiecare obiect. Scrie o valoare (o lungime în unitatea proiectului sau cu a ei: `1600`, `1.6m`) sau `=` și o formulă din ceilalți parametri (`=W / 2`): parametrul devine **derivat**, valoarea lui se calculează la fiecare cerere și nu se păstrează. O valoare scrisă peste o formulă o ia pe aceasta.
- **Acest obiect**: valoarea proprie a obiectului, pentru un parametru de *instanță*. Un parametru de *tip* arată doar valoarea implicită, aceeași pentru fiecare obiect, iar unul derivat arată `= valoare`, cu formula la trecerea mouse-ului.
- **Inst.**: instanță (fiecare obiect poate avea valoarea lui) sau tip (valoarea piesei pentru toate). Dacă schimbi un parametru de instanță în tip, valorile proprii ale obiectelor se pierd, iar tabelul o spune.
- **Publ.**: publicat, adică apare în Proprietățile obiectului din model. Un parametru nepublicat rămâne treaba internă a piesei.
- **⋯**: meniul rândului deschide restul: cheia (redenumirea ei o redenumește și în formule), eticheta, descrierea, grupul, minimul și maximul, felul și unitatea, **Mută în sus**, **Mută în jos** și **Șterge**. Motorul refuză **Șterge** cât timp o formulă sau o cotă de schiță încă folosește parametrul, și spune unde, sub rând.

**+ Parametru** adaugă unul din felul ales alături: lungime, număr, unghi, da/nu, text sau număr întreg. Fiecare schimbare este un pas de anulare.

![Tabelul de parametri: grupe, un parametru derivat cu formula lui, tip și instanță, publicat](shots/studio-part-params.webp)

## Flex

**Flex** (comutatorul din tabel) te lasă să încerci valori fără să schimbi piesa. Cât timp e pornit, schimbarea unei valori cere motorului doar o **previzualizare**: piesa cu acele valori se desenează ca o **fantomă** punctată peste piesă în 3D, iar **volumul** ei apare sub tabel. O valoare care nu dă o piesă (o adâncime nepozitivă, o formulă care eșuează) arată motivul motorului în locul fantomei. Un parametru de instanță flexează în **Acest obiect**, unul de tip în celula valorii lui implicite.

**Păstrează** aplică valorile încercate: o valoare de instanță obiectului acesta, valoarea unui parametru de tip ca noua valoare implicită a piesei, totul într-un singur pas de anulare. **Pune înapoi** le uită; oprirea Flex-ului sau închiderea tabelului face la fel.

![Flex: lățimea schimbată la 2 m, fantoma punctată peste masă, volumul sub tabel](shots/studio-part-flex.webp)

## În model: parametrii publicați

Înapoi în model, Proprietățile unei piese puse arată **doar parametrii ei publicați**. Un parametru de instanță este un câmp de editat pentru acest obiect (un câmp gol e valoarea implicită a piesei); unul de tip este doar de citit, cu insigna *tip*, fiindcă ține de piesă; unul derivat este doar de citit, cu insigna *formulă* și formula la trecerea mouse-ului. **Editează piesa (Tab)** intră în modul de editare, unde se pun la punct restul.

## Constrânge piesa: schițele proprii ale piesei

O piesă ale cărei corpuri sunt scrise ca colțuri (`[W, D]`, `[W - L, L]`…) poate deveni o piesă cu **schițe proprii**. În arborele piesei apasă **Constrânge piesa** (și în secțiunea **Piesă** din Proprietăți). Conturul fiecărui corp devine o schiță pe planul lui, fiecare colț ținut de cote orizontale și verticale care poartă aceleași formule, așa că piesa își păstrează exact forma și volumul. Arborele listează apoi schițele, cu cât de libere sunt, iar butonul dispare. Este un singur pas de anulare.

![Arborele după Constrânge piesa: schițele piesei, fiecare complet constrânsă](shots/studio-part-constrained.webp)

## Editarea schiței unei piese

Dă clic pe o schiță a piesei din arbore (sau dublu clic pe piesă în modul ei de editare, care deschide prima schiță cu un corp). Schița se editează **pe loc**:

- camera 3D se întoarce să privească drept spre planul schiței, cu o mișcare lină, și revine unde era când ieși;
- restul piesei rămâne estompat în spate, iar schița este desenată peste vedere: formele ei colorate după cât de libere sunt (albastru liber, închis complet ținut, roșu în conflict), glifele constrângerilor și cotele;
- bara de jos numește schița, spune câte **grade de libertate** mai are și ține uneltele.
- editarea schiței editează **piesa**, deci fiecare obiect al ei urmează. Schița se arată cum o păstrează piesa, cu valorile ei **implicite**: când obiectul are valori proprii (o lățime de 1600 unde implicitul piesei este 1200), bara spune *Editezi piesa: valorile arătate sunt cele implicite*, iar conturul propriu al obiectului este desenat punctat în spate.

![Schița unei piese editată pe loc: camera drept spre plan, formele, glifele și bara de unelte](shots/studio-part-sketch.webp)

- **Mută un punct** trăgându-l de mânerul pătrat: motorul rezolvă constrângerile în timp ce tragi și arată rezultatul pe loc; dacă mutarea nu e posibilă, spune de ce. O tragere este un singur pas de anulare.
- **Desenează**: **Linie**, **Dreptunghi**, **Cerc** și **Arc (3 puncte)** în bară. Dă clic pe puncte; un punct aproape de unul existent se lipește de el și rămâne **coincident** cu el (ține Ctrl apăsat la clic ca să-l pui liber), iar laturile unui dreptunghi rămân orizontale și verticale. Fiecare formă este un pas de anulare, iar formele deja existente își păstrează identitatea, deci constrângerile lor rămân. **Șterge** scoate forma pe care dai clic (sau selectează o formă și apasă Delete). Esc lasă unealta jos.
- **Constrângeri**: aceleași unelte ca la schița unui plan (Coincident, Orizontal, Egal, Fixează… și cotele), alese pe această schiță. Dublu clic pe o cotă îi schimbă valoarea, sau scrie `=` și o formulă din parametrii piesei (`=W - 2*L`): piesa o urmează, în fiecare obiect al ei. Vezi [Constrângerile schiței](sketch-constraints.md).

![O linie adăugată cu unealta Linie](shots/studio-part-sketch-line.webp)

Ieși cu **Termină schița**, cu **Esc** (după ce unealta e jos) sau cu **Tab**: ești înapoi în modul de editare al piesei, iar camera e cum era.

## Schițe și corpuri în arbore

- **+ Schiță** adaugă o schiță pe planul ales (sus, față sau dreapta). Lângă fiecare schiță a piesei sunt butoane pentru a o redenumi și a o șterge (corpurile desenate din ea pleacă odată cu ea).
- **+ Corp din schiță** face un corp din forma închisă a unei schițe: alege schița și scrie **adâncimea** lui, o lungime sau o formulă din parametri (`=H - T`). Sub pasul corpului din arbore se pot schimba adâncimea și condiția *făcut când*, iar corpul se poate scoate.

În dialogul Familii, un corp desenat dintr-o schiță spune *Desenat din schița* urmat de numele schiței, în loc de un tabel de colțuri: schița se editează în piesă, cu **Editează schița în piesă** (are nevoie de un obiect al piesei în proiect).

## Mai departe

Pagina Studio spune cum modelezi solide, iar pagina Familii cum fac parametrii și formulele o piesă.

Schițele unei piese pot fi ținute cu **constrângeri** și cote: vezi [Constrângerile schiței](sketch-constraints.md).
