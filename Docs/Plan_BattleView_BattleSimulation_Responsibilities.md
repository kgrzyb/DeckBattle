# Plan podziału odpowiedzialności BattleView i BattleSimulation

Status: propozycja implementacyjna  
Data aktualizacji: 2026-07-30  
Zakres: runtime walki, prezentacja walki, integracja z `BattleController`

## 1. Cel

Celem refaktoru jest rozdzielenie:

- deterministycznej logiki walki;
- wykonywania symulacji w czasie rzeczywistym;
- prezentacji stanu i zdarzeń w Unity;
- orkiestracji całego meczu.

Po zakończeniu prac:

- `BattleSimulation` przechowuje wyłącznie stan i reguły walki;
- `BattleTickLoop` wykonuje jeden deterministyczny krok konkretnej symulacji;
- `BattleCombatRunner` zarządza czasem klatkowym, accumulatoriem i limitem ticków;
- `BattleView` prezentuje snapshot początkowy oraz `BattleEvent`;
- `BattleController` uruchamia walkę i aplikuje jej wynik do `BattleState`;
- ścieżki realtime i synchronous dają identyczny wynik dla tych samych danych;
- stabilne tickowanie i prezentacja nie generują alokacji na każdą klatkę.

Refaktor nie powinien zmieniać:

- zasad walki;
- kolejności rozwiązywania akcji;
- deterministyczności;
- wyniku istniejących scenariuszy;
- produkcyjnych assetów, prefabów ani ich GUID-ów.

## 2. Problemy obecnej implementacji

### 2.1. BattleView pełni kilka niezależnych ról

Obecny `BattleView`:

- może stworzyć `BattleSimulation`;
- tworzy `BattleTickLoop`;
- posiada accumulator czasu i liczniki ticków;
- pilnuje `MaxTicksPerFrame` i `MaxCombatTicks`;
- przechowuje `BattleEventQueue`;
- interpretuje zdarzenia;
- tworzy i usuwa `UnitView`;
- zarządza animacjami, VFX, overlayami i pociskami;
- utrzymuje pule efektów i pocisków;
- zawiera samodzielny tryb startowy oparty o `initialUnits`.

Klasa prezentacyjna odpowiada więc również za wykonanie symulacji i część jej
konfiguracji.

### 2.2. BattleView odczytuje mutowalny stan po wykonaniu ticka

Podczas obsługi zdarzeń widok ponownie odpytuje `BattleSimulation`, między
innymi o:

- bieżące pozycje jednostek;
- target;
- definicję i parametry pocisku;
- statusy;
- maksymalne HP i manę;
- czas ruchu.

Stan może być już stanem końcowym całego ticka, a nie stanem z momentu
wygenerowania konkretnego zdarzenia. Utrudnia to replay, testowanie kolejności
zdarzeń oraz niezależną prezentację.

### 2.3. Dane mechaniczne i wizualne są połączone

`UnitDefinition` i `ProjectileDefinition` zawierają jednocześnie:

- statystyki wykorzystywane przez symulację;
- referencje do `UnitView` i `ProjectileView`;
- wysokości i inne ustawienia prezentacji.

Przez to czysta symulacja pośrednio zależy od danych Unity przeznaczonych dla
widoku.

### 2.4. BattleController i BattleView współdzielą własność UnitView

Obie klasy utrzymują własne listy i słowniki widoków. Przed walką controller
przekazuje ich własność do widoku, a po walce próbuje ją odzyskać. Zwiększa to
ryzyko:

- duplikatów;
- zniszczenia nadal używanego widoku;
- pozostawionych overlayów;
- niejednoznacznego lifetime obiektów.

### 2.5. Realtime i synchronous posiadają osobne pętle wykonania

Realtime jest obecnie napędzany przez `BattleView`, a synchronous przez
`BattleSimulationCombatService`. Obie ścieżki muszą używać identycznych:

- danych wejściowych;
- runtime tuningu;
- wartości fixed tick;
- limitów;
- reguł zakończenia.

## 3. Docelowa architektura

```text
BattleController
    |
    | tworzy BattleSimulation i rozpoczyna combat
    v
BattleCombatRunner
    |
    | fixed tick
    v
BattleTickLoop ------> BattleSimulation
    |
    | BattleEventQueue
    v
BattleView
    |
    +--> UnitViewRegistry / UnitPresenter
    +--> ProjectilePresenter
    +--> EffectPresenter
    +--> Status overlay i VFX

Po zakończeniu:
BattleCombatRunner -> wynik -> BattleController -> BattleState
```

### 3.1. BattleSimulation

Odpowiada za:

- deterministyczny stan walki;
- jednostki, pociski i planszę;
- gameplay RNG;
- czas symulacji;
- mutacje wykonywane przez resolvery;
- wynik walki.

Nie zna:

- `Time.deltaTime`;
- liczby ticków na klatkę;
- coroutine;
- prefabów i presenterów;
- animatorów, VFX, UI ani world position.

### 3.2. BattleTickLoop

Odpowiada za:

- dokładnie jeden krok konkretnej symulacji;
- kolejność resolverów;
- prealokowane workspace’y;
- wypełnienie `BattleEventQueue`;
- wykrycie końca walki w ramach ticka.

### 3.3. BattleCombatRunner

Nowy `MonoBehaviour` odpowiada za:

- przełożenie frame time na fixed tick;
- accumulator;
- `MaxTicksPerFrame`;
- `MaxCombatTicks`;
- licznik wykonanych ticków;
- start, stop i restart realtime combat;
- przekazanie eventów do prezentacji;
- publikację końcowego `BattleRunResult`;
- opcjonalne snapshoty debugowe.

Runner nie interpretuje eventów i nie tworzy obiektów prezentacyjnych.

### 3.4. BattleView

Odpowiada za:

- związanie początkowego snapshotu z prezentacją;
- przetwarzanie `BattleEvent`;
- animacje ruchu, ataku, obrażeń, śmierci i speciali;
- synchronizację HP, many i statusów;
- prezentację pocisków;
- VFX oraz pooling;
- wyczyszczenie prezentacji.

Nie tworzy i nie tickuje symulacji.

### 3.5. BattleController

Odpowiada za:

- przebieg faz meczu;
- utworzenie `BattleSimulation`;
- uruchomienie runnera;
- odebranie wyniku;
- zastosowanie wyniku do `BattleState`;
- rozliczenie rundy.

## 4. Zasady implementacji

- Każdy etap musi osobno kompilować się i przechodzić właściwe testy.
- Najpierw przenosimy kod bez zmiany zachowania, potem zmieniamy kontrakty.
- Nie łączymy refaktoru z balansem lub zmianami mechaniki.
- Nie dodajemy LINQ, closure ani alokacji w hot path.
- Bufory zdarzeń i kolekcje runnera są reużywane.
- Nie przenosimy ani nie zmieniamy nazw assetów bez potrzeby.
- Nie usuwamy legacy API, dopóki wszystkie call-site’y nie zostaną zmigrowane.

## 5. Etapy implementacji

## Etap 0 — testy bazowe i kontrakt deterministyczności

### Zakres

Uruchomić i zachować wyniki:

- `BattleSimulationTests`;
- `BattleTickLoopTests`;
- `BattleSimulationCombatServiceTests`;
- `BattleSimulationResultApplierTests`;
- `BattleViewFacingTests`;
- `UnitPrefabSourceTests`.

Dodać test zgodności realtime/synchronous dla ustalonego scenariusza:

- ten sam seed;
- te same jednostki i tuning;
- ten sam fixed tick;
- porównanie zwycięzcy;
- porównanie liczby ticków;
- porównanie HP, pozycji i stanu końcowego jednostek;
- porównanie kolejności kluczowych eventów.

### Kryterium odbioru

Istnieje automatyczny test wykrywający zmianę wyniku lub deterministycznej
kolejności zdarzeń.

## Etap 1 — powiązanie BattleTickLoop z jedną symulacją

### Zakres

Zapisać `BattleSimulation` przekazaną do konstruktora jako pole:

```csharp
public sealed class BattleTickLoop
{
    private readonly BattleSimulation simulation;

    public BattleTickLoop(BattleSimulation simulation, float tickDuration);
    public BattleTickResult Tick(BattleEventQueue events);
}
```

Usunąć redundantny parametr symulacji z `Tick`.

Zaktualizować:

- `BattleView`;
- `BattleSimulationCombatService`;
- `BattleController`;
- testy resolverów;
- testy tick loopa.

Na tym etapie nie zmieniać kolejności resolverów ani nazwy `BattleTickLoop`.

### Kryterium odbioru

Nie można wykonać ticka na innej symulacji niż ta użyta do stworzenia
workspace’ów.

## Etap 2 — wydzielenie BattleCombatRunner

### Nowe typy

```csharp
public readonly struct BattleRunResult
{
    public int TicksElapsed { get; }
    public BattleTickResult LastTickResult { get; }
    public CombatEndReason EndReason { get; }
}
```

