# Plan poprawy targetowania i rozstrzygania ruchu w symulacji walki

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

### Targetowanie

Metoda `TargetSelector.TrySelectTargetOrRetainCurrent` nie zachowuje aktualnego celu.
Wykonuje ten sam globalny wybór co `TrySelectTarget`, dlatego jednostka może porzucić
żywy i osiągalny cel, gdy pojawi się bliższy albo korzystniejszy przeciwnik.

Dotyczy:

- `Assets/DeckBattle/Scripts/Battle/TargetSelector.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`;
- `Assets/DeckBattle/Scripts/Battle/MovementResolver.cs`.

### Rozstrzyganie ruchu

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

### Commit logicznego ruchu

`MovementResolver` wywołuje `BattleSimulation.StartUnitMovement`, ale logiczne
`CurrentHex` zostaje zmienione dopiero po upływie `MovementStepDuration`.

Domyślny tick walki trwa `0.35 s`, a krok ruchu `0.4 s`. W rezultacie nowa
geometria może być dostępna dopiero po więcej niż jednym ticku. Jest to
niezgodne z wymaganiem, aby w następnym ticku przegrany konfliktu mógł ponownie
ocenić zasięg i zaatakować jednostkę, która weszła na sporny heks.

### Testy

Istniejące testy zawierają sprzeczne kontrakty:

- część testów oczekuje zachowania aktualnego, osiągalnego celu;
- test selektora nadal oczekuje przełączenia na bliższy cel;
- część testów ruchu utrwala priorytet długości drogi lub opóźniony logiczny commit.

Testy muszą zostać uporządkowane razem z implementacją.

## Docelowy przebieg ticka

1. Zakończyć logicznie wcześniej rozpoczęte efekty i rozstrzygnąć pociski.
2. Dla każdej aktywnej jednostki zweryfikować aktualny cel.
3. Zachować aktualny cel, jeżeli nadal jest prawidłowy i osiągalny.
4. Wybrać nowy cel wyłącznie dla jednostek, których aktualny cel jest nieważny lub niedostępny.
5. Rozstrzygnąć ataki.
6. Ponownie zweryfikować cele unieważnione przez śmierci i efekty ataków.
7. Zebrać intencje ruchu bez mutowania stanu symulacji.
8. Wykryć i rozstrzygnąć konflikty o docelowe heksy.
9. Zatwierdzić wszystkie zwycięskie ruchy.
10. Sprawdzić warunki zakończenia walki.

## Etap 1 — jednoznaczny kontrakt celu

### Walidacja aktualnego celu

Aktualny cel pozostaje przypisany, jeżeli:

- atakująca jednostka żyje;
- cel żyje;
- cel należy do przeciwnej strony;
- cel może być targetowany według aktualnych reguł i efektów;
- istnieje osiągalna pozycja, z której atakujący może zaatakować cel, albo cel znajduje się już w zasięgu.

Walidacja powinna korzystać z `AttackPositionSelector`, aby razem z decyzją
o zachowaniu celu otrzymać aktualny `AttackPathResult`.

### Retargetowanie

Globalny wybór nowego celu jest wykonywany dopiero wtedy, gdy aktualny cel:

- umarł;
- przestał być wrogi;
- stał się nietargetowalny;
- nie posiada żadnej osiągalnej pozycji ataku.

Brak dostępnego nowego celu powoduje wyczyszczenie `TargetUnitId`.

### Centralna reguła targetowalności

Należy wprowadzić jeden punkt decyzyjny, na przykład:

```csharp
TargetingRules.CanBeTargeted(attacker, candidate)
```

Początkowo reguła może sprawdzać życie i stronę jednostki. Późniejsze efekty,
takie jak niewidzialność, wyłączenie z walki lub chwilowa nietargetowalność,
powinny rozszerzać tę regułę bez duplikowania warunków w resolverach.

### Separacja odpowiedzialności

`MovementResolver` nie powinien:

- wybierać nowego celu;
- zmieniać `TargetUnitId`;
- ponownie obliczać targetowania podczas rozstrzygania konfliktu.

`BattleTickLoop` powinien przygotować stabilne `TargetSelection` przed fazą
ruchu. Mutacja celu następuje w osobnej fazie targetowania, nie podczas
zbierania intencji ruchu.

## Etap 2 — collect movement intents

