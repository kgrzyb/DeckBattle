# Plan: special `Arrgh!` dla Cpt. Sabatiniego

## 1. Cel i ustalone zachowanie

Dodać Cpt. Sabatiniemu drużynowy special `Arrgh!`, który po zebraniu pełnej many
nakłada `Empower` na wszystkie żywe sojusznicze jednostki na `5 s`.

Przebieg:

1. po zebraniu pełnej many Sabatini rozpoczyna istniejący cast speciala i od
   razu zużywa manę;
2. animacja oraz cue `SpecialCast` rozpoczynają się razem z castem;
3. po `0.3 s` od rozpoczęcia castu symulacja nakłada `Empower` na wszystkie
   żywe jednostki po tej samej stronie co Sabatini;
4. special kończy się po `CastDuration`, a Sabatini przechodzi przez istniejący
   recovery lock i wraca do zwykłego cyklu walki.

Założenia planu:

- zbiór sojuszników jest ustalany w chwili payloadu, a nie na początku castu;
- Sabatini jest sojusznikiem samego siebie, więc również otrzymuje `Empower`;
- martwe jednostki nie otrzymują statusu;
- special nie wymaga celu, zasięgu ani obecności przeciwnika;
- `Empower` korzysta z obecnej wartości `DefaultMagnitude = 0.5`, czyli zwiększa
  outgoing damage o `50%` przed ograniczeniami z runtime tuningu;
- czas `5 s` jest własnością tego speciala i powinien być zapisany jako override,
  aby późniejsza zmiana domyślnego czasu assetu `Empower` nie zmieniła balansu
  Sabatiniego;
- logika aktywacji pozostaje w deterministycznej symulacji. Animacja ani
  `Animation Event` nie nakładają statusu.

## 2. Stan projektu istotny dla wdrożenia

Projekt ma już potrzebną infrastrukturę:

- `SpecialCycleResolver` obsługuje absolutny deadline `SpecialEffectTime` i
  konfigurowalne `EffectDelay`;
- `HasteBurst` pokazuje wzorzec statusowego speciala aktywowanego po `0.3 s`;
- `StatusResolver.TryApply` jest jedyną poprawną ścieżką nakładania i odświeżania
  statusu oraz emituje istniejące eventy `StatusApplied`/`StatusRefreshed`;
- `UnitSpecialDefinition` i immutable `UnitSpecialCombatSpec` już przenoszą
  definicję statusu, lifetime mode i override czasu;
- formatter opisu karty już obsługuje `{status}`, `{statusDuration}` oraz
  `{statusMagnitudePercent}`;
- asset `Empower` istnieje i ma `RefreshPerSource`, `DefaultDuration = 5`,
  `DefaultMagnitude = 0.5` oraz `MaxStacks = 1`;
- `Cpt.Sabatini.asset` ma obecnie puste pole `Special`;
- `Sabatini.overrideController` mapuje istniejący stan `Special` na klip
  `root|Special`; prefab ma wyłączony root motion;
- klip speciala Sabatiniego zawiera klatki `0..89`, nie jest zapętlony i nie ma
  Animation Eventów. Jego dokładną długość i moment powrotu do idle trzeba
  potwierdzić w Unity Animation Preview;
- `_StatusPresentationCatalog` nie ma obecnie wpisu dla `Empower`, więc bez
  uzupełnienia katalogu status nie będzie miał produkcyjnej prezentacji.

Nie potrzeba nowego pola targetowania w `UnitSpecialDefinition`. Obecna
architektura i tak rozdziela zachowanie po `UnitSpecialKind`, a uogólnienie
target scope dla jednego przypadku niepotrzebnie poszerzyłoby zmianę.

## 3. Dane speciala

### 3.1. Nowy rodzaj speciala

Rozszerzyć `UnitSpecialKind` w `UnitDefinition.cs`, dopisując wartość na końcu
bez zmiany istniejących numerów zapisanych w assetach Unity:

```csharp
Arrgh = 6
```

