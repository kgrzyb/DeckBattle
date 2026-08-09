# Plan: data-driven VFX pooling dla walki

## 1. Cel

Dodać centralny, wydajny i łatwy w authoringu system VFX dla prezentacji walki.
System ma umożliwiać podpinanie różnych efektów do jednostek, pocisków, statusów
i specjali bez dopisywania osobnego poola ani kolejnego zestawu pól w
`BattleView` dla każdego nowego przypadku.

Docelowe przypadki użycia:

- przygotowanie i wykonanie zwykłego ataku;
- muzzle flash i wypuszczenie pocisku;
- slash, spark i impact na trafionym celu;
- przygotowanie, cast i kolejne uderzenia speciala;
- efekt obrażeń, krytyka, leczenia i śmierci;
- krótkie efekty przypięte do jednostki;
- długotrwałe efekty, które muszą zostać jawnie zakończone;
- warianty domyślne oraz override per jednostka, special lub pocisk.

System ma zachować separację deterministycznej symulacji od prezentacji Unity.
Gameplay wskazuje, co i kiedy logicznie się wydarzyło, natomiast warstwa
prezentacji rozwiązuje prefab, anchor, transformację i czas życia efektu.

## 2. Zakres pierwszej wersji

W zakresie:

- współdzielony runtime pool VFX indeksowany prefabem;
- `VfxDefinition` jako reużywalny asset konfiguracyjny;
- mapowanie semantycznych cue, np. `AttackFired`, na efekty;
- profil domyślny i profil per jednostka;
- profile prezentacyjne dla speciali i pocisków;
- typowane anchory na prefabach jednostek;
- efekty jednorazowe world-space i efekty podążające za anchorem;
- jawnie zwalniane efekty ciągłe;
- prewarm, limit aktywnych instancji i limit obiektów zatrzymanych w puli;
- obsługa prędkości walki 1x/2x;
- pełny reset stanu przy ponownym użyciu;
- cleanup przy śmierci jednostki, rebindzie, końcu rundy i zamknięciu bitwy;
- walidacja konfiguracji oraz testy poolingu, lookup i routingu eventów.

Poza zakresem pierwszej wersji:

- przebudowa zasad obrażeń lub timingu symulacji;
- uzależnianie wyniku walki od Animation Events;
- jeden uniwersalny komponent zastępujący ruch i rozstrzyganie pocisków;
- automatyczne ładowanie efektów przez `Resources.Load` lub Addressables;
- ciężkie efekty VFX Graph, światła realtime, cienie i fullscreen post-process;
- edytor node graph do budowania sekwencji efektów;
- migracja wszystkich istniejących statusów w tym samym zadaniu;
- audio, camera shake i haptics; mogą później korzystać z analogicznego systemu
  cue, ale nie należą do poola VFX.

## 3. Stan obecny

Projekt ma już kilka niezależnych rozwiązań prezentacyjnych:

- `BattleEffectPresenter` utrzymuje osobne listy i stosy dla dwóch
  zahardcodowanych prefabów: attack i damage;
- `PooledBattleEffect` implementuje prosty efekt world-space z własnym
  `Update`, czasem życia, skalą i kolorem;
- `BattleProjectilePresenter` posiada pule per prefab pocisku, śledzi logiczne
  `ProjectileId` i kończy widok po `ProjectileResolved`;
- `ProjectileView` odpowiada za lot i śledzenie poruszającego się celu;
- `UnitStatusVfxController` posiada pule per prefab, prewarm, one-shoty i efekty
  aktywne przypięte do jednostek;
- `StatusVfxView` resetuje Particle System i skaluje jego szybkość zgodnie z
  prędkością walki;
- `BattlePresentationLookup` rozdziela stabilne identyfikatory prezentacyjne od
  prefabów Unity;
- `BattleView.ProcessCombatTick` jest centralnym routerem eventów do
  presenterów;
- `UnitAnimationEventRelay` ma obecnie jeden sygnał używany przez animację
  speciala.

Istniejące fundamenty są poprawne, ale dodanie kolejnych rodzajów VFX wymagałoby
rozbudowy `BattleEffectPresenter`, dodawania serializowanych pól do `BattleView`
i powielania puli. Brakuje również typowanych anchorów takich jak `Muzzle`,
`Weapon`, `Body` i `Feet`.

## 4. Najważniejsze decyzje architektoniczne

### 4.1. Wspólny runtime pool, wyspecjalizowane presentery

Należy rozdzielić dwa poziomy odpowiedzialności:

```text
BattleEvent
  -> wyspecjalizowany presenter
  -> rozwiązanie VfxDefinition i miejsca spawnu
  -> BattleVfxPool
  -> PooledVfxView
```

`BattleVfxPool` zarządza wyłącznie instancjami, prewarmem, czasem życia,
prędkością i zwracaniem do puli. Nie interpretuje gameplayowych eventów.