```csharp
public sealed class BattleCombatRunner : MonoBehaviour
{
    public bool IsRunning { get; }
    public BattleSimulation Simulation { get; }
    public BattleRunResult LastResult { get; }

    public event Action<BattleTickResult, IReadOnlyList<BattleEvent>>
        TickProcessed;
    public event Action<BattleRunResult> Completed;

    public void StartCombat(
        BattleSimulation simulation,
        float tickDuration,
        int maxTicks,
        int maxTicksPerFrame);

    public void StopCombat();
}
```

### Kod przenoszony z BattleView

- `simulation`;
- `tickLoop`;
- `eventQueue`;
- `tickAccumulator`;
- `ticksElapsed`;
- `maxSimulationTicks`;
- `maxTicksReached`;
- `lastTickResult`;
- `UpdateSimulation`;
- `StopTickingBecauseMaxTicksReached`;
- `CaptureDebugSnapshot`;
- `TickProcessed`.

### Integracja

- `BattleController` uruchamia runner.
- `BattleView` subskrybuje ticki albo jest jawnie wywoływany przez runner.
- Controller obsługuje `Completed` i aplikuje wynik.
- `BattleDebugOverlay` pobiera symulację oraz snapshot z runnera.
- Runner posiada profiler marker dla `Advance` lub `UpdateSimulation`.

### Testy

- zero ticków dla zbyt małego delta time;
- poprawna liczba ticków po uzbieraniu czasu;
- poprawny limit ticków na klatkę;
- zakończenie po wyniku symulacji;
- zakończenie po `MaxCombatTicks`;
- brak ticków po zakończeniu;
- poprawny stop i ponowny start;
- kolejka eventów nie jest przechowywana po jej wyczyszczeniu.

### Kryterium odbioru

`BattleView.Update()` obsługuje wyłącznie prezentację i zwalnianie zakończonych
efektów.

## Etap 3 — usunięcie dostępu BattleView do mutowalnej symulacji

### Snapshot początkowy

Dodać lekki model prezentacyjny:

```csharp
public readonly struct UnitPresentationState
{
    public int UnitId { get; }
    public int PresentationId { get; }
    public BattleSide Side { get; }
    public HexCoord Hex { get; }
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public int CurrentMana { get; }
    public int MaxMana { get; }
}
```

Snapshot powinien korzystać z buforowanej listy lub tablicy o pojemności
dopasowanej do liczby jednostek.

Docelowe API widoku:

```csharp
public void BindInitialState(
    BattlePresentationSnapshot snapshot,
    IReadOnlyDictionary<int, UnitView> reusableViews);

public void Present(IReadOnlyList<BattleEvent> events);
public void ClearPresentation(bool releaseUnitViews);
```

### Audyt BattleEvent

Uzupełnić eventy o dane zmienne potrzebne widokowi:

- `UnitMoved`: cel i czas kroku;
- `UnitDamaged`: pozostałe HP;
- `UnitManaChanged`: bieżąca mana;
- statusy: kompletna delta lub snapshot statusów po zmianie;
- attack windup/fire: kierunek lub pozycja celu w momencie eventu;
- projectile launch: typ prezentacji, start, cel i czas lotu;
- target/facing: event tylko wtedy, gdy cel faktycznie się zmienił.

Nie dodawać danych czysto wizualnych, takich jak prefab lub world position, do
zdarzeń domenowych.

### Usunięcia z BattleView

- właściwość `Simulation`;
- `StartConfiguredBattle`;
- `StartBattle`;
- `CreateRuntimeTuning`;
- odczyty `UnitRuntimeState`;
- zapytania `TryGetUnitById`;
- `FaceIdleUnitsTowardTargets` skanujące wszystkie jednostki co tick.

### Testy

- widok prezentuje event bez referencji do symulacji;
- wiele eventów dla jednej jednostki w jednym ticku zachowuje kolejność;
- facing zmienia się wyłącznie po odpowiednim zdarzeniu;
- statusy i overlay pokazują dane eventu, a nie późniejszy stan symulacji.

### Kryterium odbioru

`BattleView` nie posiada pola ani właściwości typu `BattleSimulation` lub
`UnitRuntimeState`.

## Etap 4 — oddzielenie danych combat od presentation

### Strategia migracji

Istniejące `ScriptableObject` pozostają formatem autorskim. Fabryka konwertuje
je na czyste dane runtime przed rozpoczęciem symulacji.

Przykładowe typy:

```csharp
public readonly struct UnitCombatSpec
{
    public int DefinitionId { get; }
    // Wyłącznie statystyki i parametry mechaniczne.
}
```

```csharp
public readonly struct ProjectileCombatSpec
{
    public int PresentationId { get; }
    public float Speed { get; }
    // Wyłącznie parametry mechaniczne.
}
```

