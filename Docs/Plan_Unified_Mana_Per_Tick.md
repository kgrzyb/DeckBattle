# Plan ujednolicenia przyznawania many

## 1. Cel i ustalone zasady

Zastąpić dwa parametry `ManaPerAttack` i `ManaPerDamageTaken` jednym parametrem
`ManaPerTick`. Jest to pojedyncza, całkowita porcja many przypisana do jednostki.
Ta sama porcja jest przyznawana za trzy rodzaje impulsu:

1. jeden aktywny tick symulacji;
2. wykonanie jednego basic attacku przez jednostkę;
3. otrzymanie jednego rozstrzygnięcia obrażeń większych od zera.

Ustalone reguły:

- domyślna i początkowa wartość `ManaPerTick` dla wszystkich istniejących
  jednostek wynosi `3`;
- wyłącznie basic attack generuje impuls many atakującemu;
- speciale nie generują many rzucającemu za wystrzał, trafienie ani pojedynczy
  hit;
- obrażenia zadane przez special nadal generują impuls many dla każdego
  trafionego celu, ponieważ przechodzą przez wspólny pipeline obrażeń;
- jednostka nie otrzymuje żadnej many w fazach `Windup`, `Casting` i
  `RecoveryLock` speciala;
- pasywny impuls ticka jest rozstrzygany po atakach, obrażeniach i ruchu, ale
  przed uruchomieniem gotowych speciali;
- mana jest ograniczona przez istniejący `ManaThreshold` i nie przechodzi na
  kolejny cykl speciala;
- zmiana nie modyfikuje długości ticka symulacji ani progu many.

## 2. Stan obecny

Obecny model ma dwa pola w `UnitDefinition` i odpowiadające im pola w
`UnitCombatSpec`:

- `ManaPerAttack` jest przyznawana atakującemu przez `AttackCycleResolver` po
  wykonaniu basic attacku; dla ataku dystansowego dzieje się to przy
  wystrzeleniu pocisku;
- `ManaPerDamageTaken` jest przyznawana celowi w `DamageResolver` dopiero po
  przejściu obrażeń przez invulnerability i tarcze oraz po stwierdzeniu dodatniej
  utraty HP.

`CombatResolver.AddMana` odpowiada za walidację fazy speciala, ograniczenie do
progu i event `UnitManaChanged`. `BattleTickLoop` nie przyznaje obecnie pasywnej
many. UI szczegółów karty prezentuje dwa osobne typy statystyki many.

Aktywna konfiguracja `BattleTimingConfig_MVP.asset` używa ticka `0,15 s`.
Wartość `3` oznacza więc bazowo `20` many na sekundę ciągłej walki, zanim zostaną
doliczone impulsy za basic attack i otrzymane obrażenia. Zmiana długości ticka
świadomie zmienia tempo pasywnego ładowania, ponieważ statystyka jest określona
na tick, a nie na sekundę.

## 3. Docelowy model danych

### 3.1. `UnitDefinition`

W `Assets/DeckBattle/Scripts/Data/UnitDefinition.cs`:

- usunąć `ManaPerAttack` i `ManaPerDamageTaken`;
- dodać `public int ManaPerTick = 3`;
- w `OnValidate` ograniczyć wartość do `>= 0`;
- opcjonalnie użyć `FormerlySerializedAs("ManaPerAttack")` wyłącznie jako siatki
  bezpieczeństwa dla assetów spoza repozytorium. Nie wolno polegać na tej
  migracji dla assetów projektu, ponieważ przeniosłaby stare wartości `10/25`
  zamiast ustalonego `3`.

W każdym istniejącym assetcie w `Assets/DeckBattle/Data/Units` jawnie zapisać:

```yaml
ManaPerTick: 3
```

i usunąć oba stare klucze. Migrację assetów wykonać przez Unity MCP i
serializację Unity, aby zachować GUID-y oraz poprawny format YAML.

### 3.2. Niemutowalny runtime spec

W `Assets/DeckBattle/Scripts/Battle/CombatSpecs.cs`:

- zastąpić oba pola jednym `readonly int ManaPerTick`;
- uprościć konstruktor `UnitCombatSpec` i `FromDefinition`;
- ograniczyć runtime value przez `Math.Max(0, manaPerTick)`;
- nie dodawać osobnego stanu runtime, ponieważ wartość jest niezmienna podczas
  walki.

