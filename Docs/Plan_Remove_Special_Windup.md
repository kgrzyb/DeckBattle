# Plan: usunięcie windupu speciali

## 1. Cel

Usunąć osobną fazę `Windup` ze speciali. Gdy jednostka osiąga pełną manę i
spełnia warunki uruchomienia speciala, od razu rozpoczyna właściwy cast:

- mana zostaje wydana;
- uruchamia się animacja `Special`;
- uruchamia się VFX startu speciala;
- blokowany jest wymagany cel;
- rozpoczyna się harmonogram efektu, pocisku albo uderzeń;
- nie powstaje osobne okno windupu, które można anulować z refundem many.

Gameplay pozostaje sterowany przez deterministyczną symulację. Animator,
Animation Eventy i VFX nie mogą decydować o momencie obrażeń, aplikacji statusu,
wystrzelenia pocisku ani zakończenia castu.

## 2. Zakres i założenia

### 2.1. Moment startu

„Od razu po napełnieniu many” oznacza start w tym samym ticku symulacji, na
pierwszej legalnej granicy akcji. Nie należy wywoływać resolvera rekurencyjnie
bezpośrednio z `CombatResolver.AddMana`.

Zachować obecne reguły priorytetu:

- rozpoczęty zwykły atak dochodzi do `AttackFired`, a special może rozpocząć się
  później w tym samym ticku;
- rozpoczęty krok ruchu nie jest urywany ani teleportowany; po jego zakończeniu
  special startuje przed następnym krokiem;
- pełna mana blokuje rozpoczęcie nowego zwykłego ataku;
- special wymagający celu w zasięgu (`FurySwipes`, `MegaArrow`) może nadal
  pozwolić jednostce podejść do celu;
- `Longshot` wybiera globalnie wroga z najniższym HP;
- stun, sleep albo silence przed startem pozostawiają pełną manę i opóźniają
  cast do czasu usunięcia blokady.

Nie wprowadzać przerywania aktywnego ruchu ani attack windupu. Byłaby to osobna
zmiana zasad walki i prezentacji, niezwiązana z usunięciem windupu speciala.

### 2.2. Start speciala a timing efektu

Usunięcie windupu nie może oznaczać natychmiastowego Slam impactu ani
wystrzelenia pocisku w pierwszej klatce animacji. Special jest aktywny od razu,
ale jego payload nadal może mieć deterministyczny deadline zsynchronizowany z
klipem.

Rozdzielić pojęcia:

- **start castu** — pełna mana, commit, animacja i VFX od razu;
- **effect delay** — czas od startu castu do statusu, impaktu albo wypuszczenia
  pocisku;
- **cast duration** — całkowity czas blokady speciala, liczony od startu castu
  do jego zakończenia.

`EffectDelay` nie jest nowym windupem: jednostka jest już w `Casting`, mana jest
wydana, cooldown ustawiony, animacja/VFX działają, a anulowanie nie zwraca many.

## 3. Stan obecny i ryzyka regresji

Obecna ścieżka to `Idle -> Windup -> Casting -> RecoveryLock`. Windup odpowiada
jednocześnie za kilka różnych rzeczy:

- przechowuje `SpecialWindupEndTime` i opóźnia commit many;
- emituje `SpecialWindupStarted`/`SpecialWindupCancelled`;
- uruchamia animację większości speciali;
- może uruchamiać persistent `SpecialWindup` VFX;
- blokuje i przechowuje cel przed właściwym castem;
- dla `MegaArrow` i `Longshot` oznacza moment wypuszczenia pocisku;
- dla `Slam` oznacza moment impaktu;
- wpływa na walidację `CastDuration >= WindupDuration`.

Dlatego nie wystarczy ustawić `WindupDuration = 0`. Minimalny jeden tick nadal
pozostawiłby ukryty windup, eventy i dwufazowy commit, a usunięcie samego timera
rozsynchronizowałoby obrażenia z animacjami.

Wdrożenie musi zachować aktualną zasadę committed targetu z bieżącego worktree:
śmierć zablokowanego celu nie anuluje speciala i nie powoduje retargetowania.
Pozostałe strike'i lub pocisk kończą sekwencję jako miss bez obrażeń.

## 4. Docelowy model danych

### 4.1. `UnitSpecialDefinition`

Usunąć `WindupDuration` i zastąpić je polem opisującym timing payloadu:

```csharp
[Min(0f)] public float EffectDelay;
[Min(0f)] public float CastDuration;
```