Faza musi używać jednego snapshotu pozycji i zajętości z początku planowania.
Nie może zmieniać żadnego stanu jednostek ani planszy.

W `MovementResolver.Workspace` należy utrzymywać i ponownie wykorzystywać:

```csharp
Dictionary<UnitRuntimeState, HexCoord> DesiredMoves
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
Dictionary<HexCoord, UnitRuntimeState> WinnerByDestination
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

- zaimplementować rzeczywiste zachowanie `RetainCurrent`;
- dodać walidację aktualnego celu;
- użyć centralnej reguły targetowalności;
- uruchamiać globalny wybór dopiero po odrzuceniu aktualnego celu;
- zachować deterministyczne reguły wyboru nowego celu.

### `BattleTickLoop.cs`

- uczynić fazę targetowania jednoznacznym właścicielem mutacji `TargetUnitId`;
- przekazywać stabilne selekcje do ataku i ruchu;
- zachować drugą walidację po śmierciach i efektach;
- zapewnić, że następny tick widzi logicznie zatwierdzone pozycje.

### `MovementResolver.cs`

- rozdzielić collect, resolve i commit na osobne metody;
- wprowadzić `DesiredMoves`;
- grupować po docelowym heksie;
- stosować wyłącznie tie-break kolejności wystawienia;
- usunąć alternatywny pathfinding dla przegranych;
- usunąć specjalny konflikt wzajemny;
- współdzielić ten sam resolver pomiędzy symulacją i debug preview.

### `BattleSimulation.cs`

- zapewnić atomowy, logiczny commit zwycięskich ruchów;
- rozdzielić logiczną pozycję od czasu animacji;
- zachować walidację unikalności i sąsiedztwa docelowych heksów.

### `BattleView.cs`

- nadal animować `UnitMoved`;
- nie traktować czasu animacji jako czasu oczekiwania symulacji;
- upewnić się, że animacje kroków i ataków nie powodują wizualnego cofania modelu przy szybszych tickach;
- w razie potrzeby wykorzystać istniejącą kolejkę ruchów `UnitView`.

### Debug

- `BattleDebugOverlay.PlanMovementDestinations` powinien używać tego samego collect i resolve co właściwa symulacja;
- podgląd nie może ponownie targetować ani mutować symulacji;
- podgląd musi pokazywać wyłącznie zwycięskie intencje.

## Plan testów

### Targetowanie

- zachowanie żywego i osiągalnego celu mimo pojawienia się bliższego wroga;
- zachowanie celu, gdy inny wróg znajdzie się już w zasięgu;
- retargetowanie po śmierci celu;
- retargetowanie, gdy nie istnieje osiągalna pozycja ataku;
- retargetowanie, gdy cel staje się nietargetowalny;
- wyczyszczenie celu, gdy nie istnieje prawidłowa alternatywa;
- deterministyczny wybór nowego celu przy remisie.

### Collect

- dokładnie jeden krok na tick;
- brak intencji dla jednostki w zasięgu;
- brak mutacji pozycji, celu, ruchu, cooldownu i occupancy;
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

### Commit i kolejne ticki

- wszystkie zwycięskie ruchy są widoczne logicznie po zakończeniu ticka;
- docelowe heksy pozostają unikalne;
- przegrani pozostają na swoich heksach;
- przypadek dwóch melee z jednym pustym heksem prowadzi do ataku przegranego w następnym ticku bez dodatkowego ruchu;
- `UnitMoved` jest emitowany tylko dla zwycięzców;
- debug preview i właściwy resolver zwracają ten sam zestaw ruchów.

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

1. Uporządkować i ujednolicić testy kontraktu targetowania.
2. Zaimplementować zachowanie aktualnego celu i retargetowanie.
3. Dodać testy czystości fazy collect.
4. Przebudować `MovementResolver` na collect, resolve i commit.
5. Usunąć alternatywne kroki i specjalny konflikt wzajemny.
6. Ustalić tie-break według globalnej kolejności wystawienia.
7. Oddzielić logiczny commit pozycji od czasu animacji.
8. Dostosować `BattleView` i debug preview.
9. Uruchomić wąskie testy `TargetSelectorTests`, `MovementResolverTests` i `BattleTickLoopTests`.
10. Uruchomić cały zestaw EditMode.
11. Zweryfikować pełne walki w Play Mode.
12. Sprawdzić profil na konfiguracji z maksymalną liczbą jednostek.

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
