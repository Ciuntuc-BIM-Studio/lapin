---
title: Documente
order: 105
summary: Piesele scrise ale proiectului — memoriul tehnic, caietul de sarcini, borderoul — făcute din șabloane, completate din model, editate pe pagină, în stilul firmei tale, și exportate în DOCX, ODT și PDF fără niciun alt program.
---

## Ce e un document

Un proiect nu înseamnă doar planșe: are și piese scrise, un memoriu tehnic, un caiet de sarcini, un borderou. În Lapin acestea sunt **documente**: fiecare pornește dintr-un șablon și se completează din proiect — datele lui, suprafețele, nivelurile și încăperile, tipurile de perete, extrasele și planșele. Ce știi doar tu (structura, securitatea la incendiu, organizarea de șantier) scrii în intrările documentului. Documentul urmează modelul: schimbi un tip de perete sau adaugi o încăpere, iar documentul o spune data viitoare când îl deschizi.

Lapin scrie singur fișierele, în DOCX, ODT și PDF, fără Word sau LibreOffice pe calculator, în aplicația desktop și în browser.

## Datele proiectului

**Gestionare → Datele proiectului…** păstrează ce cer documentele de la proiect: numele, numărul și faza, beneficiarul și adresa lui, adresa amplasamentului, numărul cadastral și cartea funciară, proiectantul și adresa lui, arhitectul șef, cine a desenat și cine a verificat. Le scrii o singură dată: toate documentele le citesc, iar cartușele planșelor iau de aici cine a desenat și cine a verificat când planșa nu spune. Fiecare câmp se salvează când îl părăsești.

![Datele proiectului: numărul, faza, beneficiarul, amplasamentul și proiectantul](shots/project-info.webp)

Aceleași câmpuri sunt în tabul **Datele proiectului** al dialogului Documente.

## Un document nou

**Gestionare → Documente…** (sau **+** de lângă **Documente** în browserul proiectului) deschide documentele proiectului. În stânga sunt documentele făcute deja și, sub **Nou din șablon**, șabloanele; un clic pe unul face documentul nou și îl completează pe loc.

![Documente: documentele proiectului și șabloanele, documentul pe paginile lui, intrările și ce mai e de completat](shots/documents.webp)

La mijloc e documentul așa cum se tipărește, pagină cu pagină, cu cuprinsul, tabelele și cifrele lui. Un câmp pe care proiectul nu-l are încă e marcat acolo unde stă. Browserul proiectului arată documentele sub **Documente**; un clic deschide dialogul pe unul dintre ele.

## Intrările

Tabul **Intrări** are textele pe care doar proiectantul le poate scrie, grupate pe capitolele documentului. În șabloanele în română fiecare începe cu o indicație marcată **[De completat]**, care spune ce se scrie acolo. Scrii peste ea: previzualizarea urmează la o clipă după ce te oprești din scris. **Revino la implicit** aduce înapoi textul șablonului.

![Intrări: structura scrisă peste indicația șablonului](shots/documents-inputs.webp)

## De completat

**De completat** arată ce îi lipsește încă documentului: un câmp al proiectului lăsat gol, o valoare pe care modelul nu o are. Un clic pe unul arată unde e în document și duce la câmpul care îl completează (un câmp al proiectului deschide tabul **Datele proiectului**). Lista se golește pe măsură ce o completezi.

![De completat: câmpurile care îi lipsesc încă documentului](shots/documents-missing.webp)

## Actualizare, redenumire, copiere

- **Actualizează din șablon** ia din nou textul și așezarea șablonului, pentru un document făcut înainte ca șablonul să se schimbe. Intrările tale se păstrează.
- **Redenumește**, **Duplică** și **Șterge** lucrează pe documentul afișat. O ștergere e un singur pas de anulare.

## Editarea unui document

**Editează**, jos în dialogul Documente, face din pagina din mijloc un editor al textului propriu al documentului; **Previzualizare** îl arată din nou pe paginile lui. Ce schimbi ține doar de acest document: șablonul rămâne cum era (**Salvează ca șablon…**, mai jos, îți păstrează schimbările ca șablon).

![Editarea unui document: câmpurile ca etichete cu valorile lor, un tabel al proiectului selectat și setările lui în dreapta](shots/documents-editor.webp)

