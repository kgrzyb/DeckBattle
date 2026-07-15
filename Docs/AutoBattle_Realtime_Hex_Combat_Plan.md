# Deck Battle - Real-Time Hex Auto-Battle Plan

## 1. Cel Dokumentu

Ten dokument opisuje plan implementacji walki auto-battle w czasie rzeczywistym na planszy hexagonalnej.

Plan zaklada:

- jednostki poruszaja sie miedzy hexami,
- jednostki atakuja przeciwnikow w zasiegu liczonym dystansem hexowym,
- jednostka poza zasiegiem idzie do pozycji ataku przy najblizszym osiagalnym przeciwniku,
- jezeli do najblizszego przeciwnika nie da sie dotrzec, jednostka wybiera przeciwnika z najmniejsza iloscia HP,
- dwie lub wiecej jednostek nie moga zajmowac tego samego hexa jako pozycji koncowej,
- logika walki jest oddzielona od prezentacji Unity.

Priorytety implementacji:

1. Stabilna, deterministyczna logika.
2. Brak zaleznosci decyzji bojowych od FPS i animacji.
3. Czytelna separacja symulacji, danych i widoku.
4. Niski koszt CPU, GC i UI na mobile.
5. Przygotowanie API pod przyszle przeszkody terenowe i inne strategie targetowania.

## 2. Decyzje V1

### Targetowanie

W V1 strategia targetowania jest globalna dla calej bitwy.

Priorytet wyboru celu:

1. Najblizszy przeciwnik, do ktorego mozna dotrzec na pozycje ataku.
2. Jezeli najblizszy przeciwnik nie ma osiagalnej pozycji ataku, przeciwnik z najmniejsza iloscia HP.
3. Przy remisie stabilny tie-breaker, np. `UnitId`.

Strategia powinna byc zaimplementowana tak, aby w przyszlosci mozna bylo przeniesc ja na poziom jednostki, karty albo efektu.

### Ruch

Jednostki moga przechodzic przez hexy zajete przez sojusznikow, ale nie moga zakonczyc kroku na zajetym hexie.

Jednostka nie idzie na hex zajmowany przez przeciwnika. Zamiast tego szuka wolnego hexa, z ktorego przeciwnik znajduje sie w zasiegu ataku.

Przyklad:

- jednostka melee z `AttackRange = 1` szuka wolnego sasiada celu,
- jednostka ranged z `AttackRange = 3` szuka wolnego hexa w promieniu 3 od celu.

### Atak

Atak nastapuje dopiero po dotarciu do hexa, z ktorego cel jest w zasiegu.

Jednostka, ktora jest juz w zasiegu celu, nie rusza sie tylko po to, aby poprawic pozycje, chyba ze w przyszlosci zostanie dodana osobna taktyka pozycjonowania.

### Plansza

V1 nie zawiera przeszkod terenowych.

Mimo to `HexGrid` powinien miec API typu `IsWalkable`, aby pozniej mozna bylo dodac:

- zablokowane pola,
- trudny teren,
- pola specjalne,
- przeszkody dynamiczne.

## 3. Architektura

### Warstwa Symulacji

Warstwa symulacji powinna byc mozliwa do testowania w edit mode bez sceny Unity.

Glowne klasy/systemy:

- `BattleSimulation`
- `HexGrid`
- `UnitRuntimeState`
- `TargetSelector`
- `AttackPositionSelector`
- `MovementResolver`
- `CombatResolver`
- `BattleEventQueue`

MonoBehaviours nie powinny podejmowac decyzji bojowych. Ich rola to:

- startowanie symulacji,
- przekazywanie danych wejsciowych,
- odtwarzanie eventow,
- interpolacja ruchu i animacje.

### Warstwa Widoku

Warstwa widoku powinna subskrybowac eventy z symulacji:

- `UnitMoved`
- `UnitAttackStarted`
- `UnitDamaged`
- `UnitDied`
- `BattleEnded`

