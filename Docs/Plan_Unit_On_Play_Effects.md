# Plan implementacji efektów `OnPlay` jednostek

## 1. Cel

Celem jest dodanie data-driven systemu efektów przypisanych do jednostek,
uruchamianych dokładnie raz w chwili poprawnego zagrania karty jednostki.
Trigger `OnPlay` ma działać w fazie przygotowania, natomiast efekty należące do
walki mają zostać zapisane jako oczekujące i zmaterializowane na początku
najbliższej fazy combat.

Pierwszy pionowy przekrój funkcji:

- każda produkcyjna jednostka otrzymuje ten sam efekt `OnPlay`;
- celem jest zagrana jednostka (`self`);
- efekt zwiększa jej bazowy `Attack` o 25%;
- bonus działa przez całą najbliższą walkę;
- trigger nie uruchamia się ponownie w następnych rundach;
- po zakończeniu najbliższej walki bonus znika.

System ma docelowo obsługiwać unikalne efekty per jednostka, w tym efekty
działające na inne jednostki i obszary. Pierwszy content korzysta tylko z celu
`self`, ale model danych i resolver targetów nie mogą zakładać, że źródło jest
zawsze celem.

## 2. Ustalenia semantyczne

### 2.1. Trigger i czas życia efektu

Należy rozdzielić trzy pojęcia:

1. `Trigger` — zdarzenie uruchamiające definicję. W tym wdrożeniu istnieje tylko
   `OnPlay` i wykonuje się raz po poprawnym zagraniu jednostki.
2. `ResolutionTiming` — moment wykonania logiki efektu. Efekty bojowe są
   kolejkowane w preparation i aplikowane w czasie `0` najbliższej symulacji.
3. `Lifetime`/`Interval` — czas działania i ewentualna periodyczność rezultatu.
   Te ustawienia należą do konkretnego efektu, a nie do triggera.

`OnPlay` nigdy nie powtarza się sam. Efekt może zachować własną semantykę
trwania lub powtarzalności. Status jest tylko jednym z jawnych typów efektu,
stosowanym wyłącznie, gdy taki stan bojowy jest faktycznie wymagany. Nie należy
dodawać coroutine ani harmonogramu delegatów per jednostka.

### 2.2. Wybór celów

Cele są rozstrzygane w chwili zagrania jednostki, po ustaleniu jej
`RuntimeUnitId` i pola wystawienia. Wynik targetowania jest snapshotowany jako
stabilna lista `RuntimeUnitId`:

- późniejsze przesunięcie jednostek w formation nie zmienia odbiorców;
- jednostki zagrane później nie zostają retroaktywnie dodane do efektu;
- efekt obszarowy używa pozycji formacji istniejących w chwili triggera;
- źródło i każdy odbiorca są zachowane oddzielnie;
- targety są zapisywane w kolejności rosnącego `RuntimeUnitId`.

Pierwszy resolver powinien obsługiwać automatyczne zapytania:

- `Self`;
- `AllFriendlyUnits`;
- `AllEnemyUnits`;
- `FriendlyUnitsInRadius`;
- `EnemyUnitsInRadius`;
- `AllUnitsInRadius`.

Promień jest liczony przez `HexBoard.Distance`, a originem jest pole zagranej
jednostki. Targetowanie wymagające dodatkowego wyboru gracza (`SelectedUnit`,
`SelectedHex`) należy przewidzieć w enumie/modelu, ale wdrożyć dopiero razem z
konkretnym efektem i mobilnym workflow inputu. Nie należy teraz komplikować
atomowego zagrania karty drugim etapem UI, ponieważ pierwszy efekt używa `Self`.

Jeżeli w przyszłości efekt ma wybierać cele dopiero na początku walki, powinien
otrzymać jawny tryb `ResolveAtCombatStart`. Nie może to być nieopisany wyjątek
od domyślnego snapshotu `ResolveOnPlay`.

### 2.3. Dokładna reguła bonusu 25%

Bonus dotyczy bazowego `UnitCombatSpec.Attack`, a nie całego końcowego damage i
nie tylko pierwszego ataku.

Docelowa część wzoru:

