# Plan wprowadzenia systemu statusów

## 1. Cel

Celem jest dodanie deterministycznego, data-driven systemu statusów działającego
w realtime'owej symulacji walki. System ma obsłużyć:

- Crowd Control: `Stun`, `Slow`, `Sleep`, `Root`, `Silence`;
- Damage over Time: `Burn`, `Poison`, `Bleed`;
- osłabienia statystyk: `Weaken`, `Exposed`, `Shred`, `Drain`;
- buffy: `Shield`, `Regen`, `Invulnerability`, `Empower`, `Haste`,
  `Criticality`, `Fearless`, `Lifesteal`;
- statusy taktyczne: `Mark`, `Taunt`, `Untargetable`, `Guard`.

Symulacja pozostaje jedynym źródłem prawdy. Widok wyłącznie reaguje na
`BattleEvent` i nie może aplikować, odświeżać, usuwać ani wykonywać ticków
statusów.

Plan nie zakłada tworzenia od razu wszystkich kart i jednostek korzystających
ze statusów. Najpierw powstaje wspólny runtime, API aplikowania efektów, testy
i jeden pionowy przekrój funkcji. Podłączanie contentu jest osobnym etapem.

## 2. Stan obecny i punkty integracji

Obecna architektura jest dobrym fundamentem:

- `BattleTickLoop` wykonuje walkę stałym tickiem i ustala kolejność resolverów;
- `UnitRuntimeState` przechowuje stan jednostki tylko na czas auto-battle;
- `AttackCycleResolver` kontroluje windup, fire i winddown;
- `MovementResolver` kontroluje planowanie i wykonanie kroku między hexami;
- `TargetSelector` oraz `TargetingRules` centralizują wybór celu;
- `DamageCalculator` i `HitResolver` są wspólnymi punktami obrażeń;
- `CombatResolver` obsługuje manę i aktywację obecnego speciala;
- `BattleEventQueue` oddziela symulację od `BattleView`;
- `UnitStatusOverlayController` już używa puli elementów UI.

System statusów powinien rozszerzyć te punkty, a nie tworzyć równoległej
symulacji lub osobnych coroutine dla każdego efektu.

## 3. Ustalenia semantyczne wspólne dla wszystkich statusów

### 3.1. Czas

- Czas trwania i interwały są podawane w sekundach czasu symulacji.
- Runtime używa absolutnych deadline'ów `double`, tak jak obecny cykl ataku
  i pociski.
- Status zaaplikowany w czasie `T` z duration `D` wygasa w `T + D`.
- Efekt periodyczny z interval `I` wykonuje pierwszy tick w `T + I`.
- Tick zaplanowany dokładnie na czas wygaśnięcia wykonuje się przed usunięciem
  statusu. Przykład: duration 3 s i interval 1 s daje ticki w 1, 2 i 3 s.
- Statusy nie wykonują catch-up na podstawie czasu renderowanej klatki.
  Wszystko wynika wyłącznie z kolejnych stałych ticków symulacji.

### 3.2. Czas życia między rundami

Statusy realtime są stanem jednej fazy auto-battle:

- są tworzone w `BattleSimulationFactory`;
- nie są kopiowane z powrotem przez `BattleSimulationResultApplier`;
- są usuwane wraz z końcem walki;
- ewentualny spell z fazy przygotowania zapisuje mały
  `PendingCombatEffect` na `RuntimeUnit`, który fabryka zamienia na status na
  starcie najbliższej walki;
- `PendingCombatEffect` jest konsumowany przy tworzeniu symulacji i nie
  przechodzi automatycznie do kolejnej rundy.

Jeżeli później potrzebne będą efekty trwające przez wiele rund, powinny być
osobnym typem stanu meczu, a nie specjalnym przypadkiem realtime'owego
`StatusInstance`.

### 3.3. Identyfikacja źródła

Każda instancja przechowuje:

- `StatusKind`;
- `SourceUnitId`, albo `0`, gdy źródłem jest spell bez jednostki;
- `ApplicationSequenceId` do stabilnego porządku;
- potency/magnitude;
- liczbę stacków;
- `EndTime`;
- `NextTickTime`, jeśli efekt jest periodyczny;
- opcjonalny `LinkedUnitId` dla `Taunt` i `Guard`;
- pozostałą wartość absorpcji dla `Shield`.

Źródło jest potrzebne do:

- reguł ponownej aplikacji;
- deterministycznego wyboru przy wielu `Taunt` lub `Guard`;
- atrybucji obrażeń, leczenia i lifestealu;
- debugowania i prezentacji.

### 3.4. Efekty korzystne i szkodliwe

Każdy `StatusDefinition` posiada klasyfikację:

- `Beneficial`;
- `HarmfulCrowdControl`;
- `HarmfulDamageOverTime`;
- `HarmfulStatReduction`;
- `HarmfulTactical`.

