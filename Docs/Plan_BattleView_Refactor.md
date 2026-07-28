# Plan refaktoru BattleView i warstwy uruchamiania walki

## 1. Cel

Celem refaktoru jest doprowadzenie do sytuacji, w której:

- `BattleView` odpowiada wyłącznie za prezentację istniejącej symulacji;
- tworzenie `BattleSimulation`, wybór `BattleRuntimeTuning` oraz sterowanie
  tickami nie zależą od warstwy widoku;
- realtime i synchroniczna ścieżka walki używają identycznego tuningu,
  timingu i reguł zakończenia;
- `BattleController` pozostaje orkiestratorem przebiegu meczu, ale nie przejmuje
  szczegółów tickowania ani obsługi efektów wizualnych;
- zmiany można wdrożyć etapami bez jednoczesnego przepisywania całego flow rundy.

Refaktor nie powinien zmieniać semantyki walki, kolejności `BattleEvent`,
wyników deterministycznych ani produkcyjnego contentu.

## 2. Problemy obecnej implementacji

### 2.1. BattleView łączy symulację i prezentację

`BattleView` obecnie:

- buduje `HexBoard`, `BattleSimulation` i `BattleTickLoop`;
- odczytuje `BattleConfig`, `BattleTimingConfig` i tuning walki;
- posiada accumulator czasu oraz limit ticków na klatkę;
- obsługuje `BattleEventQueue`;
- tworzy i zwalnia widoki jednostek;
- interpretuje eventy symulacji;
- zarządza pociskami, VFX oraz ich pulami;
- aktualizuje overlay statusów;
- zawiera samodzielny tryb testowy oparty o `initialUnits`.

To powoduje, że klasa o nazwie `View` posiada część odpowiedzialności
application/runtime i może utworzyć symulację z innymi parametrami niż główny
przepływ meczu.

### 2.2. Główna ścieżka ignoruje asset runtime tuningu

`BattleController.RunCombatRoutine` i `RunCombatSynchronously` wywołują:

```text
BattleSimulationFactory.Create(state, BattleRuntimeTuning.Default)
```

Natomiast `BattleRuntimeTuningConfig` jest obecnie odczytywany tylko przez
`BattleView.StartBattle`. Oznacza to, że asset przypięty do `BattleConfig_MVP`
nie wpływa na właściwą walkę uruchamianą przez `BattleController`.

To jest pierwszy problem do naprawienia, niezależnie od dalszego podziału klas.

### 2.3. Dwie ścieżki uruchamiania walki mogą się rozjechać

Realtime:

```text
BattleController -> BattleSimulationFactory -> BattleView -> BattleTickLoop
```

Synchronicznie:

```text
BattleController -> BattleSimulationFactory -> BattleSimulationCombatService
```

Obie ścieżki osobno wybierają tuning, timing, limit ticków oraz sposób
zakończenia. Zwiększa to ryzyko innych wyników w widoku i w testach/headless.

### 2.4. Niejasna własność UnitView

`BattleController` tworzy widoki przygotowania, przekazuje je do `BattleView`,
zwalnia własność, a po walce ponownie je odzyskuje. Obie klasy posiadają osobne
listy, słowniki, wyszukiwanie duplikatów i logikę czyszczenia.

Mechanizm działa, ale odpowiedzialność i lifetime widoków są trudne do
prześledzenia.

## 3. Docelowy podział odpowiedzialności

### 3.1. BattleController

Pozostaje orkiestratorem meczu:

- tworzy `BattleState`;
- steruje fazami rundy i przygotowaniem;
- inicjuje rozpoczęcie i zakończenie walki;
- aplikuje wynik symulacji do `BattleState`;
- uruchamia prezentację wyniku rundy;
- nie tworzy ręcznie `BattleTickLoop`;
- nie wybiera `BattleRuntimeTuning.Default`.

W dalszym etapie część automatycznego flow może zostać przeniesiona do
`BattleRoundFlowCoordinator`, ale nie jest to wymagane do oczyszczenia
`BattleView`.

### 3.2. BattleSimulationFactory

Odpowiada wyłącznie za stworzenie kompletnej symulacji:

- konwertuje jednostki `BattleState` na `UnitSpawnData`;
- otrzymuje jawny `BattleRuntimeTuning`;
- posiada wygodny overload `Create(BattleState)`, który rozwiązuje tuning z
  `state.Config`;