- **Bara de sus** are stilurile textului: **Paragraf**, **Titlu 1**–**3**, îngroșat, cursiv, liste cu buline și numerotate, anulare și refacere. Merg și tastele obișnuite (Ctrl+B, Ctrl+I, Ctrl+Z în editor).
- **Câmpurile** apar ca etichete cu numele și valoarea lor de acum, de exemplu beneficiarul, „Popescu Ion și Maria”. Un câmp pe care proiectul nu-l completează încă e portocaliu.
- **Blocurile proiectului** — un tabel, o imagine, cuprinsul — sunt desenate cu conținutul lor real. Un clic pe unul îl selectează: setările lui apar în dreapta, în locul taburilor.

Salvarea e automată: la o clipă după ce te oprești din scris și când treci înapoi la **Previzualizare**, fiecare schimbare fiind un pas de anulare al proiectului.

### Inserează

**Inserează** pune la cursor:

- **Câmp…**: un câmp al proiectului, căutat după nume; lista arată valoarea fiecăruia. Într-o repetare sunt oferite și câmpurile elementelor ei (numele unui nivel, aria unei încăperi).

  ![Inserează → Câmp…: „client” căutat, câmpurile cu valorile lor](shots/documents-insert-field.webp)

- **Tabel din proiect…**: un extras sau o listă a proiectului (extrasul de tâmplărie, încăperile, materialele…). În setările lui: **Sursa**, **Titlu**, coloanele afișate și rândul de total.
- **Imaginea unei vederi…**: un plan, o secțiune, o fațadă sau o vedere 3D. În setările ei: **Scara 1:** sau **Lățimea (mm)** (niciodată mai lată decât textul) și **Legenda**.
- **Câmpuri cheie–valoare**: un tabel pe două coloane, cu etichete și valori; o valoare poate conține câmpuri, ca `{{project.name}}`.
- **Repetă pentru…**: textul dinăuntru se scrie o dată pentru fiecare element al unei colecții: nivelurile, încăperile, acoperișurile, materialele, planșele, documentele… O bandă deasupra spune **Pentru fiecare** și câte elemente sunt; săgețile ei **‹ ›** schimbă elementul pe care editorul îl arată cât scrii. În setări, **Doar elementele unde** le păstrează doar pe unele, iar **Text când nu există niciunul** adaugă ce se scrie atunci când nu e niciunul.
- **Condiție…**: textul dinăuntru se scrie doar când un câmp are o valoare (sau, bifat, doar când e gol). **Altfel: text când nu se îndeplinește** adaugă o a doua parte, scrisă în locul lui.
- **Text de completat…**: o intrare nouă, cu eticheta, grupul și textul ei implicit; textul îl scrii în tabul **Intrări**, în previzualizare.
- **Cuprins** și **Salt de pagină**.

![O repetare pe niveluri, o condiție în ea și încăperile nivelului arătat, schimbat cu ‹ ›](shots/documents-repeat.webp)

În setările unui bloc, **Elimină** îl scoate; la o repetare sau o condiție, **Păstrează doar conținutul** scoate banda și păstrează ce e în ea.

## Șabloanele tale

**Salvează ca șablon…** păstrează textul, așezarea, intrările și stilul documentului ca șablon. Îi dai un titlu și o descriere și alegi unde îl păstrezi:

- **Acest proiect**: merge cu fișierul proiectului, la oricine îl deschide.
- **Biblioteca acestui calculator**: îl oferă orice proiect de pe acest calculator.
- **Amândouă**.

**Salvează ca** poate înlocui un șablon salvat înainte; documentele făcute deja din el își păstrează textul până la **Actualizează din șablon**. Șablonul nou apare imediat sub **Nou din șablon**.

![Salvează ca șablon: titlul, descrierea și unde se păstrează](shots/documents-save-template.webp)

**Gestionare → Șabloane de documente…** arată șabloanele în trei grupuri: **Încorporate** (ale lui Lapin), **Acest proiect** și **Acest calculator**. Pentru cel ales:

- **Redenumește** și **Șterge** (ștergerea unui șablon al proiectului e un pas de anulare; documentele făcute din el își păstrează textul).
- **Duplică în proiect** sau **Duplică în bibliotecă**: un șablon încorporat se schimbă doar printr-o copie.
- **Copiază în bibliotecă** sau **Copiază în proiect**, ca să-l muți dintr-un loc în altul.
- **Exportă…** scrie șablonul ca fișier `.json`; **Importă…** citește unul în acest proiect sau în biblioteca acestui calculator. Așa își dau colegii unei firme șabloanele.

