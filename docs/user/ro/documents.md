---
title: Documente
order: 105
summary: Piesele scrise ale proiectului — memoriul tehnic, caietul de sarcini, borderoul — făcute din șabloane, completate din model și exportate în DOCX, ODT și PDF fără niciun alt program.
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

Un editor vizual pentru șabloanele tale vine mai târziu.