`CastDuration` ma od tej zmiany jednolite znaczenie: całkowity czas od
`SpecialCastStarted` do zakończenia castu. `EffectDelay` jest liczony od tego
samego startu.

Walidacja:

- `0 <= EffectDelay <= CastDuration`;
- `FurySwipes` nadal wymaga dodatniego `CastDuration` i `StrikeCount`;
- projectile speciale nadal wymagają poprawnego `Projectile`;
- walidacja nie może już odnosić się do windupu;
- wartości są snapshotowane do `UnitSpecialCombatSpec` przed walką.

### 4.2. Migracja istniejących assetów

Zachować aktualne punkty kontaktu i całkowite długości klipów:

| Special | `EffectDelay` | nowe `CastDuration` | Zachowanie |
|---|---:|---:|---|
| `HasteBurst` | `0.30 s` | `0.30 s` | Haste pojawia się w dotychczasowym momencie `0.20 + 0.10`; animacja i cast VFX startują od razu. |
| `FurySwipes` | `0` | `1.50 s` | Cast startuje od razu; ciosy pozostają w `0.15, 0.30, ..., 1.50 s`. |
| `Slam` | `0.85 s` | `1.32 s` | Impakt pozostaje przy kontakcie z ziemią, a blokada trwa przez pozostałe około `0.47 s` klipu. |
| `MegaArrow` | `0.40 s` | `1.05 s` | Pocisk jest wypuszczany w dotychczasowej klatce, cały klip nadal trwa `1.05 s`. |
| `Longshot` | `0.85 s` | `1.65 s` | Wybór celu i cast są natychmiastowe, release pocisku pozostaje przy `0.85 s`. |

Deadline'y nadal rozstrzygać w pierwszym ticku, dla którego
`ElapsedTime >= deadline`. Nie dodawać per-frame timerów ani zależności od
długości odczytanej z Animatora.

### 4.3. `UnitSpecialCombatSpec`

- zastąpić `WindupDuration` przez `EffectDelay`;
- ujednolicić `IsValid` pod nowe znaczenie `CastDuration`;
- zachować wszystkie dane damage/status/projectile/execute;
- dodać test snapshotu dla każdej definicji produkcyjnej.

## 5. Stan runtime i reguły akcji

### 5.1. `UnitRuntimeState`

Docelowe fazy:

```text
Idle -> Casting -> RecoveryLock -> Idle
```

Usunąć:

- `UnitSpecialPhase.Windup`;
- `SpecialWindupEndTime`.

Zachować:

- `SpecialSequenceId`;
- `SpecialCastStartTime` i `SpecialCastEndTime`;
- `LockedSpecialTargetUnitId`;
- `SpecialStrikesResolved` i `NextSpecialStrikeTime`;
- recovery/mana lock.

Dodać jawny deadline pojedynczego payloadu, np. `SpecialEffectTime`, zamiast
przeciążać pola strike'ów albo ponownie używać nazwy windupu.

Reset konstruktora, `ResetForBattle`, zakończenie, anulowanie i reuse muszą
czyścić wszystkie deadline'y oraz locked target bez alokacji.

### 5.2. `UnitActionRules`

- zmienić `CanStartSpecialWindup` na `CanStartSpecialCast`;
- usunąć wszystkie sprawdzenia fazy `Windup`;
- `Casting` nadal blokuje ruch i zwykły atak;
- charged special nadal ma priorytet przed nowym attack windupem;
- zachować rozdzielenie `HasChargedSpecial` od warunków celu;
- zachować reguły `SpecialRequiresTarget`/`SpecialLocksTarget` i
  `TryGetLongshotTarget`;
- `IsSpecialActive` nadal obejmuje `Casting` i `RecoveryLock` zgodnie z obecną
  blokadą many.

## 6. `SpecialCycleResolver`

### 6.1. Podział odpowiedzialności

Zastąpić dwukrotne przechodzenie przez `AdvanceActiveCycles` jawnymi etapami:

1. `AdvanceActiveCasts` raz przed zwykłymi atakami;
2. zwykłe ataki, ruch i granty many;
3. `StartReadyCasts` raz po ostatnim źródle many w ticku.

Nie uruchamiać nowo rozpoczętego castu ponownie w tym samym skanie. Wszystkie
obecne payloady mają dodatni logiczny deadline: Haste/Slam/projectile korzystają
z `EffectDelay`, a pierwszy Fury strike z `CastDuration / StrikeCount`.

### 6.2. Start castu

`StartReadyCasts` dla każdej legalnej jednostki:

1. wybiera i blokuje cel, jeśli special go wymaga;
2. ustawia `SpecialPhase = Casting`;
3. inkrementuje `SpecialSequenceId`;
4. zapisuje `SpecialCastStartTime`, `SpecialEffectTime` i
   `SpecialCastEndTime`;
5. zeruje manę i emituje `UnitManaChanged`;
6. uruchamia `AttackCycleResolver.StartCooldownForSpecialCast`;
7. inicjuje harmonogram strike'ów Fury;
8. emituje dokładnie jeden `SpecialCastStarted` dla każdego rodzaju speciala.

To jest commitment point. Po nim stun, sleep, silence albo śmierć castującej
jednostki mogą przerwać sekwencję, ale nie zwracają many.

### 6.3. Advance i rozstrzygnięcie payloadu

W `AdvanceActiveCasts`:

- najpierw anulować casty zablokowane przez śmierć lub `BlocksSpecial`;
- dla `HasteBurst` na `SpecialEffectTime` nałożyć/odświeżyć Haste i przeliczyć
  attack cooldown względem `SpecialCastStartTime`;
- dla `FurySwipes` zachować absolutny harmonogram 10 uderzeń, catch-up kilku
  deadline'ów w jednym ticku i stabilną kolejność intentów;
- dla `Slam` zebrać wszystkie due impacty przed zadaniem obrażeń, aby dwa
  równoczesne Slamy nadal oba się rozstrzygały;
- dla `MegaArrow` i `Longshot` na `SpecialEffectTime` utworzyć dokładnie jeden
  pocisk i wyemitować istniejące eventy launch/strike;
- dopiero po rozstrzygnięciu payloadów kończących się w tym samym ticku przejść
  do `RecoveryLock` i wyemitować zakończenie castu;
- jeżeli cel zginął po commicie, nie retargetować i nie anulować; zachować miss
  oraz pełny przebieg animacji/strike eventów.

Workspace Fury i Slam pozostaje prealokowany. Nie dodawać list, LINQ,
coroutine, tweenów logicznych ani per-unit `Update`.

### 6.4. Anulowanie

Zmienić `CancelWindup` na `CancelActiveSpecial`/`CancelCast` i wywoływać tę
metodę z `StatusResolver` oraz `DamageResolver`.

- przed startem castu nie ma czego anulować, a pełna mana pozostaje;
- po starcie mana jest już wydana i nie jest refundowana;
- anulowanie przechodzi do `RecoveryLock`, czyści deadline'y/target i emituje
  event cancel z `sequenceId`;
- `UnitDied` ma pierwszeństwo prezentacyjne przed późnym powrotem do idle.

## 7. Eventy, animacja i VFX

### 7.1. `BattleEvent`

Usunąć z aktywnego przepływu:

- `SpecialWindupStarted`;
- `SpecialWindupCancelled`.

Ujednolicić cykl na:

```text
SpecialCastStarted(unitId, targetId, kind, sequenceId, duration, targetHex)
SpecialCastCancelled(unitId, kind, sequenceId)
SpecialCastCompleted(unitId, kind, sequenceId, duration)
```

`SpecialCastStarted` musi być emitowany także dla `HasteBurst` i `Slam`, które
obecnie nie mają wspólnego eventu startu castu. `SpecialStrikeFired`,
`SpecialAreaImpact`, projectile eventy oraz eventy damage/status pozostają
źródłem prawdy o payloadzie.

Zastąpić obecne użycie `UnitSpecialActivated` przez jednoznaczne
`SpecialCastCompleted`; nie używać eventu zakończenia do rozpoczynania animacji.

### 7.2. `BattleView`, `BattleUnitPresenter`, `UnitView`

- `SpecialCastStarted` ustawia target world position, zapisuje aktywne sequence
  id i uruchamia `Special` dokładnie raz dla każdego speciala;
- usunąć wyjątki, według których część klipów startuje w windupie, a Fury w
  castingu;
- release/strike eventy mogą poprawić facing, ale nie restartują klipu;
- `SpecialCastCompleted` wraca do `Idle` tylko dla zgodnego sequence id;
- `SpecialCastCancelled` wraca do `Idle`, o ile jednostka nie jest martwa;
- zachować root motion wyłączony i brak callbacków animacji sterujących
  gameplayem.

### 7.3. `BattleVfxPresenter`

