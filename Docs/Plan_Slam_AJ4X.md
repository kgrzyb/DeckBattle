# Plan: special `Slam` dla AJ-4X

## 1. Cel i ustalone zachowanie

Dodać ofensywny special `Slam` i przypisać go wyłącznie jednostce AJ-4X.

Przebieg:

1. AJ-4X rozpoczyna istniejący, deterministyczny windup speciala;
2. animacja `root|Special` pokazuje podskok, opadanie i uderzenie w ziemię;
3. w logicznym momencie kontaktu z ziemią symulacja zadaje każdej żywej
   jednostce przeciwnika w odległości `<= 1` heksa od AJ-4X obrażenia równe
   `100% attack damage`;
4. pozostała część klipu jest krótkim recovery, po którym jednostka wraca do
   zwykłego cyklu walki.

Założenia planu:

- środek obszaru to aktualny logiczny `CurrentHex` AJ-4X w ticku impaktu;
- promień `1` obejmuje sześć sąsiednich heksów (oraz heks centralny, na którym
  inna jednostka i tak nie może stać);
- special nie wymaga wybranego celu i może zostać użyty bez przeciwnika w
  zasięgu; w takiej sytuacji zużywa manę i nie zadaje obrażeń;
- untargetable nie chroni przed obrażeniami obszarowymi, natomiast armor,
  penetration, outgoing damage modifiers, exposed, shield i invulnerability
  działają przez istniejący pipeline obrażeń;
- `Slam` nie może krytykować, nie generuje `ManaPerAttack` i używa
  `DamageKind.Special`, zgodnie z `FurySwipes`;
- obrażenia są rozstrzygane przez symulację, nigdy przez `Animation Event`.

## 2. Stan istniejących assetów

AJ-4X ma już:

- `Assets/DeckBattle/Art/Meshes/AJ4X/AJ4X.fbx` z klipem `root|Special`;
- klip speciala o długości około `1.317 s` przy `60 fps`;
- `AJ-4X.overrideController` z przypiętym `root|Special`;
- prefab `PF_UnitView_AJ-4X` z Animatorem i wyłączonym root motion;
- obsługę wspólnego stanu `Special` w `UnitView`.

Klip nie zawiera obecnie Animation Eventów. Analiza krzywej pionowej wskazuje
kontakt/kompresję w okolicy `0.85 s`, ale tę klatkę trzeba potwierdzić wizualnie
w Animation Preview. AJ-4X ma obecnie przypisany `Special_HasteBurst`; wdrożenie
zastąpi to przypięcie nowym assetem `Special_Slam` bez zmiany HasteBurst dla
pozostałych jednostek.

## 3. Dane speciala

### 3.1. Typ i konfiguracja

Rozszerzyć `UnitSpecialKind`:

```csharp
Slam = 3
```

Rozszerzyć `UnitSpecialDefinition` o pole ogólnego zastosowania:

```csharp
[Min(0)] public int EffectRadius;
```

Pole przenieść również do immutable `UnitSpecialCombatSpec`, aby symulacja nie
czytała `ScriptableObject` w czasie walki. `OnValidate` ogranicza promień do
wartości nieujemnej. Walidacja `UnitSpecialCombatSpec.IsValid` dla `Slam`
wymaga:

- `AttackDamageMultiplier > 0`;
- `EffectRadius >= 0`;
- `AppliedStatus` nie jest wymagany;
- `StrikeCount` nie ma znaczenia i w assetcie pozostaje `1`.

Nie hardkodować promienia ani mnożnika w `SpecialCycleResolver` — resolver
rozpoznaje zachowanie po `Kind`, ale wartości balansowe bierze ze snapshotu.

### 3.2. Asset i przypięcie do AJ-4X

Utworzyć `Assets/DeckBattle/Data/Specials/Special_Slam.asset`:

```text
SpecialId: slam
Kind: Slam
WindupDuration: ~0.85
CastDuration: ~0.47
StrikeCount: 1
AttackDamageMultiplier: 1.0
EffectRadius: 1
AppliedStatus: null
```

Znaczenie czasów dla Slam:

- `WindupDuration` to czas od startu animacji do kontaktu z ziemią;
- przy przejściu `Windup -> Casting` następuje impakt i obrażenia;
- `CastDuration` to część klipu po impakcie, utrzymująca blokadę akcji do końca
  recovery.

Wartości `0.85/0.47` są punktem startowym wynikającym z obecnego klipu, nie
ostatecznym balansem. Po preview należy dopasować je do najbliższego ticka tak,
aby damage text i VFX pojawiały się przy kontakcie, a powrót do idle nie ucinał
klipu.

W `AJ-4X.asset` zastąpić referencję `Special_HasteBurst` referencją do
`Special_Slam`. Przykładowy opis:

```text
On impact, deal {attackDamagePercent} Attack Damage to all enemy units within
a {effectRadius}-hex radius.
```

Rozszerzyć `CardDescriptionTemplateFormatter` o tokeny
`attackDamagePercent` i `effectRadius`, wraz z walidacją i testami. Dla tej
konfiguracji opis pokazuje `100%` i `1`, a nie wartość wpisaną na sztywno.

## 4. Deterministyczna logika w `SpecialCycleResolver`

### 4.1. Start speciala

`Slam` korzysta z istniejących reguł pełnej many, windupu, anulowania i
recovery. Nie dodawać wymogu celu ani przeciwnika w promieniu do
`CanStartSpecialWindup`; specjal rezerwuje akcję tak jak HasteBurst.

Podczas `Windup` i `Casting` AJ-4X nie porusza się ani nie atakuje. Stun, sleep,
silence lub śmierć przed impaktem anulują special zgodnie z istniejącymi
regułami:

- przed przejściem do `Casting` mana pozostaje;
- po rozpoczęciu `Casting` mana jest już wydana i nie wraca.

### 4.2. Impakt

W `BeginCast` dodać gałąź dla `Slam`:

1. przełączyć jednostkę do `Casting` i wyzerować manę dokładnie raz;
2. ustawić deadline końca recovery z `CastDuration`;
3. dodać caster do prealokowanego workspace impaktów;
4. po zebraniu wszystkich akcji kończących windup w tym ticku rozstrzygnąć
   wszystkie impakty.

Zbieranie przed zadaniem obrażeń zachowuje symultaniczność: dwa wrogie Slamy
kończące windup w tym samym ticku oba dochodzą do skutku, nawet jeśli pierwszy
rozstrzygnięty impakt zabije drugiego castera.

Dla każdego zebranego castera przejść po `simulation.Units` w stabilnej
kolejności i wybrać jednostki spełniające:

```text
candidate != null
candidate.IsAlive
candidate.Side != caster.Side
Board.Distance(caster.CurrentHex, candidate.CurrentHex) <= EffectRadius
```

Nie używać fizyki, colliderów, zapytań sceny, LINQ ani tymczasowych list.
Workspace powinien przechowywać maksymalnie jeden intent Slam na jednostkę i
być zaalokowany razem z istniejącym `SpecialCycleResolver.Workspace`.

Dla każdego trafionego celu:

```csharp
int damage = DamageCalculator.CalculateSpecialDamage(
    caster,
    target,
    special.AttackDamageMultiplier,
    simulation.Tuning);

DamageResolver.Resolve(
    simulation,
    target,
    new DamageRequest(caster, damage, DamageKind.Special, false),
    eventQueue);
```

Przy obecnym bazowym `Attack = 90` AJ-4X zadaje nieopancerzonemu celowi bazowo
`90` obrażeń, przed wpływem runtime modifiers. Każdy cel przechodzi osobno
przez pełny istniejący pipeline damage/status/death.

### 4.3. Koniec i przerwania po impakcie

