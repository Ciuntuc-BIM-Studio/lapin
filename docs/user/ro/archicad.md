---
title: Archicad
order: 128
summary: Iei în Lapin modelul unui Archicad deschis, prin add-on-ul gratuit Tapir, ca elemente native acolo unde Lapin refă corpurile proprii ale Archicad și ca referințe pentru restul, și trimiți IFC-ul din Archicad pe serverul Atrium al proiectului.
---

**Fișier → Colaborare → Archicad: Primește…** aduce în proiect elementele unui Archicad care rulează pe același calculator, în direct, fără un fișier IFC între ele. **Ia ultima versiune din Archicad** aduce apoi doar ce s-a schimbat în Archicad între timp, iar bara de stare spune când s-a schimbat. **Archicad: Trimite în Atrium…** pune Archicad să salveze un IFC și îl urcă pe serverul proiectului ca model de referință. Ambele sunt în aplicația desktop, nu în Lapin pentru browser, și ambele cer ca Archicad să fie deschis.

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
- **Georeferențiază proiectul după amplasamentul din Archicad** (bifat implicit): vezi [Georeferențierea](#georeferentierea).
- **Primește ca o copie**, afișat doar când modelul din Archicad a fost trimis chiar din acest proiect: elementele vin cu id-uri proprii, lângă elementele de aici, care nu sunt atinse.

**Primește** rulează ca un singur pas de anulare, cu progres și un buton Anulează. Raportul spune câte elemente au venit native și câte ca referințe, ce s-a adăugat, actualizat, rămas neschimbat și șters, ce etaje s-au făcut și de ce au rămas elemente referințe (de exemplu un perete pe care Lapin nu l-a putut reface în toleranță).

Ca să păstrezi modelul primit pe server: primește-l, apoi **Fișier → Colaborare → Salvează pe server**.

### Datele proiectului

Primirea citește și **Project Info** din Archicad (File → Info → Project Info…) și o pune în datele proiectului din Lapin (**Gestionare → Datele proiectului…**), în același pas de anulare:

- Un câmp **gol** al proiectului se completează: numele proiectului din *Project Name*, numărul din *Project Number* (altfel *Project Code*, altfel *Project ID*), faza din *Project Status* când e una din fazele Lapin (DTAC, PT…; altfel devine numele fazei), beneficiarul și adresa lui, adresa amplasamentului (adresa completă, altfel rândurile, localitatea, județul, codul poștal și țara unite), proiectantul (*Company* din Archicad) cu adresa, numărul de înregistrare, telefonul și e-mailul, șeful de proiect (persoana de contact a firmei) și cine a desenat (*CAD Technician*).
- Un câmp pe care proiectul îl are **deja** cu altă valoare **nu se suprascrie niciodată**. Raportul îl arată cu ambele valori (În Lapin, În Archicad); bifează-le pe cele de luat și apasă **Ia valorile bifate** (încă un pas de anulare).
- Ce spune Archicad și nu ia niciun câmp (descrierea proiectului, cuvintele cheie, numele amplasamentului, autotextele proprii…) se păstrează separat, doar pentru citire: Datele proiectului le arată sub **Din Archicad**. Se înlocuiesc la fiecare primire.

Înainte de primire, dialogul spune câte câmpuri ar completa Archicad și câte diferă. Dacă Archicad are un dialog deschis (de exemplu chiar Project Info), Archicad nu răspunde la nimic până nu e închis: Lapin spune că Archicad e ocupat și ce dialog e deschis; închide-l și încearcă din nou.

Lapin și Atrium Server potrivesc câmpurile din Archicad după același tabel, așa că o valoare ajunge în același câmp de ambele părți.

### Georeferențierea

Când proiectul nu e încă georeferențiat (**Gestionare → Amplasament**), o primire ia amplasamentul din Archicad (în Archicad: Options → Project Preferences → Location Settings):

- **Punctul topografic** (survey point) din Archicad, când e pus (coordonate de hartă sau un sistem de coordonate numit), leagă proiectul de hartă: estul, nordul și cota lui, sistemul de coordonate când Lapin îl cunoaște (Stereo 70, UTM, Lambert-93…; altul păstrează coordonatele fără sistem, iar raportul o spune) și unghiul axei X a proiectului față de hartă, ca unghi față de nordul adevărat. Un punct topografic rotit față de originea proiectului se ia cu rotația lui.
- Altfel, **latitudinea și longitudinea** din Archicad așază punctul de bază al proiectului.
- Valoarea **implicită, neatinsă**, din Archicad nu se ia niciodată: un proiect Archicad nou stă la Budapesta (longitudine 19,05477, latitudine 47,5597), cu punctul topografic în origine și fără sistem de coordonate, ceea ce nu spune nimic despre modelul tău.

Un proiect deja georeferențiat își păstrează georeferențierea. Dialogul spune înainte de primire ce ar face cu amplasamentul din Archicad; debifează **Georeferențiază proiectul după amplasamentul din Archicad** ca să lași amplasamentul proiectului neatins. **Origine: Punctul de teren din Archicad** mută modelul în punctul topografic doar când acesta nu e rotit; cu unul rotit, primește din originea proiectului (rotația o poartă atunci amplasamentul proiectului).

### Ce devine nativ

- **Pereți**: pereți drepți, simpli și compoziți. Un perete compozit devine un tip de perete cu straturile (skin-urile) lui. Tipul păstrează GUID-ul compozitului din Archicad: o primire ulterioară îl găsește după el, iar când compozitul s-a schimbat în Archicad (straturile, materialele lor, numele), tipul se actualizează pe loc, iar pereții lui se refac pe el și se măsoară din nou, în loc să se facă un al doilea tip. Un material de construcție (building material) redenumit în Archicad își redenumește la fel materialul din Lapin.
- **Planșee**: cu golurile lor; un planșeu compozit devine un tip de planșeu.
- **Stâlpi și grinzi** cu secțiuni dreptunghiulare sau rotunde (o grindă trece prin stâlpi, cum o face Archicad). Unul cu profil sau din mai multe segmente e citit din corpul lui și e nativ când Lapin are secțiunea lui.
- **Uși și ferestre**: golurile lor devin native, în pereții gazdă. Ușa sau fereastra în sine rămâne referință, dacă configuratorul Lapin nu-i reface corpul.
- **Zonele** devin camere.
- Restul vine ca referințe, cu suprafețele Archicad ca materiale.

Elementele păstrează **id-urile IFC proprii ale Archicad** drept GlobalId, deci sunt aceleași id-uri ca în exportul IFC al Archicad. O nouă primire lasă cum era ce nu s-a schimbat, actualizează pe loc ce s-a schimbat (își păstrează GlobalId-ul și e măsurat din nou) și șterge ce a dispărut din Archicad. Fiecare element își păstrează guid-ul, layer-ul și ID-ul din Archicad în setul de proprietăți **Archicad**, proprietățile proprii din Archicad în seturi numite ca grupurile lor, seturile de proprietăți IFC pe care Archicad i le atribuie sub numele lor IFC, și clasificarea.

## Ia ultima versiune din Archicad

După o primire, Lapin păstrează cu proiectul un semn al stării din Archicad (în istoricul lui, niciodată în model sau într-un commit). **Fișier → Colaborare → Ia ultima versiune din Archicad** întreabă Archicad ce s-a schimbat de la acel semn și citește doar elementele create sau modificate între timp (cu ușile și ferestrele unui perete și cu peretele unei uși sau ferestre), șterge pe cele șterse în Archicad și le actualizează pe celelalte pe loc, cu GlobalId-urile păstrate, măsurate de aceeași poartă de fidelitate ca la primire. E un singur pas de anulare. Elementele care nu s-au schimbat nu sunt atinse. Mutarea unui perete în Archicad schimbă de obicei și pereții îmbinați cu el și ferestrele din el; Lapin le citește din nou și le lasă cum sunt când nimic din ele nu s-a schimbat.

Ia ultima versiune citește tot modelul și compară fiecare element atunci când nu poate întreba ce s-a schimbat: Archicad a fost repornit sau proiectul închis și redeschis (Archicad își uită atunci semnele), modelul a fost primit înainte de această versiune sau proiectul s-a schimbat aici între timp (o anulare a unei primiri sau a unei actualizări). Dacă proiectul deschis în Archicad nu e cel primit (niciunul dintre elementele lui nu este), refuză și spune de ce: deschide acel proiect în Archicad sau primește-l pe acesta ca o copie.

Cât timp proiectul e legat de un model Archicad și fereastra se vede, bara de stare întreabă Archicad la fiecare 10 secunde dacă modelul s-a schimbat, fără să citească vreun element, și arată **Modelul Archicad s-a schimbat (N)** când s-a schimbat (N elemente create, modificate sau șterse). Un clic pe el rulează Ia ultima versiune din Archicad. Nu apare nimic cât timp Archicad e închis sau ocupat cu un dialog, nici cât rulează o primire sau o actualizare.

## Trimitere în Atrium

**Archicad: Trimite în Atrium…** cere ca proiectul să fie conectat la un server și să fii autentificat pe el (**Fișier → Colaborare…**). Alegi Archicad-ul și:

- **traducătorul IFC**, unul dintre cei din Archicad (cel făcut pentru previzualizări e oferit primul);
- **titlul** modelului (implicit numele proiectului din Archicad);
- **folderul** de pe server, implicit cel al modelelor de referință;
- **disciplina**, dacă vrei una;
- **numele fișierului**.

Lapin pune Archicad să salveze IFC-ul cu traducătorul ales, pune fișierul în depozitul proiectului de pe server, face acolo documentul lui de referință (un model, primit) și actualizează registrul documentelor de referință ale proiectului. Cu **Propune și datele proiectului** (bifat implicit), Project Info din Archicad merge apoi pe server ca **propunere** pentru datele proiectului: un proprietar o aplică, în parte sau toată, în **Publicare → Datele proiectului** ([Serverul CDE](cde-server.md#datele-proiectului)); până atunci nimic nu se schimbă pe server. Când Archicad nu spune nimic ce serverul nu are deja, nu se propune nimic. Dialogul spune fiecare pas și, la sfârșit, unde se află modelul. Un Archicad care nu poate salva (o copie demo sau educațională poate refuza; demo-ul Archicad 29 a salvat IFC când a fost încercat) dă mesajul lui, iar dialogul oferă celălalt drum: primești modelul, apoi **Fișier → Colaborare → Salvează pe server**.

## Limite

- Acoperișurile, scările, balustradele, pereții cortină, morph-urile și obiectele rămân referințe în această versiune, la fel pereții curbi, înclinați și trapezoidali.
- Elementele 2D (cote, texte, hașuri) nu se primesc.
- Modulele legate prin hotlink sunt aplatizate: elementele lor vin ca ale proiectului.
- Archicad trebuie să fie deschis, pe același calculator, cu Tapir instalat.
- Există doar în aplicația desktop.
