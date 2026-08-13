# Plan: special Fury_Swipes

## 1. Cel

Dodać ofensywny special `Fury_Swipes`, w którym jednostka wykonuje 10 kolejnych
uderzeń przez 1,5 sekundy. Każde uderzenie zadaje 70% wartości ataku jednostki.

Implementacja ma pozostać deterministyczna, niezależna od animacji i bez
alokacji w hot path. Logika uderzeń będzie wykonywana przez istniejący
`BattleTickLoop` i `SpecialCycleResolver`; animacja jedynie odtworzy wynik
symulacji.

## 2. Ustalone zasady działania

### 2.1. Timing

- `StrikeCount = 10`.
- `CastDuration = 1.5f`.
- Interwał nie będzie osobnym polem danych. Resolver wyliczy go jako
  `CastDuration / StrikeCount`, czyli `0.15f`.
- Pierwsze uderzenie następuje po pierwszym interwale od rozpoczęcia castu,
  a dziesiąte na końcu castu:

```text
hit 1  = cast start + 0.15 s
hit 2  = cast start + 0.30 s
...
hit 10 = cast start + 1.50 s
```

- Produkcyjny `BattleTimingConfig_MVP` ma już `CombatTickDuration = 0.15`, więc
  standardowo przypadnie dokładnie jedno uderzenie na tick.
- Logika użyje absolutnych deadline'ów (`double`), a nie odejmowanego czasu.
  Przy większym ticku resolver wykona wszystkie zaległe uderzenia w stabilnej
  kolejności, bez utraty i bez podwójnego uderzenia.
- `WindupDuration` pozostaje osobnym czasem przygotowania. Dla assetu MVP
  przyjąć `0.2f`; okres 1,5 sekundy zaczyna się dopiero po windupie.

### 2.2. Cel i commitment

Plan zakłada przypisanie speciala do `Scout`, którego prefab używa modelu
`Prawler_Cat`.

- Fury może rozpocząć windup tylko z żywym wrogim celem w aktualnym zasięgu
  ataku jednostki.
- Cel zostaje zablokowany na początku windupu w osobnym
  `LockedSpecialTargetUnitId`.
- Jednostka z pełną maną, ale bez celu w zasięgu, nadal może poruszać się w jego
  kierunku. Nie może zatrzymać się na stałe tylko dlatego, że special jest
  naładowany.
- Śmierć celu podczas windupu anuluje special bez wydania many.
- Mana jest zerowana przy rozpoczęciu castu, zgodnie z obecnym modelem
  commitment.
- Po rozpoczęciu castu nie ma retargetowania. Śmierć celu kończy serię wcześniej,
  a wydana mana nie wraca.
- Stun, sleep, silence albo śmierć rzucającego podczas castu przerywają pozostałe
  uderzenia i zachowują istniejący recovery lock.
- Ruch i zwykły atak pozostają zablokowane przez cały windup i cast.

Brak retargetowania jest świadomym założeniem MVP: upraszcza reguły, zachowuje
czytelny commitment do celu i odpowiada istniejącemu blokowaniu celu podczas
windupu zwykłego ataku.

### 2.3. Obrażenia

Każde uderzenie korzysta ze wspólnej ścieżki obliczeń obrażeń, ale z mnożnikiem
`0.7f`:

```text
damage = effective attack * 0.7 * outgoing modifiers * armor mitigation
```

- Zaokrąglenie następuje raz, na końcu obliczenia, przez istniejącą regułę
  `MidpointRounding.AwayFromZero`.
- Bez obrony i innych modyfikatorów pełna seria zadaje 700% ataku.
- Wartość jest wyliczana na moment każdego uderzenia, więc aktywne wtedy buffy,
  debuffy, armor, exposed, shield oraz invulnerability działają normalnie.
- `AttackBonusNextCombat` nie jest zużywany przez special.
- Uderzenia nie dodają rzucającemu impulsu `ManaPerTick` za basic attack.
- Cel nadal otrzymuje impuls `ManaPerTick` zgodnie ze wspólnym
  `DamageResolver`.
- Użyć `DamageKind.Special`. Oznacza to brak mechanik zastrzeżonych obecnie dla
  zwykłego `Direct`: Fury nie konsumuje `Mark`, nie uruchamia lifestealu i nie
  jest przekierowywane przez `Guard`.
- Fury MVP nie wykonuje rzutów critical. Dzięki temu pojedynczy hit pozostaje
  dokładnie 70-procentowym uderzeniem i special nie zużywa dodatkowych wartości
  z deterministycznego RNG.

