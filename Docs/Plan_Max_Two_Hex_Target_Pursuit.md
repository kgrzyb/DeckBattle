# Plan ograniczenia pościgu za celem do dwóch heksów

## Cel

Jednostka może zbliżać się do nowego celu do chwili pierwszego wykonanego ataku.
Po nawiązaniu walki może podążyć za tym samym celem najwyżej o dwa skutecznie
zatwierdzone kroki. Po wykorzystaniu limitu pozostaje na miejscu, dopóki:

- bieżący cel ponownie nie znajdzie się w zasięgu;
- bieżący cel nie umrze albo nie przestanie być prawidłowy lub osiągalny;
- jednostka nie otrzyma nowego celu zgodnie z istniejącymi regułami targetowania.

Limit jest liczony osobno dla każdej jednostki i jej bieżącego celu. Nie jest to
limit całkowitego ruchu jednostki ani promień od miejsca wystawienia.

## Przyjęty kontrakt funkcjonalny

### Co oznacza pościg

Początkowe dojście do przeciwnika nie zużywa limitu. Stan pościgu rozpoczyna się,
gdy atak jednostki zostanie zatwierdzony w `AttackCycleResolver` dla bieżącego
celu. Od tego momentu każdy zaakceptowany krok w stronę tego samego celu zużywa
jeden z dwóch kroków pościgu.

Przyjęcie momentu `AttackFired`, a nie trafienia, daje jeden kontrakt dla melee i
ranged. Pocisk może dolecieć później albo chybić przez zmianę stanu celu, ale
jednostka faktycznie podjęła walkę z tym przeciwnikiem.

### Zachowanie po wykorzystaniu dwóch kroków

- Jednostka zachowuje `TargetUnitId`; limit pościgu nie jest powodem do
  retargetowania.
- `TargetSelector` nadal waliduje cel i przygotowuje aktualną pozycję ataku.
- `MovementResolver` nie tworzy trzeciej intencji ruchu dla tego celu.
- Jeżeli cel sam wejdzie w zasięg, jednostka może normalnie zaatakować.
- Każdy kolejny zatwierdzony atak na ten sam cel rozpoczyna nowy budżet dwóch
  kroków pościgu.
- Śmierć, niedostępność lub zmiana celu zeruje stan pościgu. Nowy cel wymaga
  najpierw ataku, zanim jego ruch zacznie zużywać limit.

Takie zachowanie zachowuje obecny kontrakt stabilnego celu i nie powoduje
natychmiastowego ponownego wybrania tego samego przeciwnika z odnowionym limitem.

### Co dokładnie zwiększa licznik

Licznik rośnie wyłącznie dla zwycięskiej intencji zatwierdzonej w fazie commit
`MovementResolver`.

Nie zwiększają go:

- samo znalezienie ścieżki;
- przegranie konfliktu o heks;
- oczekiwanie podczas aktywnego kroku;
- ruch odrzucony przez occupancy albo walidację;
- animacja widoku;
- ruch wykonywany bez prawidłowego bieżącego celu.

W obecnym modelu ruchu krok należy naliczyć po udanym
`BattleSimulation.StartUnitMovement`. Jeżeli zostanie wdrożony plan
natychmiastowego logicznego commitu, naliczanie trzeba przenieść razem z commitem,
bez zmiany kontraktu.

## Stan obecny

- `BattleTickLoop.RefreshTargets` zachowuje osiągalny `TargetUnitId`.
- `AttackCycleResolver.ResolveCommittedFires` jest wspólnym punktem zatwierdzenia
  ataku melee i ranged.
- `MovementResolver` zbiera intencje, rozstrzyga konflikty i zatwierdza wygrane
  kroki w `CommitWinners`.
- `UnitRuntimeState` nie przechowuje informacji, czy jednostka zaatakowała
  bieżący cel ani ilu kroków pościgu użyła.
- `BattleRuntimeTuning` nie zawiera limitu pościgu.

Zmiana nie wymaga modyfikacji pathfindingu, `HexBoard`, widoku jednostki ani
formatu zdarzenia `UnitMoved`.

