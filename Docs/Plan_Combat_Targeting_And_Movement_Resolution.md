# Plan domknięcia targetowania i rozstrzygania ruchu w symulacji walki

## Status dokumentu

Plan zaktualizowano 2026-07-25 na podstawie aktualnego kodu projektu.

Najważniejsza korekta względem pierwotnej wersji: zachowanie aktualnego celu
zostało już zaimplementowane i jest używane przez główną pętlę walki. Pozostały
zakres dotyczy przede wszystkim przebudowy rozstrzygania ruchu oraz
natychmiastowego commitu logicznej pozycji.

Oznaczenia używane poniżej:

- **gotowe** — zachowanie jest obecne w kodzie i ma podstawowe testy;
- **częściowo gotowe** — istnieje potrzebna struktura, ale odpowiedzialności albo
  kontrakt nie są jeszcze docelowe;
- **do wykonania** — aktualny kod nadal realizuje poprzedni kontrakt.

## Cel

Celem zmian jest zapewnienie przewidywalnej i deterministycznej symulacji walki, w której:

- jednostka atakuje przypisany cel aż do jego śmierci lub utraty dostępności;
- jednostka zmienia cel dopiero wtedy, gdy obecny cel jest martwy, nietargetowalny albo nie istnieje osiągalne miejsce pozwalające go zaatakować;
- wszystkie ruchy w ticku są rozstrzygane według wzorca `collect intent -> resolve conflicts -> commit`;
- przegrany konfliktu o docelowy heks pozostaje w miejscu i nie szuka alternatywnego kroku;
- konflikty są rozstrzygane deterministycznie według kolejności wystawienia jednostek;
- logiczna geometria planszy jest gotowa do ponownej ewaluacji w następnym ticku;
- hot path symulacji nie generuje alokacji pamięci na każdy tick.

## Stan obecny

### Targetowanie — gotowe

`TargetSelector.TrySelectTargetOrRetainCurrent` faktycznie zachowuje aktualny cel.
Sprawdza `TargetUnitId`, życie i stronę celu, a następnie używa
`AttackPositionSelector.TrySelectAttackPosition`, aby potwierdzić osiągalność i
od razu uzyskać `AttackPathResult`. Globalny wybór jest uruchamiany dopiero po
odrzuceniu aktualnego celu.

`BattleTickLoop.RefreshTargets` wykonuje walidację przed `CombatResolver` i
ponownie po atakach oraz śmierciach. Przygotowane tablice `TargetSelection` i
`targetSelectionValid` są przekazywane do głównej ścieżki ruchu.

Istniejące testy potwierdzają zachowanie osiągalnego celu mimo bliższego
przeciwnika, retargetowanie po utracie osiągalności oraz użycie zachowanego celu
przez pełny tick. Nie należy ponownie planować implementacji podstawowego
`RetainCurrent`.

### Własność mutacji celu — częściowo gotowe

W głównej ścieżce `BattleTickLoop` jest właścicielem `SetTarget` i `ClearTarget`.
Publiczne przeciążenia `MovementResolver.ResolveMovement`, wywołane bez
przygotowanych selekcji, nadal jednak samodzielnie targetują oraz mutują
`TargetUnitId`. Debug preview również planuje ruch bez snapshotu selekcji z
ticka. Docelowo collect ruchu ma być czystym konsumentem gotowych selekcji.

### Rozstrzyganie ruchu — do wykonania

Aktualne intencje ruchu są sortowane najpierw według długości drogi do pozycji
ataku, a dopiero potem według `UnitId`. Nie odpowiada to regule pierwszeństwa
wynikającej wyłącznie z kolejności wystawienia.

Po przegraniu zwykłego konfliktu jednostka może uruchomić ponowny pathfinding
i wybrać alternatywny krok. Wyjątkiem jest specjalnie obsłużony konflikt dwóch
jednostek wzajemnie biegnących na jeden heks.

Do usunięcia lub zastąpienia:

- `MovementResolver.CompareIntentPriority`;
- `MovementResolver.TryFindAlternativeStep`;
- `MovementResolver.IsReciprocalConflict`;
- rezerwacje używane wyłącznie do sekwencyjnego wybierania alternatywnych tras.

### Commit logicznego ruchu — do wykonania