## 3. Dane i combat spec

### 3.1. `UnitSpecialKind`

Rozszerzyć enum bez zmiany istniejących wartości:

```csharp
None = 0,
HasteBurst = 1,
FurySwipes = 2
```

### 3.2. `UnitSpecialDefinition`

Dodać pola wspierające special wielouderzeniowy:

```csharp
[Min(1)] public int StrikeCount = 1;
[Min(0f)] public float AttackDamageMultiplier = 1f;
```

`CastDuration` pozostaje źródłem całkowitego czasu serii. Nie zapisywać osobno
interwału, ponieważ dwie wartości opisujące ten sam timing mogłyby się rozjechać.

`OnValidate` ma ograniczyć liczbę uderzeń do co najmniej 1 i mnożnik do wartości
nieujemnej. Pola statusowe pozostają używane przez `HasteBurst`, ale nie mogą być
wymagane dla `FurySwipes`.

### 3.3. `UnitSpecialCombatSpec`

Skopiować `StrikeCount` i `AttackDamageMultiplier` z `ScriptableObject` do
niemutowalnego combat specu. Przebudować `IsValid` zależnie od rodzaju speciala:

- `HasteBurst`: poprawny status `Haste`;
- `FurySwipes`: `CastDuration > 0`, `StrikeCount > 0` i
  `AttackDamageMultiplier > 0`.

`FromDefinition` nie może już zwracać `default` tylko dlatego, że
`AppliedStatus == null`; jest to poprawna konfiguracja offensive speciala.

### 3.4. Assety

Utworzyć `Assets/DeckBattle/Data/Specials/Special_Fury_Swipes.asset`:

```text
SpecialId: fury_swipes
Kind: FurySwipes
WindupDuration: 0.2
CastDuration: 1.5
StrikeCount: 10
AttackDamageMultiplier: 0.7
AppliedStatus: null
```

Przypisać asset do `Assets/DeckBattle/Data/Units/Scout.asset`, zastępując dla tej
jednostki `Special_HasteBurst`. Pozostałe jednostki i istniejący asset Haste
pozostają bez zmian.

## 4. Stan runtime

Rozszerzyć `UnitRuntimeState` o minimalny stan serii:

```csharp
public int LockedSpecialTargetUnitId;
public int SpecialStrikesResolved;
public double NextSpecialStrikeTime;
```

Opcjonalny `SpecialCastStartTime` warto dodać tylko wtedy, gdy ułatwi testy lub
debug snapshot; nie jest potrzebny do samego wykonania serii.

Wszystkie pola trzeba czyścić w konstruktorze, `ResetSpecialCycle`,
`ResetForBattle`, po anulowaniu oraz po poprawnym zakończeniu speciala. Nie
używać listy timerów ani coroutine per jednostka.

## 5. Reguły akcji i targetowania

Obecne `HasReadySpecial` rozpoznaje wyłącznie `HasteBurst` i blokuje ruch każdej
naładowanej jednostki. Należy rozdzielić dwa pojęcia:

- `HasChargedSpecial` — poprawny special i pełna mana;
- `CanStartSpecialWindup` — spełnione także warunki właściwe dla konkretnego
  rodzaju speciala.

Dla Fury `CanStartSpecialWindup` musi otrzymać kontekst `BattleSimulation`, aby
sprawdzić żywy cel, dystans i efektywny zasięg z `BattleRuntimeTuning`.

Wywołania `CanStartMovement` w `MovementResolver` i `BattleSimulation` również
muszą dostać kontekst symulacji. Reguła jest następująca:

- gotowy `HasteBurst` rezerwuje następną akcję natychmiast;
- gotowy `FurySwipes` rezerwuje akcję dopiero, gdy zablokowany/current target
  znajduje się w zasięgu;
- poza zasięgiem Scout może kontynuować dojście, ale nie rozpoczyna zwykłego
  attack windupu zamiast gotowego Fury.

Podczas `Windup` i `Casting` `BattleTickLoop.RefreshTargets` nie może zmieniać
celu jednostki. Po zakończeniu albo anulowaniu targetowanie wraca do zwykłych
reguł.

## 6. `SpecialCycleResolver`

### 6.1. Rozdzielenie faz ticka

Obecny tick wywołuje `AdvanceActiveCycles`, a później `Resolve`, które ponownie
wywołuje `AdvanceActiveCycles`. Zachować tę kolejność dla kompatybilności z
istniejącym timingiem `HasteBurst`, ale uczynić obsługę Fury idempotentną:
każde z dwóch przejść sprawdza absolutny `NextSpecialStrikeTime`, więc ten sam
deadline nie może wygenerować drugiego uderzenia.