- mapować start każdego speciala na istniejący `BattleVfxCue.SpecialCast`;
- usunąć runtime mapping `SpecialWindup`;
- persistent efekt castu, jeśli zostanie skonfigurowany jako `Manual`, kluczować
  przez `SpecialCast` i zwalniać na cancel, complete albo death;
- obecne profile `HasteBurst` i `FurySwipes` z cue `SpecialCast` zaczną działać
  od właściwego, natychmiastowego startu;
- nie przesuwać wartości liczbowych kolejnych `BattleVfxCue` w serializowanych
  assetach: wartość `6` pozostawić jako zarezerwowaną/deprecated albo nadać
  wszystkim kolejnym elementom jawne wartości;
- zaktualizować `BattleVfxValidation` i `Docs/Battle_VFX_Guide.md`, aby nie
  dokumentowały `SpecialWindup` jako aktywnego cue.

Skan assetów nie znajduje obecnie bindingu `Cue: 6`, więc nie jest potrzebna
migracja produkcyjnego profilu VFX, ale stabilność numerów enumów nadal musi być
chroniona.

## 8. Testy logiki

### 8.1. Wspólny cykl

Przebudować `SpecialCycleResolverTests` tak, aby potwierdzały:

1. mana osiąga próg i legalna jednostka w tym samym ticku wchodzi w `Casting`;
2. mana jest zerowana dokładnie raz na starcie;
3. emitowany jest jeden `SpecialCastStarted` i żaden event windupu;
4. cast startuje dla many uzyskanej z ataku, otrzymanych obrażeń i pasywnego
   pulse;
5. rozpoczęty atak dochodzi do fire, a special rozpoczyna się potem bez nowego
   attack windupu;
6. aktywny krok ruchu kończy się bez snapu, po czym cast ma pierwszeństwo przed
   kolejnym krokiem;
7. targeted special poza zasięgiem nie wydaje many i pozwala podejść;
8. stun/sleep/silence przed startem zachowują pełną manę;
9. stun/sleep/silence po starcie przerywają cast bez refundu;
10. śmierć castującej jednostki przerywa payload;
11. śmierć locked targetu nie anuluje ani nie retargetuje committed speciala;
12. reset bitwy czyści phase, sequence id, target i wszystkie deadline'y;
13. recovery lock i blokada ponownego gainu many pozostają bez zmian;
14. attack cooldown jest liczony od natychmiastowego startu castu;
15. jednostki bez speciala lub bez pełnej many zachowują dotychczasowy cadence.

### 8.2. Macierz istniejących speciali

| Special | Testy obowiązkowe |
|---|---|
| `HasteBurst` | Animacja/VFX startują przy pełnej manie; Haste pojawia się raz przy `EffectDelay`; duration i refresh statusu są poprawne; cooldown zostaje przeliczony po Haste. |
| `FurySwipes` | 10 ciosów po 70% w równych odstępach; suma 700%; brak podwójnych strike'ów przy catch-up; śmierć celu daje dalsze strike eventy jako miss; zakończenie po dziesiątym ciosie. |
| `Slam` | Brak damage przy starcie; impact po `0.85 s`; tylko wrogowie w promieniu; dwa równoczesne Slamy rozstrzygają się symetrycznie. |
| `MegaArrow` | Jeden projectile po `0.40 s`; 150% damage i stun dopiero na impact; martwy locked target daje miss; cast kończy się po `1.05 s`. |
| `Longshot` | Najniższe HP i deterministyczny tie-break są blokowane przy starcie; projectile po `0.85 s`; brak retargetu; execute jest liczony na impact; cast kończy się po `1.65 s`. |

### 8.3. Prezentacja i regresja

Rozszerzyć testy prezentacji:

- `SpecialCastStarted` ustawia facing i `UnitVisualState.Special` dla wszystkich
  pięciu rodzajów;
- `SpecialStrikeFired`/projectile release nie restartują animacji;
- cancel/complete używają sequence id;
- death nie jest nadpisywane przez późny cancel/complete;
- `SpecialCast` VFX uruchamia się dla Haste i Fury;
- nie pozostaje persistent handle po cancel, complete ani death.

Uruchomić przez Unity MCP najpierw wąskie Edit Mode suites:

- `SpecialCycleResolverTests`;
- `CombatResolverTests`;
- `StatusResolverTests`;
- `BattleViewFacingTests`;
- testy `BattleVfxPresenter`/walidacji assetów;
- `ProjectileResolverTests` i `DamageResolverTests`.

Następnie regresja:

- `AttackCycleResolverTests`;
- `MovementResolverTests`;
- `TargetSelectorTests`;
- `BattleTickLoopTests`;
- `BattleSimulationTests`;
- `BattleRuntimeTuningTests`;
- testy opisów i snapshotów `UnitCombatSpec`.

## 9. Play Mode i asset verification

W scenie `Battle` sprawdzić produkcyjne przypięcia:

- `HasteBurst`: Tankbuster, Maverick, Guard, Lancer, Kitsuro;
- `FurySwipes`: Prawler;
- `Slam`: AJ-4X;
- `MegaArrow`: Arisa;
- `Longshot`: Juni.

Dla każdego przypadku zweryfikować:

1. pasek many dochodzi do progu i resetuje się przy starcie castu;
2. animacja oraz startowy VFX ruszają bez osobnej pauzy windupu;
3. facing i locked target są stabilne;
4. payload pojawia się przy właściwej klatce/deadline;
5. klip nie restartuje się przy strike/release;
6. jednostka wraca do idle po cast/recovery;
7. stun, silence, death, pauza i wznowienie nie zostawiają zablokowanego stanu;
8. przyspieszenie fazy combat skaluje prezentację, ale nie zmienia kolejności
   logicznych eventów.

## 10. Wydajność mobilna

- jeden skan aktywnych castów i jeden skan startów na tick;
- `0 B GC.Alloc` po rozgrzaniu w `BattleTickLoop.Tick` i resolverze speciali;
- brak LINQ, reflection, stringów i `GetComponent` w hot path;
- zachować prealokowane workspace'y Fury/Slam;
- Animator i VFX wywoływać wyłącznie na eventach zmiany stanu;
- nie dodawać nowych coroutine, tweenów, fizyki ani `Update` per jednostka;
- sprawdzić liczbę aktywnych projectile/VFX views i stabilny frame time przy
  kilku jednoczesnych specialach.

Zmiana nie wymaga modyfikacji URP, shaderów, tekstur ani build size.

## 11. Kolejność implementacji

### Etap 1 — dane i migracja

- dodać `EffectDelay` i ujednolicić `CastDuration`;
- zmienić `UnitSpecialCombatSpec` i walidację;
- zmigrować pięć assetów zgodnie z tabelą;
- dodać testy poprawności produkcyjnych definicji.

### Etap 2 — runtime i resolver

- usunąć fazę/pole windupu;
- zmienić reguły na `CanStartSpecialCast`;
- przebudować start, deadline payloadu, cancel i completion;
- zachować target commitment oraz pending committed action przy końcu bitwy;
- usunąć podwójne `AdvanceActiveCycles`.

### Etap 3 — testy logiki per special

- najpierw przepisać wspólne testy cyklu;
- następnie potwierdzić osobno Haste, Fury, Slam, MegaArrow i Longshot;
- uruchomić wąską regresję resolverów przez Unity MCP.

### Etap 4 — eventy i prezentacja

- usunąć eventy/routing windupu;
- ujednolicić start/cancel/complete castu;
- uruchamiać Animator i `SpecialCast` VFX od eventu startu;
- zabezpieczyć sequence id, death precedence i persistent VFX cleanup;
- zaktualizować testy widoku oraz dokumentację VFX.

### Etap 5 — pełna weryfikacja

- uruchomić pełne istotne Edit Mode tests;
- wykonać Play Mode smoke test wszystkich produkcyjnych speciali;
- sprawdzić Unity Console, walidację assetów i Profiler mobilny.

## 12. Definition of Done

- w runtime nie istnieje `UnitSpecialPhase.Windup` ani
  `SpecialWindupEndTime`;
- pełna mana rozpoczyna legalny cast w tym samym ticku bez osobnego special
  windupu;
- mana, animacja, VFX, sequence id, target lock i attack cooldown commitują się
  razem na starcie;
- nie są emitowane eventy ani VFX cue special windupu;
- każdy special zachowuje swój poprawny punkt statusu/ciosu/impaktu/release i
  całkowity czas klipu;
- Haste, Fury, Slam, MegaArrow i Longshot przechodzą testy funkcjonalne;
- target death nie anuluje committed speciala ani nie powoduje retargetowania;
- stun/sleep/silence przed startem zachowują manę, a po starcie nie refundują;
- cancel, complete i death nie zostawiają złego Animator state ani persistent
  VFX;
- battle end czeka na committed cast/pocisk zgodnie z aktualnymi regułami;
- wszystkie wskazane testy przechodzą, Unity Console jest czysta, a hot path po
  rozgrzaniu nadal ma `0 B GC.Alloc`.
