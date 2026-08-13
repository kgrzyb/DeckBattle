# Plan: special `Longshot` dla Juni

## 1. Cel i ustalone zachowanie

Dodać ofensywny special `Longshot` i przypisać go Juni.

Przebieg speciala:

1. po zebraniu pełnej many Juni rozpoczyna istniejący windup speciala;
2. animacja `root|Special` pokazuje podskok i przygotowanie broni;
3. w chwili rozpoczęcia speciala symulacja wybiera żywego, targetowalnego
   przeciwnika z najmniejszą bieżącą liczbą HP i blokuje ten cel na całą akcję;
4. w logicznym momencie strzału Juni wystrzeliwuje jeden pocisk do wcześniej
   zablokowanego celu;
5. pocisk zadaje przy trafieniu `150% Attack Damage`;
6. jeżeli bezpośrednio przed rozstrzygnięciem trafienia cel ma mniej niż `20%`
   maksymalnego HP, zostaje zabity przez execute zamiast otrzymać zwykłe
   obrażenia;
7. Juni kończy pozostałą część animacji i wraca do zwykłego cyklu walki.

### 1.1. Precyzyjna semantyka

- „najmniejsze HP” oznacza najmniejszą bezwzględną wartość `CurrentHp`, a nie
  najmniejszy procent HP;
- remis rozstrzyga najniższy `UnitId`, aby wynik był deterministyczny;
- wybór i blokada celu następują w ticku rozpoczęcia windupu
  (`SpecialWindupStarted`), czyli w chwili odpalenia speciala;
- Longshot jest globalny: ignoruje zwykły `AttackRange`, aktualny cel i taunt;
- kandydat musi przejść `TargetingRules.CanBeTargeted`, więc martwe jednostki,
  sojusznicy i jednostki `Untargetable` są pomijane;
- po zablokowaniu celu nie następuje ponowna selekcja ani retargetowanie — ani
  przed strzałem, ani podczas lotu;
- śmierć zablokowanego celu nie anuluje windupu i nie zatrzymuje wystrzału.
  Pocisk leci do jego ostatniej znanej pozycji, a na impact kończy się jako miss
  bez damage, execute, statusów i drugiego eventu śmierci;
- próg execute jest ścisły: `CurrentHp < 20% MaxHp`; dokładnie `20%` nie
  uruchamia execute;
- próg jest sprawdzany przed zwykłymi obrażeniami Longshota. Trafienie, które
  dopiero obniży HP z co najmniej `20%` poniżej progu, nie staje się execute;
- próg używa czerwonego HP i nie uwzględnia wartości tarczy;
- execute omija shield i zabija za wartość aktualnego HP, ale `Invulnerable`
  nadal chroni cel. Jest to proponowana zgodność z istniejącym pipeline'em
  obrony;
- normalne obrażenia speciala nadal respektują armor, armor penetration,
  outgoing damage modifiers, exposed, shield i invulnerability;
- Longshot nie może krytykować, nie jest przejmowany przez guard i nie uruchamia
  mark ani lifesteal, tak jak obecne obrażenia `DamageKind.Special`.

## 2. Stan projektu istotny dla wdrożenia

Projekt ma już większość potrzebnej infrastruktury:

- `MegaArrow` pokazuje wzorzec targetowanego speciala wystrzeliwującego logiczny
  pocisk;
- `ProjectileRuntimeState` przechowuje gotowy damage i immutable impact payload;
- `ProjectileResolver` rozstrzyga trafienie dopiero po czasie lotu;
- `BattleProjectilePresenter` śledzi poruszający się cel i używa puli widoków;
- `DamageResolver` centralizuje shield, invulnerability, damage, manę i śmierć;
- `UnitView` uruchamia stan `Special` na początku windupu;
- `PF_UnitView_Juni` ma `ProjectileLaunchAnchor`, a root motion jest wyłączony.

Aktualny stan danych Juni:

- `Juni.asset` nadal wskazuje na `Special_HasteBurst`;
- `root|Special` z `Juni.fbx` trwa `1,65 s`, ma `60 FPS`, nie zapętla się i nie
  zawiera Animation Eventów;
- `Juni.overrideController` już mapuje wspólny stan `Special` na ten klip;
- `Sniper_Bullet.asset` jest już używany przez zwykły atak Juni i może zostać
  wykorzystany również przez Longshot;
