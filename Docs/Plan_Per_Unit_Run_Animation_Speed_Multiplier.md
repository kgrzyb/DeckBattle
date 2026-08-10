# Plan: dodatkowy mnożnik prędkości animacji `Run` dla każdej jednostki

## 1. Cel

Dodać do każdej `UnitDefinition` osobno konfigurowalny mnożnik prędkości
odtwarzania stanu Animatora `Run`.

Zmiana ma wpływać wyłącznie na tempo klipu biegu. Nie może zmieniać:

- logicznej prędkości ani czasu ruchu po planszy;
- ticków symulacji i kolejności eventów;
- prędkości obrotu jednostki;
- animacji `Idle`, `Attack`, `Special` ani `Dead`;
- globalnego przyspieszenia prezentacji walki.

Wartość `1` zachowuje obecne zachowanie.

## 2. Stan obecny

- `UnitView.StartMove` przełącza Animator bezpośrednio do stanu
  `Base Layer.Run`.
- `UnitView.SetCombatSpeed` ustawia globalne `Animator.speed`. Ta wartość skaluje
  całą prezentację jednostki razem z przyspieszeniem walki.
- stan `Run` we wspólnym `UnitAnimatorController.controller` ma stałe
  `m_Speed = 1` i nie korzysta z parametru `Speed Multiplier`.
- `UnitDefinition` nie zawiera ustawienia tempa animacji biegu.
- część definicji współdzieli prefab widoku:
  - `Guard`, `Lancer` i `Tankbuster` używają `PF_UnitView_Guard`;
  - `Crossbowman` i `Sniper` używają `PF_UnitView_Crosbowman`.

Z tego powodu mnożnik nie powinien być polem prefabu `UnitView`. Musi należeć
do `UnitDefinition`, aby dwie definicje korzystające z tego samego prefabu mogły
mieć różne wartości.

## 3. Kontrakt prędkości

Nowe ustawienie jest dodatkowym mnożnikiem wyłącznie stanu `Run`:

```text
effectiveRunPlaybackSpeed =
    combatSpeed
    * runAnimationSpeedMultiplier
    * baseRunStateSpeed
```

Gdzie:

- `combatSpeed` to istniejące globalne przyspieszenie prezentacji walki;
- `runAnimationSpeedMultiplier` to nowa wartość z konkretnej
  `UnitDefinition`;
- `baseRunStateSpeed` pozostaje wartością stanu `Run` w controllerze, domyślnie
  `1`.

Przykłady przy `baseRunStateSpeed = 1`:

| Mnożnik jednostki | Przyspieszenie walki | Efektywne tempo `Run` |
| ---: | ---: | ---: |
| `1.0` | `1.0` | `1.0` |
| `0.8` | `1.0` | `0.8` |
| `1.25` | `1.0` | `1.25` |
| `0.8` | `2.0` | `1.6` |

Nie ustawiać `animator.speed = combatSpeed * runAnimationSpeedMultiplier`, bo
zmieniłoby to również wszystkie pozostałe stany.

## 4. Dane jednostki

W `UnitDefinition.cs` dodać pole prezentacyjne, obok `UnitPrefab` i
`VfxProfile`, na przykład:

```csharp
[Min(0.01f)] public float RunAnimationSpeedMultiplier = 1f;
```

W `OnValidate`:

- wartość niedodatnią, `NaN` lub nieskończoną przywracać do `1f`;
- nie wprowadzać arbitralnego górnego limitu w runtime;
- pozostawić `1f` jako bezpieczny default dla istniejących assetów.

Pole jest tuningiem prezentacji i nie powinno trafiać do:

- `UnitCombatSpec`;
- `UnitRuntimeState` ani `RuntimeUnit`;
- `BattleEvent`;
- logiki wyznaczania trasy lub czasu ruchu.

Podczas implementacji dopisać pole jawnie do wszystkich dziewięciu obecnych
assetów w `Assets/DeckBattle/Data/Units`, początkowo z wartością `1`, a docelowe
wartości ustawić według tuningu contentu:

- `Arisa`;
- `Brute`;
- `Crossbowman`;
- `Guard`;
- `Kitsuro`;
- `Lancer`;
- `Prawler`;
- `Sniper`;
- `Tankbuster`.

Jawny zapis wartości w assetach ograniczy ryzyko różnego zachowania po
reserializacji lub zmianie domyślnej wartości w przyszłości.

## 5. Przeniesienie konfiguracji do widoku