W `TestDefinitions.CreateUnit` ustawić `ManaPerTick = 3`, aby domyślne dane
testowe odpowiadały assetom produkcyjnym.

## 4. Jedna ścieżka przyznawania porcji many

W `CombatResolver` zachować `AddMana` jako niskopoziomową operację zmiany,
clampowania i wysyłania eventu. Dodać mały helper, np. `GrantManaPulse`, który
zawsze pobiera `unit.CombatSpec.ManaPerTick` i wywołuje `AddMana`.

Wszystkie trzy źródła mają korzystać wyłącznie z `GrantManaPulse`. Dzięki temu
atak, obrażenia i tick nie mogą rozjechać się wartościami w przyszłości.

Helper ma odrzucać impuls, gdy:

- jednostka jest `null` lub pokonana;
- `ManaPerTick <= 0`;
- `SpecialPhase` nie jest `Idle`.

Przed dodaniem eventu obliczyć nową wartość i porównać ją z `CurrentMana`.
Jeżeli clamp do progu nie zmieni many, nie emitować `UnitManaChanged`. Zapobiega
to eventowi i pracy UI w każdym ticku dla jednostki, która czeka na możliwość
uruchomienia naładowanego speciala.

Nie tworzyć nowego event type ani obiektu reprezentującego impuls. Istniejący
`UnitManaChanged` wystarcza do prezentacji i replayu stanu.

## 5. Integracja ze źródłami impulsów

### 5.1. Pasywny impuls symulacji

W `BattleTickLoop.Tick` dodać liniowy przebieg po `simulation.Units` po
`MovementResolver.ResolveMovement`, a przed końcowym
`SpecialCycleResolver.Resolve`.

Docelowa końcówka ticka:

1. przesunąć czas symulacji;
2. rozstrzygnąć statusy okresowe i wygaśnięcia statusów;
3. zakończyć trwające ruchy;
4. rozstrzygnąć pociski;
5. odświeżyć cele i aktywne cykle speciali;
6. rozstrzygnąć basic attacki;
7. odświeżyć cele i zaplanować ruch;
8. przyznać jeden pasywny impuls każdej żywej jednostce, która w tym momencie
   ma `SpecialPhase == Idle`;
9. uruchomić gotowe speciale;
10. sprawdzić zakończenie bitwy.

Skutki tej kolejności:

- jednostka zabita wcześniej w tym ticku nie dostaje pasywnej many;
- passive mana nie blokuje basic attacku już zaplanowanego na bieżący tick;
- osiągnięcie progu przez passive mana może rozpocząć windup speciala jeszcze w
  tym samym ticku;
- jednostka kończąca `RecoveryLock` wcześniej w tym ticku jest już `Idle`, więc
  może otrzymać pasywną porcję, ale istniejąca reguła
  `LastSpecialRecoveryEndTime` nadal nie pozwala rozpocząć następnego speciala w
  tym samym ticku;
- wywołanie `Tick` po `simulation.IsBattleEnded` nadal niczego nie przyznaje.

Pętla ma używać istniejącej listy jednostek, bez LINQ, tymczasowych kolekcji i
alokacji per tick.

### 5.2. Basic attack

W `AttackCycleResolver` zastąpić oba odwołania do `ManaPerAttack` wywołaniem
`GrantManaPulse`:

- melee otrzymuje dokładnie jedną porcję po rozstrzygnięciu wykonanego ataku;
- ranged otrzymuje dokładnie jedną porcję przy utworzeniu i wystrzeleniu
  pocisku;
- późniejsze trafienie pocisku nie daje drugiej porcji atakującemu;
- późniejsze chybienie wskutek śmierci celu nie cofa porcji za wykonany atak;
- crit, wartość obrażeń i liczba efektów ubocznych nie mnożą porcji;
- special strike, `Slam` i `MegaArrow` nie wywołują impulsu dla rzucającego.

### 5.3. Otrzymane obrażenia

W `DamageResolver` zastąpić `ManaPerDamageTaken` wywołaniem
`GrantManaPulse` w obecnym miejscu po `UnitDamaged`.

Jedna porcja jest przyznawana za każde osobne rozstrzygnięcie dodatniej utraty
HP, niezależnie od jej wartości i `DamageKind`:

- basic/direct damage;
- `DamageOverTime`;
- `Mark`;
- obrażenia `Special`, w tym każdy hit `FurySwipes` i każdy cel `Slam`;
- trafienie zwykłym lub specjalnym pociskiem.