### Zakres

- `BattleSimulationFactory` buduje combat specs.
- `UnitRuntimeState` przechowuje combat spec i stabilny `PresentationId`.
- `ProjectileRuntimeState` nie przechowuje `ProjectileDefinition`.
- `BattleSimulation.SpawnProjectile` przyjmuje combat spec.
- Dodać `BattlePresentationCatalog`.
- Katalog mapuje ID na prefab, wysokości, VFX i audio.
- Symulacja emituje efektywny czas windupu.
- Widok wylicza animator speed na podstawie czasu i konfiguracji animacji.

### Migracja assetów

- Nie przenosić istniejących assetów.
- Nie zmieniać ich nazw ani GUID-ów.
- Najpierw dodać kompatybilną konwersję w fabryce.
- Fizyczny podział assetów wykonać tylko wtedy, gdy daje mierzalną korzyść.

### Kryterium odbioru

Kod symulacji nie odwołuje się do `UnitView`, `ProjectileView`, prefabów,
animatorów ani parametrów world-space.

## Etap 5 — ograniczenie mutowalnego API BattleSimulation

### Zakres

Po migracji wszystkich call-site’ów ograniczyć do `internal`, gdzie to możliwe:

- `AdvanceTime`;
- `MoveUnit`;
- `StartUnitMovement`;
- `CompleteUnitMovement`;
- `DefeatUnit`;
- `SpawnProjectile`;
- `RemoveProjectileAt`;
- `CompleteBattle`.

Publiczne pozostają przede wszystkim:

- read-only kolekcje;
- informacje o wyniku;
- czas symulacji;
- bezpieczne zapytania `TryGet...`.

Zmieniać modyfikatory małymi grupami i po każdej uruchomić testy resolverów.

### Kryterium odbioru

Kod prezentacji nie może zmienić wyniku ani stanu symulacji.

## Etap 6 — centralizacja UnitView

### BattleUnitViewRegistry

Dodać jednego właściciela mapowania:

```csharp
public sealed class BattleUnitViewRegistry : MonoBehaviour
{
    public UnitView GetOrCreate(UnitPresentationState state);
    public bool TryGet(int unitId, out UnitView view);
    public void Release(int unitId);
    public void ReleaseAll();
}
```

Registry:

- istnieje przez cały mecz;
- jest używany w preparation i combat;
- jako jedyny tworzy, śledzi i usuwa `UnitView`;
- pilnuje braku duplikatów;
- nie zawiera logiki walki ani animacji.

Usunąć:

- `ReleaseUnitViewOwnership`;
- `ReclaimUnitViews`;
- `DetachUnitView`;
- osobne słowniki widoków w controllerze i view.

### Kryterium odbioru

Dla każdego runtime ID istnieje maksymalnie jeden aktywny `UnitView`, a jego
lifetime jest zarządzany w jednym miejscu.

## Etap 7 — podział dużego BattleView

### Zakres

Po ustabilizowaniu kontraktu zdarzeń wydzielić:

- `BattleUnitPresenter`;
- `BattleProjectilePresenter`;
- `BattleEffectPresenter`.

`BattleView` pozostaje lekkim routerem eventów. Nie wprowadzać osobnej klasy dla
każdego typu eventu.

### Kryterium odbioru

`BattleView` nie posiada logiki domenowej, czasu symulacji ani bezpośredniej
implementacji pul.

## 6. Migracja scen i prefabów

1. Dodać `BattleCombatRunner` bez usuwania legacy pól.
2. Przypiąć runner do obiektu odpowiedzialnego za walkę.
3. Połączyć referencje z `BattleController` i `BattleView`.
4. Jeżeli scena posiada standalone start, dodać osobny `BattleDebugLauncher`.
5. Zweryfikować serialized references przez Unity MCP.
6. Sprawdzić brak missing scripts i missing references.
7. Dopiero po poprawnym teście sceny usunąć legacy pola.
8. Zachować wszystkie istniejące pliki `.meta`.

## 7. Strategia testów

### Edit Mode

- deterministyczność tick loopa;
- parity realtime/synchronous;
- runner sterowany ręcznym `Advance(deltaTime)`;
- limity ticków;
- kolejność eventów;
- snapshot prezentacyjny;
- widok bez dostępu do symulacji;
- registry bez duplikatów;
- cleanup po restarcie.

### Play Mode

- preparation -> combat -> round resolution;
- kolejna runda;
- restart walki;
- wejście i wyjście ze sceny;
- brak zduplikowanych jednostek;
- poprawne HP, mana, statusy i facing;
- poprawne zwolnienie overlayów, VFX i pocisków;
- prezentacja kończy się również po limicie ticków.

