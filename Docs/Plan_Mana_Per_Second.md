# Plan migracji mana gain z per tick na per second

## 1. Cel

Uniezależnić pasywne ładowanie many od długości ticka symulacji. Po zmianie
`CombatTickDuration` jednostka ma otrzymywać tę samą ilość pasywnej many za tę
samą liczbę sekund symulacji, z zachowaniem deterministycznego, bezalokacyjnego
przebiegu odpowiedniego dla urządzeń mobilnych.

Punktem odniesienia dla balansu jest aktywna konfiguracja
`BattleTimingConfig_MVP.asset`:

```text
CombatTickDuration = 0,15 s
ManaPerTick = 3
ManaPerSecond = 3 / 0,15 = 20
```

Po migracji jednostka z `ManaPerSecond = 20` nadal otrzyma `3` pasywnej many
na tick `0,15 s`, ale przy ticku `0,10 s` otrzyma średnio `2` many na tick, a
przy ticku `0,20 s` — `4`. Suma po jednej sekundzie symulacji pozostanie równa
`20`.

## 2. Istotna zależność w obecnym kodzie

`ManaPerTick` jest obecnie wspólną porcją dla trzech niezależnych źródeł:

1. pasywnego impulsu na końcu każdego aktywnego ticka;
2. wykonanego basic attacku;
3. otrzymanego rozstrzygnięcia obrażeń większych od zera.

Impulsów bojowych nie należy mnożyć przez długość ticka: attack i damage są
zdarzeniami, nie upływem czasu. Zgodnie z docelową decyzją dostają więc pełną
wartość `ManaPerSecond`.

Docelowa semantyka używa wyłącznie `ManaPerSecond` dla wszystkich źródeł many:

- pasywna mana jest naliczana proporcjonalnie do czasu ticka;
- basic attack i otrzymane obrażenia przyznają pełną wartość `ManaPerSecond`
  jako pojedynczy impuls.

Dla istniejących jednostek wartość startowa wynosi `20`. Zachowuje to pasywny
balans dla ticka `0,15 s`; impuls bojowy jest celowo równy `20` i nie ma osobnej
wartości do tuningu.

## 3. Docelowy model danych

### 3.1. Definicja i runtime spec jednostki

W `UnitDefinition`:

- zastąpić `int ManaPerTick` polem `int ManaPerSecond = 20`;
- w `OnValidate` ograniczyć wartość do `>= 0`;
- dodać tooltipy jednoznacznie opisujące źródła przyrostu.

W `UnitCombatSpec`:

- analogicznie zastąpić `ManaPerTick` jednym niemutowalnym polem;
- rozszerzyć konstruktor i `FromDefinition`;
- clampować wartości przez `Math.Max(0, value)`.

Rate pozostaje liczbą całkowitą. Pozwala to użyć deterministycznej arytmetyki
stałoprzecinkowej w runtime i jest wystarczające dla obecnej skali many. Jeśli
w przyszłości potrzebne będą wartości dziesiętne, można przejść na jawne
subjednostki (np. setne many na sekundę), bez wprowadzania `float` do stanu
symulacji.

### 3.2. Stan runtime

W `UnitRuntimeState` zachować `int CurrentMana` i dodać jedno pole typu `long`,
np. `PassiveManaRemainder`. Pole przechowuje niewidoczną część many mniejszą niż
`1`, wyrażoną jako licznik względem jednej sekundy w mikrosekundach.

Stan należy zerować:

- w konstruktorze i przy resecie jednostki;
- po osiągnięciu `ManaThreshold`, aby nadwyżka nie przeszła na kolejny cykl;
- przy starcie speciala razem z wyzerowaniem `CurrentMana` jako dodatkowe
  zabezpieczenie braku rolloveru.

Nie zmieniać typu `CurrentMana`, `ManaThreshold` ani payloadu
`UnitManaChanged`. UI i istniejące systemy nadal operują na pełnych punktach
many.

## 4. Przeliczanie per second na tick symulacji

W konstruktorze `BattleTickLoop` przeliczyć `tickDuration` dokładnie raz na
całkowitą liczbę mikrosekund:

```text
tickDurationMicros = Round(tickDuration * 1_000_000)
```

Dla każdej żywej jednostki w `SpecialPhase.Idle`, podczas pasywnej fazy ticka:

```text
remainder += ManaPerSecond * tickDurationMicros
wholeMana = remainder / 1_000_000
remainder %= 1_000_000
```

Jeżeli `wholeMana > 0`, przekazać je do istniejącej niskopoziomowej operacji
`CombatResolver.AddMana`. Jeżeli nie powstał pełny punkt many, nie emitować
eventu.

Zalety tego rozwiązania:

- wynik zależy od czasu symulacji, a nie od liczby ticków;
- brak dryfu wynikającego z wielokrotnego dodawania `float`;
- stan jest deterministyczny i łatwy do porównania w testach/replayu;
- brak alokacji i brak pracy UI dla zmian wyłącznie ułamkowych;
- `CurrentMana` i istniejący kontrakt eventów pozostają całkowite.