Widok nie powinien zmieniac logicznej pozycji jednostki. Pozycja logiczna nalezy do symulacji, a widok tylko interpoluje model miedzy centrami hexow.

## 4. Proponowany Przeplyw Ticka

Symulacja powinna dzialac w stalym kroku czasowym, np. 10-20 tickow na sekunde.

Rekomendowany przeplyw jednego ticka:

1. Wyczysc tymczasowe rezerwacje ruchu.
2. Usun lub oznacz martwe jednostki.
3. Dla zywych jednostek odswiez targety.
4. Sprawdz, ktore jednostki sa w zasiegu aktualnego celu.
5. Wykonaj gotowe ataki.
6. Dla jednostek poza zasiegiem zaplanuj ruch.
7. Rozwiaz konflikty rezerwacji hexow.
8. Zastosuj zaakceptowane ruchy logiczne.
9. Wygeneruj eventy dla warstwy widoku.
10. Sprawdz warunek konca walki.

Wazne: animacja ataku i animacja ruchu nie powinny blokowac logiki symulacji, chyba ze swiadomie zostanie wybrany model walki z pauza na animacje.

## 5. System Hex Grid

### Odpowiedzialnosci

`HexGrid` odpowiada za:

- reprezentacje wspolrzednych hexow,
- dystans hexowy,
- pobieranie sasiadow,
- sprawdzanie czy hex istnieje,
- sprawdzanie czy hex jest przechodni,
- pobieranie hexow w zasiegu,
- pathfinding.

### Rekomendowane Wspolrzedne

Uzyc axial coordinates:

```text
HexCoord
  q: int
  r: int
```

Dystans:

```text
dq = abs(a.q - b.q)
dr = abs(a.r - b.r)
ds = abs((-a.q - a.r) - (-b.q - b.r))
distance = max(dq, dr, ds)
```

### Pathfinding

V1 moze uzyc BFS albo A*.

Rekomendacja:

- BFS wystarczy przy malej planszy i prostych kosztach ruchu,
- A* bedzie lepszy, jezeli plansza urosnie albo pojawia sie przeszkody.

Pathfinding powinien szukac sciezki do wolnej pozycji ataku, a nie do hexa zajmowanego przez przeciwnika.

## 6. Runtime State Jednostek

### Definicja Jednostki

`UnitDefinition` albo podobny asset powinien zawierac dane stale:

- maksymalne HP,
- damage,
- attack range,
- attack cooldown,
- move rate,
- team/faction, jezeli wynika z danych,
- rozmiar jednostki, jezeli kiedys pojawia sie jednostki wieksze niz 1 hex.

### Stan Runtime

`UnitRuntimeState` powinien zawierac dane zmienne:

- `UnitId`,
- aktualne HP,
- team,
- aktualny hex,
- aktualny target,
- cooldown ataku,
- status zycia,
- opcjonalnie aktualnie planowana sciezka.

Runtime state nie powinien bezposrednio znac `GameObject`, `Transform` ani komponentow widoku.

## 7. Target Selection

### Regula V1

Dla kazdej jednostki:

1. Znajdz zywych przeciwnikow.
2. Posortuj po dystansie hexowym od jednostki do przeciwnika.
3. Dla kazdego przeciwnika sprawdz, czy istnieje osiagalna pozycja ataku.
4. Wybierz pierwszego osiagalnego przeciwnika.
5. Jezeli nie znaleziono osiagalnego przeciwnika w tej kolejce, wybierz przeciwnika z najmniejszym HP, ktory ma jakakolwiek osiagalna pozycje ataku.
6. Przy remisach uzyj `UnitId`.

### Uwagi

Samo sprawdzenie dystansu do przeciwnika nie wystarczy. Dla jednostki ranged najblizszy przeciwnik moze byc blisko, ale wszystkie hexy pozwalajace na atak moga byc zajete.

Dlatego target selector powinien wspolpracowac z `AttackPositionSelector`.

## 8. Attack Position Selection

`AttackPositionSelector` odpowiada za znalezienie najlepszego hexa, z ktorego jednostka moze atakowac cel.

