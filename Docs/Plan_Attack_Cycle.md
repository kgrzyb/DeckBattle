# Plan implementacji cyklu ataku

## 1. Cel

Rozbudować obecną deterministyczną walkę o jawny cykl:

`Acquire/Reload -> Windup -> Fire -> Winddown -> Acquire/Reload`

Symulacja wykonywana stałym tickiem pozostaje jedynym źródłem prawdy.
Widok reaguje na zdarzenia symulacji i nie może przez callback animacji:

- wystrzelić ataku;
- zadać obrażeń;
- utworzyć logicznego pocisku;
- wybrać albo zmienić celu;
- zakończyć żadnej fazy ataku.

`Fire` jest atomowym przejściem na końcu windupu, a nie fazą trwającą
przez wiele ticków.

## 2. Semantyka cyklu

### 2.1. Commitment point

Commitment point następuje na początku windupu:

- jednostka musi być żywa, gotowa do ataku, nieruchoma i mieć żywy cel
  w zasięgu;
- `TargetUnitId` wybrany przez targeting zostaje skopiowany do osobnego
  `LockedAttackTargetUnitId`;
- rozpoczyna się `Windup`;
- jednostka nie może poruszać się ani retargetować do końca lub anulowania
  windupu;
- nie zużywamy jeszcze bonusu jednorazowego, nie losujemy crita, nie
  przyznajemy many i nie uruchamiamy efektów `on-attack`.

Zamrożony jest identyfikator celu, nie jego pozycja. Atak pozostaje związany
z tą samą jednostką, nawet jeśli cel zmieni heks.

### 2.2. Anulowanie przed fire

Windup zostaje anulowany, jeśli przed jego końcem:

- atakujący umrze albo zostanie usunięty z walki;
- zamrożony cel umrze albo przestanie być poprawnym celem.

Domyślnie samo wyjście żywego celu z zasięgu nie anuluje już rozpoczętego
ataku. Zapobiega to nieczytelnym przerwaniom po commitment point i pozwala
obsłużyć ruch celu bez retargetowania. Przyszły interrupt, stun lub disarm
powinien być osobną, jawną regułą.

Po anulowaniu:

- nie ma `on-attack`;
- nie ma many za atak;
- nie jest zużywany `AttackBonusNextCombat`;
- nie jest przesuwany cooldown;
- nie powstaje pocisk i nie ma obrażeń;
- jednostka wraca do `AcquireReload`, może wybrać nowy cel w tym samym
  ticku, ale nowy windup rozpocznie najwcześniej w następnym ticku.

### 2.3. Fire

Na pierwszym ticku, dla którego
`simulation.ElapsedTime >= WindupEndTime`, symulacja zbiera wszystkie
gotowe fire intents. Dopiero po zebraniu całej partii rozstrzyga je w
stabilnej kolejności `BattleSimulation.Units`.

Pozwala to zachować równoczesność końca windupu: jednostka, która była żywa
na początku tej partii, może dokończyć fire, nawet jeśli otrzyma śmiertelne
obrażenia od innego fire rozstrzyganego w tej samej partii. Śmierć zadana
wcześniej w ticku przez pocisk anuluje windup, ponieważ pociski są
rozstrzygane przed cyklem ataków.

Kolejność pojedynczego fire:

1. Emituj `AttackFired`.
2. Uruchom efekty `on-attack` i manę za wykonanie ataku.
3. Zużyj jednorazowy bonus i wylicz deterministyczny payload trafienia
   (damage, crit i w przyszłości dane efektów).
4. Melee/instant: natychmiast rozstrzygnij hit.
5. Ranged/projectile: utwórz logiczny pocisk z gotowym payloadem.
6. Zaplanuj następny termin gotowości ataku.
7. Przejdź do `Winddown`.

Efekt `on-attack` występuje dokładnie raz po prawidłowym fire, nawet jeśli
pocisk później nie trafi. Crit i efekty `on-hit` są prezentowane oraz
aplikowane dopiero przy resolution point.