`BattleVfxPresenter` interpretuje eventy jednorazowych efektów walki. Istniejące
presentery mogą korzystać z tego samego poola, zachowując swoją specjalizację:

- `BattleProjectilePresenter` nadal odpowiada za lot, target tracking i
  rozstrzygnięcie pocisku;
- `UnitStatusVfxController` nadal odpowiada za stacki i reconciliation
  statusów;
- oba mogą docelowo delegować tworzenie i zwalnianie widoków do wspólnego
  runtime poola.

Nie tworzyć jednego szerokiego `VfxManager`, który zna pociski, statusy,
animatory, obrażenia i reguły speciali.

### 4.2. Symulacja nie przechowuje prefabów Unity

`BattleEvent`, `UnitCombatSpec` i resolvery nie mogą otrzymać referencji do
`GameObject`, `ParticleSystem`, `VfxDefinition` ani `UnitVfxProfile`.

Event przekazuje wyłącznie dane potrzebne do identyfikacji zdarzenia:

- typ eventu;
- source i target unit ID;
- sequence ID;
- special kind lub stabilny presentation ID;
- projectile presentation ID;
- pozycje logiczne i wynik zdarzenia.

Warstwa prezentacji korzysta z `BattlePresentationLookup` oraz danych
zbudowanych przy wejściu do bitwy. Jeżeli cue nie ma skonfigurowanego efektu,
system wykonuje bezpieczne no-op.

### 4.3. Cue opisuje znaczenie, a nie konkretny prefab

Dodać typ prezentacyjny:

```csharp
public enum BattleVfxCue
{
    None = 0,
    AttackWindup = 1,
    AttackFired = 2,
    AttackImpact = 3,
    Damaged = 4,
    CriticalImpact = 5,
    SpecialWindup = 6,
    SpecialCast = 7,
    SpecialStrike = 8,
    ProjectileLaunch = 9,
    ProjectileImpact = 10,
    Death = 11
}
```

Nie używać stringów ani nazw stanów Animatora jako kluczy konfiguracji. Enum
eliminuje literówki, daje tani lookup i pozwala walidować duplikaty.

Lista cue pierwszej wersji powinna pozostać mała. Nowy cue dodawać tylko wtedy,
gdy ma odrębną semantykę i moment odpalenia, a nie dla każdego nowego prefabu.

### 4.4. Efekt jednorazowy i efekt ciągły mają różny lifecycle

System obsługuje dwa podstawowe tryby:

- `OneShot` — uruchomiony raz i automatycznie zwracany po zakończeniu;
- `Persistent` — uruchomiony i zwalniany jawnie przez uchwyt.

Przykłady `OneShot`: slash, muzzle flash, impact i burst speciala.

Przykłady `Persistent`: aura ładowania, channeling i efekt utrzymywany przez
czas animacji. Efekt persistent nie może polegać wyłącznie na zadanym czasie,
ponieważ akcja może zostać anulowana albo jednostka może zginąć.

### 4.5. Gameplay event i Animation Event pełnią różne role

Gameplay event pozostaje źródłem prawdy. Animation Event może doprecyzować
wyłącznie klatkę prezentacji.

- obrażenia i wynik ataku wynikają z symulacji;
- `ProjectileLaunched` uruchamia logicznie powiązany widok pocisku;
- `AttackFired` i `SpecialStrikeFired` dostarczają kontekst source/target;
- Animation Event może odpalić lokalny slash, spark lub muzzle flash;
- brak albo błędny Animation Event nie może zatrzymać walki ani zmienić wyniku.

Presenter lub `UnitView` przechowuje aktualny `sequenceId` i target. Relay
emituje tylko typowany sygnał dla aktywnej sekwencji. Spóźniony event ze starej
animacji jest ignorowany.

## 5. Model danych

### 5.1. `VfxDefinition`

Reużywalny `ScriptableObject` opisujący wykonanie pojedynczego efektu:

```csharp
[CreateAssetMenu(menuName = "Deck Battle/VFX/VFX Definition")]
public sealed class VfxDefinition : ScriptableObject
{
    public PooledVfxView Prefab;
    public VfxLifetimeMode LifetimeMode;
    [Min(0.01f)] public float FallbackLifetime = 0.5f;
    [Min(0)] public int PrewarmCount = 2;
    [Min(1)] public int MaxActiveCount = 16;
    [Min(0)] public int MaxRetainedCount = 8;
    public bool ScaleWithCombatSpeed = true;
}
```

`LifetimeMode` pierwszej wersji:

- `ParticleAlive` — zakończenie po `ParticleSystem.IsAlive(true) == false`;
- `Duration` — centralny timer jako fallback dla animowanego mesha;
- `Manual` — efekt persistent zwalniany przez uchwyt.

`FallbackLifetime` chroni przed prefabem z błędnie skonfigurowanym loopingiem.
W Development Build przekroczenie czasu powinno generować jednorazowe
ostrzeżenie z nazwą definicji.