Po impakcie special pozostaje w `Casting` do końca `CastDuration`, a następnie:

- emituje `UnitSpecialActivated` raz;
- przechodzi do istniejącego `RecoveryLock`;
- restartuje zwykły cooldown ataku przez
  `AttackCycleResolver.RestartCooldownAfterSpecial`.

Jeżeli kontrola tłumu lub śmierć przerwie pozostałą część animacji już po
impakcie, zadane obrażenia nie są cofane i nie są naliczane drugi raz. Należy
zachować istniejącą semantykę anulowania castu: brak zwrotu many i rozpoczęcie
zwykłego cooldownu.

## 5. Eventy i prezentacja

Dodać ogólny event symulacji:

```text
SpecialAreaImpact(unitId, kind, sequenceId, centerHex, radius)
```

Event jest emitowany raz na Slam, tuż przed eventami `UnitDamaged`. Pole `To`
przechowuje środek, a `Amount` promień. Nie emitować osobnego eventu impaktu na
każdy cel — `UnitDamaged` już obsługuje damage flash, floating text i śmierć.

Integracja prezentacji:

- `SpecialWindupStarted` uruchamia istniejący stan `Special` od początku klipu;
- dla `Slam` nie uruchamiać klipu ponownie na przejściu do `Casting`;
- `SpecialAreaImpact` uruchamia jednorazowy VFX/SFX przy heksie AJ-4X;
- `UnitSpecialActivated` kończy sekwencję i pozwala wrócić do idle;
- sequence id chroni przed spóźnionym eventem poprzedniego użycia.

Rozszerzyć `BattleView`, `BattleUnitPresenter` i `BattleVfxPresenter` o obsługę
nowego eventu. Dodać cue `SpecialAreaImpact` lub zmapować event na dedykowany
cue `SlamImpact`; preferowany jest ogólny cue, aby wykorzystać go dla kolejnych
obszarowych speciali. Efekt powinien używać istniejącego poolingu VFX.

Opcjonalny `Animation Event` `SpecialContact` można dodać na potwierdzonej
klatce kontaktu wyłącznie dla dodatkowego kosmetycznego sygnału na modelu.
Nie może on wywoływać resolvera, zmieniać HP ani decydować, kto został trafiony.
Jeśli VFX z eventu symulacji daje wystarczającą synchronizację, nie dodawać
drugiego źródła VFX, aby uniknąć podwójnego efektu.

## 6. Animacja AJ-4X

W Unity Editorze:

1. otworzyć `root|Special` w Animation Preview i wskazać dokładną klatkę, w
   której stopy/ciało kończą opadanie i następuje kontakt;
2. sprawdzić cały klip przy prędkościach walki `1x`, `2x` i maksymalnej
   obsługiwanej;
3. dopasować `WindupDuration` do kontaktu i `CastDuration` do końca klipu;
4. pozostawić `Animator.applyRootMotion = false`;
5. potwierdzić, że model nie zmienia logicznego ani nadrzędnego world position;
6. sprawdzić anulowanie przed skokiem, w powietrzu i po impakcie.

Nie dodawać proceduralnego tweena skoku ani osobnego `Update`. Ruch ciała jest
już zapisany w klipie, a pozycja `UnitView` na planszy pozostaje kontrolowana
przez prezentację heksową.

## 7. Testy Edit Mode

Rozszerzyć `SpecialCycleResolverTests` o przypadki:

- na starcie windupu Slam nie wydaje many i nie zadaje obrażeń;
- w ticku impaktu mana spada do zera i pojawia się dokładnie jeden
  `SpecialAreaImpact`;
- cel w odległości `1` otrzymuje `100%` attack damage;
- cele na każdym z sześciu sąsiednich heksów są trafione;
- cel w odległości `2`, sojusznik i martwa jednostka nie są trafione;
- kilka celów w promieniu dostaje obrażenia w tym samym ticku;
- brak celów nadal kończy cast i zużywa manę;
- armor, shield i invulnerability przechodzą przez istniejący resolver;
- Slam nie krytykuje, nie daje `ManaPerAttack` i nie jest przejmowany przez
  guard;
