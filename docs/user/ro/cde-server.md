---
title: Lucrul pe un server CDE
order: 126
summary: Te autentifici pe serverul CDE al biroului tău (Atrium Server, cum e cde.lapinbim.com), clonezi un proiect, salvezi lucrul pe server și îl iei pe al celorlalți, ții problemele la zi, conectezi un client AI prin adresa serverului sau cu un token de acces și, ca administrator, gestionezi conturile și proiectele.
---

Un server CDE ține proiectele biroului tău într-un singur loc: fiecare salvare cu autorul și nota ei, versiunile și etichetele de emitere, membrii fiecărui proiect și rolurile lor, și problemele (subiectele BCF) la care răspund toți. Lapin lucrează cu el din aplicația desktop și din browser. Serverul companiei e la **cde.lapinbim.com**; biroul tău își poate rula propriul server, Atrium Server, care e gratuit.

Nu există înregistrare: administratorul serverului îți face contul și îți dă o primă parolă, pe care o schimbi după ce te autentifici.

## Autentificarea

**În browser**, deschizi adresa serverului. Ecranul de start îți cere adresa de e-mail și parola. Închiderea filei te deconectează; o reîncărcare te ține autentificat.

**Pe desktop**, deschizi **Fișier → Colaborare → Colaborează…**, alegi **Server**, scrii adresa serverului și te autentifici. Aplicația păstrează autentificarea pentru data viitoare pe acest calculator, niciodată în proiect.

După autentificare, **Schimbă parola…** îți setează propria parolă; celelalte autentificări ale tale se încheie.

## Proiectele și paginile lor

În browser, ecranul de start listează proiectele din care faci parte, cu rolul tău în fiecare:

- un **Cititor** se uită, păstrează copii și comentează problemele;
- un **Editor** salvează și pe server și face și modifică probleme;
- un **Proprietar** face totul și gestionează membrii și regulile proiectului.

Pagina unui proiect arată istoricul ramurii lui principale (cine a salvat, când, cu ce notă, și versiunile și etichetele de emitere făcute din fiecare salvare), ramurile în lucru, **Activitatea**, **Membrii** și **Regulile**.

Pe desktop, Fișier → Colaborare → Colaborează… arată Publicarea, Activitatea și Regulile proiectului la care e conectat fișierul.

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

**Fișier → Colaborare → Salvează pe server…** cere o notă scurtă și trimite lucrul tău. Bara de stare spune **Nesalvat pe server** cât timp ai lucru pe care serverul nu îl are; un clic deschide Salvează pe server.

**Fișier → Colaborare → Ia ultima versiune de pe server** aduce salvările celorlalți în proiectul tău, într-un singur pas de anulare.

**Deschide ultima versiune, doar pentru citire** (în browser) deschide ultima salvare ca să te uiți la ea: nimic nu se întoarce pe server. Fiecare salvare se poate descărca și ca fișier `.lapin` sau ca copia ei IFC.

## Când au salvat doi oameni

Dacă cineva a salvat pe server după ce ai luat tu ultima versiune, salvarea ta e refuzată și **Fișier → Colaborare → Colaborează…** se deschide pe alegeri:

- **Păstrează lucrul meu ca ramură** trimite lucrul tău pe server ca o ramură în lucru (numită după tine și după dată), apoi aduce proiectul tău la ultima versiune de pe server;
- **Renunță la commit-urile mele** aduce proiectul tău la ultima versiune de pe server; commit-urile tale rămân în fișier ca ramură locală.

## Problemele

Într-un proiect al cărui depozit la distanță e un server CDE, **Gestionare → Observații pe model…** arată problemele serverului. Observațiile din proiect și problemele de pe server sunt aceleași subiecte, ținute la zi după GUID; serverul e referința, iar proiectul păstrează o copie pentru lucrul fără conexiune.

**Sincronizează problemele** (în dialogul de observații) aduce ce s-a schimbat pe server (într-un singur pas de anulare) și trimite ce ai schimbat tu. În Fișier → Colaborare → Colaborează… butonul e **Sincronizează problemele și documentele**: sincronizează cu serverul și [documentele de referință](reference-documents.md) ale proiectului (serverul e adevărul, câmp cu câmp, câștigă ultimul care a scris; fișierele peste limita de mărime a proiectului rămân pe server și se aduc când le deschizi). Salvează pe server și Ia ultima versiune sincronizează și problemele, și documentele. Un server care nu ține documente de referință spune asta și nu se întâmplă nimic altceva.

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

