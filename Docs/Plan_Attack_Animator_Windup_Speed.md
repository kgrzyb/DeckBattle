# Plan: skalowanie prędkości animacji ataku do windupu

## 1. Cel

Dostosować prędkość stanu animacji `attack` do procentowej zmiany logicznego
windupu ataku.

Symulacja nadal pozostaje źródłem prawdy:

- początek i koniec windupu wynikają wyłącznie z czasu symulacji;
- Animator nie wywołuje obrażeń, pocisków ani końca windupu;
- prędkość animacji jest snapshotowana na początku windupu;
- zmiana haste lub slow podczas trwającego windupu wpływa dopiero na następny
  atak.

Nie skalować globalnego `Animator.speed`, ponieważ zmieniłoby to także `idle`,
`run`, `special` i `dead`. Skalowany ma być wyłącznie stan ataku.

## 2. Stan obecny

Obecny przepływ wygląda następująco:

1. `AttackCycleResolver` oblicza efektywny cooldown i windup.
2. `AttackWindupStarted` przenosi `Duration` efektywnego windupu.
3. `BattleView` przekazuje czas do
   `UnitView.BeginAttackWindup(sequenceId, duration)`.
4. `UnitView` ignoruje obecnie `duration` i tylko ustawia trigger `attack`.
5. `char_AC.controller` nie ma parametru prędkości ataku, a stan docelowy
   triggera `attack` ma stałą prędkość `1`.

Controller wymaga dodatkowo kontroli konfiguracji one-shot: obecny stan ataku
`feeling` używa blend tree z klipem `Win`, a sam klip jest zapętlony. Nie wolno
uzależniać gameplayu od zakończenia tego klipu, ale przed odbiorem zadania
trzeba potwierdzić poprawny wizualny powrót z ataku.

## 3. Kontrakt czasu i prędkości

### 3.1. Bazowy i efektywny windup

Przy starcie ataku obliczać dwa czasy:

```text
windupPercent =
    clamp01(UnitDefinition.AttackWindupPercent)

baseWindupDuration =
    max(tickDuration,
        UnitDefinition.AttackCooldown * windupPercent)

effectiveAttackCycleDuration =
    BattleRuntimeTuning.GetAttackCooldown(definition, runtimeUnit)

effectiveWindupDuration =
    max(tickDuration,
        effectiveAttackCycleDuration * windupPercent)
```

`baseWindupDuration` odpowiada prędkości `1` i nie zawiera
`AttackCooldownMultiplier`, haste ani slow. `effectiveWindupDuration` pozostaje
obecnym, autorytatywnym czasem windupu.

### 3.2. Mnożnik odtwarzania

```text
attackAnimationSpeed =
    baseWindupDuration / effectiveWindupDuration
```

Przykłady:

| Zmiana logicznego czasu | Efektywny windup | Prędkość animacji |
| --- | ---: | ---: |
| brak modyfikatora | `100%` bazowego | `1.0` |
| haste skraca czas do `50%` | `50%` bazowego | `2.0` |
| slow wydłuża czas do `125%` | `125%` bazowego | `0.8` |
| haste wpada w minimum jednego ticka | czas zatrzymany na ticku | stosunek bazowego czasu do ticka |

Ważne: obecny status `Haste = 0.5` daje mnożnik czasu cooldownu `0.5`.
Odpowiada to dwukrotnej prędkości animacji, nie prędkości `1.5`.

Wynik zabezpieczyć przed wartością niedodatnią, `NaN` i nieskończonością.
Nie dodawać osobnego arbitralnego limitu wizualnego, który rozjechałby animację
z logicznym windupem. Istniejące ograniczenia mnożników statystyk i minimum
jednego ticka są podstawowym limitem.

## 4. Snapshot danych w evencie

Rozszerzyć `BattleEvent` o semantyczny mnożnik czasu odtwarzania, na przykład:

```csharp
public readonly float TimeScale;
```

Wartość domyślna dla eventów, które jej nie używają, powinna wynosić `1f`.
Fabryka:

```csharp
AttackWindupStarted(
    int attackerId,
    int targetId,
    int sequenceId,
    float duration,
    float timeScale)
```

`AttackCycleResolver` oblicza `baseWindupDuration`,
`effectiveWindupDuration` i `timeScale` jeden raz przy rozpoczęciu windupu.
Event przenosi oba potrzebne elementy kontraktu:

- `Duration` — rzeczywisty czas logicznego windupu;
- `TimeScale` — względną prędkość prezentacji dla tego konkretnego ataku.

Dzięki temu `BattleView` i `UnitView` nie odtwarzają obliczeń gameplayowych,
nie odpytują statusów oraz zachowują poprawny snapshot przy późniejszej zmianie
haste lub slow.