Fazy pozostają jawne:

1. `AdvanceActiveCycles` raz, przed zwykłymi atakami — cancel, start castu,
   zebranie i rozstrzygnięcie uderzeń, zakończenie castu/recovery;
2. `StartReadyWindups` raz, po ruchu — start nowych speciali.

Nowy windup nie może przejść do castu w tym samym ticku.

### 6.2. Start Fury

Przy rozpoczęciu windupu:

- zapisać `LockedSpecialTargetUnitId`;
- wyzerować licznik uderzeń;
- wyemitować `SpecialWindupStarted` z targetem lub jego heksem, aby widok
  poprawnie ustawił facing.

Przy rozpoczęciu castu:

- ponownie sprawdzić żywego zablokowanego przeciwnika;
- wydać manę dokładnie raz;
- ustawić `NextSpecialStrikeTime = ElapsedTime + CastDuration / StrikeCount`;
- wyemitować jawny event `SpecialCastStarted` zawierający `targetId`,
  `sequenceId` i `CastDuration`.

### 6.3. Uderzenia

Dla każdego deadline'u `NextSpecialStrikeTime <= ElapsedTime`:

1. utworzyć mały intent zawierający attacker, locked target, sequence id i
   indeks uderzenia;
2. przesunąć deadline na podstawie startowego harmonogramu, a nie przez
   kumulowanie błędu `float`;
3. po zebraniu intentów rozstrzygnąć je w stabilnej kolejności jednostek i
   indeksów uderzeń;
4. obliczyć special damage;
5. przekazać go do `DamageResolver` jako `DamageKind.Special`;
6. wyemitować `SpecialStrikeFired` przed eventami obrażeń, aby prezentacja i
   testy znały źródło, cel, sequence id oraz numer uderzenia.

Po dziesiątym uderzeniu wejść w istniejący `RecoveryLock`, wyemitować
`UnitSpecialActivated` jako event zakończenia sekwencji i zrestartować cooldown
zwykłego ataku przez `AttackCycleResolver.RestartCooldownAfterSpecial`.

Jeżeli cel umrze wcześniej, zakończyć serię bez generowania pustych kolejnych
uderzeń. Nie zwracać many.

### 6.4. Workspace i równoczesność

Wykorzystać obecny pusty `SpecialCycleResolver.Workspace` do prealokowanych tablic
intentów. Pojemność powinna pokryć liczbę jednostek razy maksymalną obsługiwaną
liczbę zaległych uderzeń na tick. Dla Fury MVP można przyjąć twardy, walidowany
limit 10 lub mały limit ogólny z bezpiecznym catch-upem w kolejnych partiach.

Nie tworzyć `List`, delegatów, closure ani obiektów uderzeń w trakcie ticka.
Zebranie intentów przed resolution zachowuje granicę równoczesności podobną do
`AttackCycleResolver`: jednostki zakwalifikowane do tej samej partii wykonują
swoje należne uderzenie nawet, jeśli wcześniejszy intent w partii je zabije.

## 7. Obliczanie i rozstrzyganie obrażeń

Rozszerzyć `DamageCalculator` o ścieżkę special damage albo wspólny prywatny
rdzeń z parametrami:

```csharp
attackDamageMultiplier
canCritical
attackBonus
```

Dla Fury przekazać `0.7f`, `false` i `0`. Nie duplikować formuły armoru,
outgoing damage ani base attack multiplier w `SpecialCycleResolver`.

Do `DamageResolver` wysłać jawny `DamageRequest` z `DamageKind.Special`.
Istniejące zachowanie shield, exposed, invulnerability, mana za otrzymane
obrażenia, sleep wake-up i death pozostaje wspólne.

## 8. Eventy i prezentacja

### 8.1. `BattleEvent`

Dodać value-type eventy bez alokacji:

```text
SpecialCastStarted(attackerId, targetId, kind, sequenceId, duration, targetHex)
SpecialStrikeFired(attackerId, targetId, kind, sequenceId, strikeIndex, targetHex)
```

Do przechowania `strikeIndex` można użyć nowego jawnego pola. Nie przeciążać
`Amount`, ponieważ w eventach obrażeń oznacza ono damage.

`SpecialWindupStarted` powinien również przekazywać cel/pozycję dla poprawnego
facing. `UnitDamaged`, `UnitCrit` i `UnitDied` nadal pozostają jedynym źródłem
prawdy o wyniku damage.