```text
ModifiedBaseAttack = BaseAttack * (1 + BaseAttackBonusPercent)
RawAttackDamage = ModifiedBaseAttack + AttackBonusNextCombat
```

Dopiero później wykonywane są istniejące modyfikatory obrażeń, armor, critical
i końcowe zaokrąglenie. Obliczenia pozostają w `float`; jedyne zaokrąglenie do
`int` wykonuje obecny końcowy krok `DamageCalculator` przez
`MidpointRounding.AwayFromZero`.

Przykład bez armora i crita:

```text
BaseAttack = 110
ModifiedBaseAttack = 137.5
FinalDamage = 138
```

Nie używać `RuntimeUnit.AttackBonusNextCombat` do tego efektu. To pole jest
legacy mechanizmem spella, jest konsumowane przy pierwszym ataku i nie spełnia
semantyki bonusu trwającego przez całą walkę.

### 2.4. Łączenie modyfikatorów

Pierwszy efekt pochodzi od jednostki i działa na nią samą, więc w normalnym
przebiegu istnieje jedna jego instancja. Ogólna reguła powinna jednak być
jednoznaczna:

- kolejne `ModifyBaseAttackPercent` skierowane na tę samą jednostkę sumują
  procenty w `UnitRuntimeState.BaseAttackBonusPercent`;
- wynik jest ograniczony przez jawny limit tuningu;
- modyfikator nie zajmuje miejsca w `UnitStatusCollection` i nie generuje
  ikon, VFX ani eventów statusu;
- efekt typu `Status` zachowuje własne zasady stackowania i limit statusów.

## 3. Obecny stan i punkty integracji

Obecna architektura ma większość potrzebnych elementów, ale brakuje połączenia
między fazą preparation a statusami symulacji:

- `UnitPlayService.PlayUnit` waliduje zagranie, pobiera AP, tworzy
  `RuntimeUnit` i dodaje go do `PlayerBattleState.Units`;
- `RuntimeUnit` jest trwałym stanem jednostki pomiędzy rundami;
- `BattleSimulationFactory` konwertuje `RuntimeUnit` do `UnitSpawnData`;
- `UnitRuntimeState` i `UnitStatusCollection` istnieją wyłącznie w pojedynczej
  symulacji combat;
- `StatusResolver.TryApply` jest jedyną poprawną ścieżką aplikowania statusu;
- `BattleSimulationResultApplier` kopiuje wynik walki z powrotem i jest dobrym
  miejscem konsumpcji efektów przeznaczonych tylko dla tej walki;
- `CombatEffectDefinition` i `StatusApplicationDefinition` istnieją, ale nie
  są obecnie podłączone do spelli ani jednostek;
- istniejący `Empower` modyfikuje outgoing damage, natomiast wymagany bonus ma
  modyfikować wyłącznie bazowy `Attack`;
- `BattlePresentationSnapshot` nie kopiuje początkowych statusów, więc status
  dodany w czasie `0` nie pojawi się poprawnie w overlay/VFX bez rozszerzenia
  snapshotu.

System powinien rozszerzyć te punkty. Nie należy tworzyć osobnej symulacji
efektów dla preparation ani logiki efektów w `BattleController`/`UnitView`.

## 4. Model danych

### 4.1. Definicja umiejętności `OnPlay`

Dodać `UnitOnPlayEffectDefinition : ScriptableObject`, np. w:

```text
Assets/DeckBattle/Scripts/Data/UnitOnPlayEffectDefinition.cs
```

Definicja przechowuje dane contentowe:

```text
EffectId
DisplayName
Description
UnitEffectStepDefinition[] Steps
```

Jedna umiejętność może składać się z kilku kroków, ponieważ przyszła jednostka
może np. nałożyć shield na siebie i debuff na przeciwników w promieniu. Nie
tworzyć osobnej klasy C# dla każdej jednostki ani `SerializeReference` z
polimorficznymi obiektami.

`UnitEffectStepDefinition` powinien być serializowanym structem zawierającym:

```text
EffectTargetDefinition Target
CombatEffectDefinition Effect
```

`UnitDefinition` otrzymuje jedno opcjonalne pole:

```csharp
public UnitOnPlayEffectDefinition OnPlayEffect;
```