`Fearless` odrzuca wyłącznie `HarmfulCrowdControl`.
`Invulnerability` odrzuca wszystkie szkodliwe aplikacje oraz obrażenia, ale
nie blokuje buffów nakładanych przez sojusznika.

Odrzucona aplikacja emituje zdarzenie z przyczyną, ale nie tworzy runtime'owej
instancji.

### 3.5. Snapshot aktywnej akcji

- Modyfikatory obrażeń atakującego i critical multiplier są snapshotowane
  przy `AttackFired`.
- Armor, `Shred`, `Exposed`, `Invulnerability`, `Guard` i `Shield` celu są
  sprawdzane w resolution point, czyli przy trafieniu melee albo impact
  pocisku.
- Zmiana `Slow`/`Haste` nie przelicza trwającego już cyklu ataku. Wpływa na
  cykl rozpoczęty po zmianie statusu.
- Zmiana `Slow` nie przelicza kroku ruchu już rozpoczętego. Wpływa na następny
  krok między hexami.
- `Stun` i `Sleep` natychmiast anulują windup. `Stun`, `Sleep` i `Root`
  zatrzymują dalszą ścieżkę ruchu po dokończeniu aktualnego kroku.

Takie snapshotowanie jest proste do debugowania, nie wymaga ciągłego
przeliczania deadline'ów i zachowuje stabilne frame time.

## 4. Reguły poszczególnych statusów

### 4.1. Crowd Control

| Status | Reguła runtime | Ponowna aplikacja |
| --- | --- | --- |
| `Stun` | Blokuje targetowanie, rozpoczęcie kolejnego kroku ruchu, rozpoczęcie ataku oraz special. Natychmiast anuluje windup. Rozpoczęty krok ruchu dochodzi do zarezerwowanego destination, po czym jednostka się zatrzymuje. | Ten sam source odświeża do późniejszego `EndTime`; różne źródła mogą współistnieć. |
| `Slow` | Zwiększa effective attack cooldown i czas kroku między hexami. Wartość dodatnia oznacza procent spowolnienia, np. 30%. | Ten sam source odświeża i zastępuje potency silniejszą wartością. Wiele źródeł sumuje się do limitu. |
| `Sleep` | Jak `Stun`, ale zostaje usunięty po pierwszej dodatniej utracie HP. Absorpcja całego trafienia przez `Shield` nie budzi. | Jak `Stun`. |
| `Root` | Blokuje rozpoczęcie kolejnego kroku ruchu. Rozpoczęty krok dochodzi do zarezerwowanego destination, po czym jednostka się zatrzymuje. Nie blokuje targetowania, ataku ani speciala. | Jak `Stun`. |
| `Silence` | Blokuje aktywację speciala. Mana pozostaje maksymalna i special odpala w pierwszym legalnym ticku po końcu silence. | Jak `Stun`. |

Przerwanie ruchu oznacza zatrzymanie po aktualnym kroku, a nie cofnięcie:

- aktywna rezerwacja `MovementDestination` pozostaje ważna;
- timer rozpoczętego kroku nadal biegnie;
- jednostka logicznie kończy krok na `MovementDestination`;
- widok kończy rozpoczętą animację na tym samym hexie, bez snapowania;
- status blokuje zebranie kolejnego movement intent;
- jeżeli status wygaśnie przed końcem kroku, nie zmienia to destination ani
  czasu już rozpoczętej animacji.

Przerwanie windupu korzysta z jednej publicznej operacji
`AttackCycleResolver.CancelWindup`. Nie zużywa ataku, many, bonusu jednorazowego
ani nie dodaje osobnej kary cooldownu. Już zaplanowany deadline cyklu nadal
biegnie, więc po zdjęciu CC jednostka może zaatakować od razu, jeżeli termin
gotowości już minął.

Jeżeli hard CC zostanie nałożone przez wcześniejszy atak w tej samej,
wcześniej zebranej partii równoczesnych fire intents, nie cofa fire już
przyjętego do partii. Status blokuje kolejne akcje. Pocisk rozstrzygnięty przed
fazą ataków może przerwać windup w tym samym ticku.

### 4.2. Damage over Time

| Status | Reguła runtime | Stackowanie |
| --- | --- | --- |
| `Burn` | Zadaje określoną liczbę obrażeń co interval. | Jedna instancja na source; ponowna aplikacja odświeża czas i zachowuje większą potency. |
| `Poison` | Zadaje obrażenia co interval i redukuje każde leczenie celu o podany procent. | Jedna instancja na source; ponowna aplikacja odświeża czas i zachowuje większe damage/heal reduction. Redukcje wielu źródeł sumują się do 100%. |
| `Bleed` | Zadaje obrażenia co interval pomnożone przez liczbę stacków. | Jedna zbiorcza instancja na celu; aplikacja dodaje stacki do `MaxStacks` i odświeża czas. |

DoT:

- nie jest trafieniem i nie uruchamia `Mark`;
- domyślnie nie korzysta z crita ani lifestealu;
- podlega `Invulnerability`, `Exposed` i `Shield`;
- domyślnie omija armor, ale jest oznaczony osobnym `DamageKind`, aby można
  było to później zmienić per definicja;
- może obudzić `Sleep` tylko wtedy, gdy po shieldzie rzeczywiście odejmie HP;
- może zabić jednostkę i korzysta z tej samej ścieżki śmierci co atak.

Ticki są wykonywane w stabilnej kolejności:

1. kolejność jednostek w `BattleSimulation.Units`;
2. `ApplicationSequenceId` statusu;
3. po każdym ticku natychmiastowe rozstrzygnięcie śmierci.

### 4.3. Stat Reduction

| Status | Reguła runtime |
| --- | --- |
| `Weaken` | Zmniejsza outgoing damage jednostki o procent. |
| `Exposed` | Zwiększa damage przyjmowany przez jednostkę o procent. |
| `Shred` | Odejmuje punkty procentowe od armora celu, minimum 0. |
| `Drain` | Jest efektem natychmiastowym: odejmuje określoną liczbę many, minimum 0. Nie tworzy aktywnej instancji statusu. |

`Weaken` i `Empower` należą do jednego bucketu:

```text
OutgoingDamageMultiplier =
    clamp(1 + Sum(Empower) - Sum(Weaken), MinDamageMultiplier, MaxDamageMultiplier)
```

`Shred` jest liczony przed armor penetration atakującego:

```text
EffectiveArmor = max(0, BaseArmor - Sum(Shred))
AfterPenetration = EffectiveArmor * (1 - ArmorPenetration)
```

`Drain` emituje standardowe `UnitManaChanged` oraz osobny event efektu.
Invulnerability odrzuca szkodliwy drain.

### 4.4. Buffy

| Status | Reguła runtime |
| --- | --- |
| `Shield` | Posiada pulę absorpcji. Obrażenia zużywają shield przed HP. Wiele shieldów jest konsumowanych od najwcześniej wygasającego, potem po `ApplicationSequenceId`. |
| `Regen` | Leczy określoną liczbę HP co interval, maksymalnie do `MaxHp`. Podlega redukcji leczenia z `Poison`. |
| `Invulnerability` | Blokuje obrażenia i nowe szkodliwe statusy. Nie zatrzymuje czasu już istniejących statusów. Przy aplikacji usuwa aktywne szkodliwe statusy. |
| `Empower` | Zwiększa outgoing damage o procent. |
| `Haste` | Zmniejsza attack cooldown o procent. |
| `Criticality` | Dodaje wartość do critical damage multiplier; nie zwiększa crit chance. |
| `Fearless` | Odrzuca nowe statusy z kategorii Crowd Control. Przy aplikacji usuwa aktywne CC. |
| `Lifesteal` | Leczy o procent faktycznie odjętego HP przez bezpośredni atak lub special damage. |

Reguły dodatkowe:

- `Shield` nie zwiększa `CurrentHp` i nie może przekroczyć własnej puli.
- Obrażenia całkowicie wchłonięte przez shield nadal są trafieniem, ale nie
  budzą `Sleep` i nie generują lifestealu.
- `Regen` oraz lifesteal korzystają ze wspólnego `HealingResolver`.
- Nadleczenie jest obcinane i nie jest liczone jako faktycznie wykonane
  leczenie.
- `Haste` i `Slow` korzystają ze wspólnego bucketu:

```text
AttackCooldownMultiplier =
    clamp(1 + Sum(Slow) - Sum(Haste), MinCooldownMultiplier, MaxCooldownMultiplier)
```

- Czas ruchu korzysta tylko ze `Slow`:

```text
MovementStepDuration =
    BaseMovementStepDuration * clamp(1 + Sum(Slow), 1, MaxMovementSlowMultiplier)
```

- Lifesteal sumuje się do limitu 100%, o ile tuning nie ustali innego capu.
- Leczenie z lifestealu nie może uruchamiać kolejnego lifestealu.

### 4.5. Tactical

| Status | Reguła runtime |
| --- | --- |
| `Mark` | Pierwsze bezpośrednie trafienie jednostki przez wrogą jednostkę dodaje określone obrażenia, po czym `Mark` zostaje usunięty. Bonus nie uruchamia ponownie `Mark`. |
| `Taunt` | Status znajduje się na atakującym i przechowuje `LinkedUnitId` prowokującego. Przy następnym wyborze celu wymusza prowokującego, jeżeli jest żywy, targetowalny i istnieje osiągalna pozycja ataku. |
| `Untargetable` | Jednostka nie może zostać wybrana jako nowy cel. Nie anuluje pocisku ani prawidłowego windupu zablokowanego przed aplikacją statusu. |
| `Guard` | Status znajduje się na chronionym sojuszniku i przechowuje `LinkedUnitId` guarda. Połowa bezpośrednich obrażeń skierowanych w chronionego jest przekierowana do żywego guarda. |