`BattlePresentationLookup` jest właściwym miejscem przekazania ustawienia,
ponieważ jest budowany raz z `UnitDefinition`, działa wyłącznie po stronie
prezentacji i już mapuje `PresentationId` na prefab jednostki.

Zastąpić lub rozszerzyć mapowanie prefabu małą niemutowalną strukturą, np.
`UnitViewPresentationData`, zawierającą:

```text
Prefab
RunAnimationSpeedMultiplier
```

Wymagania dla lookupu:

- kluczem nadal pozostaje stabilny `PresentationId` konkretnej definicji;
- mnożnik znormalizować do `1f`, jeśli dane są niepoprawne;
- wykrywać konflikt, gdy ten sam `PresentationId` wskazuje różny prefab lub
  różny mnożnik;
- zachować obecną diagnostykę brakującego prefabu;
- nie dodawać mnożnika do `UnitPresentationState`, ponieważ wymuszałoby to
  przenoszenie tuningu wizualnego przez snapshot symulacji.

`UnitViewRegistry.GetOrCreate` po utworzeniu widoku powinien:

1. ustawić mnożnik `Run` pobrany z lookupu;
2. ustawić istniejące `combatSpeed`;
3. zarejestrować gotowy widok.

Konfiguracja jest stała w czasie pojedynczej bitwy. Nie trzeba jej aktualizować
co klatkę ani przy każdym kroku ruchu.

## 6. `UnitView`

Dodać hash parametru tworzony jeden raz:

```csharp
private static readonly int RunSpeedParameter =
    Animator.StringToHash("runSpeed");
```

`UnitView` powinien przechowywać bezpieczną bieżącą wartość i wystawić metodę
konfiguracyjną, np.:

```csharp
public void SetRunAnimationSpeedMultiplier(float multiplier);
```

Metoda:

- normalizuje wartość niedodatnią, `NaN` lub nieskończoną do `1f`;
- zapisuje ją w polu widoku;
- jeżeli Animator istnieje, wykonuje pojedyncze
  `animator.SetFloat(RunSpeedParameter, safeMultiplier)`.

`ResetAnimator` po `animator.Rebind()` musi ponownie ustawić zarówno istniejący
`attackSpeed`, jak i nowy `runSpeed`. `Rebind` może przywrócić domyślne wartości
parametrów, więc samo ustawienie przy instancjonowaniu nie wystarczy na ścieżce
ponownego bindowania widoku.

Nie zmieniać `Update`, `StartMove`, interpolacji pozycji ani systemu kolejkowania
kroków. `StartMove` nadal tylko przełącza do `Run`, a Animator korzysta z
wcześniej ustawionego parametru.

## 7. Animator Controller i override controllery

W `UnitAnimatorController.controller`:

1. dodać parametr `Float` o nazwie `runSpeed` z wartością domyślną `1`;
2. w stanie `Run` włączyć `Speed Multiplier`;
3. przypisać `runSpeed` jako parametr mnożnika;
4. pozostawić bazową prędkość stanu `Run` równą `1`, chyba że istnieje
   udokumentowany wspólny tuning;
5. nie przypisywać `runSpeed` do `Idle`, `Attack`, `Special` ani `Dead`;
6. pozostawić root motion wyłączony.

`Arisa.overrideController`, `Kitsuro.overrideController` i
`Prowler_Cat.overrideController` dziedziczą wspólny controller, więc nie
potrzebują osobnych parametrów. Trzeba jednak sprawdzić w Unity Editorze, że po
zmianie controller bazowy i wszystkie override controllery widzą `runSpeed` i
że każdy podmieniony klip `Run` reaguje na ten parametr.

Dla wszystkich sześciu prefabów `PF_UnitView_*` zweryfikować, czy przypisany
Animator korzysta z controllera zachowującego kontrakt stanów i parametrów.
Prefab bez właściwego controllera powinien zostać wykryty podczas walidacji
assetów, a nie przez powtarzające się logi w runtime.

## 8. Testy

### 8.1. Edit Mode

Rozszerzyć `UnitPrefabSourceTests` lub dodać osobny zestaw testów prezentacji:

1. nowa definicja używa domyślnego mnożnika `1`;
2. lookup zwraca prefab i mnożnik zapisany w `UnitDefinition`;
3. dwie definicje z różnymi `UnitId`, wspólnym prefabem i różnymi mnożnikami
   zachowują własne wartości;
4. wartość `0`, ujemna, `NaN` i nieskończona daje bezpieczne `1`;
5. `UnitViewRegistry.GetOrCreate` aplikuje mnożnik do nowej instancji;
6. ponowny `GetOrCreate` nie tworzy duplikatu i nie gubi konfiguracji;
7. globalne `SetCombatSpeed` nie nadpisuje mnożnika jednostki.

