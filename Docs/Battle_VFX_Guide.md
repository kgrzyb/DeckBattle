# Battle VFX — instrukcja dodawania i modyfikacji

Ten dokument opisuje aktualny system efektów bitewnych. Efekty są wyłącznie warstwą prezentacji: nie zmieniają symulacji walki, obrażeń ani czasu zdarzeń. Wszystkie często uruchamiane instancje przechodzą przez wspólny `BattleVfxPool`.

## Najkrótsza ścieżka

1. Utwórz lub skopiuj prefab VFX w `Assets/DeckBattle/Prefabs/Battle/Vfx`.
2. Dodaj do roota prefabu komponent `PooledVfxView`.
3. Utwórz asset `VfxDefinition` w `Assets/DeckBattle/Data/Vfx` i przypisz prefab.
4. Podepnij definicję w odpowiednim miejscu:
   - `BattleVfxProfile` — atak, obrażenia, śmierć i special jednostki;
   - `ProjectileDefinition` — start i trafienie pocisku;
   - `StatusPresentationCatalog` — aktywny VFX statusu.
5. Uruchom w Unity: `Deck Battle > Validation > Validate Battle VFX`.
6. Przetestuj sytuację w bitwie, szczególnie wielokrotne trafienia i śmierć jednostki w trakcie efektu.

Nie dodawaj kodu ani osobnego managera dla standardowego efektu.

## Gdzie znajdują się dane

| Element | Lokalizacja | Zastosowanie |
| --- | --- | --- |
| Prefaby efektów | `Assets/DeckBattle/Prefabs/Battle/Vfx` | Cząsteczki, trail i animator efektu. |
| Definicje efektów | `Assets/DeckBattle/Data/Vfx` | Limity, lifetime, prewarm i prefab. |
| Domyślny profil bitwy | `Assets/DeckBattle/Data/Vfx/BattleVfxProfile_Default.asset` | Fallback dla jednostek bez własnego profilu. |
| Profil jednostki | Pole `Vfx Profile` na `UnitDefinition` | Nadpisuje default dla zwykłych zdarzeń tej jednostki. |
| Profil speciala | Pole `Vfx Profile` na `UnitSpecialDefinition` | Nadpisuje profil jednostki dla cue speciala. |
| VFX pocisku | `Launch Vfx`, `Impact Vfx` na `ProjectileDefinition` | Efekt wystrzału i trafienia danego pocisku. |
| Aktywny VFX statusu | `Active Vfx Definition` w `_StatusPresentationCatalog` | Efekt utrzymywany tak długo, jak status. |

## 1. Przygotowanie prefabu

Prefaby VFX powinny być małe, samowystarczalne i nie zawierać logiki gameplayowej.

1. Skopiuj najbliższy istniejący prefab, np. `PF_Vfx_AttackBurst` lub `PF_Vfx_DamageBurst`.
2. Umieść `ParticleSystem`, `TrailRenderer` lub `Animator` na rootcie albo w dzieciach prefabu.
3. Dodaj na rootcie `PooledVfxView`.
4. Nie dodawaj własnego `Update`, `Destroy`, coroutine ani skryptu, który sam wyłącza obiekt. Pool odtwarza, zatrzymuje i czyści efekt przy każdym użyciu.
5. Dla efektów jednorazowych wyłącz zapętlenie (`Loop`) na Particle Systemach.

`PooledVfxView` automatycznie zbiera referencje do komponentów w dzieciach, jeśli pola `Particle Systems`, `Trail Renderers` i `Animators` są puste. Wypełnienie ich ręcznie jest opcjonalne, ale warto je ustawić, gdy prefab ma nietypową hierarchię.

### Zasady mobilne

- Preferuj jeden prosty Particle System zamiast kilku nakładających się warstw.
- Unikaj świateł real-time, kolizji cząstek, sub-emitterów i shaderów z kosztowną przezroczystością.
- Ogranicz liczbę dużych transparentnych quadów — to główne źródło overdraw na telefonie.
- Nie używaj nieograniczonego efektu cząsteczkowego. Długotrwały efekt powinien być kontrolowany przez `Manual` i mieć mały limit aktywnych instancji.

## 2. Konfiguracja `VfxDefinition`

Utwórz asset przez `Create > Deck Battle > VFX Definition`, nazwij go np. `Vfx_FireImpact` i ustaw:

| Pole | Znaczenie | Zalecenie |
| --- | --- | --- |
| `Prefab` | Prefab z komponentem `PooledVfxView`. | Wymagane. |
| `Lifetime Mode` | Sposób powrotu instancji do poola. | Wybierz zgodnie z tabelą poniżej. |
| `Fallback Lifetime` | Maksymalny czas życia w sekundach. | Ustaw rzeczywistą długość efektu z małym zapasem. |
| `Prewarm Count` | Liczba instancji utworzonych przed walką. | Zwykle 2–8, zależnie od spodziewanej równoległości. |
| `Max Active Count` | Twardy limit jednocześnie aktywnych instancji tego efektu. | Ustaw mały, świadomy limit. Nadmiarowe spawny są pomijane. |
| `Max Retained Count` | Ile zwróconych instancji może zostać w pamięci. | Zwykle równe lub mniejsze od `Max Active Count`. |
| `Scale With Combat Speed` | Czy cząsteczki i animator efektu przyspieszają wraz z bitwą. | Włącz dla reakcji bojowych; wyłącz tylko dla efektu, który ma pozostać w czasie rzeczywistym. |