Jedna referencja na jednostkę wystarcza dla obecnego triggera. Złożona
umiejętność używa wielu `Steps`, a nie wielu niezależnych triggerów.

### 4.2. Targeting

Dodać małe typy danych:

```text
EffectTargetKind
EffectTargetDefinition
EffectTargetResolutionMode
```

Model ma być prosty dla Unity Inspectora. Nie używać generycznego drzewa
warunków ani skryptowego języka zapytań. Pola nieużywane przez dany
`EffectTargetKind` powinny być ignorowane i walidowane przez `OnValidate` oraz
testy assetów.

### 4.3. Bezpośredni modyfikator bazowego Attack

Dodać `CombatEffectKind.ModifyBaseAttackPercent`. Nie tworzy on statusu i nie
zmienia semantyki istniejącego `Empower`.

Zmiany runtime:

- `UnitRuntimeState.BaseAttackBonusPercent`, istniejące wyłącznie w czasie
  pojedynczej symulacji;
- obsługa `ModifyBaseAttackPercent` w `CombatEffectResolver`;
- `EffectiveStatsResolver.GetBaseAttackMultiplier` z limitem tuningu;
- użycie mnożnika wyłącznie na `CombatSpec.Attack` w `DamageCalculator`.

Takie rozdzielenie zachowuje obecną semantykę `Empower`/`Weaken` jako
modyfikatorów całego outgoing damage i nie zmienia zachowania istniejących
statusów. Modyfikator jest resetowany wraz z `UnitRuntimeState` po walce.

### 4.4. Czas trwania statusu (tylko dla efektu `Status`)

Obecne `StatusDefinition.DefaultDuration` wymusza czas w sekundach. Nie należy
symulować statusu „do końca walki” przez arbitralnie dużą wartość.

`StatusApplicationDefinition` i `StatusApplicationRequest` mają jawny tryb:

```text
StatusLifetimeMode.UseDefinitionDuration
StatusLifetimeMode.OverrideSeconds
StatusLifetimeMode.UntilCombatEnds
```

Dla `UntilCombatEnds` runtime może używać `double.PositiveInfinity` jako
`EndTime`, ale tryb musi pozostać widoczny w danych i debugowaniu. Statusy nie
są kopiowane przez `BattleSimulationResultApplier`, więc znikają razem z
symulacją. Pierwszy efekt 25% nie używa tego mechanizmu: jego lifetime jest
automatycznie ograniczony do życia symulacji.

## 5. Stan oczekujący pomiędzy preparation i combat

### 5.1. `PendingCombatEffect`

Dodać niemutowalny typ wartości zawierający co najmniej:

```text
ApplicationSequenceId
ScheduledRoundNumber
SourceRuntimeUnitId
TargetRuntimeUnitId
CombatEffectSpec
```

`CombatEffectSpec` jest runtime'ową kopią danych potrzebnych do wykonania
efektu. Przechowuje typ i parametry bezpośredniego efektu (np. `Percent`) albo
value-type `StatusCombatSpec` z override'ami dla jawnego efektu `Status`; nie
polega na ponownym odczycie mutowalnego assetu podczas startu walki.

Kolejność wykonania:

1. kolejność zagrań jednostek;
2. kolejność `Steps` w assetcie;
3. rosnący `TargetRuntimeUnitId`.

### 5.2. Kolekcja w `BattleState`

`BattleState` otrzymuje prealokowaną `PendingCombatEffectQueue`. Kolekcja ma
jawny limit pojemności z `BattleConfig` i nie może zwiększać tablicy w trakcie
gry.

Przed pobraniem AP i przeniesieniem karty do `Played` resolver wykonuje
preflight:

- waliduje definicję;
- liczy odbiorców bez tworzenia tymczasowej listy;
- sprawdza, czy queue pomieści cały batch.

Batch jest atomowy: karta nie może zostać zagrana z pominiętym fragmentem
umiejętności. Błąd pojemności lub nieobsługiwany obowiązkowy krok zwraca nowy,
jednoznaczny `PlayUnitFailReason`.

