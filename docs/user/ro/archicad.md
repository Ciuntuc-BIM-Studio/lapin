---
title: Archicad
order: 128
summary: Iei în Lapin modelul unui Archicad deschis, prin add-on-ul gratuit Tapir, ca elemente native acolo unde Lapin refă corpurile proprii ale Archicad și ca referințe pentru restul, și trimiți IFC-ul din Archicad pe serverul Atrium al proiectului.
---

**Fișier → Colaborare → Archicad: Primește…** aduce în proiect elementele unui Archicad care rulează pe același calculator, în direct, fără un fișier IFC între ele. **Archicad: Trimite în Atrium…** pune Archicad să salveze un IFC și îl urcă pe serverul proiectului ca model de referință. Ambele sunt în aplicația desktop, nu în Lapin pentru browser, și ambele cer ca Archicad să fie deschis.

## Pregătirea

Lapin vorbește cu Archicad prin **Tapir**, un add-on gratuit (licență MIT) care adaugă comenzi la API-ul JSON propriu al Archicad.

1. Descarcă Tapir de pe [pagina lui de versiuni](https://github.com/ENZYME-APD/tapir-archicad-automation/releases): versiunea 2.0 sau mai nouă, pentru Archicad-ul tău (25 până la 30), pe Mac sau Windows. Instalează-l cum spune pagina lui.
2. Deschide Archicad cu proiectul pe care vrei să-l iei.
3. În Lapin, alege **Fișier → Colaborare → Archicad: Primește…**. Lapin caută Archicad-uri pe porturile 19723–19744 (un al doilea Archicad ia portul următor) și le listează pe fiecare cu proiectul, versiunea și portul.

Un Archicad fără Tapir, sau cu o versiune prea veche, apare cu ce e în neregulă și nu se poate primi din el până nu se repară. Dacă nu se găsește niciunul, dialogul spune asta și trimite la această pagină.

## Primirea

Alege Archicad-ul, iar dialogul îi citește modelul și spune ce ar face o primire: elementele pe tipuri, etajele, câte s-ar adăuga, actualiza sau șterge și ce rămâne pe dinafară. Apoi alegi:

- **Etaje**: **Fă etajele care lipsesc** (implicit) face câte un etaj Lapin pentru fiecare nivel (story) din Archicad pe care proiectul nu-l are și pune fiecare element pe al lui; **Doar etajele proiectului** pune totul pe etajele pe care proiectul le are deja.
- **Origine**: **Originea proiectului din Archicad** (implicit) sau **Punctul de teren din Archicad** (survey point), de unde încep coordonatele.
- **Recunoaște elementele** (pornit implicit): pereții, planșeele, stâlpii, grinzile, golurile și zonele devin elemente native Lapin acolo unde corespund corpului 3D al Archicad în limita a 1 mm sau 0,5 %; restul vin ca referințe. Oprește-l ca să primești totul ca referințe.
- **Primește ca o copie**, afișat doar când modelul din Archicad a fost trimis chiar din acest proiect: elementele vin cu id-uri proprii, lângă elementele de aici, care nu sunt atinse.

**Primește** rulează ca un singur pas de anulare, cu progres și un buton Anulează. Raportul spune câte elemente au venit native și câte ca referințe, ce s-a adăugat, actualizat, rămas neschimbat și șters, ce etaje s-au făcut și de ce au rămas elemente referințe (de exemplu un perete pe care Lapin nu l-a putut reface în toleranță).

Ca să păstrezi modelul primit pe server: primește-l, apoi **Fișier → Colaborare → Salvează pe server**.

### Ce devine nativ

- **Pereți**: pereți drepți, simpli și compoziți. Un perete compozit devine un tip de perete cu straturile (skin-urile) lui.
- **Planșee**: cu golurile lor; un planșeu compozit devine un tip de planșeu.
- **Stâlpi și grinzi** cu secțiuni dreptunghiulare sau rotunde (o grindă trece prin stâlpi, cum o face Archicad). Unul cu profil sau din mai multe segmente e citit din corpul lui și e nativ când Lapin are secțiunea lui.
- **Uși și ferestre**: golurile lor devin native, în pereții gazdă. Ușa sau fereastra în sine rămâne referință, dacă configuratorul Lapin nu-i reface corpul.
- **Zonele** devin camere.
- Restul vine ca referințe, cu suprafețele Archicad ca materiale.

Elementele păstrează **id-urile IFC proprii ale Archicad** drept GlobalId, deci sunt aceleași id-uri ca în exportul IFC al Archicad. O nouă primire lasă cum era ce nu s-a schimbat, actualizează pe loc ce s-a schimbat (își păstrează GlobalId-ul și e măsurat din nou) și șterge ce a dispărut din Archicad. Fiecare element își păstrează guid-ul, layer-ul și ID-ul din Archicad în setul de proprietăți **Archicad**, proprietățile proprii din Archicad în seturi numite ca grupurile lor, seturile de proprietăți IFC pe care Archicad i le atribuie sub numele lor IFC, și clasificarea.

## Trimitere în Atrium

**Archicad: Trimite în Atrium…** cere ca proiectul să fie conectat la un server și să fii autentificat pe el (**Fișier → Colaborare…**). Alegi Archicad-ul și:

- **traducătorul IFC**, unul dintre cei din Archicad (cel făcut pentru previzualizări e oferit primul);
- **titlul** modelului (implicit numele proiectului din Archicad);
- **folderul** de pe server, implicit cel al modelelor de referință;
- **disciplina**, dacă vrei una;
- **numele fișierului**.

Lapin pune Archicad să salveze IFC-ul cu traducătorul ales, pune fișierul în depozitul proiectului de pe server, face acolo documentul lui de referință (un model, primit) și actualizează registrul documentelor de referință ale proiectului. Dialogul spune fiecare pas și, la sfârșit, unde se află modelul. Un Archicad care nu poate salva (o copie demo sau educațională poate refuza; demo-ul Archicad 29 a salvat IFC când a fost încercat) dă mesajul lui, iar dialogul oferă celălalt drum: primești modelul, apoi **Fișier → Colaborare → Salvează pe server**.

## Limite

- Acoperișurile, scările, balustradele, pereții cortină, morph-urile și obiectele rămân referințe în această versiune, la fel pereții curbi, înclinați și trapezoidali.
- Elementele 2D (cote, texte, hașuri) nu se primesc.
- Modulele legate prin hotlink sunt aplatizate: elementele lor vin ca ale proiectului.
- Archicad trebuie să fie deschis, pe același calculator, cu Tapir instalat.
- Există doar în aplicația desktop.