### 2.4. Resolution point

Melee/instant:

- fire i resolution point są tym samym przejściem;
- damage, crit, mana celu za otrzymane obrażenia, `on-hit` i śmierć są
  rozstrzygane natychmiast w partii fire.

Ranged/projectile:

- fire tworzy `ProjectileRuntimeState`;
- pocisk istnieje niezależnie od późniejszego stanu atakującego;
- śmierć atakującego nie usuwa pocisku;
- śmierć celu nie usuwa pocisku przed zaplanowanym impactem;
- żywy cel może się poruszać, a pocisk aktualizuje jego ostatnią znaną
  pozycję;
- na impact żywego celu następują damage, crit, `on-hit`, mana za otrzymane
  obrażenia i ewentualna śmierć;
- jeśli cel już nie żyje, pocisk kończy lot w ostatniej znanej pozycji,
  wygasa bez damage i bez `on-hit`.

### 2.5. Winddown i Acquire/Reload

- `Winddown` rozpoczyna się po fire i blokuje nowy windup.
- Dla czytelności prezentacji rekomendowane MVP blokuje również rozpoczęcie
  ruchu podczas winddownu.
- Po winddownie jednostka przechodzi do `AcquireReload`.
- W `AcquireReload` może targetować i poruszać się.
- Nowy windup wymaga równocześnie zakończonego winddownu, gotowego cooldownu,
  żywego celu w zasięgu oraz braku ruchu.

`AttackCooldown` zachowuje znaczenie pełnego okresu od początku jednego
windupu do początku następnego. Chroni to dotychczasowy balans attack speedu:
windup i winddown zajmują część istniejącego okresu, a pozostała część jest
acquire/reload.

Przy rozpoczęciu windupu `AttackCycleStartTime` przejmuje zaplanowany
deadline z `NextAttackTime`, a nie aktualny `ElapsedTime`. Sam
`WindupEndTime` jest liczony od rzeczywistego ticka rozpoczęcia windupu.
Dzięki temu windup zawsze trwa wymaganą liczbę ticków, ale dotychczasowy
overshoot cooldownu nie jest tracony.

Przy fire następny termin jest liczony na bazie zaplanowanego terminu, który
uruchomił bieżący cykl:

```text
NextAttackTime = AttackCycleStartTime + EffectiveAttackCooldown
```

Kolejny windup i tak nie może zacząć się przed końcem winddownu. Oznacza to
naturalny limit haste bez nakładania animacji. Należy walidować dane i
ostrzegać, jeśli bazowy `AttackCooldown` jest krótszy niż
`WindupDuration + WinddownDuration`.

## 3. Model stanu symulacji

### `UnitDefinition`

Dodać pola danych:

```csharp
public float AttackWindupDuration;
public float AttackWinddownDuration;
```

Oba czasy muszą być nieujemne. Wartość `0` jest legalna i oznacza przejście
na najbliższym możliwym etapie bieżącego ticka, bez uzależniania logiki od
animacji.

Nie dodawać osobnego ciężkiego runtime package ani Animator-driven events.
Jeśli wiele jednostek ma identyczne timingi, współdzielenie danych można
dodać później dopiero po wykazaniu realnej potrzeby.

### `UnitRuntimeState`

Dodać:

```csharp
public UnitAttackPhase AttackPhase;
public int LockedAttackTargetUnitId;
public int AttackSequenceId;
public double AttackCycleStartTime;
public double WindupEndTime;
public double WinddownEndTime;
```

Enum:

```csharp
public enum UnitAttackPhase
{
    AcquireReload = 0,
    Windup = 1,
    Winddown = 2
}
```

`AttackSequenceId` jest inkrementowany na początku każdego windupu. Pozwala
widokowi odrzucić spóźnione anulowanie lub fire poprzedniej animacji, gdy
kilka ticków zostanie wykonanych w jednej klatce.

Reset bitwy i `DefeatUnit` muszą wyczyścić cały stan cyklu. Dla czasów
nieaktywnych użyć `double.PositiveInfinity`, zgodnie z istniejącym modelem
deadline'ów.

