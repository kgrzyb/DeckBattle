# TDD: Główny cykl ataku

## Status

Status: zaimplementowany  
Data aktualizacji: 2026-07-26

Ten dokument opisuje autorytatywny cykl podstawowego ataku używany przez
`BattleTickLoop`. Starsze plany dotyczące stałych czasów windupu i winddownu
nie definiują już zachowania runtime.

## Cel

Podstawowy atak ma być deterministycznym cyklem sterowanym wyłącznie przez
symulację:

```text
AcquireReload -> Windup -> Fire -> Winddown -> AcquireReload
```

`AttackCooldown` określa pełny okres od początku jednego windupu do początku
następnego. Windup jest procentem efektywnego cooldownu, dlatego attack speed
skraca zarówno windup, jak i winddown.

Animacja nie może zadawać obrażeń, tworzyć logicznego pocisku ani kończyć fazy
ataku. Widok wyłącznie odtwarza zdarzenia wygenerowane przez symulację.

## Źródło prawdy

Główną i jedyną ścieżką wykonywania podstawowych ataków jest:

```text
BattleTickLoop.Tick
    -> AttackCycleResolver.Resolve
        -> HitResolver.ResolveHit
        -> BattleSimulation.SpawnProjectile
```

Stara natychmiastowa ścieżka `CombatResolver.ResolveCombat` została usunięta.
`CombatResolver` obsługuje wyłącznie zasoby związane z trafieniem, obecnie manę
i aktywację speciala.

## Dane jednostki

`UnitDefinition` przechowuje:

```csharp
public float AttackCooldown;
public float AttackWindupPercent = 0.25f;
```

Wszystkie istniejące jednostki używają `AttackWindupPercent = 0.25f`.
Wartość jest przechowywana w zakresie `0..1`.

Nie istnieje osobne pole `AttackWinddownDuration`. Winddown jest wynikiem
harmonogramu cyklu.

## Stan runtime

`UnitRuntimeState` przechowuje:

```csharp
public double NextAttackTime;
public UnitAttackPhase AttackPhase;
public int LockedAttackTargetUnitId;
public int AttackSequenceId;
public double AttackCycleStartTime;
public double WindupEndTime;
```

Znaczenie pól:

- `AttackCycleStartTime` — rzeczywisty czas rozpoczęcia bieżącego windupu;
- `WindupEndTime` — najwcześniejszy czas wykonania `Fire`;
- `NextAttackTime` — najwcześniejszy czas rozpoczęcia następnego windupu;
- `LockedAttackTargetUnitId` — cel zamrożony na czas windupu;
- `AttackSequenceId` — identyfikator pozwalający widokowi ignorować spóźnione
  zdarzenia poprzedniego cyklu.

Nie istnieje osobny `WinddownEndTime`. Koniec winddownu jest równy
`NextAttackTime`.

## Inicjalizacja walki

Po rozpoczęciu walki każda jednostka czeka pełny efektywny cooldown:

```text
InitialNextAttackTime = EffectiveAttackCooldown
```

Pierwszy windup może rozpocząć się dopiero, gdy:

- `ElapsedTime >= NextAttackTime`;
- jednostka żyje;
- jednostka nie jest w ruchu;
- ma żywy cel w zasięgu.

Oznacza to, że pierwszy damage nie występuje po samym początkowym cooldownie.
Po cooldownie zaczyna się jeszcze wymagany windup.

## Ruch a cykl ataku

Ruch jest stanem symulacji trwającym przez pełne `MovementStepDuration`.
Jednostka pozostaje na źródłowym hexie, rezerwuje `MovementDestination`
i zajmuje docelowy hex dopiero na pierwszej granicy ticka przypadającej po
końcu tego czasu. Odpowiada to zaokrąglonemu czasowi animacji kroku
w `UnitView`.

Jednostka w ruchu:

- nie może rozpocząć windupu;
- nie może rozpocząć kolejnego kroku ruchu;
- nie może ukończyć windupu ani wykonać `Fire`.

Jeśli stan ruchu zostanie wykryty podczas windupu, windup jest anulowany i
emitowany jest `AttackWindupCancelled`. Normalny resolver ruchu nie dopuszcza
do rozpoczęcia ruchu podczas `Windup` ani `Winddown`; anulowanie jest
dodatkowym zabezpieczeniem spójności symulacji.