- `Sniper_Bullet.asset` ma obecnie omyłkowe `ProjectileId: bolt`, takie samo jak
  `Bolt.asset`. Należy nadać mu unikalne `ProjectileId: sniper_bullet`, aby nie
  tworzyć kolizji presentation ID.

## 3. Dane i immutable combat spec

### 3.1. Nowy rodzaj speciala

Rozszerzyć `UnitSpecialKind` w `UnitDefinition.cs`:

```csharp
Longshot = 5
```

Nie zmieniać istniejących wartości enum, ponieważ są zapisane w assetach Unity.

### 3.2. Konfigurowalny próg execute

Rozszerzyć `UnitSpecialDefinition` o pole:

```csharp
[Range(0, 100)] public int ExecuteHpThresholdPercent;
```

Użyć liczby całkowitej zamiast `float`, aby warunek mógł być porównywany bez
błędu zaokrąglenia:

```text
(long)CurrentHp * 100 < (long)MaxHp * ExecuteHpThresholdPercent
```

Pole przenieść do `UnitSpecialCombatSpec`, aby symulacja nie czytała
`ScriptableObject` w trakcie walki. `OnValidate` ogranicza wartość do `0..100`.

Walidacja `UnitSpecialCombatSpec.IsValid` dla `Longshot` wymaga:

- poprawnego `ProjectileCombatSpec`;
- `AttackDamageMultiplier > 0`;
- `ExecuteHpThresholdPercent > 0 && < 100`;
- `CastDuration >= WindupDuration`;
- brak wymaganego statusu.

### 3.3. Impact payload pocisku

Rozszerzyć `ProjectileImpactCombatSpec` o
`ExecuteHpThresholdPercent`. Wartość `0` oznacza zwykły pocisk i zachowuje
dotychczasowe zachowanie wszystkich ataków oraz `MegaArrow`.

`ProjectileRuntimeState` nadal przechowuje:

- damage wyliczony w chwili strzału;
- wybrany `TargetUnitId`;
- impact spec zawierający rodzaj obrażeń i próg execute.

Nie przechowywać referencji do `UnitSpecialDefinition` ani delegatów w pocisku.

## 4. Wybór celu i cykl speciala

### 4.1. Zablokowany cel i jego żywotność

Longshot należy do speciali blokujących cel przy rozpoczęciu windupu, ale różni
się od `FurySwipes` i `MegaArrow` zachowaniem po śmierci celu:

- `FurySwipes` i `MegaArrow` nadal wymagają żywego celu podczas windupu zgodnie
  z obecną logiką;
- `Longshot` wymaga żywego, targetowalnego celu tylko przy rozpoczęciu speciala;
- po zapisaniu `LockedSpecialTargetUnitId` wystarcza, że jednostka celu nadal
  istnieje w symulacji — nie musi już być żywa ani targetowalna;
- zwykły refresh celu pozostaje zablokowany podczas `Windup` i `Casting`, aby
  prezentacja nie obróciła Juni do innego przeciwnika;
- utrata celu nie może uruchomić ponownej selekcji.

Rozdzielić helper pobierający żywy zablokowany cel od helpera pobierającego samą
tożsamość zablokowanego celu. Dostosować użycia w `UnitActionRules`,
`BattleTickLoop` i `SpecialCycleResolver`, nie zmieniając semantyki istniejących
speciali.

Konkretnie:

- `StartReadyWindups` dla `Longshot` wywołuje dedykowany selektor i zapisuje
  wynik tak samo, jak inne targetowane speciale zapisują locked target;
- warunek anulowania aktywnego windupu z powodu `!TryGetLockedLiveTarget`
  obowiązuje nadal dla `FurySwipes` i `MegaArrow`, ale nie dla `Longshot`;
- `BeginCast` Longshota używa helpera dopuszczającego pokonaną jednostkę;
- `BattleTickLoop.RefreshTargets` nie odświeża zwykłego celu Juni podczas
  aktywnego Longshota.

### 4.2. Selektor celu Longshota

Dodać mały, bezalokacyjny helper wywoływany przy starcie windupu, najlepiej obok
logiki speciala, np.
`LongshotTargetSelector.TrySelect` albo prywatny helper
`SpecialCycleResolver.TrySelectLongshotTarget`.

Jeden liniowy przebieg po `simulation.Units`:

```text
candidate != null
TargetingRules.CanBeTargeted(attacker, candidate)
lepszy, gdy candidate.CurrentHp < selected.CurrentHp
remis: candidate.UnitId < selected.UnitId
```

Nie używać `TargetSelector`, ponieważ jego reguły preferują drogę, dystans,
zasięg, aktualny cel i taunt, czyli nie odpowiadają Longshotowi. Nie używać LINQ,
fizyki ani tymczasowej listy.

### 4.3. Start i commitment

- Windup może rozpocząć się tylko wtedy, gdy selektor znajdzie co najmniej
  jednego żywego, targetowalnego przeciwnika. Dla Longshota ten warunek zastępuje
  obecny wymóg zwykłego `TargetUnitId` w zasięgu.
- Przy starcie zapisać jego `UnitId` w `LockedSpecialTargetUnitId` i pozycję w
  evencie `SpecialWindupStarted`.
- Zmiany HP innych przeciwników podczas podskoku nie zmieniają już wyboru.
- Śmierć lub uzyskanie `Untargetable` przez zablokowany cel po starcie nie
  anuluje speciala Juni.
- Standardowe przerwania po stronie Juni — jej śmierć, stun, sleep lub silence —
  nadal anulują windup przed strzałem bez wydania many.
- W `BeginCast` pobrać dokładnie zablokowaną jednostkę po ID bez warunku
  `IsAlive`; nie uruchamiać selektora ponownie.
- Przejście do `Casting`, wyzerowanie many i cooldown/recovery pozostają w
  logicznym momencie strzału.
- Po wystrzale śmierć Juni nie usuwa istniejącego pocisku; zachować obecną regułę
  pocisków z pokonanych jednostek.

### 4.4. Wystrzał

Dodać w `SpecialCycleResolver.BeginCast` gałąź `Longshot`, analogiczną
strukturalnie do `MegaArrow`, lecz używającą celu zablokowanego na początku
windupu:

1. obliczyć damage przez `DamageCalculator.CalculateSpecialDamage` z
   `AttackDamageMultiplier = 1.5`;
2. utworzyć `ProjectileImpactCombatSpec` z `DamageKind.Special` i progiem execute;
3. wywołać `simulation.SpawnProjectile`;
4. wyemitować kolejno `SpecialCastStarted`, `SpecialStrikeFired` i
   `ProjectileLaunched` z właściwym celem oraz sequence ID;
5. ustawić koniec castu względem początku całej animacji, tak jak dla
   `MegaArrow`, a nie dodać pełnego `CastDuration` drugi raz po strzale.

`BattleSimulation.SpawnProjectile` może przyjąć pokonaną jednostkę, ponieważ
potrzebuje jej ID i aktualnego/ostatniego heksa. `ProjectileRuntimeState`
zachowuje `TargetUnitId` oraz `LastKnownTargetHex`. Na impact istniejący
`ProjectileResolver` rozpozna `targetAlive == false`, wyemituje
`ProjectileResolved(..., didHit: false)` i usunie pocisk bez damage.

Dla bazowego `Attack = 150` Juni wartość przed defensywą celu wynosi `225`.
Damage pozostaje snapshotem z chwili strzału, zgodnie z obecnym modelem pocisków;
tylko warunek execute korzysta z aktualnego HP na impact.

W `CompleteCast` traktować `Longshot` jak pozostałe ofensywne speciale:
wyemitować `UnitSpecialActivated` raz i wejść do `RecoveryLock` bez ponownego
naliczania efektu.

### 4.5. Zakończenie bitwy przy martwym celu

Obecny `BattleTickLoop.TryEndBattle` opóźnia koniec walki dla aktywnych
pocisków, ale nie dla trwającego windupu. To wymaga rozszerzenia, ponieważ
zablokowany cel może być ostatnim przeciwnikiem i zginąć jeszcze przed klatką
strzału.

Dopóki żywa Juni ma aktywny `Longshot` w fazie `Windup`, traktować go jako
oczekujące rozstrzygnięcie i nie kończyć bitwy. Dzięki temu kolejny tick może
utworzyć pocisk do martwego celu. Od chwili wystrzału istniejący warunek
`simulation.Projectiles.Count > 0` przejmuje blokadę końca walki. Po missie
pocisku walka może zakończyć się normalnie.

Helper sprawdzający pending resolution ma wykonywać prosty przebieg po
jednostkach, bez alokacji, i nie może podtrzymywać bitwy po anulowaniu windupu
albo śmierci Juni.

