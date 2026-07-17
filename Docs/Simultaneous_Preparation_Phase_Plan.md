# Simultaneous Preparation Phase Plan

## Cel

Zmienic model przygotowania przed walka z naprzemiennych tur gracza i AI na jedna rownolegla faze przygotowania.

Nowe zasady:

- Gracz i AI przygotowuja swoje formacje w tym samym czasie.
- Gracz moze zagrac dowolna liczbe jednostek z reki, o ile ma wystarczajaco AP i wolne sloty.
- Klikniecie `Ready` konczy przygotowanie gracza.
- Jednostki AI i ich pozycje sa ukryte podczas przygotowania.
- Na starcie auto-battle jednostki AI zostaja ujawnione na wyznaczonych polach.
- Walka startuje po kliknieciu `Ready`, jezeli AI jest gotowe, albo po zakonczeniu przygotowania AI / uplywie limitu czasu.

## Zakres MVP

Ta zmiana dotyczy tylko flow bitwy. Nie dodaje jeszcze:

- kart zaklec,
- deck buildera,
- rewardow,
- permanentnej smierci jednostek,
- zaawansowanego fog of war,
- predykcji pozycji przeciwnika,
- multiplayer.

Ukrycie przeciwnika w MVP moze byc proste: widoki jednostek AI nie sa tworzone albo sa nieaktywne do startu walki. Logika AI moze nadal pracowac na danych runtime.

## Aktualny Stan

Aktualny prototype uzywa `BattlePhase.Preparation` oraz `ActivePreparationSide`, co wspiera naprzemienne akcje stron.

Glowne klasy do zmiany:

- `BattleState`
- `PreparationTurnService`
- `BattleController`
- `EnemyPreparationAI`
- `BattleUIController`
- `BattleInputController`
- `BoardPresenter`
- testy EditMode dla flow przygotowania

Obecnie zagranie jednostki przez gracza konczy aktywna akcje strony przez `PreparationTurnService.CompleteActiveSideAction(state)`. Po zmianie zagranie jednostki nie powinno oddawac tury AI, bo nie ma juz tur.

## Docelowy Model Stanu

### BattleState

Stan powinien rozdzielic:

- czy gracz zakonczyl przygotowanie,
- czy AI zakonczylo przygotowanie,
- czy faza przygotowania jest nadal aktywna,
- czy countdown jest aktywny.

Rekomendowane podejscie MVP:

- zachowac `BattlePhase.Preparation`, `Combat`, `RoundResolution`, `MatchEnd`,
- usunac zaleznosc runtime flow od `ActivePreparationSide`,
- zostawic `ActivePreparationSide` tylko tymczasowo, jezeli jest potrzebne do kompatybilnosci UI/testow, ale docelowo je usunac albo przestac eksponowac,
- uzywac `Player.IsReady` i `Enemy.IsReady` jako glownego zrodla prawdy.

Warunek startu walki:

```text
state.Phase == Preparation
AND state.Player.IsReady
AND state.Enemy.IsReady
=> state.Phase = Combat
```

Jesli AI jest natychmiastowe w MVP, moze przygotowac cala formacje na poczatku fazy albo tuz po zmianie rundy. Wtedy `Ready` gracza praktycznie startuje walke.

## Flow Przygotowania

### Start Rundy

1. Gracz dobiera karty i otrzymuje AP.
2. AI dobiera karty i otrzymuje AP.
3. Obie strony maja `IsReady = false`.
4. AI przygotowuje ukryta formacje.
5. Gracz widzi tylko swoja strone planszy i swoja reke.
6. Gracz moze wystawiac jednostki i przesuwac formacje do momentu klikniecia `Ready`.

### Akcje Gracza

Gracz moze:

- zagrac jednostke, jesli karta jest w rece,
- zaplacic AP,
- ustawic jednostke na legalnym polu swojej formacji,
- powtorzyc to wiele razy w tej samej fazie,
- przesuwac swoje jednostki na legalne pola,
- kliknac `Ready`.

Zagranie jednostki:

- nie konczy przygotowania,
- nie przelacza aktywnej strony,
- nie wywoluje tury AI,
- tylko aktualizuje AP, reke, sloty, widoki i stan UI.

### Akcje AI

AI moze zostac zaimplementowane jako natychmiastowy resolver:

1. Gdy startuje faza przygotowania, AI analizuje reke, AP i sloty.
2. AI wystawia tyle jednostek, ile uzna za sensowne.
3. AI zapisuje ich pozycje w `BattleState.Enemy.Units`.
4. AI ustawia `Enemy.IsReady = true`.
5. Widoki jednostek AI pozostaja ukryte do startu walki.

To jest najprostsze i najlepsze dla MVP. Pozniej AI mozna opoznic animacyjnie albo symulowac czas przygotowania bez zmiany zasad.

### Ready

Klikniecie `Ready`:

