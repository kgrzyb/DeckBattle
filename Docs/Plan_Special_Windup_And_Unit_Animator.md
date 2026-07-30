# Plan: windup speciali i animacje jednostek przez Animator

## 1. Cel

Rozszerzyć walkę o deterministyczny windup speciali oraz przełączyć
`UnitView` z proceduralnej animacji ataku/śmierci na dostarczony osobno
`Animator`.

Logika walki pozostaje niezależna od prezentacji:

- symulacja decyduje o rozpoczęciu, anulowaniu i zakończeniu windupu;
- `BattleEvent` przenosi wynik decyzji do widoku;
- `BattleView` mapuje eventy na metody `UnitView`;
- `UnitView` wyłącznie uruchamia animacje i nie kończy akcji przez
  `Animation Event`, `StateMachineBehaviour` ani callback animacji.

## 2. Ustalone zasady

### 2.1. Animator

Każda jednostka będzie miała dostarczony osobno `Animator` i controller.
`UnitView` otrzyma tylko serializowane pole pozwalające podpiąć ten komponent.

Controller udostępnia pięć parametrów typu `Trigger`:

- `idle`;
- `run`;
- `attack`;
- `special`;
- `dead`.

Klipy i konfiguracja controllerów nie należą do zakresu implementacji tego
planu.

Ruch po planszy nadal wykonuje kod prezentacji przez zmianę pozycji
`UnitView`. Animator odtwarza tylko animację biegu. Root motion musi pozostać
wyłączony.

### 2.2. Czas windupu speciala

`UnitSpecialDefinition` otrzyma osobne pole:

```csharp
[Min(0f)] public float WindupDuration;
```

Jest to bazowy czas w sekundach. Haste, slow, attack speed ani
`AttackCooldownMultiplier` nie modyfikują tej wartości.

Tak jak w zwykłym ataku, czas jest egzekwowany przez symulację:

```text
effective duration = max(combat tick duration, WindupDuration)
end time = actual windup start time + effective duration
```

Windup trwa więc co najmniej jeden pełny tick. Zakończenie następuje w
pierwszym ticku, dla którego `ElapsedTime >= SpecialWindupEndTime`.

### 2.3. Pierwszeństwo speciala

Pełna mana oznacza gotowość, ale nie natychmiastową aktywację.

Jednostka:

1. kończy rozpoczęty krok ruchu;
2. kończy bieżący atak razem z winddownem;
3. nie rozpoczyna kolejnego kroku ruchu ani kolejnego ataku;
4. rozpoczyna windup speciala w pierwszym dozwolonym ticku.

Podczas windupu speciala jednostka:

- nie porusza się;
- nie rozpoczyna ani nie wykonuje zwykłego ataku;
- nie rozpoczyna drugiego speciala;
- zachowuje aktualny cel, ale special MVP nie zależy od tego celu.

### 2.4. Commitment i anulowanie

Mana nie jest zużywana na początku windupu. Jest zerowana dopiero po
skutecznym rozstrzygnięciu speciala.

Windup anulują:

- śmierć jednostki;
- stun;
- sleep;
- silence.

Po anulowaniu:

- efekt speciala nie jest nakładany;
- mana pozostaje bez zmian;
- po ustaniu blokady jednostka może ponownie rozpocząć windup;
- widok wraca do `idle`, o ile jednostka nie jest już w stanie `dead`.

Zmiana ilości many po rozpoczęciu windupu nie anuluje już rozpoczętej akcji.
Windup jest commitment pointem, a skuteczna aktywacja ustawia manę na `0`.

## 3. Zmiany w danych i stanie runtime

### 3.1. `UnitSpecialDefinition`

Dodać `WindupDuration` i walidację wartości nieujemnej w `OnValidate`.
Nie wyprowadzać tego czasu z długości statusu, progu many ani cooldownu
zwykłego ataku.

Istniejący asset `Special_HasteBurst` musi otrzymać jawną, balansową wartość
windupu. Nie pozostawiać przypadkowego domyślnego `0`, nawet jeśli symulacja
zabezpiecza minimum jednego ticka.

### 3.2. `UnitRuntimeState`

Dodać jawny stan speciala, niezależny od `UnitAttackPhase`:

```csharp
public enum UnitSpecialPhase
{
    Idle = 0,
    Windup = 1
}
```

Stan jednostki powinien zawierać:

```csharp
public UnitSpecialPhase SpecialPhase;
public int SpecialSequenceId;
public double SpecialWindupEndTime;
```

Pola należy zerować w konstruktorze, `ResetForBattle` i wydzielonej metodzie
`ResetSpecialCycle`.

Osobny `SpecialSequenceId` pozwala widokowi ignorować spóźnione eventy
anulowania lub zakończenia poprzedniej animacji.

Nie łączyć `UnitAttackPhase` i `UnitSpecialPhase` w jeden rozbudowany automat.
Wzajemne wykluczanie powinno pozostać w małych, jawnych regułach akcji, co
ograniczy zakres zmiany istniejącego i przetestowanego cyklu ataku.

## 4. Reguły dostępności akcji

Rozszerzyć `UnitActionRules` o:

- sprawdzenie, czy jednostka ma poprawnie skonfigurowany i gotowy special;
- `CanStartSpecialWindup`;
- blokowanie ruchu podczas `SpecialPhase.Windup`;
- blokowanie attack windupu podczas `SpecialPhase.Windup`;
- blokowanie nowych ruchów i ataków, gdy mana jest pełna i special oczekuje na
  zwolnienie bieżącego ruchu lub attack winddownu.

Rezerwacja akcji przy pełnej manie jest istotna. Bez niej jednostka mogłaby w
tym samym ticku rozpocząć kolejny atak albo krok ruchu i odkładać special bez
końca.

Sprawdzanie gotowości nie może alokować i nie powinno używać LINQ. Powinno
opierać się wyłącznie na `UnitRuntimeState`, `UnitDefinition` i aktualnym
snapshotcie statusów.

## 5. `SpecialCycleResolver`

Wydzielić obsługę czasu speciala z `CombatResolver` do małego,
deterministycznego `SpecialCycleResolver`.

Resolver ma cztery odpowiedzialności:

1. anulować nieważne aktywne windupy;
2. zebrać windupy kończące się w bieżącym ticku;
3. rozstrzygnąć zebrane speciale;
4. rozpocząć nowe, gotowe windupy.

### 5.1. Start windupu

Start jest możliwy tylko, gdy:

- jednostka żyje;
- ma poprawny `UnitSpecialDefinition`;
- mana osiągnęła `ManaThreshold`;
- żaden status nie ustawia `BlocksSpecial`;
- jednostka nie wykonuje ruchu;
- `AttackPhase == AcquireReload`;
- `SpecialPhase == Idle`.

Przy starcie:

- ustawić `SpecialPhase = Windup`;
- inkrementować `SpecialSequenceId`;
- obliczyć `SpecialWindupEndTime`;
- wyemitować `SpecialWindupStarted`.

Nie zerować many i nie nakładać efektu na tym etapie.

### 5.2. Zakończenie windupu

Po osiągnięciu deadline'u resolver rozstrzyga special przez istniejącą logikę
aplikacji efektu. Dla `HasteBurst` pozostaje to
`StatusResolver.TryApply`.

Po skutecznym `Applied` albo `Refreshed`:

- ustawić manę na `0`;
- wyemitować `UnitManaChanged`;
- wyemitować `UnitSpecialActivated` ze zgodnym `SpecialSequenceId`;
- wrócić do `SpecialPhase.Idle`.

Efekt speciala zaczyna wpływać na logikę od następnego ticka. Dzięki temu
zakończenie speciala nie nakłada się w jednym ticku z nowym ruchem lub
attack windupem tej samej jednostki.

Jeżeli konfiguracja albo aplikacja efektu jest nieważna, nie zużywać many.
Zakończyć bieżący windup i raportować błąd konfiguracji w Editorze lub
Development Build, bez logowania co tick w buildzie produkcyjnym.

### 5.3. Równoczesność

Jeżeli kilka windupów kończy się w tym samym ticku, najpierw zebrać wszystkie
zakończenia do prealokowanego workspace, a dopiero potem je rozstrzygnąć.

Workspace powinien używać tablic o pojemności liczby jednostek utworzonych
razem z `BattleTickLoop`. Nie tworzyć list ani eventów klasowych per tick.

Pozwala to zachować tę samą granicę równoczesności, którą ma
`AttackCycleResolver`, i przygotowuje system na speciale wpływające później na
inne jednostki.

## 6. Integracja z `BattleTickLoop`