Rekordy nie są usuwane przez `BattleSimulationFactory`, ponieważ wielokrotne
utworzenie symulacji z tego samego `BattleState` musi dawać identyczny wynik.
Efekty dla bieżącego `RoundNumber` są usuwane dopiero przez
`BattleSimulationResultApplier` po zakończeniu walki. `StartNextRound` powinien
dodatkowo defensywnie odrzucić przeterminowane rekordy.

## 6. Resolver `OnPlay`

Dodać `UnitOnPlayEffectResolver` jako czystą logikę domenową poza UI.

Odpowiedzialności:

1. odczyt `UnitDefinition.OnPlayEffect`;
2. walidacja obsługiwanych kroków;
3. deterministyczne rozstrzygnięcie targetów na stanie preparation;
4. snapshot definicji do `CombatEffectSpec`;
5. atomowe dodanie batcha do `PendingCombatEffectQueue`;
6. zwrot lekkiego wyniku potrzebnego do prezentacji triggera.

Integracja z `UnitPlayService.PlayUnit`:

1. wykonać obecną walidację zagrania;
2. wykonać preflight efektu dla planowanego coord;
3. pobrać AP i przenieść kartę;
4. utworzyć `RuntimeUnit`;
5. dodać jednostkę do gracza;
6. uruchomić resolver dokładnie raz;
7. zwrócić `PlayUnitResult` z jednostką i podsumowaniem efektu.

Brak `OnPlayEffect` jest legalny. Dzięki temu system nie wymusza migracji
testowych lub przyszłych neutralnych jednostek.

`EnemyPreparationAI` nadal korzysta z `UnitPlayService`, więc efekt odpali się
również dla przeciwnika bez duplikacji logiki. AI nie potrzebuje zmian dla
automatycznego targetowania.

## 7. Materializacja na początku walki

### 7.1. Dwufazowe tworzenie symulacji

Rozszerzyć `BattleSimulationFactory` oraz `BattleSimulation.Create`:

1. utworzyć wszystkie `UnitRuntimeState` i słowniki ID/hex;
2. utworzyć instancję `BattleSimulation` z `ElapsedTime == 0`;
3. pobrać oczekujące efekty bieżącej rundy w stabilnej kolejności;
4. znaleźć source i target po runtime ID;
5. wykonać efekt przez wspólny `CombatEffectResolver`;
6. dopiero po początkowych efektach wyliczyć `NextAttackTime` każdej jednostki.

Ostatni punkt jest ważny dla przyszłych efektów `Haste`/`Slow`: status obecny
od początku walki musi wpłynąć również na pierwszy cooldown.

### 7.2. `CombatEffectResolver`

W pierwszym pionowym przekroju resolver obsługuje `ModifyBaseAttackPercent`,
który zapisuje wartość bezpośrednio w runtime'owym stanie targetu. `Status` jest
obsługiwany jako świadomy, opcjonalny typ efektu i używa wyłącznie
`StatusResolver.TryApply`; nie wolno dopisywać `StatusInstance` bezpośrednio.

Kolejne istniejące wartości `CombatEffectKind` (`Damage`, `Heal`, `Drain`,
`ResetWinddown`) należy podłączać małymi etapami do `DamageResolver`,
`HealingResolver`, `StatusResolver` i `AttackCycleResolver`, kiedy pierwszy
konkretny content będzie ich wymagał. Nie należy oznaczać ich jako obsługiwane,
jeżeli nie mają testów i pełnej ścieżki eventów.

Nieprawidłowy source/target albo przekroczenie pojemności statusów daje
deterministyczny wynik i diagnostykę Editor/Development Build. Produkcyjny
build nie może rzucać wyjątku ani wykonywać częściowo zdefiniowanego kroku.

## 8. Prezentacja

### 8.1. Moment zagrania

`PlayUnitResult` powinien zwrócić małe podsumowanie triggera, np. effect ID i
liczbę/ID odbiorców. `BattleController` po utworzeniu `UnitView` może uruchomić
jednorazową, lekką prezentację `OnPlay`.

Prezentacja nie kolejkuje efektu i nie modyfikuje statystyk. Brak VFX nie może
wpływać na gameplay. Jeśli dodany zostanie prefab efektu, użyć puli tak jak w
`BattleEffectPresenter`/`UnitStatusVfxController`.

### 8.2. Początkowe statusy combat

