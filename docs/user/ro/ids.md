---
title: Verificări IDS
order: 127
summary: Verifici modelul după un fișier IDS (buildingSMART IDS 1.0), cerințele de informații pe care le dă un client sau un BIM manager; vezi ce nu trece și unde, faci observații din erori, exporți un raport și pui verificarea să ruleze înainte ca o emitere să plece pe serverul CDE.
---

Un **IDS** (Information Delivery Specification) e un fișier mic, `.ids`, care spune ce trebuie să conțină un model: ce clase și tipuri, ce atribute, seturi de proprietăți și valori, clasificări, materiale și ce face parte din ce. Clienții și BIM managerii le scriu cu orice editor IDS; Lapin citește IDS 1.0, standardul buildingSMART, și verifică modelul după el.

Lapin verifică modelul **așa cum îl scrie exportul IFC**: ce ar găsi alt program în IFC-ul pe care îl predai. Seturile de proprietăți ale lui Lapin (`Lapin_Intent`, `Lapin_Parameters`, seturile tale), tipurile, seturile de straturi și materialele lor, clasificările atribuite și nivelurile pot fi cerute toate.

## Adaugi un fișier IDS

Deschide **Gestionare → IDS…** și apasă **Adaugă fișier IDS…**. Fișierul e păstrat în proiect, așa că merge cu fișierul `.lapin` și cu salvările pe un server CDE. Adăugat din nou (o versiune nouă cu același nume) îl înlocuiește; butonul coș îl scoate. Fiecare pas se poate anula.

Un fișier care nu e IDS 1.0 e refuzat, cu fiecare problemă și rândul ei: o versiune IFC necunoscută, o proprietate cu valoare dar fără tip de date, o cerință opțională fără nimic de verificat, o valoare care nu e număr unde se cere un număr, un tipar care nu se poate citi.

## Rulezi verificarea

Apasă **Rulează verificarea**. Fiecare specificație a fiecărui fișier arată:

- **Trece**, **Nu trece** sau **Sărită** (scrisă pentru altă versiune IFC decât a modelului);
- la câte elemente se aplică și câte trec. O specificație *obligatorie* nu trece când nimic din model nu e ce cere; una *interzisă* nu trece când ceva este.

![IDS: specificațiile cu rezultatul lor, una care nu trece deschisă](shots/ids-results.webp)

Apasă pe o specificație ca s-o deschizi: descrierea, instrucțiunile, cerințele și fiecare eroare, cu elementul, ce are și ce trebuie (de exemplu *no Office_Data.Code — Office_Data.Code*). Apasă pe o eroare ca să selectezi elementul și să-l vezi în 3D; **Selectează elementele care nu trec** le selectează pe toate. La un model mare se arată primele erori; **Mai multe** le aduce pe celelalte.

Verificarea citește valorile cum spune IDS: textele exact, numerele în metri, metri pătrați și metri cubi oricare ar fi unitățile modelului, la o milionime; **true** și **false** pentru da și nu. Nu schimbă nimic în model.

## Observații și raportul

**Fă observații** face câte o observație (o problemă BCF) pentru fiecare specificație care nu trece, cu elementele ei selectate și erorile în descriere, într-un singur pas de anulare. Rulată din nou și cu observațiile făcute din nou, verificarea aduce la zi aceleași observații în loc să adauge altele. La un proiect ținut pe un server CDE, observațiile pleacă pe server cu **Sincronizează problemele**, imediat dacă ești autentificat.

**Raport (XLSX)** scrie un registru Excel: un rezumat, fiecare specificație, fiecare eroare cu GlobalId-ul ei.

## Înainte ca o emitere să plece pe serverul CDE

Când proiectul are fișiere IDS, **Gestionare → Emite… → Pe serverul CDE** le rulează înainte de **Propune spre aprobare** și arată ce nu trece. Cu setarea proiectului pornită (**O specificație care nu trece oprește trimiterea**, în Gestionare → IDS…), trimiterea așteaptă până se repară erorile, sau până bifezi **Propune în ciuda erorilor IDS** și scrii de ce: nota pleacă cu trimiterea, ca s-o citească cel care aprobă. La fel pentru **Fă acum eticheta de emitere**. **Rulează înainte de Propune spre aprobare** oprește verificarea pentru proiect.

În **Fișier → Colaborează… → Publicare**, tabul **IDS** e același panou.

## Fișiere IDS pe serverul CDE

Un server CDE poate ține fișiere IDS proprii pentru un proiect și verifica modelul fiecărei propuneri cu ele, aprobarea socotind ce găsesc: vezi [IDS pe server](cde-server.md#ids-pe-server). **Verifică pe server**, în Gestionare → Emite… → Pe serverul CDE, le rulează pe commit-ul abia salvat înainte să propui. Fișierele păstrate în proiect (acest panou) și cele de pe server sunt separate și se copiază între ele doar când ceri: **Adu fișierele IDS de pe server în acest proiect** (în acest panou, pentru editori și proprietari, când serverul proiectului oferă IDS) listează fișierele serverului, fiecare **Nou**, **La fel** sau **Modificate** (un fișier cu același nume aici, cu alt conținut); bifează-le pe cele pe care le vrei și apasă **Copiază fișierele bifate**. Înlocuirea unuia modificat se confirmă întâi, iar toate vin într-un singur pas de anulare. În sens invers, proprietarii folosesc **Pune fișierele IDS ale acestui proiect pe server** în Publicare → IDS pe server.

## Ce nu verifică IDS 1.0

IDS 1.0 nu citește geometrie, coliziuni sau calcule: pentru ele folosește [Detectarea coliziunilor](clash.md) și [verificările dinaintea unei emiteri](cde.md). Corespondența claselor de ocurențe cu tipurile din IFC2X3 nu se aplică (Lapin scrie IFC4).