Dodać test kontraktu assetu z użyciem API edytorowego Animatora:

- wspólny controller ma parametr `runSpeed` typu `Float` i default `1`;
- stan `Base Layer.Run` używa `runSpeed` jako `Speed Multiplier`;
- pozostałe stany nie używają tego parametru;
- override controllery wskazują wspólny controller;
- wszystkie używane prefaby mają Animator i kompatybilny controller.

Test kontraktu assetów zapobiega cichemu zepsuciu funkcji po podmianie
controllera lub dodaniu nowego prefabu jednostki.

### 8.2. Play Mode smoke test

Przez Unity MCP uruchomić scenę `Battle` i sprawdzić:

1. jednostka z wartością `1` wygląda identycznie jak przed zmianą;
2. wartości poniżej i powyżej `1` odpowiednio zwalniają i przyspieszają tylko
   klip `Run`;
3. dwie jednostki współdzielące prefab, ale mające różne mnożniki, biegną z
   różnym tempem animacji;
4. obie docierają do heksa w tym samym logicznie wyznaczonym czasie;
5. przy przyspieszeniu walki `2x` globalne tempo mnoży indywidualny parametr;
6. po przejściu do `Idle`, `Attack`, `Special` i `Dead` indywidualny mnożnik nie
   zmienia tych animacji;
7. reset rundy lub ponowny bind nie przywraca niepoprawnie wartości `1` i nie
   dziedziczy wartości innej jednostki;
8. brak błędów Animatora i brak zmian w root motion.

## 9. Wydajność mobilna

Koszt runtime powinien ograniczyć się do:

- jednego `float` w danych prezentacyjnych na definicję;
- jednego pola `float` w instancji `UnitView`;
- jednego `Animator.SetFloat` przy konfiguracji lub rebindzie widoku.

Nie dodawać:

- odczytu `AnimatorStateInfo` w `Update`;
- ustawiania parametru przy każdym ticku lub kroku ruchu;
- coroutine, tweenów, LINQ ani alokacji;
- osobnych controllerów tworzonych w runtime;
- zmian w URP, shaderach, materiałach albo teksturach.

Po rozgrzaniu rozpoczęcie ruchu powinno nadal mieć `0 B GC.Alloc`, a koszt
Animatora powinien pozostać praktycznie niezmieniony.

## 10. Kolejność implementacji

### Etap 1 — dane i lookup prezentacji

- dodać i walidować `RunAnimationSpeedMultiplier` w `UnitDefinition`;
- rozszerzyć dane w `BattlePresentationLookup`;
- zachować konfigurację poza `UnitCombatSpec` i snapshotem symulacji;
- dodać test dwóch definicji współdzielących prefab.

### Etap 2 — widok i controller

- dodać `runSpeed` i metodę konfiguracyjną do `UnitView`;
- aplikować parametr w `UnitViewRegistry`;
- ponownie aplikować parametr po `Animator.Rebind`;
- dodać `runSpeed` jako `Speed Multiplier` wyłącznie stanu `Run`.

### Etap 3 — assety i walidacja

- zapisać domyślną wartość `1` we wszystkich obecnych `UnitDefinition`;
- ustawić docelowe wartości tuningowe;
- zweryfikować wspólny controller, override controllery i sześć prefabów;
- dodać test kontraktu assetów.

### Etap 4 — weryfikacja

- uruchomić najwęższe testy Edit Mode przez Unity MCP;
- wykonać Play Mode smoke test dla normalnego i przyspieszonego combat speed;
- porównać jednostki współdzielące prefab;
- sprawdzić Profiler pod kątem GC i kosztu Animatora.

## 11. Definition of Done

- każda `UnitDefinition` ma niezależny mnożnik `Run`;
- wartość `1` zachowuje obecne tempo animacji;
- współdzielenie prefabu nie wymusza wspólnej wartości;
- mnożnik wpływa wyłącznie na stan `Run`;
- czas i trasa ruchu w symulacji pozostają niezmienione;
- mnożnik poprawnie składa się z globalnym `combatSpeed`;
- `Animator.Rebind` i ponowny bind zachowują właściwą wartość;
- niepoprawne dane bezpiecznie wracają do `1`;
- wszystkie używane controllery i prefaby spełniają kontrakt `runSpeed`;
- brak nowej pracy per-frame i nowych alokacji;
- testy Edit Mode oraz Play Mode smoke test przechodzą.