Când proiectul are fișiere IDS, ele se verifică înainte de **Propune spre aprobare**, iar o specificație care nu trece oprește propunerea, dacă nu spui de ce: vezi [Verificări IDS](ids.md).

Emiterea din nou a acelorași planșe face o **revizie nouă a aceleiași emiteri** (Issue… o propune: **Emite ca** → *Revizie nouă a emiterii #n*, revizia de după ultima ei): propunerea și eticheta de emitere poartă numărul și identitatea acelei emiteri, `#1 P01`, apoi `#1 P02`. Odată publicată revizia ulterioară, cea Publicată dinainte trece în **Arhivate**. **Emitere nouă** dă documentelor un număr nou.

Pagina proiectului (în browser) și **Fișier → Colaborare → Colaborează… → Publicare** (pe desktop) au aceste file:

- **Containere**: containerele ISO 19650. **WIP** listează ramurile în lucru; **Comune** și **Publicate** listează etichetele de emitere cu revizia, starea, data și cine le-a emis (**aprobată** când a trecut prin coadă); o revizie Publicată trece în **Arhivate** când aceeași emitere e publicată din nou la o revizie ulterioară. **Deschide** deschide o revizie doar pentru citire; **.lapin** și **IFC** o descarcă; **fișiere** listează fișierele emise, fiecare de descărcat. **Grupează pe foldere** (când serverul ține documente de referință) grupează elementele fiecărui container după folderul rolului lor: rolul se citește din numele ISO 19650 (al șaselea câmp, ca în `PRJ-ORG-ZZ-XX-DR-A-0001`) și se potrivește prin setările de foldere și tipuri ale proiectului (Documente → Folders and types); un rol pe care niciun folder nu-l ia merge la *Altele*.
- **Documente**: documentele de referință ale proiectului, direct pe server, fără să deschizi proiectul (doar în browser, și doar când serverul le ține). Aceleași foldere, aceeași listă și același formular ca Gestionare → Documente de referință…: tragi fișiere ca să le încarci, editezi câmpurile unui document, atașezi sau scoți fișiere, înlocuiești sau retragi un document, bifezi lista certificatului. Un fișier se citește în fundal (textul lui, antetul unui IFC): documentul spune *Se citesc fișierele…* până se termină. Cititorii citesc; editorii adaugă și schimbă documentele pe care le-au adăugat; proprietarii schimbă orice document, șterg unul și editează folderele și tipurile. Dacă altcineva a schimbat documentul în același timp, el se citește din nou și refaci modificarea.
  - **Un model de referință** (un IFC sau un .ifczip de la un consultant, de obicei de tipul *Model*) arată ce a citit serverul din el (schema, programul care l-a scris, proiectul IFC) și, când regula IDS a proiectului e pornită, numărătorile **IDS**; **Arată raportul** listează fiecare specificație care nu trece, cu elementele ei. **Deschide în vizualizator** îl deschide în 3D-ul din browser, doar pentru citire, ca proiect de sine stătător: fiecare element așa cum îl are fișierul. **Deschide cu proiectul** deschide ultimul commit al proiectului doar pentru citire și leagă modelul în el (o legătură doar pentru citire, desenată în vederi și în 3D: vezi [Legături](import-export.md#legaturi)). Nimic nu se întoarce pe server; salvarea păstrează o copie pe acest calculator. O bandă în stânga jos numește modelul și deschide raportul IDS lângă 3D; × închide banda.
- **Propuneri**: coada, întâi cele care așteaptă (**Arată** le alege pe celelalte: aprobate, respinse, retrase, toate). **Trimite fișiere…** (editorii și proprietarii) pune în coadă planșele și modelele altui program, la fel ca emiterile proprii ale proiectului: **Alege fișiere…** ia PDF-uri, DXF-uri și IFC-uri de pe acest calculator, apoi **Revizia** (P01, C01…), **Containerul** (Comune sau Publicate) și **Statutul** lui (S1–S7, sau A1–A9, B1–B9, CR), **Numărul emiterii** (gol pentru următorul) și o **Notă**; **Propune spre aprobare** le trimite. Fișierele își păstrează numele. Emiterea în care devin numește ultimul commit al proiectului pe server, așa că proiectul trebuie salvat întâi o dată pe server. Trec prin aceleași verificări (planșele unui PDF sau DXF, numele fiecărui fișier față de regula de denumire, IFC-urile prin fișierele IDS ale proiectului, nu modelul propriu al proiectului) și prin aceeași poartă și aprobare; coada le marchează **Fișiere de pe disc**. Un clic pe una arată commit-ul, documentele, fișierele și **poarta**: problemele deschise care o opresc. O problemă se leagă de un document printr-o legătură de referință `lapin-document:<numele documentului>`, pe care Lapin o pune singur: o observație făcută cu o planșă în față e legată de documentul planșei, iar dialogul observațiilor arată, adaugă și scoate documentele unei observații (vezi [Observații](cde.md)); Sincronizează problemele trimite legăturile pe server. Fără documente, orice problemă deschisă cu prioritatea Critical o oprește. Un clic pe o problemă a porții o deschide în Observații pe model (în proiectul deschis aici, după Sincronizează problemele).
  - **Aprobă** (cu o notă opțională) face eticheta de emitere; problemele rezolvate legate de propunere se închid. Cât timp poarta nu e îndeplinită, aprobă doar un proprietar, bifând **Aprobă în ciuda problemelor deschise** și scriind o notă: derogarea și nota ei rămân scrise.
  - **Respinge** cere motivul.
  - **Retrage** e a autorului.
  - Ce nu permit rolul tău și regulile proiectului e dezactivat; autorul nu își aprobă propria propunere (decât dacă e singurul proprietar al proiectului).
- **Diferențe la predare**: alege două revizii emise, **Compară**: documentele adăugate, eliminate, schimbate (alt fișier) și neschimbate între ele, și câte părți ale modelului s-au schimbat. **Exportă în XLSX** scrie raportul de predare cu aceste diferențe.
- **Datele proiectului** (când serverul le păstrează; vezi [Datele proiectului](#datele-proiectului) mai jos).
- **Registru** (când serverul verifică documentele): registrul de documente al proiectului. Proprietarii aleg **Importă un registru (CSV sau XLSX)…**: primul rând numește coloanele, codul (obligatoriu: `code`, `document`, `cod`…), titlul, revizia așteptată și orice valoare pe care cartușul trebuie s-o arate (`client`, `beneficiar`…). Fila arată documentele, ce coloane s-au citit și **avertismentele la import** (un rând fără cod, un cod întâlnit de două ori). Un import nou îl înlocuiește; **Șterge** îl scoate. Membrii îl citesc.

**Raport de predare (XLSX)** scrie un registru Excel al proiectului deschis aici: o foaie **Report** (proiectul, serverul, când, de cine, numărătorile), **Issues** (numărul, titlul, tipul, starea, prioritatea, cui e atribuită, termenul, etichetele, datele și autorii, comentariile), **Comments**, **Journal** (fiecare schimbare a problemelor pe server: cine, când, valoarea veche și cea nouă) și, din Diferențe la predare, foaia **Handover diff**. Lapin îl scrie singur; se deschide în Excel, LibreOffice și Numbers.

## Datele proiectului

Serverul păstrează pentru fiecare proiect o fișă cu **datele proiectului**, cu aceleași câmpuri ca **Gestionare → Datele proiectului…** din Lapin: numele, numărul și faza proiectului, beneficiarul, amplasamentul și autorizația, proiectantul, cei care semnează și parametrii proprii ai proiectului. **Salvează pe server**, **Ia ultima versiune de pe server** și **Sincronizează problemele și documentele** le sincronizează cu proiectul deschis în Lapin, câmp cu câmp: un câmp schimbat în Lapin merge pe server, un câmp schimbat pe server vine în Lapin (un pas de anulare), iar când doi oameni au schimbat același câmp rămâne valoarea ultimului care a salvat. O sincronizare care aduce câmpuri de pe server nu e o schimbare de salvat din nou. La prima sincronizare rămân valorile serverului, iar Lapin trimite doar câmpurile pe care serverul nu le are.

Fila **Datele proiectului** din Publicare (în browser, iar pe desktop în **Fișier → Colaborare → Colaborează… → Publicare**) arată fișa:

- **Câmpurile**: cei care văd doar le citesc; editorii și proprietarii le schimbă și apasă **Salvează**. Se trimit doar câmpurile schimbate. Dacă altcineva a schimbat fișa în același timp, ea se citește din nou, modificările tale rămân în formular și salvezi din nou. Pe desktop, proiectul deschis ia schimbarea imediat.
- **Propuneri deschise**: valori propuse de Archicad ([Trimitere în Atrium](archicad.md) sau [scriptul Archicad către Atrium](archicad-to-atrium.md)) ori de un model IFC încărcat în proiect. Fiecare schimbare arată valoarea de acum și cea propusă. Proprietarii bifează valorile de luat și apasă **Aplică valorile bifate**; proprietarii și cel care a făcut propunerea o pot **Respinge**. O propunere mai nouă din aceeași sursă o înlocuiește pe cea deschisă.
- **Din modele**: ce au spus Archicad sau modelele IFC și nu ia niciun câmp (o descriere, cuvinte cheie, numele amplasamentului…), doar pentru citire.

Parametrii proprii ai proiectului sunt listați; îi schimbi în Lapin.


Un server CDE precum cde.lapinbim.com verifică fiecare planșă PDF și DXF pe care o emite o propunere, adesea venită din alte programe, de îndată ce e propusă. Doar **erorile** opresc aprobarea; avertismentele și notele se arată.

- **V1**, numele și reviziile: codul fișierului se potrivește cu regula de denumire a proiectului și e în registru; revizia lui e a propunerii și a registrului și nu se întoarce înapoi; numărul planșei și revizia din cartuș sunt cele din numele fișierului.
- **V2**, cartușul: găsit, cu câmpurile obligatorii, revizia, scara și data scrise ca atare, data nu în viitor, valorile pe care le așteaptă registrul sau setările. O pagină fără text (o scanare) nu se poate citi.
- **V3**, o planșă goală sau desenul ieșit de pe pagină când a fost tipărit.
- **V4**, un substituent: o planșă cu puțin mai mult decât un X.
- **V5**, planșa altui proiect: cartușul numește alt proiect, alt cod sau număr de proiect, ori pe pagină e un cuvânt-cheie al altui proiect.

După **Propune spre aprobare**, Manage → Issue… așteaptă verificările și arată ce au găsit. În **Propuneri**, o propunere arată **Verificări:** și numărătorile în coadă, iar deschisă, verificările lângă problemele porții și raportul: fiecare fișier, ce e greșit în tot fișierul, apoi erorile, avertismentele și notele lui, fiecare cu pagina ei. **pagina n** arată pagina cu fiecare constatare desenată peste ea ca un chenar cu codul ei; ține mouse-ul pe chenar ca să vezi ce spune; **−** și **+** micșorează și măresc. Un DXF se arată ca pagina lui, cu cartușul citit de server, nu cu desenul.

Când verificările găsesc erori, corectează fișierul și propune din nou; sau, când registrul ori setările verificărilor erau greșite, corectează-le și apasă **Rulează din nou verificările** (autorul sau un aprobator, cât timp propunerea așteaptă): propunerea e verificată din nou cu ele. Un proprietar poate totuși aproba cu **Aprobă în ciuda erorilor** și o notă, ca la problemele deschise.

**Reguli → Verificări ale documentelor** (le schimbă proprietarii; ceilalți le citesc):

- **Regula de denumire**: o expresie regulată cu care trebuie să se potrivească tot codul, de exemplu `(?<project>EL24)-[A-Z]{3}-L2-[A-Z0-9]{2}-[A-Z]{2}-[A-Z]-\d{4}` (grupul `project` e câmpul de proiect al codului). **Încearcă un cod** spune pe loc dacă un cod se potrivește.
- **Codul proiectului**, **Numărul proiectului**, **Numele proiectului**: cu ce compară V5; **Cuvintele-cheie ale altui proiect**, câte unul pe rând (codul sau locul unui proiect anterior).
- **Valori așteptate**: rânduri `câmp = valoare` pe care cartușul fiecărei planșe trebuie să le arate (coloanele registrului au întâietate).
- **O dată în viitor după** atâtea zile; **Verificări rulate**: V1–V5, fiecare pornită sau oprită.
- **Cartuș**: *Găsit după etichetele lui* (implicit: DRAWING NO, TITLE, REVISION, DATE, SCALE, PROJECT și formele lor românești, COD PLANȘĂ, TITLU, REVIZIE, DATA, SCARA, PROIECT), sau *Descris aici*: o regiune fixă într-un colț al paginii (mărimea și marginile ei în mm) și câmpurile, fiecare cu felul lui, etichetele după care e tipărit, dacă e obligatoriu, zona lui în regiune și atributele DXF care îl dau.

**Salvează setările verificărilor** le trimite; o valoare pe care serverul n-o acceptă e spusă lângă câmpul ei. Activitatea arată fiecare rulare a verificărilor cu ce a găsit.

## IDS pe server

Un server care îl oferă (Atrium Server de la W6b, precum cde.lapinbim.com) verifică și modelul fiecărei propuneri cu **fișierele IDS** ale proiectului (buildingSMART IDS 1.0, vezi [Verificări IDS](ids.md)): copia IFC pe care Lapin o salvează cu fiecare commit și fiecare fișier `.ifc` sau `.ifczip` pe care îl emite propunerea.

**Publicare → IDS pe server** listează fișierele IDS ale proiectului cu titlul, versiunea, versiunile IFC, câte specificații au, cine le-a încărcat și când. Proprietarii apasă **Adaugă un fișier IDS…** (un fișier cu același nume îl înlocuiește pe cel de acolo) și butonul coș ca să șteargă unul; ceilalți le citesc. Butonul de descărcare păstrează o copie; butonul listă deschide specificațiile fișierului: la ce se aplică fiecare și ce cere. Un fișier care nu e IDS 1.0 e refuzat, cu fiecare problemă găsită de server și rândul ei.

Cu un proiect deschis (Fișier → Colaborare → Colaborează…), proprietarii au și **Pune fișierele IDS ale acestui proiect pe server**: listează fișierele IDS păstrate în proiect (Gestionare → IDS…), fiecare **Nou** pe server, **La fel** ca unul de acolo sau **Modificate** (un fișier cu același nume pe server, cu alt conținut). Bifează-le pe cele de trimis și apasă **Copiază fișierele bifate**; înlocuirea unuia modificat se confirmă întâi. Fiecare fișier pleacă separat: unul refuzat de server își arată problemele lângă el, iar celelalte se trimit oricum. Invers, **Adu fișierele IDS de pe server în acest proiect** e în Gestionare → IDS… (editori și proprietari).

**Reguli** are două comutatoare (le schimbă proprietarii): **Fișierele IDS rulează la fiecare propunere** și **O specificație IDS care nu trece oprește aprobarea** (oprit, o specificație care nu trece e un avertisment, nu o eroare).

Verificările unei propuneri arată un grup **IDS** după documente: câte specificații trec și câte nu, apoi fiecare fișier IDS pe fiecare model, specificație cu specificație, **Trece**, **Nu trece** sau **Sărită** (scrisă pentru altă versiune IFC). Una care nu trece arată ce cere, elementele care nu trec (GlobalId, clasa și numele, cerința, ce are elementul și ce se așteaptă; primele 50 și câte mai sunt) și **GlobalId-uri care nu trec**, cu un buton care le copiază. Notele și avertismentele spun când commit-ul nu are copie IFC pe server sau un model nu a putut fi citit, era prea mare ori a durat prea mult.

Înainte de a propune, **Gestionare → Emite… → Pe serverul CDE → Verifică pe server** (editorii și proprietarii) salvează proiectul pe server și rulează acolo fișierele IDS pe acel commit, cu același raport; nu se propune nimic. E verificarea serverului, cea pe care o socotește aprobarea; fișierele IDS proprii ale proiectului (Gestionare → IDS…) rulează în continuare aici înainte de Propune spre aprobare, ca până acum.

## Testele de coliziune pe server

Un server cu motorul lui Lapin (Atrium Server, acolo unde instalarea lui îl are) rulează și **testele de coliziune** ale proiectului la fiecare propunere: testele pe care le-ai configurat în Lapin (Gestionare → Detectarea coliziunilor: seturile de căutare, testele dure, de distanță și de goluri cu toleranțele lor și stările pe care le-ai dat coliziunilor), rulate de același motor ca pe desktop, pe commit-ul propus. Pe server nu se configurează nimic: schimbă testele în Lapin și salvează pe server.

Verificările unei propuneri arată un grup **Teste de coliziune pe server**: câte coliziuni sunt deschise, câte sunt **noi față de rularea anterioară** (rularea propunerii anterioare, pe alt commit) și câte s-au rezolvat, apoi fiecare test cu numerele lui (un test oprit arată **nu a rulat**). **Arată coliziunile** le listează, întâi cele noi: testul și numărul coliziunii, starea, ambele elemente (nume, clasă, nivel și GlobalId) și cât de adânc se intersectează. Butonul de lângă numere rulează din nou testele pe server pe acel commit (editorii și proprietarii); o încercare de felul acesta nu schimbă niciodată cu ce se compară propunerea următoare. La prima rulare a proiectului pe server toate coliziunile sunt noi.

**Reguli** are încă două rânduri acolo unde serverul le oferă (le schimbă proprietarii): **Testele de coliziune rulează la fiecare propunere** și **Coliziunile dure noi opresc aprobarea**, cu **Coliziuni dure noi permise** (0 implicit). Pornit, o propunere cu mai multe coliziuni dure noi (ale testelor dure și de goluri, noi sau active: nu cele pe care le-ai verificat sau aprobat în Lapin) decât sunt permise nu trece de poarta de publicare, ca o specificație IDS care nu trece; un proprietar o poate aproba totuși cu o notă. Dacă motorul serverului nu poate rula, verificările o spun ca avertisment: nu oprește niciodată o propunere.

## Studiul de însorire pe server

Acolo unde serverul îl oferă (instalarea lui are modulul urban al lui Lapin și o licență pentru el), **Publicare → Studiu de însorire** rulează un studiu de însorire pe cel mai recent commit al proiectului: alege **Ziua** (implicit următorul solstițiu de iarnă) și **Orele** (locale, `9, 12, 15` sau `9:30, 14`) și apasă **Rulează studiul de însorire** (editorii și proprietarii). Serverul calculează umbrele pe teren la fiecare oră în locul proiectului (georeferențierea lui, Gestionare → Amplasament; locul implicit al lui Lapin când nu are, lucru pe care studiul îl spune) și le arată ca imagine, umbrele fiecărei ore colorate de la deschis la închis, cu un tabel al orelor: înălțimea și direcția soarelui (azimutul față de nord) și aria umbrelor. Studiile făcute rămân în listă, cele mai noi întâi; aceeași zi și aceleași ore cerute din nou pe același commit își înlocuiesc studiul. Licența e a serverului: toți membrii proiectului le văd.

## Tokenuri de acces și clienți AI

Un client AI (Claude Code, Claude Desktop sau alt client al Model Context Protocol) poate lucra în proiectele serverului pentru tine: citește documentele de referință și textul fișierelor lor, registrul, containerele, propunerile și problemele, iar când îl lași să scrie, adaugă și completează documente de referință și comentează probleme. Se autentifică fie **prin adresa serverului**, printr-o pagină de autentificare a serverului în browserul tău (conectorii claude.ai și alți clienți care se autentifică singuri), fie cu un **token de acces** pe care îl faci tu (Claude Code, scripturi); are apoi rolul contului tău în fiecare proiect, nimic mai mult. Aprobarea și respingerea propunerilor, înlocuirea, retragerea sau ștergerea documentelor și schimbarea regulilor sau a membrilor rămân la oameni.

**Tokenuri de acces…** (lângă **Schimbă parola…**, pe ecranul de start în browser, și în **Fișier → Colaborare → Colaborează…** pe desktop, autentificat la server) îți arată tokenurile: numele, accesul, când expiră și când au fost folosite ultima dată. Apare când serverul oferă tokenuri de acces.

Ca să conectezi un client:

1. Scrie un nume pentru client (*Claude Code pe laptopul de la birou*: apare în activitatea proiectului lângă ce a schimbat clientul), alege **Doar citire** sau **Citire și scriere**, alege cât durează tokenul (de la 30 de zile la un an; implicit 90 de zile) și apasă **Fă un token**.
2. Tokenul e afișat **o singură dată**. Copiază-l cu butonul de lângă el; nu mai poate fi afișat (dacă îl pierzi, revocă-l și fă altul).
3. Lipește-l **doar în configurația clientului AI**: niciodată într-un chat, un document, un e-mail sau un proiect. Cine îl are lucrează pe server în numele tău până expiră sau până îl revoci. Pentru Claude Code, panoul arată comanda de rulat, cu tokenul în ea:

   ```sh
   claude mcp add --transport http atrium https://cde.lapinbim.com/mcp --header "Authorization: Bearer <token>"
   ```

Butonul coș **revocă** un token: clienții care îl folosesc se opresc imediat. Schimbarea parolei sau dezactivarea contului de către un administrator îți revocă și toate tokenurile.

### Conectarea unei aplicații prin adresa serverului

Clienții care se autentifică singuri (conectorii personalizați din claude.ai, alți clienți MCP care urmează autentificarea lui) nu au nevoie de token: dă-le adresa MCP a serverului, `https://cde.lapinbim.com/mcp` (lista **Aplicații conectate** o arată, cu un buton de copiere). În claude.ai: Settings → Connectors → **Add custom connector**, lipește adresa și conectează.

1. Clientul deschide **pagina de autentificare a serverului** în browser. Verifică în bara de adrese că e serverul tău (`cde.lapinbim.com`), apoi autentifică-te cu e-mailul și parola. Parola ajunge doar la server, niciodată la aplicație.
2. **Pagina de consimțământ** arată numele aplicației, site-ul la care se întoarce browserul după aceea (`claude.ai` pentru claude.ai), contul tău și ce poate face aplicația: să **citească** documentele de referință, registrul, containerele, propunerile și problemele proiectelor și, dacă cere, să le **schimbe** (să adauge și să completeze documente de referință, să comenteze probleme). O aplicație care s-a înregistrat singură e marcată: numele ei e cel pe care și l-a dat singură, deci mergi mai departe doar dacă ai pornit asta dintr-o aplicație în care ai încredere. O aplicație care rulează pe acest calculator primește și ea un avertisment.
3. Apasă **Allow** (permite), **Allow reading only** (doar citire: nu poate schimba nimic) sau **Deny** (refuză); pagina serverului e în engleză. Browserul se întoarce la aplicație, care e acum conectată.

**Aplicații conectate** (în **Tokenuri de acces…**, când serverul o oferă) arată aplicațiile pe care le-ai conectat: numele, site-ul la care se întoarce, accesul, când ai conectat-o și când a fost folosită ultima dată. Butonul coș **revocă** o aplicație: se oprește imediat și funcționează din nou doar dacă o conectezi din nou. Schimbarea parolei sau dezactivarea contului de către un administrator îți revocă și toate aplicațiile. O aplicație nefolosită 30 de zile trebuie să se autentifice din nou.

Ce schimbă clientul apare în **Activitatea** proiectului ca orice altă schimbare, marcat ca făcut prin MCP, cu numele tokenului sau al aplicației.

Tot ce citește un client este trimis furnizorului modelului său: textul documentelor și persoanele numite în certificate, extrase de carte funciară și contracte. Folosește un client căruia biroul tău are voie să-i trimită asta, dă unui token **Doar citire** (unei aplicații **Allow reading only**) dacă nu trebuie să scrie și revocă-l când ai terminat. Pentru dezvoltatori, uneltele și un exemplu sunt în docs/mcp.md.

## Administrare

Un administrator al serverului vede **Administrare** (pe ecranul de start în browser; **Fișier → Colaborare → Colaborează… → Administrare…** pe desktop):

- **Conturi**: fiecare cont cu proiectele lui. **Creează cont** face unul dintr-o adresă de e-mail și un nume; serverul face prima lui parolă și o arată **o singură dată**: copiaz-o și dă-o persoanei. **Dezactivează** încheie autentificările cuiva și le refuză pe cele noi (proiectele, salvările și problemele lui rămân); **Activează** îl lasă să revină. Butonul cu cheie setează o parolă nouă, arătată o singură dată. **Administrator** dă sau retrage administrarea (nu o poți retrage pe a ta și nu te poți dezactiva).
- **Proiect nou**: un proiect făcut pe server, cu tine proprietar. Deschide-i pagina ca să adaugi membrii.
- **Mesaje e-mail de trimis**: mesajele pe care le scrie serverul (o propunere către aprobatorii ei, aprobarea sau respingerea către autor, o problemă către noul ei responsabil), cu starea lor. Trimiterea e oprită până când administratorul serverului îi pune setările SMTP; până atunci mesajele așteaptă aici și nu se pierde nimic.

Browserul nu păstrează nimic din parole: sunt arătate o singură dată și dispar.