## Plan implementacji

### Etap 1 — stan runtime i konfiguracja

W `BattleRuntimeTuning` dodać:

```csharp
public readonly int MaxPursuitStepsAfterAttack;
```

Domyślna wartość wynosi `2`. Parametr należy dodać na końcu konstruktora z
wartością domyślną, aby zachować zgodność istniejących wywołań i testów.
Wartość ujemną należy normalizować do `0`.

W `UnitRuntimeState` dodać stan bez dodatkowych kolekcji:

```csharp
public int EngagedTargetUnitId;
public int PursuitStepsUsed;
```

Znaczenie:

- `EngagedTargetUnitId == NoTargetUnitId` — jednostka jeszcze nie wykonała
  zatwierdzonego ataku na bieżący cel;
- zgodność `EngagedTargetUnitId` z `TargetUnitId` — aktywny limit pościgu;
- `PursuitStepsUsed` — liczba zatwierdzonych kroków po ostatnim ataku na ten cel.

Dodać małe, jawne metody domenowe:

- `MarkTargetEngaged(int targetUnitId)` — zapisuje cel i zeruje licznik;
- `RecordPursuitStep(int targetUnitId)` — zwiększa licznik tylko dla
  zaangażowanego bieżącego celu;
- `ResetPursuit()` — czyści oba pola;
- `CanPursueTarget(int targetUnitId, int maxSteps)` — zwraca `true` przed
  pierwszym atakiem lub gdy licznik jest mniejszy od limitu.

`SetTarget` powinno resetować pościg tylko wtedy, gdy identyfikator celu faktycznie
się zmienił. Jest to konieczne, ponieważ `BattleTickLoop.RefreshTargets` wywołuje
`SetTarget` także przy ponownym zatwierdzeniu tego samego celu. `ClearTarget`,
`ResetForBattle` i pokonanie jednostki muszą zerować stan pościgu.

### Etap 2 — rozpoczęcie lub odnowienie budżetu po ataku

W `AttackCycleResolver.ResolveCommittedFires`, po przyjęciu ataku do commitu i
przed rozdzieleniem ścieżek melee/ranged, wywołać:

```csharp
attacker.MarkTargetEngaged(target.UnitId);
```

Nie oznaczać celu:

- przy rozpoczęciu windupu;
- po anulowanym windupie;
- przy samym wyborze celu;
- dopiero po trafieniu pocisku.

Dzięki temu anulowany atak nie uruchamia limitu, a melee i ranged mają identyczną
regułę.

### Etap 3 — blokada trzeciej intencji ruchu

W `MovementResolver.CollectIntents`, po potwierdzeniu prawidłowej selekcji celu,
ale przed dodaniem `MovementIntent`, sprawdzić:

```csharp
unit.CanPursueTarget(
    selection.Target.UnitId,
    simulation.Tuning.MaxPursuitStepsAfterAttack)
```

Brak budżetu oznacza pominięcie intencji. Nie należy:

- czyścić `TargetUnitId`;
- uruchamiać globalnego wyboru nowego celu;
- zmieniać `AttackPathResult`;
- oznaczać jednostki jako poruszającej się;
- emitować nowego zdarzenia tylko po to, aby poinformować o zatrzymaniu.

Sprawdzenie musi działać zarówno w produkcyjnym ticku, jak i w
`PlanMovementDestinations`, aby debug preview nie pokazywał ruchu, którego
symulacja nie wykona.

### Etap 4 — naliczenie wyłącznie zatwierdzonego kroku

W `MovementResolver.CommitWinners` po udanym rozpoczęciu ruchu wywołać:

```csharp
winner.Unit.RecordPursuitStep(winner.Unit.TargetUnitId);
```

Licznik nie może być zmieniany w `CollectIntents` ani `ResolveConflicts`, ponieważ
obie fazy powinny pozostać czyste względem stanu jednostek. Przegrany konfliktu
nie zużywa budżetu.