- stun/sleep/silence/śmierć przed impaktem anulują damage bez wydania many;
- przerwanie po impakcie nie cofa damage i nie pozwala na drugi impakt;
- dwa przeciwne Slamy kończące windup w tym samym ticku oba się rozstrzygają;
- cel, który zakończy ruch wcześniej w tym samym ticku, jest oceniany według
  nowego `CurrentHex`;
- reset bitwy czyści pending intent i nie odtwarza starego impaktu.

Rozszerzyć testy danych/UI:

- `UnitSpecialCombatSpec` poprawnie snapshotuje `EffectRadius`;
- `Slam` jest valid bez statusu;
- formatter rozpoznaje `{attackDamagePercent}` i `{effectRadius}`;
- wszystkie assety speciali mają poprawne template'y;
- `AJ-4X.asset` wskazuje na `Special_Slam`.

Regresja:

- `SpecialCycleResolverTests` dla HasteBurst i FurySwipes;
- `DamageCalculatorTests` i `DamageResolverTests`;
- `BattleTickLoopTests`;
- testy `BattleView`/VFX związane z routingiem eventów;
- `UnitAnimatorControllerTests` i `UnitPrefabSourceTests`.

Testy Edit Mode uruchomić przez Unity MCP w otwartym Editorze, nigdy przez
batchmode. Po zmianie assetów wykonać Play Mode smoke test sceny `Battle`.

## 8. Wydajność mobilna

- zero LINQ i zero zapytań fizycznych przy wyszukiwaniu celów;
- jeden liniowy przebieg po jednostkach na każdy faktyczny impakt, nie co tick;
- prealokowany workspace bez `GC.Alloc` po rozgrzaniu;
- jeden pooled VFX obszarowy na cast zamiast efektu obszarowego per cel;
- brak nowych `Update`, coroutine i tweenów;
- radius oparty na `HexBoard.Distance`, nie na odległości świata;
- brak zmian w URP, shaderach, teksturach i build size poza opcjonalnym lekkim
  prefabem VFX/SFX.

W Profilerze sprawdzić `DeckBattle` markers dla ticka i damage resolvera przy
kilku równoczesnych Slamach oraz `0 B GC.Alloc` po rozgrzaniu.

## 9. Kolejność implementacji

1. Dodać `Slam`, `EffectRadius`, snapshot i walidację danych.
2. Dodać tokeny opisu i ich testy.
3. Rozszerzyć workspace i deterministyczne rozstrzyganie impaktu.
4. Dodać `SpecialAreaImpact` oraz routing prezentacji/VFX.
5. Utworzyć `Special_Slam.asset` i przypisać go do AJ-4X.
6. Dodać testy logiki i uruchomić wąską regresję w Unity Editorze.
7. Dostroić timing `0.85/0.47` na klipie i wykonać Play Mode smoke test.
8. Sprawdzić alokacje i frame time w profilu Android/mobile-like.

## 10. Definition of Done

- AJ-4X przy pełnej manie wykonuje animację podskoku i lądowania;
- damage następuje raz, w logicznym momencie kontaktu z ziemią;
- wszystkie i tylko żywe jednostki przeciwnika w promieniu `1` są trafione;
- każdy cel otrzymuje `100% attack damage` przez wspólny pipeline obrażeń;
- brak celu oznacza poprawny whiff z wydaniem many;
- gameplay działa identycznie bez Animatora i nie zależy od Animation Eventu;
- root motion pozostaje wyłączony, a jednostka nie opuszcza logicznego heksa;
- HasteBurst i FurySwipes nie mają regresji;
- testy przechodzą, a hot path nie generuje nowych alokacji;
- timing i powrót do idle nie ucinają widocznie klipu na wspieranych
  prędkościach walki.