- ustawia `Player.IsReady = true`,
- blokuje dalsze zagrywanie kart i przesuwanie jednostek,
- sprawdza, czy `Enemy.IsReady == true`,
- jesli tak, przechodzi do `Combat`,
- jesli nie, czeka na AI albo countdown.

## Ukrywanie Jednostek AI

### Minimalna Implementacja MVP

Najprostsza wersja:

- runtime jednostki AI istnieja w `BattleState`,
- `BattleController` nie tworzy `UnitView` dla jednostek AI podczas przygotowania,
- na starcie walki tworzy albo ujawnia `UnitView` dla jednostek AI,
- `BattleView.BindSimulation` dostaje komplet widokow dopiero po ujawnieniu.

Alternatywa:

- tworzyc `UnitView` AI od razu, ale ustawic `gameObject.SetActive(false)` do startu walki.

Rekomendacja: nie tworzyc widokow AI przed startem walki, bo jest to prostsze mentalnie i ogranicza obiekty sceny podczas przygotowania.

### Board Visibility

Na MVP wystarczy:

- nie podswietlac pol AI,
- nie pokazywac jednostek AI,
- opcjonalnie przyciemnic gorne rzedy planszy prostym overlayem lub materialem.

Nie implementowac jeszcze pelnego systemu fog of war. To byloby za duze jak na obecny etap.

## Zmiany W Kodzie

### 1. PreparationTurnService

Zmienic albo zastapic obecne API.

Nowe operacje:

- `MarkPlayerReady(BattleState state)`
- `MarkEnemyReady(BattleState state)`
- `TryStartCombatIfReady(BattleState state)`
- `CanPlayerPrepare(BattleState state)`

Usunac z glownego flow:

- przelaczanie `ActivePreparationSide`,
- wymuszanie jednej akcji strony,
- automatyczne oddawanie akcji AI po zagraniu jednostki.

### 2. UnitPlayService

Walidacja zagrania jednostki powinna sprawdzac:

- `state.Phase == BattlePhase.Preparation`,
- gracz nie jest `IsReady`,
- karta jest w rece,
- gracz ma AP,
- gracz ma wolny slot,
- pole jest legalne dla strony,
- pole jest wolne.

Nie powinna sprawdzac `ActivePreparationSide == Player`.

### 3. FormationService

Przesuwanie jednostek gracza powinno byc legalne, gdy:

- trwa `Preparation`,
- gracz nie kliknal `Ready`,
- jednostka nalezy do gracza,
- docelowe pole jest legalne.

Nie powinno zalezec od naprzemiennej tury.

### 4. EnemyPreparationAI

AI powinno przygotowac cala formacje w jednej operacji:

- przejsc po kartach w rece,
- wystawic jednostki dopoki ma AP i sloty,
- preferowac melee blizej frontu,
- preferowac range dalej od frontu,
- oznaczyc `Enemy.IsReady = true`.

Metoda moze nazywac sie np.:

- `PrepareFormation(BattleState state)`
- `ExecutePreparation(BattleState state)`

Stara metoda `ExecuteTurn` moze zostac tymczasowo jako wrapper, ale docelowo nazwa powinna odzwierciedlac brak tur.

### 5. BattleController

Zmiany:

- `TryPlayPlayerCard` po sukcesie nie wywoluje `CompleteActiveSideAction`.
- `TryMovePlayerUnit` dziala bez sprawdzania `ActivePreparationSide`.
- `ConfirmReady` ustawia gracza jako gotowego i probuje startu walki.
- `ProgressAutomaticFlow` nie powinien czekac na aktywna strone AI.
- AI powinno zostac przygotowane na start rundy lub gdy flow wykryje `Enemy.IsReady == false`.
- przed startem combat trzeba ujawnic / stworzyc widoki jednostek AI.

Wazne: walka nie moze wystartowac, zanim widoki AI sa gotowe, bo `BattleView` mapuje `UnitId` na `UnitView`.

### 6. BattleUIController

UI powinno pokazac:

- AP gracza,
- sloty,
- reke,
- status `Ready`,
- faze przygotowania bez tekstu sugerujacego aktywna strone,
- opcjonalnie komunikat, ze przeciwnik przygotowuje ukryta formacje.

Usunac z tekstu fazy zaleznosc od `ActivePreparationSide`, np.:

```text
Preparation
Preparation 8s
Combat
```

### 7. BattleInputController

Input powinien pozwalac na drag kart i przesuwanie jednostek, gdy:

- `state.Phase == Preparation`,
- `state.Player.IsReady == false`.

Nie sprawdzac `ActivePreparationSide == Player`.

### 8. BoardPresenter

Potrzebne opcjonalne API:

- podswietlanie tylko legalnych pol gracza,
- brak highlightu pol AI podczas przygotowania,
- ewentualny prosty stan ukrycia gornych rzedow.

W MVP mozna zaczac bez wizualnego overlayu, jesli samo niepokazywanie jednostek AI jest wystarczajace.

## Kolejnosc Implementacji