## Obliczenie czasu cyklu

Przy rozpoczęciu windupu wykonywany jest snapshot aktualnych mnożników:

```text
effectiveCooldown =
    UnitDefinition.AttackCooldown
    * globalAttackCooldownMultiplier
    * runtimeAttackCooldownMultiplier

windupDuration =
    max(TickDuration, effectiveCooldown * AttackWindupPercent)

cycleDuration =
    max(effectiveCooldown, windupDuration)

AttackCycleStartTime = ElapsedTime
WindupEndTime = AttackCycleStartTime + windupDuration
NextAttackTime = AttackCycleStartTime + cycleDuration
```

Minimalny windup to jeden pełny tick symulacji. Nawet skrajnie wysoki attack
speed nie może spowodować natychmiastowego `Fire`.

Zmiana attack speed po rozpoczęciu windupu nie modyfikuje trwającego cyklu.
Zaczyna obowiązywać przy następnym windupie.

### Przykład

Dla:

```text
TickDuration = 0.25 s
AttackCooldown = 1.00 s
AttackWindupPercent = 25%
```

cykl wynosi:

```text
0.00  Windup start
0.25  Fire
1.00  Następny windup
```

Przy mnożniku attack speed skracającym cooldown do `0.50 s`:

```text
0.00  Windup start
0.25  Fire — windup zatrzymany na minimum jednego ticka
0.50  Następny windup
```

## Fazy cyklu

### AcquireReload

Jednostka może:

- utrzymywać albo wybrać cel;
- poruszać się w stronę pozycji ataku;
- rozpocząć windup, jeśli spełnia warunki gotowości.

### Windup

Na początku windupu:

- zapisywany jest snapshot czasów cyklu;
- cel zostaje zapisany w `LockedAttackTargetUnitId`;
- inkrementowany jest `AttackSequenceId`;
- emitowany jest `AttackWindupStarted`.

Podczas windupu jednostka:

- nie porusza się;
- nie retargetuje;
- nie zadaje obrażeń;
- nie wystrzeliwuje pocisku;
- nie otrzymuje many za wykonanie ataku.

Windup zostaje anulowany, jeżeli zablokowany cel albo atakujący umrze lub
atakujący znajdzie się w stanie ruchu.
Wyjście żywego celu z zasięgu nie anuluje już rozpoczętego ataku.

Anulowanie emituje `AttackWindupCancelled`. Nie zużywa jednorazowego bonusu
ataku i nie wykonuje `Fire`.

### Fire

`Fire` następuje w pierwszym ticku spełniającym:

```text
ElapsedTime >= WindupEndTime
```

Wszystkie kończące się windupy są najpierw zbierane do prealokowanego
workspace. Dopiero potem są rozstrzygane w stabilnej kolejności
`BattleSimulation.Units`.

Jednostka dopuszczona do tej partii może wykonać `Fire`, nawet jeśli zostanie
zabita przez wcześniejszy atak z tej samej partii.

Przy `Fire`:

1. Jednostka przechodzi do `Winddown`.
2. Emitowane są `AttackFired` i kompatybilne `UnitAttackStarted`.
3. Zużywany jest `AttackBonusNextCombat`.
4. Wyliczane są damage i crit.
5. Atakujący otrzymuje manę za wykonanie ataku.
6. Melee/instant wywołuje `HitResolver`.
7. Ranged z definicją pocisku tworzy `ProjectileRuntimeState`.

`AttackFired.Duration` zawiera pozostały czas winddownu:

```text
max(0, NextAttackTime - ElapsedTime)
```

### Winddown

Winddown trwa od `Fire` do:

```text
ElapsedTime >= NextAttackTime
```

Po zakończeniu emitowany jest `AttackWinddownEnded`, a jednostka wraca do
`AcquireReload`. Jeżeli nadal ma legalny cel w zasięgu, może rozpocząć następny
windup w tym samym wywołaniu resolvera.

Maksymalna częstotliwość pozostaje ograniczona przez minimalny jednotickowy
windup, więc jednostka nie może wykonać dwóch `Fire` w jednym ticku.

## Attack reset