`long` daje duży zapas na mnożenie rate przez mikrosekundy. Mimo to należy
clampować dane wejściowe lub wykonać mnożenie w `checked` w testach granicznych,
aby błędny asset nie mógł po cichu przepełnić akumulatora.

## 5. Zmiany w przepływie symulacji

### 5.1. Pasywny przyrost

W `CombatResolver` rozdzielić obecny `GrantManaPulse` na dwie jawne operacje:

- `AccumulatePassiveMana(unit, tickDurationMicros, eventQueue)` — używa
  `ManaPerSecond` i reszty stałoprzecinkowej;
- `GrantCombatManaPulse(unit, eventQueue)` — dodaje całkowite
  `ManaPerSecond`.

`BattleTickLoop.GrantPassiveMana` ma wywoływać wyłącznie pierwszą operację i
przekazywać skwantowaną długość ticka obliczoną w konstruktorze loopa.

Zachować obecne miejsce w kolejności ticka:

1. advance czasu i statusy okresowe;
2. ruch, pociski i ataki;
3. odświeżenie celów i planowanie ruchu;
4. pasywna mana naliczona za dokładnie jeden `TickDuration`;
5. start gotowych speciali;
6. sprawdzenie końca bitwy.

Dzięki temu jednostka może osiągnąć próg z pasywnej many i rozpocząć special w
tym samym ticku, a jednostka zabita wcześniej w ticku nie dostanie przyrostu.

### 5.2. Basic attack i otrzymane obrażenia

W `AttackCycleResolver` oraz `DamageResolver` zastąpić wywołania wspólnego
`GrantManaPulse` przez `GrantCombatManaPulse`.

Zachować dotychczasowe reguły:

- jeden impuls dla atakującego za jeden wykonany basic attack;
- ranged dostaje impuls przy launchu, nie ponownie przy impact;
- jeden impuls dla celu za każde osobne dodatnie rozstrzygnięcie utraty HP;
- speciale nie dają casterowi impulsu za atak, lecz mogą dawać impuls
  trafionemu celowi przez wspólny pipeline damage;
- `Casting` i `RecoveryLock` blokują oba rodzaje przyrostu;
- clamp do progu i brak eventu przy braku faktycznej zmiany pozostają bez zmian.

Nie przekazywać długości ticka przez pipeline ataku i obrażeń. Impuls bojowy ma
być niezależny od częstotliwości symulacji i używa pełnego `ManaPerSecond`.

## 6. Migracja assetów i UI

### 6.1. Assety jednostek

Dla wszystkich assetów w `Assets/DeckBattle/Data/Units` wykonać migrację:

```yaml
ManaPerTick: 3
```

na:

```yaml
ManaPerSecond: 20
```

`FormerlySerializedAs` nie wystarczy, ponieważ migracja wymaga przeliczenia
wartości, a nie tylko zmiany nazwy. Assety należy zmigrować przez Unity MCP i
serializację Unity, zachowując GUID-y. Implementacja powinna najpierw potwierdzić
docelowy tick referencyjny `0,15 s`, ponieważ lokalny worktree zawiera już
niezależne zmiany w konfiguracji czasu walki.

### 6.2. Statystyka w szczegółach karty

W `StatView`:

- zmienić `UnitStatType.ManaPerTick` na `ManaPerSecond`, zachowując wartość
  liczbową enumu `8`, aby scena nie zinterpretowała serializowanego pola jako
  innej statystyki;
- formatować wartość jako `+20/s`;
- pozostawić `Armor = 10` i `ArmorPenetration = 11` bez zmian.

W scenie `Battle` zmienić nazwę obiektu `Stat_ManaPerTick` na
`Stat_ManaPerSecond` i zweryfikować ikonę/tooltip. Zmianę wykonać przez Unity MCP,
ponieważ scena ma już lokalne modyfikacje, których nie wolno nadpisać.

Tooltip many powinien wskazywać, że wartość opisuje zarówno pasywny przyrost,
jak i pojedynczy impuls za basic attack oraz otrzymane dodatnie obrażenia.

## 7. Testy

### 7.1. Testy przeliczenia czasu

Dodać testy Edit Mode potwierdzające:

- `20/s` przy ticku `0,15 s` daje `3` many po pierwszym ticku;
- po `1 s` symulacji końcowa pasywna mana jest taka sama dla ticków `0,05`,
  `0,10`, `0,20` i `0,25 s`;
- rate niepodzielny przez częstotliwość ticka, np. `7/s` przy `0,15 s`, zachowuje
  resztę i po dłuższym czasie nie dryfuje;
- ticki bez pełnego punktu many nie emitują `UnitManaChanged`;
- osiągnięcie progu czyści resztę i po resecie speciala nie występuje rollover;
- zakończona bitwa nie akumuluje kolejnego fragmentu many;
- czas w `Casting` i `RecoveryLock` nie jest naliczany wstecz po powrocie do
  `Idle`.