1. Dodac testy opisujace nowy flow przygotowania.
2. Zmienic `PreparationTurnService` na model ready obu stron.
3. Zmienic walidacje inputu i zagrywania jednostek, zeby nie zalezec od `ActivePreparationSide`.
4. Zmienic `BattleController.TryPlayPlayerCard`, aby nie konczyl tury po zagraniu jednostki.
5. Zmienic AI z pojedynczej tury na przygotowanie calej formacji.
6. Ukryc tworzenie albo aktywacje widokow AI do startu combat.
7. Zmienic UI fazy przygotowania.
8. Uruchomic EditMode tests.
9. Przetestowac scene Battle w edytorze.
10. Zrobic mobile usability/profiling pass.

## Testy

### EditMode

Dodac lub zaktualizowac testy:

- gracz moze zagrac wiele jednostek w jednej fazie, jesli ma AP i sloty,
- zagranie jednostki nie ustawia `Player.IsReady`,
- zagranie jednostki nie przelacza aktywnej strony,
- `Ready` blokuje dalsze zagrywanie kart,
- combat startuje dopiero, gdy `Player.IsReady` i `Enemy.IsReady`,
- AI przygotowuje wiele jednostek w jednej operacji,
- pozycje AI istnieja w stanie przed walka,
- widoki AI nie sa wymagane w testach logiki,
- po rundzie obie strony resetuja `IsReady` do kolejnej fazy przygotowania.

### Scene / PlayMode Checklist

Sprawdzic recznie:

- gracz moze przeciagac kilka kart jednostek przed `Ready`,
- AP i sloty aktualizuja sie po kazdym wystawieniu,
- nie mozna grac po `Ready`,
- AI nie jest widoczne podczas przygotowania,
- AI pojawia sie na starcie walki,
- walka rozlicza sie tak samo jak przed zmiana,
- restart bitwy nie zostawia ukrytych albo zdublowanych widokow AI.

## Mobile Profiling Points

Po implementacji sprawdzic:

- alokacje podczas dragowania wielu kart w jednej fazie,
- Canvas rebuildy przy odswiezaniu reki po kilku zagraniach,
- liczbe `UnitView` tworzonych na starcie walki,
- koszt ujawniania AI na poczatku combat,
- restart bitwy po kilku pelnych meczach,
- czy ukrywanie AI nie dodaje kosztownych overlayow lub przezroczystosci.

## Ryzyka

### Zbyt Szybkie Zapelnienie Planszy

Skoro gracz moze wystawic wiele jednostek w jednej fazie, AP i sloty staja sie glownym ograniczeniem tempa. Trzeba testowac:

- startowe AP,
- dobor kart,
- koszt jednostek,
- limit slotow,
- wzrost slotow co kilka rund.

### Mniejsza Czytelnosc Przy Ukrytym AI

Gracz moze czuc, ze przegrywa przez niewidoczna informacje. MVP powinno pokazac jasny moment ujawnienia AI i pozwolic graczowi szybko zrozumiec, skad przyszlo zagrozenie.

### Stary Kod Oparty O ActivePreparationSide

Najwieksze ryzyko techniczne to pozostawienie czesci warunkow zaleznych od `ActivePreparationSide`. Po zmianie trzeba wyszukac wszystkie uzycia i zdecydowac, czy:

- usunac warunek,
- zastapic go `!state.Player.IsReady`,
- zostawic tylko w testach kompatybilnosciowych,
- usunac pole calkowicie w pozniejszym refaktorze.

### Widoki AI A Symulacja

Jesli jednostki AI sa ukryte przez brak `UnitView`, trzeba upewnic sie, ze `BattleView.BindSimulation` dostaje komplet widokow przed pierwszym tickiem walki. Inaczej eventy ruchu, ataku albo smierci moga nie miec obiektu prezentacji.

## Decyzje Do Potwierdzenia Przed Implementacja

1. Czy AI ma przygotowywac formacje natychmiast, czy po krotkim opoznieniu czasowym?
2. Czy gorne rzedy planszy maja byc wizualnie zasloniete, czy wystarczy brak jednostek AI?
3. Czy gracz ma widziec liczbe jednostek wystawionych przez AI przed walka?
4. Czy `Ready` ma startowac walke natychmiast, gdy AI jest gotowe, czy zawsze ma byc krotkie odliczanie?
5. Czy ukrycie AI dotyczy tylko pozycji, czy rowniez tego, jakie jednostki zostaly zagrane?

## Rekomendacja MVP

Na pierwszy etap:

- AI przygotowuje formacje natychmiast.
- Gracz nie widzi zadnych jednostek AI ani ich liczby podczas przygotowania.
- `Ready` startuje walke natychmiast, jesli AI jest gotowe.
- Widoki AI sa tworzone dopiero tuz przed `BattleView.BindSimulation`.
- Nie dodawac jeszcze pelnego fog of war ani animacji ujawnienia.

To daje najmniejsza zmiane runtime, najmniej ryzyk wydajnosciowych i wystarczajaco dobrze testuje nowa zasade gry.