Dodatkowe pole zwiększy rozmiar struktury eventu o jeden `float`, ale nie tworzy
alokacji per tick. Po wdrożeniu należy sprawdzić rozmiar kolejki i potwierdzić,
że wzrost pamięci pozostaje pomijalny dla ustalonych pojemności.

## 5. `BattleView`

Zmienić mapowanie eventu na:

```csharp
view.BeginAttackWindup(
    battleEvent.SequenceId,
    battleEvent.Duration,
    battleEvent.TimeScale);
```

`BattleView` nie powinien:

- ponownie liczyć haste, slow ani cooldownu;
- odczytywać aktualnego stanu Animatora;
- czekać na normalized time lub zakończenie klipu;
- używać Animation Events do wykonania `AttackFired`.

## 6. `UnitView`

### 6.1. Parametr Animatora

Dodać hash tworzony raz:

```csharp
private static readonly int AttackSpeedParameter =
    Animator.StringToHash("attackSpeed");
```

Rozszerzyć API:

```csharp
BeginAttackWindup(
    int sequenceId,
    float duration,
    float timeScale);
```

Przed ustawieniem triggera `attack`:

1. zwalidować `timeScale`;
2. wywołać `animator.SetFloat(AttackSpeedParameter, safeTimeScale)`;
3. dopiero potem ustawić trigger stanu ataku.

Kolejność jest istotna, aby przejście rozpoczęte w tej samej klatce od początku
widziało właściwą prędkość.

Nie dodawać logiki do `Update`. Wartość jest ustawiana dokładnie raz na każdy
rozpoczęty windup.

### 6.2. Reset i anulowanie

Przy `Bind`/`ResetAnimator` ustawić `attackSpeed = 1f`, aby pooled lub ponownie
używany widok nie odziedziczył mnożnika poprzedniej jednostki.

Anulowanie windupu nadal przełącza widok do `idle`. Nie trzeba resetować
parametru przy każdym cancelu ani fire, ponieważ:

- parametr wpływa tylko na stan ataku;
- następny `BeginAttackWindup` zawsze ustawi nową wartość;
- unika to zbędnych wywołań Animatora.

Śmierć nadal ma pierwszeństwo i spóźniony cancel/fire nie może zmienić stanu
`dead`.

Brak Animatora lub parametru nie może zatrzymać symulacji. W Editorze i
Development Build konfiguracja powinna być możliwa do zdiagnozowania bez
logowania co klatkę.

## 7. Animator Controller

W controllerze używanym przez jednostki:

1. dodać parametr `Float` o nazwie `attackSpeed` i wartości domyślnej `1`;
2. w stanie uruchamianym triggerem `attack` włączyć `Speed Multiplier`;
3. wskazać parametr `attackSpeed`;
4. zachować bazową prędkość stanu jako wartość tuningową dla tempa `100%`;
5. nie podłączać parametru do stanów `idle`, `run`, `special` ani `dead`;
6. sprawdzić, czy klip ataku nie zapętla się nieintencjonalnie i czy controller
   poprawnie wraca do idle;
7. nie dodawać Animation Events sterujących gameplayem.

Jeżeli różne jednostki otrzymają osobne controllery lub override controllery,
każdy z nich musi zachować ten sam kontrakt parametrów:

```text
idle         Trigger
run          Trigger
attack       Trigger
special      Trigger
dead         Trigger
attackSpeed  Float
```

Zmiana prędkości stanu mnoży jego bazową prędkość. Pozwala to później stroić
różne klipy jednostek bez zmiany kodu i nadal zachować procentowe
przyspieszenie wynikające z windupu.

## 8. Prefaby i pokrycie assetów

Zweryfikować przez Unity Editor wszystkie warianty:

- `PF_UnitView_Archer`;
- `PF_UnitView_Brute`;
- `PF_UnitView_Crosbowman`;
- `PF_UnitView_Guard`;
- `PF_UnitView_Scout`;
- `PF_UnitView_Swordsman`.

Sprawdzić także jednostki współdzielące te prefaby:

- `Lancer`;
- `Sniper`;
- `Tankbuster`.

Na początku planu tylko `Archer` i `Crossbowman` mają lokalne, niezacommitowane
podpięcie pola `UnitView.animator`. Nie nadpisywać tych zmian. Pozostałe
warianty trzeba zinwentaryzować i podłączyć dopiero po potwierdzeniu właściwego
Animatora na zagnieżdżonym modelu.

Root motion pozostaje wyłączony.

## 9. Testy

### 9.1. Edit Mode — timing i event

Dodać testy czystej logiki:

1. brak modyfikatorów emituje `TimeScale = 1`;
2. haste skracające windup z `1.0 s` do `0.5 s` emituje `2`;
3. slow wydłużający windup z `1.0 s` do `1.25 s` emituje `0.8`;
4. `AttackCooldownMultiplier` jest uwzględniony w efektywnym windupie i
   mnożniku;