### Tryby życia

| Tryb | Kiedy używać | Ważne ograniczenie |
| --- | --- | --- |
| `Duration` | Krótki burst o znanej długości. | Pool zwróci efekt po `Fallback Lifetime`, nawet jeśli cząsteczki nadal są widoczne. |
| `ParticleSystemAlive` | Jednorazowy efekt, którego czas zależy od cząstek. | Prefab musi mieć Particle System i żaden z nich nie może być zapętlony. `Fallback Lifetime` nadal zabezpiecza przed błędną konfiguracją. |
| `Manual` | Windup ataku, windup speciala oraz aktywny VFX statusu. | W profilach bitewnych dozwolony wyłącznie dla `AttackWindup` i `SpecialWindup`. Nie używaj go dla zwykłego impactu. |

Pool ma globalny limit prewarmu `64` instancji na komponencie `BattleVfxPool` w scenie `Battle`. Nie podnoś go bez pomiaru na urządzeniu. Diagnostyka poola udostępnia `PoolMissCount`, `SkippedSpawnCount` i `PeakActiveCount` — wartości te warto sprawdzić po gęstej walce.

## 3. Dodanie efektu do ataku, obrażeń lub śmierci

Utwórz `BattleVfxProfile` przez `Create > Deck Battle > Battle VFX Profile`, a następnie dodaj wpis w `Bindings`. Jeden cue może wystąpić w profilu tylko raz.

| Cue | Kiedy jest uruchamiany | Typowe ustawienie |
| --- | --- | --- |
| `AttackWindup` | Początek przygotowania zwykłego ataku. | `Manual`, `Source`, anchor `Weapon` lub `Muzzle`, `Follow Anchor` włączone. |
| `AttackFired` | Moment wykonania zwykłego ataku. | Burst przy `Source`, zwykle anchor `Weapon`/`Muzzle`. |
| `Damaged` | Jednostka otrzymuje niekrytyczne obrażenia. | Krótki burst przy `Target`, anchor `Body`. |
| `CriticalImpact` | Jednostka otrzymuje obrażenia krytyczne. | Mocniejszy wariant `Damaged` przy `Target`. |
| `Death` | Jednostka ginie. | Burst przy `Target`, `Body` lub `Feet`, zwykle bez `Follow Anchor`. |
| `SpecialWindup` | Początek przygotowania speciala. | `Manual`, `Source`, anchor `Special` lub `Body`, `Follow Anchor` włączone. |
| `SpecialCast` | Rozpoczęcie rzucenia speciala. | Jednorazowy burst przy źródle lub celu. |
| `SpecialStrike` | Każdy cios speciala. | Krótki impact przy celu albo efekt przy broni źródła. |

`AttackImpact` jest zarezerwowany w enumie, ale obecnie nie jest emitowany przez `BattleVfxPresenter`. Nie konfiguruj go, jeśli oczekujesz widocznego efektu.

### Pola wpisu `BattleVfxBinding`

| Pole | Działanie |
| --- | --- |
| `Effect` | Wybrany `VfxDefinition`. |
| `Subject` | Miejsce bazowe: `Source`, `Target`, `SourceHex`, `TargetHex` lub `World`. Dla `World` aktualny routing używa pozycji docelowej heksy. |
| `Anchor` | Punkt na jednostce: `Root`, `Body`, `Feet`, `Weapon`, `Muzzle`, `Head`, `Special`. |
| `Follow Anchor` | Efekt staje się dzieckiem anchora i porusza się z jednostką. Jest automatycznie zwalniany przy śmierci jednostki. |
| `Face Target` | Obraca efekt w poziomie w kierunku celu. Przydatne dla smugi ciosu i muzzle flasha kierunkowego. |
| `Local Position`, `Local Euler Angles`, `Local Scale` | Offset, obrót i skala względem anchora albo pozycji świata. Wartość skali `(0, 0, 0)` oznacza `(1, 1, 1)`. |

### Który profil zostanie użyty

Dla zwykłych cue system szuka kolejno: profil jednostki (`UnitDefinition > Vfx Profile`), a potem `BattleVfxProfile_Default`.

Dla `SpecialWindup`, `SpecialCast` i `SpecialStrike` kolejność jest następująca: profil speciala (`UnitSpecialDefinition > Vfx Profile`), profil jednostki, profil domyślny.

Dla `Damaged`, `CriticalImpact` i `Death` profil jest wybierany na podstawie jednostki będącej celem, a nie atakującego.

## 4. Pociski

Efekty pocisku nie wymagają `BattleVfxProfile`.

1. Otwórz właściwy `ProjectileDefinition`, np. `Arrow.asset`.
2. Podepnij `Launch Vfx` — efekt uruchamiany przy `ProjectileLaunched`, przy źródle.
3. Podepnij `Impact Vfx` — efekt uruchamiany przy `ProjectileResolved`, przy ciele celu.

