# Deck Battle - GDD MVP

## 1. High Concept

**Deck Battle** to mobilny autobattler 3D w widoku portretowym, tworzony w Unity URP. Gracz kolekcjonuje unikalne jednostki, buduje z nich deck, a nastepnie rozgrywa pojedynki na heksagonalnej arenie. W trakcie meczu zagrywa jednostki z reki za punkty akcji, ustawia je na planszy, a potem obserwuje automatyczna walke.

MVP skupia sie na podstawowej petli: kolekcja jednostek, deck, zagrywanie kart, ustawianie formacji, auto-battle i rozstrzygniecie meczu.

## 2. Platforma I Zalozenia Techniczne

- Platforma: mobile.
- Orientacja ekranu: portret.
- Silnik: Unity.
- Rendering: URP.
- Widok: 3D, kamera pod katem z gory.
- Sterowanie: dotyk.
- Tryb MVP: single-player kontra AI.
- PvP: planowane pozniej, poza zakresem MVP.

Priorytetem sa czytelnosc, stabilna wydajnosc i prosta architektura.

## 3. Glowna Petla Gry

1. Gracz posiada kolekcje odblokowanych jednostek.
2. Gracz buduje deck z unikalnych jednostek.
3. Gracz rozpoczyna mecz przeciwko AI.
4. W kazdej rundzie dobiera jednostki na reke.
5. Gracz zagrywa nowe jednostki za AP i ustawia formacje.
6. AI rownolegle przygotowuje swoja formacje.
7. Gracz nie widzi dokladnego ustawienia jednostek AI podczas fazy przygotowania.
8. Po kliknieciu `Ready` rozpoczyna sie auto-battle i jednostki AI zostaja ujawnione.
9. Ocalale jednostki zadaja obrazenia przeciwnikowi.
10. Jednostki wracaja na zapisane pozycje formacji.
11. Mecz trwa do utraty calego HP przez jedna ze stron.
12. Po zwyciestwie gracz otrzymuje booster.

## 4. Kolekcja I Deck

Kazda karta reprezentuje jedna unikalna jednostke.

Zasady MVP:

- Jedna karta = jedna unikalna jednostka w kolekcji.
- Deck sklada sie z wybranych jednostek z kolekcji.
- W decku nie ma kopii tej samej jednostki.
- Jednostka moze zostac zagrana tylko raz w danym meczu.
- Po zagraniu jednostka zostaje aktywna czescia armii gracza.
- Brak ulepszania jednostek w MVP.
- Duplikaty z boosterow zamieniaja sie na shardy, ale shardy nie maja jeszcze zastosowania.

Parametry MVP:

- Kolekcja startowa: 8-12 jednostek.
- Rozmiar decku: 8-10 jednostek.
- Reka startowa: 3 karty.
- Dobor co runde: 2 karty.

## 5. Mecz

Kazdy mecz to pojedynek gracza z AI.

Parametry MVP:

- HP gracza: 30.
- HP przeciwnika: 30.
- Start AP: 3.
- AP rosnie o 1 co runde.
- Maksymalne AP: 8.
- Startowy limit jednostek na planszy: 3.
- Limit jednostek rosnie o 1 co 2 rundy.
- Maksymalny limit jednostek: 7.

AP sluzy wylacznie do zagrywania nowych jednostek z reki. Przesuwanie jednostek juz obecnych na planszy jest darmowe w fazie przygotowania.

## 6. Faza Przygotowania

Faza przygotowania nie jest podzielona na naprzemienne tury. Gracz i AI przygotowuja swoje strony rownolegle.

W fazie przygotowania gracz moze:

- zagrac dowolna liczbe nowych jednostek z reki, jesli ma wystarczajaco AP i wolne sloty,
- wybrac wolne pole formacji dla nowej jednostki,
- przesunac wczesniej zagrane jednostki na inne dozwolone pola,
- zakonczyc przygotowanie przyciskiem `Ready`.

Jednostka zagrana z reki musi zostac ustawiona na planszy. Jesli gracz nie ma wolnego slotu jednostki, nie moze zagrac kolejnej jednostki.

AI wykonuje podobne akcje wedlug prostych zasad w tym samym czasie co gracz. Dokladne pozycje jednostek AI pozostaja ukryte do rozpoczecia auto-battle.