Wejscie:

- atakujaca jednostka,
- cel,
- aktualna mapa zajetosci,
- aktualna plansza,
- zasieg ataku.

Wyjscie:

- aktualny hex atakujacego, jezeli cel jest juz w zasiegu,
- albo najlepszy wolny hex w zasiegu ataku,
- albo brak wyniku.

Kryteria wyboru pozycji:

1. Hex musi istniec.
2. Hex musi byc przechodni.
3. Hex nie moze byc koncowo zajety przez inna jednostke.
4. Hex musi byc w zasiegu ataku wzgledem celu.
5. Musi istniec sciezka z aktualnej pozycji atakujacego.
6. Najlepszy hex to ten z najkrotsza sciezka.
7. Przy remisie uzyc stabilnego porzadku wspolrzednych.

## 9. Movement Resolver

### Zasada Ogolna

Jednostka planuje maksymalnie jeden krok logiczny na tick.

Ruch sklada sie z dwoch etapow:

1. Planowanie intencji ruchu.
2. Rozwiazanie konfliktow i zastosowanie zatwierdzonych ruchow.

### Occupancy

System powinien utrzymywac:

```text
occupiedHexes: HexCoord -> UnitId
reservedHexes: HexCoord -> UnitId
```

`occupiedHexes` opisuje aktualny stan planszy.

`reservedHexes` opisuje docelowe hexy dla ruchow planowanych w danym ticku.

### Przechodzenie Przez Sojusznikow

W V1 jednostki moga przechodzic przez hexy zajete przez sojusznikow, ale nie moga zakonczyc ruchu na zajetym hexie.

Praktycznie oznacza to:

- pathfinding moze traktowac sojusznikow jako przechodnich dla dalszych krokow,
- pierwszy krok ruchu nie moze konczyc sie na hexie zajetym,
- docelowa pozycja ataku nie moze byc zajeta.

Trzeba zachowac ostroznosc, aby nie dopuscic do sytuacji, w ktorej kilka jednostek konczy tick na tym samym hexie.

### Konflikty

Jezeli kilka jednostek chce wejsc na ten sam hex:

1. Wygrywa jednostka z najwyzszym deterministycznym priorytetem.
2. Przegrane jednostki probuja alternatywny krok, jezeli taki jest tani do znalezienia.
3. Jezeli nie ma alternatywy, zostaja na miejscu.

Proponowany priorytet:

1. Jednostka z krotsza sciezka do pozycji ataku.
2. Jednostka z nizszym `UnitId`.

Nie uzywac losowego rozstrzygania konfliktow w logice V1.

## 10. Combat Resolver

### Odpowiedzialnosci

`CombatResolver` odpowiada za:

- sprawdzenie zasiegu,
- obsluge cooldownu,
- zadanie obrazen,
- obsluge smierci jednostki,
- wygenerowanie eventow bojowych.

### Kolejnosc

Jednostka moze atakowac, jezeli:

- zyje,
- ma zywy cel,
- cel jest w zasiegu hexowym,
- cooldown ataku jest gotowy.

Po ataku:

- cooldown zostaje zresetowany,
- cel traci HP,
- jezeli HP celu spada do 0 lub mniej, cel umiera,
- hex celu zostaje zwolniony po przetworzeniu smierci.

## 11. Battle Events

Symulacja powinna produkowac eventy zamiast bezposrednio sterowac widokiem.

Przykladowe eventy:

```text
UnitMoved(UnitId unit, HexCoord from, HexCoord to)
UnitAttackStarted(UnitId attacker, UnitId target)
UnitDamaged(UnitId target, int amount, int remainingHp)
UnitDied(UnitId unit)
BattleEnded(BattleSide winner)
```

Eventy powinny byc proste, bez referencji do `GameObject`.

## 12. Warstwa Unity View

### UnitView

`UnitView` odpowiada za:

- interpolacje pozycji modelu miedzy centrami hexow,
- animacje ataku,
- animacje obrazen,
- animacje smierci,
- aktualizacje paska HP.

