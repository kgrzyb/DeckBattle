# Projectile System Plan

## Cel

Dodac system projectile dla jednostek ranged w realtime auto-battle.

Jednostka ranged ma wystrzeliwac projectile w momencie ataku. Cooldown ataku dotyczy wystrzalu, ale damage jest aplikowany dopiero po dolocie projectile do celu. Projectile ma miec osobny config z predkoscia i prefabem widoku.

System musi pozostac deterministyczny, testowalny w edit mode i lekki dla mobile.

## Decyzje Produktowe

- Jednostki melee zostaja bez zmian: atak i damage sa rozliczane natychmiast.
- Jednostki ranged z poprawnym `ProjectileDefinition` uzywaja opoznionego damage przez projectile.
- Brak projectile configu oznacza fallback do obecnego natychmiastowego ataku.
- Projectile sledzi cel wizualnie, jezeli cel nadal istnieje w widoku.
- Jezeli cel umrze przed trafieniem, projectile konczy lot w ostatnim znanym miejscu celu i nie zadaje damage.
- Projectile nie wybiera nowego celu po wystrzale.
- Attack range jest sprawdzany tylko w momencie wystrzalu.
- Po wystrzale cel moze wyjsc poza range, ale projectile nadal moze go trafic, jezeli cel zyje.

## Zasady Symulacji Projectile

Projectile jest opoznionym rozliczeniem juz wykonanego ataku, a nie pelnym fizycznym obiektem gameplayowym.

Przy wystrzale:

- symulacja sprawdza target i attack range;
- cooldown atakujacego startuje od razu;
- attacker dostaje mana za attack od razu;
- damage i crit sa wyliczane od razu, zeby zachowac deterministyczna kolejnosc RNG;
- damage nie jest jeszcze aplikowany;
- symulacja tworzy runtime projectile z czasem dolotu.

Czas dolotu jest liczony raz przy strzale:

```text
travelTime = distanceAtLaunch / projectile.Speed
```

Symulacja nie przelicza czasu dynamicznie, nawet jezeli cel sie poruszy. Wizualne sledzenie celu jest tylko prezentacja i nie zmienia wyniku walki.

Projectile runtime powinien zapisac:

- `ProjectileId`
- `AttackerUnitId`
- `TargetUnitId`
- `AttackerDefinition`
- `ProjectileDefinition`
- `FromHex`
- `LastKnownTargetHex`
- `TravelTimeRemaining`
- `Damage`
- `IsCritical`

Co tick `ProjectileResolver` zmniejsza `TravelTimeRemaining`.

Kiedy `TravelTimeRemaining <= 0`:

- jezeli target zyje, projectile trafia target i dopiero wtedy aplikuje damage, crit event, mana za damage taken oraz ewentualna smierc;
- jezeli target nie zyje, projectile konczy sie bez damage;
- projectile jest usuwany z listy aktywnych projectile.

## Proponowana Architektura

### ProjectileDefinition

Dodac `ProjectileDefinition : ScriptableObject`.

Proponowana lokalizacja:

- `Assets/DeckBattle/Scripts/Data/ProjectileDefinition.cs`

Proponowane pola:

- `string ProjectileId`
- `float Speed`
- `ProjectileView ProjectilePrefab`
- opcjonalnie `float SpawnHeight`
- opcjonalnie `float HitHeight`

`OnValidate`:

- `Speed = Mathf.Max(0.01f, Speed)`

### UnitDefinition

Rozszerzyc `UnitDefinition` o:

```csharp
public ProjectileDefinition Projectile;
```

Regula:

- `Projectile == null` oznacza fallback do natychmiastowego ataku.
- Dla ranged unitow docelowo asset powinien miec ustawiony projectile, ale runtime nie powinien sie wywalac przy braku configu.

### ProjectileRuntimeState

Dodac czysty runtime state bez zaleznosci od Unity scene.

Proponowana lokalizacja:

- `Assets/DeckBattle/Scripts/Battle/ProjectileRuntimeState.cs`

Stan powinien przechowywac tylko dane potrzebne do deterministycznego rozliczenia trafienia.

### BattleSimulation

Rozszerzyc `BattleSimulation` o liste aktywnych projectile.

Proponowane API:

- `IReadOnlyList<ProjectileRuntimeState> Projectiles`
- `ProjectileRuntimeState SpawnProjectile(...)`
- metoda usuwania projectile po trafieniu

Implementacja powinna unikac alokacji per tick. Usuwanie projectile mozna robic petla od konca albo swap-remove.

### CombatResolver

`CombatResolver` powinien rozdzielic atak natychmiastowy od projectile attack.

Dla melee albo ranged bez configu:

- zachowac obecne zachowanie.

Dla ranged z `ProjectileDefinition`:

- sprawdzic target i range;
- jezeli cooldown gotowy, wyemitowac `UnitAttackStarted`;
- obliczyc damage i crit;
- utworzyc projectile;
- dodac mana attackerowi za attack;
- ustawic `AttackCooldownRemaining`;
- nie aplikowac HP damage w tym ticku.

### ProjectileResolver

Dodac `ProjectileResolver`.

Proponowana lokalizacja:

- `Assets/DeckBattle/Scripts/Battle/ProjectileResolver.cs`

Odpowiedzialnosci:

- aktualizacja `TravelTimeRemaining`;
- aktualizacja `LastKnownTargetHex`, jezeli target zyje;
- aplikacja damage po dolocie;
- emisja eventow `ProjectileHit`, `UnitCrit`, `UnitDamaged`, `UnitManaChanged`, `UnitDied`;
- usuwanie projectile po zakonczeniu.

### BattleTickLoop

Proponowana kolejnosc ticka:

1. `RefreshTargets`
2. `ProjectileResolver.ResolveProjectiles`
3. `CombatResolver.ResolveCombat`
4. `MovementResolver.ResolveMovement`
5. `TryEndBattle`

Uzasadnienie:

- projectile, ktory dolatuje w danym ticku, moze zakonczyc walke przed kolejnym ruchem lub kolejnym atakiem;
- nowe projectile wystrzelone w tym samym ticku zaczynaja travel time po rozliczeniu juz aktywnych projectile;
- wynik pozostaje stabilny i latwy do testowania.

## Eventy

Rozszerzyc `BattleEventType` o:

- `ProjectileLaunched`
- opcjonalnie `ProjectileHit`

`ProjectileLaunched` powinien przenosic:

- projectile runtime id;
- attacker id;
- target id;
- from hex;
- target hex at launch albo last known target hex;
- travel duration.

`UnitDamaged`, `UnitDied` i `UnitCrit` powinny byc emitowane dopiero przy trafieniu projectile, nie przy wystrzale.

## Widok

### ProjectileView

Dodac lekki komponent `ProjectileView : MonoBehaviour`.

Proponowane API:

- `Play(Vector3 from, Transform target, Vector3 fallbackTarget, float duration)`
- `Play(Vector3 from, Vector3 target, float duration)`
- `bool IsPlaying`
- `Release()`

Zachowanie:

- jezeli target transform istnieje, projectile leci do aktualnej pozycji targetu;
- jezeli target znika, projectile zapamietuje ostatnia znana pozycje i konczy lot tam;
- `ProjectileView` nie aplikuje damage i nie decyduje o gameplayu.

### BattleView

Rozszerzyc `BattleView.ProcessEvents()` o obsluge projectile eventow.

Przy `ProjectileLaunched`:

- znalezc attacker view i target view;
- pobrac projectile prefab z `attacker.Definition.Projectile.ProjectilePrefab`;
- uruchomic `ProjectileView` z poola;
- przekazac `duration` zgodny z eventem.

Pooling:

- dodac liste aktywnych projectile views;
- dodac stack poola projectile views;
- zwracac view do poola po `IsPlaying == false`;
- czyscic pool przy `ClearBattle`.

Efekt damage zostaje pod `UnitDamaged`, dzieki czemu odpala sie dopiero przy trafieniu.

## Assety

Dodac przykladowe assety:

- `Assets/DeckBattle/Data/Projectiles/Arrow.asset`
- `Assets/DeckBattle/Data/Projectiles/Bolt.asset`

Przypisac:

- `Archer.Projectile = Arrow`
- `Crossbowman.Projectile = Bolt`

Prefab projectile powinien byc prosty i tani:

- prosty mesh albo sprite;
- prosty unlit/mobile material;
- bez ciezkich particle systemow;
- bez real-time swiatel i cieni.

## Testy

Dodac lub zaktualizowac edit mode tests.

Minimalne przypadki:

- ranged unit z projectile nie zadaje damage w ticku wystrzalu;
- ranged unit z projectile ustawia cooldown od razu po wystrzale;
- projectile zadaje damage dopiero po `TravelTimeRemaining <= 0`;
- projectile trafia zywy cel nawet jezeli cel po wystrzale wyszedl poza pierwotny range;
- projectile nie zadaje damage, jezeli target umarl przed dolotem;
- crit i mana za damage taken sa emitowane przy trafieniu, nie przy wystrzale;
- battle nie konczy sie przed dolotem lethal projectile;
- brak projectile configu zachowuje obecny natychmiastowy atak.

Istniejace testy do przejrzenia:

- `Assets/DeckBattle/Tests/EditMode/CombatResolverTests.cs`
- `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`
- `Assets/DeckBattle/Tests/EditMode/BattleSimulationCombatServiceTests.cs`

## Performance I Mobile

- Brak alokacji per tick w resolverach.
- Brak LINQ w hot path.
- Projectile views musza byc poolowane.
- Symulacja nie powinna zalezec od `Transform`, `GameObject` ani FPS.
- Widok moze interpolowac co klatke, ale gameplay rozlicza tylko `BattleTickLoop`.
- Prefaby projectile powinny byc tanie w renderingu, szczegolnie przy wielu ranged unitach.
- Unikac przezroczystego overdraw i ciezkich particle effects.

## Ryzyka

- Jezeli travel time bedzie zbyt dlugi, ranged combat moze wydawac sie malo responsywny.
- Jezeli wiele projectile dolatuje w tym samym ticku, kolejnosc rozliczenia powinna byc stabilna, najlepiej wedlug kolejnosci listy aktywnych projectile lub `ProjectileId`.
- Zbyt szybkie projectile moga zachowywac sie prawie jak natychmiastowy damage. To jest akceptowalne, ale powinno byc swiadomym tuningiem.
- Wizualne sledzenie celu moze wygladac inaczej niz logiczny czas trafienia, jezeli cel mocno sie porusza. To jest celowa decyzja: symulacja pozostaje deterministyczna.

## Kolejnosc Implementacji

1. Dodac `ProjectileDefinition`, `ProjectileRuntimeState` i pola w `UnitDefinition`.
2. Rozszerzyc `BattleSimulation` o aktywne projectile.
3. Dodac eventy projectile.
4. Wydzielic `ProjectileResolver`.
5. Zmienic `CombatResolver`, aby ranged z configiem spawnowal projectile zamiast aplikowac damage natychmiast.
6. Podpiac `ProjectileResolver` w `BattleTickLoop`.
7. Dodac testy czystej symulacji.
8. Dodac `ProjectileView` i pooling w `BattleView`.
9. Dodac przykladowe assety/prefaby projectile.
10. Uruchomic narrow edit mode tests i sprawdzic scene battle w edytorze.