Wykrzyknik należy do nazwy prezentacyjnej `Arrgh!`. Identyfikatory techniczne
używają bezpiecznej formy bez znaku interpunkcyjnego: `Arrgh`/`arrgh`.

### 3.2. Walidacja immutable combat specu

W `UnitSpecialCombatSpec.IsValid` dodać przypadek `Arrgh`, wymagający:

```text
AppliedStatus.Kind == StatusKind.Empower
CastDuration > 0
EffectDelay <= CastDuration
AppliedStatusLifetimeMode != OverrideSeconds || AppliedStatusDuration > 0
```

Nie dodawać nowych pól do `UnitSpecialDefinition` ani
`UnitSpecialCombatSpec` — obecne dane statusowe w pełni opisują payload.

### 3.3. Asset speciala i przypięcie

Utworzyć `Assets/DeckBattle/Data/Specials/Special_Arrgh.asset`:

```text
SpecialId: arrgh
Kind: Arrgh
Description: Grant all allied units {status} for {statusDuration}, increasing
             damage dealt by {statusMagnitudePercent}.
EffectDelay: 0.3
CastDuration: 1.5 (wartość startowa do potwierdzenia z klipem)
AppliedStatus: Empower
AppliedStatusLifetimeMode: OverrideSeconds
AppliedStatusDurationOverride: 5
Projectile: null
StrikeCount: 1
AttackDamageMultiplier: 1
EffectRadius: 0
ExecuteHpThresholdPercent: 0
VfxProfile: opcjonalny profil Sabatiniego albo null dla MVP
```

`CastDuration` nie steruje chwilą nałożenia statusu — odpowiada jedynie za czas
blokady akcji i prezentacji castu. Punktem startowym jest około `1.5 s`,
wynikające z klipu `0..89` przy typowym imporcie `60 FPS`; przed zapisaniem
wartości produkcyjnej trzeba sprawdzić rzeczywisty czas klipu w Inspectorze i
upewnić się, że zakończenie castu nie ucina animacji. Niezależnie od tej korekty
`EffectDelay` pozostaje dokładnie `0.3 s`.

Przypisać nowy asset do pola `Special` w
`Assets/DeckBattle/Data/Units/Cpt.Sabatini.asset`.

## 4. Logika symulacji

### 4.1. Start i opóźnienie payloadu

`Arrgh` korzysta bez zmian z istniejącego `StartCast`:

- przy starcie przechodzi do `Casting`;
- zeruje manę i emituje `UnitManaChanged`;
- emituje `SpecialCastStarted`;
- zapisuje `SpecialEffectTime = castStart + 0.3`;
- nie zapisuje ani nie blokuje targetu.

Przy produkcyjnym `CombatTickDuration = 0.15 s` payload przypadnie dwa ticki po
starcie castu. Resolver nadal powinien porównywać absolutny deadline z
`DeadlineEpsilon`, dzięki czemu większy tick aktywuje efekt przy pierwszym ticku
osiągającym deadline, bez zgubienia i bez podwójnej aplikacji.

### 4.2. Aplikacja na drużynę

W `AdvanceCast` dodać gałąź `UnitSpecialKind.Arrgh`, która po osiągnięciu
`SpecialEffectTime` wywoła dedykowany helper, np.
`ApplyStatusToAllAllies`.

Helper wykonuje jeden liniowy przebieg po `simulation.Units` w stabilnej
kolejności i dla każdego kandydata sprawdza:

```text
candidate != null
candidate.IsAlive
candidate.Side == caster.Side
```

Dla każdego pasującego celu wywołuje:

```csharp
StatusResolver.TryApply(
    simulation,
    candidate,
    new StatusApplicationRequest(
        special.AppliedStatus,
        caster.UnitId,
        special.AppliedStatusDuration,
        lifetimeMode: special.AppliedStatusLifetimeMode),
    eventQueue);
```

Zasady implementacyjne:

- nie używać LINQ, fizyki, zapytań sceny ani listy tymczasowej;
- każdy cel emituje własny event statusu przez `StatusResolver`;
- `CapacityReached` lub odrzucenie na pojedynczej jednostce nie wycofuje statusów
  już nałożonych na pozostałych sojuszników;
- nie wywoływać `AttackCycleResolver.RefreshCooldownForSpecialCast`, ponieważ
  `Empower` modyfikuje damage, a nie cooldown ataku;
- po pierwszej próbie payloadu ustawić `SpecialEffectTime` na infinity zgodnie z
  istniejącym przepływem, aby efekt nie mógł wykonać się ponownie;
- kolejność względem innych akcji w tym samym ticku pozostaje obecna i
  deterministyczna: statusowe payloady są wykonywane podczas
  `AdvanceActiveCasts`, przed zwykłymi atakami rozstrzyganymi później w ticku.

Nie dodawać osobnego eventu `ArrghApplied`. Istniejący
`SpecialCastStarted` opisuje cast, a eventy statusów są wystarczającym źródłem
prawdy dla overlaya i VFX każdej jednostki.

### 4.3. Przerwanie i zakończenie

Zachować obecne reguły cyklu speciala:

- śmierć, stun, sleep lub silence Sabatiniego przed `0.3 s` anuluje cast po
  wydaniu many i status nie zostaje nałożony;
- przerwanie po payloadzie nie usuwa już nałożonego `Empower`;
- śmierć sojusznika przed payloadem wyklucza go z aplikacji;
- `SpecialCastCompleted` jest emitowany po `CastDuration`, niezależnie od tego,
  ilu sojuszników przyjęło status;
- ponowny cast tego samego Sabatiniego odświeża `Empower` tego źródła zgodnie z
  `RefreshPerSource` do pełnych `5 s` od nowej aplikacji;
- dwa różne źródła `Empower` zachowują obecną semantykę statusu per source. Nie
  zmieniać jej w ramach tego zadania.

## 5. Prezentacja

### 5.1. Animacja

Nie dodawać Animation Eventu do klipu. `UnitView.BeginSpecialCast` powinien
uruchomić już podpięty stan `Special`, a wynik gameplayowy pozostaje związany z
deadline'em symulacji.

W Unity sprawdzić:

- czy `Sabatini.overrideController` odtwarza `root|Special`;
- czy `CastDuration` pozwala klipowi zakończyć się bez ucięcia;
- czy `EffectDelay = 0.3` wizualnie odpowiada momentowi gestu/okrzyku, w którym
  buff powinien pojawić się na drużynie;
- czy root motion pozostaje wyłączony, a model nie przesuwa logicznej pozycji.

### 5.2. Status i VFX

Dodać `StatusKind.Empower` do `_StatusPresentationCatalog` jako lekki wpis
`Icon`, używając ikony już przypisanej do `Empower.asset`. Dzięki temu każdy
sojusznik pokazuje buff bez tworzenia nowych runtime'owych obiektów VFX.

Jeżeli special ma otrzymać osobny burst na casterze, utworzyć mały,
poolowany `BattleVfxProfile` z cue `SpecialCast` i przypisać go do assetu
speciala. Jest to opcjonalna warstwa contentowa i nie może blokować implementacji
gameplayu. Nie tworzyć po jednym ciężkim particle effect na każdego sojusznika
bez pomiaru overdraw i frame time na urządzeniu mobilnym.

## 6. Testy

### 6.1. `UnitSpecialCombatSpec`

Dodać testy graniczne:

- `Arrgh` z `AppliedStatus = Empower` i dodatnim czasem jest poprawny;
- brak statusu albo inny `StatusKind` daje niepoprawny special;
- `OverrideSeconds` z czasem `0` jest niepoprawny;
- wartości istniejących enumów pozostają bez zmian, a nowy kind ma wartość `6`.

### 6.2. `SpecialCycleResolverTests`

Dodać małą symulację z Sabatinim, dwoma żywymi sojusznikami, martwym
sojusznikiem i przeciwnikiem. Zweryfikować:

1. cast startuje bez targetu, zużywa manę i nie nakłada statusu od razu;
2. po `0.15 s` od startu nadal nie ma `Empower`, a dokładnie po osiągnięciu
   deadline'u `0.3 s` status pojawia się raz;
3. caster i wszyscy żywi sojusznicy mają `Empower` ze
   `SourceUnitId = caster.UnitId`;
4. przeciwnik i martwy sojusznik nie mają statusu;
5. powstaje dokładnie po jednym `StatusApplied` na żywego sojusznika;
6. każdy status ma deadline odpowiadający `5 s` od chwili payloadu i wygasa po
   tym czasie;
7. ponowny cast emituje `StatusRefreshed`, nie tworzy drugiego statusu tego
   samego źródła i ustawia pełne kolejne `5 s`;
8. przerwanie castu przed `0.3 s` nie nakłada statusu, a przerwanie po payloadzie
   go nie usuwa;
9. zapełniona kolekcja statusów jednego sojusznika nie blokuje aplikacji na
   pozostałych;
10. payload nie uruchamia się drugi raz podczas dłuższego `CastDuration`.

### 6.3. Integracja danych i prezentacji

Rozszerzyć test produkcyjnych assetów lub dodać dedykowany test, który ładuje
`Cpt.Sabatini.asset` i sprawdza:

- przypisany special ma `Kind == Arrgh`;
- `EffectDelay == 0.3f`;
- override czasu statusu wynosi `5f`;
- `AppliedStatus.Kind == Empower`;
- szablon opisu jest poprawny i formatuje `Empower`, `5 s` oraz `50%`;
- katalog prezentacji ma wpis `Icon` dla `Empower`.

Po implementacji uruchomić najpierw testy `SpecialCycleResolverTests`, potem
`CardDescriptionTemplateFormatterTests` i `CombatSpecBoundaryTests`, a na końcu
pełny zestaw Edit Mode przez otwarty Unity Editor/Unity MCP. Nie uruchamiać Edit
Mode tests w batchmode.

## 7. Kolejność wdrożenia

1. Dodać `UnitSpecialKind.Arrgh = 6` i walidację w
   `UnitSpecialCombatSpec.IsValid`.
2. Dodać bezalokacyjną gałąź payloadu drużynowego w `SpecialCycleResolver`.
3. Dodać testy symulacji timingu, selekcji sojuszników, refreshu, expiry i
   przerwań.
4. Utworzyć `Special_Arrgh.asset` i przypisać go Sabatiniemu.
5. Dodać wpis `Empower` do `_StatusPresentationCatalog` oraz test danych/opisu.
6. W Unity sprawdzić import klipu, dobrać finalny `CastDuration` i wykonać Play
   Mode smoke test dla obu stron bitwy.
7. Na urządzeniu lub profilu mobilnym sprawdzić frame time i brak GC Alloc w
   ticku nakładającym status na pełną drużynę.

## 8. Kryteria akceptacji

- Sabatini z pełną maną rozpoczyna special bez wymaganego celu;
- mana jest wydana na starcie castu, a `Empower` nie pojawia się przed `0.3 s`;
- przy pierwszym ticku osiągającym `0.3 s` każdy żywy sojusznik, łącznie z
  Sabatinim, otrzymuje dokładnie jeden `Empower` na `5 s`;
- przeciwnicy i martwe jednostki nie otrzymują statusu;
- ponowna aplikacja tego samego źródła odświeża czas zgodnie z
  `RefreshPerSource`;
- status wpływa na damage przez istniejący `EffectiveStatsResolver`, bez
  specjalnego kodu w UI lub prezentacji;
- overlay pokazuje ikonę `Empower`, a całość nie wymaga instancji, coroutine,
  LINQ ani alokacji per cel;
- istniejące speciale oraz ich wartości enum nie mają regresji;
- animacja speciala nie jest ucinana i nie steruje wynikiem symulacji.