Jeżeli commit ruchu zostanie później przeniesiony z `StartUnitMovement` do
natychmiastowej zmiany `CurrentHex`, wywołanie należy utrzymać dokładnie przy
zaakceptowanym logicznym kroku.

### Etap 5 — debug i diagnostyka

Opcjonalnie rozszerzyć `BattleDebugSnapshot` lub `BattleDebugOverlay` o zapis:

```text
pursuit=<used>/<max>, engagedTarget=<id>
```

Nie jest to wymagane do działania funkcji. Warto dodać tę informację tylko wtedy,
gdy mieści się w istniejącym, aktualizowanym bez zbędnych alokacji widoku
diagnostycznym.

## Zakres zmian według plików

- `Assets/DeckBattle/Scripts/Battle/BattleRuntimeTuning.cs`
  — globalny, domyślny limit dwóch kroków.
- `Assets/DeckBattle/Scripts/Battle/UnitRuntimeState.cs`
  — stan i operacje domenowe pościgu.
- `Assets/DeckBattle/Scripts/Battle/AttackCycleResolver.cs`
  — rozpoczęcie lub odnowienie budżetu po zatwierdzonym ataku.
- `Assets/DeckBattle/Scripts/Battle/MovementResolver.cs`
  — blokada trzeciej intencji oraz naliczenie zwycięskiego kroku.
- `Assets/DeckBattle/Tests/EditMode/BattleRuntimeTuningTests.cs`
  — domyślna wartość i normalizacja konfiguracji.
- `Assets/DeckBattle/Tests/EditMode/BattleSimulationTests.cs`
  — inicjalizacja, zmiana celu i reset stanu.
- `Assets/DeckBattle/Tests/EditMode/AttackCycleResolverTests.cs`
  — rozpoczęcie budżetu tylko dla zatwierdzonego ataku.
- `Assets/DeckBattle/Tests/EditMode/MovementResolverTests.cs`
  — limit, konflikty i czystość planowania.
- `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`
  — scenariusz integracyjny pełnego pościgu.

`TargetSelector.cs` nie powinien wymagać zmiany. Zachowanie aktualnego celu jest
celowe i zapobiega obchodzeniu limitu przez ponowne targetowanie.

## Plan testów

### `UnitRuntimeState`

- Nowa jednostka ma pusty `EngagedTargetUnitId` i `PursuitStepsUsed == 0`.
- Ponowne `SetTarget` z tym samym celem nie resetuje wykorzystanych kroków.
- `SetTarget` z innym celem zeruje stan pościgu.
- `ClearTarget` i `ResetForBattle` zerują stan pościgu.
- `MarkTargetEngaged` zeruje licznik także po kolejnym ataku na ten sam cel.

### `AttackCycleResolver`

- Zatwierdzony atak melee oznacza cel jako zaangażowany.
- Zatwierdzone wystrzelenie pocisku ranged robi to samo bez oczekiwania na hit.
- Rozpoczęty, ale anulowany windup nie uruchamia limitu.
- Kolejny zatwierdzony atak na ten sam cel zeruje wykorzystane kroki.

### `MovementResolver`

- Przed pierwszym atakiem jednostka może przejść więcej niż dwa heksy do celu.
- Po ataku wykonuje pierwszy i drugi krok pościgu.
- Nie tworzy trzeciej intencji dla tego samego celu.
- Jednostka z wykorzystanym limitem nadal nie rusza się po kolejnych tickach.
- Przegrany konflikt o heks nie zwiększa licznika.
- `PlanMovementDestinations` uwzględnia limit, ale nie zmienia licznika.
- Limit `0` blokuje pościg od razu po ataku, ale nie blokuje początkowego dojścia.
- Jednostka może atakować cel, który sam wrócił w zasięg.

### Integracja w `BattleTickLoop`

Scenariusz powinien zawierać jednostkę A, która atakuje B, podczas gdy B porusza
się w stronę innego celu:

1. A zatwierdza atak na B.
2. B wychodzi z zasięgu.
3. A wykonuje dwa zaakceptowane kroki.
4. A nie wykonuje trzeciego kroku i zachowuje B jako cel.
5. B wraca w zasięg.
6. A może ponownie zaatakować B.
7. Po tym ataku A otrzymuje nowy budżet dwóch kroków.

Test powinien sprawdzać `TargetUnitId`, `PursuitStepsUsed`, liczbę zdarzeń
`UnitMoved` i brak trzeciego ruchu. Należy użyć kontrolowanych cooldownów oraz
ticków, aby wynik nie zależał od animacji.

### Regresje

- Jednostki ranged nadal stoją, gdy cel pozostaje w zasięgu.
- Początkowe pojedynki z dużej odległości nadal dochodzą do pierwszego ataku.
- Śmierć celu nadal powoduje wybór nowego celu.
- Ruch podczas winddownu pozostaje dozwolony, ale respektuje limit.
- Deterministyczny tie-break konfliktu o heks pozostaje bez zmian.
- Brak nowych alokacji na tick.

## Kolejność wdrożenia i weryfikacji

1. Dodać testy stanu runtime oraz konfiguracji.
2. Dodać pola i metody w `UnitRuntimeState` oraz parametr tuningu.
3. Dodać testy rozpoczęcia budżetu przez zatwierdzony atak.
4. Podłączyć `AttackCycleResolver`.
5. Dodać testy dwóch zwycięskich kroków i zablokowanego trzeciego.
6. Podłączyć kontrolę i naliczanie w `MovementResolver`.
7. Dodać scenariusz integracyjny `BattleTickLoop`.
8. Uruchomić w Unity EditMode kolejno wąskie zestawy:
   `BattleRuntimeTuningTests`, `BattleSimulationTests`,
   `AttackCycleResolverTests`, `MovementResolverTests`, `BattleTickLoopTests`.
9. Uruchomić pełny zestaw EditMode z menu
   `DeckBattle > Tests > Run EditMode Tests`.
10. Sprawdzić w profilerze brak GC Alloc w `BattleTickLoop.Tick`,
    `AttackCycleResolver.Resolve` i `MovementResolver.ResolveMovement`.

## Kryteria akceptacji

- Jednostka może bez limitu dojść do pierwszego ataku na nowy cel.
- Po zatwierdzonym ataku przesuwa się za tym samym celem maksymalnie o dwa heksy.
- Trzecia intencja nie powstaje, a cel nie jest automatycznie zmieniany.
- Przegrany konflikt nie zużywa kroku.
- Ponowny zatwierdzony atak odnawia budżet dwóch kroków.
- Zmiana lub utrata celu czyści stary stan pościgu.
- Preview ruchu i właściwa symulacja respektują tę samą regułę.
- Wynik pozostaje deterministyczny i bezalokacyjny w hot path.
- Wszystkie testy EditMode przechodzą.

## Ryzyka i decyzje projektowe

### Znaczenie „pościgu”

Plan rozróżnia początkowe podejście od pościgu po pierwszym ataku. Gdyby limit
miał obejmować także dojście do pierwszego ataku, odległe jednostki mogłyby
zatrzymać się poza zasięgiem i nigdy nie zakończyć walki. Taka alternatywa wymaga
osobnej reguły remisu, powrotu na pozycję albo retargetowania.

### Stabilny cel po zatrzymaniu

Jednostka celowo zachowuje przeciwnika po wykorzystaniu limitu. Automatyczne
wyczyszczenie celu pozwoliłoby selektorowi natychmiast wybrać go ponownie i
obejść limit. Jeżeli projekt ma wymagać przełączenia na bliższego przeciwnika,
trzeba osobno zaprojektować pamięć odrzuconych celów i warunek jej czyszczenia.

### Zależność od planowanej przebudowy ruchu

Istniejący plan rozstrzygania ruchu przewiduje natychmiastowy logiczny commit.
Ograniczenie pościgu jest zgodne z obecną i docelową architekturą, o ile licznik
jest zwiększany przy rzeczywiście zatwierdzonym kroku, a nie podczas collect.