Nie przyznawać porcji za invulnerability, pełną absorpcję przez shield ani
request z zerową wartością. Crit nadal daje jedną porcję. Przy Guardzie każde z
dwóch faktycznie zranionych źródeł docelowych otrzymuje własną porcję. Przy
śmiertelnych obrażeniach zachować obecną kolejność eventów:

```text
UnitDamaged -> UnitManaChanged (jeśli wartość się zmieniła) -> UnitDied
```

## 6. UI i kompatybilność serializacji

Aktualny UI korzysta już z przebudowy opartej na `CardDetailsPopupView` i
`StatView`. Przed implementacją trzeba ponownie sprawdzić stan worktree, bazować
na jego aktualnej wersji oraz zachować wszystkie równoległe zmiany użytkownika
w scenie, skryptach i assetach UI.

W `StatView.cs`:

- zastąpić dwa typy `ManaPerAttack` i `ManaPerDamageTaken` jednym
  `ManaPerTick`;
- formatować wartość jak dotychczas, np. `+3`;
- nadać enumowi jawne wartości liczbowe. Nowy `ManaPerTick` powinien przejąć
  slot `8`, slot `9` pozostawić nieużywany, a `Armor = 10` i
  `ArmorPenetration = 11` zachować bez zmian. Zapobiega to reinterpretacji już
  zapisanych `statType` w scenie po usunięciu elementu enumu.

W scenie pozostawić jedną pozycję statystyki many na tick i usunąć wyłącznie
drugą pozycję przyrostu many. Nie ruszać `ManaThreshold`. Zmianę sceny wykonać
przez Unity MCP, zachowując aktualny layout użytkownika oraz wszystkie
niezwiązane zmiany.

Docelowo statystyka powinna dostać ikonę oznaczającą pasywny impuls/tick. Nie
zmieniać nazw ani GUID-ów istniejących ikon użytkownika. Jeśli dedykowana ikona
nie jest gotowa w chwili implementacji, można tymczasowo zachować ikonę slotu
`ManaPerAttack`, ale nie ikonę obrażeń; osobna podmiana grafiki nie może blokować
migracji logiki.

Zaktualizować `CardDetailsPopupViewTests`, aby budował jeden `StatView` many i
oczekiwał `+3`. Zaktualizować także aktywne dokumenty w `Docs` odwołujące się do
dwóch starych statystyk, bez przeszukiwania ani modyfikowania
`Docs/CompletedPlans`.

## 7. Testy Edit Mode

### 7.1. `CombatResolverTests` / test helpera

Dodać testy:

- jeden impuls zwiększa manę dokładnie o `ManaPerTick = 3`;
- clamp zatrzymuje manę na `ManaThreshold`;
- brak zmiany na progu nie emituje `UnitManaChanged`;
- jednostka pokonana nie dostaje many;
- `ManaPerTick = 0` niczego nie zmienia;
- każda faza `Windup`, `Casting` i `RecoveryLock` blokuje impuls;
- po powrocie do `Idle` impuls znów działa.

### 7.2. `BattleTickLoopTests`

Sprawdzić:

- każda żywa, bezczynna względem speciala jednostka dostaje `+3` dokładnie raz
  na aktywny tick, także bez ataków i obrażeń;
- zakończona bitwa nie nalicza kolejnego impulsu;
- jednostka zabita przez DoT, pocisk albo melee wcześniej w ticku nie dostaje
  późniejszej pasywnej porcji;
- basic attacker otrzymuje w jednym ticku osobno porcję za atak oraz porcję
  pasywną, jeżeli pozostaje w `Idle`;
- cel otrzymujący obrażenia otrzymuje osobno porcję za damage oraz porcję
  pasywną, jeżeli przeżywa i pozostaje w `Idle`;
- przekroczenie progu przez pasywną porcję rozpoczyna special w tym samym ticku;
- pasywna porcja zostaje naliczona po basic attacku, a przed eventem
  `SpecialWindupStarted`;
- przebieg synchroniczny i realtime pozostają deterministycznie zgodne.

### 7.3. Ataki, pociski i obrażenia

Zaktualizować oczekiwania w:

- `AttackCycleResolverTests` — jeden impuls za melee/ranged basic attack;
- `ProjectileResolverTests` — impuls atakującego przy launchu, brak drugiego
  przy impact oraz jeden impuls celu za dodatni damage;
- `DamageResolverTests` — damage `> 0`, shield, invulnerability, crit, lethal,
  Guard, DoT i Mark;