- nie zna kamer, prefabów, coroutine ani frame time.

### 3.3. BattleRuntimeConfigResolver

Mała, czysta klasa bez stanu:

```text
ResolveTuning(BattleConfig) -> BattleRuntimeTuning
ResolveTiming(BattleTimingConfig) -> BattleRuntimeExecutionSettings
```

Zasady:

- config obecny: użyć assetu;
- config nieobecny w teście lub starym prefabie: jawny fallback do wartości
  domyślnych;
- walidacja i clamping pozostają w typach config/runtime;
- brak odczytu configu w `BattleView`.

`BattleRuntimeExecutionSettings` powinien zawierać tylko parametry wykonania:

- `TickDuration`;
- `MaxCombatTicks`;
- `MaxTicksPerFrame`.

`RoundResolutionDelay` pozostaje parametrem flow/presentation, a nie symulacji.

### 3.4. BattleRealtimeRunner

Nowy, mały komponent odpowiedzialny za wykonanie realtime:

- przyjmuje gotową `BattleSimulation`;
- tworzy `BattleTickLoop`;
- posiada accumulator czasu i limit ticków na klatkę;
- reużywa `BattleEventQueue`;
- emituje `TickProcessed` z wynikiem i eventami;
- emituje `CombatCompleted` albo `MaxTicksReached`;
- nie interpretuje eventów;
- nie tworzy GameObjectów, VFX ani UI.

Runner jest adapterem czasu klatki do deterministycznego fixed ticka. Sama
logika walki nadal pozostaje w `BattleTickLoop`.

### 3.5. BattleView

Po refaktorze odpowiada za:

- związanie gotowej symulacji z prezentacją;
- tworzenie/wiązanie widoków jednostek;
- interpretację `BattleEvent`;
- ruch, facing, animacje ataku i obrażeń;
- pociski oraz poolowane VFX;
- overlay HP, many i statusów;
- czyszczenie prezentacji.

Nie powinien posiadać:

- `BattleConfig`;
- `BattleTimingConfig`;
- pól tuningu symulacji;
- `BattleTickLoop`;
- `BattleEventQueue`;
- tick accumulatora;
- `StartConfiguredBattle`, `StartBattle` ani `initialUnits`;
- `CreateRuntimeTuning`.

Dozwolony jest `Update` służący wyłącznie do prezentacji, np. zwalniania
zakończonych VFX i pocisków.

### 3.6. BattleDebugLauncher

Samodzielny tryb testowy z `initialUnits` i `startOnAwake` należy przenieść do
osobnego komponentu `BattleDebugLauncher`, dostępnego w Editor/Development
Build.

Launcher:

- buduje debugowe `UnitSpawnData`;
- korzysta z tej samej fabryki i resolvera configu co produkcja;
- uruchamia `BattleRealtimeRunner`;
- nie dodaje alternatywnych reguł walki do `BattleView`.

### 3.7. Własność UnitView

W pierwszym kroku można zachować obecny jawny transfer widoków, aby ograniczyć
ryzyko.

Po ustabilizowaniu runnera należy wprowadzić jednego właściciela:
`BattleUnitViewRegistry`.

Registry:

- przechowuje mapowanie runtime ID -> `UnitView`;
- tworzy widoki z `UnitDefinition.UnitPrefab`;
- obsługuje przejście preparation/combat bez duplikatów;
- zwalnia widoki przy końcu meczu;
- jest używany przez `BattleController` i `BattleView`, ale tylko registry
  modyfikuje kolekcję.

Nie powinien zawierać logiki walki ani animacji.

## 4. Docelowy przepływ

```text
BattleConfig + BattleTimingConfig
              |
              v
BattleRuntimeConfigResolver
              |
              v
BattleController
    | tworzy BattleState
    | prosi fabrykę o symulację
              v
BattleSimulationFactory
              |
              v
BattleSimulation
    |                         |
    v                         v
BattleRealtimeRunner      BattleSimulationCombatService
    | eventy/tick             | headless/synchronous
    v                         |
BattleView                   wynik
    |
    v
UnitView / ProjectileView / VFX / Overlay
```

Realtime i headless otrzymują tę samą symulację, tuning i fixed tick.

## 5. Etapy implementacji

### Etap 0: testy kontraktowe przed zmianami

Dodać testy zabezpieczające:

- `BattleSimulationFactory.Create(state)` używa
  `state.Config.RuntimeTuningConfig`;
- realtime i synchronous dla tego samego seed/tuningu kończą się tym samym
  wynikiem;
- kolejność eventów istniejącego replayu nie zmienia się;
- brak configu daje `BattleRuntimeTuning.Default`;
- `BattleView.Bind` nie zmienia stanu symulacji.

Warunek zakończenia: obecny behavior jest uchwycony testami przed przenoszeniem
odpowiedzialności.

### Etap 1: jedno źródło tuningu

- dodać `BattleRuntimeConfigResolver`;
- zmienić `BattleSimulationFactory.Create(state)`, aby używał configu ze stanu;
- usunąć jawne użycie `BattleRuntimeTuning.Default` z obu ścieżek
  `BattleController`;
- pozostawić overload przyjmujący jawny tuning dla testów;
- upewnić się, że realtime i synchronous korzystają z tego samego resolvera.

To jest mała zmiana o wysokim priorytecie i może zostać wykonana jako osobny
commit.

### Etap 2: wydzielenie BattleRealtimeRunner

- przenieść z `BattleView`:
  - `BattleTickLoop`;
  - `BattleEventQueue`;
  - accumulator;
  - liczniki ticków;
  - obsługę `MaxTicksReached`;
  - ograniczenie `MaxTicksPerFrame`;
- runner emituje event/callback po każdym ticku;
- `BattleController` tworzy symulację i uruchamia runner;
- `BattleView` otrzymuje eventy od runnera;
- synchronous service pozostaje bez MonoBehaviour.

Warunek zakończenia: wyłączenie lub brak `BattleView` nie uniemożliwia runnerowi
ukończenia symulacji.

### Etap 3: oczyszczenie BattleView

- usunąć tworzenie symulacji i standalone config;
- zmienić API na:

```text
Bind(BattleSimulation, reusable views)
Present(IReadOnlyList<BattleEvent>)
Clear(release views)
```

- zachować obecne event handlery i pule prezentacyjne;
- pozostawić debug snapshot jako opcjonalną prezentację/debug lub przenieść jego
  capture do runnera, jeżeli ma pokazywać stan ticka niezależnie od widoku.

Warunek zakończenia: `BattleView` nie importuje ani nie odczytuje configów
symulacji.

### Etap 4: debug launcher

- przenieść `initialUnits`, `startOnAwake` i tworzenie debugowego boardu do
  `BattleDebugLauncher`;
- ograniczyć launcher do Editor/Development Build;
- używać produkcyjnego resolvera, fabryki i runnera;
- usunąć standalone entry pointy z `BattleView`.

Warunek zakończenia: istnieje jedna implementacja uruchamiania realtime, a
launcher jedynie dostarcza dane wejściowe.

### Etap 5: uporządkowanie UnitView

- dodać `BattleUnitViewRegistry`;
- usunąć duplikaty kolekcji i wyszukiwania widoków z `BattleController` oraz
  `BattleView`;
- zastąpić `ReleaseUnitViewOwnership`/`ReclaimUnitViews` jawną zmianą trybu
  prezentacji lub zachowaniem jednego registry przez całą rundę;
- zachować stabilność prefabów i GUID.

Warunek zakończenia: dla runtime ID istnieje maksymalnie jeden aktywny
`UnitView`, a jego właściciel jest jednoznaczny.

### Etap 6: opcjonalny podział prezentacji

Wykonać tylko wtedy, gdy `BattleView` nadal jest zbyt duży lub trudny do
testowania:

- `BattleProjectilePresenter` — pociski i ich pule;
- `BattleEffectPresenter` — attack/damage/status VFX;
- `BattleUnitPresenter` — mapowanie jednostek i animacje eventów.

Nie wprowadzać tych klas z góry. Każde wydzielenie powinno usuwać konkretny
problem z testowaniem lub własnością, a nie tylko zmniejszać liczbę linii.

## 6. API i zależności

Preferowane zależności:

```text
BattleController
  -> BattleRuntimeConfigResolver
  -> BattleSimulationFactory
  -> BattleRealtimeRunner
  -> BattleView

BattleRealtimeRunner
  -> BattleSimulation
  -> BattleTickLoop
  -> BattleEventQueue

BattleView
  -> BattleSimulation (read-only lookup)
  -> BattleEvent
  -> presentation components
```

Niedozwolone zależności po refaktorze:

```text
BattleView -> BattleConfig
BattleView -> BattleRuntimeTuningConfig
BattleView -> BattleSimulationFactory
BattleView -> new BattleTickLoop(...)
BattleView -> zmiana BattleState
```

## 7. Testy

### Edit Mode

- resolver configu mapuje wszystkie wartości i fallback;
- fabryka pobiera tuning z `BattleState.Config`;
- explicit tuning w testach nadal działa;
- realtime runner wykonuje dokładnie jeden fixed tick na należny krok;
- limit ticków na klatkę działa bez utraty deterministycznego czasu symulacji;
- max combat ticks kończy runner z właściwym reason;
- eventy są przekazywane do prezentera raz i w tej samej kolejności;
- `BattleView` nie tworzy ani nie tickuje symulacji;
- synchronous i realtime dają identyczny winner, HP, pozycje i eventy.

### Play Mode

- przejście preparation -> combat -> round resolution;
- istniejące `UnitView` nie są duplikowane podczas wejścia w combat;
- po walce registry odzyskuje widoki bez pozostawionych overlayów i pocisków;
- zatrzymanie lub restart walki czyści runner i prezentację;
- brak `BattleView` pozwala zakończyć walkę headless z ostrzeżeniem lub bez
  prezentacji, zależnie od entry pointu.

### Wydajność

- `BattleRealtimeRunner.Update` po warm-upie nie alokuje;
- `BattleView.Present` nie tworzy kolekcji per event/tick;
- pule VFX i pocisków zachowują dotychczasowe pojemności;
- wydzielenie klas nie dodaje eventowych closure ani LINQ w hot path.

## 8. Migracja scen i prefabów

- najpierw dodać nowe komponenty bez usuwania pól legacy;
- przypiąć `BattleRealtimeRunner` oraz ewentualny `BattleDebugLauncher`;
- przepiąć `BattleController`;
- zweryfikować serialized references przez Unity MCP;
- dopiero po poprawnym teście scen usunąć legacy pola z `BattleView`;
- nie zmieniać nazw ani położenia prefabów jednostek;
- zachować `.meta` i GUID wszystkich przenoszonych assetów.

Jeżeli nie ma obecnie sceny zawierającej `BattleView`, migrację referencji należy
wykonać w prefabie lub scenie, która faktycznie tworzy go w docelowym flow, a nie
przez runtime `FindObjectOfType`.

## 9. Ryzyka

- zmiana kolejności wywołania `ProcessEvents` względem ticka może rozjechać
  animacje i aktualny stan prezentacji;
- niejawne przejęcie `UnitView` może stworzyć duplikaty lub zniszczyć widok
  używany przez preparation;
- dwa niezależne źródła timingu mogą powodować inne wyniki max-tick;
- event callback z runnera nie może przechowywać referencji do kolejki, która
  zostanie wyczyszczona przed prezentacją;
- runner nie może używać renderowanego `deltaTime` do logiki symulacji poza
  obliczeniem liczby należnych fixed ticków;
- wyodrębnienie zbyt wielu małych presenterów naraz zwiększy liczbę referencji
  scenowych i koszt migracji bez pewnej korzyści.

## 10. Kryteria zakończenia

Refaktor jest zakończony, gdy:

- produkcyjna i synchroniczna walka używają assetu
  `BattleRuntimeTuningConfig_MVP`;
- `BattleView` nie tworzy ani nie tickuje symulacji;
- realtime tick jest obsługiwany przez osobny runner;
- `BattleController` nie przekazuje `BattleRuntimeTuning.Default` ręcznie;
- eventy i końcowe wyniki pozostają deterministyczne;
- nie ma duplikatów `UnitView`, overlayów ani pozostawionych pocisków;
- pełne Edit Mode i odpowiednie Play Mode testy przechodzą;
- profiler nie pokazuje nowych alokacji w ustabilizowanym ticku.

## 11. Zalecana kolejność commitów

1. `fix: resolve combat tuning from battle config`
2. `refactor: extract realtime battle runner`
3. `refactor: make battle view presentation-only`
4. `refactor: move standalone battle setup to debug launcher`
5. `refactor: centralize unit view ownership`
6. `test: cover realtime and synchronous combat parity`

Pierwsze trzy commity realizują główny cel. Etapy 4–6 można wykonać osobno po
potwierdzeniu stabilności sceny i presentation flow.