`MovementResolver` wywołuje `BattleSimulation.StartUnitMovement`, ale logiczne
`CurrentHex` zostaje zmienione dopiero po upływie `MovementStepDuration`.

Domyślny tick walki trwa `0.35 s`, a krok ruchu `0.4 s`. W rezultacie nowa
geometria może być dostępna dopiero po więcej niż jednym ticku. Jest to
niezgodne z wymaganiem, aby w następnym ticku przegrany konfliktu mógł ponownie
ocenić zasięg i zaatakować jednostkę, która weszła na sporny heks.

### Prezentacja ruchu — częściowo gotowe

`BattleView` już reaguje na `UnitMoved`, a `UnitView` posiada bezalokacyjną
kolejkę do czterech kroków ruchu. Po natychmiastowym logicznym commicie trzeba
dostosować synchronizację widoku, ale nie jest potrzebny nowy system animacji.

### Testy

Testy targetowania są zgodne z kontraktem zachowania aktualnego celu. Do
przepisania pozostają testy ruchu oczekujące:

- `IsMoving == true` po zaakceptowaniu intencji;
- commitu dopiero po `MovementStepDuration`;
- blokowania jednocześnie `CurrentHex` i `MovementDestination`;
- specjalnego przypadku konfliktu wzajemnego;
- alternatywnego kroku po przegraniu zwykłego konfliktu;
- priorytetu wynikającego z długości drogi.

## Docelowy przebieg ticka

1. Zakończyć logicznie wcześniej rozpoczęte efekty i rozstrzygnąć pociski.
2. Dla każdej aktywnej jednostki zweryfikować aktualny cel.
3. Zachować aktualny cel, jeżeli nadal jest prawidłowy i osiągalny.
4. Wybrać nowy cel wyłącznie dla jednostek, których aktualny cel jest nieważny lub niedostępny.
5. Rozstrzygnąć ataki.
6. Ponownie zweryfikować cele unieważnione przez śmierci i efekty ataków.
7. Utworzyć snapshot logicznych pozycji i occupancy, a następnie zebrać
   intencje ruchu bez mutowania stanu symulacji.
8. Wykryć i rozstrzygnąć konflikty o docelowe heksy.
9. Zatwierdzić wszystkie zwycięskie ruchy.
10. Sprawdzić warunki zakończenia walki.

## Etap 1 — domknięcie kontraktu celu

### Walidacja aktualnego celu — gotowe

Aktualny cel pozostaje przypisany, jeżeli:

- atakująca jednostka żyje;
- cel żyje;
- cel należy do przeciwnej strony;
- cel może być targetowany według aktualnych reguł i efektów;
- istnieje osiągalna pozycja, z której atakujący może zaatakować cel, albo cel znajduje się już w zasięgu.

Walidacja korzysta już z `AttackPositionSelector`, dzięki czemu razem z decyzją
o zachowaniu celu zwraca aktualny `AttackPathResult`. To zachowanie należy
utrzymać.

### Retargetowanie — gotowe

Globalny wybór nowego celu jest wykonywany dopiero wtedy, gdy aktualny cel:

- umarł;
- przestał być wrogi;
- stał się nietargetowalny;
- nie posiada żadnej osiągalnej pozycji ataku.

Brak dostępnego nowego celu powoduje wyczyszczenie `TargetUnitId`. Obecna
implementacja spełnia ten kontrakt.

### Centralna reguła targetowalności — do wykonania

Należy wprowadzić jeden punkt decyzyjny, na przykład:

```csharp
TargetingRules.CanBeTargeted(attacker, candidate)
```

Podstawowe warunki żywy/wrogi są obecnie powtórzone w `TargetSelector`,
`AttackPositionSelector` i `CombatResolver`. Reguła ma jedynie skonsolidować
aktualny kontrakt. Nie należy w ramach tego zadania budować ogólnego systemu
statusów; przyszłe efekty mogą później rozszerzyć ten punkt decyzyjny.

### Separacja odpowiedzialności — częściowo gotowe

`MovementResolver` nie powinien:

- wybierać nowego celu;
- zmieniać `TargetUnitId`;
- ponownie obliczać targetowania podczas rozstrzygania konfliktu.

