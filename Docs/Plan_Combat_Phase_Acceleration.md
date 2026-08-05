# Plan: automatyczne przyspieszenie fazy walki

Status: propozycja implementacyjna  
Data: 2026-08-05  
Zakres: `BattleTimingConfig`, realtime combat runner i prezentacja fazy `Combat`

## 1. Cel

Dodać automatyczne przyspieszenie trwającej zbyt długo fazy walki. Po upływie
skonfigurowanego czasu zarówno symulacja, jak i prezentacja mają przejść z
prędkości `1x` na skonfigurowany mnożnik.

Po wdrożeniu:

- czas oczekiwania na przyspieszenie jest edytowalny w `BattleTimingConfig`;
- mnożnik prędkości po przyspieszeniu jest edytowalny w tym samym assetcie;
- fixed tick i zasady walki pozostają niezmienione;
- przyspieszenie zmienia wyłącznie tempo wykonywania fazy `Combat`;
- HUD pokazuje pozostały czas do przyspieszenia przez `RoundTimer/ProgressBar`;
- ruch, obroty, ataki, speciale, pociski, obrażenia, śmierć, VFX i animacje UI
  związane z jednostkami zachowują tempo symulacji;
- preparation, komunikat `Fight!`, rozliczenie rundy i pozostałe ekrany nie są
  przyspieszane;
- kolejna walka zawsze zaczyna się od `1x`, chyba że opóźnienie ustawiono na
  `0`;
- zmiana nie powoduje alokacji na klatkę i nie używa globalnego
  `Time.timeScale`.

## 2. Potwierdzony zakres i pozostałe założenia

Plan przyjmuje następujący kontrakt:

1. Zgodnie z potwierdzeniem obie kontrole będą polami w
   `BattleTimingConfig`/Inspectorze, bez przycisków UI dostępnych graczowi.
2. Zegar zaczyna biec w `BattleCombatRunner.StartCombat`, czyli po zakończeniu
   komunikatu `Fight!` i po faktycznym starcie symulacji.
3. Opóźnienie jest liczone w zwykłym czasie gry, przed zastosowaniem mnożnika.
   Przyspieszenie nie może skracać własnego progu aktywacji.
4. Przejście jest jednorazowe i skokowe: `1x -> AcceleratedCombatSpeed`.
5. Mnożnik obowiązuje do zakończenia bieżącej prezentacji walki i jest
   resetowany przed przejściem do kolejnej rundy.
6. `RoundTimer` jest widoczny wyłącznie podczas aktywnej symulacji `Combat`.
   Jego `ProgressBar` zaczyna od skali X `1`, maleje liniowo do `0` i znika po
   aktywacji przyspieszenia.

Jeżeli potrzebne jest płynne narastanie prędkości, ręczny przycisk, komunikat UI
albo objęcie przyspieszeniem `RoundResolution`, zakres należy rozszerzyć przed
implementacją.

Proponowany tuning początkowy dla `BattleTimingConfig_MVP.asset`:

- `CombatAccelerationDelay = 10 s`;
- `AcceleratedCombatSpeed = 2x`.

Wartości są punktem startowym do testów na urządzeniu i powinny zostać
potwierdzone przed zmianą produkcyjnego assetu.

## 3. Stan obecny

### 3.1. Symulacja

`BattleCombatRunner`:

- pobiera `Time.deltaTime` w `Update`;
- dodaje czas do `tickAccumulator`;
- wykonuje fixed ticki o czasie `BattleTickLoop.TickDuration`;
- ogranicza pracę przez `MaxTicksPerFrame` i `MaxCombatTicks`.

`BattleSimulation.ElapsedTime` jest przesuwany wyłącznie przez fixed tick.
Realtime i synchronous korzystają z tych samych reguł oraz tej samej długości
ticka. To należy zachować.

### 3.2. Prezentacja

Czas prezentacji jest obecnie rozproszony:

- `UnitView` używa `Time.deltaTime` dla ruchu, obrotu, flasha obrażeń i śmierci;
- `Animator` jednostki działa we własnym tempie;
- `ProjectileView` używa `Time.deltaTime` dla lotu;
- `PooledBattleEffect` używa `Time.deltaTime` dla czasu życia i skali;
- `UnitStatusOverlayController` używa `Time.deltaTime` dla animacji paska HP;
- `StatusVfxView` używa `Time.time` dla one-shot lifetime;
- systemy cząsteczek posiadają własny `simulationSpeed`.

Samo przyspieszenie runnera spowodowałoby więc rozjazd: eventy symulacji
pojawiałyby się szybciej, ale animacje i efekty nadal trwałyby w tempie `1x`.

## 4. Kontrakt czasu

Należy rozróżnić trzy wartości:

```text
baseFrameDelta       = Time.deltaTime
combatElapsedTime   += baseFrameDelta
combatDelta          = baseFrameDelta * CurrentCombatSpeed
```

- `combatElapsedTime` steruje wyłącznie progiem przyspieszenia;
- `combatDelta` zasila accumulator symulacji i zegary prezentacji;
- `BattleTickLoop.TickDuration` nie jest mnożony ani dzielony;
- wartości `Duration` w `BattleEvent` nadal są wyrażone w czasie symulacji.

Próg ma być sprawdzany na granicy klatki. Pierwsza pełna klatka rozpoczęta po
osiągnięciu opóźnienia używa nowego mnożnika. Daje to maksymalnie jedną klatkę
opóźnienia, ale pozwala użyć identycznego mnożnika dla runnera i wszystkich
widoków bez dzielenia klatki na dwa różne tempa.

Zegar korzysta z `Time.deltaTime`, więc zatrzymuje się razem z grą przy pauzie i
nie biegnie podczas braku klatek po zminimalizowaniu lub uśpieniu aplikacji.

## 5. Konfiguracja

### 5.1. `BattleTiming`

Dodać stałe domyślne i minimalne:

```csharp
public const float DefaultCombatAccelerationDelay = 10f;
public const float DefaultAcceleratedCombatSpeed = 2f;
public const float MinAcceleratedCombatSpeed = 1f;
```

Nie wprowadzać osobnego `bool`. Wartość prędkości `1` naturalnie wyłącza efekt,
a opóźnienie `0` oznacza natychmiastowe tempo przyspieszone.

### 5.2. `BattleTimingConfig`

Dodać pola:

```csharp
[Min(0f)] public float CombatAccelerationDelay;
[Min(1f)] public float AcceleratedCombatSpeed;
```

Pola umieścić pod osobnym nagłówkiem `Combat Acceleration`. `OnValidate` oraz
runtime resolver w `BattleController` i `StandaloneBattleBootstrap` muszą
obronić się przed wartościami ujemnymi, `NaN` i nieskończonością. Poprawny
mnożnik nigdy nie może być mniejszy niż `1`.

Zaktualizować `BattleTimingConfig_MVP.asset` jawnie, aby nie polegać na wartości
domyślnej nowo dodanego pola po deserializacji istniejącego assetu.

## 6. Sterowanie tempem symulacji

### 6.1. `BattleCombatRunner`

Dodać stan:

```csharp
public float CurrentCombatSpeed { get; private set; }
public float CombatElapsedTime { get; private set; }
public event Action<float> CombatSpeedChanged;
```

`StartCombat` otrzymuje dwa dodatkowe parametry:

```csharp
float accelerationDelay,
float acceleratedCombatSpeed
```

Przy starcie runner:

- waliduje ustawienia;
- zeruje `CombatElapsedTime`;
- ustawia `CurrentCombatSpeed` na `1`, a dla opóźnienia `0` od razu na
  skonfigurowany mnożnik;
- nie zmienia `tickDuration`.

W `Advance(deltaTime)`:

1. na początku klatki sprawdzić, czy zwykły zegar osiągnął próg;
2. przy pierwszym przekroczeniu ustawić mnożnik i wyemitować
   `CombatSpeedChanged` dokładnie raz;
3. dodać `deltaTime * CurrentCombatSpeed` do `tickAccumulator`;
4. wykonać dotychczasową pętlę fixed ticków;
5. dodać nieprzyspieszone `deltaTime` do `CombatElapsedTime`.

Runner powinien wykonywać aktualizację przed komponentami prezentacji, np.
przez jawny `DefaultExecutionOrder`, aby nowy mnożnik był widoczny dla całej
klatki prezentacyjnej.

`StopCombat` i kolejny `StartCombat` resetują zegar oraz prędkość. Samo
`Complete` zatrzymuje ticki, ale może pozostawić bieżący mnożnik do czasu
wyczyszczenia ostatnich efektów walki.

### 6.2. Determinizm

Przyspieszenie nie trafia do `BattleSimulation`, resolverów ani eventów. Runner
wykonuje te same ticki w tej samej kolejności, tylko częściej na sekundę czasu
rzeczywistego.

Nie zmieniać:

- `BattleTickLoop.TickDuration`;
- `BattleSimulation.ElapsedTime` poza istniejącym przesuwaniem per tick;
- cooldownów, windupów, movement duration ani projectile duration;
- RNG;
- ścieżki synchronous w `BattleSimulationCombatService`.

Dla tego samego seeda i stanu wejściowego wynik oraz liczba logicznych ticków do
rozstrzygnięcia muszą pozostać identyczne dla `1x` i trybu przyspieszonego.

### 6.3. Ochrona czasu klatki

Istniejący `MaxTicksPerFrame` pozostaje twardym limitem kosztu CPU. Jeśli
urządzenie nie jest w stanie wykonać wymaganej liczby ticków, runner zachowuje
obecne zabezpieczenie przed spiralą nadrabiania. Oznacza to, że bardzo wysoki
mnożnik może być efektywnie ograniczony przez budżet klatki.

Nie podnosić automatycznie `MaxTicksPerFrame` na podstawie mnożnika. Wartość
należy dobrać na podstawie profilowania, ponieważ stabilna klatka ma wyższy
priorytet niż idealne tempo przy przeciążeniu.

## 7. Przekazanie mnożnika do prezentacji

### 7.1. `BattleController` i standalone bootstrap

`BattleController`:

- rozwiązuje nowe wartości z `BattleTimingConfig`;
- przekazuje je do `BattleCombatRunner.StartCombat`;
- po starcie ustawia bieżącą wartość w `BattleView`;
- subskrybuje `CombatSpeedChanged` razem z `TickProcessed` i `Completed`;
- zawsze odpina subskrypcję przy stopie, restarcie i wyjściu z walki;
- resetuje widok do `1x` podczas cleanupu.

Tę samą ścieżkę trzeba zastosować w `StandaloneBattleBootstrap`, aby debugowa
walka nie miała innego zachowania niż główna scena.

### 7.2. `BattleView`

Dodać jedno wejście:

```csharp
public void SetCombatSpeed(float speed)
```

`BattleView` przechowuje ostatni poprawny mnożnik i rozprowadza go wyłącznie,
gdy wartość faktycznie się zmieniła. Nie wolno skanować obiektów prezentacji co
klatkę.

Mnożnik należy przekazać do:

- `UnitViewRegistry`;
- `BattleProjectilePresenter`;
- `BattleEffectPresenter`;
- `UnitStatusOverlayController`;
- `UnitStatusVfxController`.

Presenter i registry zapisują bieżącą wartość, aby obiekt pobrany z puli lub
utworzony już po aktywacji przyspieszenia od razu dostał poprawne tempo.

### 7.3. Jednostki i Animator

`UnitView` przechowuje `combatSpeed` i używa:

```csharp
float combatDeltaTime = Time.deltaTime * combatSpeed;
```

dla:

- ruchu między heksami;
- obracania modelu;
- flasha obrażeń;
- czasu prezentacji śmierci.

Ustawić również `animator.speed = combatSpeed`. Jest to mnożnik fazy walki,
nie zamiennik istniejącego parametru `attackSpeed`:

```text
wynikowa prędkość stanu ataku = attackSpeed * animator.speed
```

`attackSpeed` nadal synchronizuje konkretny windup z jego logicznym czasem, a
`animator.speed` skaluje całą prezentację walki. `Bind`, reuse i wyjście z
combat muszą przywrócić bezpieczne `1x` lub aktualną wartość przekazaną przez
registry.

Nie dzielić `BattleEvent.Duration` przez mnożnik przy odbiorze eventu. Dzięki
skalowanemu delta time także animacja rozpoczęta przed progiem może przyspieszyć
w trakcie bez skoku pozycji i bez ponownego planowania czasu.

### 7.4. Pociski i proste efekty

`ProjectileView` i `PooledBattleEffect` otrzymują `SetCombatSpeed`. Ich lokalne
zegary używają skalowanego delta time. Presentery:

- aktualizują wszystkie aktywne obiekty przy zmianie mnożnika;
- przechowują mnożnik dla obiektów pobieranych później z puli;
- resetują obiekt przy zwrocie do puli, aby nie przenosił tempa do innego
  kontekstu.

Logiczny `ProjectileResolved` nadal decyduje o końcu pocisku. Widok ma tylko
dotrzeć do celu w tempie odpowiadającym symulacji.

### 7.5. Overlay HP i status VFX

`UnitStatusOverlayController` przekazuje do `TickDamageFill` skalowany delta
time. Pozycjonowanie overlayu nadal odbywa się raz na `LateUpdate` i nie wymaga
dodatkowych odczytów ani przebudowy layoutu.

W `StatusVfxView` zastąpić absolutny deadline oparty o `Time.time` licznikiem
pozostałego czasu one-shot. `UnitStatusVfxController.Update` zmniejsza go o
skalowany delta time podczas istniejącej iteracji po aktywnych one-shotach.

Dla `ParticleSystem` ustawić `main.simulationSpeed`:

- przy pobraniu VFX z puli;
- przy zmianie mnożnika dla aktywnych VFX;
- zresetować przed zwrotem do puli.

Nie iterować po systemach cząsteczek co klatkę. Zmiana ma następować tylko przy
starcie obiektu i przy pojedynczym przełączeniu prędkości.

### 7.6. Elementy celowo poza zakresem

Nie przyspieszać:

- `RoundAnnouncementView` i tweenów `Fight!`, preparation oraz wyniku rundy;
- opóźnień AI w preparation;
- `RoundResolutionDelay`;
- UI talii, kart i wejścia gracza;
- całej sceny przez `Time.timeScale`;
- audio, dopóki nie zostaną dodane dźwięki wymagające jawnej synchronizacji.

### 7.7. HUD czasu do przyspieszenia

`BattleUIController` otrzymuje referencje do `BattleCombatRunner`, obiektu
`RoundTimer` i jego `ProgressBar`. UI odczytuje wyłącznie:

```text
remaining = 1 - clamp01(CombatElapsedTime / CombatAccelerationDelay)
```

Następnie zapisuje `remaining` do skali X `ProgressBar`, zachowując jego bazową
skalę Y i Z. Nie tworzyć tweena ani coroutine — jeden HUD aktualizuje wartość
tylko w czasie oczekiwania na przyspieszenie. Timer pozostaje ukryty podczas
`Fight!`, preparation, round resolution, przy mnożniku `1x` i przy opóźnieniu
`0`.

## 8. Kolejność implementacji

### Etap 0 — testy bazowe

- Uruchomić przez Unity MCP istniejące `BattleCombatRunnerTests`,
  `BattleTickLoopTests`, `BattleRealtimeSynchronousCompatibilityTests`,
  `BattlePresentationContractTests` i `UnitViewFacingTests`.