Rozszerzyć `BattlePresentationSnapshot`, aby kopiował aktywne statusy istniejące
w czasie `0`:

- status kind;
- source unit ID;
- stacks;
- total shield, jeśli dotyczy;
- slice/index statusów przypisany do jednostki bez listy alokowanej per unit.

`BattleView.BindInitialState` musi zainicjalizować overlay oraz status VFX z
tego snapshotu. Nie generować sztucznych `StatusApplied` w pierwszym ticku,
ponieważ gameplayowy status istnieje już przed tickiem i event sugerowałby
nieprawidłowy moment aplikacji.

### 8.3. Opis karty

`CardDetailsPopupView` powinien pokazywać `DisplayName` i `Description` efektu
`OnPlay` dla jednostki. Tekst pochodzi z assetu efektu; UI nie może budować
opisu przez switch po `EffectId`.

Pierwszy opis contentowy:

```text
On Play: Ta jednostka otrzymuje +25% bazowego Attack podczas najbliższej walki.
```

## 9. Pierwszy asset i migracja contentu

Utworzyć:

```text
Assets/DeckBattle/Data/UnitEffects/OnPlay_BaseAttack25_NextCombat.asset
```

Konfiguracja efektu:

```text
Target: Self
ResolutionMode: ResolveOnPlay
EffectKind: ModifyBaseAttackPercent
Percent: 0.25
```

Efekt materializuje się w `UnitRuntimeState` najbliższej symulacji i dlatego
nie potrzebuje definicji statusu ani osobnego pola lifetime.

Ten sam asset efektu podłączyć do wszystkich dziewięciu produkcyjnych
`UnitDefinition`:

- `Archer`;
- `Brute`;
- `Crossbowman`;
- `Guard`;
- `Lancer`;
- `Scout`;
- `Sniper`;
- `Swordsman`;
- `Tankbuster`.

Współdzielenie assetu jest zamierzone dla pierwszego contentu. Architektura
pozwala później podmienić referencję pojedynczej jednostki na jej unikalny
asset bez zmian w kodzie.

## 10. Testy

### 10.1. `UnitOnPlayEffectResolverTests`

Dodać testy:

- brak definicji nie tworzy pending effect;
- `Self` wybiera wyłącznie zagraną jednostkę;
- friendly/enemy targetowanie respektuje stronę źródła;
- radius używa `HexBoard.Distance`;
- targety są uporządkowane po runtime ID;
- późniejsze przesunięcie jednostki nie zmienia zapisanych targetów;
- jednostka zagrana później nie zostaje dodana do wcześniejszego snapshotu;
- wielostopniowy efekt zachowuje kolejność stepów;
- brak miejsca w queue odrzuca cały batch;
- nieobsługiwany krok nie daje częściowego wyniku.

### 10.2. `DeckHandUnitPlayTests`

Rozszerzyć testy:

- poprawne zagranie tworzy dokładnie jeden batch `OnPlay`;
- AP i karta zmieniają stan tylko po udanym preflight;
- odrzucone zagranie nie uruchamia efektu;
- ponowne wywołanie dla zagranej karty nie tworzy drugiej aplikacji;
- efekt działa identycznie dla gracza i `EnemyPreparationAI`.

### 10.3. `BattleSimulationFactoryTests`

Dodać testy:

- pending efekt nie istnieje na `RuntimeUnit` jako aktywny modyfikator preparation;
- `ModifyBaseAttackPercent` materializuje się przy `ElapsedTime == 0` najbliższej walki;
- source/target są mapowane po ID, niezależnie od kolejności stron;
- utworzenie dwóch symulacji z tego samego `BattleState` daje ten sam stan;
- początkowy `Haste` wpływa na pierwszy cooldown — regresja architektury;
- brak targetu jest obsługiwany deterministycznie;
- efekt `Status` odrzuca przekroczenie `MaxStatusesPerUnit` bez nowej kolekcji.

### 10.4. `DamageCalculatorTests`

Dodać testy:

- `ModifyBaseAttackPercent 0.25` zwiększa każdy atak o 25% bazowej wartości;
- `Attack = 110` daje 138 przed obroną i bez crita;
- `AttackBonusNextCombat` jest dodawany po bonusie procentowym i nadal zużywa
  się tylko przy pierwszym ataku;