`BattleTickLoop` już przygotowuje stabilne `TargetSelection` przed fazą ruchu.
Należy usunąć z `MovementResolver` tryb `updateUnitTargets` oraz fallback do
samodzielnego targetowania. Jeżeli proste publiczne przeciążenia resolvera są
potrzebne testom, powinny przygotować selekcje w jawnej fazie pomocniczej przed
collect, zamiast mieszać mutację celu z rozstrzyganiem konfliktów.

## Etap 2 — collect movement intents

Faza musi używać jednego snapshotu pozycji i zajętości z początku planowania.
Nie może zmieniać żadnego stanu jednostek ani planszy.

W `MovementResolver.Workspace` należy utrzymywać i ponownie wykorzystywać:

```csharp
Dictionary<UnitRuntimeState, HexCoord> DesiredMoves;
Dictionary<HexCoord, MovementIntent> WinnerByDestination;
HashSet<HexCoord> OccupiedAtCollectStart;
```

Dla każdej żywej jednostki zdolnej do podjęcia akcji:

1. Pobierz wcześniej zatwierdzony cel i `AttackPathResult`.
2. Jeżeli cel jest już w zasięgu, nie twórz intencji.
3. Pobierz wyłącznie `NextStep`.
4. Potwierdź, że krok:
   - jest sąsiednim heksem;
   - jest walkable;
   - nie był zajęty w snapshotcie początku fazy;
   - różni się od aktualnego heksa.
5. Zapisz dokładnie jedną intencję dla jednostki.

W tej fazie nie wolno:

- wywoływać `SetTarget` ani `ClearTarget`;
- rozpoczynać ruchu;
- modyfikować `CurrentHex`;
- rezerwować heksów zależnie od kolejności iteracji;
- szukać alternatywnej trasy po zobaczeniu cudzej intencji.

## Etap 3 — wykrycie i rozstrzygnięcie konfliktów

W workspace należy utrzymywać ponownie wykorzystywaną mapę zwycięzców:

```csharp
Dictionary<HexCoord, MovementIntent> WinnerByDestination
```

Algorytm:

1. Przejdź po wszystkich `DesiredMoves`.
2. Jeżeli docelowy heks nie ma jeszcze kandydata, zapisz jednostkę jako tymczasowego zwycięzcę.
3. Jeżeli heks ma już kandydata, porównaj kolejność wystawienia.
4. Zachowaj wcześniejszą jednostkę.
5. Nie generuj żadnej drugiej intencji dla przegranego.

Algorytm musi obsłużyć jednakowo:

- konflikt dwóch przeciwników;
- konflikt sojuszników;
- konflikt jednostek ścigających różne cele;
- konflikt trzech lub większej liczby jednostek;
- konflikt wzajemnie ścigających się jednostek.

Nie jest potrzebny specjalny przypadek `IsReciprocalConflict`.

## Etap 4 — deterministyczny tie-break

W produkcyjnym przepływie rosnący `RuntimeId` jest nadawany w chwili zagrania
jednostki przez `BattleState.AllocateRuntimeUnitId`. Może więc reprezentować
globalną kolejność wystawienia niezależnie od strony.

Ten przepływ jest już obecny: `UnitPlayService` pobiera identyfikator z
`BattleState`, a `BattleSimulationFactory` przenosi go do
`UnitRuntimeState.UnitId`. Fabryka grupuje jednak jednostki według strony,
dlatego indeks `BattleSimulation.Units` nadal nie może być tie-breakiem.

Reguła:

```text
niższy RuntimeId/UnitId = wcześniej wystawiona jednostka = zwycięzca
```

Nie wolno używać indeksu `BattleSimulation.Units`, ponieważ
`BattleSimulationFactory` grupuje jednostki gracza i przeciwnika podczas
budowania danych symulacji.

Kontrakt związku `RuntimeId` z kolejnością wystawienia powinien zostać:

- opisany przy metodzie porównującej priorytet;
- zabezpieczony testem;
- odizolowany w metodzie o nazwie odnoszącej się do kolejności wystawienia,
  zamiast traktować `UnitId` jako przypadkowy tie-break.

Jeżeli w przyszłości identyfikatory będą importowane lub odtwarzane bez
zachowania kolejności, należy dodać osobne, niemutowalne `DeploymentSequence`.