## 5. Execute w `DamageResolver`

Nie zabijać celu bezpośrednio w `ProjectileResolver`, ponieważ ominęłoby to
wspólną obsługę eventów, many, anulowania speciala celu i śmierci.

Rozszerzyć `DamageRequest` o opcjonalny `ExecuteHpThresholdPercent = 0` i
rozstrzygać execute w `DamageResolver`:

1. zachować wczesny check martwego celu;
2. zachować `Invulnerable` jako ochronę przed całym trafieniem;
3. przed exposed i absorpcją tarcz sprawdzić ścisły próg HP;
4. gdy próg jest spełniony, ustawić faktyczny damage na `target.CurrentHp` i
   ominąć `AbsorbShields`;
5. przejść dalej wspólną ścieżką `UnitDamaged -> mana pulse -> wake ->
   DefeatUnit -> CancelWindup -> UnitDied`;
6. gdy próg nie jest spełniony, wykonać niezmienioną zwykłą ścieżkę damage.

`ProjectileResolver` przekazuje próg z impact spec do `DamageRequest`. Inne
pociski przekazują `0`, więc nie mogą wykonać execute.

Na potrzeby MVP nie jest wymagany nowy `BattleEventType`: istniejące
`ProjectileResolved`, `UnitDamaged` i `UnitDied` zapewniają poprawny impact VFX,
tekst obrażeń oraz animację śmierci. Osobny napis lub VFX `EXECUTE` można dodać
później jako niezależny polish bez wpływu na reguły walki.

## 6. Animacja i prezentacja

### 6.1. Synchronizacja klipu

W Unity Editorze otworzyć `Juni/root|Special` w Animation Preview i znaleźć
dokładną klatkę, w której broń oddaje strzał.

Konfiguracja czasów:

- `WindupDuration` = czas od klatki `0` do klatki wystrzału;
- `CastDuration` = całkowity czas animacji, obecnie `1,65 s`;
- koniec castu nie może nastąpić przed windupem;
- po preview zaokrąglić logiczny moment do pierwszego ticka spełniającego
  `ElapsedTime >= SpecialWindupEndTime`.

Nie wpisywać na stałe niepotwierdzonego momentu strzału do planu. Sprawdzić
klatkę w podglądzie oraz w bitwie przy `1x`, `2x` i maksymalnej wspieranej
prędkości.

### 6.2. `UnitView`

Istniejący `BeginSpecialWindup` uruchomi klip `Special`. Zmienić
`BeginSpecialCast`, aby dla `Longshot`, podobnie jak dla `MegaArrow`, nie
triggerował `Special` drugi raz w momencie strzału. Klip ma płynnie przejść od
podskoku przez strzał do recovery.

`SpecialCastStarted` ustawia kierunek na wybrany cel przed obsługą
`ProjectileLaunched`. Pocisk startuje z istniejącego `ProjectileLaunchAnchor`
Juni, a `BattleProjectilePresenter` nadal śledzi transform celu.

Nie dodawać proceduralnego tweena skoku, nowego `Update` ani root motion. Ruch
ciała pozostaje w klipie, a logiczna pozycja Juni na heksie nie zmienia się.

### 6.3. Animation Event

Gameplay nie może zależeć od Animation Eventu. Obecny klip nie ma eventów i nie
trzeba ich dodawać, ponieważ `ProjectileLaunched` uruchamia także launch VFX w
logicznym momencie strzału.

Jeżeli po dostrojeniu timera potrzebny będzie dodatkowy kosmetyczny sygnał z
dokładnej klatki, można dodać `SpecialContact`, ale nie może on wybierać celu,
tworzyć pocisku, zmieniać HP ani uruchamiać drugiego muzzle flasha.

## 7. Assety i opis karty

Utworzyć `Assets/DeckBattle/Data/Specials/Special_Longshot.asset`:

```text
SpecialId: longshot
Kind: Longshot
WindupDuration: czas potwierdzonej klatki strzału
CastDuration: 1.65
AppliedStatus: null
Projectile: Sniper_Bullet
StrikeCount: 1
AttackDamageMultiplier: 1.5
EffectRadius: 0
ExecuteHpThresholdPercent: 20
```

