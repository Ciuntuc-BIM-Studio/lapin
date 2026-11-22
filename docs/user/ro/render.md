---
title: Randare
order: 72
summary: Randezi vederea 3D ca pe o fotografie, cu lumina urmărită cum se reflectă, și o salvezi ca imagine de orice mărime.
---

# Randare

Vederea 3D se poate **randa**: lumina e urmărită cum călătorește de fapt, de la soare, de la cer și de la fiecare lampă, reflectată de pereți, pardoseli și tavane și trecând prin sticlă, până când imaginea arată ca o fotografie. Lapin randează exact ce arată vederea (aceleași elemente, materiale și texturi, aceeași tăietură a unei secțiuni sau a cutiei de secțiune, aceleași niveluri și elemente ascunse), cu cerul și soarele vederii la ziua și ora proiectului, și cu expunerea și balansul ei de alb.

Randarea cere vederea **Mediu** sau **Realist** (Vedere → **Calitate afișare**, vezi [Realist, cu soarele](views.md#realist-cu-soarele)).

## Randarea vederii

Apasă **Randează** în colțul din dreapta jos al vederii 3D (sau Vedere → **Randează**). Imaginea pornește cu purici și se limpezește eșantion cu eșantion cât timp camera stă; rotește, apropie sau mută și o ia de la capăt, iar cât se mișcă camera se vede vederea live. Eticheta de jos spune cât a ajuns (*128 eșantioane · 0:42*); se oprește când imaginea e gata (implicit după 256 de eșantioane). **×** aduce înapoi vederea live.

![O casă de cărămidă la amiază, randată: lumina cerului, umbrele moi ale soarelui, relieful cărămizii](shots/render-exterior.webp)

Ce are o imagine randată și nu are vederea live:

- **Lumina care se reflectă.** O cameră luminată de lămpile ei are tavanul luminat de lumina întoarsă de pardoseală și de pereți; un colț e mai întunecat pentru că ajunge mai puțină lumină în el, nu printr-o umbrire pusă în locul ei.
- **Toate luminile.** Vederea live desenează doar luminile cele mai apropiate, câteva cu umbre; randarea le ia pe toate, fiecare cu umbra ei moale (un bec are câțiva centimetri, un panou mărimea lui), iar distribuția IES a producătorului dă forma luminii.
- **Umbrele moi ale soarelui și lumina cerului** venită din tot cerul deodată.
- **Sticla** care frânge și reflectă lumina și materialele atât de aspre sau lucioase cât spun hărțile lor (vezi [Seturi de texturi](materials.md#seturi-de-texturi)).

## Setările randării

**Setări…** din etichetă (sau Vedere → **Setări randare**) deschide dialogul Randează:

![Dialogul Randează](shots/render-dialog.webp)

- **Mărime**: a ferestrei, **1080p** (1920 lățime), **4K** (3840 lățime) sau una proprie, până la 8192 de pixeli pe latură; 1080p și 4K păstrează proporția vederii. Imaginea salvată e randată la mărimea aceea, oricare ar fi a ferestrei.
- **Gata după**: atâtea **eșantioane** (64 până la 4096: mai multe dau o imagine mai curată, mai încet) sau după atâtea secunde.
- **Elimină zgomotul** curăță ultimii purici. În vedere o face denoiserul Lapin, ghidat de culorile, normalele și adâncimea imaginii, ca muchiile, rosturile și texturile să rămână clare. O imagine salvată în aplicația desktop trece prin Intel Open Image Denoise, care vine cu Lapin; în browser, prin al Lapin.
- **Adâncimea câmpului**: ce e aproape și ce e departe se înmoaie, ca printr-un obiectiv. **Alege în vedere** și clic pe ce trebuie să fie clar; **diafragma** spune cât de moale devine restul (f/1.4 mult, f/16 abia).
- **Expunere** și **Balansul de alb**: ale vederii (Vedere → **Calitate afișare**) sau ale imaginii.

**Salvează imaginea…** randează imaginea la mărimea ei și o salvează ca PNG. Cât se randează, vederea o arată venind, iar dialogul spune cât a ajuns (**Oprește** renunță).

Setările se păstrează pe acest calculator; nimic din ele nu intră în proiect.

## Noaptea

Pune o oră după apus în Vedere → **Soare și umbre**: cerul se întunecă, expunerea se deschide și corpurile de iluminat luminează camerele (vezi [Lumini și noaptea](views.md#lumini-și-noaptea)). O randare arată atunci camerele cum le luminează lămpile, lumina reflectată de pereți și de tavan; la amurg fereastra mai arată ultima lumină a cerului.

![O sufragerie la amurg, luminată de două spoturi încastrate, o lustră și un spot](shots/render-interior.webp)

## Cât de repede

O casă mică se randează la 1080p și 256 de eșantioane în aproximativ un minut pe un calculator recent; o cameră noaptea, unde lumina se reflectă mai mult, durează mai mult. Imaginea din vedere e randată la pixelii ecranului (pe un ecran de densitate mare, jumătate din pixelii lui fizici), ca să se limpezească mai repede; o imagine salvată e randată la mărimea ei întreagă.

Cerul e al vederii: locul, ziua și ora proiectului. Un cer fotografiat (HDRI) vine într-o versiune următoare, ca și randarea pe plăci a imaginilor mai mari decât permite placa video și o coadă de vederi randate una după alta.