## Etap 5 — commit ruchów

Po zakończeniu rozstrzygania konfliktów:

1. Iteruj wyłącznie po zwycięskich intencjach.
2. Zmień logiczną pozycję każdej zwycięskiej jednostki.
3. Wyemituj jeden `UnitMoved` z pozycją źródłową i docelową.
4. Nie zmieniaj stanu przegranych.

Wszystkie docelowe heksy są wtedy unikalne, dlatego kolejność commitów nie może
zmieniać wyniku rozstrzygnięcia.

### Oddzielenie logiki od prezentacji

Logiczny `CurrentHex` powinien zostać zaktualizowany w fazie commit tego samego
ticka. `BattleView` powinien animować zdarzenie `UnitMoved` niezależnie od
logicznej pozycji.

`IsMoving`, `MovementDestination` i `MovementTimeRemaining` nie powinny
opóźniać:

- aktualizacji logicznej geometrii;
- targetowania innych jednostek;
- ewaluacji zasięgu w następnym ticku.

Jeżeli pola te pozostaną potrzebne prezentacji lub synchronizacji animacji,
nie mogą być częścią reguł occupancy i podejmowania decyzji przez symulację.
Preferowanym kierunkiem jest utrzymywanie czasu animacji wyłącznie w warstwie
prezentacji.

Po migracji głównej ścieżki należy również:

- usunąć `MovementResolver.AdvanceActiveMovements` z przebiegu ticka;
- przestać pomijać `IsMoving` w `BattleTickLoop` i `CombatResolver`;
- usunąć z `AttackPositionSelector` zgodność opartą na
  `target.MovementDestination`;
- budować occupancy wyłącznie z logicznych `CurrentHex`.

`StartUnitMovement` i `CompleteUnitMovement` należy usunąć po migracji wszystkich
produkcyjnych wywołań albo pozostawić tymczasowo wyłącznie jako kod
kompatybilności nieużywany przez główną symulację. Nie należy utrzymywać dwóch
równoległych modeli occupancy dłużej niż wymaga migracja testów.

## Etap 6 — re-ewaluacja w następnym ticku

Przykład dwóch jednostek melee i jednego pustego heksa pomiędzy nimi:

### Tick N

1. Obie jednostki zapisują intencję wejścia na środkowy heks.
2. Konflikt wygrywa jednostka wystawiona wcześniej.
3. Zwycięzca logicznie zajmuje środkowy heks.
4. Przegrany pozostaje w miejscu.

### Tick N+1

1. Przegrany ponownie ocenia swój aktualny cel.
2. Cel nadal żyje i jest aktualnym celem.
3. Nowa geometria wskazuje, że cel znajduje się w zasięgu.
4. Jednostka nie tworzy intencji ruchu.
5. Jeżeli cooldown na to pozwala, jednostka atakuje.

## Zakres zmian według plików

### `TargetSelector.cs`

- zachować obecną implementację `RetainCurrent` i walidację aktualnego celu;
- użyć centralnej reguły targetowalności;
- utrzymać globalny wybór dopiero po odrzuceniu aktualnego celu;
- zachować deterministyczne reguły wyboru nowego celu.

### `AttackPositionSelector.cs`

- użyć centralnej reguły targetowalności;
- po migracji commitu usunąć logikę pending `MovementDestination`;
- budować occupancy z `CurrentHex`, bez prezentacyjnego stanu ruchu.

### `BattleTickLoop.cs`

- zachować fazę targetowania jako właściciela mutacji `TargetUnitId`;
- przekazywać stabilne selekcje do ataku i ruchu;
- zachować drugą walidację po śmierciach i efektach;
- usunąć `AdvanceActiveMovements`;
- zapewnić, że następny tick widzi logicznie zatwierdzone pozycje.

### `MovementResolver.cs`

- rozdzielić collect, resolve i commit na osobne metody;
- wprowadzić `DesiredMoves`;
- grupować po docelowym heksie;
- stosować wyłącznie tie-break kolejności wystawienia;
- usunąć alternatywny pathfinding dla przegranych;
- usunąć specjalny konflikt wzajemny;
- usunąć tryb `updateUnitTargets` i fallback do samodzielnego targetowania;
- współdzielić ten sam resolver pomiędzy symulacją i debug preview.

