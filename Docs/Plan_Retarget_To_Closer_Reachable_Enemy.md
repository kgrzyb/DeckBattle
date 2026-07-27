# Plan zmiany celu na bliższego osiągalnego przeciwnika

## Cel

Jednostka, która ma już prawidłowy cel, powinna ponownie oceniać sytuację i zmienić
cel, jeżeli inna wroga jednostka:

- jest osiągalna;
- ma dostępną pozycję, z której atakujący może ją zaatakować;
- wymaga ściśle mniejszej liczby kroków dojścia do pozycji ataku niż obecny cel.

Przy jednakowej długości drogi jednostka zachowuje obecny cel. Zmiana nie może
przerywać aktywnego kroku ruchu ani rozpoczętego windupu ataku.

## Przyjęty kontrakt funkcjonalny

### Znaczenie „bliżej”

Odległość jest mierzona przez `AttackPathResult.PathSteps`, czyli liczbę kroków
po najkrótszej aktualnie dostępnej ścieżce od `CurrentHex` do legalnej pozycji
ataku. Nie należy porównywać:

- prostego dystansu heksowego do przeciwnika;
- dystansu do heksa zajmowanego przez przeciwnika;
- długości drogi do samego celu bez uwzględnienia `AttackRange`;
- przewidywanego czasu ataku, HP celu ani czasu odnowienia ataku.

Dzięki temu jednostka ranged może uznać cel za bliski bez podchodzenia do niego,
jeżeli już znajduje się w zasięgu, a przeszkody i zajęte heksy są uwzględnione
przez istniejący pathfinding.

### Reguła przełączenia

Niech:

- `currentSteps` oznacza liczbę kroków do pozycji ataku obecnego celu;
- `bestSteps` oznacza najmniejszą liczbę kroków do pozycji ataku dowolnego
  prawidłowego, osiągalnego przeciwnika.

Zachowanie:

- `bestSteps < currentSteps` — zmiana na najlepszy bliższy cel;
- `bestSteps == currentSteps` — zachowanie obecnego celu;
- obecny cel jest martwy, nieprawidłowy albo nieosiągalny — wybór najlepszego
  osiągalnego celu;
- brak osiągalnego celu — wyczyszczenie `TargetUnitId`;
- obecny cel jest już w zasięgu (`currentSteps == 0`) — brak zmiany celu.

Wśród kilku nowych celów o tej samej minimalnej długości drogi pozostają obecne
deterministyczne tie-breaki: mniejszy dystans celu od pozycji ataku, niższe HP,
niższy `UnitId`, a na końcu porządek współrzędnych pozycji ataku.

### Bezpieczny moment zmiany

Obecne ograniczenia w `BattleTickLoop.RefreshTargets` pozostają:

- nie zmieniać celu, gdy `unit.IsMoving`;
- nie zmieniać celu w `UnitAttackPhase.Windup`.

Ponowna ocena może działać w `AcquireReload` i `Winddown`. Atak rozpoczęty
windupem pozostaje związany z `LockedAttackTargetUnitId`, a wystrzelony pocisk
zachowuje swój zapisany cel niezależnie od późniejszego retargetowania.

## Stan obecny

- `BattleTickLoop.RefreshTargets` wykonuje targetowanie przed i po rozstrzygnięciu
  ataków.
- `TargetSelector.TrySelectTargetOrRetainCurrent` zachowuje każdy prawidłowy
  i osiągalny obecny cel, nawet jeżeli inny przeciwnik ma znacznie krótszą drogę
  do pozycji ataku.
- `TargetSelector.TrySelectTargetByPath` już wykonuje BFS i zwraca przeciwnika
  osiągalnego najmniejszą liczbą kroków do pozycji ataku.
- `AttackPositionSelector.AttackPathResult.PathSteps` reprezentuje potrzebną
  metrykę.
- `BattleTickLoop` przekazuje przygotowane `TargetSelection` do
  `MovementResolver`, więc ruch nie powinien wykonywać drugiego wyboru celu.