Zgodnie z zasadami projektu testów Edit Mode nie należy uruchamiać w Unity
batchmode.

### Profilowanie

Sprawdzić na ustabilizowanej walce:

- `0 B GC Alloc` w runnerze;
- `0 B GC Alloc` w `BattleView.Present`;
- brak LINQ i closure w hot path;
- koszt `BattleTickLoop.Tick`;
- koszt prezentacji eventów;
- przestrzeganie `MaxTicksPerFrame`;
- stabilną liczbę aktywnych oraz poolowanych obiektów między rundami;
- brak wzrostu pamięci po wielokrotnym restarcie walki.

## 8. Ryzyka i zabezpieczenia

### Zmiana kolejności prezentacji eventów

Ryzyko: animacje pokazują stan późniejszy niż event.

Zabezpieczenie: eventy są konsumowane synchronicznie przed wyczyszczeniem
reużywanej kolejki, a kolejność jest pokryta testem.

### Rozjazd realtime i synchronous

Ryzyko: inne limity, tuning lub fixed tick.

Zabezpieczenie: wspólny resolver ustawień i test parity.

### Duplikaty UnitView

Ryzyko: jednoczesna własność controller/view podczas migracji.

Zabezpieczenie: etap przejściowy z legacy transferem, a następnie jeden registry.

### Utrata serialized references

Ryzyko: usunięcie pól przed migracją sceny lub prefabu.

Zabezpieczenie: nowe komponenty i pola najpierw, usunięcie legacy dopiero po
walidacji Unity.

### Nowe alokacje

Ryzyko: snapshoty, eventy albo callbacki tworzą kolekcje per tick.

Zabezpieczenie: struktury wartościowe, prealokowane bufory, brak LINQ i pomiar
Profilerem.

## 9. Zakres plików

### Nowe pliki

- `Assets/DeckBattle/Scripts/Battle/BattleCombatRunner.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleRunResult.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattlePresentationSnapshot.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitPresentationState.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleUnitViewRegistry.cs`;
- opcjonalnie `BattleDebugLauncher.cs`;
- opcjonalnie osobne presentery.

### Główne pliki modyfikowane

- `BattleView.cs`;
- `BattleController.cs`;
- `BattleSimulation.cs`;
- `BattleTickLoop.cs`;
- `BattleEvent.cs`;
- `BattleSimulationFactory.cs`;
- `BattleSimulationCombatService.cs`;
- `BattleDebugOverlay.cs`;
- `UnitRuntimeState.cs`;
- `ProjectileRuntimeState.cs`;
- `UnitDefinition.cs`;
- `ProjectileDefinition.cs`;
- testy Edit Mode i Play Mode związane z powyższymi typami.

Lista nowych typów może zostać zmniejszona, jeżeli podczas implementacji dwa
małe, zawsze współzmieniające się typy nie uzasadniają osobnych plików.

## 10. Zalecana kolejność commitów

1. `test: lock battle simulation parity`
2. `refactor: bind battle tick loop to simulation`
3. `refactor: extract realtime battle runner`
4. `refactor: introduce battle presentation snapshot`
5. `refactor: make battle view presentation-only`
6. `refactor: separate combat and presentation data`
7. `refactor: restrict battle simulation mutation API`
8. `refactor: centralize unit view ownership`
9. `refactor: split battle presentation components`

Commity 1-5 realizują podstawowy podział odpowiedzialności. Dalsze commity mogą
być wykonywane niezależnie po ustabilizowaniu sceny i kontraktu eventów.

## 11. Definition of Done

Refaktor jest zakończony, gdy:

- `BattleView` nie tworzy ani nie tickuje `BattleSimulation`;
- `BattleView` nie odczytuje mutowalnego `UnitRuntimeState`;
- realtime jest napędzany przez `BattleCombatRunner`;
- `BattleTickLoop` jest związany z jedną symulacją;
- realtime i synchronous korzystają z tych samych ustawień;
- eventy zawierają dane niezbędne do prawidłowej prezentacji;
- symulacja nie zależy od prefabów i komponentów Unity;
- mutacje symulacji nie są publicznie dostępne dla prezentacji;
- `UnitView` posiada jednego właściciela;
- wszystkie właściwe testy Edit Mode i Play Mode przechodzą;
- Unity nie raportuje missing references ani błędów kompilacji;
- profiler nie pokazuje nowych alokacji w stabilnym ticku;
- wyniki istniejących deterministycznych scenariuszy nie uległy zmianie.
