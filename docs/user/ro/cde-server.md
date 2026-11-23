---
title: Lucrul pe un server CDE
order: 126
summary: Te autentifici pe serverul CDE al biroului tău (Atrium Server, cum e cde.lapinbim.com), clonezi un proiect, salvezi lucrul pe server și îl iei pe al celorlalți, ții problemele la zi și, ca administrator, gestionezi conturile și proiectele.
---

Un server CDE ține proiectele biroului tău într-un singur loc: fiecare salvare cu autorul și nota ei, versiunile și etichetele de emitere, membrii fiecărui proiect și rolurile lor, și problemele (subiectele BCF) la care răspund toți. Lapin lucrează cu el din aplicația desktop și din browser. Serverul companiei e la **cde.lapinbim.com**; biroul tău își poate rula propriul server, Atrium Server, care e gratuit.

Nu există înregistrare: administratorul serverului îți face contul și îți dă o primă parolă, pe care o schimbi după ce te autentifici.

## Autentificarea

**În browser**, deschizi adresa serverului. Ecranul de start îți cere adresa de e-mail și parola. Închiderea filei te deconectează; o reîncărcare te ține autentificat.

**Pe desktop**, deschizi **Fișier → Colaborează…**, alegi **Server**, scrii adresa serverului și te autentifici. Aplicația păstrează autentificarea pentru data viitoare pe acest calculator, niciodată în proiect.

După autentificare, **Schimbă parola…** îți setează propria parolă; celelalte autentificări ale tale se încheie.

## Proiectele și paginile lor

În browser, ecranul de start listează proiectele din care faci parte, cu rolul tău în fiecare:

- un **Cititor** se uită, păstrează copii și comentează problemele;
- un **Editor** salvează și pe server și face și modifică probleme;
- un **Proprietar** face totul și gestionează membrii și regulile proiectului.

Pagina unui proiect arată istoricul ramurii lui principale (cine a salvat, când, cu ce notă, și versiunile și etichetele de emitere făcute din fiecare salvare), ramurile în lucru, **Activitatea**, **Membrii** și **Regulile**.

Pe desktop, Fișier → Colaborează… arată Publicarea, Activitatea și Regulile proiectului la care e conectat fișierul.

O **etichetă de emitere** e o emitere făcută cu **Manage → Issue…** (revizia, starea, containerul și fișierele ei) odată ajunsă pe server; nu se mai schimbă niciodată.

**Activitatea** listează, cele mai noi primele, salvările pe server, versiunile și etichetele de emitere, propunerile și deciziile asupra lor, și fiecare schimbare a unei probleme: cine a creat-o, a comentat-o, i-a schimbat starea, a atribuit-o. **Mai multe** citește intrările mai vechi.

**Membri**: proprietarii adaugă pe cineva care are un cont pe server (după adresa de e-mail), schimbă un rol sau scot un membru. Un proiect păstrează întotdeauna cel puțin un proprietar.

**Reguli** (le schimbă proprietarii; ceilalți le citesc):

- cine salvează în main: editorii și proprietarii, sau doar proprietarii;
- cine face direct etichetele de emitere: doar proprietarii, sau editorii și proprietarii;
- cine aprobă propunerile: doar proprietarii, sau editorii și proprietarii;
- cine comentează problemele: toată lumea, și cititorii, sau editorii și proprietarii;
- cine închide o problemă: autorul ei și proprietarii, sau oricine o poate modifica;
- dacă **Rezolvată** cere un comentariu care spune cum a fost rezolvată.

## Clonează, salvează pe server, ia ultima versiune

**Clonează pentru lucru** aduce proiectul cu istoricul lui. În browser copia stă în browser până salvezi pe server; pe desktop ajunge într-un fișier nou.

**Fișier → Salvează pe server…** cere o notă scurtă și trimite lucrul tău. Bara de stare spune **Nesalvat pe server** cât timp ai lucru pe care serverul nu îl are; un clic deschide Salvează pe server.

**Fișier → Ia ultima versiune de pe server** aduce salvările celorlalți în proiectul tău, într-un singur pas de anulare.