### BattleView

`BattleView` odpowiada za:

- mapowanie `UnitId` na `UnitView`,
- odbieranie eventow z symulacji,
- odpalanie odpowiednich animacji i efektow,
- pooling jednostek i efektow.

### Mobile Performance

Unikac:

- layout rebuildow UI co klatke,
- aktualizacji tekstow bez zmiany wartosci,
- alokacji w `Update`,
- wyszukiwania komponentow w petlach,
- ciezkich particle systemow,
- przezroczystosci o duzym overdraw.

## 13. Milestones

### Milestone 1 - Hex Grid Core

Cel: przygotowac pewna baze planszy.

Zakres:

- `HexCoord`,
- dystans hexowy,
- sasiedzi,
- `IsValidHex`,
- `IsWalkable`,
- `GetHexesInRange`,
- prosty BFS albo A*.

Kryteria akceptacji:

- testy edit mode potwierdzaja dystans,
- testy potwierdzaja liste sasiadow,
- testy potwierdzaja znajdowanie sciezki.

### Milestone 2 - Unit Runtime State

Cel: przygotowac dane jednostek niezalezne od widoku.

Zakres:

- `UnitDefinition`,
- `UnitRuntimeState`,
- `BattleSide`,
- inicjalizacja jednostek na planszy,
- walidacja braku duplikatow pozycji startowych.

Kryteria akceptacji:

- symulacja moze wystartowac bez sceny Unity,
- bledne pozycje startowe sa wykrywane.

### Milestone 3 - Global Target Selection

Cel: jednostki wybieraja cele zgodnie z regula V1.

Zakres:

- wybor najblizszego osiagalnego przeciwnika,
- fallback do najnizszego HP,
- tie-breakery po `UnitId`,
- testy kilku scenariuszy.

Kryteria akceptacji:

- wynik jest deterministyczny,
- targetowanie nie zalezy od kolejnosci obiektow w scenie.

### Milestone 4 - Attack Position Selection

Cel: jednostka umie znalezc pozycje, z ktorej moze atakowac.

Zakres:

- wyszukiwanie wolnych hexow w zasiegu celu,
- wybor najblizszej osiagalnej pozycji,
- brak wejscia na hex przeciwnika,
- przygotowanie pod przyszle przeszkody.

Kryteria akceptacji:

- melee wybiera wolnego sasiada celu,
- ranged wybiera wolny hex w zasiegu,
- zajete pozycje sa pomijane.

### Milestone 5 - Movement Reservation

Cel: jednostki poruszaja sie bez zajmowania tych samych hexow.

Zakres:

- `occupiedHexes`,
- `reservedHexes`,
- planowanie jednego kroku na tick,
- deterministyczne konflikty,
- przechodzenie przez sojusznikow bez konczenia na ich hexie.

Kryteria akceptacji:

- dwie jednostki nigdy nie koncza ticka na tym samym hexie,
- konflikty sa powtarzalne,
- jednostka zablokowana nie generuje blednego stanu.

### Milestone 6 - Combat Resolver

Cel: jednostki atakuja po dotarciu do zasiegu.

Zakres:

- sprawdzanie zasiegu hexowego,
- cooldown ataku,
- obrazenia,
- smierc,
- zwalnianie hexa po smierci.

Kryteria akceptacji:

- jednostka w zasiegu atakuje,
- jednostka poza zasiegiem rusza sie,
- martwy cel nie jest dalej atakowany.

### Milestone 7 - Battle Tick Loop

Cel: polaczyc systemy w pelna symulacje.

Zakres:

- staly tick,
- event queue,
- warunek konca walki,
- test 1v1 melee,
- test 1v1 ranged,
- test kilku jednostek na planszy.

Kryteria akceptacji:

- bitwa startuje i konczy sie deterministycznie,
- symulacja dziala bez widoku Unity,
- eventy opisuja wszystkie istotne zmiany.

### Milestone 8 - Unity View Integration

