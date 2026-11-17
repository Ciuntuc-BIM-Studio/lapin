---
title: Familii
order: 86
summary: Familiile sunt acum piese — obiecte proprii cu parametri, o masă, un dulap, o bancă — gestionate în Studio → Piese…, așezate ca orice element și exportate ca clasa IFC pe care o alegi.
---

## Familiile sunt acum piese

Ceea ce se numea **familie** este acum o **piesă**. Un proiect salvat cu familii le deschide ca piese, cu aceleași nume, valori și identificatori, iar obiectele așezate din ele rămân unde erau. Fiecare colț care era o formulă din parametri este o schiță proprie a piesei, cu dimensiunile ca restricții, așa că o piesă se editează pe loc: selectezi un obiect al ei și apeși **Tab** (vezi pagina [Piese](studio-parts.md)). Fișierele **`.lapinfamily`** mai vechi se citesc în continuare și intră ca piese.

O piesă este un obiect pe care îl definești o dată și îl așezi de câte ori ai nevoie. Are **parametri** (o lățime, o înălțime, un da/nu pentru un raft) și **corpuri**, fiecare o schiță închisă împinsă la o adâncime, și poate ține și **elemente** precum pereți sau mobilier (vezi Piese). Fiecare obiect așezat își păstrează valorile lui și urmează piesa când o schimbi. O piesă aparține proiectului; ca s-o folosești în alte proiecte, păstreaz-o într-o bibliotecă (mai jos).

## Studio → Piese…

**Studio → Piese…** deschide [catalogul de plasare](interface.md#catalogul-de-pus) pe piese, ca să plasezi una; butonul **Piese…** al lui deschide managerul: piesele proiectului, fiecare cu numărul de obiecte care o folosesc.

![Dialogul Piese: piesele proiectului și, pentru cea aleasă, numele, clasa și butoanele ei](shots/parts-dialog.webp)

- **Nou** pornește o cutie cu parametrii W, H și D, gata de editat.
- **Nume**: scrie unul nou și apasă Enter. **Clasa IFC** este ce se exportă obiectul: Element generic de clădire (implicit), Element de mobilier, Stâlp, Grindă, Membru, Placă, Planșeu, Perete, Terminal de flux și altele. Restul definiției se păstrează.
- **Editează piesa** închide dialogul, trece în 3D și deschide modul de editare al piesei pe un obiect plasat, unde se editează schițele, pașii, parametrii și elementele ei. Dacă piesa nu are încă niciun obiect, se plasează unul în originea nivelului activ, iar o notificare spune asta.
- **Plasează pe plan**, **Exportă…**, **Păstrează pe acest calculator** și **Șterge** sunt dedesubt; ștergerea e refuzată cât timp în proiect stau obiecte ale piesei.
- **Importă…** citește un fișier `.lapinpart` sau unul `.lapinfamily` mai vechi, prin previzualizarea importului.

Parametrii, corpurile și formulele lor nu se mai editează aici într-un tabel: sunt arborele piesei în modul ei de editare (vezi Piese). O celulă rămâne un număr sau o formulă din parametri, precum `W - 0.04`, `H / 2` sau `2 * T`, iar condiția **Făcut când** a unui corp îl oprește în continuare (`HasShelf` sau o comparație precum `W > 1.2`).

## Așezarea obiectelor

**Studio → Piesă** așază un obiect: alegi piesa în bară, faci clic pe plan. **R** o rotește un sfert, **M** o oglindește, iar butoanele barei fac la fel. O fantomă a obiectului, desenată din linii, urmează cursorul pe plan, rotită și oglindită cum spune bara și la lățimea pe care o dau valorile din bară. Bara are și câte un câmp pentru fiecare parametru: scrie o valoare pentru următorul obiect sau lasă-l gol pentru cea a piesei. **Editează…** deschide piesa în manager. **Plasează pe plan** din manager pornește aceeași unealtă.

![Fantoma mesei la cursor, rotită un sfert](shots/part-place-ghost.webp)

![Bara uneltei Piesă: piesa, rotirea, oglindirea și valorile ei](shots/part-place-bar.webp)

Un obiect se mută, se rotește, se oglindește, se copiază și se șterge ca orice element. Se păstrează pe nivelul unde l-ai așezat. Numele vechi ale uneltelor (**Familie**, **Familii…**) merg în continuare în scurtături, unelte rapide și interfețe salvate.

## Proprietățile unui obiect

Selectează un obiect ca să vezi secțiunea **Piesă** din proprietăți. Arată piesa și clasa ei IFC, iar **Piese…** deschide managerul pe ea; butonul **Editează piesa** de mai jos (sau **Tab**) intră în modul de editare. Dedesubt, secțiunea **Parametri** listează doar parametrii pe care piesa îi **publică**: un parametru de instanță e un câmp pentru acest obiect (un câmp gol e valoarea piesei; scrie o valoare ca să dai obiectului una proprie, șterge-o ca să te întorci), un parametru de tip e doar de citit, unul derivat își arată formula. Cu mai multe obiecte ale unei piese selectate, o valoare se pune pe toate. Vezi [Piese](studio-parts.md) pentru configurarea parametrilor. Dedesubt, **Rotire**, **Înălțime deasupra nivelului** și **Oglindit** sunt câmpurile proprii ale obiectului.

![Proprietățile unui obiect: lățimea lui proprie, raftul oprit, valorile piesei în gri](shots/part-properties.webp)

![Obiectele masă în 3D, în casă](shots/part-3d.webp)

O piesă nu poate fi ștearsă cât timp în proiect stau obiecte ale ei: șterge întâi obiectele.

## Fă piesă din schiță

Desenează contururi închise pe un plan, o secțiune sau o fațadă cu uneltele Desen (polilinii închise, dreptunghiuri și cercuri), selectează-le și alege **Fă piesă din schiță…**, în grupul **Piesă** din fila Desen sau cu clic dreapta. Dialogul cere **numele**, **adâncimea** și **clasa IFC**. Contururile unui plan stau culcate, iar cele ale unei secțiuni sau fațade stau în picioare. Cu **Lățimea, înălțimea și adâncimea ca parametri** bifat, piesa are parametrii `W`, `H` și `D`, iar colțurile din dreapta și de sus ale conturului urmează `W` și `H`, deci se întinde. Desenul rămâne unde era. Piesa nouă e aleasă în manager; **Editează piesa** îi deschide schița, desenată din conturul tău și restricționată.

![Fă piesă din schiță: numele, adâncimea și clasa IFC](shots/part-from-sketch.webp)

![Piesa nouă în modul ei de editare: schița este a piesei](shots/part-from-sketch-tree.webp)

## Fișiere și biblioteci

O piesă se exportă ca fișier **`.lapinpart`** (**Exportă…**) și se citește înapoi cu **Importă…**; un fișier `.lapinfamily` făcut de o versiune mai veche se citește și el. **Păstrează pe acest calculator** păstrează o copie pe care **Catalogul** o oferă în fiecare proiect. În aplicația desktop, **Folderele firmei** din dialog listează piesele din folderele firmei; un clic adaugă una în proiect, iar **Salvează în folderul firmei…** scrie piesa aleasă într-un folder scriptibil. Vezi *Folderele firmei în dialogurile bibliotecilor* în pagina Interfață.