5. minimum jednego ticka ogranicza zarówno czas, jak i wynikowy mnożnik;
6. wartość nie jest zerowa, `NaN` ani nieskończona dla skrajnych poprawnych
   danych;
7. zmiana statusu po starcie nie zmienia już wyemitowanego snapshotu;
8. kolejny windup po zmianie statusu dostaje nowy mnożnik;
9. anulowany windup nie pozostawia błędnego sequence id dla kolejnego ataku.

Rozszerzyć istniejący
`AttackCycleResolverTests.Haste_ShortensWindupAndWholeCycleFromWindupStart`,
aby poza deadline'ami sprawdzał także `Duration` i `TimeScale` eventu.

Uruchomić co najmniej:

- `AttackCycleResolverTests`;
- `BattleRuntimeTuningTests`;
- `BattleTickLoopTests`;
- `CombatResolverTests`.

### 9.2. Prezentacja

W Play Mode sprawdzić:

1. normalny windup odtwarza atak z mnożnikiem `1`;
2. haste `0.5` daje wizualnie dwukrotnie szybszy atak;
3. slow daje proporcjonalnie wolniejszy atak;
4. jednostka z windupem ograniczonym do jednego ticka nie przyspiesza ponad
   faktyczną zmianę czasu;
5. cancel wraca do idle bez pozostawienia zapętlonego ataku;
6. kolejny atak po wygaśnięciu haste wraca do prędkości `1`;
7. special, run i death zachowują niezmienioną prędkość;
8. kilka ticków wykonanych w jednej klatce nie powoduje użycia mnożnika z
   niewłaściwego sequence id;
9. pooling/rebind resetuje `attackSpeed`.

Gameplayowe `AttackFired` powinno w każdym przypadku wystąpić w tym samym ticku
co przed zmianą prezentacji.

## 10. Wydajność mobilna

W hot path dopuszczalne są tylko:

- jedno obliczenie mnożnika przy rozpoczęciu windupu;
- zapis wartości do struktury eventu;
- jedno `Animator.SetFloat` i istniejący trigger na rozpoczęty atak.

Nie dodawać:

- per-unit `Update` dla synchronizacji animacji;
- odczytów `AnimatorStateInfo` co klatkę;
- stringów przekazywanych do Animatora w runtime;
- LINQ, coroutine ani nowych tweenów;
- callbacków animacji wpływających na symulację.

Po rozgrzaniu ścieżka startu windupu powinna nadal mieć `0 B GC.Alloc`.

## 11. Kolejność implementacji

### Etap 1 — kontrakt timingowy

- wydzielić lub dodać małą, czystą funkcję liczącą bazowy i efektywny windup;
- obliczyć `TimeScale` w `AttackCycleResolver`;
- rozszerzyć `AttackWindupStarted` i `BattleEvent`;
- dodać testy wzoru oraz snapshotu.

### Etap 2 — prezentacja kodowa

- przekazać `TimeScale` przez `BattleView`;
- dodać hash `attackSpeed` w `UnitView`;
- ustawiać parametr przed triggerem `attack`;
- resetować parametr przy bind/reuse;
- zachować obsługę braku Animatora.

### Etap 3 — controller i prefaby

- dodać `attackSpeed` do controllerów;
- podpiąć parametr jako mnożnik wyłącznie stanu ataku;
- zweryfikować one-shot, loop i powrót do idle;
- sprawdzić wszystkie warianty prefabów bez nadpisania istniejących zmian.

### Etap 4 — weryfikacja

- uruchomić wąskie testy Edit Mode przez Unity MCP;
- wykonać Play Mode smoke test sceny `Battle`;
- sprawdzić Profiler pod kątem GC i kosztu Animatora;
- potwierdzić brak zmian w ticku `AttackFired`.

## 12. Definition of Done

- prędkość stanu `attack` jest proporcjonalna do faktycznej zmiany czasu
  windupu;
- haste `0.5` daje `TimeScale = 2`, jeśli windupu nie ogranicza tick;
- slow proporcjonalnie zwalnia animację;
- minimum jednego ticka jest respektowane przez logikę i prezentację;
- mnożnik jest snapshotowany na początku ataku;
- tylko stan ataku używa `attackSpeed`;
- normalny atak używa wartości `1`;
- pooled widok nie dziedziczy poprzedniej prędkości;
- brak Animatora nie wpływa na wynik walki;
- Animator nie steruje gameplayem;
- nie powstaje nowa logika per-frame ani alokacja per attack/tick;
- testy regresyjne przechodzą;
- scena `Battle` przechodzi smoke test dla normal, haste, slow, cancel, death i
  reuse.