**Deschide ultima versiune, doar pentru citire** (în browser) deschide ultima salvare ca să te uiți la ea: nimic nu se întoarce pe server. Fiecare salvare se poate descărca și ca fișier `.lapin` sau ca copia ei IFC.

## Când au salvat doi oameni

Dacă cineva a salvat pe server după ce ai luat tu ultima versiune, salvarea ta e refuzată și **Fișier → Colaborează…** se deschide pe alegeri:

- **Păstrează lucrul meu ca ramură** trimite lucrul tău pe server ca o ramură în lucru (numită după tine și după dată), apoi aduce proiectul tău la ultima versiune de pe server;
- **Renunță la commit-urile mele** aduce proiectul tău la ultima versiune de pe server; commit-urile tale rămân în fișier ca ramură locală.

## Problemele

Într-un proiect al cărui depozit la distanță e un server CDE, **Gestionare → Observații pe model…** arată problemele serverului. Observațiile din proiect și problemele de pe server sunt aceleași subiecte, ținute la zi după GUID; serverul e referința, iar proiectul păstrează o copie pentru lucrul fără conexiune.

**Sincronizează problemele** (în dialogul de observații și în Fișier → Colaborează…) aduce ce s-a schimbat pe server (într-un singur pas de anulare) și trimite ce ai schimbat tu. Salvează pe server și Ia ultima versiune sincronizează și ele problemele.

- Doi oameni care schimbă **câmpuri diferite** ale aceleiași probleme (unul prioritatea, celălalt descrierea) își păstrează amândoi modificarea.
- Când doi oameni schimbă **același câmp**, câștigă cel care sincronizează ultimul. Activitatea arată cine ce a schimbat.
- Un comentariu nou ajunge pe server înaintea unei schimbări de stare, așa că **Rezolvată** găsește comentariul care o explică.
- O problemă pe care o ștergi se șterge și pe server când rolul tău o permite (proprietarii); altfel revine la următoarea sincronizare.

Ce nu permite rolul tău apare estompat:

- cititorii nu fac probleme; un cititor căruia i s-a atribuit o problemă îi schimbă doar starea;
- editorii schimbă problemele pe care le-au făcut sau care le-au fost atribuite;
- doar autorul problemei sau un proprietar o închide (afară de cazul în care regulile proiectului lasă pe oricine are voie să o modifice să o închidă);
- doar proprietarii șterg probleme.

Tipurile, stările, prioritățile și etichetele oferite sunt cele proprii proiectului pe server, iar **Responsabil** listează membrii proiectului. Când serverul refuză o schimbare, spune de ce (de exemplu *Spune cum a fost rezolvată: adaugă un comentariu, apoi setează Rezolvată.*): o valoare pe care rolul tău nu o poate seta revine așa cum o are serverul; o stare care așteaptă comentariul rămâne în proiectul tău până adaugi comentariul și sincronizezi din nou.

## Publicarea

Pe un server CDE o emitere ajunge din Lapin pe server în două feluri, amândouă de la sfârșitul **Manage → Issue…**, sub **Pe serverul CDE**. În ambele cazuri proiectul se salvează întâi pe server, ca emiterea să meargă cu o salvare pe care serverul o are; fișierele păstrate în proiect (PDF-urile, borderoul, IFC-ul când e păstrat) merg cu ea.

- **Propune spre aprobare** cere unui aprobator să publice emiterea. Ea așteaptă în **Propuneri** până când un aprobator o aprobă (serverul îi face atunci eticheta de emitere) sau o respinge. Propun editorii și proprietarii.
- **Fă acum eticheta de emitere** pune eticheta de emitere pe server imediat, fără aprobare, acolo unde regulile proiectului îți permit (implicit proprietarilor). Pe un dosar la distanță e singurul fel.

O emitere WIP rămâne în proiect: emite în Comune sau Publicate ca să ajungă pe server.