Usunąć natychmiastowe uruchamianie speciala:

- `CombatResolver.AddMana` ma tylko zmienić i ograniczyć manę oraz wyemitować
  `UnitManaChanged`;
- nie powinien bezpośrednio wywoływać aktywacji;
- zastąpić obecne `ActivateReadySpecials` wywołaniem
  `SpecialCycleResolver.Resolve`.

Rekomendowana kolejność końca ticka:

1. zaktualizować statusy czasowe;
2. dokończyć aktywne kroki ruchu;
3. rozstrzygnąć pociski;
4. odświeżyć cele i rozstrzygnąć cykle ataku;
5. zaplanować dozwolony ruch;
6. rozstrzygnąć i rozpocząć speciale;
7. sprawdzić zakończenie bitwy.

Reguła gotowego speciala blokuje kroki 4 i 5 przed rozpoczęciem nowej akcji.
Umieszczenie resolvera speciali po nich zapewnia, że:

- attack winddown może zakończyć się w tym ticku;
- kończący się krok ruchu może zwolnić jednostkę;
- special startuje bez konkurencyjnego triggera `run` lub `attack`;
- po aktywacji kolejna akcja zacznie się najwcześniej w następnym ticku.

## 7. Eventy symulacji

Rozszerzyć `BattleEventType` i fabryki `BattleEvent` o:

```text
SpecialWindupStarted(unitId, specialKind, sequenceId, duration)
SpecialWindupCancelled(unitId, specialKind, sequenceId)
UnitSpecialActivated(unitId, specialKind, sequenceId, effectDuration)
```

Istniejący `UnitSpecialActivated` należy rozszerzyć o sequence id, zamiast
tworzyć drugi event oznaczający to samo zakończenie.

Event `Duration` w `SpecialWindupStarted` oznacza czas windupu. W
`UnitSpecialActivated` może nadal oznaczać czas działania efektu, ponieważ
typ eventu jednoznacznie określa semantykę pola.

Eventy pozostają strukturami i nie mogą wprowadzać alokacji per tick.

Przy anulowaniu przez status `StatusResolver` powinien wywołać wspólną metodę
`SpecialCycleResolver.CancelWindup` dla stun, sleep i silence, analogicznie do
anulowania attack windupu. Śmierć ma ostateczne pierwszeństwo prezentacyjne:
spóźniony cancel nie może przełączyć martwej jednostki z `dead` na `idle`.

## 8. `BattleView`

Uzupełnić mapowanie eventów:

- `SpecialWindupStarted`:
  `UnitView.BeginSpecialWindup(sequenceId, duration)`;
- `SpecialWindupCancelled`:
  `UnitView.CancelSpecialWindup(sequenceId)`;
- `UnitSpecialActivated`:
  `UnitView.CompleteSpecialWindup(sequenceId)`.

`BattleView` nie odpytuje Animatora i nie czeka na zakończenie klipu.
Wszystkie decyzje pozostają po stronie eventów symulacji.

Obecny event `UnitSpecialActivated` nie jest obsługiwany przez
`BattleView`; tę lukę należy zamknąć w ramach wdrożenia.

## 9. `UnitView` i Animator

### 9.1. Referencja i hashe

Dodać:

```csharp
[SerializeField] private Animator animator;
```

Hashe parametrów utworzyć raz:

```csharp
private static readonly int IdleTrigger = Animator.StringToHash("idle");
private static readonly int RunTrigger = Animator.StringToHash("run");
private static readonly int AttackTrigger = Animator.StringToHash("attack");
private static readonly int SpecialTrigger = Animator.StringToHash("special");
private static readonly int DeadTrigger = Animator.StringToHash("dead");
```

Nie przekazywać nazw parametrów jako stringów podczas walki.

W każdym wariancie `PF_UnitView_*` podpiąć Animator znajdujący się na
zagnieżdżonym modelu. Bazowy `PF_UnitView` nie zawiera modelowego Animatora,
więc referencja będzie override'em wariantu.

### 9.2. Lokalny stan prezentacji

`UnitView` powinien pamiętać prosty stan wizualny, aby nie wysyłać tego samego
triggera wielokrotnie:

```text
Idle / Run / Attack / Special / Dead
```

Jest to wyłącznie ochrona prezentacji. Nie może być używana przez symulację.

Mapowanie:

- `Bind` i reset ponownie używanego widoku -> `idle`;
- start pierwszego wizualnego kroku ruchu -> `run`;
- koniec ostatniego zakolejkowanego kroku -> `idle`;
- `BeginAttackWindup` -> `attack`;
- anulowanie attack windupu -> `idle`;
- `BeginSpecialWindup` -> `special`;
- anulowanie special windupu -> `idle`;
- `PlayDeath` -> `dead`, bez późniejszego powrotu do innych stanów.

`AttackFired` i `UnitSpecialActivated` potwierdzają koniec logicznego
windupu, ale nie uruchamiają ponownie triggerów `attack`/`special`. Controller
powinien sam przejść z jednorazowego klipu do idle przez exit time.

### 9.3. Usunięcie konkurencyjnych animacji proceduralnych

Usunąć z `UnitView` DOTweenową sekwencję ataku i transformowe:

- lean back;
- strike rotation;
- attack pulse.

Nie odtwarzać jednocześnie Animatora i tweena na tym samym modelu.
Po usunięciu ostatniego użycia DOTween usunąć `using DG.Tweening` z
`UnitView`, ale nie usuwać paczki z projektu, ponieważ może mieć innych
użytkowników.

Animacja śmierci ma być sterowana triggerem `dead`. Istniejący timer może
nadal określać moment wyłączenia całego widoku, ale nie powinien już skalować
ani zatapiać modelu. `deathDuration` należy ustawić tak, aby nie ucinał
dostarczonego klipu.

Flash obrażeń przez `MaterialPropertyBlock` i interpolacja pozycji ruchu
pozostają bez zmian.

### 9.4. Ponowne użycie widoku

Przy `Bind`, `OnDisable` i ponownym włączeniu:

- wyczyścić lokalne sequence id ataku i speciala;
- wyczyścić zaległe triggery;
- odtworzyć poprawny stan początkowy Animatora;
- nie pozwolić, aby `idle`, `run`, `attack` lub `special` nadpisały `dead`
  przed ponownym bindem.

Operacje resetu Animatora wykonywać tylko podczas bind/reuse, nie co klatkę.

## 10. Prefaby i dane

Po dostarczeniu controllerów i klipów:

1. podpiąć Animator w sześciu istniejących wariantach:
   `Archer`, `Brute`, `Crossbowman`, `Guard`, `Scout`, `Swordsman`;
2. sprawdzić jednostki współdzielące te prefaby:
   `Lancer`, `Sniper`, `Tankbuster`;
3. zweryfikować obecność pięciu triggerów o dokładnej pisowni i typie;
4. wyłączyć root motion;
5. zachować obecny `AnimatorCullingMode`, o ile animacja śmierci i powroty
   z cullingu zachowują się poprawnie;
6. ustawić `WindupDuration` w `Special_HasteBurst`.

Brak Animatora albo parametru powinien być wyraźnie raportowany w Editorze,
ale runtime nie może rzucać wyjątku w buildzie produkcyjnym. Jednostka bez
poprawnej prezentacji nadal musi działać logicznie.

## 11. Testy Edit Mode

### 11.1. `SpecialCycleResolverTests`

Dodać testy:

- pełna mana rozpoczyna windup bez natychmiastowego efektu;
- mana nie jest zerowana na starcie;
- efekt i `UnitSpecialActivated` pojawiają się dopiero po deadline;
- `WindupDuration` krótszy niż tick trwa jeden pełny tick;
- windup zaczyna się od rzeczywistego ticka zwolnienia jednostki;
- haste i slow nie zmieniają deadline'u speciala;
- rozpoczęty krok ruchu kończy się przed startem speciala;
- nowy krok ruchu nie zaczyna się przy gotowym specialu;
- attack winddown kończy się przed startem speciala;
- gotowy special blokuje nowy attack windup;
- aktywny special blokuje ruch i atak;
- stun, sleep i silence anulują windup bez utraty many;
- śmierć anuluje windup bez aktywacji efektu;
- anulowany special może rozpocząć nowy windup po usunięciu blokady;
- kilka windupów kończących się w jednym ticku jest zbieranych przed
  resolution;
- jeden special nie może aktywować się dwa razy w tym samym ticku;
- reset bitwy czyści phase, deadline i sequence id.

### 11.2. Regresja

Zaktualizować obecne `CombatResolverTests`, które zakładają natychmiastowe
nałożenie `HasteBurst` przy attack fire.