### 8.2. `BattleView`, `BattleUnitPresenter`, `UnitView`

- Przy windupie ustawić kierunek na zablokowany cel i uruchomić stan `Special`.
- `SpecialCastStarted` utrzymuje animację speciala przez logiczne 1,5 sekundy.
- `SpecialStrikeFired` może uruchamiać lekki, pulowany efekt trafienia; nie może
  zadawać obrażeń ani wywoływać symulacji.
- `CompleteSpecialWindup` powinno jawnie przełączyć Animator do `Idle`, zamiast
  jedynie zmieniać lokalny enum i polegać na exit time klipu.
- Cancel i death mają pierwszeństwo nad późniejszymi eventami tej samej
  sekwencji.

Nie uruchamiać 10 coroutine ani 10 tweenów logicznych. Floating damage text już
jest pulowany; sprawdzić jego `prewarmCount = 16` i `maxActive = 32` przy kilku
równoczesnych Fury.

### 8.3. Animacja Prawler_Cat

Do prezentacji Fury wykorzystać istniejący klip osadzony bezpośrednio w modelu:

```text
Assets/DeckBattle/Art/Meshes/Prawler_Cat.fbx
clip: root|Special
length: około 0.3167 s
```

Plan konfiguracji:

1. zachować mapowanie slotu `Special` w
   `Assets/DeckBattle/Art/Animations/Units/Prowler_Cat.overrideController` na
   wewnętrzny klip `root|Special` z `Prawler_Cat.fbx`;
2. nie tworzyć ani nie podmieniać osobnego pliku animacji dla Fury;
3. zachować i zweryfikować `Loop Time` wewnętrznego klipu, aby mógł trwać przez
   całą serię;
4. zakończenie animacji sterować eventem symulacji, nie `Animation Event` ani
   długością klipu;
5. pozostawić root motion wyłączony;
6. sprawdzić w Play Mode, czy szybkość pętli czytelnie komunikuje serię. Jeżeli
   potrzebna jest dokładna synchronizacja wizualna 1:1, dodać parametr
   `specialSpeed` do Animatora i wyliczać go wyłącznie w prezentacji na podstawie
   długości klipu oraz interwału 0,15 s.

## 9. Testy

### 9.1. Edit Mode — dane i combat spec

- `FurySwipes` jest valid bez `AppliedStatus`.
- `HasteBurst` nadal wymaga poprawnego `Haste`.
- konwersja kopiuje 10, 1,5 i 0,7 bez odwołań do mutowalnego assetu;
- walidacja odrzuca zerową liczbę uderzeń, czas i mnożnik.

### 9.2. Edit Mode — `SpecialCycleResolverTests`

- pełna mana poza zasięgiem nie zamraża ruchu;
- wejście w zasięg rezerwuje Fury przed kolejnym zwykłym atakiem;
- start windupu blokuje i zapisuje cel, ale nie wydaje many;
- śmierć celu w windupie anuluje bez kosztu;
- start castu zeruje manę dokładnie raz;
- brak obrażeń przed `cast start + 0.15`;
- kolejne uderzenia wypadają na deadline'ach 0,15 s;
- dokładnie 10 uderzeń kończy się po 1,5 s;
- przy Attack = 100, bez obrony i modyfikatorów, każdy hit zadaje 70, a seria
  700;
- armor, outgoing modifier, exposed, shield i invulnerability współdziałają ze
  wspólną ścieżką obrażeń;
- brak critów oraz brak zużycia RNG;
- brak impulsu many dla rzucającego za basic attack, Mark, lifesteal i Guard dla `DamageKind.Special`;
- cel otrzymuje impuls `ManaPerTick`;
- coarse tick wykonuje wszystkie zaległe uderzenia raz i w dobrym porządku;
- dwa Fury w tym samym ticku zachowują ustaloną granicę równoczesności;
- stun/sleep/silence i śmierć rzucającego w castingu zatrzymują dalsze hity bez
  zwrotu many;
- śmierć celu podczas castu kończy serię bez retargetowania;
- reset bitwy czyści target, licznik i deadline.

### 9.3. Regresja

Uruchomić przez Unity MCP, w otwartym Editorze:

- `SpecialCycleResolverTests`;
- `CombatResolverTests`;
- `DamageCalculatorTests`;
- `DamageResolverTests`;
- `AttackCycleResolverTests`;
- `MovementResolverTests`;
- `BattleTickLoopTests`;
- `BattleRealtimeSynchronousCompatibilityTests`;
- testy kontraktu prezentacji i assetów Animatora.