### `ProjectileRuntimeState`

Zastąpić odejmowane co tick `TravelTimeRemaining` absolutnym:

```csharp
public readonly double ImpactTime;
```

Pozostawić `TravelDuration` jako dane diagnostyczne/prezentacyjne. Absolutny
deadline usuwa narastający błąd float i upraszcza deterministyczne testy.
Payload pocisku nadal przechowuje wyliczony przy fire damage i crit.

W przyszłości payload można zamknąć w małym `readonly struct AttackPayload`,
ale w MVP nie należy wprowadzać list efektów ani delegatów alokowanych dla
każdego ataku.

## 4. Odpowiedzialności resolverów

### Nowy `AttackCycleResolver`

Wydzielić sterowanie fazami z obecnego `CombatResolver`. Resolver powinien:

- anulować nieważne windupy;
- zebrać kończące się windupy do prealokowanego workspace;
- wykonać fire intents;
- zakończyć winddowny;
- rozpocząć nowe windupy dla gotowych jednostek;
- emitować zdarzenia bez sterowania prezentacją.

Workspace powinien mieć tablice o pojemności liczby jednostek i być
utworzony raz w `BattleTickLoop`. W hot path nie używać LINQ, nowych list,
closure ani sortowania.

### `HitResolver`

Wyciągnąć wspólną ścieżkę trafienia z obecnych `CombatResolver` i
`ProjectileResolver`:

- crit event;
- odjęcie HP;
- `UnitDamaged`;
- mana za otrzymane obrażenia;
- efekty `on-hit`;
- oznaczenie śmierci i `UnitDied`.

Jedna ścieżka zapobiega rozjazdowi melee i projectile. Resolver powinien
przyjmować jawny kontekst trafienia i nie wyszukiwać ponownie celu dla
retargetowania.

### `ProjectileResolver`

Powinien jedynie:

- aktualizować `LastKnownTargetHex`, gdy cel żyje;
- sprawdzać `ElapsedTime >= ImpactTime`;
- wywoływać `HitResolver` dla żywego celu;
- emitować zakończenie lotu także dla pocisku, który nie trafił;
- usuwać rozstrzygnięty pocisk.

Nie przenosić efektów `on-attack` do projectile resolvera.

### `TargetSelector` i `MovementResolver`

- `RefreshTargets` nie może zmieniać `TargetUnitId` jednostki w `Windup`.
- `MovementResolver` pomija `Windup` oraz rekomendowane MVP `Winddown`.
- `AcquireReload` zachowuje obecne targetowanie i ruch.
- Start windupu następuje wyłącznie, gdy jednostka po logicznym ruchu z
  poprzedniego ticka jest już nieruchoma.

## 5. Kolejność operacji w ticku

Docelowy `BattleTickLoop.Tick`:

1. Wyczyść kolejkę zdarzeń.
2. Przesuń `ElapsedTime` dokładnie raz.
3. Zaktualizuj czasowe statusy/speciale.
4. Rozstrzygnij pociski z `ImpactTime <= ElapsedTime`.
5. Odśwież cele jednostek, które nie są zablokowane w windupie.
6. Zaktualizuj cykle ataku:
   - anulowania windupów;
   - zebranie wszystkich fire intents;
   - rozstrzygnięcie partii fire;
   - zakończenie winddownów;
   - start nowych windupów.
7. Odśwież cele po śmierciach i anulowaniach.
8. Rozstrzygnij ruch uprawnionych jednostek.
9. Sprawdź koniec bitwy.

Konsekwencje:

- pocisk trafiający na początku ticka może anulować cudzy windup kończący
  się w tym samym ticku;
- dwa windupy kończące się w tym samym ticku fire'ują równocześnie jako
  jedna partia;
- jednostka, która właśnie weszła w zasięg w kroku ruchu, zaczyna windup
  najwcześniej w następnym ticku;
