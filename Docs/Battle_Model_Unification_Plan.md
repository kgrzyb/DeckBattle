# Deck Battle - Battle Model Unification Plan

## 1. Cel Dokumentu

Ten dokument opisuje plan ujednolicenia modelu walki w projekcie Deck Battle.

Obecnie w repo istnieja dwa poziomy logiki:

- starszy przeplyw meczu, rundy i preparacji oparty o `BattleState` oraz `RuntimeUnit`,
- nowsza realtime symulacja walki oparta o `BattleSimulation`, `UnitRuntimeState`, `BattleTickLoop` oraz `BattleEventQueue`.

Celem migracji jest doprowadzenie do stanu, w ktorym:

- logika realtime walki uzywa tylko `BattleSimulation`,
- prezentacja walki reaguje tylko na `BattleEventQueue`,
- `BattleState` odpowiada tylko za stan meczu, rundy, kart, AP, slotow i HP graczy,
- stary tick walki oparty o `RuntimeUnit` zostaje usuniety,
- kod pozostaje deterministyczny, testowalny i przyjazny dla mobile.

## 2. Stan Obecny

### Starszy Tor

Starszy tor jest zwiazany z pelna petla meczu:

- `BattleState`
- `PlayerBattleState`
- `RuntimeUnit`
- `PreparationTurnService`
- `UnitPlayService`
- `FormationService`
- `BattleController`
- `BattleStateCombatTickLoop`
- `CombatSimulator`
- `MovementService`
- `TargetingService`
- `DamageService`
- `RoundFlowService`
- `RoundDamageResolver`

Ten tor odpowiada za:

- tworzenie meczu,
- talie i reke,
- AP,
- sloty wystawienia,
- faze przygotowania,
- aktywna strone przygotowania,
- jednostki ustawione w formacji,
- start kolejnej rundy,
- obrazenia po rundzie,
- koniec meczu.

Problem: ten tor ma tez wlasna walke realtime przez `BattleStateCombatTickLoop` i `CombatSimulator`.

### Nowszy Tor

Nowszy tor jest zwiazany z czysta symulacja walki:

- `BattleSimulation`
- `UnitRuntimeState`
- `UnitSpawnData`
- `BattleTickLoop`
- `BattleEventQueue`
- `BattleEvent`
- `TargetSelector`
- `AttackPositionSelector`
- `MovementResolver`
- `CombatResolver`
- `BattleView`

Ten tor odpowiada za:

- realtime pozycje jednostek,
- indeksowanie jednostek po hexie i ID,
- targetowanie,
- ruch,
- cooldown ataku,
- obrazenia,
- smierc jednostek,
- koniec walki,
- eventy dla widoku.

Problem: ten tor nie jest jeszcze glownym torem walki w `BattleController`.

## 3. Decyzja Architektoniczna

Docelowy podzial odpowiedzialnosci:

```text
BattleState / Match State
  - rundy
  - talie
  - reka
  - AP
  - sloty
  - HP graczy
  - fazy meczu
  - przygotowanie/formacja

BattleSimulation
  - realtime walka
  - aktualne hexy jednostek podczas walki
  - aktualne HP jednostek podczas walki
  - targety
  - cooldowny
  - ruch
  - ataki
  - wynik walki

BattleEventQueue
  - jedyny kanal komunikacji z prezentacja walki
```

Wazna zasada:

```text
Stan meczu decyduje, kiedy walka startuje.
Symulacja walki decyduje, co dzieje sie w walce.
Widok tylko pokazuje eventy.
```

## 4. Zakres Migracji

### W Zakresie

- Przeniesienie realtime walki w `BattleController` na `BattleSimulation`.
- Stabilne ID jednostek w `BattleSimulation`.
- Mapper ze stanu preparacji do `BattleSimulation`.
- Synchronizacja wyniku `BattleSimulation` z `BattleState` przed `RoundDamageResolver`.
- Usuniecie `BattleStateCombatTickLoop` po migracji.
- Usuniecie lub zastapienie starego `CombatSimulator`, jezeli nie bedzie juz uzywany.
- Aktualizacja testow.