### 9.4. Play Mode / scena `Battle`

- Scout dochodzi do celu z pełną maną zamiast zatrzymać się poza zasięgiem;
- po windupie wykonuje serię przez pełne 1,5 s;
- damage text i flash są czytelne dla 10 trafień;
- model pozostaje zwrócony do zablokowanego celu;
- target death, stun, silence, śmierć Scouta i koniec bitwy nie pozostawiają
  animacji w stanie `Special`;
- zachowanie jest poprawne przy combat acceleration, pauzie i resume;
- wewnętrzny klip `root|Special` nie używa root motion i nie przesuwa jednostki
  logicznie ani wizualnie.

## 10. Wydajność mobilna

- Oczekiwane `0 B GC.Alloc` po rozgrzaniu w `BattleTickLoop.Tick`,
  `SpecialCycleResolver` i ścieżce damage.
- Brak nowego `Update` per jednostka, coroutine, LINQ i wyszukiwania komponentów
  podczas walki.
- Uderzenia są ograniczoną pętlą nad prealokowanym workspace.
- Nie dodawać ciężkich shaderów ani nowych przezroczystych efektów ekranowych.
- Jeżeli powstanie efekt swipe/hit, użyć istniejącego podejścia poolingowego,
  prostego materiału URP i małej liczby cząstek.
- Sprawdzić pojemność `BattleEventQueue` oraz puli floating text przy kilku
  Fury aktywnych jednocześnie, aby uniknąć wzrostu kolekcji w pierwszym
  rzeczywistym starciu.
- Profilować CPU `DeckBattle.BattleTickLoop.Tick`, `Damage.Resolve`, liczbę
  aktywnych tekstów oraz overdraw efektów na profilu Android/mid-range.

## 11. Kolejność implementacji

### Etap 1 — kontrakt danych

- dodać `FurySwipes` do enumu;
- rozszerzyć `UnitSpecialDefinition` i `UnitSpecialCombatSpec`;
- dodać testy validacji i konwersji.

### Etap 2 — stan i deterministyczny resolver

- dodać target, licznik i deadline do `UnitRuntimeState`;
- rozdzielić charge od warunków aktywacji;
- poprawić regułę ruchu dla Fury poza zasięgiem;
- zabezpieczyć podwójne `AdvanceActiveCycles` przed ponownym wykonaniem tego
  samego uderzenia Fury;
- dodać harmonogram 10 uderzeń i prealokowany workspace.

### Etap 3 — damage i eventy

- dodać wspólną kalkulację z mnożnikiem 0,7 bez crita;
- rozstrzygać przez `DamageKind.Special`;
- dodać `SpecialCastStarted` i `SpecialStrikeFired`;
- uzupełnić testy integracyjne i regresję.

### Etap 4 — assety i prezentacja

- utworzyć `Special_Fury_Swipes.asset` i przypisać go do Scouta;
- zweryfikować, że override controller używa wewnętrznego `root|Special` z
  `Prawler_Cat.fbx`;
- utrzymać/kończyć animację na podstawie eventów symulacji;
- zweryfikować damage text, facing, cancel i death w Play Mode.

### Etap 5 — weryfikacja mobilna

- uruchomić pełny wskazany zestaw Edit Mode tests przez Unity MCP;
- wykonać smoke test sceny `Battle`;
- sprawdzić GC, CPU, pule i overdraw na profilu mobilnym.

## 12. Definition of Done

- Scout z pełną maną i celem w zasięgu rozpoczyna Fury przed kolejnym zwykłym
  atakiem;
- poza zasięgiem nadal dochodzi do celu;
- windup nie zadaje obrażeń i nie wydaje many;
- cast wydaje manę raz i wykonuje maksymalnie 10 uderzeń w 1,5 s;
- każdy hit ma bazowy mnożnik 0,7, bez crita i bez impulsu many dla rzucającego;
- pełna seria przeciw niebronionemu celowi daje 700% attack damage;
- target jest zablokowany, a seria nie retargetuje po jego śmierci;
- przerwania kończą logikę i animację bez zaległych hitów;
- `HasteBurst` oraz zwykły attack cycle nie mają regresji;
- wewnętrzna animacja `root|Special` z `Prawler_Cat.fbx` jest podpięta, nie
  steruje logiką i nie używa root motion;
- testy przechodzą, a hot path po rozgrzaniu pozostaje bez alokacji.