- Zapisać baseline czasu CPU runnera i GC Alloc w scenie `Battle`.
- Potwierdzić aktualny asset `BattleTimingConfig_MVP` i serialized reference w
  `Battle.unity`.

### Etap 1 — konfiguracja i czysty kontrakt runnera

- Dodać stałe do `BattleTiming`.
- Dodać oraz walidować pola `BattleTimingConfig`.
- Rozszerzyć `BattleCombatRunner.StartCombat`.
- Dodać zegar, mnożnik, event i reset stanu.
- Pokryć runner testami bez dotykania prezentacji.

Kryterium etapu: runner wykonuje więcej tych samych fixed ticków po progu, nie
zmieniając długości ticka ani wyniku symulacji.

### Etap 2 — integracja przepływu walki

- Przekazać konfigurację w `BattleController`.
- Rozszerzyć subskrypcje i cleanup.
- Zaktualizować `StandaloneBattleBootstrap`.
- Zachować synchronous fallback bez przyspieszenia czasu ściennego.

Kryterium etapu: każda realtime walka resetuje zegar i emituje maksymalnie jedną
zmianę prędkości.

### Etap 3 — prezentacja jednostek

- Dodać `BattleView.SetCombatSpeed`.
- Rozszerzyć `UnitViewRegistry` i `UnitView`.
- Skalować customowe timery i `Animator.speed`.
- Sprawdzić mnożenie z istniejącym parametrem `attackSpeed`.

Kryterium etapu: ruch, obrót, attack, special, damage i death zachowują relację
czasową z eventami symulacji przed oraz po przełączeniu.

### Etap 4 — pozostałe efekty

- Rozszerzyć pociski i ich presenter.
- Rozszerzyć pooled battle effects i ich presenter.
- Rozszerzyć overlay obrażeń.
- Przenieść one-shot lifetime statusu na skalowany licznik.
- Ustawić `ParticleSystem.main.simulationSpeed` dla status VFX.

Kryterium etapu: żaden zegar prezentacji aktywny w `Combat` nie korzysta z
nieprzeskalowanego `Time.deltaTime` lub absolutnego `Time.time`.

### Etap 5 — asset, scena i walidacja

- Ustawić zatwierdzone wartości w `BattleTimingConfig_MVP.asset` przez Unity
  Editor/Unity MCP.
- Sprawdzić serialized fields, brak missing references i brak zmian GUID.
- Uruchomić wąskie testy, pełny zestaw Edit Mode i Play Mode smoke test.
- Wykonać profilowanie na mobile-like profilu i docelowym urządzeniu.

## 9. Testy

### 9.1. Edit Mode — konfiguracja

Dodać `BattleTimingConfigTests` lub rozszerzyć właściwy test konfiguracji:

- opóźnienie ujemne jest korygowane do `0`;
- prędkość poniżej `1` jest korygowana do `1`;
- `NaN` i nieskończoność wracają do wartości bezpiecznych;
- poprawne wartości są przekazywane bez zmiany;
- istniejący asset posiada jawnie zapisane oba pola.

### 9.2. Edit Mode — runner

Rozszerzyć `BattleCombatRunnerTests` o przypadki:

1. przed progiem działa dokładnie `1x`;
2. próg nie jest liczony z przyspieszonego czasu;
3. opóźnienie `0` daje mnożnik od startu;
4. mnożnik `1` zachowuje zachowanie bez przyspieszenia;
5. event zmiany jest emitowany dokładnie raz;
6. stop i restart resetują zegar i mnożnik;
7. `MaxTicksPerFrame` nadal ogranicza koszt przy wysokim mnożniku;
8. brak dodatkowych ticków po `Completed`;
9. ta sama symulacja kończy się tym samym wynikiem i po tej samej liczbie
   logicznych ticków przy `1x` i przyspieszeniu;