- armor i crit zachowują dotychczasową kolejność;
- limit mnożnika jest respektowany;
- brak modyfikatora zachowuje dotychczasowe wyniki bit-for-bit.

### 10.5. Cykl życia

Dodać test integracyjny obejmujący dwie walki:

1. zagrać jednostkę;
2. utworzyć pierwszą symulację i potwierdzić bonus;
3. zakończyć walkę oraz wykonać `BattleSimulationResultApplier`;
4. rozpocząć kolejną rundę;
5. utworzyć drugą symulację;
6. potwierdzić brak bonusu i brak ponownej aktywacji `OnPlay`.

Przetestować również ścieżkę `MaxTicksReached`, ponieważ ona także konsumuje
pending effects przez result applier.

### 10.6. Prezentacja i assety

Dodać testy:

- początkowy status trafia do `BattlePresentationSnapshot`;
- `BattleView.BindInitialState` pokazuje status bez oczekiwania na pierwszy
  tick;
- popup jednostki pokazuje opis `OnPlay`;
- wszystkie dziewięć produkcyjnych assetów jednostek ma przypisany efekt;
- wspólny asset ma target `Self` i `Percent` równe `0.25`.

Po zmianach uruchomić przez Unity MCP najpierw wąski zestaw Edit Mode:

```text
UnitOnPlayEffectResolverTests
DeckHandUnitPlayTests
BattleSimulationFactoryTests
DamageCalculatorTests
StatusResolverTests
BattlePresentationSnapshot/BattleView tests
EnemyPreparationAITests
```

Następnie uruchomić pełny zestaw Edit Mode w otwartym Unity Editorze. Nie
uruchamiać Edit Mode tests w batchmode.

Manualny Play Mode smoke test:

1. zagrać jednostkę gracza i zobaczyć jednorazową prezentację triggera;
2. potwierdzić modyfikator runtime na początku walki;
3. porównać damage z bazowym Attack;
4. zakończyć walkę i potwierdzić brak modyfikatora w kolejnej rundzie;
5. sprawdzić tę samą ścieżkę dla jednostki AI;
6. sprawdzić pauzę/background/resume przed i podczas walki.

## 11. Wydajność mobilna

- Brak `Update`, coroutine i callbacków per efekt gameplayowy.
- Brak LINQ, refleksji, `SerializeReference` i runtime delegate w resolverach.
- Targetowanie wykonuje proste pętle po maksymalnie kilku jednostkach w fazie
  preparation, nie w hot path ticka.
- `PendingCombatEffectQueue` oraz workspaces mają stałą/prealokowaną pojemność.
- `UnitStatusCollection` pozostaje jedyną kolekcją aktywnych statusów jednostki.
- Snapshot statusów używa jednej płaskiej listy/bufora, nie listy per jednostka.
- Po rozgrzaniu `BattleTickLoop.Tick` nadal powinien mieć `0 B GC.Alloc`.
- Zmiana nie dodaje paczek, shaderów, tekstur ani stałych efektów cząsteczkowych.

Punkty do sprawdzenia w Profilerze:

- `DeckBattle.Status.Apply` przy starcie walki;
- koszt utworzenia symulacji przy maksymalnej liczbie jednostek i pending
  effects;
- brak alokacji podczas kolejnych ticków;
- brak dodatkowych layout rebuildów UI po inicjalnym bindzie statusu.

## 12. Kolejność implementacji

### Etap 1 — kontrakt danych

- dodać definicję `OnPlay`, step i targeting;
- dodać pole w `UnitDefinition`;
- dodać `CombatEffectKind.ModifyBaseAttackPercent` i jego parametr `Percent`;
- zachować lifetime `UntilCombatEnds` dla przyszłych efektów `Status`;
- dodać walidację danych.

### Etap 2 — preparation i pending queue

- dodać `PendingCombatEffect` oraz bounded queue;
- dodać `UnitOnPlayEffectResolver` i automatyczne targety;
- podłączyć preflight i atomowe wykonanie do `UnitPlayService`;
- rozszerzyć `PlayUnitResult` dla prezentacji;
- pokryć resolver i zagranie testami.