Reguły dodatkowe:

- `Mark` nie reaguje na DoT, własny bonus `Mark`, damage środowiskowy ani
  sam koszt HP.
- Pierwsze kwalifikujące się bezpośrednie trafienie konsumuje `Mark` w tym
  samym resolution point. Status zostaje usunięty również wtedy, gdy jego
  bonus zostanie później całkowicie zatrzymany przez `Invulnerability` albo
  `Shield`.
- Bonus `Mark` przechodzi przez zwykły incoming pipeline celu:
  `Guard -> Invulnerability -> Exposed -> Shield -> HP`, ale omija armor.
- Lifesteal liczy tylko główne obrażenia ataku, bez dodatkowego damage z
  `Mark`.
- `Taunt` nie przerywa committed windupu. Wpływa na kolejne target acquisition.
- Przy wielu poprawnych `Taunt` wybór jest deterministyczny: najkrótsza
  osiągalna ścieżka, potem najniższy `LinkedUnitId`, potem najstarsze
  `ApplicationSequenceId`.
- `Untargetable` oznacza brak nowego wyboru celu, a nie dodge i nie immunity.
- `Guard` dzieli bazowy pakiet damage przed obroną odbiorców. Dla liczb
  nieparzystych guard otrzymuje `damage / 2` zaokrąglone w dół, a chroniony
  cel pozostałą część. Przykład: 5 damage daje 2 dla guarda i 3 dla celu.
- Obie części osobno przechodzą przez armor, `Shred`, `Exposed`,
  `Invulnerability` i `Shield` odpowiedniego odbiorcy.
- `Guard` nie tworzy łańcuchów przekierowań. Część przekierowana ma flagę
  `Redirected` i nie może zostać ponownie podzielona przez kolejny `Guard`.
- Przy wielu guardach wygrywa najstarszy aktywny status od żywego,
  poprawnego sojusznika.
- Guard może stracić HP, zużyć własny shield, obudzić się ze `Sleep` i umrzeć,
  ale przekierowana część nie jest drugim trafieniem: nie uruchamia `Mark`
  guarda ani dodatkowych on-hit statusów.
- Lifesteal głównego ataku korzysta z sumy HP faktycznie odjętego chronionemu
  celowi i guardowi, nadal z pominięciem bonusu `Mark`.
- Śmierć, zakończenie statusu lub brak guarda powoduje normalne trafienie
  pierwotnego celu.

## 5. Model danych

### 5.1. Definicje

Dodać:

```text
Scripts/Data/StatusKind.cs
Scripts/Data/StatusCategory.cs
Scripts/Data/StatusDefinition.cs
Scripts/Data/StatusApplicationDefinition.cs
Scripts/Data/DamageKind.cs
Scripts/Data/CombatEffectDefinition.cs
```

`StatusDefinition : ScriptableObject` przechowuje wyłącznie immutable dane:

- identyfikator i `StatusKind`;
- kategorię beneficial/harmful;
- domyślne duration, interval, magnitude i procent;
- regułę stackowania i `MaxStacks`;
- capy;
- dane prezentacyjne: krótka nazwa, opis, ikona i kolory;
- flagi opisujące obsługiwane parametry.

Nie tworzyć osobnej klasy C# dla każdego statusu. Zachowania wymagające logiki
pozostają w małych, jawnych resolverach według kategorii.

`StatusApplicationDefinition` opisuje pojedynczą aplikację w assetach
speciali/spelli/on-hit:

- referencję do `StatusDefinition`;
- opcjonalne nadpisanie duration, potency, interval i stacków;
- docelowy odbiorca: self, selected target, hit target, allies/enemies in
  przyszłości.

`CombatEffectDefinition` jest lekkim discriminated-data typem dla:

- aplikacji statusu;
- natychmiastowego damage;
- heal;
- drain;
- resetu attack winddown, jeżeli pozostaje potrzebny.

Na tym etapie nie dodawać refleksji, `SerializeReference`, runtime delegate ani
zewnętrznego frameworka efektów.

### 5.2. Runtime

Dodać:

```text
Scripts/Battle/StatusInstance.cs
Scripts/Battle/UnitStatusCollection.cs
Scripts/Battle/UnitStatusSnapshot.cs
Scripts/Battle/StatusApplicationRequest.cs
Scripts/Battle/StatusApplicationResult.cs
```

`UnitRuntimeState` otrzymuje:

- `UnitStatusCollection Statuses`;
- cache `UnitStatusSnapshot`, przeliczany tylko po add/remove/stack change;
- licznik wersji statusów dla debugowania i UI.

`UnitStatusCollection` używa prealokowanej tablicy lub listy o ustalonej
pojemności. Nie używa LINQ, słowników tworzonych per jednostka ani osobnych
coroutine. Usuwanie wykonuje kompaktowanie w miejscu.

`UnitStatusSnapshot` przechowuje często odczytywane wartości:

- flagi blokad akcji;
- sumy `Slow`, `Haste`, `Weaken`, `Empower`, `Exposed`, `Shred`,
  `Criticality`, heal reduction i lifesteal;
- flagi `Invulnerable`, `Fearless`, `Untargetable`;
- sumę aktywnych shieldów do prezentacji.

Dzięki snapshotowi hot path ataku, ruchu i targetowania nie skanuje pełnej
listy statusów wielokrotnie w tym samym ticku.

### 5.3. Limity

W `BattleRuntimeTuning` dodać jawne, walidowane limity:

- `MaxStatusesPerUnit`;
- `MaxBleedStacks`;
- min/max damage multiplier;
- min/max attack cooldown multiplier;
- max movement slow multiplier;
- max healing reduction;
- max lifesteal.

Przekroczenie pojemności nie może powodować alokacji ani cichego nadpisania.
API zwraca `CapacityReached`, emituje diagnostykę w Editor/Development Build
i odrzuca aplikację deterministycznie.

## 6. Resolver statusów i kolejność ticka

Dodać:

```text
Scripts/Battle/StatusResolver.cs
Scripts/Battle/PeriodicStatusResolver.cs
Scripts/Battle/UnitActionRules.cs
Scripts/Battle/EffectiveStatsResolver.cs
Scripts/Battle/HealingResolver.cs
Scripts/Battle/DamageResolver.cs
Scripts/Battle/SpecialResolver.cs
```

Docelowa kolejność w `BattleTickLoop`:

1. `AdvanceTime`.
2. Wygaśnięcie statusów, których deadline minął po wykonaniu należnego ticka
   na granicy.
3. Tick `Burn`, `Poison`, `Bleed` i `Regen`.
4. Rozstrzygnięcie śmierci spowodowanych przez DoT.
5. Aktualizacja/odpalenie speciali, które są gotowe i nie są wyciszone.
6. Dokończenie aktywnych ruchów, o ile nie są blokowane.
7. Resolution logicznych pocisków.
8. Odświeżenie targetów z `Taunt` i `Untargetable`.
9. Cykl ataków z blokadami CC.
10. Ponowne odświeżenie targetów.
11. Planowanie nowych ruchów z blokadami `Stun`, `Sleep` i `Root`.
12. Sprawdzenie końca walki.

`StatusResolver.TryApply` wykonuje atomowo:

1. walidację targetu i danych;
2. sprawdzenie `Invulnerability`/`Fearless`;
3. znalezienie reguły stacking/refresh;
4. anulowanie windupu przez `Stun`/`Sleep`; aktywny krok ruchu pozostaje
   committed do swojego destination;
5. modyfikację kolekcji;
6. przebudowę snapshotu;
7. eventy.

Nie wolno bezpośrednio dopisywać `StatusInstance` poza tym API.

## 7. Reguły akcji

`UnitActionRules` jest wspólnym miejscem zapytań:

```text
CanAcquireTarget
CanStartMovement
CanStartAttackWindup
CanResolveCommittedFire
CanActivateSpecial
CanReceiveHarmfulStatus
CanBeSelectedAsTarget
```

Integracje:

- `BattleTickLoop.RefreshTargets` pomija `Stun` i `Sleep`;
- `TargetingRules` odrzuca `Untargetable` przy nowej selekcji;
- `TargetSelector` najpierw próbuje wymuszonego celu `Taunt`, potem normalnej
  selekcji;
- `MovementResolver` nie zbiera nowego intent dla `Stun`, `Sleep` i `Root`,
  ale `AdvanceActiveMovements` zawsze pozwala dokończyć committed krok;
- `AttackCycleResolver` respektuje `Stun` i `Sleep`;
- `SpecialResolver` respektuje `Stun`, `Sleep`, `Silence`;
- `BattleSimulation.StartUnitMovement` i publiczne entry pointy zachowują
  defensive validation, aby test lub przyszły system nie ominął reguł.

Nie dodawać rozproszonych sprawdzeń `Statuses.Has(Stun)` bezpośrednio w wielu
resolverach.

## 8. Wspólny pipeline damage, hit i heal

Obecny `DamageCalculator` liczy także armor celu w momencie fire. To należy
rozdzielić, aby statusy celu były rozstrzygane przy faktycznym trafieniu.

### 8.1. Payload ataku

Przy fire utworzyć mały `AttackPayload`:

- source unit id;
- bazowe damage po `Weaken`/`Empower`;
- wynik crita i multiplier po `Criticality`;
- armor penetration;
- on-hit effect references/identifiers;
- flagi `DirectHit`, `CanLifesteal`, `CanTriggerMark`.

Pocisk przechowuje payload, a nie gotowe finalne obrażenia po armorze celu.

### 8.2. Resolution

`DamageResolver.Resolve` przyjmuje `DamageRequest` i wykonuje:

1. znalezienie poprawnego guarda i podział bazowego pakietu 50/50;
2. osobne rozstrzygnięcie części chronionego celu i części `Redirected`;
3. odrzucenie każdej części damage przez `Invulnerability` odbiorcy;
4. armor po `Shred` i armor penetration, jeśli dany `DamageKind` używa armora;
5. `Exposed`;
6. absorpcję shieldów;
7. odjęcie HP obu odbiorcom;
8. jednorazowy bonus `Mark` chronionego celu bez rekurencji oraz natychmiastowe
   usunięcie wykorzystanego statusu;
9. lifesteal z sumy HP faktycznie odjętego przez główny atak;
10. zdjęcie `Sleep` z każdego odbiorcy, który utracił HP;
11. on-hit statusy dla pierwotnego celu trafienia;
12. śmierć i cleanup statusów/relacji;
13. eventy, w tym `DamageRedirected`.

Melee i projectile używają dokładnie tej samej ścieżki.

### 8.3. Leczenie

`HealingResolver.Resolve` wykonuje:

1. walidację żywego celu;
2. sumowanie healing reduction z `Poison`;
3. clamp do `MaxHp`;
4. zwrot faktycznie uleczonej ilości;
5. `UnitHealed`.

`Regen` i `Lifesteal` nie modyfikują HP bezpośrednio.

## 9. Speciale, spelle i aplikowanie statusów

Obecny `UnitSpecialKind.AttackSpeed` pozostaje bez zmian w pierwszym wdrożeniu.
Wszystkie aktualne produkcyjne definicje jednostek korzystają z assetu
`Special_AtackSpeed`, a decyzja contentowa mówi, aby na razie żadna z nich nie
otrzymała statusu. Migracja tego speciala do `Haste` zostaje odłożona do
osobnego etapu aktywacji statusów w contentcie.

Zmiany:

- `UnitSpecialDefinition` otrzymuje tablicę `CombatEffectDefinition`;
- `SpecialResolver` pobiera gotową manę, sprawdza CC/Silence, wykonuje efekty
  w kolejności assetu i emituje event;
- nie zeruje many, jeżeli special nie może zostać wykonany;
- legacy `AttackSpeed` zachowuje obecną ścieżkę do czasu świadomej migracji
  contentu; nowe statusy nie zapisują jego pól runtime;
- `SpellDefinition` może otrzymać efekty przygotowujące
  `PendingCombatEffect`;
- przyszłe on-hit/on-attack efekty jednostek korzystają z tego samego opisu
  aplikacji, ale z jawnie określonym triggerem.

Nie rozszerzać `SpellEffectKind` o osobną wartość dla każdego z 24 statusów.
Prowadziłoby to do rosnącego switcha i duplikacji logiki.

## 10. Eventy, widok i debugowanie

Rozszerzyć `BattleEventType` co najmniej o:

```text
StatusApplied
StatusRefreshed
StatusStackChanged
StatusRemoved
StatusRejected
PeriodicEffectTicked
ShieldChanged
UnitHealed
DamageRedirected
ManaDrained
```

Event powinien przenosić małe typy wartości:

- unit/source/linked unit id;
- `StatusKind`;
- stack count;
- amount/remaining amount;
- duration;
- reason code.

Nie przenosić referencji do `ScriptableObject` w kolejce symulacji.

### 10.1. UI

Rozszerzyć istniejący overlay:

- osobny, poolowany pasek ikon statusów;
- maksymalnie 4 najważniejsze ikony oraz `+N` dla pozostałych na telefonie;
- priorytet: hard CC, invulnerability, tactical, DoT, pozostałe buff/debuff;
- stack count widoczny dla `Bleed` i opcjonalnie `Shield`;
- shield pokazany dodatkowym segmentem/liczbą przy HP;
- UI aktualizowane tylko po eventach add/refresh/stack/remove;
- brak tworzenia tekstu i ikon co klatkę;
- brak pełnego odliczania czasu tekstem per frame w pierwszej wersji.

`BattleDebugSnapshot` powinien pokazywać dla każdej jednostki:

- aktywne statusy;
- source/linked unit;
- stacki;
- remaining duration;
- effective outgoing/incoming/cooldown/move/armor/healing modifiers.

## 11. Stackowanie i odświeżanie

Wprowadzić jawny enum reguł:

```text
IndependentPerSource
RefreshPerSource
AggregateStacks
IndependentShield
InstantOnly
```

Zasady:

- ten sam source jest identyfikowany przez `SourceUnitId + StatusKind`;
- refresh ustawia `EndTime = max(CurrentEndTime, Now + NewDuration)`;
- potency przy refreshu przyjmuje większą wartość, chyba że definicja jawnie
  zezwala na zastąpienie słabszą;
- wiele źródeł stat modifierów sumuje się w bucketach i podlega capom;
- hard CC nie sumuje czasu addytywnie;
- `Bleed` ma jeden agregat stacków;
- każdy `Shield` ma oddzielną pulę;
- efekt z duration `0` jest odrzucany dla statusu czasowego, ale legalny dla
  `Drain`, instant damage i heal.