Cel: podpiac prezentacje do gotowej symulacji.

Zakres:

- `BattleView`,
- `UnitView`,
- interpolacja ruchu,
- animacje ataku,
- paski HP,
- pooling prostych efektow.

Kryteria akceptacji:

- ruch wyglada plynnie,
- animacje nie steruja logika,
- UI aktualizuje sie tylko na eventach.

### Milestone 9 - Debug Tools i Tuning

Cel: ulatwic diagnoze zachowania walki.

Zakres:

- debug overlay dla targetow,
- debug overlay dla sciezek,
- debug overlay dla zasiegu,
- podglad zajetych i zarezerwowanych hexow,
- parametry tuningu cooldownu, zasiegu i ruchu.

Kryteria akceptacji:

- da sie zobaczyc, dlaczego jednostka stoi albo idzie do danego celu,
- podstawowy tuning nie wymaga zmian w kodzie.

### Milestone 10 - Stabilizacja i Profilowanie

Cel: przygotowac system pod dalszy rozwoj i mobile.

Zakres:

- testy edit mode dla logiki,
- sprawdzenie alokacji GC podczas walki,
- sprawdzenie kosztu pathfindingu,
- sprawdzenie kosztu widoku i UI,
- testy na scenariuszach z wieksza liczba jednostek.

Kryteria akceptacji:

- brak alokacji w goracych sciezkach symulacji,
- brak layout rebuildow UI co klatke,
- koszt ticka jest stabilny dla docelowej liczby jednostek prototypu.

## 14. Minimalny Zestaw Testow

Testy edit mode:

- dystans hexow,
- sasiedzi hexa,
- hexy w zasiegu,
- pathfinding do wolnej pozycji,
- brak sciezki,
- target nearest reachable,
- fallback lowest HP,
- konflikt dwoch jednostek o jeden hex,
- smierc celu,
- cooldown ataku,
- koniec walki.

Scenariusze testowe:

- 1v1 melee,
- 1v1 ranged,
- 2v1 z konfliktem ruchu,
- jednostka otoczona sojusznikami,
- cel ginie przed ruchem innej jednostki,
- ranged unit juz w zasiegu na starcie,
- brak wolnej pozycji ataku przy najblizszym celu.

## 15. Ryzyka i Punkty Do Decyzji

### Koszt Pathfindingu

Przy wiekszej liczbie jednostek pathfinding wykonywany co tick dla kazdej jednostki moze byc kosztowny.

Mozliwe optymalizacje pozniej:

- cache sciezek przez kilka tickow,
- przeliczanie targetu nie co tick,
- BFS od celu zamiast osobnego szukania dla kazdego atakujacego,
- ograniczenie liczby jednostek aktywnie planujacych ruch w jednym ticku.

### Korki Jednostek

Przy waskich przejsciach albo wielu jednostkach przy jednym celu moze dojsc do korkow.

V1 powinno byc deterministyczne i stabilne, nawet jezeli nie zawsze optymalne taktycznie.

### Przechodzenie Przez Sojusznikow

Regula pozwala planowac sciezke przez sojusznikow, ale wymaga ostroznego rozroznienia:

- przechodni dla sciezki,
- niedostepny jako koncowy hex kroku.

### Przyszle Przeszkody

Chociaz V1 nie ma przeszkod, API powinno byc gotowe na `IsWalkable`.

Nie nalezy jednak implementowac ciezkich systemow terenu przed realna potrzeba.

## 16. Rekomendacja Implementacyjna

Najpierw zbudowac czysta symulacje i testy edit mode.

Kolejnosc pracy:

1. Hex grid.
2. Runtime state.
3. Target selection.
4. Attack position selection.
5. Movement reservation.
6. Combat resolver.
7. Battle tick loop.
8. Unity view.
9. Debug i profiling.

Nie podpinac animacji ani efektow, dopoki logika ruchu, targetowania i ataku nie przechodzi testow.

Najwazniejsza zasada: symulacja decyduje, widok pokazuje.