Przypisać asset do `Assets/DeckBattle/Data/Units/Juni.asset`, zastępując wyłącznie
referencję Juni do `Special_HasteBurst`. Pozostałe jednostki korzystające z
HasteBurst pozostają bez zmian.

Rozszerzyć `CardDescriptionTemplateFormatter` o token:

```text
{executeHpThresholdPercent}
```

Przykładowy template zgodny z obecnym angielskim UI:

```text
Jump and fire at the enemy with the lowest HP, dealing {attackDamagePercent}
Attack Damage. Execute the target if it is below {executeHpThresholdPercent} HP
on impact.
```

Formatter powinien wyświetlić `150%` i `20%`. `{totalDamage}` może opcjonalnie
pokazać bazowe `225`, ale procent lepiej komunikuje skalowanie z runtime Attack.

## 8. Testy

### 8.1. `SpecialCycleResolverTests`

Dodać przypadki:

- Longshot wybiera i blokuje cel dokładnie na początku windupu;
- zmiana HP dwóch wrogów podczas windupu nie zmienia zablokowanego celu;
- selekcja porównuje bezwzględne HP, nie procent HP;
- remis HP wybiera najniższy `UnitId`;
- selektor pomija sojusznika, martwego i `Untargetable`;
- globalny cel jest wybierany poza zwykłym zasięgiem Juni;
- taunt i aktualny zwykły target nie nadpisują reguły najniższego HP;
- brak kandydata nie rozpoczyna windupu;
- śmierć zablokowanego celu podczas windupu nie anuluje speciala i nie wybiera
  kolejnego przeciwnika;
- pocisk zostaje wystrzelony do martwego celu i zachowuje jego ostatni heks;
- strzał zeruje manę dokładnie raz i tworzy dokładnie jeden pocisk;
- eventy strzału zawierają ID faktycznie wybranego celu;
- `CastDuration` mierzy całą animację od początku windupu;
- stun, sleep, silence i śmierć Juni przed strzałem anulują special bez pocisku;
- po wystrzale śmierć Juni nie usuwa pocisku i nie cofa wydanej many;
- śmierć ostatniego przeciwnika podczas windupu nie kończy bitwy przed
  wystrzałem; bitwa kończy się dopiero po missie pocisku.

### 8.2. `ProjectileResolverTests` i `DamageResolverTests`

- damage nie występuje przy launchu, tylko przy impact;
- cel na co najmniej `20%` otrzymuje zwykłe `150% Attack Damage`;
- cel dokładnie na `20%` nie jest execute;
- cel poniżej `20%` umiera niezależnie od tego, czy zwykłe 150% byłoby lethal;
- cel powyżej progu, którego trafienie obniża poniżej `20%`, nie jest execute;
- heal lub wcześniejsze obrażenia podczas lotu zmieniają wynik checku execute;
- execute używa `CurrentHp / MaxHp`, a nie HP zapamiętanego przy wystrzale;
- execute omija shield, pozostawiając spójne `UnitDamaged` i `UnitDied`;
- invulnerability blokuje execute;
- zwykła ścieżka nadal respektuje armor, exposed i shield;
- cel zabity przed impactem nie otrzymuje damage ani drugiego `UnitDied`;
- pocisk wystrzelony do celu martwego już przed launch emituje
  `ProjectileResolved(..., didHit: false)` i nie emituje `ProjectileHit`;
- eventy mają kolejność `ProjectileResolved -> ProjectileHit -> UnitDamaged ->
  UnitDied` dla udanego execute;
- istniejące pociski z progiem `0` nie mogą wykonać execute.

Przypadki graniczne progu konstruować tak, aby zwykłe `225` damage nie zacierało
różnicy między normalnym trafieniem i execute, np. przez wysokie `MaxHp` celu.

### 8.3. Dane, UI i prezentacja

- `UnitSpecialCombatSpec` snapshotuje próg execute;
- `Longshot` jest valid bez statusu, ale invalid bez pocisku lub poprawnego progu;
- formatter rozpoznaje i formatuje `{executeHpThresholdPercent}`;
- wszystkie template'y speciali pozostają poprawne;
- `Juni.asset` wskazuje na `Special_Longshot`;
- `Sniper_Bullet` ma unikalny `ProjectileId`;
- test `UnitView` potwierdza jeden trigger `Special` na cały Longshot i brak
  restartu klipu przy `SpecialCastStarted`;
- test prezentera potwierdza użycie `ProjectileLaunchAnchor`.