### Poza Zakresem Pierwszego Kroku

- Pelne usuniecie `BattleState`.
- Usuniecie mechaniki kart, AP, slotow i rund.
- Przebudowa calego UI.
- Dodawanie nowych zasad walki.
- Zmiana Unity, URP albo paczek.

## 5. Etap 1 - Stabilne ID Jednostek W BattleSimulation

### Problem

`BattleSimulation.Create()` obecnie nadaje jednostkom ID na podstawie pozycji w liscie:

```text
UnitId = i + 1
```

Starszy tor uzywa `RuntimeUnit.RuntimeId`, ktory jest stabilnym ID w ramach meczu.

Jezeli eventy z `BattleSimulation` maja sterowac istniejacymi `UnitView`, ID musza byc zgodne.

### Zadania

- Rozszerzyc `UnitSpawnData` o `UnitId`.
- Zmienic `BattleSimulation.Create()`, aby uzywalo `spawn.UnitId`.
- Dodac walidacje:
  - `UnitId > 0`,
  - brak duplikatow ID,
  - brak duplikatow pozycji startowych.
- Zaktualizowac testy `BattleSimulationTests`.
- Zaktualizowac miejsca tworzace `UnitSpawnData`.

### Kryteria Zakonczenia

- `BattleSimulation` zachowuje ID przekazane ze spawna.
- Eventy walki zawieraja stabilne ID jednostek.
- Testy wykrywaja duplikaty ID.

## 6. Etap 2 - Mapper BattleState Do BattleSimulation

### Cel

Na starcie fazy walki `BattleController` powinien budowac `BattleSimulation` z jednostek ustawionych w preparacji.

### Zadania

- Dodac mapper, np. `BattleSimulationFactory`.
- Mapper powinien przyjmowac:
  - `BattleState`,
  - `BattleRuntimeTuning`.
- Mapper powinien tworzyc `UnitSpawnData` z:
  - `RuntimeUnit.RuntimeId` jako `UnitId`,
  - `RuntimeUnit.Definition`,
  - `RuntimeUnit.Side`,
  - `RuntimeUnit.BattleCoord` albo `FormationCoord` jako startowy hex, zgodnie z aktualnym flow.
- Mapper powinien pomijac jednostki null i martwe, jezeli takie moga wystapic.
- Dodac testy mappera.

### Kryteria Zakonczenia

- Dla ustawionych jednostek gracza i AI powstaje poprawna `BattleSimulation`.
- Pozycje startowe odpowiadaja pozycjom bojowym w `BattleState`.
- ID w symulacji odpowiadaja ID jednostek w `BattleState`.

## 7. Etap 3 - BattleController Uzywa BattleTickLoop

### Stan Obecny

`BattleController.RunCombatRoutine()` uzywa:

- `BattleStateCombatTickLoop`
- `BattleEventQueue`
- `ProcessCombatEvents()`

### Stan Docelowy

`BattleController.RunCombatRoutine()` powinien uzywac:

- `BattleSimulation`
- `BattleTickLoop`
- `BattleEventQueue`
- `ProcessCombatEvents()`

`ProcessCombatEvents()` moze w duzej mierze zostac, bo eventy maja te same typy:

- `UnitMoved`
- `UnitAttackStarted`
- `UnitDamaged`
- `UnitDied`
- `BattleEnded`

### Zadania

- Dodac pole `BattleSimulation activeSimulation`.
- Dodac pole `BattleTickLoop activeTickLoop`.
- Na starcie walki zbudowac `activeSimulation` z `BattleState`.
- Tickowac `activeTickLoop.Tick(activeSimulation, combatEventQueue)`.
- Obslugiwac `BattleTickResult.BattleEnded`.
- Dodac limit tickow, ktory zastapi obecne `maxCombatTicks`.
- Po zakonczeniu walki ustawic faze `BattlePhase.RoundResolution`.
- Przed `RoundDamageResolver` zsynchronizowac wynik symulacji do `BattleState`.