Każda reguła musi mieć osobny test dla tego samego i różnego source.

## 12. Etapy implementacji

### Etap 0: zatwierdzenie semantyki

- Przyjąć zatwierdzone decyzje z sekcji 15 jako kontrakt testów.
- Nie przypisywać statusów żadnej produkcyjnej jednostce, specialowi ani
  spellowi w pierwszym wdrożeniu.
- Ustalić bazowe capy i maksymalną liczbę statusów na jednostkę.

Warunek zakończenia: brak niejednoznaczności zmieniających damage pipeline,
targetowanie albo lifecycle rundy.

### Etap 1: fundament runtime

- Dodać definicje, request/result i `UnitStatusCollection`.
- Dodać snapshot oraz bezalokacyjne query.
- Dodać application/refresh/remove/expiry.
- Dodać podstawowe eventy i debug snapshot.
- Dodać reset/cleanup przy śmierci i końcu symulacji.

Pionowy przekrój: `Stun` działający od aplikacji, przez przerwanie windupu,
wygaśnięcie, eventy i test integracyjny.

### Etap 2: action gates i stat modifiers

- Dodać `UnitActionRules`.
- Podłączyć atak, ruch, special i targeting.
- Dodać `Slow`, `Root`, `Sleep`, `Silence`, `Fearless`, `Untargetable`.
- Dodać `EffectiveStatsResolver`.
- Dodać `Weaken`, `Exposed`, `Shred`, `Empower`, `Haste`, `Criticality`,
  `Lifesteal`.
- Przetestować `Haste` na definicji tworzonej wyłącznie w testach, bez
  migracji `Special_AtackSpeed`.

Warunek zakończenia: brak alternatywnej ścieżki, która może ominąć blokadę
akcji lub policzyć bazową statystykę bez statusów.

### Etap 3: damage, heal i efekty periodyczne

- Wprowadzić `AttackPayload`, `DamageRequest`, `DamageResolver`.
- Przełączyć melee i projectile na wspólny pipeline.
- Dodać `Shield`, `Invulnerability`, `Mark`.
- Dodać `HealingResolver`, `Regen`, healing reduction.
- Dodać `Burn`, `Poison`, `Bleed`.
- Dodać instant `Drain`.

Pionowy przekrój: projectile trafia oznaczony, osłabiony i osłonięty cel,
wykonuje prawidłowy damage, mark, shield, lifesteal oraz eventy bez
podwójnego triggera.

### Etap 4: statusy taktyczne

- Dodać priorytet `Taunt` do targetowania i pathfindingu.
- Dodać `Guard` do damage resolution.
- Obsłużyć utratę source/linked unit, death i expiry.
- Dodać testy wielu tauntów, wielu guardów i relacji z `Untargetable`.

Ten etap jest osobny, ponieważ dotyka najbardziej ryzykownych reguł
targetowania i przekierowania obrażeń.

### Etap 5: definicje i UI bez aktywacji produkcyjnego contentu

- Utworzyć `StatusDefinition` assets.
- Zweryfikować aplikowanie statusów przez definicje testowe.
- Nie dodawać referencji do statusów w produkcyjnych assetach jednostek,
  speciali ani spelli.
- Rozszerzyć poolowany overlay o ikony i shield.
- Dodać placeholderowe, lekkie VFX tylko dla kluczowych aplikacji.
- Zweryfikować czytelność na portretowych safe area i małych ekranach.

Późniejsze podłączenie konkretnych statusów do contentu będzie osobnym,
świadomie zleconym etapem. Wtedy należy również zdecydować, czy
`Special_AtackSpeed` migruje do `Haste`, i usunąć legacy pola dopiero po
pełnej migracji wszystkich korzystających z niego jednostek.

### Etap 6: balans, profilowanie i stabilizacja

- Ustalić wartości duration, interval, potency i capów.
- Przeprowadzić determinism replay z tym samym seedem.
- Sprawdzić brak GC alloc w ustabilizowanym ticku walki.
- Zmierzyć koszt statusów przy maksymalnej liczbie jednostek i statusów.
- Sprawdzić overdraw ikon/VFX i liczbę aktywnych elementów UI.
- Wykonać test na docelowym urządzeniu klasy mid-range Android.

## 13. Testy

### 13.1. Edit Mode, czysta logika

Dodać testy:

- `UnitStatusCollectionTests`;
- `StatusResolverTests`;
- `PeriodicStatusResolverTests`;
- `EffectiveStatsResolverTests`;
- `UnitActionRulesTests`;
- `DamageResolverTests`;
- `HealingResolverTests`;
- `TacticalStatusTests`;
- rozszerzenia `AttackCycleResolverTests`;
- rozszerzenia `MovementResolverTests`;
- rozszerzenia `TargetSelectorTests`;
- rozszerzenia `ProjectileResolverTests`;
- rozszerzenia `BattleTickLoopTests`;
- rozszerzenia `BattleSimulationResultApplierTests`.