10. kilka wywołań `Advance` wokół granicy progu daje zdefiniowaną semantykę
    przełączenia na granicy klatki.

### 9.3. Edit Mode — prezentacja

Dodać testowalne, wewnętrzne metody aktualizacji tam, gdzie obecny kod jest
zamknięty w `Update`, i sprawdzić:

- ruch jednostki przy `2x` zużywa dwa razy więcej czasu prezentacji;
- zmiana na `2x` w połowie ruchu nie teleportuje jednostki;
- obrót, flash obrażeń i death timer używają tego samego mnożnika;
- `animator.speed` jest ustawiany i resetowany, a `attackSpeed` pozostaje
  niezależnym mnożnikiem stanu ataku;
- pocisk przy `2x` pokonuje odpowiednią część drogi;
- pooled effect kończy się dwa razy szybciej w czasie rzeczywistym;
- damage fill używa skalowanego delta time;
- one-shot status VFX kończy się według czasu walki;
- aktywne i nowo pobrane z puli ParticleSystemy otrzymują poprawny
  `simulationSpeed`;
- `BattleView.SetCombatSpeed` propaguje wartość do obiektów istniejących oraz
  tworzonych po przełączeniu;
- cleanup przywraca `1x`.

### 9.4. Play Mode i smoke test

W scenie `Battle` sprawdzić:

- próg aktywuje się dopiero po starcie faktycznego combat;
- `Fight!` pozostaje w normalnym tempie;
- przełączenie podczas ruchu, windupu, speciala i lotu pocisku;
- jednoczesne przyspieszenie eventów i odpowiadających im animacji;
- killing blow, death i końcowe VFX nie rozjeżdżają się;
- wynik rundy i preparation kolejnej rundy działają w `1x`;
- następny combat ponownie zaczyna od `1x`;
- restart sceny i przerwanie walki nie pozostawiają mnożnika;
- zachowanie po pause/background/resume;
- walka zakończona przez `MaxCombatTicks` wykonuje pełny cleanup.

## 10. Wydajność mobilna

Implementacja powinna zachować:

- `0 B GC.Alloc` w `BattleCombatRunner.Advance` po rozgrzaniu;
- `0 B GC.Alloc` podczas propagowania i używania mnożnika;
- brak LINQ, closure tworzonych per frame i stringów w hot path;
- jedną zmianę Animatora i ParticleSystemów na aktywację, a nie na klatkę;
- istniejący pooling pocisków, efektów, overlayów i status VFX;
- twardy limit `MaxTicksPerFrame`.

Profilować co najmniej:

- CPU `BattleCombatRunner.Advance` przed i po progu;
- liczbę ticków wykonywanych w jednej klatce;
- koszt Animatorów i ParticleSystemów przy maksymalnej liczbie jednostek;
- długość kolejek ruchu i liczbę aktywnych pocisków przy przyspieszeniu;
- frame pacing przy spadku do 30 i 20 FPS;
- brak wzrostu pamięci po wielu rundach i restartach.

Zmiana nie dotyka URP, shaderów, wariantów, tekstur, overdraw ani build size i
nie wymaga nowych pakietów.

## 11. Ryzyka i zabezpieczenia