### Kryteria Zakonczenia

- `BattleController` nie tworzy juz `BattleStateCombatTickLoop`.
- Walka w scenie dziala przez `BattleSimulation`.
- Widok nadal reaguje na eventy.
- Runda przechodzi do `RoundResolution`.

## 8. Etap 4 - Synchronizacja Wyniku Walki

### Problem

`RoundDamageResolver` liczy obrazenia po rundzie na podstawie zywych jednostek w `BattleState`.

Po walce w `BattleSimulation` trzeba przepisac wynik do `RuntimeUnit`, dopoki `BattleState` nadal przechowuje jednostki jako `RuntimeUnit`.

### Zadania

- Dodac synchronizator, np. `BattleSimulationResultApplier`.
- Synchronizator powinien:
  - znalezc `RuntimeUnit` po ID,
  - przepisac `CurrentHp`,
  - przepisac `BattleCoord`,
  - przepisac `IsDefeated`,
  - zachowac `FormationCoord`.
- Zadbac, aby martwe jednostki nie byly liczone jako ocalale.
- Po synchronizacji wywolac `RoundFlowService.ResolveRoundAndStartNext(state)`.

### Kryteria Zakonczenia

- `RoundDamageResolver` dostaje aktualny stan po walce.
- Obrazenia po rundzie sa liczone z jednostek, ktore przezyly w `BattleSimulation`.
- Po kolejnej rundzie jednostki wracaja na formacje przez istniejacy flow.

## 9. Etap 5 - Testy Przejsciowe

### Testy Edit Mode

Dodac albo zaktualizowac testy:

- `BattleSimulation` zachowuje przekazane `UnitId`.
- `BattleSimulation` odrzuca duplikaty `UnitId`.
- Mapper tworzy poprawne spawn data z `BattleState`.
- Synchronizator przepisuje HP, pozycje i smierc do `BattleState`.
- Po walce `RoundDamageResolver` liczy obrazenia z wyniku symulacji.
- `BattleController` albo nizszy serwis konczy walke po `BattleEnded`.

### Test Manualny W Unity

Sprawdzic:

- start sceny Battle,
- zagranie jednostek,
- AI wystawia jednostki,
- klikniecie Ready,
- realtime walka,
- animacje ruchu/ataku/obrazen/smierci,
- obrazenia po rundzie,
- kolejna runda,
- koniec meczu.

## 10. Etap 6 - Usuniecie Starego Toru Walki

Po potwierdzeniu, ze scena i testy dzialaja na `BattleSimulation`, mozna usuwac stary tor.

### Kandydaci Do Usuniecia

- `BattleStateCombatTickLoop`
- `CombatSimulator`, jezeli nie ma juz fallbacku bez animacji
- `MovementService`, jezeli caly ruch przejal `MovementResolver`
- `TargetingService`, jezeli cale targetowanie przejal `TargetSelector`
- `DamageService`, jezeli caly damage przejal `CombatResolver`

### Warunek Usuniecia

Nie usuwac klasy tylko dlatego, ze wyglada na stara.

Przed usunieciem sprawdzic:

- `rg "ClassName"` nie pokazuje aktywnych uzyc,
- testy starego toru zostaly usuniete albo przepisane,
- scena Battle nie zalezy od klasy,
- nie ma zaleznosci w prefabach albo komponentach Unity.

## 11. Etap 7 - Decyzja O RuntimeUnit

Po migracji walki zostanie pytanie, co z `RuntimeUnit`.

### Opcja A - Bezpieczna

Zostawic `RuntimeUnit` jako jednostke w stanie meczu/preparacji.

Wtedy:

- `RuntimeUnit` zna formacje i stan miedzy rundami,
- `BattleSimulation` zna stan realtime walki,
- wynik walki jest synchronizowany po rundzie.

Plusy:

- mniejszy zakres zmian,
- mniejsze ryzyko regresji w kartach, AP, formacji i UI,
- prostszy pierwszy etap migracji.

Minusy:

- nadal istnieja dwa typy jednostki,
- potrzebny mapper i synchronizator.

### Opcja B - Czysta

Zastapic `RuntimeUnit` przez rozszerzony `UnitRuntimeState` albo nowy wspolny typ.

Wtedy:

- `PlayerBattleState.Units` zmienia typ listy,
- formacja przenosi sie do wspolnego typu jednostki,
- walka i preparacja uzywaja tego samego runtime state.

Plusy:

- jeden typ jednostki,
- mniej mapowania.

Minusy:

- wiekszy blast radius,
- wiecej zmian w UI, input, kartach, AI i testach,
- wieksze ryzyko regresji.

### Rekomendacja

Najpierw wdrozyc Opcje A.

Dopiero po stabilizacji walki przez `BattleSimulation` zdecydowac, czy koszt Opcji B jest uzasadniony.

## 12. Proponowany Podzial Commitow

1. Add stable unit ids to battle simulation.
2. Add battle state to simulation mapper.
3. Add simulation result applier.
4. Switch battle controller combat loop to battle simulation.
5. Update tests for unified combat path.
6. Remove legacy battle state combat loop.
7. Revisit RuntimeUnit ownership after stabilization.

## 13. Ryzyka

### Rozjazd ID Jednostek

Najwieksze ryzyko na starcie migracji.

Mitigacja:

- stabilne `UnitId` w `UnitSpawnData`,
- test duplikatow ID,
- test mapowania eventow do widokow.

### Bledne Obrazenia Po Rundzie

`RoundDamageResolver` nadal czyta `BattleState`.

Mitigacja:

- synchronizacja `BattleSimulation -> BattleState` przed rozliczeniem rundy,
- test, w ktorym jedna jednostka ginie w symulacji i nie zadaje `Power`.

### Podwojna Logika Ruchu

Przez pewien czas moga istniec:

- `MovementService`,
- `MovementResolver`.

Mitigacja:

- po migracji `BattleController` usunac aktywne uzycia `MovementService`,
- nie rozwijac starego toru,
- testy utrzymywac przy nowym torze.

### Regresje UI

`UnitView` obecnie umie bindowac jednostki z roznych modeli.

Mitigacja:

- nie przebudowywac UI w tym samym kroku,
- zachowac eventy `BattleEvent`,
- utrzymac mapowanie widokow po ID.

### Alokacje W Ticku

Migracja nie powinna pogorszyc stabilnosci frame time na mobile.

Mitigacja:

- nie tworzyc list w kazdym ticku bez potrzeby,
- uzywac workspace juz istniejacych w `MovementResolver` i `TargetSelector`,
- sprawdzic Profiler po migracji.

## 14. Kryteria Zakonczenia Migracji

Migracja moze zostac uznana za zakonczona, gdy:

- `BattleController` nie uzywa `BattleStateCombatTickLoop`,
- realtime walka w scenie Battle dziala przez `BattleSimulation`,
- eventy walki nadal steruja prezentacja,
- po walce `RoundDamageResolver` liczy poprawne obrazenia,
- kolejna runda startuje poprawnie,
- testy edit mode przechodza,
- nie ma aktywnych uzyc starego toru walki,
- usunieto albo oznaczono do usuniecia martwe klasy starego combat flow.

## 15. Kolejnosc Pracy Rekomendowana

Najbezpieczniejsza kolejnosc:

1. Stabilne ID w `BattleSimulation`.
2. Mapper `BattleState -> BattleSimulation`.
3. Synchronizator `BattleSimulation -> BattleState`.
4. Testy mappera i synchronizatora.
5. Podmiana `BattleController.RunCombatRoutine()`.
6. Manualny test sceny Battle.
7. Usuniecie `BattleStateCombatTickLoop`.
8. Przeglad `RuntimeUnit` i decyzja, czy migrowac go pozniej.

Nie laczyc podmiany glownego combat loopa z usuwaniem `RuntimeUnit`.

Najpierw trzeba miec jeden dzialajacy tor walki. Dopiero potem warto sprzatac model jednostek.