- `SpecialCycleResolverTests` — brak przyrostu rzucającego za hity speciala,
  przyrost trafionych celów oraz blokada wszystkich impulsów podczas trzech faz
  speciala;
- testach `FurySwipes`, `Slam` i `MegaArrow` — każdy osobny poszkodowany target
  lub hit dostaje `+3`, caster nie dostaje porcji za special attack.

Nie sprawdzać jedynie końcowej sumy. W testach krytycznej kolejności sprawdzać
również liczbę i kolejność eventów `UnitManaChanged`, aby wykryć podwójne
naliczenie.

### 7.4. UI i regresja

- `CardDetailsPopupViewTests` pokazuje tylko jedną wartość `ManaPerTick = +3`;
- wszystkie istniejące `StatView` zachowują poprawne typy po zmianie enumu;
- snapshoty prezentacyjne i overlay many nadal reagują na niezmieniony event
  `UnitManaChanged`;
- pełny zestaw Edit Mode nie zawiera odwołań runtime/testów do
  `ManaPerAttack` ani `ManaPerDamageTaken`.

## 8. Kolejność wdrożenia

1. Zmienić `UnitDefinition`, `UnitCombatSpec` i `TestDefinitions` na
   `ManaPerTick`.
2. Wprowadzić wspólny `GrantManaPulse` i testy jego reguł.
3. Podłączyć basic attack oraz `DamageResolver` do wspólnego impulsu.
4. Dodać pasywny przebieg w ustalonym miejscu `BattleTickLoop`.
5. Zaktualizować wszystkie testy symulacji i potwierdzić kolejność eventów.
6. Przez Unity MCP zmigrować assety jednostek do `3` i sprawdzić kompilację.
7. Na aktualnej wersji UI zastąpić dwie statystyki jedną, zachowując numery
   serializowanego enumu, layout sceny i równoległe zmiany użytkownika.
8. Uruchomić najpierw wąskie testy resolverów, potem cały zestaw Edit Mode oraz
   odpowiednie testy Play Mode dla sceny/UI.
9. W Profilerze potwierdzić `0 B` GC Alloc dla nowej ścieżki ticka i brak
   zbędnych eventów po osiągnięciu progu.

## 9. Ryzyka i zabezpieczenia

| Ryzyko | Zabezpieczenie |
| --- | --- |
| Zbyt szybkie ładowanie przy ticku `0,15 s` | Start od ustalonego `3`; po wdrożeniu zmierzyć realny czas do speciala dla kilku archetypów |
| Podwójna mana za ranged attack | Test osobno dla launchu i impactu; atakujący dostaje impuls tylko przy launchu |
| Special zaczyna generować manę casterowi | Brak wywołań impulsu w `SpecialCycleResolver`; testy wszystkich rodzajów speciala |
| Wielokrotne eventy przy pełnym pasku | Emitować `UnitManaChanged` wyłącznie przy faktycznej zmianie |
| Przesunięcie typów statystyk w scenie | Jawne wartości `UnitStatType` z zachowaniem `Armor = 10` i `ArmorPenetration = 11` |
| Nadpisanie trwających zmian UI | Edycja sceny przez Unity MCP na bieżącym worktree, bez revertu i bez odtwarzania layoutu |
| Alokacje per tick | Prosta pętla indeksowa po istniejącej liście, bez LINQ i kolekcji tymczasowych |

## 10. Kryteria akceptacji

- w kodzie runtime i aktywnych testach nie występują `ManaPerAttack` ani
  `ManaPerDamageTaken`;
- każde źródło impulsu używa jednej wartości `UnitCombatSpec.ManaPerTick`;
- wszystkie istniejące jednostki mają `ManaPerTick: 3`;
- żywa jednostka w `Idle` dostaje `+3` raz za tick, `+3` za własny basic attack
  i `+3` za każde osobne otrzymane dodatnie obrażenie;
- caster nie dostaje many za ataki speciala;
- w `Windup`, `Casting` i `RecoveryLock` żadne źródło nie zwiększa many;
- pasywna mana jest naliczana po akcji bojowej, ale przed startem gotowego
  speciala;
- UI pokazuje jedną statystykę przyrostu many z wartością `+3` i zachowuje
  istniejący layout;
- testy Edit Mode i właściwe testy Play Mode przechodzą;
- nowa ścieżka nie alokuje pamięci per tick i nie emituje eventów bez zmiany
  stanu.