- `UnitRuntimeState.SetTarget` resetuje stan pościgu tylko przy rzeczywistej
  zmianie `TargetUnitId`.

Zmiana dotyczy logiki symulacji. Nie wymaga zmian w widoku, animacji, prefabach,
scenach ani URP.

## Plan implementacji

### Etap 1 — testy nowego kontraktu selektora

W `TargetSelectorTests` zastąpić regresję
`SelectTargetOrRetainCurrent_KeepsReachableCurrentTarget_WhenAnotherEnemyIsCloser`
testem oczekującym przełączenia na cel z krótszą osiągalną drogą.

Dodać osobne przypadki:

1. Obecny cel ma drogę długości kilku kroków, a drugi przeciwnik jest już
   w zasięgu — wybierany jest drugi przeciwnik.
2. Oba cele są osiągalne, ale nowy ma ściśle krótszy `PathSteps` — następuje
   zmiana.
3. Oba cele wymagają tej samej liczby kroków — pozostaje obecny cel, nawet jeżeli
   standardowy tie-break HP lub `UnitId` wskazałby innego przeciwnika.
4. Pozornie bliski przeciwnik nie ma osiągalnej pozycji ataku — pozostaje
   osiągalny obecny cel.
5. Obecny cel jest nieosiągalny — wybierany jest najlepszy osiągalny przeciwnik.
6. Obecny cel jest już w zasięgu — pozostaje bez zmian.
7. Brak obecnego celu — zachowane są dotychczasowe reguły wyboru najbliższego
   osiągalnego celu i tie-breaki.

Testy powinny sprawdzać zarówno `Target`, jak i `AttackPath.PathSteps`,
`AttackPosition` oraz `NextStep`, żeby wykrywać rozjazd między wyborem celu
i ruchem.

### Etap 2 — jedna globalna selekcja BFS z preferencją obecnego celu przy remisie

Nie wykonywać osobnego pathfindingu dla obecnego celu, a następnie drugiego dla
pozostałych przeciwników. Zamiast tego rozszerzyć istniejące
`TrySelectTargetByPath` o opcjonalny `preferredTargetUnitId`.

Algorytm BFS już przetwarza planszę poziomami:

1. Poziom `0` oznacza atak z aktualnego heksa.
2. Kolejne poziomy oznaczają odpowiednio `1`, `2`, ... kroków do pozycji ataku.
3. Pierwszy poziom zawierający co najmniej jeden prawidłowy cel jest globalnym
   minimum `PathSteps`.
4. Jeżeli na tym poziomie występuje obecny cel, ma on pierwszeństwo przed innymi
   celami z tego samego poziomu.
5. Jeżeli obecnego celu nie ma na minimalnym poziomie, selektor używa obecnych
   tie-breaków i zwraca bliższego przeciwnika.

Preferencję obecnego celu trzeba uwzględnić w obu miejscach wyboru w obrębie
poziomu BFS:

- w `SelectTargetInAttackRange`, gdy wiele celów można zaatakować z tej samej
  pozycji;
- w `IsBetterEncounter`, gdy cel jest dostępny z kilku pozycji ataku albo różne
  pozycje wskazują różnych kandydatów.

Preferencja działa wyłącznie w obrębie tego samego poziomu BFS. Nie może pozwolić
obecnemu celowi z dłuższą drogą wygrać z celem znalezionym na wcześniejszym
poziomie.

### Etap 3 — czytelne API bez mylącego „retain always”

Obecna nazwa `TrySelectTargetOrRetainCurrent` opisuje kontrakt, który przestanie
obowiązywać. Zastąpić ją API wskazującym preferencję tylko przy remisie, np.:

```csharp
TrySelectTarget(
    BattleSimulation simulation,
    UnitRuntimeState attacker,
    Workspace workspace,
    int preferredTargetUnitId,
    out TargetSelection selection)
```