- jednostka po anulowaniu może od razu retargetować lub ruszyć, ale nie
  rozpoczyna drugiego windupu w tym samym ticku;
- maksymalnie jeden fire na jednostkę na tick pozostaje żelazną regułą.

## 6. Zdarzenia symulacji

Zastąpić niejednoznaczne `UnitAttackStarted` precyzyjnymi zdarzeniami:

- `AttackWindupStarted(attackerId, targetId, sequenceId, duration)`;
- `AttackWindupCancelled(attackerId, targetId, sequenceId)`;
- `AttackFired(attackerId, targetId, sequenceId)`;
- opcjonalnie `AttackWinddownEnded`, tylko jeśli widok faktycznie go
  potrzebuje;
- `ProjectileLaunched(...)`;
- `ProjectileResolved(projectileId, targetId, targetHex, didHit)`.

`UnitDamaged`, `UnitCrit` i `UnitDied` pozostają zdarzeniami resolution
point. `ProjectileResolved` powinno zastąpić lub rozszerzyć obecne
`ProjectileHit`, ponieważ widok musi zakończyć pocisk również wtedy, gdy cel
umarł przed impactem.

Pola zdarzeń pozostają value type. Rozbudować istniejący `BattleEvent`,
zamiast tworzyć hierarchię klas i alokować obiekty per event.

## 7. Warstwa wizualna

### `BattleView`

Mapowanie zdarzeń:

- `AttackWindupStarted`:
  - ustaw facing na zamrożony cel;
  - zsynchronizuj widok z logicznym heksem, jeśli końcówka ruchu jest nadal
    wizualnie kolejkowana;
  - uruchom pre-fire o czasie podanym przez symulację;
- `AttackWindupCancelled`:
  - przerwij wyłącznie animację o zgodnym `AttackSequenceId`;
  - wróć płynnie do idle;
- `AttackFired`:
  - odtwórz release/strike i przejdź w winddown;
- `ProjectileLaunched`:
  - pobierz widok pocisku z istniejącej puli;
  - rozpocznij lot do transformu celu z fallbackiem do ostatniej pozycji;
- `ProjectileResolved`:
  - zakończ lot/impact lub miss i zwróć widok do puli.

Widok nie wywołuje metod symulacji w `OnComplete`, Animation Event ani
callbacku DOTween.

### `UnitView`

Zastąpić obecne ogólne `PlayAttack`/`PlayMeleeAttack` API metodami
prezentacyjnymi odpowiadającymi fazom, np.:

```csharp
BeginAttackWindup(int sequenceId, float duration);
PlayAttackFire(int sequenceId, float winddownDuration);
CancelAttackWindup(int sequenceId);
```

MVP może nadal używać DOTween, ale sekwencje muszą:

- być przerywalne po sequence id;
- nie alokować co klatkę;
- nie sterować logiką;
- bezpiecznie kończyć się po śmierci lub wyłączeniu obiektu.

Docelowo prefab może użyć Animatora, lecz parametry i czasy nadal będą
napędzane zdarzeniami symulacji, a nie odwrotnie.

### `ProjectileView`

Lot wizualny nie jest dowodem trafienia. Jeśli tween dotrze przed logicznym
tickiem impactu, pocisk czeka w pozycji końcowej. Jeśli przyjdzie
`ProjectileResolved`, widok odtwarza hit/miss i wraca do puli.

Do eventu launch należy przekazać czas skwantowany do ticka albo absolutny
czas impactu, aby ograniczyć wizualny rozjazd:

```text
presentationTravelDuration =
    ceil(rawTravelDuration / TickDuration) * TickDuration
```

## 8. Zakres zmian w plikach

Główne pliki runtime:

- `Assets/DeckBattle/Scripts/Data/UnitDefinition.cs`
  - windup/winddown i walidacja;
- `Assets/DeckBattle/Scripts/Battle/UnitRuntimeState.cs`
  - faza oraz deadline'y cyklu;
- `Assets/DeckBattle/Scripts/Battle/AttackCycleResolver.cs`
  - nowa maszyna stanów i fire batch;