Uruchomić także:

- `AttackCycleResolverTests`;
- `MovementResolverTests`;
- `StatusResolverTests`;
- `BattleTickLoopTests`;
- `BattleRuntimeTuningTests`.

Szczególnie sprawdzić, że dodanie special phase nie zmienia cadence jednostek,
które nie mają speciala albo nie osiągnęły pełnej many.

## 12. Testy prezentacji

Po dostarczeniu Animator Controllerów wykonać Play Mode smoke test sceny
`Battle`:

1. idle po spawnie i ponownym użyciu widoku;
2. run przez całą kolejkę kroków i idle po ostatnim kroku;
3. attack przy `AttackWindupStarted`;
4. special przez cały logiczny windup;
5. poprawny powrót do idle po cancelu;
6. dead bez późniejszego nadpisania przez spóźniony event;
7. brak przesuwania jednostek przez root motion;
8. brak podwójnych triggerów przy kilku tickach wykonanych w jednej klatce;
9. poprawne działanie po cullingu, pauzie oraz wznowieniu aplikacji.

## 13. Wydajność mobilna

Po wdrożeniu sprawdzić w Profilerze:

- `0 B GC.Alloc` po rozgrzaniu w `BattleTickLoop.Tick` i
  `SpecialCycleResolver`;
- brak LINQ, stringów i pobierania komponentów w hot path;
- wywołania Animatora tylko w odpowiedzi na zmianę stanu prezentacji;
- brak pozostałych DOTween sequences tworzonych dla ataku;
- stabilny frame time przy wielu równoczesnych windupach;
- poprawne culling i brak stale aktualizowanych Animatorów poza ekranem.

Nie dodawać per-unit `Update` do logiki speciala. Istniejący
`UnitView.Update` pozostaje wyłącznie dla aktywnej interpolacji ruchu,
obrażeń i czasu życia animacji śmierci.

## 14. Kolejność implementacji

### Etap 1 — dane i runtime

- dodać `WindupDuration`;
- dodać `UnitSpecialPhase` i pola runtime;
- dodać reset stanu speciala;
- rozszerzyć `UnitActionRules`.

### Etap 2 — deterministyczna symulacja

- utworzyć `SpecialCycleResolver` i prealokowany workspace;
- usunąć natychmiastową aktywację z `AddMana`;
- ustalić kolejność w `BattleTickLoop`;
- podłączyć anulowanie przez statusy i śmierć;
- dodać eventy faz speciala.

### Etap 3 — testy logiki

- dodać testy resolvera;
- zaktualizować testy obecnego `HasteBurst`;
- uruchomić wąski zestaw regresyjny w otwartym Unity Editorze.

### Etap 4 — `BattleView` i `UnitView`

- dodać obsługę eventów speciala;
- dodać pole Animatora i hashe triggerów;
- podłączyć idle/run/attack/special/dead;
- usunąć proceduralną animację ataku i śmierci konkurującą z Animatorem;
- zabezpieczyć sequence id i reset ponownie używanego widoku.

### Etap 5 — assety i weryfikacja

- po dostarczeniu controllerów podpiąć Animator w wariantach prefabów;
- ustawić balansowy czas `Special_HasteBurst`;
- uruchomić pełne Edit Mode tests;
- wykonać Play Mode smoke test;
- sprawdzić profil mobilny/Android.

## 15. Definition of Done

- special nie aktywuje się w ticku napełnienia many, jeśli jednostka nie może
  jeszcze rozpocząć windupu;
- rozpoczęty ruch i attack winddown kończą się przed specialem;
- pełna mana rezerwuje następną akcję dla speciala;
- efekt nie występuje przed końcem windupu;
- haste i slow nie zmieniają czasu windupu speciala;
- stun, sleep, silence i śmierć anulują windup;
- anulowanie nie zużywa many;
- aktywacja zużywa manę dokładnie raz;
- symulacja działa identycznie bez Animatora;
- `UnitView` używa wyłącznie pięciu uzgodnionych triggerów;
- root motion nie wpływa na pozycję logiczną ani wizualną na planszy;
- brak callbacków animacji sterujących gameplayem;
- brak alokacji per tick i brak dodatkowej logiki per-frame;
- wszystkie testy regresyjne przechodzą;
- scena `Battle` przechodzi smoke test bez błędów i nakładających się stanów
  Animatora.