Jedynym wspieranym resetem timera podstawowego ataku jest winddown reset:

```csharp
AttackCycleResolver.TryResetWinddown(
    BattleSimulation simulation,
    UnitRuntimeState unit,
    BattleEventQueue eventQueue = null);
```

Wyniki:

```csharp
AttackResetResult.Applied
AttackResetResult.IgnoredDuringWindup
AttackResetResult.IgnoredOutsideWinddown
AttackResetResult.IgnoredDead
```

Semantyka:

- podczas `Winddown`: `NextAttackTime` zostaje ustawiony na `ElapsedTime`,
  winddown kończy się i jednostka staje się gotowa do nowego windupu;
- podczas `Windup`: reset jest ignorowany;
- podczas `AcquireReload`: reset jest ignorowany;
- dla martwej jednostki: reset jest ignorowany.

Reset nie zadaje natychmiastowego damage. Nowy atak nadal musi przejść przez
co najmniej jeden tick windupu.

Umiejętności i aktywne przedmioty nie powinny bezpośrednio modyfikować
`NextAttackTime`. Powinny wywoływać `TryResetWinddown`.

## Melee i ranged

### Melee oraz ranged bez pocisku

Damage jest rozstrzygany przy `Fire` przez `HitResolver`.

### Ranged z pociskiem

Przy `Fire` powstaje logiczny pocisk zawierający gotowy payload damage/crit.
Damage następuje dopiero przy `ImpactTime`.

- śmierć atakującego nie usuwa pocisku;
- pocisk śledzi żywy cel;
- śmierć celu przed impactem powoduje resolution bez damage;
- impact nie zmienia harmonogramu następnego ataku.

## Kolejność ticka

`BattleTickLoop.Tick` wykonuje:

1. Wyczyszczenie kolejki zdarzeń.
2. Jedno przesunięcie `ElapsedTime`.
3. Wygaśnięcie aktywnych speciali.
4. Aktualizację czasu trwających kroków ruchu.
5. Rozstrzygnięcie pocisków.
6. Odświeżenie celów jednostek poza windupem.
7. Aktualizację głównego cyklu ataku.
8. Ponowne odświeżenie celów po trafieniach i śmierciach.
9. Rozstrzygnięcie nowych kroków ruchu.
10. Sprawdzenie zakończenia bitwy.

Pocisk rozstrzygnięty w kroku 4 może zabić jednostkę i anulować jej windup
przed krokiem `Fire`.

## Zdarzenia i prezentacja

Symulacja emituje:

- `AttackWindupStarted`;
- `AttackWindupCancelled`;
- `AttackFired`;
- `AttackWinddownEnded`;
- `ProjectileLaunched`;
- `ProjectileResolved`;
- zdarzenia trafienia, obrażeń, many i śmierci.

`BattleView` przekazuje czasy wyliczone przez symulację do `UnitView`.
Nie odczytuje już statycznego winddownu z `UnitDefinition`.

`UnitView` uruchamia `attackSequence` przy obsłudze `AttackWindupStarted`.
Łączny czas sekwencji jest równy `Duration` windupu z eventu. `AttackFired`
nie rozpoczyna kolejnej sekwencji ataku; w razie rozjazdu czasu domyka aktywną
sekwencję i uruchamia wyłącznie krótki efekt fire/pulse.

Callback animacji, tween lub Animation Event nie może wywołać logicznego
ataku.

## Granica symulacji i prezentacji

Przy każdej decyzji obowiązuje test:

> Czy ta wartość wpływa na to, kto wygra walkę?

Jeżeli tak, wartość i wynikająca z niej logika należą do symulacji. Muszą być
deterministyczne, aktualizowane na podstawie ticków i całkowicie niezależne od
faktycznego FPS, `Time.deltaTime`, `Update`, Animatora, tweenów, coroutine oraz
pozycji prezentacyjnych `Transform`.

Do symulacji należą między innymi:

- `IsMoving`, `CurrentHex`, `MovementDestination` i moment zakończenia kroku;
- wybór celu, zasięg i możliwość rozpoczęcia windupu;
- `AttackPhase`, `WindupEndTime`, `NextAttackTime` i attack reset;
- `Fire`, utworzenie logicznego pocisku, trafienie, damage, mana i śmierć.