Porównania między różnymi długościami ticka wykonywać dla tej samej osiągalnej
granicy czasu symulacji, a nie dla tej samej liczby ticków.

### 7.2. Testy impulsów bojowych

Zaktualizować testy `CombatResolver`, `AttackCycleResolver`, `DamageResolver`,
`ProjectileResolver` i `SpecialCycleResolver`, aby potwierdzić:

- atak i damage dają pełne `ManaPerSecond` niezależnie od `TickDuration`;
- pasywna mana i impuls bojowy mogą wystąpić w jednym ticku jako dwa logiczne
  źródła;
- kolejność `UnitDamaged -> UnitManaChanged -> UnitDied` pozostaje poprawna;
- nie ma podwójnej many za ranged launch/impact ani many dla castera za special;
- pełny pasek nie emituje zbędnych eventów.

W testach integracyjnych nie opierać się wyłącznie na końcowej sumie. Dla
krytycznych przypadków sprawdzać liczbę oraz kolejność eventów.

### 7.3. Testy danych i UI

- `TestDefinitions.CreateUnit` otrzymuje wartości `20` i `3`;
- wszystkie produkcyjne assety przechodzą walidację i nie zawierają
  `ManaPerTick`;
- `CardDetailsPopupViewTests` oczekuje `+20/s` i typu enumu o wartości `8`;
- overlay many nadal dostaje wyłącznie całkowite wartości przez niezmieniony
  event `UnitManaChanged`;
- wąskie testy Edit Mode resolverów uruchomić przed pełnym zestawem Edit Mode;
- scenę i testy Play Mode UI zweryfikować przez Unity MCP.

## 8. Kolejność wdrożenia

1. Dodać nowe pola danych i runtime spec, zachowując tymczasowo kompilowalny
   etap migracji.
2. Dodać `PassiveManaRemainder` oraz bezalokacyjne przeliczenie
   `ManaPerSecond * tickDuration`.
3. Rozdzielić pasywną ścieżkę czasu od stałego impulsu bojowego.
4. Dodać wąskie testy matematyki, progu, faz speciala i eventów.
5. Zaktualizować testy ataków, obrażeń, pocisków i speciali.
6. Przez Unity MCP zmigrować assety jednostek do `20/s`.
7. Zmienić statystykę UI na `ManaPerSecond`, zachowując numer enumu i istniejący
   layout sceny.
8. Uruchomić pełne Edit Mode oraz właściwe Play Mode; porównać wynik tej samej
   walki przy kilku długościach ticka.
9. W Profilerze potwierdzić `0 B` GC Alloc w ścieżce ticka i brak nadmiarowych
   eventów/UI refreshy.

## 9. Kryteria akceptacji

- pasywna mana po tej samej liczbie sekund symulacji nie zależy od długości
  ticka, z dokładnością do niepełnego punktu przechowywanego w akumulatorze;
- basic attack i otrzymane obrażenia dają pełne `ManaPerSecond` niezależnie od ticka;
- aktywna konfiguracja `0,15 s` zachowuje dotychczasowe tempo `3` pasywnej many
  na tick dzięki `ManaPerSecond = 20`;
- próg, reset speciala, blokady faz i kolejność eventów pozostają zgodne z
  obecnym zachowaniem;
- w aktywnym kodzie, testach, assetach i scenie nie pozostają odwołania do
  `ManaPerTick`;
- obliczenie nie używa LINQ, kolekcji tymczasowych ani alokacji per tick;
- pełny zestaw właściwych testów przechodzi, a profilowanie nie wykazuje nowych
  kosztów GC ani zauważalnego kosztu CPU.

## 10. Ryzyka i zabezpieczenia

| Ryzyko | Zabezpieczenie |
| --- | --- |
| Jedna wysoka wartość `ManaPerSecond` ładuje special szybko po ataku/damage | Zbalansować jedyną statystykę na realnych walkach przed wydaniem |
| Ułamki many zginą przy każdym ticku | Przechowywać całkowitą resztę stałoprzecinkową w `UnitRuntimeState` |
| Nadwyżka przejdzie na następny special | Czyścić resztę przy progu i resecie many |
| Zmiana ticka zmieni wynik testów niezwiązanych z maną | W helperach testowych jawnie ustawiać rate/pulse; porównywać po równym czasie |
| Migracja nazwy zachowa stare `3`, ale nada mu znaczenie `3/s` | Nie polegać na samym `FormerlySerializedAs`; jawnie przeliczyć assety do `20/s` |
| Edycja sceny/configu nadpisze lokalne zmiany | Użyć Unity MCP na bieżącym worktree i ograniczyć zapis do wymaganych pól |
| Częste eventy od części ułamkowej obciążą UI | Emitować event dopiero po zmianie pełnego `CurrentMana` |