### 5.2. `BattleVfxBinding`

Pojedyncze mapowanie cue na definicję i sposób umieszczenia:

```csharp
[Serializable]
public struct BattleVfxBinding
{
    public BattleVfxCue Cue;
    public VfxDefinition Effect;
    public VfxSpawnSubject Subject;
    public UnitVfxAnchor Anchor;
    public bool FollowAnchor;
    public bool FaceTarget;
    public Vector3 LocalPosition;
    public Vector3 LocalEulerAngles;
    public Vector3 LocalScale;
}
```

`VfxSpawnSubject`:

- `Source` — jednostka wykonująca akcję;
- `Target` — jednostka będąca celem;
- `SourceHex` — pozycja heksa źródłowego;
- `TargetHex` — pozycja heksa docelowego;
- `World` — jawnie przekazana pozycja prezentacyjna.

Jeżeli jednostka docelowa nie ma aktywnego `UnitView`, system używa pozycji
heksa z eventu. Brak anchora na istniejącym widoku oznacza fallback do root
jednostki oraz ostrzeżenie wyłącznie w Development Build.

### 5.3. Profile VFX

Dodać małe profile `ScriptableObject` z tablicą `BattleVfxBinding` i lookupem
budowanym w `OnEnable`/`OnValidate`, bez LINQ.

Minimalny zestaw:

- `BattleVfxCatalog` — globalne fallbacki;
- `UnitVfxProfile` — override dla jednostki;
- `SpecialVfxProfile` — prezentacja konkretnego speciala;
- pola launch/impact w prezentacji `ProjectileDefinition` albo mały
  `ProjectileVfxProfile`.

Kolejność rozwiązywania bindingu:

1. profil konkretnego speciala lub pocisku;
2. override jednostki;
3. globalny `BattleVfxCatalog`;
4. brak efektu jako poprawny no-op.

Profil nie może zawierać więcej niż jednego bindingu dla tego samego cue.
Jeżeli w przyszłości jedno cue ma odpalać kilka efektów, należy wprowadzić
`VfxSequenceDefinition` lub tablicę definicji jako jawne rozszerzenie, zamiast
akceptować przypadkowe duplikaty.

### 5.4. Powiązanie z istniejącymi definicjami

Proponowane pola prezentacyjne:

```text
UnitDefinition.UnitVfxProfile
UnitSpecialDefinition.SpecialVfxProfile
ProjectileDefinition.LaunchVfx
ProjectileDefinition.ImpactVfx
```

Projekt już przechowuje `UnitPrefab` i `ProjectilePrefab` w tych definicjach,
więc jest to spójne z obecnym sposobem authoringu. Referencje pozostają poza
runtime specami symulacji.

`BattlePresentationLookup.Rebuild` zbiera profile i definicje tylko dla kart
dostępnych w danej bitwie. Pozwala to prewarmować wyłącznie potrzebne prefaby.

## 6. Anchory jednostki

### 6.1. `UnitVfxAnchors`

Każdy prefab jednostki może otrzymać komponent z serializowanymi referencjami:

```csharp
public enum UnitVfxAnchor
{
    Root = 0,
    Body = 1,
    Feet = 2,
    Weapon = 3,
    Muzzle = 4,
    Head = 5,
    Special = 6
}
```

```csharp
public sealed class UnitVfxAnchors : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private Transform feet;
    [SerializeField] private Transform weapon;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform head;
    [SerializeField] private Transform special;

    public Transform Resolve(UnitVfxAnchor anchor) { /* switch */ }
}
```

`UnitView` cache'uje komponent w `Awake` i udostępnia read-only metodę
rozwiązania anchora. Nie wykonywać `Transform.Find`, `GetComponentInChildren`
ani wyszukiwania po nazwie przy każdym efekcie.

Anchory są opcjonalne. `Root` zawsze zwraca `UnitView.transform`.

### 6.2. Przykładowe mapowania

| Sytuacja | Subject | Anchor | Follow |
|---|---|---|---|
| Muzzle flash łucznika | Source | Muzzle | Nie |
| Trail ładowania broni | Source | Weapon | Tak |
| Slash ataku melee | Source | Weapon | Nie |
| Impact ataku | Target | Body | Nie |
| Aura speciala | Source | Feet | Tak |
| Krytyczne trafienie | Target | Body | Nie |
| Eksplozja obszarowa | TargetHex | Root | Nie |

Efekt parentowany do anchora podczas one-shota powinien być używany wyłącznie,
gdy ma faktycznie podążać za ruchem. Krótki impact zwykle należy odparentować po
ustawieniu pozycji, aby nie przesuwał się razem z celem.

## 7. Runtime pool

### 7.1. `PooledVfxView`

Pasywny komponent prefabu:

- cache'uje tablicę `ParticleSystem`, opcjonalne `TrailRenderer`, `Animator` i
  renderery potrzebne do resetu;
- nie interpretuje `BattleEvent`;
- nie pobiera samodzielnie konfiguracji z katalogów;
- udostępnia `Play`, `Tick`, `SetCombatSpeed` i `Release`;
- po `Release` zatrzymuje i czyści particle, czyści trail, resetuje Animator,
  parent, local transform oraz flagi poprzedniego użycia;
- nie wywołuje `Destroy`.

Widok nie powinien mieć własnego `Update`. Jeden centralny tick po aktywnych
efektach daje przewidywalny koszt i ogranicza liczbę Unity callbacks.

### 7.2. `VfxSpawnRequest`

Żądanie spawnu powinno być `readonly struct`, aby nie generować alokacji:

```csharp
public readonly struct VfxSpawnRequest
{
    public readonly VfxDefinition Definition;
    public readonly Transform Anchor;
    public readonly Vector3 WorldPosition;
    public readonly Quaternion WorldRotation;
    public readonly Vector3 LocalPosition;
    public readonly Quaternion LocalRotation;
    public readonly Vector3 LocalScale;
    public readonly bool FollowAnchor;
}
```

Nie używać słowników string/object ani dynamicznych parametrów. Jeżeli efekt
wymaga koloru lub intensywności, należy później dodać kilka jawnych opcjonalnych
pól oraz zastosować `MaterialPropertyBlock`, bez tworzenia materiałów per
instancja.

### 7.3. `BattleVfxPool`

Centralny komponent sceny odpowiada za:

- `Dictionary<PooledVfxView, Stack<PooledVfxView>>` lub równoważny lookup per
  prefab;
- jedną listę aktywnych instancji;
- prewarm na początku bitwy;
- `Play(in VfxSpawnRequest)`;
- `Release(VfxHandle)` dla efektów manualnych;
- `ReleaseOwnedByUnit(int unitId)` dla efektów podążających za jednostką;
- `SetCombatSpeed(float)`;
- `ReleaseAll()` i cleanup w `OnDisable`;
- diagnostykę pool miss oraz peak active count w Development Build.

Instancja pobrana z puli przechowuje referencję do prefabu będącego kluczem
puli. Przy zwrocie nie wykonuje lookupu po nazwie ani ID stringowym.

### 7.4. Polityka przepełnienia

Każda definicja posiada `MaxActiveCount` i `MaxRetainedCount`.

Po przekroczeniu `MaxActiveCount` pierwsza wersja pomija nowy efekt i zwiększa
licznik diagnostyczny. Nie należy zwalniać losowej aktywnej instancji, ponieważ
może to uciąć ważny impact albo aurę.

Jeżeli w puli znajduje się już `MaxRetainedCount` wolnych instancji, nadmiarowy
obiekt można zniszczyć dopiero podczas kontrolowanego cleanupu poza gorącą
ścieżką walki. W normalnym przebiegu odpowiedni prewarm i limity powinny
sprawić, że ta sytuacja nie wystąpi.

## 8. Routing eventów

### 8.1. `BattleVfxPresenter`

Presenter otrzymuje:

- `BattlePresentationLookup`;
- `UnitViewRegistry`;
- `BoardPresenter`;
- `BattleVfxPool`;
- globalny `BattleVfxCatalog`.

Przykładowe mapowanie:

| BattleEvent | Cue | Profil bazowy |
|---|---|---|
| `AttackWindupStarted` | `AttackWindup` | jednostka |
| `AttackFired` | `AttackFired` | jednostka |
| `ProjectileLaunched` | `ProjectileLaunch` | pocisk |
| `ProjectileResolved` przy trafieniu | `ProjectileImpact` | pocisk |
| `UnitDamaged` | `Damaged` / `CriticalImpact` | target/globalny |
| `SpecialWindupStarted` | `SpecialWindup` | special/jednostka |
| `SpecialCastStarted` | `SpecialCast` | special/jednostka |
| `SpecialStrikeFired` | `SpecialStrike` | special/jednostka |
| `UnitDied` | `Death` | jednostka/globalny |

Nie każdy event musi odpalać efekt. Presenter wykonuje lookup i kończy pracę,
jeśli binding nie istnieje.

### 8.2. Impact zwykłego ataku

`UnitDamaged` jest poprawnym globalnym punktem prezentacji faktycznie
zapisanego damage, ale sam nie identyfikuje rodzaju ataku, który go spowodował.

Pierwsza wersja powinna rozdzielić:

- generyczny `Damaged`/`CriticalImpact` na podstawie `UnitDamaged`;
- projectile-specific impact na podstawie `ProjectileResolved` i istniejącego
  projectile presentation ID;
- special-specific impact na podstawie `SpecialStrikeFired`.

Jeżeli w przyszłości zwykły melee impact ma zależeć od atakującego, event
obrażeń powinien otrzymać jawny source ID lub korelacyjny sequence ID. Nie
należy zgadywać źródła przez kolejność eventów.