Istniejący overload bez `preferredTargetUnitId` powinien przekazywać
`NoTargetUnitId` i zachować zachowanie początkowego wyboru. Wywołania z
`BattleTickLoop.RefreshTargets` i `MovementResolver.PrepareTargetSelections`
powinny przekazywać `unit.TargetUnitId`.

Jeżeli stare metody `SelectTargetOrRetainCurrent*` nie mają innych konsumentów,
usunąć je zamiast pozostawiać publiczne API o nieprawdziwej nazwie. Przed
usunięciem potwierdzić wszystkie odwołania przez `rg`.

### Etap 4 — integracja z tickiem i ruchem

`BattleTickLoop.RefreshTargets` nadal odpowiada za:

- przygotowanie jednej `TargetSelection` na jednostkę;
- wywołanie `unit.SetTarget(selection.Target)`;
- przekazanie tej samej selekcji do `MovementResolver`.

Nie dodawać retargetowania w `MovementResolver.CollectIntents`. Resolver ruchu ma
wyłącznie wykonać przygotowaną decyzję. Samodzielny overload
`MovementResolver.ResolveMovement`, używany przez testy lub narzędzia, powinien
korzystać z tej samej nowej reguły w `PrepareTargetSelections`.

Zmiana celu przez `SetTarget` wyzeruje `EngagedTargetUnitId` i
`PursuitStepsUsed`. Jest to zamierzone: limit pościgu dotyczy poprzedniego celu,
a nowy cel otrzyma własny budżet dopiero po pierwszym zatwierdzonym ataku.

### Etap 5 — testy integracyjne cyklu walki

W `BattleTickLoopTests` dodać scenariusze:

1. Jednostka ma odległy cel, po zakończeniu kroku pojawia się bliższy osiągalny
   przeciwnik — w następnym bezpiecznym odświeżeniu zmienia cel i rusza zgodnie
   z `NextStep` nowej selekcji.
2. Bliższy przeciwnik pojawia się podczas aktywnego ruchu — cel nie zmienia się
   do zakończenia kroku.
3. Bliższy przeciwnik pojawia się podczas windupu — `TargetUnitId` i
   `LockedAttackTargetUnitId` pozostają stabilne, a atak trafia w zatwierdzony cel.
4. Po windupie lub w winddownie kolejny refresh może już wybrać ściśle bliższy
   cel.
5. Retargetowanie resetuje stan pościgu starego celu, ale nie zmienia celu
   istniejącego pocisku.
6. Przy remisie długości dróg kolejne ticki nie przełączają celu między
   przeciwnikami.

## Zakres zmian według plików