Jeżeli wartość nie wpływa na wynik walki, należy do warstwy prezentacji. Może
korzystać z `Time.deltaTime`, `Update`, Animatora, DOTween, interpolacji
transformów, VFX i coroutine-podobnych wzorców.

Do prezentacji należą między innymi:

- pozycja i rotacja `UnitView`;
- tween przejścia między hexami;
- `attackSequence`, pulse, flash obrażeń i animacja śmierci;
- wizualny pocisk, efekty i timing kosmetyczny.

Symulacja nigdy nie odczytuje stanu `UnitView`, `Transform`, Animatora ani
tweenu. Określenie „jednostka fizycznie się zatrzymała” oznacza w regułach,
że symulacja zakończyła krok ruchu i ustawiła `IsMoving = false`. Spadek FPS
może opóźnić prezentację, ale nie może zmienić kolejności zdarzeń ani wyniku
walki.

## Wydajność mobilna

`AttackCycleResolver`:

- używa workspace utworzonego raz przez `BattleTickLoop`;
- nie używa LINQ;
- nie sortuje jednostek;
- nie tworzy list ani delegatów per tick;
- nie wykonuje wyszukiwań komponentów Unity;
- używa stabilnej kolejności istniejącej listy jednostek.

Zmiana nie wpływa na URP, shadery, tekstury, overdraw ani build size.

## TDD i testy kontraktu

Testy Edit Mode są podstawowym kontraktem implementacji.

`AttackCycleResolverTests` obejmuje:

1. Pełny cooldown przed pierwszym windupem.
2. Brak damage podczas windupu.
3. `NextAttackTime` liczony od rzeczywistego początku windupu.
4. Skracanie windupu i pełnego cyklu przez attack speed.
5. Minimum jednego ticka windupu.
6. Winddown równy pozostałemu czasowi do `NextAttackTime`.
7. Reset działający podczas winddownu.
8. Reset ignorowany podczas windupu.
9. Nowy atak po resecie nadal wymagający windupu.
10. Anulowanie windupu po śmierci celu.
11. Brak startu windupu przez pełny czas trwania kroku ruchu.
12. Anulowanie windupu, jeśli atakujący znajdzie się w ruchu.
13. Odrzucenie próby rozpoczęcia ruchu podczas windupu.

Pozostałe zestawy testów weryfikują:

- deterministyczną kolejność ataków;
- simultaneous fire;
- aktywację i wygaśnięcie attack-speed speciala;
- rozdzielenie projectile launch od impactu;
- zużycie jednorazowego bonusu ataku;
- zakończenie bitwy z aktywnymi pociskami;
- brak przesuwania czasu po zakończeniu bitwy.

Aktualny wynik regresji:

```text
Edit Mode: 206 / 206 testów zaliczonych
Kompilacja Unity: 0 błędów, 0 ostrzeżeń
```

Projekt nie zawiera obecnie testów Play Mode dla prezentacji ataku.

## Kryteria akceptacji

Cykl jest głównym sposobem ataku, gdy:

- `BattleTickLoop` wywołuje wyłącznie `AttackCycleResolver`;
- nie istnieje alternatywny resolver wykonujący natychmiastowe basic attack;
- wszystkie jednostki mają `AttackWindupPercent = 0.25f`;
- pierwszy windup czeka pełny początkowy cooldown;
- attack speed skaluje windup i pełny cykl;
- windup ma minimum jednego ticka;
- reset działa wyłącznie dla winddownu;
- śmierć celu anuluje windup;
- melee zadaje damage przy `Fire`, a projectile przy impact;
- pełny zestaw testów Edit Mode przechodzi;
- hot path nie generuje nowych alokacji.

## Pliki implementacji

- `Assets/DeckBattle/Scripts/Battle/AttackCycleResolver.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`
- `Assets/DeckBattle/Scripts/Battle/UnitRuntimeState.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleEvent.cs`
- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`
- `Assets/DeckBattle/Scripts/Battle/HitResolver.cs`
- `Assets/DeckBattle/Scripts/Battle/ProjectileResolver.cs`
- `Assets/DeckBattle/Scripts/Data/UnitDefinition.cs`
- `Assets/DeckBattle/Tests/EditMode/AttackCycleResolverTests.cs`