Emiterea din nou a acelorași planșe face o **revizie nouă a aceleiași emiteri** (Issue… o propune: **Emite ca** → *Revizie nouă a emiterii #n*, revizia de după ultima ei): propunerea și eticheta de emitere poartă numărul și identitatea acelei emiteri, `#1 P01`, apoi `#1 P02`. Odată publicată revizia ulterioară, cea Publicată dinainte trece în **Arhivate**. **Emitere nouă** dă documentelor un număr nou.

Pagina proiectului (în browser) și **Fișier → Colaborează… → Publicare** (pe desktop) au trei file:

- **Containere**: containerele ISO 19650. **WIP** listează ramurile în lucru; **Comune** și **Publicate** listează etichetele de emitere cu revizia, starea, data și cine le-a emis (**aprobată** când a trecut prin coadă); o revizie Publicată trece în **Arhivate** când aceeași emitere e publicată din nou la o revizie ulterioară. **Deschide** deschide o revizie doar pentru citire; **.lapin** și **IFC** o descarcă; **fișiere** listează fișierele emise, fiecare de descărcat.
- **Propuneri**: coada, întâi cele care așteaptă (**Arată** le alege pe celelalte: aprobate, respinse, retrase, toate). Un clic pe una arată commit-ul, documentele, fișierele și **poarta**: problemele deschise care o opresc. O problemă se leagă de un document printr-o legătură de referință `lapin-document:<numele documentului>`, pe care Lapin o pune singur: o observație făcută cu o planșă în față e legată de documentul planșei, iar dialogul observațiilor arată, adaugă și scoate documentele unei observații (vezi [Observații](cde.md)); Sincronizează problemele trimite legăturile pe server. Fără documente, orice problemă deschisă cu prioritatea Critical o oprește. Un clic pe o problemă a porții o deschide în Observații pe model (în proiectul deschis aici, după Sincronizează problemele).
  - **Aprobă** (cu o notă opțională) face eticheta de emitere; problemele rezolvate legate de propunere se închid. Cât timp poarta nu e îndeplinită, aprobă doar un proprietar, bifând **Aprobă în ciuda problemelor deschise** și scriind o notă: derogarea și nota ei rămân scrise.
  - **Respinge** cere motivul.
  - **Retrage** e a autorului.
  - Ce nu permit rolul tău și regulile proiectului e dezactivat; autorul nu își aprobă propria propunere (decât dacă e singurul proprietar al proiectului).
- **Diferențe la predare**: alege două revizii emise, **Compară**: documentele adăugate, eliminate, schimbate (alt fișier) și neschimbate între ele, și câte părți ale modelului s-au schimbat. **Exportă în XLSX** scrie raportul de predare cu aceste diferențe.

**Raport de predare (XLSX)** scrie un registru Excel al proiectului deschis aici: o foaie **Report** (proiectul, serverul, când, de cine, numărătorile), **Issues** (numărul, titlul, tipul, starea, prioritatea, cui e atribuită, termenul, etichetele, datele și autorii, comentariile), **Comments**, **Journal** (fiecare schimbare a problemelor pe server: cine, când, valoarea veche și cea nouă) și, din Diferențe la predare, foaia **Handover diff**. Lapin îl scrie singur; se deschide în Excel, LibreOffice și Numbers.

## Administrare

Un administrator al serverului vede **Administrare** (pe ecranul de start în browser; **Fișier → Colaborează… → Administrare…** pe desktop):

- **Conturi**: fiecare cont cu proiectele lui. **Creează cont** face unul dintr-o adresă de e-mail și un nume; serverul face prima lui parolă și o arată **o singură dată**: copiaz-o și dă-o persoanei. **Dezactivează** încheie autentificările cuiva și le refuză pe cele noi (proiectele, salvările și problemele lui rămân); **Activează** îl lasă să revină. Butonul cu cheie setează o parolă nouă, arătată o singură dată. **Administrator** dă sau retrage administrarea (nu o poți retrage pe a ta și nu te poți dezactiva).
- **Proiect nou**: un proiect făcut pe server, cu tine proprietar. Deschide-i pagina ca să adaugi membrii.
- **Mesaje e-mail de trimis**: mesajele pe care le scrie serverul (o propunere către aprobatorii ei, aprobarea sau respingerea către autor, o problemă către noul ei responsabil), cu starea lor. Trimiterea e oprită până când administratorul serverului îi pune setările SMTP; până atunci mesajele așteaptă aici și nu se pierde nimic.

Browserul nu păstrează nimic din parole: sunt arătate o singură dată și dispar.
