---
title: Wspólna praca
order: 125
summary: Udostępnij otwarty projekt i pracuj w nim na żywo z innymi, z Lapin lub z przeglądarki.
---

<!-- video sharing -->

Kilka osób może pracować w tym samym projekcie jednocześnie, tak jak gracze dołączają do serwera gry. Jedna z was udostępnia projekt z Lapin na swoim komputerze: jest **gospodarzem**. Pozostali dołączają jako **goście**, z Lapin na własnym komputerze lub z przeglądarki, przez odnośnik wysłany przez gospodarza.

Projekt pozostaje na komputerze gospodarza, a Lapin gospodarza rozstrzyga o każdej zmianie. Goście widzą model na bieżąco, gdy się zmienia, rysują tymi samymi narzędziami i widzą, gdzie są inni.

## Udostępnij projekt

**Plik → Udostępnij sesję…**, podaj swoje imię, wybierz, jak dołączają goście, a następnie naciśnij **Udostępnij**:

- **edytorzy** modelują tak jak ty;
- **obserwatorzy** oglądają i poruszają się po modelu, ale niczego nie zmieniają.

Lapin pokazuje **odnośnik** i **kod**. Wyślij odnośnik temu, kto dołącza z przeglądarki, a kod temu, kto dołącza z Lapin. Każdy, kto je ma, może dołączyć, dopóki nie naciśniesz **Zakończ udostępnianie**. Za każdym udostępnieniem odnośnik i kod są nowe.

![Udostępnianie: odnośnik, kod i uczestnicy sesji](shots/share-dialog.webp)

W tym samym oknie dialogowym możesz uczynić gościa edytorem lub obserwatorem albo **poprosić go o opuszczenie sesji**. Zamknij okno, a sesja trwa dalej. Ikona osób obok uczestników na górnym pasku otwiera je ponownie.

Gdy projekt jest udostępniony i ma plik, Lapin zapisuje go co 5 minut. Zapisuje tylko gospodarz; goście mogą eksportować dla siebie pliki IFC i 3D.

## Dołącz do sesji

- **Z przeglądarki**: otwórz odnośnik, podaj swoje imię i naciśnij **Dołącz**. Nic nie trzeba instalować.
- **Z Lapin**: na ekranie startowym lub w menu **Plik** wybierz **Dołącz do sesji…**, wklej odnośnik lub kod i podaj swoje imię.

![Dołączanie z Lapin](shots/join-dialog.webp)

Pasek na dole informuje, w czyjej sesji jesteś. **Wyjdź** przenosi cię z powrotem do twojego własnego Lapin. Jeśli gospodarz zakończy udostępnianie lub zamknie Lapin, Lapin cię o tym poinformuje, a ty wracasz przyciskiem **Wróć do Lapina**.

## Kto jest gdzie

Uczestnicy są na górnym pasku, każdy w swoim kolorze. Kliknij jednego z nich, aby przejść do widoku, na który patrzy.

![Uczestnicy](shots/participants.webp)

Na rzucie widzisz kursory innych z ich imionami oraz to, co zaznaczyli, obrysowane ich kolorem.

![Kursor i zaznaczenie gościa na rzucie gospodarza](shots/sharing-plan.webp)

## Cofanie jest twoje

<kbd>Ctrl Z</kbd> cofa twoją ostatnią zmianę, a nie czyjąś. Jeśli ktoś zmienił ją po tobie, Lapin zostawia ją bez zmian i mówi ci, kto to zrobił. Wtedy twoje cofanie nie sięga dalej wstecz niż ta zmiana.

## Bezpieczne z założenia

- Wszystko między gospodarzem a gośćmi przechodzi przez przekaźnik Lapin, **szyfrowane end-to-end** kluczem z zaproszenia. Przekaźnik przekazuje dane dalej i nie może ich odczytać.
- Gość nie może otwierać, zapisywać ani czytać plików na komputerze gospodarza ani otworzyć tam innego projektu.
- Lapin niczego nie otwiera na sieć, dopóki nie udostępnisz projektu, i przestaje, gdy zakończysz udostępnianie.