### 8.3. Anulowanie i cleanup sekwencji

- `AttackWindupCancelled` zwalnia manualny VFX windupu powiązany z unit ID i
  sequence ID;
- `SpecialWindupCancelled` robi to samo dla speciala;
- rozpoczęcie nowej sekwencji na tym samym slocie najpierw kończy poprzedni
  efekt persistent;
- `UnitDied` zwalnia wszystkie efekty śledzące jednostkę;
- `ClearBattle` zwalnia wszystkie aktywne instancje i resetuje liczniki;
- efekty world-space one-shot mogą dokończyć animację po śmierci celu, jeśli
  nie są do niego parentowane.

## 9. Animation Events

Rozszerzyć `UnitAnimationEventRelay` o mały, jawny zestaw metod:

```csharp
public void AttackContact();
public void ProjectileRelease();
public void SpecialContact();
```

Relay przekazuje sygnał do `UnitView`. `UnitView` sprawdza:

- czy jednostka nie umiera;
- czy stan wizualny odpowiada sygnałowi;
- czy aktywny sequence ID nadal jest aktualny.

Następnie emituje prezentacyjne zdarzenie z `UnitView` i cue. Presenter posiada
już kontekst targetu ustawiony przez odpowiedni `BattleEvent`.

Nie wywoływać poola bezpośrednio z Animation Event. Pozwala to zachować lookup,
fallbacki, limity i cleanup w jednym miejscu.

## 10. Integracja z istniejącym kodem

### `BattleView`

- zastąpić pola `attackEffectPrefab` i `damageEffectPrefab` referencjami do
  `BattleVfxPool` oraz `BattleVfxCatalog`;
- utworzyć `BattleVfxPresenter` w `EnsurePresenters`;
- routować odpowiednie eventy do presentera;
- propagować `SetCombatSpeed`;
- wywoływać `ReleaseAll` przy `BindInitialState`, `ClearBattle` i wyłączeniu;
- nie umieszczać logiki wyboru profilu bezpośrednio w switchu eventów.

### `BattleEffectPresenter`

Po przepięciu attack i damage:

- usunąć albo zastąpić cienkim adapterem do `BattleVfxPresenter`;
- nie utrzymywać osobnych stosów per znaczenie efektu;
- zachować istniejące testy kontraktu prezentacji podczas migracji.

### `BattlePresentationLookup`

- przechowywać unit VFX profile według unit presentation ID;
- przechowywać special i projectile VFX według stabilnych presentation ID;
- wykrywać kolizje i różne assety pod tym samym ID;
- udostępniać tylko read-only metody `TryGet`.

### `UnitView`

- cache'ować `UnitVfxAnchors`;
- udostępniać rozwiązywanie anchora bez wyszukiwania komponentów w hot path;
- uogólnić obecny `SpecialAttackAnimationEvent` do typowanego eventu
  prezentacyjnego;
- przy rebindzie wyczyścić aktywne sequence ID i subskrypcje.

### `BattleProjectilePresenter`

- zachować odpowiedzialność za pozycję, lot, target tracking i powiązanie z
  `ProjectileId`;
- odpalać launch/impact one-shoty przez `BattleVfxPresenter` lub wspólny pool;
- nie zamieniać `ProjectileView` w zwykły czasowy VFX;
- w późniejszej migracji może korzystać ze wspólnej infrastruktury puli, ale
  powinien zachować własny typ widoku i lifecycle logicznego resolve.

### `UnitStatusVfxController`

- w pierwszym etapie pozostawić bez zmian funkcjonalnych;
- następnie zastąpić wewnętrzne `Instantiate`/stosy delegacją do wspólnego
  runtime poola;
- zachować shadow-state, reconciliation stacków i obsługę Apply/Active/Remove;
- nie przenosić reguł statusów do generycznego poola.

## 11. Proponowane pliki

Nowe skrypty:

```text
Assets/DeckBattle/Scripts/Data/VfxDefinition.cs
Assets/DeckBattle/Scripts/Data/BattleVfxCatalog.cs
Assets/DeckBattle/Scripts/Data/UnitVfxProfile.cs
Assets/DeckBattle/Scripts/Data/SpecialVfxProfile.cs
Assets/DeckBattle/Scripts/Battle/BattleVfxCue.cs
Assets/DeckBattle/Scripts/Battle/UnitVfxAnchor.cs
Assets/DeckBattle/Scripts/Battle/UnitVfxAnchors.cs
Assets/DeckBattle/Scripts/Battle/VfxSpawnRequest.cs
Assets/DeckBattle/Scripts/Battle/VfxHandle.cs
Assets/DeckBattle/Scripts/Battle/PooledVfxView.cs
Assets/DeckBattle/Scripts/Battle/BattleVfxPool.cs
Assets/DeckBattle/Scripts/Battle/BattleVfxPresenter.cs
```

