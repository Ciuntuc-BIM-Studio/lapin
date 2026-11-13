---
title: Familii
order: 86
summary: Obiecte proprii cu parametri — o masă, un dulap, o bancă — făcute din corpuri ale căror dimensiuni urmează formule, așezate ca orice element și exportate ca clasa IFC pe care o alegi.
---

## Ce este o familie

O **familie** este un obiect pe care îl definești o dată și îl așezi de câte ori ai nevoie. Are **parametri** (o lățime, o înălțime, un da/nu pentru un raft) și **corpuri**, fiecare un contur închis împins la o adâncime. Fiecare colț, adâncime și deplasare este un număr sau o **formulă** din parametri, așa că o familie dă o masă de orice mărime. Fiecare obiect așezat își păstrează valorile lui și urmează familia când o schimbi.

O familie e mai ușoară decât un configurator de tip: nu are straturi sau dimensiuni de catalog, ci doar solidele pe care le descrii. Aparține proiectului. Ca s-o folosești în alte proiecte, păstrează-o într-o bibliotecă (mai jos).

## Studio → Familii…

**Studio → Familii…** deschide familiile proiectului, fiecare cu numărul de obiecte care o folosesc. **Nou** pornește o cutie cu parametrii W, H și D.

![Dialogul Familii: o masă cu schița ei în fire, valorile de încercat și parametrii](shots/families-dialog.webp)

- **Nume**, **Clasă IFC** și **Descriere** vin întâi. Clasa este ce devine obiectul la export: Building element proxy (implicit), Furnishing element, Column, Beam, Member, Plate, Slab, Wall, Flow terminal și altele.
- **Schița în fire** e construită de motor din familia așa cum e acum, la fiecare schimbare. Scrie valori la **Încearcă valori** ca să vezi altă mărime; una goală e cea implicită.
- **Parametrii** au o cheie (litere și cifre, începând cu o literă: `W`, `Height`, `HasShelf`), o etichetă, un fel și o valoare implicită. Felurile sunt Lungime, Număr, Unghi și Da/nu. Aici lungimile sunt în metri și unghiurile în grade.
- **Corpurile** au fiecare un plan: *Culcat (xy)* e un contur pe podea împins în sus, *În picioare, față (xz)* și *În picioare, lateral (yz)* stau în picioare. **Colțurile** sunt rânduri de două celule, x și y. **Adâncime** e distanța pe care e împins conturul. **Deplasare x, y, z** mută corpul față de originea obiectului. **Material** îi dă un material al proiectului; fără el e gri.

Apasă **Salvează** ca să păstrezi schimbările: obiectele deja așezate le urmează. **Renunță la modificări** revine la familia salvată.

## Formule și condiții

O celulă are un număr sau o formulă: `W - 0.04`, `H / 2`, `2 * T`. Numele sunt cheile parametrilor, iar funcțiile sunt cele ale formulelor de parametri (min, max, round, floor, ceil, sqrt, sin, cos, tan, atan2, if, clamp). Deschide **Formule** în josul dialogului pentru lista numelor pe care le poate folosi familia. O celulă cu formulă e arătată colorat.

**Făcut când** este o condiție: corpul se face doar când ea nu este 0. Merge un parametru da/nu ca `HasShelf`, și o comparație ca `W > 1.2`. În masa de mai sus raftul are `HasShelf`, deci obiectele cu raftul oprit nu au raft.

Când o formulă nu se citește, motorul spune de ce sub imagine, iar **Salvează** o refuză. Schița rămâne la ultima bună.

![O condiție care nu se citește: fraza motorului sub imagine](shots/families-error.webp)

## Așezarea obiectelor

**Studio → Familie** așază un obiect: alege familia în bară și dă clic pe plan. **R** îl întoarce un sfert, **M** îl oglindește, iar butoanele barei fac la fel. Bara are și un câmp pentru fiecare parametru: scrie o valoare pentru următorul obiect sau lasă-l gol pentru cea a familiei. **Editează…** deschide familia. **Așază pe plan** din dialog pornește aceeași unealtă.

![Bara uneltei Familie: familia, întoarcerea, oglindirea și valorile ei](shots/family-place-bar.webp)

Un obiect se mută, se rotește, se oglindește, se copiază și se șterge ca orice element. Se ține pe nivelul unde l-ai așezat.

## Proprietățile unui obiect

Selectează un obiect ca să vezi secțiunea **Familie** din proprietăți. Arată familia și clasa ei IFC, iar **Editează familia…** deschide dialogul pe ea. Fiecare parametru are valoarea lui: cea a familiei e arătată cenușiu, iar un câmp gol este al familiei. Scrie o valoare ca să-i dai obiectului una proprie și golește câmpul ca să revii. Cu mai multe obiecte ale unei familii selectate, o valoare se pune pe toate. Dedesubt, **Rotație**, **Înălțimea deasupra nivelului** și **Oglindit** sunt câmpurile proprii ale obiectului.

![Proprietățile unui obiect: lățimea lui proprie, raftul oprit, valorile familiei cenușii](shots/family-properties.webp)

![Obiectele masă în 3D, în casă](shots/family-3d.webp)

O familie nu poate fi ștearsă cât timp în proiect stau obiecte de-ale ei: șterge întâi obiectele.

## Fă familie din schiță

Desenează contururi închise pe un plan, o secțiune sau o fațadă cu uneltele Desen (polilinii închise, dreptunghiuri și cercuri), selectează-le și alege **Fă familie din schiță…**, în grupul **Familie** al filei Desen sau cu clic dreapta. Dialogul întreabă **numele**, **adâncimea** și **clasa IFC**. Contururile unui plan stau culcate, iar ale unei secțiuni sau fațade stau în picioare. Cu **Lățimea, înălțimea și adâncimea ca parametri** bifat, familia are parametrii `W`, `H` și `D`, iar colțurile din dreapta și de sus ale conturului urmează `W` și `H`, deci se întinde. Desenul rămâne unde era. Familia nouă se deschide în editor.

![Fă familie din schiță: numele, adâncimea și clasa IFC](shots/family-from-sketch.webp)

## Fișiere și biblioteci

O familie se exportă ca fișier **`.lapinfamily`** (**Exportă…**) și se citește înapoi cu **Importă…**. **Păstrează pe acest calculator** păstrează o copie pe care **Catalogul** o oferă în orice proiect. În aplicația desktop, **Folderele firmei** din dialog arată familiile din folderele firmei; un clic adaugă una în proiect, iar **Salvează în folderul firmei…** scrie familia aleasă într-un folder care poate fi scris. Vezi *Folderele firmei în dialogurile bibliotecilor* în pagina Interfață.