- `Assets/DeckBattle/Scripts/Battle/HitResolver.cs`
  - wspólny resolution point;
- `Assets/DeckBattle/Scripts/Battle/CombatResolver.cs`
  - usunięcie starego natychmiastowego cyklu lub redukcja do kompatybilnego
    facade dla testów;
- `Assets/DeckBattle/Scripts/Battle/ProjectileRuntimeState.cs`
  - absolutny `ImpactTime`;
- `Assets/DeckBattle/Scripts/Battle/ProjectileResolver.cs`
  - impact przez `HitResolver`;
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`
  - nowa kolejność faz;
- `Assets/DeckBattle/Scripts/Battle/BattleEvent.cs`
  - precyzyjne eventy faz;
- `Assets/DeckBattle/Scripts/Battle/BattleView.cs`
  - playback eventów;
- `Assets/DeckBattle/Scripts/Battle/UnitView.cs`
  - wizualny windup/fire/winddown/cancel;
- `Assets/DeckBattle/Scripts/Battle/ProjectileView.cs`
  - jawne zakończenie hit/miss.

Nie przenosić ani nie zmieniać GUID istniejących assetów i prefabów.

## 9. Testy akceptacyjne

### Edit Mode — `AttackCycleResolverTests`

1. Gotowa, nieruchoma jednostka w zasięgu zaczyna windup i zamraża target.
2. Start windupu nie zadaje obrażeń, nie daje many, nie zużywa bonusu i nie
   przesuwa cooldownu.
3. Jednostka nie rusza się ani nie retargetuje podczas windupu.
4. Żywy cel, który zmieni pozycję podczas windupu, pozostaje celem fire.
5. Śmierć celu przed fire anuluje windup bez kosztów i pozwala później
   wybrać inny cel.
6. Śmierć atakującego przed fire anuluje atak.
7. Melee zadaje obrażenia dokładnie na końcu windupu.
8. Ranged tworzy pocisk na końcu windupu bez natychmiastowych obrażeń.
9. `on-attack`/mana atakującego występuje przy fire także wtedy, gdy pocisk
   później nie trafi.
10. `on-hit`, crit i mana celu występują wyłącznie przy impact żywego celu.
11. Dwa fire w tym samym ticku zachowują wzajemne trafienie.
12. Projectile z wcześniejszej fazy ticka może zabić cel i anulować jego
    kończący się windup.
13. Winddown blokuje kolejny windup i ruch.
14. Acquire/reload pozwala na targetowanie i ruch.
15. Cooldown zachowuje start-to-start cadence oraz istniejący overshoot.
16. Haste nie pozwala rozpocząć nowego cyklu przed końcem winddownu.
17. Jednostka wykonuje najwyżej jeden fire na tick.

### Edit Mode — `ProjectileResolverTests`

1. Pocisk trafia dopiero, gdy `ElapsedTime >= ImpactTime`.
2. Pocisk śledzi żywy cel, który zmienił heks.
3. Pocisk pozostaje aktywny po śmierci atakującego.
4. Pocisk pozostaje aktywny po śmierci celu i wygasa dopiero na impact.
5. Martwy cel nie otrzymuje damage ani `on-hit`.
6. Lethal impact emituje zdarzenia w kolejności:
   `ProjectileResolved -> UnitCrit? -> UnitDamaged -> UnitDied`.
7. Sekwencja wyników jest identyczna dla tego samego seeda.

### Edit Mode — `BattleTickLoopTests`

1. Pełna kolejność ticka odpowiada sekcji 5.
2. Wejście w zasięg przez ruch nie rozpoczyna windupu w tym samym ticku.
3. Bitwa nie kończy się, dopóki istnieje aktywny pocisk.
4. Ostatnie windupy są anulowane po śmierci celów i nie blokują końca bitwy.
5. `ElapsedTime` rośnie dokładnie raz na aktywny tick.
6. Kilka ticków wykonanych w jednej klatce daje ten sam stan i eventy co
   wykonanie ich osobno.

### Play Mode / manualna scena `Battle`

- ruch wizualny kończy się przed windupem;
- facing nie przeskakuje na nowy cel w trakcie windupu;
- cancel płynnie wraca do idle;
- projectile nie teleportuje się po retargetowaniu atakującego;
- damage flash i śmierć występują na logicznym impact;
- brak podwójnych animacji przy `MaxTicksPerFrame > 1`;
- pauza, background/resume nie zmienia stanu symulacji.

## 10. Kolejność implementacji

### Krok 1 — testy kontraktu i stan danych

- dodać enum, pola runtime i timingi definicji;
- rozszerzyć reset/defeat;
- napisać testy commitment, cancel i deadline'ów bez widoku.

### Krok 2 — maszyna stanów i melee

- wprowadzić `AttackCycleResolver`;
- uruchomić windup/fire/winddown dla melee;
- zachować batchową równoczesność;
- rozdzielić wspólny `HitResolver`;
- przełączyć `BattleTickLoop`.

Po tym kroku melee musi być w pełni deterministyczne bez zależności od
animacji.

### Krok 3 — projectile resolution

- przełączyć pociski na `ImpactTime`;
- tworzyć je wyłącznie przy fire;
- utrzymać lot po śmierci atakującego lub celu;
- rozstrzygać hit przez `HitResolver`;
- uzupełnić event hit/miss.

### Krok 4 — eventy i prezentacja

- podłączyć windup/cancel/fire/winddown do `BattleView` i `UnitView`;
- podłączyć jawny koniec pocisku;
- zachować pooling efektów i projectile views;
- usunąć wizualne API sugerujące, że animacja inicjuje atak.

### Krok 5 — migracja danych i regresja

- ustawić bezpieczne wartości windup/winddown na istniejących
  `UnitDefinition`;
- sprawdzić sumę timingów względem cooldownu;
- uruchomić pełne Edit Mode tests przez otwarty Unity Editor;
- wykonać manualny Play Mode smoke test sceny `Battle`;
- sprawdzić Android/mobile profile.

## 11. Weryfikacja wydajności mobilnej

Po wdrożeniu sprawdzić na walce z maksymalną planowaną liczbą jednostek:

- `GC.Alloc` na `BattleTickLoop.Tick`: oczekiwane `0 B` po rozgrzaniu;
- CPU time `AttackCycleResolver`, `ProjectileResolver` i `TargetSelector`;
- liczbę aktywnych i pulowanych `ProjectileView`;
- brak wzrostu liczby DOTween sequences po zakończonych/anulowanych atakach;
- stabilność frame time przy kilku tickach nadrabianych w jednej klatce;
- overdraw efektów fire/hit oraz koszt materiałów URP.

Nie dodawać per-unit `Update` do logiki symulacji. Istniejący `UnitView.Update`
może pozostać wyłącznie warstwą prezentacji, ale nie powinien wykonywać
targetowania ani sprawdzać faz logicznych.

## 12. Definition of Done

- target jest zamrażany dokładnie na początku windupu;
- jednostka logicznie nie porusza się podczas windupu;
- śmierć celu przed fire anuluje przygotowywany atak;
- fire uruchamia `on-attack` niezależnie od późniejszego trafienia;
- melee rozstrzyga hit przy fire;
- ranged rozstrzyga hit wyłącznie na impact;
- już wystrzelony pocisk kontynuuje lot po śmierci celu lub atakującego;
- `on-hit` i damage nie występują dla martwego celu;
- tick jest jedynym źródłem prawdy, a callbacki animacji nie zmieniają
  symulacji;
- zachowana jest deterministyczna kolejność i start-to-start attack cadence;
- hot path nie wprowadza alokacji per tick;
- wszystkie nowe i istniejące testy Edit Mode przechodzą;
- scena `Battle` przechodzi Play Mode smoke test bez błędów kompilacji i
  bez rozjazdu animacji z eventami.