- `Assets/DeckBattle/Scripts/Battle/TargetSelector.cs`
  — preferencja obecnego celu tylko na minimalnym poziomie BFS i nowe API.
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`
  — użycie selekcji z `unit.TargetUnitId` jako preferowanym celem.
- `Assets/DeckBattle/Scripts/Battle/MovementResolver.cs`
  — ta sama reguła w samodzielnym przygotowaniu selekcji.
- `Assets/DeckBattle/Tests/EditMode/TargetSelectorTests.cs`
  — testy krótszej drogi, remisu, zasięgu i nieosiągalnego celu.
- `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`
  — bezpieczny moment zmiany i integracja z ruchem oraz atakiem.
- opcjonalnie `Assets/DeckBattle/Tests/EditMode/MovementResolverTests.cs`
  — regresja dla samodzielnego overloadu przygotowującego cele.

Nie przewiduje się zmian w:

- `AttackPositionSelector.cs`;
- `AttackCycleResolver.cs`;
- `UnitRuntimeState.cs`;
- `HexBoard.cs`;
- warstwie prezentacji.

## Wydajność mobilna

Implementacja powinna zachować jedno globalne przejście BFS na jednostkę podczas
odświeżenia celu. Nie należy:

- wykonywać pathfindingu osobno dla każdego przeciwnika;
- wykonywać dwóch pełnych wyszukiwań: dla obecnego i najlepszego celu;
- tworzyć list, słowników ani workspace'ów na tick;
- używać LINQ;
- przechowywać pełnej ścieżki osobno dla każdego kandydata.

Istniejący `TargetSelector.Workspace` ma pozostać reużywany. Dodatkowy koszt
preferencji celu powinien ograniczać się do porównań `UnitId` podczas już
wykonywanego BFS. Po rozgrzaniu cel to `0 B` GC alloc na tick.

Do profilowania:

- czas `BattleTickLoop.RefreshTargets`;
- czas i liczba wywołań `TargetSelector`;
- GC Alloc na tick;
- najgorszy czas ticka przy pełnej planszy i wielu osiągalnych przeciwnikach.

## Kolejność wdrożenia i weryfikacji

1. Dodać i zaktualizować testy `TargetSelectorTests`.
2. Rozszerzyć wewnętrzny wybór BFS o `preferredTargetUnitId`.
3. Dodać nowy overload selektora i usunąć mylące metody retain po sprawdzeniu
   odwołań.
4. Podłączyć nowe API w `BattleTickLoop` i `MovementResolver`.
5. Dodać testy integracyjne `BattleTickLoopTests`.
6. Uruchomić w otwartym Unity Editorze wąskie testy EditMode:
   `TargetSelectorTests`, `MovementResolverTests`, `BattleTickLoopTests`.
7. Uruchomić pełny zestaw EditMode z menu
   `DeckBattle > Tests > Run EditMode Tests`.
8. W Play Mode sprawdzić melee, ranged, cel poruszający się i ciasne układy
   z blokadą heksów.
9. Zweryfikować profilerem brak nowych alokacji i brak istotnej regresji czasu
   ticka.

## Kryteria akceptacji

- Jednostka zmienia osiągalny obecny cel, gdy inny przeciwnik ma ściśle krótszą
  drogę do legalnej pozycji ataku.
- Jednostka nie wybiera geometrycznie bliskiego przeciwnika, jeżeli nie może
  dojść do żadnej pozycji ataku.
- Przy równej liczbie kroków pozostaje obecny cel.
- Cel w zasięgu nie jest zastępowany innym celem.
- Aktywny krok ruchu i windup nie są przerywane.
- Ruch wykonuje `NextStep` odpowiadający nowo wybranemu celowi.
- Zmiana celu prawidłowo resetuje stan pościgu starego celu.
- Pociski i zatwierdzone ataki zachowują własny cel.
- Wynik pozostaje deterministyczny.
- Symulacja nie generuje nowych alokacji w hot path.
- Wszystkie testy EditMode przechodzą.

## Ryzyka i decyzje projektowe

### Chwilowe blokady dynamiczne

Jednostki oraz ich zarezerwowane cele ruchu są traktowane jako przeszkody. Może to
chwilowo wydłużyć drogę do obecnego celu i spowodować retargetowanie. Reguła
ścisłej poprawy oraz brak zmiany podczas aktywnego ruchu ograniczają oscylację.
Jeżeli testy Play Mode wykażą częste przełączanie wskutek pojedynczego ticka
blokady, osobnym rozszerzeniem może być próg poprawy większy niż jeden krok lub
krótki cooldown retargetowania. Nie należy dodawać go bez zmierzonej potrzeby.

### Retargetowanie po zatwierdzonym ataku

Drugi `RefreshTargets` odbywa się po rozstrzygnięciu ataku. W fazie `Winddown`
jednostka może więc wybrać bliższy cel, podczas gdy pocisk lub efekt poprzedniego
ataku nadal dotyczy starego celu. Jest to spójne z rozdzieleniem symulacji ataku
od bieżącego celu jednostki, ale wymaga testu regresyjnego.

### Zgodność z dotychczasowym stabilnym targetowaniem

Dotychczasowy kontrakt „zachowaj każdy osiągalny cel” zostaje celowo zastąpiony
kontraktem „zachowaj cel tylko wtedy, gdy żaden przeciwnik nie ma krótszej drogi”.
Plany i testy opisujące bezwarunkowe `RetainCurrent` trzeba podczas implementacji
zaktualizować, aby nie utrwalały sprzecznego zachowania.