Klikniecie `Ready` konczy faze przygotowania gracza. Jesli AI zakonczylo swoje przygotowanie, walka rozpoczyna sie od razu. Jesli AI nadal przygotowuje formacje, walka rozpoczyna sie po zakonczeniu przygotowania AI albo po uplywie limitu czasu fazy przygotowania.

## 7. Plansza

Plansza jest heksagonalna arena 3D.

Parametry MVP:

- Rozmiar: 5x6 lub 5x7 hexow.
- Gracz wystawia jednostki na dolnych rzedach.
- AI wystawia jednostki na gornych rzedach.
- Podczas fazy przygotowania czesc planszy przeciwnika jest ukryta przed graczem.
- Jednostki AI pojawiaja sie na swoich polach dopiero na poczatku auto-battle.
- Kamera jest stala.
- Brak obracania kamery w MVP.
- Pola musza byc duze i czytelne na ekranie telefonu.

Plansza przechowuje pozycje formacji. Podczas walki jednostki moga sie poruszac, ale po zakonczeniu rundy wracaja na zapisane pozycje startowe.

## 8. Formacja I Powrot Po Rundzie

Kazda aktywna jednostka ma zapisana pozycje formacji.

Zasady:

- Pozycja formacji okresla, gdzie jednostka zaczyna runde.
- Podczas auto-battle jednostka moze przemieszczac sie po planszy.
- Po zakonczeniu rundy pozycje bojowe sa ignorowane.
- Wszystkie aktywne jednostki wracaja na swoje zapisane pozycje formacji.
- Jednostki pokonane w walce nie gina trwale.
- Gracz moze zmienic pozycje formacji w kolejnej fazie przygotowania.

## 9. Auto-Battle

Po fazie przygotowania rozpoczyna sie automatyczna walka.

Na starcie auto-battle gra ujawnia jednostki AI na wyznaczonych pozycjach formacji. Od tego momentu obie strony sa widoczne, a walka przebiega wedlug tych samych zasad dla gracza i AI.

Podstawowe zasady AI jednostek:

- Jednostka wybiera najblizszego przeciwnika.
- Jesli przeciwnik jest w zasiegu, jednostka atakuje.
- Jesli przeciwnik jest poza zasiegiem, jednostka porusza sie w jego strone.
- Melee musza podejsc do celu.
- Range moga atakowac z dystansu.
- Jesli kilka celow ma taki sam priorytet, wybor powinien byc deterministyczny.

W MVP walka powinna byc prosta, przewidywalna i latwa do debugowania.

## 10. Obrazenia Po Rundzie

Po zakonczeniu auto-battle sprawdzane sa ocalale jednostki.

Zasada:

- Ocalale jednostki gracza zadaja obrazenia przeciwnikowi.
- Ocalale jednostki AI zadaja obrazenia graczowi.
- Obrazenia sa rowne sumie wartosci `Power` ocalalych jednostek.

Przyklad:

- Ocalal Swordsman z `Power 3`.
- Ocalal Archer z `Power 2`.
- Przeciwnik otrzymuje 5 obrazen.

## 11. Jednostki

W MVP sa dwa podstawowe typy jednostek:

### Melee

Jednostki walczace wrecz.

Cechy:

- wyzsze HP,
- krotki zasieg,
- blokuja przeciwnikow,
- chronia jednostki dystansowe.

### Range

Jednostki dystansowe.

Cechy:

- nizsze HP,
- wiekszy zasieg,
- wymagaja ochrony,
- zadaja obrazenia z tylu formacji.

Podstawowe statystyki jednostki:

- `HP`
- `Attack`
- `Power`
- `Range`
- `Move`
- `AP Cost`
- `Attack Cooldown`
- `Unit Type`

## 12. Przykladowe Jednostki MVP

### Guard

- Typ: Melee
- Koszt: 2 AP
- Rola: defensywny frontliner
- Cechy: wysokie HP, niski atak

### Swordsman

- Typ: Melee
- Koszt: 2 AP
- Rola: zbalansowana jednostka
- Cechy: srednie HP, sredni atak

### Brute

- Typ: Melee
- Koszt: 4 AP
- Rola: ciezka jednostka ofensywna
- Cechy: wysokie HP, wysoki atak, wolniejszy atak

### Scout

- Typ: Melee
- Koszt: 1 AP
- Rola: tania jednostka pomocnicza
- Cechy: niskie HP, szybkie tempo

### Archer

- Typ: Range
- Koszt: 2 AP
- Rola: podstawowy dystans
- Cechy: niski HP, dobry zasieg

### Crossbowman