| Ryzyko | Zabezpieczenie |
| --- | --- |
| Zmiana fixed ticka wpływa na wynik walki | Skalować wyłącznie wejście accumulatura; `TickDuration` pozostaje stały |
| Próg aktywuje się coraz wcześniej przez własny mnożnik | Osobny, nieprzyspieszony `CombatElapsedTime` |
| Symulacja wyprzedza prezentację | Jeden mnożnik runnera, event zmiany i skalowanie wszystkich lokalnych zegarów |
| Animator ataku jest przyspieszony podwójnie niezgodnie z intencją | Jawny test kontraktu `attackSpeed * animator.speed`; oba mnożniki mają różne role |
| Obiekt z puli dziedziczy poprzednią prędkość | Setter przy pobraniu i reset przy zwrocie/cleanupie |
| Status one-shot kończy się w tempie `1x` | Usunięcie deadline opartego o `Time.time`, skalowany licznik w controllerze |
| Za wysoki mnożnik powoduje spike CPU | Zachowanie `MaxTicksPerFrame`, brak automatycznego podnoszenia limitu, profilowanie |
| Kilka ticków na klatkę przepełnia kolejkę ruchu | Test maksymalnego mnożnika i obserwacja `MaxQueuedMoves`; tuning przed zwiększaniem pojemności |
| Przyspieszenie wycieka do preparation/UI | Brak `Time.timeScale`, reset `BattleView` i test kolejnej rundy |
| Subskrypcja pozostaje po restarcie | Symetryczny subscribe/unsubscribe w controllerze i standalone bootstrapie |
| Przejście jest widoczne klatkę za późno | Zdefiniowana semantyka granicy klatki i wcześniejszy execution order runnera |

## 12. Przewidywane pliki

Kod i konfiguracja:

- `Assets/DeckBattle/Scripts/Battle/BattleTiming.cs`;
- `Assets/DeckBattle/Scripts/Data/BattleTimingConfig.cs`;
- `Assets/DeckBattle/Data/Configs/BattleTimingConfig_MVP.asset`;
- `Assets/DeckBattle/Scripts/Battle/BattleCombatRunner.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleController.cs`;
- `Assets/DeckBattle/Scripts/Battle/StandaloneBattleBootstrap.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitViewRegistry.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitView.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleProjectilePresenter.cs`;
- `Assets/DeckBattle/Scripts/Battle/ProjectileView.cs`;
- `Assets/DeckBattle/Scripts/Battle/BattleEffectPresenter.cs`;
- `Assets/DeckBattle/Scripts/Battle/PooledBattleEffect.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitStatusOverlayController.cs`;
- `Assets/DeckBattle/Scripts/Battle/UnitStatusVfxController.cs`;
- `Assets/DeckBattle/Scripts/Battle/StatusVfxView.cs`.
- `Assets/DeckBattle/Scripts/UI/BattleUIController.cs`;
- `Assets/DeckBattle/Scenes/Battle.unity`.

Testy:

- nowy `Assets/DeckBattle/Tests/EditMode/BattleTimingConfigTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattleCombatRunnerTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/BattlePresentationContractTests.cs`;
- `Assets/DeckBattle/Tests/EditMode/UnitViewFacingTests.cs`;
- testy widoków pocisku, efektu, overlaya i status VFX w istniejących lub małych,
  dedykowanych plikach.
- `Assets/DeckBattle/Tests/EditMode/BattleUIControllerTests.cs`.

Nie powinny być potrzebne zmiany `BattleSimulation`, resolverów, `BattleEvent`,
URP, prefabów ani sceny. Scenę należy jedynie zweryfikować przez Unity MCP po
zmianie serialized config.

## 13. Definition of Done

Zadanie jest zakończone, gdy:

- Inspector udostępnia dokładnie kontrolę opóźnienia i prędkości;
- przyspieszenie aktywuje się raz na combat według nieprzyspieszonego zegara;
- fixed tick, wynik, RNG i kolejność eventów pozostają niezmienione;
- wszystkie zegary prezentacji walki używają tego samego mnożnika;
- przełączenie w połowie animacji nie powoduje teleportu ani desynchronizacji;
- `Fight!`, round result i preparation pozostają w `1x`;
- cleanup i kolejna runda przywracają `1x`;
- synchronous fallback nadal daje ten sam wynik;
- testy wąskie oraz pełny zestaw Edit Mode przechodzą w Unity Editor;
- Play Mode smoke test obejmuje zmianę prędkości i reset;
- Profiler potwierdza brak nowych alokacji i akceptowalny frame pacing na
  mid-range mobile;
- wartości produkcyjnego assetu zostały zaakceptowane i zapisane jawnie.