![Șabloane de documente: cele încorporate, ale proiectului și ale calculatorului, cel ales cu stilul lui](shots/document-templates.webp)

## Stilul firmei

Un document, ca și un șablon al proiectului sau al bibliotecii, are stilul lui: tabul **Stil** din dialogul Documente pentru documentul afișat, partea **Stil** din Gestionare → Șabloane de documente… pentru un șablon. Șabloanele încorporate păstrează aspectul lui Lapin: duplică unul ca să-i dai stilul tău.

- **Fontul (DOCX, ODT)**: Arial (al lui Lapin), Calibri, Times New Roman, Georgia, Verdana sau Liberation Sans.
- **Fontul PDF**: Helvetica sau Times, fonturile proprii ale PDF-ului; implicit cel mai apropiat de fontul de mai sus.
- **Mărimea textului (pt)**: mărimea textului curent, între 6 și 24 pt.
- **Culoarea titlurilor**.
- **Logo-ul în antet**: **Alege logo-ul…** ia un PNG sau JPEG (sub 1 MB); îi stabilești **Lățimea (mm)** și dacă stă **În stânga** sau **În dreapta**. Se scrie la fel în DOCX, ODT și PDF.

O mostră sub câmpuri arată partea de sus a paginii așa cum o vor arăta fișierele. **Aspectul propriu al lui Lapin** scoate stilul. Fiecare schimbare e un pas de anulare.

![Tabul Stil: culoarea titlurilor și un logo în antet, cu mostra](shots/documents-style.webp)

## Exportul

- **Exportă DOCX** și **Exportă ODT** scriu un fișier pe care îl deschizi și îl editezi în Word sau LibreOffice, cu titlurile ca stilurile programului, cuprinsul și numerotarea paginilor ca câmpuri, și imaginile proiectului.
- **Exportă PDF** scrie documentul așezat în pagină de Lapin, gata de tipărit sau de trimis.

Un câmp încă gol rămâne gol în fișiere, așa că verifică **De completat** înainte de export.

## Emiterea documentelor

**Gestionare → Emite…** ia documentele împreună cu planșele și modelul (vezi [Folderul proiectului și emiterile](cde.md)). Sub **Documente, fiecare ca PDF:** fiecare document e bifat de la început; bifează **Și ca DOCX** pentru o copie editabilă. Fiecare fișier e denumit după convenția ISO 19650 (`RP` pentru un memoriu, `SP` pentru un caiet de sarcini), are hash-ul calculat și apare în borderoul de transmitere, iar documentul primește revizia și starea emiterii.

![Emite: documentele împreună cu planșele și modelul](shots/issue-documents.webp)

## Șabloanele

Lapin vine cu trei șabloane, deocamdată în română:

- **Memoriu tehnic de arhitectură** (după conținutul-cadru al Legii 50/1991): datele generale din Datele proiectului; regimul de înălțime (de exemplu `P+1E`) și înălțimea maximă; suprafețele — construită, desfășurată, utilă și a subsolului — în total și pe niveluri; încăperile fiecărui nivel, cu ariile lor; pereții pe tipuri, cu straturile lor; planșeele și acoperișurile; extrasul de tâmplărie; finisajele încăperilor; intrări pentru situația existentă, funcțiune, structură, fațade, instalații, securitatea la incendiu, accesibilitate, eficiența energetică și organizarea de șantier.
- **Caiet de sarcini: tâmplărie și finisaje**: materialele proiectului pe categorii, extrasul de tâmplărie cu numărul bucăților, finisajele încăpere cu încăpere și intrări pentru generalități, cerințe, execuție și recepție, cu texte implicite de adaptat.
- **Borderou de piese scrise și desenate**: documentele și planșele proiectului, cu numărul, titlul, scara și revizia fiecăreia.

Pornește de la unul dintre ele, schimbă-l în editor, apoi **Salvează ca șablon…** ca să ai propriul șablon.

## Pentru asistenții AI

Un asistent conectat prin bridge-ul MCP al lui Lapin poate face un document, îi poate scrie intrările și îl poate exporta cu `lapin_document_export`: vezi [Lapin și agenții AI](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (în engleză).