- Typ: Range
- Koszt: 3 AP
- Rola: mocny dystans
- Cechy: wyzszy atak, wolniejsze tempo

## 13. AI Przeciwnika

AI w MVP powinno byc proste.

Zasady:

- AI ma wlasny deck.
- AI dobiera karty tak jak gracz.
- AI zagrywa jednostki, jesli ma AP.
- AI ustawia melee blizej frontu.
- AI ustawia range dalej od frontu.
- AI przygotowuje formacje rownolegle z graczem.
- Pozycje jednostek AI sa ukryte przed graczem do startu auto-battle.
- AI nie musi miec zaawansowanej strategii w MVP.

Celem AI jest umozliwienie testowania podstawowej petli gry, nie stworzenie docelowego przeciwnika.

## 14. Boostery

Po wygranym meczu gracz otrzymuje booster.

Zasady MVP:

- Booster daje 3 jednostki.
- Jesli jednostka nie jest w kolekcji, zostaje dodana.
- Jesli jednostka juz jest w kolekcji, zamienia sie na shardy.
- Shardy sa zapisywane, ale nie maja jeszcze uzycia.

Rzadkosci moga istniec w danych, ale nie musza miec duzego wplywu na MVP.

Proponowane rzadkosci:

- Common
- Rare
- Epic
- Legendary

## 15. Ekrany MVP

MVP powinno zawierac nastepujace ekrany:

### Main Menu

- start meczu,
- wejscie do kolekcji,
- wejscie do decku.

### Collection

- lista posiadanych jednostek,
- podglad statystyk jednostki.

### Deck Builder

- wybor jednostek do decku,
- limit decku,
- informacja o typach i kosztach.

### Battle

- arena,
- HP obu stron,
- AP gracza,
- reka kart,
- przycisk `Ready`,
- aktualna runda.

### Booster Result

- pokazanie zdobytych jednostek,
- informacja o duplikatach zamienionych na shardy.

## 16. Styl Wizualny MVP

Klimat gry nie jest jeszcze ustalony, wiec MVP powinno uzywac neutralnych placeholderow.

Zalozenia:

- proste modele 3D,
- czytelne kolory druzyn,
- minimalne efekty,
- brak ciezkiego post-processingu,
- proste materialy URP,
- nacisk na czytelnosc planszy i jednostek.

Na start jednostki moga byc reprezentowane przez proste bryly 3D z ikona lub kolorem typu.

## 17. Poza Zakresem MVP

MVP nie obejmuje:

- PvP,
- zaklec,
- pulapek,
- subclass,
- kampanii,
- levelowania jednostek,
- uzycia shardow,
- monetyzacji,
- sklepu premium,
- pelnego stylu artystycznego,
- zaawansowanego AI,
- rozbudowanych animacji,
- reliktow lub pasywnych bonusow.

## 18. Glowne Ryzyka

### Balans Tempa Meczu

Jesli gracz zbyt szybko zapelni plansze, decyzje moga stac sie oczywiste. Limit jednostek na planszy powinien kontrolowac tempo eskalacji.

### Czytelnosc Na Telefonie

Hexy, jednostki i karty musza byc latwe do dotkniecia i rozroznienia na malym ekranie.

### Dlugosc Fazy Przygotowania

Rownolegla faza przygotowania powinna przyspieszyc mecz, ale moze zwiekszyc presje decyzyjna. MVP powinno testowac, czy gracz ma wystarczajaco czasu na zagrywanie jednostek, przestawianie formacji i klikniecie `Ready` na ekranie telefonu.

### Ukryte Ustawienie Przeciwnika

Ukrycie czesci planszy AI moze zwiekszyc napiecie i replayability, ale moze tez oslabic poczucie kontroli. MVP powinno sprawdzic, czy gracz rozumie, dlaczego walka zaczela sie w dany sposob po ujawnieniu jednostek przeciwnika.

### Determinizm Walki

Auto-battle musi byc przewidywalny. Losowosc w walce powinna byc ograniczona albo kontrolowana.

## 19. Cel MVP

Celem MVP jest sprawdzenie, czy podstawowa petla jest przyjemna:

```text
dobieram jednostki -> zagrywam je za AP -> ustawiam formacje -> ogladam walke -> poprawiam ustawienie -> dokladam kolejne jednostki
```

Jesli ta petla dziala, dopiero wtedy warto rozwijac klimat, wieksza kolekcje, zaklecia, pulapki, progresje, PvP i system shardow.