Impact jest odtwarzany tylko, gdy pocisk faktycznie zadał obrażenia. Efekt trafienia jest zapamiętywany dla konkretnego `ProjectileId`, dlatego kilka równoległych pocisków może używać różnych definicji bez mieszania efektów.

## 5. Anchory jednostki

Na prefabie `UnitView` dodaj lub skonfiguruj komponent `UnitVfxAnchors`.

1. Utwórz puste dzieci prefabu we właściwych miejscach modelu, np. `Vfx_Weapon`, `Vfx_Muzzle`, `Vfx_Head`.
2. Podepnij ich transformy do pól `Body`, `Feet`, `Weapon`, `Muzzle`, `Head` i `Special` komponentu `UnitVfxAnchors`.
3. W profilu wybierz odpowiednią wartość `Anchor`.

Brak przypisanego anchora nie blokuje efektu: system użyje roota jednostki. To bezpieczny fallback, lecz precyzja położenia efektu będzie mniejsza.

## 6. Speciale

Aby zmodyfikować efekt istniejącego speciala, otwórz jego `UnitSpecialDefinition` i zmień pole `Vfx Profile`. Profile `BattleVfxProfile_HasteBurst` i `BattleVfxProfile_FurySwipes` są przykładami osobnych konfiguracji speciali.

Jeżeli special ma kilka ciosów, `SpecialStrike` uruchamia się dla każdego zdarzenia ciosu. Ustaw niewielki `Max Active Count`, aby wiele trafień w tym samym czasie nie zwiększało niekontrolowanie kosztu renderowania.

## 7. Statusy

`_StatusPresentationCatalog` korzysta wyłącznie ze wspólnego `BattleVfxPool` i trzech definicji:

- `Apply Vfx Definition` — jednorazowy efekt nałożenia lub zwiększenia stacków; użyj `Duration` albo `ParticleSystemAlive`.
- `Active Vfx Definition` — efekt utrzymywany tak długo, jak status; użyj `Manual`.
- `Remove Vfx Definition` — jednorazowy efekt zdjęcia lub zmniejszenia stacków; użyj `Duration` albo `ParticleSystemAlive`.

Każdy z tych efektów używa pozycji, obrotu i skali z tego samego wpisu statusu. Wszystkie definicje są prewarmowane przez wspólny pool. Aktywny VFX jest zwalniany po usunięciu statusu, rebindzie bitwy albo śmierci jednostki. Jednorazowe efekty wracają do poola zgodnie ze swoim lifetime.

## 8. Animation Events

`UnitAnimationEventRelay` udostępnia metody dla klipów animacji:

- `AttackContact()`
- `ProjectileRelease()`
- `SpecialContact()`

Dodaj Animation Event do klipu w wybranej klatce kontaktu i wpisz dokładnie nazwę metody. Metoda `Attack()` jest zachowana wyłącznie dla zgodności ze starszymi klipami — nie używaj jej w nowych klipach.

Te metody emitują typowany sygnał prezentacyjny (`UnitAnimationVfxSignal`). Standardowe VFX walki są obecnie uruchamiane przez zdarzenia symulacji (`AttackFired`, `ProjectileLaunched`, `SpecialStrikeFired` itd.), więc samo dodanie Animation Event nie tworzy dodatkowego VFX. Sygnały są przeznaczone dla przyszłego routingu efektu zsynchronizowanego dokładnie z klatką animacji.

## Kontrola przed oddaniem efektu

- Prefab ma `PooledVfxView` i nie niszczy się sam.
- `Prewarm Count <= Max Active Count` oraz `Max Retained Count <= Max Active Count`.
- `ParticleSystemAlive` nie ma zapętlonych cząstek.
- Profil nie zawiera dwóch wpisów z tym samym cue.
- `Manual` jest użyty tylko dla `AttackWindup`, `SpecialWindup` lub aktywnego efektu statusu.
- Efekt jest sprawdzony przy przyspieszonej walce, kilku trafieniach naraz i śmierci jednostki.
- Walidator `Deck Battle > Validation > Validate Battle VFX` kończy się bez błędów.

## Gdy efekt nie jest widoczny

1. Sprawdź, czy `VfxDefinition` ma przypisany prefab i czy prefab ma `PooledVfxView`.
2. Sprawdź, czy wybrany profil jest podpięty do `UnitDefinition` albo `UnitSpecialDefinition`.
3. Sprawdź priorytet profili — profil speciala może nadpisywać profil jednostki dla cue speciala.
4. Sprawdź `Max Active Count`; pool celowo pomija spawn po osiągnięciu limitu.
5. Sprawdź anchor na prefabie jednostki i offset w bindingu.
6. Uruchom walidator oraz zajrzyj do Console.

W razie potrzeby kod routingowy znajduje się w `Assets/DeckBattle/Scripts/Battle/BattleVfxPresenter.cs`, a ownership i limity instancji w `Assets/DeckBattle/Scripts/Battle/BattleVfxPool.cs`.