Modyfikowane skrypty:

```text
Assets/DeckBattle/Scripts/Data/UnitDefinition.cs
Assets/DeckBattle/Scripts/Data/UnitSpecialDefinition.cs
Assets/DeckBattle/Scripts/Data/ProjectileDefinition.cs
Assets/DeckBattle/Scripts/Battle/BattlePresentationLookup.cs
Assets/DeckBattle/Scripts/Battle/BattleView.cs
Assets/DeckBattle/Scripts/Battle/BattleUnitPresenter.cs
Assets/DeckBattle/Scripts/Battle/UnitView.cs
Assets/DeckBattle/Scripts/Battle/UnitAnimationEventRelay.cs
Assets/DeckBattle/Scripts/Battle/BattleProjectilePresenter.cs
```

Opcjonalna późniejsza migracja:

```text
Assets/DeckBattle/Scripts/Battle/UnitStatusVfxController.cs
Assets/DeckBattle/Scripts/Battle/StatusVfxView.cs
```

Assety produkcyjne:

```text
Assets/DeckBattle/Data/Vfx/_BattleVfxCatalog.asset
Assets/DeckBattle/Data/Vfx/Definitions/*.asset
Assets/DeckBattle/Data/Vfx/Profiles/Units/*.asset
Assets/DeckBattle/Data/Vfx/Profiles/Specials/*.asset
Assets/DeckBattle/Prefabs/Battle/Vfx/*.prefab
```

Scenę, prefaby i assety tworzyć lub modyfikować przez Unity MCP, aby zachować
GUID-y, poprawną serializację i natychmiast sprawdzić brakujące referencje.

## 12. Authoring workflow

Dodanie nowego efektu nie powinno wymagać zmiany kodu:

1. Utworzyć lekki prefab VFX z `PooledVfxView`.
2. Utworzyć `VfxDefinition` i ustawić lifecycle, prewarm oraz limity.
3. Dodać binding do profilu jednostki, speciala albo pocisku.
4. Wybrać cue, source/target, anchor oraz opcję `FollowAnchor`.
5. Jeśli prefab jednostki potrzebuje nowego punktu, przypisać go w
   `UnitVfxAnchors`.
6. Uruchomić walidację authoringu i podgląd w scenie testowej.

Przykład konfiguracji Crossbowmana:

```text
AttackWindup  -> CrossbowChargeVfx -> Source/Weapon -> Follow
AttackFired   -> CrossbowMuzzleVfx -> Source/Muzzle -> OneShot
Projectile    -> Crossbow_Arrow
Impact        -> ArrowImpactVfx    -> Target/Body   -> OneShot
Death         -> DefaultDeathPuff  -> Source/Root   -> OneShot
```

Przykład Fury Swipes:

```text
SpecialWindup -> FuryAuraVfx       -> Source/Feet   -> Manual
SpecialStrike -> FuryClawSlashVfx  -> Target/Body   -> OneShot per strike
Cancel/End    -> release FuryAuraVfx handle
```

## 13. Walidacja danych

Walidacja Editora powinna wykrywać:

- null prefab w używanej `VfxDefinition`;
- `MaxRetainedCount` większe niż `MaxActiveCount`;
- `PrewarmCount` większe niż limit aktywnych;
- `Manual` użyte dla cue, które nie ma ścieżki zwolnienia;
- duplikat cue w jednym profilu;
- `BattleVfxCue.None` w bindingu;
- zerową skalę, jeśli nie jest zamierzonym wyłączeniem renderowania;
- brak wymaganego anchora w prefabie jednostki;
- looping Particle System w definicji `ParticleAlive`;
- ten sam stabilny presentation ID wskazujący różne profile;
- prewarm przekraczający globalny budżet bitwy.

Błędy uniemożliwiające poprawne działanie powinny być logowane jako error w
Editorze. Opcjonalne brakujące VFX pozostają warningiem albo poprawnym no-op.

## 14. Wydajność mobilna i URP

- zero `Instantiate`/`Destroy` w typowym przebiegu walki po prewarmie;
- zero LINQ, closure, coroutine i string lookup w hot path;
- jedna centralna pętla tylko po aktywnych one-shotach;
- efekty parentowane nie wymagają ręcznego aktualizowania pozycji;
- lookup cue budowany raz i wykonywany w O(1);
- tablice `ParticleSystem` cache'owane w `Awake`;
- wspólne materiały URP, bez `renderer.material` i kopii materiału per efekt;
- parametry koloru przez `MaterialPropertyBlock`, jeśli są potrzebne;
- brak świateł realtime i realtime shadows w standardowych VFX;
- niski transparent overdraw, małe tekstury i ograniczona liczba cząsteczek;
- krótkie czasy życia oraz małe bounds particle systemów;
- brak `Collision`, `Trails` i `Noise` w Particle System bez wyraźnej potrzeby;
- limit aktywnych instancji zabezpiecza frame pacing i pamięć;
- prewarm obejmuje tylko definicje dostępne w bieżącej bitwie.