Regresja:

- `SpecialCycleResolverTests` dla `HasteBurst`, `FurySwipes`, `Slam` i
  `MegaArrow`;
- `ProjectileResolverTests`;
- `DamageResolverTests` i `DamageCalculatorTests`;
- `BattleTickLoopTests`;
- `UnitAnimatorControllerTests`;
- testy lookupu presentation ID i prefabów jednostek.

Testy Edit Mode uruchomić przez Unity MCP w otwartym Editorze, nigdy przez
batchmode. Następnie wykonać Play Mode smoke test sceny `Battle`.

## 9. Wydajność mobilna

- sprawdzenie kandydata i wybór celu używają wyłącznie krótkich liniowych
  przebiegów podczas oceny/startu speciala, bez LINQ i alokacji;
- w ticku strzału następuje tylko lookup zablokowanego `UnitId`, bez ponownego
  skanowania kandydatów;
- check pending Longshot przy potencjalnym końcu bitwy jest liniowy i wykonuje
  się tylko w ścieżce rozstrzygania końca walki;
- warunek execute używa arytmetyki całkowitej `long`, bez dzielenia i błędów
  float;
- payload pocisku rozszerza się tylko o jedno pole typu `int`;
- widok pocisku i VFX korzystają z istniejących pul;
- brak nowych `Update`, coroutine, tweenów, colliderów i zapytań fizycznych;
- brak zmian w URP, shaderach, teksturach i post-processingu;
- po rozgrzaniu Longshot nie powinien generować `GC.Alloc` w ticku strzału ani
  impactu.

W Profilerze sprawdzić `DeckBattle.BattleTickLoop.Tick`,
`DeckBattle.Damage.Resolve`, liczbę aktywnych projectile views i `GC.Alloc` przy
kilku jednoczesnych Longshotach.

## 10. Kolejność implementacji

1. Dodać `Longshot`, pole progu execute, snapshot i walidację danych.
2. Rozdzielić reguły locked-target od blokady refreshu aktywnego speciala.
3. Dodać deterministyczny selektor najniższego HP przy starcie windupu, obsługę
   martwego zablokowanego celu i gałąź Longshota w `SpecialCycleResolver`.
4. Przenieść próg przez impact payload i rozszerzyć wspólny `DamageResolver` o
   execute.
5. Zabezpieczyć zakończenie bitwy do czasu wystrzelenia pending Longshota.
6. Utworzyć `Special_Longshot.asset`, poprawić ID `Sniper_Bullet` i przypisać
   special Juni.
7. Dodać token opisu oraz testy danych i UI.
8. Dostosować `UnitView`, aby nie restartował klipu w ticku strzału.
9. Uruchomić wąskie testy Edit Mode przez Unity MCP i pełną regresję istotnych
   resolverów.
10. W Animation Preview ustalić klatkę strzału, wpisać `WindupDuration` i
   zweryfikować animację przy wszystkich prędkościach walki.
11. Wykonać Play Mode smoke test oraz sprawdzić alokacje i frame time.

## 11. Definition of Done

- Juni przy pełnej manie odtwarza jeden płynny klip podskoku, strzału i recovery;
- przy rozpoczęciu speciala wybierany i blokowany jest żywy, targetowalny wróg z
  najmniejszym bieżącym HP, niezależnie od odległości;
- remis celu jest deterministyczny;
- późniejsze zmiany HP i śmierć celu nie powodują retargetowania ani anulowania
  wystrzału;
- powstaje dokładnie jeden pooled projectile z launch anchora Juni;
- damage i execute następują wyłącznie na logicznym impact;
- zwykłe trafienie zadaje `150% Attack Damage`, czyli bazowo `225` dla aktualnych
  danych Juni przed defensywą celu;
- cel poniżej, ale nie równy `20%`, zostaje execute według ustalonej interakcji z
  shield i invulnerability;
- zmiany HP podczas lotu wpływają na execute, ale nie przeliczają snapshotu
  zwykłego damage;
- martwy cel powoduje dolot pocisku i miss bez retargetowania, damage ani
  execute;
- gameplay działa identycznie bez Animatora i nie zależy od Animation Eventu;
- root motion pozostaje wyłączony, a Juni nie zmienia logicznego heksa;
- istniejące speciale i pociski nie mają regresji;
- testy przechodzą, a hot path nie generuje nowych alokacji.