Minimalna macierz dla każdego statusu:

- poprawna aplikacja;
- expiry na granicy ticka;
- ponowna aplikacja przez ten sam source;
- aplikacja przez różne source;
- interakcja z death;
- interakcja z `Invulnerability`;
- eventy bez duplikatów;
- stabilny wynik przy identycznym seedzie i kolejności wejścia.

Testy szczególnie ważne:

- `Stun` przed fire anuluje windup, ale nie cofa fire zebrany do równoczesnej
  partii;
- `Stun`, `Sleep` i `Root` nałożone podczas kroku pozwalają logicznie oraz
  wizualnie dotrzeć do zarezerwowanego destination, ale nie pozwalają
  rozpocząć kolejnego kroku;
- `Sleep` nie budzi się od pełnej absorpcji shieldem;
- `Silence` nie konsumuje pełnej many;
- `Slow` nie zmienia trwającego kroku/cyklu, lecz wpływa na następny;
- `Bleed` respektuje cap i odświeża duration;
- DoT może zakończyć walkę przed fazą akcji;
- `Invulnerability` czyści harmful statuses i blokuje nowe;
- `Guard` przekierowuje połowę bazowego pakietu dokładnie raz, zachowuje sumę
  integer damage przy liczbach nieparzystych i stosuje obronę obu odbiorców;
- `Taunt` nie wybiera nieosiągalnego lub untargetable źródła;
- projectile używa statusów celu z impact time, nie fire time;
- `Mark` uruchamia się tylko przy pierwszym bezpośrednim trafieniu, natychmiast
  znika i nie tworzy rekurencji z lifestealem.

### 13.2. Play Mode / prezentacja

- poprawne tworzenie, pooling i zwalnianie ikon;
- brak pozostawionych ikon po śmierci i kolejnej rundzie;
- CC nałożone podczas ruchu kończy widok na `MovementDestination`, zgodnie
  z logicznym stanem symulacji;
- event burst z wielu statusów zachowuje prawidłowy finalny stan UI;
- shield i HP są zgodne z runtime.

### 13.3. Wydajność

- po warm-upie `BattleTickLoop.Tick` nie alokuje pamięci dla statusów;
- brak LINQ, closure i string formatting w runtime ticku;
- pojemności kolekcji są tworzone przy budowie symulacji;
- event queue i workspaces są reużywane;
- profiler markers dla: status expiry/ticks, damage resolution, target selection
  z taunt i UI event processing.

## 14. Kryteria akceptacji

System jest gotowy, gdy:

- wszystkie wymienione statusy mają zdefiniowaną i przetestowaną semantykę;
- statusy działają wyłącznie w deterministycznej symulacji;
- melee i projectile używają jednego damage pipeline;
- CC nie może zostać ominięte przez alternatywny entry point;
- stacking, refresh, capy i expiry są jawne i pokryte testami;
- replay z tym samym seedem daje identyczne eventy oraz końcowy stan;
- ustabilizowany tick nie generuje GC alloc;
- statusy są czytelne na ekranie telefonu bez aktualizowania layoutu co klatkę;
- po końcu rundy nie pozostają runtime'owe statusy, relacje ani pooled UI w
  stanie aktywnym;
- żaden produkcyjny asset jednostki, speciala ani spella nie otrzymuje statusu
  bez osobnej decyzji contentowej;
- legacy attack speed pozostaje izolowany, a plan jego późniejszej migracji
  nie wymaga jednoczesnego utrzymywania dwóch źródeł modyfikatora na tej samej
  jednostce.

## 15. Zatwierdzone decyzje

Zatwierdzone:

1. `Slow` zwiększa czas przejścia z hexa na hex, czyli faktycznie spowalnia
   ruch, oraz zwiększa attack cooldown.
2. `Invulnerability` przy aplikacji usuwa aktywne szkodliwe statusy i blokuje
   nowe przez czas trwania.
3. `Fearless` przy aplikacji usuwa aktywne CC i blokuje nowe CC przez czas
   trwania.
4. `Mark` zadaje dodatkowe obrażenia tylko przy pierwszym bezpośrednim
   trafieniu, a następnie zostaje usunięty.
5. `Guard` przekierowuje połowę obrażeń do guarda.
6. Wszystkie runtime'owe statusy wygasają po każdej walce.
7. `Stun`, `Sleep` i `Root` przerywają dalszy ruch po aktualnym kroku.
   Rozpoczęta animacja dochodzi do zarezerwowanego hexa; jednostka nie cofa
   się i nie pozostaje pomiędzy polami.
8. `Stun` i `Sleep` natychmiast anulują rozpoczęty windup.
9. W pierwszym wdrożeniu żadna produkcyjna jednostka, special ani spell nie
   otrzymuje statusu. Integracja jest weryfikowana definicjami testowymi.