Punkty do profilowania na mid-range Android:

- `GC Alloc` podczas serii attack/special/projectile impact;
- czas `BattleVfxPool.Tick`;
- liczba aktywnych particle systemów i cząsteczek;
- transparent overdraw i draw calle;
- peak active per `VfxDefinition`;
- pool misses po rozpoczęciu walki;
- frame pacing podczas burstu wielu speciali przy 1x i 2x;
- brak wzrostu liczby GameObjectów pomiędzy rundami.

## 15. Testy

### Edit Mode — definicje i lookup

- profil zwraca poprawny binding dla cue;
- brak bindingu zwraca bezpieczne `false`;
- duplikaty cue są wykrywane;
- kolejność override to special/projectile, unit, global fallback;
- `BattlePresentationLookup` mapuje profile po właściwych ID;
- kolizja presentation ID nie wybiera losowego profilu;
- prewarm zbiera wyłącznie definicje używane w aktualnej bitwie.

### Edit Mode — pool i lifecycle

- `Play` pobiera istniejącą instancję zamiast tworzyć nową;
- zakończony one-shot wraca do właściwej puli;
- ponowne użycie resetuje parent, transform, particle, trail i Animator;
- `Duration`, `ParticleAlive` i `Manual` kończą się zgodnie z kontraktem;
- `SetCombatSpeed(2)` przyspiesza timer i Particle System;
- `Release(handle)` nie zwalnia instancji należącej do innego pokolenia
  uchwytu;
- podwójne `Release` jest bezpiecznym no-op;
- `ReleaseOwnedByUnit` kończy tylko efekty śledzące wskazaną jednostkę;
- `ReleaseAll` czyści aktywne instancje i nie pozostawia uchwytów;
- `MaxActiveCount` zapobiega dalszemu wzrostowi;
- przekroczenie limitu nie usuwa losowego aktywnego efektu.

`VfxHandle` powinien zawierać indeks/ID instancji i generation counter. Dzięki
temu spóźnione anulowanie starego windupu nie zwolni efektu, który został już
ponownie użyty dla innej akcji.

### Edit Mode — routing prezentacji

- `AttackFired` wybiera profil atakującej jednostki;
- `UnitDamaged.IsCritical` wybiera `CriticalImpact`;
- `ProjectileResolved` wybiera impact z właściwego projectile presentation ID;
- `SpecialStrikeFired.StrikeIndex` odpala dokładnie jeden efekt na strike;
- brak source/target view korzysta z fallbacku heksa;
- brak konfiguracji nie zgłasza wyjątku i nie tworzy obiektu;
- anulowanie sekwencji zwalnia właściwy manualny efekt.

### Play Mode / weryfikacja sceny

- muzzle flash pojawia się na poprawnym anchorze ranged unit;
- melee slash i impact nie zamieniają miejsc source/target;
- efekt z `FollowAnchor` podąża za poruszającą się jednostką;
- world-space impact pozostaje w miejscu po śmierci lub ruchu celu;
- pocisk nadal kończy się logicznym `ProjectileResolved`;
- animation event nie wpływa na obrażenia ani tempo symulacji;
- spóźniony animation event starej sekwencji jest ignorowany;
- przy śmierci i anulowaniu nie pozostają persistent VFX;
- rebind, nowa runda i `ClearBattle` nie pozostawiają starych instancji;
- 1x/2x zachowuje wizualną synchronizację i poprawny cleanup.

Po zmianach uruchomić najpierw wąskie testy Edit Mode przez Unity MCP, potem
testy Play Mode i manualny stres test w scenie `Battle`. Nie uruchamiać Edit Mode
w batchmode.

## 16. Etapy realizacji

### Etap 1 — kontrakty i wspólny pool

- dodać `BattleVfxCue`, `VfxDefinition`, `PooledVfxView`, `VfxSpawnRequest` i
  generation-safe `VfxHandle`;
- dodać `BattleVfxPool` z prewarmem, limitami i obsługą combat speed;
- dodać testy ponownego użycia, resetu, limitów i lifecycle;
- utworzyć jeden techniczny prefab testowy.

### Etap 2 — profile, lookup i anchory

- dodać katalog globalny oraz profile unit/special;
- rozszerzyć `BattlePresentationLookup`;
- dodać `UnitVfxAnchors` i cache w `UnitView`;
- dodać walidację duplikatów, anchorów i budżetów;
- przygotować profile testowe dla jednej melee i jednej ranged unit.

### Etap 3 — integracja attack/damage