### `BattleSimulation.cs`

- zapewnić atomowy, logiczny commit zwycięskich ruchów;
- rozdzielić logiczną pozycję od czasu animacji;
- zachować walidację unikalności i sąsiedztwa docelowych heksów.

### `CombatResolver.cs`

- użyć centralnej reguły targetowalności;
- po migracji nie blokować ataku przez prezentacyjny stan ruchu.

### `BattleView.cs`

- nadal animować `UnitMoved`;
- nie traktować czasu animacji jako czasu oczekiwania symulacji;
- upewnić się, że animacje kroków i ataków nie powodują wizualnego cofania modelu przy szybszych tickach;
- w razie potrzeby wykorzystać istniejącą kolejkę ruchów `UnitView`.

Kolejka `UnitView` ma obecnie stałą pojemność czterech kroków. Należy
przetestować szybkie kolejne ruchy oraz świadomie ustalić zachowanie po jej
przepełnieniu.

### Debug

- `BattleDebugOverlay.PlanMovementDestinations` powinien używać tego samego collect i resolve co właściwa symulacja;
- podgląd nie może ponownie targetować ani mutować symulacji;
- podgląd musi pokazywać wyłącznie zwycięskie intencje.

## Plan testów

### Targetowanie

Już pokryte i do zachowania:

- zachowanie żywego i osiągalnego celu mimo pojawienia się bliższego wroga;
- retargetowanie, gdy nie istnieje osiągalna pozycja ataku;
- deterministyczny wybór nowego celu według dystansu, HP i `UnitId`;
- użycie zachowanego celu przez pełny tick.

Do uzupełnienia:

- zachowanie celu, gdy inny wróg znajdzie się już w zasięgu;
- retargetowanie po śmierci celu;
- wyczyszczenie celu, gdy nie istnieje prawidłowa alternatywa;
- wspólna reguła targetowalności używana przez selektor, pozycję ataku i combat.

Test nietargetowalności innej niż śmierć i strona należy dodać dopiero wraz z
rzeczywistym modelem takiego stanu, nie z wyprzedzeniem.

### Collect

- dokładnie jeden krok na tick;
- brak intencji dla jednostki w zasięgu;
- brak mutacji pozycji, celu, ruchu, cooldownu i occupancy;
- brak zdarzeń w fazie collect;
- brak intencji na heks zajęty w snapshotcie początku fazy;
- wynik niezależny od kolejności iteracji jednostek.

### Konflikty

- jeden kandydat wykonuje ruch;
- dwóch kandydatów: wygrywa wcześniej wystawiony;
- trzech kandydatów: wygrywa wcześniej wystawiony;
- krótsza droga nie daje pierwszeństwa;
- strona jednostki nie daje pierwszeństwa;
- seed RNG nie wpływa na wynik;
- przegrany nie wybiera alternatywnego heksa;
- przegrany nie zmienia celu tylko dlatego, że przegrał konflikt.
- grupowanie jednostek według strony w `BattleSimulationFactory` nie zmienia
  priorytetu wynikającego z kolejności wystawienia.

### Commit i kolejne ticki

- wszystkie zwycięskie ruchy są widoczne logicznie po zakończeniu ticka;
- docelowe heksy pozostają unikalne;
- przegrani pozostają na swoich heksach;
- przypadek dwóch melee z jednym pustym heksem prowadzi do ataku przegranego w następnym ticku bez dodatkowego ruchu;
- `UnitMoved` jest emitowany tylko dla zwycięzców;
- debug preview i właściwy resolver zwracają ten sam zestaw ruchów.
- szybkie kolejne ruchy nie powodują cofnięcia modelu ani niejawnej utraty
  istotnego kroku po zapełnieniu kolejki `UnitView`.

### Regresje

- jednostki ranged nie poruszają się, gdy cel jest w zasięgu;
- pociski nadal podążają za swoim zapisanym celem zgodnie z obecnym kontraktem;
- jednoczesne ataki pozostają deterministyczne;
- śmierć celu między fazami nie powoduje ruchu w stronę martwej jednostki;
- zakończenie walki nadal czeka na aktywne pociski;
- wynik pełnej symulacji jest identyczny dla tego samego stanu i seeda.