### Etap 3 — start symulacji

- przekazać pending specs przez `BattleSimulationFactory`;
- przebudować tworzenie symulacji na dwa przebiegi;
- dodać `CombatEffectResolver` dla bezpośredniego modyfikatora i opcjonalnego statusu;
- wyliczać pierwszy cooldown po efektach początkowych;
- konsumować rekordy w `BattleSimulationResultApplier`;
- dodać test dwóch kolejnych walk.

### Etap 4 — dokładny bonus Attack

- rozszerzyć `EffectiveStatsResolver`;
- zmienić formułę `DamageCalculator` bez wpływu na legacy
  `AttackBonusNextCombat`;
- dodać testy zaokrąglania, armora, crita i braku regresji.

### Etap 5 — prezentacja

- rozszerzyć początkowy snapshot statusów;
- zainicjalizować overlay/VFX przed pierwszym tickiem;
- dodać jednorazową prezentację triggera w preparation;
- pokazać opis efektu w popupie karty.

### Etap 6 — content i weryfikacja

- utworzyć wspólny asset bezpośredniego efektu;
- przypisać efekt do dziewięciu jednostek;
- uruchomić wąskie i pełne testy przez Unity MCP;
- wykonać Play Mode smoke test;
- sprawdzić GC i czas startu symulacji w Profilerze.

## 13. Ryzyka i zabezpieczenia

- **Początkowy efekt `Status` niewidoczny w UI** — rozszerzyć snapshot, zamiast
  emitować opóźniony, fałszywy event w pierwszym ticku. Nie dotyczy
  `ModifyBaseAttackPercent`, który nie ma prezentacji statusu.
- **Efekt przechodzi do kolejnej walki** — tagować rekord `RoundNumber` i
  konsumować go wyłącznie w result applierze bieżącej walki.
- **Częściowe wykonanie wielostopniowego efektu** — preflight całego batcha i
  atomowy enqueue.
- **Zmiana celu po przestawieniu formacji** — zapisywać target runtime IDs przy
  `OnPlay`, nie zapytanie do późniejszego wykonania.
- **Bonus obejmuje legacy first-attack bonus** — bezpośredni modyfikator bazowego
  Attack i jawna kolejność wzoru.
- **Przypadkowa zmiana wszystkich jednostek** — wspólny asset jest celowy, ale
  test contentu powinien wykrywać zmianę wartości 25%/lifetime.
- **Przepełnienie pending queue** — stały limit, jawny wynik błędu i test
  maksymalnego contentu. Limit statusów dotyczy wyłącznie efektu `Status`.
- **Rozrost systemu triggerów** — w tym wdrożeniu tylko `OnPlay`; nowe triggery
  dostają osobne plany i punkty integracji, ale mogą ponownie użyć stepów,
  targetowania oraz `CombatEffectResolver`.

## 14. Definition of Done

- poprawne zagranie jednostki uruchamia jej `OnPlay` dokładnie raz;
- nieudane zagranie nie uruchamia efektu i nie zmienia kolejki;
- targety są rozstrzygane deterministycznie w chwili zagrania;
- modyfikator bojowy nie istnieje aktywnie w preparation;
- modyfikator pojawia się w czasie `0` najbliższej symulacji przed wyliczeniem
  pierwszego cooldownu;
- każda z dziewięciu produkcyjnych jednostek ma +25% bazowego Attack przez
  całą najbliższą walkę;
- bonus działa na każdy atak tej walki, ale nie mnoży
  `AttackBonusNextCombat`;
- końcowe zaokrąglenie jest deterministyczne i zgodne z
  `MidpointRounding.AwayFromZero`;
- efekt nie pojawia się ponownie w kolejnej rundzie;
- pierwszy efekt nie tworzy statusu, ikony ani VFX;
- gracz i AI korzystają z tej samej ścieżki;
- logika nie zależy od widoku, VFX ani Animatora;
- brak nowych alokacji w `BattleTickLoop.Tick` po rozgrzaniu;
- wąskie i pełne testy Edit Mode przechodzą;
- Play Mode smoke test potwierdza poprawny cykl preparation → combat → next
  round.