- dodać `BattleVfxPresenter`;
- przepiąć obecne attack/damage z `BattleEffectPresenter`;
- podłączyć `AttackWindupStarted`, `AttackFired`, `UnitDamaged` i `UnitDied`;
- usunąć serializowane pola dwóch globalnych prefabów po potwierdzeniu migracji;
- zachować globalne fallbacki odpowiadające dotychczasowym efektom.

### Etap 4 — pociski i speciale

- podłączyć launch/impact VFX pocisku bez zmiany jego logicznego lifecycle;
- podłączyć `SpecialWindup`, `SpecialCast` i `SpecialStrike`;
- wprowadzić manualne uchwyty dla anulowanych windupów;
- rozszerzyć relay Animation Events o prezentacyjne contact/release;
- zweryfikować multi-strike `FurySwipes` i ranged projectile.

### Etap 5 — statusy i konsolidacja

- przepiąć wewnętrzny storage instancji `UnitStatusVfxController` na wspólny
  runtime pool;
- zachować reconciliation i shadow-state bez zmian gameplayowych;
- usunąć dopiero wtedy zduplikowane stosy/aktywne listy;
- nie łączyć tego etapu z konfiguracją nowych produkcyjnych VFX statusów.

### Etap 6 — content, testy i profilowanie

- utworzyć produkcyjny katalog oraz podstawowe profile jednostek;
- przypisać anchory prefabom przez Unity MCP;
- uruchomić pełną walidację sceny i assetów;
- wykonać testy 1x/2x, cleanup oraz burst speciali;
- sprofilować CPU, GC, particle count, overdraw i pamięć na urządzeniu;
- dostroić prewarm oraz limity na podstawie zmierzonego peak usage.

## 17. Kryteria akceptacji

- dodanie zwykłego one-shot VFX do jednostki nie wymaga zmiany kodu;
- jednostka, special i pocisk mogą posiadać własne efekty oraz korzystać z
  globalnych fallbacków;
- VFX może być odpalony na source, target, heksie lub typowanym anchorze;
- efekty jednorazowe wracają automatycznie, a persistent są bezpiecznie
  zwalniane także po anulowaniu;
- pociski zachowują obecne target tracking i logiczne `ProjectileResolved`;
- symulacja i runtime combat specs nie zawierają referencji do assetów VFX;
- animation events nie wpływają na wynik walki;
- spóźnione eventy i uchwyty nie zwalniają ponownie użytych instancji;
- po prewarmie typowa walka nie wykonuje `Instantiate`/`Destroy` dla VFX;
- brak per-frame GC Alloc po rozgrzaniu systemu;
- liczba obiektów nie rośnie między rundami;
- `ClearBattle`, rebind, śmierć i anulowanie usuwają właściwe efekty;
- 1x/2x poprawnie zmienia szybkość particle, timerów i cleanupu;
- brak konfiguracji danego cue jest poprawnym no-op;
- profiler potwierdza zaakceptowany budżet CPU, particle count i overdraw na
  urządzeniu mobilnym.

## 18. Ryzyka i środki zaradcze

### Zbyt szeroki system od pierwszej wersji

Ryzyko: próba objęcia jednym API pocisków, statusów, audio, haptics i camera
shake utrudni utrzymanie.

Środek: wspólny tylko runtime pool; interpretacja eventów pozostaje w małych,
wyspecjalizowanych presenterach.

### Błędny lifecycle Particle System

Ryzyko: looping prefab nigdy nie wróci do puli.

Środek: walidacja, fallback timeout i diagnostyka w Development Build.

### Zbyt duży prewarm

Ryzyko: niepotrzebny koszt pamięci i dłuższe wejście do bitwy.

Środek: zbierać wyłącznie definicje używane w aktualnych deckach i stroić
wartości według peak usage.

### Niejawne uzależnienie od kolejności eventów

Ryzyko: niewłaściwy source, target albo special przy kilku akcjach w jednym
ticku.

Środek: opierać korelację o unit ID, projectile ID, sequence ID i stabilne
presentation ID. Nie parować eventów przez ich sąsiedztwo na liście.

### Transparent overdraw

Ryzyko: wiele lekkich logicznie efektów nadal może obniżyć frame rate przez
duże nakładające się quady.

Środek: małe bounds, krótki lifetime, limity aktywnych instancji i profilowanie
overdraw na docelowym telefonie.

## 19. Szacunek

- kontrakty, pool i testy lifecycle: 1–1,5 dnia;
- profile, lookup, anchory i walidacja: 1 dzień;
- migracja attack/damage: 0,5–1 dnia;
- integracja projectile/special/animation events: 1–1,5 dnia;
- prefabrykaty, scena, testy i profiling mobilny: 1–1,5 dnia;
- opcjonalna konsolidacja status VFX: dodatkowe 0,5–1 dnia.

Łącznie dla pierwszej wersji bez migracji statusów: około 4,5–6,5 dnia pracy.
Szacunek zakłada użycie istniejących placeholderów VFX; przygotowanie finalnego
contentu graficznego wymaga osobnego budżetu.