## Wydajność mobilna

Implementacja nie powinna używać:

- LINQ ani `GroupBy`;
- nowych słowników i list na każdy tick;
- alokowanych enumeratorów;
- pełnej ścieżki przechowywanej osobno dla każdej jednostki;
- ponownego pathfindingu po przegraniu konfliktu.

`MovementResolver.Workspace` powinien utrzymywać słowniki, listy i bufory
o pojemności odpowiadającej maksymalnej liczbie jednostek. Po pierwszym
rozgrzaniu symulacja powinna generować `0 B` GC alloc na tick.

Do profilowania:

- czas `TargetSelector`;
- czas `MovementResolver` osobno dla collect, resolve i commit;
- liczba wywołań pathfindingu na tick;
- GC alloc na tick;
- maksymalny i średni czas ticka przy pełnej planszy;
- długość kolejki animacji `UnitView`.

## Kolejność wdrożenia

1. Dodać brakujące regresje dla już działającego `RetainCurrent`.
2. Ujednolicić podstawową regułę prawidłowego celu.
3. Uczynić selekcje przygotowane przez tick wymaganym wejściem collect ruchu.
4. Dodać testy czystości i wydzielić fazę collect.
5. Wydzielić resolve z mapą `WinnerByDestination`.
6. Zastąpić priorytet drogi priorytetem kolejności wystawienia.
7. Usunąć alternatywne kroki i specjalny konflikt wzajemny.
8. Wprowadzić natychmiastowy logiczny commit zwycięskich ruchów.
9. Usunąć zależności gameplayu od `IsMoving` i pending destination.
10. Dostosować `BattleView`, `UnitView` i debug preview.
11. Przepisać testy utrwalające opóźniony commit.
12. Uruchomić wąskie testy `TargetSelectorTests`, `MovementResolverTests`,
    `BattleTickLoopTests` i `BattleSimulationTests`.
13. Uruchomić cały zestaw EditMode w otwartym Unity Editorze.
14. Zweryfikować pełne walki w Play Mode.
15. Sprawdzić profil na konfiguracji z maksymalną liczbą jednostek.

## Kryteria ukończenia

Zmiana jest ukończona, gdy:

- jednostka nie zmienia prawidłowego, osiągalnego celu;
- zmiana celu następuje wyłącznie po śmierci lub utracie dostępności celu;
- wszystkie intencje ruchu są zbierane bez mutacji stanu;
- każdy konflikt jest rozstrzygany przed rozpoczęciem commitów;
- tie-break wynika wyłącznie z kolejności wystawienia;
- przegrany konfliktu zawsze czeka i nie szuka alternatywy;
- następny tick widzi nową logiczną geometrię;
- przykład dwóch jednostek melee zachowuje się zgodnie z opisanym scenariuszem;
- debug preview odpowiada wynikowi resolvera;
- testy EditMode przechodzą;
- profiler nie wykazuje alokacji na tick ani istotnej regresji czasu symulacji.

## Ryzyka

### Synchronizacja symulacji i animacji

Natychmiastowy logiczny commit może spowodować, że animacja pozostanie chwilowo
za stanem symulacji. Należy traktować to jako problem prezentacji i kontrolować
czas animacji lub kolejkę zdarzeń, bez ponownego blokowania logiki walki.

### Znaczenie `UnitId`

Tie-break jest poprawny tylko tak długo, jak `UnitId` odpowiada kolejności
wystawienia. Jeżeli ten kontrakt przestanie być gwarantowany, konieczne będzie
osobne `DeploymentSequence`.

### Dynamiczne efekty targetowania

Obecnie projekt nie posiada ogólnego modelu nietargetowalności. Centralna
reguła `CanBeTargeted` powinna być punktem rozszerzenia, ale nie należy
wprowadzać rozbudowanego systemu statusów w ramach tej zmiany.

### Zakres regresji

Zmiana logicznego commitu wpływa na większą liczbę testów niż sam resolver
ruchu. Szczególną uwagę należy poświęcić:

- atakom podczas animacji;
- zajętości heksów;
- debug overlay;
- orientacji modeli;
- kolejce ruchów `UnitView`;
- rozstrzyganiu pocisków i śmierci w tym samym ticku.
